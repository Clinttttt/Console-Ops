using System.Collections.Concurrent;
using System.Net;
using System.Text;
using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Infrastructure.Integrations.ApplicationMonitoring;

namespace ConsoleOps.Tests.Integration.ApplicationMonitoring;

public sealed class HttpApplicationProbeTests
{
    private const string CommitSha = "ABCDEF0123456789ABCDEF0123456789ABCDEF01";
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 8, 14, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProbeAsync_MapsSupportedHealthAndVersionPayloads()
    {
        RecordingHandler handler = new(request => request.RequestUri!.AbsolutePath switch
        {
            "/health" => JsonResponse("""
                {
                  "status": "Degraded",
                  "entries": {
                    "Database": { "status": "Healthy", "description": "not retained" },
                    "Payments": { "status": "Unhealthy" },
                    "Unsupported": { "status": "Unknown" }
                  }
                }
                """),
            "/version" => JsonResponse($$"""
                {
                  "application": "Spinner.Api",
                  "version": "1.4.2",
                  "commit": "{{CommitSha}}",
                  "environment": "Production",
                  "builtAt": "2026-08-14T13:30:00+08:00",
                  "secret": "not retained"
                }
                """),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        HttpApplicationProbe probe = CreateProbe(handler);

        ApplicationProbeResult result = await probe.ProbeAsync(
            new ApplicationProbeTarget(
                "https://application.example/health",
                "https://application.example/version"),
            CancellationToken.None);

        Assert.Equal(ApplicationHealthState.Degraded, result.Health.State);
        Assert.NotNull(result.Health.ResponseDuration);
        Assert.True(result.Health.ResponseDuration >= TimeSpan.Zero);
        Assert.Equal(ObservedAt, result.Health.ObservedAtUtc);
        Assert.Collection(
            result.Health.Dependencies,
            dependency =>
            {
                Assert.Equal("Database", dependency.Name);
                Assert.Equal(ApplicationHealthState.Healthy, dependency.State);
            },
            dependency =>
            {
                Assert.Equal("Payments", dependency.Name);
                Assert.Equal(ApplicationHealthState.Unhealthy, dependency.State);
            });

        Assert.Equal(ApplicationVersionState.Available, result.Version.State);
        Assert.Equal("Spinner.Api", result.Version.Application);
        Assert.Equal("1.4.2", result.Version.Version);
        Assert.Equal(CommitSha.ToLowerInvariant(), result.Version.CommitSha);
        Assert.Equal("Production", result.Version.Environment);
        Assert.Equal(DateTimeOffset.Parse("2026-08-14T05:30:00Z"), result.Version.BuiltAtUtc);
        Assert.Equal(ObservedAt, result.Version.ObservedAtUtc);

        CapturedRequest[] requests = handler.Requests.ToArray();
        Assert.Equal(2, requests.Length);
        Assert.All(requests, request =>
        {
            Assert.Equal("application/json", request.Accept);
            Assert.Equal("ConsoleOps/1.0", request.UserAgent);
        });
    }

    [Fact]
    public async Task ProbeAsync_WithoutConfiguredUrls_DoesNotSendRequests()
    {
        RecordingHandler handler = new(_ => throw new InvalidOperationException("No request expected."));
        HttpApplicationProbe probe = CreateProbe(handler);

        ApplicationProbeResult result = await probe.ProbeAsync(
            new ApplicationProbeTarget(null, null),
            CancellationToken.None);

        Assert.Equal(ApplicationHealthState.NotConfigured, result.Health.State);
        Assert.Null(result.Health.ResponseDuration);
        Assert.Equal(ApplicationVersionState.NotConfigured, result.Version.State);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ProbeAsync_WhenHealthHasNoSupportedBody_TreatsSuccessfulResponseAsHealthy()
    {
        RecordingHandler handler = new(request => request.RequestUri!.AbsolutePath == "/health"
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : JsonResponse(ValidVersionJson()));
        HttpApplicationProbe probe = CreateProbe(handler);

        ApplicationProbeResult result = await probe.ProbeAsync(
            new ApplicationProbeTarget(
                "https://application.example/health",
                "https://application.example/version"),
            CancellationToken.None);

        Assert.Equal(ApplicationHealthState.Healthy, result.Health.State);
        Assert.Empty(result.Health.Dependencies);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, null, ApplicationHealthState.Unhealthy)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "Degraded", ApplicationHealthState.Degraded)]
    [InlineData(HttpStatusCode.InternalServerError, "Healthy", ApplicationHealthState.Unhealthy)]
    public async Task ProbeAsync_MapsReachableNonSuccessHealthResponses(
        HttpStatusCode statusCode,
        string? payloadStatus,
        ApplicationHealthState expectedState)
    {
        RecordingHandler handler = new(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/version")
            {
                return JsonResponse(ValidVersionJson());
            }

            return payloadStatus is null
                ? new HttpResponseMessage(statusCode)
                : JsonResponse($$"""{ "status": "{{payloadStatus}}" }""", statusCode);
        });
        HttpApplicationProbe probe = CreateProbe(handler);

        ApplicationProbeResult result = await probe.ProbeAsync(
            new ApplicationProbeTarget(
                "https://application.example/health",
                "https://application.example/version"),
            CancellationToken.None);

        Assert.Equal(expectedState, result.Health.State);
    }

    [Fact]
    public async Task ProbeAsync_WhenHealthTransportFails_PreservesVersionObservation()
    {
        RecordingHandler handler = new(request => request.RequestUri!.AbsolutePath == "/health"
            ? throw new HttpRequestException("provider details")
            : JsonResponse(ValidVersionJson()));
        HttpApplicationProbe probe = CreateProbe(handler);

        ApplicationProbeResult result = await probe.ProbeAsync(
            new ApplicationProbeTarget(
                "https://application.example/health",
                "https://application.example/version"),
            CancellationToken.None);

        Assert.Equal(ApplicationHealthState.Unreachable, result.Health.State);
        Assert.NotNull(result.Health.ResponseDuration);
        Assert.Equal(ApplicationVersionState.Available, result.Version.State);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"commit\":\"123456\"}")]
    [InlineData("{\"commit\":\"not-a-sha\"}")]
    [InlineData("{not-json")]
    public async Task ProbeAsync_WhenVersionPayloadIsInvalid_ReturnsUnknown(string payload)
    {
        RecordingHandler handler = new(request => request.RequestUri!.AbsolutePath == "/health"
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : JsonResponse(payload));
        HttpApplicationProbe probe = CreateProbe(handler);

        ApplicationProbeResult result = await probe.ProbeAsync(
            new ApplicationProbeTarget(
                "https://application.example/health",
                "https://application.example/version"),
            CancellationToken.None);

        Assert.Equal(ApplicationVersionState.Unknown, result.Version.State);
        Assert.Null(result.Version.CommitSha);
    }

    [Fact]
    public async Task ProbeAsync_WhenResponseExceedsLimit_DoesNotParseBody()
    {
        string oversizedJson = "{\"commit\":\"" + CommitSha + "\",\"padding\":\""
            + new string('x', HttpApplicationProbe.MaximumResponseBytes)
            + "\"}";
        RecordingHandler handler = new(request => request.RequestUri!.AbsolutePath == "/health"
            ? JsonResponse(oversizedJson)
            : JsonResponse(oversizedJson));
        HttpApplicationProbe probe = CreateProbe(handler);

        ApplicationProbeResult result = await probe.ProbeAsync(
            new ApplicationProbeTarget(
                "https://application.example/health",
                "https://application.example/version"),
            CancellationToken.None);

        Assert.Equal(ApplicationHealthState.Healthy, result.Health.State);
        Assert.Empty(result.Health.Dependencies);
        Assert.Equal(ApplicationVersionState.Unknown, result.Version.State);
    }

    [Fact]
    public async Task ProbeAsync_WhenUrlSchemeIsUnsafe_DoesNotSendRequest()
    {
        RecordingHandler handler = new(_ => throw new InvalidOperationException("No request expected."));
        HttpApplicationProbe probe = CreateProbe(handler);

        ApplicationProbeResult result = await probe.ProbeAsync(
            new ApplicationProbeTarget("file:///etc/passwd", "ftp://application.example/version"),
            CancellationToken.None);

        Assert.Equal(ApplicationHealthState.Unreachable, result.Health.State);
        Assert.Equal(ApplicationVersionState.Unknown, result.Version.State);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ProbeAsync_WhenCallerCancels_PropagatesCancellation()
    {
        RecordingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK));
        HttpApplicationProbe probe = CreateProbe(handler);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.ProbeAsync(
            new ApplicationProbeTarget("https://application.example/health", null),
            cancellation.Token));
    }

    private static HttpApplicationProbe CreateProbe(HttpMessageHandler handler) => new(
        new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan },
        new FixedTimeProvider(ObservedAt));

    private static string ValidVersionJson() => $$"""
        {
          "application": "Spinner.Api",
          "version": "1.4.2",
          "commit": "{{CommitSha}}",
          "environment": "Production",
          "builtAt": "2026-08-14T05:30:00Z"
        }
        """;

    private static HttpResponseMessage JsonResponse(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public ConcurrentQueue<CapturedRequest> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Enqueue(new CapturedRequest(
                request.Headers.Accept.Single().MediaType,
                request.Headers.UserAgent.ToString()));
            return Task.FromResult(responder(request));
        }
    }

    private sealed record CapturedRequest(string? Accept, string UserAgent);
}
