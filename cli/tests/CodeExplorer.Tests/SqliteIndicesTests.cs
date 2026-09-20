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
            "idx_nodes_kind",
            "idx_nodes_kind_path",
            "idx_nodes_kind_file_path",
            "idx_nodes_kind_name",
            "idx_nodes_kind_lower_path",
            "idx_edges_from",
            "idx_edges_to",
            "idx_edges_kind",
            "idx_edges_from_kind",
            "idx_edges_to_kind",
            "idx_edges_from_kind_to",
            "idx_edges_to_kind_from",
            "idx_edges_kind_from_to"
        };

        foreach (var expected in expectedIndices)
        {
            Assert.That(indices, Does.Contain(expected), $"Index '{expected}' should exist in database.");
        }
    }

    [TestCase("SELECT * FROM nodes WHERE kind = 'Endpoint' AND json_extract(properties, '$.path') = 'foo'", "idx_nodes_kind_path")]
    [TestCase("SELECT * FROM nodes WHERE kind = 'ExternalService' AND json_extract(properties, '$.file_path') = 'bar'", "idx_nodes_kind_file_path")]
    [TestCase("SELECT * FROM nodes WHERE kind = 'Project' AND json_extract(properties, '$.name') = 'proj'", "idx_nodes_kind_name")]
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
        Assert.That(fullPlan, Does.Contain(expectedIndex),
            $"Query '{query}' should utilize index '{expectedIndex}'. Query plan was:\n{fullPlan}");
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
        Assert.That(fullPlan, Does.Contain("COVERING INDEX"),
            $"Query '{query}' should utilize a covering index. Query plan was:\n{fullPlan}");
    }
}
