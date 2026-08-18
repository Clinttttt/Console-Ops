using ConsoleOps.Api.Features.Logs;
using ConsoleOps.Api.Features.Settings;
using ConsoleOps.Api.Features.Projects;
using Microsoft.AspNetCore.RateLimiting;

namespace ConsoleOps.Api.Extensions;

/// <summary>
/// Per-client limits on the endpoints that reach outside Console Ops during a request.
/// </summary>
public static class RateLimiterExtensions
{
    /// <summary>
    /// Endpoint verification probes URLs the operator typed, so a caller cannot use it as a scanner.
    /// </summary>
    private const int VerificationPermitsPerMinute = 10;

    /// <summary>
    /// Reading logs queries Azure during the request, which costs money and provider quota.
    /// </summary>
    private const int LogReadPermitsPerMinute = 60;

    /// <summary>A sweep contacts every configured provider for every project, so it is the costliest read.</summary>
    private const int SweepPermitsPerMinute = 5;

    public static IServiceCollection AddConsoleOpsRateLimiter(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindow(
                VerifyProjectEndpointsEndpoint.RateLimitPolicy,
                VerificationPermitsPerMinute);
            options.AddFixedWindow(LogEndpoints.RateLimitPolicy, LogReadPermitsPerMinute);
            options.AddFixedWindow(SettingsEndpoints.SweepRateLimitPolicy, SweepPermitsPerMinute);
        });

        return services;
    }

    /// <summary>
    /// A one-minute window with no queue: a caller over the limit is told immediately rather than parked.
    /// </summary>
    private static void AddFixedWindow(
        this RateLimiterOptions options,
        string policy,
        int permitsPerMinute) =>
        options.AddFixedWindowLimiter(policy, limiter =>
        {
            limiter.PermitLimit = permitsPerMinute;
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.QueueLimit = 0;
        });
}
