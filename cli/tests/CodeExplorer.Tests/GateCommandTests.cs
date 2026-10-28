using System.Text.Json;
using CodeExplorer.Commands;
using CodeExplorer.Options;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class GateCommandTests
{
    private string _tempWorkspace = null!;
    private TextWriter _originalOut = null!;

    [SetUp]
    public void SetUp()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_gate_cmd_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempWorkspace);
        _originalOut = Console.Out;
    }

    [TearDown]
    public void TearDown()
    {
        Console.SetOut(_originalOut);
        try
        {
            if (Directory.Exists(_tempWorkspace))
            {
                Directory.Delete(_tempWorkspace, true);
            }
        }
        catch { }
    }

    private async Task SetupWorkspaceWithGatesAsync()
    {
        var ws = WorkspaceLocator.Initialize(_tempWorkspace, "GateTestApp");
        var client = new SqliteGraphClient(ws.DbPath);
        await client.CreateIndicesAsync();

        // Seed nodes into graph
        var node1 = new Node(
            "f1",
            "Function",
            new()
            {
                ["name"] = "CallerMethod",
                ["path"] = "src/Domain/OrderService.cs",
                ["file_path"] = "src/Domain/OrderService.cs",
                ["start_line"] = 42
            }
        );
        var node2 = new Node(
            "f2",
            "Function",
            new()
            {
                ["name"] = "DirectDbQuery",
                ["path"] = "src/Infrastructure/Db.cs",
                ["file_path"] = "src/Infrastructure/Db.cs",
                ["start_line"] = 10
            }
        );
        await client.UploadNodesAsync([node1, node2]);

        var rel = new Relationship(
            "f1",
            "f2",
            "CALLS",
            new() { ["line"] = 45 }
        );
        await client.UploadRelationshipsAsync([rel]);

        // Add 2 gate manifests: 1 will fail, 1 will pass
        var gatesDir = Path.Combine(_tempWorkspace, ".codeexplorer", "gates");
        Directory.CreateDirectory(gatesDir);

        var failGateYaml = """
            id: SIG-FAIL-001
            scope: Module
            dimension: Boundaries
            name: "Disallow Direct Db Calls from Domain"
            severity: Error
            description: "Domain layer must not directly call Infrastructure Db methods."
            remediation: "Inject an abstraction interface and move Db implementation to Infrastructure."
            query: |
              MATCH (caller:Function)-[r:CALLS]->(target:Function)
              WHERE caller.path CONTAINS 'Domain/' AND target.path CONTAINS 'Infrastructure/'
              RETURN caller.file_path AS file, r.line AS line, caller.name AS symbol, target.name AS target, 'Forbidden direct call' AS message
            """;
        await File.WriteAllTextAsync(Path.Combine(gatesDir, "SIG-FAIL-001.yaml"), failGateYaml);

        var passGateYaml = """
            id: SIG-PASS-001
            scope: Module
            dimension: Rules
            name: "Prohibit External Service Leaks"
            severity: Error
            description: "External services must be encapsulated."
            remediation: "Wrap external service in facade."
            query: |
              MATCH (caller:Function)-[r:CALLS]->(target:ExternalService)
              RETURN caller.file_path AS file, r.line AS line, caller.name AS symbol, target.name AS target, 'Leak' AS message
            """;
        await File.WriteAllTextAsync(Path.Combine(gatesDir, "SIG-PASS-001.yaml"), passGateYaml);
    }

    [Test]
    public async Task GateCheck_ReturnsSummaryOnly_WithoutDetails()
    {
        await SetupWorkspaceWithGatesAsync();

        using var sw = new StringWriter();
        Console.SetOut(sw);

        var exitCode = await GateCommandHandler.HandleAsync(new GateOptions
        {
            Action = "check",
            Dir = _tempWorkspace
        });

        var output = sw.ToString();

        // 1. Should fail since SIG-FAIL-001 has a violation
        exitCode.Should().Be(1);

        // 2. Contains summary table
        output.Should().Contain("Semantic Invariant Gates Evaluation");
        output.Should().Contain("SIG-FAIL-001");
        output.Should().Contain("SIG-PASS-001");
        output.Should().Contain("[FAIL]");
        output.Should().Contain("[PASS]");
        output.Should().Contain("Total: 2 gates | Passed: 1 | Warnings: 0 | Failed: 1");

        // 3. Contains tip pointing to details
        output.Should().Contain("Tip: Run 'ce gate details' to view failure details and remediation.");

        // 4. MUST NOT contain individual violation listings or remediation dumps
        output.Should().NotContain("Failed Gates (1 gate(s) failed");
        output.Should().NotContain("Remediation: Inject an abstraction interface");
        output.Should().NotContain("src/Domain/OrderService.cs:45");
    }

    [Test]
    public async Task GateCheck_SingleGate_ReturnsSummaryOnly()
    {
        await SetupWorkspaceWithGatesAsync();

        using var sw = new StringWriter();
        Console.SetOut(sw);

        var exitCode = await GateCommandHandler.HandleAsync(new GateOptions
        {
            Action = "check",
            GateId = "SIG-FAIL-001",
            Dir = _tempWorkspace
        });

        var output = sw.ToString();

        exitCode.Should().Be(1);
        output.Should().Contain("[FAIL]");
        output.Should().Contain("SIG-FAIL-001");
        output.Should().Contain("Violations: 1 new");
        output.Should().Contain("Tip: Run 'ce gate details -g SIG-FAIL-001' to view violation details and remediation.");

        // MUST NOT contain violation row dumps or remediation
        output.Should().NotContain("New Violations (1):");
        output.Should().NotContain("Remediation: Inject an abstraction");
        output.Should().NotContain("src/Domain/OrderService.cs:45");
    }

    [Test]
    public async Task GateDetails_ReturnsCompleteFailureDetailsAndViolations()
    {
        await SetupWorkspaceWithGatesAsync();

        using var sw = new StringWriter();
        Console.SetOut(sw);

        var exitCode = await GateCommandHandler.HandleAsync(new GateOptions
        {
            Action = "details",
            Dir = _tempWorkspace
        });

        var output = sw.ToString();

        exitCode.Should().Be(1);
        output.Should().Contain("--- Semantic Invariant Gate Details ---");
        output.Should().Contain("SIG-FAIL-001");
        output.Should().Contain("Rule: Domain layer must not directly call Infrastructure Db methods.");
        output.Should().Contain("Remediation: Inject an abstraction interface and move Db implementation to Infrastructure.");
        output.Should().Contain("Violations (1 new, 0 tolerated)");
        output.Should().Contain("CallerMethod");
        output.Should().Contain("DirectDbQuery");
        output.Should().Contain("src/Domain/OrderService.cs:45");
    }

    [Test]
    public async Task GateDetails_SingleGate_ReturnsCompleteGateDetails()
    {
        await SetupWorkspaceWithGatesAsync();

        using var sw = new StringWriter();
        Console.SetOut(sw);

        var exitCode = await GateCommandHandler.HandleAsync(new GateOptions
        {
            Action = "details",
            GateId = "SIG-FAIL-001",
            Dir = _tempWorkspace
        });

        var output = sw.ToString();

        exitCode.Should().Be(1);
        output.Should().Contain("SIG-FAIL-001");
        output.Should().Contain("Rule: Domain layer must not directly call Infrastructure Db methods.");
        output.Should().Contain("Remediation: Inject an abstraction interface");
        output.Should().Contain("CallerMethod");
        output.Should().Contain("src/Domain/OrderService.cs:45");
    }

    [Test]
    public async Task GateCheck_And_GateDetails_JsonOutput_Distinction()
    {
        await SetupWorkspaceWithGatesAsync();

        // 1. Check in JSON: Summary only, no violations array
        using var swCheck = new StringWriter();
        Console.SetOut(swCheck);

        var checkExit = await GateCommandHandler.HandleAsync(new GateOptions
        {
            Action = "check",
            Format = "json",
            Dir = _tempWorkspace
        });

        checkExit.Should().Be(1);
        var checkJson = JsonDocument.Parse(swCheck.ToString());
        var checkRoot = checkJson.RootElement;
        checkRoot.GetProperty("totalGates").GetInt32().Should().Be(2);
        checkRoot.GetProperty("failedCount").GetInt32().Should().Be(1);
        checkRoot.GetProperty("gates").EnumerateArray().First().GetProperty("violations").ValueKind.Should().Be(
            JsonValueKind.Number,
            "Gate check JSON summary must provide violation count number, not violation details array");

        // 2. Details in JSON: Complete details with violations array
        using var swDetails = new StringWriter();
        Console.SetOut(swDetails);

        var detailsExit = await GateCommandHandler.HandleAsync(new GateOptions
        {
            Action = "details",
            Format = "json",
            Dir = _tempWorkspace
        });

        detailsExit.Should().Be(1);
        var detailsJson = JsonDocument.Parse(swDetails.ToString());
        var detailsRoot = detailsJson.RootElement;
        var failGate = detailsRoot.GetProperty("gates").EnumerateArray().First(g => g.GetProperty("id").GetString() == "SIG-FAIL-001");
        failGate.GetProperty("remediation").GetString().Should().Contain("Inject an abstraction interface");
        var viols = failGate.GetProperty("violations").EnumerateArray().ToList();
        viols.Count.Should().Be(1);
        viols[0].GetProperty("symbol").GetString().Should().Be("CallerMethod");
        viols[0].GetProperty("location").GetString().Should().Be("src/Domain/OrderService.cs:45");
    }
}
