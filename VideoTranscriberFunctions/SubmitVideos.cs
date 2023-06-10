using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using VideoTranscriberCore;
using VideoTranscriberData;
using VideoTranscriberStorage;
using VideoTranscriberVideoClient;

namespace VideoTranscriberFunctions;

public static class SubmitVideos
{
    [FunctionName("SubmitVideos")]
    public static async Task Run(
        [TimerTrigger("1 * * * * *")]TimerInfo timer, Microsoft.Azure.WebJobs.ExecutionContext context)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(context.FunctionAppDirectory)
            .AddUserSecrets(Assembly.GetExecutingAssembly(), optional:true, reloadOnChange:true)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        IStorageClient storageClient =
            new AzureStorageClient(config.GetConnectionString("VideoTranscriberStorageAccount"),
                config["ContainerName"]);

        List<string> fileNames = await storageClient.GetFileNames("toBeProcessed");

        if (fileNames.Any())
        {
            var repository =
                new TranscriptionDataCosmosRepository(config.GetConnectionString("VideoTranscriberCosmosDb"));
            var videoClient = new VideoIndexerClientClassic(config["ApiKey"], config["AccountId"], config["location"]);

            foreach (string fileName in fileNames)
            {
                string fileNameWithoutFolder = fileName.Substring(fileName.IndexOf("/")+1);
                TranscriptionData data = await repository.Get(fileNameWithoutFolder);
                storageClient.MoveToFolder(fileName, "processing");
                Uri fileUri = await storageClient.GetFileUri($"processing/{fileNameWithoutFolder}");
                Guid videoGuid = data.id;

                videoClient.SubmitVideoForIndexing(fileUri, fileNameWithoutFolder, videoGuid,  new Uri(config["CallbackUri"]));

                data.TranscriptionStatus = TranscriptionStatus.Transcribing;
                repository.Update(data);
            }
        }
    }
}