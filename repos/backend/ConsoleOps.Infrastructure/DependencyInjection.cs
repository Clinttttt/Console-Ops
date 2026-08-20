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
using ConsoleOps.Application.Integrations.Diagnostics;
using ConsoleOps.Application.Features.Workflows;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Infrastructure.Integrations.ApplicationMonitoring;
using ConsoleOps.Infrastructure.Integrations.AzureMonitor;
using ConsoleOps.Infrastructure.Integrations.Diagnostics;
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
        services.AddScoped<Application.Features.Health.GetOverview.IHealthOverviewReadStore, HealthOverviewReadStore>();
        services.AddScoped<Application.Features.Logs.GetStream.ILogMarkerReadStore, LogMarkerReadStore>();
        AddAzureMonitor(services, configuration);
        services.AddHttpClient<IGitHubProjectReader, GitHubProjectReader>(client =>
            ConfigureGitHubClient(client, configuration));
        services.AddHttpClient<IGitHubRepositoryCatalog, GitHubRepositoryCatalog>(client =>
            ConfigureGitHubClient(client, configuration));

        // Workflows reads the provider during the request, like the log stream: bounded, read-only, and never
        // from stored observations, because a stale workflow list answers a question nobody asked.
        services.AddHttpClient<IGitHubWorkflowInventory, GitHubWorkflowInventory>(client =>
            ConfigureGitHubClient(client, configuration));
        IReadOnlySet<string> allowedPrivateHosts = GetAllowedPrivateProbeHosts(configuration);
        services.AddHttpClient<IApplicationProbe, HttpApplicationProbe>(client =>
            client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() =>
                ProbeHttpMessageHandlerFactory.Create(allowedPrivateHosts));
        AddIntegrationProbes(services, configuration);

        return services;
    }

    /// <summary>
    /// Credential checks for the configuration status report. Registered as a set so the report asks whatever
    /// probes exist, and adding an integration does not mean editing the report.
    /// </summary>
    private static void AddIntegrationProbes(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IConsoleOpsBuildInfo, ConsoleOpsBuildInfo>();
        services.AddScoped<IIntegrationProbe, DatabaseProbe>();
        services.AddScoped<IIntegrationProbe, AzureCredentialProbe>();
        services.AddScoped<IWorkflowRiskReadStore, WorkflowRiskReadStore>();
        services.AddScoped<IIntegrationProbe, GitHubTokenProbe>();
        services.AddHttpClient(nameof(GitHubTokenProbe), client =>
            ConfigureGitHubClient(client, configuration));
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

    /// <summary>
    /// The identity Console Ops reads Azure with: a service principal when all three keys are configured,
    /// otherwise the ambient identity of the machine it runs on.
    /// </summary>
    /// <remarks>
    /// The ambient chain is bounded rather than left at its defaults. Two sources are excluded deliberately:
    /// the Visual Studio token service, which reports a failure loudly when the signed-in account belongs to a
    /// different tenant than the subscription - observed here as an unhandled credential error on a log read -
    /// and the Visual Studio Code source, which is deprecated. What remains are the identities a service
    /// actually runs as: environment variables, a workload or managed identity, and the Azure CLI or Azure
    /// PowerShell session of the operator running it. Excluding sources that cannot apply also shortens the
    /// first call, which was measured at roughly five seconds of credential probing.
    /// </remarks>
    private static TokenCredential ResolveAzureCredential(IConfiguration configuration)
    {
        string? tenantId = configuration["Azure:TenantId"];
        string? clientId = configuration["Azure:ClientId"];
        string? clientSecret = configuration["Azure:ClientSecret"];

        if (!string.IsNullOrWhiteSpace(tenantId)
            && !string.IsNullOrWhiteSpace(clientId)
            && !string.IsNullOrWhiteSpace(clientSecret))
        {
            return new ClientSecretCredential(tenantId.Trim(), clientId.Trim(), clientSecret.Trim());
        }

        return new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeVisualStudioCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeInteractiveBrowserCredential = true,
        });
    }

    /// <summary>
    /// One place that decides how Console Ops talks to GitHub, so a second adapter cannot drift from
    /// the first on base address, timeout, or credential handling.
    /// </summary>
    private static void ConfigureGitHubClient(HttpClient client, IConfiguration configuration)
    {
        client.BaseAddress = new Uri("https://api.github.com/");
        client.Timeout = TimeSpan.FromSeconds(GetGitHubTimeoutSeconds(configuration));

        // GitHub rejects a request with no User-Agent, and the API version is negotiated by Accept. Both are
        // set here rather than per call so a new caller cannot forget them: the first one that did was answered
        // with 403 and reported it as a token without access.
        client.DefaultRequestHeaders.UserAgent.ParseAdd(GitHubProjectReader.UserAgent);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

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
