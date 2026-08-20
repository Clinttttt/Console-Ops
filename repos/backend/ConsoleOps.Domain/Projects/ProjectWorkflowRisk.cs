namespace ConsoleOps.Domain.Projects;

/// <summary>
/// An operator's decision about how much intent one workflow's execution should require.
/// </summary>
/// <remarks>
/// Keyed by the workflow's path rather than by a provider id, because the path is what an operator recognises and
/// what survives a repository being re-created. A provider workflow id changes in that case; the file does not.
/// </remarks>
public sealed class ProjectWorkflowRisk
{
    private ProjectWorkflowRisk()
    {
    }

    private ProjectWorkflowRisk(
        Guid id,
        string workflowPath,
        WorkflowRiskLevel level,
        DateTimeOffset decidedAtUtc)
    {
        Id = id;
        WorkflowPath = workflowPath;
        NormalizedWorkflowPath = ProjectRules.Normalize(workflowPath);
        Level = level;
        DecidedAtUtc = decidedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    /// <summary>The definition path as the provider reports it, such as <c>.github/workflows/ci.yml</c>.</summary>
    public string WorkflowPath { get; private set; } = string.Empty;

    public string NormalizedWorkflowPath { get; private set; } = string.Empty;

    public WorkflowRiskLevel Level { get; private set; }

    /// <summary>When an operator decided this, so a screen can say the marking is theirs and how old it is.</summary>
    public DateTimeOffset DecidedAtUtc { get; private set; }

    internal static ProjectWorkflowRisk Create(
        Guid id,
        string workflowPath,
        WorkflowRiskLevel level,
        DateTimeOffset decidedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);

        string trimmed = (workflowPath ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("A workflow path is required.", nameof(workflowPath));
        }

        if (trimmed.Length > ProjectRules.WorkflowPathMaxLength)
        {
            throw new ArgumentException(
                $"A workflow path must be {ProjectRules.WorkflowPathMaxLength} characters or fewer.",
                nameof(workflowPath));
        }

        if (level == WorkflowRiskLevel.Unclassified)
        {
            // Unclassified is the absence of a decision, so it is recorded by removing the row rather than by
            // storing one that says nothing.
            throw new ArgumentException(
                "Unclassified is the absence of a marking and is not stored.",
                nameof(level));
        }

        return new ProjectWorkflowRisk(id, trimmed, level, decidedAtUtc);
    }

    internal void ChangeLevel(WorkflowRiskLevel level, DateTimeOffset decidedAtUtc)
    {
        if (level == WorkflowRiskLevel.Unclassified)
        {
            throw new ArgumentException(
                "Unclassified is the absence of a marking and is not stored.",
                nameof(level));
        }

        Level = level;
        DecidedAtUtc = decidedAtUtc;
    }
}
