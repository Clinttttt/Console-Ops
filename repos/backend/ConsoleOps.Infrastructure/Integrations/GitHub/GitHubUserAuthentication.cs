using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using ConsoleOps.Application.Integrations.GitHub;
using Microsoft.Extensions.Configuration;

namespace ConsoleOps.Infrastructure.Integrations.GitHub;

/// <summary>
/// The GitHub App's user authorization, over GitHub's OAuth endpoints.
/// </summary>
/// <remarks>
/// <para>
/// A GitHub App is used rather than an OAuth App so an operator grants Console Ops the permissions the App declares
/// on the repositories they install it on, instead of blanket access to everything they can reach. The tokens that
/// come back expire, which is why a refresh token is asked for and kept.
/// </para>
/// <para>
/// The only place the client secret is used. It is read from configuration per request rather than captured, so a
/// rotated secret takes effect without a restart, and it is never written to a response, a log, or an error.
/// </para>
/// </remarks>
public sealed class GitHubUserAuthentication(HttpClient httpClient, IConfiguration configuration)
    : IGitHubUserAuthentication
{
    internal const string AuthorizeUrl = "https://github.com/login/oauth/authorize";
    internal const string TokenUrl = "https://github.com/login/oauth/access_token";

    /// <summary>
    /// Assumed when GitHub does not say. Eight hours is what a GitHub App user token lasts, and treating an
    /// unstated expiry as "already expired" would sign an operator out immediately.
    /// </summary>
    private static readonly TimeSpan DefaultAccessTokenLifetime = TimeSpan.FromHours(8);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Uri BuildAuthorizationUrl(string state, string redirectUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectUri);

        string clientId = Uri.EscapeDataString(ClientId ?? string.Empty);

        // No scope parameter: a GitHub App's permissions are declared on the App and granted at installation, so
        // asking for scopes here would be an OAuth App's habit carried into the wrong flow.
        return new Uri(
            $"{AuthorizeUrl}?client_id={clientId}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + $"&state={Uri.EscapeDataString(state)}");
    }

    public Task<GitHubAuthenticationResult<GitHubUserToken>> ExchangeCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken) =>
        RequestTokenAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
            },
            cancellationToken);

    public Task<GitHubAuthenticationResult<GitHubUserToken>> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken) =>
        RequestTokenAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
            },
            cancellationToken);

    public async Task<GitHubAuthenticationResult<GitHubUserIdentity>> ReadUserAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, "https://api.github.com/user");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd(GitHubRead.UserAgent);
            request.Headers.Add("X-GitHub-Api-Version", GitHubRead.ApiVersion);

            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return GitHubAuthenticationResult<GitHubUserIdentity>.Failed(
                    response.StatusCode is System.Net.HttpStatusCode.Unauthorized
                        or System.Net.HttpStatusCode.Forbidden
                        ? GitHubAuthenticationFailure.Rejected
                        : GitHubAuthenticationFailure.Unavailable);
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            UserDto? user = await JsonSerializer.DeserializeAsync<UserDto>(
                stream,
                SerializerOptions,
                cancellationToken);

            if (user is null || user.Id <= 0 || string.IsNullOrWhiteSpace(user.Login))
            {
                return GitHubAuthenticationResult<GitHubUserIdentity>.Failed(
                    GitHubAuthenticationFailure.InvalidResponse);
            }

            return GitHubAuthenticationResult<GitHubUserIdentity>.Success(new GitHubUserIdentity(
                user.Id,
                user.Login!.Trim(),
                NullIfWhiteSpace(user.AvatarUrl),
                NullIfWhiteSpace(user.Name)));
        }
        catch (HttpRequestException)
        {
            return GitHubAuthenticationResult<GitHubUserIdentity>.Failed(
                GitHubAuthenticationFailure.Unavailable);
        }
        catch (JsonException)
        {
            return GitHubAuthenticationResult<GitHubUserIdentity>.Failed(
                GitHubAuthenticationFailure.InvalidResponse);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return GitHubAuthenticationResult<GitHubUserIdentity>.Failed(
                GitHubAuthenticationFailure.Unavailable);
        }
    }

    private string? ClientId => configuration["GitHub:App:ClientId"];

    private string? ClientSecret => configuration["GitHub:App:ClientSecret"];

    /// <summary>
    /// Asks GitHub for a token, whether that is a first exchange or a refresh.
    /// </summary>
    /// <remarks>
    /// GitHub answers a rejected grant with a 200 and an <c>error</c> field rather than a failure status, so the
    /// body decides the outcome here. Reading only the status code would treat an invalid code as a success.
    /// </remarks>
    private async Task<GitHubAuthenticationResult<GitHubUserToken>> RequestTokenAsync(
        Dictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        string? clientId = ClientId;
        string? clientSecret = ClientSecret;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return GitHubAuthenticationResult<GitHubUserToken>.Failed(
                GitHubAuthenticationFailure.Rejected,
                "GitHub sign-in is not configured.");
        }

        form["client_id"] = clientId;
        form["client_secret"] = clientSecret;

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, TokenUrl)
            {
                Content = new FormUrlEncodedContent(form),
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.ParseAdd(GitHubRead.UserAgent);

            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            TokenDto? token = await JsonSerializer.DeserializeAsync<TokenDto>(
                stream,
                SerializerOptions,
                cancellationToken);

            if (token is null)
            {
                return GitHubAuthenticationResult<GitHubUserToken>.Failed(
                    GitHubAuthenticationFailure.InvalidResponse);
            }

            if (!string.IsNullOrWhiteSpace(token.Error))
            {
                // The description names what to do about it - an expired code, a reused one - without repeating
                // anything about the credential itself.
                return GitHubAuthenticationResult<GitHubUserToken>.Failed(
                    GitHubAuthenticationFailure.Rejected,
                    NullIfWhiteSpace(token.ErrorDescription));
            }

            if (string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return GitHubAuthenticationResult<GitHubUserToken>.Failed(
                    GitHubAuthenticationFailure.InvalidResponse);
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            return GitHubAuthenticationResult<GitHubUserToken>.Success(new GitHubUserToken(
                token.AccessToken!.Trim(),
                now.Add(Lifetime(token.ExpiresIn, DefaultAccessTokenLifetime)),
                NullIfWhiteSpace(token.RefreshToken),
                token.RefreshTokenExpiresIn is null
                    ? null
                    : now.Add(Lifetime(token.RefreshTokenExpiresIn, TimeSpan.Zero))));
        }
        catch (HttpRequestException)
        {
            return GitHubAuthenticationResult<GitHubUserToken>.Failed(GitHubAuthenticationFailure.Unavailable);
        }
        catch (JsonException)
        {
            return GitHubAuthenticationResult<GitHubUserToken>.Failed(
                GitHubAuthenticationFailure.InvalidResponse);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return GitHubAuthenticationResult<GitHubUserToken>.Failed(GitHubAuthenticationFailure.Unavailable);
        }
    }

    private static TimeSpan Lifetime(int? seconds, TimeSpan fallback) =>
        seconds is > 0 ? TimeSpan.FromSeconds(seconds.Value) : fallback;

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record TokenDto(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int? ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("refresh_token_expires_in")] int? RefreshTokenExpiresIn,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("error_description")] string? ErrorDescription);

    private sealed record UserDto(
        long Id,
        string? Login,
        [property: JsonPropertyName("avatar_url")] string? AvatarUrl,
        string? Name);
}
