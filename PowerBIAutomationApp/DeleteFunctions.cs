using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PowerBIAutomationApp.DTO;
using PowerBIAutomationApp.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;




namespace PowerBIAutomationApp
{
    public class DeleteFunctions
    {
        private readonly ILogger<DeleteFunctions> _logger;
        private readonly HttpClient _httpClient;

        public DeleteFunctions(ILogger<DeleteFunctions> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        [Function("DeleteAllSemanticModels")]
        public async Task<HttpResponseData> DeleteAllSemanticModels([
            HttpTrigger(AuthorizationLevel.Function, "delete",
            Route = "workspace/{workspaceId}/semanticmodels/delete")] HttpRequestData req,
            string workspaceId)
        {
            _logger.LogInformation($"Deleting all semantic models in workspace: {workspaceId}");

            string accessToken;
            try
            {
                accessToken = await FBConfigManager.GetAccessToken();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting access token: {ex}");
                return await CreateErrorResponse(req, "Error retrieving access token.", ex);
            }

            try
            {
                // FETCHING ALL THE SEMANTIC MODELS
                string modelsUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/datasets";
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                HttpResponseMessage response = await _httpClient.GetAsync(modelsUrl);
                string responseJson = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Power BI API Response: {responseJson}");

                // API Response Handler
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Error fetching semantic models: {responseJson}");
                    return await CreateErrorResponse(req, "Failed to retrieve semantic models.", new Exception(responseJson));
                }

                // PARSING JSON RESPONSE
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var responseObj = JsonSerializer.Deserialize<SemanticModelListResponse>(responseJson, options);
                var models = responseObj?.Value ?? new List<SemanticModel>();

                // NO MODELS FOUND HANDLER
                if (models.Count == 0)
                {
                    _logger.LogWarning($"No semantic models found in workspace: {workspaceId}");
                    var noModelsResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                    await noModelsResponse.WriteStringAsync("No semantic models found.");
                    return noModelsResponse;
                }

                // LOOPS THROUGH DELETING EACH SEMANTIC MODEL
                foreach (var model in models)
                {
                    string deleteUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/datasets/{model.Id}";
                    _logger.LogInformation($"Deleting Dataset ID: {model.Id}");

                    HttpResponseMessage deleteResponse = await _httpClient.DeleteAsync(deleteUrl);
                    string deleteResponseContent = await deleteResponse.Content.ReadAsStringAsync();

                    if (!deleteResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError($"Failed to delete model {model.Id}: {deleteResponseContent}");
                        return await CreateErrorResponse(req, $"Failed to delete model {model.Id}.", new Exception(deleteResponseContent));
                    }
                    else
                    {
                        _logger.LogInformation($"Successfully deleted model {model.Id}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting semantic models: {ex}");
                return await CreateErrorResponse(req, "Error deleting semantic models.", ex);
            }

            // SUCCESS RESPONSE
            var successResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await successResponse.WriteStringAsync("All semantic models deleted successfully.");
            return successResponse;
        }

        [Function("DeleteReport")]
        public async Task<IActionResult> DeleteReport([
            HttpTrigger(AuthorizationLevel.Function, "delete", Route = "workspaces/{workspaceID}/reports/{reportID}")] HttpRequest req,
            string workspaceID,
            string reportId)
        {
            _logger.LogInformation($"Attempting to delete report '{reportId}' in workspace: {workspaceID}");

            try
            {
                if (string.IsNullOrEmpty(workspaceID) || string.IsNullOrEmpty(reportId))
                {
                    return new BadRequestObjectResult("Missing workspaceID or reportID parameter.");
                }

                // Get access token
                string accessToken = await FBConfigManager.GetAccessToken();

                // Attempt to delete the report
                var result = await DeleteReportById(workspaceID, reportId, accessToken);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting report '{reportId}': {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        private async Task<IActionResult> DeleteReportById(string workspaceID, string reportID, string accessToken)
        {
            using (HttpClient client = new HttpClient())
            {
                string deleteUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceID}/reports/{reportID}";

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = await client.DeleteAsync(deleteUrl);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Successfully deleted report: {reportID}");
                    return new OkObjectResult($"Successfully deleted report: {reportID}");
                }

                switch (response.StatusCode)
                {
                    case System.Net.HttpStatusCode.NotFound: // 404 Report Not Found
                        _logger.LogWarning($"Report '{reportID}' not found in workspace '{workspaceID}'.");
                        return new NotFoundObjectResult($"Report '{reportID}' not found in workspace '{workspaceID}'.");

                    case System.Net.HttpStatusCode.Unauthorized: // 401 Unauthorized
                        _logger.LogError("Unauthorized access - invalid or expired token.");
                        return new UnauthorizedObjectResult("Unauthorized access. Please check your credentials.");

                    case System.Net.HttpStatusCode.Forbidden: // 403 Forbidden
                        _logger.LogError("Forbidden - Insufficient permissions to delete the report.");
                        return new ObjectResult("Forbidden - Insufficient permissions.") { StatusCode = StatusCodes.Status403Forbidden };

                    default: // Other errors
                        _logger.LogError($"Failed to delete report '{reportID}': {response.StatusCode} - {responseContent}");
                        return new ObjectResult($"Error deleting report: {response.StatusCode} - {responseContent}")
                        {
                            StatusCode = (int)response.StatusCode
                        };
                }
            }
        }

        [Function("DeleteSemanticModel")]
        public async Task<HttpResponseData> DeleteSemanticModel([
            HttpTrigger(AuthorizationLevel.Function, "delete", Route = "workspace/{workspaceId}/semanticmodel/{modelId}")] HttpRequestData req,
            string workspaceId, string modelId)
        {
            _logger.LogInformation($"Deleting semantic model {modelId} in workspace {workspaceId}...");

            string accessToken;
            try
            {
                accessToken = await FBConfigManager.GetAccessToken();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting access token: {ex.Message}");
                return await CreateErrorResponse(req, "Error retrieving access token.", ex);
            }

            // SENDING DELETE REQUEST TO POWER BI API
            try
            {
                string deleteUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/datasets/{modelId}";
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                HttpResponseMessage response = await _httpClient.DeleteAsync(deleteUrl);

                if (!response.IsSuccessStatusCode)
                {
                    string errorMessage = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error deleting semantic model: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting semantic model: {ex.Message}");
                return await CreateErrorResponse(req, "Error deleting semantic model.", ex);
            }

            var successResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await successResponse.WriteStringAsync("Semantic model deleted successfully.");
            return successResponse;
        }

        private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, string message, Exception ex)
        {
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"{message} Details: {ex.Message}");
            return errorResponse;
        }

        [Function("DeleteWorkspace")]
        public async Task<HttpResponseData> DeleteWorkspace(
           [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "workspaces/{workspaceId}/delete")] HttpRequestData req,
           string workspaceId)
        {
            _logger.LogInformation($"Attempting to delete workspace: {workspaceId}");

            // Get Access Token
            string accessToken;
            try
            {
                accessToken = await FBConfigManager.GetAccessToken();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving access token: {ex.Message}");
                return await CreateErrorResponse(req, "Failed to retrieve access token.", ex);
            }

            try
            {
                // Power BI API URL for deleting a workspace
                string deleteUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}";

                // Add Authorization Header
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                // Send DELETE Request
                HttpResponseMessage response = await _httpClient.DeleteAsync(deleteUrl);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to delete workspace {workspaceId}: {responseContent}");
                    return await CreateErrorResponse(req, $"Failed to delete workspace {workspaceId}.", new Exception(responseContent));
                }

                _logger.LogInformation($"Successfully deleted workspace {workspaceId}");

                // Success Response
                var successResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await successResponse.WriteStringAsync($"Workspace {workspaceId} deleted successfully.");
                return successResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting workspace: {ex.Message}");
                throw;
            }
        }

       


    }
}
