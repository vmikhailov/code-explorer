using CodeExplorer.Commands;
using CodeExplorer.Core.Common;
using CodeExplorer.Options;
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

    private static async Task<(int ExitCode, string Output, string Error)> RunCliAsync(params string[] args)
    {
        var prevOut = Console.Out;
        var prevErr = Console.Error;

        using var outSw = new StringWriter();

        using var errSw = new StringWriter();

        try
        {
            Console.SetOut(outSw);
            Console.SetError(errSw);
            var exitCode = await Program.Main(args);
            return (exitCode, outSw.ToString(), errSw.ToString());
        }
        finally
        {
            Console.SetOut(prevOut);
            Console.SetError(prevErr);
        }
    }

    [Test]
    public async Task CeCli_FullWorkflow_InitScanStatusQueryClear()
    {
        // 1. ce init MyTestWorkspace -d <tempDir>
        var (initExit, _, _) = await RunCliAsync("init", "MyTestWorkspace", "-d", _tempDir);
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
        var (scanExit, _, _) = await RunCliAsync("scan", _tempDir);
        Assert.That(scanExit, Is.EqualTo(0));

        // 4. ce status -d <tempDir> --json
        var (statusExit, statusJson, _) = await RunCliAsync("status", "-d", _tempDir, "--json");
        Assert.That(statusExit, Is.EqualTo(0));
        Assert.That(statusJson, Does.Contain("MyTestWorkspace"));

        // 5. ce query "MATCH (t:Type) RETURN t.name AS name" -d <tempDir> --format json
        var (queryExit, queryJson, _) = await RunCliAsync("query", "MATCH (t:Type) RETURN t.name AS name", "-d", _tempDir, "--format", "json");
        Assert.That(queryExit, Is.EqualTo(0));
        Assert.That(queryJson, Does.Contain("OrderService"));

        // 6. ce clear -d <tempDir> -y
        var (clearExit, _, _) = await RunCliAsync("clear", "-d", _tempDir, "-y");
        Assert.That(clearExit, Is.EqualTo(0));
    }

    [Test]
    public async Task CeScan_WhenNotInWorkspace_ReturnsExitCode1()
    {
        var nonWs = Path.Combine(_tempDir, "isolated");
        Directory.CreateDirectory(nonWs);

        var (exit, _, _) = await RunCliAsync("scan", nonWs);
        Assert.That(exit, Is.EqualTo(1));
    }

    [Test]
    public async Task CeCli_NoArgs_PrintsWelcomeAndHelp_Returns0()
    {
        var (exit, output, _) = await RunCliAsync();

        Assert.That(exit, Is.EqualTo(0));
        Assert.That(output, Does.Contain("CodeExplorer (ce)"));
        Assert.That(output, Does.Contain("QUICK START WORKFLOW"));
        Assert.That(output, Does.Contain("AVAILABLE COMMANDS"));
        Assert.That(output, Does.Contain("EXAMPLES"));
    }

    [Test]
    public async Task CeQueries_ListsBuiltInQueriesAndCategories()
    {
        var (exit, output, _) = await RunCliAsync("queries", "-d", _tempDir);

        Assert.That(exit, Is.EqualTo(0));
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
        var (exit, output, _) = await RunCliAsync("query", "-l", "--format", "json", "-d", _tempDir);

        Assert.That(exit, Is.EqualTo(0));
        Assert.That(output, Does.Contain("\"built_in\""));
        Assert.That(output, Does.Contain("\"get_architecture_map_workspace\""));
    }

    [Test]
    public async Task CeQuery_Show_PrintsCypherSource()
    {
        var (exit, output, _) = await RunCliAsync("query", "--show", "get_architecture_map_workspace");

        Assert.That(exit, Is.EqualTo(0));
        Assert.That(output, Does.Contain("// Built-in Query: get_architecture_map_workspace"));
        Assert.That(output, Does.Contain("MATCH (w:Workspace)"));
    }

    [Test]
    public async Task CeQuery_CustomQuery_ListedAndExecuted()
    {
        // 1. Initialize workspace
        var (initExit, _, _) = await RunCliAsync("init", "CustomQueryTest", "-d", _tempDir);
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
        var (listExit, listOutput, _) = await RunCliAsync("queries", "-d", _tempDir);
        Assert.That(listExit, Is.EqualTo(0));
        Assert.That(listOutput, Does.Contain("get_my_ws"));
        Assert.That(listOutput, Does.Contain("Returns current workspace name"));

        // 4. ce query --show get_my_ws
        var (showExit, showOutput, _) = await RunCliAsync("query", "--show", "get_my_ws", "-d", _tempDir);
        Assert.That(showExit, Is.EqualTo(0));
        Assert.That(showOutput, Does.Contain("// Custom Query: get_my_ws"));
        Assert.That(showOutput, Does.Contain(customCypher));

        // 5. ce query -n get_my_ws --format json
        var (runExit, runJson, _) = await RunCliAsync("query", "-n", "get_my_ws", "-d", _tempDir, "--format", "json");
        Assert.That(runExit, Is.EqualTo(0));
        Assert.That(runJson, Does.Contain("CustomQueryTest"));
    }

    [Test]
    public async Task CeQuery_ShortJsonFlag_OutputsJson()
    {
        var (initExit, _, _) = await RunCliAsync("init", "JsonFlagTest", "-d", _tempDir);
        Assert.That(initExit, Is.EqualTo(0));

        var (exit, output, _) = await RunCliAsync("query", "MATCH (w:Workspace) RETURN w.name AS name", "-d", _tempDir, "-j");

        Assert.That(exit, Is.EqualTo(0));
        Assert.That(output, Does.Contain("\"name\": \"JsonFlagTest\""));
    }

    [Test]
    public async Task CeQuery_NoTruncate_DoesNotCutoffLongStrings()
    {
        var (initExit, _, _) = await RunCliAsync("init", "LongStringTest", "-d", _tempDir);
        Assert.That(initExit, Is.EqualTo(0));

        var longString = new string('A', 120);
        var (exit, output, _) = await RunCliAsync("query", $"MATCH (w:Workspace) RETURN '{longString}' AS long_val", "-d", _tempDir, "--no-truncate");

        Assert.That(exit, Is.EqualTo(0));
        Assert.That(output, Does.Contain(longString));
        Assert.That(output, Does.Not.Contain("..."));
    }

    [Test]
    public async Task CeTest_WhenNotInWorkspace_ReturnsErrorAndExitCode1()
    {
        var nonWs = Path.Combine(_tempDir, "EmptyNonWs");
        Directory.CreateDirectory(nonWs);

        var (exit, output, error) = await RunCliAsync("test", "-d", nonWs);

        Assert.That(exit, Is.EqualTo(1));
        var combined = output + error;
        Assert.That(combined, Does.Contain("Error: No CodeExplorer workspace found"));
    }

    [Test]
    public async Task CeTest_WhenWorkspaceNotIndexed_ReturnsErrorAndExitCode1()
    {
        // Directory with .codeexplorer folder but without graph.db
        Directory.CreateDirectory(Path.Combine(_tempDir, WorkspaceLocator.FolderName));

        var (exit, output, error) = await RunCliAsync("test", "-d", _tempDir);

        Assert.That(exit, Is.EqualTo(1));
        var combined = output + error;
        Assert.That(combined, Does.Contain("database has not been initialized yet"));
    }

    [Test]
    public void BaseCommandHandler_HasTarget_MatchesCandidateAliases()
    {
        Assert.That(BaseCommandHandler.HasTarget("context", "context", "bounded-context"), Is.True);
        Assert.That(BaseCommandHandler.HasTarget("BOUNDED-CONTEXT", "context", "bounded-context"), Is.True);
        Assert.That(BaseCommandHandler.HasTarget("  layers  ", "layers", "ontology"), Is.True);
        Assert.That(BaseCommandHandler.HasTarget("other", "context", "bounded-context"), Is.False);
        Assert.That(BaseCommandHandler.HasTarget(null, "context", "bounded-context"), Is.False);
        Assert.That(BaseCommandHandler.HasTarget("   ", "context"), Is.False);

        var viewOpts = new ViewOptions { Target = "context-map" };
        Assert.That(viewOpts.HasTarget("context", "bounded-context", "context-map"), Is.True);
        Assert.That(BaseCommandHandler.HasTarget(viewOpts.Target, "context-map"), Is.True);
    }

    [Test]
    public void BaseCommandHandler_IsOneOf_And_IsFormat()
    {
        Assert.That(BaseCommandHandler.IsOneOf("coverage", "coverage", "test"), Is.True);
        Assert.That(BaseCommandHandler.IsOneOf("Y", "y", "yes"), Is.True);
        Assert.That(BaseCommandHandler.IsOneOf("no", "y", "yes"), Is.False);

        Assert.That(BaseCommandHandler.IsFormat("JSON", "json"), Is.True);
        Assert.That(BaseCommandHandler.IsJsonFormat("json"), Is.True);
        Assert.That(BaseCommandHandler.IsJsonFormat("markdown", jsonFlag: true), Is.True);
        Assert.That(BaseCommandHandler.IsJsonFormat("markdown", jsonFlag: false), Is.False);
    }

    [Test]
    public void BaseCommandHandler_ParseList_SplitsAndCleansTokens()
    {
        var result = BaseCommandHandler.ParseList(" file1.cs, file2.ts ; file3.java ; ");
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo(new[] { "file1.cs", "file2.ts", "file3.java" }));

        Assert.That(BaseCommandHandler.ParseList(null), Is.Null);
        Assert.That(BaseCommandHandler.ParseList("   "), Is.Null);
        Assert.That(BaseCommandHandler.ParseList(";;,,"), Is.Null);
    }

    [Test]
    public void BaseCommandHandler_FileHelpers_WorkCorrectly()
    {
        Assert.That(BaseCommandHandler.ContainsAny(new[] { "apple", "banana" }, "BANANA", "cherry"), Is.True);
        Assert.That(BaseCommandHandler.ContainsAny(new[] { "apple", "banana" }, "cherry", "date"), Is.False);

        Assert.That(BaseCommandHandler.HasExtension("foo/bar.cs", "cs", "ts"), Is.True);
        Assert.That(BaseCommandHandler.HasExtension("foo/bar.CS", ".cs"), Is.True);
        Assert.That(BaseCommandHandler.HasExtension("foo/bar.py", "cs", "ts"), Is.False);

        Assert.That(BaseCommandHandler.HasAnyWithExtension(new[] { "foo.js", "bar.ts" }, "ts"), Is.True);
        Assert.That(BaseCommandHandler.HasAnyWithExtension(new[] { "foo.js", "bar.ts" }, "py"), Is.False);

        Assert.That(BaseCommandHandler.HasFileName("path/to/package.json", "package.json", "pom.xml"), Is.True);
        Assert.That(BaseCommandHandler.HasFileName("path/to/other.txt", "package.json"), Is.False);
    }
}
