using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class Neo4jDriverLibraryParser : ISemanticExtension
{
    public string Type => "db:graph";
    public string Name => "Neo4j";
    public string Id => "neo4j";
    public IReadOnlyList<string> SupportedPatterns => ["Neo4j.Driver", "Neo4j.Driver.*", "Neo4j.Driver.Simple"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> Neo4jMethods = new(StringComparer.Ordinal)
    {
        "Run", "RunAsync", "ExecuteReadAsync", "ExecuteWriteAsync"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsNeo4jCall(node, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsNeo4jCall(node, out var method))
        {
            var query = AdoNetCommandHelper.ExtractSqlFromArguments(node);
            if (!string.IsNullOrEmpty(query))
            {
                var clean = NestedSqlParser.CleanQueryText(query);
                return $"Neo4j: {clean}";
            }
            return $"Neo4j: {method}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsNeo4jCall(node, out _))
        {
            references.Add(new Reference(scopeSymbolId, "neo4j", OntologyConstants.Relationships.UsesDb));
        }
    }

    private static bool IsNeo4jCall(Node node, out string? methodName)
    {
        methodName = null;
        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return false;

        var func = node.GetFunctionNode();
        if (func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            var name = func.GetChildFieldText(TreeSitterSyntax.Fields.Name);
            if (!string.IsNullOrEmpty(name) && Neo4jMethods.Contains(name))
            {
                methodName = name;
                return true;
            }
        }
        return false;
    }
}
