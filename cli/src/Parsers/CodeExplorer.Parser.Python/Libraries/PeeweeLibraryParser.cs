using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class PeeweeLibraryParser : ISemanticExtension
{
    public string Type => OntologyConstants.LibraryTypes.RelationalDb;
    public string Name => "Peewee";
    public string Id => "peewee";
    public IReadOnlyList<string> SupportedPatterns => ["peewee", "peewee.*"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> PeeweeQueryMethods = new(StringComparer.Ordinal)
    {
        "select", "create", "get", "get_or_none", "delete", "update", "insert", "insert_many", "raw", "execute_sql"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsPeeweeModelClass(node, out _))
        {
            return OntologyConstants.NodeLabels.Table;
        }

        if (IsPeeweeCall(node, out _, out _, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }

        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsPeeweeModelClass(node, out var className))
        {
            return className;
        }

        if (IsPeeweeCall(node, out var method, out var targetInfo, out var isSql))
        {
            if (isSql && !string.IsNullOrEmpty(targetInfo))
            {
                if (NestedSqlParser.TryParseSql(targetInfo, out var firstWord, out _))
                {
                    var clean = NestedSqlParser.CleanQueryText(targetInfo);
                    return $"{firstWord} Query: {clean}";
                }
                return $"Peewee: {targetInfo}";
            }

            if (!string.IsNullOrEmpty(targetInfo))
            {
                return $"Peewee: {targetInfo}.{method}";
            }

            return $"Peewee: {method}";
        }

        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsPeeweeModelClass(node, out var className))
        {
            references.Add(new Reference(scopeSymbolId, className, OntologyConstants.Relationships.PersistedIn));
            references.Add(new Reference(scopeSymbolId, "database", OntologyConstants.Relationships.UsesDb));
            return;
        }

        if (IsPeeweeCall(node, out _, out var targetInfo, out var isSql))
        {
            if (isSql && !string.IsNullOrEmpty(targetInfo))
            {
                NestedSqlParser.TryDetectSqlDependencies(targetInfo, scopeSymbolId, references);
            }
            else if (!string.IsNullOrEmpty(targetInfo))
            {
                references.Add(new Reference(scopeSymbolId, targetInfo, OntologyConstants.Relationships.UsesDb));
            }

            references.Add(new Reference(scopeSymbolId, "database", OntologyConstants.Relationships.UsesDb));
        }
    }

    private static bool IsPeeweeModelClass(Node node, out string className)
    {
        className = string.Empty;
        if (!node.Is(TreeSitterSyntax.Python.ClassDefinition)) return false;

        var superclasses = node.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
        if (superclasses.IsValid())
        {
            foreach (var sc in superclasses.Children)
            {
                var text = sc.Text;
                if (text is "Model" or "peewee.Model" or "BaseModel")
                {
                    var nameNode = node.GetChildFieldText(TreeSitterSyntax.Fields.Name);
                    className = nameNode ?? node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.Identifier) || c.Type == "identifier")?.Text ?? string.Empty;
                    return !string.IsNullOrEmpty(className);
                }
            }
        }

        return false;
    }

    private static bool IsPeeweeCall(Node node, out string? methodName, out string? targetInfo, out bool isSql)
    {
        methodName = null;
        targetInfo = null;
        isSql = false;

        if (PythonAstHelper.TryGetMemberAccess(node, out var objNode, out methodName))
        {
            if (methodName != null && PeeweeQueryMethods.Contains(methodName))
            {
                if (methodName is "execute_sql" or "raw")
                {
                    isSql = true;
                    var args = PythonAstHelper.GetCallArguments(node);
                    if (args.Count > 0)
                    {
                        targetInfo = PythonAstHelper.ResolveStringOrVariable(args[0]);
                    }
                }
                else
                {
                    targetInfo = objNode?.Text;
                }
                return true;
            }
        }

        return false;
    }
}
