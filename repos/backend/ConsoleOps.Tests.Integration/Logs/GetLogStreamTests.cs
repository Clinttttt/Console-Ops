using System.Net;
using System.Net.Http.Json;
using ConsoleOps.Api.Features.Projects;
using ConsoleOps.Application.Features.Logs.GetStream;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Integrations.AzureMonitor;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Infrastructure.Persistence;
using ConsoleOps.Infrastructure.Persistence.Deployments;
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

        LogEventResponse first = Assert.IsType<LogEventResponse>(stream.Items[0]);
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

    [Fact]
    public async Task Stream_CarriesTheDiscriminatorOnTheWire()
    {
        StubLogReader reader = new()
        {
            Entries = [Entry("info", "Order created", ApplicationLogLevel.Information, "Spinner.Orders")],
        };
        using WebApplicationFactory<Program> application = CreateApplication(reader);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client, withLogSource: true);

        string body = await client.GetStringAsync($"/api/logs?projectId={project.Id}");

        // Asserted on the raw JSON rather than the deserialized type, because the client selects on this
        // property. It was once absent from the wire while every typed test still passed, and a correct
        // response with seventeen events rendered as an empty stream.
        Assert.Contains("\"kind\":\"event\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stream_PlacesARecordedRunInTheTimelineAsAMarker()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        StubLogReader reader = new()
        {
            Entries =
            [
                Entry("newer", "After the release", ApplicationLogLevel.Information, "Spinner.Orders", now.AddMinutes(-2)),
                Entry("older", "Before the release", ApplicationLogLevel.Information, "Spinner.Orders", now.AddMinutes(-20)),
            ],
        };
        using WebApplicationFactory<Program> application = CreateApplication(reader);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client, withLogSource: true);
        Guid deploymentId = await RecordRunAsync(
            application,
            project.Id,
            "0f1e2d3c4b5a69788796a5b4c3d2e1f001234567",
            now.AddMinutes(-10));

        LogStreamResponse stream = await ReadAsync(client, project.Id);

        Assert.Equal(3, stream.Items.Count);
        // Newest first, so the marker sits between the lines it separates.
        Assert.IsType<LogEventResponse>(stream.Items[0]);
        LogMarkerResponse marker = Assert.IsType<LogMarkerResponse>(stream.Items[1]);
        Assert.IsType<LogEventResponse>(stream.Items[2]);
        Assert.Equal("deployment", marker.MarkerKind);
        Assert.Equal("0f1e2d3", marker.CommitShortSha);
        Assert.Equal(deploymentId, marker.DeploymentId);
        // A run proves CI built a commit, not that a particular revision started serving it.
        Assert.Null(marker.Revision);
    }

    [Fact]
    public async Task Stream_DoesNotMarkARunOlderThanTheLinesOnScreen()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        StubLogReader reader = new()
        {
            Entries =
            [
                Entry("only", "Recent line", ApplicationLogLevel.Information, "Spinner.Orders", now.AddMinutes(-1)),
            ],
        };
        using WebApplicationFactory<Program> application = CreateApplication(reader);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client, withLogSource: true);
        // Inside the requested 24-hour window, but well before the oldest line the provider returned.
        await RecordRunAsync(application, project.Id, "abcdef1234567890abcdef1234567890abcdef12", now.AddHours(-6));

        LogStreamResponse stream = await ReadAsync(client, project.Id);

        // A marker below the oldest visible line would sit where nothing it explains can be seen.
        Assert.Single(stream.Items);
        Assert.DoesNotContain(stream.Items, item => item is LogMarkerResponse);
    }

    [Fact]
    public async Task Stream_MarksARevisionChangeItObservedInTheLinesThemselves()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        StubLogReader reader = new()
        {
            Entries =
            [
                Entry("newer", "Serving", ApplicationLogLevel.Information, "Spinner.Orders", now.AddMinutes(-2), "spinner-api-stg--0000044"),
                Entry("older", "Serving", ApplicationLogLevel.Information, "Spinner.Orders", now.AddMinutes(-9), "spinner-api-stg--0000043"),
            ],
        };
        using WebApplicationFactory<Program> application = CreateApplication(reader);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client, withLogSource: true);

        LogStreamResponse stream = await ReadAsync(client, project.Id);

        LogMarkerResponse marker = Assert.Single(stream.Items.OfType<LogMarkerResponse>());
        Assert.Equal("revision", marker.MarkerKind);
        // The revision the newer lines came from, taken from the rows and not from a control-plane call.
        Assert.Equal("spinner-api-stg--0000044", marker.Revision);
        Assert.Null(marker.DeploymentId);
        Assert.Equal(stream.Items.OfType<LogEventResponse>().First().OccurredAt, marker.OccurredAt);
    }

    [Fact]
    public async Task Stream_WhenTwoRevisionsOverlapped_MarksEachOnceInsteadOfFlapping()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        // Taken from a real rollout: the outgoing revision keeps logging while the incoming one starts, so
        // the lines interleave. Detecting a change between neighbours produced three markers for two
        // revisions and claimed the old one had started serving.
        StubLogReader reader = new()
        {
            Entries =
            [
                Entry("e5", "Serving", ApplicationLogLevel.Information, "Spinner.Orders", now.AddMinutes(-1), "spinner-api-stg--0000043"),
                Entry("e4", "Serving", ApplicationLogLevel.Information, "Spinner.Orders", now.AddMinutes(-2), "spinner-api-stg--0000042"),
                Entry("e3", "Serving", ApplicationLogLevel.Information, "Spinner.Orders", now.AddMinutes(-3), "spinner-api-stg--0000043"),
                Entry("e2", "Serving", ApplicationLogLevel.Information, "Spinner.Orders", now.AddMinutes(-4), "spinner-api-stg--0000042"),
                Entry("e1", "Serving", ApplicationLogLevel.Information, "Spinner.Orders", now.AddMinutes(-5), "spinner-api-stg--0000042"),
            ],
        };
        using WebApplicationFactory<Program> application = CreateApplication(reader);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client, withLogSource: true);

        LogStreamResponse stream = await ReadAsync(client, project.Id);

        LogMarkerResponse marker = Assert.Single(stream.Items.OfType<LogMarkerResponse>());
        // Only the revision that appeared during the window is marked, once, at its earliest line. The
        // revision that was already serving is not announced.
        Assert.Equal("spinner-api-stg--0000043", marker.Revision);
        Assert.Equal(now.AddMinutes(-3), marker.OccurredAt);
    }

    [Fact]
    public async Task Stream_WhenOneRevisionServedTheWholeWindow_MarksNothing()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        StubLogReader reader = new()
        {
            Entries =
            [
                Entry("newer", "Serving", ApplicationLogLevel.Information, "Spinner.Orders", now.AddMinutes(-2)),
                Entry("older", "Serving", ApplicationLogLevel.Information, "Spinner.Orders", now.AddMinutes(-9)),
            ],
        };
        using WebApplicationFactory<Program> application = CreateApplication(reader);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client, withLogSource: true);

        LogStreamResponse stream = await ReadAsync(client, project.Id);

        // The oldest revision was already serving when the window opened. Console Ops does not know when
        // it started, so it says nothing rather than marking the edge of what it happened to read.
        Assert.DoesNotContain(stream.Items, item => item is LogMarkerResponse);
    }

    /// <summary>
    /// Writes a recorded run directly, standing in for what a refresh would have collected from GitHub.
    /// </summary>
    private static async Task<Guid> RecordRunAsync(
        WebApplicationFactory<Program> application,
        Guid projectId,
        string commitSha,
        DateTimeOffset completedAt)
    {
        await using AsyncServiceScope scope = application.Services.CreateAsyncScope();
        ConsoleOpsDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConsoleOpsDbContext>();
        DeploymentEntity entity = new()
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            ExternalRunId = Random.Shared.NextInt64(1, long.MaxValue),
            RunNumber = 12,
            WorkflowFile = "deploy.yml",
            WorkflowName = "Deploy",
            Branch = "main",
            CommitSha = commitSha,
            Result = GitHubWorkflowState.Passed,
            StartedAtUtc = completedAt.AddMinutes(-3),
            CompletedAtUtc = completedAt,
            RecordedAtUtc = completedAt,
            ObservedAtUtc = completedAt,
        };
        dbContext.Deployments.Add(entity);
        await dbContext.SaveChangesAsync();
        return entity.Id;
    }

    [Fact]
    public async Task Stream_LeavesOutFrameworkChatterAndSaysHowMuch()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        StubLogReader reader = new()
        {
            Entries =
            [
                Entry("app", "Order created", ApplicationLogLevel.Information, "Spinner.Orders", now.AddMinutes(-1)),
                Entry("ef", "Executed DbCommand (3ms)", ApplicationLogLevel.Information, "Microsoft.EntityFrameworkCore.Database.Command", now.AddMinutes(-2)),
                Entry("http", "Start processing HTTP request", ApplicationLogLevel.Information, "System.Net.Http.HttpClient.IProbe.LogicalHandler", now.AddMinutes(-3)),
            ],
        };
        using WebApplicationFactory<Program> application = CreateApplication(reader);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client, withLogSource: true);

        LogStreamResponse stream = await ReadAsync(client, project.Id);

        // The reader is told to filter, so the screen never has to hide anything after the fact.
        Assert.True(reader.LastQuery?.ExcludeNoise);
        Assert.True(stream.Noise.Excluded);
        // Nothing is dropped silently: the count is why a quiet window is quiet.
        Assert.Equal(2, stream.Noise.HiddenCount);
        // And what produced it, so a window of nothing but chatter still says what the service was doing.
        Assert.Equal(2, stream.Noise.Categories.Count);
        Assert.Contains(
            stream.Noise.Categories,
            category => category.Category == "Microsoft.EntityFrameworkCore.Database.Command"
                && category.Count == 1);
        LogEventResponse kept = Assert.Single(stream.Items.OfType<LogEventResponse>());
        Assert.Equal("Spinner.Orders", kept.Source);
    }

    [Fact]
    public async Task Stream_WhenNoiseIsAskedFor_KeepsItAndSaysNothingWasHidden()
    {
        StubLogReader reader = new()
        {
            Entries =
            [
                Entry("ef", "Executed DbCommand (3ms)", ApplicationLogLevel.Information, "Microsoft.EntityFrameworkCore.Database.Command"),
            ],
        };
        using WebApplicationFactory<Program> application = CreateApplication(reader);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client, withLogSource: true);

        LogStreamResponse stream = await ReadAsync(client, project.Id, "&includeNoise=true");

        Assert.False(reader.LastQuery?.ExcludeNoise);
        Assert.False(stream.Noise.Excluded);
        Assert.Equal(0, stream.Noise.HiddenCount);
        Assert.Single(stream.Items);
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
        string category,
        DateTimeOffset? occurredAt = null,
        string revision = "spinner-api-stg--0000043") =>
        new(
            id,
            occurredAt ?? DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow,
            level,
            true,
            category,
            message,
            null,
            ApplicationLogStream.Stdout,
            revision,
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
            if (Failure is not null)
            {
                return Task.FromResult(ApplicationLogReadResult.Failed(Failure.Value, DateTimeOffset.UtcNow));
            }

            if (!query.ExcludeNoise)
            {
                return Task.FromResult(
                    ApplicationLogReadResult.Success(Entries, false, DateTimeOffset.UtcNow));
            }

            // Stands in for the real adapter, which filters after folding continuation lines.
            ApplicationLogEntry[] kept = Entries.Where(entry => !ApplicationLogNoise.IsNoise(entry)).ToArray();
            ApplicationLogNoiseCount[] byCategory = Entries
                .Where(ApplicationLogNoise.IsNoise)
                .GroupBy(entry => entry.Category!, StringComparer.Ordinal)
                .Select(group => new ApplicationLogNoiseCount(group.Key, group.Count()))
                .OrderByDescending(count => count.Count)
                .ToArray();

            return Task.FromResult(ApplicationLogReadResult.Success(
                kept,
                false,
                DateTimeOffset.UtcNow,
                Entries.Count - kept.Length,
                byCategory));
        }
    }
}
