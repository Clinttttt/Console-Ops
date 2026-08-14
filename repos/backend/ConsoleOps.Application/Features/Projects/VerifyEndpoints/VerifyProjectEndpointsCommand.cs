using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using FluentValidation;
using MediatR;

namespace ConsoleOps.Application.Features.Projects.VerifyEndpoints;

/// <summary>
/// Probes candidate endpoints before a project exists, so registration can be checked rather than
/// guessed at.
/// </summary>
/// <remarks>
/// URLs arrive absolute: the caller resolves any relative path against the application base URL, the
/// same way registration does. This command adds a caller to the existing probe, not a second probe
/// path, so every scheme, redirect, timeout, response-size and outbound-address safeguard still applies.
/// </remarks>
public sealed record VerifyProjectEndpointsCommand(string? HealthUrl, string? VersionUrl)
    : IRequest<Result<EndpointVerificationResponse>>;

public sealed record EndpointVerificationResponse(
    HealthVerificationResponse Health,
    VersionVerificationResponse Version,
    DateTimeOffset ObservedAt);

/// <param name="State">Camel-case health state such as <c>healthy</c> or <c>unreachable</c>.</param>
public sealed record HealthVerificationResponse(
    string State,
    int? ResponseMilliseconds,
    IReadOnlyList<DependencyVerificationResponse> Dependencies);

public sealed record DependencyVerificationResponse(string Name, string State);

/// <param name="State">Camel-case version state such as <c>available</c>.</param>
public sealed record VersionVerificationResponse(
    string State,
    string? Application,
    string? Version,
    string? CommitSha,
    string? CommitShortSha,
    DateTimeOffset? BuiltAt);

public sealed class VerifyProjectEndpointsCommandValidator
    : AbstractValidator<VerifyProjectEndpointsCommand>
{
    public VerifyProjectEndpointsCommandValidator()
    {
        RuleFor(command => command)
            .Must(command =>
                !string.IsNullOrWhiteSpace(command.HealthUrl)
                || !string.IsNullOrWhiteSpace(command.VersionUrl))
            .WithMessage("Provide a health endpoint, a version endpoint, or both.");

        RuleFor(command => command.HealthUrl)
            .Must(BeSafeAbsoluteUrl)
            .When(command => !string.IsNullOrWhiteSpace(command.HealthUrl))
            .WithMessage("The health endpoint must be an absolute http or https URL without credentials.");

        RuleFor(command => command.VersionUrl)
            .Must(BeSafeAbsoluteUrl)
            .When(command => !string.IsNullOrWhiteSpace(command.VersionUrl))
            .WithMessage("The version endpoint must be an absolute http or https URL without credentials.");
    }

    /// <summary>Mirrors the registration rule: absolute HTTP(S) and never a credentialed URL.</summary>
    private static bool BeSafeAbsoluteUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && string.IsNullOrEmpty(uri.UserInfo);
}

public sealed class VerifyProjectEndpointsCommandHandler(IApplicationProbe probe, TimeProvider timeProvider)
    : IRequestHandler<VerifyProjectEndpointsCommand, Result<EndpointVerificationResponse>>
{
    public async Task<Result<EndpointVerificationResponse>> Handle(
        VerifyProjectEndpointsCommand request,
        CancellationToken cancellationToken)
    {
        ApplicationProbeResult result = await probe.ProbeAsync(
            new ApplicationProbeTarget(
                NullIfWhiteSpace(request.HealthUrl),
                NullIfWhiteSpace(request.VersionUrl)),
            cancellationToken);

        // An unreachable application is an observation, never a validation failure: it may simply not
        // be deployed yet, and that must not block registration.
        return Result<EndpointVerificationResponse>.Success(new EndpointVerificationResponse(
            new HealthVerificationResponse(
                ToCamelCase(result.Health.State),
                ToMilliseconds(result.Health.ResponseDuration),
                result.Health.Dependencies
                    .Select(dependency => new DependencyVerificationResponse(
                        dependency.Name,
                        ToCamelCase(dependency.State)))
                    .ToArray()),
            new VersionVerificationResponse(
                ToCamelCase(result.Version.State),
                result.Version.Application,
                result.Version.Version,
                result.Version.CommitSha,
                ToShortSha(result.Version.CommitSha),
                result.Version.BuiltAtUtc),
            timeProvider.GetUtcNow()));
    }

    private static int? ToMilliseconds(TimeSpan? duration) =>
        duration is null ? null : (int)Math.Round(duration.Value.TotalMilliseconds);

    private static string? ToShortSha(string? commitSha) =>
        commitSha is { Length: >= 7 } ? commitSha[..7] : commitSha;

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Enums cross the wire as camel-case strings, as the dashboard response does.</summary>
    private static string ToCamelCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string name = value.ToString()!;
        return string.Concat(char.ToLowerInvariant(name[0]), name[1..]);
    }
}
