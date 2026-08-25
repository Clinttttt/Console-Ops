using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Integrations.GitHub;
using MediatR;

namespace ConsoleOps.Application.Features.Authentication.SignIn;

/// <param name="Code">The authorization code GitHub sent to the callback.</param>
/// <param name="RedirectUri">
/// The callback the code was issued for. GitHub checks it matches, which is why it is carried rather than rebuilt.
/// </param>
public sealed record SignInWithGitHubCommand(string Code, string RedirectUri)
    : IRequest<Result<SignedInOperatorResponse>>;

/// <param name="SessionId">
/// The opaque id the browser is given. It is the only part of a session that leaves the server: the GitHub tokens
/// stay here, as every other provider credential in Console Ops does.
/// </param>
public sealed record SignedInOperatorResponse(
    Guid SessionId,
    string Login,
    string? AvatarUrl,
    DateTimeOffset SignedInAt);

/// <summary>
/// Completes a GitHub App authorization and records the operator's session.
/// </summary>
/// <remarks>
/// The order matters. The code is exchanged, the identity is read, and only then is the allowlist consulted - a
/// session is never written for an account that is not an operator here. Authorizing the App proves who somebody
/// is; it does not decide whether they may use this Console Ops.
/// </remarks>
public sealed class SignInWithGitHubCommandHandler(
    IGitHubUserAuthentication authentication,
    IOperatorSessionStore sessions,
    OperatorAllowList allowList,
    TimeProvider timeProvider)
    : IRequestHandler<SignInWithGitHubCommand, Result<SignedInOperatorResponse>>
{
    public async Task<Result<SignedInOperatorResponse>> Handle(
        SignInWithGitHubCommand request,
        CancellationToken cancellationToken)
    {
        if (!allowList.IsConfigured)
        {
            // Failing closed: no operators configured admits nobody rather than everybody.
            return Result<SignedInOperatorResponse>.Failure(AuthenticationErrors.NoOperatorsConfigured);
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Result<SignedInOperatorResponse>.Failure(AuthenticationErrors.CodeRejected);
        }

        GitHubAuthenticationResult<GitHubUserToken> exchange = await authentication.ExchangeCodeAsync(
            request.Code,
            request.RedirectUri,
            cancellationToken);

        if (!exchange.IsSuccess)
        {
            return Result<SignedInOperatorResponse>.Failure(ToError(exchange.Failure));
        }

        GitHubUserToken token = exchange.Value!;
        GitHubAuthenticationResult<GitHubUserIdentity> identity = await authentication.ReadUserAsync(
            token.AccessToken,
            cancellationToken);

        if (!identity.IsSuccess)
        {
            return Result<SignedInOperatorResponse>.Failure(ToError(identity.Failure));
        }

        GitHubUserIdentity user = identity.Value!;
        if (!allowList.Admits(user.Login))
        {
            // Nothing is stored for an account that is not an operator, so a refused sign-in leaves no trace to
            // reuse and no session to expire.
            return Result<SignedInOperatorResponse>.Failure(AuthenticationErrors.NotAnOperator);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        OperatorSession session = new(
            Guid.CreateVersion7(),
            user.UserId,
            user.Login,
            user.AvatarUrl,
            token.AccessToken,
            token.AccessTokenExpiresAtUtc,
            token.RefreshToken,
            token.RefreshTokenExpiresAtUtc,
            now,
            now);

        await sessions.SaveAsync(session, cancellationToken);

        return Result<SignedInOperatorResponse>.Success(new SignedInOperatorResponse(
            session.Id,
            session.Login,
            session.AvatarUrl,
            session.SignedInAtUtc));
    }

    private static Error ToError(GitHubAuthenticationFailure? failure) => failure switch
    {
        GitHubAuthenticationFailure.Rejected => AuthenticationErrors.CodeRejected,
        GitHubAuthenticationFailure.InvalidResponse => AuthenticationErrors.ProviderUnavailable,
        _ => AuthenticationErrors.ProviderUnavailable
    };
}
