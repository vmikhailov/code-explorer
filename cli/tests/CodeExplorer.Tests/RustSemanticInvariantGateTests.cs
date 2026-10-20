using CodeExplorer.Core.Database;
using CodeExplorer.Core.Gates.Engine;
using CodeExplorer.Core.Gates.Models;
using CodeExplorer.Core.Gates.Storage;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class RustSemanticInvariantGateTests
{
    private string _tempWorkspace = null!;

    [SetUp]
    public void SetUp()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_rust_gates_" + Guid.NewGuid().ToString("N"));
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
    public async Task Gate_SIG_R_S_001_DetectsUnsafeCodeOutsideSysBoundary()
    {
        var dbPath = Path.Combine(_tempWorkspace, "graph_unsafe.db");
        await using var client = new SqliteGraphClient(dbPath);
        await client.CreateIndicesAsync();

        var nodes = new List<Node>
        {
            // Business project and file with unsafe function -> VIOLATION
            new("p:auth-service", "Project", new Dictionary<string, object>
            {
                ["name"] = "auth-service",
                ["path"] = "crates/auth-service"
            }),
            new("f:auth_main", "File", new Dictionary<string, object>
            {
                ["path"] = "crates/auth-service/src/main.rs"
            }),
            new("fn:decrypt_raw", "Function", new Dictionary<string, object>
            {
                ["name"] = "decrypt_raw",
                ["is_unsafe"] = true,
                ["is_test"] = false,
                ["start_line"] = 42,
                ["file_path"] = "crates/auth-service/src/main.rs"
            }),

            // Low-level sys project with unsafe function -> COMPLIANT (whitelisted)
            new("p:crypto-sys", "Project", new Dictionary<string, object>
            {
                ["name"] = "crypto-sys",
                ["path"] = "crates/crypto-sys"
            }),
            new("f:crypto_lib", "File", new Dictionary<string, object>
            {
                ["path"] = "crates/crypto-sys/src/lib.rs"
            }),
            new("fn:ffi_call", "Function", new Dictionary<string, object>
            {
                ["name"] = "ffi_call",
                ["is_unsafe"] = true,
                ["is_test"] = false,
                ["start_line"] = 10,
                ["file_path"] = "crates/crypto-sys/src/lib.rs"
            }),

            // Test function with unsafe -> COMPLIANT (test excluded)
            new("fn:test_unsafe", "Function", new Dictionary<string, object>
            {
                ["name"] = "test_unsafe",
                ["is_unsafe"] = true,
                ["is_test"] = true,
                ["start_line"] = 99,
                ["file_path"] = "crates/auth-service/src/main.rs"
            })
        };

        var rels = new List<Relationship>
        {
            new("p:auth-service", "f:auth_main", "CONTAINS"),
            new("fn:decrypt_raw", "f:auth_main", "DECLARED_IN"),
            new("fn:test_unsafe", "f:auth_main", "DECLARED_IN"),

            new("p:crypto-sys", "f:crypto_lib", "CONTAINS"),
            new("fn:ffi_call", "f:crypto_lib", "DECLARED_IN")
        };

        await client.UploadNodesAsync(nodes);
        await client.UploadRelationshipsAsync(rels);

        var repo = new FileGateRepository(_tempWorkspace);
        var engine = new GateExecutionEngine(client, repo);

        var gate = BuiltInGateCatalog.RustUnsafeCodeIsolationGate;
        gate.Should().NotBeNull();

        // 1. Evaluate without baseline -> fails with exactly 1 violation
        var res1 = await engine.CheckGateAsync(gate);
        res1.Status.Should().Be(GateCheckStatus.FailedNew);
        res1.Violations.Count.Should().Be(1);
        res1.Violations[0].Symbol.Should().Be("decrypt_raw");
        res1.Violations[0].Target.Should().Be("auth-service");
        res1.Violations[0].Message.Should().Contain("decrypt_raw");

        // 2. Ratchet baseline -> passes tolerated
        var baseline = RatchetService.CreateUpdatedBaseline(gate, res1.Violations);
        await repo.SaveBaselineAsync(baseline, gate);

        var res2 = await engine.CheckGateAsync(gate);
        res2.Status.Should().Be(GateCheckStatus.PassedTolerated);
        res2.NewViolations.Should().BeEmpty();
        res2.ToleratedViolations.Count.Should().Be(1);
    }

    [Test]
    public async Task Gate_SIG_R_A_001_DetectsCleanArchitectureBoundaryViolations()
    {
        var dbPath = Path.Combine(_tempWorkspace, "graph_arch.db");
        await using var client = new SqliteGraphClient(dbPath);
        await client.CreateIndicesAsync();

        var nodes = new List<Node>
        {
            new("p:domain-models", "Project", new Dictionary<string, object>
            {
                ["name"] = "domain-models",
                ["path"] = "crates/domain-models"
            }),
            new("p:infra-service", "Project", new Dictionary<string, object>
            {
                ["name"] = "infra-service",
                ["path"] = "crates/infra-service"
            }),
            new("pkg:axum", "Package", new Dictionary<string, object>
            {
                ["name"] = "axum",
                ["version"] = "0.7.0"
            }),
            new("pkg:serde", "Package", new Dictionary<string, object>
            {
                ["name"] = "serde",
                ["version"] = "1.0.0"
            })
        };

        var rels = new List<Relationship>
        {
            // domain-models depends on axum -> VIOLATION
            new("p:domain-models", "pkg:axum", "DEPENDS_ON"),
            // domain-models depends on serde -> COMPLIANT
            new("p:domain-models", "pkg:serde", "DEPENDS_ON"),
            // infra-service depends on axum -> COMPLIANT (not domain/core)
            new("p:infra-service", "pkg:axum", "DEPENDS_ON")
        };

        await client.UploadNodesAsync(nodes);
        await client.UploadRelationshipsAsync(rels);

        var repo = new FileGateRepository(_tempWorkspace);
        var engine = new GateExecutionEngine(client, repo);

        var gate = BuiltInGateCatalog.RustCleanArchitectureCrateBoundaryGate;
        gate.Should().NotBeNull();

        var res1 = await engine.CheckGateAsync(gate);
        res1.Status.Should().Be(GateCheckStatus.FailedNew);
        res1.Violations.Count.Should().Be(1);
        res1.Violations[0].Symbol.Should().Be("domain-models");
        res1.Violations[0].Target.Should().Be("axum");
        res1.Violations[0].Message.Should().Contain("axum");

        // Ratchet baseline -> passes tolerated
        var baseline = RatchetService.CreateUpdatedBaseline(gate, res1.Violations);
        await repo.SaveBaselineAsync(baseline, gate);

        var res2 = await engine.CheckGateAsync(gate);
        res2.Status.Should().Be(GateCheckStatus.PassedTolerated);
        res2.NewViolations.Should().BeEmpty();
        res2.ToleratedViolations.Count.Should().Be(1);
    }

    [Test]
    public async Task Gate_SIG_R_C_001_DetectsAsyncTokioBlockingCalls()
    {
        var dbPath = Path.Combine(_tempWorkspace, "graph_tokio.db");
        await using var client = new SqliteGraphClient(dbPath);
        await client.CreateIndicesAsync();

        var nodes = new List<Node>
        {
            new("fn:async_handler", "Function", new Dictionary<string, object>
            {
                ["name"] = "async_handler",
                ["is_async"] = true,
                ["is_test"] = false,
                ["file_path"] = "src/handler.rs",
                ["start_line"] = 15
            }),
            new("fn:sync_worker", "Function", new Dictionary<string, object>
            {
                ["name"] = "sync_worker",
                ["is_async"] = false,
                ["is_test"] = false,
                ["file_path"] = "src/worker.rs",
                ["start_line"] = 30
            }),
            new("fn:sleep", "Function", new Dictionary<string, object>
            {
                ["name"] = "std::thread::sleep",
                ["full_name"] = "std::thread::sleep"
            }),
            new("fn:tokio_sleep", "Function", new Dictionary<string, object>
            {
                ["name"] = "tokio::time::sleep",
                ["full_name"] = "tokio::time::sleep"
            })
        };

        var rels = new List<Relationship>
        {
            // async calling blocking std::thread::sleep -> VIOLATION
            new("fn:async_handler", "fn:sleep", "CALLS"),
            // async calling non-blocking tokio::time::sleep -> COMPLIANT
            new("fn:async_handler", "fn:tokio_sleep", "CALLS"),
            // sync calling std::thread::sleep -> COMPLIANT (not async)
            new("fn:sync_worker", "fn:sleep", "CALLS")
        };

        await client.UploadNodesAsync(nodes);
        await client.UploadRelationshipsAsync(rels);

        var repo = new FileGateRepository(_tempWorkspace);
        var engine = new GateExecutionEngine(client, repo);

        var gate = BuiltInGateCatalog.RustAsyncTokioAntiBlockingGate;
        gate.Should().NotBeNull();

        var res1 = await engine.CheckGateAsync(gate);
        res1.Status.Should().Be(GateCheckStatus.FailedNew);
        res1.Violations.Count.Should().Be(1);
        res1.Violations[0].Symbol.Should().Be("async_handler");
        res1.Violations[0].Target.Should().Be("std::thread::sleep");
        res1.Violations[0].Message.Should().Contain("std::thread::sleep");

        // Ratchet baseline
        var baseline = RatchetService.CreateUpdatedBaseline(gate, res1.Violations);
        await repo.SaveBaselineAsync(baseline, gate);

        var res2 = await engine.CheckGateAsync(gate);
        res2.Status.Should().Be(GateCheckStatus.PassedTolerated);
        res2.NewViolations.Should().BeEmpty();
        res2.ToleratedViolations.Count.Should().Be(1);
    }

    [Test]
    public async Task Gate_SIG_R_D_001_DetectsUnparameterizedSqlQueries()
    {
        var dbPath = Path.Combine(_tempWorkspace, "graph_sqli.db");
        await using var client = new SqliteGraphClient(dbPath);
        await client.CreateIndicesAsync();

        var nodes = new List<Node>
        {
            new("fn:vuln_query", "Function", new Dictionary<string, object>
            {
                ["name"] = "vuln_query",
                ["file_path"] = "src/db.rs",
                ["start_line"] = 25
            }),
            new("fn:safe_query", "Function", new Dictionary<string, object>
            {
                ["name"] = "safe_query",
                ["file_path"] = "src/db.rs",
                ["start_line"] = 50
            }),
            new("tbl:users", "Table", new Dictionary<string, object>
            {
                ["name"] = "users"
            }),
            new("lit:bad_sql", "CallLiteral", new Dictionary<string, object>
            {
                ["text"] = "format!(\"SELECT * FROM users WHERE id = {}\", user_id)",
                ["value"] = "format!(\"SELECT * FROM users WHERE id = {}\", user_id)",
                ["line"] = 26,
                ["file_path"] = "src/db.rs"
            }),
            new("lit:good_sql", "CallLiteral", new Dictionary<string, object>
            {
                ["text"] = "\"SELECT * FROM users WHERE id = $1\"",
                ["value"] = "\"SELECT * FROM users WHERE id = $1\"",
                ["line"] = 51,
                ["file_path"] = "src/db.rs"
            })
        };

        var rels = new List<Relationship>
        {
            // vuln_query queries users and calls format! dynamic query -> VIOLATION
            new("fn:vuln_query", "tbl:users", "QUERIES_DB"),
            new("fn:vuln_query", "lit:bad_sql", "CALLS"),

            // safe_query queries users with parameterized query -> COMPLIANT
            new("fn:safe_query", "tbl:users", "QUERIES_DB"),
            new("fn:safe_query", "lit:good_sql", "CALLS")
        };

        await client.UploadNodesAsync(nodes);
        await client.UploadRelationshipsAsync(rels);

        var repo = new FileGateRepository(_tempWorkspace);
        var engine = new GateExecutionEngine(client, repo);

        var gate = BuiltInGateCatalog.RustUnparameterizedSqlQueryGate;
        gate.Should().NotBeNull();

        var res1 = await engine.CheckGateAsync(gate);
        res1.Status.Should().Be(GateCheckStatus.FailedNew);
        res1.Violations.Count.Should().Be(1);
        res1.Violations[0].Symbol.Should().Be("vuln_query");
        res1.Violations[0].Target.Should().Be("users");
        res1.Violations[0].Message.Should().Contain("format!");

        // Ratchet baseline
        var baseline = RatchetService.CreateUpdatedBaseline(gate, res1.Violations);
        await repo.SaveBaselineAsync(baseline, gate);

        var res2 = await engine.CheckGateAsync(gate);
        res2.Status.Should().Be(GateCheckStatus.PassedTolerated);
        res2.NewViolations.Should().BeEmpty();
        res2.ToleratedViolations.Count.Should().Be(1);
    }
}
