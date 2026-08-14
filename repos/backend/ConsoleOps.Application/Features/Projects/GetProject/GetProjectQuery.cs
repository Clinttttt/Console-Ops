using ConsoleOps.Application.Abstractions.Messaging;
using MediatR;

namespace ConsoleOps.Application.Features.Projects.GetProject;

public sealed record GetProjectQuery(Guid ProjectId) : IRequest<Result<ProjectResponse>>;
