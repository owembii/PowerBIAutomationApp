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

        private readonly GetFunctions _getFunctions;

        public DeleteFunctions(ILogger<DeleteFunctions> logger, HttpClient httpClient, GetFunctions getFunctions)
        {
            _logger = logger;
            _httpClient = httpClient;
            _getFunctions = getFunctions;
        }

        [Function("DeleteAllSemanticModels")]
        public async Task<HttpResponseData> DeleteAllSemanticModels([
            HttpTrigger(AuthorizationLevel.Function, "delete",
            Route = "workspaces/{workspaceId}/semanticmodels/delete-all")] HttpRequestData req,
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
                var responseObj = JsonSerializer.Deserialize<SemanticModelListDTO>(responseJson, options);
                var semanticModels = responseObj?.Value ?? new List<SemanticModelDTO>();

                // NO MODELS FOUND HANDLER
                if (semanticModels.Count == 0)
                {
                    _logger.LogWarning($"No semantic models found in workspace: {workspaceId}");
                    var noModelsResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                    await noModelsResponse.WriteStringAsync("No semantic models found.");
                    return noModelsResponse;
                }

                // LOOPS THROUGH DELETING EACH SEMANTIC MODEL
                foreach (var model in semanticModels)
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
            HttpTrigger(AuthorizationLevel.Function, "delete", 
            Route = "workspaces/{workspaceId}/reports/{reportId}")] HttpRequest req,
            string workspaceId,
            string reportId)
        {
            _logger.LogInformation($"Attempting to delete report '{reportId}' in workspace: {workspaceId}");

            try
            {
                if (string.IsNullOrEmpty(workspaceId) || string.IsNullOrEmpty(reportId))
                {
                    return new BadRequestObjectResult("Missing workspaceID or reportID parameter.");
                }

                // Get access token
                string accessToken = await FBConfigManager.GetAccessToken();

                // Attempt to delete the report
                var result = await DeleteReportById(workspaceId, reportId, accessToken);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting report '{reportId}': {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<IActionResult> DeleteReportById(
            string workspaceId, 
            string reportId, 
            string accessToken)
        {
            using (HttpClient client = new HttpClient())
            {
                string deleteUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/reports/{reportId}";

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = await client.DeleteAsync(deleteUrl);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Successfully deleted report: {reportId}");
                    return new OkObjectResult($"Successfully deleted report: {reportId}");
                }

                switch (response.StatusCode)
                {
                    case System.Net.HttpStatusCode.NotFound: // 404 Report Not Found
                        _logger.LogWarning($"Report '{reportId}' not found in workspace '{workspaceId}'.");
                        return new NotFoundObjectResult($"Report '{reportId}' not found in workspace '{workspaceId}'.");

                    case System.Net.HttpStatusCode.Unauthorized: // 401 Unauthorized
                        _logger.LogError("Unauthorized access - invalid or expired token.");
                        return new UnauthorizedObjectResult("Unauthorized access. Please check your credentials.");

                    case System.Net.HttpStatusCode.Forbidden: // 403 Forbidden
                        _logger.LogError("Forbidden - Insufficient permissions to delete the report.");
                        return new ObjectResult("Forbidden - Insufficient permissions.") { StatusCode = StatusCodes.Status403Forbidden };

                    default: // Other errors
                        _logger.LogError($"Failed to delete report '{reportId}': {response.StatusCode} - {responseContent}");
                        return new ObjectResult($"Error deleting report: {response.StatusCode} - {responseContent}")
                        {
                            StatusCode = (int)response.StatusCode
                        };
                }
            }
        }
        [Function("DeleteAllReports")]
        public async Task<IActionResult> DeleteAllReports(
            [HttpTrigger(AuthorizationLevel.Function, "delete",
    Route = "workspaces/{workspaceId}/reports/delete-all")] HttpRequest req,
            string workspaceId)
        {
            _logger.LogInformation($"Attempting to delete all reports in workspace: {workspaceId}");

            if (string.IsNullOrEmpty(workspaceId))
            {
                return new BadRequestObjectResult("Missing workspaceID parameter.");
            }

            // Retrieve access token.
            string accessToken;
            try
            {
                accessToken = await FBConfigManager.GetAccessToken();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving access token: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            try
            {
                // Use the injected GetFunctions class to fetch all reports.
                string reportsJson = await _getFunctions.FetchReportsAsync(workspaceId, accessToken);
                _logger.LogInformation($"Reports JSON: {reportsJson}");

                // Deserialize the JSON into your ReportListDTO.
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var reportList = JsonSerializer.Deserialize<ReportListDTO>(reportsJson, options);
                var reports = reportList?.Value ?? new List<ReportDTO>();

                if (reports.Count == 0)
                {
                    _logger.LogInformation("No reports found in workspace.");
                    return new OkObjectResult("No reports found in workspace.");
                }

                // Use a list of objects for deleted reports, each containing the report ID and name.
                List<object> deletedReports = new List<object>();
                List<string> failedReports = new List<string>();

                // Loop through each report and attempt deletion.
                foreach (var report in reports)
                {
                    _logger.LogInformation($"Deleting report: {report.Id}");
                    IActionResult deleteResult = await DeleteReportById(workspaceId, report.Id, accessToken);
                    if (deleteResult is OkObjectResult)
                    {
                        // Add an object with report id and name.
                        deletedReports.Add(new { Id = report.Id, Name = report.Name });
                    }
                    else
                    {
                        failedReports.Add(report.Id);
                    }
                }

                return new OkObjectResult(new
                {
                    Message = "Report deletion process completed.",
                    DeletedReports = deletedReports,
                    FailedReports = failedReports
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during report deletion process: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }





        [Function("DeleteSemanticModel")]
        public async Task<HttpResponseData> DeleteSemanticModel([
            HttpTrigger(AuthorizationLevel.Function, "delete", 
            Route = "workspaces/{workspaceId}/semanticmodels/{semanticModelId}")] HttpRequestData req,
            string workspaceId, 
            string semanticModelId)
        {
            _logger.LogInformation($"Deleting semantic model {semanticModelId} in workspace {workspaceId}...");

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
                string deleteUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/datasets/{semanticModelId}";
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

        private async Task<HttpResponseData> CreateErrorResponse(
            HttpRequestData req, 
            string message, 
            Exception ex)
        {
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"{message} Details: {ex.Message}");
            return errorResponse;
        }

        [Function("DeleteWorkspace")]
        public async Task<HttpResponseData> DeleteWorkspace([
            HttpTrigger(AuthorizationLevel.Function, "delete", 
            Route = "workspaces/{workspaceId}/delete-workspace")] HttpRequestData req,
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
