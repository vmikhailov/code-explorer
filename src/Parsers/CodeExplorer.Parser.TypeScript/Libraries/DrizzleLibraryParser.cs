using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class DrizzleLibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "Drizzle ORM";
    public string Id => "drizzle";
    public IReadOnlyList<string> SupportedPatterns => ["drizzle-orm", "drizzle-orm/*"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsDrizzleCall(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
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

            if (TryExtractDrizzleOperation(node, out var table, out var op))
            {
                return $"Drizzle: {op} from {table}";
            }

            return "Drizzle Query";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
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
            else if (TryExtractDrizzleOperation(node, out var table, out _))
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

        if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName))
        {
            if (propName is "select" or "insert" or "update" or "delete" or "from" or "values" or "set" or "query")
            {
                return true;
            }
        }

        return false;
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

    private static bool TryExtractDrizzleOperation(Node node, out string table, out string op)
    {
        table = "table";
        op = "query";

        var current = node;
        while (current.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            if (AstHelper.TryGetMemberAccess(current, out var objNode, out var propName))
            {
                if (propName is "from" or "insert" or "update" or "delete")
                {
                    var args = AstHelper.GetCallArguments(current);
                    if (args.Count > 0)
                    {
                        table = args[0].Text;
                        op = propName;
                        return true;
                    }
                }

                if (objNode.IsValid() && objNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
                {
                    current = objNode;
                    continue;
                }
            }

            break;
        }

        return false;
    }
}
