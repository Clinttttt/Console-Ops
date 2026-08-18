using ConsoleOps.Application.Abstractions.Messaging;
using MediatR;

namespace ConsoleOps.Application.Features.Projects.RegisterProject;

public sealed record RegisterProjectCommand(
    string Name,
    string? Description,
    RegisterProjectRepository Repository,
    IReadOnlyCollection<RegisterProjectEnvironment> Environments)
    : IRequest<Result<ProjectResponse>>;

public sealed record RegisterProjectRepository(
    string Owner,
    string Name,
    string DefaultBranch,
    string? WorkflowFile);

public sealed record RegisterProjectEnvironment(
    string Name,
    string Kind,
    string? ApplicationUrl,
    string? HealthUrl,
    string? VersionUrl,
    ProjectLogSource? LogSource = null);
