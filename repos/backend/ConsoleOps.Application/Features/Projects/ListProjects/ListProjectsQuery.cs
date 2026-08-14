using MediatR;

namespace ConsoleOps.Application.Features.Projects.ListProjects;

public sealed record ListProjectsQuery : IRequest<ProjectResponse[]>;
