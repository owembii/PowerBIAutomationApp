using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Identity.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace PBIFunctionApp
{
    public class GetAccessKey
    {
        private readonly ILogger<GetAccessKey> _logger;
        private readonly string? clientId = Environment.GetEnvironmentVariable("FBDEV_AzureClientID", EnvironmentVariableTarget.Process); // Application Id
        private readonly string? clientSecret = Environment.GetEnvironmentVariable("FBDEV_AzureClientSecret", EnvironmentVariableTarget.Process);
        private readonly string? tenantId = Environment.GetEnvironmentVariable("FBDEV_AzureTenantID", EnvironmentVariableTarget.Process); // Directory Id

        public GetAccessKey(ILogger<GetAccessKey> logger)
        {
            _logger = logger;
        }

        [Function("GetAccessKey")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req,
            FunctionContext executionContext)  // Updated to accept FunctionContext
        {
            string accessToken;

            try
            {
                accessToken = await GetAccessToken(); // Call the method to retrieve the access token
                return new OkObjectResult(accessToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while getting the access token: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        // Method to retrieve access token using Azure AD
        public async Task<string> GetAccessToken()
        {
            string authority = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
            string resource = "https://analysis.windows.net/powerbi/api/.default"; // Power BI API resource

            var app = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(new Uri(authority))
                .Build();

            var result = await app.AcquireTokenForClient(new[] { resource })
                                  .ExecuteAsync();

            return result.AccessToken;
        }
    }
}
