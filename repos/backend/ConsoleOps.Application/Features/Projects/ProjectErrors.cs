using ConsoleOps.Application.Abstractions.Messaging;

namespace ConsoleOps.Application.Features.Projects;

public static class ProjectErrors
{
    public static readonly Error NotFound = new(
        "Projects.NotFound",
        "The project was not found.",
        ErrorType.NotFound);

    public static readonly Error DuplicateName = new(
        "Projects.DuplicateName",
        "A project with this name already exists.",
        ErrorType.Conflict);

    public static readonly Error DuplicateRepository = new(
        "Projects.DuplicateRepository",
        "This repository is already registered.",
        ErrorType.Conflict);

    public static readonly Error ConfigurationConflict = new(
        "Projects.ConfigurationConflict",
        "The project configuration changed after it was read. Reload it and try again.",
        ErrorType.Conflict);

    public static readonly Error UnknownEnvironment = new(
        "Projects.UnknownEnvironment",
        "An environment identifier does not belong to this project.",
        ErrorType.Conflict);
}
