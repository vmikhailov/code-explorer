using AwesomeAssertions;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.TypeScript;
using CodeExplorer.Tests.Shared;
using NUnit.Framework;
using TreeSitter;

namespace CodeExplorer.Tests;

[TestFixture]
public class NodeSelectorTests
{
    private readonly TypeScriptParser _parser = new();

    [Test]
    [ParserFileSource("SingleFiles/TypeScript", "node_selector_decorator.ts.test")]
    public async Task Test_NodeSelector_HasType_And_FirstChild_And_GetChildForField(string filePath)
    {
        var workspacePath = Path.GetDirectoryName(filePath)!;

        using var syntaxTree = await SyntaxTree.ParseAsync(filePath, "decorator_test.ts", "parent-id", _parser,
            "ws-id", workspacePath);

        var root = syntaxTree.Tree?.RootNode;

        var decoratorNode = FindNode(root, "decorator");
        decoratorNode.Should().NotBeNull();

        var selector = NodeSelector.New().HasType("decorator").FirstChild.HasType("call_expression")
            .GetChildForField("function").Text("Controller|Get|Post|Put|Delete|Patch|SubscribeMessage");

        selector.Matches(decoratorNode).Should().BeTrue();

        var selectorFalse = NodeSelector.New().HasType("decorator").FirstChild.HasType("call_expression")
            .GetChildForField("function").Text("Get|Post");

        selectorFalse.Matches(decoratorNode).Should().BeFalse();
    }

    [Test]
    [ParserFileSource("SingleFiles/TypeScript", "node_selector_express_axios.ts.test")]
    public async Task Test_NodeSelector_Or_And_HasChild_And_FunctionNode(string filePath)
    {
        var workspacePath = Path.GetDirectoryName(filePath)!;

        using var syntaxTree = await SyntaxTree.ParseAsync(filePath, "express_and_axios_test.ts", "parent-id", _parser,
            "ws-id", workspacePath);

        var root = syntaxTree.Tree?.RootNode;

        // Express route selector test
        var expressCallNode = FindNode(root, "call_expression");
        expressCallNode.Should().NotBeNull();

        var expressSelector = NodeSelector.New().HasType("call_expression").FunctionNode
            .HasType("member_expression").HasChild("object", NodeSelector.New().TextContains("app|router|express"))
            .HasChild("property", NodeSelector.New().Text("get|post|put|delete"));

        expressSelector.Matches(expressCallNode).Should().BeTrue();

        // HttpClientCallSelector test
        var fetchCallNode = FindNode(root, "call_expression", "fetch");
        fetchCallNode.Should().NotBeNull();

        var axiosCallNode = FindNode(root, "call_expression", "axios.post");
        axiosCallNode.Should().NotBeNull();

        var httpClientCallSelector = NodeSelector.New().HasType("call_expression").FunctionNode.Where(
            NodeSelector.Or(NodeSelector.New().HasType("identifier").Text("fetch"),
                NodeSelector.New().HasType("member_expression").HasChild("object", NodeSelector.New().Text("axios"))
                    .HasChild("property", NodeSelector.New().Text("get|post|put|delete|request"))));

        httpClientCallSelector.Matches(fetchCallNode).Should().BeTrue();
        httpClientCallSelector.Matches(axiosCallNode).Should().BeTrue();
        httpClientCallSelector.Matches(expressCallNode).Should().BeFalse();
    }

    [Test]
    [ParserFileSource("SingleFiles/TypeScript", "node_selector_select.ts.test")]
    public async Task Test_NodeSelector_Select(string filePath)
    {
        var workspacePath = Path.GetDirectoryName(filePath)!;

        using var syntaxTree =
            await SyntaxTree.ParseAsync(filePath, "select_test.ts", "parent-id", _parser, "ws-id", workspacePath);

        var root = syntaxTree.Tree?.RootNode;

        var callNode = FindNode(root, "call_expression");
        callNode.Should().NotBeNull();

        var propertySelector = NodeSelector.New().FunctionNode.GetChildForField("property");

        var propNode = propertySelector.Select(callNode);
        propNode.Should().NotBeNull();
        propNode!.Text.Should().Be("get");
    }

    private Node? FindNode(Node? node, string type, string? functionTextName = null)
    {
        if (!node.IsValid()) return null;
        if (node!.Is(type))
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
