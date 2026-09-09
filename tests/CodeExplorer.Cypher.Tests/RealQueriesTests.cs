using System.IO;
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

    [TestCase("find_symbol_all.cypher")]
    [TestCase("find_symbol_class.cypher")]
    [TestCase("find_symbol_function.cypher")]
    [TestCase("find_symbol_interface.cypher")]
    [TestCase("get_file_outline.cypher")]
    [TestCase("get_file_outline_no_ws.cypher")]
    [TestCase("get_project_dependencies_all.cypher")]
    [TestCase("get_project_dependencies_all_no_ws.cypher")]
    [TestCase("get_all_workspaces.cypher")]
    [TestCase("get_workspace_id.cypher")]
    [TestCase("get_call_chain.cypher")]
    [TestCase("get_call_chain_no_ws.cypher")]
    [TestCase("resolve_call_target.cypher")]
    [TestCase("resolve_call_target_no_ws.cypher")]
    [TestCase("get_project_entry_points.cypher")]
    [TestCase("get_project_entry_points_no_ws.cypher")]
    [TestCase("get_project_dependencies_filtered.cypher")]
    [TestCase("get_project_dependencies_filtered_no_ws.cypher")]
    [TestCase("inspect_data_lineage.cypher")]
    [TestCase("inspect_data_lineage_no_ws.cypher")]
    [TestCase("analyze_code_impact.cypher")]
    [TestCase("analyze_code_impact_no_ws.cypher")]
    public void Test_CanParseAndCompile_RealQuery(string fileName)
    {
        var filePath = Path.Combine(_queriesDir, fileName);
        var rawText = File.ReadAllText(filePath);

        // Preprocess template placeholders as CodeExplorer does at runtime
        var preparedQuery = rawText
            .Replace("{prefixClause}", " AND n.id STARTS WITH $wsIdPrefix")
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
