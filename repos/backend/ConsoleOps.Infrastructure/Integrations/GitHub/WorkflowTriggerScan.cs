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
    /// The inputs the dispatch trigger declares, in declaration order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read from the <c>workflow_dispatch.inputs</c> block only, so nothing a form asks for was invented here: a
    /// workflow that declares no inputs yields none, and a definition this scan cannot follow yields none rather
    /// than a guess at what it might want.
    /// </para>
    /// <para>
    /// Deliberately shallow. Each input's own keys - description, required, type, default, options - are read
    /// where they are simple scalars or an inline list, which is how workflows are actually written. Anything more
    /// elaborate is left out rather than half understood.
    /// </para>
    /// </remarks>
    internal static IReadOnlyList<WorkflowInputDeclaration> ReadDispatchInputs(string definition)
    {
        if (string.IsNullOrWhiteSpace(definition))
        {
            return [];
        }

        string[] lines = definition.Replace("\r\n", "\n").Split('\n');
        int inputsIndent = -1;
        int start = -1;

        for (int index = 0; index < lines.Length; index++)
        {
            string line = WithoutComment(lines[index]);
            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            int indent = IndentOf(line);

            if (start < 0)
            {
                // Find `inputs:` nested under `workflow_dispatch:`; anything shallower ends the search area.
                if (trimmed.StartsWith("workflow_dispatch:", StringComparison.Ordinal))
                {
                    inputsIndent = indent;
                    continue;
                }

                if (inputsIndent >= 0 && indent <= inputsIndent && !trimmed.StartsWith("inputs:", StringComparison.Ordinal))
                {
                    inputsIndent = -1;
                    continue;
                }

                if (inputsIndent >= 0 && trimmed.StartsWith("inputs:", StringComparison.Ordinal))
                {
                    start = index + 1;
                    inputsIndent = indent;
                }

                continue;
            }

            if (indent <= inputsIndent)
            {
                break;
            }
        }

        return start < 0 ? [] : ReadInputBlock(lines, start, inputsIndent);
    }

    private static List<WorkflowInputDeclaration> ReadInputBlock(string[] lines, int start, int inputsIndent)
    {
        List<WorkflowInputDeclaration> inputs = [];
        int nameIndent = -1;
        string? name = null;
        string? description = null;
        string? type = null;
        string? defaultValue = null;
        bool required = false;
        List<string> options = [];

        void Flush()
        {
            if (name is not null)
            {
                inputs.Add(new WorkflowInputDeclaration(
                    name,
                    description,
                    required,
                    type ?? "string",
                    defaultValue,
                    options));
            }

            name = null;
            description = null;
            type = null;
            defaultValue = null;
            required = false;
            options = [];
        }

        for (int index = start; index < lines.Length; index++)
        {
            string line = WithoutComment(lines[index]);
            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            int indent = IndentOf(line);
            if (indent <= inputsIndent)
            {
                break;
            }

            if (nameIndent < 0)
            {
                nameIndent = indent;
            }

            if (indent == nameIndent)
            {
                Flush();
                name = Unquote(trimmed.Split(':', 2)[0]);
                continue;
            }

            string[] parts = trimmed.Split(':', 2);
            string key = Unquote(parts[0]);
            string value = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            switch (key)
            {
                case "description":
                    description = Unquote(value);
                    break;
                case "required":
                    required = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                    break;
                case "type":
                    type = Unquote(value);
                    break;
                case "default":
                    defaultValue = Unquote(value);
                    break;
                case "options":
                    options = ReadInlineList(value);
                    break;
                default:
                    // A nested list under `options:` arrives as `- value` lines.
                    if (trimmed.StartsWith("- ", StringComparison.Ordinal))
                    {
                        options.Add(Unquote(trimmed[2..].Trim()));
                    }

                    break;
            }
        }

        Flush();
        return inputs;
    }

    private static List<string> ReadInlineList(string value)
    {
        string trimmed = value.Trim();
        if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']'))
        {
            return [];
        }

        return trimmed[1..^1]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Unquote)
            .Where(entry => entry.Length > 0)
            .ToList();
    }

    private static int IndentOf(string line) => line.Length - line.TrimStart().Length;

    private static string Unquote(string value) => value.Trim().Trim('"', '\'').Trim();

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

/// <summary>One declared dispatch input, as written in the workflow definition.</summary>
internal sealed record WorkflowInputDeclaration(
    string Name,
    string? Description,
    bool Required,
    string Type,
    string? Default,
    IReadOnlyList<string> Options);