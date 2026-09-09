using CodeExplorer.Cypher.Ast;
using Superpower;

namespace CodeExplorer.Cypher.Parser;

public static class CypherQueryParser
{
    public static TokenListParser<CypherToken, CypherQuery> Query { get; } =
        from matches in ClauseParsers.Match.AtLeastOnce()
        from topWhere in ClauseParsers.Where.OptionalOrDefault()
        from ret in ClauseParsers.Return
        from orderBy in ClauseParsers.OrderBy.OptionalOrDefault()
        from skip in ClauseParsers.Skip.OptionalOrDefault()
        from limit in ClauseParsers.Limit.OptionalOrDefault()
        select new CypherQuery(
            matches.ToList(),
            topWhere,
            ret,
            orderBy,
            skip,
            limit
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
