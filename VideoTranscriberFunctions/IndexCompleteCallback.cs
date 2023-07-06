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
    public static class IndexCompleteCallback
    {
        [FunctionName("IndexCompleteCallback")]
        public static async Task Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ExecutionContext context, 
            IVideoIndexerClient videoIndexerClient, ITranscriptionDataRepository repository, IStorageClient storageClient)
        {
            string videoIndexerId = req.Query["id"];
            string state = req.Query["state"];

            if (state.ToLowerInvariant() == "processed")
            {
                var indexResult = await videoIndexerClient.GetVideoIndex(videoIndexerId);

                // Get the externalId value
                Guid videoId = indexResult.VideoId;
                
                // Look up the record in CosmosDB by the externalId
                // Update the record with the transcription data
                var transcriptionData = repository.Get(videoId);
                string[] durationElements = indexResult.Duration.Split(':');
                int hours = int.Parse(durationElements[0]);
                int minutes = int.Parse(durationElements[1]);
                int seconds = (int)Math.Round(double.Parse(durationElements[2]));
                TimeSpan duration = new TimeSpan(hours, minutes, seconds);

                DateTime endTime = DateTime.UtcNow;

                TranscriptionData updateData = await repository.Get(videoId);
                updateData.Language = indexResult.Language;
                updateData.Transcript = indexResult.Transcript;
                updateData.Duration = duration.TotalSeconds;
                updateData.SpeakerCount = indexResult.SpeakerCount;
                updateData.Confidence = indexResult.Confidence;
                updateData.Keywords = indexResult.Keywords;
                updateData.Speakers = indexResult.Speakers;
                updateData.TranscriptionStatus = TranscriptionStatus.Transcribed;
                await repository.Update(updateData);

                // Move the video to the Processed container
                await storageClient.MoveToFolder($"processing/{updateData.OriginalFilename}", "processed");
            }

        }
    }
}
