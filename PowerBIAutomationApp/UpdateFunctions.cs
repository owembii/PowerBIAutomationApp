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
            HttpTrigger(AuthorizationLevel.Function, "get",
            Route = "semanticmodel/{modelId}/updateparameter")] HttpRequest req,
            string modelId)
        {
            _logger.LogInformation("Processing update dataset parameters request.");

            try
            {
                string accessToken = await FBConfigManager.GetAccessToken();

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

        [Function("UpdateSemanticModelParameter")]
        public async Task<HttpResponseData> UpdateSemanticModelParameter([
            HttpTrigger(AuthorizationLevel.Function, "post", 
            Route = "workspace/{workspaceId}/semanticmodel/{modelId}/updateparameter")] HttpRequestData req,
            string workspaceId, 
            string modelId)
        {
            _logger.LogInformation($"Updating parameter for semantic model {modelId} in workspace {workspaceId}...");

            // Get the access token
            string accessToken;
            try
            {
                accessToken = await FBConfigManager.GetAccessToken();
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

        //[Function("UploadSemanticModel")]
        //public async Task<IActionResult> UploadSemanticModel([
        //    HttpTrigger(AuthorizationLevel.Function, "post", 
        //    Route = "targetWorkspaceId={targetWorkspaceId}&semanticModelName={modelName}")] HttpRequest req)
        //{
        //    _logger.LogInformation("Processing upload semantic model request.");

        //    try
        //    {
        //        string accessToken = await FBConfigManager.GetAccessToken();

        //        string? targetWorkspaceId = req.Query["targetWorkspaceId"];
        //        string? semanticModelName = req.Query["semanticModelName"];

        //        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        //        var uploadRequest = JsonSerializer.Deserialize<UploadSemanticModelDTO>(requestBody, new JsonSerializerOptions
        //        {
        //            PropertyNameCaseInsensitive = true
        //        });

        //        // Validate that targetWorkspaceId, semanticModelName, and semanticModelPath are not null or empty
        //        if (string.IsNullOrEmpty(targetWorkspaceId) ||
        //           string.IsNullOrEmpty(semanticModelName) ||
        //           string.IsNullOrEmpty(uploadRequest?.semanticModelPath))
        //        {
        //            return new BadRequestObjectResult("targetWorkspaceId, semanticModelName, and semanticModelPath must be provided and cannot be null or empty.");
        //        }

        //        // Upload semantic model
        //        string? uploadSemanticModel = await UploadSemanticModelAsync(
        //            targetWorkspaceId,
        //            semanticModelName,
        //            uploadRequest.semanticModelPath,
        //            accessToken);

        //        return !string.IsNullOrEmpty(uploadSemanticModel) ?
        //            new OkObjectResult($"Successfuly uploaded semantic status code: {uploadSemanticModel}") :
        //            new BadRequestObjectResult("Failed to upload semantic model");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError($"An error occurred while uploading the report: {ex.Message}");
        //        return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        //    }
        //}

        //public async Task<string?> UploadSemanticModelAsync(
        //    string targetWorkspaceId,
        //    string semanticModelName,
        //    string semanticModelPath,
        //    string accessToken)
        //{
        //    string uploadSemanticUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{targetWorkspaceId}/imports?datasetDisplayName={semanticModelName}";

        //    using (var client = new HttpClient())
        //    {
        //        // Set Authorization Header
        //        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        //        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        //        try
        //        {
        //            using (var fileStream = new FileStream(@semanticModelPath, FileMode.Open, FileAccess.Read))
        //            {
        //                using (var content = new MultipartFormDataContent())
        //                {
        //                    // Create the file content
        //                    var fileContent = new StreamContent(fileStream);
        //                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        //                    content.Add(fileContent, "file", Path.GetFileName(semanticModelPath));

        //                    HttpResponseMessage response = await client.PostAsync(uploadSemanticUrl, content);

        //                    if (response.IsSuccessStatusCode)
        //                    {
        //                        string logMessage = response.StatusCode == HttpStatusCode.OK
        //                            ? "Upload successful"
        //                            : "Upload in queue";

        //                        _logger.LogInformation(logMessage);
        //                    }
        //                    else
        //                    {
        //                        string errorResponse = await response.Content.ReadAsStringAsync();
        //                        _logger.LogError($"Failed to upload model. Status Code: {response.StatusCode}, Response: {errorResponse}");
        //                    }

        //                    return response.StatusCode.ToString();
        //                }
        //            }
        //        }
        //        catch (Exception)
        //        {
        //            throw;
        //        }
        //    }
        //}
    }
}
