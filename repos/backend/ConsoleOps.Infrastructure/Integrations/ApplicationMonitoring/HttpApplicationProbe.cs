using System.Buffers;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using ConsoleOps.Application.Integrations.ApplicationMonitoring;

namespace ConsoleOps.Infrastructure.Integrations.ApplicationMonitoring;

public sealed class HttpApplicationProbe(HttpClient httpClient, TimeProvider timeProvider)
    : IApplicationProbe
{
    internal const int MaximumResponseBytes = 64 * 1024;
    internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private const int MaximumDependencyCount = 50;
    private const int MaximumDependencyNameLength = 100;
    private const int MaximumVersionFieldLength = 200;

    public async Task<ApplicationProbeResult> ProbeAsync(
        ApplicationProbeTarget target,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        Task<ApplicationHealthObservation> healthTask =
            ProbeHealthAsync(target.HealthUrl, cancellationToken);
        Task<ApplicationVersionObservation> versionTask =
            ProbeVersionAsync(target.VersionUrl, cancellationToken);

        await Task.WhenAll(healthTask, versionTask);

        return new ApplicationProbeResult(
            await healthTask,
            await versionTask);
    }

    private async Task<ApplicationHealthObservation> ProbeHealthAsync(
        string? healthUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(healthUrl))
        {
            return new ApplicationHealthObservation(
                ApplicationHealthState.NotConfigured,
                null,
                timeProvider.GetUtcNow(),
                []);
        }

        long startedTimestamp = timeProvider.GetTimestamp();
        if (!TryCreateHttpUri(healthUrl, out Uri? uri))
        {
            return UnreachableHealth(startedTimestamp);
        }

        using CancellationTokenSource timeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            using HttpRequestMessage request = CreateRequest(uri!);
            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);

            ParsedHealthPayload? payload = null;
            if (IsJson(response.Content.Headers.ContentType))
            {
                byte[]? body = await ReadBoundedContentAsync(response.Content, timeout.Token);
                payload = body is null ? null : ParseHealthPayload(body);
            }

            ApplicationHealthState state = ResolveHealthState(
                response.IsSuccessStatusCode,
                payload?.State);

            return new ApplicationHealthObservation(
                state,
                timeProvider.GetElapsedTime(startedTimestamp),
                timeProvider.GetUtcNow(),
                payload?.Dependencies ?? []);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return UnreachableHealth(startedTimestamp);
        }
        catch (HttpRequestException)
        {
            return UnreachableHealth(startedTimestamp);
        }
        catch (IOException)
        {
            return UnreachableHealth(startedTimestamp);
        }
    }

    private async Task<ApplicationVersionObservation> ProbeVersionAsync(
        string? versionUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(versionUrl))
        {
            return UnknownVersion(ApplicationVersionState.NotConfigured);
        }

        if (!TryCreateHttpUri(versionUrl, out Uri? uri))
        {
            return UnknownVersion(ApplicationVersionState.Unknown);
        }

        using CancellationTokenSource timeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            using HttpRequestMessage request = CreateRequest(uri!);
            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);

            if (!response.IsSuccessStatusCode || !IsJson(response.Content.Headers.ContentType))
            {
                return UnknownVersion(ApplicationVersionState.Unknown);
            }

            byte[]? body = await ReadBoundedContentAsync(response.Content, timeout.Token);
            ParsedVersionPayload? payload = body is null ? null : ParseVersionPayload(body);

            return payload is null
                ? UnknownVersion(ApplicationVersionState.Unknown)
                : new ApplicationVersionObservation(
                    ApplicationVersionState.Available,
                    payload.Application,
                    payload.Version,
                    payload.CommitSha,
                    payload.Environment,
                    payload.BuiltAtUtc,
                    timeProvider.GetUtcNow());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return UnknownVersion(ApplicationVersionState.Unknown);
        }
        catch (HttpRequestException)
        {
            return UnknownVersion(ApplicationVersionState.Unknown);
        }
        catch (IOException)
        {
            return UnknownVersion(ApplicationVersionState.Unknown);
        }
    }

    private ApplicationHealthObservation UnreachableHealth(long startedTimestamp) => new(
        ApplicationHealthState.Unreachable,
        timeProvider.GetElapsedTime(startedTimestamp),
        timeProvider.GetUtcNow(),
        []);

    private ApplicationVersionObservation UnknownVersion(ApplicationVersionState state) => new(
        state,
        null,
        null,
        null,
        null,
        null,
        timeProvider.GetUtcNow());

    private static HttpRequestMessage CreateRequest(Uri uri)
    {
        HttpRequestMessage request = new(HttpMethod.Get, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("ConsoleOps/1.0");
        return request;
    }

    private static bool TryCreateHttpUri(string value, out Uri? uri)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            uri = null;
            return false;
        }

        return true;
    }

    private static bool IsJson(MediaTypeHeaderValue? contentType)
    {
        string? mediaType = contentType?.MediaType;
        return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static async Task<byte[]?> ReadBoundedContentAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaximumResponseBytes)
        {
            return null;
        }

        await using Stream stream = await content.ReadAsStreamAsync(cancellationToken);
        using MemoryStream output = new();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(8 * 1024);

        try
        {
            while (true)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (read == 0)
                {
                    return output.ToArray();
                }

                if (output.Length + read > MaximumResponseBytes)
                {
                    return null;
                }

                output.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static ParsedHealthPayload? ParseHealthPayload(byte[] body)
    {
        if (body.Length == 0)
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                body,
                new JsonDocumentOptions { MaxDepth = 16 });
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("status", out JsonElement statusElement)
                || statusElement.ValueKind != JsonValueKind.String
                || !TryMapHealthState(statusElement.GetString(), out ApplicationHealthState state))
            {
                return null;
            }

            IReadOnlyList<DependencyHealthObservation> dependencies =
                ParseDependencies(root);
            return new ParsedHealthPayload(state, dependencies);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<DependencyHealthObservation> ParseDependencies(JsonElement root)
    {
        if (!root.TryGetProperty("entries", out JsonElement entries)
            || entries.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        List<DependencyHealthObservation> dependencies = [];
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

        foreach (JsonProperty entry in entries.EnumerateObject())
        {
            if (dependencies.Count == MaximumDependencyCount)
            {
                break;
            }

            string name = entry.Name.Trim();
            if (name.Length is 0 or > MaximumDependencyNameLength
                || name.Any(char.IsControl)
                || !names.Add(name)
                || entry.Value.ValueKind != JsonValueKind.Object
                || !entry.Value.TryGetProperty("status", out JsonElement statusElement)
                || statusElement.ValueKind != JsonValueKind.String
                || !TryMapHealthState(statusElement.GetString(), out ApplicationHealthState state))
            {
                continue;
            }

            dependencies.Add(new DependencyHealthObservation(name, state));
        }

        return dependencies;
    }

    private static ParsedVersionPayload? ParseVersionPayload(byte[] body)
    {
        if (body.Length == 0)
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                body,
                new JsonDocumentOptions { MaxDepth = 8 });
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? commit = GetSafeString(root, "commit", 64);
            if (!IsCommitSha(commit))
            {
                return null;
            }

            return new ParsedVersionPayload(
                GetSafeString(root, "application", MaximumVersionFieldLength),
                GetSafeString(root, "version", MaximumVersionFieldLength),
                commit!.ToLowerInvariant(),
                GetSafeString(root, "environment", MaximumVersionFieldLength),
                GetUtcInstant(root, "builtAt"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetSafeString(JsonElement root, string propertyName, int maximumLength)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement element)
            || element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? value = element.GetString()?.Trim();
        return value is null
            || value.Length is 0
            || value.Length > maximumLength
            || value.Any(char.IsControl)
                ? null
                : value;
    }

    private static DateTimeOffset? GetUtcInstant(JsonElement root, string propertyName)
    {
        string? value = GetSafeString(root, propertyName, 64);
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTimeOffset instant)
                ? instant
                : null;
    }

    private static bool IsCommitSha(string? value) =>
        value is { Length: >= 7 and <= 64 } && value.All(Uri.IsHexDigit);

    private static bool TryMapHealthState(
        string? value,
        out ApplicationHealthState state)
    {
        state = value?.Trim().ToLowerInvariant() switch
        {
            "healthy" => ApplicationHealthState.Healthy,
            "degraded" => ApplicationHealthState.Degraded,
            "unhealthy" => ApplicationHealthState.Unhealthy,
            _ => ApplicationHealthState.Unknown
        };

        return state != ApplicationHealthState.Unknown;
    }

    private static ApplicationHealthState ResolveHealthState(
        bool isSuccessStatusCode,
        ApplicationHealthState? payloadState)
    {
        if (isSuccessStatusCode)
        {
            return payloadState ?? ApplicationHealthState.Healthy;
        }

        return payloadState is ApplicationHealthState.Degraded or ApplicationHealthState.Unhealthy
            ? payloadState.Value
            : ApplicationHealthState.Unhealthy;
    }

    private sealed record ParsedHealthPayload(
        ApplicationHealthState State,
        IReadOnlyList<DependencyHealthObservation> Dependencies);

    private sealed record ParsedVersionPayload(
        string? Application,
        string? Version,
        string CommitSha,
        string? Environment,
        DateTimeOffset? BuiltAtUtc);
}
