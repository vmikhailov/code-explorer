using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.Python;
using CodeExplorer.Parser.SQL;
using CodeExplorer.Parser.TypeScript;
using CodeExplorer.Tests.Shared;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class FileBasedParserTests
{
    private readonly TypeScriptFileParser _tsParser = new();
    private readonly CSharpFileParser _csParser = new();
    private readonly PythonFileParser _pyParser = new();
    private readonly GoFileParser _goParser = new();
    private readonly SqlFileParser _sqlParser = new();

    [Test]
    [ParserFileSource("SingleFiles/TypeScript", "*.ts.test")]
    public async Task TypeScript_Files_ParseSuccessfully(string filePath)
    {
        File.Exists(filePath).Should().BeTrue($"File does not exist: {filePath}");
        var workspacePath = Path.GetDirectoryName(filePath)!;

        using var syntaxTree = await _tsParser.ParseAsync(filePath, "parent-id", "ws-id", workspacePath);

        syntaxTree.Should().NotBeNull();
        syntaxTree.FileNode.Should().NotBeNull();
        syntaxTree.Tree.Should().NotBeNull();
    }

    [Test]
    [ParserFileSource("SingleFiles/CSharp", "*.cs.test")]
    public async Task CSharp_Files_ParseSuccessfully(string filePath)
    {
        File.Exists(filePath).Should().BeTrue($"File does not exist: {filePath}");
        var workspacePath = Path.GetDirectoryName(filePath)!;

        using var syntaxTree = await _csParser.ParseAsync(filePath, "parent-id", "ws-id", workspacePath);

        syntaxTree.Should().NotBeNull();
        syntaxTree.FileNode.Should().NotBeNull();
        syntaxTree.Tree.Should().NotBeNull();
    }

    [Test]
    [ParserFileSource("SingleFiles/Python", "*.py.test")]
    public async Task Python_Files_ParseSuccessfully(string filePath)
    {
        File.Exists(filePath).Should().BeTrue($"File does not exist: {filePath}");
        var workspacePath = Path.GetDirectoryName(filePath)!;

        using var syntaxTree = await _pyParser.ParseAsync(filePath, "parent-id", "ws-id", workspacePath);

        syntaxTree.Should().NotBeNull();
        syntaxTree.FileNode.Should().NotBeNull();
        syntaxTree.Tree.Should().NotBeNull();
    }

    [Test]
    [ParserFileSource("SingleFiles/Go", "*.go.test")]
    public async Task Go_Files_ParseSuccessfully(string filePath)
    {
        File.Exists(filePath).Should().BeTrue($"File does not exist: {filePath}");
        var workspacePath = Path.GetDirectoryName(filePath)!;

        using var syntaxTree = await _goParser.ParseAsync(filePath, "parent-id", "ws-id", workspacePath);

        syntaxTree.Should().NotBeNull();
        syntaxTree.FileNode.Should().NotBeNull();
        syntaxTree.Tree.Should().NotBeNull();
    }

    [Test]
    [ParserFileSource("SingleFiles/SQL", "*.sql.test")]
    public async Task SQL_Files_ParseSuccessfully(string filePath)
    {
        File.Exists(filePath).Should().BeTrue($"File does not exist: {filePath}");
        var workspacePath = Path.GetDirectoryName(filePath)!;

        using var syntaxTree = await _sqlParser.ParseAsync(filePath, "parent-id", "ws-id", workspacePath);

        syntaxTree.Should().NotBeNull();
        syntaxTree.FileNode.Should().NotBeNull();
    }
}
