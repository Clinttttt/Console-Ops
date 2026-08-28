using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Integrations.AzureMonitor;
using ConsoleOps.Domain.Projects;
using MediatR;

namespace ConsoleOps.Application.Features.Azure.ListLogSources;

/// <param name="Query">Optional case-insensitive filter on the resource or resource group name.</param>
public sealed record ListAzureLogSourcesQuery(string? Query)
    : IRequest<Result<AzureLogSourcesResponse>>;

public sealed record AzureLogSourcesResponse(
    IReadOnlyList<AzureLogSourceResponse> Sources,
    bool HasMore);

/// <param name="Platform">
/// <c>containerApp</c> or <c>appService</c>. Logs are not read the same way for each, so the screen groups
/// by it rather than presenting one flat list of names.
/// </param>
/// <param name="WorkspaceId">
/// `null` when Console Ops could not establish where this resource's logs are sent. The UI must then say the
/// logs are not available rather than offering it as a source.
/// </param>
/// <param name="Status">
/// Why this resource can or cannot be used: <c>readable</c>, <c>noWorkspace</c>, or
/// <c>platformNotSupported</c>. Listing a resource Console Ops cannot read is deliberate - an operator who
/// cannot find their App Service has no way to tell "Azure does not have it" from "Console Ops does not look
/// for it" - but it must never be offered as though it would work.
/// </param>
/// <param name="ApplicationUrl">
/// The resource''s public address as Azure reports it, or <c>null</c> when it has none Console Ops could reach -
/// a container app whose ingress is internal resolves only inside its own network. Registering a project can
/// offer it instead of asking an operator to copy a generated host name by hand.
/// </param>
public sealed record AzureLogSourceResponse(
    string Provider,
    string Platform,
    string Name,
    string ResourceGroup,
    string SubscriptionId,
    string? Location,
    string? EnvironmentName,
    Guid? WorkspaceId,
    string? ApplicationUrl,
    string Status);

/// <summary>
/// Lists the Azure resources that host applications, so the operator can pick a log source rather than
/// typing a workspace GUID.
/// <para>
/// Discovery may prefill but never decide: this hands back what Azure reports, and choosing a source
/// remains an explicit act in the form.
/// </para>
/// </summary>
public sealed class ListAzureLogSourcesQueryHandler(IAzureLogSourceCatalog catalog)
    : IRequestHandler<ListAzureLogSourcesQuery, Result<AzureLogSourcesResponse>>
{
    public async Task<Result<AzureLogSourcesResponse>> Handle(
        ListAzureLogSourcesQuery request,
        CancellationToken cancellationToken)
    {
        AzureLogSourceCatalogResult result =
            await catalog.ListLogSourcesAsync(request.Query, cancellationToken);

        if (!result.IsSuccess)
        {
            return Result<AzureLogSourcesResponse>.Failure(
                AzureDiscoveryErrors.From(result.Failure ?? AzureCatalogFailure.Unavailable));
        }

        AzureLogSourceResponse[] sources = result.Sources
            .Select(source => new AzureLogSourceResponse(
                "azure",
                ToCamelCase(source.Platform),
                source.Name,
                source.ResourceGroup,
                source.SubscriptionId,
                source.Location,
                source.EnvironmentName,
                source.WorkspaceId,
                source.ApplicationUrl,
                Status(source)))
            .ToArray();

        return Result<AzureLogSourcesResponse>.Success(
            new AzureLogSourcesResponse(sources, result.HasMore));
    }

    /// <summary>
    /// Whether this resource could be used as a log source, and if not, which fact stands in the way. The
    /// platform is checked first: a workspace is irrelevant while nothing can read the platform anyway.
    /// </summary>
    private static string Status(AzureLogSourceCandidate source)
    {
        if (!AzureLogPlatformSupport.CanRead(source.Platform))
        {
            return "platformNotSupported";
        }

        return source.WorkspaceId is null ? "noWorkspace" : "readable";
    }

    private static string ToCamelCase(AzureLogPlatform platform)
    {
        string text = platform.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
