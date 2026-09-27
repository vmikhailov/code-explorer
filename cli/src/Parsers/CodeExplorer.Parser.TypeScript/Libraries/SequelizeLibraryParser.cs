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
        if (IsSequelizeTableDecorator(node))
        {
            return OntologyConstants.NodeLabels.Table;
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
        if (IsSequelizeTableDecorator(node))
        {
            return ExtractTableNameFromDecorator(node);
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
        else if (IsSequelizeTableDecorator(node))
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
                if (IsSequelizeTableDecorator(dec))
                {
                    var tableName = ExtractTableNameFromDecorator(dec);
                    if (string.IsNullOrEmpty(tableName)) tableName = symbol.Name;
                    symbol.References.Add(new Reference(symbol.Name, tableName, OntologyConstants.Relationships.PersistedIn));

                    var schema = ExtractSchemaFromDecorator(dec);
                    if (!string.IsNullOrEmpty(schema))
                    {
                        symbol.Properties["schema"] = schema;
                    }
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

    private static bool IsSequelizeTableDecorator(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.Decorator)) return false;
        var text = node.Text;
        return text.StartsWith("@Table", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractTableNameFromDecorator(Node decoratorNode)
    {
        var callExpr = decoratorNode.FindChildOfType(TreeSitterSyntax.TypeScript.CallExpression);
        if (callExpr.IsValid())
        {
            var str = AstHelper.ExtractFirstStringArgument(callExpr);
            if (!string.IsNullOrEmpty(str)) return str;

            var argsNode = callExpr.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);
            if (argsNode.IsValid())
            {
                foreach (var arg in argsNode.Children)
                {
                    if (arg.Is(TreeSitterSyntax.TypeScript.Object))
                    {
                        if (AstHelper.TryGetObjectProperty(arg, "tableName", out var tVal) && tVal != null && tVal.IsValid())
                        {
                            var text = tVal.Text.Trim('\'', '"', '`');
                            if (!string.IsNullOrEmpty(text))
                            {
                                if (ConstantRegistry.TryResolve(null, text, out var resolvedName)) return resolvedName;
                                return text;
                            }
                        }
                        if (AstHelper.TryGetObjectProperty(arg, "name", out var nVal) && nVal != null && nVal.IsValid())
                        {
                            var text = nVal.Text.Trim('\'', '"', '`');
                            if (!string.IsNullOrEmpty(text))
                            {
                                if (ConstantRegistry.TryResolve(null, text, out var resolvedName)) return resolvedName;
                                return text;
                            }
                        }
                    }
                }
            }
        }
        return null;
    }

    public static string? ExtractSchemaFromDecorator(Node decoratorNode)
    {
        var callExpr = decoratorNode.FindChildOfType(TreeSitterSyntax.TypeScript.CallExpression);
        if (callExpr.IsValid())
        {
            var argsNode = callExpr.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);
            if (argsNode.IsValid())
            {
                foreach (var arg in argsNode.Children)
                {
                    if (arg.Is(TreeSitterSyntax.TypeScript.Object))
                    {
                        if (AstHelper.TryGetObjectProperty(arg, "schema", out var schemaVal) && schemaVal != null && schemaVal.IsValid())
                        {
                            var text = schemaVal.Text.Trim('\'', '"', '`');
                            if (!string.IsNullOrEmpty(text))
                            {
                                if (ConstantRegistry.TryResolve(null, text, out var resolvedSchema))
                                {
                                    return resolvedSchema;
                                }
                                return text;
                            }
                        }
                    }
                }
            }
        }
        return null;
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
