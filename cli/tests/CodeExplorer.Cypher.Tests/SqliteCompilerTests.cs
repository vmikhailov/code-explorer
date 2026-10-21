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

        rows.Should().HaveCount(1);
        rows[0]["name"].Should().Be("OrderService");
        rows[0]["fullName"].Should().Be("MyProject.OrderService");
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

        rows.Should().HaveCount(2);
        rows[0]["className"].Should().Be("ICustomer");
        rows[1]["className"].Should().Be("OrderService");
        rows[1]["fileName"].Should().Be("Order.cs");
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

        rows.Should().HaveCount(5); // 2 types + 3 functions
        var orderRow = rows.Find(r => (string)r["name"]! == "OrderService");
        orderRow!["mappedType"].Should().Be("Class");

        var fnRow = rows.Find(r => (string)r["name"]! == "ProcessOrder");
        fnRow!["mappedType"].Should().Be("Function");
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

        rows.Should().HaveCount(2);
        // Customer.cs defines ICustomer (interface, not class), so c is null!
        var customerRow = rows.Find(r => (string)r["fileName"]! == "Customer.cs");
        customerRow!["className"].Should().BeNull();

        // Order.cs defines OrderService (class)
        var orderRow = rows.Find(r => (string)r["fileName"]! == "Order.cs");
        orderRow!["className"].Should().Be("OrderService");
    }

    [Test]
    public void Test_RecursiveCTE_VariableLengthCallChain()
    {
        var cypher = @"
            MATCH path = (src:Function {name: 'ProcessOrder'})-[:CALLS*1..5]->(tgt:Function)
            RETURN src.name AS srcName, tgt.name AS tgtName, nodes(path) AS chain
        ";
        var rows = ExecuteCypher(cypher);

        rows.Should().HaveCount(2); // ProcessOrder -> SaveToDb (depth 1), ProcessOrder -> SendNotification (depth 2)
        var targets = new List<string?> { (string?)rows[0]["tgtName"], (string?)rows[1]["tgtName"] };
        targets.Should().Contain("SaveToDb");
        targets.Should().Contain("SendNotification");
    }

    [Test]
    public void Test_Aggregations_CountAndCollect()
    {
        var cypher = @"
            MATCH (c:Type)-[:HAS_METHOD]->(m:Function)
            RETURN c.name AS className, count(m) AS methodCount
        ";
        var rows = ExecuteCypher(cypher);

        rows.Should().HaveCount(1);
        rows[0]["className"].Should().Be("OrderService");
        Convert.ToInt64(rows[0]["methodCount"]).Should().Be(3);
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
        compiled.Sql.Should().Contain("LEFT JOIN nodes ep ON 1=1 AND ep.kind = 'Endpoint' AND (json_extract(ep.properties, '$.path') = json_extract(f.properties, '$.path'))");

        var rows = ExecuteCypher(cypher);

        // We expect exactly 2 rows (one for each file), NOT 4 (no Cartesian product!)
        rows.Should().HaveCount(2);

        var customerRow = rows.Find(r => (string)r["fileName"]! == "Customer.cs");
        customerRow!["endpointName"].Should().BeNull();

        var orderRow = rows.Find(r => (string)r["fileName"]! == "Order.cs");
        orderRow!["endpointName"].Should().Be("OrderEndpoint");
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
        compiled.Sql.Should().Contain("LEFT JOIN nodes es ON 1=1 AND es.kind = 'ExternalService' AND (json_extract(es.properties, '$.file_path') = json_extract(f.properties, '$.path'))");

        var rows = ExecuteCypher(cypher);

        rows.Should().HaveCount(1);
        var egressJson = (string)rows[0]["egress"]!;
        egressJson.Should().Contain("PaymentApi");
        egressJson.Should().NotContain("BillingApi");
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
        compiled.Sql.Should().NotBeEmpty();
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
        rows.Should().HaveCount(1);
        rows[0]["relType"].Should().Be("CALLS");
        rows[0]["rKind"].Should().Be("CALLS");
        rows[0]["fromId"].Should().Be("fn:process");
        rows[0]["toId"].Should().Be("fn:save");
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
        rows.Should().HaveCount(1);
        var relJson = (string)rows[0]["r"]!;

        using var doc = JsonDocument.Parse(relJson);

        doc.RootElement.GetProperty("type").GetString().Should().Be("CALLS");
        doc.RootElement.GetProperty("from").GetString().Should().Be("fn:process");
        doc.RootElement.GetProperty("to").GetString().Should().Be("fn:save");
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
        compiled.Sql.Should().StartWith("SELECT DISTINCT");
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
        rows.Should().HaveCount(1);
        rows[0]["id"].Should().Be("pkg:2");
    }

    [Test]
    public void Test_SemanticRoleLabels_Service_App_Library_Matching()
    {
        InsertNode("proj:svc", "Service", new() { ["name"] = "OrdersApi", ["role"] = "Service" });
        InsertNode("proj:app", "App", new() { ["name"] = "ShopWeb", ["role"] = "FrontendApp" });
        InsertNode("proj:lib", "Library", new() { ["name"] = "ShopCore", ["role"] = "SharedLibrary", ["is_library"] = true });
        InsertNode("proj:worker", "Worker", new() { ["name"] = "NotificationWorker", ["role"] = "Worker" });
        InsertNode("proj:cli", "CliTool", new() { ["name"] = "MigratorCli", ["role"] = "CliTool" });
        InsertNode("proj:generic", "Project", new() { ["name"] = "GeneralProj", ["role"] = "Project" });

        // 1. MATCH (s:Service)
        var svcRows = ExecuteCypher("MATCH (s:Service) RETURN s.name AS name");
        svcRows.Should().HaveCount(1);
        svcRows[0]["name"].Should().Be("OrdersApi");

        // 2. MATCH (a:App)
        var appRows = ExecuteCypher("MATCH (a:App) RETURN a.name AS name");
        appRows.Should().HaveCount(1);
        appRows[0]["name"].Should().Be("ShopWeb");

        // 3. MATCH (l:Library)
        var libRows = ExecuteCypher("MATCH (l:Library) RETURN l.name AS name");
        libRows.Should().HaveCount(1);
        libRows[0]["name"].Should().Be("ShopCore");

        // 4. MATCH (w:Worker)
        var workerRows = ExecuteCypher("MATCH (w:Worker) RETURN w.name AS name");
        workerRows.Should().HaveCount(1);
        workerRows[0]["name"].Should().Be("NotificationWorker");

        // 5. MATCH (c:CliTool)
        var cliRows = ExecuteCypher("MATCH (c:CliTool) RETURN c.name AS name");
        cliRows.Should().HaveCount(1);
        cliRows[0]["name"].Should().Be("MigratorCli");

        // 6. MATCH (p:Project) matches Project
        var projRows = ExecuteCypher("MATCH (p:Project) RETURN p.name AS name");
        projRows.Should().HaveCount(1);
        projRows[0]["name"].Should().Be("GeneralProj");

        // 7. WHERE s:Service filter
        var whereRows = ExecuteCypher("MATCH (s) WHERE s:Service RETURN s.name AS name");
        whereRows.Should().HaveCount(1);
        whereRows[0]["name"].Should().Be("OrdersApi");

        // 8. labels(s) includes both Service and Project
        var labelsRows = ExecuteCypher("MATCH (s:Service) RETURN labels(s) AS lbls");
        labelsRows.Should().HaveCount(1);
        var lbls = (string)labelsRows[0]["lbls"]!;
        lbls.Should().Contain("Project");
        lbls.Should().Contain("Service");
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
        svcRows.Should().HaveCount(1);
        svcRows[0]["name"].Should().Be("BillingService");

        // 2. MATCH (a:App) matches native App
        var appRows = ExecuteCypher("MATCH (a:App) WHERE a.name = 'PortalApp' RETURN a.name AS name");
        appRows.Should().HaveCount(1);
        appRows[0]["name"].Should().Be("PortalApp");

        // 3. MATCH (l:Library) matches native Library
        var libRows = ExecuteCypher("MATCH (l:Library) WHERE l.name = 'DomainCommon' RETURN l.name AS name");
        libRows.Should().HaveCount(1);
        libRows[0]["name"].Should().Be("DomainCommon");

        // 4. MATCH (w:Worker) matches native Worker
        var workerRows = ExecuteCypher("MATCH (w:Worker) WHERE w.name = 'AuditWorker' RETURN w.name AS name");
        workerRows.Should().HaveCount(1);
        workerRows[0]["name"].Should().Be("AuditWorker");

        // 5. MATCH (c:CliTool) matches native CliTool
        var cliRows = ExecuteCypher("MATCH (c:CliTool) WHERE c.name = 'AdminCli' RETURN c.name AS name");
        cliRows.Should().HaveCount(1);
        cliRows[0]["name"].Should().Be("AdminCli");

        // 6. MATCH (p:Project) matches native Project
        var projRows = ExecuteCypher("MATCH (p:Project) RETURN p.name AS name");
        projRows.Should().HaveCount(1);
        projRows[0]["name"].Should().Be("LegacyProject");

        // 7. labels(s) on native Service includes both Service and Project
        var labelsRows = ExecuteCypher("MATCH (s:Service) WHERE s.name = 'BillingService' RETURN labels(s) AS lbls");
        labelsRows.Should().HaveCount(1);
        var lbls = (string)labelsRows[0]["lbls"]!;
        lbls.Should().Contain("Project");
        lbls.Should().Contain("Service");

        // 8. labels(l) on native Library includes both Library and Project
        var libLabelsRows = ExecuteCypher("MATCH (l:Library) WHERE l.name = 'DomainCommon' RETURN labels(l) AS lbls");
        libLabelsRows.Should().HaveCount(1);
        var libLbls = (string)libLabelsRows[0]["lbls"]!;
        libLbls.Should().Contain("Project");
        libLbls.Should().Contain("Library");
    }

    [Test]
    public void Test_Epic3_PathPredicates_And_Quantifiers()
    {
        var rows = ExecuteCypher("MATCH (a:Function) WHERE EXISTS((a)-[:CALLS]->(:Function)) RETURN a.name AS name");
        rows.Should().HaveCountGreaterThan(0);

        var rows2 = ExecuteCypher("MATCH (a:Function) WHERE (a)-[:CALLS]->(:Function) RETURN a.name AS name");
        rows2.Should().HaveCount(rows.Count);

        // Test quantifiers: any, all, none, single
        InsertNode("fn:tagged", "Function", new() { ["name"] = "TaggedFn", ["tags"] = new[] { "security", "audit" } });
        var rowsAny = ExecuteCypher("MATCH (f:Function) WHERE any(t IN f.tags WHERE t = 'security') RETURN f.name AS name");
        rowsAny.Should().HaveCount(1);
        rowsAny[0]["name"].Should().Be("TaggedFn");

        var rowsAll = ExecuteCypher("MATCH (f:Function) WHERE all(t IN ['a', 'b'] WHERE t IN ['a', 'b', 'c']) RETURN f.name AS name LIMIT 1");
        rowsAll.Should().HaveCount(1);

        var rowsNone = ExecuteCypher("MATCH (f:Function) WHERE none(t IN f.tags WHERE t = 'nonexistent') AND f.name = 'TaggedFn' RETURN f.name AS name");
        rowsNone.Should().HaveCount(1);

        var rowsSingle = ExecuteCypher("MATCH (f:Function) WHERE single(t IN f.tags WHERE t = 'security') AND f.name = 'TaggedFn' RETURN f.name AS name");
        rowsSingle.Should().HaveCount(1);
    }

    [Test]
    public void Test_Epic3_Relationship_Functions_In_With_And_Aggregations()
    {
        // Insert edge with properties

        using var cmd = _conn.CreateCommand();

        cmd.CommandText =
            "INSERT INTO edges (from_id, to_id, kind, properties) VALUES ('fn:process', 'fn:notify', 'ASYNC_CALL', '{\"via\":\"rabbitmq\",\"call_chain\":\"process->notify\"}')";
        cmd.ExecuteNonQuery();

        // 1. Direct access to relationship properties: r.via, r.call_chain
        var rowsDirect =
            ExecuteCypher(
                "MATCH (a:Function)-[r:ASYNC_CALL]->(b:Function) RETURN r.via AS via, r.call_chain AS chain");
        rowsDirect.Should().HaveCount(1);
        rowsDirect[0]["via"].Should().Be("rabbitmq");
        rowsDirect[0]["chain"].Should().Be("process->notify");

        // 2. type(r), startNode(r), endNode(r), properties(r) in WITH
        var rowsWith = ExecuteCypher(@"
            MATCH (a:Function)-[r]->(b:Function)
            WITH type(r) AS relType, startNode(r) AS src, endNode(r) AS dst, properties(r) AS props, count(r) AS cnt
            RETURN relType, src, dst, cnt
            ORDER BY relType
        ");
        rowsWith.Should().HaveCountGreaterThan(0);

        // 3. WITH r (passing relationship object itself) and then type(r)
        var rowsWithRel = ExecuteCypher(@"
            MATCH (a:Function)-[r:ASYNC_CALL]->(b:Function)
            WITH r, count(b) AS cnt
            RETURN type(r) AS t, r.via AS via, cnt
        ");
        rowsWithRel.Should().HaveCount(1);
        rowsWithRel[0]["t"].Should().Be("ASYNC_CALL");
        rowsWithRel[0]["via"].Should().Be("rabbitmq");
    }

    [Test]
    public void Test_Epic3_CartesianProduct_Decomposition()
    {
        InsertNode("proj:decomp", "Project", new() { ["name"] = "DecompApi" });
        InsertNode("ep:d1", "Endpoint", new() { ["name"] = "get_orders" });
        InsertNode("ep:d2", "Endpoint", new() { ["name"] = "create_order" });
        InsertNode("db:d1", "Database", new() { ["name"] = "orders_db" });
        InsertNode("db:d2", "Database", new() { ["name"] = "cache_redis" });

        InsertEdge("proj:decomp", "ep:d1", "CONTAINS");
        InsertEdge("proj:decomp", "ep:d2", "CONTAINS");
        InsertEdge("proj:decomp", "db:d1", "USES_DB");
        InsertEdge("proj:decomp", "db:d2", "USES_DB");

        var cypher = @"
            MATCH (p:Project) WHERE p.name = 'DecompApi'
            OPTIONAL MATCH (p)-[:CONTAINS]->(ep:Endpoint)
            OPTIONAL MATCH (p)-[:USES_DB]->(db:Database)
            RETURN p.name, collect(ep.name) AS eps, collect(db.name) AS dbs, count(ep) AS epCount, count(db) AS dbCount
        ";
        var rows = ExecuteCypher(cypher);
        rows.Should().HaveCount(1);
        rows[0]["eps"].Should().Be("[\"get_orders\",\"create_order\"]");
        rows[0]["dbs"].Should().Be("[\"orders_db\",\"cache_redis\"]");
        Convert.ToInt64(rows[0]["epCount"]).Should().Be(2);
        Convert.ToInt64(rows[0]["dbCount"]).Should().Be(2);
    }

    [Test]
    public void Test_Epic3_ListComprehension_TemplateRendering()
    {
        InsertNode("fn:comp", "Function", new() { ["name"] = "CompFn", ["tags"] = new[] { "security", "audit", "internal" } });

        // 1. Direct List Comprehension over JSON property (uses RenderListComprehension)
        var rowsDirect = ExecuteCypher(@"
            MATCH (f:Function) WHERE f.name = 'CompFn'
            RETURN [t IN f.tags WHERE t <> 'audit' | toUpper(t)] AS activeTags
        ");
        rowsDirect.Should().HaveCount(1);
        var tags = (string)rowsDirect[0]["activeTags"]!;
        tags.Should().Contain("SECURITY");
        tags.Should().Contain("INTERNAL");
        tags.Should().NotContain("audit");

        // 2. List Comprehension over collected array in WITH (uses RenderCollectedArray)
        var rowsCollected = ExecuteCypher(@"
            MATCH (f:Function)
            WITH collect(f.name) AS allNames
            RETURN [name IN allNames WHERE name STARTS WITH 'Comp' | toLower(name)] AS compNames
        ");
        rowsCollected.Should().HaveCount(1);
        var compNames = (string)rowsCollected[0]["compNames"]!;
        compNames.Should().Contain("compfn");
    }
    [Test]
    public void Test_DirectLabels_GeneratesCleanSqlWithoutSubqueries()
    {
        var cypher = "MATCH (s1:Service)-[:CALLS]->(s2:Service)-[:USES_DB]->(d:Database) RETURN s1.name, s2.name, d.name LIMIT 50";
        var ast = CypherQueryParser.Parse(cypher);

        var compiled = SqliteCompiler.Compile(ast);
        compiled.Sql.Should().NotContain("NOT EXISTS");
        compiled.Sql.Should().Contain("s1.kind = 'Service'");
        compiled.Sql.Should().Contain("s2.kind = 'Service'");
        compiled.Sql.Should().Contain("d.kind = 'Database'");
    }

    [Test]
    public void Test_ArithmeticAddition_EvaluatesNumerically()
    {
        InsertNode("fn:math", "Function", new() { ["name"] = "MathFn", ["start_line"] = 10, ["end_line"] = 25 });

        var rows = ExecuteCypher(@"
            MATCH (f:Function) WHERE f.name = 'MathFn'
            RETURN (f.end_line - f.start_line + 1) AS loc,
                   'Length: ' + toString(f.end_line - f.start_line + 1) AS label
        ");
        rows.Should().HaveCount(1);
        Convert.ToInt64(rows[0]["loc"]).Should().Be(16);
        ((string)rows[0]["label"]!).Should().Be("Length: 16");
    }

    [Test]
    public void Test_SingleNodePatternPredicate_InWhereClause()
    {
        InsertNode("es:users", "ExternalService", new() { ["name"] = "UsersClient", ["path"] = "/api/v1/users" });
        InsertNode("es:orders", "ExternalService", new() { ["name"] = "OrdersClient", ["path"] = "/api/v1/orders" });
        InsertNode("ep:users", "Endpoint", new() { ["name"] = "UsersEndpoint", ["path"] = "/api/v1/users" });

        // 1. NOT (:Endpoint { path: es.path })
        var rowsPattern = ExecuteCypher(@"
            MATCH (es:ExternalService)
            WHERE NOT (:Endpoint { path: es.path })
            RETURN es.name AS name
        ");
        rowsPattern.Should().HaveCount(1);
        ((string)rowsPattern[0]["name"]!).Should().Be("OrdersClient");

        // 2. NOT exists((:Endpoint { path: es.path }))
        var rowsExists = ExecuteCypher(@"
            MATCH (es:ExternalService)
            WHERE NOT exists((:Endpoint { path: es.path }))
            RETURN es.name AS name
        ");
        rowsExists.Should().HaveCount(1);
        ((string)rowsExists[0]["name"]!).Should().Be("OrdersClient");
    }

    [Test]
    public void Test_VarLenCte_PushesWherePredicateIntoAnchor()
    {
        InsertNode("fn:a", "Function", new() { ["name"] = "StartFn" });
        InsertNode("fn:b", "Function", new() { ["name"] = "MidFn" });
        InsertNode("fn:c", "Function", new() { ["name"] = "EndFn" });
        InsertEdge("fn:a", "fn:b", "CALLS");
        InsertEdge("fn:b", "fn:c", "CALLS");

        var cypher = "MATCH (a:Function)-[:CALLS*1..2]->(b:Function) WHERE a.name = 'StartFn' RETURN b.name AS name ORDER BY b.name";
        var ast = CypherQueryParser.Parse(cypher);
        var compiled = SqliteCompiler.Compile(ast);

        compiled.Sql.Should().Contain("FROM nodes _src_anc CROSS JOIN edges e ON e.from_id = _src_anc.id");
        compiled.Sql.Should().Contain("json_extract(_src_anc.properties, '$.name')");

        var rows = ExecuteCypher(cypher);
        rows.Should().HaveCount(2);
        rows.Select(r => (string)r["name"]!).Should().ContainInOrder("EndFn", "MidFn");
    }

    [Test]
    public void Test_VarLenCte_WalksBackwardsWhenOnlyEndNodeConstrained()
    {
        InsertNode("fn:r1", "Function", new() { ["name"] = "RootCaller" });
        InsertNode("fn:r2", "Function", new() { ["name"] = "DirectCaller" });
        InsertNode("fn:r3", "Function", new() { ["name"] = "LeafTarget" });
        InsertEdge("fn:r1", "fn:r2", "CALLS");
        InsertEdge("fn:r2", "fn:r3", "CALLS");

        var cypher = "MATCH (caller)-[:CALLS*1..2]->(target:Function) WHERE target.name = 'LeafTarget' RETURN caller.name AS name ORDER BY caller.name";
        var ast = CypherQueryParser.Parse(cypher);
        var compiled = SqliteCompiler.Compile(ast);

        compiled.Sql.Should().Contain("FROM nodes _src_anc CROSS JOIN edges e ON e.to_id = _src_anc.id");
        compiled.Sql.Should().Contain("json_extract(_src_anc.properties, '$.name')");

        var rows = ExecuteCypher(cypher);
        rows.Should().HaveCount(2);
        rows.Select(r => (string)r["name"]!).Should().ContainInOrder("DirectCaller", "RootCaller");
    }

    [Test]
    public void Test_VarLenCte_SupportsUndirectedVariableLengthPath()
    {
        InsertNode("fn:u1", "Function", new() { ["name"] = "UpstreamCaller" });
        InsertNode("fn:u2", "Function", new() { ["name"] = "CenterHub" });
        InsertNode("fn:u3", "Function", new() { ["name"] = "DownstreamCallee" });
        InsertEdge("fn:u1", "fn:u2", "CALLS");
        InsertEdge("fn:u2", "fn:u3", "CALLS");

        var rows = ExecuteCypher(@"
            MATCH (a:Function {name: 'CenterHub'})-[:CALLS*1..1]-(b:Function)
            WHERE b.name <> 'CenterHub'
            RETURN DISTINCT b.name AS name
            ORDER BY name
        ");

        rows.Should().HaveCount(2);
        rows.Select(r => (string)r["name"]!).Should().ContainInOrder("DownstreamCallee", "UpstreamCaller");
    }

    [Test]
    public void Test_CollectCompoundIdentifiers_PreservesHasLabelAndListSliceInWithAggregation()
    {
        InsertNode("svc:order", "Service", new() { ["name"] = "OrderSvc", ["tags"] = new[] { "t1", "t2", "t3" } });
        InsertNode("fn:op1", "Function", new() { ["name"] = "Op1" });
        InsertEdge("svc:order", "fn:op1", "CONTAINS");

        var rows = ExecuteCypher(@"
            MATCH (s:Service)-[:CONTAINS]->(f:Function)
            WHERE s.name = 'OrderSvc'
            WITH s, count(f) AS fnCount
            RETURN CASE WHEN s:Service THEN s.tags[0..2] ELSE [] END AS slicedTags, fnCount
        ");

        rows.Should().HaveCount(1);
        Convert.ToInt64(rows[0]["fnCount"]).Should().Be(1);
        ((string)rows[0]["slicedTags"]!).Should().Contain("t1").And.Contain("t2");
    }
}