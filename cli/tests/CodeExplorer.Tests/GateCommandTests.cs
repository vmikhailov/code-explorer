using CodeExplorer.Commands;
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

        var gateRepo = new Core.Gates.Storage.FileGateRepository(_tempWorkspace);
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

        var evaluator = new Core.Gates.Engine.GateExecutionEngine(client, gateRepo);
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

        var gateRepo = new Core.Gates.Storage.FileGateRepository(_tempWorkspace);
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

        var evaluator = new Core.Gates.Engine.GateExecutionEngine(client, gateRepo);

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

    private async Task<(SqliteGraphClient Client, Core.Gates.Storage.FileGateRepository Repo, Core.Gates.Engine.GateExecutionEngine Engine)> IndexAndActivateCqrsGatesAsync()
    {
        var ws = WorkspaceLocator.Initialize(_tempWorkspace, "CqrsGateApp");
        var client = new SqliteGraphClient(ws.DbPath);
        await client.CreateIndicesAsync();
        await new WorkspaceIndexer(client).IndexAsync(_tempWorkspace, clear: true, enableIntentAnalysis: false);

        var repo = new Core.Gates.Storage.FileGateRepository(_tempWorkspace);
        await repo.InitStarterGatesAsync(overwrite: true);
        var gatesDir = Path.Combine(_tempWorkspace, ".codeexplorer", "gates");
        foreach (var name in new[] { "cqrs-query-no-writes", "cqrs-no-query-to-command-calls", "cqrs-http-get-read-only" })
        {
            File.Move(Path.Combine(gatesDir, name + ".yaml.example"), Path.Combine(gatesDir, name + ".yaml"));
        }
        return (client, repo, new Core.Gates.Engine.GateExecutionEngine(client, repo));
    }

    [Test]
    public async Task CqrsGates_ReadOnlyQueryHandler_ProducesNoViolations()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_tempWorkspace, "App.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"Dapper\" Version=\"2.1.35\" /><PackageReference Include=\"MediatR\" Version=\"12.4.1\" /></ItemGroup></Project>");
        var dir = Path.Combine(_tempWorkspace, "Application", "Orders");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(
            Path.Combine(dir, "GetOrderQueryHandler.cs"),
            """"
            using System.Data;
            using System.Threading;
            using System.Threading.Tasks;
            using Dapper;
            using MediatR;

            namespace SampleApp.Application.Orders;

            public record GetOrderQuery(int OrderId) : IRequest<OrderDto>;
            public class OrderDto { public int Id { get; set; } }

            public class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, OrderDto>
            {
                private readonly IDbConnection _connection;
                public GetOrderQueryHandler(IDbConnection connection) => _connection = connection;

                public async Task<OrderDto> Handle(GetOrderQuery query, CancellationToken ct)
                {
                    const string sql = "SELECT [Id] FROM [Orders] WHERE [Id] = @OrderId";
                    return await _connection.QuerySingleAsync<OrderDto>(sql, new { query.OrderId });
                }
            }
            """");

        var (client, repo, engine) = await IndexAndActivateCqrsGatesAsync();
        using (client)
        {
            (await client.ExecuteCypherAsync("MATCH (t:Type {name: 'GetOrderQueryHandler'}) WHERE t.implements CONTAINS 'QueryHandler' RETURN t.name AS n", null))
                .Rows.Count.Should().BeGreaterThan(0, "the query side must be recognised by its IQueryHandler contract, not by folder name");
            foreach (var id in new[] { "SIG-CQRS-001", "SIG-CQRS-002", "SIG-CQRS-003" })
            {
                var res = await engine.CheckGateAsync((await repo.GetGateByIdAsync(id))!);
                res.Violations.Should().BeEmpty($"{id} must not flag a read-only query handler");
            }
        }
    }

    [Test]
    public async Task CqrsGates_NestJsQueryBusHandler_WithHiddenTypeOrmUpdate_IsDetected()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_tempWorkspace, "package.json"),
            """{ "name": "cqrs-nest", "dependencies": { "@nestjs/common": "^10.0.0", "@nestjs/cqrs": "^10.0.0", "@nestjs/typeorm": "^10.0.0", "typeorm": "^0.3.20" } }""");
        var src = Path.Combine(_tempWorkspace, "src", "invoice");
        Directory.CreateDirectory(Path.Combine(src, "handlers"));

        await File.WriteAllTextAsync(Path.Combine(src, "invoice.entity.ts"),
            """
            import { Entity, PrimaryGeneratedColumn, Column } from 'typeorm';

            @Entity('invoice')
            export class Invoice {
                @PrimaryGeneratedColumn('uuid') id: string;
                @Column() status: string;
            }
            """);
        await File.WriteAllTextAsync(Path.Combine(src, "find-invoice.query.ts"),
            """
            export class FindInvoiceQuery {
                constructor(public readonly id: string) {}
            }
            """);
        await File.WriteAllTextAsync(Path.Combine(src, "invoice.service.ts"),
            """
            import { Injectable } from '@nestjs/common';
            import { InjectRepository } from '@nestjs/typeorm';
            import { Repository } from 'typeorm';
            import { Invoice } from './invoice.entity';

            @Injectable()
            export class InvoiceService {
                constructor(@InjectRepository(Invoice) private readonly invoiceRepository: Repository<Invoice>) {}

                async findOne(id: string): Promise<Invoice> {
                    const invoice = await this.invoiceRepository.findOneOrFail({ where: { id } });
                    return await this.markViewed(invoice);
                }

                private async markViewed(invoice: Invoice): Promise<Invoice> {
                    await this.invoiceRepository.update({ id: invoice.id }, { status: 'VIEWED' });
                    return invoice;
                }
            }
            """);
        await File.WriteAllTextAsync(Path.Combine(src, "handlers", "find-invoice.handler.ts"),
            """
            import { IQueryHandler, QueryHandler } from '@nestjs/cqrs';
            import { FindInvoiceQuery } from '../find-invoice.query';
            import { InvoiceService } from '../invoice.service';

            @QueryHandler(FindInvoiceQuery)
            export class FindInvoiceHandler implements IQueryHandler<FindInvoiceQuery> {
                constructor(private readonly invoiceService: InvoiceService) {}

                async execute(query: FindInvoiceQuery) {
                    return await this.invoiceService.findOne(query.id);
                }
            }
            """);
        await File.WriteAllTextAsync(Path.Combine(src, "invoice.controller.ts"),
            """
            import { Controller, Get, Param } from '@nestjs/common';
            import { QueryBus } from '@nestjs/cqrs';
            import { FindInvoiceQuery } from './find-invoice.query';

            @Controller('/invoice')
            export class InvoiceController {
                constructor(private readonly queryBus: QueryBus) {}

                @Get(':id')
                async findOne(@Param('id') id: string) {
                    return await this.queryBus.execute(new FindInvoiceQuery(id));
                }
            }
            """);

        var (client, repo, engine) = await IndexAndActivateCqrsGatesAsync();
        using (client)
        {
            var res1 = await engine.CheckGateAsync((await repo.GetGateByIdAsync("SIG-CQRS-001"))!);
            res1.Violations.Should().Contain(v => (v.Message ?? "").Contains("FindInvoiceQuery") && (v.Target ?? "").Contains("invoice"),
                "the @QueryHandler must be linked via HANDLES_QUERY -> Message through the service to the TypeORM update on 'invoice'");

            var res3 = await engine.CheckGateAsync((await repo.GetGateByIdAsync("SIG-CQRS-003"))!);
            res3.Violations.Should().Contain(v => (v.Target ?? "").Contains("invoice"),
                "GET -> queryBus.execute(new FindInvoiceQuery) -> @QueryHandler -> update must be traced end to end");
        }
    }

    [Test]
    public async Task CqrsMessageOntology_ExtractsHandlesQueryCommandEvent_AcrossJavaTsGoAndPhp()
    {
        // 1. Java Axon projection with method-level @QueryHandler, @CommandHandler, and @EventSourcingHandler + JPA write
        var javaDir = Path.Combine(_tempWorkspace, "java-svc");
        Directory.CreateDirectory(Path.Combine(javaDir, "src", "main", "java", "com", "app"));
        await File.WriteAllTextAsync(Path.Combine(javaDir, "pom.xml"),
            """
            <project>
              <modelVersion>4.0.0</modelVersion>
              <groupId>com.app</groupId><artifactId>java-svc</artifactId><version>1.0.0</version>
              <dependencies>
                <dependency><groupId>org.axonframework</groupId><artifactId>axon-messaging</artifactId><version>4.9.0</version></dependency>
                <dependency><groupId>org.springframework.boot</groupId><artifactId>spring-boot-starter-data-jpa</artifactId><version>3.2.0</version></dependency>
              </dependencies>
            </project>
            """);
        await File.WriteAllTextAsync(Path.Combine(javaDir, "src", "main", "java", "com", "app", "OrderEntity.java"),
            """
            package com.app;
            import jakarta.persistence.*;
            @Entity @Table(name = "orders")
            public class OrderEntity { @Id public String id; }
            """);
        await File.WriteAllTextAsync(Path.Combine(javaDir, "src", "main", "java", "com", "app", "OrderRepository.java"),
            """
            package com.app;
            import org.springframework.data.jpa.repository.JpaRepository;
            public interface OrderRepository extends JpaRepository<OrderEntity, String> {}
            """);
        await File.WriteAllTextAsync(Path.Combine(javaDir, "src", "main", "java", "com", "app", "OrderProjection.java"),
            """
            package com.app;
            import org.axonframework.queryhandling.QueryHandler;
            import org.axonframework.commandhandling.CommandHandler;
            import org.axonframework.eventsourcing.EventSourcingHandler;

            record FindOrderQuery(String id) {}
            record PlaceOrderCommand(String id) {}
            record OrderPlacedEvent(String id) {}

            public class OrderProjection {
                private final OrderRepository repo;
                public OrderProjection(OrderRepository repo) { this.repo = repo; }

                @QueryHandler
                public OrderEntity handle(FindOrderQuery query) {
                    return repo.save(new OrderEntity());
                }

                @CommandHandler
                public void handleCommand(PlaceOrderCommand cmd) {}

                @EventSourcingHandler
                public void on(OrderPlacedEvent evt) {}
            }
            """);

        // 2. TypeScript interface-only IQueryHandler (without @QueryHandler decorator)
        var tsDir = Path.Combine(_tempWorkspace, "ts-svc");
        Directory.CreateDirectory(Path.Combine(tsDir, "src"));
        await File.WriteAllTextAsync(Path.Combine(tsDir, "package.json"), """{ "name": "ts-svc" }""");
        await File.WriteAllTextAsync(Path.Combine(tsDir, "src", "get-profile.handler.ts"),
            """
            export class GetProfileQuery { constructor(public readonly id: string) {} }
            export interface IQueryHandler<Q> { execute(query: Q): Promise<any>; }
            export class GetProfileHandler implements IQueryHandler<GetProfileQuery> {
                async execute(query: GetProfileQuery) { return { id: query.id }; }
            }
            """);

        // 3. Go CQRS query handler struct in /query/ package
        var goDir = Path.Combine(_tempWorkspace, "go-svc");
        Directory.CreateDirectory(Path.Combine(goDir, "internal", "app", "query"));
        await File.WriteAllTextAsync(Path.Combine(goDir, "go.mod"), "module example.com/gosvc\n\ngo 1.22\n");
        await File.WriteAllTextAsync(Path.Combine(goDir, "internal", "app", "query", "available_hours.go"),
            """
            package query
            import "context"
            type AvailableHoursQuery struct { Date string }
            type AvailableHoursHandler struct {}
            func (h AvailableHoursHandler) Handle(ctx context.Context, q AvailableHoursQuery) error { return nil }
            """);

        // 4. PHP DDD QueryHandler implementing QueryHandler contract
        var phpDir = Path.Combine(_tempWorkspace, "php-svc");
        Directory.CreateDirectory(Path.Combine(phpDir, "src"));
        await File.WriteAllTextAsync(Path.Combine(phpDir, "composer.json"), """{ "name": "demo/php-svc" }""");
        await File.WriteAllTextAsync(Path.Combine(phpDir, "src", "FindStudentQueryHandler.php"),
            """
            <?php
            namespace App;
            interface QueryHandler {}
            final class FindStudentQuery {}
            final class FindStudentQueryHandler implements QueryHandler {
                public function __invoke(FindStudentQuery $query): void {}
            }
            """);

        var (client, repo, engine) = await IndexAndActivateCqrsGatesAsync();
        using (client)
        {
            var qRes = await client.ExecuteCypherAsync(
                "MATCH (fn:Function)-[:HANDLES_QUERY]->(m:Message) RETURN m.name AS msg, m.sub_kind AS kind", null);
            var queryMsgs = qRes.Rows.Select(r => r.Values[0].AsString()).ToList();
            queryMsgs.Should().Contain(new[] { "FindOrderQuery", "GetProfileQuery", "AvailableHoursQuery", "FindStudentQuery" });

            var cRes = await client.ExecuteCypherAsync(
                "MATCH (fn:Function)-[:HANDLES_COMMAND]->(m:Message) RETURN m.name AS msg", null);
            cRes.Rows.Select(r => r.Values[0].AsString()).Should().Contain("PlaceOrderCommand");

            var eRes = await client.ExecuteCypherAsync(
                "MATCH (fn:Function)-[:HANDLES_EVENT]->(m:Message) RETURN m.name AS msg", null);
            eRes.Rows.Select(r => r.Values[0].AsString()).Should().Contain("OrderPlacedEvent");

            // Java Axon @QueryHandler calling JpaRepository.save must be flagged by SIG-CQRS-001
            var sig1 = await engine.CheckGateAsync((await repo.GetGateByIdAsync("SIG-CQRS-001"))!);
            sig1.Violations.Should().Contain(v => (v.Message ?? "").Contains("FindOrderQuery") && (v.Target ?? "").Contains("orders"));
        }
    }
}
