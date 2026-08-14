using ConsoleOps.Domain.Projects;
using ConsoleOps.Infrastructure.Persistence.Monitoring;
using Microsoft.EntityFrameworkCore;

namespace ConsoleOps.Infrastructure.Persistence;

public sealed class ConsoleOpsDbContext(DbContextOptions<ConsoleOpsDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectEnvironment> ProjectEnvironments => Set<ProjectEnvironment>();

    public DbSet<SourceObservationEntity> SourceObservations => Set<SourceObservationEntity>();

    public DbSet<WorkflowObservationEntity> WorkflowObservations => Set<WorkflowObservationEntity>();

    public DbSet<HealthObservationEntity> HealthObservations => Set<HealthObservationEntity>();

    public DbSet<DependencyHealthObservationEntity> DependencyHealthObservations =>
        Set<DependencyHealthObservationEntity>();

    public DbSet<VersionObservationEntity> VersionObservations => Set<VersionObservationEntity>();

    public DbSet<VersionSyncObservationEntity> VersionSyncObservations =>
        Set<VersionSyncObservationEntity>();

    public DbSet<MonitoringActivityEntity> MonitoringActivities => Set<MonitoringActivityEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConsoleOpsDbContext).Assembly);
    }
}
