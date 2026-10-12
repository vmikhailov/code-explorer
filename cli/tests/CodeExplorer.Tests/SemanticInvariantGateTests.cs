using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Gates.Engine;
using CodeExplorer.Core.Gates.Models;
using CodeExplorer.Core.Gates.Storage;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class SemanticInvariantGateTests
{
    private string _tempWorkspace = null!;

    [SetUp]
    public void SetUp()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_gate_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempWorkspace);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_tempWorkspace))
            {
                Directory.Delete(_tempWorkspace, true);
            }
        }
        catch { }
    }

    [Test]
    public void FingerprintGenerator_IsLineShiftImmune()
    {
        var fp1 = GateFingerprintGenerator.Generate("SIG-001", "src/Core/Parser.cs", "MyParser", "TargetType");
        var fp2 = GateFingerprintGenerator.Generate("SIG-001", "src/Core/Parser.cs", "MyParser", "TargetType");

        Assert.That(fp2, Is.EqualTo(fp1));
        Assert.That(fp1, Is.Not.Empty);

        // Path normalization
        var fp3 = GateFingerprintGenerator.Generate("SIG-001", @".\src\Core\Parser.cs", "MyParser", "TargetType");
        Assert.That(fp3, Is.EqualTo(fp1));

        // Distinct symbols produce distinct fingerprints
        var fpOther = GateFingerprintGenerator.Generate("SIG-001", "src/Core/Parser.cs", "OtherParser", "TargetType");
        Assert.That(fpOther, Is.Not.EqualTo(fp1));
    }

    [Test]
    public void GateSerializer_ParsesYamlManifestCorrectly()
    {
        var yaml = """
            id: SIG-TEST-001
            scope: Module
            dimension: Boundaries
            name: Test Layer Invariant
            severity: Error
            description: "Test description"
            remediation: "Fix the boundary"
            query: |
              MATCH (a:Type)-[:CALLS]->(b:Type)
              RETURN a.file AS file, a.line AS line, a.name AS symbol, b.name AS target, 'forbidden call' AS message
            ratchet:
              enabled: true
              baseline: baselines/SIG-TEST-001.json
            """;

        var gate = GateSerializer.DeserializeGate(yaml, "custom.yaml");

        Assert.That(gate.Id, Is.EqualTo("SIG-TEST-001"));
        Assert.That(gate.Scope, Is.EqualTo(InvariantScope.Module));
        Assert.That(gate.Category, Is.EqualTo(GateCategory.Architectural));
        Assert.That(gate.Platform, Is.EqualTo("agnostic"));
        Assert.That(gate.Dimension, Is.EqualTo(InvariantDimension.Boundaries));
        Assert.That(gate.Name, Is.EqualTo("Test Layer Invariant"));
        Assert.That(gate.Severity, Is.EqualTo(GateSeverity.Error));
        Assert.That(gate.CoordinateCode, Is.EqualTo("[M, B]"));
        Assert.That(gate.Ratchet.Enabled, Is.True);
        Assert.That(gate.Ratchet.Baseline, Is.EqualTo("baselines/SIG-TEST-001.json"));
        Assert.That(gate.Query, Does.Contain("MATCH (a:Type)"));
    }

    [Test]
    public void GateSerializer_RoundTripsCategoryAndPlatform()
    {
        var original = new SemanticGate
        {
            Id = "SIG-PLAT-001",
            Category = GateCategory.Platform,
            Platform = "dotnet",
            Scope = InvariantScope.Service,
            Dimension = InvariantDimension.Boundaries,
            Name = "Platform Gate",
            Severity = GateSeverity.Warning,
            Query = "MATCH (n) RETURN n"
        };

        var yaml = GateSerializer.SerializeGate(original);
        Assert.That(yaml, Does.Contain("category: Platform"));
        Assert.That(yaml, Does.Contain("platform: dotnet"));

        var restored = GateSerializer.DeserializeGate(yaml);
        Assert.That(restored.Category, Is.EqualTo(GateCategory.Platform));
        Assert.That(restored.Platform, Is.EqualTo("dotnet"));
    }

    [Test]
    public void GateSerializer_RoundTripsBaselineJson()
    {
        var baseline = new GateBaseline
        {
            GateId = "SIG-TEST-001",
            AllowedViolations = 2,
            Entries =
            [
                new GateBaselineEntry
                {
                    Fingerprint = "hash1",
                    File = "src/A.cs",
                    Symbol = "ClassA",
                    Target = "ClassB",
                    Reason = "Legacy debt"
                },
                new GateBaselineEntry
                {
                    Fingerprint = "hash2",
                    File = "src/C.cs",
                    Symbol = "ClassC",
                    Target = "ClassD"
                }
            ]
        };

        var json = GateSerializer.SerializeBaseline(baseline);
        var restored = GateSerializer.DeserializeBaseline(json);

        Assert.That(restored.GateId, Is.EqualTo(baseline.GateId));
        Assert.That(restored.AllowedViolations, Is.EqualTo(2));
        Assert.That(restored.Entries.Count, Is.EqualTo(2));
        Assert.That(restored.Entries[0].Fingerprint, Is.EqualTo("hash1"));
    }

    [Test]
    public void RatchetService_EvaluatesViolationsAgainstBaseline()
    {
        var gate = new SemanticGate
        {
            Id = "SIG-001",
            Name = "Test",
            Query = "MATCH (n) RETURN n"
        };

        // 1. Zero violations = Passed
        var (status1, new1, tol1, _) = RatchetService.Evaluate(gate, [], null);
        Assert.That(status1, Is.EqualTo(GateCheckStatus.Passed));
        Assert.That(new1, Is.Empty);

        // 2. Violations without baseline = FailedNew
        var v1 = new GateViolation
        {
            GateId = "SIG-001",
            File = "A.cs",
            Line = 10,
            Symbol = "Foo",
            Target = "Bar",
            Fingerprint = "fp1"
        };
        var (status2, new2, _, _) = RatchetService.Evaluate(gate, [v1], null);
        Assert.That(status2, Is.EqualTo(GateCheckStatus.FailedNew));
        Assert.That(new2.Count, Is.EqualTo(1));

        // 3. Violations recognized in baseline = PassedTolerated
        var baseline = new GateBaseline
        {
            GateId = "SIG-001",
            AllowedViolations = 1,
            Entries = [new GateBaselineEntry { Fingerprint = "fp1" }]
        };
        var (status3, new3, tol3, count3) = RatchetService.Evaluate(gate, [v1], baseline);
        Assert.That(status3, Is.EqualTo(GateCheckStatus.PassedTolerated));
        Assert.That(new3, Is.Empty);
        Assert.That(tol3.Count, Is.EqualTo(1));
        Assert.That(count3, Is.EqualTo(1));

        // 4. Additional un-baselined violation = FailedNew
        var v2 = new GateViolation
        {
            GateId = "SIG-001",
            File = "B.cs",
            Line = 20,
            Symbol = "Baz",
            Target = "Qux",
            Fingerprint = "fp2"
        };
        var (status4, new4, tol4, _) = RatchetService.Evaluate(gate, [v1, v2], baseline);
        Assert.That(status4, Is.EqualTo(GateCheckStatus.FailedNew));
        Assert.That(new4.Count, Is.EqualTo(1));
        Assert.That(new4[0].Fingerprint, Is.EqualTo("fp2"));
        Assert.That(tol4.Count, Is.EqualTo(1));
    }

    [Test]
    public void RatchetService_RatchetsDownAllowedDebt()
    {
        var gate = new SemanticGate { Id = "SIG-001", Name = "Test", Query = "MATCH (n) RETURN n" };
        var initialBaseline = new GateBaseline
        {
            GateId = "SIG-001",
            AllowedViolations = 5,
            Entries =
            [
                new GateBaselineEntry { Fingerprint = "fp1" },
                new GateBaselineEntry { Fingerprint = "fp2" },
                new GateBaselineEntry { Fingerprint = "fp3" }
            ]
        };

        // 2 violations fixed, only fp1 remains
        var currentViolations = new List<GateViolation>
        {
            new() { GateId = "SIG-001", Fingerprint = "fp1", File = "A.cs", Symbol = "A", Target = "B" }
        };

        var updated = RatchetService.CreateUpdatedBaseline(gate, currentViolations, initialBaseline);
        Assert.That(updated.AllowedViolations, Is.EqualTo(1));
        Assert.That(updated.Entries.Count, Is.EqualTo(1));
        Assert.That(updated.Entries[0].Fingerprint, Is.EqualTo("fp1"));
    }

    [Test]
    public async Task FileGateRepository_LoadsBuiltInAndWorkspaceGates()
    {
        var repo = new FileGateRepository(_tempWorkspace);

        // No surprise built-ins out of the box (workspace is clean by default)
        var allBuiltIns = await repo.GetAllGatesAsync();
        Assert.That(allBuiltIns, Is.Empty);

        // Init starter gates directory and template
        var createdCount = await repo.InitStarterGatesAsync();
        Assert.That(createdCount, Is.EqualTo(1));
        Assert.That(File.Exists(Path.Combine(_tempWorkspace, ".codeexplorer", "gates", "example-gate.yaml.example")), Is.True);

        // Add custom workspace gate
        var customGatePath = Path.Combine(_tempWorkspace, ".codeexplorer", "gates", "SIG-CUSTOM-001.yaml");
        var customYaml = """
            id: SIG-CUSTOM-001
            scope: System
            dimension: Rules
            name: Custom Gate
            query: "MATCH (n) RETURN n"
            """;
        await File.WriteAllTextAsync(customGatePath, customYaml);

        var refreshed = await repo.GetAllGatesAsync();
        Assert.That(refreshed.Any(g => g.Id == "SIG-CUSTOM-001"), Is.True);

        var fetched = await repo.GetGateByIdAsync("SIG-CUSTOM-001");
        Assert.That(fetched, Is.Not.Null);
        Assert.That(fetched!.Name, Is.EqualTo("Custom Gate"));
    }

    [Test]
    public async Task FileGateRepository_InitStarterGates_CreatesDirectoriesAndTemplate()
    {
        var repo = new FileGateRepository(_tempWorkspace);
        var count = await repo.InitStarterGatesAsync();
        Assert.That(count, Is.EqualTo(1));

        var gatesDir = Path.Combine(_tempWorkspace, ".codeexplorer", "gates");
        var baselinesDir = Path.Combine(_tempWorkspace, ".codeexplorer", "baselines");

        Assert.That(Directory.Exists(gatesDir), Is.True);
        Assert.That(Directory.Exists(baselinesDir), Is.True);
        Assert.That(File.Exists(Path.Combine(gatesDir, "example-gate.yaml.example")), Is.True);
    }

    [Test]
    public async Task GateExecutionEngine_FiltersByCategoryAndPlatform()
    {
        var mockDb = new MockGateDbClient();
        var repo = new FileGateRepository(_tempWorkspace);
        var engine = new GateExecutionEngine(mockDb, repo);

        var gates = new List<SemanticGate>
        {
            new() { Id = "G1", Category = GateCategory.Architectural, Platform = "agnostic", Name = "A1", Query = "MATCH (n) RETURN n" },
            new() { Id = "G2", Category = GateCategory.Architectural, Platform = "agnostic", Name = "A2", Query = "MATCH (n) RETURN n" },
            new() { Id = "G3", Category = GateCategory.Platform, Platform = "dotnet", Name = "P1", Query = "MATCH (n) RETURN n" },
            new() { Id = "G4", Category = GateCategory.Platform, Platform = "typescript", Name = "P2", Query = "MATCH (n) RETURN n" }
        };

        // Filter by Category
        var archSuite = await engine.CheckSuiteAsync(gates, categoryFilter: GateCategory.Architectural);
        Assert.That(archSuite.Results.Select(r => r.Gate.Id), Is.EquivalentTo(new[] { "G1", "G2" }));

        var platSuite = await engine.CheckSuiteAsync(gates, categoryFilter: GateCategory.Platform);
        Assert.That(platSuite.Results.Select(r => r.Gate.Id), Is.EquivalentTo(new[] { "G3", "G4" }));

        // Filter by Platform
        var dotnetSuite = await engine.CheckSuiteAsync(gates, platformFilter: "dotnet");
        Assert.That(dotnetSuite.Results.Select(r => r.Gate.Id), Is.EquivalentTo(new[] { "G3" }));

        var tsSuite = await engine.CheckSuiteAsync(gates, platformFilter: "typescript");
        Assert.That(tsSuite.Results.Select(r => r.Gate.Id), Is.EquivalentTo(new[] { "G4" }));
    }

    [Test]
    public async Task GateExecutionEngine_EvaluatesGateWithZeroMatchContract()
    {
        var mockDb = new MockGateDbClient();
        var repo = new FileGateRepository(_tempWorkspace);
        var engine = new GateExecutionEngine(mockDb, repo);

        var gate = new SemanticGate
        {
            Id = "SIG-MOCK-001",
            Name = "Mock Gate",
            Query = "MATCH (a)-[:CALLS]->(b) RETURN a.file AS file, a.line AS line, a.name AS symbol, b.name AS target, 'error' AS message"
        };

        // 1. Zero rows returned -> Passed
        mockDb.QueryResponse = "[]";
        var res1 = await engine.CheckGateAsync(gate);
        Assert.That(res1.Status, Is.EqualTo(GateCheckStatus.Passed));
        Assert.That(res1.Violations, Is.Empty);

        // 2. Rows returned without baseline -> FailedNew
        mockDb.QueryResponse = """
            [
              {
                "file": "src/Core/Parser.cs",
                "line": 42,
                "symbol": "MyParser",
                "target": "BadTarget",
                "message": "Illegal call"
              }
            ]
            """;
        var res2 = await engine.CheckGateAsync(gate);
        Assert.That(res2.Status, Is.EqualTo(GateCheckStatus.FailedNew));
        Assert.That(res2.Violations.Count, Is.EqualTo(1));
        Assert.That(res2.Violations[0].File, Is.EqualTo("src/Core/Parser.cs"));
        Assert.That(res2.Violations[0].Line, Is.EqualTo(42));
        Assert.That(res2.Violations[0].Symbol, Is.EqualTo("MyParser"));

        // 3. Save baseline -> PassedTolerated
        var baseline = RatchetService.CreateUpdatedBaseline(gate, res2.Violations);
        await repo.SaveBaselineAsync(baseline, gate);

        var res3 = await engine.CheckGateAsync(gate);
        Assert.That(res3.Status, Is.EqualTo(GateCheckStatus.PassedTolerated));
        Assert.That(res3.NewViolations, Is.Empty);
        Assert.That(res3.ToleratedViolations.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Gate_SIG_M_I_001_DetectsHardcodedCallArgumentsAndToleratesAllowedPatterns()
    {
        var srcDir = Path.Combine(_tempWorkspace, "Services").Replace('\\', '/');
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(Path.Combine(srcDir, "Services.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"/>");

        var code = """
            namespace MyApp.Services;

            public enum UserRole { Admin, User }

            public class AuthService
            {
                private const string AllowedRole = "Operator";

                public void Process()
                {
                    // 1. Violations:
                    AssignRole("SuperAdmin");
                    InvokeTarget("https://api.internal/v1");
                    var holder = new ParamHolder("secret-key");

                    // 2. Allowed patterns (must not be flagged):
                    AssignRole(AllowedRole);
                    AssignRole(UserRole.Admin.ToString());
                    AssignRole("");
                    Console.WriteLine("Logging text");
                    LogInformation("Processing user");
                    string.Format("Template {0}", 1);
                }

                public void AssignRole(string role) {}
                public void InvokeTarget(string url) {}
                public void LogInformation(string msg) {}
            }

            public class ParamHolder
            {
                public ParamHolder(string key) {}
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(srcDir, "AuthService.cs"), code);

        var dbPath = Path.Combine(_tempWorkspace, "graph.db");
        await using var client = new SqliteGraphClient(dbPath);

        CodeExplorer.Core.Parser.WorkspaceIndexer.Register(new CodeExplorer.Parser.CSharp.CSharpParser());
        var indexer = new CodeExplorer.Core.Parser.WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempWorkspace, _tempWorkspace, clear: true);

        var repo = new FileGateRepository(_tempWorkspace);
        var engine = new GateExecutionEngine(client, repo);

        var gate = BuiltInGateCatalog.GetExampleGates().First(g => g.Id == "SIG-M-I-001");
        Assert.That(gate, Is.Not.Null);

        // 1. Check without baseline -> fails with exactly 3 new violations
        var res1 = await engine.CheckGateAsync(gate!);
        Assert.That(res1.Status, Is.EqualTo(GateCheckStatus.FailedNew));
        Assert.That(res1.Violations.Count, Is.EqualTo(3));

        var targets = res1.Violations.Select(v => v.Target).ToList();
        Assert.That(targets, Does.Contain("AssignRole"));
        Assert.That(targets, Does.Contain("InvokeTarget"));
        Assert.That(targets, Does.Contain("new ParamHolder"));

        var messages = res1.Violations.Select(v => v.Message).ToList();
        Assert.That(messages.Any(m => m != null && m.Contains("SuperAdmin")), Is.True);

        // 2. Save baseline -> passes as tolerated
        var baseline = RatchetService.CreateUpdatedBaseline(gate!, res1.Violations);
        await repo.SaveBaselineAsync(baseline, gate!);

        var res2 = await engine.CheckGateAsync(gate!);
        Assert.That(res2.Status, Is.EqualTo(GateCheckStatus.PassedTolerated));
        Assert.That(res2.NewViolations, Is.Empty);
        Assert.That(res2.ToleratedViolations.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task Gate_SIG_M_I_002_DetectsMethodsExceeding500Lines()
    {
        var srcDir = Path.Combine(_tempWorkspace, "Services").Replace('\\', '/');
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(Path.Combine(srcDir, "Services.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"/>");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("namespace MyTestApp;");
        sb.AppendLine("public class LongService {");
        sb.AppendLine("    public void MonsterMethod() {");
        for (int i = 0; i < 510; i++)
        {
            sb.AppendLine($"        int x_{i} = {i};");
        }
        sb.AppendLine("    }");
        sb.AppendLine("    public void ShortMethod() {");
        sb.AppendLine("        int a = 1;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        await File.WriteAllTextAsync(Path.Combine(srcDir, "LongService.cs"), sb.ToString());

        var dbPath = Path.Combine(_tempWorkspace, "graph.db");
        await using var client = new SqliteGraphClient(dbPath);

        CodeExplorer.Core.Parser.WorkspaceIndexer.Register(new CodeExplorer.Parser.CSharp.CSharpParser());
        var indexer = new CodeExplorer.Core.Parser.WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempWorkspace, _tempWorkspace, clear: true);

        var repo = new FileGateRepository(_tempWorkspace);
        var engine = new GateExecutionEngine(client, repo);

        var gate = BuiltInGateCatalog.GetExampleGates().First(g => g.Id == "SIG-M-I-002");
        Assert.That(gate, Is.Not.Null);

        var res1 = await engine.CheckGateAsync(gate!);
        Assert.That(res1.Status, Is.EqualTo(GateCheckStatus.FailedNew));
        Assert.That(res1.Violations.Count, Is.EqualTo(1));
        Assert.That(res1.Violations[0].Symbol, Is.EqualTo("MonsterMethod"));
        Assert.That(res1.Violations[0].Target, Does.Contain("lines"));
        Assert.That(res1.Violations[0].Message, Does.Contain("MonsterMethod"));

        // Save baseline -> passes as tolerated
        var baseline = RatchetService.CreateUpdatedBaseline(gate!, res1.Violations);
        await repo.SaveBaselineAsync(baseline, gate!);

        var res2 = await engine.CheckGateAsync(gate!);
        Assert.That(res2.Status, Is.EqualTo(GateCheckStatus.PassedTolerated));
        Assert.That(res2.NewViolations, Is.Empty);
        Assert.That(res2.ToleratedViolations.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Gate_SIG_M_R_001_DetectsMethodsExceeding500Lines()
    {
        var srcDir = Path.Combine(_tempWorkspace, "ServicesR").Replace('\\', '/');
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(Path.Combine(srcDir, "ServicesR.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"/>");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("namespace MyTestApp;");
        sb.AppendLine("public class LongServiceR {");
        sb.AppendLine("    public void MonsterMethod() {");
        for (int i = 0; i < 510; i++)
        {
            sb.AppendLine($"        int x_{i} = {i};");
        }
        sb.AppendLine("    }");
        sb.AppendLine("    public void ShortMethod() {");
        sb.AppendLine("        int a = 1;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        await File.WriteAllTextAsync(Path.Combine(srcDir, "LongServiceR.cs"), sb.ToString());

        var dbPath = Path.Combine(_tempWorkspace, "graph_r.db");
        await using var client = new SqliteGraphClient(dbPath);

        CodeExplorer.Core.Parser.WorkspaceIndexer.Register(new CodeExplorer.Parser.CSharp.CSharpParser());
        var indexer = new CodeExplorer.Core.Parser.WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempWorkspace, _tempWorkspace, clear: true);

        var repo = new FileGateRepository(_tempWorkspace);
        var engine = new GateExecutionEngine(client, repo);

        var gate = BuiltInGateCatalog.GetExampleGates().First(g => g.Id == "SIG-M-R-001");
        Assert.That(gate, Is.Not.Null);

        var res1 = await engine.CheckGateAsync(gate!);
        Assert.That(res1.Status, Is.EqualTo(GateCheckStatus.FailedNew));
        Assert.That(res1.Violations.Count, Is.EqualTo(1));
        Assert.That(res1.Violations[0].Symbol, Is.EqualTo("MonsterMethod"));
        Assert.That(res1.Violations[0].Target, Does.Contain("lines"));
        Assert.That(res1.Violations[0].Message, Does.Contain("MonsterMethod"));

        // Save baseline -> passes as tolerated
        var baseline = RatchetService.CreateUpdatedBaseline(gate!, res1.Violations);
        await repo.SaveBaselineAsync(baseline, gate!);

        var res2 = await engine.CheckGateAsync(gate!);
        Assert.That(res2.Status, Is.EqualTo(GateCheckStatus.PassedTolerated));
        Assert.That(res2.NewViolations, Is.Empty);
        Assert.That(res2.ToleratedViolations.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Test_Gate_SigMI001_OnlyFlagsBusinessLiterals_ExcludesClassifiedRoles()
    {
        var srcDir = Path.Combine(_tempWorkspace, "OrderApi");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(Path.Combine(srcDir, "OrderApi.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        var code = """
        using System;
        using Microsoft.AspNetCore.Mvc;
        using Microsoft.Extensions.Logging;
        using Microsoft.Extensions.Configuration;

        [ApiController]
        [Route("api/orders")]
        public class OrderController : ControllerBase
        {
            private readonly ILogger _logger;
            private readonly IConfiguration _config;
            private readonly OrderService _orderService;

            public OrderController(ILogger logger, IConfiguration config, OrderService orderService)
            {
                _logger = logger;
                _config = config;
                _orderService = orderService;
            }

            [HttpGet("{id}")]
            public IActionResult GetOrder([FromQuery(Name = "filter")] string filter)
            {
                _logger.LogInformation("Getting order with filter: {Filter}", filter);
                var conn = _config.GetConnectionString("DefaultConnection");
                _orderService.Process("HARDCODED_PAYMENT_STATUS");
                return Ok();
            }
        }

        public class OrderService
        {
            public void Process(string status) {}
        }
        """;

        await File.WriteAllTextAsync(Path.Combine(srcDir, "OrderController.cs"), code);

        var dbPath = Path.Combine(_tempWorkspace, "graph.db");
        await using var client = new SqliteGraphClient(dbPath);

        CodeExplorer.Core.Parser.WorkspaceIndexer.Register(new CodeExplorer.Parser.CSharp.CSharpParser());
        var indexer = new CodeExplorer.Core.Parser.WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempWorkspace, _tempWorkspace, clear: true);

        var repo = new FileGateRepository(_tempWorkspace);
        var engine = new GateExecutionEngine(client, repo);

        var gate = BuiltInGateCatalog.GetExampleGates().First(g => g.Id == "SIG-M-I-001");
        Assert.That(gate, Is.Not.Null);

        var res = await engine.CheckGateAsync(gate!);
        Assert.That(res.Status, Is.EqualTo(GateCheckStatus.FailedNew));
        Assert.That(res.Violations.Count, Is.EqualTo(1));
        Assert.That(res.Violations[0].Symbol, Is.EqualTo("GetOrder"));
        Assert.That(res.Violations[0].Message, Does.Contain("HARDCODED_PAYMENT_STATUS"));
    }

    [Test]
    public async Task Test_Gate_SigMI005_FlagsEmptyCatchWithoutComment_AllowsCommentedOrHandled()
    {
        var srcDir = Path.Combine(_tempWorkspace, "Services");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(Path.Combine(srcDir, "Services.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        var csharpFile = Path.Combine(srcDir, "WorkerService.cs");
        await File.WriteAllTextAsync(csharpFile, """
            using System;

            public class WorkerService
            {
                public void RunBad()
                {
                    try
                    {
                        Console.WriteLine("Bad");
                    }
                    catch (Exception ex)
                    {
                    }
                }

                public void RunGoodCommented()
                {
                    try
                    {
                        Console.WriteLine("Good");
                    }
                    catch (Exception)
                    {
                        // ignored: expected transient failure
                    }
                }

                public void RunGoodHandled()
                {
                    try
                    {
                        Console.WriteLine("Good");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
            """);

        var dbPath = Path.Combine(_tempWorkspace, "graph.db");
        await using var client = new SqliteGraphClient(dbPath);

        CodeExplorer.Core.Parser.WorkspaceIndexer.Register(new CodeExplorer.Parser.CSharp.CSharpParser());
        var indexer = new CodeExplorer.Core.Parser.WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempWorkspace, _tempWorkspace, clear: true);

        var repo = new FileGateRepository(_tempWorkspace);
        var engine = new GateExecutionEngine(client, repo);

        var gate = BuiltInGateCatalog.GetExampleGates().First(g => g.Id == "SIG-M-I-005");

        var res = await engine.CheckGateAsync(gate);
        Assert.That(res.Status, Is.EqualTo(GateCheckStatus.FailedNew));
        Assert.That(res.Violations.Count, Is.EqualTo(1));
        Assert.That(res.Violations[0].Symbol, Is.EqualTo("Exception"));
        Assert.That(res.Violations[0].Message, Does.Contain("Empty catch block without an explanatory comment"));
    }

    [Test]
    public async Task Test_Gate_SigAC001_FlagsDeadInternalEgress()
    {
        var dbPath = Path.Combine(_tempWorkspace, "graph.db");
        await using var client = new SqliteGraphClient(dbPath);
        await client.CreateIndicesAsync();

        var fnNode = new Node("fn:1", OntologyConstants.NodeLabels.Function, new()
        {
            ["name"] = "FetchOrders",
            ["path"] = "src/orderService.ts",
            ["file_path"] = "src/orderService.ts",
            ["start_line"] = 10
        });

        var projOrders = new Node("ws:p:order-service", OntologyConstants.NodeLabels.Project, new()
        {
            ["name"] = "order-service"
        });

        var projUsers = new Node("ws:p:user-service", OntologyConstants.NodeLabels.Project, new()
        {
            ["name"] = "user-service"
        });

        var esDead = new Node("es:dead", OntologyConstants.NodeLabels.ExternalService, new()
        {
            ["name"] = "order-service",
            ["path"] = "/api/v1/non-existent-orders",
            ["is_internal"] = true
        });

        var esValid = new Node("es:valid", OntologyConstants.NodeLabels.ExternalService, new()
        {
            ["name"] = "user-service",
            ["path"] = "/api/v1/users",
            ["is_internal"] = true
        });

        // External service targeting an external/unindexed service not in workspace Projects
        var esOutside = new Node("es:outside", OntologyConstants.NodeLabels.ExternalService, new()
        {
            ["name"] = "billing-service",
            ["path"] = "/api/v1/payments",
            ["is_internal"] = true
        });

        var epNode = new Node("ep:users", OntologyConstants.NodeLabels.Endpoint, new()
        {
            ["name"] = "GetUsers",
            ["route_template"] = "/api/v1/users"
        });

        await client.UploadNodesAsync([projOrders, projUsers, fnNode, esDead, esValid, esOutside, epNode]);

        var rel1 = new Relationship("es:dead", "fn:1", OntologyConstants.Relationships.CalledBy);
        var rel2 = new Relationship("es:valid", "fn:1", OntologyConstants.Relationships.CalledBy);
        var rel3 = new Relationship("es:outside", "fn:1", OntologyConstants.Relationships.CalledBy);
        var relCallsEp = new Relationship("es:valid", "ep:users", OntologyConstants.Relationships.CallsEndpoint);
        await client.UploadRelationshipsAsync([rel1, rel2, rel3, relCallsEp]);

        var repo = new FileGateRepository(_tempWorkspace);
        var engine = new GateExecutionEngine(client, repo);

        var gate = BuiltInGateCatalog.GetExampleGates().First(g => g.Id == "SIG-A-C-001");
        Assert.That(gate, Is.Not.Null);

        var res = await engine.CheckGateAsync(gate);
        Assert.That(res.Status, Is.EqualTo(GateCheckStatus.FailedNew));
        Assert.That(res.Violations.Count, Is.EqualTo(1));
        Assert.That(res.Violations[0].Symbol, Is.EqualTo("FetchOrders"));
        Assert.That(res.Violations[0].Target, Is.EqualTo("/api/v1/non-existent-orders"));
        Assert.That(res.Violations[0].Message, Does.Contain("Internal HTTP call to non-existent endpoint /api/v1/non-existent-orders"));
    }

    [Test]
    public async Task GateCheck_EndpointIdentityUniqueness_DetectsDuplicates()
    {
        var dbPath = Path.Combine(_tempWorkspace, "graph_ep_dup.db");
        await using var client = new SqliteGraphClient(dbPath);
        await client.CreateIndicesAsync();

        var proj = new Node("ws:p:order-service", OntologyConstants.NodeLabels.Project, new()
        {
            ["name"] = "order-service",
            ["path"] = "src/services/order-service"
        });

        var ep1 = new Node("ws:ep:order-service:GET:/ping", OntologyConstants.NodeLabels.Endpoint, new()
        {
            ["name"] = "GET:/ping",
            ["http_method"] = "GET",
            ["route_template"] = "/ping",
            ["path"] = "src/services/order-service/src/index.ts"
        });

        var ep2 = new Node("ws:ep:order-service-legacy:GET:/ping", OntologyConstants.NodeLabels.Endpoint, new()
        {
            ["name"] = "GET:/ping",
            ["http_method"] = "GET",
            ["route_template"] = "/ping",
            ["path"] = "src/services/order-service/src/index.ts"
        });

        await client.UploadNodesAsync([proj, ep1, ep2]);
        await client.UploadRelationshipsAsync([
            new Relationship("ws:p:order-service", "ws:ep:order-service:GET:/ping", OntologyConstants.Relationships.Contains),
            new Relationship("ws:p:order-service", "ws:ep:order-service-legacy:GET:/ping", OntologyConstants.Relationships.Contains)
        ]);

        var repo = new FileGateRepository(_tempWorkspace);
        var engine = new GateExecutionEngine(client, repo);

        var gate = BuiltInGateCatalog.EndpointIdentityUniquenessGate;
        var res = await engine.CheckGateAsync(gate);

        Assert.That(res.Status, Is.EqualTo(GateCheckStatus.FailedNew));
        Assert.That(res.Violations.Count, Is.EqualTo(1));
        Assert.That(res.Violations[0].Message, Does.Contain("Duplicate endpoint route declared in project order-service"));
    }

    [Test]
    public async Task GateCheck_SqlTableSanitization_DetectsInvalidKeywordTable()
    {
        var dbPath = Path.Combine(_tempWorkspace, "graph_tbl_san.db");
        await using var client = new SqliteGraphClient(dbPath);
        await client.CreateIndicesAsync();

        var validTable = new Node("ws:db:orders", OntologyConstants.NodeLabels.Table, new()
        {
            ["name"] = "orders"
        });

        var invalidTable = new Node("ws:db:skip", OntologyConstants.NodeLabels.Table, new()
        {
            ["name"] = "SKIP"
        });

        await client.UploadNodesAsync([validTable, invalidTable]);

        var repo = new FileGateRepository(_tempWorkspace);
        var engine = new GateExecutionEngine(client, repo);

        var gate = BuiltInGateCatalog.SqlTableSanitizationGate;
        var res = await engine.CheckGateAsync(gate);

        Assert.That(res.Status, Is.EqualTo(GateCheckStatus.FailedNew));
        Assert.That(res.Violations.Count, Is.EqualTo(1));
        Assert.That(res.Violations[0].Symbol, Is.EqualTo("SKIP"));
        Assert.That(res.Violations[0].Message, Does.Contain("Invalid or keyword SQL Table entity detected in graph: SKIP"));
    }

    [Test]
    public async Task GateCheck_UnmountedExpressRoute_DetectsBareParamRoute()
    {
        var dbPath = Path.Combine(_tempWorkspace, "graph_unmounted_ep.db");
        await using var client = new SqliteGraphClient(dbPath);
        await client.CreateIndicesAsync();

        var validEp = new Node("ws:ep:orders:GET:/api/orders/:id", OntologyConstants.NodeLabels.Endpoint, new()
        {
            ["name"] = "GET:/api/orders/:id",
            ["http_method"] = "GET",
            ["route_template"] = "/api/orders/:id",
            ["path"] = "src/services/orders/routes.ts"
        });

        var invalidEp = new Node("ws:ep:orders:DELETE:/:id", OntologyConstants.NodeLabels.Endpoint, new()
        {
            ["name"] = "DELETE:/:id",
            ["http_method"] = "DELETE",
            ["route_template"] = "/:id",
            ["path"] = "src/services/orders/routes.ts"
        });

        await client.UploadNodesAsync([validEp, invalidEp]);

        var repo = new FileGateRepository(_tempWorkspace);
        var engine = new GateExecutionEngine(client, repo);

        var gate = BuiltInGateCatalog.UnmountedExpressRouteGate;
        var res = await engine.CheckGateAsync(gate);

        Assert.That(res.Status, Is.EqualTo(GateCheckStatus.FailedNew));
        Assert.That(res.Violations.Count, Is.EqualTo(1));
        Assert.That(res.Violations[0].Symbol, Is.EqualTo("DELETE:/:id"));
        Assert.That(res.Violations[0].Message, Does.Contain("Unmounted parameter root route detected: DELETE /:id"));
    }

    private class MockGateDbClient : IGraphClient
    {
        public string QueryResponse { get; set; } = "[]";

        public Task<string> ExecuteQueryAsync(string query, object? parameters = null, CancellationToken cancellationToken = default)
            => Task.FromResult(QueryResponse);

        public Task CreateIndicesAsync() => Task.CompletedTask;
        public Task ClearDatabaseAsync() => Task.CompletedTask;
        public Task<bool> ClearWorkspaceAsync(string workspacePath) => Task.FromResult(true);
        public Task<string> GetOrCreateWorkspaceIdAsync(string workspacePath) => Task.FromResult("ws1");
        public Task SaveEmptyWorkspaceNodeAsync(string id, string path) => Task.CompletedTask;
        public Task UploadNodesAsync(List<Node> nodes) => Task.CompletedTask;
        public Task UploadRelationshipsAsync(List<Relationship> rels) => Task.CompletedTask;
        public Task ExecuteWriteAsync(string query, object? parameters = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
