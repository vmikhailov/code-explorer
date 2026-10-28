using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class ViteLibraryParser : ISemanticExtension
{
    public string Id => "vite";
    public string Name => "Vite";
    public string Type => "framework";
    public LibraryRole LibraryRole => LibraryRole.FrontendFramework;
    public IReadOnlyList<string> SupportedPatterns => ["vite"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsViteConfig(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsViteConfig(node))
        {
            return "Vite:Config";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsViteConfig(node))
        {
            ExtractVitePlugins(node, scopeSymbolId, references);
        }
    }

    private static bool IsViteConfig(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;
        var name = func.Text;
        return name is "defineConfig" || name.EndsWith(".defineConfig", StringComparison.Ordinal);
    }

    private static void ExtractVitePlugins(Node node, string scopeSymbolId, List<Reference> references)
    {
        var args = AstHelper.GetCallArguments(node);
        if (args.Count == 0) return;

        Node? configObj = null;
        if (args[0].Is(TreeSitterSyntax.TypeScript.Object))
        {
            configObj = args[0];
        }
        else if (args[0].Is(TreeSitterSyntax.TypeScript.ArrowFunction) ||
                 args[0].Is(TreeSitterSyntax.TypeScript.FunctionExpression))
        {
            // E.g. defineConfig(async ({ mode }) => ({ plugins: [react()] }))
            var body = args[0].GetField(TreeSitterSyntax.Fields.Body);
            if (body.IsValid())
            {
                configObj = body.FindChildOfType(TreeSitterSyntax.TypeScript.Object);
            }
        }

        if (configObj != null && configObj.IsValid())
        {
            if (AstHelper.TryGetObjectProperty(configObj, "plugins", out var pluginsArr) &&
                pluginsArr.IsValid() && pluginsArr.Is(TreeSitterSyntax.TypeScript.Array))
            {
                foreach (var child in pluginsArr.Children)
                {
                    if (child.Is(TreeSitterSyntax.TypeScript.CallExpression))
                    {
                        var pluginFunc = child.GetFunctionNode();
                        if (pluginFunc.IsValid())
                        {
                            var pluginName = pluginFunc.Text;
                            if (!string.IsNullOrEmpty(pluginName))
                            {
                                references.Add(new Reference(scopeSymbolId, pluginName, OntologyConstants.Relationships.DependsOn));
                            }
                        }
                    }
                    else if (child.Is(TreeSitterSyntax.TypeScript.Identifier))
                    {
                        var pluginName = child.Text;
                        if (!string.IsNullOrEmpty(pluginName))
                        {
                            references.Add(new Reference(scopeSymbolId, pluginName, OntologyConstants.Relationships.DependsOn));
                        }
                    }
                }
            }
        }
    }
}
