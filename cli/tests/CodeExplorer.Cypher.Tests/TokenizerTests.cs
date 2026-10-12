using AwesomeAssertions;
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

        tokens.Select(t => t.Kind).Should().Equal(expectedTokens);
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

        (tokens.Any(t => t.Kind == CypherToken.Match)).Should().BeTrue();
        (tokens.Any(t => t.Kind == CypherToken.Where)).Should().BeTrue();
        (tokens.Any(t => t.Kind == CypherToken.Return)).Should().BeTrue();
        (tokens.Count(t => t.Kind == CypherToken.Identifier)).Should().BeGreaterThanOrEqualTo(3); // n, File, name, n
    }

    [Test]
    public void Tokenizer_RecognizesParametersAndLiterals()
    {
        var input = "$workspaceId 'hello world' \"escaped \\\"string\\\"\" 42 3.14 true false null";
        var tokens = CypherTokenizer.Instance.Tokenize(input).ToList();

        tokens[0].Kind.Should().Be(CypherToken.Parameter);
        tokens[0].ToStringValue().Should().Be("$workspaceId");

        tokens[1].Kind.Should().Be(CypherToken.StringLiteral);
        tokens[2].Kind.Should().Be(CypherToken.StringLiteral);

        tokens[3].Kind.Should().Be(CypherToken.Number);
        tokens[4].Kind.Should().Be(CypherToken.Number);

        tokens[5].Kind.Should().Be(CypherToken.True);
        tokens[6].Kind.Should().Be(CypherToken.False);
        tokens[7].Kind.Should().Be(CypherToken.Null);
    }

    [Test]
    public void Tokenizer_RecognizesArrowsAndOperators()
    {
        var input = "-> <- - .. * | : = != <> <= >= < > +";
        var tokens = CypherTokenizer.Instance.Tokenize(input).ToList();

        var kinds = tokens.Select(t => t.Kind).ToList();
        kinds.Should().Contain(CypherToken.ArrowRight);
        kinds.Should().Contain(CypherToken.ArrowLeft);
        kinds.Should().Contain(CypherToken.Dash);
        kinds.Should().Contain(CypherToken.DotDot);
        kinds.Should().Contain(CypherToken.Asterisk);
        kinds.Should().Contain(CypherToken.Pipe);
        kinds.Should().Contain(CypherToken.Colon);
        kinds.Should().Contain(CypherToken.Equal);
        kinds.Should().Contain(CypherToken.NotEqual);
        kinds.Should().Contain(CypherToken.LessOrEqual);
        kinds.Should().Contain(CypherToken.GreaterOrEqual);
        kinds.Should().Contain(CypherToken.Plus);
    }
}
