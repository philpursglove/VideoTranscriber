using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Newtonsoft.Json;

namespace VideoTranscriberVideoClient
{
    public class VideoIndexerClientArm : VideoIndexerClientBase, IVideoIndexerClient
    {
        private const string _apiVersion = "2022-08-01";
        private const string _azureResourceManager = "https://management.azure.com";
        private const string _apiUrl = "https://api.videoindexer.ai";
        private string _subscriptionId;
        private string _resourceGroupName;
        private string _accountName;
        private string _location;
        private string _accountId;
        private VideoIndexerResourceProviderClient _videoIndexerResourceProviderClient;

        public VideoIndexerClientArm()
        {
            // Build Azure Video Indexer resource provider client that has access token throuhg ARM
            _videoIndexerResourceProviderClient = VideoIndexerResourceProviderClient.BuildVideoIndexerResourceProviderClient().Result;

            // Get account details
            var account = _videoIndexerResourceProviderClient.GetAccount().Result;
            _location = account.Location;
            _accountId = account.Properties.Id;
        }

        public Task<IndexingResult> GetVideoIndex(string videoIndexerId)
        {
            throw new NotImplementedException();
        }

        public Task<IndexingResult> IndexVideo(Uri videoUrl, string videoName, Guid videoGuid)
        {
            throw new NotImplementedException();
        }

        public Task SubmitVideoForIndexing(Uri videoUri, string videoName, Guid videoId, Uri callbackUri)
        {
            throw new NotImplementedException();
        }

        internal class VideoIndexerResourceProviderClient
        {
            private readonly string _armAccessToken;
            async static public Task<VideoIndexerResourceProviderClient> BuildVideoIndexerResourceProviderClient()
            {
                var tokenRequestContext = new TokenRequestContext(new[] { $"{_azureResourceManager}/.default" });
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

                Console.WriteLine($"\nGetting access token: {JsonConvert.SerializeObject(accessTokenRequest)}");

                // Set the generateAccessToken (from video indexer) http request content
                try
                {
                    var jsonRequestBody = JsonConvert.SerializeObject(accessTokenRequest);
                    var httpContent = new StringContent(jsonRequestBody, System.Text.Encoding.UTF8, "application/json");

                    // Set request uri
                    var requestUri = $"{_azureResourceManager}/subscriptions/{_subscriptionId}/resourcegroups/{_resourceGroupName}/providers/Microsoft.VideoIndexer/accounts/{_accountName}/generateAccessToken?api-version={_apiVersion}";
                    var client = new HttpClient(new HttpClientHandler());
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _armAccessToken);

                    var result = await client.PostAsync(requestUri, httpContent);

                    VerifyStatus(result, System.Net.HttpStatusCode.OK);
                    var jsonResponseBody = await result.Content.ReadAsStringAsync();
                    Console.WriteLine($"Got access token: {scope} {videoId}, {permission}");
                    return JsonConvert.DeserializeObject<GenerateAccessTokenResponse>(jsonResponseBody).AccessToken;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    throw;
                }
            }

            /// <summary>
            /// Gets an account. Calls the getAccount API (https://github.com/Azure/azure-rest-api-specs/blob/main/specification/vi/resource-manager/Microsoft.VideoIndexer/stable/2022-08-01/vi.json#:~:text=%22/subscriptions/%7BsubscriptionId%7D/resourceGroups/%7BresourceGroupName%7D/providers/Microsoft.VideoIndexer/accounts/%7BaccountName%7D%22%3A%20%7B)
            /// </summary>
            /// <returns> The Account, otherwise throws an exception</returns>
            public async Task<Account> GetAccount()
            {
                Console.WriteLine($"Getting account {_accountName}.");
                Account account;
                try
                {
                    // Set request uri
                    var requestUri = $"{_azureResourceManager}/subscriptions/{_subscriptionId}/resourcegroups/{_resourceGroupName}/providers/Microsoft.VideoIndexer/accounts/{_accountName}?api-version={_apiVersion}";
                    var client = new HttpClient(new HttpClientHandler());
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _armAccessToken);

                    var result = await client.GetAsync(requestUri);

                    VerifyStatus(result, System.Net.HttpStatusCode.OK);
                    var jsonResponseBody = await result.Content.ReadAsStringAsync();
                    account = JsonConvert.DeserializeObject<Account>(jsonResponseBody);
                    VerifyValidAccount(account);
                    Console.WriteLine($"The account ID is {account.Properties.Id}");
                    Console.WriteLine($"The account location is {account.Location}");
                    return account;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    throw;
                }
            }

            private void VerifyValidAccount(Account account)
            {
                if (string.IsNullOrWhiteSpace(account.Location) || account.Properties == null || string.IsNullOrWhiteSpace(account.Properties.Id))
                {
                    Console.WriteLine($"{nameof(_accountName)} {_accountName} not found. Check {nameof(_subscriptionId)}, {nameof(_resourceGroupName)}, {nameof(_accountName)} ar valid.");
                    throw new Exception($"Account {_accountName} not found.");
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
