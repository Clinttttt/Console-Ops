using System.Globalization;
using ConsoleOps.Application.Features.Workflows.GetInventory;
using ConsoleOps.Application.Integrations.GitHub;

namespace ConsoleOps.Application.Features.Workflows;

/// <summary>
/// How a provider run becomes what a screen is told.
/// </summary>
/// <remarks>
/// One place, because the inventory, the run history and the job list all describe the same run and three copies
/// of this projection would eventually disagree about what a run with no end means. Extracted when the second
/// caller appeared rather than after the third.
/// </remarks>
internal static class WorkflowRunMapping
{
    internal static WorkflowRunResponse? ToRun(GitHubRunSummary? run)
    {
        if (run is null)
        {
            return null;
        }

        return new WorkflowRunResponse(
            run.RunId.ToString(CultureInfo.InvariantCulture),
            run.Number,
            ToCamelCase(run.Status),
            run.Conclusion is null ? null : ToCamelCase(run.Conclusion.Value),
            run.Branch,
            run.CommitSha,
            ShortSha(run.CommitSha),
            run.Event,
            run.Actor,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            DurationSeconds(run.StartedAtUtc, run.CompletedAtUtc),
            run.RunUrl,
            // Jobs cost a request per run, so they are read for the run an operator opens rather than for
            // every run in a list.
            []);
    }

    internal static WorkflowRunJobResponse ToJob(GitHubRunJob job) =>
        new(
            job.Name,
            ToCamelCase(job.Status),
            job.Conclusion is null ? null : ToCamelCase(job.Conclusion.Value),
            DurationSeconds(job.StartedAtUtc, job.CompletedAtUtc),
            FailedStepOf(job),
            job.Steps.Select(ToStep).ToArray());

    private static WorkflowRunStepResponse ToStep(GitHubRunStep step) =>
        new(
            step.Name,
            step.Number,
            ToCamelCase(step.Status),
            step.Conclusion is null ? null : ToCamelCase(step.Conclusion.Value),
            DurationSeconds(step.StartedAtUtc, step.CompletedAtUtc));

    /// <summary>
    /// The first step the provider reported as failed, or <c>null</c> when none did.
    /// </summary>
    /// <remarks>
    /// First rather than last, because a failure cascades: the step that broke is the one worth naming, and the
    /// ones after it failed or were skipped because of it. A job that failed with no failing step - a runner that
    /// died, a cancelled queue - names nothing rather than blaming a step that reported success.
    /// </remarks>
    private static string? FailedStepOf(GitHubRunJob job) =>
        job.Steps
            .FirstOrDefault(step => step.Conclusion
                is GitHubRunConclusion.Failed
                or GitHubRunConclusion.TimedOut
                or GitHubRunConclusion.ActionRequired)
            ?.Name;

    /// <summary>
    /// The elapsed time a run or job reports, or <c>null</c> when either end is missing.
    /// </summary>
    /// <remarks>
    /// A run still going has no duration. Substituting "now minus started" would report an elapsed time as a
    /// final one, and a negative span means the provider's own timestamps disagree - neither is worth a number.
    /// </remarks>
    internal static int? DurationSeconds(DateTimeOffset? startedAt, DateTimeOffset? completedAt)
    {
        if (startedAt is null || completedAt is null || completedAt < startedAt)
        {
            return null;
        }

        return (int)(completedAt.Value - startedAt.Value).TotalSeconds;
    }

    /// <summary>Enums cross the wire as camelCase strings, as everywhere else in the contract.</summary>
    internal static string ToCamelCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string name = value.ToString()!;
        return string.Concat(char.ToLowerInvariant(name[0]), name[1..]);
    }

    private static string ShortSha(string commitSha) =>
        commitSha.Length <= 7 ? commitSha : commitSha[..7];
}
