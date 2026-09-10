using ConsoleOps.Domain.Projects;
using ConsoleOps.Infrastructure.Persistence.Deployments;
using ConsoleOps.Infrastructure.Persistence.Monitoring;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ConsoleOps.Infrastructure.Persistence;

public sealed class ConsoleOpsDbContext(DbContextOptions<ConsoleOpsDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Project> Projects => Set<Project>();

    /// <summary>
    /// Where Data Protection keeps the keys that encrypt operators' GitHub tokens.
    /// </summary>
    /// <remarks>
    /// Kept in the database rather than the container filesystem because Console Ops runs with
    /// scale-to-zero: a replica is destroyed whenever it goes idle, and filesystem keys die with it,
    /// which would silently make every stored session undecryptable and sign every operator out.
    /// The database already holds the sessions, so it is the one place whose lifetime matches theirs.
    /// </remarks>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<ProjectEnvironment> ProjectEnvironments => Set<ProjectEnvironment>();

    public DbSet<ProjectWorkflowRisk> ProjectWorkflowRisks => Set<ProjectWorkflowRisk>();

    internal DbSet<Authentication.OperatorSessionEntity> OperatorSessions =>
        Set<Authentication.OperatorSessionEntity>();

    public DbSet<SourceObservationEntity> SourceObservations => Set<SourceObservationEntity>();

    public DbSet<WorkflowObservationEntity> WorkflowObservations => Set<WorkflowObservationEntity>();

    public DbSet<HealthObservationEntity> HealthObservations => Set<HealthObservationEntity>();

    public DbSet<DependencyHealthObservationEntity> DependencyHealthObservations =>
        Set<DependencyHealthObservationEntity>();

    public DbSet<VersionObservationEntity> VersionObservations => Set<VersionObservationEntity>();

    public DbSet<VersionSyncObservationEntity> VersionSyncObservations =>
        Set<VersionSyncObservationEntity>();

    public DbSet<MonitoringActivityEntity> MonitoringActivities => Set<MonitoringActivityEntity>();

    public DbSet<DeploymentEntity> Deployments => Set<DeploymentEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConsoleOpsDbContext).Assembly);
    }
}
