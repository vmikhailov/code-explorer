using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class VueLibraryParser : DeclarativeSemanticExtension
{
    public override string Id => "vue";
    public override string Name => "Vue";
    public override SemanticExtensionType Type => SemanticExtensionType.Framework;
    public override LibraryRole LibraryRole => LibraryRole.FrontendFramework;
    public override IReadOnlyList<string> SupportedPatterns => ["vue", "vue/*"];

    private static readonly IReadOnlyList<CustomAstRule> Rules =
    [
        new CustomAstRule
        {
            NodeTypes = [TreeSitterSyntax.TypeScript.CallExpression, TreeSitterSyntax.TypeScript.NewExpression],
            MapNodeType = static (node, ctx) => IsVueRootMount(node) ? OntologyConstants.NodeLabels.EntryPoint : null,
            ExtractIdentifier = static (node, ctx, _, _) => IsVueRootMount(node) ? "Vue:Root" : null,
            CollectReferences = CollectReferencesCore
        }
    ];

    public override IReadOnlyList<CustomAstRule> CustomAstRules => Rules;

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
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
                        references.Add(new Reference(scopeSymbolId, compName, OntologyConstants.Relationships.Triggers));
                        references.Add(new Reference("", compName, OntologyConstants.Relationships.Calls));
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
                            references.Add(new Reference(scopeSymbolId, id.Text, OntologyConstants.Relationships.Triggers));
                            references.Add(new Reference("", id.Text, OntologyConstants.Relationships.Calls));
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
