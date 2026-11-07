using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class MongodbLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> MongoMethods = new(StringComparer.Ordinal)
    {
        "find", "findOne", "insertOne", "insertMany", "updateOne", "updateMany",
        "deleteOne", "deleteMany", "aggregate", "countDocuments", "estimatedDocumentCount",
        "distinct", "bulkWrite", "createIndex", "drop", "dropDatabase", "replaceOne", "command"
    };

    public MongodbLibraryParser()
    {
        Type = SemanticExtensionType.DocumentDb;
        Name = "MongoDB";
        Id = "mongodb";
        SupportedPatterns = ["mongodb"];
        LibraryRole = LibraryRole.OrmOrDatabase;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.TypeScript.CallExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsMongoCall(node) ? OntologyConstants.NodeLabels.Query : null,
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (!IsMongoCall(node)) return null;
                    if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName))
                    {
                        var collectionName = TryExtractCollectionName(objNode);
                        if (!string.IsNullOrEmpty(collectionName))
                        {
                            return $"MongoDB: {collectionName}.{propName}";
                        }
                        if (objNode.IsValid() && objNode.Is(TreeSitterSyntax.TypeScript.Identifier))
                        {
                            return $"MongoDB: {objNode.Text}.{propName}";
                        }
                        return $"MongoDB: {propName}";
                    }
                    return "MongoDB Query";
                }
            }
        ];
    }

    private static bool IsMongoCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        if (AstHelper.TryGetMemberAccess(node, out _, out var propName))
        {
            return propName != null && MongoMethods.Contains(propName);
        }

        return false;
    }

    private static string? TryExtractCollectionName(Node? objNode)
    {
        if (!objNode.IsValid()) return null;

        if (objNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            if (AstHelper.TryGetMemberAccess(objNode, out _, out var innerProp) && innerProp == "collection")
            {
                return AstHelper.ExtractFirstStringArgument(objNode);
            }
        }

        return null;
    }
}
