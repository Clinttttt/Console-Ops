using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ConsoleOps.Application.Integrations.GitHub;

namespace ConsoleOps.Infrastructure.Integrations.GitHub;

/// <summary>
/// One bounded GitHub read: the request Console Ops sends, and what a response means.
/// </summary>
/// <remarks>
/// Extracted from <see cref="GitHubProjectReader"/> and <see cref="GitHubRepositoryCatalog"/>, which carried the
/// same request, the same exception handling and the same status mapping in two copies and said so. A third
/// caller arrived, which is the point at which copying it again would guarantee the three eventually disagree
/// about what a 403 or a truncated body means.
/// </remarks>
internal static class GitHubRead
{
    /// <summary>Pinned so a change in GitHub's default response shape cannot arrive unannounced.</summary>
    internal const string ApiVersion = "2026-03-10";

    /// <summary>GitHub rejects a request without one.</summary>
    internal const string UserAgent = "ConsoleOps/1.0";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Reads one page from GitHub.
    /// </summary>
    /// <remarks>
    /// Every failure is named. A cancellation Console Ops did not ask for is a timeout rather than a caller
    /// abandoning the read, so it reports the provider as unavailable rather than swallowing the request.
    /// </remarks>
    internal static async Task<GitHubReadResponse<T>> GetAsync<T>(
        HttpClient httpClient,
        string relativePath,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, relativePath);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Add("X-GitHub-Api-Version", ApiVersion);

            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return GitHubReadResponse<T>.Failed(MapFailure(response));
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            T? value = await JsonSerializer.DeserializeAsync<T>(
                stream,
                SerializerOptions,
                cancellationToken);

            return value is null
                ? GitHubReadResponse<T>.Failed(GitHubReadFailure.InvalidResponse)
                : GitHubReadResponse<T>.Success(value, HasNextPage(response));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return GitHubReadResponse<T>.Failed(GitHubReadFailure.Unavailable);
        }
        catch (HttpRequestException)
        {
            return GitHubReadResponse<T>.Failed(GitHubReadFailure.Unavailable);
        }
        catch (JsonException)
        {
            return GitHubReadResponse<T>.Failed(GitHubReadFailure.InvalidResponse);
        }
        catch (NotSupportedException)
        {
            return GitHubReadResponse<T>.Failed(GitHubReadFailure.InvalidResponse);
        }
    }

    private static bool HasNextPage(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Link", out IEnumerable<string>? links)
        && links.Any(link => link.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// What a failed response means, as one answer for every GitHub caller.
    /// </summary>
    /// <remarks>
    /// Rate limiting is checked before the status code, because GitHub reports an exhausted limit as a 403 that
    /// is otherwise indistinguishable from a credential without access - and telling an operator their token
    /// lacks permission when it is merely spent sends them to fix the wrong thing.
    /// </remarks>
    internal static GitHubReadFailure MapFailure(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests
            || response.Headers.TryGetValues("X-RateLimit-Remaining", out IEnumerable<string>? values)
            && values.Contains("0", StringComparer.Ordinal))
        {
            return GitHubReadFailure.RateLimited;
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => GitHubReadFailure.Unauthorized,
            HttpStatusCode.NotFound => GitHubReadFailure.NotFound,
            >= HttpStatusCode.InternalServerError => GitHubReadFailure.Unavailable,
            _ => GitHubReadFailure.InvalidResponse
        };
    }
}

/// <param name="HasNextPage">
/// Whether GitHub reported more pages, so a caller can say a list is bounded rather than complete.
/// </param>
internal sealed record GitHubReadResponse<T>(T? Value, GitHubReadFailure? Failure, bool HasNextPage)
    where T : class
{
    internal static GitHubReadResponse<T> Success(T value, bool hasNextPage = false) =>
        new(value, null, hasNextPage);

    internal static GitHubReadResponse<T> Failed(GitHubReadFailure failure) => new(null, failure, false);
}
