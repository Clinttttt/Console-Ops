using System.Net;
using System.Net.Http.Json;
using ConsoleOps.Api.Features.Projects;
using ConsoleOps.Application.Features.Logs.GetStream;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Integrations.AzureMonitor;
using ConsoleOps.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConsoleOps.Tests.Integration.Logs;

/// <summary>
/// The log stream reads a provider during the request. These tests pin what the screen is told: which
/// scopes are readable, the window that was queried, and the difference between an empty window and a
/// provider that could not be asked.
/// </summary>
[Collection(ConsoleOpsApiCollection.Name)]
public sealed class GetLogStreamTests(ConsoleOpsApiFactory factory)
{
    private static readonly Guid Workspace = Guid.Parse("6f5c1a2b-3d4e-5f60-7182-93a4b5c6d7e8");

    [Fact]
    public async Task Stream_ReadsTheConfiguredScopeAndStatesTheWindow()
    {
        StubLogReader reader = new()
        {
            Entries =
            [
                Entry("info", "Order created", ApplicationLogLevel.Information, "Spinner.Orders"),
                Entry("fail", "Payment failed", ApplicationLogLevel.Error, "Spinner.Payments"),
            ],
        };
        using WebApplicationFactory<Program> application = CreateApplication(reader);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client, withLogSource: true);

        LogStreamResponse stream = await ReadAsync(client, project.Id);

        Assert.Equal(24, stream.Window.Hours);
        Assert.Equal(stream.Window.To.AddHours(-24), stream.Window.From);
        Assert.False(stream.Window.Truncated);
        Assert.Contains(stream.Scopes, scope => scope.ProjectId == project.Id);
        Assert.Equal(project.Id, stream.Scope?.ProjectId);
        Assert.Equal("azureContainerApps", stream.Scope?.Provider);
        Assert.Equal(2, stream.Items.Count);

        LogEventResponse first = stream.Items[0];
        Assert.Equal("information", first.Level);
        // Console output carries no severity column, so a parsed level is reported as derived.
        Assert.True(first.LevelIsDerived);
        Assert.Equal("Spinner.Orders", first.Source);
        Assert.Equal("application", first.SourceKind);
        Assert.Equal("stdout", first.Stream);
        Assert.NotNull(first.ReceivedAt);

        // The adapter was asked for exactly the configured source and the stated window.
        Assert.Equal(Workspace, reader.LastQuery?.WorkspaceId);
        Assert.Equal("spinner-api-stg", reader.LastQuery?.ContainerAppName);
        Assert.Equal(stream.Window.From, reader.LastQuery?.FromUtc);
    }

    [Fact]
    public async Task Stream_WhenTheWindowHeldNothing_ReturnsAnEmptyStreamRatherThanAnError()
    {
        StubLogReader reader = new();
        using WebApplicationFactory<Program> application = CreateApplication(reader);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client, withLogSource: true);

        LogStreamResponse stream = await ReadAsync(client, project.Id);

        // Nothing logged is a fact about the window, not a failure.
        Assert.Empty(stream.Items);
        Assert.NotNull(stream.Scope);
    }

    [Fact]
    public async Task Stream_WhenTheProviderCouldNotBeAsked_SaysSoInsteadOfShowingNothing()
    {
        StubLogReader reader = new() { Failure = ApplicationLogReadFailure.Unauthorized };
        using WebApplicationFactory<Program> application = CreateApplication(reader);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client, withLogSource: true);

        HttpResponseMessage response = await client.GetAsync($"/api/logs?projectId={project.Id}");

        // "Could not ask" must never be rendered as an empty window.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Logs.Unauthorized", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stream_ForAnEnvironmentWithoutALogSource_IsRefusedRatherThanAnsweredEmpty()
    {
        StubLogReader reader = new();
        using WebApplicationFactory<Program> application = CreateApplication(reader);
        using HttpClient client = CreateClient(application);
        ProjectResponse configured = await RegisterAsync(client, withLogSource: true);
        ProjectResponse bare = await RegisterAsync(client, withLogSource: false);

        HttpResponseMessage response = await client.GetAsync($"/api/logs?projectId={bare.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(
            "Logs.ScopeNotFound",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        // The unreadable environment is not offered as a scope either.
        LogStreamResponse stream = await ReadAsync(client, configured.Id);
        Assert.DoesNotContain(stream.Scopes, scope => scope.ProjectId == bare.Id);
    }

    [Fact]
    public async Task Stream_PagesBackwardsFromTheGivenInstant()
    {
        StubLogReader reader = new();
        using WebApplicationFactory<Program> application = CreateApplication(reader);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client, withLogSource: true);
        DateTimeOffset before = new(2026, 8, 16, 10, 0, 0, TimeSpan.Zero);

        LogStreamResponse stream = await ReadAsync(
            client,
            project.Id,
            $"&before={Uri.EscapeDataString(before.ToString("o"))}");

        Assert.Equal(before, stream.Window.To);
        Assert.Equal(before.AddHours(-24), stream.Window.From);
    }

    private WebApplicationFactory<Program> CreateApplication(IApplicationLogReader reader) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IApplicationLogReader>();
            services.AddSingleton(reader);
        }));

    private static HttpClient CreateClient(WebApplicationFactory<Program> application) =>
        application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

    private static ApplicationLogEntry Entry(
        string id,
        string message,
        ApplicationLogLevel level,
        string category) =>
        new(
            id,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow,
            level,
            true,
            category,
            message,
            null,
            ApplicationLogStream.Stdout,
            "spinner-api-stg--0000043",
            "spinner-api-stg-abc123");

    private static async Task<LogStreamResponse> ReadAsync(
        HttpClient client,
        Guid projectId,
        string extraQuery = "")
    {
        HttpResponseMessage response = await client.GetAsync($"/api/logs?projectId={projectId}{extraQuery}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<LogStreamResponse>(
            await response.Content.ReadFromJsonAsync<LogStreamResponse>());
    }

    private static async Task<ProjectResponse> RegisterAsync(HttpClient client, bool withLogSource)
    {
        string unique = Guid.NewGuid().ToString("N");
        RegisterProjectRequest request = new(
            $"Log Stream {unique}",
            "Log stream integration test",
            new ProjectRepositoryRequest("console-ops-tests", $"stream-{unique}", "main", "deploy.yml"),
            [
                new RegisterProjectEnvironmentRequest(
                    "Production",
                    "production",
                    null,
                    null,
                    null,
                    withLogSource ? new ProjectLogSourceRequest(Workspace, "spinner-api-stg") : null)
            ]);
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/projects", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return Assert.IsType<ProjectResponse>(await response.Content.ReadFromJsonAsync<ProjectResponse>());
    }

    private sealed class StubLogReader : IApplicationLogReader
    {
        public IReadOnlyList<ApplicationLogEntry> Entries { get; set; } = [];

        public ApplicationLogReadFailure? Failure { get; set; }

        public ApplicationLogQuery? LastQuery { get; private set; }

        public Task<ApplicationLogReadResult> ReadAsync(
            ApplicationLogQuery query,
            CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult(Failure is null
                ? ApplicationLogReadResult.Success(Entries, false, DateTimeOffset.UtcNow)
                : ApplicationLogReadResult.Failed(Failure.Value, DateTimeOffset.UtcNow));
        }
    }
}
