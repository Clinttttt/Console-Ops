namespace ConsoleOps.Infrastructure.Integrations.AzureMonitor;

/// <summary>
/// The Resource Graph query that finds container apps and the workspace their logs go to.
/// <para>
/// Read-only inventory: it lists resources and joins each app to its Container Apps environment so the
/// workspace comes from Azure rather than from an operator typing a GUID. Nothing here modifies a resource.
/// </para>
/// <para>
/// The shape is fixed and bounded. An operator's filter text is emitted as an escaped literal, never
/// concatenated into the query, because this text arrives from a form.
/// </para>
/// </summary>
internal static class AzureLogSourceDiscoveryQuery
{
    internal const string ContainerAppType = "microsoft.app/containerapps";
    internal const string ManagedEnvironmentType = "microsoft.app/managedenvironments";
    internal const string AppServiceType = "microsoft.web/sites";

    /// <summary>
    /// Maps workspace resource ids to the customer GUIDs a log query is addressed with.
    /// </summary>
    /// <remarks>
    /// One query for every id rather than a call each. The ids come from Azure's own diagnostic settings, not from
    /// an operator, and are still emitted as escaped literals: this file's rule is that no value reaches a query
    /// unquoted, regardless of where it came from.
    /// </remarks>
    public static string BuildWorkspaceLookup(IReadOnlyCollection<string> resourceIds)
    {
        ArgumentNullException.ThrowIfNull(resourceIds);

        if (resourceIds.Count == 0)
        {
            throw new ArgumentException("At least one workspace id is required.", nameof(resourceIds));
        }

        string ids = string.Join(", ", resourceIds.Select(Literal));

        return $"""
            resources
            | where type =~ 'microsoft.operationalinsights/workspaces'
            | where id in~ ({ids})
            | project id, customerId = tostring(properties.customerId)
            | limit {resourceIds.Count}
            """;
    }

    public static string Build(string? query, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        string filter = string.IsNullOrWhiteSpace(query)
            ? string.Empty
            : $"""

                | where name contains {Literal(query.Trim())} or resourceGroup contains {Literal(query.Trim())}
                """;

        // Both service types in one query, so finding an app costs one round trip whatever hosts it. App
        // Service carries no workspace here: for Container Apps the workspace is a property of the managed
        // environment, while for a site it lives in a diagnostic setting that Resource Graph does not expose.
        // The catalog resolves those separately, per site and bounded, once a reader exists to use them.
        //
        // The host name comes from a different property per platform, and for a container app it is only
        // reachable when ingress is external: an internal FQDN resolves inside the environment's network and
        // not from Console Ops, so it is carried separately rather than assumed usable.
        return $"""
            resources
            | where type =~ '{ContainerAppType}' or type =~ '{AppServiceType}'{filter}
            | extend platform = iff(type =~ '{AppServiceType}', 'appService', 'containerApp')
            | extend hostName = iff(
                  type =~ '{AppServiceType}',
                  tostring(properties.defaultHostName),
                  tostring(properties.configuration.ingress.fqdn))
            | extend ingressExternal = tostring(properties.configuration.ingress.external)
            | extend environmentKey = tolower(tostring(properties.managedEnvironmentId))
            | project name, platform, resourceGroup, subscriptionId, location, hostName, ingressExternal, environmentKey
            | join kind=leftouter (
                resources
                | where type =~ '{ManagedEnvironmentType}'
                | project environmentKey = tolower(tostring(id)),
                          environmentName = name,
                          workspaceId = tostring(properties.appLogsConfiguration.logAnalyticsConfiguration.customerId)
            ) on environmentKey
            | project name, platform, resourceGroup, subscriptionId, location, hostName, ingressExternal, environmentName, workspaceId
            | order by platform asc, name asc
            | limit {limit}
            """;
    }

    /// <summary>
    /// Emits a value as a KQL string literal. Quotes, backslashes, and control characters are escaped so
    /// filter text cannot become query syntax.
    /// </summary>
    internal static string Literal(string value)
    {
        System.Text.StringBuilder escaped = new(value.Length + 2);
        escaped.Append('"');

        foreach (char character in value)
        {
            switch (character)
            {
                case '\\':
                    escaped.Append("\\\\");
                    break;
                case '"':
                    escaped.Append("\\\"");
                    break;
                case '\r':
                    escaped.Append("\\r");
                    break;
                case '\n':
                    escaped.Append("\\n");
                    break;
                case '\t':
                    escaped.Append("\\t");
                    break;
                default:
                    if (char.IsControl(character))
                    {
                        escaped.Append("\\u").Append(((int)character).ToString("x4"));
                    }
                    else
                    {
                        escaped.Append(character);
                    }

                    break;
            }
        }

        escaped.Append('"');
        return escaped.ToString();
    }
}
