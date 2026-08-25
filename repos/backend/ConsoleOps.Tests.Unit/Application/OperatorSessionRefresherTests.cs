using ConsoleOps.Application.Features.Authentication;
using ConsoleOps.Application.Integrations.GitHub;

namespace ConsoleOps.Tests.Unit.Application;

/// <summary>
/// The one judgement about when a session is over, now that both the session read and the GitHub credential ask for
/// a fresh token.
/// </summary>
public sealed class OperatorSessionRefresherTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Leaves_a_token_alone_while_it_has_time_left()
    {
        RecordingSessionStore sessions = new();
        StubAuthentication github = new();
        OperatorSession session = Session(expiresAt: Now.AddHours(4));

        OperatorSession? result = await Refresher(sessions, github).EnsureFreshAsync(session, default);

        Assert.Same(session, result);
        Assert.Equal(0, github.RefreshCalls);
        Assert.Empty(sessions.Saved);
        Assert.Empty(sessions.Deleted);
    }

    [Fact]
    public async Task Renews_a_token_that_is_about_to_expire()
    {
        RecordingSessionStore sessions = new();
        StubAuthentication github = new()
        {
            Refreshed = GitHubAuthenticationResult<GitHubUserToken>.Success(
                new GitHubUserToken("renewed-token", Now.AddHours(8), "renewed-refresh", Now.AddMonths(6))),
        };
        OperatorSession session = Session(expiresAt: Now.AddMinutes(5));

        OperatorSession? result = await Refresher(sessions, github).EnsureFreshAsync(session, default);

        Assert.NotNull(result);
        Assert.Equal("renewed-token", result.AccessToken);
        Assert.Equal("renewed-refresh", result.RefreshToken);
        Assert.Equal(Now, result.LastSeenAtUtc);
        Assert.Single(sessions.Saved);
        Assert.Empty(sessions.Deleted);
    }

    /// <summary>GitHub does not always issue a new refresh token, and losing the old one would sign the operator out.</summary>
    [Fact]
    public async Task Keeps_the_existing_refresh_token_when_GitHub_returns_none()
    {
        RecordingSessionStore sessions = new();
        StubAuthentication github = new()
        {
            Refreshed = GitHubAuthenticationResult<GitHubUserToken>.Success(
                new GitHubUserToken("renewed-token", Now.AddHours(8), null, null)),
        };
        OperatorSession session = Session(expiresAt: Now.AddMinutes(5));

        OperatorSession? result = await Refresher(sessions, github).EnsureFreshAsync(session, default);

        Assert.NotNull(result);
        Assert.Equal("stored-refresh", result.RefreshToken);
        Assert.Equal(session.RefreshTokenExpiresAtUtc, result.RefreshTokenExpiresAtUtc);
    }

    [Fact]
    public async Task Ends_the_session_when_GitHub_rejects_the_refresh_token()
    {
        RecordingSessionStore sessions = new();
        StubAuthentication github = new()
        {
            Refreshed = GitHubAuthenticationResult<GitHubUserToken>.Failed(GitHubAuthenticationFailure.Rejected),
        };
        OperatorSession session = Session(expiresAt: Now.AddMinutes(5));

        OperatorSession? result = await Refresher(sessions, github).EnsureFreshAsync(session, default);

        Assert.Null(result);
        Assert.Contains(session.Id, sessions.Deleted);
        Assert.Empty(sessions.Saved);
    }

    /// <summary>
    /// The distinction the whole class exists for: an unreachable provider is not a signed-out operator.
    /// </summary>
    [Fact]
    public async Task Keeps_the_session_when_GitHub_cannot_be_reached()
    {
        RecordingSessionStore sessions = new();
        StubAuthentication github = new()
        {
            Refreshed = GitHubAuthenticationResult<GitHubUserToken>.Failed(GitHubAuthenticationFailure.Unavailable),
        };
        OperatorSession session = Session(expiresAt: Now.AddMinutes(5));

        OperatorSession? result = await Refresher(sessions, github).EnsureFreshAsync(session, default);

        Assert.Same(session, result);
        Assert.Empty(sessions.Deleted);
        Assert.Empty(sessions.Saved);
    }

    [Fact]
    public async Task Ends_a_session_that_has_no_refresh_token()
    {
        RecordingSessionStore sessions = new();
        StubAuthentication github = new();
        OperatorSession session = Session(expiresAt: Now.AddMinutes(5)) with { RefreshToken = null };

        OperatorSession? result = await Refresher(sessions, github).EnsureFreshAsync(session, default);

        Assert.Null(result);
        Assert.Contains(session.Id, sessions.Deleted);
        Assert.Equal(0, github.RefreshCalls);
    }

    [Fact]
    public async Task Ends_a_session_whose_refresh_token_has_expired()
    {
        RecordingSessionStore sessions = new();
        StubAuthentication github = new();
        OperatorSession session = Session(expiresAt: Now.AddMinutes(5)) with
        {
            RefreshTokenExpiresAtUtc = Now.AddMinutes(-1),
        };

        OperatorSession? result = await Refresher(sessions, github).EnsureFreshAsync(session, default);

        Assert.Null(result);
        Assert.Contains(session.Id, sessions.Deleted);
        Assert.Equal(0, github.RefreshCalls);
    }

    private static OperatorSessionRefresher Refresher(
        IOperatorSessionStore sessions,
        IGitHubUserAuthentication github) =>
        new(sessions, github, new FixedTimeProvider(Now));

    private static OperatorSession Session(DateTimeOffset expiresAt) => new(
        Guid.CreateVersion7(),
        12345,
        "Clinttttt",
        null,
        "stored-token",
        expiresAt,
        "stored-refresh",
        Now.AddMonths(5),
        Now.AddMinutes(-30),
        Now.AddMinutes(-1));

    private sealed class RecordingSessionStore : IOperatorSessionStore
    {
        public List<OperatorSession> Saved { get; } = [];

        public List<Guid> Deleted { get; } = [];

        public Task SaveAsync(OperatorSession session, CancellationToken cancellationToken)
        {
            Saved.Add(session);
            return Task.CompletedTask;
        }

        public Task<OperatorSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<OperatorSession?>(null);

        public Task TouchAsync(Guid sessionId, DateTimeOffset seenAtUtc, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            Deleted.Add(sessionId);
            return Task.CompletedTask;
        }

        public Task<int> DeleteExpiredAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken) =>
            Task.FromResult(0);
    }

    private sealed class StubAuthentication : IGitHubUserAuthentication
    {
        public GitHubAuthenticationResult<GitHubUserToken> Refreshed { get; init; } =
            GitHubAuthenticationResult<GitHubUserToken>.Failed(GitHubAuthenticationFailure.Unavailable);

        public int RefreshCalls { get; private set; }

        public Uri BuildAuthorizationUrl(string state, string redirectUri) => new("https://github.test/authorize");

        public Task<GitHubAuthenticationResult<GitHubUserToken>> ExchangeCodeAsync(
            string code,
            string redirectUri,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Refreshing does not exchange a code.");

        public Task<GitHubAuthenticationResult<GitHubUserToken>> RefreshAsync(
            string refreshToken,
            CancellationToken cancellationToken)
        {
            RefreshCalls++;
            return Task.FromResult(Refreshed);
        }

        public Task<GitHubAuthenticationResult<GitHubUserIdentity>> ReadUserAsync(
            string accessToken,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Refreshing does not read the user.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
