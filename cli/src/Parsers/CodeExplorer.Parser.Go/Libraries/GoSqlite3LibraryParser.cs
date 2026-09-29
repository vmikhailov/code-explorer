using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public class GoSqlite3LibraryParser : ISemanticExtension
{
    public string Type => "db:relational";
    public string Name => "SQLite3";
    public string Id => "sqlite3";
    public IReadOnlyList<string> SupportedPatterns => ["github.com/mattn/go-sqlite3"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (GoSqlHelper.IsSqlCall(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (GoSqlHelper.IsSqlCall(node, out var sqlText, out var method))
        {
            if (!string.IsNullOrEmpty(sqlText))
            {
                var clean = NestedSqlParser.CleanQueryText(sqlText);
                if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                {
                    return $"{firstWord} Query: {clean}";
                }
                return $"SQLite: {clean}";
            }
            return $"SQLite: {method ?? "Query"}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (GoSqlHelper.IsSqlCall(node, out var sqlText, out _))
        {
            if (!string.IsNullOrEmpty(sqlText))
            {
                NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
            }
            references.Add(new Reference(scopeSymbolId, "sqlite", OntologyConstants.Relationships.UsesDb));
        }
    }
}
