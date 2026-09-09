using System.Collections.Generic;
using System.Data.Common;
using System.Text.Json;
using CodeExplorer.Cypher.Compiler;
using CodeExplorer.Cypher.Parser;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace CodeExplorer.Cypher.Tests;

[TestFixture]
public class SqliteCompilerTests
{
    private SqliteConnection _conn = null!;

    [SetUp]
    public void SetUp()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();

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
    }

    [TearDown]
    public void TearDown()
    {
        _conn.Dispose();
    }

    private void SeedData()
    {
        InsertNode("ws:1", "Workspace", new() { ["name"] = "MyProject", ["path"] = "c:/projects/my_project" });
        InsertNode("file:1", "File", new() { ["name"] = "Order.cs", ["path"] = "src/Order.cs", ["file_path"] = "src/Order.cs" });
        InsertNode("file:2", "File", new() { ["name"] = "Customer.cs", ["path"] = "src/Customer.cs", ["file_path"] = "src/Customer.cs" });
        InsertNode("type:order", "Type", new() { ["name"] = "OrderService", ["kind"] = "class", ["symbol"] = "MyProject.OrderService" });
        InsertNode("type:customer", "Type", new() { ["name"] = "ICustomer", ["kind"] = "interface", ["symbol"] = "MyProject.ICustomer" });
        InsertNode("fn:process", "Function", new() { ["name"] = "ProcessOrder", ["symbol"] = "MyProject.OrderService.ProcessOrder", ["start_line"] = 10, ["end_line"] = 25 });
        InsertNode("fn:save", "Function", new() { ["name"] = "SaveToDb", ["symbol"] = "MyProject.OrderService.SaveToDb", ["start_line"] = 30, ["end_line"] = 45 });
        InsertNode("fn:notify", "Function", new() { ["name"] = "SendNotification", ["symbol"] = "MyProject.OrderService.SendNotification", ["start_line"] = 50, ["end_line"] = 65 });

        // Workspace -> Files
        InsertEdge("ws:1", "file:1", "CONTAINS");
        InsertEdge("ws:1", "file:2", "CONTAINS");

        // Files -> Types
        InsertEdge("file:1", "type:order", "DEFINES");
        InsertEdge("file:2", "type:customer", "DEFINES");

        // Types -> Methods
        InsertEdge("type:order", "fn:process", "HAS_METHOD");
        InsertEdge("type:order", "fn:save", "HAS_METHOD");
        InsertEdge("type:order", "fn:notify", "HAS_METHOD");

        // Call chain: ProcessOrder -> SaveToDb -> SendNotification
        InsertEdge("fn:process", "fn:save", "CALLS");
        InsertEdge("fn:save", "fn:notify", "CALLS");
    }

    private void InsertNode(string id, string kind, Dictionary<string, object> props)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO nodes (id, kind, properties) VALUES (@id, @kind, @props)";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@kind", kind);
        cmd.Parameters.AddWithValue("@props", JsonSerializer.Serialize(props));
        cmd.ExecuteNonQuery();
    }

    private void InsertEdge(string fromId, string toId, string kind)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO edges (from_id, to_id, kind, properties) VALUES (@from, @to, @kind, '{}')";
        cmd.Parameters.AddWithValue("@from", fromId);
        cmd.Parameters.AddWithValue("@to", toId);
        cmd.Parameters.AddWithValue("@kind", kind);
        cmd.ExecuteNonQuery();
    }

    private List<Dictionary<string, object?>> ExecuteCypher(string cypher, Dictionary<string, object?>? parameters = null)
    {
        var ast = CypherQueryParser.Parse(cypher);
        var compiled = SqliteCompiler.Compile(ast, parameters);

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = compiled.Sql;
        foreach (var (k, v) in compiled.Parameters)
        {
            cmd.Parameters.AddWithValue("@" + k.TrimStart('@'), v ?? DBNull.Value);
        }

        var results = new List<Dictionary<string, object?>>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }
        return results;
    }

    [Test]
    public void Test_SimpleNodeFilter_ReturnsMatchingNode()
    {
        var cypher = "MATCH (n:Type) WHERE n.name = $name RETURN n.name AS name, n.symbol AS fullName";
        var rows = ExecuteCypher(cypher, new() { ["name"] = "OrderService" });

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["name"], Is.EqualTo("OrderService"));
        Assert.That(rows[0]["fullName"], Is.EqualTo("MyProject.OrderService"));
    }

    [Test]
    public void Test_MultiHopPattern_NavigatesGraph()
    {
        var cypher = @"
            MATCH (w:Workspace)-[:CONTAINS]->(f:File)-[:DEFINES]->(c:Type)
            RETURN w.name AS wsName, f.name AS fileName, c.name AS className
            ORDER BY className ASC
        ";
        var rows = ExecuteCypher(cypher);

        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows[0]["className"], Is.EqualTo("ICustomer"));
        Assert.That(rows[1]["className"], Is.EqualTo("OrderService"));
        Assert.That(rows[1]["fileName"], Is.EqualTo("Order.cs"));
    }

    [Test]
    public void Test_CaseExpression_And_LabelsAccess()
    {
        var cypher = @"
            MATCH (n) WHERE n:Type OR n:Function
            RETURN n.name AS name,
                   CASE WHEN n:Type THEN (CASE WHEN n.kind = 'class' THEN 'Class' ELSE 'Interface' END)
                        ELSE labels(n)[0] END AS mappedType
            ORDER BY name ASC
        ";
        var rows = ExecuteCypher(cypher);

        Assert.That(rows, Has.Count.EqualTo(5)); // 2 types + 3 functions
        var orderRow = rows.Find(r => (string)r["name"]! == "OrderService");
        Assert.That(orderRow!["mappedType"], Is.EqualTo("Class"));

        var fnRow = rows.Find(r => (string)r["name"]! == "ProcessOrder");
        Assert.That(fnRow!["mappedType"], Is.EqualTo("Function"));
    }

    [Test]
    public void Test_OptionalMatch_PreservesRowsWhenNoMatch()
    {
        var cypher = @"
            MATCH (f:File)
            OPTIONAL MATCH (f)-[:DEFINES]->(c:Type {kind: 'class'})
            RETURN f.name AS fileName, c.name AS className
            ORDER BY fileName ASC
        ";
        var rows = ExecuteCypher(cypher);

        Assert.That(rows, Has.Count.EqualTo(2));
        // Customer.cs defines ICustomer (interface, not class), so c is null!
        var customerRow = rows.Find(r => (string)r["fileName"]! == "Customer.cs");
        Assert.That(customerRow!["className"], Is.Null);

        // Order.cs defines OrderService (class)
        var orderRow = rows.Find(r => (string)r["fileName"]! == "Order.cs");
        Assert.That(orderRow!["className"], Is.EqualTo("OrderService"));
    }

    [Test]
    public void Test_RecursiveCTE_VariableLengthCallChain()
    {
        var cypher = @"
            MATCH path = (src:Function {name: 'ProcessOrder'})-[:CALLS*1..5]->(tgt:Function)
            RETURN src.name AS srcName, tgt.name AS tgtName, nodes(path) AS chain
        ";
        var rows = ExecuteCypher(cypher);

        Assert.That(rows, Has.Count.EqualTo(2)); // ProcessOrder -> SaveToDb (depth 1), ProcessOrder -> SendNotification (depth 2)
        var targets = new List<string?> { (string?)rows[0]["tgtName"], (string?)rows[1]["tgtName"] };
        Assert.That(targets, Contains.Item("SaveToDb"));
        Assert.That(targets, Contains.Item("SendNotification"));
    }

    [Test]
    public void Test_Aggregations_CountAndCollect()
    {
        var cypher = @"
            MATCH (c:Type)-[:HAS_METHOD]->(m:Function)
            RETURN c.name AS className, count(m) AS methodCount
        ";
        var rows = ExecuteCypher(cypher);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["className"], Is.EqualTo("OrderService"));
        Assert.That(Convert.ToInt64(rows[0]["methodCount"]), Is.EqualTo(3));
    }
}
