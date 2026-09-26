using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class EfCoreLibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "Microsoft.EntityFrameworkCore";
    public string Id => "microsoft.entityframeworkcore";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.EntityFrameworkCore.*",
        "System.ComponentModel.DataAnnotations.Schema"
    ];

    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsTableAttribute(node) || IsDbSetProperty(node) || IsToTableCall(node))
        {
            return OntologyConstants.NodeLabels.Table;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
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
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
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
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
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

    public static string? FindConstantValueInClass(Node classNode, string? constName = null)
    {
        if (!classNode.IsValid()) return null;
        foreach (var field in classNode.FindChildrenOfType(TreeSitterSyntax.CSharp.FieldDeclaration))
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
                                var eqClause = declarator.FindChildOfType(TreeSitterSyntax.CSharp.EqualsValueClause);
                                if (eqClause.IsValid())
                                {
                                    var str = eqClause.Children.FirstOrDefault(c => c.Type.Contains("string"));
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
                            return strNode.Text.Trim('"');
                        }
                        var idNode = firstArg.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                        if (idNode.IsValid())
                        {
                            var parentClass = GetParentClass(node);
                            if (parentClass != null && parentClass.IsValid())
                            {
                                var constVal = FindConstantValueInClass(parentClass, idNode.Text);
                                if (!string.IsNullOrEmpty(constVal)) return constVal;
                            }
                            return idNode.Text;
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
        var named = args.FirstOrDefault(a => a.Text.StartsWith("schema:", StringComparison.OrdinalIgnoreCase));
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
                if (arg.Text.StartsWith("Schema", StringComparison.OrdinalIgnoreCase))
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
}
