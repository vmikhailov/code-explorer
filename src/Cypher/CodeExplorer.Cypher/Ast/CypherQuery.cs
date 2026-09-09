namespace CodeExplorer.Cypher.Ast;

public record CypherQuery(
    List<MatchClause> Matches,
    WhereClause? Where,
    ReturnClause Return,
    OrderByClause? OrderBy = null,
    SkipClause? Skip = null,
    LimitClause? Limit = null
);
