namespace ConsoleOps.Infrastructure.Integrations.GitHub;

/// <summary>
/// Whether a workflow declares a manual dispatch trigger, read from its own definition.
/// </summary>
/// <remarks>
/// <para>
/// GitHub's workflow listing does not report triggers, and no read endpoint answers "can this be dispatched" -
/// the answer lives in the file's <c>on:</c> block. This scans that block only, so a job step written as
/// <c>if: github.event_name == 'workflow_dispatch'</c> is not mistaken for a trigger declaration.
/// </para>
/// <para>
/// Deliberately narrow rather than a YAML parser. It answers definitively when it finds a trigger block, and
/// reports that it does not know when the file does not look like one it understands - a wrong "cannot be run"
/// would hide a workflow an operator relies on.
/// </para>
/// </remarks>
internal static class WorkflowTriggerScan
{
    private const string DispatchTrigger = "workflow_dispatch";

    /// <summary>
    /// <c>true</c> or <c>false</c> when the trigger block was found, <c>null</c> when it was not.
    /// </summary>
    internal static bool? DeclaresManualDispatch(string definition)
    {
        if (string.IsNullOrWhiteSpace(definition))
        {
            return null;
        }

        string[] lines = definition.Replace("\r\n", "\n").Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string line = WithoutComment(lines[index]);
            if (!TryReadTriggerKey(line, out string remainder))
            {
                continue;
            }

            // `on: workflow_dispatch` and `on: [push, workflow_dispatch]` declare triggers on one line.
            if (remainder.Length > 0)
            {
                return remainder.Contains(DispatchTrigger, StringComparison.Ordinal);
            }

            return BlockDeclaresDispatch(lines, index + 1);
        }

        return null;
    }

    /// <summary>
    /// Whether the indented block beneath <c>on:</c> lists a manual dispatch trigger.
    /// </summary>
    /// <remarks>
    /// The block ends at the next line that starts in column zero, which is the next top-level key. Only the
    /// keys of that block are considered, so a branch filter or an input's description cannot be read as a
    /// trigger.
    /// </remarks>
    private static bool BlockDeclaresDispatch(string[] lines, int start)
    {
        for (int index = start; index < lines.Length; index++)
        {
            string raw = lines[index];
            string line = WithoutComment(raw);
            if (line.Trim().Length == 0)
            {
                continue;
            }

            if (!char.IsWhiteSpace(raw[0]))
            {
                return false;
            }

            string trimmed = line.TrimStart();
            // A list form nests one level deeper: `on:` then `  - workflow_dispatch`.
            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                trimmed = trimmed[2..].TrimStart();
            }

            string key = trimmed.Split(':', 2)[0].Trim().Trim('"', '\'');
            if (string.Equals(key, DispatchTrigger, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether this line is the top-level trigger key, and what follows it on the same line.
    /// </summary>
    /// <remarks>
    /// The key is often quoted as <c>"on":</c> because YAML 1.1 reads a bare <c>on</c> as a boolean, and both
    /// spellings mean the same thing to GitHub.
    /// </remarks>
    private static bool TryReadTriggerKey(string line, out string remainder)
    {
        remainder = string.Empty;
        if (line.Length == 0 || char.IsWhiteSpace(line[0]))
        {
            return false;
        }

        int separator = line.IndexOf(':');
        if (separator < 0)
        {
            return false;
        }

        string key = line[..separator].Trim().Trim('"', '\'');
        if (!string.Equals(key, "on", StringComparison.Ordinal))
        {
            return false;
        }

        remainder = line[(separator + 1)..].Trim();
        return true;
    }

    private static string WithoutComment(string line)
    {
        int comment = line.IndexOf('#');
        return comment < 0 ? line : line[..comment];
    }
}
