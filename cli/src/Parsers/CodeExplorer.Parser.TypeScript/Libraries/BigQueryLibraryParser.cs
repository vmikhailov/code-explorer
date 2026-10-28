using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class BigQueryLibraryParser : ISemanticExtension
{
    public string Type => OntologyConstants.LibraryTypes.AnalyticsDb;
    public string Name => "BigQuery";
    public string Id => "bigquery";
    public IReadOnlyList<string> SupportedPatterns => ["@google-cloud/bigquery", "bigquery"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    private static readonly HashSet<string> BigQueryQueryMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "query", "createQueryJob"
    };

    private static readonly HashSet<string> BigQueryTableMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "insert", "load", "extract"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsBigQueryCall(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsBigQueryCall(node))
        {
            var sqlText = ExtractSql(node);
            if (!string.IsNullOrEmpty(sqlText))
            {
                var clean = NestedSqlParser.CleanQueryText(sqlText);
                if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                {
                    return $"{firstWord} Query: {clean}";
                }
                return $"BigQuery Query: {clean}";
            }

            var tableName = ExtractTable(node);
            if (!string.IsNullOrEmpty(tableName))
            {
                return $"INSERT Query: INSERT INTO {tableName}";
            }

            return "BigQuery Query";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsBigQueryCall(node))
        {
            var sqlText = ExtractSql(node);
            if (!string.IsNullOrEmpty(sqlText))
            {
                NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
            }
            else
            {
                var tableName = ExtractTable(node);
                if (!string.IsNullOrEmpty(tableName))
                {
                    references.Add(new Reference(scopeSymbolId, tableName, OntologyConstants.Relationships.DependsOn));
                    references.Add(new Reference(scopeSymbolId, tableName, OntologyConstants.Relationships.UsesDb));
                }
            }
        }
    }

    private static bool IsBigQueryCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName))
        {
            if (propName != null)
            {
                if (BigQueryQueryMethods.Contains(propName))
                {
                    return ExtractSql(node) != null || IsBigQueryReceiver(objNode);
                }

                if (BigQueryTableMethods.Contains(propName))
                {
                    return ExtractTable(node) != null;
                }
            }
        }

        return false;
    }

    private static bool IsBigQueryReceiver(Node? objNode)
    {
        if (!objNode.IsValid()) return false;
        var name = objNode.Text;
        return name is "bigquery" or "bq" or "client" or "dataset";
    }

    private static string? ExtractSql(Node node)
    {
        // 1. Direct string argument: bigquery.query('SELECT ...')
        var directString = ExtractRawSql(node);
        if (!string.IsNullOrEmpty(directString))
        {
            return directString;
        }

        // 2. Options object argument: bigquery.query({ query: 'SELECT ...' })
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
                var resolved = ResolveQueryPropertyFromVariable(firstArg);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
            }
        }

        return null;
    }

    private static string? ExtractTable(Node node)
    {
        if (!AstHelper.TryGetMemberAccess(node, out var objNode, out var propName)) return null;
        if (propName == null || !BigQueryTableMethods.Contains(propName)) return null;

        // Check if objNode is dataset.table('tableName') or table('tableName')
        if (objNode.IsValid())
        {
            if (objNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
            {
                if (AstHelper.TryGetMemberAccess(objNode, out _, out var tableMethod) &&
                    string.Equals(tableMethod, "table", StringComparison.OrdinalIgnoreCase))
                {
                    var tableArg = AstHelper.ExtractFirstStringArgument(objNode);
                    if (!string.IsNullOrEmpty(tableArg))
                    {
                        return CleanTableName(tableArg);
                    }
                }
            }
            else if (objNode.Is(TreeSitterSyntax.TypeScript.Identifier))
            {
                // Follow variable back: const table = dataset.table('tableName');
                var tableName = ResolveTableFromVariable(objNode);
                if (!string.IsNullOrEmpty(tableName))
                {
                    return CleanTableName(tableName);
                }
            }
        }

        return null;
    }

    private static string CleanTableName(string rawTable)
    {
        var parts = rawTable.Split('.').Select(NestedSqlParser.CleanSqlIdentifier).Where(p => !string.IsNullOrEmpty(p)).ToArray();
        return parts.Length > 0 ? parts[^1] : rawTable;
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

    private static string? ResolveTableFromVariable(Node identNode)
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
                                if (valNode.IsValid() && valNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
                                {
                                    if (AstHelper.TryGetMemberAccess(valNode, out _, out var tableMethod) &&
                                        string.Equals(tableMethod, "table", StringComparison.OrdinalIgnoreCase))
                                    {
                                        return AstHelper.ExtractFirstStringArgument(valNode);
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
