using System.Net;
using System.Net.Http.Json;
using ConsoleOps.Api.Features.Settings;
using ConsoleOps.Application.Features.Settings.GetConfigurationStatus;
using ConsoleOps.Application.Integrations.Diagnostics;
using ConsoleOps.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConsoleOps.Tests.Integration.Settings;

/// <summary>
/// The configuration status report. These tests pin the one rule that makes it safe to expose - names only,
/// never values - and the distinction between "a key is set" and "the credentials work".
/// </summary>
[Collection(ConsoleOpsApiCollection.Name)]
public sealed class GetConfigurationStatusTests(ConsoleOpsApiFactory factory)
{
    /// <summary>A value no other part of the response could contain by coincidence.</summary>
    private const string Sentinel = "sentinel-value-2f9c41d7b83e";

    [Fact]
    public async Task Status_NeverReturnsAConfiguredValue()
    {
        using WebApplicationFactory<Program> application = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Api:Key", Sentinel);
            builder.UseSetting("GitHub:Token", Sentinel);
            builder.UseSetting("Azure:ClientSecret", Sentinel);
        });
        using HttpClient client = CreateClient(application);
        client.DefaultRequestHeaders.Add("X-Console-Ops-Key", Sentinel);

        string body = await client.GetStringAsync("/api/settings/configuration");

        // The whole point of the endpoint: it answers "is this configured" without becoming a way to read
        // secrets out of a running instance.
        Assert.DoesNotContain(Sentinel, body, StringComparison.Ordinal);
        Assert.Contains("GitHub:Token", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Status_SaysWhichCapabilityIsMissingAKey()
    {
        using WebApplicationFactory<Program> application = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("GitHub:Token", string.Empty));
        using HttpClient client = CreateClient(application);

        ConfigurationStatusResponse status = await ReadAsync(client);

        CapabilityStatusResponse source = Single(status, "Source and CI");
        // A blank value configures nothing, so it reads as missing rather than as set.
        Assert.Equal("missing", source.State);
        Assert.Contains(source.Keys, key => key.Key == "GitHub:Token" && key.State == "missing" && key.Required);

        // The database is configured for these tests, so it must not be reported as a problem.
        Assert.Equal("configured", Single(status, "Database").State);
    }

    [Fact]
    public async Task Status_DoesNotRequireAnApiKeyOnLoopback()
    {
        using WebApplicationFactory<Program> application = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Api:Key", string.Empty));
        using HttpClient client = CreateClient(application);

        ConfigurationStatusResponse status = await ReadAsync(client);

        CapabilityStatusResponse exposure = Single(status, "Exposure");
        ConfigurationKeyResponse key = Assert.Single(exposure.Keys);
        // Console Ops has no accounts by design, which is safe on loopback. The report must not nag about a
        // key that is not needed, or the one that matters gets ignored.
        Assert.False(key.Required);
        Assert.Equal("missing", key.State);
        Assert.NotEqual("missing", exposure.State);
    }

    [Fact]
    public async Task Status_TestsCredentialsOnlyWhenAsked()
    {
        RecordingProbe probe = new();
        using WebApplicationFactory<Program> application = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IIntegrationProbe>();
                services.AddSingleton<IIntegrationProbe>(probe);
            }));
        using HttpClient client = CreateClient(application);

        ConfigurationStatusResponse cheap = await ReadAsync(client);

        // A screen that loads must not spend seconds contacting providers.
        Assert.Equal(0, probe.Calls);
        Assert.False(cheap.Probed);
        Assert.All(cheap.Capabilities, capability => Assert.Null(capability.Connection));

        ConfigurationStatusResponse probed = await ReadAsync(client, "?probe=true");

        Assert.Equal(1, probe.Calls);
        Assert.True(probed.Probed);
        ConnectionCheckResponse connection = Assert.IsType<ConnectionCheckResponse>(
            Single(probed, "Database").Connection);
        Assert.False(connection.Succeeded);
        Assert.Equal("A stubbed failure.", connection.Failure);
    }

    [Fact]
    public async Task Status_WhenAProbeThrows_ReportsAFailedCheckRatherThanFailingTheRequest()
    {
        using WebApplicationFactory<Program> application = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IIntegrationProbe>();
                services.AddSingleton<IIntegrationProbe>(new ThrowingProbe());
            }));
        using HttpClient client = CreateClient(application);

        ConfigurationStatusResponse status = await ReadAsync(client, "?probe=true");

        // One unreachable provider must not hide the state of everything else.
        ConnectionCheckResponse connection = Assert.IsType<ConnectionCheckResponse>(
            Single(status, "Azure").Connection);
        Assert.False(connection.Succeeded);
        Assert.NotNull(connection.Failure);
    }

    [Fact]
    public async Task Collection_BeforeAnySweep_ReportsTheScheduleAndNothingElse()
    {
        using WebApplicationFactory<Program> application = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Monitoring:Refresh:Enabled", "true");
            builder.UseSetting("Monitoring:Refresh:IntervalSeconds", "300");
            // A long start-up delay keeps the worker from sweeping while this test asserts that none has.
            builder.UseSetting("Monitoring:Refresh:StartupDelaySeconds", "3600");
        });
        using HttpClient client = CreateClient(application);

        ConfigurationStatusResponse status = await ReadAsync(client);

        Assert.True(status.Collection.IsEnabled);
        Assert.Equal(300, status.Collection.IntervalSeconds);
        // Sweeps live in memory for one process, so nothing is reported until one has actually run.
        Assert.Null(status.Collection.LastSweepAt);
        Assert.Null(status.Collection.LastSweepSucceeded);
        Assert.Null(status.Collection.NextSweepAt);
    }

    [Fact]
    public async Task Collection_WhenScheduledCollectionIsOff_SaysSoRatherThanReportingNothingDue()
    {
        using WebApplicationFactory<Program> application = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Monitoring:Refresh:Enabled", "false"));
        using HttpClient client = CreateClient(application);

        ConfigurationStatusResponse status = await ReadAsync(client);

        // Off is a configuration, not a fault, and is not the same as "nothing has run yet".
        Assert.False(status.Collection.IsEnabled);
        Assert.Null(status.Collection.NextSweepAt);
    }

    [Fact]
    public async Task Sweep_RunsNowAndIsThenReportedByTheStatus()
    {
        using WebApplicationFactory<Program> application = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Monitoring:Refresh:Enabled", "true");
            builder.UseSetting("Monitoring:Refresh:IntervalSeconds", "300");
            builder.UseSetting("Monitoring:Refresh:StartupDelaySeconds", "3600");
        });
        using HttpClient client = CreateClient(application);

        HttpResponseMessage response = await client.PostAsync("/api/settings/collection/sweeps", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CollectionSweepResponse sweep = Assert.IsType<CollectionSweepResponse>(
            await response.Content.ReadFromJsonAsync<CollectionSweepResponse>());
        // Listing projects succeeded, which is what makes a sweep a sweep. Individual projects may still fail.
        Assert.True(sweep.Succeeded);

        ConfigurationStatusResponse status = await ReadAsync(client);
        Assert.Equal(sweep.CompletedAt, status.Collection.LastSweepAt);
        Assert.True(status.Collection.LastSweepSucceeded);
        Assert.NotNull(status.Collection.LastSweepMilliseconds);
        // Due an interval after the sweep began, because the schedule's timer runs from the start.
        Assert.NotNull(status.Collection.NextSweepAt);
        Assert.True(status.Collection.NextSweepAt > status.Collection.LastSweepAt);
    }

    private static CapabilityStatusResponse Single(ConfigurationStatusResponse status, string capability) =>
        Assert.Single(status.Capabilities, entry => entry.Capability == capability);

    private static async Task<ConfigurationStatusResponse> ReadAsync(
        HttpClient client,
        string query = "")
    {
        HttpResponseMessage response = await client.GetAsync($"/api/settings/configuration{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<ConfigurationStatusResponse>(
            await response.Content.ReadFromJsonAsync<ConfigurationStatusResponse>());
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> application) =>
        application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

    private sealed class RecordingProbe : IIntegrationProbe
    {
        public int Calls { get; private set; }

        public string Capability => "Database";

        public Task<IntegrationProbeResult> ProbeAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(IntegrationProbeResult.Failed("A stubbed failure."));
        }
    }

    private sealed class ThrowingProbe : IIntegrationProbe
    {
        public string Capability => "Azure";

        public Task<IntegrationProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("the provider client blew up");
    }
}
