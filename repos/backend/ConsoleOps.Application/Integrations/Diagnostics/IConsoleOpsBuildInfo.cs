namespace ConsoleOps.Application.Integrations.Diagnostics;

/// <summary>
/// Which build of Console Ops is running, and whether its database schema matches it.
/// <para>
/// The first question worth answering when a screen looks wrong is "which build am I actually looking at?".
/// Every value here is read from the running assembly or the database, never configured by hand.
/// </para>
/// </summary>
public interface IConsoleOpsBuildInfo
{
    Task<ConsoleOpsBuild> ReadAsync(CancellationToken cancellationToken);
}

/// <param name="Build">
/// The source revision this assembly was built from, or <c>null</c> when the build did not record one. A
/// locally built binary has no revision, and inventing one would hide exactly that.
/// </param>
/// <param name="SchemaState">
/// <c>upToDate</c>, <c>pendingMigrations</c>, or <c>unknown</c> when the database could not be asked.
/// </param>
public sealed record ConsoleOpsBuild(
    string Version,
    string? Build,
    string Runtime,
    string SchemaState);
