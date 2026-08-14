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
        ConfigurationVersion = 1;
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

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public bool IsArchived { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public long ConfigurationVersion { get; private set; }

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

        List<ProjectEnvironment> environmentList = ValidateEnvironments(environments);

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

    public void UpdateConfiguration(
        string name,
        string? description,
        string repositoryOwner,
        string repositoryName,
        string defaultBranch,
        string? workflowFile,
        IEnumerable<ProjectEnvironment> environments,
        DateTimeOffset updatedAtUtc)
    {
        if (IsArchived)
        {
            throw new InvalidOperationException("An archived project cannot be updated.");
        }

        string trimmedName = Require(name, nameof(name), ProjectRules.NameMaxLength);
        string? trimmedDescription = Optional(description, nameof(description), ProjectRules.DescriptionMaxLength);
        string trimmedOwner = Require(repositoryOwner, nameof(repositoryOwner), ProjectRules.RepositoryOwnerMaxLength);
        string trimmedRepository = Require(repositoryName, nameof(repositoryName), ProjectRules.RepositoryNameMaxLength);
        string trimmedBranch = Require(defaultBranch, nameof(defaultBranch), ProjectRules.DefaultBranchMaxLength);
        string? trimmedWorkflow = Optional(workflowFile, nameof(workflowFile), ProjectRules.WorkflowFileMaxLength);
        List<ProjectEnvironment> replacements = ValidateEnvironments(environments);

        Dictionary<Guid, ProjectEnvironment> existingById = _environments.ToDictionary(environment => environment.Id);
        List<ProjectEnvironment> reconciled = new(replacements.Count);

        foreach (ProjectEnvironment replacement in replacements)
        {
            if (existingById.TryGetValue(replacement.Id, out ProjectEnvironment? existing))
            {
                existing.Apply(replacement);
                reconciled.Add(existing);
            }
            else
            {
                reconciled.Add(replacement);
            }
        }

        Name = trimmedName;
        NormalizedName = ProjectRules.Normalize(trimmedName);
        Description = trimmedDescription;
        RepositoryOwner = trimmedOwner;
        NormalizedRepositoryOwner = ProjectRules.Normalize(trimmedOwner);
        RepositoryName = trimmedRepository;
        NormalizedRepositoryName = ProjectRules.Normalize(trimmedRepository);
        DefaultBranch = trimmedBranch;
        WorkflowFile = trimmedWorkflow;
        UpdatedAtUtc = updatedAtUtc;
        ConfigurationVersion = checked(ConfigurationVersion + 1);

        _environments.Clear();
        _environments.AddRange(reconciled);
    }

    public void Archive(DateTimeOffset archivedAtUtc)
    {
        if (IsArchived)
        {
            throw new InvalidOperationException("The project is already archived.");
        }

        IsArchived = true;
        ArchivedAtUtc = archivedAtUtc;
        UpdatedAtUtc = archivedAtUtc;
        ConfigurationVersion = checked(ConfigurationVersion + 1);
    }

    private static List<ProjectEnvironment> ValidateEnvironments(IEnumerable<ProjectEnvironment>? environments)
    {
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

        if (environmentList.Select(environment => environment.Id).Distinct().Count() != environmentList.Count)
        {
            throw new ArgumentException("Environment identifiers must be unique within a project.", nameof(environments));
        }

        return environmentList;
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
