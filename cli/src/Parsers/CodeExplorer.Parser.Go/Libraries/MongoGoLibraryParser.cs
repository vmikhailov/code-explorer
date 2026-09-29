using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public class MongoGoLibraryParser : ISemanticExtension
{
    public string Type => "db:document";
    public string Name => "MongoDB";
    public string Id => "mongodb";
    public IReadOnlyList<string> SupportedPatterns => ["go.mongodb.org/mongo-driver/mongo", "go.mongodb.org/mongo-driver"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> MongoMethods = new(StringComparer.Ordinal)
    {
        "Find", "FindOne", "InsertOne", "InsertMany", "UpdateOne", "UpdateMany",
        "DeleteOne", "DeleteMany", "Aggregate", "CountDocuments", "ReplaceOne"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsMongoCall(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsMongoCall(node, out var method, out var target))
        {
            return !string.IsNullOrEmpty(target) ? $"MongoDB: {target}.{method}" : $"MongoDB: {method}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsMongoCall(node, out _, out var target))
        {
            if (!string.IsNullOrEmpty(target))
            {
                references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.UsesDb));
            }
            references.Add(new Reference(scopeSymbolId, "mongodb", OntologyConstants.Relationships.UsesDb));
        }
    }

    private static bool IsMongoCall(Node node, out string? methodName, out string? targetInfo)
    {
        methodName = null;
        targetInfo = null;

        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        var funcText = func.Text;
        foreach (var m in MongoMethods)
        {
            if (funcText.EndsWith("." + m, StringComparison.Ordinal) || funcText == m)
            {
                methodName = m;
                if (func.Is(TreeSitterSyntax.Go.SelectorExpression))
                {
                    var operand = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
                    if (operand.IsValid())
                    {
                        targetInfo = operand.Text;
                    }
                }
                return true;
            }
        }

        return false;
    }
}
