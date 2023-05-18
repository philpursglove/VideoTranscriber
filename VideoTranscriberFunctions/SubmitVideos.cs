using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace VideoTranscriberFunctions;

public static class SubmitVideos
{
    [FunctionName("SubmitVideos")]
    public static async Task Run(
        [TimerTrigger("1 * * * * *")] ILogger log)
    {
        log.LogInformation("C# HTTP trigger function processed a request.");

        // Get the blobContainer
        var blobServiceClient = new BlobServiceClient(_connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

        List<Uri> fileUris = new List<Uri>();

        // Get the list of blobs
        var files = containerClient.GetBlobsAsync(prefix:"toBeProcessed");

        // For each blob
        //   Get the blob
        await foreach (var file in files)
        {
            var blobClient = containerClient.GetBlobClient(file.Name);
            fileUris.Add(blobClient.Uri);
        }

        // Move the blob to the Processing container
        //   Submit the blob to the Video Indexer

    }
}