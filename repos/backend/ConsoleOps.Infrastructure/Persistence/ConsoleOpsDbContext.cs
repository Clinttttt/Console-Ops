using ConsoleOps.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace ConsoleOps.Infrastructure.Persistence;

public sealed class ConsoleOpsDbContext(DbContextOptions<ConsoleOpsDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectEnvironment> ProjectEnvironments => Set<ProjectEnvironment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConsoleOpsDbContext).Assembly);
    }
}
