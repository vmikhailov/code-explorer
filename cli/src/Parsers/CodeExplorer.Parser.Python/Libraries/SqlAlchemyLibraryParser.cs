using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class SqlAlchemyLibraryParser : ISemanticExtension
{
    public SemanticExtensionType Type => SemanticExtensionType.RelationalDb;
    public string Name => "SQLAlchemy";
    public string Id => "sqlalchemy";
    private const string DefaultDatabaseTarget = "database";
    public IReadOnlyList<string> SupportedPatterns => ["sqlalchemy", "sqlalchemy.*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    private static readonly HashSet<string> SqlAlchemyMethods = new(StringComparer.Ordinal)
    {
        "execute", "scalar", "scalars", "query", "select", "insert", "update", "delete", "Table"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsDeclarativeModel(node, out _))
        {
            return OntologyConstants.NodeLabels.Table;
        }

        if (IsSqlAlchemyCall(node, out _, out _, out var isTable))
        {
            return isTable ? OntologyConstants.NodeLabels.Table : OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsDeclarativeModel(node, out var modelTable))
        {
            return modelTable;
        }

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
        if (IsDeclarativeModel(node, out var modelTable))
        {
            references.Add(new Reference(scopeSymbolId, modelTable, OntologyConstants.Relationships.PersistedIn));
            references.Add(new Reference(scopeSymbolId, DefaultDatabaseTarget, OntologyConstants.Relationships.UsesDb));
            return;
        }

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
            references.Add(new Reference(scopeSymbolId, DefaultDatabaseTarget, OntologyConstants.Relationships.UsesDb));
        }
    }

    private static bool IsDeclarativeModel(Node node, out string tableName)
    {
        tableName = string.Empty;
        if (!node.Is(TreeSitterSyntax.Python.ClassDefinition)) return false;

        var body = node.FindChildOfType(TreeSitterSyntax.Python.Block);
        if (!body.IsValid()) return false;

        foreach (var stmt in body.Children)
        {
            var assignNode = stmt.Is(TreeSitterSyntax.Python.Assignment)
                ? stmt
                : stmt.FindChildOfType(TreeSitterSyntax.Python.Assignment);

            if (assignNode.IsValid())
            {
                var left = assignNode.GetChildForField(TreeSitterSyntax.Fields.Left)
                    ?? assignNode.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.Identifier));

                if (left.IsValid() && left.Text == "__tablename__")
                {
                    var right = assignNode.GetChildForField(TreeSitterSyntax.Fields.Right)
                        ?? assignNode.Children.LastOrDefault();

                    if (right.IsValid() && right.Is(TreeSitterSyntax.Python.String))
                    {
                        tableName = right.Text.Trim('\'', '"');
                        return !string.IsNullOrEmpty(tableName);
                    }
                }
            }
        }

        return false;
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
                        targetInfo = PythonAstHelper.ResolveSqlStringOrVariable(innerArgs[0]);
                    }
                }
                else
                {
                    targetInfo = PythonAstHelper.ResolveSqlStringOrVariable(firstArg) ?? firstArg.Text.Trim();
                }
            }
            return true;
        }

        return false;
    }
}
