using CodeExplorer.Cypher.Compiler;
using CodeExplorer.Cypher.Parser;
using NUnit.Framework;

namespace CodeExplorer.Cypher.Tests;

[TestFixture]
public class RealQueriesTests
{
    private string _queriesDir = null!;

    [SetUp]
    public void SetUp()
    {
        // Navigate from test output folder to queries folder
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CodeExplorer.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.That(dir, Is.Not.Null, "Could not find solution root");
        _queriesDir = Path.Combine(dir!.FullName, "src", "Core", "CodeExplorer.Core", "Resources", "Queries");
        Assert.That(Directory.Exists(_queriesDir), Is.True, $"Queries directory not found: {_queriesDir}");
    }

    public static IEnumerable<string> GetAllCypherQueryFiles()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CodeExplorer.slnx")))
        {
            dir = dir.Parent;
        }

        if (dir == null) yield break;
        var queriesDir = Path.Combine(dir.FullName, "src", "Core", "CodeExplorer.Core", "Resources", "Queries");
        if (!Directory.Exists(queriesDir)) yield break;

        foreach (var file in Directory.GetFiles(queriesDir, "*.cypher"))
        {
            yield return Path.GetFileName(file);
        }
    }

    [Test]
    public void Test_AllCypherFilesDiscovered()
    {
        var files = GetAllCypherQueryFiles().ToList();
        Assert.That(files.Count, Is.GreaterThanOrEqualTo(33), "Expected at least 33 cypher files in Resources/Queries");
    }

    [TestCaseSource(nameof(GetAllCypherQueryFiles))]
    public void Test_CanParseAndCompile_RealQuery(string fileName)
    {
        var filePath = Path.Combine(_queriesDir, fileName);
        var rawText = File.ReadAllText(filePath);

        // Preprocess template placeholders as CodeExplorer does at runtime
        var prefixClause = fileName.StartsWith("find_refactor_") ? " WHERE p.id STARTS WITH $wsIdPrefix" : " AND n.id STARTS WITH $wsIdPrefix";
        var preparedQuery = rawText
            .Replace("{prefixClause}", prefixClause)
            .Replace("{prefixFilter}", "WHERE p.id STARTS WITH $wsIdPrefix")
            .Replace("{depth}", "5");

        // 1. Parse
        var ast = CypherQueryParser.Parse(preparedQuery);
        Assert.That(ast, Is.Not.Null);

        // 2. Compile
        var compiled = SqliteCompiler.Compile(ast);
        Assert.That(compiled.Sql, Is.Not.Null.And.Not.Empty);
    }
}
