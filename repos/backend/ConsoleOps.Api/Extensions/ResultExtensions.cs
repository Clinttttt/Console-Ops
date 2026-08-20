using ConsoleOps.Application.Abstractions.Messaging;

namespace ConsoleOps.Api.Extensions;

public static class ResultExtensions
{
  
    public static IResult ToHttpResult<TValue>(this Result<TValue> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();

    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();

    public static IResult ToProblemDetails(this Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("A successful result cannot be converted to a problem response.");
        }

        int statusCode = result.Error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Failure => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };

        return Results.Problem(
            statusCode: statusCode,
            title: GetTitle(result.Error.Type),
            detail: result.Error.Description,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = result.Error.Code
            });
    }

    private static string GetTitle(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => "Validation failed",
        ErrorType.NotFound => "Resource not found",
        ErrorType.Conflict => "Conflict",
            ErrorType.Forbidden => "Forbidden",
        _ => "Request failed"
    };
}
