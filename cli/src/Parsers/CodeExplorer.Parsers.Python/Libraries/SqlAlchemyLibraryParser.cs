using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.Python.Libraries;

public class SqlAlchemyLibraryParser : DeclarativeSemanticExtension
{
    private const string DefaultDatabaseTarget = "database";

    private static readonly HashSet<string> SqlAlchemyMethods = new(StringComparer.Ordinal)
    {
        "execute", "scalar", "scalars", "query", "select", "insert", "update", "delete", "Table"
    };

    public SqlAlchemyLibraryParser()
    {
        Type = SemanticExtensionType.RelationalDb;
        Name = "SQLAlchemy";
        Id = "sqlalchemy";
        SupportedPatterns = ["sqlalchemy", "sqlalchemy.*"];
        LibraryRole = LibraryRole.OrmOrDatabase;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Python.ClassDefinition, TreeSitterSyntax.Python.Call }.ToFrozenSet(),
                MapNodeType = static (node, ctx) =>
                {
                    if (IsDeclarativeModel(node, out _)) return OntologyConstants.NodeLabels.Table;
                    if (IsSqlAlchemyCall(node, out _, out _, out var isTable))
                    {
                        return isTable ? OntologyConstants.NodeLabels.Table : OntologyConstants.NodeLabels.Query;
                    }
                    return null;
                },
                ExtractIdentifier = ExtractSqlAlchemyIdentifier,
                CollectReferences = CollectSqlAlchemyReferences
            }
        ];
    }

    private static string? ExtractSqlAlchemyIdentifier(Node node, SemanticParseContext ctx, string? projectName, string? relativePath)
    {
        if (IsDeclarativeModel(node, out var modelTable))
        {
            return modelTable;
        }

        if (IsSqlAlchemyCall(node, out var method, out var target, out var isTable))
        {
            if (isTable)
            {
                return !string.IsNullOrEmpty(target) && ResourceNamingHelper.IsValidTableIdentifier(target)
                    ? target
                    : null;
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

    private static void CollectSqlAlchemyReferences(Node node, string scopeSymbolId, List<Reference> references, SemanticParseContext ctx)
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

        var rawFuncText = func.Text;
        var funcName = rawFuncText;
        string? receiverPrefix = null;
        if (funcName.Contains('.'))
        {
            var lastDot = funcName.LastIndexOf('.');
            receiverPrefix = funcName[..lastDot].Trim();
            funcName = funcName[(lastDot + 1)..];
        }

        if (SqlAlchemyMethods.Contains(funcName))
        {
            var args = PythonAstHelper.GetCallArguments(node);
            if (funcName == "Table")
            {
                if (!IsSqlAlchemyTableConstructor(node, receiverPrefix, args))
                {
                    return false;
                }
                var firstTableArg = args[0];
                string? resolvedTbl = null;
                if (firstTableArg.Is(TreeSitterSyntax.Python.String))
                {
                    resolvedTbl = PythonAstHelper.ResolveStringOrVariable(firstTableArg);
                }
                else
                {
                    var varKey = firstTableArg.Text.Trim();
                    if (!string.IsNullOrEmpty(varKey) && ConstantRegistry.TryResolve(null, varKey, out var constTbl))
                    {
                        resolvedTbl = constTbl;
                    }
                }
                resolvedTbl = resolvedTbl?.Trim();
                if (string.IsNullOrWhiteSpace(resolvedTbl) || !ResourceNamingHelper.IsValidTableIdentifier(resolvedTbl))
                {
                    return false;
                }
                methodName = funcName;
                isTable = true;
                targetInfo = resolvedTbl;
                return true;
            }

            methodName = funcName;

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

    private static bool IsSqlAlchemyTableConstructor(Node callNode, string? receiverPrefix, IReadOnlyList<Node> args)
    {
        if (args.Count == 0) return false;

        if (!string.IsNullOrEmpty(receiverPrefix))
        {
            var lowerRecv = receiverPrefix.ToLowerInvariant();
            if (lowerRecv is "sa" or "sqlalchemy" or "db" or "sql" or "sqlalchemy.schema")
            {
                return true;
            }
            if (lowerRecv.Contains("rich") || lowerRecv.Contains("pretty") || lowerRecv.Contains("console") || lowerRecv.Contains("ui"))
            {
                return false;
            }
        }

        var docText = callNode.Document?.SourceText;
        if (!string.IsNullOrEmpty(docText))
        {
            var hasSqlAlchemy = docText.Contains("sqlalchemy", StringComparison.OrdinalIgnoreCase) ||
                                docText.Contains("sqlmodel", StringComparison.OrdinalIgnoreCase) ||
                                docText.Contains("alembic", StringComparison.OrdinalIgnoreCase);
            if (!hasSqlAlchemy)
            {
                return false;
            }
            if ((docText.Contains("from rich", StringComparison.Ordinal) ||
                 docText.Contains("import rich", StringComparison.Ordinal) ||
                 docText.Contains("prettytable", StringComparison.OrdinalIgnoreCase)) &&
                args.Count < 2)
            {
                return false;
            }
            return true;
        }

        return args.Count >= 2;
    }
}
