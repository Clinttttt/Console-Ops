namespace ConsoleOps.Api.Security;

/// <summary>
/// Requires the configured key on API requests, and does nothing when no key is configured.
/// </summary>
public sealed class ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly string? configuredKey = configuration["Api:Key"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ApiKeyAuthentication.RequiresKey(configuredKey, context.Request.Path))
        {
            await next(context);
            return;
        }

        string? presented = context.Request.Headers[ApiKeyAuthentication.HeaderName];
        if (ApiKeyAuthentication.IsKeyValid(configuredKey, presented))
        {
            await next(context);
            return;
        }

        // The response says a key is required and nothing about the key itself.
        await Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: $"This API requires the {ApiKeyAuthentication.HeaderName} header.",
                extensions: new Dictionary<string, object?> { ["code"] = "Api.KeyRequired" })
            .ExecuteAsync(context);
    }
}
