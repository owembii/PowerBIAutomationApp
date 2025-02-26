using System.Net.Http.Headers;
using FBFunctionServiceBL.UtilityVault;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace PowerBIAutomationApp
{
    public class GetAllSemanticModels
    {
        private readonly ILogger<GetAllSemanticModels> _logger;

        public GetAllSemanticModels(ILogger<GetAllSemanticModels> logger)
        {
            _logger = logger;
        }

        [Function("GetSemanticModels")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "workspaces/{workspaceID}/semanticmodels")] HttpRequest req, string workspaceID)
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
    }
}
