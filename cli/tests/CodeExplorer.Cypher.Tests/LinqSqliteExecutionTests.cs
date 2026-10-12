using System.Text.Json;
using CodeExplorer.Cypher.Ast;
using CodeExplorer.Cypher.Compiler;
using CodeExplorer.Cypher.Linq;
using CodeExplorer.Cypher.Tests.Shared;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using AwesomeAssertions;

namespace CodeExplorer.Cypher.Tests;

[TestFixture]
public class LinqSqliteExecutionTests
{
    private SqliteConnection _conn = null!;
    private SqliteTestQueryExecutor _executor = null!;
    private GraphContext _graph = null!;

    private class SqliteTestQueryExecutor : ICypherQueryExecutor
    {
        private readonly SqliteConnection _conn;

        public SqliteTestQueryExecutor(SqliteConnection conn)
        {
            _conn = conn;
        }

        public async Task<string> ExecuteQueryAsync(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken ct = default)
        {
            var compiled = SqliteCompiler.Compile(query, parameters);

            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = compiled.Sql;

            foreach (var (k, v) in compiled.Parameters)
            {
                cmd.Parameters.AddWithValue("@" + k, v ?? DBNull.Value);
            }

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var rows = new List<Dictionary<string, object?>>();

            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var colName = reader.GetName(i);
                    var val = reader.IsDBNull(i) ? null : reader.GetValue(i);

                    // Parse JSON if needed
                    if (val is string strVal && (strVal.StartsWith("{") || strVal.StartsWith("[")))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(strVal);
                            row[colName] = doc.RootElement.Clone();
                            continue;
                        }
                        catch
                        {
                            // Not JSON, keep string
                        }
                    }

                    row[colName] = val;
                }
                rows.Add(row);
            }

            return JsonSerializer.Serialize(rows);
        }

        public Task<string> ExecuteRawAsync(string cypher, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }

    [SetUp]
    public void SetUp()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        SqliteCypherFunctions.Register(_conn);

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE nodes (
                id TEXT PRIMARY KEY,
                kind TEXT NOT NULL,
                properties TEXT NOT NULL
            );

            CREATE TABLE edges (
                from_id TEXT NOT NULL,
                to_id TEXT NOT NULL,
                kind TEXT NOT NULL,
                properties TEXT NOT NULL
            );

            CREATE INDEX idx_edges_from ON edges(from_id);
            CREATE INDEX idx_edges_to ON edges(to_id);
            CREATE INDEX idx_edges_kind ON edges(kind);
            CREATE INDEX idx_nodes_kind ON nodes(kind);
        ";
        cmd.ExecuteNonQuery();

        SeedData();

        _executor = new SqliteTestQueryExecutor(_conn);
        _graph = new GraphContext(_executor);
    }

    [TearDown]
    public void TearDown()
    {
        _conn.Dispose();
    }

    private void InsertNode(string id, string kind, Dictionary<string, object?> props)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO nodes (id, kind, properties) VALUES (@id, @kind, @props)";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@kind", kind);
        cmd.Parameters.AddWithValue("@props", JsonSerializer.Serialize(props));
        cmd.ExecuteNonQuery();
    }

    private void SeedData()
    {
        // Services
        InsertNode("ws:s:bff", "Service", new()
        {
            ["name"] = "bff",
            ["display_name"] = "Backend For Frontend",
            ["framework"] = "nestjs",
            ["language"] = "typescript",
            ["role"] = "Service",
            ["is_library"] = false,
            ["port"] = 3000
        });

        InsertNode("ws:s:billing", "Service", new()
        {
            ["name"] = "billing",
            ["display_name"] = "Billing Service",
            ["framework"] = "aspnetcore",
            ["language"] = "csharp",
            ["role"] = "Service",
            ["is_library"] = false,
            ["port"] = 5001
        });

        InsertNode("ws:s:inventory", "Service", new()
        {
            ["name"] = "inventory",
            ["display_name"] = "Inventory Service",
            ["framework"] = "aspnetcore",
            ["language"] = "csharp",
            ["role"] = "Service",
            ["is_library"] = false,
            ["port"] = 5002
        });

        InsertNode("ws:s:auth", "Service", new()
        {
            ["name"] = "auth",
            ["display_name"] = "Auth Service",
            ["framework"] = "aspnetcore",
            ["language"] = "csharp",
            ["role"] = "Service",
            ["is_library"] = false,
            ["port"] = 5003
        });

        // Apps
        InsertNode("ws:app:web", "App", new()
        {
            ["name"] = "web-portal",
            ["display_name"] = "Web Portal",
            ["framework"] = "react",
            ["app_type"] = "spa",
            ["port"] = 8080
        });

        // Workers
        InsertNode("ws:w:order-worker", "Worker", new()
        {
            ["name"] = "order-worker",
            ["display_name"] = "Order Background Worker",
            ["framework"] = "dotnet",
            ["queue_type"] = "rabbitmq"
        });

        // CliTools
        InsertNode("ws:cli:ce", "CliTool", new()
        {
            ["name"] = "code-explorer",
            ["display_name"] = "CodeExplorer CLI",
            ["command_name"] = "ce",
            ["language"] = "csharp"
        });

        // Databases
        InsertNode("ws:db:main", "Database", new()
        {
            ["name"] = "main_postgres",
            ["display_name"] = "Main Postgres DB",
            ["db_type"] = "relational",
            ["engine"] = "postgresql",
            ["port"] = 5432
        });

        InsertNode("ws:db:cache", "Database", new()
        {
            ["name"] = "redis_cache",
            ["display_name"] = "Redis Cache",
            ["db_type"] = "cache",
            ["engine"] = "redis",
            ["port"] = 6379
        });

        // Topics
        InsertNode("ws:top:orders", "Topic", new()
        {
            ["name"] = "orders-topic",
            ["display_name"] = "Orders Event Stream",
            ["broker_type"] = "kafka",
            ["partition_count"] = 6
        });

        // ExternalServices
        InsertNode("ws:ext:stripe", "ExternalService", new()
        {
            ["name"] = "stripe",
            ["display_name"] = "Stripe Payments",
            ["protocol"] = "https",
            ["base_url"] = "https://api.stripe.com",
            ["category"] = "Payment"
        });

        InsertNode("ws:ext:twilio", "ExternalService", new()
        {
            ["name"] = "twilio",
            ["display_name"] = "Twilio SMS",
            ["protocol"] = "https",
            ["base_url"] = "https://api.twilio.com",
            ["category"] = "Notification"
        });

        // Libraries
        InsertNode("ws:lib:context-menu", "Library", new()
        {
            ["name"] = "context-menu",
            ["display_name"] = "Context Menu UI Library",
            ["package_name"] = "ui-context-menu",
            ["version"] = "1.0.0",
            ["language"] = "typescript",
            ["is_library"] = true
        });

        // Endpoints
        InsertNode("ws:ep:create-order", "Endpoint", new()
        {
            ["name"] = "CreateOrder",
            ["display_name"] = "POST /api/v1/orders",
            ["route"] = "/api/v1/orders",
            ["http_method"] = "POST",
            ["protocol"] = "http",
            ["port"] = 5001
        });

        InsertNode("ws:ep:get-order", "Endpoint", new()
        {
            ["name"] = "GetOrder",
            ["display_name"] = "GET /api/v1/orders/{id}",
            ["route"] = "/api/v1/orders/{id}",
            ["http_method"] = "GET",
            ["protocol"] = "http",
            ["port"] = 5001
        });
    }

    [Test]
    public async Task Services_Where_Framework_Aspnetcore_ReturnsFiltered()
    {
        var services = await _graph.Services
            .Where(s => s.Framework == "aspnetcore")
            .ToListAsync();

        services.Should().HaveCount(3);
        services.Select(s => s.Name).Should().BeEquivalentTo(new[] { "billing", "inventory", "auth" });
    }

    [Test]
    public async Task Services_OrderBy_Name_ReturnsSortedAscending()
    {
        var services = await _graph.Services
            .OrderBy(s => s.Name)
            .ToListAsync();

        services.Should().HaveCount(4);
        var names = services.Select(s => s.Name).ToList();
        names.Should().Equal(new[] { "auth", "bff", "billing", "inventory" });
    }

    [Test]
    public async Task Services_OrderByDescending_Name_ReturnsSortedDescending()
    {
        var services = await _graph.Services
            .OrderByDescending(s => s.Name)
            .ToListAsync();

        services.Should().HaveCount(4);
        var names = services.Select(s => s.Name).ToList();
        names.Should().Equal(new[] { "inventory", "billing", "bff", "auth" });
    }

    [Test]
    public async Task Services_Paging_Skip_Take()
    {
        var services = await _graph.Services
            .OrderBy(s => s.Name)
            .Skip(1)
            .Take(2)
            .ToListAsync();

        services.Should().HaveCount(2);
        var names = services.Select(s => s.Name).ToList();
        names.Should().Equal(new[] { "bff", "billing" });
    }

    [Test]
    public async Task Services_CountAsync_AllServices()
    {
        var count = await _graph.Services.CountAsync();
        count.Should().Be(4);
    }

    [Test]
    public async Task Services_CountAsync_WithFilter()
    {
        var count = await _graph.Services
            .Where(s => s.Language == "csharp")
            .CountAsync();

        count.Should().Be(3);
    }

    [Test]
    public async Task Services_AnyAsync_ReturnsTrueWhenFound()
    {
        var found = await _graph.Services
            .Where(s => s.Name == "bff")
            .AnyAsync();

        found.Should().BeTrue();
    }

    [Test]
    public async Task Services_AnyAsync_ReturnsFalseWhenNotFound()
    {
        var found = await _graph.Services
            .Where(s => s.Name == "nonexistent")
            .AnyAsync();

        found.Should().BeFalse();
    }

    [Test]
    public async Task Databases_FirstOrDefaultAsync_FindsMatching()
    {
        var db = await _graph.Databases
            .Where(d => d.DbType == "relational")
            .FirstOrDefaultAsync();

        db.Should().NotBeNull();
        db!.Name.Should().Be("main_postgres");
        db.Engine.Should().Be("postgresql");
        db.Port.Should().Be(5432);
    }

    [Test]
    public async Task ExternalServices_Where_Category()
    {
        var ext = await _graph.ExternalServices
            .Where(e => e.Category == "Payment")
            .ToListAsync();

        ext.Should().HaveCount(1);
        ext[0].Name.Should().Be("stripe");
        ext[0].BaseUrl.Should().Be("https://api.stripe.com");
    }

    [Test]
    public async Task Apps_Where_AppType()
    {
        var apps = await _graph.Apps
            .Where(a => a.AppType == "spa")
            .ToListAsync();

        apps.Should().HaveCount(1);
        apps[0].Name.Should().Be("web-portal");
        apps[0].Port.Should().Be(8080);
    }

    [Test]
    public async Task Workers_Where_QueueType()
    {
        var workers = await _graph.Workers
            .Where(w => w.QueueType == "rabbitmq")
            .ToListAsync();

        workers.Should().HaveCount(1);
        workers[0].Name.Should().Be("order-worker");
    }

    [Test]
    public async Task Topics_Where_PartitionCount()
    {
        var topics = await _graph.Topics
            .Where(t => t.PartitionCount > 5)
            .ToListAsync();

        topics.Should().HaveCount(1);
        topics[0].Name.Should().Be("orders-topic");
        topics[0].BrokerType.Should().Be("kafka");
    }

    [Test]
    public async Task Libraries_FirstOrDefaultAsync()
    {
        var lib = await _graph.Libraries
            .Where(l => l.PackageName == "ui-context-menu")
            .FirstOrDefaultAsync();

        lib.Should().NotBeNull();
        lib!.Name.Should().Be("context-menu");
        lib.Version.Should().Be("1.0.0");
        lib.IsLibrary.Should().BeTrue();
    }

    [Test]
    public async Task Endpoints_Where_HttpMethod()
    {
        var endpoints = await _graph.Endpoints
            .Where(ep => ep.HttpMethod == "POST")
            .ToListAsync();

        endpoints.Should().HaveCount(1);
        endpoints[0].Name.Should().Be("CreateOrder");
        endpoints[0].Route.Should().Be("/api/v1/orders");
    }

    [Test]
    public async Task Services_BooleanFilter_NotIsLibrary()
    {
        var nonLibServices = await _graph.Services
            .Where(s => !s.IsLibrary)
            .ToListAsync();

        nonLibServices.Should().HaveCount(4);
    }
}
