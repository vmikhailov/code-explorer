using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class PyMongoLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> MongoMethods = new(StringComparer.Ordinal)
    {
        "find", "find_one", "insert_one", "insert_many", "update_one", "update_many",
        "delete_one", "delete_many", "aggregate", "count_documents", "replace_one", "distinct"
    };

    public PyMongoLibraryParser()
    {
        Type = SemanticExtensionType.DocumentDb;
        Name = "MongoDB";
        Id = "mongodb";
        SupportedPatterns = ["pymongo", "pymongo.*", "motor", "motor.*"];
        LibraryRole = LibraryRole.OrmOrDatabase;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Python.Call }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsMongoCall(node, out _, out _) ? OntologyConstants.NodeLabels.Query : null,
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (!IsMongoCall(node, out var method, out var collection)) return null;
                    return !string.IsNullOrEmpty(collection) ? $"MongoDB: {collection}.{method}" : $"MongoDB: {method}";
                },
                CollectReferences = static (node, scopeSymbolId, references, ctx) =>
                {
                    if (IsMongoCall(node, out _, out var collection))
                    {
                        if (!string.IsNullOrEmpty(collection))
                        {
                            references.Add(new Reference(scopeSymbolId, collection, OntologyConstants.Relationships.UsesDb));
                        }
                        references.Add(new Reference(scopeSymbolId, MongoDbId, OntologyConstants.Relationships.UsesDb));
                    }
                }
            }
        ];
    }

    private const string MongoDbId = "mongodb";

    private static bool IsMongoCall(Node node, out string? methodName, out string? collection)
    {
        methodName = null;
        collection = null;

        if (PythonAstHelper.TryGetMemberAccess(node, out var objNode, out methodName))
        {
            if (methodName != null && MongoMethods.Contains(methodName))
            {
                collection = ExtractCollectionName(objNode);
                return true;
            }
        }

        return false;
    }

    private static string? ExtractCollectionName(Node? objNode)
    {
        if (!objNode.IsValid()) return null;
        if (objNode.Is(TreeSitterSyntax.Python.Attribute))
        {
            return objNode.GetChildFieldText(TreeSitterSyntax.Fields.Property) ??
                   objNode.Children.LastOrDefault(c => c.Is(TreeSitterSyntax.Python.Identifier))?.Text;
        }
        if (objNode.Is(TreeSitterSyntax.Python.Subscript))
        {
            var sub = objNode.GetField(TreeSitterSyntax.Fields.Subscript) ?? (objNode.Children.Count >= 2 ? objNode.Children[^1] : null);
            if (sub.IsValid())
            {
                var resolved = PythonAstHelper.ResolveStringOrVariable(sub);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
                return sub.Text.Trim('\'', '"');
            }
        }
        if (objNode.Is(TreeSitterSyntax.Python.Identifier))
        {
            var text = objNode.Text;
            if (text is not "db" and not "client" and not "self")
            {
                return text;
            }
        }
        return null;
    }
}
