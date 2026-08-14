using ConsoleOps.Application.Abstractions.Messaging;
using MediatR;

namespace ConsoleOps.Application.Features.Projects.UpdateProject;

public sealed record UpdateProjectCommand(
    Guid ProjectId,
    long ConfigurationVersion,
    string Name,
    string? Description,
    UpdateProjectRepository Repository,
    IReadOnlyCollection<UpdateProjectEnvironment> Environments)
    : IRequest<Result<ProjectResponse>>;

public sealed record UpdateProjectRepository(
    string Owner,
    string Name,
    string DefaultBranch,
    string? WorkflowFile);

public sealed record UpdateProjectEnvironment(
    Guid? Id,
    string Name,
    string Kind,
    string? ApplicationUrl,
    string? HealthUrl,
    string? VersionUrl);
