using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class ElasticsearchPythonLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> EsMethods = new(StringComparer.Ordinal)
    {
        "search", "index", "update", "delete", "count", "bulk", "get", "create"
    };

    public ElasticsearchPythonLibraryParser()
    {
        Type = SemanticExtensionType.AnalyticsDb;
        Name = "Elasticsearch";
        Id = "elasticsearch";
        SupportedPatterns = ["elasticsearch", "elasticsearch.*"];
        LibraryRole = LibraryRole.OrmOrDatabase;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Python.Call }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsEsCall(node, out _, out _) ? OntologyConstants.NodeLabels.Query : null,
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (!IsEsCall(node, out var method, out var indexName)) return null;
                    return !string.IsNullOrEmpty(indexName) ? $"Elasticsearch: {method} ({indexName})" : $"Elasticsearch: {method}";
                },
                CollectReferences = static (node, scopeSymbolId, references, ctx) =>
                {
                    if (IsEsCall(node, out _, out var indexName))
                    {
                        if (!string.IsNullOrEmpty(indexName))
                        {
                            references.Add(new Reference(scopeSymbolId, indexName, OntologyConstants.Relationships.UsesDb));
                        }
                        references.Add(new Reference(scopeSymbolId, ElasticsearchDbId, OntologyConstants.Relationships.UsesDb));
                    }
                }
            }
        ];
    }

    private const string ElasticsearchDbId = "elasticsearch";

    private static bool IsEsCall(Node node, out string? methodName, out string? indexName)
    {
        methodName = null;
        indexName = null;

        if (PythonAstHelper.TryGetMemberAccess(node, out _, out methodName))
        {
            if (methodName != null && EsMethods.Contains(methodName))
            {
                indexName = PythonAstHelper.ResolveCallArgument(node, 0, "index");
                return true;
            }
        }

        return false;
    }
}
