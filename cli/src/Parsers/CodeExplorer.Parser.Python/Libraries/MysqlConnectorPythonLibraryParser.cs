using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class MysqlConnectorPythonLibraryParser : ISemanticExtension
{
    public string Type => "db:relational";
    public string Name => "MySQL (mysql-connector)";
    public string Id => "mysql-connector";
    public IReadOnlyList<string> SupportedPatterns => ["mysql.connector", "mysql.connector.*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (DbApiHelper.IsDbApiCall(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (DbApiHelper.IsDbApiCall(node, out var sqlText, out var method))
        {
            return DbApiHelper.FormatQueryIdentifier("MySQL", sqlText, method);
        }
        return null;
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        DbApiHelper.EnrichDbApiSymbol(node, symbol);
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (DbApiHelper.IsDbApiCall(node, out var sqlText, out _))
        {
            if (!string.IsNullOrEmpty(sqlText))
            {
                NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
            }
            references.Add(new Reference(scopeSymbolId, "mysql", OntologyConstants.Relationships.UsesDb));
        }
    }
}
