namespace CodeExplorer.Core.Parser.Sql.Dialects;

public sealed class SoqlParser : BaseSqlParser
{
    public override SqlDialect Dialect => SqlDialect.Soql;

    public override bool IsDialectKeyword(string token)
    {
        var lower = token.ToLowerInvariant();
        return (lower is "typeof" or "when" or "then" or "scope") || base.IsDialectKeyword(token);
    }
}
