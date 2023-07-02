using Azure.Core;
using Azure.Identity;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Net.Http.Headers;
using VideoTranscriberCore;

namespace VideoTranscriberVideoClient
{
    public class VideoIndexerClientArm : VideoIndexerClientBase, IVideoIndexerClient
    {
        private const string ApiVersion = "2022-08-01";
        private const string AzureResourceManager = "https://management.azure.com";
        private readonly string _subscriptionId;
        private readonly string _resourceGroupName;
        private readonly string _accountName;
        private readonly string _location;
        private readonly string _accountId;
        private VideoIndexerResourceProviderClient _videoIndexerResourceProviderClient;
        private readonly HttpClient _httpClient;

        public VideoIndexerClientArm(string subscriptionId, string resourceGroupName, string accountName)
        {
            _subscriptionId = subscriptionId;
            _resourceGroupName = resourceGroupName;
            _accountName = accountName;

            // Build Azure Video Indexer resource provider client that has access token through ARM
            _videoIndexerResourceProviderClient = VideoIndexerResourceProviderClient.BuildVideoIndexerResourceProviderClient().Result;

            _videoIndexerResourceProviderClient.SubscriptionId = _subscriptionId;
            _videoIndexerResourceProviderClient.ResourceGroupName = _resourceGroupName;
            _videoIndexerResourceProviderClient.AccountName = _accountName;

            // Get account details
            var account = _videoIndexerResourceProviderClient.GetAccount().Result;
            _location = account.Location;
            _accountId = account.Properties.Id;

            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false
            };
            _httpClient = new HttpClient(handler);

        }
        public async Task<IndexingResult> GetVideoIndex(string videoIndexerId)
        {

            var accountAccessToken = await _videoIndexerResourceProviderClient.GetAccessToken(ArmAccessTokenPermission.Contributor, ArmAccessTokenScope.Account);

            string queryParams;
            queryParams = CreateQueryString(
                new Dictionary<string, string>()
                {
                        {"accessToken", accountAccessToken},
                        {"language", "English"},
                });


            var videoGetIndexRequestResult = await _httpClient.GetAsync($"{ApiUrl}/{_location}/Accounts/{_accountId}/Videos/{videoIndexerId}/Index?{queryParams}");

            VerifyStatus(videoGetIndexRequestResult, System.Net.HttpStatusCode.OK);
            var videoGetIndexResult = await videoGetIndexRequestResult.Content.ReadAsStringAsync();
            string processingState = JsonConvert.DeserializeObject<Video>(videoGetIndexResult).State;

            List<TranscriptElement> transcriptElements = new List<TranscriptElement>();
            string language = String.Empty;
            string duration = String.Empty;
            int speakerCount = 0;
            List<Speaker> speakers = new List<Speaker>();
            List<string> keywords = new List<string>();

            if (processingState == IndexingStatus.Processed)
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

        public async Task<IndexingResult> IndexVideo(Uri videoUrl, string videoName, Guid videoGuid)
        {
            var content = new MultipartFormDataContent();

            var accountAccessToken =
                await _videoIndexerResourceProviderClient.GetAccessToken(ArmAccessTokenPermission.Contributor,
                    ArmAccessTokenScope.Account);

            // Get the video from URL
            var queryParams = CreateQueryString(
                new Dictionary<string, string>()
                {
                    {"accessToken", accountAccessToken},
                    {"name", videoName},
                    {"privacy", "private"},
                    {"externalId", videoGuid.ToString()},
                    {"videoUrl", videoUrl.ToString()},
                });

            var uploadRequestResult = await _httpClient.PostAsync($"{ApiUrl}/{_location}/Accounts/{_accountId}/Videos?{queryParams}",
                content);
            var uploadResult = await uploadRequestResult.Content.ReadAsStringAsync();
            var videoId = JsonConvert.DeserializeObject<dynamic>(uploadResult)["id"];

            queryParams = CreateQueryString(
                new Dictionary<string, string>()
                {
                    {"accessToken", accountAccessToken},
                    {"language", "English"},
                });

            var videoGetIndexRequestResult =
                await _httpClient.GetAsync(
                    $"{ApiUrl}/{_location}/Accounts/{_accountId}/Videos/{videoId}/Index?{queryParams}");

            VerifyStatus(videoGetIndexRequestResult, System.Net.HttpStatusCode.OK);

            List<TranscriptElement> transcriptElements = new List<TranscriptElement>();
            string language = String.Empty;
            string duration = String.Empty;
            int speakerCount = 0;
            List<Speaker> speakers = new List<Speaker>();
            List<string> keywords = new List<string>();

            while (true)
            {
                Thread.Sleep(10000);

                var videoGetIndexResult = await videoGetIndexRequestResult.Content.ReadAsStringAsync();
                string processingState = JsonConvert.DeserializeObject<Video>(videoGetIndexResult).State;

                if (processingState != IndexingStatus.Uploaded && processingState != IndexingStatus.Processing)
                {
                    var result = JsonConvert.DeserializeObject<dynamic>(videoGetIndexResult);

                    var video = result.videos[0];
                    var insights = video.insights;
                    duration = insights.duration;
                    language = insights.sourceLanguage;

                    foreach (var speaker in insights.speakers)
                    {
                        speakers.Add(new Speaker {Id = speaker.id, Name = speaker.name});
                    }

                    speakerCount = speakers.Count;

                    foreach (var keyword in insights.keywords)
                    {
                        keywords.Add((string) keyword.text);
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

        public async Task SubmitVideoForIndexing(Uri videoUri, string videoName, Guid videoGuid, Uri callbackUri)
            {
                var content = new MultipartFormDataContent();

                var accountAccessToken = await _videoIndexerResourceProviderClient.GetAccessToken(ArmAccessTokenPermission.Contributor, ArmAccessTokenScope.Account);

                // Get the video from URL
                var queryParams = CreateQueryString(
                    new Dictionary<string, string>()
                    {
                        {"accessToken", accountAccessToken},
                        {"name", videoName},
                        {"privacy", "private"},
                        {"externalId", videoGuid.ToString()},
                        {"videoUrl", videoUri.ToString()},
                        {"callbackUrl", callbackUri.ToString()}
                    });

                _ = await _httpClient.PostAsync($"{ApiUrl}/{_location}/Accounts/{_accountId}/Videos?{queryParams}", content);
            }

        internal class VideoIndexerResourceProviderClient
        {
            private readonly string _armAccessToken;
            public string SubscriptionId { get; set; }
            public string ResourceGroupName { get; set; }
            public string AccountName { get; set; }
            public static async Task<VideoIndexerResourceProviderClient> BuildVideoIndexerResourceProviderClient()
            {
                var tokenRequestContext = new TokenRequestContext(new[] { $"{AzureResourceManager}/.default" });
                var tokenRequestResult = await new DefaultAzureCredential().GetTokenAsync(tokenRequestContext);
                return new VideoIndexerResourceProviderClient(tokenRequestResult.Token);
            }
            public VideoIndexerResourceProviderClient(string armAccessToken)
            {
                _armAccessToken = armAccessToken;
            }

            /// <summary>
            /// Generates an access token. Calls the generateAccessToken API  (https://github.com/Azure/azure-rest-api-specs/blob/main/specification/vi/resource-manager/Microsoft.VideoIndexer/stable/2022-08-01/vi.json#:~:text=%22/subscriptions/%7BsubscriptionId%7D/resourceGroups/%7BresourceGroupName%7D/providers/Microsoft.VideoIndexer/accounts/%7BaccountName%7D/generateAccessToken%22%3A%20%7B)
            /// </summary>
            /// <param name="permission"> The permission for the access token</param>
            /// <param name="scope"> The scope of the access token </param>
            /// <param name="videoId"> if the scope is video, this is the video Id </param>
            /// <param name="projectId"> If the scope is project, this is the project Id </param>
            /// <returns> The access token, otherwise throws an exception</returns>
            public async Task<string> GetAccessToken(ArmAccessTokenPermission permission, ArmAccessTokenScope scope, string videoId = null, string projectId = null)
            {
                var accessTokenRequest = new AccessTokenRequest
                {
                    PermissionType = permission,
                    Scope = scope,
                    VideoId = videoId,
                    ProjectId = projectId
                };

                // Set the generateAccessToken (from video indexer) http request content
                try
                {
                    var jsonRequestBody = JsonConvert.SerializeObject(accessTokenRequest);
                    var httpContent = new StringContent(jsonRequestBody, System.Text.Encoding.UTF8, "application/json");

                    // Set request uri
                    var requestUri = $"{AzureResourceManager}/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroupName}/providers/Microsoft.VideoIndexer/accounts/{AccountName}/generateAccessToken?api-version={ApiVersion}";
                    var client = new HttpClient(new HttpClientHandler());
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _armAccessToken);

                    var result = await client.PostAsync(requestUri, httpContent);

                    VerifyStatus(result, System.Net.HttpStatusCode.OK);
                    var jsonResponseBody = await result.Content.ReadAsStringAsync();

                    return JsonConvert.DeserializeObject<GenerateAccessTokenResponse>(jsonResponseBody).AccessToken;
                }
                catch (Exception ex)
                {
                    throw;
                }
            }

            /// <summary>
            /// Gets an account. Calls the getAccount API (https://github.com/Azure/azure-rest-api-specs/blob/main/specification/vi/resource-manager/Microsoft.VideoIndexer/stable/2022-08-01/vi.json#:~:text=%22/subscriptions/%7BsubscriptionId%7D/resourceGroups/%7BresourceGroupName%7D/providers/Microsoft.VideoIndexer/accounts/%7BaccountName%7D%22%3A%20%7B)
            /// </summary>
            /// <returns> The Account, otherwise throws an exception</returns>
            public async Task<Account> GetAccount()
            {
                Account account;
                try
                {
                    // Set request uri
                    var requestUri = $"{AzureResourceManager}/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroupName}/providers/Microsoft.VideoIndexer/accounts/{AccountName}?api-version={ApiVersion}";
                    var client = new HttpClient(new HttpClientHandler());
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _armAccessToken);

                    var result = await client.GetAsync(requestUri);

                    VerifyStatus(result, System.Net.HttpStatusCode.OK);
                    var jsonResponseBody = await result.Content.ReadAsStringAsync();
                    account = JsonConvert.DeserializeObject<Account>(jsonResponseBody);
                    VerifyValidAccount(account);

                    return account;
                }
                catch (Exception ex)
                {
                    throw;
                }
            }

            private void VerifyValidAccount(Account account)
            {
                if (string.IsNullOrWhiteSpace(account.Location) || account.Properties == null || string.IsNullOrWhiteSpace(account.Properties.Id))
                {
                    throw new Exception($"Account {AccountName} not found.");
                }
            }
        }

        public class AccessTokenRequest
        {
            [JsonProperty("permissionType")]
            public ArmAccessTokenPermission PermissionType { get; set; }

            [JsonProperty("scope")]
            public ArmAccessTokenScope Scope { get; set; }

            [JsonProperty("projectId")]
            public string ProjectId { get; set; }

            [JsonProperty("videoId")]
            public string VideoId { get; set; }
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum ArmAccessTokenPermission
        {
            Reader,
            Contributor,
            MyAccessAdministrator,
            Owner,
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum ArmAccessTokenScope
        {
            Account,
            Project,
            Video
        }

        public class GenerateAccessTokenResponse
        {
            [JsonProperty("accessToken")]
            public string AccessToken { get; set; }
        }

        public class AccountProperties
        {
            [JsonProperty("accountId")]
            public string Id { get; set; }
        }

        public class Account
        {
            [JsonProperty("properties")]
            public AccountProperties Properties { get; set; }

            [JsonProperty("location")]
            public string Location { get; set; }
        }

        public class Video
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("state")]
            public string State { get; set; }
        }

        public enum ProcessingState
        {
            Uploaded,
            Processing,
            Processed,
            Failed
        }

        public static void VerifyStatus(HttpResponseMessage response, System.Net.HttpStatusCode expectedStatusCode)
        {
            if (response.StatusCode != expectedStatusCode)
            {
                throw new Exception(response.ToString());
            }
        }

    }
}
