using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class DrizzleLibraryParser : DeclarativeSemanticExtension
{
    public override SemanticExtensionType Type => SemanticExtensionType.RelationalDb;
    public override string Name => "Drizzle ORM";
    public override string Id => "drizzle";
    public override IReadOnlyList<string> SupportedPatterns => ["drizzle-orm", "drizzle-orm/*"];
    public override LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    private static readonly IReadOnlyList<CustomAstRule> Rules =
    [
        new CustomAstRule
        {
            NodeTypes = [TreeSitterSyntax.TypeScript.TaggedTemplateExpression, TreeSitterSyntax.TypeScript.CallExpression],
            MapNodeType = static (node, ctx) => IsDrizzleCall(node) ? OntologyConstants.NodeLabels.Query : null,
            ExtractIdentifier = static (node, ctx, _, _) => ExtractIdentifierCore(node, ctx),
            CollectReferences = CollectReferencesCore
        }
    ];

    public override IReadOnlyList<CustomAstRule> CustomAstRules => Rules;

    private static string? ExtractIdentifierCore(Node node, ParsingContext ctx)
    {
        if (IsDrizzleCall(node))
        {
            if (IsDrizzleRawSql(node, out var sqlText))
            {
                if (!string.IsNullOrEmpty(sqlText))
                {
                    var clean = NestedSqlParser.CleanQueryText(sqlText);
                    if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                    {
                        return $"{firstWord} Query: {clean}";
                    }
                    return $"Drizzle Raw Query: {clean}";
                }
                return "Drizzle Raw Query";
            }

            if (TryExtractDrizzleOperation(node, ctx, out var table, out var op))
            {
                return $"Drizzle: {op} from {table}";
            }

            return "Drizzle Query";
        }
        return null;
    }

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsDrizzleCall(node))
        {
            if (IsDrizzleRawSql(node, out var sqlText))
            {
                if (!string.IsNullOrEmpty(sqlText))
                {
                    NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
                }
            }
            else if (TryExtractDrizzleOperation(node, ctx, out var table, out _))
            {
                references.Add(new Reference(scopeSymbolId, table, OntologyConstants.Relationships.UsesDb));
            }
        }
    }

    private static bool IsDrizzleCall(Node node)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.TaggedTemplateExpression))
        {
            var tag = node.Children.FirstOrDefault();
            if (tag.IsValid() && tag.Text == "sql") return true;
        }

        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        return TryExtractDrizzleOperation(node, null, out _, out _);
    }

    private static bool IsDrizzleRawSql(Node node, out string? sqlText)
    {
        sqlText = null;
        if (node.Is(TreeSitterSyntax.TypeScript.TaggedTemplateExpression))
        {
            var tag = node.Children.FirstOrDefault();
            if (tag.IsValid() && tag.Text == "sql")
            {
                var template = node.GetField("template")
                    ?? node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.TemplateString) || c.Type.Contains("template"));
                if (template.IsValid())
                {
                    sqlText = template.Text.Trim('`');
                    return true;
                }

                var tickStart = node.Text.IndexOf('`');
                var tickEnd = node.Text.LastIndexOf('`');
                if (tickStart >= 0 && tickEnd > tickStart)
                {
                    sqlText = node.Text[(tickStart + 1)..tickEnd];
                    return true;
                }
            }
        }
        return false;
    }

    private static bool TryExtractDrizzleOperation(Node node, ParsingContext? ctx, out string table, out string op)
    {
        table = "table";
        op = "query";

        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;
        if (!AstHelper.TryGetMemberAccess(node, out var objNode, out var propName)) return false;

        // 1. db.select(...).from(table) / tx.selectDistinct(...).from(table)
        if (propName == "from")
        {
            if (objNode.IsValid() && objNode.Is(TreeSitterSyntax.TypeScript.CallExpression) &&
                AstHelper.TryGetMemberAccess(objNode, out var selectReceiver, out var selectMethod) &&
                selectMethod is "select" or "selectDistinct" or "selectDistinctOn" &&
                IsDrizzleDbReceiver(selectReceiver))
            {
                var args = AstHelper.GetCallArguments(node);
                if (args.Count > 0 && IsValidTableArg(args[0]))
                {
                    table = ResolveDrizzleTable(args[0], ctx);
                    op = "select";
                    return true;
                }
            }
            return false;
        }

        // 2. db.insert(table), db.update(table), db.delete(table)
        if (propName is "insert" or "update" or "delete")
        {
            if (IsDrizzleDbReceiver(objNode))
            {
                var args = AstHelper.GetCallArguments(node);
                if (args.Count > 0 && IsValidTableArg(args[0]))
                {
                    table = ResolveDrizzleTable(args[0], ctx);
                    op = propName;
                    return true;
                }
            }
            return false;
        }

        // 3. db.query.<table>.findMany(...) / db.query.<table>.findFirst(...)
        if (propName is "findMany" or "findFirst" &&
            objNode.IsValid() && objNode.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var queryObj = objNode.GetField(TreeSitterSyntax.Fields.Object);
            var tableProp = objNode.GetField(TreeSitterSyntax.Fields.Property);
            if (queryObj.IsValid() && tableProp.IsValid() &&
                queryObj.Is(TreeSitterSyntax.TypeScript.MemberExpression))
            {
                var rootReceiver = queryObj.GetField(TreeSitterSyntax.Fields.Object);
                var queryProp = queryObj.GetField(TreeSitterSyntax.Fields.Property);
                if (queryProp.IsValid() && queryProp.Text == "query" && IsDrizzleDbReceiver(rootReceiver))
                {
                    table = ResolveDrizzleTable(tableProp, ctx);
                    op = propName;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsDrizzleDbReceiver(Node? receiver)
    {
        if (!receiver.IsValid()) return false;
        var text = receiver.Text.Trim();
        if (text.StartsWith("this.", StringComparison.Ordinal)) text = text[5..];
        if (text.Contains('.')) text = text.Split('.')[^1];
        if (text.EndsWith("()", StringComparison.Ordinal)) text = text[..^2];

        return text is "db" or "tx" or "trx" or "database" or "drizzle" or "conn" or "client" ||
               text.EndsWith("Db", StringComparison.Ordinal) ||
               text.EndsWith("DB", StringComparison.Ordinal) ||
               text.EndsWith("Database", StringComparison.Ordinal) ||
               text.EndsWith("Tx", StringComparison.Ordinal) ||
               text.EndsWith("Trx", StringComparison.Ordinal);
    }

    private static bool IsValidTableArg(Node arg)
    {
        if (!arg.IsValid()) return false;
        return arg.IsAny(
            TreeSitterSyntax.TypeScript.Identifier,
            TreeSitterSyntax.TypeScript.MemberExpression);
    }

    private static string ResolveDrizzleTable(Node tableNode, ParsingContext? ctx)
    {
        var clean = tableNode.Text.Trim();
        if (clean.Contains('.'))
        {
            clean = clean.Split('.')[^1];
        }

        var lookupKey = clean;
        var src = tableNode.Document?.SourceText;
        if (!string.IsNullOrEmpty(src) && src.Contains(" as " + clean, StringComparison.Ordinal))
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                src,
                $@"\b([a-zA-Z0-9_]+)\s+as\s+{System.Text.RegularExpressions.Regex.Escape(clean)}\b");
            if (m.Success)
            {
                lookupKey = m.Groups[1].Value;
            }
        }

        var drizzleKey = "drizzle_table:" + lookupKey;
        if ((ctx != null && ConstantRegistry.TryResolve(ctx.WorkspaceId, drizzleKey, out var resolved) && IsValidResolvedTableName(resolved)) ||
            (ConstantRegistry.TryResolve(null, drizzleKey, out resolved) && IsValidResolvedTableName(resolved)))
        {
            return resolved!;
        }

        return clean;
    }

    private static bool IsValidResolvedTableName(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        if (candidate.Contains("://", StringComparison.Ordinal) ||
            candidate.Contains('/') ||
            candidate.Contains(' ') ||
            candidate.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return ResourceNamingHelper.IsValidTableIdentifier(candidate);
    }
}
