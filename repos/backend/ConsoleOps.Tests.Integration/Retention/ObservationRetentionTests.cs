using System.Net.Http.Json;
using ConsoleOps.Api.BackgroundServices;
using ConsoleOps.Api.Features.Projects;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Application.Integrations.Diagnostics;
using ConsoleOps.Infrastructure.Persistence;
using ConsoleOps.Infrastructure.Persistence.Monitoring;
using ConsoleOps.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ConsoleOps.Tests.Integration.Retention;

/// <summary>
/// Retention is the only part of Console Ops that destroys recorded facts, so what it deletes and what it spares
/// are both pinned here, along with the window's floor.
/// </summary>
[Collection(ConsoleOpsApiCollection.Name)]
public sealed class ObservationRetentionTests(ConsoleOpsApiFactory factory)
{
    [Fact]
    public async Task Sweep_RemovesObservationsPastTheWindowAndKeepsTheRest()
    {
        using WebApplicationFactory<Program> application = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Monitoring:Retention:Enabled", "true");
            builder.UseSetting("Monitoring:Retention:Days", "7");
            // Long enough that the hosted service cannot sweep on its own while this test drives one directly.
            builder.UseSetting("Monitoring:Retention:StartupDelaySeconds", "3600");
        });

        // Observations reference a real project and environment, so one is registered rather than invented.
        (Guid projectId, Guid environmentId) = await RegisterAsync(application);
        await SeedAsync(application, projectId, environmentId);

        ObservationRetentionWorker worker = application.Services
            .GetServices<IHostedService>()
            .OfType<ObservationRetentionWorker>()
            .Single();

        await InvokeSweepAsync(worker);

        await using AsyncServiceScope scope = application.Services.CreateAsyncScope();
        ConsoleOpsDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConsoleOpsDbContext>();

        List<DateTimeOffset> remaining = await dbContext.HealthObservations
            .Where(row => row.EnvironmentId == environmentId)
            .Select(row => row.ObservedAtUtc)
            .ToListAsync();

        // The recent check survives; the one past the window is gone.
        Assert.Single(remaining);
        Assert.True(remaining[0] > DateTimeOffset.UtcNow.AddDays(-2));

        // The dependency rows of a deleted check go with it through the database's own cascade, so nothing is
        // left pointing at an observation that no longer exists.
        Assert.False(
            await dbContext.DependencyHealthObservations
                .AnyAsync(row => row.Name == "Retention probe"));

        // The sweep reports what it did, because deleting facts silently would be the worst version of this.
        IRetentionJournal journal = application.Services.GetRequiredService<IRetentionJournal>();
        RetentionSweep sweep = Assert.IsType<RetentionSweep>(journal.LastSweep);
        Assert.True(sweep.Succeeded);
        Assert.True(sweep.ObservationsRemoved >= 1);
        Assert.True(sweep.Before < DateTimeOffset.UtcNow.AddDays(-6));
    }

    [Fact]
    public void Window_HasAFloorSoAShortSettingCannotDeleteLiveHistory()
    {
        ObservationRetentionOptions options = new() { Days = 1 };

        // A one-day window would delete the history the availability figures and release verification read.
        Assert.Equal(7, options.Window.TotalDays);
    }

    [Fact]
    public void Batch_IsBoundedSoAFirstRunCannotHoldALongLock()
    {
        Assert.Equal(50_000, new ObservationRetentionOptions { BatchSize = 10_000_000 }.Batch);
        Assert.Equal(100, new ObservationRetentionOptions { BatchSize = 1 }.Batch);
    }

    private static async Task<(Guid ProjectId, Guid EnvironmentId)> RegisterAsync(
        WebApplicationFactory<Program> application)
    {
        using HttpClient client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

        string unique = Guid.NewGuid().ToString("N");
        RegisterProjectRequest request = new(
            $"Retention {unique}",
            "Observation retention integration test",
            new ProjectRepositoryRequest("console-ops-tests", $"retention-{unique}", "main", "deploy.yml"),
            [new RegisterProjectEnvironmentRequest("Production", "production", null, null, null, null)]);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/projects", request);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        ProjectResponse project = Assert.IsType<ProjectResponse>(
            await response.Content.ReadFromJsonAsync<ProjectResponse>());

        return (project.Id, Assert.Single(project.Environments).Id);
    }

    private static async Task InvokeSweepAsync(ObservationRetentionWorker worker)
    {
        // The sweep is private because nothing but the schedule should call it; the test drives it directly
        // rather than waiting out an interval.
        object? result = typeof(ObservationRetentionWorker)
            .GetMethod("SweepAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(worker, [CancellationToken.None]);

        await (Task)result!;
    }

    private static async Task SeedAsync(
        WebApplicationFactory<Program> application,
        Guid projectId,
        Guid environmentId)
    {
        await using AsyncServiceScope scope = application.Services.CreateAsyncScope();
        ConsoleOpsDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConsoleOpsDbContext>();

        Guid expiredId = Guid.CreateVersion7();
        dbContext.HealthObservations.AddRange(
            Observation(expiredId, projectId, environmentId, DateTimeOffset.UtcNow.AddDays(-30)),
            Observation(Guid.CreateVersion7(), projectId, environmentId, DateTimeOffset.UtcNow.AddMinutes(-5)));
        dbContext.DependencyHealthObservations.Add(new DependencyHealthObservationEntity
        {
            Id = Guid.CreateVersion7(),
            HealthObservationId = expiredId,
            Name = "Retention probe",
            State = ApplicationHealthState.Healthy,
        });

        await dbContext.SaveChangesAsync();
    }

    private static HealthObservationEntity Observation(
        Guid id,
        Guid projectId,
        Guid environmentId,
        DateTimeOffset observedAt) =>
        new()
        {
            Id = id,
            ProjectId = projectId,
            EnvironmentId = environmentId,
            EnvironmentName = "Production",
            EnvironmentKind = "production",
            State = ApplicationHealthState.Healthy,
            ResponseMilliseconds = 42,
            ObservedAtUtc = observedAt,
        };
}
