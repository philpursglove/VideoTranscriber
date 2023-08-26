using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using VideoTranscriberCore;
using VideoTranscriberData;
using VideoTranscriberStorage;
using VideoTranscriberVideoClient;

namespace VideoTranscriberFunctions
{
    public class IndexCompleteCallback
    {
        private readonly IVideoIndexerClient _videoIndexerClient;
        private readonly ITranscriptionDataRepository _repository;
        private readonly IStorageClient _storageClient;

        public IndexCompleteCallback(IVideoIndexerClient videoIndexerClient, ITranscriptionDataRepository repository, IStorageClient storageClient)
        {
            _videoIndexerClient = videoIndexerClient;
            _repository = repository;
            _storageClient = storageClient;
        }

        [FunctionName("IndexCompleteCallback")]
        public async Task Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ExecutionContext context)
        {
            string videoIndexerId = req.Query["id"];
            string state = req.Query["state"];

            if (state.ToLowerInvariant() == "processed")
            {
                var indexResult = await _videoIndexerClient.GetVideoIndex(videoIndexerId);

                // Get the externalId value
                Guid videoId = indexResult.VideoId;
                
                // Look up the record in CosmosDB by the externalId
                // Update the record with the transcription data
                string[] durationElements = indexResult.Duration.Split(':');
                int hours = int.Parse(durationElements[0]);
                int minutes = int.Parse(durationElements[1]);
                int seconds = (int)Math.Round(double.Parse(durationElements[2]));
                TimeSpan duration = new TimeSpan(hours, minutes, seconds);

                DateTime endTime = DateTime.UtcNow;

                TranscriptionData updateData = await _repository.Get(videoId);
                updateData.Language = indexResult.Language;
                updateData.Transcript = indexResult.Transcript;
                updateData.Duration = duration.TotalSeconds;
                updateData.SpeakerCount = indexResult.SpeakerCount;
                updateData.Confidence = indexResult.Confidence;
                updateData.Keywords = indexResult.Keywords;
                updateData.Speakers = indexResult.Speakers;
                updateData.TranscriptionStatus = TranscriptionStatus.Transcribed;
                await _repository.Update(updateData);

                // Move the video to the Processed container
                await _storageClient.MoveToFolder($"processing/{updateData.OriginalFilename}", "processed");
            }

        }
    }
}
