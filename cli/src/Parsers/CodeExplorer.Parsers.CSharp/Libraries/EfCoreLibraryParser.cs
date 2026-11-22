using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.CSharp.Libraries;

public class EfCoreLibraryParser : DeclarativeSemanticExtension
{
    public EfCoreLibraryParser()
    {
        Type = SemanticExtensionType.RelationalDb;
        Name = "EF Core";
        Id = "microsoft.entityframeworkcore";
        SupportedPatterns =
        [
            "Microsoft.EntityFrameworkCore",
            "Microsoft.EntityFrameworkCore.*",
            "Ardalis.Specification",
            "Ardalis.Specification.*",
            "System.Data.Entity",
            "System.Data.Entity.*",
            "EntityFramework",
            "System.ComponentModel.DataAnnotations.Schema"
        ];
        LibraryRole = LibraryRole.OrmOrDatabase;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.CSharp.Attribute, TreeSitterSyntax.CSharp.PropertyDeclaration, TreeSitterSyntax.CSharp.InvocationExpression, TreeSitterSyntax.CSharp.ClassDeclaration }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => (IsTableAttribute(node) || IsDbSetProperty(node) || IsToTableCall(node)) ? OntologyConstants.NodeLabels.Table : null,
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (IsTableAttribute(node))
                    {
                        var tableName = ExtractTableNameFromAttribute(node);
                        if (!string.IsNullOrEmpty(tableName)) return tableName;
                    }
                    if (IsDbSetProperty(node))
                    {
                        var tableName = ExtractTableNameFromDbSet(node);
                        if (!string.IsNullOrEmpty(tableName)) return tableName;
                    }
                    if (IsToTableCall(node))
                    {
                        var tableName = ExtractTableNameFromToTable(node);
                        if (!string.IsNullOrEmpty(tableName)) return tableName;
                    }
                    return null;
                },
                CollectReferences = CollectEfCoreReferences,
                EnrichSymbol = EnrichEfCoreSymbol
            }
        ];
    }

    private static void CollectEfCoreReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsTableAttribute(node))
        {
            var tableName = ExtractTableNameFromAttribute(node);
            var classDecl = GetParentClass(node);
            if (classDecl.IsValid() && !string.IsNullOrEmpty(tableName))
            {
                var classNameNode = classDecl.GetField(TreeSitterSyntax.Fields.Name);
                if (classNameNode.IsValid() && classNameNode.Text != tableName)
                {
                    references.Add(new Reference(classNameNode.Text, tableName, OntologyConstants.Relationships.PersistedIn));
                }
            }
        }
        else if (IsDbSetProperty(node))
        {
            var (entityType, tableName) = ExtractDbSetInfo(node);
            if (!string.IsNullOrEmpty(entityType) && !string.IsNullOrEmpty(tableName))
            {
                references.Add(new Reference(entityType, tableName, OntologyConstants.Relationships.PersistedIn));
                if (!string.IsNullOrEmpty(scopeSymbolId))
                {
                    references.Add(new Reference(scopeSymbolId, entityType, OntologyConstants.Relationships.UsesType));
                }
            }
        }
        else if (IsToTableCall(node))
        {
            var tableName = ExtractTableNameFromToTable(node);
            var entityType = FindEntityTypeForToTable(node);
            if (!string.IsNullOrEmpty(tableName) && !string.IsNullOrEmpty(entityType))
            {
                references.Add(new Reference(entityType, tableName, OntologyConstants.Relationships.PersistedIn));
                if (!string.IsNullOrEmpty(scopeSymbolId))
                {
                    references.Add(new Reference(scopeSymbolId, entityType, OntologyConstants.Relationships.UsesType));
                }
            }
        }
        else if (node.Is(TreeSitterSyntax.CSharp.InvocationExpression) &&
                 TryExtractEfCoreDbSetAccess(node, out var dbSetTarget, out var isWriteAccess))
        {
            var relKind = isWriteAccess ? OntologyConstants.Relationships.WritesTo : OntologyConstants.Relationships.ReadsFrom;
            var relEnum = isWriteAccess ? CodeExplorer.Cypher.Common.RelationshipKind.WritesTo : CodeExplorer.Cypher.Common.RelationshipKind.ReadsFrom;
            if (!references.Any(r => r.Kind == relEnum && string.Equals(r.TargetName, dbSetTarget, StringComparison.OrdinalIgnoreCase)))
            {
                references.Add(new Reference(scopeSymbolId, dbSetTarget, relKind, node.StartPosition.Row + 1, node.StartPosition.Column + 1));
            }
        }
    }

    private static void EnrichEfCoreSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (node.Is(TreeSitterSyntax.CSharp.ClassDeclaration))
        {
            var classSchema = ExtractSchemaFromClass(node);
            if (!string.IsNullOrEmpty(classSchema))
            {
                symbol.Properties["schema"] = classSchema;
            }

            foreach (var child in node.Children)
            {
                if (child.Is(TreeSitterSyntax.CSharp.AttributeList))
                {
                    foreach (var attr in child.Children)
                    {
                        if (attr.Is(TreeSitterSyntax.CSharp.Attribute) && IsTableAttribute(attr))
                        {
                            var tableName = ExtractTableNameFromAttribute(attr);
                            if (!string.IsNullOrEmpty(tableName))
                            {
                                symbol.References.Add(new Reference(symbol.Name, tableName, OntologyConstants.Relationships.PersistedIn));
                            }
                            var attrSchema = ExtractSchemaFromTableAttribute(attr);
                            if (!string.IsNullOrEmpty(attrSchema))
                            {
                                symbol.Properties["schema"] = attrSchema;
                            }
                        }
                    }
                }
            }
        }
        else if (IsDbSetProperty(node))
        {
            var (entityType, tableName) = ExtractDbSetInfo(node);
            if (!string.IsNullOrEmpty(entityType) && !string.IsNullOrEmpty(tableName))
            {
                symbol.References.Add(new Reference(entityType, tableName, OntologyConstants.Relationships.PersistedIn));
            }
        }
        else if (IsToTableCall(node))
        {
            var tableName = ExtractTableNameFromToTable(node);
            var entityType = FindEntityTypeForToTable(node);
            if (!string.IsNullOrEmpty(tableName) && !string.IsNullOrEmpty(entityType))
            {
                symbol.References.Add(new Reference(entityType, tableName, OntologyConstants.Relationships.PersistedIn));
            }
            var toTableSchema = ExtractSchemaFromToTable(node);
            if (!string.IsNullOrEmpty(toTableSchema))
            {
                symbol.Properties["schema"] = toTableSchema;
            }
        }
    }

    public static string? ExtractSchemaFromClass(Node classNode)
    {
        if (!classNode.IsValid()) return null;

        // 1. Look for const string SchemaName = "..." or DefaultSchemaName = "..."
        var constSchema = FindConstantValueInClass(classNode);
        if (!string.IsNullOrEmpty(constSchema)) return constSchema;

        // 2. Scan entire class AST for HasDefaultSchema("...") invocation
        var schemaFromMethod = ScanForHasDefaultSchema(classNode);
        if (!string.IsNullOrEmpty(schemaFromMethod)) return schemaFromMethod;

        return null;
    }

    private static bool IsSchemaStopWord(string? schema)
    {
        if (string.IsNullOrWhiteSpace(schema)) return true;
        var clean = schema.Trim('"', '\'', ' ', ';');
        return clean.Equals("DefaultSchemaName", StringComparison.OrdinalIgnoreCase) ||
               clean.Equals("SchemaName", StringComparison.OrdinalIgnoreCase) ||
               clean.Equals("Schema", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<Node> GetClassFields(Node classNode)
    {
        var declList = classNode.FindChildOfType(TreeSitterSyntax.CSharp.DeclarationList);
        if (declList.IsValid())
        {
            foreach (var f in declList.FindChildrenOfType(TreeSitterSyntax.CSharp.FieldDeclaration))
                yield return f;
        }
        foreach (var f in classNode.FindChildrenOfType(TreeSitterSyntax.CSharp.FieldDeclaration))
        {
            yield return f;
        }
    }

    public static string? FindConstantValueInClass(Node classNode, string? constName = null)
    {
        if (!classNode.IsValid()) return null;
        foreach (var field in GetClassFields(classNode))
        {
            var text = field.Text;
            if (text.Contains("const") && text.Contains("string"))
            {
                var varDecl = field.FindChildOfType(TreeSitterSyntax.CSharp.VariableDeclaration);
                if (varDecl.IsValid())
                {
                    foreach (var declarator in varDecl.FindChildrenOfType(TreeSitterSyntax.CSharp.VariableDeclarator))
                    {
                        var nameNode = declarator.GetField(TreeSitterSyntax.Fields.Name);
                        if (nameNode.IsValid())
                        {
                            if (constName == null || string.Equals(nameNode.Text, constName, StringComparison.OrdinalIgnoreCase) ||
                                nameNode.Text.Contains("Schema", StringComparison.OrdinalIgnoreCase))
                            {
                                var valNode = declarator.GetField(TreeSitterSyntax.Fields.Value);
                                if (valNode.IsValid() && valNode.Type.Contains("string", StringComparison.Ordinal))
                                {
                                    return valNode.Text.Trim('"');
                                }
                                var str = declarator.Children.FirstOrDefault(c => c.Type.Contains("string", StringComparison.Ordinal));
                                if (str.IsValid())
                                {
                                    return str.Text.Trim('"');
                                }
                            }
                        }
                    }
                }
            }
        }
        return null;
    }

    private static string? ScanForHasDefaultSchema(Node node)
    {
        if (node.Is(TreeSitterSyntax.CSharp.InvocationExpression))
        {
            var func = node.GetFunctionNode();
            if (func.IsValid() && func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            {
                var nameNode = func.GetField(TreeSitterSyntax.Fields.Name);
                if (nameNode.IsValid() && nameNode.Text == "HasDefaultSchema")
                {
                    var argList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
                    var firstArg = argList?.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
                    if (firstArg.IsValid())
                    {
                        var strNode = firstArg.Children.FirstOrDefault(c => c.Type.Contains("string"));
                        if (strNode.IsValid())
                        {
                            var val = strNode.Text.Trim('"', '\'');
                            if (!IsSchemaStopWord(val)) return val;
                        }
                        var idNode = firstArg.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                        if (idNode.IsValid())
                        {
                            var parentClass = GetParentClass(node);
                            if (parentClass != null && parentClass.IsValid())
                            {
                                var constVal = FindConstantValueInClass(parentClass, idNode.Text);
                                if (!string.IsNullOrEmpty(constVal) && !IsSchemaStopWord(constVal)) return constVal;
                            }
                            return null;
                        }
                    }
                }
            }
        }

        foreach (var child in node.Children)
        {
            var found = ScanForHasDefaultSchema(child);
            if (!string.IsNullOrEmpty(found)) return found;
        }

        return null;
    }

    public static string? ExtractSchemaFromToTable(Node invocationNode)
    {
        var argList = invocationNode.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
        if (argList == null || !argList.IsValid()) return null;

        var args = argList.FindChildrenOfType(TreeSitterSyntax.CSharp.Argument).ToList();
        var named = args.FirstOrDefault(a => a.Text.StartWithIC("schema:"));
        if (named.IsValid())
        {
            var str = named.Children.FirstOrDefault(c => c.Type.Contains("string"));
            if (str.IsValid()) return str.Text.Trim('"');
        }
        if (args.Count >= 2)
        {
            var second = args[1];
            var str = second.Children.FirstOrDefault(c => c.Type.Contains("string"));
            if (str.IsValid()) return str.Text.Trim('"');
        }
        return null;
    }

    public static string? ExtractSchemaFromTableAttribute(Node attrNode)
    {
        var argList = attrNode.FindChildOfType(TreeSitterSyntax.CSharp.AttributeArgumentList);
        if (argList.IsValid())
        {
            foreach (var arg in argList.FindChildrenOfType(TreeSitterSyntax.CSharp.AttributeArgument))
            {
                if (arg.Text.StartWithIC("Schema"))
                {
                    var parts = arg.Text.Split('=');
                    if (parts.Length == 2)
                    {
                        return parts[1].Trim().Trim('"');
                    }
                }
            }
        }
        return null;
    }

    private static bool IsTableAttribute(Node node)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.Attribute)) return false;
        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name)
                       ?? node.FindChildOfType(TreeSitterSyntax.Common.Identifier);
        return nameNode.IsValid() && (nameNode.Text is "Table" or "TableAttribute");
    }

    private static string? ExtractTableNameFromAttribute(Node attrNode)
    {
        var argList = attrNode.FindChildOfType(TreeSitterSyntax.CSharp.AttributeArgumentList);
        if (argList.IsValid())
        {
            foreach (var arg in argList.FindChildrenOfType(TreeSitterSyntax.CSharp.AttributeArgument))
            {
                foreach (var child in arg.Children)
                {
                    if (child.Type.Contains("string"))
                    {
                        return child.Text.Trim('"');
                    }
                }
                var text = arg.Text.Trim('"');
                if (text.Contains('=')) text = text[(text.IndexOf('=') + 1)..].Trim().Trim('"');
                return text;
            }
        }
        return null;
    }

    private static bool IsDbSetProperty(Node node)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.PropertyDeclaration)) return false;
        var typeNode = node.GetField(TreeSitterSyntax.Fields.Type);
        if (!typeNode.IsValid() || (!typeNode.Text.StartsWith("DbSet<") && !typeNode.Text.StartsWith("IDbSet<"))) return false;

        var (entityType, tableName) = ExtractDbSetInfo(node);
        if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(entityType)) return false;

        if (tableName.Equals("DbSet", StringComparison.OrdinalIgnoreCase) || tableName.Equals("IDbSet", StringComparison.OrdinalIgnoreCase))
            return false;

        if (IsGenericTypeParameter(node, entityType))
            return false;

        return true;
    }

    private static bool IsGenericTypeParameter(Node propNode, string typeName)
    {
        var classDecl = GetParentClass(propNode);
        if (!classDecl.IsValid()) return false;

        var typeParams = classDecl.FindChildOfType(TreeSitterSyntax.CSharp.TypeParameterList);
        if (typeParams.IsValid())
        {
            foreach (var child in typeParams.Children)
            {
                if ((child.Is(TreeSitterSyntax.CSharp.TypeParameter) || child.Is(TreeSitterSyntax.Common.Identifier)) && child.Text == typeName)
                    return true;
            }
        }
        return false;
    }

    private static bool IsToTableCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression)) return false;
        var nameNode = func.GetField(TreeSitterSyntax.Fields.Name);
        return nameNode.IsValid() && nameNode.Text == "ToTable";
    }

    private static string? ExtractTableNameFromToTable(Node invocationNode)
    {
        var argList = invocationNode.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
        var firstArg = argList?.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
        if (firstArg == null || !firstArg.IsValid()) return null;

        var strNode = firstArg.Children.FirstOrDefault(c => c.Type.Contains("string"));
        if (strNode.IsValid())
        {
            return strNode.Text.Trim('"');
        }

        var expr = firstArg.GetField(TreeSitterSyntax.Fields.Expression)
                   ?? firstArg.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.MemberAccessExpression, TreeSitterSyntax.Common.Identifier));
        if (expr.IsValid())
        {
            if (expr.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            {
                var nameChild = expr.GetField(TreeSitterSyntax.Fields.Name);
                if (nameChild.IsValid()) return nameChild.Text;
            }
            return expr.Text.Trim('"');
        }

        return null;
    }

    private static string? FindEntityTypeForToTable(Node invocationNode)
    {
        var current = invocationNode.Parent;
        while (current.IsValid())
        {
            if (current.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
            {
                var entityType = ExtractEntityTypeFromConfigurationClass(current);
                if (!string.IsNullOrEmpty(entityType)) return entityType;
            }
            current = current.Parent;
        }

        var func = invocationNode.GetFunctionNode();
        if (func.IsValid() && func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            var receiver = func.GetField(TreeSitterSyntax.Fields.Expression);
            if (receiver.IsValid() && receiver.Is(TreeSitterSyntax.CSharp.InvocationExpression))
            {
                var receiverFunc = receiver.GetFunctionNode();
                if (receiverFunc.IsValid() && receiverFunc.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
                {
                    var receiverMethodName = receiverFunc.GetField(TreeSitterSyntax.Fields.Name);
                    if (receiverMethodName.IsValid() && receiverMethodName.Text == "Entity")
                    {
                        var typeArgList = receiver.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList)
                                          ?? receiverFunc.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
                        var firstType = typeArgList?.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier));
                        if (firstType.IsValid()) return firstType.Text;
                    }
                }
            }
        }

        return null;
    }

    private static string? ExtractEntityTypeFromConfigurationClass(Node classNode)
    {
        var baseList = classNode.FindChildOfType(TreeSitterSyntax.CSharp.BaseList);
        if (!baseList.IsValid()) return null;

        foreach (var child in baseList.Children)
        {
            if (child.Is(TreeSitterSyntax.CSharp.GenericName) || child.Text.Contains("IEntityTypeConfiguration"))
            {
                var typeArgList = child.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
                var firstType = typeArgList?.Children.FirstOrDefault(c =>
                    c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier, TreeSitterSyntax.CSharp.GenericName));
                if (firstType.IsValid()) return firstType.Text;
            }
        }
        return null;
    }

    private static (string? EntityType, string? TableName) ExtractDbSetInfo(Node propNode)
    {
        var nameNode = propNode.GetField(TreeSitterSyntax.Fields.Name);
        var tableName = nameNode.IsValid() ? nameNode.Text : null;

        var typeNode = propNode.GetField(TreeSitterSyntax.Fields.Type);
        string? entityType = null;
        if (typeNode.IsValid() && typeNode.Text.Contains('<'))
        {
            var start = typeNode.Text.IndexOf('<');
            var end = typeNode.Text.LastIndexOf('>');
            if (end > start)
            {
                entityType = typeNode.Text[(start + 1)..end].Trim();
            }
        }
        return (entityType, tableName);
    }

    private static string? ExtractTableNameFromDbSet(Node node)
    {
        if (!IsDbSetProperty(node)) return null;
        var (_, tableName) = ExtractDbSetInfo(node);
        return tableName;
    }

    private static Node? GetParentClass(Node node)
    {
        var current = node.Parent;
        while (current.IsValid())
        {
            if (current.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.StructDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
                return current;
            current = current.Parent;
        }
        return null;
    }

    private static readonly FrozenSet<string> EfCoreWriteMethods = new[]
    {
        "Add", "AddAsync", "AddRange", "AddRangeAsync",
        "Update", "UpdateAsync", "UpdateRange", "UpdateRangeAsync",
        "Remove", "RemoveRange",
        "Delete", "DeleteAsync", "DeleteRange", "DeleteRangeAsync",
        "SaveChanges", "SaveChangesAsync",
        "ExecuteUpdate", "ExecuteUpdateAsync",
        "ExecuteDelete", "ExecuteDeleteAsync"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> EfCoreReadMethods = new[]
    {
        "Find", "FindAsync", "GetByIdAsync", "GetBySpecAsync", "ListAsync",
        "First", "FirstAsync", "FirstOrDefault", "FirstOrDefaultAsync",
        "Single", "SingleAsync", "SingleOrDefault", "SingleOrDefaultAsync",
        "Last", "LastAsync", "LastOrDefault", "LastOrDefaultAsync",
        "ToList", "ToListAsync", "ToArray", "ToArrayAsync",
        "ToDictionary", "ToDictionaryAsync", "ToHashSet", "ToHashSetAsync",
        "Any", "AnyAsync", "All", "AllAsync",
        "Count", "CountAsync", "LongCount", "LongCountAsync",
        "Sum", "SumAsync", "Max", "MaxAsync", "Min", "MinAsync", "Average", "AverageAsync",
        "AsNoTracking", "AsQueryable", "AsAsyncEnumerable", "Load", "LoadAsync"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ExcludedDbContextMembers = new[]
    {
        "Database", "ChangeTracker", "Model", "ContextId", "Entry", "Set"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static bool TryExtractEfCoreDbSetAccess(Node invocationNode, out string targetTableOrEntity, out bool isWrite)
    {
        targetTableOrEntity = string.Empty;
        isWrite = false;

        var func = invocationNode.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            return false;
        }

        var methodNode = func.GetField(TreeSitterSyntax.Fields.Name);
        if (!methodNode.IsValid()) return false;
        var methodName = methodNode.Text;
        var ltIdx = methodName.IndexOf('<');
        if (ltIdx > 0) methodName = methodName[..ltIdx].Trim();

        if (methodName == "Entry")
        {
            var parentAssign = invocationNode.Parent?.Parent;
            if (parentAssign.IsValid() &&
                parentAssign.Text.Contains("EntityState.", StringComparison.Ordinal) &&
                !parentAssign.Text.Contains("EntityState.Unchanged", StringComparison.Ordinal) &&
                !parentAssign.Text.Contains("EntityState.Detached", StringComparison.Ordinal))
            {
                var entityType = TryResolveFirstArgParameterType(invocationNode);
                if (!string.IsNullOrEmpty(entityType))
                {
                    targetTableOrEntity = entityType;
                    isWrite = true;
                    return true;
                }
            }
            return false;
        }

        var isWriteMethod = EfCoreWriteMethods.Contains(methodName);
        var isReadMethod = !isWriteMethod && EfCoreReadMethods.Contains(methodName);
        if (!isWriteMethod && !isReadMethod)
        {
            return false;
        }

        isWrite = isWriteMethod;

        var curr = func.GetField(TreeSitterSyntax.Fields.Expression);
        for (var depth = 0; depth < 12 && curr.IsValid(); depth++)
        {
            if (curr.Is(TreeSitterSyntax.CSharp.InvocationExpression))
            {
                var innerFunc = curr.GetFunctionNode();
                if (innerFunc.IsValid() && innerFunc.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
                {
                    var innerName = innerFunc.GetField(TreeSitterSyntax.Fields.Name);
                    var innerReceiver = innerFunc.GetField(TreeSitterSyntax.Fields.Expression);
                    if (innerName.IsValid() && innerName.Text.StartsWith("Set<", StringComparison.Ordinal) &&
                        IsLikelyDbContextReceiver(innerReceiver))
                    {
                        var gStart = innerName.Text.IndexOf('<');
                        var gEnd = innerName.Text.LastIndexOf('>');
                        if (gEnd > gStart + 1)
                        {
                            targetTableOrEntity = innerName.Text[(gStart + 1)..gEnd].Trim();
                            return !string.IsNullOrEmpty(targetTableOrEntity);
                        }
                    }
                    curr = innerReceiver;
                    continue;
                }
                break;
            }

            if (curr.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            {
                var propNode = curr.GetField(TreeSitterSyntax.Fields.Name);
                var objNode = curr.GetField(TreeSitterSyntax.Fields.Expression);
                if (propNode.IsValid() && IsLikelyDbContextReceiver(objNode))
                {
                    var propName = propNode.Text;
                    if (!ExcludedDbContextMembers.Contains(propName))
                    {
                        targetTableOrEntity = propName;
                        return true;
                    }
                }
                curr = objNode;
                continue;
            }

            if (isWriteMethod && IsLikelyDbContextReceiver(curr))
            {
                var entityType = TryResolveFirstArgParameterType(invocationNode);
                if (!string.IsNullOrEmpty(entityType))
                {
                    targetTableOrEntity = entityType;
                    return true;
                }
            }

            if (IsLikelyRepositoryReceiver(curr))
            {
                var repoEntity = TryResolveEnclosingRepositoryEntityType(invocationNode, curr)
                                 ?? (isWriteMethod ? TryResolveFirstArgParameterType(invocationNode) : null);
                if (!string.IsNullOrEmpty(repoEntity))
                {
                    targetTableOrEntity = repoEntity;
                    return true;
                }
            }

            break;
        }

        return false;
    }

    private static bool IsLikelyDbContextReceiver(Node? receiverNode)
    {
        if (!receiverNode.IsValid()) return false;
        var span = receiverNode.AsSpan().Trim();
        if (span.StartsWith("this.", StringComparison.Ordinal))
        {
            span = span[5..].Trim();
        }
        if (span.IsEmpty || span.Contains('(') || span.Contains('.')) return false;

        return span.Equals("context", StringComparison.OrdinalIgnoreCase) ||
               span.Equals("_context", StringComparison.OrdinalIgnoreCase) ||
               span.Equals("db", StringComparison.OrdinalIgnoreCase) ||
               span.Equals("_db", StringComparison.OrdinalIgnoreCase) ||
               span.Equals("dbContext", StringComparison.OrdinalIgnoreCase) ||
               span.Equals("_dbContext", StringComparison.OrdinalIgnoreCase) ||
               span.EndsWith("Context", StringComparison.OrdinalIgnoreCase) ||
               span.EndsWith("DbContext", StringComparison.OrdinalIgnoreCase);
    }

    private const string ThisDotPrefix = "this.";
    private const string RepoSuffix = "Repo";
    private const string RepositorySuffix = "Repository";
    private const char OpenAngle = '<';
    private const char CloseAngle = '>';
    private const char CommaChar = ',';

    private static bool IsLikelyRepositoryReceiver(Node? receiverNode)
    {
        if (!receiverNode.IsValid()) return false;
        var span = receiverNode.AsSpan().Trim();
        if (span.StartsWith(ThisDotPrefix, StringComparison.Ordinal))
        {
            span = span[ThisDotPrefix.Length..].Trim();
        }
        if (span.IsEmpty || span.Contains('(') || span.Contains('.')) return false;

        return span.EndsWith(RepositorySuffix, StringComparison.OrdinalIgnoreCase) ||
               span.EndsWith(RepoSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryResolveEnclosingRepositoryEntityType(Node invocationNode, Node receiverNode)
    {
        var recvSpan = receiverNode.AsSpan().Trim();
        if (recvSpan.StartsWith(ThisDotPrefix, StringComparison.Ordinal))
        {
            recvSpan = recvSpan[ThisDotPrefix.Length..].Trim();
        }
        if (recvSpan.IsEmpty) return null;

        var classDecl = GetParentClass(invocationNode);
        if (!classDecl.IsValid()) return null;

        // Check primary constructor parameters, constructor parameters, and fields for matching identifier or generic Repository<T>
        var headerSpan = classDecl.AsSpan();
        var recvName = recvSpan.ToString();
        var pattern = $@"(?:IRepository|IReadRepository|IAsyncRepository|RepositoryBase|Repository)\s*<\s*([A-Za-z_][A-Za-z0-9_.]*)\s*(?:,|>)\s*[^>]*?\b{System.Text.RegularExpressions.Regex.Escape(recvName)}\b";
        var match = System.Text.RegularExpressions.Regex.Match(headerSpan.ToString(), pattern);
        if (match.Success)
        {
            var entity = match.Groups[1].Value.Trim();
            var dot = entity.LastIndexOf('.');
            return dot >= 0 && dot + 1 < entity.Length ? entity[(dot + 1)..] : entity;
        }

        var fallbackMatch = System.Text.RegularExpressions.Regex.Match(
            headerSpan.ToString(),
            @"(?:IRepository|IReadRepository|IAsyncRepository|RepositoryBase)\s*<\s*([A-Za-z_][A-Za-z0-9_.]*)\s*(?:,|>)");
        if (fallbackMatch.Success)
        {
            var entity = fallbackMatch.Groups[1].Value.Trim();
            var dot = entity.LastIndexOf('.');
            return dot >= 0 && dot + 1 < entity.Length ? entity[(dot + 1)..] : entity;
        }

        return null;
    }

    private static string? TryResolveFirstArgParameterType(Node invocationNode)
    {
        var argList = invocationNode.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
        var firstArg = argList?.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
        if (!firstArg.IsValid()) return null;
        var argIdent = firstArg.AsSpan().Trim();
        if (argIdent.IsEmpty) return null;

        var curr = invocationNode.Parent;
        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.CSharp.MethodDeclaration, TreeSitterSyntax.Common.FunctionDeclaration))
            {
                var paramList = curr.FindChildOfType(TreeSitterSyntax.CSharp.ParameterList);
                if (paramList.IsValid())
                {
                    foreach (var p in paramList.FindChildrenOfType(TreeSitterSyntax.CSharp.Parameter))
                    {
                        var pName = p.GetField(TreeSitterSyntax.Fields.Name);
                        var pType = p.GetField(TreeSitterSyntax.Fields.Type);
                        if (pName.IsValid() && pType.IsValid() && pName.AsSpan().Trim().SequenceEqual(argIdent))
                        {
                            var tText = pType.Text.Trim().TrimEnd('?');
                            var lt = tText.IndexOf('<');
                            return lt > 0 ? tText[..lt].Trim() : tText;
                        }
                    }
                }
                break;
            }
            curr = curr.Parent;
        }
        return null;
    }
}
