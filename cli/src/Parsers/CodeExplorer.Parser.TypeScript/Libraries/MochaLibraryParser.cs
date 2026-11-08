using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class MochaLibraryParser : DeclarativeSemanticExtension
{
    public override string Id => "mocha";
    public override string Name => "Mocha";
    public override SemanticExtensionType Type => SemanticExtensionType.Library;
    public override LibraryRole LibraryRole => LibraryRole.TestFramework;
    public override IReadOnlyList<string> SupportedPatterns => ["mocha", "@types/mocha"];
    public override bool IsBuiltIn => true;

    private static readonly IReadOnlyList<CustomAstRule> Rules =
    [
        new CustomAstRule
        {
            NodeTypes = [TreeSitterSyntax.TypeScript.CallExpression],
            MapNodeType = static (node, ctx) => IsMochaCall(node, out var kind) ? kind : null,
            ExtractIdentifier = ExtractIdentifierStatic,
            EnrichSymbol = EnrichSymbolCore
        }
    ];

    public override IReadOnlyList<CustomAstRule> CustomAstRules => Rules;

    public override IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor("mocha", "Mocha", LibraryRole.TestFramework, "library", ["mocha", "@types/mocha"]);
    }

    private static string? ExtractIdentifierStatic(Node node, ParsingContext ctx, string? projectName, string? relativePath)
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

    private static void EnrichSymbolCore(Node node, SyntacticSymbol symbol, ParsingContext ctx)
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

    private static bool IsMochaCall(Node node, out string kind)
    {
        kind = OntologyConstants.NodeLabels.Function;
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        // Distinctive Mocha idioms: `context(...)` and `specify(...)`
        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid()) return false;

        GetRootIdentifierAndMembers(func, out var rootId, out var members);
        if (rootId is "context" or "specify")
        {
            return members.Count == 0 || members.All(m => m is "only" or "skip");
        }

        if (rootId is not ("describe" or "suite" or "it" or "test"))
        {
            return false;
        }

        if (members.Count > 0 && !members.All(m => m is "only" or "skip"))
        {
            return false;
        }

        // In files explicitly importing mocha or matching mocha conventions
        return IsMochaContext(node);
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

        if (funcNode.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            var span = funcNode.AsSpan();
            if (span is "describe" or "context" or "suite" or "it" or "specify" or "test")
            {
                rootId = funcNode.Text;
            }
            return;
        }

        var probe = funcNode;
        while (probe.IsValid())
        {
            if (probe.Is(TreeSitterSyntax.TypeScript.Identifier))
            {
                if (probe.AsSpan() is not ("describe" or "context" or "suite" or "it" or "specify" or "test"))
                {
                    return;
                }
                break;
            }
            if (probe.Is(TreeSitterSyntax.TypeScript.MemberExpression))
            {
                probe = probe.GetChildForField(TreeSitterSyntax.Fields.Object);
                continue;
            }
            return;
        }

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
