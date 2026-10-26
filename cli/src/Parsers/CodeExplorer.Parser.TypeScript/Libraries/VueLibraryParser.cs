using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class VueLibraryParser : ISemanticExtension
{
    public string Id => "vue";
    public string Name => "Vue";
    public string Type => "framework";
    public LibraryRole LibraryRole => LibraryRole.FrontendFramework;
    public IReadOnlyList<string> SupportedPatterns => ["vue", "vue/*"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsVueRootMount(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsVueRootMount(node))
        {
            return "Vue:Root";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsVueRootMount(node))
        {
            ExtractVueRootReferences(node, scopeSymbolId, references);
        }
        else if (IsAsyncComponent(node, out var importTarget))
        {
            if (!string.IsNullOrEmpty(importTarget))
            {
                references.Add(new Reference(scopeSymbolId, importTarget, OntologyConstants.Relationships.DependsOn));
            }
        }
    }

    private static bool IsVueRootMount(Node node)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var func = node.GetFunctionNode();
            if (func.IsValid() && func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
            {
                var prop = func.GetField(TreeSitterSyntax.Fields.Property);
                var propName = prop.IsValid() ? prop.Text : "";
                if (propName is "mount")
                {
                    var obj = func.GetField(TreeSitterSyntax.Fields.Object);
                    if (obj.IsValid() && obj.Is(TreeSitterSyntax.TypeScript.CallExpression))
                    {
                        var innerFunc = obj.GetFunctionNode();
                        var innerName = innerFunc.IsValid() ? innerFunc.Text : "";
                        if (innerName is "createApp" or "createSSRApp" ||
                            innerName.EndsWith(".createApp", StringComparison.Ordinal) ||
                            innerName.EndsWith(".createSSRApp", StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }
            }
            else if (func.IsValid() && func.Text is "createApp" or "createSSRApp")
            {
                if (node.Parent.IsValid() && node.Parent.Is(TreeSitterSyntax.TypeScript.MemberExpression))
                {
                    return false;
                }
                return true;
            }
        }
        else if (node.Is(TreeSitterSyntax.TypeScript.NewExpression))
        {
            var ctor = node.GetField("constructor") ?? node.Children.FirstOrDefault(c => c.IsValid() && c.Type != "new");
            if (ctor.IsValid() && ctor.Text == "Vue")
            {
                return true;
            }
        }

        return false;
    }

    private static void ExtractVueRootReferences(Node node, string scopeSymbolId, List<Reference> references)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var func = node.GetFunctionNode();
            Node? createAppCall = null;

            if (func.IsValid() && func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
            {
                var obj = func.GetField(TreeSitterSyntax.Fields.Object);
                if (obj.IsValid() && obj.Is(TreeSitterSyntax.TypeScript.CallExpression))
                {
                    createAppCall = obj;
                }
            }
            else
            {
                createAppCall = node;
            }

            if (createAppCall != null && createAppCall.IsValid())
            {
                var args = AstHelper.GetCallArguments(createAppCall);
                if (args.Count > 0 && args[0].Is(TreeSitterSyntax.TypeScript.Identifier))
                {
                    var compName = args[0].Text;
                    if (!string.IsNullOrEmpty(compName) && compName != "undefined")
                    {
                        references.Add(new Reference(scopeSymbolId, compName, OntologyConstants.Relationships.DependsOn));
                    }
                }
            }
        }
        else if (node.Is(TreeSitterSyntax.TypeScript.NewExpression))
        {
            var args = AstHelper.GetCallArguments(node);
            if (args.Count > 0 && args[0].Is(TreeSitterSyntax.TypeScript.Object))
            {
                if (AstHelper.TryGetObjectProperty(args[0], "render", out var renderFn) && renderFn.IsValid())
                {
                    foreach (var id in renderFn.FindChildrenOfType(TreeSitterSyntax.TypeScript.Identifier))
                    {
                        if (char.IsUpper(id.Text[0]))
                        {
                            references.Add(new Reference(scopeSymbolId, id.Text, OntologyConstants.Relationships.DependsOn));
                        }
                    }
                }
            }
        }
    }

    private static bool IsAsyncComponent(Node node, out string? importTarget)
    {
        importTarget = null;
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        var func = node.GetFunctionNode();
        var funcName = func.IsValid() ? func.Text : "";
        if (funcName is not ("defineAsyncComponent" or "Vue.defineAsyncComponent")) return false;

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
