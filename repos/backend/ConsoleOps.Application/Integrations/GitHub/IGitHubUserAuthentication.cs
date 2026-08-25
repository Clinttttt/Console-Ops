namespace ConsoleOps.Application.Integrations.GitHub;

/// <summary>
/// A user access token issued by GitHub for one operator.
/// </summary>
/// <remarks>
/// A GitHub App's user tokens expire, which is why this carries both expiries. The refresh token outlives the
/// access token by months and is what keeps an operator signed in without another authorization round.
/// </remarks>
public sealed record GitHubUserToken(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string? RefreshToken,
    DateTimeOffset? RefreshTokenExpiresAtUtc);

/// <summary>Who GitHub says the token belongs to.</summary>
public sealed record GitHubUserIdentity(long UserId, string Login, string? AvatarUrl, string? Name);

/// <summary>Why an authorization could not be completed.</summary>
public enum GitHubAuthenticationFailure
{
    /// <summary>GitHub rejected the code, the refresh token, or the client credentials.</summary>
    Rejected,

    /// <summary>GitHub answered something Console Ops could not read.</summary>
    InvalidResponse,

    Unavailable
}

public sealed class GitHubAuthenticationResult<TValue>
    where TValue : class
{
    private GitHubAuthenticationResult(TValue? value, GitHubAuthenticationFailure? failure, string? description)
    {
        Value = value;
        Failure = failure;
        Description = description;
    }

    public TValue? Value { get; }

    public GitHubAuthenticationFailure? Failure { get; }

    /// <summary>What GitHub said, when it said anything. Never a token or a client secret.</summary>
    public string? Description { get; }

    public bool IsSuccess => Failure is null;

    public static GitHubAuthenticationResult<TValue> Success(TValue value) => new(value, null, null);

    public static GitHubAuthenticationResult<TValue> Failed(
        GitHubAuthenticationFailure failure,
        string? description = null) =>
        new(null, failure, description);
}

/// <summary>
/// The GitHub App's user authorization: turning an authorization code into a token, keeping it fresh, and asking
/// who it belongs to.
/// </summary>
/// <remarks>
/// Separate from the reading ports because it authenticates rather than reads, and because it is the only place a
/// client secret is used. Nothing here touches a repository.
/// </remarks>
public interface IGitHubUserAuthentication
{
    /// <summary>Where GitHub asks the operator to authorize the App.</summary>
    Uri BuildAuthorizationUrl(string state, string redirectUri);

    Task<GitHubAuthenticationResult<GitHubUserToken>> ExchangeCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken);

    Task<GitHubAuthenticationResult<GitHubUserToken>> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken);

    Task<GitHubAuthenticationResult<GitHubUserIdentity>> ReadUserAsync(
        string accessToken,
        CancellationToken cancellationToken);
}
