using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class MongoGoLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> MongoMethods = new(StringComparer.Ordinal)
    {
        "Find", "FindOne", "InsertOne", "InsertMany", "UpdateOne", "UpdateMany",
        "DeleteOne", "DeleteMany", "Aggregate", "CountDocuments", "ReplaceOne"
    };

    public MongoGoLibraryParser()
    {
        Type = SemanticExtensionType.DocumentDb;
        Name = "MongoDB";
        Id = "mongodb";
        SupportedPatterns = ["go.mongodb.org/mongo-driver/mongo*", "go.mongodb.org/mongo-driver*"];
        LibraryRole = LibraryRole.OrmOrDatabase;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Go.CallExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsMongoCall(node, out _, out _) ? OntologyConstants.NodeLabels.Query : null,
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (!IsMongoCall(node, out var method, out var target)) return null;
                    return !string.IsNullOrEmpty(target) ? $"MongoDB: {target}.{method}" : $"MongoDB: {method}";
                },
                CollectReferences = static (node, scopeSymbolId, references, ctx) =>
                {
                    if (IsMongoCall(node, out _, out var target))
                    {
                        if (!string.IsNullOrEmpty(target))
                        {
                            references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.UsesDb));
                        }
                        references.Add(new Reference(scopeSymbolId, MongoDbId, OntologyConstants.Relationships.UsesDb));
                    }
                }
            }
        ];
    }

    private const string MongoDbId = "mongodb";

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
