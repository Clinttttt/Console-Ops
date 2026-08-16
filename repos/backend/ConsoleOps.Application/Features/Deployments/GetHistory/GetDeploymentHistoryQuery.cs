using MediatR;

namespace ConsoleOps.Application.Features.Deployments.GetHistory;

/// <summary>
/// Reads recorded releases, newest first.
/// </summary>
/// <param name="Limit">
/// Maximum records to return. Clamped by the handler so a caller cannot ask for an unbounded page.
/// </param>
public sealed record GetDeploymentHistoryQuery(int? Limit = null)
    : IRequest<DeploymentHistoryResponse>;
