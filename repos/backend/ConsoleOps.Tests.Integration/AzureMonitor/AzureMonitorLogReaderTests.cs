using Azure.Core;
using Azure.Identity;
using Azure.Monitor.Query;
using ConsoleOps.Application.Integrations.AzureMonitor;
using ConsoleOps.Infrastructure.Integrations.AzureMonitor;

namespace ConsoleOps.Tests.Integration.AzureMonitor;

/// <summary>
/// How the log reader behaves when Azure cannot be asked at all.
/// <para>
/// A credential that cannot be established is not the provider refusing a query: no token is ever sent. It was
/// escaping the adapter as an unhandled fault, which reached the operator as a broken screen rather than as
/// "Console Ops could not authenticate", so it is pinned here.
/// </para>
/// </summary>
public sealed class AzureMonitorLogReaderTests
{
    private static readonly Guid Workspace = Guid.Parse("6f5c1a2b-3d4e-5f60-7182-93a4b5c6d7e8");

    [Fact]
    public async Task Read_WhenTheCredentialCannotBeEstablished_ReportsUnauthorized()
    {
        AzureMonitorLogReader reader = CreateReader(
            new ThrowingCredential(new CredentialUnavailableException(
                "VisualStudioCredential authentication failed: the account is not in this tenant")));

        ApplicationLogReadResult result = await reader.ReadAsync(Query(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationLogReadFailure.Unauthorized, result.Failure);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public async Task Read_WhenTheChainItselfFails_ReportsUnauthorizedRatherThanThrowing()
    {
        // The shape Azure.Identity produces when every source in the chain has been tried and failed.
        AzureMonitorLogReader reader = CreateReader(
            new ThrowingCredential(new AuthenticationFailedException("no credential provided a token")));

        ApplicationLogReadResult result = await reader.ReadAsync(Query(), CancellationToken.None);

        Assert.Equal(ApplicationLogReadFailure.Unauthorized, result.Failure);
    }

    [Fact]
    public async Task Read_WithASourceThatCannotBeReal_NeverContactsAzure()
    {
        ThrowingCredential credential = new(new AuthenticationFailedException("should not be reached"));
        AzureMonitorLogReader reader = CreateReader(credential);

        ApplicationLogReadResult result = await reader.ReadAsync(
            Query() with { WorkspaceId = Guid.Empty },
            CancellationToken.None);

        Assert.Equal(ApplicationLogReadFailure.NotFound, result.Failure);
        // A source that cannot be a real app is refused before a token is even requested.
        Assert.Equal(0, credential.Calls);
    }

    private static AzureMonitorLogReader CreateReader(TokenCredential credential) =>
        new(
            new LogsQueryClient(credential),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch),
            new AzureMonitorOptions());

    private static ApplicationLogQuery Query() =>
        new(
            Workspace,
            "spinner-api-stg",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddHours(1),
            200);

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class ThrowingCredential(Exception failure) : TokenCredential
    {
        public int Calls { get; private set; }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Calls++;
            throw failure;
        }

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            Calls++;
            throw failure;
        }
    }
}
