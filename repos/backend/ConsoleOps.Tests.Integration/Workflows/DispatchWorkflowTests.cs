using System.Net;
using System.Net.Http.Json;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Features.Workflows.Dispatch;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConsoleOps.Tests.Integration.Workflows;

/// <summary>
/// Starting a workflow.
/// </summary>
/// <remarks>
/// Every gate is asserted against the API rather than the screen, because the API is what refuses: a screen that
/// offered a run it should not have must still be turned down here.
/// </remarks>
[Collection(ConsoleOpsApiCollection.Name)]
public sealed class DispatchWorkflowTests(ConsoleOpsApiFactory factory)
{
    private const string WorkflowPath = ".github/workflows/database-restore.yml";
    private const string WorkflowName = "Database restore";
    private const long WorkflowId = 101;

    [Fact]
    public async Task Dispatch_RefusesAWorkflowWhoseRiskNobodyHasMarked()
    {
        FakeGitHubWorkflowInventory provider = Provider();
        using WebApplicationFactory<Program> application = CreateApplication(provider);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client);

        using HttpResponseMessage response = await RunAsync(client, project.Id, "master");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("risk", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        // Nothing reached the provider: the refusal happened before Console Ops asked for anything.
        Assert.Empty(provider.Dispatches);
    }

    [Fact]
    public async Task Dispatch_StartsANormalWorkflowOnTheStatedRefWithoutAskingForTypedText()
    {
        FakeGitHubWorkflowInventory provider = Provider();
        using WebApplicationFactory<Program> application = CreateApplication(provider);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client);
        await MarkAsync(client, project.Id, "normal");

        using HttpResponseMessage response = await RunAsync(client, project.Id, "master");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        WorkflowDispatchResponse? accepted = await response.Content.ReadFromJsonAsync<WorkflowDispatchResponse>();
        // The provider accepts without reporting a run, so this says requested rather than claiming one started.
        Assert.Equal("requested", accepted!.Status);
        Assert.Equal("master", accepted.Reference);

        (_, _, long workflowId, string reference, IReadOnlyDictionary<string, string> inputs) =
            Assert.Single(provider.Dispatches);
        Assert.Equal(WorkflowId, workflowId);
        Assert.Equal("master", reference);
        Assert.Empty(inputs);
    }

    [Fact]
    public async Task Dispatch_RefusesADestructiveWorkflowUntilItsNameIsTyped()
    {
        FakeGitHubWorkflowInventory provider = Provider();
        using WebApplicationFactory<Program> application = CreateApplication(provider);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client);
        await MarkAsync(client, project.Id, "destructive");

        using HttpResponseMessage withoutText = await RunAsync(client, project.Id, "master");
        Assert.Equal(HttpStatusCode.BadRequest, withoutText.StatusCode);
        Assert.Empty(provider.Dispatches);

        using HttpResponseMessage wrongText = await RunAsync(client, project.Id, "master", "Database backup");
        Assert.Equal(HttpStatusCode.BadRequest, wrongText.StatusCode);
        Assert.Empty(provider.Dispatches);

        // Trimmed and case-insensitive: the point is deliberate intent, not transcription accuracy.
        using HttpResponseMessage typed = await RunAsync(client, project.Id, "master", "  database RESTORE ");
        Assert.Equal(HttpStatusCode.Accepted, typed.StatusCode);
        Assert.Single(provider.Dispatches);
    }

    [Fact]
    public async Task Dispatch_RequiresARefRatherThanChoosingOne()
    {
        FakeGitHubWorkflowInventory provider = Provider();
        using WebApplicationFactory<Program> application = CreateApplication(provider);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client);
        await MarkAsync(client, project.Id, "normal");

        using HttpResponseMessage response = await RunAsync(client, project.Id, "   ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(provider.Dispatches);
    }

    [Fact]
    public async Task Dispatch_RefusesAWorkflowWhoseDefinitionDeclaresNoDispatchTrigger()
    {
        FakeGitHubWorkflowInventory provider = Provider();
        provider.SupportsManualRun = false;
        using WebApplicationFactory<Program> application = CreateApplication(provider);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client);
        await MarkAsync(client, project.Id, "normal");

        using HttpResponseMessage response = await RunAsync(client, project.Id, "master");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(provider.Dispatches);
    }

    [Fact]
    public async Task Dispatch_RefusesAWorkflowWhoseDefinitionCouldNotBeRead()
    {
        FakeGitHubWorkflowInventory provider = Provider();
        provider.SupportsManualRun = null;
        using WebApplicationFactory<Program> application = CreateApplication(provider);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client);
        await MarkAsync(client, project.Id, "normal");

        using HttpResponseMessage response = await RunAsync(client, project.Id, "master");

        // Not established is refused as well: starting a workflow on a guess is the thing this avoids.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(provider.Dispatches);
    }

    [Fact]
    public async Task Dispatch_SendsOnlyInputsTheWorkflowDeclared()
    {
        FakeGitHubWorkflowInventory provider = Provider();
        provider.Inputs =
        [
            new GitHubWorkflowInput("environment", "Target", true, "choice", "rehearsal", ["rehearsal", "live"]),
            new GitHubWorkflowInput("backup", null, false, "string", null, [])
        ];

        using WebApplicationFactory<Program> application = CreateApplication(provider);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client);
        await MarkAsync(client, project.Id, "normal");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/workflows/projects/{project.Id}/workflows/{WorkflowId}/runs",
            new
            {
                reference = "master",
                inputs = new Dictionary<string, string>
                {
                    ["environment"] = "live",
                    // Left blank, so the workflow's own default is the better answer than an empty value.
                    ["backup"] = "   ",
                    // Never declared, so it is not forwarded whatever a caller asks for.
                    ["adminToken"] = "sneaky"
                }
            });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        IReadOnlyDictionary<string, string> sent = Assert.Single(provider.Dispatches).Inputs;
        Assert.Equal(new[] { "environment" }, sent.Keys);
        Assert.Equal("live", sent["environment"]);
    }

    [Fact]
    public async Task Dispatch_ReportsATokenWithoutActionsWriteAsUnauthorized()
    {
        FakeGitHubWorkflowInventory provider = Provider();
        provider.DispatchOutcome = GitHubDispatchOutcome.Forbidden;
        using WebApplicationFactory<Program> application = CreateApplication(provider);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client);
        await MarkAsync(client, project.Id, "normal");

        using HttpResponseMessage response = await RunAsync(client, project.Id, "master");
        string body = await response.Content.ReadAsStringAsync();

        // A token scope the operator has not granted is not a server fault, so it is not reported as one.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Workflows.Unauthorized", body, StringComparison.Ordinal);
        // The reason names what to check without implying the workflow or the marking was at fault.
        Assert.Contains("write access", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispatch_RefusesAWorkflowTheProviderReportsAsDisabled()
    {
        FakeGitHubWorkflowInventory provider = Provider();
        provider.Workflow = new GitHubWorkflowDefinition(
            WorkflowId,
            WorkflowName,
            WorkflowPath,
            Active: false,
            SupportsManualRun: null,
            LatestRun: null);

        using WebApplicationFactory<Program> application = CreateApplication(provider);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client);
        await MarkAsync(client, project.Id, "normal");

        using HttpResponseMessage response = await RunAsync(client, project.Id, "master");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(provider.Dispatches);
    }

    /// <summary>A dispatchable workflow the provider is happy with, so each test disables one thing at a time.</summary>
    private static FakeGitHubWorkflowInventory Provider() =>
        new(new GitHubWorkflowInventoryPage(
        [
            new GitHubWorkflowDefinition(
                WorkflowId,
                WorkflowName,
                WorkflowPath,
                Active: true,
                SupportsManualRun: null,
                LatestRun: null)
        ]))
        {
            SupportsManualRun = true
        };

    private static Task<HttpResponseMessage> RunAsync(
        HttpClient client,
        Guid projectId,
        string reference,
        string? confirmation = null) =>
        client.PostAsJsonAsync(
            $"/api/workflows/projects/{projectId}/workflows/{WorkflowId}/runs",
            new { reference, confirmation });

    private static async Task MarkAsync(HttpClient client, Guid projectId, string level)
    {
        using HttpResponseMessage marked = await client.PutAsJsonAsync(
            $"/api/workflows/projects/{projectId}/risk",
            new { workflowPath = WorkflowPath, level });

        marked.EnsureSuccessStatusCode();
    }

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

    private static async Task<ProjectResponse> RegisterAsync(HttpClient client)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        using HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/projects",
            new
            {
                name = $"Dispatch project {suffix}",
                repository = new
                {
                    owner = "clint",
                    name = $"repo-{suffix}",
                    defaultBranch = "master"
                },
                environments = new[] { new { name = "Production", kind = "production" } }
            });

        created.EnsureSuccessStatusCode();
        ProjectResponse? project = await created.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(project);
        return project;
    }
}
