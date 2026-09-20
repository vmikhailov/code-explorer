using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class MongodbLibraryParser : ILibraryParser
{
    public string Type => "db:document";
    public string Name => "MongoDB";
    public string Id => "mongodb";
    public IReadOnlyList<string> SupportedPatterns => ["mongodb"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> MongoMethods = new(StringComparer.Ordinal)
    {
        "find", "findOne", "insertOne", "insertMany", "updateOne", "updateMany",
        "deleteOne", "deleteMany", "aggregate", "countDocuments", "estimatedDocumentCount",
        "distinct", "bulkWrite", "createIndex", "drop", "dropDatabase", "replaceOne", "command"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsMongoCall(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsMongoCall(node))
        {
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
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // Document queries represent DB Query nodes
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
