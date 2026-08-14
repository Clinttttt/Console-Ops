using ConsoleOps.Application.Abstractions.Messaging;
using MediatR;

namespace ConsoleOps.Application.Features.Projects.ArchiveProject;

public sealed record ArchiveProjectCommand(Guid ProjectId) : IRequest<Result>;
