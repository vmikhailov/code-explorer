using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class ReactLibraryParser : ISemanticExtension
{
    public string Id => "react";
    public string Name => "React";
    public string Type => "framework";
    public LibraryRole LibraryRole => LibraryRole.FrontendFramework;
    public IReadOnlyList<string> SupportedPatterns => ["react", "react-dom", "react-dom/*", "react/*"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsReactRootMount(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsReactRootMount(node))
        {
            return "React:Root";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsReactRootMount(node))
        {
            ExtractRootComponentReferences(node, scopeSymbolId, references);
        }
        else if (IsLazyImport(node, out var importTarget))
        {
            if (!string.IsNullOrEmpty(importTarget))
            {
                references.Add(new Reference(scopeSymbolId, importTarget, OntologyConstants.Relationships.DependsOn));
            }
        }
    }

    private static bool IsReactRootMount(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        if (func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var prop = func.GetField(TreeSitterSyntax.Fields.Property);
            var propName = prop.IsValid() ? prop.Text : "";
            if (propName is "render" or "hydrate")
            {
                var obj = func.GetField(TreeSitterSyntax.Fields.Object);
                if (obj.IsValid())
                {
                    if (obj.Is(TreeSitterSyntax.TypeScript.CallExpression))
                    {
                        var innerFunc = obj.GetFunctionNode();
                        var innerName = innerFunc.IsValid() ? innerFunc.Text : "";
                        if (innerName is "createRoot" or "hydrateRoot" ||
                            innerName.EndsWith(".createRoot", StringComparison.Ordinal) ||
                            innerName.EndsWith(".hydrateRoot", StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                    else if (obj.Text is "ReactDOM" or "Client" or "root" ||
                             obj.Text.EndsWith("root", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }
        else if (func.Text is "createRoot" or "hydrateRoot" ||
                 func.Text.EndsWith(".createRoot", StringComparison.Ordinal) ||
                 func.Text.EndsWith(".hydrateRoot", StringComparison.Ordinal))
        {
            if (node.Parent.IsValid() &&
                (node.Parent.Is(TreeSitterSyntax.TypeScript.MemberExpression) ||
                 node.Parent.Is(TreeSitterSyntax.TypeScript.VariableDeclarator)))
            {
                return false;
            }
            return true;
        }

        return false;
    }

    private static void ExtractRootComponentReferences(Node node, string scopeSymbolId, List<Reference> references)
    {
        // Search inside arguments for JSX elements, createElement calls, or component identifiers
        var args = AstHelper.GetCallArguments(node);
        foreach (var arg in args)
        {
            if (arg.Is(TreeSitterSyntax.TypeScript.Identifier) &&
                !string.IsNullOrEmpty(arg.Text) &&
                char.IsUpper(arg.Text[0]))
            {
                references.Add(new Reference(scopeSymbolId, arg.Text, OntologyConstants.Relationships.DependsOn));
            }
            else
            {
                FindJsxOrComponentReferences(arg, scopeSymbolId, references);
            }
        }
    }

    private static void FindJsxOrComponentReferences(Node node, string scopeSymbolId, List<Reference> references)
    {
        if (!node.IsValid()) return;

        if (node.Type is "jsx_opening_element" or "jsx_self_closing_element")
        {
            var nameNode = node.GetField(TreeSitterSyntax.Fields.Name) ??
                           node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.Identifier));

            if (nameNode.IsValid())
            {
                var tagName = nameNode.Text;
                if (!string.IsNullOrEmpty(tagName) &&
                    char.IsUpper(tagName[0]) &&
                    tagName is not ("StrictMode" or "Fragment"))
                {
                    references.Add(new Reference(scopeSymbolId, tagName, OntologyConstants.Relationships.DependsOn));
                }
            }
        }
        else if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var func = node.GetFunctionNode();
            var funcName = func.IsValid() ? func.Text : "";
            if (funcName is "createElement" || funcName.EndsWith(".createElement", StringComparison.Ordinal))
            {
                var callArgs = AstHelper.GetCallArguments(node);
                if (callArgs.Count > 0 && callArgs[0].Is(TreeSitterSyntax.TypeScript.Identifier))
                {
                    var compName = callArgs[0].Text;
                    if (!string.IsNullOrEmpty(compName) && char.IsUpper(compName[0]))
                    {
                        references.Add(new Reference(scopeSymbolId, compName, OntologyConstants.Relationships.DependsOn));
                    }
                }
            }
        }

        foreach (var child in node.Children)
        {
            FindJsxOrComponentReferences(child, scopeSymbolId, references);
        }
    }

    private static bool IsLazyImport(Node node, out string? importTarget)
    {
        importTarget = null;
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        var func = node.GetFunctionNode();
        var funcName = func.IsValid() ? func.Text : "";
        if (funcName is not ("lazy" or "React.lazy")) return false;

        var queue = new Queue<Node>();
        queue.Enqueue(node);
        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            if (curr.Is(TreeSitterSyntax.TypeScript.CallExpression) && curr != node)
            {
                var callFunc = curr.GetFunctionNode();
                var callName = callFunc.IsValid() ? callFunc.Text : "";
                if (callName == "import" || curr.Text.StartsWith("import("))
                {
                    var target = AstHelper.ExtractFirstStringArgument(curr);
                    if (!string.IsNullOrEmpty(target))
                    {
                        importTarget = target;
                        return true;
                    }
                }
            }
            foreach (var child in curr.Children)
            {
                if (child.IsValid()) queue.Enqueue(child);
            }
        }

        return false;
    }
}
