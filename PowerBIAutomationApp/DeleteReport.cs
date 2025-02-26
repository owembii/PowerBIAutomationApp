using System.Net.Http.Headers;
using FBFunctionServiceBL.UtilityVault;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace PowerBIAutomationApp
{
    public class DeleteReport
    {
        private readonly ILogger<DeleteReport> _logger;

        public DeleteReport(ILogger<DeleteReport> logger)
        {
            _logger = logger;
        }

        [Function("DeleteReport")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "workspaces/{workspaceID}/reports/{reportID}")] HttpRequest req,
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
    }
}
