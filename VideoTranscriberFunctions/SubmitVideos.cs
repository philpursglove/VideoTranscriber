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

public class SubmitVideos
{
    private readonly IVideoIndexerClient _videoIndexerClient;
    private readonly ITranscriptionDataRepository _repository;
    private readonly IStorageClient _storageClient;
    private readonly ConfigValues _configValues;

    public SubmitVideos(IVideoIndexerClient videoIndexerClient, ITranscriptionDataRepository repository,
        IStorageClient storageClient, ConfigValues configValues)
    {
        _videoIndexerClient = videoIndexerClient;
        _repository = repository;
        _storageClient = storageClient;
        _configValues = configValues;
    }

    [FunctionName("SubmitVideos")]
    public async Task Run(
        [TimerTrigger("1 * * * * *")]TimerInfo timer, ExecutionContext context)
    {
        List<string> fileNames = await _storageClient.GetFileNames("toBeProcessed");

        if (fileNames.Any())
        {
            foreach (string fileName in fileNames)
            {
                string fileNameWithoutFolder = fileName.Substring(fileName.IndexOf("/", StringComparison.InvariantCulture)+1);
                TranscriptionData data = await _repository.Get(fileNameWithoutFolder);
                await _storageClient.MoveToFolder(fileName, "processing");
                Uri fileUri = await _storageClient.GetFileUri($"processing/{fileNameWithoutFolder}");
                Guid videoGuid = data.id;

                await _videoIndexerClient.SubmitVideoForIndexing(fileUri, fileNameWithoutFolder, videoGuid,  _configValues.CallbackUrl);

                data.TranscriptionStatus = TranscriptionStatus.Transcribing;
                await _repository.Update(data);
            }
        }
    }
}