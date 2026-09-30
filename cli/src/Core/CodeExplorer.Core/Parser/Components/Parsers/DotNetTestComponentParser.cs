using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Parser.Components.Parsers;

/// <summary>
/// Component parser for .NET test projects (xUnit, NUnit, MSTest).
/// </summary>
public class DotNetTestComponentParser : IComponentLibraryParser
{
    public string Id => "dotnet-test";
    public string Name => ".NET Test Framework";

    public bool CanHandle(ProjectContext context)
    {
        return context.Dependencies.Any(d =>
            d.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) ||
            d.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase) ||
            d.StartsWith("MSTest", StringComparison.OrdinalIgnoreCase) ||
            d.Equals("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase)) ||
            context.ManifestProperties.GetValueOrDefault("is_test_project") == "true" ||
            context.ManifestProperties.GetValueOrDefault("manifest_type") == "test";
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

    private static readonly HashSet<string> TestAttributeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Test", "TestAttribute",
        "TestCase", "TestCaseAttribute",
        "TestCaseSource", "TestCaseSourceAttribute",
        "Fact", "FactAttribute",
        "Theory", "TheoryAttribute",
        "TestMethod", "TestMethodAttribute",
        "DataTestMethod", "DataTestMethodAttribute",
        "Benchmark", "BenchmarkAttribute"
    };

    public void EnrichWithSyntax(ComponentAnalysisResult? result, SyntaxTree syntaxTree, ParsingContext ctx)
    {
        if (syntaxTree.Tree == null) return;

        var root = syntaxTree.Tree.RootNode;
        var hasTestsInFile = false;

        void WalkNode(TreeSitter.Node node)
        {
            if (node.Is(TreeSitterSyntax.CSharp.MethodDeclaration))
            {
                var attrList = node.FindChildrenOfType(TreeSitterSyntax.CSharp.AttributeList);
                string? detectedFramework = null;

                foreach (var al in attrList)
                {
                    foreach (var attr in al.FindChildrenOfType(TreeSitterSyntax.CSharp.Attribute))
                    {
                        var nameNode = attr.FindChildOfType(TreeSitterSyntax.Common.Identifier) 
                                       ?? attr.Children.FirstOrDefault(c => c.IsValid());
                        if (nameNode.IsValid() && TestAttributeNames.Contains(nameNode.Text))
                        {
                            var attrText = nameNode.Text;
                            detectedFramework = attrText.StartsWith("Fact", StringComparison.OrdinalIgnoreCase) || attrText.StartsWith("Theory", StringComparison.OrdinalIgnoreCase)
                                ? "xunit"
                                : attrText.StartsWith("TestMethod", StringComparison.OrdinalIgnoreCase) || attrText.StartsWith("DataTestMethod", StringComparison.OrdinalIgnoreCase)
                                    ? "mstest"
                                    : attrText.StartsWith("Benchmark", StringComparison.OrdinalIgnoreCase)
                                        ? "benchmarkdotnet"
                                        : "nunit";
                            break;
                        }
                    }
                    if (detectedFramework != null) break;
                }

                if (detectedFramework != null)
                {
                    hasTestsInFile = true;
                    var methodName = node.GetChildForField("name")?.Text
                                     ?? node.FindChildOfType(TreeSitterSyntax.Common.Identifier)?.Text;

                    var startRow = node.StartPosition.Row;
                    MarkMatchingFunction(syntaxTree.FileNode, methodName, startRow, detectedFramework, result);
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
        string? methodName,
        int startRow,
        string framework,
        ComponentAnalysisResult? result)
    {
        foreach (var child in parent.Children)
        {
            if (child is Common.Nodes.Layer3_Syntactic.FunctionNode fn)
            {
                if (string.Equals(fn.Name, methodName, StringComparison.OrdinalIgnoreCase) ||
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
            MarkMatchingFunction(child, methodName, startRow, framework, result);
        }
    }
}
