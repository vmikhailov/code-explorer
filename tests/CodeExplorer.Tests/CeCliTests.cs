using CodeExplorer.Core.Common;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class CeCliTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ce_cli_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Test]
    public async Task CeCli_FullWorkflow_InitScanStatusQueryClear()
    {
        // 1. ce init MyTestWorkspace -d <tempDir>
        var initExit = await Program.Main(["init", "MyTestWorkspace", "-d", _tempDir]);
        Assert.That(initExit, Is.EqualTo(0));

        var ws = WorkspaceLocator.Find(_tempDir);
        Assert.That(ws, Is.Not.Null);
        Assert.That(File.Exists(ws!.DbPath), Is.True);

        // 2. Add a sample C# project
        var srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(Path.Combine(srcDir, "sample.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(srcDir, "OrderService.cs"), "public class OrderService { public void Process() {} }");

        // 3. ce scan <tempDir>
        var scanExit = await Program.Main(["scan", _tempDir]);
        Assert.That(scanExit, Is.EqualTo(0));

        // 4. ce status -d <tempDir> --json
        var originalOut = Console.Out;
        using var statusSw = new StringWriter();
        Console.SetOut(statusSw);
        var statusExit = await Program.Main(["status", "-d", _tempDir, "--json"]);
        Console.SetOut(originalOut);

        Assert.That(statusExit, Is.EqualTo(0));
        var statusJson = statusSw.ToString();
        Assert.That(statusJson, Does.Contain("MyTestWorkspace"));

        // 5. ce query "MATCH (t:Type) RETURN t.name AS name" -d <tempDir> --format json
        using var querySw = new StringWriter();
        Console.SetOut(querySw);
        var queryExit = await Program.Main(["query", "MATCH (t:Type) RETURN t.name AS name", "-d", _tempDir, "--format", "json"]);
        Console.SetOut(originalOut);

        Assert.That(queryExit, Is.EqualTo(0));
        var queryJson = querySw.ToString();
        Assert.That(queryJson, Does.Contain("OrderService"));

        // 6. ce clear -d <tempDir> -y
        var clearExit = await Program.Main(["clear", "-d", _tempDir, "-y"]);
        Assert.That(clearExit, Is.EqualTo(0));
    }

    [Test]
    public async Task CeScan_WhenNotInWorkspace_ReturnsExitCode1()
    {
        var nonWs = Path.Combine(_tempDir, "isolated");
        Directory.CreateDirectory(nonWs);

        var exit = await Program.Main(["scan", nonWs]);
        Assert.That(exit, Is.EqualTo(1));
    }

    [Test]
    public async Task CeCli_NoArgs_PrintsWelcomeAndHelp_Returns0()
    {
        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        var exit = await Program.Main([]);
        Console.SetOut(originalOut);

        Assert.That(exit, Is.EqualTo(0));
        var output = sw.ToString();
        Assert.That(output, Does.Contain("CodeExplorer (ce)"));
        Assert.That(output, Does.Contain("QUICK START WORKFLOW"));
        Assert.That(output, Does.Contain("AVAILABLE COMMANDS"));
        Assert.That(output, Does.Contain("EXAMPLES"));
    }

    [Test]
    public async Task CeQueries_ListsBuiltInQueriesAndCategories()
    {
        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        var exit = await Program.Main(["queries", "-d", _tempDir]);
        Console.SetOut(originalOut);

        Assert.That(exit, Is.EqualTo(0));
        var output = sw.ToString();
        Assert.That(output, Does.Contain("CodeExplorer Queries"));
        Assert.That(output, Does.Contain("[Architecture]"));
        Assert.That(output, Does.Contain("[Refactoring]"));
        Assert.That(output, Does.Contain("[Symbols]"));
        Assert.That(output, Does.Contain("[Taxonomy]"));
        Assert.That(output, Does.Contain("get_architecture_map_workspace"));
        Assert.That(output, Does.Contain("find_refactor_dead_code"));
    }

    [Test]
    public async Task CeQuery_List_JsonFormat_ReturnsStructuredJson()
    {
        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        var exit = await Program.Main(["query", "-l", "--format", "json", "-d", _tempDir]);
        Console.SetOut(originalOut);

        Assert.That(exit, Is.EqualTo(0));
        var output = sw.ToString();
        Assert.That(output, Does.Contain("\"built_in\""));
        Assert.That(output, Does.Contain("\"get_architecture_map_workspace\""));
    }

    [Test]
    public async Task CeQuery_Show_PrintsCypherSource()
    {
        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        var exit = await Program.Main(["query", "--show", "get_architecture_map_workspace"]);
        Console.SetOut(originalOut);

        Assert.That(exit, Is.EqualTo(0));
        var output = sw.ToString();
        Assert.That(output, Does.Contain("// Built-in Query: get_architecture_map_workspace"));
        Assert.That(output, Does.Contain("MATCH (w:Workspace)"));
    }

    [Test]
    public async Task CeQuery_CustomQuery_ListedAndExecuted()
    {
        // 1. Initialize workspace
        var initExit = await Program.Main(["init", "CustomQueryTest", "-d", _tempDir]);
        Assert.That(initExit, Is.EqualTo(0));

        var ws = WorkspaceLocator.Find(_tempDir)!;

        // 2. Add custom query file
        var customCypher = "MATCH (w:Workspace) RETURN w.name AS ws_name";
        var customFile = Path.Combine(ws.QueriesDirectory, "get_my_ws.cypher");
        await File.WriteAllTextAsync(customFile, customCypher);

        var customMeta = """
        {
          "name": "get_my_ws",
          "description": "Returns current workspace name"
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(ws.QueriesDirectory, "get_my_ws.json"), customMeta);

        // 3. ce queries lists custom query
        var originalOut = Console.Out;
        using var listSw = new StringWriter();
        Console.SetOut(listSw);
        var listExit = await Program.Main(["queries", "-d", _tempDir]);
        Console.SetOut(originalOut);

        Assert.That(listExit, Is.EqualTo(0));
        var listOutput = listSw.ToString();
        Assert.That(listOutput, Does.Contain("get_my_ws"));
        Assert.That(listOutput, Does.Contain("Returns current workspace name"));

        // 4. ce query --show get_my_ws
        using var showSw = new StringWriter();
        Console.SetOut(showSw);
        var showExit = await Program.Main(["query", "--show", "get_my_ws", "-d", _tempDir]);
        Console.SetOut(originalOut);

        Assert.That(showExit, Is.EqualTo(0));
        var showOutput = showSw.ToString();
        Assert.That(showOutput, Does.Contain("// Custom Query: get_my_ws"));
        Assert.That(showOutput, Does.Contain(customCypher));

        // 5. ce query -n get_my_ws --format json
        using var runSw = new StringWriter();
        Console.SetOut(runSw);
        var runExit = await Program.Main(["query", "-n", "get_my_ws", "-d", _tempDir, "--format", "json"]);
        Console.SetOut(originalOut);

        Assert.That(runExit, Is.EqualTo(0));
        var runJson = runSw.ToString();
        Assert.That(runJson, Does.Contain("CustomQueryTest"));
    }

    [Test]
    public async Task CeQuery_ShortJsonFlag_OutputsJson()
    {
        var initExit = await Program.Main(["init", "JsonFlagTest", "-d", _tempDir]);
        Assert.That(initExit, Is.EqualTo(0));

        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        var exit = await Program.Main(["query", "MATCH (w:Workspace) RETURN w.name AS name", "-d", _tempDir, "-j"]);
        Console.SetOut(originalOut);

        Assert.That(exit, Is.EqualTo(0));
        var output = sw.ToString();
        Assert.That(output, Does.Contain("\"name\": \"JsonFlagTest\""));
    }

    [Test]
    public async Task CeQuery_NoTruncate_DoesNotCutoffLongStrings()
    {
        var initExit = await Program.Main(["init", "LongStringTest", "-d", _tempDir]);
        Assert.That(initExit, Is.EqualTo(0));

        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        var longString = new string('A', 120);
        var exit = await Program.Main(["query", $"MATCH (w:Workspace) RETURN '{longString}' AS long_val", "-d", _tempDir, "--no-truncate"]);
        Console.SetOut(originalOut);

        Assert.That(exit, Is.EqualTo(0));
        var output = sw.ToString();
        Assert.That(output, Does.Contain(longString));
        Assert.That(output, Does.Not.Contain("..."));
    }
}
