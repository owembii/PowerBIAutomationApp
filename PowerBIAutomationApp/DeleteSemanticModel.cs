using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using PBIFunctionApp;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PowerBIAutomationApp;

namespace PBIFunctionApp
{
    public class DeleteSemanticModel
    {
        private readonly ILogger<DeleteSemanticModel> _logger;
        private readonly ILogger<GetAccessKey> _accessKeyLogger;
        private readonly HttpClient _httpClient;

        public DeleteSemanticModel(ILogger<DeleteSemanticModel> logger, ILogger<GetAccessKey> accessKeyLogger, HttpClient httpClient)
        {
            _logger = logger;
            _accessKeyLogger = accessKeyLogger;
            _httpClient = httpClient;
        }

        [Function("DeleteSemanticModel")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "workspace/{workspaceId}/semanticmodel/{modelId}")] HttpRequestData req,
            string workspaceId, string modelId)
        {
            _logger.LogInformation($"Deleting semantic model {modelId} in workspace {workspaceId}...");

            string accessToken;
            try
            {
                var authProvider = new GetAccessKey(_accessKeyLogger);
                accessToken = await authProvider.GetAccessToken();
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
    }
}
