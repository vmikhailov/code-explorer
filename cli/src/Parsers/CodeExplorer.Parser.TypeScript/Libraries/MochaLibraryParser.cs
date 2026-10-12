using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class MochaLibraryParser : ISemanticExtension
{
    public string Id => "mocha";
    public string Name => "Mocha";
    public string Type => "library";
    public LibraryRole LibraryRole => LibraryRole.TestFramework;
    public IReadOnlyList<string> SupportedPatterns => ["mocha", "@types/mocha"];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;

    public IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor("mocha", "Mocha", LibraryRole.TestFramework, "library", ["mocha", "@types/mocha"]);
    }

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsMochaCall(node, out var kind))
        {
            return kind;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsMochaCall(node, out _))
        {
            var isSuite = IsSuiteCall(node);
            var title = ExtractTitle(node);
            var prefix = isSuite ? "Suite" : "Test";
            return $"Mocha:{prefix}:{title}";
        }
        return null;
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsMochaCall(node, out _))
        {
            var isSuite = IsSuiteCall(node);
            symbol.Properties["is_test"] = "true";
            symbol.Properties["test_framework"] = "mocha";
            symbol.Properties["test_kind"] = isSuite ? "suite" : "test";
            symbol.Properties["test_title"] = ExtractTitle(node);
        }
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
    }

    private static bool IsMochaCall(Node node, out string kind)
    {
        kind = OntologyConstants.NodeLabels.EntryPoint;
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        // Distinctive Mocha idioms: `context(...)` and `specify(...)`
        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid()) return false;

        GetRootIdentifierAndMembers(func, out var rootId, out _);
        if (rootId is "context" or "specify")
        {
            return true;
        }

        // In files explicitly importing mocha or matching mocha conventions
        if (IsMochaContext(node))
        {
            return IsSuiteCall(node) || IsTestCall(node);
        }

        return false;
    }

    private static bool IsSuiteCall(Node node)
    {
        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid()) return false;

        GetRootIdentifierAndMembers(func, out var rootId, out var members);
        if (rootId is "describe" or "context" or "suite")
        {
            if (members.Count == 0) return true;
            return members.All(m => m is "only" or "skip");
        }
        return false;
    }

    private static bool IsTestCall(Node node)
    {
        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid()) return false;

        GetRootIdentifierAndMembers(func, out var rootId, out var members);
        if (rootId is "it" or "specify" or "test")
        {
            if (members.Count == 0) return true;
            return members.All(m => m is "only" or "skip");
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

    private static bool IsMochaContext(Node node)
    {
        var root = node.Tree?.RootNode;
        if (root.IsValid())
        {
            var text = root.Text;
            if (text.Contains("from 'mocha'") || text.Contains("from \"mocha\"") ||
                text.Contains("import 'mocha'") || text.Contains("import \"mocha\"") ||
                text.Contains("require('mocha')") || text.Contains("require(\"mocha\")"))
            {
                return true;
            }
        }
        return false;
    }
}
