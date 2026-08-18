namespace ConsoleOps.Api.Extensions;

/// <summary>
/// RFC 7807 problem responses, carrying the trace identifier so a reported failure can be found in the logs.
/// </summary>
public static class ProblemDetailsExtensions
{
    public static IServiceCollection AddConsoleOpsProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        });

        return services;
    }
}
