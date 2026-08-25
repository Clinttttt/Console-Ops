using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Authentication;
using ConsoleOps.Application.Features.Authentication.GetSession;
using ConsoleOps.Application.Features.Authentication.SignIn;
using ConsoleOps.Application.Integrations.GitHub;

namespace ConsoleOps.Tests.Integration.Authentication;

/// <summary>
/// Signing in with GitHub.
/// </summary>
/// <remarks>
/// The behaviour worth pinning is what happens to an account that is not an operator, and what happens to a session
/// whose token is running out. Everything else about the flow is GitHub's.
/// </remarks>
public sealed class SignInWithGitHubTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SignIn_RecordsASessionForAnOperator()
    {
        FakeGitHubUserAuthentication github = new();
        InMemoryOperatorSessionStore sessions = new();

        Result<SignedInOperatorResponse> result = await Handler(github, sessions, ["Clinttttt"])
            .Handle(new SignInWithGitHubCommand("a-code", "https://console-ops.vercel.app/api/auth/github/callback"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Clinttttt", result.Value.Login);

        OperatorSession stored = Assert.Single(sessions.Saved);
        Assert.Equal("token-from-github", stored.AccessToken);
        Assert.Equal("refresh-from-github", stored.RefreshToken);
        // The code was exchanged against the callback it was issued for, which is what GitHub checks.
        Assert.Equal("https://console-ops.vercel.app/api/auth/github/callback", github.RedirectUriUsed);
    }

    [Fact]
    public async Task SignIn_RefusesAnAccountThatIsNotAnOperatorAndStoresNothing()
    {
        FakeGitHubUserAuthentication github = new() { Login = "octocat" };
        InMemoryOperatorSessionStore sessions = new();

        Result<SignedInOperatorResponse> result = await Handler(github, sessions, ["Clinttttt"])
            .Handle(new SignInWithGitHubCommand("a-code", "https://example.test/callback"), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("Auth.NotAnOperator", result.Error.Code);
        // Authorizing the App proves who somebody is, not that they belong here - and nothing is kept for them.
        Assert.Empty(sessions.Saved);
    }

    [Fact]
    public async Task SignIn_RefusesEverybodyWhenNoOperatorsAreConfigured()
    {
        FakeGitHubUserAuthentication github = new();
        InMemoryOperatorSessionStore sessions = new();

        Result<SignedInOperatorResponse> result = await Handler(github, sessions, [])
            .Handle(new SignInWithGitHubCommand("a-code", "https://example.test/callback"), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("Auth.NoOperatorsConfigured", result.Error.Code);
        // Failing closed: the code is never even exchanged, so an unconfigured deployment cannot be signed into.
        Assert.Equal(0, github.Exchanges);
    }

    [Fact]
    public async Task Session_ReportsWhoIsSignedIn()
    {
        InMemoryOperatorSessionStore sessions = new();
        OperatorSession session = Session();
        await sessions.SaveAsync(session, default);

        Result<OperatorSessionResponse> result = await SessionHandler(new FakeGitHubUserAuthentication(), sessions, ["Clinttttt"])
            .Handle(new GetOperatorSessionQuery(session.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Clinttttt", result.Value.Login);
        Assert.Equal("https://avatars.test/clint.png", result.Value.AvatarUrl);
    }

    [Fact]
    public async Task Session_ReportsNoSessionForAnUnknownCookie()
    {
        Result<OperatorSessionResponse> result = await SessionHandler(
                new FakeGitHubUserAuthentication(),
                new InMemoryOperatorSessionStore(),
                ["Clinttttt"])
            .Handle(new GetOperatorSessionQuery(Guid.CreateVersion7()), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("Auth.NoSession", result.Error.Code);
    }

    [Fact]
    public async Task Session_DropsAnOperatorWhoHasBeenRemovedFromTheList()
    {
        InMemoryOperatorSessionStore sessions = new();
        OperatorSession session = Session();
        await sessions.SaveAsync(session, default);

        Result<OperatorSessionResponse> result = await SessionHandler(
                new FakeGitHubUserAuthentication(),
                sessions,
                ["somebody-else"])
            .Handle(new GetOperatorSessionQuery(session.Id), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("Auth.NotAnOperator", result.Error.Code);
        // Removing an operator takes effect now rather than whenever their token happened to expire.
        Assert.Empty(sessions.Saved);
    }

    [Fact]
    public async Task Session_RenewsATokenThatIsAboutToExpire()
    {
        FakeGitHubUserAuthentication github = new();
        InMemoryOperatorSessionStore sessions = new();
        OperatorSession session = Session() with { AccessTokenExpiresAtUtc = Now.AddMinutes(2) };
        await sessions.SaveAsync(session, default);

        Result<OperatorSessionResponse> result = await SessionHandler(github, sessions, ["Clinttttt"])
            .Handle(new GetOperatorSessionQuery(session.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, github.Refreshes);
        // A tab left open keeps working, without a background job whose only purpose is holding a token alive.
        Assert.Equal("token-from-github", Assert.Single(sessions.Saved).AccessToken);
    }

    [Fact]
    public async Task Session_KeepsTheSessionWhenTheProviderCouldNotBeReached()
    {
        FakeGitHubUserAuthentication github = new()
        {
            RefreshFailure = GitHubAuthenticationFailure.Unavailable,
        };
        InMemoryOperatorSessionStore sessions = new();
        OperatorSession session = Session() with { AccessTokenExpiresAtUtc = Now.AddMinutes(2) };
        await sessions.SaveAsync(session, default);

        Result<OperatorSessionResponse> result = await SessionHandler(github, sessions, ["Clinttttt"])
            .Handle(new GetOperatorSessionQuery(session.Id), default);

        // GitHub being unreachable is not the operator being signed out.
        Assert.True(result.IsSuccess);
        Assert.Single(sessions.Saved);
    }

    [Fact]
    public async Task Session_EndsWhenGitHubRejectsTheRefreshToken()
    {
        FakeGitHubUserAuthentication github = new()
        {
            RefreshFailure = GitHubAuthenticationFailure.Rejected,
        };
        InMemoryOperatorSessionStore sessions = new();
        OperatorSession session = Session() with { AccessTokenExpiresAtUtc = Now.AddMinutes(2) };
        await sessions.SaveAsync(session, default);

        Result<OperatorSessionResponse> result = await SessionHandler(github, sessions, ["Clinttttt"])
            .Handle(new GetOperatorSessionQuery(session.Id), default);

        // Keeping a record whose token no longer works would report somebody as signed in while every read fails.
        Assert.False(result.IsSuccess);
        Assert.Empty(sessions.Saved);
    }

    [Fact]
    public async Task Session_EndsWhenTheRefreshTokenItselfHasExpired()
    {
        InMemoryOperatorSessionStore sessions = new();
        OperatorSession session = Session() with
        {
            AccessTokenExpiresAtUtc = Now.AddMinutes(2),
            RefreshTokenExpiresAtUtc = Now.AddMinutes(-1),
        };
        await sessions.SaveAsync(session, default);

        Result<OperatorSessionResponse> result = await SessionHandler(
                new FakeGitHubUserAuthentication(),
                sessions,
                ["Clinttttt"])
            .Handle(new GetOperatorSessionQuery(session.Id), default);

        Assert.False(result.IsSuccess);
        Assert.Empty(sessions.Saved);
    }

    private static SignInWithGitHubCommandHandler Handler(
        IGitHubUserAuthentication github,
        IOperatorSessionStore sessions,
        string[] operators) =>
        new(github, sessions, new OperatorAllowList(operators), new FixedTimeProvider(Now));

    private static GetOperatorSessionQueryHandler SessionHandler(
        IGitHubUserAuthentication github,
        IOperatorSessionStore sessions,
        string[] operators)
    {
        FixedTimeProvider time = new(Now);

        return new GetOperatorSessionQueryHandler(
            sessions,
            new OperatorSessionRefresher(sessions, github, time),
            new OperatorAllowList(operators),
            time);
    }

    private static OperatorSession Session() => new(
        Guid.CreateVersion7(),
        12345,
        "Clinttttt",
        "https://avatars.test/clint.png",
        "stored-token",
        Now.AddHours(6),
        "stored-refresh",
        Now.AddMonths(5),
        Now.AddMinutes(-30),
        Now.AddMinutes(-1));

    /// <summary>The same fixed clock the other provider tests use, so time is a fact rather than a race.</summary>
    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    /// <summary>Answers as GitHub would, and records what it was asked.</summary>
    private sealed class FakeGitHubUserAuthentication : IGitHubUserAuthentication
    {
        public string Login { get; init; } = "Clinttttt";

        public GitHubAuthenticationFailure? RefreshFailure { get; init; }

        public int Exchanges { get; private set; }

        public int Refreshes { get; private set; }

        public string? RedirectUriUsed { get; private set; }

        public Uri BuildAuthorizationUrl(string state, string redirectUri) =>
            new($"https://github.test/login/oauth/authorize?state={state}");

        public Task<GitHubAuthenticationResult<GitHubUserToken>> ExchangeCodeAsync(
            string code,
            string redirectUri,
            CancellationToken cancellationToken)
        {
            Exchanges++;
            RedirectUriUsed = redirectUri;
            return Task.FromResult(GitHubAuthenticationResult<GitHubUserToken>.Success(Token()));
        }

        public Task<GitHubAuthenticationResult<GitHubUserToken>> RefreshAsync(
            string refreshToken,
            CancellationToken cancellationToken)
        {
            Refreshes++;
            return Task.FromResult(RefreshFailure is null
                ? GitHubAuthenticationResult<GitHubUserToken>.Success(Token())
                : GitHubAuthenticationResult<GitHubUserToken>.Failed(RefreshFailure.Value));
        }

        public Task<GitHubAuthenticationResult<GitHubUserIdentity>> ReadUserAsync(
            string accessToken,
            CancellationToken cancellationToken) =>
            Task.FromResult(GitHubAuthenticationResult<GitHubUserIdentity>.Success(
                new GitHubUserIdentity(12345, Login, "https://avatars.test/clint.png", "Clint")));

        private static GitHubUserToken Token() => new(
            "token-from-github",
            Now.AddHours(8),
            "refresh-from-github",
            Now.AddMonths(6));
    }

    private sealed class InMemoryOperatorSessionStore : IOperatorSessionStore
    {
        private readonly Dictionary<Guid, OperatorSession> sessions = [];

        public IReadOnlyCollection<OperatorSession> Saved => sessions.Values;

        public Task SaveAsync(OperatorSession session, CancellationToken cancellationToken)
        {
            sessions[session.Id] = session;
            return Task.CompletedTask;
        }

        public Task<OperatorSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(sessions.TryGetValue(sessionId, out OperatorSession? session) ? session : null);

        public Task TouchAsync(Guid sessionId, DateTimeOffset seenAtUtc, CancellationToken cancellationToken)
        {
            if (sessions.TryGetValue(sessionId, out OperatorSession? session))
            {
                sessions[sessionId] = session with { LastSeenAtUtc = seenAtUtc };
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            sessions.Remove(sessionId);
            return Task.CompletedTask;
        }

        public Task<int> DeleteExpiredAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken)
        {
            int removed = sessions
                .Where(entry => entry.Value.RefreshTokenExpiresAtUtc <= asOfUtc)
                .Select(entry => entry.Key)
                .ToList()
                .Count(key => sessions.Remove(key));

            return Task.FromResult(removed);
        }
    }
}
