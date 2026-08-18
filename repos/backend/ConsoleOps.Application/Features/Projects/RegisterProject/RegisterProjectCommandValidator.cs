using ConsoleOps.Domain.Projects;
using FluentValidation;

namespace ConsoleOps.Application.Features.Projects.RegisterProject;

public sealed class RegisterProjectCommandValidator : AbstractValidator<RegisterProjectCommand>
{
    public RegisterProjectCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(ProjectRules.NameMaxLength);

        RuleFor(command => command.Description)
            .MaximumLength(ProjectRules.DescriptionMaxLength);

        RuleFor(command => command.Repository)
            .NotNull()
            .SetValidator(new RegisterProjectRepositoryValidator());

        RuleFor(command => command.Environments)
            .NotNull()
            .NotEmpty();

        RuleForEach(command => command.Environments)
            .SetValidator(new RegisterProjectEnvironmentValidator());

        RuleFor(command => command.Environments)
            .Must(HaveUniqueEnvironmentNames)
            .WithMessage("Environment names must be unique within a project.");
    }

    private static bool HaveUniqueEnvironmentNames(IReadOnlyCollection<RegisterProjectEnvironment>? environments)
    {
        if (environments is null)
        {
            return true;
        }

        return environments
            .Select(environment => environment.Name?.Trim())
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == environments.Count(environment => !string.IsNullOrWhiteSpace(environment.Name));
    }
}

internal sealed class RegisterProjectRepositoryValidator : AbstractValidator<RegisterProjectRepository>
{
    public RegisterProjectRepositoryValidator()
    {
        RuleFor(repository => repository.Owner)
            .NotEmpty()
            .MaximumLength(ProjectRules.RepositoryOwnerMaxLength);

        RuleFor(repository => repository.Name)
            .NotEmpty()
            .MaximumLength(ProjectRules.RepositoryNameMaxLength);

        RuleFor(repository => repository.DefaultBranch)
            .NotEmpty()
            .MaximumLength(ProjectRules.DefaultBranchMaxLength);

        RuleFor(repository => repository.WorkflowFile)
            .MaximumLength(ProjectRules.WorkflowFileMaxLength);
    }
}

internal sealed class RegisterProjectEnvironmentValidator : AbstractValidator<RegisterProjectEnvironment>
{
    public RegisterProjectEnvironmentValidator()
    {
        RuleFor(environment => environment.Name)
            .NotEmpty()
            .MaximumLength(ProjectRules.EnvironmentNameMaxLength);

        RuleFor(environment => environment.Kind)
            .NotEmpty()
            .Must(IsEnvironmentKind)
            .WithMessage("Environment kind must be production, staging, development, or local.");

        ValidateOptionalUrl(environment => environment.ApplicationUrl);
        ValidateOptionalUrl(environment => environment.HealthUrl);
        ValidateOptionalUrl(environment => environment.VersionUrl);

        RuleFor(environment => environment.LogSource)
            .SetValidator(new ProjectLogSourceValidator()!)
            .When(environment => environment.LogSource is not null);
    }

    private static bool IsEnvironmentKind(string kind) =>
        !int.TryParse(kind, out _)
        && Enum.TryParse(kind, true, out EnvironmentKind parsed)
        && Enum.IsDefined(parsed);

    private void ValidateOptionalUrl(System.Linq.Expressions.Expression<Func<RegisterProjectEnvironment, string?>> expression)
    {
        RuleFor(expression)
            .MaximumLength(ProjectRules.UrlMaxLength)
            .Must(ProjectRules.IsValidHttpUrl)
            .WithMessage("URL must be an absolute HTTP(S) URL without embedded credentials.");
    }
}
