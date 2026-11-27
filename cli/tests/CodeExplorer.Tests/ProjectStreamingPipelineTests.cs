using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Database;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public sealed class ProjectStreamingPipelineTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ce_proj_stream_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort temp cleanup on Windows file handles
            }
        }
    }

    [Test]
    public async Task StreamingBulkLoad_HandlesForwardEdgesDuplicateNodesAndTelemetryUpdates()
    {
        var dbPath = Path.Combine(_tempDir, "streaming_bulk.db");
        await using var client = new SqliteGraphClient(dbPath);
        await client.CreateIndicesAsync();

        await client.BeginBulkLoadAsync();

        // 1. Upload an edge BEFORE its target node exists (forward reference)
        var forwardEdge = new Relationship("ws:fn:caller", "ws:fn:callee", RelationshipKind.Calls)
        {
            Line = 42,
            Column = 9
        };
        await client.UploadRelationshipsAsync([forwardEdge]);

        // 2. Stream syntactic nodes across separate batches, including an idempotent node update
        var callerNode = new Node("ws:fn:caller", NodeKind.Function, new Dictionary<string, object>
        {
            ["name"] = "Caller",
            ["file_path"] = "src/Caller.cs"
        });
        var scopeNode = new Node("ws:scope:caller:1", NodeKind.Scope, new Dictionary<string, object>
        {
            ["name"] = "if_1",
            ["file_path"] = "src/Caller.cs"
        });
        await client.UploadNodesAsync([callerNode, scopeNode]);

        // Immediate structural edge (both endpoints already streamed, no telemetry)
        await client.UploadRelationshipsAsync([
            new Relationship("ws:fn:caller", "ws:scope:caller:1", RelationshipKind.Declares),
            new Relationship("ws:fn:caller", "ws:scope:caller:1", RelationshipKind.Declares)
        ]);

        // Second batch: target node + updated caller properties
        var updatedCallerNode = new Node("ws:fn:caller", NodeKind.Function, new Dictionary<string, object>
        {
            ["name"] = "CallerUpdated",
            ["file_path"] = "src/Caller.cs"
        });
        var calleeNode = new Node("ws:fn:callee", NodeKind.Function, new Dictionary<string, object>
        {
            ["name"] = "Callee",
            ["file_path"] = "src/Callee.cs"
        });
        await client.UploadNodesAsync([updatedCallerNode, calleeNode]);

        await client.CommitBulkLoadAsync();

        var (nodeCount, relCount) = await client.GetGraphCountsAsync();
        Assert.That(nodeCount, Is.EqualTo(3));
        Assert.That(relCount, Is.EqualTo(2));

        var callerName = await client.QueryScalarAsync<string>(
            "MATCH (f:Function {id: 'ws:fn:caller'}) RETURN f.name");
        Assert.That(callerName, Is.EqualTo("CallerUpdated"));

        var callLine = await client.QueryScalarAsync<long>(
            "MATCH (a:Function {id: 'ws:fn:caller'})-[r:CALLS]->(b:Function {id: 'ws:fn:callee'}) RETURN r.line");
        Assert.That(callLine, Is.EqualTo(42));
    }

    [Test]
    public async Task ProjectStreamingPipeline_PersistsScopesToSqlite_EvictsAsts_AndHonorsInternalsVisibleTo()
    {
        var coreDir = Path.Combine(_tempDir, "CoreLib");
        var friendDir = Path.Combine(_tempDir, "FriendService");
        var strangerDir = Path.Combine(_tempDir, "StrangerService");
        Directory.CreateDirectory(coreDir);
        Directory.CreateDirectory(friendDir);
        Directory.CreateDirectory(strangerDir);

        await File.WriteAllTextAsync(Path.Combine(coreDir, "CoreLib.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <InternalsVisibleTo Include="FriendService" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(Path.Combine(coreDir, "AssemblyAttributes.cs"), """
            using System.Runtime.CompilerServices;
            [assembly: InternalsVisibleTo("ExtraFriend, PublicKey=00240000")]
            """);

        await File.WriteAllTextAsync(Path.Combine(coreDir, "TokenGate.cs"), """
            namespace CoreLib;

            public sealed class TokenGate
            {
                internal static bool VerifyInternalToken(string raw)
                {
                    if (string.IsNullOrEmpty(raw))
                    {
                        return false;
                    }
                    return raw.Length > 3;
                }
            }
            """);

        await File.WriteAllTextAsync(Path.Combine(friendDir, "FriendService.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\CoreLib\CoreLib.csproj" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(Path.Combine(friendDir, "FriendCaller.cs"), """
            using CoreLib;

            namespace FriendService;

            public sealed class FriendCaller
            {
                public bool Execute(string token) => TokenGate.VerifyInternalToken(token);
            }
            """);

        await File.WriteAllTextAsync(Path.Combine(strangerDir, "StrangerService.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\CoreLib\CoreLib.csproj" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(Path.Combine(strangerDir, "StrangerCaller.cs"), """
            namespace StrangerService;

            public sealed class StrangerCaller
            {
                public bool Execute(string token) => VerifyInternalToken(token);
            }
            """);

        // 1. Verify InMemoryWorkspaceScanner retains ASTs for Layer3-only parser tests, and evicts per-project in Layer5_Global scans
        var l3Only = await WorkspaceIndexer.ScanInMemoryAsync(_tempDir);
        Assert.That(l3Only.Layer3!.SyntaxTrees, Is.Not.Empty);

        var inMem = await WorkspaceIndexer.ScanInMemoryAsync(_tempDir, new WorkspaceScanOptions
        {
            MaxLayer = AnalysisLayer.Layer5_Global
        });
        Assert.That(inMem.Layer3, Is.Not.Null);
        Assert.That(inMem.Layer3!.SyntaxTrees, Is.Empty, "SyntaxTrees must be evicted per project in full Layer 5 scans.");

        // 2. Verify full SqliteGraphClient streaming scan persists ScopeNodes & resolves InternalsVisibleTo accurately
        var dbDir = Path.Combine(_tempDir, ".codeexplorer");
        Directory.CreateDirectory(dbDir);
        var dbPath = Path.Combine(dbDir, "graph.db");

        await using var client = new SqliteGraphClient(dbPath);
        await client.CreateIndicesAsync();
        var indexer = new WorkspaceIndexer(client, NullLogger<WorkspaceIndexer>.Instance);
        await indexer.IndexAsync(_tempDir, clear: true);

        var scopeCount = await client.QueryScalarAsync<long>("MATCH (s:Scope) RETURN count(s)");
        Assert.That(scopeCount, Is.GreaterThan(0), "ScopeNodes must be streamed into SQLite before per-project eviction.");

        var friendCallsCount = await client.QueryScalarAsync<long>(
            "MATCH (caller:Function {name: 'Execute'})-[:CALLS]->(callee:Function {name: 'VerifyInternalToken'}) WHERE caller.file_path CONTAINS 'FriendService' RETURN count(*)");
        Assert.That(friendCallsCount, Is.EqualTo(1), "FriendService declared in <InternalsVisibleTo> must resolve internal target.");

        var strangerCallsCount = await client.QueryScalarAsync<long>(
            "MATCH (caller:Function {name: 'Execute'})-[:CALLS]->(callee:Function {name: 'VerifyInternalToken'}) WHERE caller.file_path CONTAINS 'StrangerService' RETURN count(*)");
        Assert.That(strangerCallsCount, Is.EqualTo(0), "StrangerService without InternalsVisibleTo must not bind internal target.");
    }

    [Test]
    public async Task TerraformSemanticEnricher_ReusesSharedWorkspaceCacheAcrossManyModules()
    {
        var sharedModDir = Path.Combine(_tempDir, "modules", "rds");
        Directory.CreateDirectory(sharedModDir);
        await File.WriteAllTextAsync(Path.Combine(sharedModDir, "main.tf"), """
            variable "db_name" {
              type = string
            }
            resource "aws_db_instance" "this" {
              db_name = var.db_name
              engine  = "postgres"
            }
            output "endpoint" {
              value = aws_db_instance.this.endpoint
            }
            """);

        const int unitCount = 40;
        for (var i = 0; i < unitCount; i++)
        {
            var unitDir = Path.Combine(_tempDir, "envs", $"unit-{i:D2}");
            Directory.CreateDirectory(unitDir);
            await File.WriteAllTextAsync(Path.Combine(unitDir, "terragrunt.hcl"), $$"""
                terraform {
                  source = "../../modules/rds"
                }
                inputs = {
                  db_name = "app_db_{{i}}"
                }
                """);
        }

        var result = await WorkspaceIndexer.ScanInMemoryAsync(_tempDir, new WorkspaceScanOptions
        {
            MaxLayer = AnalysisLayer.Layer5_Global
        });

        Assert.That(result.Layer2!.Projects.Count, Is.EqualTo(unitCount + 1));
        Assert.That(result.Layer4, Is.Not.Null);
        Assert.That(result.Layer4!.SemanticRelationships, Is.Not.Empty);
    }
}
