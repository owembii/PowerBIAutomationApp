using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PBIFunctionApp.DTOs;

namespace PBIFunctionApp.Workspaces
{
    public class Workspace
    {
        private readonly ILogger<Workspace> _logger;
        private readonly ILogger<GetAccessKey> _accessKeyLogger;
        private static readonly string baseUrl = "https://api.powerbi.com/v1.0/myorg/groups";

        public Workspace(ILogger<Workspace> logger, ILogger<GetAccessKey> accessKeyLogger)
        {
            _logger = logger;
            _accessKeyLogger = accessKeyLogger;
        }

        [Function("CreatePowerBIWorkspace")]
        public async Task<HttpResponseData> CreateWorkspace(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "workspace/create")] HttpRequestData req)
        {
            _logger.LogInformation("Creating Power BI workspace...");
            var requestBody = await JsonSerializer.DeserializeAsync<CreateWorkspaceDTO>(req.Body);
            if (requestBody == null || string.IsNullOrEmpty(requestBody.WorkspaceName))
            {
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request: Workspace name is required.");
                return errorResponse;
            }

            string accessToken;
            try
            {
                var authProvider = new GetAccessKey(_accessKeyLogger);
                accessToken = await authProvider.GetAccessToken();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting access token: {ex.Message}");
                Console.WriteLine($"Error getting access token: {ex}");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error retrieving access token: {ex.Message}");
                return errorResponse;
            }

            string workspaceResponse;
            try
            {
                workspaceResponse = await CreateWorkspaceAsync(accessToken, requestBody.WorkspaceName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating workspace: {ex.Message}");
                Console.WriteLine($"Error creating workspace: {ex}");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error creating workspace: {ex.Message}");
                return errorResponse;
            }

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(workspaceResponse);
            return response;
        }

        //private static async Task<string> GetAccessTokenAsync()
        //{
        //    string tokenUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
        //    var data = new Dictionary<string, string>
        //    {
        //        { "grant_type", "client_credentials" },
        //        { "scope", "https://analysis.windows.net/powerbi/api/.default" },
        //        { "client_id", clientId },
        //        { "client_secret", clientSecret }
        //    };

        //    using var content = new FormUrlEncodedContent(data);
        //    HttpResponseMessage response = await _httpClient.PostAsync(tokenUrl, content);
        //    string responseJson = await response.Content.ReadAsStringAsync();

        //    if (!response.IsSuccessStatusCode)
        //    {
        //        throw new Exception($"Failed to retrieve token: {responseJson}");
        //    }

        //    var tokenObj = JsonSerializer.Deserialize<TokenResponse>(responseJson);
        //    return tokenObj?.AccessToken ?? throw new Exception("Access token not found in response.");
        //}

        [Function("GetAllPowerBIWorkspaces")]
        public async Task<HttpResponseData> GetAllWorkspaces(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "workspaces/all")] HttpRequestData req)
        {
            _logger.LogInformation("Retrieving all Power BI workspaces...");

            string accessToken;
            try
            {
                var authProvider = new GetAccessKey(_accessKeyLogger);
                accessToken = await authProvider.GetAccessToken();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting access token: {ex.Message}");
                Console.WriteLine($"Error getting access token: {ex}");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error retrieving access token: {ex.Message}");
                return errorResponse;
            }

            string workspacesResponse;
            try
            {
                workspacesResponse = await GetAllWorkspacesAsync(accessToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving workspaces: {ex.Message}");
                Console.WriteLine($"Error retrieving workspaces: {ex}");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error retrieving workspaces: {ex.Message}");
                return errorResponse;
            }

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(workspacesResponse);
            return response;
        }

        private static async Task<string> GetAllWorkspacesAsync(string accessToken)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                HttpResponseMessage response = await client.GetAsync(baseUrl);
                string responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Error retrieving workspaces: {responseJson}");
                }

                return responseJson;
            }
        }

        private static async Task<string> CreateWorkspaceAsync(string accessToken, string workspaceName)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var requestBody = new { name = workspaceName, type = "Workspace" };
                string jsonBody = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(baseUrl, content);
                string responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Error creating workspace: {responseJson}");
                }

                return responseJson;
            }
        }

        [Function("AddUserToPowerBIWorkspace")]
        public async Task<HttpResponseData> AddUser(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "workspace/{workspaceId}/addUser")] HttpRequestData req,
            string workspaceId)
        {
            _logger.LogInformation($"Adding user to Power BI workspace: {workspaceId}");
            var requestBody = await JsonSerializer.DeserializeAsync<AddUserRequest>(req.Body);
            if (requestBody == null || string.IsNullOrEmpty(requestBody.UserEmail) || string.IsNullOrEmpty(requestBody.AccessRight))
            {
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request: User email and access right are required.");
                return errorResponse;
            }

            string accessToken;
            try
            {
                var authProvider = new GetAccessKey(_accessKeyLogger);
                accessToken = await authProvider.GetAccessToken();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting access token: {ex.Message}");
                Console.WriteLine($"Error getting access token: {ex}");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error retrieving access token: {ex.Message}");
                return errorResponse;
            }

            string apiResponse;
            try
            {
                apiResponse = await AddUserToWorkspaceAsync(accessToken, workspaceId, requestBody.UserEmail, requestBody.AccessRight);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding user to workspace: {ex.Message}");
                Console.WriteLine($"Error adding user to workspace: {ex}");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error adding user to workspace: {ex.Message}");
                return errorResponse;
            }

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(apiResponse);
            return response;
        }

        private static async Task<string> AddUserToWorkspaceAsync(string accessToken, string workspaceId, string userEmail, string accessRight)
        {
            string apiUrl = $"{baseUrl}/{workspaceId}/users";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var requestBody = new
                {
                    identifier = userEmail,
                    groupUserAccessRight = accessRight,
                    principalType = "User"
                };

                string jsonBody = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(apiUrl, content);
                string responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Error adding user: {responseJson}");
                }

                return responseJson;
            }
        }

        public class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; }
        }

        public class AddUserRequest
        {
            public string UserEmail { get; set; }
            public string AccessRight { get; set; }
        }
    }
}