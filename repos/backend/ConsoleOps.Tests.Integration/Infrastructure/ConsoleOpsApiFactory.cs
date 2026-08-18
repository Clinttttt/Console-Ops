using ConsoleOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ConsoleOps.Tests.Integration.Infrastructure;

public sealed class ConsoleOpsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        ConsoleOpsDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConsoleOpsDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeResourcesAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());

        // Tests assert exactly what a given refresh recorded. A background sweep would add refreshes
        // nobody asked for, so scheduled collection is off unless a test opts in.
        builder.UseSetting("Monitoring:Refresh:Enabled", "false");
    }

    private async Task DisposeResourcesAsync()
    {
        await _postgres.DisposeAsync();
        Dispose();
    }
}
