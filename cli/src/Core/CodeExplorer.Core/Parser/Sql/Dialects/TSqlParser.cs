namespace CodeExplorer.Core.Parser.Sql.Dialects;

public sealed class TSqlParser : BaseSqlParser
{
    public override SqlDialect Dialect => SqlDialect.TSql;

    public override bool IsTableValuedFunction(string name)
    {
        var lower = name.ToLowerInvariant();
        return (lower is "string_split" or "openjson" or "nodes" or "fn_trace_gettable")
            || base.IsTableValuedFunction(name);
    }

    public override bool IsTemporaryTable(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.StartsWith('#') || lower.StartsWith('@') || base.IsTemporaryTable(name);
    }
}
