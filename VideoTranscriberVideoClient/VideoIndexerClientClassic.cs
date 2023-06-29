using Newtonsoft.Json;
using VideoTranscriberCore;

namespace VideoTranscriberVideoClient;

public class VideoIndexerClientClassic : VideoIndexerClientBase, IVideoIndexerClient
{
    private readonly string _apiKey;
    private readonly string _accountId;
    private readonly string _location;

    private const string SubscriptionKeyHeader = "Ocp-Apim-Subscription-Key";

    public VideoIndexerClientClassic(string apiKey, string accountId, string location)
    {
        _apiKey = apiKey;
        _accountId = accountId;
        _location = location;
    }

    public async Task<IndexingResult> GetVideoIndex(string videoIndexerId)
    {
        System.Net.ServicePointManager.SecurityProtocol = System.Net.ServicePointManager.SecurityProtocol | System.Net.SecurityProtocolType.Tls12;

        // create the http client
        var handler = new HttpClientHandler();
        handler.AllowAutoRedirect = false;
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add(SubscriptionKeyHeader, _apiKey);

        // obtain video access token
        var videoTokenRequestResult = await client.GetAsync($"{ApiUrl}/auth/{_location}/Accounts/{_accountId}/Videos/{videoIndexerId}/AccessToken?allowEdit=true");
        var videoAccessToken = videoTokenRequestResult.Content.ReadAsStringAsync().Result.Replace("\"", "");

        var videoGetIndexRequestResult = await client.GetAsync($"{ApiUrl}/{_location}/Accounts/{_accountId}/Videos/{videoIndexerId}/Index?accessToken={videoAccessToken}&language=English");
        var videoGetIndexResult = await videoGetIndexRequestResult.Content.ReadAsStringAsync();

        var processingState = JsonConvert.DeserializeObject<dynamic>(videoGetIndexResult)["state"];

        if (processingState != IndexingStatus.Processing) return null;

        List<TranscriptElement> transcriptElements = new List<TranscriptElement>();
        string language;
        string duration;
        int speakerCount;
        List<Speaker> speakers = new List<Speaker>();
        List<string> keywords = new List<string>();
        Guid videoId;

        var result = JsonConvert.DeserializeObject<dynamic>(videoGetIndexResult);

        var video = result.videos[0];
        var insights = video.insights;
        duration = insights.duration;
        language = insights.sourceLanguage;
        videoId = video.externalId;

        foreach (var speaker in insights.speakers)
        {
            speakers.Add(new Speaker { Id = speaker.id, Name = speaker.name });
        }
        speakerCount = speakers.Count;

        foreach (var keyword in insights.keywords)
        {
            keywords.Add((string)keyword.text);
        }

        var transcript = insights.transcript;

        foreach (var transcriptItem in transcript)
        {
            TranscriptElement element = new TranscriptElement
            {
                Text = transcriptItem.text,
                Confidence = transcriptItem.confidence,
                Id = transcriptItem.id,
                StartTimeIndex = transcriptItem.instances[0].start,
                SpeakerId = transcriptItem.speakerId
            };
            transcriptElements.Add(element);
        }

        return new IndexingResult()
        {
            Duration = duration,
            Language = language,
            Transcript = transcriptElements,
            Confidence = transcriptElements.Average(e => e.Confidence),
            SpeakerCount = speakerCount,
            Keywords = keywords,
            Speakers = speakers,
            VideoId = videoId
        };
    }

    public async Task SubmitVideoForIndexing(Uri videoUri, string videoName, Guid videoId, Uri callbackUri)
    {
        System.Net.ServicePointManager.SecurityProtocol = System.Net.ServicePointManager.SecurityProtocol | System.Net.SecurityProtocolType.Tls12;

        // create the http client
        var handler = new HttpClientHandler();
        handler.AllowAutoRedirect = false;
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add(SubscriptionKeyHeader, _apiKey);

        // obtain account access token
        var accountAccessTokenRequestResult = await client.GetAsync($"{ApiUrl}/auth/{_location}/Accounts/{_accountId}/AccessToken?allowEdit=true");
        var accountAccessToken = accountAccessTokenRequestResult.Content.ReadAsStringAsync().Result.Replace("\"", "");

        client.DefaultRequestHeaders.Remove(SubscriptionKeyHeader);

        // upload a video
        var content = new MultipartFormDataContent();

        var correctedName = CorrectName(videoName);

        _ = client.PostAsync($"{ApiUrl}/{_location}/Accounts/{_accountId}/Videos?accessToken={accountAccessToken}&name={correctedName}&privacy=private&videoUrl={videoUri}&indexingPreset=AudioOnly&streamingPreset=NoStreaming&externalId={videoId.ToString()}&callbackUrl={callbackUri}", content).Result;
    }


    public async Task<IndexingResult> IndexVideo(Uri videoUrl, string videoName, Guid videoGuid)
    {
        System.Net.ServicePointManager.SecurityProtocol = System.Net.ServicePointManager.SecurityProtocol | System.Net.SecurityProtocolType.Tls12;

        // create the http client
        var handler = new HttpClientHandler();
        handler.AllowAutoRedirect = false;
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add(SubscriptionKeyHeader, _apiKey);

        // obtain account access token
        var accountAccessTokenRequestResult = await client.GetAsync($"{ApiUrl}/auth/{_location}/Accounts/{_accountId}/AccessToken?allowEdit=true");
        var accountAccessToken = accountAccessTokenRequestResult.Content.ReadAsStringAsync().Result.Replace("\"", "");

        client.DefaultRequestHeaders.Remove(SubscriptionKeyHeader);

        // upload a video
        var content = new MultipartFormDataContent();

        var correctedName = CorrectName(videoName);

        var uploadRequestResult = await client.PostAsync($"{ApiUrl}/{_location}/Accounts/{_accountId}/Videos?accessToken={accountAccessToken}&name={correctedName}&privacy=private&videoUrl={videoUrl}&indexingPreset=AudioOnly&streamingPreset=NoStreaming&externalId={videoGuid}", content);
        var uploadResult = await uploadRequestResult.Content.ReadAsStringAsync();

        // get the video id from the upload result
        var videoId = JsonConvert.DeserializeObject<dynamic>(uploadResult)["id"];

        // obtain video access token
        client.DefaultRequestHeaders.Add(SubscriptionKeyHeader, _apiKey);
        var videoTokenRequestResult = await client.GetAsync($"{ApiUrl}/auth/{_location}/Accounts/{_accountId}/Videos/{videoId}/AccessToken?allowEdit=true");
        var videoAccessToken = videoTokenRequestResult.Content.ReadAsStringAsync().Result.Replace("\"", "");

        client.DefaultRequestHeaders.Remove(SubscriptionKeyHeader);

        // wait for the video index to finish
        List<TranscriptElement> transcriptElements = new List<TranscriptElement>();
        string language;
        string duration;
        int speakerCount;
        List<Speaker> speakers = new List<Speaker>();
        List<string> keywords = new List<string>();
        while (true)
        {
            Thread.Sleep(10000);

            var videoGetIndexRequestResult = await client.GetAsync($"{ApiUrl}/{_location}/Accounts/{_accountId}/Videos/{videoId}/Index?accessToken={videoAccessToken}&language=English");
            var videoGetIndexResult = await videoGetIndexRequestResult.Content.ReadAsStringAsync();

            var processingState = JsonConvert.DeserializeObject<dynamic>(videoGetIndexResult)["state"];

            // job is finished
            if (processingState != IndexingStatus.Uploaded && processingState != IndexingStatus.Processing)
            {
                var result = JsonConvert.DeserializeObject<dynamic>(videoGetIndexResult);

                var video = result.videos[0];
                var insights = video.insights;
                duration = insights.duration;
                language = insights.sourceLanguage;

                foreach (var speaker in insights.speakers)
                {
                    speakers.Add(new Speaker { Id = speaker.id, Name = speaker.name });
                }
                speakerCount = speakers.Count;

                foreach (var keyword in insights.keywords)
                {
                    keywords.Add((string)keyword.text);
                }

                var transcript = insights.transcript;

                foreach (var transcriptItem in transcript)
                {
                    TranscriptElement element = new TranscriptElement
                    {
                        Text = transcriptItem.text,
                        Confidence = transcriptItem.confidence,
                        Id = transcriptItem.id,
                        StartTimeIndex = transcriptItem.instances[0].start,
                        SpeakerId = transcriptItem.speakerId
                    };
                    transcriptElements.Add(element);
                }

                break;
            }
        }

        return new IndexingResult()
        {
            Duration = duration,
            Language = language,
            Transcript = transcriptElements,
            Confidence = transcriptElements.Average(e => e.Confidence),
            SpeakerCount = speakerCount,
            Keywords = keywords,
            Speakers = speakers
        };
    }
}