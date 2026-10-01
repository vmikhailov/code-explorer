using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Parser.Components.Parsers;

/// <summary>
/// Component parser for JavaScript/TypeScript test runners and frameworks (Jest, Vitest, Mocha, Cypress, Playwright).
/// </summary>
public class JavaScriptTestRunnerComponentParser : IComponentLibraryParser
{
    public string Id => "js-test-runner";
    public string Name => "JavaScript Test Runner";

    private static readonly HashSet<string> TestFrameworks = new(StringComparer.OrdinalIgnoreCase)
    {
        "jest", "@types/jest", "ts-jest", "babel-jest",
        "vitest", "@vitest/ui",
        "mocha", "@types/mocha",
        "cypress",
        "@playwright/test", "playwright",
        "jasmine", "supertest"
    };

    public bool CanHandle(ProjectContext context)
    {
        return context.Dependencies.Any(d => TestFrameworks.Contains(d)) ||
               context.ManifestProperties.GetValueOrDefault("manifest_type") == "test" ||
               context.ManifestProperties.GetValueOrDefault("is_test_project") == "true";
    }

    public ComponentAnalysisResult AnalyzeManifest(ProjectContext context)
    {
        return new ComponentAnalysisResult
        {
            ComponentId = Id,
            ComponentName = Name,
            Role = LibraryRole.TestFramework,
            Capabilities = ComponentCapabilities.TestRunner,
            Metadata = new Dictionary<string, string>
            {
                ["test_runner"] = "true"
            }
        };
    }

    private static readonly HashSet<string> TestFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "it", "test", "describe", "suite", "specify", "it.only", "it.skip", "test.only", "test.skip", "it.each", "test.each"
    };

    public void EnrichWithSyntax(ComponentAnalysisResult? result, SyntaxTree syntaxTree, ParsingContext ctx)
    {
        if (syntaxTree.Tree == null) return;

        var root = syntaxTree.Tree.RootNode;
        var hasTestsInFile = syntaxTree.RelativePath.EndsWith(".test.ts", StringComparison.OrdinalIgnoreCase) ||
                             syntaxTree.RelativePath.EndsWith(".spec.ts", StringComparison.OrdinalIgnoreCase) ||
                             syntaxTree.RelativePath.EndsWith(".test.js", StringComparison.OrdinalIgnoreCase) ||
                             syntaxTree.RelativePath.EndsWith(".spec.js", StringComparison.OrdinalIgnoreCase);

        void WalkNode(TreeSitter.Node node)
        {
            if (node.Is(TreeSitterSyntax.Common.CallExpression))
            {
                var func = node.GetChildForField(TreeSitterSyntax.Fields.Function) ?? node.Children.FirstOrDefault(c => c.IsValid());
                if (func.IsValid())
                {
                    var funcName = func.Text;
                    if (TestFunctions.Contains(funcName))
                    {
                        hasTestsInFile = true;
                        var args = node.GetChildForField(TreeSitterSyntax.Fields.Arguments) ?? node.FindChildOfType(TreeSitterSyntax.Common.Arguments);
                        var testTitle = "";
                        if (args.IsValid() && args.Children.Count > 1)
                        {
                            var firstArg = args.Children.FirstOrDefault(c => c.IsValid() && (c.Type.Contains("string") || c.Type.Contains("template")));
                            if (firstArg.IsValid()) testTitle = firstArg.Text.Trim('"', '\'', '`');
                        }

                        var startRow = node.StartPosition.Row;
                        MarkMatchingFunction(syntaxTree.FileNode, testTitle, startRow, "jest", result);
                    }
                }
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (child.IsValid())
                {
                    WalkNode(child);
                }
            }
        }

        WalkNode(root);

        if (hasTestsInFile)
        {
            syntaxTree.FileNode.SetExtension("is_test", "true");
        }
    }

    private static void MarkMatchingFunction(
        Common.Nodes.IOntologyNode parent,
        string? testTitle,
        int startRow,
        string framework,
        ComponentAnalysisResult? result)
    {
        foreach (var child in parent.Children)
        {
            if (child is Common.Nodes.Layer3_Syntactic.FunctionNode fn)
            {
                if (fn.StartLine == startRow || fn.StartLine == startRow + 1 ||
                    (!string.IsNullOrEmpty(testTitle) && string.Equals(fn.Name, testTitle, StringComparison.OrdinalIgnoreCase)))
                {
                    fn.SetExtension("is_test", "true");
                    fn.SetExtension("test_framework", framework);
                    if (result != null && !string.IsNullOrEmpty(fn.Name))
                    {
                        result.Tests.Add(fn.Name);
                    }
                }
            }
            MarkMatchingFunction(child, testTitle, startRow, framework, result);
        }
    }
}
