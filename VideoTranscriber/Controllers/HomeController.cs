using Microsoft.AspNetCore.Mvc;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using VideoTranscriber.Models;
using Newtonsoft.Json;

namespace VideoTranscriber.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly string _connString;
        private readonly string _accountId;
        private readonly string _apiKey;
        private readonly Uri _tableUri;
        private readonly string _storageAccountName;
        private readonly string _storageAccountKey;
        private readonly string _location;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _connString = configuration.GetConnectionString("VideoTranscriberStorageAccount");
            _accountId = configuration["AccountId"];
            _apiKey = configuration["ApiKey"];
            _tableUri = new Uri(configuration["TableUri"]);
            _storageAccountName = configuration["StorageAccountName"];
            _storageAccountKey = configuration["StorageAccountKey"];
            _location = configuration["Location"];
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
                    PartitionKey = "Transcriptions"
                };
                TableServiceClient tableServiceClient =
                    new TableServiceClient(_tableUri, new TableSharedKeyCredential(_storageAccountName, _storageAccountKey));
                TableClient tableClient = tableServiceClient.GetTableClient("Transcriptions");
                await tableClient.CreateIfNotExistsAsync();
                await tableClient.AddEntityAsync(data);

                var videoUrl = await TransferToAzureStorage(model);

                Tuple<string, string> indexResult = await IndexVideo(videoUrl, model.VideoFile.FileName);

                TranscriptionData updateData =
                    await tableClient.GetEntityAsync<TranscriptionData>(rowKey: videoGuid.ToString(),
                        partitionKey: "Transcriptions");
                updateData.Language = indexResult.Item1;
                updateData.Transcript = indexResult.Item2;
                await tableClient.UpdateEntityAsync(updateData, ETag.All);

                return RedirectToAction("ViewTranscript", "Home", new {videoId = videoGuid});
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<string> TransferToAzureStorage(HomeIndexModel model)
        {
            var blobServiceClient = new BlobServiceClient(_connString);
            var containerClient = blobServiceClient.GetBlobContainerClient("videos");
            var blobClient = containerClient.GetBlobClient(model.VideoFile.FileName);
            await blobClient.UploadAsync(model.VideoFile.OpenReadStream(), true);

            return blobClient.Uri.ToString();
        }

        public async Task<IActionResult> ViewTranscript(Guid videoId)
        {
            TableServiceClient tableServiceClient =
                new TableServiceClient(_tableUri, new TableSharedKeyCredential(_storageAccountName, _storageAccountKey));
            TableClient tableClient = tableServiceClient.GetTableClient("Transcriptions");
            TranscriptionData transcriptData =
                await tableClient.GetEntityAsync<TranscriptionData>(rowKey: videoId.ToString(),
                    partitionKey: "Transcriptions");

            ViewTranscriptViewModel model = new ViewTranscriptViewModel()
            {
                Filename = transcriptData.OriginalFilename,
                Language = transcriptData.Language,
                Transcript = transcriptData.Transcript
            };
            
            return View(model);
        }

        private async Task<Tuple<string, string>> IndexVideo(string videoUrl, string videoName)
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

            var uploadRequestResult = client.PostAsync($"{apiUrl}/{_location}/Accounts/{_accountId}/Videos?accessToken={accountAccessToken}&name={correctedName}&description=some_description&privacy=private&partition=some_partition&videoUrl={videoUrl}&indexingPreset=BasicAudio", content).Result;
            var uploadResult = uploadRequestResult.Content.ReadAsStringAsync().Result;

            // get the video id from the upload result
            var videoId = JsonConvert.DeserializeObject<dynamic>(uploadResult)["id"];

            // obtain video access token
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
            var videoTokenRequestResult = client.GetAsync($"{apiUrl}/auth/{_location}/Accounts/{_accountId}/Videos/{videoId}/AccessToken?allowEdit=true").Result;
            var videoAccessToken = videoTokenRequestResult.Content.ReadAsStringAsync().Result.Replace("\"", "");

            client.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");

            // wait for the video index to finish
            string assembledTranscript = "";
            string language;
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
                    language = insights.sourceLanguage;
                    var transcript = insights.transcript;

                    foreach (var transcriptItem in transcript)
                    {
                        assembledTranscript += transcriptItem.text;
                    }

                    break;
                }
            }

            return new Tuple<string, string>(language, assembledTranscript);
        }
    }

    public class ViewTranscriptViewModel
    {
        public string Filename { get; set; }
        public string Language { get; set; }
        public string Transcript { get; set; }
    }
}

public class TranscriptionData : ITableEntity
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Language { get; set; }

    public string Transcript { get; set; }

    public string OriginalFilename { get; set; }

    public Guid VideoId { get; set; }

    public Guid BatchId { get; set; }
}