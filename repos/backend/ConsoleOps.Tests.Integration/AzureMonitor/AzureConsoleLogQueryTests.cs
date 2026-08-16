using ConsoleOps.Infrastructure.Integrations.AzureMonitor;

namespace ConsoleOps.Tests.Integration.AzureMonitor;

/// <summary>
/// The log query carries operator-supplied text, so these tests are about keeping that text data.
/// </summary>
public sealed class AzureConsoleLogQueryTests
{
    [Fact]
    public void Build_ProducesABoundedQueryOverTheDocumentedTable()
    {
        string query = AzureConsoleLogQuery.Build("spinner-api", 200, null);

        Assert.StartsWith("ContainerAppConsoleLogs", query, StringComparison.Ordinal);
        Assert.Contains("where ContainerAppName == \"spinner-api\"", query, StringComparison.Ordinal);
        Assert.Contains("order by TimeGenerated desc", query, StringComparison.Ordinal);
        Assert.Contains("take 200", query, StringComparison.Ordinal);
        // Basic-tier tables restrict the operator set, so the query stays inside it.
        Assert.DoesNotContain("join", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("summarize", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_WithoutSearch_DoesNotFilterOnText()
    {
        string query = AzureConsoleLogQuery.Build("spinner-api", 50, "   ");

        Assert.DoesNotContain("Log contains", query, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithSearch_FiltersOnAnEscapedLiteral()
    {
        string query = AzureConsoleLogQuery.Build("spinner-api", 50, " order 2048 ");

        Assert.Contains("| where Log contains \"order 2048\"", query, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("say \"hello\"", "\"say \\\"hello\\\"\"")]
    [InlineData("back\\slash", "\"back\\\\slash\"")]
    [InlineData("line\nbreak", "\"line\\nbreak\"")]
    [InlineData("carriage\rreturn", "\"carriage\\rreturn\"")]
    [InlineData("tab\tstop", "\"tab\\tstop\"")]
    [InlineData("bell\u0007", "\"bell\\u0007\"")]
    public void Literal_EscapesWhatCouldBecomeSyntax(string value, string expected) =>
        Assert.Equal(expected, AzureConsoleLogQuery.Literal(value));

    [Fact]
    public void Literal_KeepsAnInjectionAttemptInsideTheString()
    {
        // A quote that closed the literal would let the rest become query text.
        string literal = AzureConsoleLogQuery.Literal("\" | project Log | take 1 //");

        Assert.Equal("\"\\\" | project Log | take 1 //\"", literal);
        Assert.Equal(1, literal.Count(character => character == '"') - 2);
    }

    [Fact]
    public void Build_RefusesAnUnusableRequest()
    {
        Assert.Throws<ArgumentException>(() => AzureConsoleLogQuery.Build("   ", 10, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => AzureConsoleLogQuery.Build("spinner-api", 0, null));
    }
}
