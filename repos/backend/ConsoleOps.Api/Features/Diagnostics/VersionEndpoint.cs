using ConsoleOps.Application.Integrations.Diagnostics;

namespace ConsoleOps.Api.Features.Diagnostics;

/// <summary>
/// Reports which build is running, in the shape Console Ops expects every monitored application to use.
/// </summary>
/// <remarks>
/// <para>
/// Console Ops asks other applications for a version and reads a commit from the answer. It could not answer that
/// question about itself, so its own Deployment column read "Not configured" - accurate, and a poor advertisement
/// for the contract it asks others to honour.
/// </para>
/// <para>
/// Mapped at the root rather than under <c>/api</c>, so it is reachable without a session: a version probe arrives
/// with no credential, which is the whole point of it being a probe. That makes the running commit and environment
/// name public, which is normal for a version endpoint and is all this exposes - no configuration, no dependency
/// state, nothing about who is signed in.
/// </para>
/// <para>
/// The commit is whatever the build recorded, and <c>null</c> when it recorded nothing. A locally built binary has
/// no revision, and a short or invented value would be worse than an absent one: version comparison is only
/// deterministic on a full commit, so a guess would produce a confident and wrong verdict.
/// </para>
/// </remarks>
public static class VersionEndpoint
{
    public static IEndpointRouteBuilder MapVersionEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/version", Handle)
            .WithName("GetVersion")
            .WithTags("Diagnostics")
            .WithSummary("Reports the running build.")
            .WithDescription(
                "The same shape Console Ops reads from other applications: application, version, commit and "
                + "environment. The commit is absent when the build did not record one.")
            .Produces<VersionResponse>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<IResult> Handle(
        IConsoleOpsBuildInfo builds,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        ConsoleOpsBuild build = await builds.ReadAsync(cancellationToken);

        return Results.Ok(new VersionResponse(
            "Console Ops",
            build.Version,
            FullCommitOrNull(build.Build),
            environment.EnvironmentName));
    }

    /// <summary>
    /// The revision only when it is a whole commit.
    /// </summary>
    /// <remarks>
    /// A local build stamps an abbreviated hash - seven characters - and Console Ops refuses a short commit when it
    /// reads one from another application, because version comparison is only deterministic on a full commit. It
    /// would be indefensible to send what it will not accept, so a short value is reported as no commit at all. The
    /// deployment build passes the full sha explicitly.
    /// </remarks>
    private static string? FullCommitOrNull(string? revision) =>
        revision is { Length: 40 } commit && commit.All(Uri.IsHexDigit) ? commit : null;
}

/// <param name="Commit">
/// The full source revision this build came from, or <c>null</c> when it recorded none. Never shortened: Console
/// Ops refuses a short commit from other applications, so it must not send one either.
/// </param>
public sealed record VersionResponse(
    string Application,
    string Version,
    string? Commit,
    string Environment);
