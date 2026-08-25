using ConsoleOps.Application.Abstractions.Messaging;
using MediatR;

namespace ConsoleOps.Application.Features.Authentication.SignOut;

/// <summary>
/// Ends a session.
/// </summary>
/// <remarks>
/// Deleting the record is what signs an operator out, not clearing the cookie: a cookie an operator kept a copy of
/// must stop working, and it only does if the server no longer knows the session.
/// </remarks>
public sealed record SignOutCommand(Guid? SessionId) : IRequest<Result>;

public sealed class SignOutCommandHandler(IOperatorSessionStore sessions)
    : IRequestHandler<SignOutCommand, Result>
{
    public async Task<Result> Handle(SignOutCommand request, CancellationToken cancellationToken)
    {
        if (request.SessionId is not null)
        {
            await sessions.DeleteAsync(request.SessionId.Value, cancellationToken);
        }

        // Signing out something that was already gone is not a failure: the operator asked to be signed out and
        // they are.
        return Result.Success();
    }
}
