namespace CodeExplorer.Cypher.Ast;

public record MatchClause(bool IsOptional, List<PathPattern> Paths, WhereClause? Where = null);

public record WhereClause(Expression Predicate);

public record ProjectionItem(Expression Expression, string? Alias);

public record ReturnClause(bool IsDistinct, List<ProjectionItem> Items);

public record OrderByItem(Expression Expression, bool IsDescending);

public record OrderByClause(List<OrderByItem> Items);

public record SkipClause(int Count);

public record LimitClause(int Count);
