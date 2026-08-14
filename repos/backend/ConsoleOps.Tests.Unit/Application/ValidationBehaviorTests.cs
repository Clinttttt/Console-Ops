using ConsoleOps.Application.Behaviors;
using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Projects.RegisterProject;
using FluentValidation;

namespace ConsoleOps.Tests.Unit.Application;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_WithValidRequest_InvokesNextExactlyOnce()
    {
        RegisterProjectCommand command = CreateValidCommand();
        ValidationBehavior<RegisterProjectCommand, Result<RegisterProjectResponse>> behavior =
            new([new RegisterProjectCommandValidator()]);
        int invocations = 0;

        Result<RegisterProjectResponse> result = await behavior.Handle(
            command,
            _ =>
            {
                invocations++;
                return Task.FromResult(Result<RegisterProjectResponse>.Failure(RegisterProjectErrors.DuplicateName));
            },
            CancellationToken.None);

        Assert.Equal(1, invocations);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_WithInvalidRequest_DoesNotInvokeNext()
    {
        RegisterProjectCommand command = CreateValidCommand() with { Name = string.Empty };
        ValidationBehavior<RegisterProjectCommand, Result<RegisterProjectResponse>> behavior =
            new([new RegisterProjectCommandValidator()]);
        int invocations = 0;

        await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            command,
            _ =>
            {
                invocations++;
                throw new InvalidOperationException("The handler must not run.");
            },
            CancellationToken.None));

        Assert.Equal(0, invocations);
    }

    private static RegisterProjectCommand CreateValidCommand() => new(
        "Console Ops",
        null,
        new RegisterProjectRepository("Clinttttt", "Console-Ops", "main", "ci.yml"),
        [new RegisterProjectEnvironment(
            "Production",
            "production",
            "https://console.example.com",
            "https://console.example.com/health",
            "https://console.example.com/version")]);
}
