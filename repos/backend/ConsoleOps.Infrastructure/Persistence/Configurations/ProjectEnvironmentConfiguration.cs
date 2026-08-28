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

        // Owned rather than two loose columns: the domain refuses half a log source, and this keeps that
        // rule expressible in one place. Both columns are null together when no source is configured.
        builder.OwnsOne(environment => environment.LogSource, logSource =>
        {
            logSource.Property(source => source.WorkspaceId)
                .HasColumnName("azure_log_workspace_id");

            logSource.Property(source => source.ContainerAppName)
                .HasColumnName("azure_log_container_app_name")
                // Sized for the longest name any supported platform allows, so an App Service site is storable.
                // The column keeps its original name: renaming it would rewrite a live column for cosmetics.
                .HasMaxLength(AzureLogSource.SiteNameMaxLength);

            // Stored rather than inferred from the name, because two platforms can hold the same name and the
            // platform decides which table is read. Existing rows are container apps: nothing else could be saved.
            logSource.Property(source => source.Platform)
                .HasColumnName("azure_log_platform")
                .HasConversion<string>()
                .HasMaxLength(32)
                .HasDefaultValue(AzureLogPlatform.ContainerApp);
        });

        builder.HasIndex(environment => new { environment.ProjectId, environment.NormalizedName })
            .IsUnique()
            .HasDatabaseName("ux_project_environments_project_name");
    }
}
