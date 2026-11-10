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
            var projRes = await client.ExecuteCypherAsync("MATCH (p:Project) RETURN p.name AS name, p.role AS role, p.kind AS kind, p.path AS path");
            projRes.Count.Should().Be(1, "Should detect exactly one ScriptProject for the loose scripts folder");
            var proj = projRes.Rows[0];
            proj.GetStringProp("name").Should().Be("scripts");
            proj.GetStringProp("role").Should().Be(nameof(ProjectRole.ScriptProject));
            proj.GetStringProp("kind").Should().Be(nameof(ProjectEntityKind.ScriptProject));

            // 2. Verify DECLARED_IN edges connect functions to their file (especially in subfolders)
            var fnRes = await client.ExecuteCypherAsync(@"
                MATCH (fn:Function)-[:DECLARED_IN]->(f:File)
                RETURN fn.name AS fnName, f.path AS filePath
            ");
            fnRes.Count.Should().Be(4, "All 4 functions should have valid DECLARED_IN edges");

            var helperFn = fnRes.Rows.FirstOrDefault(x => x.GetStringProp("fnName") == "format_table");
            helperFn.FieldCount.Should().BeGreaterThan(0);
            helperFn.GetStringProp("filePath").Should().Be("scripts/helpers/util.py");

            // 3. Verify get_file_outline query returns functions for subfolder file
            var outlineQuery = Queries.Get("get_file_outline");
            var outlineRes = await client.ExecuteCypherAsync(outlineQuery, new
            {
                filePath = "scripts/helpers/util.py"
            });
            outlineRes.Count.Should().BeGreaterThan(0, "get_file_outline must find functions declared in scripts/helpers/util.py");
            var fnNames = outlineRes.Rows.Select(r => r.GetStringProp("name")).ToList();
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
