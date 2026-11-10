namespace CodeExplorer.Core.Parser.Sql.Dialects;

public sealed class ClickHouseParser : BaseSqlParser
{
    public override SqlDialect Dialect => SqlDialect.ClickHouse;

    public override bool IsTableValuedFunction(string name)
    {
        var lower = name.ToLowerInvariant();
        return (lower is "numbers" or "remote" or "cluster" or "s3" or "url" or "file" or "merge")
            || base.IsTableValuedFunction(name);
    }

    public override bool IsDialectKeyword(string token)
    {
        var lower = token.ToLowerInvariant();
        return (lower is "final" or "sample" or "engine" or "mergetree") || base.IsDialectKeyword(token);
    }
}
