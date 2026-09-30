using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Parser.Components.Parsers;

/// <summary>
/// Component parser for Python test frameworks (pytest, unittest).
/// </summary>
public class PythonTestComponentParser : IComponentLibraryParser
{
    public string Id => "python-test";
    public string Name => "Python Test Framework";

    public bool CanHandle(ProjectContext context)
    {
        return context.ProjectType.Equals("python", StringComparison.OrdinalIgnoreCase) &&
               (context.Dependencies.Any(d => d.StartsWith("pytest", StringComparison.OrdinalIgnoreCase) || d.Equals("unittest", StringComparison.OrdinalIgnoreCase)) ||
                context.FilesInDirectory.Any(f => Path.GetFileName(f).StartsWith("test_") || Path.GetFileName(f).EndsWith("_test.py")) ||
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

        var fileName = Path.GetFileName(syntaxTree.RelativePath).ToLowerInvariant();
        var hasTestsInFile = fileName.StartsWith("test_") || fileName.EndsWith("_test.py");

        var root = syntaxTree.Tree.RootNode;

        void WalkNode(TreeSitter.Node node)
        {
            if (node.Is("function_definition"))
            {
                var funcName = node.GetChildForField("name")?.Text
                               ?? node.FindChildOfType("identifier")?.Text;

                if (!string.IsNullOrEmpty(funcName) && (funcName.StartsWith("test_", StringComparison.OrdinalIgnoreCase) || hasTestsInFile))
                {
                    hasTestsInFile = true;
                    var startRow = node.StartPosition.Row;
                    MarkMatchingFunction(syntaxTree.FileNode, funcName, startRow, "pytest", result);
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
