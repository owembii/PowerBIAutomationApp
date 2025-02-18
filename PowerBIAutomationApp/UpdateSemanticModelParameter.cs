using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PBIFunctionApp;
using PBIFunctionApp.DTO;


namespace PBIFunctionApp
{
    public class UpdateSemanticModelParameter
    {
        private readonly ILogger<UpdateSemanticModelParameter> _logger;
        private readonly ILogger<GetAccessKey> _accessKeyLogger;
        private readonly HttpClient _httpClient;

        public UpdateSemanticModelParameter(ILogger<UpdateSemanticModelParameter> logger, ILogger<GetAccessKey> accessKeyLogger)
        {
            _logger = logger;
            _accessKeyLogger = accessKeyLogger;
        }

        [Function("UpdateSemanticModelParameter")]
        public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "workspace/{workspaceId}/semanticmodel/{modelId}/updateparameter")] HttpRequestData req,
        string workspaceId, string modelId)
        {
            _logger.LogInformation($"Updating parameter for semantic model {modelId} in workspace {workspaceId}...");

            // Get the access token
            string accessToken;
            try
            {
                var authProvider = new GetAccessKey(_accessKeyLogger);
                accessToken = await authProvider.GetAccessToken();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting access token: {ex.Message}");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error retrieving access token: {ex.Message}");
                return errorResponse;
            }

            // Deserialize the request body
            UpdateSemanticModelParameterDTO? requestBody;
            try
            {
                requestBody = await JsonSerializer.DeserializeAsync<UpdateSemanticModelParameterDTO>(req.Body);

            }
            catch (Exception ex)
            {
                _logger.LogError($"Invalud request body: {ex.Message}");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Unable to parse JSON body");
                return errorResponse;
            }

            // Validate the request body
            if (requestBody == null || string.IsNullOrEmpty(requestBody.ParameterName) || string.IsNullOrEmpty(requestBody.NewValue))
            {
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request: Parameter name and new value are required.");
                return errorResponse;
            }


            string updateUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/datasets/{modelId}/UpdateParameters";


            var jsonBody = JsonSerializer.Serialize(new
            {
                updateDetails = new[]
                {
                    new
                    {
                        name = requestBody.ParameterName,
                        newValue = requestBody.NewValue
                    }
                }
            });



            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                _logger.LogInformation($"Sending update request to: {updateUrl}");

                HttpResponseMessage response = await _httpClient.PostAsync(updateUrl, content);
                string responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Error updating parameter: {responseJson}");
                    var errorResponse = req.CreateResponse(response.StatusCode);
                    await errorResponse.WriteStringAsync(responseJson);
                    return errorResponse;
                }

                var successResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await successResponse.WriteStringAsync("Parameter updated successfully.");
                return successResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating semantic model parameter: {ex.Message}");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error updating semantic model parameter: {ex.Message}");
                return errorResponse;
            }
        }
    }
}
