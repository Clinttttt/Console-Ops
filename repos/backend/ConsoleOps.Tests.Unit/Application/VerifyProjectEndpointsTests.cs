using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Projects.VerifyEndpoints;
using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using FluentValidation.Results;

namespace ConsoleOps.Tests.Unit.Application;

public sealed class VerifyProjectEndpointsTests
{
    private static readonly DateTimeOffset ObservedAt = new(2026, 8, 15, 4, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_ReportsHealthAndVersionTheProbeObserved()
    {
        StubProbe probe = new(new ApplicationProbeResult(
            new ApplicationHealthObservation(
                ApplicationHealthState.Healthy,
                TimeSpan.FromMilliseconds(103.4),
                ObservedAt,
                [new DependencyHealthObservation("Database", ApplicationHealthState.Degraded)]),
            new ApplicationVersionObservation(
                ApplicationVersionState.Available,
                "Spinner.Api",
                "1.5.0",
                "8a17c2f4e1b9d0a6c3f5e2b8d7a4c1f0e9b6d3a2",
                "Production",
                ObservedAt,
                ObservedAt)));

        Result<EndpointVerificationResponse> result = await Handle(
            probe,
            new VerifyProjectEndpointsCommand(
                "https://api.spinnerapp.com/health",
                "https://api.spinnerapp.com/version"));

        EndpointVerificationResponse response = result.Value;
        Assert.Equal("healthy", response.Health.State);
        Assert.Equal(103, response.Health.ResponseMilliseconds);
        DependencyVerificationResponse dependency = Assert.Single(response.Health.Dependencies);
        Assert.Equal("Database", dependency.Name);
        Assert.Equal("degraded", dependency.State);
        Assert.Equal("available", response.Version.State);
        Assert.Equal("1.5.0", response.Version.Version);
        Assert.Equal("8a17c2f", response.Version.CommitShortSha);
        Assert.Equal(ObservedAt, response.ObservedAt);
    }

    [Fact]
    public async Task Handle_TreatsAnUnreachableApplicationAsAnObservationNotAFailure()
    {
        StubProbe probe = new(new ApplicationProbeResult(
            new ApplicationHealthObservation(
                ApplicationHealthState.Unreachable,
                null,
                ObservedAt,
                []),
            new ApplicationVersionObservation(
                ApplicationVersionState.Unknown,
                null,
                null,
                null,
                null,
                null,
                ObservedAt)));

        Result<EndpointVerificationResponse> result = await Handle(
            probe,
            new VerifyProjectEndpointsCommand("https://api.spinnerapp.com/health", null));

        // Registration must not be blocked because the application is not deployed yet.
        Assert.True(result.IsSuccess);
        Assert.Equal("unreachable", result.Value.Health.State);
        Assert.Null(result.Value.Health.ResponseMilliseconds);
        Assert.Equal("unknown", result.Value.Version.State);
    }

    [Fact]
    public async Task Handle_PassesOnlyConfiguredEndpointsToTheProbe()
    {
        StubProbe probe = new(NotConfiguredResult());

        await Handle(probe, new VerifyProjectEndpointsCommand("  ", "https://api.spinnerapp.com/version"));

        Assert.Null(probe.LastTarget!.HealthUrl);
        Assert.Equal("https://api.spinnerapp.com/version", probe.LastTarget.VersionUrl);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    public void Validator_RequiresAtLeastOneEndpoint(string? healthUrl, string? versionUrl)
    {
        ValidationResult result = Validate(new VerifyProjectEndpointsCommand(healthUrl, versionUrl));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("api.spinnerapp.com/health")]
    [InlineData("ftp://api.spinnerapp.com/health")]
    [InlineData("https://user:secret@api.spinnerapp.com/health")]
    public void Validator_RejectsUnsafeOrRelativeUrls(string healthUrl)
    {
        ValidationResult result = Validate(new VerifyProjectEndpointsCommand(healthUrl, null));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_AcceptsAbsoluteHttpUrls()
    {
        ValidationResult result = Validate(new VerifyProjectEndpointsCommand(
            "http://localhost:7027/health",
            "https://api.spinnerapp.com/version"));

        Assert.True(result.IsValid);
    }

    private static Task<Result<EndpointVerificationResponse>> Handle(
        IApplicationProbe probe,
        VerifyProjectEndpointsCommand command) =>
        new VerifyProjectEndpointsCommandHandler(
                probe,
                new FixedTimeProvider(ObservedAt))
            .Handle(command, CancellationToken.None);

    private static ValidationResult Validate(VerifyProjectEndpointsCommand command) =>
        new VerifyProjectEndpointsCommandValidator().Validate(command);

    private static ApplicationProbeResult NotConfiguredResult() => new(
        new ApplicationHealthObservation(ApplicationHealthState.NotConfigured, null, ObservedAt, []),
        new ApplicationVersionObservation(
            ApplicationVersionState.NotConfigured,
            null,
            null,
            null,
            null,
            null,
            ObservedAt));

    private sealed class StubProbe(ApplicationProbeResult result) : IApplicationProbe
    {
        public ApplicationProbeTarget? LastTarget { get; private set; }

        public Task<ApplicationProbeResult> ProbeAsync(
            ApplicationProbeTarget target,
            CancellationToken cancellationToken)
        {
            LastTarget = target;
            return Task.FromResult(result);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
