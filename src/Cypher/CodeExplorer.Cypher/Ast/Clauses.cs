namespace CodeExplorer.Cypher.Ast;

public record MatchClause(bool IsOptional, List<PathPattern> Paths, WhereClause? Where = null);

public record WhereClause(Expression Predicate);

public record WithClause(bool IsDistinct, List<ProjectionItem> Items, WhereClause? Where = null);

public record UnwindClause(Expression Expression, string Alias);

public record ProjectionItem(Expression Expression, string? Alias);

public record ReturnClause(bool IsDistinct, List<ProjectionItem> Items);

public record OrderByItem(Expression Expression, bool IsDescending);

public record OrderByClause(List<OrderByItem> Items);

public record SkipClause(Expression Expression, int? StaticCount = null)
{
    public SkipClause(int count) : this(new NumberLiteralExpression(count, true), count) { }
    public int Count => StaticCount ?? (Expression is NumberLiteralExpression num ? (int)num.Value : 0);
}

public record LimitClause(Expression Expression, int? StaticCount = null)
{
    public LimitClause(int count) : this(new NumberLiteralExpression(count, true), count) { }
    public int Count => StaticCount ?? (Expression is NumberLiteralExpression num ? (int)num.Value : 0);
}

public record CallClause(CypherQuery Subquery);

public record UnionClause(bool IsAll, CypherQuery Query);
