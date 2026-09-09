using System.Linq;
using CodeExplorer.Cypher.Parser;
using NUnit.Framework;

namespace CodeExplorer.Cypher.Tests;

[TestFixture]
public class TokenizerTests
{
    [Test]
    public void Tokenizer_RecognizesKeywords_CaseInsensitively()
    {
        var input = "match OPTIONAL match WhErE ReTuRn distinct AS order by asc desc skip limit and or not";
        var tokens = CypherTokenizer.Instance.Tokenize(input).ToList();

        var expectedTokens = new[]
        {
            CypherToken.Match,
            CypherToken.Optional,
            CypherToken.Match,
            CypherToken.Where,
            CypherToken.Return,
            CypherToken.Distinct,
            CypherToken.As,
            CypherToken.Order,
            CypherToken.By,
            CypherToken.Asc,
            CypherToken.Desc,
            CypherToken.Skip,
            CypherToken.Limit,
            CypherToken.And,
            CypherToken.Or,
            CypherToken.Not
        };

        Assert.That(tokens.Select(t => t.Kind), Is.EqualTo(expectedTokens));
    }

    [Test]
    public void Tokenizer_IgnoresComments_SingleLineAndBlock()
    {
        var input = @"
            // This is a single line comment
            MATCH (n:File) /* This is a
            multi-line C-style comment */
            WHERE n.name = 'test' // end of line comment
            RETURN n
        ";

        var tokens = CypherTokenizer.Instance.Tokenize(input).ToList();

        Assert.That(tokens.Any(t => t.Kind == CypherToken.Match), Is.True);
        Assert.That(tokens.Any(t => t.Kind == CypherToken.Where), Is.True);
        Assert.That(tokens.Any(t => t.Kind == CypherToken.Return), Is.True);
        Assert.That(tokens.Count(t => t.Kind == CypherToken.Identifier), Is.GreaterThanOrEqualTo(3)); // n, File, name, n
    }

    [Test]
    public void Tokenizer_RecognizesParametersAndLiterals()
    {
        var input = "$workspaceId 'hello world' \"escaped \\\"string\\\"\" 42 3.14 true false null";
        var tokens = CypherTokenizer.Instance.Tokenize(input).ToList();

        Assert.That(tokens[0].Kind, Is.EqualTo(CypherToken.Parameter));
        Assert.That(tokens[0].ToStringValue(), Is.EqualTo("$workspaceId"));

        Assert.That(tokens[1].Kind, Is.EqualTo(CypherToken.StringLiteral));
        Assert.That(tokens[2].Kind, Is.EqualTo(CypherToken.StringLiteral));

        Assert.That(tokens[3].Kind, Is.EqualTo(CypherToken.Number));
        Assert.That(tokens[4].Kind, Is.EqualTo(CypherToken.Number));

        Assert.That(tokens[5].Kind, Is.EqualTo(CypherToken.True));
        Assert.That(tokens[6].Kind, Is.EqualTo(CypherToken.False));
        Assert.That(tokens[7].Kind, Is.EqualTo(CypherToken.Null));
    }

    [Test]
    public void Tokenizer_RecognizesArrowsAndOperators()
    {
        var input = "-> <- - .. * | : = != <> <= >= < > +";
        var tokens = CypherTokenizer.Instance.Tokenize(input).ToList();

        var kinds = tokens.Select(t => t.Kind).ToList();
        Assert.That(kinds, Contains.Item(CypherToken.ArrowRight));
        Assert.That(kinds, Contains.Item(CypherToken.ArrowLeft));
        Assert.That(kinds, Contains.Item(CypherToken.Dash));
        Assert.That(kinds, Contains.Item(CypherToken.DotDot));
        Assert.That(kinds, Contains.Item(CypherToken.Asterisk));
        Assert.That(kinds, Contains.Item(CypherToken.Pipe));
        Assert.That(kinds, Contains.Item(CypherToken.Colon));
        Assert.That(kinds, Contains.Item(CypherToken.Equal));
        Assert.That(kinds, Contains.Item(CypherToken.NotEqual));
        Assert.That(kinds, Contains.Item(CypherToken.LessOrEqual));
        Assert.That(kinds, Contains.Item(CypherToken.GreaterOrEqual));
        Assert.That(kinds, Contains.Item(CypherToken.Plus));
    }
}
