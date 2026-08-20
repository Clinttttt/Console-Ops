using ConsoleOps.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConsoleOps.Infrastructure.Persistence.Configurations;

internal sealed class ProjectWorkflowRiskConfiguration : IEntityTypeConfiguration<ProjectWorkflowRisk>
{
    public void Configure(EntityTypeBuilder<ProjectWorkflowRisk> builder)
    {
        builder.ToTable("project_workflow_risks");

        builder.HasKey(risk => risk.Id).HasName("pk_project_workflow_risks");

        builder.Property(risk => risk.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(risk => risk.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(risk => risk.WorkflowPath)
            .HasColumnName("workflow_path")
            .HasMaxLength(ProjectRules.WorkflowPathMaxLength)
            .IsRequired();

        builder.Property(risk => risk.NormalizedWorkflowPath)
            .HasColumnName("normalized_workflow_path")
            .HasMaxLength(ProjectRules.WorkflowPathMaxLength)
            .IsRequired();

        builder.Property(risk => risk.Level)
            .HasColumnName("level")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(risk => risk.DecidedAtUtc)
            .HasColumnName("decided_at_utc")
            .IsRequired();

        // One decision per workflow: a second row for the same file would let the two disagree about whether
        // running it is destructive, and nothing could say which one won.
        builder.HasIndex(risk => new { risk.ProjectId, risk.NormalizedWorkflowPath })
            .IsUnique()
            .HasDatabaseName("ux_project_workflow_risks_project_path");
    }
}
