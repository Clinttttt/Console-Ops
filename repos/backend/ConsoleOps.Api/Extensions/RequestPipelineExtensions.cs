using ConsoleOps.Api.Features.Authentication;
using ConsoleOps.Api.Features.Azure;
using ConsoleOps.Api.Features.Dashboard;
using ConsoleOps.Api.Features.Deployments;
using ConsoleOps.Api.Features.GitHub;
using ConsoleOps.Api.Features.Health;
using ConsoleOps.Api.Features.Workflows;
using ConsoleOps.Api.Features.Logs;
using ConsoleOps.Api.Features.Projects;
using ConsoleOps.Api.Features.Settings;
using ConsoleOps.Api.Middleware;
using ConsoleOps.Api.Security;

namespace ConsoleOps.Api.Extensions;

/// <summary>
/// The request pipeline and the endpoint map, kept in one place so their order is reviewable.
/// </summary>
public static class RequestPipelineExtensions
{
    /// <summary>
    /// Order matters: the exception handler wraps everything, the authentication check runs before any endpoint work,
    /// and rate limiting runs before an endpoint can reach a provider.
    /// </summary>
    public static WebApplication UseConsoleOpsPipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseSwagger();
        app.UseSwaggerUI();
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseMiddleware<ApiAuthenticationMiddleware>();
        app.UseStatusCodePages();
        app.UseRateLimiter();
        app.UseHealthChecks("/health");
        return app;
    }

    public static WebApplication MapConsoleOpsEndpoints(this WebApplication app)
    {
        app.MapAuthenticationEndpoints();
        app.MapDashboardEndpoints();
        app.MapAzureEndpoints();
        app.MapDeploymentEndpoints();
        app.MapGitHubEndpoints();
        app.MapHealthEndpoints();
        app.MapWorkflowEndpoints();
        app.MapLogEndpoints();
        app.MapProjectEndpoints();
        app.MapSettingsEndpoints();

        return app;
    }
}
