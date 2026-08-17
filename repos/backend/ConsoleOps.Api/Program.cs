using ConsoleOps.Api.Extensions;
using ConsoleOps.Application;
using ConsoleOps.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddConsoleOpsProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddConsoleOpsRateLimiter();
builder.Services.AddScheduledCollection(builder.Configuration);

WebApplication app = builder.Build();

app.EnsureSafeExposure(builder.Configuration);
app.UseConsoleOpsPipeline();
app.MapConsoleOpsEndpoints();

app.Run();

public partial class Program;
