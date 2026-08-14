namespace ConsoleOps.Domain.Projects;

public sealed class ProjectEnvironment
{
    private ProjectEnvironment()
    {
    }

    private ProjectEnvironment(
        Guid id,
        string name,
        EnvironmentKind kind,
        string? applicationUrl,
        string? healthUrl,
        string? versionUrl)
    {
        Id = id;
        Name = name;
        NormalizedName = ProjectRules.Normalize(name);
        Kind = kind;
        ApplicationUrl = applicationUrl;
        HealthUrl = healthUrl;
        VersionUrl = versionUrl;
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public EnvironmentKind Kind { get; private set; }

    public string? ApplicationUrl { get; private set; }

    public string? HealthUrl { get; private set; }

    public string? VersionUrl { get; private set; }

    public static ProjectEnvironment Create(
        Guid id,
        string name,
        EnvironmentKind kind,
        string? applicationUrl,
        string? healthUrl,
        string? versionUrl)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Environment kind is not supported.");
        }

        string trimmedName = Require(name, nameof(name), ProjectRules.EnvironmentNameMaxLength);
        string? trimmedApplicationUrl = ValidateUrl(applicationUrl, nameof(applicationUrl));
        string? trimmedHealthUrl = ValidateUrl(healthUrl, nameof(healthUrl));
        string? trimmedVersionUrl = ValidateUrl(versionUrl, nameof(versionUrl));

        return new ProjectEnvironment(
            id,
            trimmedName,
            kind,
            trimmedApplicationUrl,
            trimmedHealthUrl,
            trimmedVersionUrl);
    }

    internal void Apply(ProjectEnvironment replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        if (Id != replacement.Id)
        {
            throw new ArgumentException("The replacement environment must have the same identifier.", nameof(replacement));
        }

        Name = replacement.Name;
        NormalizedName = replacement.NormalizedName;
        Kind = replacement.Kind;
        ApplicationUrl = replacement.ApplicationUrl;
        HealthUrl = replacement.HealthUrl;
        VersionUrl = replacement.VersionUrl;
    }

    private static string? ValidateUrl(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (!ProjectRules.IsValidHttpUrl(trimmed))
        {
            throw new ArgumentException("URL must be an absolute HTTP(S) URL without embedded credentials.", parameterName);
        }

        return trimmed;
    }

    private static string Require(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        string trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value must not exceed {maxLength} characters.", parameterName);
        }

        return trimmed;
    }
}
