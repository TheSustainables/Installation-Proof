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
using System.Text.RegularExpressions;
using System.Text;
using Newtonsoft.Json;

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
                string blobPath = $"installation-proof/";

                foreach (var part in content.Contents)
                {
                    var fieldName = part.Headers.ContentDisposition.Name.Trim('"');

                    if (fieldName == "pretty")
                    {
                        rawRequestValue = await part.ReadAsStringAsync();
                        
                        var extractedData = ExtractProof(rawRequestValue);

                        log.LogInformation("Factuurnummer: " + extractedData.Factuurnummer);
                        log.LogInformation("Klant: " + extractedData.Klant);
                        log.LogInformation("Adres: " + extractedData.Adres);
                        log.LogInformation("Factuur uploaden: " + extractedData.FactuurUploaden);
                        log.LogInformation("Offerte uploaden: " + extractedData.OfferteUploaden);

                        blobPath += extractedData.Factuurnummer;

                        await UploadFileToBlob(extractedData, blobPath, log);
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
            var storageAccount = CloudStorageAccount.Parse("key_here");
            var blobClient = storageAccount.CreateCloudBlobClient();
            return blobClient.GetContainerReference("installationproofcontainertest");
        }

        static Proof ExtractProof(string input)
        {
            Proof result = new Proof();

            Regex factuurnummerRegex = new Regex(@"Factuurnummer:(\w+)");
            Regex klantRegex = new Regex(@"Klant\s*:(.+?)\s*,");
            Regex adresRegex = new Regex(@"Adres\s*:(.+?)\s*,");
            Regex factuurUploadenRegex = new Regex(@"Factuur uploaden\s*:(.+?)\s*,");
            Regex offerteUploadenRegex = new Regex(@"Offerte uploaden\s*:(.+)$");

            Match factuurnummerMatch = factuurnummerRegex.Match(input);
            Match klantMatch = klantRegex.Match(input);
            Match adresMatch = adresRegex.Match(input);
            Match factuurUploadenMatch = factuurUploadenRegex.Match(input);
            Match offerteUploadenMatch = offerteUploadenRegex.Match(input);

            result.Factuurnummer = factuurnummerMatch.Success ? factuurnummerMatch.Groups[1].Value.Trim() : "";
            result.Klant = klantMatch.Success ? klantMatch.Groups[1].Value.Trim() : "";
            result.Adres = adresMatch.Success ? adresMatch.Groups[1].Value.Trim() : "";
            result.FactuurUploaden = factuurUploadenMatch.Success ? factuurUploadenMatch.Groups[1].Value.Trim() : "";
            result.OfferteUploaden = offerteUploadenMatch.Success ? offerteUploadenMatch.Groups[1].Value.Trim() : "";

            return result;
        }

        private static async Task UploadFileToBlob(Proof proof, string blobPath, ILogger log)
        {
            var container = GetBlobContainer();
            await container.CreateIfNotExistsAsync();

            var guidValue = Guid.NewGuid().ToString();

            var blobDirectoryPath = $"{blobPath}/{guidValue}";

            var blob = container.GetBlockBlobReference(blobDirectoryPath);

            string proofJson = JsonConvert.SerializeObject(proof);

            byte[] fileContentBytes = Encoding.UTF8.GetBytes(proofJson);

            using (var stream = new MemoryStream(fileContentBytes))
            {
                await blob.UploadFromStreamAsync(stream);
                log.LogInformation("Data stored in Azure Storage successfully.");
            }
        }
    }
}
