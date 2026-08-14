using ConsoleOps.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConsoleOps.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(project => project.Id).HasName("pk_projects");

        builder.Property(project => project.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(project => project.Name)
            .HasColumnName("name")
            .HasMaxLength(ProjectRules.NameMaxLength)
            .IsRequired();

        builder.Property(project => project.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(ProjectRules.NameMaxLength)
            .IsRequired();

        builder.Property(project => project.Description)
            .HasColumnName("description")
            .HasMaxLength(ProjectRules.DescriptionMaxLength);

        builder.Property(project => project.RepositoryOwner)
            .HasColumnName("repository_owner")
            .HasMaxLength(ProjectRules.RepositoryOwnerMaxLength)
            .IsRequired();

        builder.Property(project => project.NormalizedRepositoryOwner)
            .HasColumnName("normalized_repository_owner")
            .HasMaxLength(ProjectRules.RepositoryOwnerMaxLength)
            .IsRequired();

        builder.Property(project => project.RepositoryName)
            .HasColumnName("repository_name")
            .HasMaxLength(ProjectRules.RepositoryNameMaxLength)
            .IsRequired();

        builder.Property(project => project.NormalizedRepositoryName)
            .HasColumnName("normalized_repository_name")
            .HasMaxLength(ProjectRules.RepositoryNameMaxLength)
            .IsRequired();

        builder.Property(project => project.DefaultBranch)
            .HasColumnName("default_branch")
            .HasMaxLength(ProjectRules.DefaultBranchMaxLength)
            .IsRequired();

        builder.Property(project => project.WorkflowFile)
            .HasColumnName("workflow_file")
            .HasMaxLength(ProjectRules.WorkflowFileMaxLength);

        builder.Property(project => project.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(project => project.NormalizedName)
            .IsUnique()
            .HasDatabaseName("ux_projects_normalized_name");

        builder.HasIndex(project => new
        {
            project.NormalizedRepositoryOwner,
            project.NormalizedRepositoryName
        })
            .IsUnique()
            .HasDatabaseName("ux_projects_repository");

        builder.HasMany(project => project.Environments)
            .WithOne()
            .HasForeignKey(environment => environment.ProjectId)
            .HasConstraintName("fk_project_environments_projects_project_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(project => project.Environments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
