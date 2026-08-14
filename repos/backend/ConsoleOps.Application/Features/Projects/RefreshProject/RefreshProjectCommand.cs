using ConsoleOps.Application.Abstractions.Messaging;
using MediatR;

namespace ConsoleOps.Application.Features.Projects.RefreshProject;

public sealed record RefreshProjectCommand(Guid ProjectId)
    : IRequest<Result<RefreshProjectResponse>>;
