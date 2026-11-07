using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class CouchDbPythonLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> CouchDbMethods = new(StringComparer.Ordinal)
    {
        "save", "get", "delete", "view", "query", "create"
    };

    public CouchDbPythonLibraryParser()
    {
        Type = SemanticExtensionType.DocumentDb;
        Name = "CouchDB";
        Id = "couchdb";
        SupportedPatterns = ["couchdb", "couchdb.*"];
        LibraryRole = LibraryRole.OrmOrDatabase;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Python.Call }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsCouchDbCall(node, out _, out _) ? OntologyConstants.NodeLabels.Query : null,
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (!IsCouchDbCall(node, out var method, out var target)) return null;
                    return !string.IsNullOrEmpty(target) ? $"CouchDB: {target}.{method}" : $"CouchDB: {method}";
                },
                CollectReferences = static (node, scopeSymbolId, references, ctx) =>
                {
                    if (IsCouchDbCall(node, out _, out var target))
                    {
                        if (!string.IsNullOrEmpty(target))
                        {
                            references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.UsesDb));
                        }
                        references.Add(new Reference(scopeSymbolId, CouchDbId, OntologyConstants.Relationships.UsesDb));
                    }
                }
            }
        ];
    }

    private const string CouchDbId = "couchdb";

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
