using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class KnexLibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "Knex";
    public string Id => "knex";
    public IReadOnlyList<string> SupportedPatterns => ["knex", "@types/knex"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsKnexCall(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsKnexCall(node))
        {
            if (IsKnexRaw(node))
            {
                var sqlText = AstHelper.ExtractFirstStringArgument(node);
                if (!string.IsNullOrEmpty(sqlText))
                {
                    var clean = NestedSqlParser.CleanQueryText(sqlText);
                    if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                    {
                        return $"{firstWord} Query: {clean}";
                    }
                    return $"Knex Raw Query: {clean}";
                }
                return "Knex Raw Query";
            }

            var table = ExtractTableName(node);
            if (!string.IsNullOrEmpty(table))
            {
                return $"Knex: {table}";
            }

            return "Knex Query";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsKnexCall(node))
        {
            if (IsKnexRaw(node))
            {
                var sqlText = AstHelper.ExtractFirstStringArgument(node);
                if (!string.IsNullOrEmpty(sqlText))
                {
                    NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
                }
            }
            else
            {
                var table = ExtractTableName(node);
                if (!string.IsNullOrEmpty(table))
                {
                    references.Add(new Reference(scopeSymbolId, table, OntologyConstants.Relationships.UsesDb));
                }
            }
        }
    }

    private static bool IsKnexCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        // knex('users') or k('users') or db('users')
        if (func.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            var firstArg = AstHelper.ExtractFirstStringArgument(node);
            return !string.IsNullOrEmpty(firstArg);
        }

        // knex.raw('SELECT ...') or qb.from('users') or db.table('users')
        if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName))
        {
            if (propName == "raw") return true;

            var objText = objNode?.Text;
            if (objText is "knex" or "k" or "db" or "trx" or "qb")
            {
                return propName is "select" or "insert" or "update" or "del" or "delete" or "from" or "table" or "into" or "schema";
            }

            return propName is "from" or "into" or "table";
        }

        return false;
    }

    private static bool IsKnexRaw(Node node)
    {
        if (AstHelper.TryGetMemberAccess(node, out _, out var propName))
        {
            return propName == "raw";
        }
        return false;
    }

    private static string? ExtractTableName(Node node)
    {
        var current = node;
        while (current.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var func = current.GetFunctionNode();
            if (func.IsValid() && func.Is(TreeSitterSyntax.TypeScript.Identifier))
            {
                var arg = AstHelper.ExtractFirstStringArgument(current);
                if (!string.IsNullOrEmpty(arg)) return arg;
            }

            if (AstHelper.TryGetMemberAccess(current, out var objNode, out var propName))
            {
                if (propName is "from" or "into" or "table")
                {
                    var arg = AstHelper.ExtractFirstStringArgument(current);
                    if (!string.IsNullOrEmpty(arg)) return arg;
                }

                if (objNode.IsValid() && objNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
                {
                    current = objNode;
                    continue;
                }
            }

            break;
        }

        return null;
    }
}
