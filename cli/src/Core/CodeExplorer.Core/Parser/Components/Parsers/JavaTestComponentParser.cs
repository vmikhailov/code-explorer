using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Parser.Components.Parsers;

/// <summary>
/// Component parser for Java test frameworks (JUnit, TestNG).
/// </summary>
public class JavaTestComponentParser : IComponentLibraryParser
{
    public string Id => "java-test";
    public string Name => "Java Test Framework";

    public bool CanHandle(ProjectContext context)
    {
        return context.ProjectType.Equals("java", StringComparison.OrdinalIgnoreCase) &&
               (context.Dependencies.Any(d => d.Contains("junit") || d.Contains("testng")) ||
                context.FilesInDirectory.Any(f => f.EndsWith("Test.java") || f.EndsWith("Tests.java")) ||
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

        var isTestFile = syntaxTree.RelativePath.EndsWith("Test.java", StringComparison.OrdinalIgnoreCase) ||
                         syntaxTree.RelativePath.EndsWith("Tests.java", StringComparison.OrdinalIgnoreCase);

        var root = syntaxTree.Tree.RootNode;

        void WalkNode(TreeSitter.Node node)
        {
            if (node.Is(TreeSitterSyntax.Java.MethodDeclaration))
            {
                var isTest = isTestFile;
                var annotations = node.FindChildrenOfType("marker_annotation");
                foreach (var a in annotations)
                {
                    if (a.Text.Contains("Test", StringComparison.OrdinalIgnoreCase))
                    {
                        isTest = true;
                        break;
                    }
                }

                if (isTest)
                {
                    var methodName = node.GetChildForField("name")?.Text
                                     ?? node.FindChildOfType("identifier")?.Text;

                    var startRow = node.StartPosition.Row;
                    MarkMatchingFunction(syntaxTree.FileNode, methodName, startRow, "junit", result);
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
