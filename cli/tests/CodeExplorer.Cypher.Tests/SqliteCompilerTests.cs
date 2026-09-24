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

    [Test]
    public void Test_OptionalMatch_SingleNode_WithWherePredicate_PreservesConditionInOnClause()
    {
        // Insert endpoints: one matches Order.cs, one is unrelated
        InsertNode("ep:1", "Endpoint", new() { ["name"] = "OrderEndpoint", ["path"] = "src/Order.cs" });
        InsertNode("ep:2", "Endpoint", new() { ["name"] = "OtherEndpoint", ["path"] = "src/Other.cs" });

        var cypher = @"
            MATCH (f:File)
            OPTIONAL MATCH (ep:Endpoint) WHERE ep.path = f.path
            RETURN f.name AS fileName, ep.name AS endpointName
            ORDER BY fileName ASC
        ";

        var ast = CypherQueryParser.Parse(cypher);
        var compiled = SqliteCompiler.Compile(ast);

        // Verify the condition was placed into the LEFT JOIN ON clause, not dropped
        Assert.That(compiled.Sql, Does.Contain("LEFT JOIN nodes ep ON 1=1 AND ep.kind = 'Endpoint' AND (json_extract(ep.properties, '$.path') = json_extract(f.properties, '$.path'))"));

        var rows = ExecuteCypher(cypher);

        // We expect exactly 2 rows (one for each file), NOT 4 (no Cartesian product!)
        Assert.That(rows, Has.Count.EqualTo(2));

        var customerRow = rows.Find(r => (string)r["fileName"]! == "Customer.cs");
        Assert.That(customerRow!["endpointName"], Is.Null);

        var orderRow = rows.Find(r => (string)r["fileName"]! == "Order.cs");
        Assert.That(orderRow!["endpointName"], Is.EqualTo("OrderEndpoint"));
    }

    [Test]
    public void Test_OptionalMatch_SingleNode_WithInListComprehension_DoesNotCartesianJoin()
    {
        // Insert external services: one matching Order.cs, one matching an unrelated path
        InsertNode("es:1", "ExternalService", new() { ["name"] = "PaymentApi", ["file_path"] = "src/Order.cs" });
        InsertNode("es:2", "ExternalService", new() { ["name"] = "BillingApi", ["file_path"] = "src/External.cs" });

        var cypher = @"
            MATCH (w:Workspace)-[:CONTAINS]->(f:File)
            WITH w, collect(DISTINCT f) AS files
            WITH w, files, [x IN files | x.path] AS filePaths
            OPTIONAL MATCH (es:ExternalService) WHERE es.file_path IN filePaths
            RETURN w.name AS wsName, collect(DISTINCT es.name) AS egress
        ";

        var ast = CypherQueryParser.Parse(cypher);
        var compiled = SqliteCompiler.Compile(ast);

        // Verify the list comprehension was collapsed to an equality join in the ON clause
        Assert.That(compiled.Sql, Does.Contain("LEFT JOIN nodes es ON 1=1 AND es.kind = 'ExternalService' AND (json_extract(es.properties, '$.file_path') = json_extract(f.properties, '$.path'))"));

        var rows = ExecuteCypher(cypher);

        Assert.That(rows, Has.Count.EqualTo(1));
        var egressJson = (string)rows[0]["egress"]!;
        Assert.That(egressJson, Does.Contain("PaymentApi"));
        Assert.That(egressJson, Does.Not.Contain("BillingApi"));
    }

    [Test]
    public void Test_AttributedTo_Compilation()
    {
        var cypher = """
            MATCH path = (ep:EntryPoint)<-[:IMPLEMENTS]-(fn:Function)-[:CALLS*0..15]->(sink)
            WHERE ep.id STARTS WITH '1:' AND (sink:ExternalService OR sink:DB OR sink:Query)
            RETURN ep.id AS from_id, sink.id AS to_id, min(length(path)) AS hops, labels(sink)[0] AS sinkKind
            """;
        var ast = CypherQueryParser.Parse(cypher);
        var compiled = SqliteCompiler.Compile(ast);
        Assert.That(compiled.Sql, Is.Not.Empty);
    }

    [Test]
    public void Test_Relationship_Functions_Type_StartNode_EndNode_Properties()
    {
        var cypher = @"
            MATCH (a:Function)-[r:CALLS]->(b:Function)
            RETURN type(r) AS relType, startNode(r) AS fromId, endNode(r) AS toId, properties(r) AS relProps, r.kind AS rKind
            LIMIT 1
        ";

        var rows = ExecuteCypher(cypher);
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["relType"], Is.EqualTo("CALLS"));
        Assert.That(rows[0]["rKind"], Is.EqualTo("CALLS"));
        Assert.That(rows[0]["fromId"], Is.EqualTo("fn:process"));
        Assert.That(rows[0]["toId"], Is.EqualTo("fn:save"));
    }

    [Test]
    public void Test_Return_Relationship_Object()
    {
        var cypher = @"
            MATCH (a:Function)-[r:CALLS]->(b:Function)
            RETURN r
            LIMIT 1
        ";

        var rows = ExecuteCypher(cypher);
        Assert.That(rows, Has.Count.EqualTo(1));
        var relJson = (string)rows[0]["r"]!;
        using var doc = JsonDocument.Parse(relJson);
        Assert.That(doc.RootElement.GetProperty("type").GetString(), Is.EqualTo("CALLS"));
        Assert.That(doc.RootElement.GetProperty("from").GetString(), Is.EqualTo("fn:process"));
        Assert.That(doc.RootElement.GetProperty("to").GetString(), Is.EqualTo("fn:save"));
    }

    [Test]
    public void Test_With_Distinct_Propagates_To_Select()
    {
        var cypher = @"
            MATCH (n)-[r]->(m)
            WITH DISTINCT labels(n)[0] AS fromLabel, type(r) AS relType, labels(m)[0] AS toLabel
            RETURN fromLabel, relType, toLabel
        ";

        var compiled = SqliteCompiler.Compile(CypherQueryParser.Parse(cypher));
        Assert.That(compiled.Sql, Does.StartWith("SELECT DISTINCT"));
    }

    [Test]
    public void Test_PatternExpression_WithOuterTargetNode()
    {
        InsertNode("proj:1", "Project", new() { ["name"] = "MyProject" });
        InsertNode("pkg:1", "Package", new() { ["name"] = "MyPkg" });
        InsertNode("pkg:2", "Package", new() { ["name"] = "OtherPkg" });
        InsertEdge("proj:1", "pkg:1", "DEPENDS_ON");
        InsertEdge("proj:1", "pkg:2", "DEPENDS_ON");
        InsertEdge("pkg:1", "proj:1", "IMPLEMENTED_BY");

        var cypher = @"
            MATCH (p:Project {id: 'proj:1'})-[:DEPENDS_ON]->(pkg:Package)
            WHERE NOT (pkg)-[:IMPLEMENTED_BY]->(p)
            RETURN pkg.id AS id
        ";

        var rows = ExecuteCypher(cypher);
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["id"], Is.EqualTo("pkg:2"));
    }

    [Test]
    public void Test_SemanticRoleLabels_Service_App_Library_Matching()
    {
        InsertNode("proj:svc", "Project", new() { ["name"] = "OrdersApi", ["role"] = "Service" });
        InsertNode("proj:app", "Project", new() { ["name"] = "ShopWeb", ["role"] = "FrontendApp" });
        InsertNode("proj:lib", "Project", new() { ["name"] = "ShopCore", ["role"] = "SharedLibrary", ["is_library"] = true });
        InsertNode("proj:worker", "Project", new() { ["name"] = "NotificationWorker", ["role"] = "Worker" });
        InsertNode("proj:cli", "Project", new() { ["name"] = "MigratorCli", ["role"] = "CliTool" });

        // 1. MATCH (s:Service)
        var svcRows = ExecuteCypher("MATCH (s:Service) RETURN s.name AS name");
        Assert.That(svcRows, Has.Count.EqualTo(1));
        Assert.That(svcRows[0]["name"], Is.EqualTo("OrdersApi"));

        // 2. MATCH (a:App)
        var appRows = ExecuteCypher("MATCH (a:App) RETURN a.name AS name");
        Assert.That(appRows, Has.Count.EqualTo(1));
        Assert.That(appRows[0]["name"], Is.EqualTo("ShopWeb"));

        // 3. MATCH (l:Library)
        var libRows = ExecuteCypher("MATCH (l:Library) RETURN l.name AS name");
        Assert.That(libRows, Has.Count.EqualTo(1));
        Assert.That(libRows[0]["name"], Is.EqualTo("ShopCore"));

        // 4. MATCH (w:Worker)
        var workerRows = ExecuteCypher("MATCH (w:Worker) RETURN w.name AS name");
        Assert.That(workerRows, Has.Count.EqualTo(1));
        Assert.That(workerRows[0]["name"], Is.EqualTo("NotificationWorker"));

        // 5. MATCH (c:CliTool)
        var cliRows = ExecuteCypher("MATCH (c:CliTool) RETURN c.name AS name");
        Assert.That(cliRows, Has.Count.EqualTo(1));
        Assert.That(cliRows[0]["name"], Is.EqualTo("MigratorCli"));

        // 6. MATCH (p:Project) matches all
        var projRows = ExecuteCypher("MATCH (p:Project) WHERE p.name IN ['OrdersApi', 'ShopWeb', 'ShopCore', 'NotificationWorker', 'MigratorCli'] RETURN p.name AS name");
        Assert.That(projRows, Has.Count.EqualTo(5));

        // 7. WHERE p:Service filter
        var whereRows = ExecuteCypher("MATCH (p:Project) WHERE p:Service RETURN p.name AS name");
        Assert.That(whereRows, Has.Count.EqualTo(1));
        Assert.That(whereRows[0]["name"], Is.EqualTo("OrdersApi"));

        // 8. labels(p) includes both Project and role
        var labelsRows = ExecuteCypher("MATCH (s:Service) RETURN labels(s) AS lbls");
        Assert.That(labelsRows, Has.Count.EqualTo(1));
        var lbls = (string)labelsRows[0]["lbls"]!;
        Assert.That(lbls, Does.Contain("Project"));
        Assert.That(lbls, Does.Contain("Service"));
    }

    [Test]
    public void Test_FirstClass_SemanticNodeTypes_Polymorphism()
    {
        InsertNode("sem:svc", "Service", new() { ["name"] = "BillingService", ["role"] = "Service" });
        InsertNode("sem:app", "App", new() { ["name"] = "PortalApp", ["role"] = "FrontendApp" });
        InsertNode("sem:lib", "Library", new() { ["name"] = "DomainCommon", ["role"] = "SharedLibrary", ["is_library"] = true });
        InsertNode("sem:worker", "Worker", new() { ["name"] = "AuditWorker", ["role"] = "Worker" });
        InsertNode("sem:cli", "CliTool", new() { ["name"] = "AdminCli", ["role"] = "CliTool" });
        InsertNode("sem:proj", "Project", new() { ["name"] = "LegacyProject", ["role"] = "Service" });

        // 1. MATCH (s:Service) matches native Service
        var svcRows = ExecuteCypher("MATCH (s:Service) WHERE s.name = 'BillingService' RETURN s.name AS name");
        Assert.That(svcRows, Has.Count.EqualTo(1));
        Assert.That(svcRows[0]["name"], Is.EqualTo("BillingService"));

        // 2. MATCH (a:App) matches native App
        var appRows = ExecuteCypher("MATCH (a:App) WHERE a.name = 'PortalApp' RETURN a.name AS name");
        Assert.That(appRows, Has.Count.EqualTo(1));
        Assert.That(appRows[0]["name"], Is.EqualTo("PortalApp"));

        // 3. MATCH (l:Library) matches native Library
        var libRows = ExecuteCypher("MATCH (l:Library) WHERE l.name = 'DomainCommon' RETURN l.name AS name");
        Assert.That(libRows, Has.Count.EqualTo(1));
        Assert.That(libRows[0]["name"], Is.EqualTo("DomainCommon"));

        // 4. MATCH (w:Worker) matches native Worker
        var workerRows = ExecuteCypher("MATCH (w:Worker) WHERE w.name = 'AuditWorker' RETURN w.name AS name");
        Assert.That(workerRows, Has.Count.EqualTo(1));
        Assert.That(workerRows[0]["name"], Is.EqualTo("AuditWorker"));

        // 5. MATCH (c:CliTool) matches native CliTool
        var cliRows = ExecuteCypher("MATCH (c:CliTool) WHERE c.name = 'AdminCli' RETURN c.name AS name");
        Assert.That(cliRows, Has.Count.EqualTo(1));
        Assert.That(cliRows[0]["name"], Is.EqualTo("AdminCli"));

        // 6. MATCH (p:Project) polymorphically matches ALL first-class semantic entities + Project
        var projRows = ExecuteCypher("MATCH (p:Project) WHERE p.name IN ['BillingService', 'PortalApp', 'DomainCommon', 'AuditWorker', 'AdminCli', 'LegacyProject'] RETURN p.name AS name");
        Assert.That(projRows, Has.Count.EqualTo(6));

        // 7. labels(s) on native Service includes both Service and Project
        var labelsRows = ExecuteCypher("MATCH (s:Service) WHERE s.name = 'BillingService' RETURN labels(s) AS lbls");
        Assert.That(labelsRows, Has.Count.EqualTo(1));
        var lbls = (string)labelsRows[0]["lbls"]!;
        Assert.That(lbls, Does.Contain("Project"));
        Assert.That(lbls, Does.Contain("Service"));

        // 8. labels(l) on native Library includes both Library and Project
        var libLabelsRows = ExecuteCypher("MATCH (l:Library) WHERE l.name = 'DomainCommon' RETURN labels(l) AS lbls");
        Assert.That(libLabelsRows, Has.Count.EqualTo(1));
        var libLbls = (string)libLabelsRows[0]["lbls"]!;
        Assert.That(libLbls, Does.Contain("Project"));
        Assert.That(libLbls, Does.Contain("Library"));
    }
}

