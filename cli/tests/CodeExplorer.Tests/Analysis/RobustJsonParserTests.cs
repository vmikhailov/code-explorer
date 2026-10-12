using AwesomeAssertions;
using CodeExplorer.Core.Analysis;
using NUnit.Framework;

namespace CodeExplorer.Tests.Analysis;

[TestFixture]
public class RobustJsonParserTests
{
    private record SampleModel(string Name, int Value, List<string>? Items);

    [Test]
    public void CleanAndHealJson_ValidJson_ReturnsSame()
    {
        var input = """{"name": "test", "value": 42}""";
        var result = RobustJsonParser.CleanAndHealJson(input);
        result.Should().Be(input);
    }

    [Test]
    public void CleanAndHealJson_WithMarkdownFences_StripsFences()
    {
        var input = """
            ```json
            {
                "name": "test",
                "value": 42
            }
            ```
            """;
        var result = RobustJsonParser.Deserialize<SampleModel>(input);
        result.Should().NotBeNull();
        result!.Name.Should().Be("test");
        result.Value.Should().Be(42);
    }

    [Test]
    public void CleanAndHealJson_WithThinkTags_StripsReasoning()
    {
        var input = """
            <think>
            The user wants a domain called Billing with value 100.
            Let's construct the JSON output now.
            </think>
            {
                "name": "Billing",
                "value": 100
            }
            """;
        var result = RobustJsonParser.Deserialize<SampleModel>(input);
        result.Should().NotBeNull();
        result!.Name.Should().Be("Billing");
        result.Value.Should().Be(100);
    }

    [Test]
    public void CleanAndHealJson_WithConversationalPreambleAndPostamble_ExtractsJson()
    {
        var input = """
            Here is the requested architecture summary:
            {
                "name": "Core",
                "value": 1,
                "items": ["a", "b"]
            }
            I hope this meets your architectural requirements! Let me know if you need more.
            """;
        var result = RobustJsonParser.Deserialize<SampleModel>(input);
        result.Should().NotBeNull();
        result!.Name.Should().Be("Core");
        result.Items.Should().HaveCount(2);
    }

    [Test]
    public void CleanAndHealJson_WithTrailingCommas_HealsTrailingCommas()
    {
        var input = """
            {
                "name": "Trailing",
                "value": 99,
                "items": [
                    "first",
                    "second",
                ],
            }
            """;
        var result = RobustJsonParser.Deserialize<SampleModel>(input);
        result.Should().NotBeNull();
        result!.Name.Should().Be("Trailing");
        result.Items.Should().HaveCount(2);
    }

    [Test]
    public void CleanAndHealJson_WithComments_StripsComments()
    {
        var input = """
            {
                // This is a line comment
                "name": "Commented",
                /* This is a
                   multi-line comment */
                "value": 12
            }
            """;
        var result = RobustJsonParser.Deserialize<SampleModel>(input);
        result.Should().NotBeNull();
        result!.Name.Should().Be("Commented");
        result.Value.Should().Be(12);
    }

    [Test]
    public void CleanAndHealJson_TruncatedOutput_AutoClosesBrackets()
    {
        var input = """
            {
                "name": "Truncated",
                "value": 5,
                "items": [
                    "item1",
                    "ite
            """;
        var result = RobustJsonParser.Deserialize<SampleModel>(input);
        result.Should().NotBeNull();
        result!.Name.Should().Be("Truncated");
    }
}
