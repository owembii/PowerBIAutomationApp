using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PowerBIAutomationApp.Utilities;

namespace PowerBIAutomationApp
{
    public class ExportFunctions
    {
        private readonly ILogger<ExportFunctions> _logger;
        // Local My Documents folder
        private readonly string pbixPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public ExportFunctions(ILogger<ExportFunctions> logger)
        {
            _logger = logger;
        }

        [Function("ExportSemanticModel")]
        public async Task<IActionResult> ExportSemanticModel([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
        {
            _logger.LogInformation("Processing export semantic model request.");

            try
            {
                string accessToken = await FBConfigManager.GetAccessToken();

                // string? can hold a null value if the parameter is missing
                string? workspaceId = req.Query["workspaceId"];
                string? modelReportId = req.Query["modelReportId"];

                // Validate that workspaceId and modelReportId are not null or empty
                if (string.IsNullOrEmpty(workspaceId) || string.IsNullOrEmpty(modelReportId))
                {
                    return new BadRequestObjectResult("workspaceId and modelReportId must be provided and cannot be null or empty.");
                }

                // Export semantic model
                string? exportSemanticModel = await ExportSemanticModelAsync(
                    workspaceId,
                    modelReportId,
                    accessToken);

                // Return exported file path
                return new OkObjectResult(exportSemanticModel);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while exporting the report: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<string?> ExportSemanticModelAsync(
            string workspaceId,
            string modelReportId,
            string accessToken)
        {
            using (HttpClient client = new HttpClient())
            {
                string exportSemanticUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/reports/{modelReportId}/Export?downloadType=IncludeModel";

                // Set Authorization Header
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                try
                {
                    HttpResponseMessage response = await client.GetAsync(exportSemanticUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        byte[] fileBytes = await response.Content.ReadAsByteArrayAsync();
                        // Unique filename using Guid
                        string exportedFile = Path.Combine(pbixPath, $"{Guid.NewGuid()}.pbix");
                        await File.WriteAllBytesAsync(exportedFile, fileBytes);

                        _logger.LogInformation($"Export successful at '{exportedFile}'");

                        // Replace single backslashes with double backslashes
                        return exportedFile.Replace("\\", "\\\\");
                    }
                    else
                    {
                        string errorStatusCode = response.StatusCode.ToString();
                        var errorResponse = await response.Content.ReadAsStringAsync();

                        _logger.LogError($"Failed to export model. Status Code: {errorStatusCode}, Response: {errorResponse}");
                        return null;
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
