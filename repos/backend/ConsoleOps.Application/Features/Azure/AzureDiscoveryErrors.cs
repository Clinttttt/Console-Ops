using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Integrations.AzureMonitor;

namespace ConsoleOps.Application.Features.Azure;

/// <summary>
/// Maps an Azure read failure to a stable application error.
/// </summary>
/// <remarks>
/// The descriptions name what went wrong and what to check, without leaking the credential, the target
/// URL, or a raw provider payload. A missing or rejected credential is a Console Ops configuration fault
/// rather than the caller's mistake, so it is reported as a failure rather than as invalid input.
/// </remarks>
internal static class AzureDiscoveryErrors
{
    public static Error From(AzureCatalogFailure failure) => failure switch
    {
        AzureCatalogFailure.NotFound => new Error(
            "Azure.NotFound",
            "Azure does not expose those resources to the configured credential.",
            ErrorType.NotFound),
        AzureCatalogFailure.Unauthorized => new Error(
            "Azure.Unauthorized",
            "Azure rejected the configured credential. Console Ops needs a signed-in Azure identity with "
            + "read access to the subscription.",
            ErrorType.Failure),
        AzureCatalogFailure.RateLimited => new Error(
            "Azure.RateLimited",
            "Azure rate limited the request. Try again shortly.",
            ErrorType.Failure),
        AzureCatalogFailure.InvalidResponse => new Error(
            "Azure.InvalidResponse",
            "Azure returned a response Console Ops could not read.",
            ErrorType.Failure),
        _ => new Error(
            "Azure.Unavailable",
            "Azure could not be reached.",
            ErrorType.Failure)
    };
}
