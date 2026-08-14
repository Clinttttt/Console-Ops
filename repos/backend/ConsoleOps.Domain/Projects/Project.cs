namespace ConsoleOps.Domain.Projects;

public sealed class Project
{
    private readonly List<ProjectEnvironment> _environments = [];

    private Project()
    {
    }

    private Project(
        Guid id,
        string name,
        string? description,
        string repositoryOwner,
        string repositoryName,
        string defaultBranch,
        string? workflowFile,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Name = name;
        NormalizedName = ProjectRules.Normalize(name);
        Description = description;
        RepositoryOwner = repositoryOwner;
        NormalizedRepositoryOwner = ProjectRules.Normalize(repositoryOwner);
        RepositoryName = repositoryName;
        NormalizedRepositoryName = ProjectRules.Normalize(repositoryName);
        DefaultBranch = defaultBranch;
        WorkflowFile = workflowFile;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string RepositoryOwner { get; private set; } = string.Empty;

    public string NormalizedRepositoryOwner { get; private set; } = string.Empty;

    public string RepositoryName { get; private set; } = string.Empty;

    public string NormalizedRepositoryName { get; private set; } = string.Empty;

    public string DefaultBranch { get; private set; } = string.Empty;

    public string? WorkflowFile { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<ProjectEnvironment> Environments => _environments.AsReadOnly();

    public static Project Create(
        Guid id,
        string name,
        string? description,
        string repositoryOwner,
        string repositoryName,
        string defaultBranch,
        string? workflowFile,
        IEnumerable<ProjectEnvironment> environments,
        DateTimeOffset createdAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);

        string trimmedName = Require(name, nameof(name), ProjectRules.NameMaxLength);
        string? trimmedDescription = Optional(description, nameof(description), ProjectRules.DescriptionMaxLength);
        string trimmedOwner = Require(repositoryOwner, nameof(repositoryOwner), ProjectRules.RepositoryOwnerMaxLength);
        string trimmedRepository = Require(repositoryName, nameof(repositoryName), ProjectRules.RepositoryNameMaxLength);
        string trimmedBranch = Require(defaultBranch, nameof(defaultBranch), ProjectRules.DefaultBranchMaxLength);
        string? trimmedWorkflow = Optional(workflowFile, nameof(workflowFile), ProjectRules.WorkflowFileMaxLength);

        List<ProjectEnvironment> environmentList = environments?.ToList()
            ?? throw new ArgumentNullException(nameof(environments));

        if (environmentList.Count == 0)
        {
            throw new ArgumentException("A project must have at least one environment.", nameof(environments));
        }

        if (environmentList.Select(environment => environment.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            != environmentList.Count)
        {
            throw new ArgumentException("Environment names must be unique within a project.", nameof(environments));
        }

        Project project = new(
            id,
            trimmedName,
            trimmedDescription,
            trimmedOwner,
            trimmedRepository,
            trimmedBranch,
            trimmedWorkflow,
            createdAtUtc);

        project._environments.AddRange(environmentList);
        return project;
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

    private static string? Optional(string? value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value must not exceed {maxLength} characters.", parameterName);
        }

        return trimmed;
    }
}
