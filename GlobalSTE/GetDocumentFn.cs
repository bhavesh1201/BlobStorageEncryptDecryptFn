using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace GlobalSTE
{
    public class GetDocumentFn
    {
        private readonly ILogger<GetDocumentFn> _logger;

        public GetDocumentFn(ILogger<GetDocumentFn> logger)
        {
            _logger = logger;
        }

        [Function("GetDocument")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string containerName = Environment.GetEnvironmentVariable("ContainerName");
            string keyVaultUrl = Environment.GetEnvironmentVariable("KeyVaultUrl");
            string keyName = Environment.GetEnvironmentVariable("KeyName");

            string blobName = req.Query["blobName"];
            if (string.IsNullOrEmpty(blobName))
            {
                return new BadRequestObjectResult("Please pass a valid blob name on the query string.");
            }

            try
            {
                
                BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);


                //filedownload
                
                BlobClient blobClient = containerClient.GetBlobClient(blobName); 
                BlobDownloadInfo download = await blobClient.DownloadAsync();

               //meta data 
                BlobProperties properties = await blobClient.GetPropertiesAsync();
                if (!properties.Metadata.TryGetValue("iv", out string ivBase64) ||
                    !properties.Metadata.TryGetValue("encryptedKey", out string encryptedKeyBase64))
                {
                    return new BadRequestObjectResult("Metadata for IV or encrypted key not found.");
                }

               //converting from base64 cause store ke time base64 main convert kia tha
                byte[] iv = Convert.FromBase64String(ivBase64);
                byte[] encryptedAesKey = Convert.FromBase64String(encryptedKeyBase64);

                //RSA KEY from vault
              
                var keyClient = new KeyClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
                KeyVaultKey rsaKey = await keyClient.GetKeyAsync(keyName);
                var cryptoClient = new CryptographyClient(rsaKey.Id, new DefaultAzureCredential());

                // Decrypt AES key using RSA Key
                DecryptResult decryptedAesKeyResult = await cryptoClient.DecryptAsync(EncryptionAlgorithm.RsaOaep, encryptedAesKey);
                byte[] aesKey = decryptedAesKeyResult.Plaintext;

                //Bhavesh Singh
                // Decrypt the file using AES
                using var aes = Aes.Create();
                aes.Key = aesKey;
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                var decryptedFileStream = new MemoryStream();

                // Read and decrypt the file
                await using (var cryptoStream = new CryptoStream(download.Content, decryptor, CryptoStreamMode.Read))
                {
                    await cryptoStream.CopyToAsync(decryptedFileStream);
                }

                decryptedFileStream.Position = 0; 

                // Return the decrypted file
                return new FileStreamResult(decryptedFileStream, "application/octet-stream")
                {
                    FileDownloadName = blobName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving or decrypting blob: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
