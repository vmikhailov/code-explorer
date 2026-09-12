using System.Text.Json;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
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
            cmd.CommandText = "SELECT from_id, to_id, properties FROM edges WHERE kind = 'TRANSITIVELY_CALLS';";
            using var reader = cmd.ExecuteReader();
            var tcRels = new Dictionary<(string, string), int>();
            while (reader.Read())
            {
                var from = reader.GetString(0);
                var to = reader.GetString(1);
                var props = JsonDocument.Parse(reader.GetString(2)).RootElement;
                tcRels[(from, to)] = props.GetProperty("hops").GetInt32();
            }

            Assert.That(tcRels, Has.Count.EqualTo(8));
            Assert.That(tcRels[("1:fn:order_controller", "1:sink:stripe")], Is.EqualTo(2));
            Assert.That(tcRels[("1:fn:order_controller", "1:sink:postgres")], Is.EqualTo(3));
            Assert.That(tcRels[("1:fn:order_controller", "1:sink:select_query")], Is.EqualTo(3));
            Assert.That(tcRels[("1:fn:order_service", "1:sink:stripe")], Is.EqualTo(1));
            Assert.That(tcRels[("1:fn:order_service", "1:sink:postgres")], Is.EqualTo(2));
            Assert.That(tcRels[("1:fn:order_service", "1:sink:select_query")], Is.EqualTo(2));
            Assert.That(tcRels[("1:fn:repo", "1:sink:postgres")], Is.EqualTo(1));
            Assert.That(tcRels[("1:fn:repo", "1:sink:select_query")], Is.EqualTo(1));
        }

        // 5. Verify ATTRIBUTED_TO in edges table
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT from_id, to_id, properties FROM edges WHERE kind = 'ATTRIBUTED_TO';";
            using var reader = cmd.ExecuteReader();
            var attrRels = new Dictionary<(string, string), (int Hops, string SinkKind)>();
            while (reader.Read())
            {
                var from = reader.GetString(0);
                var to = reader.GetString(1);
                var props = JsonDocument.Parse(reader.GetString(2)).RootElement;
                attrRels[(from, to)] = (props.GetProperty("hops").GetInt32(), props.GetProperty("sink_kind").GetString()!);
            }

            Assert.That(attrRels, Has.Count.EqualTo(3));
            Assert.That(attrRels[("1:ep:create_order", "1:sink:stripe")].Hops, Is.EqualTo(3));
            Assert.That(attrRels[("1:ep:create_order", "1:sink:stripe")].SinkKind, Is.EqualTo("ExternalService"));

            Assert.That(attrRels[("1:ep:create_order", "1:sink:postgres")].Hops, Is.EqualTo(4));
            Assert.That(attrRels[("1:ep:create_order", "1:sink:postgres")].SinkKind, Is.EqualTo("DB"));

            Assert.That(attrRels[("1:ep:create_order", "1:sink:select_query")].Hops, Is.EqualTo(4));
            Assert.That(attrRels[("1:ep:create_order", "1:sink:select_query")].SinkKind, Is.EqualTo("Query"));
        }

        // 6. Verify Project external_apis annotation
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT properties FROM nodes WHERE id = '1:project:main';";
            var rawJson = (string)cmd.ExecuteScalar()!;
            var doc = JsonDocument.Parse(rawJson);
            Assert.That(doc.RootElement.TryGetProperty("external_apis", out var extApis), Is.True);
            var apiList = extApis.EnumerateArray().Select(x => x.GetString()).ToList();
            Assert.That(apiList, Does.Contain("api.stripe.com"));
        }

        // 7. Verify Idempotency - running again should not duplicate edges
        await analyzer.RunAsync("1");

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT count(*) FROM edges WHERE kind = 'TRANSITIVELY_CALLS';";
            var tcCount = Convert.ToInt32(cmd.ExecuteScalar());
            Assert.That(tcCount, Is.EqualTo(8));

            cmd.CommandText = "SELECT count(*) FROM edges WHERE kind = 'ATTRIBUTED_TO';";
            var attrCount = Convert.ToInt32(cmd.ExecuteScalar());
            Assert.That(attrCount, Is.EqualTo(3));
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

        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(5000), $"PostIndexAnalyzer took {sw.ElapsedMilliseconds}ms, expected under 5000ms");
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

        Assert.That(result.TransitivelyCalls, Has.Count.EqualTo(8));
        Assert.That(result.AttributedTo, Has.Count.EqualTo(3));
        Assert.That(result.ProjectExternalApis["1:project:main"], Does.Contain("api.stripe.com"));

        var attrStripe = result.AttributedTo.First(a => a.EpId == "1:ep:create_order" && a.SinkId == "1:sink:stripe");
        Assert.That(attrStripe.Hops, Is.EqualTo(3));
        Assert.That(attrStripe.SinkKind, Is.EqualTo("ExternalService"));
    }
}

