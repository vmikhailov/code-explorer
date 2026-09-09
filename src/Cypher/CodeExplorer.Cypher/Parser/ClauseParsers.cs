using CodeExplorer.Cypher.Ast;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;

namespace CodeExplorer.Cypher.Parser;

public static class ClauseParsers
{
    // WHERE predicate
    public static TokenListParser<CypherToken, WhereClause> Where { get; } =
        from whereTok in Token.EqualTo(CypherToken.Where)
        from pred in ExpressionParsers.ExpressionParser
        select new WhereClause(pred);

    // MATCH [path, path...] [WHERE ...]
    public static TokenListParser<CypherToken, MatchClause> Match { get; } =
        from opt in Token.EqualTo(CypherToken.Optional).Optional()
        from match in Token.EqualTo(CypherToken.Match)
        from paths in PatternParsers.Path.ManyDelimitedBy(Token.EqualTo(CypherToken.Comma))
        from whereClause in Where.OptionalOrDefault()
        select new MatchClause(opt.HasValue, paths.ToList(), whereClause);

    // Projection item: expr [AS alias]
    public static TokenListParser<CypherToken, ProjectionItem> ProjectionItem { get; } =
        from expr in ExpressionParsers.ExpressionParser
        from alias in (
            from asTok in Token.EqualTo(CypherToken.As)
            from name in ExpressionParsers.PropertyNameText
            select name
        ).OptionalOrDefault()
        select new ProjectionItem(expr, alias);

    // RETURN [DISTINCT] item, item...
    public static TokenListParser<CypherToken, ReturnClause> Return { get; } =
        from returnTok in Token.EqualTo(CypherToken.Return)
        from distinct in Token.EqualTo(CypherToken.Distinct).Optional()
        from items in ProjectionItem.ManyDelimitedBy(Token.EqualTo(CypherToken.Comma))
        select new ReturnClause(distinct.HasValue, items.ToList());

    // ORDER BY item [ASC|DESC], ...
    public static TokenListParser<CypherToken, OrderByClause> OrderBy { get; } =
        from order in Token.EqualTo(CypherToken.Order)
        from byTok in Token.EqualTo(CypherToken.By)
        from items in (
            from expr in ExpressionParsers.ExpressionParser
            from dir in (
                Token.EqualTo(CypherToken.Desc).Value(true)
                .Or(Token.EqualTo(CypherToken.Asc).Value(false))
            ).Optional()
            select new OrderByItem(expr, dir.GetValueOrDefault(false))
        ).ManyDelimitedBy(Token.EqualTo(CypherToken.Comma))
        select new OrderByClause(items.ToList());

    // SKIP number
    public static TokenListParser<CypherToken, SkipClause> Skip { get; } =
        from skip in Token.EqualTo(CypherToken.Skip)
        from num in Token.EqualTo(CypherToken.Number)
        select new SkipClause(int.Parse(num.ToStringValue()));

    // LIMIT number
    public static TokenListParser<CypherToken, LimitClause> Limit { get; } =
        from limit in Token.EqualTo(CypherToken.Limit)
        from num in Token.EqualTo(CypherToken.Number)
        select new LimitClause(int.Parse(num.ToStringValue()));
}
