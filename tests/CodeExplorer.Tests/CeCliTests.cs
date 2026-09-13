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
}
