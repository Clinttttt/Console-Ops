namespace ConsoleOps.Domain.Projects;

/// <summary>
/// How much intent starting a workflow should require.
/// </summary>
/// <remarks>
/// Set by an operator, never derived. A name cannot carry this: "Database restore" reads destructive to a person
/// and is unprovable to Console Ops, and a workflow called "cleanup" may drop a production schema. Because the
/// default is <see cref="Unclassified"/>, a workflow nobody has marked is not offered for execution at all -
/// refusing to run something whose risk is unknown is the only safe default.
/// </remarks>
public enum WorkflowRiskLevel
{
    /// <summary>Nobody has said. Not executable from Console Ops.</summary>
    Unclassified,

    /// <summary>Ordinary automation. Executable after an explicit confirmation.</summary>
    Normal,

    /// <summary>Destroys or replaces something. Executable only after the workflow's name is typed.</summary>
    Destructive
}
