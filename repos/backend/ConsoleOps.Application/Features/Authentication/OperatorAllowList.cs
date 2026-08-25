namespace ConsoleOps.Application.Features.Authentication;

/// <summary>
/// Which GitHub accounts may use this Console Ops.
/// </summary>
/// <remarks>
/// <para>
/// Signing in with GitHub proves who somebody is, not that they are allowed here. Without this list, authorizing
/// the App would let any GitHub account read the repositories Console Ops watches and start the workflows it can
/// dispatch - which is the whole product surface.
/// </para>
/// <para>
/// An empty list therefore admits nobody. Failing closed is the only safe reading of "no operators configured":
/// the alternative is an ops console that anybody can sign in to the moment it is exposed.
/// </para>
/// </remarks>
public sealed class OperatorAllowList(IEnumerable<string> logins)
{
    private readonly HashSet<string> logins = new(
        logins.Select(login => login.Trim()).Where(login => login.Length > 0),
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether any operator has been configured at all.</summary>
    public bool IsConfigured => logins.Count > 0;

    /// <summary>
    /// Whether this GitHub login may sign in.
    /// </summary>
    /// <remarks>
    /// Compared case-insensitively, because GitHub logins are: an operator typing their own name with different
    /// capitalisation is the same person and should not be locked out by it.
    /// </remarks>
    public bool Admits(string? login) =>
        !string.IsNullOrWhiteSpace(login) && logins.Contains(login.Trim());
}
