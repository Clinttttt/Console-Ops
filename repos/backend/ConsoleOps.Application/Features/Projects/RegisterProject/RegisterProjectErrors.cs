using ConsoleOps.Application.Abstractions.Messaging;

namespace ConsoleOps.Application.Features.Projects.RegisterProject;

public static class RegisterProjectErrors
{
    public static readonly Error DuplicateName = new(
        "Projects.DuplicateName",
        "A project with this name already exists.",
        ErrorType.Conflict);

    public static readonly Error DuplicateRepository = new(
        "Projects.DuplicateRepository",
        "This repository is already registered.",
        ErrorType.Conflict);
}
