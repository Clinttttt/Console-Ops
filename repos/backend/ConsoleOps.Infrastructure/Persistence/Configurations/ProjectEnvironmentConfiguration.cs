using ConsoleOps.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConsoleOps.Infrastructure.Persistence.Configurations;

internal sealed class ProjectEnvironmentConfiguration : IEntityTypeConfiguration<ProjectEnvironment>
{
    public void Configure(EntityTypeBuilder<ProjectEnvironment> builder)
    {
        builder.ToTable("project_environments");

        builder.HasKey(environment => environment.Id).HasName("pk_project_environments");

        builder.Property(environment => environment.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(environment => environment.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(environment => environment.Name)
            .HasColumnName("name")
            .HasMaxLength(ProjectRules.EnvironmentNameMaxLength)
            .IsRequired();

        builder.Property(environment => environment.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(ProjectRules.EnvironmentNameMaxLength)
            .IsRequired();

        builder.Property(environment => environment.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(environment => environment.ApplicationUrl)
            .HasColumnName("application_url")
            .HasMaxLength(ProjectRules.UrlMaxLength);

        builder.Property(environment => environment.HealthUrl)
            .HasColumnName("health_url")
            .HasMaxLength(ProjectRules.UrlMaxLength);

        builder.Property(environment => environment.VersionUrl)
            .HasColumnName("version_url")
            .HasMaxLength(ProjectRules.UrlMaxLength);

        builder.HasIndex(environment => new { environment.ProjectId, environment.NormalizedName })
            .IsUnique()
            .HasDatabaseName("ux_project_environments_project_name");
    }
}
