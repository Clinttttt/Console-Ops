using System.Net.Http.Headers;
using ConsoleOps.Application.Abstractions.Persistence;
using ConsoleOps.Application.Features.Dashboard.GetOverview;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Features.Projects.RefreshProject;
using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Infrastructure.Integrations.ApplicationMonitoring;
using ConsoleOps.Infrastructure.Integrations.GitHub;
using ConsoleOps.Infrastructure.Persistence;
using ConsoleOps.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleOps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Required database connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<ConsoleOpsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectReadStore, ProjectReadStore>();
        services.AddScoped<IProjectRefreshStore, ProjectRefreshStore>();
        services.AddScoped<IDashboardOverviewReadStore, DashboardOverviewReadStore>();
        services.AddHttpClient<IGitHubProjectReader, GitHubProjectReader>(client =>
            ConfigureGitHubClient(client, configuration));
        services.AddHttpClient<IGitHubRepositoryCatalog, GitHubRepositoryCatalog>(client =>
            ConfigureGitHubClient(client, configuration));
        IReadOnlySet<string> allowedPrivateHosts = GetAllowedPrivateProbeHosts(configuration);
        services.AddHttpClient<IApplicationProbe, HttpApplicationProbe>(client =>
            client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() =>
                ProbeHttpMessageHandlerFactory.Create(allowedPrivateHosts));

        return services;
    }

    /// <summary>
    /// One place that decides how Console Ops talks to GitHub, so a second adapter cannot drift from
    /// the first on base address, timeout, or credential handling.
    /// </summary>
    private static void ConfigureGitHubClient(HttpClient client, IConfiguration configuration)
    {
        client.BaseAddress = new Uri("https://api.github.com/");
        client.Timeout = TimeSpan.FromSeconds(GetGitHubTimeoutSeconds(configuration));

        string? token = configuration["GitHub:Token"];
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token.Trim());
        }
    }

    private static int GetGitHubTimeoutSeconds(IConfiguration configuration)
    {
        const int defaultTimeoutSeconds = 10;
        const int maximumTimeoutSeconds = 60;

        return int.TryParse(configuration["GitHub:TimeoutSeconds"], out int configuredSeconds)
            ? Math.Clamp(configuredSeconds, 1, maximumTimeoutSeconds)
            : defaultTimeoutSeconds;
    }

    private static IReadOnlySet<string> GetAllowedPrivateProbeHosts(IConfiguration configuration)
    {
        string? configuredHosts = configuration["ApplicationProbes:AllowedPrivateHosts"];
        if (string.IsNullOrWhiteSpace(configuredHosts))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return configuredHosts
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(OutboundAddressPolicy.NormalizeHost)
            .Where(host => host.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
