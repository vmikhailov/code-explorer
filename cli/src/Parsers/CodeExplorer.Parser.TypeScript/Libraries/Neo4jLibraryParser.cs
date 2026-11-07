using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class Neo4jLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> Neo4jMethods = new(StringComparer.Ordinal)
    {
        "run", "executeRead", "executeWrite", "beginTransaction"
    };

    public Neo4jLibraryParser()
    {
        Type = SemanticExtensionType.GraphDb;
        Name = "Neo4j";
        Id = "neo4j";
        SupportedPatterns = ["neo4j-driver"];
        LibraryRole = LibraryRole.OrmOrDatabase;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.TypeScript.CallExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsNeo4jCall(node) ? OntologyConstants.NodeLabels.Query : null,
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (!IsNeo4jCall(node)) return null;
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
            }
        ];
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
