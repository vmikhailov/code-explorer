using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class MongoDbCsLibraryParser : ILibraryParser
{
    public string Type => "db:document";
    public string Name => "MongoDB";
    public string Id => "mongodb";
    public IReadOnlyList<string> SupportedPatterns => ["MongoDB.Driver", "MongoDB.Bson"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> MongoMethods =
    [
        "Find", "FindAsync", "InsertOne", "InsertOneAsync", "InsertMany", "InsertManyAsync",
        "UpdateOne", "UpdateOneAsync", "UpdateMany", "UpdateManyAsync",
        "DeleteOne", "DeleteOneAsync", "DeleteMany", "DeleteManyAsync",
        "ReplaceOne", "ReplaceOneAsync", "Aggregate", "AggregateAsync",
        "CountDocuments", "CountDocumentsAsync", "BulkWrite", "BulkWriteAsync"
    ];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsGetCollectionCall(node) || IsMongoCollectionProperty(node))
        {
            return OntologyConstants.NodeLabels.Table;
        }

        if (IsMongoQueryCall(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }

        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsGetCollectionCall(node))
        {
            var (entityType, collectionName) = ExtractCollectionInfo(node);
            return collectionName ?? entityType ?? "MongoCollection";
        }

        if (IsMongoCollectionProperty(node))
        {
            var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
            if (nameNode.IsValid()) return nameNode.Text;
        }

        if (IsMongoQueryCall(node))
        {
            var func = node.GetFunctionNode();
            var methodName = func.GetField(TreeSitterSyntax.Fields.Name)?.Text ?? "Query";
            var receiver = func.GetField(TreeSitterSyntax.Fields.Expression)?.Text ?? "Mongo";
            return $"MongoDB: {receiver}.{methodName}";
        }

        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsGetCollectionCall(node))
        {
            var (entityType, collectionName) = ExtractCollectionInfo(node);
            var target = collectionName ?? entityType;
            if (!string.IsNullOrEmpty(target))
            {
                references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.QueriesDb));
                if (!string.IsNullOrEmpty(entityType))
                {
                    references.Add(new Reference(entityType, target, OntologyConstants.Relationships.PersistedIn));
                    references.Add(new Reference(scopeSymbolId, entityType, OntologyConstants.Relationships.UsesType));
                }
            }
        }
        else if (IsMongoCollectionProperty(node))
        {
            var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
            var tableName = nameNode.IsValid() ? nameNode.Text : null;
            var entityType = ExtractEntityTypeFromTypeNode(node.GetField(TreeSitterSyntax.Fields.Type));
            if (!string.IsNullOrEmpty(tableName))
            {
                references.Add(new Reference(scopeSymbolId, tableName, OntologyConstants.Relationships.QueriesDb));
                if (!string.IsNullOrEmpty(entityType))
                {
                    references.Add(new Reference(entityType, tableName, OntologyConstants.Relationships.PersistedIn));
                    references.Add(new Reference(scopeSymbolId, entityType, OntologyConstants.Relationships.UsesType));
                }
            }
        }
        else if (IsMongoQueryCall(node))
        {
            var func = node.GetFunctionNode();
            var receiver = func.GetField(TreeSitterSyntax.Fields.Expression)?.Text;
            if (!string.IsNullOrEmpty(receiver))
            {
                references.Add(new Reference(scopeSymbolId, receiver, OntologyConstants.Relationships.QueriesDb));
            }
        }
    }

    private static bool IsGetCollectionCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression)) return false;

        var nameNode = func.GetField(TreeSitterSyntax.Fields.Name);
        if (!nameNode.IsValid()) return false;

        var text = nameNode.Is(TreeSitterSyntax.CSharp.GenericName)
            ? nameNode.FindChildOfType(TreeSitterSyntax.Common.Identifier)?.Text
            : nameNode.Text;

        return text == "GetCollection";
    }

    private static bool IsMongoCollectionProperty(Node node)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.PropertyDeclaration)) return false;
        var typeNode = node.GetField(TreeSitterSyntax.Fields.Type);
        return typeNode.IsValid() && (typeNode.Text.StartsWith("IMongoCollection<") || typeNode.Text.StartsWith("MongoCollection<"));
    }

    private static bool IsMongoQueryCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression)) return false;

        var nameNode = func.GetField(TreeSitterSyntax.Fields.Name);
        if (!nameNode.IsValid()) return false;

        var methodName = nameNode.Is(TreeSitterSyntax.CSharp.GenericName)
            ? nameNode.FindChildOfType(TreeSitterSyntax.Common.Identifier)?.Text
            : nameNode.Text;

        return methodName != null && MongoMethods.Contains(methodName);
    }

    private static (string? EntityType, string? CollectionName) ExtractCollectionInfo(Node invocationNode)
    {
        string? entityType = null;
        string? collectionName = null;

        var func = invocationNode.GetFunctionNode();
        if (func.IsValid() && func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            var nameNode = func.GetField(TreeSitterSyntax.Fields.Name);
            if (nameNode.IsValid() && nameNode.Is(TreeSitterSyntax.CSharp.GenericName))
            {
                var typeArgs = nameNode.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
                var firstType = typeArgs?.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier));
                if (firstType.IsValid()) entityType = firstType.Text;
            }
        }

        var argList = invocationNode.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
        if (argList.IsValid())
        {
            var firstArg = argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
            if (firstArg.IsValid())
            {
                var strChild = firstArg.Children.FirstOrDefault(c => c.Type.Contains("string"));
                if (strChild.IsValid())
                {
                    collectionName = strChild.Text.Trim('"');
                }
            }
        }

        return (entityType, collectionName);
    }

    private static string? ExtractEntityTypeFromTypeNode(Node? typeNode)
    {
        if (!typeNode.IsValid()) return null;
        var text = typeNode.Text;
        var start = text.IndexOf('<');
        var end = text.LastIndexOf('>');
        if (start >= 0 && end > start)
        {
            return text[(start + 1)..end].Trim();
        }
        return null;
    }
}
