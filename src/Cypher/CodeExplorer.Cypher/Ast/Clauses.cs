namespace CodeExplorer.Cypher.Ast;

public record MatchClause(bool IsOptional, List<PathPattern> Paths, WhereClause? Where = null);

public record WhereClause(Expression Predicate);

public record WithClause(bool IsDistinct, List<ProjectionItem> Items, WhereClause? Where = null);

public record UnwindClause(Expression Expression, string Alias);

public record ProjectionItem(Expression Expression, string? Alias);

public record ReturnClause(bool IsDistinct, List<ProjectionItem> Items);

public record OrderByItem(Expression Expression, bool IsDescending);

public record OrderByClause(List<OrderByItem> Items);

public record SkipClause(int Count);

public record LimitClause(int Count);

public record CallClause(CypherQuery Subquery);

public record UnionClause(bool IsAll, CypherQuery Query);
