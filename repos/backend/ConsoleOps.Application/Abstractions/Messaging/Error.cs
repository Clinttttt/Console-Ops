namespace ConsoleOps.Application.Abstractions.Messaging;

public enum ErrorType
{
    None,
    Validation,
    NotFound,
    Conflict,

    /// <summary>
    /// The credential Console Ops is configured with is not allowed to do this.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="Failure"/> because nothing broke: reporting a permission the operator has not
    /// granted as a server fault sends them looking for an outage instead of a token scope.
    /// </remarks>
    Forbidden,

    Failure
}

public sealed record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);
}
