using ConsoleOps.Application.Integrations.GitHub;
using Microsoft.Extensions.Configuration;

namespace ConsoleOps.Infrastructure.Integrations.GitHub;

/// <summary>
/// The service token Console Ops is configured with.
/// </summary>
/// <remarks>
/// What scheduled collection uses, because it runs with no operator, and the fallback for anything else that is not
/// an operator's request. Read once: a token changing means a restart, which is true of every other credential here.
/// </remarks>
public sealed class ConfiguredGitHubCredential : IGitHubCredential
{
    private readonly string? token;

    public ConfiguredGitHubCredential(IConfiguration configuration)
    {
        string? configured = configuration["GitHub:Token"];
        token = string.IsNullOrWhiteSpace(configured) ? null : configured.Trim();
    }

    public ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(token);
}
