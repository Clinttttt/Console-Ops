using ConsoleOps.Api.Extensions;
using ConsoleOps.Application;
using ConsoleOps.Infrastructure;
using Microsoft.OpenApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddConsoleOpsProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOperatorGitHubCredential();
builder.Services.AddConsoleOpsRateLimiter();
builder.Services.AddScheduledCollection(builder.Configuration);
builder.Services.AddConsoleOpsConfigurationInspection();
builder.Services.AddSwagger();
builder.Services.AddHealthChecks();

WebApplication app = builder.Build();



app.EnsureSafeExposure(builder.Configuration);
app.UseConsoleOpsPipeline();
app.MapConsoleOpsEndpoints();

app.Run();

public partial class Program;
