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
        Assert.That(result, Is.EqualTo(input));
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
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("test"));
        Assert.That(result.Value, Is.EqualTo(42));
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
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("Billing"));
        Assert.That(result.Value, Is.EqualTo(100));
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
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("Core"));
        Assert.That(result.Items, Has.Count.EqualTo(2));
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
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("Trailing"));
        Assert.That(result.Items, Has.Count.EqualTo(2));
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
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("Commented"));
        Assert.That(result.Value, Is.EqualTo(12));
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
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("Truncated"));
    }
}
