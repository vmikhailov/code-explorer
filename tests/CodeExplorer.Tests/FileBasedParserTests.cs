using NUnit.Framework;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.Python;
using CodeExplorer.Parser.SQL;
using CodeExplorer.Parser.TypeScript;
using CodeExplorer.Tests.Shared;

namespace CodeExplorer.Tests;

[TestFixture]
public class FileBasedParserTests
{
    private readonly TypeScriptParser _tsParser = new();
    private readonly CSharpParser _csParser = new();
    private readonly PythonParser _pyParser = new();
    private readonly GoParser _goParser = new();
    private readonly SqlParser _sqlParser = new();

    [Test]
    [ParserFileSource("SingleFiles/TypeScript", "*.ts.test")]
    public async Task TypeScript_Files_ParseSuccessfully(string fixturePath)
    public async Task TypeScript_Files_ParseSuccessfully(string filePath)
    {
        using var tempFile = ParserTestData.GetPreparedFile(fixturePath);
        using var syntaxTree = await _tsParser.ParseAsync(tempFile.FilePath, "parent-id", "ws-id", tempFile.DirectoryPath);
        Assert.That(File.Exists(filePath), Is.True, $"File does not exist: {filePath}");
        var workspacePath = Path.GetDirectoryName(filePath)!;
        using var syntaxTree = await _tsParser.ParseAsync(filePath, "parent-id", "ws-id", workspacePath);

        Assert.That(syntaxTree, Is.Not.Null);
        Assert.That(syntaxTree.FileNode, Is.Not.Null);
        Assert.That(syntaxTree.Tree, Is.Not.Null);
    }

    [Test]
    [ParserFileSource("SingleFiles/CSharp", "*.cs.test")]
    public async Task CSharp_Files_ParseSuccessfully(string fixturePath)
    public async Task CSharp_Files_ParseSuccessfully(string filePath)
    {
        using var tempFile = ParserTestData.GetPreparedFile(fixturePath);
        using var syntaxTree = await _csParser.ParseAsync(tempFile.FilePath, "parent-id", "ws-id", tempFile.DirectoryPath);
        Assert.That(File.Exists(filePath), Is.True, $"File does not exist: {filePath}");
        var workspacePath = Path.GetDirectoryName(filePath)!;
        using var syntaxTree = await _csParser.ParseAsync(filePath, "parent-id", "ws-id", workspacePath);

        Assert.That(syntaxTree, Is.Not.Null);
        Assert.That(syntaxTree.FileNode, Is.Not.Null);
        Assert.That(syntaxTree.Tree, Is.Not.Null);
    }

    [Test]
    [ParserFileSource("SingleFiles/Python", "*.py.test")]
    public async Task Python_Files_ParseSuccessfully(string fixturePath)
    public async Task Python_Files_ParseSuccessfully(string filePath)
    {
        using var tempFile = ParserTestData.GetPreparedFile(fixturePath);
        using var syntaxTree = await _pyParser.ParseAsync(tempFile.FilePath, "parent-id", "ws-id", tempFile.DirectoryPath);
        Assert.That(File.Exists(filePath), Is.True, $"File does not exist: {filePath}");
        var workspacePath = Path.GetDirectoryName(filePath)!;
        using var syntaxTree = await _pyParser.ParseAsync(filePath, "parent-id", "ws-id", workspacePath);

        Assert.That(syntaxTree, Is.Not.Null);
        Assert.That(syntaxTree.FileNode, Is.Not.Null);
        Assert.That(syntaxTree.Tree, Is.Not.Null);
    }

    [Test]
    [ParserFileSource("SingleFiles/Go", "*.go.test")]
    public async Task Go_Files_ParseSuccessfully(string fixturePath)
    public async Task Go_Files_ParseSuccessfully(string filePath)
    {
        using var tempFile = ParserTestData.GetPreparedFile(fixturePath);
        using var syntaxTree = await _goParser.ParseAsync(tempFile.FilePath, "parent-id", "ws-id", tempFile.DirectoryPath);
        Assert.That(File.Exists(filePath), Is.True, $"File does not exist: {filePath}");
        var workspacePath = Path.GetDirectoryName(filePath)!;
        using var syntaxTree = await _goParser.ParseAsync(filePath, "parent-id", "ws-id", workspacePath);

        Assert.That(syntaxTree, Is.Not.Null);
        Assert.That(syntaxTree.FileNode, Is.Not.Null);
        Assert.That(syntaxTree.Tree, Is.Not.Null);
    }

    [Test]
    [ParserFileSource("SingleFiles/SQL", "*.sql.test")]
    public async Task SQL_Files_ParseSuccessfully(string fixturePath)
    public async Task SQL_Files_ParseSuccessfully(string filePath)
    {
        using var tempFile = ParserTestData.GetPreparedFile(fixturePath);
        using var syntaxTree = await _sqlParser.ParseAsync(tempFile.FilePath, "parent-id", "ws-id", tempFile.DirectoryPath);
        Assert.That(File.Exists(filePath), Is.True, $"File does not exist: {filePath}");
        var workspacePath = Path.GetDirectoryName(filePath)!;
        using var syntaxTree = await _sqlParser.ParseAsync(filePath, "parent-id", "ws-id", workspacePath);

        Assert.That(syntaxTree, Is.Not.Null);
        Assert.That(syntaxTree.FileNode, Is.Not.Null);
    }
}
