using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class SvelteLibraryParser : ISemanticExtension
{
    public string Id => "svelte";
    public string Name => "Svelte";
    public string Type => "framework";
    public LibraryRole LibraryRole => LibraryRole.FrontendFramework;
    public IReadOnlyList<string> SupportedPatterns => ["svelte", "svelte/*"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsSvelteMount(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsSvelteMount(node))
        {
            return "Svelte:Mount";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsSvelteMount(node))
        {
            ExtractSvelteRootReferences(node, scopeSymbolId, references);
        }
    }

    private static bool IsSvelteMount(Node node)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var func = node.GetFunctionNode();
            var name = func.IsValid() ? func.Text : "";
            if (name is "mount" or "hydrate" ||
                name.EndsWith(".mount", StringComparison.Ordinal) ||
                name.EndsWith(".hydrate", StringComparison.Ordinal))
            {
                var args = AstHelper.GetCallArguments(node);
                if (args.Count >= 2 && args[1].Is(TreeSitterSyntax.TypeScript.Object))
                {
                    if (AstHelper.TryGetObjectProperty(args[1], "target", out _))
                    {
                        return true;
                    }
                }
                else if (args.Count >= 1)
                {
                    return true;
                }
            }
        }
        else if (node.Is(TreeSitterSyntax.TypeScript.NewExpression))
        {
            var args = AstHelper.GetCallArguments(node);
            if (args.Count > 0 && args[0].Is(TreeSitterSyntax.TypeScript.Object))
            {
                if (AstHelper.TryGetObjectProperty(args[0], "target", out _))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void ExtractSvelteRootReferences(Node node, string scopeSymbolId, List<Reference> references)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var args = AstHelper.GetCallArguments(node);
            if (args.Count > 0 && args[0].Is(TreeSitterSyntax.TypeScript.Identifier))
            {
                var rootName = args[0].Text;
                if (!string.IsNullOrEmpty(rootName))
                {
                    references.Add(new Reference(scopeSymbolId, rootName, OntologyConstants.Relationships.DependsOn));
                }
            }
        }
        else if (node.Is(TreeSitterSyntax.TypeScript.NewExpression))
        {
            var ctor = node.GetField("constructor") ?? node.Children.FirstOrDefault(c => c.IsValid() && c.Type != "new");
            if (ctor.IsValid())
            {
                var ctorName = ctor.Text;
                if (!string.IsNullOrEmpty(ctorName))
                {
                    references.Add(new Reference(scopeSymbolId, ctorName, OntologyConstants.Relationships.DependsOn));
                }
            }
        }
    }
}
