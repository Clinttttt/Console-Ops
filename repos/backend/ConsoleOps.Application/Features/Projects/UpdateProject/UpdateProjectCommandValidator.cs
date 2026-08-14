using ConsoleOps.Domain.Projects;
using FluentValidation;

namespace ConsoleOps.Application.Features.Projects.UpdateProject;

public sealed class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.ConfigurationVersion).GreaterThan(0);

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(ProjectRules.NameMaxLength);

        RuleFor(command => command.Description)
            .MaximumLength(ProjectRules.DescriptionMaxLength);

        RuleFor(command => command.Repository)
            .NotNull()
            .SetValidator(new UpdateProjectRepositoryValidator());

        RuleFor(command => command.Environments)
            .NotNull()
            .NotEmpty();

        RuleForEach(command => command.Environments)
            .SetValidator(new UpdateProjectEnvironmentValidator());

        RuleFor(command => command.Environments)
            .Must(HaveUniqueEnvironmentNames)
            .WithMessage("Environment names must be unique within a project.");

        RuleFor(command => command.Environments)
            .Must(HaveUniqueEnvironmentIds)
            .WithMessage("Environment identifiers must be unique within a project.");
    }

    private static bool HaveUniqueEnvironmentNames(IReadOnlyCollection<UpdateProjectEnvironment>? environments)
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

    private static bool HaveUniqueEnvironmentIds(IReadOnlyCollection<UpdateProjectEnvironment>? environments)
    {
        if (environments is null)
        {
            return true;
        }

        Guid[] ids = environments
            .Where(environment => environment.Id.HasValue)
            .Select(environment => environment.Id!.Value)
            .ToArray();

        return ids.Distinct().Count() == ids.Length;
    }
}

internal sealed class UpdateProjectRepositoryValidator : AbstractValidator<UpdateProjectRepository>
{
    public UpdateProjectRepositoryValidator()
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

internal sealed class UpdateProjectEnvironmentValidator : AbstractValidator<UpdateProjectEnvironment>
{
    public UpdateProjectEnvironmentValidator()
    {
        RuleFor(environment => environment.Id)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Environment identifier must not be empty.");

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
    }

    private static bool IsEnvironmentKind(string kind) =>
        !int.TryParse(kind, out _)
        && Enum.TryParse(kind, true, out EnvironmentKind parsed)
        && Enum.IsDefined(parsed);

    private void ValidateOptionalUrl(System.Linq.Expressions.Expression<Func<UpdateProjectEnvironment, string?>> expression)
    {
        RuleFor(expression)
            .MaximumLength(ProjectRules.UrlMaxLength)
            .Must(ProjectRules.IsValidHttpUrl)
            .WithMessage("URL must be an absolute HTTP(S) URL without embedded credentials.");
    }
}
