﻿using Microsoft.AspNetCore.Mvc;
using VideoTranscriber.Models;
using Newtonsoft.Json;
using VideoTranscriber.ViewModels;

namespace VideoTranscriber.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly string _accountId;
        private readonly string _apiKey;
        private readonly string _location;
        private readonly ITranscriptionDataRepository _transcriptionDataRepository;
        private readonly IStorageClient _storageClient;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration, ITranscriptionDataRepository transcriptionDataRepository, IStorageClient storageClient)
        {
            _logger = logger;
            _accountId = configuration["AccountId"];
            _apiKey = configuration["ApiKey"];
            _transcriptionDataRepository = transcriptionDataRepository;
            _location = configuration["Location"];
            _storageClient = storageClient;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(HomeIndexModel model)
        {
            if (ModelState.IsValid)
            {
                Guid videoGuid = Guid.NewGuid();
                TranscriptionData data = new TranscriptionData
                {
                    OriginalFilename = model.VideoFile.FileName,
                    VideoId = videoGuid,
                    RowKey = videoGuid.ToString(),
                    PartitionKey = "Transcriptions",
                    ProjectName = model.ProjectName,
                    UploadDate = DateTime.UtcNow
                };

                await _transcriptionDataRepository.Add(data);

                var videoUrl =
                    await _storageClient.UploadFile(model.VideoFile.FileName, model.VideoFile.OpenReadStream());

                IndexingResult indexResult = await IndexVideo(videoUrl, model.VideoFile.FileName);

                TranscriptionData updateData = await _transcriptionDataRepository.Get(videoGuid);
                updateData.Language = indexResult.Language;
                updateData.Transcript = JsonConvert.SerializeObject(indexResult.Transcript);
                updateData.Duration = indexResult.Duration;
                updateData.SpeakerCount = indexResult.SpeakerCount;
                updateData.Confidence = indexResult.Confidence;
                updateData.Keywords = JsonConvert.SerializeObject(indexResult.Keywords);
                await _transcriptionDataRepository.Update(updateData);

                await _storageClient.MoveToFolder(model.VideoFile.FileName, "processed");

                return RedirectToAction("ViewTranscript", "Home", new { videoId = videoGuid });
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> ViewTranscript(Guid videoId)
        {
            TranscriptionData transcriptData =
                await _transcriptionDataRepository.Get(videoId);

            ViewTranscriptViewModel model = new ViewTranscriptViewModel()
            {
                Filename = transcriptData.OriginalFilename,
                Language = transcriptData.Language,
                Transcript = JsonConvert.DeserializeObject<IEnumerable<TranscriptElement>>(transcriptData.Transcript),
                Keywords = JsonConvert.DeserializeObject<IEnumerable<string>>(transcriptData.Keywords)
            };

            return View(model);
        }

        private async Task<IndexingResult> IndexVideo(Uri videoUrl, string videoName)
        {
            var apiUrl = "https://api.videoindexer.ai";


            System.Net.ServicePointManager.SecurityProtocol = System.Net.ServicePointManager.SecurityProtocol | System.Net.SecurityProtocolType.Tls12;

            // create the http client
            var handler = new HttpClientHandler();
            handler.AllowAutoRedirect = false;
            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);

            // obtain account access token
            var accountAccessTokenRequestResult = client.GetAsync($"{apiUrl}/auth/{_location}/Accounts/{_accountId}/AccessToken?allowEdit=true").Result;
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

            var uploadRequestResult = client.PostAsync($"{apiUrl}/{_location}/Accounts/{_accountId}/Videos?accessToken={accountAccessToken}&name={correctedName}&description=some_description&privacy=private&partition=some_partition&videoUrl={videoUrl}&indexingPreset=AudioOnly", content).Result;
            var uploadResult = uploadRequestResult.Content.ReadAsStringAsync().Result;

            // get the video id from the upload result
            var videoId = JsonConvert.DeserializeObject<dynamic>(uploadResult)["id"];

            // obtain video access token
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
            var videoTokenRequestResult = client.GetAsync($"{apiUrl}/auth/{_location}/Accounts/{_accountId}/Videos/{videoId}/AccessToken?allowEdit=true").Result;
            var videoAccessToken = videoTokenRequestResult.Content.ReadAsStringAsync().Result.Replace("\"", "");

            client.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");

            // wait for the video index to finish
            List<TranscriptElement> transcriptElements = new List<TranscriptElement>();
            string language;
            string duration;
            int speakerCount;
            List<string> keywords = new List<string>();
            while (true)
            {
                Thread.Sleep(10000);

                var videoGetIndexRequestResult = client.GetAsync($"{apiUrl}/{_location}/Accounts/{_accountId}/Videos/{videoId}/Index?accessToken={videoAccessToken}&language=English").Result;
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
                    speakerCount = insights.speakers.Count;

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
                            StartTimeIndex = transcriptItem.instances[0].start
                        };
                        transcriptElements.Add(element);
                    }

                    break;
                }
            }

            return new IndexingResult() { Duration = duration, Language = language, 
                Transcript = transcriptElements, Confidence = transcriptElements.Average(e => e.Confidence), 
                SpeakerCount = speakerCount, Keywords = keywords};
        }

        public async Task<IActionResult> Transcripts()
        {
            var transcripts = await _transcriptionDataRepository.GetAll();

            return View(transcripts.Where(t => t.Transcript.Any()));
        }

        public async Task<IActionResult> TranscriptsForProject(string projectName)
        {
            var transcripts = await _transcriptionDataRepository.GetAll();

            return View("Transcripts", transcripts.Where(t => t.ProjectName == projectName));
        }

        public async Task<IActionResult> DownloadTranscript(Guid videoId)
        {
            TranscriptionData transcriptData =
                await _transcriptionDataRepository.Get(videoId);

            //FileContentResult result = new FileContentResult(Encoding.UTF8.GetBytes(transcriptData.Transcript), "text/plain")
            //{
            //    FileDownloadName = transcriptData.OriginalFilename + ".txt",
            //};

            return null;
        }
    }
}