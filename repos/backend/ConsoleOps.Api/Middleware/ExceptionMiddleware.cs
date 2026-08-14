using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace ConsoleOps.Api.Middleware;

public sealed class ExceptionMiddleware(
    RequestDelegate next,
    ILogger<ExceptionMiddleware> logger,
    IProblemDetailsService problemDetailsService)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (ValidationException exception)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteValidationProblemAsync(context, exception);
        }
        catch (BadHttpRequestException)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Invalid request",
                "The request body could not be read.");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unhandled exception while processing request {TraceId}.",
                context.TraceIdentifier);

            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred",
                "The server could not complete the request.");
        }
    }

    private async Task WriteValidationProblemAsync(HttpContext context, ValidationException exception)
    {
        Dictionary<string, string[]> errors = exception.Errors
            .GroupBy(error => ToCamelCase(error.PropertyName))
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).Distinct().ToArray());

        HttpValidationProblemDetails details = new(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed"
        };

        await WriteAsync(context, details);
    }

    private Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail) =>
        WriteAsync(context, new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        });

    private async Task WriteAsync(HttpContext context, ProblemDetails details)
    {
        context.Response.StatusCode = details.Status ?? StatusCodes.Status500InternalServerError;

        ProblemDetailsContext problemContext = new()
        {
            HttpContext = context,
            ProblemDetails = details
        };

        if (!await problemDetailsService.TryWriteAsync(problemContext))
        {
            await context.Response.WriteAsJsonAsync(details, context.RequestAborted);
        }
    }

    private static string ToCamelCase(string value) => string.IsNullOrEmpty(value)
        ? value
        : char.ToLowerInvariant(value[0]) + value[1..];
}
