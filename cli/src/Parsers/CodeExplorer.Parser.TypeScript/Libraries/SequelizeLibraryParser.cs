using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class SequelizeLibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "Sequelize";
    public string Id => "sequelize";
    public IReadOnlyList<string> SupportedPatterns => ["sequelize", "sequelize-typescript", "@types/sequelize"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsSequelizeCall(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsSequelizeCall(node))
        {
            if (IsSequelizeRawQuery(node))
            {
                var sqlText = AstHelper.ExtractFirstStringArgument(node);
                if (!string.IsNullOrEmpty(sqlText))
                {
                    var clean = NestedSqlParser.CleanQueryText(sqlText);
                    if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                    {
                        return $"{firstWord} Query: {clean}";
                    }
                    return $"Sequelize Query: {clean}";
                }
                return "Sequelize Query";
            }

            if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName))
            {
                var model = objNode?.Text ?? "Model";
                return $"Sequelize: {model}.{propName}";
            }

            return "Sequelize Query";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsSequelizeCall(node))
        {
            if (IsSequelizeRawQuery(node))
            {
                var sqlText = AstHelper.ExtractFirstStringArgument(node);
                if (!string.IsNullOrEmpty(sqlText))
                {
                    NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
                }
            }
            else if (AstHelper.TryGetMemberAccess(node, out var objNode, out _))
            {
                var model = objNode?.Text;
                if (!string.IsNullOrEmpty(model) && char.IsUpper(model[0]))
                {
                    references.Add(new Reference(scopeSymbolId, model, OntologyConstants.Relationships.UsesDb));
                }
            }
        }
    }

    private static bool IsSequelizeCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName))
        {
            if (propName == "query")
            {
                var obj = objNode?.Text?.ToLowerInvariant() ?? "";
                return obj.Contains("sequelize");
            }

            return propName is "findAll" or "findOne" or "findByPk" or "findOrCreate"
                               or "create" or "bulkCreate" or "update" or "destroy" or "count" or "max" or "min";
        }

        return false;
    }

    private static bool IsSequelizeRawQuery(Node node)
    {
        if (AstHelper.TryGetMemberAccess(node, out _, out var propName))
        {
            return propName == "query";
        }
        return false;
    }
}
