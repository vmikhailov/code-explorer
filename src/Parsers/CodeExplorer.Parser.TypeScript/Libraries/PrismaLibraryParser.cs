using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class PrismaLibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "Prisma";
    public string Id => "prisma";
    public IReadOnlyList<string> SupportedPatterns => ["@prisma/client", "prisma"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsPrismaCall(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsPrismaCall(node))
        {
            if (IsPrismaRawQuery(node, out var rawSql))
            {
                if (!string.IsNullOrEmpty(rawSql))
                {
                    var clean = NestedSqlParser.CleanQueryText(rawSql);
                    if (NestedSqlParser.TryParseSql(rawSql, out var firstWord, out _))
                    {
                        return $"{firstWord} Query: {clean}";
                    }
                    return $"Prisma Raw Query: {clean}";
                }
                return "Prisma Raw Query";
            }

            if (TryExtractModelAndMethod(node, out var model, out var method))
            {
                return $"Prisma: {model}.{method}";
            }

            return "Prisma Query";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsPrismaCall(node))
        {
            if (IsPrismaRawQuery(node, out var rawSql))
            {
                if (!string.IsNullOrEmpty(rawSql))
                {
                    NestedSqlParser.TryDetectSqlDependencies(rawSql, scopeSymbolId, references);
                }
            }
            else if (TryExtractModelAndMethod(node, out var model, out _))
            {
                references.Add(new Reference(scopeSymbolId, model, OntologyConstants.Relationships.UsesDb));
            }
        }
    }

    private static bool IsPrismaCall(Node node)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.TaggedTemplateExpression))
        {
            var tag = node.Children.FirstOrDefault();
            if (tag.IsValid() && (tag.Text.Contains("$queryRaw") || tag.Text.Contains("$executeRaw")))
            {
                return true;
            }
        }

        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        // Check prisma.$queryRaw(...)
        if (func.Text.Contains("$queryRaw") || func.Text.Contains("$executeRaw"))
        {
            return true;
        }

        // Check prisma.<model>.<method>(...)
        if (func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var prop = func.GetField(TreeSitterSyntax.Fields.Property);
            if (prop.IsValid() && IsPrismaMethod(prop.Text))
            {
                var obj = func.GetField(TreeSitterSyntax.Fields.Object);
                if (obj.IsValid())
                {
                    // obj could be prisma.user or this.prisma.user
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPrismaRawQuery(Node node, out string? sqlText)
    {
        sqlText = null;
        if (node.Is(TreeSitterSyntax.TypeScript.TaggedTemplateExpression))
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
                sqlText = node.Text.Substring(tickStart + 1, tickEnd - tickStart - 1);
                return true;
            }
        }

        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var func = node.GetFunctionNode();
            if (func.IsValid() && (func.Text.Contains("$queryRaw") || func.Text.Contains("$executeRaw")))
            {
                sqlText = AstHelper.ExtractFirstStringArgument(node);
                return true;
            }
        }

        return false;
    }

    private static bool TryExtractModelAndMethod(Node node, out string model, out string method)
    {
        model = "";
        method = "";

        var func = node.GetFunctionNode();
        if (func.IsValid() && func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var propNode = func.GetField(TreeSitterSyntax.Fields.Property);
            var objNode = func.GetField(TreeSitterSyntax.Fields.Object);

            if (propNode.IsValid() && objNode.IsValid())
            {
                method = propNode.Text;
                if (objNode.Is(TreeSitterSyntax.TypeScript.MemberExpression))
                {
                    var innerProp = objNode.GetField(TreeSitterSyntax.Fields.Property);
                    if (innerProp.IsValid())
                    {
                        model = innerProp.Text;
                        return true;
                    }
                }
                model = objNode.Text;
                return true;
            }
        }

        return false;
    }

    private static bool IsPrismaMethod(string name)
    {
        return name is "findMany" or "findUnique" or "findUniqueOrThrow" or "findFirst"
                       or "findFirstOrThrow" or "create" or "createMany" or "createManyAndReturn"
                       or "update" or "updateMany" or "upsert" or "delete" or "deleteMany"
                       or "count" or "aggregate" or "groupBy";
    }
}
