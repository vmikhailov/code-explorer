using CodeExplorer.Core.Parser.Sql.Ast;

namespace CodeExplorer.Core.Parser.Sql.Dialects;

public interface ISqlDialectParser
{
    SqlDialect Dialect { get; }
    SqlScriptAst Parse(string sqlText);
    bool TryParse(string sqlText, out SqlScriptAst? script, out string? errorMessage);
}
