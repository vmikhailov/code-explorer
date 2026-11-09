using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class TypeOrmLibraryParser : DeclarativeSemanticExtension
{
    public override SemanticExtensionType Type => SemanticExtensionType.RelationalDb;
    public override string Name => "TypeORM";
    public override string Id => "typeorm";
    public override IReadOnlyList<string> SupportedPatterns =>
    [
        "typeorm",
        "typeorm/*",
        "@nestjs/typeorm",
        "*sql-tools*",
        "kysely",
        "kysely/*",
        "nestjs-kysely",
        "*twenty-orm*",
        "*workspace-scoped-repository*"
    ];
    public override LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    private static readonly IReadOnlyList<CustomAstRule> Rules =
    [
        new CustomAstRule
        {
            NodeTypes =
            [
                TreeSitterSyntax.TypeScript.CallExpression,
                TreeSitterSyntax.TypeScript.Decorator,
                TreeSitterSyntax.TypeScript.ClassDeclaration,
                TreeSitterSyntax.TypeScript.ClassExpression
            ],
            MapNodeType = MapNodeTypeCore,
            ExtractIdentifier = static (node, ctx, _, _) => ExtractIdentifierCore(node, ctx),
            CollectReferences = CollectReferencesCore,
            EnrichSymbol = EnrichSymbolCore
        }
    ];

    public override IReadOnlyList<CustomAstRule> CustomAstRules => Rules;

    private static string? MapNodeTypeCore(Node node, ParsingContext ctx)
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

    private static string? ExtractIdentifierCore(Node node, ParsingContext ctx)
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

            if (IsKyselyQueryCall(node, out var kyselyTable, out _))
            {
                AstHelper.TryGetMemberAccess(node, out _, out var kProp);
                return $"Kysely: {kProp}({kyselyTable}):{node.StartPosition.Row + 1}";
            }

            if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName))
            {
                var objText = objNode?.Text ?? "Entity";
                return $"TypeORM: {objText}.{propName}:{node.StartPosition.Row + 1}";
            }

            return $"TypeORM Query:{node.StartPosition.Row + 1}";
        }
        if (IsTypeOrmEntityDecorator(node))
        {
            return ExtractTableNameFromDecorator(node);
        }
        return null;
    }

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsTypeOrmCall(node))
        {
            if (IsTypeOrmRawQuery(node))
            {
                var sqlText = AstHelper.ExtractFirstStringArgument(node);
                if (!string.IsNullOrEmpty(sqlText))
                {
                    NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
                }
            }
            else if (IsKyselyQueryCall(node, out var kyselyTable, out var isKyselyWrite))
            {
                var relKind = isKyselyWrite ? OntologyConstants.Relationships.WritesTo : OntologyConstants.Relationships.ReadsFrom;
                references.Add(new Reference(scopeSymbolId, kyselyTable, relKind));
            }
            else if (TryResolveRepositoryTargetTable(node, out var repoTable, out var isRepoWrite))
            {
                var relKind = isRepoWrite ? OntologyConstants.Relationships.WritesTo : OntologyConstants.Relationships.ReadsFrom;
                references.Add(new Reference(scopeSymbolId, repoTable, relKind));
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

    private static void EnrichSymbolCore(Node node, SyntacticSymbol symbol, ParsingContext ctx)
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

    private static bool IsTypeOrmEntityDecorator(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.Decorator)) return false;
        var text = node.Text;
        return text.StartWithIC("@Entity") ||
               text.StartWithIC("@ViewEntity") ||
               text.StartsWith("@Table(", StringComparison.Ordinal) ||
               text.Equals("@Table", StringComparison.Ordinal);
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
                        if (AstHelper.TryGetObjectProperty(arg, "name", out var nameVal) && nameVal != null && nameVal.IsValid())
                        {
                            var text = nameVal.Text.Trim('\'', '"', '`');
                            if (!string.IsNullOrEmpty(text))
                            {
                                if (!AstHelper.IsStringLiteralNode(nameVal) &&
                                    ConstantRegistry.TryResolve(null, text, out var resolvedName))
                                {
                                    return resolvedName;
                                }
                                return text;
                            }
                        }
                    }
                }
            }
        }
        var rawText = decoratorNode.Text;
        if (rawText.Contains('\'') || rawText.Contains('"'))
        {
            var f = rawText.IndexOfAny(['\'', '"']);
            var l = rawText.LastIndexOfAny(['\'', '"']);
            if (l > f) return rawText[(f + 1)..l];
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
                                if (!AstHelper.IsStringLiteralNode(schemaVal) &&
                                    ConstantRegistry.TryResolve(null, text, out var resolvedSchema))
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

    private static bool IsTypeOrmCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        if (IsKyselyQueryCall(node, out _, out _))
        {
            return true;
        }

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
                return IsRepositoryReadMethod(propName) || IsRepositoryWriteMethod(propName);
            }
        }

        return false;
    }

    private static bool IsRepositoryReadMethod(string? propName) =>
        propName is "find" or "findOne" or "findOneBy" or "findBy" or "findAndCount"
            or "findOneOrFail" or "findOneByOrFail" or "count" or "countBy"
            or "exist" or "exists" or "existsBy" or "createQueryBuilder";

    private static bool IsRepositoryWriteMethod(string? propName) =>
        propName is "save" or "insert" or "insertAndReturnOne" or "insertAndReturnMany"
            or "update" or "updateAndReturnOne" or "delete" or "softDelete" or "restore"
            or "upsert" or "remove" or "softRemove" or "clear";

    private static bool IsKyselyQueryCall(Node node, out string tableName, out bool isWrite)
    {
        tableName = "";
        isWrite = false;
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        if (!AstHelper.TryGetMemberAccess(node, out _, out var propName)) return false;
        if (propName is not ("selectFrom" or "insertInto" or "updateTable" or "deleteFrom")) return false;

        var firstStr = AstHelper.ExtractFirstStringArgument(node);
        if (string.IsNullOrWhiteSpace(firstStr)) return false;

        var cleanTbl = firstStr.Trim();
        var spaceIdx = cleanTbl.IndexOf(' ');
        if (spaceIdx > 0) cleanTbl = cleanTbl[..spaceIdx];
        if (!WorkspaceConventions.IsValidTopicOrQueueName(cleanTbl)) return false;

        tableName = cleanTbl;
        isWrite = propName is "insertInto" or "updateTable" or "deleteFrom";
        return true;
    }

    private static bool TryResolveRepositoryTargetTable(Node callNode, out string tableName, out bool isWrite)
    {
        tableName = "";
        isWrite = false;
        if (!AstHelper.TryGetMemberAccess(callNode, out var objNode, out var propName)) return false;

        isWrite = IsRepositoryWriteMethod(propName);
        if (!isWrite && !IsRepositoryReadMethod(propName)) return false;

        var objText = objNode.IsValid() ? objNode.Text.Trim() : "";
        string? entityOrTable = null;

        if (objNode.IsValid() && objNode.Is(TreeSitterSyntax.TypeScript.CallExpression) &&
            AstHelper.TryGetMemberAccess(objNode, out _, out var innerProp) &&
            innerProp is "getRepository" or "getCustomRepository")
        {
            var innerArgs = AstHelper.GetCallArguments(objNode);
            if (innerArgs.Count > 0 && innerArgs[0].IsValid())
            {
                entityOrTable = StripEntityOrTableSuffix(innerArgs[0].Text.Trim());
            }
        }
        else if (objText.Contains("manager", StringComparison.OrdinalIgnoreCase))
        {
            var callArgs = AstHelper.GetCallArguments(callNode);
            if (callArgs.Count > 0 && callArgs[0].IsValid())
            {
                var firstArg = callArgs[0].Text.Trim();
                if (firstArg.EndsWith("Entity", StringComparison.Ordinal) || firstArg.EndsWith("Table", StringComparison.Ordinal))
                {
                    entityOrTable = StripEntityOrTableSuffix(firstArg);
                }
            }
        }

        if (string.IsNullOrEmpty(entityOrTable))
        {
            var fieldName = objNode.IsValid() ? objNode.GetCanonicalMemberPath() : objText;
            if (fieldName.Contains('.')) fieldName = fieldName[(fieldName.LastIndexOf('.') + 1)..];

            entityOrTable = ResolveEntityFromEnclosingClass(callNode, fieldName);
            if (string.IsNullOrEmpty(entityOrTable))
            {
                entityOrTable = StripRepositorySuffix(fieldName);
            }
            else
            {
                entityOrTable = StripEntityOrTableSuffix(entityOrTable);
            }
        }

        if (string.IsNullOrWhiteSpace(entityOrTable) ||
            entityOrTable.Equals("repository", StringComparison.OrdinalIgnoreCase) ||
            entityOrTable.Equals("manager", StringComparison.OrdinalIgnoreCase) ||
            entityOrTable.Equals("entity", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        tableName = char.ToLowerInvariant(entityOrTable[0]) + entityOrTable[1..];
        return true;
    }

    private static string? ResolveEntityFromEnclosingClass(Node callNode, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName)) return null;

        var curr = callNode.Parent;
        while (curr.IsValid() && !curr.IsAny(TreeSitterSyntax.TypeScript.ClassDeclaration, TreeSitterSyntax.TypeScript.ClassExpression))
        {
            curr = curr.Parent;
        }
        if (!curr.IsValid()) return null;

        foreach (var dec in curr.FindDescendantsOfType(TreeSitterSyntax.TypeScript.Decorator))
        {
            var decCall = dec.FindChildOfType(TreeSitterSyntax.TypeScript.CallExpression);
            if (!decCall.IsValid()) continue;

            var decFunc = decCall.GetFunctionNode();
            if (!decFunc.IsValid() || !decFunc.Text.Contains("Repository", StringComparison.Ordinal)) continue;

            var paramNode = dec.Parent;
            if (paramNode.IsValid() && paramNode.Text.Contains(fieldName, StringComparison.Ordinal))
            {
                var args = AstHelper.GetCallArguments(decCall);
                if (args.Count > 0 && args[0].IsValid())
                {
                    return args[0].Text.Trim();
                }
            }
        }

        return null;
    }

    private static string StripRepositorySuffix(string name)
    {
        if (name.EndsWith("Repository", StringComparison.OrdinalIgnoreCase))
            return name[..^"Repository".Length];
        if (name.EndsWith("Repo", StringComparison.OrdinalIgnoreCase))
            return name[..^"Repo".Length];
        return name;
    }

    private static string StripEntityOrTableSuffix(string name)
    {
        if (name.EndsWith("Entity", StringComparison.Ordinal) && name.Length > "Entity".Length)
            return name[..^"Entity".Length];
        if (name.EndsWith("Table", StringComparison.Ordinal) && name.Length > "Table".Length)
            return name[..^"Table".Length];
        return name;
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
