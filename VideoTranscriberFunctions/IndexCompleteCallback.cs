using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using VideoTranscriberCore;
using VideoTranscriberData;
using VideoTranscriberVideoClient;

namespace VideoTranscriberFunctions
{
    public static class IndexCompleteCallback
    {
        [FunctionName("IndexCompleteCallback")]
        public static async Task Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ExecutionContext context)
        {
            string videoIndexerId = req.Query["id"];
            string state = req.Query["state"];

            if (state == "Processed")
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(context.FunctionAppDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

                // Get the Index record by the id
                var videoIndexerClient =
                    new VideoIndexerClient(config["ApiKey"], config["AccountId"], config["Location"]);

                var indexResult = await videoIndexerClient.GetVideoIndex(videoIndexerId);

                // Get the externalId value
                Guid videoId = indexResult.VideoId;
                
                // Look up the record in CosmosDB by the externalId
                var repository = new TranscriptionDataCosmosRepository(config.GetConnectionString("VideoTranscriberCosmosDb"));
                
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
                await repository.Update(updateData);

                // Move the video to the Processed container

            }

        }
    }
}
