using ConsoleOps.Domain.Projects;
using FluentValidation;

namespace ConsoleOps.Application.Features.Projects;

/// <summary>
/// Validates an optional log source so a bad one is refused at the boundary with a message, rather than
/// reaching the domain and surfacing as an unexpected fault.
/// <para>
/// Shared by registration and update: both edit the same configuration, and a rule that applied to only
/// one of them would be a way to store something the other rejects.
/// </para>
/// </summary>
internal sealed class ProjectLogSourceValidator : AbstractValidator<ProjectLogSource>
{
    public ProjectLogSourceValidator()
    {
        RuleFor(source => source.WorkspaceId)
            .NotNull()
            .NotEqual(Guid.Empty)
            .WithMessage("A log source needs a Log Analytics workspace id as well as a container app name.");

        RuleFor(source => source.ContainerAppName)
            .NotEmpty()
            .WithMessage("A log source needs a container app name as well as a workspace id.")
            .MaximumLength(AzureLogSource.ContainerAppNameMaxLength)
            .Must(AzureLogSource.IsValidContainerAppName)
            .WithMessage(
                "Container app name must be 2 to 32 characters of lower-case letters, digits, or single "
                + "hyphens, starting with a letter and ending with a letter or digit.");
    }
}
