using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class CypressLibraryParser : ISemanticExtension
{
    public string Id => "cypress";
    public string Name => "Cypress";
    public string Type => "library";
    public LibraryRole LibraryRole => LibraryRole.TestFramework;
    public IReadOnlyList<string> SupportedPatterns => ["cypress"];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;

    public IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor("cypress", "Cypress", LibraryRole.TestFramework, "library", ["cypress"]);
    }

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsCyExternalCall(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }

        if (IsCypressSuiteOrTest(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }

        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsCyExternalCall(node, out var argNode, out _))
        {
            if (argNode.IsValid())
            {
                var resolved = AstHelper.ResolveStringOrTemplate(argNode, projectName ?? ctx?.WorkspaceId);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    return resolved;
                }
            }
            return "http:unknown-service";
        }

        if (IsCypressSuiteOrTest(node))
        {
            var isSuite = IsSuiteCall(node);
            var title = ExtractTitle(node);
            var prefix = isSuite ? "Suite" : "Test";
            return $"Cypress:{prefix}:{title}";
        }

        return null;
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsCypressSuiteOrTest(node) || IsCyExternalCall(node, out _, out _))
        {
            symbol.Properties["is_test"] = "true";
            symbol.Properties["test_framework"] = "cypress";

            if (IsSuiteCall(node)) symbol.Properties["test_kind"] = "suite";
            else if (IsTestCall(node)) symbol.Properties["test_kind"] = "test";
        }
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsCyCall(node, "visit", out var argNode))
        {
            var raw = ExtractStringValue(argNode);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                references.Add(new Reference(scopeSymbolId, raw, OntologyConstants.Relationships.CallsEndpoint));
            }
        }
        else if (IsCyRequestCall(node, out var reqArg))
        {
            var raw = ExtractStringValue(reqArg);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                references.Add(new Reference(scopeSymbolId, raw, OntologyConstants.Relationships.CallsEndpoint));
            }
        }
    }

    private static bool IsCyExternalCall(Node node, out Node? argNode, out string rawUrl)
    {
        argNode = null;
        rawUrl = "";

        if (IsCyCall(node, "visit", out argNode))
        {
            rawUrl = ExtractStringValue(argNode);
            if (rawUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                rawUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        else if (IsCyRequestCall(node, out argNode))
        {
            rawUrl = ExtractStringValue(argNode);
            if (rawUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                rawUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCyCall(Node node, string methodName, out Node? firstArg)
    {
        firstArg = null;
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.TypeScript.MemberExpression)) return false;

        var obj = func.GetChildForField(TreeSitterSyntax.Fields.Object);
        var prop = func.GetChildForField(TreeSitterSyntax.Fields.Property);

        if (obj.IsValid() && prop.IsValid() && obj.Text == "cy" && prop.Text == methodName)
        {
            var args = node.GetChildForField(TreeSitterSyntax.Fields.Arguments) ??
                       node.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);
            if (args.IsValid() && args.Children.Count > 1)
            {
                var candidate = args.Children.FirstOrDefault(c =>
                    c.IsValid() && (c.IsAny(TreeSitterSyntax.TypeScript.String, TreeSitterSyntax.TypeScript.TemplateString) || c.Type.Contains("string")));
                if (candidate != null && candidate.IsValid())
                {
                    firstArg = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsCyRequestCall(Node node, out Node? urlNode)
    {
        urlNode = null;
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.TypeScript.MemberExpression)) return false;

        var obj = func.GetChildForField(TreeSitterSyntax.Fields.Object);
        var prop = func.GetChildForField(TreeSitterSyntax.Fields.Property);

        if (obj.IsValid() && prop.IsValid() && obj.Text == "cy" && prop.Text == "request")
        {
            var args = node.GetChildForField(TreeSitterSyntax.Fields.Arguments) ??
                       node.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);
            if (!args.IsValid() || args.Children.Count <= 1) return false;

            // Handle: cy.request(url), cy.request(method, url), or cy.request({ url: '...' })
            foreach (var child in args.Children)
            {
                if (!child.IsValid()) continue;
                if (child.IsAny(TreeSitterSyntax.TypeScript.String, TreeSitterSyntax.TypeScript.TemplateString) || child.Type.Contains("string"))
                {
                    var txt = child.Text.Trim('"', '\'', '`');
                    if (txt is "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "HEAD") continue;
                    urlNode = child;
                    return true;
                }
                if (child.Is(TreeSitterSyntax.TypeScript.Object) || child.Type.Contains("object"))
                {
                    // Look for `url: "..."` property
                    foreach (var propNode in child.Children)
                    {
                        if (propNode.Is(TreeSitterSyntax.TypeScript.Pair))
                        {
                            var key = propNode.GetChildForField(TreeSitterSyntax.Fields.Key);
                            var val = propNode.GetChildForField(TreeSitterSyntax.Fields.Value);
                            if (key.IsValid() && val.IsValid() && key.Text.Trim('"', '\'') == "url")
                            {
                                urlNode = val;
                                return true;
                            }
                        }
                    }
                }
            }
        }

        return false;
    }

    private static bool IsCypressSuiteOrTest(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        if (!IsCypressContext(node)) return false;

        return IsSuiteCall(node) || IsTestCall(node);
    }

    private static bool IsSuiteCall(Node node)
    {
        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid()) return false;
        var text = func.Text;
        return text is "describe" or "context" or "describe.only" or "describe.skip";
    }

    private static bool IsTestCall(Node node)
    {
        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid()) return false;
        var text = func.Text;
        return text is "it" or "specify" or "it.only" or "it.skip";
    }

    private static bool IsCypressContext(Node node)
    {
        var root = node.Tree?.RootNode;
        if (root.IsValid())
        {
            var text = root.Text;
            if (text.Contains("cy.") || text.Contains("Cypress.") ||
                text.Contains("from 'cypress'") || text.Contains("from \"cypress\"") ||
                text.Contains("/// <reference types=\"cypress\""))
            {
                return true;
            }
        }
        return false;
    }

    private static string ExtractTitle(Node node)
    {
        var args = node.GetChildForField(TreeSitterSyntax.Fields.Arguments) ??
                   node.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);
        if (!args.IsValid()) return "unnamed";

        foreach (var child in args.Children)
        {
            if (!child.IsValid()) continue;
            if (child.IsAny(TreeSitterSyntax.TypeScript.String, TreeSitterSyntax.TypeScript.TemplateString) ||
                child.Type.Contains("string"))
            {
                var raw = child.Text.Trim('"', '\'', '`');
                if (!string.IsNullOrWhiteSpace(raw)) return raw;
            }
        }
        return "unnamed";
    }

    private static string ExtractStringValue(Node? node)
    {
        if (node == null || !node.IsValid()) return "";
        return node.Text.Trim('"', '\'', '`');
    }
}
