using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using PowerBIAutomationApp.Utilities;
using System.Text.Json;

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

        [Function("GetReports")]
        public async Task<IActionResult> GetAllReports([
            HttpTrigger(AuthorizationLevel.Function, "get",
            Route = "workspaces/{workspaceId}/reports")] HttpRequest req,
            string workspaceId)
        {
            _logger.LogInformation($"Fetching reports for workspace: {workspaceId}");

            try
            {
                if (string.IsNullOrEmpty(workspaceId))
                {
                    return new BadRequestObjectResult("Missing workspaceID parameter.");
                }

                // Get access token
                string accessToken = await FBConfigManager.GetAccessToken();

                // Fetch reports
                string reportsJson = await FetchReportsAsync(workspaceId, accessToken);

                _logger.LogInformation("Successfully retrieved reports.");
                return new OkObjectResult(reportsJson);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while fetching reports: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<string> FetchReportsAsync(
            string workspaceId, 
            string accessToken)
        {
            using (HttpClient client = new HttpClient())
            {
                string reportsUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/reports";

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
        public async Task<IActionResult> GetSemanticModels([
            HttpTrigger(AuthorizationLevel.Function, "get",
            Route = "workspaces/{workspaceId}/semanticmodels")] HttpRequest req,
            string workspaceId)
        {
            _logger.LogInformation($"Fetching semantic models for workspace: {workspaceId}");

            try
            {
                if (string.IsNullOrEmpty(workspaceId))
                {
                    return new BadRequestObjectResult("Missing workspaceId parameter.");
                }

                // Get access token
                string accessToken = await FBConfigManager.GetAccessToken();

                // Fetch semantic models
                string modelsJson = await FetchSemanticModelsAsync(workspaceId, accessToken);

                _logger.LogInformation("Successfully retrieved semantic models.");
                return new OkObjectResult(modelsJson);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while fetching semantic models: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        private async Task<string> FetchSemanticModelsAsync(
            string workspaceId,
            string accessToken)
        {
            using (HttpClient client = new HttpClient())
            {
                string datasetsUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/datasets";

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

        [Function("GetSemanticModelParameters")]
        public async Task<HttpResponseData> GetSemanticModelParameterValue([
            HttpTrigger(AuthorizationLevel.Function, "get",
            Route = "workspaces/{workspaceId}/semanticmodels/{semanticModelId}/parameters")] HttpRequestData req,
            string workspaceId,
            string semanticModelId)
        {
            _logger.LogInformation($"Retrieving parameters for semantic model {semanticModelId} in workspace {workspaceId}...");

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
                string parametersUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/datasets/{semanticModelId}/parameters";

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

        [Function("GetReportId")]
        public async Task<IActionResult> GetReportId([
            HttpTrigger(AuthorizationLevel.Function, "get", 
            Route = "workspaces/{workspaceId}/reports/{reportName}/name")] HttpRequest req,
            string workspaceId,
            string reportName)
        {
            _logger.LogInformation($"Searching for report: {reportName} in workspace: {workspaceId}");

            try
            {
                // Step 1: Get Power BI Access Token
                string accessToken = await FBConfigManager.GetAccessToken();

                // Step 2: Find the report ID by its name
                string reportId = await FindReportIdByName(workspaceId, reportName, accessToken);

                if (!string.IsNullOrEmpty(reportId))
                {
                    _logger.LogInformation($"Found Report: {reportName}, Report ID: {reportId}");
                    return new OkObjectResult(new { ReportId = reportId });
                }
                else
                {
                    return new NotFoundObjectResult(new { Error = "Report not found" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error finding report: {ex.Message}");
                return new ObjectResult(new { Error = "Internal Server Error", Details = ex.Message })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }

        public async Task<string> FindReportIdByName(
            string workspaceId, 
            string reportName, 
            string accessToken)
        {
            using (HttpClient client = new HttpClient())
            {
                string reportsUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/reports";

                // Set Authorization Header
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = await client.GetAsync(reportsUrl);

                if (!response.IsSuccessStatusCode)
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to retrieve reports: {errorResponse}");
                }

                var jsonBody = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(jsonBody))
                {
                    foreach (var report in doc.RootElement.GetProperty("value").EnumerateArray())
                    {
                        if (report.TryGetProperty("name", out JsonElement nameElement) && nameElement.GetString() == reportName)
                        {
                            // If a match is found, return the report ID
                            return report.GetProperty("id").GetString();
                        }
                    }
                }

                return null; // Return null if no report found with the given name
            }

        }

        [Function("GetAllWorkspaces")]
        public async Task<HttpResponseData> GetAllWorkspaces([
            HttpTrigger(AuthorizationLevel.Function, "get",
            Route = "workspaces/all")] HttpRequestData req)
        {
            _logger.LogInformation("Retrieving all Power BI workspaces...");

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
                string baseUrl = "https://api.powerbi.com/v1.0/myorg/groups";

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
    }
}
