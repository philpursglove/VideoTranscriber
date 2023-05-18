using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace VideoTranscriberFunctions
{
    public static class IndexCompleteCallback
    {
        [FunctionName("IndexCompleteCallback")]
        public static async Task Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string id = req.Query["id"];
            string state = req.Query["state"];

            if (state == "Processed")
            {
                // Get the Index record by the id
                // Get the externalId value
                // Look up the record in CosmosDB by the externalId
                // Update the record with the transcription data
                // Move the video to the Processed container

            }
            
        }
    }
}
