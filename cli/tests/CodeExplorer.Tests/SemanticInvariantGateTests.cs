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
        Assert.That(gate.Dimension, Is.EqualTo(InvariantDimension.Boundaries));
        Assert.That(gate.Name, Is.EqualTo("Test Layer Invariant"));
        Assert.That(gate.Severity, Is.EqualTo(GateSeverity.Error));
        Assert.That(gate.CoordinateCode, Is.EqualTo("[M, B]"));
        Assert.That(gate.Ratchet.Enabled, Is.True);
        Assert.That(gate.Ratchet.Baseline, Is.EqualTo("baselines/SIG-TEST-001.json"));
        Assert.That(gate.Query, Does.Contain("MATCH (a:Type)"));
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

        // BuiltIns are present out of the box
        var allBuiltIns = await repo.GetAllGatesAsync();
        Assert.That(allBuiltIns.Count, Is.GreaterThanOrEqualTo(8));
        Assert.That(allBuiltIns.Any(g => g.Id == "SIG-P-B-001"), Is.True);

        // Init starter gates
        var createdCount = await repo.InitStarterGatesAsync();
        Assert.That(createdCount, Is.GreaterThanOrEqualTo(8));

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

        var gate = (await repo.GetAllGatesAsync()).FirstOrDefault(g => g.Id == "SIG-M-I-001");
        Assert.That(gate, Is.Not.Null);

        // 1. Check without baseline -> fails with exactly 3 new violations
        var res1 = await engine.CheckGateAsync(gate!);
        Assert.That(res1.Status, Is.EqualTo(GateCheckStatus.FailedNew));
        Assert.That(res1.Violations.Count, Is.EqualTo(3));

        var targets = res1.Violations.Select(v => v.Target).ToList();
        Assert.That(targets, Does.Contain("AssignRole(SuperAdmin)"));
        Assert.That(targets, Does.Contain("InvokeTarget(https://api.internal/v1)"));
        Assert.That(targets, Does.Contain("new ParamHolder(secret-key)"));

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
