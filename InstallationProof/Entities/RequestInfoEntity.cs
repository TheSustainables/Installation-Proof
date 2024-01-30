using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace InstallationProof.JotFormWebhook.Entities
{
    public class RequestInfoEntity : TableEntity
    {
        public RequestInfoEntity(string guid, string rawRequest)
        {
            this.PartitionKey = guid;
            this.RowKey = Guid.NewGuid().ToString();
            this.RawRequest = rawRequest;
        }

        public RequestInfoEntity()
        {
        }

        public string RawRequest { get; set; }
    }
}
