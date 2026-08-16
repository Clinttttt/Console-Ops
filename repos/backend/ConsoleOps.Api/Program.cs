using System.Threading.RateLimiting;
using ConsoleOps.Api.BackgroundServices;
using ConsoleOps.Api.Features.Azure;
using ConsoleOps.Api.Features.Dashboard;
using ConsoleOps.Api.Features.Deployments;
using ConsoleOps.Api.Features.GitHub;
using ConsoleOps.Api.Features.Logs;
using ConsoleOps.Api.Features.Projects;
using ConsoleOps.Api.Middleware;
using ConsoleOps.Api.Security;
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

    // Reading logs asks Azure during the request, so it is bounded per client as well as per query.
    options.AddFixedWindowLimiter(LogEndpoints.RateLimitPolicy, limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

// Collect observations on a schedule so the screens are current without the operator pressing refresh.
// The worker sends the same command the manual endpoint does; it is registered only when enabled, so a
// deployment that wants collection strictly on demand simply turns it off.
builder.Services.Configure<ProjectRefreshOptions>(
    builder.Configuration.GetSection(ProjectRefreshOptions.SectionName));
ProjectRefreshOptions refreshOptions = new();
builder.Configuration.GetSection(ProjectRefreshOptions.SectionName).Bind(refreshOptions);

if (refreshOptions.Enabled)
{
    builder.Services.AddHostedService<ProjectRefreshWorker>();
}

WebApplication app = builder.Build();

// Console Ops has no user accounts by design, which is safe only while it answers on loopback. If it is
// bound anywhere else, it must not start without a configured key: its endpoints expose repository names
// and probe operator-supplied URLs.
string[] boundUrls = app.Urls.Count > 0
    ? [.. app.Urls]
    : [builder.Configuration["urls"] ?? builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost"];

if (!NetworkExposure.IsLoopbackOnly(boundUrls)
    && string.IsNullOrWhiteSpace(builder.Configuration["Api:Key"]))
{
    throw new InvalidOperationException(
        "Console Ops is bound to a non-loopback address without 'Api:Key' configured. Set Api:Key "
        + "(user-secrets or environment) so requests must send the "
        + $"{ApiKeyAuthentication.HeaderName} header, or bind to localhost only.");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.MapDashboardEndpoints();
app.MapAzureEndpoints();
app.MapDeploymentEndpoints();
app.MapGitHubEndpoints();
app.MapLogEndpoints();
app.MapProjectEndpoints();

app.Run();

public partial class Program;
