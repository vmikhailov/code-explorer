using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class Neo4jLibraryParser : ILibraryParser
{
    public string Type => "db:graph";
    public string Name => "Neo4j";
    public string Id => "neo4j";
    public IReadOnlyList<string> SupportedPatterns => ["neo4j-driver"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> Neo4jMethods = new(StringComparer.Ordinal)
    {
        "run", "executeRead", "executeWrite", "beginTransaction"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsNeo4jCall(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsNeo4jCall(node))
        {
            var query = AstHelper.ExtractFirstStringArgument(node);
            if (!string.IsNullOrEmpty(query))
            {
                var clean = NestedSqlParser.CleanQueryText(query);
                return $"Neo4j: {clean}";
            }

            if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName))
            {
                var target = objNode.IsValid() ? objNode.Text : "session";
                return $"Neo4j: {target}.{propName}";
            }

            return "Neo4j Query";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // Graph queries represent Query nodes
    }

    private static bool IsNeo4jCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        if (AstHelper.TryGetMemberAccess(node, out _, out var propName))
        {
            return propName != null && Neo4jMethods.Contains(propName);
        }

        return false;
    }
}
