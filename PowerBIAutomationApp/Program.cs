using PowerBIAutomationApp;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Register dependencies
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ExportFunctions>();
builder.Services.AddSingleton<UploadFunctions>();
builder.Services.AddSingleton<GetFunctions>();
builder.Build().Run();

