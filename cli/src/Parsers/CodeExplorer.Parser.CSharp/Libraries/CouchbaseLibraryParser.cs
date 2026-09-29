using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class CouchbaseLibraryParser : ISemanticExtension
{
    public string Type => "db:document";
    public string Name => "Couchbase";
    public string Id => "couchbase";
    public IReadOnlyList<string> SupportedPatterns => ["Couchbase", "Couchbase.NetClient", "Couchbase.*"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> CouchbaseMethods = new(StringComparer.Ordinal)
    {
        "GetAsync", "InsertAsync", "UpsertAsync", "ReplaceAsync", "RemoveAsync", "QueryAsync", "Get", "Insert", "Upsert", "Replace", "Remove", "Query"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsCouchbaseCall(node, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsCouchbaseCall(node, out var method))
        {
            if (method is "QueryAsync" or "Query")
            {
                var queryText = AdoNetCommandHelper.ExtractSqlFromArguments(node);
                if (!string.IsNullOrEmpty(queryText))
                {
                    var clean = NestedSqlParser.CleanQueryText(queryText);
                    return $"Couchbase N1QL: {clean}";
                }
            }
            return $"Couchbase: {method}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsCouchbaseCall(node, out var method))
        {
            if (method is "QueryAsync" or "Query")
            {
                var queryText = AdoNetCommandHelper.ExtractSqlFromArguments(node);
                if (!string.IsNullOrEmpty(queryText))
                {
                    NestedSqlParser.TryDetectSqlDependencies(queryText, scopeSymbolId, references);
                    return;
                }
            }
            references.Add(new Reference(scopeSymbolId, "couchbase", OntologyConstants.Relationships.UsesDb));
        }
    }

    private static bool IsCouchbaseCall(Node node, out string? methodName)
    {
        methodName = null;
        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return false;

        var func = node.GetFunctionNode();
        if (func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            var name = func.GetChildFieldText(TreeSitterSyntax.Fields.Name);
            if (!string.IsNullOrEmpty(name) && CouchbaseMethods.Contains(name))
            {
                methodName = name;
                return true;
            }
        }
        return false;
    }
}
