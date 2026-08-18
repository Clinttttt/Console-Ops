using System.Reflection;
using System.Runtime.InteropServices;
using ConsoleOps.Application.Integrations.Diagnostics;
using ConsoleOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConsoleOps.Infrastructure.Integrations.Diagnostics;

/// <summary>
/// Reads the running build from the assembly, and the schema state from the database.
/// </summary>
internal sealed class ConsoleOpsBuildInfo(ConsoleOpsDbContext dbContext) : IConsoleOpsBuildInfo
{
    public async Task<ConsoleOpsBuild> ReadAsync(CancellationToken cancellationToken)
    {
        Assembly assembly = typeof(ConsoleOpsBuildInfo).Assembly;

        return new ConsoleOpsBuild(
            assembly.GetName().Version?.ToString(3) ?? "unknown",
            ReadSourceRevision(assembly),
            RuntimeInformation.FrameworkDescription,
            await ReadSchemaStateAsync(cancellationToken));
    }

    /// <summary>
    /// The commit the build came from, which SourceLink appends to the informational version as
    /// <c>1.2.3+&lt;sha&gt;</c>. A build without one reports nothing rather than a placeholder.
    /// </summary>
    private static string? ReadSourceRevision(Assembly assembly)
    {
        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        int separator = informational?.IndexOf('+') ?? -1;
        if (informational is null || separator < 0 || separator == informational.Length - 1)
        {
            return null;
        }

        string revision = informational[(separator + 1)..];
        return revision.Length > 7 ? revision[..7] : revision;
    }

    private async Task<string> ReadSchemaStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            IEnumerable<string> pending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
            return pending.Any() ? "pendingMigrations" : "upToDate";
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // A database that cannot be asked is unknown, not up to date. The distinction matters here.
            return "unknown";
        }
    }
}
