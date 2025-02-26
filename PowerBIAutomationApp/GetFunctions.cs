using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using PowerBIAutomationApp.Utilities;

namespace PowerBIAutomationApp
{
    public class GetFunctions
    {
        private readonly ILogger<GetFunctions> _logger;
        private readonly HttpClient _httpClient;

        public GetFunctions(ILogger<GetFunctions> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        [Function("GetAccessKey")]
        public async Task<IActionResult> GetAccessKey([
            HttpTrigger(AuthorizationLevel.Function, "get", "post")]
            HttpRequestData req)  // Updated to accept FunctionContext
        {
            string accessToken;

            try
            {
                accessToken = await FBConfigManager.GetAccessToken(); // Call the method to retrieve the access token
                return new OkObjectResult(accessToken);
            }
            catch (Exception)
            {
                _logger.LogError($"An error occurred while getting the access token");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [Function("GetAllReports")]
        public async Task<IActionResult> GetAllReports([
            HttpTrigger(AuthorizationLevel.Function, "get", 
            Route = "workspaces/{workspaceID}/reports")] HttpRequest req, 
            string workspaceID)
        {
            _logger.LogInformation($"Fetching reports for workspace: {workspaceID}");

            try
            {
                if (string.IsNullOrEmpty(workspaceID))
                {
                    return new BadRequestObjectResult("Missing workspaceID parameter.");
                }

                // Get access token
                string accessToken = await FBConfigManager.GetAccessToken();

                // Fetch reports
                string reportsJson = await FetchReportsAsync(workspaceID, accessToken);

                _logger.LogInformation("Successfully retrieved reports.");
                return new OkObjectResult(reportsJson);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while fetching reports: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        private async Task<string> FetchReportsAsync(string workspaceID, string accessToken)
        {
            using (HttpClient client = new HttpClient())
            {
                string reportsUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceID}/reports";

                // Set Authorization Header
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = await client.GetAsync(reportsUrl);

                if (!response.IsSuccessStatusCode)
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to retrieve reports: {errorResponse}");
                }

                return await response.Content.ReadAsStringAsync();
            }
        }

        [Function("GetSemanticModels")]
        public async Task<IActionResult> GetSemanticModels([HttpTrigger(AuthorizationLevel.Function, "get", 
            Route = "workspaces/{workspaceID}/semanticmodels")] HttpRequest req, 
            string workspaceID)
        {
            _logger.LogInformation($"Fetching semantic models for workspace: {workspaceID}");

            try
            {
                if (string.IsNullOrEmpty(workspaceID))
                {
                    return new BadRequestObjectResult("Missing workspaceID parameter.");
                }

                // Get access token
                string accessToken = await FBConfigManager.GetAccessToken();

                // Fetch semantic models
                string modelsJson = await FetchSemanticModelsAsync(workspaceID, accessToken);

                _logger.LogInformation("Successfully retrieved semantic models.");
                return new OkObjectResult(modelsJson);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while fetching semantic models: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        private async Task<string> FetchSemanticModelsAsync(string workspaceID, string accessToken)
        {
            using (HttpClient client = new HttpClient())
            {
                string datasetsUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceID}/datasets";

                // Set Authorization Header
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = await client.GetAsync(datasetsUrl);

                if (!response.IsSuccessStatusCode)
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to retrieve semantic models: {errorResponse}");
                }

                return await response.Content.ReadAsStringAsync();
            }
        }

        [Function("GetSemanticModelParameterValue")]
        public async Task<HttpResponseData> GetSemanticModelParameterValue([
            HttpTrigger(AuthorizationLevel.Function, "get", 
            Route = "workspace/{workspaceId}/semanticmodel/{modelId}/parameters")] HttpRequestData req,
           string workspaceId, 
           string modelId)
        {
            _logger.LogInformation($"Retrieving parameters for semantic model {modelId} in workspace {workspaceId}...");

            string accessToken;
            try
            {
                accessToken = await FBConfigManager.GetAccessToken();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting access token: {ex.Message}");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error retrieving access token: {ex.Message}");
                return errorResponse;
            }

            try
            {
                string parametersUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/datasets/{modelId}/parameters";

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                HttpResponseMessage response = await _httpClient.GetAsync(parametersUrl);
                string responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Error retrieving parameters: {responseJson}");
                    var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync($"Error retrieving parameters: {responseJson}");
                    return errorResponse;

                }

                var successResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await successResponse.WriteStringAsync(responseJson);
                return successResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving semantic model parameters: {ex.Message}");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error retrieving semantic model parameters: {ex.Message}");
                return errorResponse;
            }
        }
    }
}
