using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;
using PBIFunctionApp;
using Microsoft.Azure.Functions.Worker;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net.Http.Headers;


namespace PBIFunctionApp
{
    public class GetSemanticModelParameterValue
    {
        private readonly ILogger<GetSemanticModelParameterValue> _logger;
        private readonly ILogger<GetAccessKey> _accessKeyLogger;
        private readonly HttpClient _httpClient;

        public GetSemanticModelParameterValue(ILogger<GetSemanticModelParameterValue> logger, ILogger<GetAccessKey> accessKeyLogger, HttpClient httpClient)
        {
            _logger = logger;
            _accessKeyLogger = accessKeyLogger;
            _httpClient = httpClient;
        }

        [Function("GetSemanticModelParameterValue")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "workspace/{workspaceId}/semanticmodel/{modelId}/parameters")] HttpRequestData req,
            string workspaceId, string modelId)
        {
            _logger.LogInformation($"Retrieving parameters for semantic model {modelId} in workspace {workspaceId}...");

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

            try
            {
                string parametersUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/datasets/{modelId}/parameters";

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                HttpResponseMessage response = await _httpClient.GetAsync(parametersUrl);
                string responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Error retrieving parameters: {responseJson}");
                    var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync($"Error retrieving parameters: {responseJson}");
                    return errorResponse;

                }

                var successResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await successResponse.WriteStringAsync(responseJson);
                return successResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving semantic model parameters: {ex.Message}");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error retrieving semantic model parameters: {ex.Message}");
                return errorResponse;
            }
        }
    }


}
