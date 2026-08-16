using System.Net.Http.Headers;
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.Query;
using ConsoleOps.Application.Abstractions.Persistence;
using ConsoleOps.Application.Features.Dashboard.GetOverview;
using ConsoleOps.Application.Features.Deployments.GetHistory;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Features.Projects.RefreshProject;
using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Application.Integrations.AzureMonitor;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Infrastructure.Integrations.ApplicationMonitoring;
using ConsoleOps.Infrastructure.Integrations.AzureMonitor;
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
        services.AddScoped<IDeploymentHistoryReadStore, DeploymentHistoryReadStore>();
        services.AddScoped<Application.Features.Logs.GetStream.ILogMarkerReadStore, LogMarkerReadStore>();
        AddAzureMonitor(services, configuration);
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
    /// One place that decides how Console Ops reads Azure logs.
    /// <para>
    /// Read-only by construction: a Log Analytics query client and nothing else. The credential comes from
    /// Console Ops' own configuration - a service principal when all three keys are present, otherwise the
    /// ambient Azure identity - so no project row ever carries a secret. A missing or unauthorized
    /// credential surfaces later as a read failure, which the screens already render as unavailable rather
    /// than as an empty stream.
    /// </para>
    /// </summary>
    private static void AddAzureMonitor(IServiceCollection services, IConfiguration configuration)
    {
        AzureMonitorOptions options = new();
        configuration.GetSection(AzureMonitorOptions.SectionName).Bind(options);
        services.AddSingleton(options);

        TokenCredential credential = ResolveAzureCredential(configuration);
        services.AddSingleton(credential);
        services.AddSingleton(_ => new LogsQueryClient(credential));
        services.AddScoped<IApplicationLogReader, AzureMonitorLogReader>();

        // Resource inventory for the log-source picker. Read-only: it lists resources and changes nothing.
        services.AddHttpClient<IAzureLogSourceCatalog, AzureResourceGraphCatalog>(client =>
        {
            client.BaseAddress = new Uri("https://management.azure.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }

    private static TokenCredential ResolveAzureCredential(IConfiguration configuration)
    {
        string? tenantId = configuration["Azure:TenantId"];
        string? clientId = configuration["Azure:ClientId"];
        string? clientSecret = configuration["Azure:ClientSecret"];

        return string.IsNullOrWhiteSpace(tenantId)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret)
            ? new DefaultAzureCredential()
            : new ClientSecretCredential(tenantId.Trim(), clientId.Trim(), clientSecret.Trim());
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
