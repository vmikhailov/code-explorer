using System.Text.RegularExpressions;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class SqlModelLibraryParser : ISemanticExtension
{
    public SemanticExtensionType Type => SemanticExtensionType.RelationalDb;
    public string Name => "SQLModel";
    public string Id => "sqlmodel";
    private const string DefaultDatabaseTarget = "database";
    public IReadOnlyList<string> SupportedPatterns => ["sqlmodel", "sqlmodel.*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    private static readonly HashSet<string> SqlModelQueryMethods = new(StringComparer.Ordinal)
    {
        "select", "exec", "execute", "add", "commit", "refresh", "delete", "get"
    };

    private static readonly Regex ForeignKeyRegex = new(@"foreign_key\s*=\s*[""']([A-Za-z0-9_]+)\.[A-Za-z0-9_]+[""']", RegexOptions.Compiled);

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsSqlModelTableClass(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.Table;
        }

        if (IsSqlModelCall(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }

        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsSqlModelTableClass(node, out _, out var tableName))
        {
            return tableName;
        }

        if (IsSqlModelCall(node, out var queryText, out _))
        {
            return queryText;
        }

        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsSqlModelTableClass(node, out var className, out var tableName))
        {
            references.Add(new Reference(scopeSymbolId, tableName, OntologyConstants.Relationships.PersistedIn));
            references.Add(new Reference(scopeSymbolId, DefaultDatabaseTarget, OntologyConstants.Relationships.UsesDb));

            // Extract foreign keys to link table dependencies
            var classText = node.Text;
            if (!string.IsNullOrEmpty(classText))
            {
                var matches = ForeignKeyRegex.Matches(classText);
                foreach (Match m in matches)
                {
                    var foreignTable = m.Groups[1].Value.ToLowerInvariant();
                    if (!string.IsNullOrEmpty(foreignTable) && !foreignTable.Equals(tableName, StringComparison.OrdinalIgnoreCase))
                    {
                        references.Add(new Reference(scopeSymbolId, foreignTable, OntologyConstants.Relationships.DependsOn));
                    }
                }
            }
            return;
        }

        if (IsSqlModelCall(node, out _, out var targetTable))
        {
            if (!string.IsNullOrEmpty(targetTable))
            {
                references.Add(new Reference(scopeSymbolId, targetTable, OntologyConstants.Relationships.ReadsFrom));
            }
            references.Add(new Reference(scopeSymbolId, DefaultDatabaseTarget, OntologyConstants.Relationships.UsesDb));
        }
    }

    public static bool IsSqlModelTableClass(Node node, out string className, out string tableName)
    {
        className = string.Empty;
        tableName = string.Empty;

        if (!node.Is(TreeSitterSyntax.Python.ClassDefinition)) return false;

        var classArgs = node.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
        var isTable = false;

        if (classArgs.IsValid())
        {
            foreach (var child in classArgs.Children)
            {
                if (child.Is(TreeSitterSyntax.Python.KeywordArgument))
                {
                    var kwName = child.Children.FirstOrDefault()?.Text;
                    if (kwName == "table")
                    {
                        var valNode = child.Children.Skip(2).FirstOrDefault() ?? child.Children.LastOrDefault();
                        if (valNode.IsValid() && valNode.Text.Equals("True", StringComparison.OrdinalIgnoreCase))
                        {
                            isTable = true;
                            break;
                        }
                    }
                }
            }
        }

        if (!isTable)
        {
            // Also check for explicit __tablename__ in class body
            if (HasTableNameAssignment(node, out var explicitTable))
            {
                var nameField = node.GetChildFieldText(TreeSitterSyntax.Fields.Name);
                className = nameField ?? node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.Identifier))?.Text ?? string.Empty;
                tableName = explicitTable;
                return !string.IsNullOrEmpty(className) && !string.IsNullOrEmpty(tableName);
            }
            return false;
        }

        var nameNode = node.GetChildFieldText(TreeSitterSyntax.Fields.Name);
        className = nameNode ?? node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.Identifier))?.Text ?? string.Empty;
        if (string.IsNullOrEmpty(className)) return false;

        if (HasTableNameAssignment(node, out var customTable))
        {
            tableName = customTable;
        }
        else
        {
            tableName = className.ToLowerInvariant();
        }

        return true;
    }

    private static bool HasTableNameAssignment(Node classNode, out string explicitTableName)
    {
        explicitTableName = string.Empty;
        var body = classNode.FindChildOfType(TreeSitterSyntax.Python.Block);
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
                        explicitTableName = right.Text.Trim('\'', '"');
                        return !string.IsNullOrEmpty(explicitTableName);
                    }
                }
            }
        }

        return false;
    }

    private static bool IsSqlModelCall(Node node, out string? queryText, out string? targetTable)
    {
        queryText = null;
        targetTable = null;

        if (!node.Is(TreeSitterSyntax.Python.Call)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        var funcName = func.Text;
        if (funcName.Contains('.'))
        {
            funcName = funcName[(funcName.LastIndexOf('.') + 1)..];
        }

        if (SqlModelQueryMethods.Contains(funcName))
        {
            var args = PythonAstHelper.GetCallArguments(node);
            if (funcName == "select" && args.Count > 0)
            {
                var modelArg = args[0].Text?.Trim();
                if (!string.IsNullOrEmpty(modelArg) && !modelArg.Contains('('))
                {
                    targetTable = modelArg.ToLowerInvariant();
                    queryText = $"SELECT: {modelArg}";
                    return true;
                }
            }
            else if (funcName is "add" or "delete" or "refresh" && args.Count > 0)
            {
                var entityArg = args[0].Text?.Trim();
                queryText = $"SQLModel: {funcName}";
                return true;
            }
            else if (funcName is "exec" or "execute")
            {
                queryText = "SQLModel: exec";
                return true;
            }
        }

        return false;
    }
}
