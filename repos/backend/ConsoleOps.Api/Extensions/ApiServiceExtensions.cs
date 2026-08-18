using ConsoleOps.Api.BackgroundServices;
using ConsoleOps.Api.Configuration;
using ConsoleOps.Application.Integrations.Diagnostics;

namespace ConsoleOps.Api.Extensions;

/// <summary>
/// Services the API layer itself provides to the Application layer.
/// </summary>
public static class ApiServiceExtensions
{
    /// <summary>
    /// Configuration inspection lives here rather than in Infrastructure: whether a key is required depends on
    /// the addresses the host is listening on.
    /// </summary>
    public static IServiceCollection AddConsoleOpsConfigurationInspection(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationInspector, ConfigurationInspector>();
        services.AddSingleton<IProbeJournal, ProbeJournal>();
        services.AddSingleton<CollectionJournal>();
        services.AddSingleton<ICollectionJournal>(provider => provider.GetRequiredService<CollectionJournal>());
        services.AddSingleton<ProjectCollectionSweeper>();
        return services;
    }
}