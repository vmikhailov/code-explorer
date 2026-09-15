using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class TypeOrmLibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "TypeORM";
    public string Id => "typeorm";
    public IReadOnlyList<string> SupportedPatterns => ["typeorm", "typeorm/*"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsTypeOrmCall(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        if (IsTypeOrmEntityDecorator(node))
        {
            return OntologyConstants.NodeLabels.Table;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsTypeOrmCall(node))
        {
            if (IsTypeOrmRawQuery(node))
            {
                var sqlText = AstHelper.ExtractFirstStringArgument(node);
                if (!string.IsNullOrEmpty(sqlText))
                {
                    var clean = NestedSqlParser.CleanQueryText(sqlText);
                    if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                    {
                        return $"{firstWord} Query: {clean}";
                    }
                    return $"TypeORM Query: {clean}";
                }
                return "TypeORM Query";
            }

            if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName))
            {
                var objText = objNode?.Text ?? "Entity";
                return $"TypeORM: {objText}.{propName}";
            }

            return "TypeORM Query";
        }
        if (IsTypeOrmEntityDecorator(node))
        {
            return ExtractTableNameFromDecorator(node);
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsTypeOrmCall(node) && IsTypeOrmRawQuery(node))
        {
            var sqlText = AstHelper.ExtractFirstStringArgument(node);
            if (!string.IsNullOrEmpty(sqlText))
            {
                NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
            }
        }
        else if (IsTypeOrmEntityDecorator(node))
        {
            var tableName = ExtractTableNameFromDecorator(node);
            if (!string.IsNullOrEmpty(tableName))
            {
                references.Add(new Reference(scopeSymbolId, tableName, OntologyConstants.Relationships.PersistedIn));
            }
        }
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (node.IsAny(TreeSitterSyntax.TypeScript.ClassDeclaration, TreeSitterSyntax.TypeScript.ClassExpression))
        {
            var decorators = new List<Node>();
            decorators.AddRange(node.FindChildrenOfType(TreeSitterSyntax.TypeScript.Decorator));
            decorators.AddRange(GetPrecedingDecorators(node));

            if (node.Parent.IsValid())
            {
                if (node.Parent.Is(TreeSitterSyntax.TypeScript.ExportStatement))
                {
                    decorators.AddRange(node.Parent.FindChildrenOfType(TreeSitterSyntax.TypeScript.Decorator));
                    decorators.AddRange(GetPrecedingDecorators(node.Parent));
                }
            }

            foreach (var dec in decorators)
            {
                if (IsTypeOrmEntityDecorator(dec))
                {
                    var tableName = ExtractTableNameFromDecorator(dec);
                    if (string.IsNullOrEmpty(tableName)) tableName = symbol.Name;
                    symbol.References.Add(new Reference(symbol.Name, tableName, OntologyConstants.Relationships.PersistedIn));
                }
            }
        }
    }

    private static List<Node> GetPrecedingDecorators(Node node)
    {
        var result = new List<Node>();
        var parent = node.Parent;
        if (!parent.IsValid()) return result;
        var children = parent.Children;
        var idx = children.ToList().FindIndex(c => c.Id == node.Id);
        if (idx <= 0) return result;

        for (var i = idx - 1; i >= 0; i--)
        {
            var sibling = children[i];
            if (sibling.Is(TreeSitterSyntax.TypeScript.Decorator))
            {
                result.Add(sibling);
            }
            else
            {
                break;
            }
        }
        return result;
    }

    private static bool IsTypeOrmEntityDecorator(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.Decorator)) return false;
        var text = node.Text;
        return text.StartsWith("@Entity", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractTableNameFromDecorator(Node decoratorNode)
    {
        var callExpr = decoratorNode.FindChildOfType(TreeSitterSyntax.TypeScript.CallExpression);
        if (callExpr.IsValid())
        {
            var str = AstHelper.ExtractFirstStringArgument(callExpr);
            if (!string.IsNullOrEmpty(str)) return str;
        }
        var text = decoratorNode.Text;
        if (text.Contains('\'') || text.Contains('"'))
        {
            var f = text.IndexOfAny(['\'', '"']);
            var l = text.LastIndexOfAny(['\'', '"']);
            if (l > f) return text.Substring(f + 1, l - f - 1);
        }
        return null;
    }

    private static bool IsTypeOrmCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName))
        {
            if (propName == "query")
            {
                return true;
            }

            if (propName is "createQueryBuilder") return true;

            var obj = objNode?.Text?.ToLowerInvariant() ?? "";
            if (obj.Contains("repo") || obj.Contains("repository") || obj.Contains("manager"))
            {
                return propName is "find" or "findOne" or "findOneBy" or "findAndCount"
                                   or "save" or "insert" or "update" or "delete" or "remove" or "count";
            }
        }

        return false;
    }

    private static bool IsTypeOrmRawQuery(Node node)
    {
        if (AstHelper.TryGetMemberAccess(node, out _, out var propName))
        {
            return propName == "query";
        }
        return false;
    }
}
