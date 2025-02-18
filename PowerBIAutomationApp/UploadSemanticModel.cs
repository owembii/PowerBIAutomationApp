using System.Net.Http.Headers;
using PBIFunctionApp.DTO;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Net;

namespace PBIFunctionApp
{
    public class UploadSemanticModel
    {
        private readonly ILogger<UploadSemanticModel> _logger;
        private readonly ILogger<GetAccessKey> _accessKeyLogger;

        public UploadSemanticModel(ILogger<UploadSemanticModel> logger, ILogger<GetAccessKey> accessKeyLogger)
        {
            _logger = logger;
            _accessKeyLogger = accessKeyLogger;
        }

        [Function("UploadSemanticModel")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "targetWorkspaceId={targetWorkspaceId}&semanticModelName={modelName}")] HttpRequest req)
        {
            _logger.LogInformation("Processing upload semantic model request.");

            try
            {
                var authProvider = new GetAccessKey(_accessKeyLogger);
                string accessToken = await authProvider.GetAccessToken();

                string? targetWorkspaceId = req.Query["targetWorkspaceId"];
                string? semanticModelName = req.Query["semanticModelName"];

                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var uploadRequest = JsonSerializer.Deserialize<UploadSemanticModelDTO>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Validate that targetWorkspaceId, semanticModelName, and semanticModelPath are not null or empty
                if (string.IsNullOrEmpty(targetWorkspaceId) ||
                   string.IsNullOrEmpty(semanticModelName) ||
                   string.IsNullOrEmpty(uploadRequest?.semanticModelPath))
                {
                    return new BadRequestObjectResult("targetWorkspaceId, semanticModelName, and semanticModelPath must be provided and cannot be null or empty.");
                }

                // Upload semantic model
                string? uploadSemanticModel = await UploadSemanticModelAsync(
                    targetWorkspaceId,
                    semanticModelName,
                    uploadRequest.semanticModelPath,
                    accessToken);

                return !string.IsNullOrEmpty(uploadSemanticModel) ?
                    new OkObjectResult($"Successfuly uploaded semantic status code: {uploadSemanticModel}") :
                    new BadRequestObjectResult("Failed to upload semantic model");
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while uploading the report: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<string?> UploadSemanticModelAsync(
            string targetWorkspaceId,
            string semanticModelName,
            string semanticModelPath,
            string accessToken)
        {
            string uploadSemanticUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{targetWorkspaceId}/imports?datasetDisplayName={semanticModelName}";

            using (var client = new HttpClient())
            {
                // Set Authorization Header
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                try
                {
                    using (var fileStream = new FileStream(@semanticModelPath, FileMode.Open, FileAccess.Read))
                    {
                        using (var content = new MultipartFormDataContent())
                        {
                            // Create the file content
                            var fileContent = new StreamContent(fileStream);
                            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                            content.Add(fileContent, "file", Path.GetFileName(semanticModelPath));

                            HttpResponseMessage response = await client.PostAsync(uploadSemanticUrl, content);

                            if (response.IsSuccessStatusCode)
                            {
                                string logMessage = response.StatusCode == HttpStatusCode.OK
                                    ? "Upload successful"
                                    : "Upload in queue";

                                _logger.LogInformation(logMessage);
                            }
                            else
                            {
                                string errorResponse = await response.Content.ReadAsStringAsync();
                                _logger.LogError($"Failed to upload model. Status Code: {response.StatusCode}, Response: {errorResponse}");
                            }

                            return response.StatusCode.ToString();
                        }
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

    }
}
