using System.Text.Json;
using ConsoleOps.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsoleOps.Tests.Integration.Middleware;

public sealed class ExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithUnexpectedException_ReturnsSafeProblemDetails()
    {
        ServiceCollection services = new();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        });
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IProblemDetailsService problemDetailsService = serviceProvider.GetRequiredService<IProblemDetailsService>();
        DefaultHttpContext context = new()
        {
            RequestServices = serviceProvider,
            TraceIdentifier = "test-trace"
        };
        context.Response.Body = new MemoryStream();
        ExceptionMiddleware middleware = new(
            _ => throw new InvalidOperationException("Sensitive internal detail"),
            NullLogger<ExceptionMiddleware>.Instance,
            problemDetailsService);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using JsonDocument body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("test-trace", body.RootElement.GetProperty("traceId").GetString());
        Assert.DoesNotContain("Sensitive internal detail", body.RootElement.GetRawText(), StringComparison.Ordinal);
    }
}
