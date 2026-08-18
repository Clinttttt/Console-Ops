using ConsoleOps.Api.BackgroundServices;
using ConsoleOps.Application.Integrations.Diagnostics;

namespace ConsoleOps.Api.Extensions;

/// <summary>
/// Scheduled collection of the observations every screen reads.
/// <para>
/// The worker sends the same command the manual refresh endpoint does, so the two can never record different
/// facts. It is registered only when enabled, so a deployment that wants collection strictly on demand turns
/// it off rather than having a disabled worker running.
/// </para>
/// </summary>
public static class ScheduledCollectionExtensions
{
    public static IServiceCollection AddScheduledCollection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(ProjectRefreshOptions.SectionName);
        services.Configure<ProjectRefreshOptions>(section);

        ProjectRefreshOptions options = new();
        section.Bind(options);
        if (options.Enabled)
        {
            services.AddHostedService<ProjectRefreshWorker>();
        }

        AddRetention(services, configuration);
        return services;
    }

    /// <summary>
    /// Deleting recorded facts is opt-out rather than opt-in, because an instance nobody prunes grows for as long
    /// as it runs. The worker is registered only when enabled, so turning it off means nothing runs at all.
    /// </summary>
    private static void AddRetention(IServiceCollection services, IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(ObservationRetentionOptions.SectionName);
        services.Configure<ObservationRetentionOptions>(section);
        services.AddSingleton<IRetentionJournal, RetentionJournal>();

        ObservationRetentionOptions options = new();
        section.Bind(options);
        if (options.Enabled)
        {
            services.AddHostedService<ObservationRetentionWorker>();
        }
    }
}
