namespace CodeExplorer.Core.Parser.Sql.Dialects;

public sealed class AnsiSqlParser : BaseSqlParser
{
    public override SqlDialect Dialect => SqlDialect.Ansi;
}
