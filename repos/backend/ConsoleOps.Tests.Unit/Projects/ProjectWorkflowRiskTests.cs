using ConsoleOps.Domain.Projects;

namespace ConsoleOps.Tests.Unit.Projects;

/// <summary>
/// Risk markings on a project.
/// </summary>
/// <remarks>
/// These pin the safety property the feature rests on: the absence of a decision is a state of its own, and it is
/// stored as an absence rather than as a row that says nothing.
/// </remarks>
public sealed class ProjectWorkflowRiskTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RiskOf_ReportsUnclassifiedForAWorkflowNobodyHasMarked()
    {
        Project project = CreateProject();

        Assert.Equal(WorkflowRiskLevel.Unclassified, project.RiskOf(".github/workflows/ci.yml"));
        Assert.Empty(project.WorkflowRisks);
    }

    [Fact]
    public void SetWorkflowRisk_RecordsTheDecisionAndWhenItWasMade()
    {
        Project project = CreateProject();

        project.SetWorkflowRisk(
            Guid.CreateVersion7(),
            ".github/workflows/database-restore.yml",
            WorkflowRiskLevel.Destructive,
            Now);

        ProjectWorkflowRisk risk = Assert.Single(project.WorkflowRisks);
        Assert.Equal(".github/workflows/database-restore.yml", risk.WorkflowPath);
        Assert.Equal(WorkflowRiskLevel.Destructive, risk.Level);
        Assert.Equal(Now, risk.DecidedAtUtc);
        Assert.Equal(WorkflowRiskLevel.Destructive, project.RiskOf(".github/workflows/database-restore.yml"));
    }

    [Fact]
    public void SetWorkflowRisk_ChangesAnExistingDecisionRatherThanAddingASecond()
    {
        Project project = CreateProject();
        project.SetWorkflowRisk(Guid.CreateVersion7(), ".github/workflows/ci.yml", WorkflowRiskLevel.Normal, Now);

        project.SetWorkflowRisk(
            Guid.CreateVersion7(),
            // Same file, differently cased: two rows would let the two disagree and nothing could say which won.
            ".GITHUB/Workflows/CI.yml",
            WorkflowRiskLevel.Destructive,
            Now.AddMinutes(5));

        ProjectWorkflowRisk risk = Assert.Single(project.WorkflowRisks);
        Assert.Equal(WorkflowRiskLevel.Destructive, risk.Level);
        Assert.Equal(Now.AddMinutes(5), risk.DecidedAtUtc);
    }

    [Fact]
    public void SetWorkflowRisk_RemovesTheDecisionWhenItIsSetBackToUnclassified()
    {
        Project project = CreateProject();
        project.SetWorkflowRisk(Guid.CreateVersion7(), ".github/workflows/ci.yml", WorkflowRiskLevel.Normal, Now);

        project.SetWorkflowRisk(
            Guid.CreateVersion7(),
            ".github/workflows/ci.yml",
            WorkflowRiskLevel.Unclassified,
            Now.AddMinutes(1));

        // Returned to the state where Console Ops will not run it, stored as an absence.
        Assert.Empty(project.WorkflowRisks);
        Assert.Equal(WorkflowRiskLevel.Unclassified, project.RiskOf(".github/workflows/ci.yml"));
    }

    [Fact]
    public void SetWorkflowRisk_DoesNotAdvanceTheConfigurationVersion()
    {
        Project project = CreateProject();
        long before = project.ConfigurationVersion;

        project.SetWorkflowRisk(Guid.CreateVersion7(), ".github/workflows/ci.yml", WorkflowRiskLevel.Normal, Now);

        // That version guards the project form against a concurrent save; a marking made from another screen
        // must not make an unrelated edit look stale.
        Assert.Equal(before, project.ConfigurationVersion);
    }

    [Fact]
    public void SetWorkflowRisk_RefusesAnArchivedProject()
    {
        Project project = CreateProject();
        project.Archive(Now);

        Assert.Throws<InvalidOperationException>(() => project.SetWorkflowRisk(
            Guid.CreateVersion7(),
            ".github/workflows/ci.yml",
            WorkflowRiskLevel.Normal,
            Now));
    }

    [Fact]
    public void SetWorkflowRisk_RefusesAPathThatNamesNothing()
    {
        Project project = CreateProject();

        Assert.Throws<ArgumentException>(() => project.SetWorkflowRisk(
            Guid.CreateVersion7(),
            "   ",
            WorkflowRiskLevel.Normal,
            Now));
    }

    private static Project CreateProject() => Project.Create(
        Guid.CreateVersion7(),
        "EEMO-Cantilan-SDS",
        description: null,
        "clint",
        "EEMO-Cantilan-SDS",
        "master",
        workflowFile: "deploy-production.yml",
        [ProjectEnvironment.Create(
            Guid.CreateVersion7(),
            "Production",
            EnvironmentKind.Production,
            applicationUrl: null,
            healthUrl: null,
            versionUrl: null,
            logSource: null)],
        Now);
}
