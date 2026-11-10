using NUnit.Framework;
using Superpower;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace CodeExplorer.Tests;

public enum TestSqlToken
{
    None,
    Select,
    From,
    Identifier,
    StringLiteral
}

[TestFixture]
public class SuperpowerSqlTokenizerTests
{
    [Test]
    public void StringLiteral_ContainingDashes_IsNotCorruptedByComment()
    {
        var tokenizer = new TokenizerBuilder<TestSqlToken>()
            .Ignore(Span.WhiteSpace)
            .Match(Span.Regex(@"(?s)'(?:[^'\\]|\\.|'')*'"), TestSqlToken.StringLiteral)
            .Ignore(Span.Regex(@"--[^\r\n]*"))
            .Ignore(Comment.CStyle)
            .Match(Span.Regex(@"(?i)\bSELECT\b"), TestSqlToken.Select)
            .Match(Span.Regex(@"(?i)\bFROM\b"), TestSqlToken.From)
            .Match(Identifier.CStyle, TestSqlToken.Identifier)
            .Build();

        var sql = "SELECT 'hello -- world' AS col FROM my_table -- trailing comment";
        var tokenList = tokenizer.Tokenize(sql).ToList();

        tokenList.Count.Should().Be(6);
        tokenList[0].Kind.Should().Be(TestSqlToken.Select);
        tokenList[1].Kind.Should().Be(TestSqlToken.StringLiteral);
        tokenList[1].ToStringValue().Should().Be("'hello -- world'");
        tokenList[2].Kind.Should().Be(TestSqlToken.Identifier); // AS
        tokenList[3].Kind.Should().Be(TestSqlToken.Identifier); // col
        tokenList[4].Kind.Should().Be(TestSqlToken.From);
        tokenList[5].Kind.Should().Be(TestSqlToken.Identifier); // my_table
    }
}
