using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace GlobalSTE
{
    public class UploadDocumentFn
    {
        private readonly ILogger<UploadDocumentFn> _logger;

        public UploadDocumentFn(ILogger<UploadDocumentFn> logger)
        {
            _logger = logger;
        }

        [Function("UploadDocument")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req)
        {
            try
            {
               
               // string connection = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
                string keyVaultUrl = Environment.GetEnvironmentVariable("KeyVaultUrl");
                string keyName = Environment.GetEnvironmentVariable("KeyName");

                string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
                string containerName = Environment.GetEnvironmentVariable("ContainerName");

                
                var file = req.Form.Files["File"];
                if (file == null)
                {
                    return new BadRequestObjectResult("No file uploaded.");
                }

                Stream fileStream = file.OpenReadStream();

                // genrating aes key , iv ( storing both in blob meta )

                byte[] aesKey;
                byte[] encryptedFileBytes;
                byte[] iv;

                using (Aes aes = Aes.Create())
                {
                    aes.GenerateKey();
                    aesKey = aes.Key;
                    iv = aes.IV; 

                    // encrypt using AES
                    using (var memoryStream = new MemoryStream())
                    using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        await fileStream.CopyToAsync(cryptoStream);
                        cryptoStream.FlushFinalBlock();
                        encryptedFileBytes = memoryStream.ToArray();
                    }
                }
                //Bhavesh Singh
                // AES key encrypt using rsa key
                var keyClient = new KeyClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
                KeyVaultKey rsaKey = await keyClient.GetKeyAsync(keyName);
                var cryptoClient = new CryptographyClient(rsaKey.Id, new DefaultAzureCredential());

                EncryptResult encryptedAesKey = await cryptoClient.EncryptAsync(EncryptionAlgorithm.RsaOaep, aesKey);

                // upload file to store
                BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
                var blobClient = blobServiceClient.GetBlobContainerClient(containerName);

                
                var blob = blobClient.GetBlobClient(file.FileName);
                using (var encryptedFileStream = new MemoryStream(encryptedFileBytes))
                {
                    await blob.UploadAsync(encryptedFileStream, overwrite: true);
                }

                // upload encrypted key in meta data
                var metadata = new Dictionary<string, string>
                {
                    { "iv", Convert.ToBase64String(iv) },  // Store IV as Base64 string
                    { "encryptedKey", Convert.ToBase64String(encryptedAesKey.Ciphertext) }  // Store encrypted AES key as Base64 string
                };

                // Apply metadata to the uploaded blob
                await blob.SetMetadataAsync(metadata);

                return new OkObjectResult("File uploaded, encrypted, and metadata stored successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file upload and encryption");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
