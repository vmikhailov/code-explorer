using AwesomeAssertions;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class SqliteIndicesTests
{
    private string _tempDbPath = null!;
    private SqliteGraphClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_db_{Guid.NewGuid():N}.db");
        _client = new SqliteGraphClient(_tempDbPath);
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
    }

    [Test]
    public void Test_AllRequiredIndices_AreCreated()
    {
        using var conn = new SqliteConnection($"Data Source={_tempDbPath};Mode=ReadOnly");

        conn.Open();

        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index';";
        var indices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            indices.Add(reader.GetString(0));
        }

        var expectedIndices = new[]
        {
            "idx_nodes_kind", "idx_nodes_kind_path", "idx_nodes_kind_file_path", "idx_nodes_kind_name",
            "idx_nodes_name_kind",
            "idx_nodes_kind_symbol", "idx_nodes_kind_lower_path", "idx_edges_from_kind_to",
            "idx_edges_to_kind_from", "idx_edges_kind_from_to", "idx_edges_unique",
            "idx_file_registry_hash"
        };

        foreach (var expected in expectedIndices)
        {
            indices.Should().Contain(expected, $"Index '{expected}' should exist in database.");
        }
    }

    [TestCase("SELECT * FROM nodes WHERE kind = 'Endpoint' AND json_extract(properties, '$.path') = 'foo'", "idx_nodes_kind_path")]
    [TestCase("SELECT * FROM nodes WHERE kind = 'ExternalService' AND json_extract(properties, '$.file_path') = 'bar'", "idx_nodes_kind_file_path")]
    [TestCase("SELECT * FROM nodes WHERE kind = 'Project' AND json_extract(properties, '$.name') = 'proj'", "idx_nodes_name_kind")]
    [TestCase("SELECT * FROM nodes WHERE json_extract(properties, '$.name') = 'proj'", "idx_nodes_name_kind")]
    [TestCase("SELECT * FROM nodes INDEXED BY idx_nodes_kind_name WHERE kind = 'Project' AND json_extract(properties, '$.name') = 'proj'", "idx_nodes_kind_name")]
    [TestCase("SELECT * FROM nodes WHERE kind = 'Function' AND json_extract(properties, '$.symbol') = 'MySym'", "idx_nodes_kind_symbol")]
    [TestCase("SELECT * FROM nodes WHERE kind = 'Workspace' AND lower(json_extract(properties, '$.path')) = 'baz'", "idx_nodes_kind_lower_path")]
    public void Test_NodeExpressionIndices_AreUtilizedByQueryPlanner(string query, string expectedIndex)
    {
        using var conn = new SqliteConnection($"Data Source={_tempDbPath};Mode=ReadOnly");

        conn.Open();

        using var cmd = conn.CreateCommand();

        cmd.CommandText = "EXPLAIN QUERY PLAN " + query;

        var planOutput = new List<string>();

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            planOutput.Add(reader.GetString(3)); // column 3 is 'detail'
        }

        var fullPlan = string.Join("\n", planOutput);

        fullPlan.Should().Contain(expectedIndex, $"Query '{query}' should utilize index '{expectedIndex}'. Query plan was:\n{fullPlan}");
    }

    [TestCase("SELECT to_id FROM edges WHERE from_id = 'a' AND kind = 'CONTAINS'")]
    [TestCase("SELECT from_id FROM edges WHERE to_id = 'b' AND kind = 'LOCATED_IN'")]
    public void Test_EdgeCoveringIndices_AreUtilizedByQueryPlanner(string query)
    {
        using var conn = new SqliteConnection($"Data Source={_tempDbPath};Mode=ReadOnly");

        conn.Open();

        using var cmd = conn.CreateCommand();

        cmd.CommandText = "EXPLAIN QUERY PLAN " + query;

        var planOutput = new List<string>();

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            planOutput.Add(reader.GetString(3));
        }

        var fullPlan = string.Join("\n", planOutput);

        fullPlan.Should().Contain("COVERING INDEX", $"Query '{query}' should utilize a covering index. Query plan was:\n{fullPlan}");
    }

    [Test]
    public async Task Test_LoadSymbolsByNamesAsync_OptimizedWithTempTable_FindsExpectedSymbols()
    {
        var typeNode = new Node("ws:Type:OrderService", "Type", new Dictionary<string, object> { ["name"] = "OrderService" });
        var funcNode = new Node("ws:Function:ProcessOrder", "Function", new Dictionary<string, object> { ["name"] = "ProcessOrder" });
        var epNode = new Node("ws:Endpoint:POST /orders", "Endpoint", new Dictionary<string, object> { ["name"] = "POST /orders" });
        await _client.UploadNodesAsync([typeNode, funcNode, epNode]);

        // Small batch
        var result = await _client.LoadSymbolsByNamesAsync(["OrderService", "ProcessOrder", "POST /orders", "NonExistent"], "ws");
        result.Should().ContainKey(("Type", "OrderService"));
        result[("Type", "OrderService")].Should().Be("ws:Type:OrderService");
        result.Should().ContainKey(("Function", "ProcessOrder"));
        result.Should().ContainKey(("Endpoint", "POST /orders"));

        // Large batch (5,000 items)
        var largeBatch = new List<string> { "OrderService", "ProcessOrder" };
        for (int i = 0; i < 5000; i++)
        {
            largeBatch.Add($"dummy_symbol_{i}");
        }
        var largeResult = await _client.LoadSymbolsByNamesAsync(largeBatch, "ws");
        largeResult.Should().ContainKey(("Type", "OrderService"));
        largeResult.Should().ContainKey(("Function", "ProcessOrder"));
        largeResult.Count.Should().Be(2);
    }

    [Test]
    public async Task Test_LoadImplementationsForTypesAsync_OptimizedWithTempTable_FindsExpectedImplementations()
    {
        var ifaceNode = new Node("ws:Type:IOrderService", "Type", new Dictionary<string, object> { ["name"] = "IOrderService" });
        var implNode = new Node("ws:Type:OrderService", "Type", new Dictionary<string, object> { ["name"] = "OrderService" });
        var rel = new Relationship(implNode.Id, ifaceNode.Id, "IMPLEMENTS");
        await _client.UploadNodesAsync([ifaceNode, implNode]);
        await _client.UploadRelationshipsAsync([rel]);

        var result = await _client.LoadImplementationsForTypesAsync(["IOrderService", "NonExistent"], "ws");
        result.Should().ContainKey("IOrderService");
        result["IOrderService"].Should().Contain("OrderService");

        // Large batch (5,000 items)
        var largeBatch = new List<string> { "IOrderService" };
        for (int i = 0; i < 5000; i++)
        {
            largeBatch.Add($"dummy_type_{i}");
        }
        var largeResult = await _client.LoadImplementationsForTypesAsync(largeBatch, "ws");
        largeResult.Should().ContainKey("IOrderService");
        largeResult["IOrderService"].Should().Contain("OrderService");
    }

    [Test]
    public async Task Test_LoadLateBindingCandidatesAsync_CorrectlyLoadsEndpointsEntryPointsAndExternalServices()
    {
        var ep = new EndpointNode("ws:ep:GET:/ping", "GET /ping", "ping.controller.ts", "GET", "/ping");
        var entry = new EntryPointNode("ws:entry:cron:cleanup", "cleanup", "cron.ts", "cron");
        var ext = new ExternalServiceNode("ws:es:http:bundle-service", "bundle-service", "http", "bundle-service", "/bundle/info");
        var proj = new ProjectNode("ws:p:bundle-service:", "bundle-service", "bundle-service", "typescript");

        await _client.UploadNodesAsync([Node.FromNode(ep), Node.FromNode(entry), Node.FromNode(ext), Node.FromNode(proj)]);

        var (endpoints, entryPoints, externalServices, projects) = await _client.LoadLateBindingCandidatesAsync("ws", needEndpoints: true, needExternalServices: true);

        endpoints.Should().HaveCount(1);
        endpoints[0].Id.Should().Be("ws:ep:GET:/ping");
        endpoints[0].RouteTemplate.Should().Be("/ping");
        endpoints[0].Path.Should().Be("ping.controller.ts");
        endpoints[0].HttpMethod.Should().Be("GET");

        entryPoints.Should().HaveCount(1);
        entryPoints[0].Id.Should().Be("ws:entry:cron:cleanup");
        entryPoints[0].Name.Should().Be("cleanup");
        entryPoints[0].Path.Should().Be("cron.ts");
        entryPoints[0].EntryType.Should().Be("cron");

        externalServices.Should().HaveCount(1);
        externalServices[0].Id.Should().Be("ws:es:http:bundle-service");
        externalServices[0].Name.Should().Be("bundle-service");
        externalServices[0].DomainOrService.Should().Be("bundle-service");
        externalServices[0].Path.Should().Be("/bundle/info");
        externalServices[0].Protocol.Should().Be("http");

        projects.Should().HaveCount(1);
        projects[0].Id.Should().Be("ws:p:bundle-service:");
        projects[0].Name.Should().Be("bundle-service");
        projects[0].Path.Should().Be("bundle-service");
    }
}


