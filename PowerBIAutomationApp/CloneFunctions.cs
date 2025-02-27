using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PowerBIAutomationApp.DTO;
using PowerBIAutomationApp.Utilities;

namespace PowerBIAutomationApp
{
    public class CloneFunctions
    {
        private readonly ILogger<CloneFunctions> _logger;
        private DeleteFunctions _deleteFunctions;
        private ExportFunctions _exportFunctions;
        private UploadFunctions _uploadFunctions;
        public CloneFunctions(ILogger<CloneFunctions> logger, DeleteFunctions deleteFunctions, ExportFunctions exportFunctions, UploadFunctions uploadFunctions)
        {
            _logger = logger;
            _deleteFunctions = deleteFunctions;
            _exportFunctions = exportFunctions;
            _uploadFunctions = uploadFunctions;
        }

        [Function("CloneReport")]
        public async Task<IActionResult> CloneReport([
            HttpTrigger(AuthorizationLevel.Function, "post",
            Route = "workspaces/{sourceWorkspaceId}/reports/{reportId}/clone-report")] HttpRequest req,
            string sourceWorkspaceId,
            string reportId)
        {
            _logger.LogInformation("Processing clone report request.");

            try
            {
                string accessToken = await FBConfigManager.GetAccessToken();

                // Read and deserialize request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var cloneRequest = new CloneReportDTO();

                // Only deserialize when request body is not null or empty
                if (!string.IsNullOrWhiteSpace(requestBody))
                {
                    cloneRequest = JsonSerializer.Deserialize<CloneReportDTO>(requestBody, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }

                // Clone the report
                string newReportID = await CloneReportAsync(
                    sourceWorkspaceId,
                    reportId,
                    cloneRequest?.name,
                    cloneRequest?.targetWorkspaceId,
                    cloneRequest?.targetModelId,
                    accessToken);

                _logger.LogInformation($"Successfully cloned report. New Report ID: {newReportID}");

                return new OkObjectResult(new { ClonedReportId = newReportID });
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while cloning the report: {ex}");
                return new ObjectResult(new { Error = "Internal Server Error", Details = ex.Message })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }

        private async Task<string> GetOriginalReportName(string workspaceId, string reportId, string accessToken)
        {
            using (HttpClient client = new HttpClient())
            {
                string reportUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/reports/{reportId}";
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = await client.GetAsync(reportUrl);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to retrieve original report name: {await response.Content.ReadAsStringAsync()}");
                }

                string jsonBody = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(jsonBody))
                {
                    if (doc.RootElement.TryGetProperty("name", out JsonElement nameElement))
                    {
                        return nameElement.GetString() ?? throw new Exception("Original report name not found.");
                    }
                    else
                    {
                        throw new Exception("Response JSON does not contain 'name'.");
                    }
                }
            }
        }

        private async Task<string> CloneReportAsync(
            string sourceWorkspaceId,
            string reportId,
            string? reportName,
            string? targetWorkspaceId,
            string? targetModelId,
            string accessToken)
        {
            using (HttpClient client = new HttpClient())
            {
                string cloneUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{sourceWorkspaceId}/reports/{reportId}/Clone";

                // Set Authorization Header
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // If no report name is provided, fetch the original report name
                if (string.IsNullOrWhiteSpace(reportName))
                {
                    _logger.LogInformation("No report name provided. Retrieving current report name.");
                    reportName = await GetOriginalReportName(sourceWorkspaceId, reportId, accessToken);
                }

                var requestBody = new
                {
                    name = reportName,
                    targetWorkspaceId,
                    targetModelId
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(cloneUrl, jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to clone report: {errorResponse}");
                }

                var jsonBody = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(jsonBody))
                {
                    if (doc.RootElement.TryGetProperty("id", out JsonElement idElement))
                    {
                        return idElement.GetString() ?? throw new Exception("Failed to retrieve cloned report ID.");
                    }
                    else
                    {
                        throw new Exception("Response JSON does not contain 'id'.");
                    }
                }
            }
        }

        [Function("CloneSemanticModel")]
        public async Task<IActionResult> CloneSemanticModel([
            HttpTrigger(AuthorizationLevel.Function, "post",
            Route = "workspaces/{sourceWorkspaceId}/reports/{reportId}/clone-semantic-model")] HttpRequest req,
            string sourceWorkspaceId,
            string reportId)
        {
            _logger.LogInformation("Processing cloning semantic model request.");

            try
            {
                string accessToken = await FBConfigManager.GetAccessToken();

                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var cloneRequest = JsonSerializer.Deserialize<CloneSemanticModelDTO>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Validate that sourceWorkspaceId, modelReportId, modelName, and targetWorkspaceId are not null or empty
                if (string.IsNullOrEmpty(sourceWorkspaceId) ||
                   string.IsNullOrEmpty(reportId) ||
                   string.IsNullOrEmpty(cloneRequest?.modelName) ||
                   string.IsNullOrEmpty(cloneRequest?.targetWorkspaceId))
                {
                    return new BadRequestObjectResult("sourceWorkspaceId, modelReportId, modelName, and targetWorkspaceId must be provided and cannot be null or empty.");
                }

                // Export semantic model
                string? modelPath = await _exportFunctions.ExportSemanticModelAsync(
                    sourceWorkspaceId,
                    reportId,
                    accessToken);

                return new OkObjectResult(await _uploadFunctions.UploadSemanticModelAsync(
                    cloneRequest.targetWorkspaceId,
                    cloneRequest.modelName,
                    modelPath,
                    accessToken));

                //_logger.LogInformation($"Successfully cloned semantic model. Deleted auto-generated report ID: {deletedReportID}");

                //return new OkObjectResult(new { DeletedReportId = deletedReportID });

            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while uploading the report: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
