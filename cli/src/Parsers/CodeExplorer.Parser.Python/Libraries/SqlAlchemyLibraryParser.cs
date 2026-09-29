using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class SqlAlchemyLibraryParser : ISemanticExtension
{
    public string Type => "db:relational";
    public string Name => "SQLAlchemy";
    public string Id => "sqlalchemy";
    public IReadOnlyList<string> SupportedPatterns => ["sqlalchemy", "sqlalchemy.*"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> SqlAlchemyMethods = new(StringComparer.Ordinal)
    {
        "execute", "scalar", "scalars", "query", "select", "insert", "update", "delete", "Table"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsSqlAlchemyCall(node, out _, out _, out var isTable))
        {
            return isTable ? OntologyConstants.NodeLabels.Table : OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsSqlAlchemyCall(node, out var method, out var target, out var isTable))
        {
            if (isTable && !string.IsNullOrEmpty(target))
            {
                return target;
            }

            if (!string.IsNullOrEmpty(target))
            {
                if (NestedSqlParser.TryParseSql(target, out var firstWord, out _))
                {
                    var clean = NestedSqlParser.CleanQueryText(target);
                    return $"{firstWord} Query: {clean}";
                }
                return $"SQLAlchemy: {target}";
            }

            return $"SQLAlchemy: {method}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsSqlAlchemyCall(node, out _, out var target, out var isTable))
        {
            if (isTable && !string.IsNullOrEmpty(target))
            {
                references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.PersistedIn));
            }
            else if (!string.IsNullOrEmpty(target))
            {
                if (NestedSqlParser.TryParseSql(target, out _, out _))
                {
                    NestedSqlParser.TryDetectSqlDependencies(target, scopeSymbolId, references);
                }
                else
                {
                    references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.UsesDb));
                }
            }
            references.Add(new Reference(scopeSymbolId, "database", OntologyConstants.Relationships.UsesDb));
        }
    }

    private static bool IsSqlAlchemyCall(Node node, out string? methodName, out string? targetInfo, out bool isTable)
    {
        methodName = null;
        targetInfo = null;
        isTable = false;

        if (!node.Is(TreeSitterSyntax.Python.Call)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        var funcName = func.Text;
        if (funcName.Contains('.'))
        {
            funcName = funcName[(funcName.LastIndexOf('.') + 1)..];
        }

        if (SqlAlchemyMethods.Contains(funcName))
        {
            methodName = funcName;
            var args = PythonAstHelper.GetCallArguments(node);
            if (funcName == "Table" && args.Count > 0)
            {
                isTable = true;
                targetInfo = PythonAstHelper.ResolveStringOrVariable(args[0]);
                return true;
            }

            if (args.Count > 0)
            {
                var firstArg = args[0];
                // text("SELECT...")
                if (firstArg.Is(TreeSitterSyntax.Python.Call))
                {
                    var innerArgs = PythonAstHelper.GetCallArguments(firstArg);
                    if (innerArgs.Count > 0)
                    {
                        targetInfo = PythonAstHelper.ResolveStringOrVariable(innerArgs[0]);
                    }
                }
                else
                {
                    targetInfo = PythonAstHelper.ResolveStringOrVariable(firstArg) ?? firstArg.Text.Trim();
                }
            }
            return true;
        }

        return false;
    }
}
