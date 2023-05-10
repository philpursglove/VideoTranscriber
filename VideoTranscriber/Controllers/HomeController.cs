using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Azure.Storage.Blobs;
using VideoTranscriber.Models;
using Azure.Core;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Text.Json;
using Azure.Identity;
using System.Web;

namespace VideoTranscriber.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly string _connString;
        private readonly string _subscriptionId;
        private readonly string _resourceGroup;
        private readonly string _accountName;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _connString = configuration.GetConnectionString("VideoTranscriberStorageAccount");
            _subscriptionId = configuration["SubscriptionId"];
            _resourceGroup = configuration["ResourceGroup"];
            _accountName = configuration["AccountName"];
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Index(HomeIndexModel model)
        {
            if (ModelState.IsValid)
            {
                var blobServiceClient = new BlobServiceClient(_connString);
                var containerClient = blobServiceClient.GetBlobContainerClient("videos");
                var blobClient = containerClient.GetBlobClient(model.VideoFile.FileName);
                var blobResponse = blobClient.Upload(model.VideoFile.OpenReadStream(), true);

                string videoUrl = blobClient.Uri.ToString();

                IndexVideo(videoUrl).Wait();
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task IndexVideo(string videoUrl)
        {
            var videoIndexerResourceProviderClient = new VideoIndexerResourceProviderClient("https://management.azure.com", _subscriptionId, _accountName.Replace(" ", "%20"));

            // Get account details
            var account = await videoIndexerResourceProviderClient.GetAccount();
            var accountId = account.Properties.Id;
            var accountLocation = account.Location;

            // Get account-level access token for Azure Video Indexer
            var accessTokenRequest = new AccessTokenRequest
            {
                PermissionType = AccessTokenPermission.Contributor,
                Scope = ArmAccessTokenScope.Account
            };

            var accessToken = await videoIndexerResourceProviderClient.GetAccessToken(accessTokenRequest);
            var apiUrl = "https://api.videoindexer.ai";
            System.Net.ServicePointManager.SecurityProtocol = System.Net.ServicePointManager.SecurityProtocol | System.Net.SecurityProtocolType.Tls12;

            var handler = new HttpClientHandler();
            handler.AllowAutoRedirect = false;
            var client = new HttpClient(handler);

            MultipartFormDataContent content = null;
            var queryParams = CreateQueryString(
                new Dictionary<string, string>()
                {
                    {"accessToken", accessToken},
                    {"name", "video sample"},
                    {"description", "video_description"},
                    {"privacy", "private"},
                    {"partition", "partition"},
                    {"videoUrl", videoUrl},
                });
            var uploadRequestResult = await client.PostAsync($"{apiUrl}/{accountLocation}/Accounts/{accountId}/Videos?{queryParams}", content);
            var uploadResult = await uploadRequestResult.Content.ReadAsStringAsync();

            // Get the video ID from the upload result
            string videoId = JsonSerializer.Deserialize<Video>(uploadResult).Id;

            while (true)
            {
                await Task.Delay(10000);

                queryParams = CreateQueryString(
                    new Dictionary<string, string>()
                    {
                        {"accessToken", accessToken},
                        {"language", "English"},
                    });

                var videoGetIndexRequestResult = await client.GetAsync($"{apiUrl}/{accountLocation}/Accounts/{accountId}/Videos/{videoId}/Index?{queryParams}");
                var videoGetIndexResult = await videoGetIndexRequestResult.Content.ReadAsStringAsync();

                string processingState = JsonSerializer.Deserialize<Video>(videoGetIndexResult).State;

                ;

                // Job is finished
                if (processingState != "Uploaded" && processingState != "Processing")
                {
                    
                    break;
                }
            }

            queryParams = CreateQueryString(
                new Dictionary<string, string>()
                {
                    {"accessToken", accessToken},
                    {"id", videoId},
                });

            var searchRequestResult = await client.GetAsync($"{apiUrl}/{accountLocation}/Accounts/{accountId}/Videos/Search?{queryParams}");
            var searchResult = await searchRequestResult.Content.ReadAsStringAsync();


        }

        static string CreateQueryString(IDictionary<string, string> parameters)
        {
            var queryParameters = HttpUtility.ParseQueryString(string.Empty);
            foreach (var parameter in parameters)
            {
                queryParameters[parameter.Key] = parameter.Value;
            }

            return queryParameters.ToString();
        }
    }
}

public class VideoIndexerResourceProviderClient
{
    private readonly string armAaccessToken;
    private readonly string _AzureResourceManager;
    private readonly string _SubscriptionId;
    private readonly string _ResourceGroup;
    private readonly string _AccountName;

    public VideoIndexerResourceProviderClient(string azureResourceManager, string subscriptionId, string accountName)
    {
        _AzureResourceManager = azureResourceManager;
        _SubscriptionId = subscriptionId;
        _AccountName = accountName;
    }

    async public Task<VideoIndexerResourceProviderClient> BuildVideoIndexerResourceProviderClient()
    {
        var tokenRequestContext = new TokenRequestContext(new[] { $"{_AzureResourceManager}/.default" });
        var tokenRequestResult = await new DefaultAzureCredential().GetTokenAsync(tokenRequestContext);
        return new VideoIndexerResourceProviderClient(tokenRequestResult.Token);
    }
    public VideoIndexerResourceProviderClient(string armAaccessToken)
    {
        this.armAaccessToken = armAaccessToken;
    }

    public async Task<string> GetAccessToken(AccessTokenRequest accessTokenRequest)
    {
        // Set the generateAccessToken (from video indexer) HTTP request content
        var jsonRequestBody = JsonSerializer.Serialize(accessTokenRequest);
        var httpContent = new StringContent(jsonRequestBody, System.Text.Encoding.UTF8, "application/json");

        // Set request URI
        var requestUri = $"{_AzureResourceManager}/subscriptions/{_SubscriptionId}/resourcegroups/{_ResourceGroup}/providers/Microsoft.VideoIndexer/accounts/{_AccountName}/generateAccessToken?api-version=2021-08-16-preview";

        // Generate access token from video indexer
        var client = new HttpClient(new HttpClientHandler());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", armAaccessToken);
        var result = await client.PostAsync(requestUri, httpContent);
        var jsonResponseBody = await result.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GenerateAccessTokenResponse>(jsonResponseBody).AccessToken;
    }

    public async Task<Account> GetAccount()
    {

        // Set request URI
        var requestUri = $"{_AzureResourceManager}/subscriptions/{_SubscriptionId}/resourcegroups/{_ResourceGroup}/providers/Microsoft.VideoIndexer/accounts/{_AccountName}/?api-version=2021-08-16-preview";

        // Get account
        var client = new HttpClient(new HttpClientHandler());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", armAaccessToken);
        var result = await client.GetAsync(requestUri);
        var jsonResponseBody = await result.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Account>(jsonResponseBody);
    }
}

public class AccessTokenRequest
{
    [JsonPropertyName("permissionType")]
    public AccessTokenPermission PermissionType { get; set; }

    [JsonPropertyName("scope")]
    public ArmAccessTokenScope Scope { get; set; }

    [JsonPropertyName("projectId")]
    public string ProjectId { get; set; }

    [JsonPropertyName("videoId")]
    public string VideoId { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccessTokenPermission
{
    Reader,
    Contributor,
    MyAccessAdministrator,
    Owner,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ArmAccessTokenScope
{
    Account,
    Project,
    Video
}

public class GenerateAccessTokenResponse
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; }

}
public class AccountProperties
{
    [JsonPropertyName("accountId")]
    public string Id { get; set; }
}

public class Account
{
    [JsonPropertyName("properties")]
    public AccountProperties Properties { get; set; }

    [JsonPropertyName("location")]
    public string Location { get; set; }

}

public class Video
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; }
}
    