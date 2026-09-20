using Superpower;
using Superpower.Model;

namespace CodeExplorer.Cypher.Parser;

public static class ParserExtensions
{
    public static TokenListParser<TToken, T?> OptionalOrDefault<TToken, T>(this TokenListParser<TToken, T> parser) where T : class
    {
        ArgumentNullException.ThrowIfNull(parser);

        return input =>
        {
            var result = parser(input);
            if (result.HasValue)
            {
                return TokenListParserResult.Value<TToken, T?>(result.Value, input, result.Remainder);
            }
            return TokenListParserResult.Value<TToken, T?>(null, input, input);
        };
    }
}
