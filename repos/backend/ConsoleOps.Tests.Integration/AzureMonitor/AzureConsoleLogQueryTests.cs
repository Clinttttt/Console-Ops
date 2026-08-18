using ConsoleOps.Infrastructure.Integrations.AzureMonitor;

namespace ConsoleOps.Tests.Integration.AzureMonitor;

/// <summary>
/// The log query carries operator-supplied text, so these tests are about keeping that text data.
/// </summary>
public sealed class AzureConsoleLogQueryTests
{
    [Fact]
    public void Build_ReadsBothTableShapesOrderedByTheEmitterClock()
    {
        string query = AzureConsoleLogQuery.Build("spinner-api", 200, null);

        // Two table shapes exist in the wild; reading only one returns an empty stream on the other.
        Assert.Contains("union isfuzzy=true", query, StringComparison.Ordinal);
        Assert.Contains($"({AzureConsoleLogQuery.TableName}", query, StringComparison.Ordinal);
        Assert.Contains($"({AzureConsoleLogQuery.LegacyTableName}", query, StringComparison.Ordinal);
        Assert.Contains("ContainerAppName =~ \"spinner-api\"", query, StringComparison.Ordinal);
        Assert.Contains("ContainerAppName_s =~ \"spinner-api\"", query, StringComparison.Ordinal);
        // Ingestion time is shared across a batch, so ordering by it would scramble a stack trace.
        Assert.Contains("EmittedAt = coalesce(time_t, TimeGenerated)", query, StringComparison.Ordinal);
        Assert.Contains("order by EmittedAt desc", query, StringComparison.Ordinal);
        Assert.Contains("take 200", query, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithoutSearch_DoesNotFilterOnText()
    {
        string query = AzureConsoleLogQuery.Build("spinner-api", 50, "   ");

        Assert.DoesNotContain("Message contains", query, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithSearch_FiltersOnAnEscapedLiteral()
    {
        string query = AzureConsoleLogQuery.Build("spinner-api", 50, " order 2048 ");

        Assert.Contains("| where Message contains \"order 2048\"", query, StringComparison.Ordinal);
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
