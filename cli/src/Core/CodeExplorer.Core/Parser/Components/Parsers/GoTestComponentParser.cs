using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Parser.Components.Parsers;

/// <summary>
/// Component parser for Go test packages (testing, testify).
/// </summary>
public class GoTestComponentParser : IComponentLibraryParser
{
    public string Id => "go-test";
    public string Name => "Go Testing Framework";

    public bool CanHandle(ProjectContext context)
    {
        return context.ProjectType.Equals("go", StringComparison.OrdinalIgnoreCase) &&
               (context.Dependencies.Any(d => d.Contains("testify")) ||
                context.FilesInDirectory.Any(f => f.EndsWith("_test.go")) ||
                context.ManifestProperties.GetValueOrDefault("manifest_type") == "test" ||
                context.ManifestProperties.GetValueOrDefault("is_test_project") == "true");
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

    public void EnrichWithSyntax(ComponentAnalysisResult? result, SyntaxTree syntaxTree, ParsingContext ctx)
    {
        if (syntaxTree.Tree == null) return;

        var isTestFile = syntaxTree.RelativePath.EndsWith("_test.go", StringComparison.OrdinalIgnoreCase);
        var root = syntaxTree.Tree.RootNode;

        void WalkNode(TreeSitter.Node node)
        {
            if (node.IsAny(TreeSitterSyntax.Go.FunctionDeclaration, TreeSitterSyntax.Go.MethodDeclaration))
            {
                var funcName = node.GetChildForField("name")?.Text
                               ?? node.FindChildOfType("identifier")?.Text;

                if (!string.IsNullOrEmpty(funcName) &&
                    (funcName.StartsWith("Test", StringComparison.Ordinal) ||
                     funcName.StartsWith("Benchmark", StringComparison.Ordinal) ||
                     funcName.StartsWith("Example", StringComparison.Ordinal) ||
                     isTestFile))
                {
                    var startRow = node.StartPosition.Row;
                    MarkMatchingFunction(syntaxTree.FileNode, funcName, startRow, "go-testing", result);
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

        if (isTestFile)
        {
            syntaxTree.FileNode.SetExtension("is_test", "true");
        }
    }

    private static void MarkMatchingFunction(
        Common.Nodes.IOntologyNode parent,
        string? funcName,
        int startRow,
        string framework,
        ComponentAnalysisResult? result)
    {
        foreach (var child in parent.Children)
        {
            if (child is Common.Nodes.Layer3_Syntactic.FunctionNode fn)
            {
                if (string.Equals(fn.Name, funcName, StringComparison.OrdinalIgnoreCase) ||
                    fn.StartLine == startRow || fn.StartLine == startRow + 1)
                {
                    fn.SetExtension("is_test", "true");
                    fn.SetExtension("test_framework", framework);
                    if (result != null && !string.IsNullOrEmpty(fn.Name))
                    {
                        result.Tests.Add(fn.Name);
                    }
                }
            }
            MarkMatchingFunction(child, funcName, startRow, framework, result);
        }
    }
}
