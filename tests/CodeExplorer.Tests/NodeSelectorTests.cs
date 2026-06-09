using NUnit.Framework;
using TreeSitter;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.TypeScript;

namespace CodeExplorer.Tests;

[TestFixture]
public class NodeSelectorTests
{
    private TypeScriptParser _parser;
    private string _tempDir;

    [SetUp]
    public void SetUp()
    {
        _parser = new TypeScriptParser();
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Test]
    public async Task Test_NodeSelector_HasType_And_FirstChild_And_GetChildForField()
    {
        var tempFile = Path.Combine(_tempDir, "decorator_test.ts");
        var code = @"
@Controller('leads')
class MyController {}
";
        await File.WriteAllTextAsync(tempFile, code);

        using var syntaxTree = await SyntaxTree.ParseAsync(tempFile, "decorator_test.ts", "parent-id", _parser, "ws-id", _tempDir);
        var root = syntaxTree.Tree?.RootNode;

        var decoratorNode = FindNode(root, "decorator");
        Assert.That(decoratorNode, Is.Not.Null);

        var selector = NodeSelector.New()
            .HasType("decorator")
            .FirstChild
            .HasType("call_expression")
            .GetChildForField("function")
            .Text("Controller|Get|Post|Put|Delete|Patch|SubscribeMessage");

        Assert.That(selector.Matches(decoratorNode), Is.True);

        var selectorFalse = NodeSelector.New()
            .HasType("decorator")
            .FirstChild
            .HasType("call_expression")
            .GetChildForField("function")
            .Text("Get|Post");

        Assert.That(selectorFalse.Matches(decoratorNode), Is.False);
    }

    [Test]
    public async Task Test_NodeSelector_Or_And_HasChild_And_FunctionNode()
    {
        var tempFile = Path.Combine(_tempDir, "express_and_axios_test.ts");
        var code = @"
app.get('/leads', (req, res) => {
    fetch('/target');
    axios.post('/another');
});
";
        await File.WriteAllTextAsync(tempFile, code);

        using var syntaxTree = await SyntaxTree.ParseAsync(tempFile, "express_and_axios_test.ts", "parent-id", _parser, "ws-id", _tempDir);
        var root = syntaxTree.Tree?.RootNode;

        // Express route selector test
        var expressCallNode = FindNode(root, "call_expression");
        Assert.That(expressCallNode, Is.Not.Null);

        var expressSelector = NodeSelector.New()
            .HasType("call_expression")
            .FunctionNode
            .HasType("member_expression")
            .HasChild("object", NodeSelector.New().TextContains("app|router|express"))
            .HasChild("property", NodeSelector.New().Text("get|post|put|delete"));

        Assert.That(expressSelector.Matches(expressCallNode), Is.True);

        // HttpClientCallSelector test
        var fetchCallNode = FindNode(root, "call_expression", "fetch");
        Assert.That(fetchCallNode, Is.Not.Null);

        var axiosCallNode = FindNode(root, "call_expression", "axios.post");
        Assert.That(axiosCallNode, Is.Not.Null);

        var httpClientCallSelector = NodeSelector.New()
            .HasType("call_expression")
            .FunctionNode
            .Where(NodeSelector.Or(
                NodeSelector.New().HasType("identifier").Text("fetch"),
                NodeSelector.New()
                    .HasType("member_expression")
                    .HasChild("object", NodeSelector.New().Text("axios"))
                    .HasChild("property", NodeSelector.New().Text("get|post|put|delete|request"))
            ));

        Assert.That(httpClientCallSelector.Matches(fetchCallNode), Is.True);
        Assert.That(httpClientCallSelector.Matches(axiosCallNode), Is.True);
        Assert.That(httpClientCallSelector.Matches(expressCallNode), Is.False);
    }

    [Test]
    public async Task Test_NodeSelector_Select()
    {
        var tempFile = Path.Combine(_tempDir, "select_test.ts");
        var code = @"
app.get('/leads');
";
        await File.WriteAllTextAsync(tempFile, code);

        using var syntaxTree = await SyntaxTree.ParseAsync(tempFile, "select_test.ts", "parent-id", _parser, "ws-id", _tempDir);
        var root = syntaxTree.Tree?.RootNode;

        var callNode = FindNode(root, "call_expression");
        Assert.That(callNode, Is.Not.Null);

        var propertySelector = NodeSelector.New()
            .FunctionNode
            .GetChildForField("property");

        var propNode = propertySelector.Select(callNode);
        Assert.That(propNode, Is.Not.Null);
        Assert.That(propNode!.Text, Is.EqualTo("get"));
    }

    private Node? FindNode(Node? node, string type, string? functionTextName = null)
    {
        if (node == null || node.Id == System.IntPtr.Zero) return null;
        if (node.Type == type)
        {
            if (functionTextName == null) return node;
            var func = node.GetFunctionNode();
            if (func.IsValid() && func!.Text == functionTextName) return node;
        }
        foreach (var child in node.Children)
        {
            var result = FindNode(child, type, functionTextName);
            if (result != null) return result;
        }
        return null;
    }
}
