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

    public static string Build(string? query, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        string filter = string.IsNullOrWhiteSpace(query)
            ? string.Empty
            : $"""

                | where name contains {Literal(query.Trim())} or resourceGroup contains {Literal(query.Trim())}
                """;

        return $"""
            resources
            | where type =~ '{ContainerAppType}'{filter}
            | extend environmentKey = tolower(tostring(properties.managedEnvironmentId))
            | project name, resourceGroup, subscriptionId, location, environmentKey
            | join kind=leftouter (
                resources
                | where type =~ '{ManagedEnvironmentType}'
                | project environmentKey = tolower(tostring(id)),
                          environmentName = name,
                          workspaceId = tostring(properties.appLogsConfiguration.logAnalyticsConfiguration.customerId)
            ) on environmentKey
            | project name, resourceGroup, subscriptionId, location, environmentName, workspaceId
            | order by name asc
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
