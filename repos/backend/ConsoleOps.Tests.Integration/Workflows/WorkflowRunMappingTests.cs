using ConsoleOps.Application.Features.Workflows;
using ConsoleOps.Application.Features.Workflows.GetInventory;
using ConsoleOps.Application.Integrations.GitHub;

namespace ConsoleOps.Tests.Integration.Workflows;

/// <summary>
/// How a job's steps become what a screen is told.
/// </summary>
/// <remarks>
/// The point of naming a failing step is to answer "where did this break" without opening the provider, so the
/// rule that matters is which step gets named - and that a job which failed without a failing step names none.
/// </remarks>
public sealed class WorkflowRunMappingTests
{
    [Fact]
    public void ToJob_NamesTheFirstFailingStep()
    {
        WorkflowRunJobResponse job = WorkflowRunMapping.ToJob(Job(
            GitHubRunConclusion.Failed,
            Step("Checkout", GitHubRunConclusion.Passed),
            Step("Build", GitHubRunConclusion.Failed),
            // A later failure is a consequence of the first, so the first is the one worth naming.
            Step("Test", GitHubRunConclusion.Failed)));

        Assert.Equal("Build", job.FailedStep);
        Assert.Equal(3, job.Steps.Count);
    }

    [Fact]
    public void ToJob_NamesAStepThatTimedOutOrNeedsAnOperator()
    {
        Assert.Equal(
            "Deploy",
            WorkflowRunMapping.ToJob(Job(GitHubRunConclusion.Failed, Step("Deploy", GitHubRunConclusion.TimedOut)))
                .FailedStep);

        Assert.Equal(
            "Approve",
            WorkflowRunMapping.ToJob(Job(
                GitHubRunConclusion.Failed,
                Step("Approve", GitHubRunConclusion.ActionRequired))).FailedStep);
    }

    [Fact]
    public void ToJob_NamesNoStepWhenTheJobFailedWithoutOneFailing()
    {
        // A runner that died or a cancelled queue fails the job while every step it ran reported success.
        WorkflowRunJobResponse job = WorkflowRunMapping.ToJob(Job(
            GitHubRunConclusion.Failed,
            Step("Checkout", GitHubRunConclusion.Passed)));

        Assert.Null(job.FailedStep);
    }

    [Fact]
    public void ToJob_NamesNoStepForAJobThatPassed()
    {
        WorkflowRunJobResponse job = WorkflowRunMapping.ToJob(Job(
            GitHubRunConclusion.Passed,
            Step("Checkout", GitHubRunConclusion.Passed),
            Step("Build", GitHubRunConclusion.Passed)));

        Assert.Null(job.FailedStep);
        Assert.Equal("passed", job.Conclusion);
    }

    [Fact]
    public void ToJob_ReportsAStepWithNoEndAsHavingNoDuration()
    {
        GitHubRunJob source = new(
            "Backend",
            GitHubRunStatus.InProgress,
            Conclusion: null,
            DateTimeOffset.UtcNow.AddMinutes(-2),
            CompletedAtUtc: null,
            [
                new GitHubRunStep(
                    "Build",
                    1,
                    GitHubRunStatus.InProgress,
                    Conclusion: null,
                    DateTimeOffset.UtcNow.AddMinutes(-2),
                    CompletedAtUtc: null)
            ]);

        WorkflowRunJobResponse job = WorkflowRunMapping.ToJob(source);

        // Still going: a duration would imply an end neither the job nor the step has reached.
        Assert.Null(job.DurationSeconds);
        Assert.Null(Assert.Single(job.Steps).DurationSeconds);
        Assert.Equal("inProgress", job.Status);
    }

    private static GitHubRunJob Job(GitHubRunConclusion conclusion, params GitHubRunStep[] steps) =>
        new(
            "Backend",
            GitHubRunStatus.Completed,
            conclusion,
            new DateTimeOffset(2026, 8, 19, 6, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 19, 6, 2, 41, TimeSpan.Zero),
            steps);

    private static GitHubRunStep Step(string name, GitHubRunConclusion conclusion) =>
        new(
            name,
            Number: null,
            GitHubRunStatus.Completed,
            conclusion,
            new DateTimeOffset(2026, 8, 19, 6, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 19, 6, 0, 12, TimeSpan.Zero));
}
