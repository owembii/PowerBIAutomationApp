using System.Net.Http.Headers;
using System.Text.Json;
using PBIFunctionApp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace PBIFunctionApp
{
    public class GetAllReports
    {
        private readonly ILogger<GetAllReports> _logger;
        private readonly ILogger<GetAccessKey> _accessKeyLogger;

        public GetAllReports(ILogger<GetAllReports> logger, ILogger<GetAccessKey> accessKeyLogger)
        {
            _logger = logger;
            _accessKeyLogger = accessKeyLogger;
        }

        [Function("GetAllReports")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "workspaces/{workspaceID}/reports")] HttpRequest req, string workspaceID)
        {
            _logger.LogInformation($"Fetching reports for workspace: {workspaceID}");

            try
            {
                if (string.IsNullOrEmpty(workspaceID))
                {
                    return new BadRequestObjectResult("Missing workspaceID parameter.");
                }

                // Get access token
                var authProvider = new GetAccessKey(_accessKeyLogger);
                string accessToken = await authProvider.GetAccessToken();

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
    }
}
