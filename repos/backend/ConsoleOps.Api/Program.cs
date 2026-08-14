using System.Threading.RateLimiting;
using ConsoleOps.Api.Features.Dashboard;
using ConsoleOps.Api.Features.GitHub;
using ConsoleOps.Api.Features.Projects;
using ConsoleOps.Api.Middleware;
using ConsoleOps.Application;
using ConsoleOps.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Endpoint verification accepts operator-supplied targets, so it is bounded per client.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter(VerifyProjectEndpointsEndpoint.RateLimitPolicy, limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.MapDashboardEndpoints();
app.MapGitHubEndpoints();
app.MapProjectEndpoints();

app.Run();

public partial class Program;
