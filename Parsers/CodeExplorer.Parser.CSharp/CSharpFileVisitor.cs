using System;
using System.Collections.Generic;
using System.Linq;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp;

public class CSharpFileVisitor : BaseParserVisitor
{
    private readonly CSharpParser _parser;

    public CSharpFileVisitor(
        Node rootNode,
        List<ILibraryParser> activeLibraryParsers,
        CSharpParser parser,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        LibraryTrieRegistry libraryRegistry)
        : base(rootNode, activeLibraryParsers, relativePath, absoluteWorkspacePath, fileParser, libraryRegistry)
    {
        _parser = parser;
    }

    protected override string? MapNodeType(Node node)
    {
        if (node.Type == "attribute")
        {
            var nameNode = node.Children.FirstOrDefault(c => c.Type == "identifier");
            if (nameNode != null && (nameNode.Text == "Route" || nameNode.Text.StartsWith("Http")))
            {
                return OntologyConstants.NodeLabels.EntryPoint;
            }
        }

        if (IsHttpClientCall(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }

        if (node.Type.Contains("string") &&
            node.Type != "interpolated_string_expression" &&
            node.Type != "interpolated_verbatim_string_expression" &&
            node.Type != "interpolated_raw_string_expression")
        {
            if (NestedSqlParser.TryParseSql(node.Text, out _, out _))
            {
                return OntologyConstants.NodeLabels.Query;
            }
        }

        return node.Type switch
        {
            "class_declaration" or "struct_declaration" or "record_declaration" => "Class",
            "interface_declaration" => "Interface",
            "method_declaration" or "function_declaration" or "constructor_declaration" or "local_function_statement" => OntologyConstants.NodeLabels.Function,
            _ => null
        };
    }

    protected override string? ExtractIdentifier(Node node)
    {
        if (node.Type == "attribute")
        {
            return ExtractCSharpAttributeRoute(node);
        }

        if (IsHttpClientCall(node))
        {
            return ExtractHttpClientTarget(node);
        }

        if (node.Type.Contains("string"))
        {
            if (NestedSqlParser.TryParseSql(node.Text, out var firstWord, out _))
            {
                return $"{firstWord} Query";
            }
        }

        return ExtractCsIdentifier(node);
    }

    private static bool IsHttpClientCall(Node node)
    {
        if (node.Type != "invocation_expression") return false;
        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "member_access_expression")
        {
            var nameChild = func.GetChildForField("name");
            if (nameChild != null && nameChild.Id != IntPtr.Zero)
            {
                var methodName = nameChild.Text;
                return methodName is "GetAsync" or "PostAsync" or "PutAsync" or "DeleteAsync" or "SendAsync" or "PostAsJsonAsync" or "GetFromJsonAsync";
            }
        }
        return false;
    }

    private static string? ExtractHttpClientTarget(Node node)
    {
        var argList = node.Children.FirstOrDefault(c => c.Type == "argument_list");
        if (argList != null && argList.Children.Count > 1)
        {
            var arg = argList.Children.FirstOrDefault(c => c.Type == "argument");
            if (arg != null)
            {
                var valNode = arg.Children.FirstOrDefault();
                if (valNode != null)
                {
                    var text = valNode.Text.Trim('"');
                    if (text.Contains("://"))
                    {
                        try
                        {
                            var uri = new Uri(text);
                            return $"http:{uri.Host}";
                        }
                        catch
                        {
                        }
                    }
                    return $"http:{text}";
                }
            }
        }
        return "http:unknown-service";
    }

    private static string? ExtractCSharpAttributeRoute(Node attributeNode)
    {
        var nameNode = attributeNode.Children.FirstOrDefault(c => c.Type == "identifier");
        if (nameNode == null) return null;
        var name = nameNode.Text;
        if (name != "Route" && name != "HttpGet" && name != "HttpPost" && name != "HttpPut" && name != "HttpDelete" && name != "HttpPatch")
        {
            return null;
        }

        var argList = attributeNode.Children.FirstOrDefault(c => c.Type == "attribute_argument_list");
        var routeVal = "/";
        if (argList != null)
        {
            var arg = argList.Children.FirstOrDefault(c => c.Type == "attribute_argument");
            if (arg != null)
            {
                var strNode = arg.Children.FirstOrDefault(c => c.Type.Contains("string"));
                if (strNode != null)
                {
                    routeVal = strNode.Text.Trim('"');
                }
            }
        }

        var method = name == "Route" ? "GET" : name.Replace("Http", "").ToUpperInvariant();
        return $"{method}:{routeVal}";
    }

    private string? ExtractCsIdentifier(Node node)
    {
        var nameNode = node.GetChildForField("name");
        if (nameNode != null && nameNode.Id != IntPtr.Zero)
        {
            return nameNode.Text;
        }

        foreach (var child in node.Children)
        {
            if (child.Type is "identifier" or "variable_name")
            {
                return child.Text;
            }
        }

        foreach (var child in node.Children)
        {
            if (child.Type.Contains("name"))
            {
                return child.Text;
            }
        }

        return null;
    }

    protected override void VisitVariableDeclaration(Node node, int depth)
    {
        if (node.Type == "variable_declaration")
        {
            var typeNode = node.GetChildForField("type");
            if (typeNode != null && typeNode.Id != IntPtr.Zero)
            {
                var typeName = typeNode.Text;
                foreach (var declarator in node.Children.Where(c => c.Type == "variable_declarator"))
                {
                    var nameNode = declarator.GetChildForField("name");
                    if (nameNode != null && nameNode.Id != IntPtr.Zero)
                    {
                        var varName = nameNode.Text;
                        var resolvedTypeName = typeName;
                        if (typeName == "var")
                        {
                            var valueNode = declarator.GetChildForField("value") ?? declarator.Children.FirstOrDefault(c => c.Type == "equals_value_clause")?.Children.ElementAtOrDefault(1);
                            if (valueNode != null && valueNode.Type == "object_creation_expression")
                            {
                                var objectTypeNode = valueNode.GetChildForField("type") ?? valueNode.Children.FirstOrDefault(c => c.Type is "type_identifier" or "identifier");
                                if (objectTypeNode != null)
                                {
                                    resolvedTypeName = objectTypeNode.Text;
                                }
                            }
                        }

                        if (resolvedTypeName != "var")
                        {
                            var scopeName = GetContainingScopeName(node);
                            RawTypeBindings.Add(new RawTypeBinding(varName, resolvedTypeName, "", scopeName));
                        }
                    }
                }
            }
            VisitChildren(node, depth);
        }
        else
        {
            CollectVariable(node);
            VisitChildren(node, depth);
        }
    }

    protected override void VisitParameter(Node node, int depth)
    {
        var typeNode = node.GetChildForField("type");
        var nameNode = node.GetChildForField("name") ?? node.Children.FirstOrDefault(c => c.Type == "identifier");
        if (typeNode != null && nameNode != null && typeNode.Id != IntPtr.Zero && nameNode.Id != IntPtr.Zero)
        {
            var scopeName = GetContainingScopeName(node);
            RawTypeBindings.Add(new RawTypeBinding(nameNode.Text, typeNode.Text, "", scopeName));
            RawTypeBindings.Add(new RawTypeBinding("this." + nameNode.Text, typeNode.Text, "", scopeName));
        }
        VisitChildren(node, depth);
    }

    protected override void VisitImportStatement(Node node, int depth)
    {
        var nameNode = node.GetChildForField("name") ?? node.Children.FirstOrDefault(c => c.Type is "qualified_name" or "identifier");
        if (nameNode != null && nameNode.Id != IntPtr.Zero)
        {
            var importPath = nameNode.Text;
            RawImports.Add(new RawImport(importPath, "", ImportType.External));
            ResolveAndInjectLibraryParser(importPath);
        }
        VisitChildren(node, depth);
    }

    protected override string? FindCallName(Node callNode)
    {
        var expr = callNode.GetChildForField("function");
        if (expr != null && expr.Id == IntPtr.Zero && callNode.Children.Count > 0)
        {
            expr = callNode.Children[0];
        }
        if (expr == null || expr.Id == IntPtr.Zero) return null;

        if (expr.Type == "identifier")
        {
            return expr.Text;
        }
        if (expr.Type == "member_access_expression")
        {
            var nameChild = expr.GetChildForField("name");
            var expressionChild = expr.GetChildForField("expression");
            if (nameChild != null && nameChild.Id != IntPtr.Zero)
            {
                if (expressionChild != null && expressionChild.Id != IntPtr.Zero)
                {
                    return $"{expressionChild.Text}.{nameChild.Text}";
                }
                return nameChild.Text;
            }
        }
        return null;
    }

    protected override void VisitInheritanceClause(Node node, int depth)
    {
        var currentScope = SymbolStack.Peek();
        if (currentScope.Kind != "file")
        {
            foreach (var child in node.Children)
            {
                if (child.Type.Contains("identifier") || child.Type.Contains("name"))
                {
                    var baseName = child.Text;
                    var refKind = baseName.StartsWith('I') && baseName.Length > 1 && char.IsUpper(baseName[1])
                        ? "IMPLEMENTS"
                        : "INHERITS_FROM";
                    currentScope.References.Add(new Reference("", baseName, refKind));
                }
            }
        }
        VisitChildren(node, depth);
    }

    private void CollectVariable(Node node)
    {
        var name = node.GetChildForField("name")?.Text;
        if (string.IsNullOrEmpty(name))
        {
            name = node.Children.FirstOrDefault(c => c.Type == "identifier")?.Text;
        }

        if (!string.IsNullOrEmpty(name))
        {
            var valueNode = node.GetChildForField("value");
            if (valueNode == null || valueNode.Id == IntPtr.Zero)
            {
                var eqClause = node.Children.FirstOrDefault(c => c.Type == "equals_value_clause");
                if (eqClause != null && eqClause.Children.Count > 1)
                {
                    valueNode = eqClause.Children[1];
                }
            }
            var initializerText = valueNode != null && valueNode.Id != IntPtr.Zero ? valueNode.Text : "";
            var isConstant = IsCSharpConstant(node);
            var scope = DetermineCSharpScope(node);

            RawVariables.Add(new RawVariable(
                name,
                initializerText,
                scope,
                isConstant,
                "",
                node.StartPosition.Row,
                node.EndPosition.Row,
                node.StartPosition.Column,
                node.EndPosition.Column
            ));

            if (node.Type == "property_declaration")
            {
                var typeNode = node.GetChildForField("type");
                if (typeNode != null && typeNode.Id != IntPtr.Zero)
                {
                    var scopeName = GetContainingScopeName(node);
                    RawTypeBindings.Add(new RawTypeBinding(name, typeNode.Text, "", scopeName));
                    RawTypeBindings.Add(new RawTypeBinding("this." + name, typeNode.Text, "", scopeName));
                }
            }
        }
    }

    private static bool IsCSharpConstant(Node node)
    {
        var curr = node;
        while (curr != null && curr.Id != IntPtr.Zero)
        {
            if (curr.Type is "field_declaration" or "local_declaration_statement")
            {
                foreach (var child in curr.Children)
                {
                    if (child.Type is "const" or "readonly" || child.Text is "const" or "readonly")
                        return true;
                }
            }
            curr = curr.Parent;
        }
        return false;
    }

    private static string DetermineCSharpScope(Node node)
    {
        var curr = node.Parent;
        while (curr != null && curr.Id != IntPtr.Zero)
        {
            if (curr.Type is "class_declaration" or "struct_declaration" or "record_declaration" or "interface_declaration")
                return "class";
            if (curr.Type is "method_declaration" or "local_function_statement" or "block" or "constructor_declaration")
                return "local";
            curr = curr.Parent;
        }
        return "global";
    }

    private static string GetContainingScopeName(Node node)
    {
        var curr = node.Parent;
        while (curr != null && curr.Id != IntPtr.Zero)
        {
            if (curr.Type is "class_declaration" or "interface_declaration" or "struct_declaration" or "record_declaration")
            {
                var nameNode = curr.GetChildForField("name");
                if (nameNode != null && nameNode.Id != IntPtr.Zero) return nameNode.Text;
            }
            else if (curr.Type is "method_declaration" or "function_declaration" or "constructor_declaration" or "local_function_statement")
            {
                var nameNode = curr.GetChildForField("name");
                if (nameNode != null && nameNode.Id != IntPtr.Zero) return nameNode.Text;
            }
            curr = curr.Parent;
        }
        return "global";
    }
}
