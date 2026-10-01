using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class PyMongoLibraryParser : ISemanticExtension
{
    public string Type => OntologyConstants.LibraryTypes.DocumentDb;
    public string Name => "MongoDB";
    public string Id => "mongodb";
    public IReadOnlyList<string> SupportedPatterns => ["pymongo", "pymongo.*", "motor", "motor.*"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> MongoMethods = new(StringComparer.Ordinal)
    {
        "find", "find_one", "insert_one", "insert_many", "update_one", "update_many",
        "delete_one", "delete_many", "aggregate", "count_documents", "replace_one", "distinct"
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
        if (IsMongoCall(node, out var method, out var collection))
        {
            return !string.IsNullOrEmpty(collection) ? $"MongoDB: {collection}.{method}" : $"MongoDB: {method}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsMongoCall(node, out _, out var collection))
        {
            if (!string.IsNullOrEmpty(collection))
            {
                references.Add(new Reference(scopeSymbolId, collection, OntologyConstants.Relationships.UsesDb));
            }
            references.Add(new Reference(scopeSymbolId, "mongodb", OntologyConstants.Relationships.UsesDb));
        }
    }

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
            var sub = objNode.GetField(TreeSitterSyntax.Fields.Subscript) ?? (objNode.Children.Count >= 3 ? objNode.Children[2] : null);
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
