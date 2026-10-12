using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class JestVitestLibraryParser : ISemanticExtension
{
    public string Id => "jest";
    public string Name => "Jest / Vitest";
    public string Type => "library";
    public LibraryRole LibraryRole => LibraryRole.TestFramework;
    public IReadOnlyList<string> SupportedPatterns => ["jest", "@types/jest", "ts-jest", "babel-jest", "@jest/globals", "vitest", "@vitest/*"];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;

    public IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor("jest", "Jest", LibraryRole.TestFramework, "library", ["jest", "@types/jest", "ts-jest", "babel-jest", "@jest/globals"]);
        yield return new PackageDescriptor("vitest", "Vitest", LibraryRole.TestFramework, "library", ["vitest", "@vitest/*"]);
    }

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsJestVitestCall(node, out var kind))
        {
            return kind;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsJestVitestCall(node, out _))
        {
            var framework = DetectFramework(node, relativePath);
            var isSuite = IsSuiteCall(node);
            var title = ExtractTitle(node);
            var prefix = isSuite ? "Suite" : "Test";
            return $"{framework}:{prefix}:{title}";
        }
        return null;
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsJestVitestCall(node, out _))
        {
            var framework = DetectFramework(node, null);
            var isSuite = IsSuiteCall(node);
            symbol.Properties["is_test"] = "true";
            symbol.Properties["test_framework"] = framework.ToLowerInvariant();
            symbol.Properties["test_kind"] = isSuite ? "suite" : "test";
            symbol.Properties["test_title"] = ExtractTitle(node);
        }
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // Extract mocked modules from jest.mock('...') and vi.mock('...')
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
            if (func.IsValid() && func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
            {
                var obj = func.GetChildForField(TreeSitterSyntax.Fields.Object);
                var prop = func.GetChildForField(TreeSitterSyntax.Fields.Property);
                if (obj.IsValid() && prop.IsValid() &&
                    (obj.Text == "jest" || obj.Text == "vi") &&
                    prop.Text == "mock")
                {
                    var args = node.GetChildForField(TreeSitterSyntax.Fields.Arguments) ??
                               node.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);
                    if (args.IsValid() && args.Children.Count > 1)
                    {
                        var firstArg = args.Children.FirstOrDefault(c =>
                            c.IsValid() && (c.IsAny(TreeSitterSyntax.TypeScript.String, TreeSitterSyntax.TypeScript.TemplateString) || c.Type.Contains("string")));
                        if (firstArg.IsValid())
                        {
                            var modulePath = firstArg.Text.Trim('"', '\'', '`');
                            if (!string.IsNullOrWhiteSpace(modulePath))
                            {
                                references.Add(new Reference(scopeSymbolId, modulePath, OntologyConstants.Relationships.DependsOn));
                            }
                        }
                    }
                }
            }
        }
    }

    private static bool IsJestVitestCall(Node node, out string kind)
    {
        kind = OntologyConstants.NodeLabels.EntryPoint;
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        // Ensure this is not Playwright (which uses `test` with `{ page }` or imports `@playwright/test`)
        if (IsPlaywrightCall(node)) return false;

        // Ensure this is not Cypress (`cy.*`)
        if (IsCypressCall(node)) return false;

        // Ensure this is not Mocha (which imports mocha or uses context/specify)
        if (IsMochaCall(node)) return false;

        return IsSuiteCall(node) || IsTestCall(node);
    }

    private static bool IsSuiteCall(Node node)
    {
        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid()) return false;

        GetRootIdentifierAndMembers(func, out var rootId, out var members);
        if (rootId is "describe" or "suite")
        {
            if (members.Count == 0) return true;
            return members.All(m => m is "only" or "skip" or "each" or "concurrent");
        }
        return false;
    }

    private static bool IsTestCall(Node node)
    {
        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid()) return false;

        GetRootIdentifierAndMembers(func, out var rootId, out var members);
        if (rootId is "test" or "it" or "fit" or "xit")
        {
            // Do not match `test.describe` (Playwright suite)
            if (members.Contains("describe")) return false;
            if (members.Count == 0) return true;
            return members.All(m => m is "only" or "skip" or "each" or "concurrent" or "todo" or "failing");
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
            if (current.Is(TreeSitterSyntax.TypeScript.CallExpression))
            {
                // Handles `.each(...)` which is a call returning a function
                current = current.GetChildForField(TreeSitterSyntax.Fields.Function);
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

    private static string DetectFramework(Node node, string? relativePath)
    {
        if (relativePath != null && relativePath.Contains("vitest", StringComparison.OrdinalIgnoreCase))
        {
            return "Vitest";
        }

        var root = node.Tree?.RootNode;
        if (root.IsValid())
        {
            var text = root.Text;
            if (text.Contains("from 'vitest'") || text.Contains("from \"vitest\"") ||
                text.Contains("require('vitest')") || text.Contains("require(\"vitest\")") ||
                text.Contains("vi.mock") || text.Contains("vi.fn"))
            {
                return "Vitest";
            }
        }
        return "Jest";
    }

    private static bool IsPlaywrightCall(Node node)
    {
        var root = node.Tree?.RootNode;
        if (root.IsValid())
        {
            var text = root.Text;
            if (text.Contains("@playwright/test"))
            {
                return true;
            }
        }

        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (func.IsValid() && func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var obj = func.GetChildForField(TreeSitterSyntax.Fields.Object);
            var prop = func.GetChildForField(TreeSitterSyntax.Fields.Property);
            if (obj.IsValid() && prop.IsValid() && obj.Text == "test" && prop.Text == "describe")
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCypressCall(Node node)
    {
        var root = node.Tree?.RootNode;
        if (root.IsValid())
        {
            var text = root.Text;
            if (text.Contains("cy.") || text.Contains("from 'cypress'") || text.Contains("from \"cypress\""))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsMochaCall(Node node)
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

        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (func.IsValid() && (func.Text == "context" || func.Text == "specify"))
        {
            return true;
        }

        return false;
    }
}
