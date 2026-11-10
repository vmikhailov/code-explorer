namespace CodeExplorer.Core.Parser.Sql.Dialects;

public sealed class PostgreSqlParser : BaseSqlParser
{
    public override SqlDialect Dialect => SqlDialect.PostgreSql;

    public override bool IsTableValuedFunction(string name)
    {
        var lower = name.ToLowerInvariant();
        return (lower is "unnest" or "generate_series" or "jsonb_array_elements"
            or "jsonb_array_elements_text" or "jsonb_to_recordset" or "jsonb_each"
            or "jsonb_each_text" or "jsonb_populate_recordset" or "json_array_elements"
            or "json_to_recordset" or "string_to_array" or "regexp_split_to_table")
            || base.IsTableValuedFunction(name);
    }

    public override bool IsDialectKeyword(string token)
    {
        var lower = token.ToLowerInvariant();
        return (lower is "only" or "lateral" or "returning") || base.IsDialectKeyword(token);
    }
}
