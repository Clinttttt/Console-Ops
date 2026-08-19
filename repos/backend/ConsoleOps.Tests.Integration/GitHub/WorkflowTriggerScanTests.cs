using ConsoleOps.Infrastructure.Integrations.GitHub;

namespace ConsoleOps.Tests.Integration.GitHub;

/// <summary>
/// Whether a workflow declares a manual dispatch trigger, read from its definition.
/// </summary>
/// <remarks>
/// The forms below are all real GitHub workflow spellings. The point of these tests is the two ways this can be
/// wrong: claiming a manual run is unavailable when the file says otherwise, and reading a mention of the
/// trigger elsewhere in the file as a declaration of it.
/// </remarks>
public sealed class WorkflowTriggerScanTests
{
    [Fact]
    public void DeclaresManualDispatch_ReadsTheMappingForm()
    {
        bool? result = WorkflowTriggerScan.DeclaresManualDispatch("""
            name: Database backup
            on:
              schedule:
                - cron: '0 2 * * *'
              workflow_dispatch:
            jobs:
              backup:
                runs-on: ubuntu-latest
            """);

        Assert.True(result);
    }

    [Fact]
    public void DeclaresManualDispatch_ReadsTheInlineListForm()
    {
        bool? result = WorkflowTriggerScan.DeclaresManualDispatch("""
            on: [push, workflow_dispatch]
            jobs: {}
            """);

        Assert.True(result);
    }

    [Fact]
    public void DeclaresManualDispatch_ReadsTheSingleValueForm()
    {
        Assert.True(WorkflowTriggerScan.DeclaresManualDispatch("on: workflow_dispatch"));
    }

    [Fact]
    public void DeclaresManualDispatch_ReadsTheBlockListForm()
    {
        bool? result = WorkflowTriggerScan.DeclaresManualDispatch("""
            on:
              - push
              - workflow_dispatch
            """);

        Assert.True(result);
    }

    [Fact]
    public void DeclaresManualDispatch_ReadsTheQuotedKeyYamlRequires()
    {
        // YAML 1.1 reads a bare `on` as a boolean, so many workflows quote it. Both mean the same to GitHub.
        bool? result = WorkflowTriggerScan.DeclaresManualDispatch("""
            "on":
              workflow_dispatch:
                inputs:
                  environment:
                    required: true
            """);

        Assert.True(result);
    }

    [Fact]
    public void DeclaresManualDispatch_ReportsAWorkflowWithoutTheTriggerAsUnavailable()
    {
        bool? result = WorkflowTriggerScan.DeclaresManualDispatch("""
            on:
              push:
                branches: [master]
              pull_request:
            """);

        Assert.False(result);
    }

    [Fact]
    public void DeclaresManualDispatch_DoesNotReadAJobConditionAsATriggerDeclaration()
    {
        bool? result = WorkflowTriggerScan.DeclaresManualDispatch("""
            on:
              push:
                branches: [master]
            jobs:
              deploy:
                if: github.event_name == 'workflow_dispatch'
                runs-on: ubuntu-latest
            """);

        // The trigger is mentioned outside the trigger block, which is not a declaration of it.
        Assert.False(result);
    }

    [Fact]
    public void DeclaresManualDispatch_IgnoresACommentedOutTrigger()
    {
        bool? result = WorkflowTriggerScan.DeclaresManualDispatch("""
            on:
              push:
              # workflow_dispatch:
            """);

        Assert.False(result);
    }

    [Fact]
    public void DeclaresManualDispatch_ReportsUnknownWhenNoTriggerBlockWasFound()
    {
        // Nothing recognisable was read, so the answer is that Console Ops does not know - not that a manual
        // run is unavailable, which would hide a workflow an operator relies on.
        Assert.Null(WorkflowTriggerScan.DeclaresManualDispatch("name: CI\njobs: {}"));
        Assert.Null(WorkflowTriggerScan.DeclaresManualDispatch(string.Empty));
    }
}
