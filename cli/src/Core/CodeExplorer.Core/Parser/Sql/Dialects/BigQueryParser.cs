namespace CodeExplorer.Core.Parser.Sql.Dialects;

public sealed class BigQueryParser : BaseSqlParser
{
    public override SqlDialect Dialect => SqlDialect.BigQuery;

    public override bool IsTableValuedFunction(string name)
    {
        var lower = name.ToLowerInvariant();
        return (lower is "unnest" or "external_query") || base.IsTableValuedFunction(name);
    }

    public override bool IsDialectKeyword(string token)
    {
        var lower = token.ToLowerInvariant();
        return (lower is "struct" or "_table_suffix") || base.IsDialectKeyword(token);
    }
}
