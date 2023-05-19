﻿using Newtonsoft.Json;
using VideoTranscriberCore;

namespace VideoTranscriberVideoClient;

public class VideoIndexerClient
{
    private readonly string _apiKey;
    private readonly string _accountId;
    private readonly string _location;
    private readonly Uri _apiUri = new Uri("https://api.videoindexer.ai");

    public VideoIndexerClient(string apiKey, string accountId, string location)
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
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);

        // obtain video access token
        var videoTokenRequestResult = client.GetAsync($"{_apiUri}/auth/{_location}/Accounts/{_accountId}/Videos/{videoIndexerId}/AccessToken?allowEdit=true").Result;
        var videoAccessToken = videoTokenRequestResult.Content.ReadAsStringAsync().Result.Replace("\"", "");

        var videoGetIndexRequestResult = client.GetAsync($"{_apiUri}/{_location}/Accounts/{_accountId}/Videos/{videoIndexerId}/Index?accessToken={videoAccessToken}&language=English").Result;
        var videoGetIndexResult = videoGetIndexRequestResult.Content.ReadAsStringAsync().Result;

        var processingState = JsonConvert.DeserializeObject<dynamic>(videoGetIndexResult)["state"];

        if (processingState != "Processed") return null;

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
        videoId = insights.externalId;

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
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);

        // obtain account access token
        var accountAccessTokenRequestResult = client.GetAsync($"{_apiUri}/auth/{_location}/Accounts/{_accountId}/AccessToken?allowEdit=true").Result;
        var accountAccessToken = accountAccessTokenRequestResult.Content.ReadAsStringAsync().Result.Replace("\"", "");

        client.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");

        // upload a video
        var content = new MultipartFormDataContent();
        //Debug.WriteLine("Uploading...");
        // get the video from URL
        //var videoUrl = "VIDEO_URL"; // replace with the video URL

        // as an alternative to specifying video URL, you can upload a file.
        // remove the videoUrl parameter from the query string below and add the following lines:
        //FileStream video =File.OpenRead(Globals.VIDEOFILE_PATH);
        //byte[] buffer = new byte[video.Length];
        //video.Read(buffer, 0, buffer.Length);
        //content.Add(new ByteArrayContent(buffer));

        string correctedName = videoName.Replace(" ", string.Empty);
        if (correctedName.Length > 80)
        {
            correctedName = correctedName.Substring(0, 80);
        }

        var uploadRequestResult = client.PostAsync($"{_apiUri}/{_location}/Accounts/{_accountId}/Videos?accessToken={accountAccessToken}&name={correctedName}&privacy=private&videoUrl={videoUri}&indexingPreset=AudioOnly&streamingPreset=NoStreaming&externalId={videoId.ToString()}&callbackUrl={callbackUri}", content).Result;
        var uploadResult = uploadRequestResult.Content.ReadAsStringAsync().Result;

    }

    public async Task<IndexingResult> IndexVideo(Uri videoUrl, string videoName, Guid videoGuid)
    {
        System.Net.ServicePointManager.SecurityProtocol = System.Net.ServicePointManager.SecurityProtocol | System.Net.SecurityProtocolType.Tls12;

        // create the http client
        var handler = new HttpClientHandler();
        handler.AllowAutoRedirect = false;
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);

        // obtain account access token
        var accountAccessTokenRequestResult = client.GetAsync($"{_apiUri}/auth/{_location}/Accounts/{_accountId}/AccessToken?allowEdit=true").Result;
        var accountAccessToken = accountAccessTokenRequestResult.Content.ReadAsStringAsync().Result.Replace("\"", "");

        client.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");

        // upload a video
        var content = new MultipartFormDataContent();
        //Debug.WriteLine("Uploading...");
        // get the video from URL
        //var videoUrl = "VIDEO_URL"; // replace with the video URL

        // as an alternative to specifying video URL, you can upload a file.
        // remove the videoUrl parameter from the query string below and add the following lines:
        //FileStream video =File.OpenRead(Globals.VIDEOFILE_PATH);
        //byte[] buffer = new byte[video.Length];
        //video.Read(buffer, 0, buffer.Length);
        //content.Add(new ByteArrayContent(buffer));

        string correctedName = videoName.Replace(" ", string.Empty);
        if (correctedName.Length > 80)
        {
            correctedName = correctedName.Substring(0, 80);
        }

        var uploadRequestResult = client.PostAsync($"{_apiUri}/{_location}/Accounts/{_accountId}/Videos?accessToken={accountAccessToken}&name={correctedName}&privacy=private&videoUrl={videoUrl}&indexingPreset=AudioOnly&streamingPreset=NoStreaming&externalId={videoGuid.ToString()}", content).Result;
        var uploadResult = uploadRequestResult.Content.ReadAsStringAsync().Result;

        // get the video id from the upload result
        var videoId = JsonConvert.DeserializeObject<dynamic>(uploadResult)["id"];

        // obtain video access token
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
        var videoTokenRequestResult = client.GetAsync($"{_apiUri}/auth/{_location}/Accounts/{_accountId}/Videos/{videoId}/AccessToken?allowEdit=true").Result;
        var videoAccessToken = videoTokenRequestResult.Content.ReadAsStringAsync().Result.Replace("\"", "");

        client.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");

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

            var videoGetIndexRequestResult = client.GetAsync($"{_apiUri}/{_location}/Accounts/{_accountId}/Videos/{videoId}/Index?accessToken={videoAccessToken}&language=English").Result;
            var videoGetIndexResult = videoGetIndexRequestResult.Content.ReadAsStringAsync().Result;

            var processingState = JsonConvert.DeserializeObject<dynamic>(videoGetIndexResult)["state"];

            //Debug.WriteLine("");
            //Debug.WriteLine("State:");
            //Debug.WriteLine(processingState);

            // job is finished
            if (processingState != "Uploaded" && processingState != "Processing")
            {
                //Debug.WriteLine("");
                // Debug.WriteLine("Full JSON:");
                //Debug.WriteLine(videoGetIndexResult);
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