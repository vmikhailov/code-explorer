using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class ClickHouseLibraryParser : ISemanticExtension
{
    public string Type => OntologyConstants.LibraryTypes.AnalyticsDb;
    public string Name => "ClickHouse";
    public string Id => "clickhouse";
    public IReadOnlyList<string> SupportedPatterns => ["@clickhouse/client", "@clickhouse/client-web", "clickhouse"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    private static readonly HashSet<string> ClickHouseMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "query", "command", "exec", "insert"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsClickHouseCall(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsClickHouseCall(node))
        {
            var sqlText = ExtractSql(node);
            if (!string.IsNullOrEmpty(sqlText))
            {
                var clean = NestedSqlParser.CleanQueryText(sqlText);
                if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                {
                    return $"{firstWord} Query: {clean}";
                }
                return $"ClickHouse Query: {clean}";
            }

            var insertTable = ExtractInsertTable(node);
            if (!string.IsNullOrEmpty(insertTable))
            {
                return $"INSERT Query: INSERT INTO {insertTable}";
            }

            return "ClickHouse Query";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsClickHouseCall(node))
        {
            var sqlText = ExtractSql(node);
            if (!string.IsNullOrEmpty(sqlText))
            {
                NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);

                // Additional fallback for DDL / maintenance statements: ALTER TABLE / OPTIMIZE TABLE / TRUNCATE TABLE
                var alterMatch = Regex.Match(sqlText, @"\b(?:ALTER|OPTIMIZE|TRUNCATE)\s+TABLE\s+([a-zA-Z0-9_\.\[\]""#@'`\$\{\}\*]+)", RegexOptions.IgnoreCase);
                if (alterMatch.Success)
                {
                    var rawTable = alterMatch.Groups[1].Value;
                    var parts = rawTable.Split('.').Select(NestedSqlParser.CleanSqlIdentifier).Where(p => !string.IsNullOrEmpty(p)).ToArray();
                    var tbl = parts.Length > 0 ? parts[^1] : null;
                    if (!string.IsNullOrEmpty(tbl) && !NestedSqlParser.IsSqlKeyword(tbl))
                    {
                        references.Add(new Reference(scopeSymbolId, tbl, OntologyConstants.Relationships.DependsOn));
                    }
                }
            }
            else
            {
                var insertTable = ExtractInsertTable(node);
                if (!string.IsNullOrEmpty(insertTable))
                {
                    references.Add(new Reference(scopeSymbolId, insertTable, OntologyConstants.Relationships.DependsOn));
                    references.Add(new Reference(scopeSymbolId, insertTable, OntologyConstants.Relationships.UsesDb));
                }
            }
        }
    }

    private static bool IsClickHouseCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName))
        {
            if (propName != null && ClickHouseMethods.Contains(propName))
            {
                return ExtractSql(node) != null || ExtractInsertTable(node) != null || IsClickHouseReceiver(objNode);
            }
        }

        return false;
    }

    private static bool IsClickHouseReceiver(Node? objNode)
    {
        if (!objNode.IsValid()) return false;
        var name = objNode.Text;
        return name is "client" or "ch" or "clickhouse" or "db";
    }

    private static string? ExtractSql(Node node)
    {
        // 1. Direct string argument: client.query('SELECT ...')
        var directString = ExtractRawSql(node);
        if (!string.IsNullOrEmpty(directString))
        {
            return directString;
        }

        // 2. Options object argument: client.query({ query: 'SELECT ...' })
        var args = AstHelper.GetCallArguments(node);
        if (args.Count > 0)
        {
            var firstArg = args[0];
            if (firstArg.Is(TreeSitterSyntax.TypeScript.Object))
            {
                if (AstHelper.TryGetObjectProperty(firstArg, "query", out var queryValNode) && queryValNode.IsValid())
                {
                    return ExtractStringFromNode(queryValNode);
                }
            }
            else if (firstArg.Is(TreeSitterSyntax.TypeScript.Identifier))
            {
                // Options variable passed: const opts = { query: '...' }; client.query(opts);
                var resolved = ResolveQueryPropertyFromVariable(firstArg);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
            }
        }

        return null;
    }

    private static string? ExtractInsertTable(Node node)
    {
        var args = AstHelper.GetCallArguments(node);
        if (args.Count > 0)
        {
            var firstArg = args[0];
            if (firstArg.Is(TreeSitterSyntax.TypeScript.Object))
            {
                if (AstHelper.TryGetObjectProperty(firstArg, "table", out var tableValNode) && tableValNode.IsValid())
                {
                    var table = ExtractStringFromNode(tableValNode);
                    if (!string.IsNullOrEmpty(table))
                    {
                        var parts = table.Split('.').Select(NestedSqlParser.CleanSqlIdentifier).Where(p => !string.IsNullOrEmpty(p)).ToArray();
                        return parts.Length > 0 ? parts[^1] : table;
                    }
                }
            }
            else if (AstHelper.IsStringLiteralNode(firstArg))
            {
                var table = firstArg.Text.Trim('\'', '"', '`');
                var parts = table.Split('.').Select(NestedSqlParser.CleanSqlIdentifier).Where(p => !string.IsNullOrEmpty(p)).ToArray();
                return parts.Length > 0 ? parts[^1] : table;
            }
        }

        return null;
    }

    private static string? ExtractRawSql(Node node)
    {
        var args = AstHelper.GetCallArguments(node);
        if (args.Count == 0) return null;

        var firstArg = args[0];
        return ExtractStringFromNode(firstArg);
    }

    private static string? ExtractStringFromNode(Node node)
    {
        if (!node.IsValid()) return null;

        if (AstHelper.IsStringLiteralNode(node))
        {
            return node.Text.Trim('\'', '"', '`');
        }

        if (node.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            var varName = node.Text;
            var curr = node.Parent;
            var steps = 0;
            while (curr.IsValid() && ++steps <= 50)
            {
                if (curr.IsAny(TreeSitterSyntax.TypeScript.StatementBlock, TreeSitterSyntax.TypeScript.Program))
                {
                    foreach (var child in curr.Children)
                    {
                        var targetNode = child.Is("export_statement")
                            ? (child.GetChildForField("declaration") ?? child.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.TypeScript.LexicalDeclaration, TreeSitterSyntax.TypeScript.VariableDeclaration)))
                            : child;

                        if (targetNode.IsValid() && targetNode.IsAny(TreeSitterSyntax.TypeScript.LexicalDeclaration, TreeSitterSyntax.TypeScript.VariableDeclaration))
                        {
                            foreach (var decl in targetNode.Children.Where(c => c.Is(TreeSitterSyntax.TypeScript.VariableDeclarator)))
                            {
                                var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name);
                                if (nameNode.IsValid() && nameNode.Text == varName)
                                {
                                    var valNode = decl.GetField(TreeSitterSyntax.Fields.Value) ?? (decl.Children.Count >= 3 ? decl.Children[2] : null);
                                    if (valNode.IsValid() && AstHelper.IsStringLiteralNode(valNode))
                                    {
                                        return valNode.Text.Trim('\'', '"', '`');
                                    }
                                }
                            }
                        }
                    }
                }
                curr = curr.Parent;
            }
        }

        return null;
    }

    private static string? ResolveQueryPropertyFromVariable(Node identNode)
    {
        var varName = identNode.Text;
        var curr = identNode.Parent;
        var steps = 0;
        while (curr.IsValid() && ++steps <= 50)
        {
            if (curr.IsAny(TreeSitterSyntax.TypeScript.StatementBlock, TreeSitterSyntax.TypeScript.Program))
            {
                foreach (var child in curr.Children)
                {
                    var targetNode = child.Is("export_statement")
                        ? (child.GetChildForField("declaration") ?? child.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.TypeScript.LexicalDeclaration, TreeSitterSyntax.TypeScript.VariableDeclaration)))
                        : child;

                    if (targetNode.IsValid() && targetNode.IsAny(TreeSitterSyntax.TypeScript.LexicalDeclaration, TreeSitterSyntax.TypeScript.VariableDeclaration))
                    {
                        foreach (var decl in targetNode.Children.Where(c => c.Is(TreeSitterSyntax.TypeScript.VariableDeclarator)))
                        {
                            var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name);
                            if (nameNode.IsValid() && nameNode.Text == varName)
                            {
                                var valNode = decl.GetField(TreeSitterSyntax.Fields.Value) ?? (decl.Children.Count >= 3 ? decl.Children[2] : null);
                                if (valNode.IsValid() && valNode.Is(TreeSitterSyntax.TypeScript.Object))
                                {
                                    if (AstHelper.TryGetObjectProperty(valNode, "query", out var queryValNode) && queryValNode.IsValid())
                                    {
                                        return ExtractStringFromNode(queryValNode);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            curr = curr.Parent;
        }

        return null;
    }
}
