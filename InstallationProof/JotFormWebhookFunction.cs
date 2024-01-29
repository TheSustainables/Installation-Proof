using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using InstallationProof.JotFormWebhook.Entities;

namespace InstallationProof.JotFormWebhook
{
    public static class JotFormWebhookFunction
    {
        [FunctionName("JotFormWebhookFunction")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req,
            [Blob("installation-proof/{rand-guid}", FileAccess.Write)] CloudBlobStream outputBlob,
            [Table("installationprooftabletest")] CloudTable requestInfoTable,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            try
            {
                var content = await req.Content.ReadAsMultipartAsync();

                if (content == null)
                {
                    log.LogError("Invalid content type. Expected multipart/form-data.");
                    return req.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid content type. Expected multipart/form-data.");
                }

                var rawRequestValue = "";
                string guidValue = Guid.NewGuid().ToString();
                string blobPath = $"installation-proof/{guidValue}/";

                foreach (var part in content.Contents)
                {
                    var fieldName = part.Headers.ContentDisposition.Name.Trim('"');

                    if (fieldName == "rawRequest")
                    {
                        rawRequestValue = await part.ReadAsStringAsync();
                        var container = GetBlobContainer();
                        await container.CreateIfNotExistsAsync();

                        var blob = container.GetBlockBlobReference(blobPath);

                        using (var stream = await part.ReadAsStreamAsync())
                        {
                            await blob.UploadFromStreamAsync(stream);
                            log.LogInformation("Data stored in Azure Storage successfully.");
                        }
                    }
                }

                await SaveRequestInfoToTable(requestInfoTable, guidValue, rawRequestValue);

                return req.CreateResponse(HttpStatusCode.OK, "Data received and stored successfully");
            }
            catch (Exception ex)
            {
                log.LogInformation($"Error processing webhook request: {ex.Message}");
                return req.CreateErrorResponse(HttpStatusCode.InternalServerError, "Error processing request");
            }
        }

        private static async Task SaveRequestInfoToTable(CloudTable table, string guid, string rawRequest)
        {
            var entity = new RequestInfoEntity(guid, rawRequest);
            var operation = TableOperation.InsertOrReplace(entity);
            await table.ExecuteAsync(operation);
        }
        private static CloudBlobContainer GetBlobContainer()
        {
            var storageAccount = CloudStorageAccount.Parse("installation-proof/{rand-guid}");
            var blobClient = storageAccount.CreateCloudBlobClient();
            return blobClient.GetContainerReference("installationproofcontainertest");
        }
    }
}
