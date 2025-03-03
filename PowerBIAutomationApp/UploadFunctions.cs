using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PowerBIAutomationApp.DTO;
using PowerBIAutomationApp.Utilities;

namespace PowerBIAutomationApp
{
    public class UploadFunctions
    {
        private readonly ILogger<UploadFunctions> _logger;
        public UploadFunctions(ILogger<UploadFunctions> logger)
        {
            _logger = logger;
        }

        [Function("UploadSemanticModel")]
        public async Task<IActionResult> UploadSemanticModel([
            HttpTrigger(AuthorizationLevel.Function, "post",
            Route = "workspaces/{workspaceId}/upload-semantic-model")] HttpRequest req,
            string workspaceId)
        {
            _logger.LogInformation("Processing upload semantic model request.");

            try
            {
                string accessToken = await FBConfigManager.GetAccessToken();

                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var uploadRequest = JsonSerializer.Deserialize<UploadSemanticModelDTO>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Validate that targetWorkspaceId, semanticModelName, and semanticModelPath are not null or empty
                if (string.IsNullOrEmpty(workspaceId) ||
                   string.IsNullOrEmpty(uploadRequest?.name) ||
                   string.IsNullOrEmpty(uploadRequest?.semanticModelPath))
                {
                    return new BadRequestObjectResult("targetWorkspaceId, semanticModelName, and semanticModelPath must be provided and cannot be null or empty.");
                }

                // Upload semantic model
                string? uploadSemanticModel = await UploadSemanticModelAsync(
                    workspaceId,
                    uploadRequest.name,
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
            string workspaceId,
            string semanticModelName,
            string semanticModelPath,
            string accessToken)
        {
            string uploadSemanticUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/imports?datasetDisplayName={semanticModelName}&skipReport=true";

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
