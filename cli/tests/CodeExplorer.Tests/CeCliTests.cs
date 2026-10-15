using AwesomeAssertions;
using CodeExplorer.Commands;
using CodeExplorer.Core.Common;
using CodeExplorer.Options;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[NonParallelizable]
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
        initExit.Should().Be(0);

        var ws = WorkspaceLocator.Find(_tempDir);
        ws.Should().NotBeNull();
        File.Exists(ws!.DbPath).Should().BeTrue();

        // 2. Add a sample C# project
        var srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(Path.Combine(srcDir, "sample.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(srcDir, "OrderService.cs"), "public class OrderService { public void Process() {} }");

        // 3. ce scan <tempDir>
        var (scanExit, _, _) = await RunCliAsync("scan", _tempDir);
        scanExit.Should().Be(0);

        // 4. ce status -d <tempDir> --json
        var (statusExit, statusJson, _) = await RunCliAsync("status", "-d", _tempDir, "--json");
        statusExit.Should().Be(0);
        statusJson.Should().Contain("MyTestWorkspace");

        // 5. ce query "MATCH (t:Type) RETURN t.name AS name" -d <tempDir> --format json
        var (queryExit, queryJson, _) = await RunCliAsync("query", "MATCH (t:Type) RETURN t.name AS name", "-d", _tempDir, "--format", "json");
        queryExit.Should().Be(0);
        queryJson.Should().Contain("OrderService");

        // 6. ce clear -d <tempDir> -y
        var (clearExit, _, _) = await RunCliAsync("clear", "-d", _tempDir, "-y");
        clearExit.Should().Be(0);
    }

    [Test]
    public async Task CeScan_WhenNotInWorkspace_ReturnsExitCode1()
    {
        var nonWs = Path.Combine(_tempDir, "isolated");
        Directory.CreateDirectory(nonWs);

        var (exit, _, _) = await RunCliAsync("scan", nonWs);
        exit.Should().Be(1);
    }

    [Test]
    public async Task CeCli_NoArgs_PrintsWelcomeAndHelp_Returns0()
    {
        var (exit, output, _) = await RunCliAsync();

        exit.Should().Be(0);
        output.Should().Contain("CodeExplorer (ce)");
        output.Should().Contain("QUICK START WORKFLOW");
        output.Should().Contain("AVAILABLE COMMANDS");
        output.Should().Contain("EXAMPLES");
    }

    [Test]
    public async Task CeQueries_ListsBuiltInQueriesAndCategories()
    {
        var (exit, output, _) = await RunCliAsync("queries", "-d", _tempDir);

        exit.Should().Be(0);
        output.Should().Contain("CodeExplorer Queries");
        output.Should().Contain("[Architecture]");
        output.Should().Contain("[Refactoring]");
        output.Should().Contain("[Symbols]");
        output.Should().Contain("[Taxonomy]");
        output.Should().Contain("get_architecture_map_workspace");
        output.Should().Contain("find_refactor_dead_code");
    }

    [Test]
    public async Task CeQuery_List_JsonFormat_ReturnsStructuredJson()
    {
        var (exit, output, _) = await RunCliAsync("query", "-l", "--format", "json", "-d", _tempDir);

        exit.Should().Be(0);
        output.Should().Contain("\"built_in\"");
        output.Should().Contain("\"get_architecture_map_workspace\"");
    }

    [Test]
    public async Task CeQuery_Show_PrintsCypherSource()
    {
        var (exit, output, _) = await RunCliAsync("query", "--show", "get_architecture_map_workspace");

        exit.Should().Be(0);
        output.Should().Contain("// Built-in Query: get_architecture_map_workspace");
        output.Should().Contain("MATCH (w:Workspace)");
    }

    [Test]
    public async Task CeQuery_CustomQuery_ListedAndExecuted()
    {
        // 1. Initialize workspace
        var (initExit, _, _) = await RunCliAsync("init", "CustomQueryTest", "-d", _tempDir);
        initExit.Should().Be(0);

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
        listExit.Should().Be(0);
        listOutput.Should().Contain("get_my_ws");
        listOutput.Should().Contain("Returns current workspace name");

        // 4. ce query --show get_my_ws
        var (showExit, showOutput, _) = await RunCliAsync("query", "--show", "get_my_ws", "-d", _tempDir);
        showExit.Should().Be(0);
        showOutput.Should().Contain("// Custom Query: get_my_ws");
        showOutput.Should().Contain(customCypher);

        // 5. ce query -n get_my_ws --format json
        var (runExit, runJson, _) = await RunCliAsync("query", "-n", "get_my_ws", "-d", _tempDir, "--format", "json");
        runExit.Should().Be(0);
        runJson.Should().Contain("CustomQueryTest");
    }

    [Test]
    public async Task CeQuery_ShortJsonFlag_OutputsJson()
    {
        var (initExit, _, _) = await RunCliAsync("init", "JsonFlagTest", "-d", _tempDir);
        initExit.Should().Be(0);

        var (exit, output, _) = await RunCliAsync("query", "MATCH (w:Workspace) RETURN w.name AS name", "-d", _tempDir, "-j");

        exit.Should().Be(0);
        output.Should().Contain("\"name\": \"JsonFlagTest\"");
    }

    [Test]
    public async Task CeQuery_NoTruncate_DoesNotCutoffLongStrings()
    {
        var (initExit, _, _) = await RunCliAsync("init", "LongStringTest", "-d", _tempDir);
        initExit.Should().Be(0);

        var longString = new string('A', 120);
        var (exit, output, _) = await RunCliAsync("query", $"MATCH (w:Workspace) RETURN '{longString}' AS long_val", "-d", _tempDir, "--no-truncate");

        exit.Should().Be(0);
        output.Should().Contain(longString);
        output.Should().NotContain("...");
    }

    [Test]
    public async Task CeQuery_CompactFlag_OutputsCompactJson()
    {
        var (initExit, _, _) = await RunCliAsync("init", "CompactJsonTest", "-d", _tempDir);
        initExit.Should().Be(0);

        var (exit, output, _) = await RunCliAsync("query", "MATCH (w:Workspace) RETURN w.name AS name", "-d", _tempDir, "-c");

        exit.Should().Be(0);
        output.Trim().Should().StartWith("[{\"name\":");
        output.Should().NotContain("\r\n  ");
    }

    [Test]
    public async Task CeQuery_ToonFormat_OutputsToon()
    {
        var (initExit, _, _) = await RunCliAsync("init", "ToonFormatTest", "-d", _tempDir);
        initExit.Should().Be(0);

        var (exit, output, _) = await RunCliAsync("query", "MATCH (w:Workspace) RETURN w.name AS name", "-d", _tempDir, "--format", "toon");

        exit.Should().Be(0);
        output.Should().Contain("[name]:");
        output.Should().Contain("ToonFormatTest");
    }

    [Test]
    public async Task CeQuery_YamlFormat_OutputsYaml()
    {
        var (initExit, _, _) = await RunCliAsync("init", "YamlFormatTest", "-d", _tempDir);
        initExit.Should().Be(0);

        var (exit, output, _) = await RunCliAsync("query", "MATCH (w:Workspace) RETURN w.name AS name", "-d", _tempDir, "--format", "yaml");

        exit.Should().Be(0);
        output.Should().Contain("- name: YamlFormatTest");
    }

    [Test]
    public async Task CeTest_WhenNotInWorkspace_ReturnsErrorAndExitCode1()
    {
        var nonWs = Path.Combine(_tempDir, "EmptyNonWs");
        Directory.CreateDirectory(nonWs);

        var (exit, output, error) = await RunCliAsync("test", "-d", nonWs);

        exit.Should().Be(1);
        var combined = output + error;
        combined.Should().Contain("Error: No CodeExplorer workspace found");
    }

    [Test]
    public async Task CeTest_WhenWorkspaceNotIndexed_ReturnsErrorAndExitCode1()
    {
        // Directory with .codeexplorer folder but without graph.db
        Directory.CreateDirectory(Path.Combine(_tempDir, WorkspaceLocator.FolderName));

        var (exit, output, error) = await RunCliAsync("test", "-d", _tempDir);

        exit.Should().Be(1);
        var combined = output + error;
        combined.Should().Contain("database has not been initialized yet");
    }

    [Test]
    public void BaseCommandHandler_HasTarget_MatchesCandidateAliases()
    {
        BaseCommandHandler.HasTarget("context", "context", "bounded-context").Should().BeTrue();
        BaseCommandHandler.HasTarget("BOUNDED-CONTEXT", "context", "bounded-context").Should().BeTrue();
        BaseCommandHandler.HasTarget("  layers  ", "layers", "ontology").Should().BeTrue();
        BaseCommandHandler.HasTarget("other", "context", "bounded-context").Should().BeFalse();
        BaseCommandHandler.HasTarget(null, "context", "bounded-context").Should().BeFalse();
        BaseCommandHandler.HasTarget("   ", "context").Should().BeFalse();

        var viewOpts = new ViewOptions { Target = "context-map" };
        viewOpts.HasTarget("context", "bounded-context", "context-map").Should().BeTrue();
        BaseCommandHandler.HasTarget(viewOpts.Target, "context-map").Should().BeTrue();
    }

    [Test]
    public void BaseCommandHandler_IsOneOf_And_IsFormat()
    {
        BaseCommandHandler.IsOneOf("coverage", "coverage", "test").Should().BeTrue();
        BaseCommandHandler.IsOneOf("Y", "y", "yes").Should().BeTrue();
        BaseCommandHandler.IsOneOf("no", "y", "yes").Should().BeFalse();

        BaseCommandHandler.IsFormat("JSON", "json").Should().BeTrue();
        BaseCommandHandler.IsJsonFormat("json").Should().BeTrue();
        BaseCommandHandler.IsJsonFormat("markdown", jsonFlag: true).Should().BeTrue();
        BaseCommandHandler.IsJsonFormat("markdown", jsonFlag: false).Should().BeFalse();
    }

    [Test]
    public void BaseCommandHandler_ParseList_SplitsAndCleansTokens()
    {
        var result = BaseCommandHandler.ParseList(" file1.cs, file2.ts ; file3.java ; ");
        result.Should().NotBeNull();
        result.Should().Equal(new[] { "file1.cs", "file2.ts", "file3.java" });

        BaseCommandHandler.ParseList(null).Should().BeNull();
        BaseCommandHandler.ParseList("   ").Should().BeNull();
        BaseCommandHandler.ParseList(";;,,").Should().BeNull();
    }

    [Test]
    public void BaseCommandHandler_FileHelpers_WorkCorrectly()
    {
        BaseCommandHandler.ContainsAny(["apple", "banana"], "BANANA", "cherry").Should().BeTrue();
        BaseCommandHandler.ContainsAny(["apple", "banana"], "cherry", "date").Should().BeFalse();

        BaseCommandHandler.HasExtension("foo/bar.cs", "cs", "ts").Should().BeTrue();
        BaseCommandHandler.HasExtension("foo/bar.CS", ".cs").Should().BeTrue();
        BaseCommandHandler.HasExtension("foo/bar.py", "cs", "ts").Should().BeFalse();

        BaseCommandHandler.HasAnyWithExtension(["foo.js", "bar.ts"], "ts").Should().BeTrue();
        BaseCommandHandler.HasAnyWithExtension(["foo.js", "bar.ts"], "py").Should().BeFalse();

        BaseCommandHandler.HasFileName("path/to/package.json", "package.json", "pom.xml").Should().BeTrue();
        BaseCommandHandler.HasFileName("path/to/other.txt", "package.json").Should().BeFalse();
    }

    [Test]
    public async Task CeCli_Gate_Workflow_InitListCheckRatchet()
    {
        // 1. Initialize workspace
        var (initCode, initOut, _) = await RunCliAsync("init", "--dir", _tempDir);
        initCode.Should().Be(0);

        // 2. Gate init - scaffold starter gates
        var (gateInitCode, gateInitOut, _) = await RunCliAsync("gate", "init", "--dir", _tempDir);
        gateInitCode.Should().Be(0);
        gateInitOut.Should().Contain("Initialized");

        // 3. Create dummy file and scan
        var srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(Path.Combine(srcDir, "sample.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(srcDir, "Program.cs"), "class Program { static void Main() {} }");

        var (scanCode, _, _) = await RunCliAsync("scan", _tempDir);
        scanCode.Should().Be(0);

        // 4. Gate list (initially empty, then with custom workspace gate)
        var (emptyListCode, emptyListOut, _) = await RunCliAsync("gate", "list", "--dir", _tempDir);
        emptyListCode.Should().Be(0);
        emptyListOut.Should().Contain("No semantic gates configured");

        var gateDir = Path.Combine(_tempDir, ".codeexplorer", "gates");
        Directory.CreateDirectory(gateDir);
        await File.WriteAllTextAsync(Path.Combine(gateDir, "sig-test-001.yaml"), """
            id: SIG-TEST-001
            category: Architectural
            platform: agnostic
            scope: System
            dimension: Rules
            name: Test Gate
            query: "MATCH (n:Type) WHERE n.name = 'NonExistent' RETURN n.name AS symbol, n.file_path AS file, n.start_line AS line, 'target' AS target, 'msg' AS message"
            """);

        var (listCode, listOut, _) = await RunCliAsync("gate", "list", "--dir", _tempDir);
        listCode.Should().Be(0);
        listOut.Should().Contain("SIG-TEST-001");

        // 5. Gate check (against dummy workspace)
        var (checkCode, checkOut, checkErr) = await RunCliAsync("gate", "check", "--dir", _tempDir);
        checkCode.Should().Be(0, $"Check failed with out:\n{checkOut}\nerr:\n{checkErr}");
        checkOut.Should().Contain("Semantic Invariant Gates Evaluation");

        // 6. Gate ratchet check
        var (ratchetCode, _, _) = await RunCliAsync("gate", "ratchet", "--dir", _tempDir);
        ratchetCode.Should().Be(0);

        // 7. Gate violations check (empty or populated)
        var (violCode, violOut, _) = await RunCliAsync("gate", "violations", "--dir", _tempDir);
        violCode.Should().Be(0);
        violOut.Should().Contain("Architectural Violations");

        // 8. Gate violations with --group-by file
        var (grpCode, grpOut, _) = await RunCliAsync("gate", "violations", "--group-by", "file", "--dir", _tempDir);
        grpCode.Should().Be(0);
        grpOut.Should().Contain("Violations Summary Grouped by 'file'");
    }
}
