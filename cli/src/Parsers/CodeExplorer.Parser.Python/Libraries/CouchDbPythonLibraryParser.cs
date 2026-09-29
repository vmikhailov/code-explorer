using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class CouchDbPythonLibraryParser : ISemanticExtension
{
    public string Type => OntologyConstants.LibraryTypes.DocumentDb;
    public string Name => "CouchDB";
    public string Id => "couchdb";
    public IReadOnlyList<string> SupportedPatterns => ["couchdb", "couchdb.*"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> CouchDbMethods = new(StringComparer.Ordinal)
    {
        "save", "get", "delete", "view", "query", "create"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsCouchDbCall(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsCouchDbCall(node, out var method, out var target))
        {
            return !string.IsNullOrEmpty(target) ? $"CouchDB: {target}.{method}" : $"CouchDB: {method}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsCouchDbCall(node, out _, out var target))
        {
            if (!string.IsNullOrEmpty(target))
            {
                references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.UsesDb));
            }
            references.Add(new Reference(scopeSymbolId, "couchdb", OntologyConstants.Relationships.UsesDb));
        }
    }

    private static bool IsCouchDbCall(Node node, out string? methodName, out string? targetInfo)
    {
        methodName = null;
        targetInfo = null;

        if (PythonAstHelper.TryGetMemberAccess(node, out var objNode, out methodName))
        {
            if (methodName != null && CouchDbMethods.Contains(methodName))
            {
                if (methodName == "create")
                {
                    targetInfo = PythonAstHelper.ResolveCallArgument(node, 0);
                }
                else
                {
                    var callerText = objNode?.Text;
                    if (callerText is not null and not "server" and not "self")
                    {
                        targetInfo = callerText;
                    }
                }
                return true;
            }
        }

        return false;
    }
}
