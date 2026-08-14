using ConsoleOps.Domain.Projects;
using ConsoleOps.Infrastructure.Persistence.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConsoleOps.Infrastructure.Persistence.Configurations;

internal sealed class SourceObservationConfiguration
    : IEntityTypeConfiguration<SourceObservationEntity>
{
    public void Configure(EntityTypeBuilder<SourceObservationEntity> builder)
    {
        builder.ToTable("source_observations");
        ConfigureObservationKey(builder);
        builder.Property(entity => entity.IsAvailable).HasColumnName("is_available").IsRequired();
        builder.Property(entity => entity.Repository).HasColumnName("repository").HasMaxLength(201).IsRequired();
        builder.Property(entity => entity.DefaultBranch).HasColumnName("default_branch").HasMaxLength(255).IsRequired();
        builder.Property(entity => entity.CommitSha).HasColumnName("commit_sha").HasMaxLength(64);
        builder.Property(entity => entity.ShortCommitSha).HasColumnName("short_commit_sha").HasMaxLength(12);
        builder.Property(entity => entity.CommittedAtUtc).HasColumnName("committed_at_utc");
        builder.Property(entity => entity.Failure).HasColumnName("failure").HasConversion<string>().HasMaxLength(30);
        builder.Property(entity => entity.ObservedAtUtc).HasColumnName("observed_at_utc").IsRequired();
        ConfigureProjectRelationship(builder, "fk_source_observations_projects_project_id");
        builder.HasIndex(entity => new { entity.ProjectId, entity.ObservedAtUtc })
            .HasDatabaseName("ix_source_observations_project_observed_at");
    }

    private static void ConfigureObservationKey(EntityTypeBuilder<SourceObservationEntity> builder)
    {
        builder.HasKey(entity => entity.Id).HasName("pk_source_observations");
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.ProjectId).HasColumnName("project_id").IsRequired();
    }

    private static void ConfigureProjectRelationship(
        EntityTypeBuilder<SourceObservationEntity> builder,
        string constraintName) =>
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .HasConstraintName(constraintName)
            .OnDelete(DeleteBehavior.Cascade);
}

internal sealed class WorkflowObservationConfiguration
    : IEntityTypeConfiguration<WorkflowObservationEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowObservationEntity> builder)
    {
        builder.ToTable("workflow_observations");
        builder.HasKey(entity => entity.Id).HasName("pk_workflow_observations");
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(entity => entity.WorkflowFile).HasColumnName("workflow_file").HasMaxLength(255);
        builder.Property(entity => entity.WorkflowName).HasColumnName("workflow_name").HasMaxLength(200);
        builder.Property(entity => entity.State).HasColumnName("state").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(entity => entity.CommitSha).HasColumnName("commit_sha").HasMaxLength(64);
        builder.Property(entity => entity.StartedAtUtc).HasColumnName("started_at_utc");
        builder.Property(entity => entity.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(entity => entity.Failure).HasColumnName("failure").HasConversion<string>().HasMaxLength(30);
        builder.Property(entity => entity.ObservedAtUtc).HasColumnName("observed_at_utc").IsRequired();
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .HasConstraintName("fk_workflow_observations_projects_project_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(entity => new { entity.ProjectId, entity.ObservedAtUtc })
            .HasDatabaseName("ix_workflow_observations_project_observed_at");
    }
}

internal sealed class HealthObservationConfiguration
    : IEntityTypeConfiguration<HealthObservationEntity>
{
    public void Configure(EntityTypeBuilder<HealthObservationEntity> builder)
    {
        builder.ToTable("health_observations");
        ConfigureEnvironmentObservation(builder, "pk_health_observations");
        builder.Property(entity => entity.State).HasColumnName("state").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(entity => entity.ResponseMilliseconds).HasColumnName("response_milliseconds");
        builder.Property(entity => entity.ObservedAtUtc).HasColumnName("observed_at_utc").IsRequired();
        ConfigureProjectRelationship(builder, "fk_health_observations_projects_project_id");
        builder.HasMany(entity => entity.Dependencies)
            .WithOne()
            .HasForeignKey(entity => entity.HealthObservationId)
            .HasConstraintName("fk_dependency_health_observations_health_observations_health_observation_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(entity => new { entity.ProjectId, entity.EnvironmentId, entity.ObservedAtUtc })
            .HasDatabaseName("ix_health_observations_project_environment_observed_at");
    }

    private static void ConfigureEnvironmentObservation(
        EntityTypeBuilder<HealthObservationEntity> builder,
        string keyName)
    {
        builder.HasKey(entity => entity.Id).HasName(keyName);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(entity => entity.EnvironmentId).HasColumnName("environment_id").IsRequired();
        builder.Property(entity => entity.EnvironmentName).HasColumnName("environment_name").HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.EnvironmentKind).HasColumnName("environment_kind").HasMaxLength(20).IsRequired();
    }

    private static void ConfigureProjectRelationship(
        EntityTypeBuilder<HealthObservationEntity> builder,
        string constraintName) =>
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .HasConstraintName(constraintName)
            .OnDelete(DeleteBehavior.Cascade);
}

internal sealed class DependencyHealthObservationConfiguration
    : IEntityTypeConfiguration<DependencyHealthObservationEntity>
{
    public void Configure(EntityTypeBuilder<DependencyHealthObservationEntity> builder)
    {
        builder.ToTable("dependency_health_observations");
        builder.HasKey(entity => entity.Id).HasName("pk_dependency_health_observations");
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.HealthObservationId).HasColumnName("health_observation_id").IsRequired();
        builder.Property(entity => entity.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.State).HasColumnName("state").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.HasIndex(entity => entity.HealthObservationId)
            .HasDatabaseName("ix_dependency_health_observations_health_observation_id");
    }
}

internal sealed class VersionObservationConfiguration
    : IEntityTypeConfiguration<VersionObservationEntity>
{
    public void Configure(EntityTypeBuilder<VersionObservationEntity> builder)
    {
        builder.ToTable("version_observations");
        ConfigureEnvironmentObservation(builder, "pk_version_observations");
        builder.Property(entity => entity.State).HasColumnName("state").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(entity => entity.Application).HasColumnName("application").HasMaxLength(200);
        builder.Property(entity => entity.Version).HasColumnName("version").HasMaxLength(200);
        builder.Property(entity => entity.CommitSha).HasColumnName("commit_sha").HasMaxLength(64);
        builder.Property(entity => entity.Environment).HasColumnName("reported_environment").HasMaxLength(200);
        builder.Property(entity => entity.BuiltAtUtc).HasColumnName("built_at_utc");
        builder.Property(entity => entity.ObservedAtUtc).HasColumnName("observed_at_utc").IsRequired();
        ConfigureProjectRelationship(builder, "fk_version_observations_projects_project_id");
        builder.HasIndex(entity => new { entity.ProjectId, entity.EnvironmentId, entity.ObservedAtUtc })
            .HasDatabaseName("ix_version_observations_project_environment_observed_at");
    }

    private static void ConfigureEnvironmentObservation(
        EntityTypeBuilder<VersionObservationEntity> builder,
        string keyName)
    {
        builder.HasKey(entity => entity.Id).HasName(keyName);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(entity => entity.EnvironmentId).HasColumnName("environment_id").IsRequired();
        builder.Property(entity => entity.EnvironmentName).HasColumnName("environment_name").HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.EnvironmentKind).HasColumnName("environment_kind").HasMaxLength(20).IsRequired();
    }

    private static void ConfigureProjectRelationship(
        EntityTypeBuilder<VersionObservationEntity> builder,
        string constraintName) =>
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .HasConstraintName(constraintName)
            .OnDelete(DeleteBehavior.Cascade);
}

internal sealed class VersionSyncObservationConfiguration
    : IEntityTypeConfiguration<VersionSyncObservationEntity>
{
    public void Configure(EntityTypeBuilder<VersionSyncObservationEntity> builder)
    {
        builder.ToTable("version_sync_observations");
        builder.HasKey(entity => entity.Id).HasName("pk_version_sync_observations");
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(entity => entity.EnvironmentId).HasColumnName("environment_id").IsRequired();
        builder.Property(entity => entity.EnvironmentName).HasColumnName("environment_name").HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.EnvironmentKind).HasColumnName("environment_kind").HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.State).HasColumnName("state").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(entity => entity.CommitsBehind).HasColumnName("commits_behind");
        builder.Property(entity => entity.ObservedAtUtc).HasColumnName("observed_at_utc").IsRequired();
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .HasConstraintName("fk_version_sync_observations_projects_project_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(entity => new { entity.ProjectId, entity.EnvironmentId, entity.ObservedAtUtc })
            .HasDatabaseName("ix_version_sync_observations_project_environment_observed_at");
    }
}

internal sealed class MonitoringActivityConfiguration
    : IEntityTypeConfiguration<MonitoringActivityEntity>
{
    public void Configure(EntityTypeBuilder<MonitoringActivityEntity> builder)
    {
        builder.ToTable("monitoring_activities");
        builder.HasKey(entity => entity.Id).HasName("pk_monitoring_activities");
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(entity => entity.EnvironmentId).HasColumnName("environment_id").IsRequired();
        builder.Property(entity => entity.EnvironmentName).HasColumnName("environment_name").HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(entity => entity.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .HasConstraintName("fk_monitoring_activities_projects_project_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(entity => new { entity.ProjectId, entity.OccurredAtUtc })
            .HasDatabaseName("ix_monitoring_activities_project_occurred_at");
    }
}
