using MediatR;

namespace ConsoleOps.Application.Features.Projects.ListProjects;

public sealed class ListProjectsQueryHandler(IProjectReadStore readStore)
    : IRequestHandler<ListProjectsQuery, ProjectResponse[]>
{
    public Task<ProjectResponse[]> Handle(ListProjectsQuery request, CancellationToken cancellationToken) =>
        readStore.ListAsync(cancellationToken);
}
