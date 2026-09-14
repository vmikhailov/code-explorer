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
