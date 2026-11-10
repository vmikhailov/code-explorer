namespace CodeExplorer.Core.Parser.Sql.Dialects;

public sealed class PlSqlParser : BaseSqlParser
{
    public override SqlDialect Dialect => SqlDialect.PlSql;

    public override bool IsTableValuedFunction(string name)
    {
        var lower = name.ToLowerInvariant();
        return (lower is "table" or "xmltable") || base.IsTableValuedFunction(name);
    }

    public override bool IsDialectKeyword(string token)
    {
        var lower = token.ToLowerInvariant();
        // DUAL is Oracle's 1-row pseudo-table, not a real persistent database entity
        return (lower is "dual" or "rownum") || base.IsDialectKeyword(token);
    }
}
