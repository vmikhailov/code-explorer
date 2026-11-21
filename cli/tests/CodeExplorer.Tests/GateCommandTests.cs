using CodeExplorer.Commands;
using CodeExplorer.Core.Parser;
using CodeExplorer.Options;
using Microsoft.Extensions.Logging.Abstractions;
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
        var checkRoot = CypherValue.TryParseJsonText(swCheck.ToString());
        checkRoot.GetIntProp("totalGates").Should().Be(2);
        checkRoot.GetIntProp("failedCount").Should().Be(1);
        checkRoot["gates"].AsArray()[0]["violations"].Kind.Should().Be(
            CypherValueKind.Int64,
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
        var detailsRoot = CypherValue.TryParseJsonText(swDetails.ToString());
        var failGate = detailsRoot["gates"].AsArray().First(g => g.GetStringProp("id") == "SIG-FAIL-001");
        failGate.GetStringProp("remediation").Should().Contain("Inject an abstraction interface");
        var viols = failGate["violations"].AsArray();
        viols.Count.Should().Be(1);
        viols[0].GetStringProp("symbol").Should().Be("CallerMethod");
        viols[0].GetStringProp("location").Should().Be("src/Domain/OrderService.cs:45");
    }

    [Test]
    public async Task GateCheck_SarifFormat_EmitsValidSarifV210WithRulesAndLocations()
    {
        await SetupWorkspaceWithGatesAsync();

        using var sw = new StringWriter();
        Console.SetOut(sw);

        var exitCode = await GateCommandHandler.HandleAsync(new GateOptions
        {
            Action = "check",
            Format = "sarif",
            Dir = _tempWorkspace
        });

        exitCode.Should().Be(1);
        var sarif = CypherValue.TryParseJsonText(sw.ToString());
        sarif.GetStringProp("version").Should().Be("2.1.0");
        sarif.GetStringProp("$schema").Should().Contain("sarif-2.1.0.json");

        var run = sarif["runs"].AsArray()[0];
        run["tool"]["driver"].GetStringProp("name").Should().Be("CodeExplorer");
        var rules = run["tool"]["driver"]["rules"].AsArray();
        rules.Count.Should().Be(2);

        var results = run["results"].AsArray();
        results.Count.Should().Be(1);
        var finding = results[0];
        finding.GetStringProp("ruleId").Should().Be("SIG-FAIL-001");
        finding.GetStringProp("level").Should().Be("error");
        finding["partialFingerprints"].GetStringProp("codeExplorerViolationHash/v1").Should().NotBeNullOrWhiteSpace();

        var phys = finding["locations"].AsArray()[0]["physicalLocation"];
        phys["artifactLocation"].GetStringProp("uri").Should().Be("src/Domain/OrderService.cs");
        phys["region"].GetIntProp("startLine").Should().Be(45);
        phys["region"].GetIntProp("startColumn").Should().Be(1);
    }

    [Test]
    public async Task GateCheck_GithubFormat_EmitsWorkflowAnnotations()
    {
        await SetupWorkspaceWithGatesAsync();

        using var sw = new StringWriter();
        Console.SetOut(sw);

        var exitCode = await GateCommandHandler.HandleAsync(new GateOptions
        {
            Action = "check",
            Format = "github",
            Dir = _tempWorkspace
        });

        exitCode.Should().Be(1);
        var output = sw.ToString().Trim();
        output.Should().StartWith("::error file=src/Domain/OrderService.cs,line=45,col=1,title=SIG-FAIL-001");
        output.Should().Contain("CallerMethod: Forbidden direct call");
    }

    [Test]
    public async Task TestGateQueryAsync_ZeroRowNodeReturnQuery_ReportsZeroMissingColumns()
    {
        await SetupWorkspaceWithGatesAsync();
        var ws = WorkspaceLocator.Find(_tempWorkspace)!;
        using var client = new SqliteGraphClient(ws.DbPath);
        var handler = new WebSocketServerHandler(
            client,
            new CodeExplorerRepository(client),
            new WorkspaceIndexer(client),
            NullLogger<WebSocketServerHandler>.Instance,
            idleTimeoutSeconds: 0,
            workspaceRoot: _tempWorkspace);

        var nodeQueryResult = await handler.TestGateQueryAsync(
            "MATCH (fn:Function)-[r:CALLS]->(tgt:Function) WHERE fn.name = '__nonexistent__' RETURN fn, r, tgt",
            gateId: "SIG-CUSTOM-001",
            messageTemplate: null,
            targetTemplate: null);

        nodeQueryResult.Success.Should().BeTrue();
        nodeQueryResult.ViolationsCount.Should().Be(0);
        nodeQueryResult.MissingColumns.Should().BeEmpty();

        var incompleteScalarResult = await handler.TestGateQueryAsync(
            "MATCH (fn:Function) WHERE fn.name = '__nonexistent__' RETURN fn.name AS symbol",
            gateId: "SIG-CUSTOM-002",
            messageTemplate: null,
            targetTemplate: null);

        incompleteScalarResult.Success.Should().BeTrue();
        incompleteScalarResult.MissingColumns.Should().Contain("file");
        incompleteScalarResult.MissingColumns.Should().Contain("line");
    }

    [Test]
    public async Task TwelveFactorBasis_ExtractsMutableStaticMembers_FileLogSinks_AndStarterTemplates()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_tempWorkspace, "App.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        await File.WriteAllTextAsync(
            Path.Combine(_tempWorkspace, "OrderProcessor.cs"),
            """
            using System.Collections.Generic;

            namespace SampleApp;

            public class OrderProcessor
            {
                public static Dictionary<string, string> StickyCache = new();
                public static readonly string ImmutableVersion = "1.0";
                public const int MaxBatchSize = 50;

                public void ConfigureLogging(dynamic loggerConfig)
                {
                    loggerConfig.WriteTo.File("logs/order-service.log");
                }
            }
            """);

        var ws = WorkspaceLocator.Initialize(_tempWorkspace, "TwelveFactorApp");
        using var client = new SqliteGraphClient(ws.DbPath);
        await client.CreateIndicesAsync();
        var indexer = new WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempWorkspace, clear: true, enableIntentAnalysis: false);

        var gateRepo = new CodeExplorer.Core.Gates.Storage.FileGateRepository(_tempWorkspace);
        await gateRepo.InitStarterGatesAsync(overwrite: true);

        var gatesDir = Path.Combine(_tempWorkspace, ".codeexplorer", "gates");
        File.Exists(Path.Combine(gatesDir, "12factor-dependencies-no-unpinned-packages.yaml.example")).Should().BeTrue();
        File.Exists(Path.Combine(gatesDir, "12factor-config-no-hardcoded-connections.yaml.example")).Should().BeTrue();
        File.Exists(Path.Combine(gatesDir, "12factor-processes-no-mutable-static-state.yaml.example")).Should().BeTrue();
        File.Exists(Path.Combine(gatesDir, "12factor-logs-no-file-log-sinks.yaml.example")).Should().BeTrue();

        // Activate 12-Factor VI (Mutable Static State) and XI (File Log Sinks) gates
        File.Move(
            Path.Combine(gatesDir, "12factor-processes-no-mutable-static-state.yaml.example"),
            Path.Combine(gatesDir, "12factor-processes-no-mutable-static-state.yaml"));
        File.Move(
            Path.Combine(gatesDir, "12factor-logs-no-file-log-sinks.yaml.example"),
            Path.Combine(gatesDir, "12factor-logs-no-file-log-sinks.yaml"));

        var evaluator = new CodeExplorer.Core.Gates.Engine.GateExecutionEngine(client, gateRepo);
        var processGate = await gateRepo.GetGateByIdAsync("SIG-12F-006");
        processGate.Should().NotBeNull();
        var processResult = await evaluator.CheckGateAsync(processGate!);
        processResult.Violations.Select(v => v.Target).Should().Contain("OrderProcessor.StickyCache");
        processResult.Violations.Select(v => v.Target).Should().NotContain("OrderProcessor.ImmutableVersion");
        processResult.Violations.Select(v => v.Target).Should().NotContain("OrderProcessor.MaxBatchSize");

        var logsGate = await gateRepo.GetGateByIdAsync("SIG-12F-011");
        logsGate.Should().NotBeNull();
        var logsResult = await evaluator.CheckGateAsync(logsGate!);
        logsResult.Violations.Should().ContainSingle(v => (v.Message ?? "").Contains("WriteTo.File") && (v.Message ?? "").Contains("logs/order-service.log"));
    }

    [Test]
    public async Task CqrsBasis_DetectsQueryWrites_QueryToCommandCalls_HttpGetWrites_AndStarterTemplates()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_tempWorkspace, "App.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"Dapper\" Version=\"2.1.35\" /><PackageReference Include=\"Microsoft.EntityFrameworkCore\" Version=\"9.0.0\" /></ItemGroup></Project>");

        var queriesDir = Path.Combine(_tempWorkspace, "Application", "Queries");
        var commandsDir = Path.Combine(_tempWorkspace, "Application", "Commands");
        var apiDir = Path.Combine(_tempWorkspace, "Api");
        Directory.CreateDirectory(queriesDir);
        Directory.CreateDirectory(commandsDir);
        Directory.CreateDirectory(apiDir);

        await File.WriteAllTextAsync(
            Path.Combine(commandsDir, "CreateOrderCommandHandler.cs"),
            """
            using System.Threading;
            using System.Threading.Tasks;

            namespace SampleApp.Application.Commands;

            public record CreateOrderCommand(string CustomerId);

            public class CreateOrderCommandHandler
            {
                private readonly dynamic _dbContext;

                public CreateOrderCommandHandler(dynamic dbContext) => _dbContext = dbContext;

                public async Task Handle(CreateOrderCommand command, CancellationToken ct)
                {
                    _dbContext.Orders.Add(command);
                }
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(queriesDir, "GetOrderQueryHandler.cs"),
            """"
            using System.Data;
            using System.Threading;
            using System.Threading.Tasks;
            using Dapper;
            using SampleApp.Application.Commands;

            namespace SampleApp.Application.Queries;

            public record GetOrderQuery(int OrderId);

            public class OrderDto
            {
                public int Id { get; set; }
            }

            public class GetOrderQueryHandler
            {
                private readonly IDbConnection _connection;
                private readonly dynamic _mediator;

                public GetOrderQueryHandler(IDbConnection connection, dynamic mediator)
                {
                    _connection = connection;
                    _mediator = mediator;
                }

                public async Task<OrderDto> Handle(GetOrderQuery query, CancellationToken ct)
                {
                    const string readSql = $"""
                        SELECT [Id] AS [{nameof(OrderDto.Id)}]
                        FROM [Orders]
                        WHERE [Id] = @OrderId
                        """;
                    var dto = await _connection.QuerySingleAsync<OrderDto>(readSql, new { query.OrderId });

                    // Violation 1 (SIG-CQRS-001): Query handler performs a write
                    const string auditWriteSql = "INSERT INTO [OrderReadAudit] ([OrderId]) VALUES (@OrderId)";
                    await _connection.ExecuteAsync(auditWriteSql, new { query.OrderId });

                    // Violation 2 (SIG-CQRS-002): Query handler dispatches a command
                    await _mediator.Send(new CreateOrderCommand("cust-1"));

                    return dto;
                }
            }
            """");

        await File.WriteAllTextAsync(
            Path.Combine(apiDir, "OrdersController.cs"),
            """
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Mvc;
            using SampleApp.Application.Commands;

            namespace SampleApp.Api;

            [ApiController]
            [Route("api/orders")]
            public class OrdersController : ControllerBase
            {
                private readonly dynamic _mediator;

                public OrdersController(dynamic mediator) => _mediator = mediator;

                // Violation 3 (SIG-CQRS-003): HTTP GET endpoint dispatches a state-mutating Command
                [HttpGet("{id}")]
                public async Task<IActionResult> GetOrderWithSideEffect(int id)
                {
                    await _mediator.Send(new CreateOrderCommand("cust-1"));
                    return Ok();
                }
            }
            """);

        var ws = WorkspaceLocator.Initialize(_tempWorkspace, "CqrsTestApp");
        using var client = new SqliteGraphClient(ws.DbPath);
        await client.CreateIndicesAsync();
        var indexer = new WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempWorkspace, clear: true, enableIntentAnalysis: false);

        var gateRepo = new CodeExplorer.Core.Gates.Storage.FileGateRepository(_tempWorkspace);
        await gateRepo.InitStarterGatesAsync(overwrite: true);

        var gatesDir = Path.Combine(_tempWorkspace, ".codeexplorer", "gates");
        File.Exists(Path.Combine(gatesDir, "cqrs-query-no-writes.yaml.example")).Should().BeTrue();
        File.Exists(Path.Combine(gatesDir, "cqrs-no-query-to-command-calls.yaml.example")).Should().BeTrue();
        File.Exists(Path.Combine(gatesDir, "cqrs-http-get-read-only.yaml.example")).Should().BeTrue();

        File.Move(
            Path.Combine(gatesDir, "cqrs-query-no-writes.yaml.example"),
            Path.Combine(gatesDir, "cqrs-query-no-writes.yaml"));
        File.Move(
            Path.Combine(gatesDir, "cqrs-no-query-to-command-calls.yaml.example"),
            Path.Combine(gatesDir, "cqrs-no-query-to-command-calls.yaml"));
        File.Move(
            Path.Combine(gatesDir, "cqrs-http-get-read-only.yaml.example"),
            Path.Combine(gatesDir, "cqrs-http-get-read-only.yaml"));

        var evaluator = new CodeExplorer.Core.Gates.Engine.GateExecutionEngine(client, gateRepo);

        var gate1 = await gateRepo.GetGateByIdAsync("SIG-CQRS-001");
        gate1.Should().NotBeNull();
        var res1 = await evaluator.CheckGateAsync(gate1!);
        res1.Violations.Select(v => v.Target).Should().Contain("Handle -> OrderReadAudit");

        var gate2 = await gateRepo.GetGateByIdAsync("SIG-CQRS-002");
        gate2.Should().NotBeNull();
        var res2 = await evaluator.CheckGateAsync(gate2!);
        res2.Violations.Select(v => v.Target).Should().Contain("Handle -> Handle");

        var gate3 = await gateRepo.GetGateByIdAsync("SIG-CQRS-003");
        gate3.Should().NotBeNull();
        var res3 = await evaluator.CheckGateAsync(gate3!);
        res3.Violations.Should().Contain(v => (v.Target ?? "").Contains("Orders"));
    }
}
