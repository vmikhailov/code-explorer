using System.Text.Json;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class PostIndexAnalyzerTests
{
    private string _tempDbPath = null!;
    private SqliteGraphClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"postindex_test_{Guid.NewGuid():N}.db");
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
    public async Task PostIndexAnalyzer_ComputesTransitivelyCallsAndAttributedTo_Correctly()
    {
        // 1. Setup Nodes
        var nodes = new List<Node>
        {
            new("1:project:main", "Project", new() { ["name"] = "MainProject" }),
            new("1:ep:create_order", "EntryPoint", new() { ["name"] = "POST /orders" }),
            new("1:fn:order_controller", "Function", new() { ["name"] = "CreateOrder" }),
            new("1:fn:order_service", "Function", new() { ["name"] = "ProcessOrder" }),
            new("1:fn:repo", "Function", new() { ["name"] = "SaveOrder" }),
            new("1:sink:stripe", "ExternalService", new() { ["domain_or_service"] = "api.stripe.com" }),
            new("1:sink:postgres", "DB", new() { ["name"] = "orders_db" }),
            new("1:sink:select_query", "Query", new() { ["query"] = "SELECT * FROM orders" }),
            new("2:fn:other_ws", "Function", new() { ["name"] = "OtherWorkspaceFunc" })
        };
        await _client.UploadNodesAsync(nodes);

        // 2. Setup Edges
        var edges = new List<Relationship>
        {
            new("1:project:main", "1:ep:create_order", "CONTAINS", new()),
            new("1:fn:order_controller", "1:ep:create_order", "IMPLEMENTS", new()),
            new("1:fn:order_controller", "1:fn:order_service", "CALLS", new()),
            new("1:fn:order_service", "1:sink:stripe", "CALLS", new()),
            new("1:fn:order_service", "1:fn:repo", "CALLS", new()),
            new("1:fn:repo", "1:sink:postgres", "CALLS", new()),
            new("1:fn:repo", "1:sink:select_query", "CALLS", new()),
            // Self-loop cycle to test cycle handling
            new("1:fn:order_service", "1:fn:order_service", "CALLS", new())
        };
        await _client.UploadRelationshipsAsync(edges);

        // 3. Run PostIndexAnalyzer
        var analyzer = new PostIndexAnalyzer(_client);
        await analyzer.RunAsync("1");

        // 4. Verify TRANSITIVELY_CALLS in edges table

        using var conn = new SqliteConnection($"Data Source={_tempDbPath};Mode=ReadOnly");

        conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT from_id, to_id, json(properties) FROM edges WHERE kind = 'TRANSITIVELY_CALLS';";

            using (var reader = cmd.ExecuteReader())
            {
                var tcRels = new Dictionary<(string, string), int>();

                while (reader.Read())
                {
                    var from = reader.GetString(0);
                    var to = reader.GetString(1);
                    var props = JsonDocument.Parse(reader.GetString(2)).RootElement;
                    tcRels[(from, to)] = props.GetProperty("hops").GetInt32();
                }

                tcRels.Should().HaveCount(8);
                tcRels[("1:fn:order_controller", "1:sink:stripe")].Should().Be(2);
                tcRels[("1:fn:order_controller", "1:sink:postgres")].Should().Be(3);
                tcRels[("1:fn:order_controller", "1:sink:select_query")].Should().Be(3);
                tcRels[("1:fn:order_service", "1:sink:stripe")].Should().Be(1);
                tcRels[("1:fn:order_service", "1:sink:postgres")].Should().Be(2);
                tcRels[("1:fn:order_service", "1:sink:select_query")].Should().Be(2);
                tcRels[("1:fn:repo", "1:sink:postgres")].Should().Be(1);
                tcRels[("1:fn:repo", "1:sink:select_query")].Should().Be(1);
            }
        }

        // 5. Verify ATTRIBUTED_TO in edges table
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT from_id, to_id, json(properties) FROM edges WHERE kind = 'ATTRIBUTED_TO';";

            using (var reader = cmd.ExecuteReader())
            {
                var attrRels = new Dictionary<(string, string), (int Hops, string SinkKind)>();

                while (reader.Read())
                {
                    var from = reader.GetString(0);
                    var to = reader.GetString(1);
                    var props = JsonDocument.Parse(reader.GetString(2)).RootElement;

                    attrRels[(from, to)] = (props.GetProperty("hops").GetInt32(),
                        props.GetProperty("sink_kind").GetString()!);
                }

                attrRels.Should().HaveCount(3);
                attrRels[("1:ep:create_order", "1:sink:stripe")].Hops.Should().Be(3);

                attrRels[("1:ep:create_order", "1:sink:stripe")].SinkKind.Should().Be("ExternalService");

                attrRels[("1:ep:create_order", "1:sink:postgres")].Hops.Should().Be(4);
                attrRels[("1:ep:create_order", "1:sink:postgres")].SinkKind.Should().Be("DB");

                attrRels[("1:ep:create_order", "1:sink:select_query")].Hops.Should().Be(4);
                attrRels[("1:ep:create_order", "1:sink:select_query")].SinkKind.Should().Be("Query");
            }
        }

        // 6. Verify Project external_apis annotation
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT json(properties) FROM nodes WHERE id = '1:project:main';";
            var rawJson = (string)cmd.ExecuteScalar()!;
            var doc = JsonDocument.Parse(rawJson);
            doc.RootElement.TryGetProperty("external_apis", out var extApis).Should().BeTrue();
            var apiList = extApis.EnumerateArray().Select(x => x.GetString()).ToList();
            apiList.Should().Contain("api.stripe.com");
        }

        // 7. Verify Idempotency - running again should not duplicate edges
        await analyzer.RunAsync("1");

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT count(*) FROM edges WHERE kind = 'TRANSITIVELY_CALLS';";
            var tcCount = Convert.ToInt32(cmd.ExecuteScalar());
            tcCount.Should().Be(8);

            cmd.CommandText = "SELECT count(*) FROM edges WHERE kind = 'ATTRIBUTED_TO';";
            var attrCount = Convert.ToInt32(cmd.ExecuteScalar());
            attrCount.Should().Be(3);
        }
    }

    [Test]
    public async Task Benchmark_RealGraphDb_IfPresent()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CodeExplorer.slnx")))
        {
            dir = dir.Parent;
        }
        var dbPath = Path.Combine(dir!.FullName, ".codeexplorer", "graph.db");
        if (!File.Exists(dbPath)) return;

        using var client = new SqliteGraphClient(dbPath);

        var analyzer = new PostIndexAnalyzer(client);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await analyzer.RunAsync("1");
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(5000, $"PostIndexAnalyzer took {sw.ElapsedMilliseconds}ms, expected under 5000ms");
    }

    [Test]
    public async Task Benchmark_ArchitectureMap_OnRealDb()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CodeExplorer.slnx")))
        {
            dir = dir.Parent;
        }
        var dbPath = Path.Combine(dir!.FullName, ".codeexplorer", "graph.db");
        if (!File.Exists(dbPath)) return;

        // Find workspace
        using var client = new SqliteGraphClient(dbPath);

        var wsRes = await client.ExecuteQueryAsync(
            "MATCH (w:Workspace) RETURN w.id AS id, w.name AS name, w.path AS path");

        var queriesDir = Path.Combine(dir.FullName, "src", "Core", "CodeExplorer.Core", "Resources", "Queries");
        var cypher = File.ReadAllText(Path.Combine(queriesDir, "get_architecture_map_workspace.cypher"));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var res = await client.ExecuteQueryAsync(cypher, new { workspaceId = "2" });
        sw.Stop();
    }

    [Test]
    public void PostIndexAnalyzer_Analyze_PureInMemory_ComputesExpectedGraph()
    {
        var calls = new Dictionary<string, List<string>>
        {
            ["1:fn:order_controller"] = ["1:fn:order_service"],
            ["1:fn:order_service"] = ["1:sink:stripe", "1:fn:repo"],
            ["1:fn:repo"] = ["1:sink:postgres", "1:sink:select_query"]
        };

        var sinks = new Dictionary<string, string>
        {
            ["1:sink:stripe"] = "ExternalService",
            ["1:sink:postgres"] = "DB",
            ["1:sink:select_query"] = "Query"
        };

        var sinkDomains = new Dictionary<string, string?>
        {
            ["1:sink:stripe"] = "api.stripe.com"
        };

        var callers = new List<string> { "1:fn:order_controller", "1:fn:order_service", "1:fn:repo" };
        var implements = new Dictionary<string, List<string>>
        {
            ["1:ep:create_order"] = ["1:fn:order_controller"]
        };
        var entryPoints = new List<string> { "1:ep:create_order" };
        var projectToEps = new Dictionary<string, List<string>>
        {
            ["1:project:main"] = ["1:ep:create_order"]
        };

        var data = new PostIndexGraphData(calls, sinks, sinkDomains, callers, implements, entryPoints, projectToEps);
        var result = PostIndexAnalyzer.Analyze(data, "1:");

        result.TransitivelyCalls.Should().HaveCount(8);
        result.AttributedTo.Should().HaveCount(3);
        result.ProjectExternalApis["1:project:main"].Should().Contain("api.stripe.com");

        var attrStripe = result.AttributedTo.First(a => a.EpId == "1:ep:create_order" && a.SinkId == "1:sink:stripe");
        attrStripe.Hops.Should().Be(3);
        attrStripe.SinkKind.Should().Be("ExternalService");
    }

    [Test]
    public async Task DeduplicateProjectNodesAsync_RemovesLegacyDuplicates_WhenCanonicalExists()
    {
        // 1. Setup duplicate project nodes sharing the same path
        var nodes = new List<Node>
        {
            new("ws:p:app:", "Project", new() { ["name"] = "App", ["path"] = "src/app/App.csproj", ["kind"] = "App" }),
            new("ws:project:app:", "Project", new() { ["name"] = "App", ["path"] = "src/app/App.csproj" }),
            new("ws:folder:app", "Folder", new() { ["name"] = "app" })
        };
        await _client.UploadNodesAsync(nodes);

        var edges = new List<Relationship>
        {
            new("ws:p:app:", "ws:folder:app", "LOCATED_IN", new()),
            new("ws:project:app:", "ws:folder:app", "LOCATED_IN", new())
        };
        await _client.UploadRelationshipsAsync(edges);

        // 2. Run deduplication
        var analyzer = new PostIndexAnalyzer(_client);
        await analyzer.DeduplicateProjectNodesAsync();

        // 3. Verify that only ws:p:app: remains and legacy edges removed

        using var conn = new SqliteConnection($"Data Source={_tempDbPath};Mode=ReadOnly");

        conn.Open();

        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT id FROM nodes WHERE kind = 'Project'";

        using (var reader = cmd.ExecuteReader())
        {
            var remainingIds = new List<string>();
            while (reader.Read()) remainingIds.Add(reader.GetString(0));
            remainingIds.Should().HaveCount(1);
            remainingIds[0].Should().Be("ws:p:app:");
        }

        // Verify dangling edge from legacy project was removed
        cmd.CommandText =
            "SELECT COUNT(*) FROM edges WHERE from_id = 'ws:project:app:' OR to_id = 'ws:project:app:'";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        count.Should().Be(0);
    }

    [Test]
    public async Task DeduplicateProjectNodesAsync_RetainsLegacy_WhenNoCanonicalExists()
    {
        // Setup only legacy project node
        var nodes = new List<Node>
        {
            new("ws:project:legacy:", "Project", new() { ["name"] = "Legacy", ["path"] = "src/legacy/Legacy.csproj" })
        };
        await _client.UploadNodesAsync(nodes);

        var analyzer = new PostIndexAnalyzer(_client);
        await analyzer.DeduplicateProjectNodesAsync();

        using var conn = new SqliteConnection($"Data Source={_tempDbPath};Mode=ReadOnly");

        conn.Open();

        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT id FROM nodes WHERE kind = 'Project'";

        using var reader = cmd.ExecuteReader();

        var remainingIds = new List<string>();
        while (reader.Read()) remainingIds.Add(reader.GetString(0));
        remainingIds.Should().HaveCount(1);
        remainingIds[0].Should().Be("ws:project:legacy:");
    }

    [Test]
    public void DeduplicateProjectNodes_InMemory_RemovesLegacyDuplicates()
    {
        var nodes = new List<Node>
        {
            new("ws:p:api:", "Project", new() { ["name"] = "Api", ["path"] = "src/api/Api.csproj", ["kind"] = "Service" }),
            new("ws:project:api:", "Project", new() { ["name"] = "Api", ["path"] = "src/api/Api.csproj" }),
            new("ws:file:api", "File", new() { ["name"] = "Program.cs" })
        };
        var edges = new List<Relationship>
        {
            new("ws:p:api:", "ws:file:api", "CONTAINS", new()),
            new("ws:project:api:", "ws:file:api", "CONTAINS", new())
        };

        PostIndexAnalyzer.DeduplicateProjectNodes(nodes, edges);

        (nodes.Count(n => n.Kind == "Project")).Should().Be(1);
        (nodes.Any(n => n.Id == "ws:p:api:")).Should().BeTrue();
        (nodes.Any(n => n.Id == "ws:project:api:")).Should().BeFalse();
        (edges.Any(e => e.From == "ws:project:api:")).Should().BeFalse();
    }
}


