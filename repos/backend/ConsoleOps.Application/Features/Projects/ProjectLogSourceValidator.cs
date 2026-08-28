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
/// <para>
/// The name rule depends on the platform, so the check asks the domain which rule applies rather than holding a
/// second copy of it. Two copies would drift, and the one here is the one an operator sees.
/// </para>
/// </summary>
internal sealed class ProjectLogSourceValidator : AbstractValidator<ProjectLogSource>
{
    public ProjectLogSourceValidator()
    {
        RuleFor(source => source.WorkspaceId)
            .NotNull()
            .NotEqual(Guid.Empty)
            .WithMessage("A log source needs a Log Analytics workspace id as well as a resource name.");

        RuleFor(source => source.Platform)
            .IsInEnum()
            .WithMessage("Log source platform must be one Console Ops knows: containerApp or appService.");

        RuleFor(source => source.ContainerAppName)
            .NotEmpty()
            .WithMessage("A log source needs a resource name as well as a workspace id.")
            .MaximumLength(AzureLogSource.SiteNameMaxLength);

        // Applied per platform so each message names the rule that was actually broken. A single combined message
        // would tell an operator with a valid site name that it is not a valid container app name.
        RuleFor(source => source.ContainerAppName)
            .Must(AzureLogSource.IsValidContainerAppName)
            .When(source => source.Platform == AzureLogPlatform.ContainerApp)
            .WithMessage(
                "Container app name must be 2 to 32 characters of lower-case letters, digits, or single "
                + "hyphens, starting with a letter and ending with a letter or digit.");

        RuleFor(source => source.ContainerAppName)
            .Must(AzureLogSource.IsValidSiteName)
            .When(source => source.Platform == AzureLogPlatform.AppService)
            .WithMessage(
                "App Service name must be 2 to 60 characters of letters, digits, or hyphens, and may not "
                + "start or end with a hyphen.");
    }
}
