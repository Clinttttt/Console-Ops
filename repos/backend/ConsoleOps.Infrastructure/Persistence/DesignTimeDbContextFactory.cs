using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ConsoleOps.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build the model without starting the API host.
/// <para>
/// This exists because generating a migration through the API project fails whenever a Console Ops
/// instance is running and holding its output assemblies. Because a design-time factory takes precedence
/// over the API's host, it has to resolve the same connection string the API would; otherwise commands
/// that really touch the database - <c>database update</c>, <c>migrations list</c> - would target the
/// wrong server.
/// </para>
/// <para>
/// Resolution order: the <c>CONSOLEOPS_DESIGN_TIME_CONNECTION</c> override, then
/// <c>ConnectionStrings__DefaultConnection</c> from the environment, then
/// <c>ConnectionStrings:DefaultConnection</c> from the API's <c>appsettings</c> files. Values are read at
/// design time only: nothing is cached, logged, or written back, and no credential lives in source.
/// </para>
/// <para>
/// The settings files are read directly rather than through the configuration providers, so that a
/// design-time convenience does not add packages to a production assembly.
/// </para>
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ConsoleOpsDbContext>
{
    private const string OverrideVariable = "CONSOLEOPS_DESIGN_TIME_CONNECTION";
    private const string EnvironmentVariable = "ConnectionStrings__DefaultConnection";
    private const string ApiProjectDirectory = "ConsoleOps.Api";
    private const string ConnectionName = "DefaultConnection";

    /// <summary>
    /// Used only when no connection string is configured anywhere. Named so the provider's own failure
    /// message explains the cause instead of looking like a missing password.
    /// </summary>
    private const string UnconfiguredConnection =
        "Host=localhost;Database=console_ops_no_connection_string_configured";

    public ConsoleOpsDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<ConsoleOpsDbContext> options =
            new DbContextOptionsBuilder<ConsoleOpsDbContext>()
                .UseNpgsql(ResolveConnectionString())
                .Options;

        return new ConsoleOpsDbContext(options);
    }

    private static string ResolveConnectionString()
    {
        foreach (string variable in new[] { OverrideVariable, EnvironmentVariable })
        {
            string? value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        foreach (string basePath in GetSettingsDirectories())
        {
            foreach (string fileName in new[] { $"appsettings.{environment}.json", "appsettings.json" })
            {
                string? connectionString = ReadConnectionString(Path.Combine(basePath, fileName));
                if (connectionString is not null)
                {
                    return connectionString;
                }
            }
        }

        return UnconfiguredConnection;
    }

    private static string? ReadConnectionString(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(settingsPath);
            using JsonDocument document = JsonDocument.Parse(
                stream,
                new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("ConnectionStrings", out JsonElement connections)
                || connections.ValueKind != JsonValueKind.Object
                || !connections.TryGetProperty(ConnectionName, out JsonElement connection)
                || connection.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string? value = connection.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (JsonException)
        {
            // A malformed settings file is the API's problem to report, not this factory's.
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Where to look for the API's settings. <c>dotnet ef</c> runs in the startup project's directory:
    /// the API when a command is run there, and this project when a migration is scaffolded without
    /// building the API.
    /// </summary>
    private static IEnumerable<string> GetSettingsDirectories()
    {
        string current = Directory.GetCurrentDirectory();
        yield return current;

        DirectoryInfo? parent = Directory.GetParent(current);
        if (parent is not null)
        {
            yield return Path.Combine(parent.FullName, ApiProjectDirectory);
        }
    }
}
