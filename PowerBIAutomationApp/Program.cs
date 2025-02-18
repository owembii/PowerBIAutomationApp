using PBIFunctionApp;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Register dependencies
builder.Services.AddSingleton<GetAccessKey>();
builder.Services.AddSingleton<CloneReport>();
builder.Services.AddHttpClient();



builder.Build().Run();
