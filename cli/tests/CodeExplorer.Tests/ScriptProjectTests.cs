using System.Text.Json;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[NonParallelizable]
[Category("Integration")]
public class ScriptProjectTests
{
    [Test]
    public async Task Test_ScriptProject_LooseScriptsDetected_AndFunctionsHaveValidDeclaredIn()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "codeexplorer_script_proj_test_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            // Folder with loose Python scripts (no pyproject.toml, no setup.py)
            var scriptsDir = Path.Combine(tempWorkspace, "scripts").Replace('\\', '/');
            var subDir = Path.Combine(scriptsDir, "helpers").Replace('\\', '/');
            Directory.CreateDirectory(subDir);

            // Script in root of scripts/
            var rootScriptPath = Path.Combine(scriptsDir, "lineage_viewer.py").Replace('\\', '/');
            var rootScriptCode = @"
def render_graph(data):
    pass

def export_json(graph):
    pass
";
            await File.WriteAllTextAsync(rootScriptPath, rootScriptCode);

            // Script in subfolder scripts/helpers/
            var subScriptPath = Path.Combine(subDir, "util.py").Replace('\\', '/');
            var subScriptCode = @"
def format_table(name):
    pass

def sanitize_input(val):
    pass
";
            await File.WriteAllTextAsync(subScriptPath, subScriptCode);

            var dbPath = Path.Combine(tempWorkspace, "test_scripts.db");
            await using var client = new SqliteGraphClient(dbPath);

            WorkspaceIndexer.AutoRegisterParsers();
            var indexer = new WorkspaceIndexer(client);

            var indexResult = await indexer.IndexAsync(tempWorkspace, clear: true);
            indexResult.NodesCount.Should().BeGreaterThan(0);

            // 1. Verify ScriptProject is detected with Role = ScriptProject and Kind = ScriptProject
            var projJson = await client.ExecuteQueryAsync("MATCH (p:Project) RETURN p.name AS name, p.role AS role, p.kind AS kind, p.path AS path");
            using var projDoc = JsonDocument.Parse(projJson);
            var projList = projDoc.RootElement.EnumerateArray().ToList();
            projList.Count.Should().Be(1, "Should detect exactly one ScriptProject for the loose scripts folder");
            var proj = projList[0];
            proj.GetProperty("name").GetString().Should().Be("scripts");
            proj.GetProperty("role").GetString().Should().Be(nameof(ProjectRole.ScriptProject));
            proj.GetProperty("kind").GetString().Should().Be(nameof(ProjectEntityKind.ScriptProject));

            // 2. Verify DECLARED_IN edges connect functions to their file (especially in subfolders)
            var fnJson = await client.ExecuteQueryAsync(@"
                MATCH (fn:Function)-[:DECLARED_IN]->(f:File)
                RETURN fn.name AS fnName, f.path AS filePath
            ");
            using var fnDoc = JsonDocument.Parse(fnJson);
            var fnList = fnDoc.RootElement.EnumerateArray().ToList();
            fnList.Count.Should().Be(4, "All 4 functions should have valid DECLARED_IN edges");

            var helperFn = fnList.FirstOrDefault(x => x.GetProperty("fnName").GetString() == "format_table");
            helperFn.ValueKind.Should().NotBe(JsonValueKind.Undefined);
            helperFn.GetProperty("filePath").GetString().Should().Be("scripts/helpers/util.py");

            // 3. Verify get_file_outline query returns functions for subfolder file
            var outlineQuery = Queries.Get("get_file_outline");
            var outlineJson = await client.ExecuteQueryAsync(outlineQuery, new Dictionary<string, object?>
            {
                ["filePath"] = "scripts/helpers/util.py"
            });
            using var outlineDoc = JsonDocument.Parse(outlineJson);
            var outlineRows = outlineDoc.RootElement.EnumerateArray().ToList();
            outlineRows.Count.Should().BeGreaterThan(0, "get_file_outline must find functions declared in scripts/helpers/util.py");
            var fnNames = outlineRows.Select(r => r.GetProperty("name").GetString()).ToList();
            fnNames.Should().Contain("format_table");
            fnNames.Should().Contain("sanitize_input");
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                try { Directory.Delete(tempWorkspace, true); } catch { }
            }
        }
    }
}
