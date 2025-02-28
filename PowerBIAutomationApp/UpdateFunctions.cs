using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PowerBIAutomationApp.DTO;
using PowerBIAutomationApp.Utilities;

namespace PowerBIAutomationApp
{
    public class UpdateFunctions
    {
        private readonly ILogger<UpdateFunctions> _logger;
        private readonly HttpClient _httpClient;
        public UpdateFunctions(ILogger<UpdateFunctions> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }


        [Function("UpdateParameters")]
        public async Task<IActionResult> UpdateParameters([
            HttpTrigger(AuthorizationLevel.Function, "post",
            Route = "workspaces/{workspaceId}/semanticmodels/{semanticModelId}/update-parameters")] HttpRequest req,
            string workspaceId,
            string semanticModelId)
        {
            _logger.LogInformation("Processing update dataset parameters request.");

            try
            {
                string accessToken = await FBConfigManager.GetAccessToken();

                // Read and deserialize request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var updateRequest = new List<UpdateParametersDTO>();

                // Only deserialize when request body is not null or empty
                if (!string.IsNullOrWhiteSpace(requestBody))
                {
                    updateRequest = JsonSerializer.Deserialize<List<UpdateParametersDTO>>(requestBody, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }

                _logger.LogInformation($"Updating parameters for dataset: {semanticModelId}");

                // Update the dataset parameters
                string result = await UpdateParametersAsync(
                    workspaceId,
                    semanticModelId,
                    updateRequest,
                    accessToken);

                _logger.LogInformation($"Successfully updated dataset parameters: {result}");

                //return new OkObjectResult(new { ClonedReportId = newReportID });
                return new OkObjectResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while updating the dataset: {ex}");
                return new ObjectResult(new { Error = "Internal Server Error", Details = ex.Message })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }

        private async Task<string> UpdateParametersAsync(
            string workspaceId, 
            string semanticModelId,
            List<UpdateParametersDTO> updateRequest,
            string accessToken)
        {
            // Take over the semantic model
            await TakeOverSemanticModel(
                workspaceId,
                semanticModelId,
                accessToken);

            try
            {
                string datasetsUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/datasets/{semanticModelId}/Default.UpdateParameters";

                // Set Authorization Header
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var requestBody = new
                {
                    updateDetails = updateRequest
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync(datasetsUrl, jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to update dataset parameters: {errorResponse}");
                }

                return "Success";
            }
            catch (Exception)
            {
                throw;
            }
        }

        private async Task TakeOverSemanticModel(
            string workspaceId,
            string semanticModelId,
            string accessToken)
        {
            try
            {
                string datasetsUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/datasets/{semanticModelId}/Default.TakeOver";

                // Set Authorization Header
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = await _httpClient.PostAsync(datasetsUrl, null);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Takeover successful");
                }
                else
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to takeover model. Status Code: {response.StatusCode}, Response: {errorResponse}");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}