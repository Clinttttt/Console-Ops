using ConsoleOps.Application.Features.Workflows;
using ConsoleOps.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace ConsoleOps.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads the risk markings operators have made across every project.
/// </summary>
/// <remarks>
/// One query for all of them, because the Workflows screen reads every project's inventory in one pass and asking
/// per project would add a query per repository to a request that already spends provider calls.
/// </remarks>
internal sealed class WorkflowRiskReadStore(ConsoleOpsDbContext dbContext) : IWorkflowRiskReadStore
{
    public async Task<IReadOnlyList<WorkflowRiskRecord>> ListAsync(CancellationToken cancellationToken)
    {
        List<ProjectWorkflowRisk> risks = await dbContext.ProjectWorkflowRisks
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return risks
            .Select(risk => new WorkflowRiskRecord(
                risk.ProjectId,
                risk.WorkflowPath,
                risk.NormalizedWorkflowPath,
                risk.Level.ToString().ToLowerInvariant(),
                risk.DecidedAtUtc))
            .ToList();
    }
}
