using System.Net.Http.Json;
using ConsoleOps.Api.Features.Projects;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Features.Workflows.GetInventory;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConsoleOps.Tests.Integration.Workflows;

/// <summary>
/// What the Workflows screen is told. These pin the distinctions the feature exists to make: a workflow is a
/// deployment only where an operator said so, a repository that could not be read is not a repository without
/// automation, and a run still going has no outcome.
/// </summary>
[Collection(ConsoleOpsApiCollection.Name)]
public sealed class GetWorkflowInventoryTests(ConsoleOpsApiFactory factory)
{
    [Fact]
    public async Task Inventory_CallsAWorkflowADeploymentOnlyWhereTheOperatorConfiguredIt()
    {
        StubInventory inventory = new(new GitHubWorkflowInventoryPage(
        [
            Workflow(101, "Deploy production", ".github/workflows/deploy-production.yml"),
            Workflow(202, "Database backup", ".github/workflows/database-backup.yml")
        ]));

        using WebApplicationFactory<Program> application = CreateApplication(inventory);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client, workflowFile: "deploy-production.yml");

        WorkflowInventoryResponse response = await ReadAsync(client);
        WorkflowProjectGroupResponse group = Assert.Single(
            response.Groups,
            candidate => candidate.ProjectId == project.Id);

        Assert.Equal(
            "deployment",
            Assert.Single(group.Workflows, workflow => workflow.Name == "Deploy production").Classification);
        // Maintenance to a reader, unprovable to Console Ops: the provider reports no category and the name
        // is not evidence.
        Assert.Equal(
            "unclassified",
            Assert.Single(group.Workflows, workflow => workflow.Name == "Database backup").Classification);
    }

    [Fact]
    public async Task Inventory_ReportsAnUnreadableRepositoryAsSuchRatherThanAsHavingNoAutomation()
    {
        StubInventory inventory = new(GitHubReadFailure.Unauthorized);

        using WebApplicationFactory<Program> application = CreateApplication(inventory);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client);

        WorkflowInventoryResponse response = await ReadAsync(client);
        WorkflowProjectGroupResponse group = Assert.Single(
            response.Groups,
            candidate => candidate.ProjectId == project.Id);

        Assert.Empty(group.Workflows);
        Assert.Equal("unauthorized", group.ReadFailure);
    }

    [Fact]
    public async Task Inventory_DescribesARunningRunWithoutAnOutcomeOrADuration()
    {
        StubInventory inventory = new(new GitHubWorkflowInventoryPage(
        [
            Workflow(101, "Deploy production", ".github/workflows/deploy-production.yml") with
            {
                LatestRun = new GitHubRunSummary(
                    535,
                    535,
                    GitHubRunStatus.InProgress,
                    Conclusion: null,
                    "master",
                    "2ac8bf0f4c1e9d7a3b5c8e2f1a4d6b9c0e3f7a21",
                    "push",
                    "Clinttttt",
                    DateTimeOffset.UtcNow.AddMinutes(-8),
                    CompletedAtUtc: null,
                    "https://github.test/run/535")
            }
        ]));

        using WebApplicationFactory<Program> application = CreateApplication(inventory);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client);

        WorkflowInventoryResponse response = await ReadAsync(client);
        WorkflowRunResponse run = Assert.Single(
            Assert.Single(response.Groups, group => group.ProjectId == project.Id).Workflows).LatestRun!;

        Assert.Equal("inProgress", run.Status);
        Assert.Null(run.Conclusion);
        // A duration would imply an end the run has not reached.
        Assert.Null(run.DurationSeconds);
        Assert.Equal("2ac8bf0", run.CommitShortSha);
        Assert.Equal("push", run.Trigger);
        // Jobs cost a request each and are read only for the workflow an operator selects.
        Assert.Empty(run.Jobs);
    }

    [Fact]
    public async Task Inventory_ReportsAWorkflowWithNoRunWithoutInventingOne()
    {
        StubInventory inventory = new(new GitHubWorkflowInventoryPage(
        [
            Workflow(303, "Database restore", ".github/workflows/database-restore.yml")
        ]));

        using WebApplicationFactory<Program> application = CreateApplication(inventory);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client);

        WorkflowInventoryResponse response = await ReadAsync(client);
        WorkflowResponse workflow = Assert.Single(
            Assert.Single(response.Groups, group => group.ProjectId == project.Id).Workflows);

        Assert.Null(workflow.LatestRun);
        // Not knowing whether it can be dispatched is different from knowing it cannot.
        Assert.Equal("unknown", workflow.ManualRun);
        Assert.Equal("active", workflow.State);
    }

    [Fact]
    public async Task RunJobs_RefusesARunWhoseProjectIsNotRegistered()
    {
        StubInventory inventory = new(new GitHubWorkflowInventoryPage([]));

        using WebApplicationFactory<Program> application = CreateApplication(inventory);
        using HttpClient client = CreateClient(application);

        using HttpResponseMessage response = await client.GetAsync(
            $"/api/workflows/projects/{Guid.NewGuid()}/runs/535/jobs");

        // The repository comes from the project, so an unknown project cannot be used to read anything.
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    private static GitHubWorkflowDefinition Workflow(long id, string name, string path) =>
        new(id, name, path, Active: true, SupportsManualRun: null, LatestRun: null);

    private WebApplicationFactory<Program> CreateApplication(IGitHubWorkflowInventory inventory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGitHubWorkflowInventory>();
            services.AddSingleton(inventory);
        }));

    private static HttpClient CreateClient(WebApplicationFactory<Program> application) =>
        application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

    private static async Task<WorkflowInventoryResponse> ReadAsync(HttpClient client)
    {
        WorkflowInventoryResponse? response =
            await client.GetFromJsonAsync<WorkflowInventoryResponse>("/api/workflows");

        Assert.NotNull(response);
        return response;
    }

    private static async Task<ProjectResponse> RegisterAsync(
        HttpClient client,
        string? workflowFile = null)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        using HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/projects",
            new
            {
                name = $"Workflow project {suffix}",
                repository = new
                {
                    owner = "clint",
                    name = $"repo-{suffix}",
                    defaultBranch = "main",
                    workflowFile
                },
                environments = new[]
                {
                    new { name = "Production", kind = "production" }
                }
            });

        created.EnsureSuccessStatusCode();
        ProjectResponse? project = await created.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(project);
        return project;
    }

    /// <summary>Answers with one prepared page, or one prepared failure, for every repository.</summary>
    private sealed class StubInventory : IGitHubWorkflowInventory
    {
        private readonly GitHubWorkflowInventoryPage? page;
        private readonly GitHubReadFailure? failure;

        public StubInventory(GitHubWorkflowInventoryPage page) => this.page = page;

        public StubInventory(GitHubReadFailure failure) => this.failure = failure;

        public Task<GitHubFactResult<GitHubWorkflowInventoryPage>> ListWorkflowsAsync(
            string owner,
            string repository,
            CancellationToken cancellationToken) =>
            Task.FromResult(page is null
                ? GitHubFactResult<GitHubWorkflowInventoryPage>.Failed(failure!.Value)
                : GitHubFactResult<GitHubWorkflowInventoryPage>.Success(page));

        public Task<GitHubFactResult<GitHubRunPage>> ListRunsAsync(
            string owner,
            string repository,
            long workflowId,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult(GitHubFactResult<GitHubRunPage>.Success(new GitHubRunPage([], false)));

        public Task<GitHubFactResult<GitHubManualRunSupport>> ReadManualRunSupportAsync(
            string owner,
            string repository,
            string workflowPath,
            CancellationToken cancellationToken) =>
            Task.FromResult(GitHubFactResult<GitHubManualRunSupport>.Success(
                new GitHubManualRunSupport(null, workflowPath)));

        public Task<GitHubFactResult<GitHubRunJobs>> ListRunJobsAsync(
            string owner,
            string repository,
            long runId,
            CancellationToken cancellationToken) =>
            Task.FromResult(GitHubFactResult<GitHubRunJobs>.Success(new GitHubRunJobs([])));
    }
}
