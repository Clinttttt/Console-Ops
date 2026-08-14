using ConsoleOps.Application.Abstractions.Messaging;
using MediatR;

namespace ConsoleOps.Application.Features.Projects.RegisterProject;

public sealed record RegisterProjectCommand(
    string Name,
    string? Description,
    RegisterProjectRepository Repository,
    IReadOnlyCollection<RegisterProjectEnvironment> Environments)
    : IRequest<Result<RegisterProjectResponse>>;

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
    string? VersionUrl);

public sealed record RegisterProjectResponse(
    Guid Id,
    string Name,
    string? Description,
    RegisterProjectRepository Repository,
    IReadOnlyCollection<RegisteredEnvironmentResponse> Environments,
    DateTimeOffset CreatedAtUtc);

public sealed record RegisteredEnvironmentResponse(
    Guid Id,
    string Name,
    string Kind,
    string? ApplicationUrl,
    string? HealthUrl,
    string? VersionUrl);
