namespace ConsoleOps.Domain.Projects;

public static class ProjectRules
{
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 1_000;
    public const int RepositoryOwnerMaxLength = 100;
    public const int RepositoryNameMaxLength = 100;
    public const int DefaultBranchMaxLength = 255;
    public const int WorkflowFileMaxLength = 255;

    /// <summary>A workflow's definition path, such as <c>.github/workflows/deploy-production.yml</c>.</summary>
    public const int WorkflowPathMaxLength = 400;
    public const int EnvironmentNameMaxLength = 100;
    public const int UrlMaxLength = 2_048;

    public static bool IsValidHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return value.Length <= UrlMaxLength
            && Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrWhiteSpace(uri.Host)
            && string.IsNullOrEmpty(uri.UserInfo);
    }

    public static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
