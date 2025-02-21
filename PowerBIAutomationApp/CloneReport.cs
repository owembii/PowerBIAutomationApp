using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PBIFunctionApp.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PowerBIAutomationApp;
using Microsoft.AspNetCore.Http.HttpResults;

namespace PBIFunctionApp
{
    public class CloneReport
    {
        private readonly ILogger<CloneReport> _logger;
        private readonly ILogger<GetAccessKey> _accessKeyLogger;

        public CloneReport(ILogger<CloneReport> logger, ILogger<GetAccessKey> accessKeyLogger)
        {
            _logger = logger;
            _accessKeyLogger = accessKeyLogger;
        }

        [Function("CloneReport")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post",
            Route = "workspaces/{sourceWorkspaceId}/reports/{reportId}/clone-report")] HttpRequest req,
            string sourceWorkspaceId,
            string reportId)
        {
            _logger.LogInformation("Processing clone report request.");

            try
            {
                var authProvider = new GetAccessKey(_accessKeyLogger);
                string accessToken = await authProvider.GetAccessToken();

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
    }
}
