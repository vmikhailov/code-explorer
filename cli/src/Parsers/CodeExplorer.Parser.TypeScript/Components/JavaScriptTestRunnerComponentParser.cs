using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Parser.Components;

namespace CodeExplorer.Parser.TypeScript.Components;

/// <summary>
/// Component parser for JavaScript/TypeScript test runners and frameworks (Jest, Vitest, Mocha, Cypress, Playwright).
/// </summary>
public class JavaScriptTestRunnerComponentParser : IComponentLibraryParser
{
    public string Id => "js-test-runner";
    public string Name => "JavaScript Test Runner";

    private static readonly PackageDescriptor[] TestPackageDescriptors =
    [
        .. new Libraries.JestVitestLibraryParser().GetPackageDescriptors(),
        .. new Libraries.MochaLibraryParser().GetPackageDescriptors(),
        .. new Libraries.CypressLibraryParser().GetPackageDescriptors(),
        .. new Libraries.PlaywrightLibraryParser().GetPackageDescriptors(),
        new PackageDescriptor("jasmine", "Jasmine / Karma", LibraryRole.TestFramework, "library", ["jasmine", "jasmine-core", "@types/jasmine", "karma", "supertest"])
    ];

    public bool CanHandle(ProjectContext context)
    {
        return TestPackageDescriptors.Any(p => p.Matches(context)) ||
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

    private static readonly string[] TestFileSuffixes =
    [
        ".spec.ts", ".spec.tsx", ".spec.js", ".spec.jsx", ".test.ts", ".test.tsx", ".test.js", ".test.jsx"
    ];

    private const string DefaultFrameworkJest = "jest";

    /// <inheritdoc />
    public bool IsTestSource(SyntaxTree syntaxTree)
    {
        if (TestFileSuffixes.Any(sfx => syntaxTree.RelativePath.EndsWith(sfx, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (syntaxTree.Tree == null) return false;

        // A runner file without the conventional suffix: a top-level describe()/it()/test() call.
        foreach (var stmt in syntaxTree.Tree.RootNode.Children)
        {
            if (!stmt.IsValid() || !stmt.Is("expression_statement")) continue;
            var call = stmt.Children.FirstOrDefault(c => c.IsValid() && c.Is(TreeSitterSyntax.Common.CallExpression));
            if (!call.IsValid()) continue;
            var func = call.GetChildForField(TreeSitterSyntax.Fields.Function);
            if (func.IsValid() && TestFunctions.Contains(func.Text))
            {
                return true;
            }
        }

        return false;
    }

    public void EnrichWithSyntax(ComponentAnalysisResult? result, SyntaxTree syntaxTree, ParsingContext ctx)
    {
        if (syntaxTree.Tree == null) return;

        var hasTestsInFile = syntaxTree.RelativePath.EndsWith(".test.ts", StringComparison.OrdinalIgnoreCase) ||
                             syntaxTree.RelativePath.EndsWith(".spec.ts", StringComparison.OrdinalIgnoreCase) ||
                             syntaxTree.RelativePath.EndsWith(".test.js", StringComparison.OrdinalIgnoreCase) ||
                             syntaxTree.RelativePath.EndsWith(".spec.js", StringComparison.OrdinalIgnoreCase);

        var doc = syntaxTree.Document;
        if (doc != null)
        {
            for (int i = 0; i < doc.AllCalls.Count; i++)
            {
                var node = doc.AllCalls[i];
                var func = node.Callee ?? node.GetChildForField(TreeSitterSyntax.Fields.Function);
                if (!func.IsValid()) continue;
                var funcSpan = func.AsSpan();
                if (funcSpan.Length > 24) continue;
                var funcName = func.Text;
                if (TestFunctions.Contains(funcName))
                {
                    hasTestsInFile = true;
                    var testTitle = "";
                    if (node.Arguments.Count > 0)
                    {
                        var firstArg = node.Arguments[0].Value ?? node.Arguments[0];
                        if (firstArg.IsValid() && (firstArg is CodeExplorer.Core.Parser.Syntax.LiteralExprSyntax || firstArg.Type.Contains("string") || firstArg.Type.Contains("template")))
                        {
                            testTitle = firstArg.Text.Trim('"', '\'', '`');
                        }
                    }

                    var startRow = node.StartPosition.Row;
                    MarkMatchingFunction(syntaxTree.FileNode, testTitle, startRow, DefaultFrameworkJest, result);
                }
            }
        }

        if (hasTestsInFile)
        {
            syntaxTree.FileNode.IsTest = true;
        }
    }

    private static void MarkMatchingFunction(
        IOntologyNode parent,
        string? testTitle,
        int startRow,
        string framework,
        ComponentAnalysisResult? result)
    {
        foreach (var child in parent.Children)
        {
            if (child is FunctionNode fn)
            {
                if (fn.StartLine == startRow || fn.StartLine == startRow + 1 ||
                    (!string.IsNullOrEmpty(testTitle) && string.Equals(fn.Name, testTitle, StringComparison.OrdinalIgnoreCase)))
                {
                    fn.IsTest = true;
                    fn.TestFramework = framework;
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
