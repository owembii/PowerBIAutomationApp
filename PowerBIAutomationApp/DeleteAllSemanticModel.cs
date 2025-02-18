using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using PBIFunctionApp;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PBIFunctionApp.DTOs;
using PowerBIAutomationApp;

namespace PBIFunctionApp
{
    public class DeleteAllSemanticModels
    {
        private readonly ILogger<DeleteAllSemanticModels> _logger;
        private readonly ILogger<GetAccessKey> _accessKeyLogger;
        private readonly HttpClient _httpClient;

        public DeleteAllSemanticModels(ILogger<DeleteAllSemanticModels> logger, ILogger<GetAccessKey> accessKeyLogger, HttpClient httpClient)
        {
            _logger = logger;
            _accessKeyLogger = accessKeyLogger;
            _httpClient = httpClient;
        }

        [Function("DeleteAllSemanticModels")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "delete",
            Route = "workspace/{workspaceId}/semanticmodels/delete")] HttpRequestData req,
            string workspaceId)
        {
            _logger.LogInformation($"Deleting all semantic models in workspace: {workspaceId}");

            string accessToken;
            try
            {
                var authProvider = new GetAccessKey(_accessKeyLogger);
                accessToken = await authProvider.GetAccessToken();
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
                var responseObj = JsonSerializer.Deserialize<SemanticModelListResponse>(responseJson, options);
                var models = responseObj?.Value ?? new List<SemanticModel>();

                // NO MODELS FOUND HANDLER
                if (models.Count == 0)
                {
                    _logger.LogWarning($"No semantic models found in workspace: {workspaceId}");
                    var noModelsResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                    await noModelsResponse.WriteStringAsync("No semantic models found.");
                    return noModelsResponse;
                }

                // LOOPS THROUGH DELETING EACH SEMANTIC MODEL
                foreach (var model in models)
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

        private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, string message, Exception ex)
        {
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"{message} Details: {ex.Message}");
            return errorResponse;
        }
    }
}
