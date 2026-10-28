using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class AngularCoreLibraryParser : ISemanticExtension
{
    public string Id => "angular";
    public string Name => "Angular";
    public string Type => "framework";
    public LibraryRole LibraryRole => LibraryRole.FrontendFramework;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "@angular/core",
        "@angular/core/*",
        "@angular/router",
        "@angular/router/*"
    ];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsAngularBootstrap(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }

        if (IsAngularRouteServiceItem(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }

        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName)
    {
        if (IsAngularBootstrap(node))
        {
            return "Angular:Bootstrap";
        }

        if (IsAngularRouteServiceItem(node))
        {
            var projectContext = projectName ?? ctx?.WorkspaceId;
            var resolved = AstHelper.ResolveStringOrTemplate(node, projectContext);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                var clean = resolved.Trim().Trim('\'', '"', '`');
                return clean.StartsWith('/') ? clean : $"/{clean}";
            }
            var text = node.Text.Trim().Trim('\'', '"', '`');
            return text.StartsWith('/') ? text : $"/{text}";
        }

        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsAngularBootstrap(node))
        {
            ExtractBootstrapReferences(node, scopeSymbolId, references);
        }
        else if (IsComponentOrModuleDecorator(node))
        {
            ExtractDecoratorReferences(node, scopeSymbolId, references);
        }
        else if (node.IsAny(TreeSitterSyntax.TypeScript.ClassDeclaration, TreeSitterSyntax.TypeScript.ClassExpression))
        {
            var decorators = node.FindChildrenOfType(TreeSitterSyntax.TypeScript.Decorator).ToList();
            if (node.Parent.IsValid() && node.Parent.Is(TreeSitterSyntax.TypeScript.ExportStatement))
            {
                decorators.AddRange(node.Parent.FindChildrenOfType(TreeSitterSyntax.TypeScript.Decorator));
            }
            foreach (var dec in decorators)
            {
                if (IsComponentOrModuleDecorator(dec))
                {
                    ExtractDecoratorReferences(dec, scopeSymbolId, references);
                }
            }
        }
    }

    private static bool IsAngularBootstrap(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        var funcName = func.Text;
        if (funcName is "bootstrapApplication" or "bootstrapModule" ||
            funcName.EndsWith(".bootstrapApplication", StringComparison.Ordinal) ||
            funcName.EndsWith(".bootstrapModule", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static bool IsComponentOrModuleDecorator(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.Decorator)) return false;

        if (TryGetDecoratorNameAndCall(node, out var decName, out _))
        {
            return decName is "Component" or "NgModule";
        }

        return false;
    }

    private static string ExtractDecoratorIdentifier(Node decoratorNode)
    {
        if (TryGetDecoratorNameAndCall(decoratorNode, out var decName, out var callExpr))
        {
            if (decName == "Component" && callExpr != null && callExpr.IsValid())
            {
                var args = AstHelper.GetCallArguments(callExpr);
                if (args.Count > 0 && args[0].Is(TreeSitterSyntax.TypeScript.Object))
                {
                    if (AstHelper.TryGetObjectProperty(args[0], "selector", out var selNode) && selNode.IsValid())
                    {
                        var selector = AstHelper.ResolveStringOrTemplate(selNode);
                        if (!string.IsNullOrEmpty(selector))
                        {
                            return $"Component:{selector}";
                        }
                    }
                }
                return "Component";
            }

            if (decName == "NgModule")
            {
                return "NgModule";
            }
        }

        return "Component";
    }

    private static void ExtractBootstrapReferences(Node node, string scopeSymbolId, List<Reference> references)
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

    private static void ExtractDecoratorReferences(Node decoratorNode, string scopeSymbolId, List<Reference> references)
    {
        if (!TryGetDecoratorNameAndCall(decoratorNode, out var decName, out var callExpr) || callExpr == null || !callExpr.IsValid())
        {
            return;
        }

        var args = AstHelper.GetCallArguments(callExpr);
        if (args.Count == 0 || !args[0].Is(TreeSitterSyntax.TypeScript.Object))
        {
            return;
        }

        var configObj = args[0];

        // 1. templateUrl: './app.component.html'
        if (AstHelper.TryGetObjectProperty(configObj, "templateUrl", out var tmplNode) && tmplNode.IsValid())
        {
            var tmplPath = tmplNode.Text.Trim('\'', '"', '`');
            if (!string.IsNullOrEmpty(tmplPath))
            {
                references.Add(new Reference(scopeSymbolId, tmplPath, OntologyConstants.Relationships.DependsOn));
            }
        }

        // 2. imports: [CommonModule, NavComponent], declarations: [...], bootstrap: [...]
        string[] arrayProps = ["imports", "declarations", "bootstrap"];
        foreach (var prop in arrayProps)
        {
            if (AstHelper.TryGetObjectProperty(configObj, prop, out var arrNode) && arrNode.IsValid() &&
                arrNode.Is(TreeSitterSyntax.TypeScript.Array))
            {
                foreach (var child in arrNode.Children)
                {
                    if (child.Is(TreeSitterSyntax.TypeScript.Identifier))
                    {
                        var name = child.Text;
                        if (!string.IsNullOrEmpty(name))
                        {
                            references.Add(new Reference(scopeSymbolId, name, OntologyConstants.Relationships.DependsOn));
                        }
                    }
                }
            }
        }
    }

    private static bool TryGetDecoratorNameAndCall(Node decoratorNode, out string decoratorName, out Node? callExpr)
    {
        decoratorName = string.Empty;
        callExpr = null;

        if (!decoratorNode.Is(TreeSitterSyntax.TypeScript.Decorator)) return false;

        var call = decoratorNode.FindChildOfType(TreeSitterSyntax.TypeScript.CallExpression);
        if (call.IsValid())
        {
            callExpr = call;
            var func = call.GetFunctionNode();
            if (func.IsValid())
            {
                decoratorName = func.Text.Trim();
                return true;
            }
        }

        foreach (var child in decoratorNode.Children)
        {
            if (child.Is(TreeSitterSyntax.TypeScript.Identifier))
            {
                decoratorName = child.Text.Trim();
                return true;
            }
        }

        return false;
    }

    private static bool IsAngularRouteServiceItem(Node node)
    {
        if (!node.IsNamed)
            return false;

        var text = node.Text.Trim('\'', '"', '`');
        if (string.IsNullOrWhiteSpace(text) || text is "[" or "]" or ",")
            return false;

        var parent = node.Parent;
        if (!parent.IsValid() || !parent.Is(TreeSitterSyntax.TypeScript.Array))
            return false;

        var pair = parent.Parent;
        if (!pair.IsValid() || !pair.Is(TreeSitterSyntax.TypeScript.Pair))
            return false;

        var key = pair.GetField(TreeSitterSyntax.Fields.Key);
        if (!key.IsValid()) return false;

        var keyText = key.Text.Trim('\'', '"', '`');
        if (!keyText.Equals("services", StringComparison.OrdinalIgnoreCase) &&
            !keyText.Equals("requiredServices", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var enclosingObj = pair.Parent;
        if (!enclosingObj.IsValid() || !enclosingObj.Is(TreeSitterSyntax.TypeScript.Object))
            return false;

        if (AstHelper.TryGetObjectProperty(enclosingObj, "path", out _) ||
            AstHelper.TryGetObjectProperty(enclosingObj, "roles", out _))
        {
            return true;
        }

        var parentPair = enclosingObj.Parent;
        if (parentPair.IsValid() && parentPair.Is(TreeSitterSyntax.TypeScript.Pair))
        {
            var parentKey = parentPair.GetField(TreeSitterSyntax.Fields.Key);
            if (parentKey.IsValid() && parentKey.Text.Trim('\'', '"', '`').Equals("data", StringComparison.OrdinalIgnoreCase))
            {
                var routeObj = parentPair.Parent;
                if (routeObj.IsValid() && (AstHelper.TryGetObjectProperty(routeObj, "path", out _) ||
                                           AstHelper.TryGetObjectProperty(routeObj, "component", out _) ||
                                           AstHelper.TryGetObjectProperty(routeObj, "loadChildren", out _) ||
                                           AstHelper.TryGetObjectProperty(routeObj, "loadComponent", out _)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
