using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace PowerBIAutomationApp
{
    public class UpdateParameters
    {
        private readonly ILogger<UpdateParameters> _logger;
        private readonly ILogger<GetAccessKey> _accessKeyLogger;

        public UpdateParameters(ILogger<UpdateParameters> logger, ILogger<GetAccessKey> accessKeyLogger)
        {
            _logger = logger;
            _accessKeyLogger = accessKeyLogger;
        }

        [Function("UpdateParameters")]
        public async Task<IActionResult> Run([
            HttpTrigger(AuthorizationLevel.Function, "get", 
            Route = "semanticmodel/{modelId}/updateparameter")] HttpRequest req,
            string modelId)
        {
            _logger.LogInformation("Processing update dataset parameters request.");

            try
            {
                var authProvider = new GetAccessKey(_accessKeyLogger);
                string accessToken = await authProvider.GetAccessToken();

                _logger.LogInformation($"Updating parameters for dataset: {modelId}");

                // Update the dataset parameters
                string result = await UpdateParametersAsync(
                    modelId,
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

        private async Task<string> UpdateParametersAsync(string datasetID, string accessToken)
        {
            using (HttpClient client = new HttpClient())
            {
                string datasetsUrl = $"https://api.powerbi.com/v1.0/myorg/datasets/{datasetID}/Default.UpdateParameters";

                // Set Authorization Header
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var requestBody = new
                {
                    updateDetails = new[]
                    {
                        new
                        {
                            name = "OrganizationID",
                            newValue = 1
                        }
                    }
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(datasetsUrl, jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to update dataset parameters: {errorResponse}");
                }

                return "Success";
            }
        }
    }
}
