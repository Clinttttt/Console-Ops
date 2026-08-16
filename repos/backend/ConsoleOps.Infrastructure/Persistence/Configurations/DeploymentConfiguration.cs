using ConsoleOps.Domain.Projects;
using ConsoleOps.Infrastructure.Persistence.Deployments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConsoleOps.Infrastructure.Persistence.Configurations;

internal sealed class DeploymentConfiguration : IEntityTypeConfiguration<DeploymentEntity>
{
    public void Configure(EntityTypeBuilder<DeploymentEntity> builder)
    {
        builder.ToTable("deployments");
        builder.HasKey(entity => entity.Id).HasName("pk_deployments");
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(entity => entity.ExternalRunId).HasColumnName("external_run_id").IsRequired();
        builder.Property(entity => entity.RunNumber).HasColumnName("run_number");
        builder.Property(entity => entity.WorkflowFile).HasColumnName("workflow_file").HasMaxLength(255);
        builder.Property(entity => entity.WorkflowName).HasColumnName("workflow_name").HasMaxLength(200);
        builder.Property(entity => entity.Branch).HasColumnName("branch").HasMaxLength(255).IsRequired();
        builder.Property(entity => entity.CommitSha).HasColumnName("commit_sha").HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Result).HasColumnName("result").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(entity => entity.StartedAtUtc).HasColumnName("started_at_utc");
        builder.Property(entity => entity.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(entity => entity.TriggeredBy).HasColumnName("triggered_by").HasMaxLength(200);
        builder.Property(entity => entity.RunUrl).HasColumnName("run_url").HasMaxLength(500);
        builder.Property(entity => entity.RecordedAtUtc).HasColumnName("recorded_at_utc").IsRequired();
        builder.Property(entity => entity.ObservedAtUtc).HasColumnName("observed_at_utc").IsRequired();
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .HasConstraintName("fk_deployments_projects_project_id")
            .OnDelete(DeleteBehavior.Cascade);

        // One row per provider run: re-reading a run must update it, never duplicate it.
        builder.HasIndex(entity => new { entity.ProjectId, entity.ExternalRunId })
            .IsUnique()
            .HasDatabaseName("ix_deployments_project_external_run_id");
        builder.HasIndex(entity => new { entity.ProjectId, entity.CommitSha })
            .HasDatabaseName("ix_deployments_project_commit_sha");
        builder.HasIndex(entity => entity.StartedAtUtc)
            .HasDatabaseName("ix_deployments_started_at");
    }
}
