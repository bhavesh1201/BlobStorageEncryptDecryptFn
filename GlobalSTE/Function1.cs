using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using System.Text.Json;

namespace GlobalSTE
{
    public class Function1
    {


        private readonly ILogger<Function1> _logger;

        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
        }
        [Function("Run")]
        public async Task<IActionResult> Run(
       [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
       ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

           
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<RequestData>(requestBody);

           
            if (data == null || string.IsNullOrEmpty(data.FileName))
            {
                return new BadRequestObjectResult("Please provide a valid file name in the request body.");
            }

            string fileName = data.FileName;

           
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string containerName = Environment.GetEnvironmentVariable("ContainerName");

            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(containerName))
            {
                return new BadRequestObjectResult("Invalid configuration for connection string or container name.");
            }

            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            BlobClient blobClient = containerClient.GetBlobClient(fileName);

            if (await blobClient.ExistsAsync())
            {
                var blobDownloadInfo = await blobClient.DownloadAsync();
                using (var memoryStream = new MemoryStream())
                {
                    await blobDownloadInfo.Value.Content.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    return new FileStreamResult(memoryStream, "application/octet-stream")
                    {
                        FileDownloadName = fileName
                    };
                }
            }
            else
            {
                return new NotFoundObjectResult($"File '{fileName}' not found in container '{containerName}'.");
            }
        }
    }

}

public class RequestData
{
    public string FileName { get; set; }
}