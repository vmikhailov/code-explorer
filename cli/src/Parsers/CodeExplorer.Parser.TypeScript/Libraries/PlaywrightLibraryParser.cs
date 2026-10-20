using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class PlaywrightLibraryParser : ISemanticExtension
{
    public string Id => "playwright";
    public string Name => "Playwright";
    public string Type => "library";
    public LibraryRole LibraryRole => LibraryRole.TestFramework;
    public IReadOnlyList<string> SupportedPatterns => ["@playwright/test", "playwright"];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;

    public IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor("playwright", "Playwright", LibraryRole.TestFramework, "library", ["@playwright/test", "playwright"]);
    }

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsPlaywrightExternalCall(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }

        if (IsPlaywrightSuiteOrTest(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }

        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsPlaywrightExternalCall(node, out var argNode, out _))
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

        if (IsPlaywrightSuiteOrTest(node))
        {
            var isSuite = IsSuiteCall(node);
            var title = ExtractTitle(node);
            var prefix = isSuite ? "Suite" : "Test";
            return $"Playwright:{prefix}:{title}";
        }

        return null;
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsPlaywrightSuiteOrTest(node) || IsPlaywrightExternalCall(node, out _, out _))
        {
            symbol.Properties["is_test"] = "true";
            symbol.Properties["test_framework"] = "playwright";

            if (IsSuiteCall(node)) symbol.Properties["test_kind"] = "suite";
            else if (IsTestCall(node)) symbol.Properties["test_kind"] = "test";
        }
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsGotoCall(node, out var gotoArg))
        {
            var raw = ExtractStringValue(gotoArg);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                references.Add(new Reference(scopeSymbolId, raw, OntologyConstants.Relationships.CallsEndpoint));
            }
        }
        else if (IsPlaywrightRequestCall(node, out var reqArg))
        {
            var raw = ExtractStringValue(reqArg);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                references.Add(new Reference(scopeSymbolId, raw, OntologyConstants.Relationships.CallsEndpoint));
            }
        }
    }

    private static bool IsPlaywrightExternalCall(Node node, out Node? argNode, out string rawUrl)
    {
        argNode = null;
        rawUrl = "";

        if (IsGotoCall(node, out argNode))
        {
            rawUrl = ExtractStringValue(argNode);
            if (rawUrl.StartWithIC("http://") ||
                rawUrl.StartWithIC("https://"))
            {
                return true;
            }
        }
        else if (IsPlaywrightRequestCall(node, out argNode))
        {
            rawUrl = ExtractStringValue(argNode);
            if (rawUrl.StartWithIC("http://") ||
                rawUrl.StartWithIC("https://"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGotoCall(Node node, out Node? urlArg)
    {
        urlArg = null;
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.TypeScript.MemberExpression)) return false;

        var prop = func.GetChildForField(TreeSitterSyntax.Fields.Property);
        if (prop.IsValid() && prop.Text == "goto")
        {
            var args = node.GetChildForField(TreeSitterSyntax.Fields.Arguments) ??
                       node.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);
            if (args.IsValid() && args.Children.Count > 1)
            {
                var candidate = args.Children.FirstOrDefault(c =>
                    c.IsValid() && (c.IsAny(TreeSitterSyntax.TypeScript.String, TreeSitterSyntax.TypeScript.TemplateString) || c.Type.Contains("string")));
                if (candidate != null && candidate.IsValid())
                {
                    urlArg = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPlaywrightRequestCall(Node node, out Node? urlArg)
    {
        urlArg = null;
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.TypeScript.MemberExpression)) return false;

        var prop = func.GetChildForField(TreeSitterSyntax.Fields.Property);
        var obj = func.GetChildForField(TreeSitterSyntax.Fields.Object);

        if (prop.IsValid() && prop.Text is "get" or "post" or "put" or "delete" or "patch" or "fetch" or "head")
        {
            var objText = obj.IsValid() ? obj.Text : "";
            if (objText.Equals("request", StringComparison.OrdinalIgnoreCase) ||
                objText.EndsWith(".request", StringComparison.OrdinalIgnoreCase) ||
                objText.Equals("apiContext", StringComparison.OrdinalIgnoreCase))
            {
                var args = node.GetChildForField(TreeSitterSyntax.Fields.Arguments) ??
                           node.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);
                if (args.IsValid() && args.Children.Count > 1)
                {
                    var candidate = args.Children.FirstOrDefault(c =>
                        c.IsValid() && (c.IsAny(TreeSitterSyntax.TypeScript.String, TreeSitterSyntax.TypeScript.TemplateString) || c.Type.Contains("string")));
                    if (candidate.IsValid())
                    {
                        urlArg = candidate;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsPlaywrightSuiteOrTest(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        return IsSuiteCall(node) || IsTestCall(node);
    }

    private static bool IsSuiteCall(Node node)
    {
        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid()) return false;

        GetRootIdentifierAndMembers(func, out var rootId, out var members);
        if (rootId == "test" && members.Contains("describe"))
        {
            return true;
        }

        return false;
    }

    private static bool IsTestCall(Node node)
    {
        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid()) return false;

        GetRootIdentifierAndMembers(func, out var rootId, out var members);
        if (rootId == "test")
        {
            if (members.Contains("describe")) return false;

            // In Playwright: test('title', ...) or test.only('title', ...), test.skip('title', ...)
            if (IsPlaywrightContext(node))
            {
                if (members.Count == 0) return true;
                return members.All(m => m is "only" or "skip" or "fixme" or "fail" or "slow" or "use");
            }
        }

        return false;
    }

    private static void GetRootIdentifierAndMembers(Node funcNode, out string rootId, out List<string> members)
    {
        rootId = "";
        members = [];

        var current = funcNode;
        while (current.IsValid())
        {
            if (current.Is(TreeSitterSyntax.TypeScript.Identifier))
            {
                rootId = current.Text;
                break;
            }
            if (current.Is(TreeSitterSyntax.TypeScript.MemberExpression))
            {
                var prop = current.GetChildForField(TreeSitterSyntax.Fields.Property);
                if (prop.IsValid())
                {
                    members.Insert(0, prop.Text);
                }
                current = current.GetChildForField(TreeSitterSyntax.Fields.Object);
                continue;
            }
            break;
        }
    }

    private static bool IsPlaywrightContext(Node node)
    {
        var root = node.Tree?.RootNode;
        if (root.IsValid())
        {
            var text = root.Text;
            if (text.Contains("@playwright/test") || text.Contains("playwright") ||
                text.Contains("page.goto") || text.Contains("expect(page)"))
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
