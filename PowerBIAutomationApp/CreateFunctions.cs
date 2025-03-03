using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PowerBIAutomationApp.DTO;
using PowerBIAutomationApp.Utilities;

namespace PowerBIAutomationApp
{
    public class CreateFunctions
    {
        private readonly ILogger<CreateFunctions> _logger;
        private static readonly string baseUrl = "https://api.powerbi.com/v1.0/myorg/groups";

        public CreateFunctions(ILogger<CreateFunctions> logger)
        {
            _logger = logger;
        }

        [Function("CreateWorkspace")]
        public async Task<HttpResponseData> CreateWorkspace([
            HttpTrigger(AuthorizationLevel.Function, "post", 
            Route = "workspaces/create")] HttpRequestData req)
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
                accessToken = await FBConfigManager.GetAccessToken();
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

        private static async Task<string> CreateWorkspaceAsync(
            string accessToken, 
            string workspaceName)
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

        [Function("AddUserToWorkspace")]
        public async Task<HttpResponseData> AddUser([
            HttpTrigger(AuthorizationLevel.Function, "post", 
            Route = "workspaces/{workspaceId}/addUser")] HttpRequestData req,
            string workspaceId)
        {
            _logger.LogInformation($"Adding user to Power BI workspace: {workspaceId}");
            var requestBody = await JsonSerializer.DeserializeAsync<AddUserRequestDTO>(req.Body);
            if (requestBody == null || string.IsNullOrEmpty(requestBody.UserEmail) || string.IsNullOrEmpty(requestBody.AccessRight))
            {
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request: User email and access right are required.");
                return errorResponse;
            }

            string accessToken;
            try
            {
                accessToken = await FBConfigManager.GetAccessToken();
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

        private static async Task<string> AddUserToWorkspaceAsync(
            string accessToken, 
            string workspaceId, 
            string userEmail, 
            string accessRight)
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

    }
}