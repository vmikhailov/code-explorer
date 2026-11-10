namespace CodeExplorer.Core.Parser.Sql.Dialects;

public sealed class MySqlParser : BaseSqlParser
{
    public override SqlDialect Dialect => SqlDialect.MySql;

    public override bool IsTableValuedFunction(string name)
    {
        var lower = name.ToLowerInvariant();
        return (lower is "json_table") || base.IsTableValuedFunction(name);
    }

    public override bool IsDialectKeyword(string token)
    {
        var lower = token.ToLowerInvariant();
        return (lower is "straight_join") || base.IsDialectKeyword(token);
    }
}
