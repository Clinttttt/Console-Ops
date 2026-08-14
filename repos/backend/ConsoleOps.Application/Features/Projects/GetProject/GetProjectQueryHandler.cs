using ConsoleOps.Application.Abstractions.Messaging;
using MediatR;

namespace ConsoleOps.Application.Features.Projects.GetProject;

public sealed class GetProjectQueryHandler(IProjectReadStore readStore)
    : IRequestHandler<GetProjectQuery, Result<ProjectResponse>>
{
    public async Task<Result<ProjectResponse>> Handle(
        GetProjectQuery request,
        CancellationToken cancellationToken)
    {
        ProjectResponse? project = await readStore.GetAsync(request.ProjectId, cancellationToken);

        return project is null
            ? Result<ProjectResponse>.Failure(ProjectErrors.NotFound)
            : Result<ProjectResponse>.Success(project);
    }
}
