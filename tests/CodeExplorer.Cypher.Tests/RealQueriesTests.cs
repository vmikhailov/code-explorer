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

    [Test]
    public void Test_PrintArchitectureMapSql()
    {
        var filePath = Path.Combine(_queriesDir, "get_architecture_map_workspace.cypher");
        var rawText = File.ReadAllText(filePath);
        var ast = CypherQueryParser.Parse(rawText);
        var compiled = SqliteCompiler.Compile(ast);
        TestContext.WriteLine("=== COMPILED SQL ===");
        TestContext.WriteLine(compiled.Sql);
        TestContext.WriteLine("====================");
    }

    [Test]
    public void Test_Benchmark_ArchitectureMap_OnRealDb()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CodeExplorer.slnx")))
        {
            dir = dir.Parent;
        }
        var dbPath = Path.Combine(dir!.FullName, ".codeexplorer", "graph.db");
        if (!File.Exists(dbPath)) return;

        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        conn.Open();
        Shared.SqliteCypherFunctions.Register(conn);

        // Find workspaces
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, json_extract(properties, '$.name'), json_extract(properties, '$.path') FROM nodes WHERE kind = 'Workspace';";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                TestContext.WriteLine($"Workspace: id={reader.GetString(0)}, name={reader.GetString(1)}, path={reader.GetString(2)}");
            }
        }

        var filePath = Path.Combine(_queriesDir, "get_architecture_map_workspace.cypher");
        var rawText = File.ReadAllText(filePath);
        var ast = CypherQueryParser.Parse(rawText);
        var compiled = SqliteCompiler.Compile(ast, new Dictionary<string, object?> { ["workspaceId"] = "3" });

        // Measure execution time of compiled query
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = compiled.Sql;
            cmd.Parameters.AddWithValue("@workspaceId", "3");
            using var reader = cmd.ExecuteReader();
            int rows = 0;
            while (reader.Read()) rows++;
            sw.Stop();
            TestContext.WriteLine($"Compiled query executed in {sw.ElapsedMilliseconds}ms, rows: {rows}");
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1000), $"Query took {sw.ElapsedMilliseconds}ms, expected under 1000ms");
        }

        var allPath = Path.Combine(_queriesDir, "get_architecture_map_all.cypher");
        var allAst = CypherQueryParser.Parse(File.ReadAllText(allPath));
        var allCompiled = SqliteCompiler.Compile(allAst);
        sw.Restart();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = allCompiled.Sql;
            using var reader = cmd.ExecuteReader();
            int rows = 0;
            while (reader.Read()) rows++;
            sw.Stop();
            TestContext.WriteLine($"All workspaces compiled query executed in {sw.ElapsedMilliseconds}ms, rows: {rows}");
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1000), $"Query took {sw.ElapsedMilliseconds}ms, expected under 1000ms");
        }
    }
}
