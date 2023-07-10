using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using VideoTranscriberCore;
using VideoTranscriberData;
using VideoTranscriberStorage;
using VideoTranscriberVideoClient;

namespace VideoTranscriberFunctions;

public static class SubmitVideos
{
    [FunctionName("SubmitVideos")]
    public static async Task Run(
        [TimerTrigger("1 * * * * *")]TimerInfo timer, ExecutionContext context, IStorageClient storageClient, 
        ITranscriptionDataRepository repository, IVideoIndexerClient videoIndexerClient, ConfigValues configValues)
    {
        List<string> fileNames = await storageClient.GetFileNames("toBeProcessed");

        if (fileNames.Any())
        {
            foreach (string fileName in fileNames)
            {
                string fileNameWithoutFolder = fileName.Substring(fileName.IndexOf("/")+1);
                TranscriptionData data = await repository.Get(fileNameWithoutFolder);
                await storageClient.MoveToFolder(fileName, "processing");
                Uri fileUri = await storageClient.GetFileUri($"processing/{fileNameWithoutFolder}");
                Guid videoGuid = data.id;

                await videoIndexerClient.SubmitVideoForIndexing(fileUri, fileNameWithoutFolder, videoGuid,  configValues.CallbackUrl);

                data.TranscriptionStatus = TranscriptionStatus.Transcribing;
                await repository.Update(data);
            }
        }
    }
}