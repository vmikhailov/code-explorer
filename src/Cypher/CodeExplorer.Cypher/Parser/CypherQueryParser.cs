using CodeExplorer.Cypher.Ast;
using Superpower;
using Superpower.Model;

namespace CodeExplorer.Cypher.Parser;

public static class CypherQueryParser
{
    public static TokenListParser<CypherToken, (List<MatchClause> Matches, List<WithClause> Withs, List<UnwindClause> Unwinds)> QueryBody { get; } =
        input =>
        {
            var matches = new List<MatchClause>();
            var withs = new List<WithClause>();
            var unwinds = new List<UnwindClause>();
            var remainder = input;

            while (!remainder.IsAtEnd)
            {
                var next = remainder.ConsumeToken();
                if (!next.HasValue) break;

                if (next.Value.Kind == CypherToken.Match || next.Value.Kind == CypherToken.Optional)
                {
                    var mRes = ClauseParsers.Match(remainder);
                    if (!mRes.HasValue) break;
                    matches.Add(mRes.Value);
                    remainder = mRes.Remainder;
                }
                else if (next.Value.Kind == CypherToken.With)
                {
                    var wRes = ClauseParsers.With(remainder);
                    if (!wRes.HasValue) break;
                    withs.Add(wRes.Value);
                    remainder = wRes.Remainder;
                }
                else if (next.Value.Kind == CypherToken.Unwind)
                {
                    var uRes = ClauseParsers.Unwind(remainder);
                    if (!uRes.HasValue) break;
                    unwinds.Add(uRes.Value);
                    remainder = uRes.Remainder;
                }
                else
                {
                    break;
                }
            }

            if (matches.Count == 0 && withs.Count == 0 && unwinds.Count == 0)
            {
                return TokenListParserResult.Empty<CypherToken, (List<MatchClause>, List<WithClause>, List<UnwindClause>)>(input);
            }

            return TokenListParserResult.Value((matches, withs, unwinds), input, remainder);
        };

    public static TokenListParser<CypherToken, CypherQuery> Query { get; } =
        from body in QueryBody
        from topWhere in ClauseParsers.Where.OptionalOrDefault()
        from ret in ClauseParsers.Return
        from orderBy in ClauseParsers.OrderBy.OptionalOrDefault()
        from skip in ClauseParsers.Skip.OptionalOrDefault()
        from limit in ClauseParsers.Limit.OptionalOrDefault()
        select new CypherQuery(
            body.Matches,
            topWhere,
            ret,
            orderBy,
            skip,
            limit,
            body.Withs,
            body.Unwinds
        );

    public static CypherQuery Parse(string cypherText)
    {
        var tokens = CypherTokenizer.Instance.Tokenize(cypherText);
        return Query.Parse(tokens);
    }

    public static bool TryParse(string cypherText, out CypherQuery? query, out string? error)
    {
        var tokens = CypherTokenizer.Instance.Tokenize(cypherText);
        var result = Query.TryParse(tokens);
        if (result.HasValue)
        {
            query = result.Value;
            error = null;
            return true;
        }

        query = null;
        error = result.FormatErrorMessageFragment();
        return false;
    }
}
