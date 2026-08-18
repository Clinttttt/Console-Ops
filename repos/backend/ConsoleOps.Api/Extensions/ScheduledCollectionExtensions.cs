using ConsoleOps.Api.BackgroundServices;

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

        return services;
    }
}
