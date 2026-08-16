using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Integrations.AzureMonitor;
using MediatR;

namespace ConsoleOps.Application.Features.Azure.ListLogSources;

/// <param name="Query">Optional case-insensitive filter on the container app or resource group name.</param>
public sealed record ListAzureLogSourcesQuery(string? Query)
    : IRequest<Result<AzureLogSourcesResponse>>;

public sealed record AzureLogSourcesResponse(
    IReadOnlyList<AzureLogSourceResponse> ContainerApps,
    bool HasMore);

/// <param name="WorkspaceId">
/// `null` when the app's environment has no Log Analytics configuration. The UI must then say logs are not
/// available for it rather than offering it as a source.
/// </param>
public sealed record AzureLogSourceResponse(
    string Provider,
    string ContainerAppName,
    string ResourceGroup,
    string SubscriptionId,
    string? Location,
    string? EnvironmentName,
    Guid? WorkspaceId);

/// <summary>
/// Lists container apps so the operator can pick a log source rather than typing a workspace GUID.
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
            await catalog.ListContainerAppsAsync(request.Query, cancellationToken);

        if (!result.IsSuccess)
        {
            return Result<AzureLogSourcesResponse>.Failure(
                AzureDiscoveryErrors.From(result.Failure ?? AzureCatalogFailure.Unavailable));
        }

        AzureLogSourceResponse[] containerApps = result.ContainerApps
            .Select(app => new AzureLogSourceResponse(
                "azureContainerApps",
                app.Name,
                app.ResourceGroup,
                app.SubscriptionId,
                app.Location,
                app.EnvironmentName,
                app.WorkspaceId))
            .ToArray();

        return Result<AzureLogSourcesResponse>.Success(
            new AzureLogSourcesResponse(containerApps, result.HasMore));
    }
}
