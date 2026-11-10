using System.Text.Json;
using CodeExplorer.Cypher.Common;
using CodeExplorer.Cypher.Compiler;
using CodeExplorer.Cypher.Parser;
using CodeExplorer.Cypher.Tests.Shared;
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
        SqliteCypherFunctions.Register(_conn);

        using var cmd = _conn.CreateCommand();

        cmd.CommandText = @"
            CREATE TABLE nodes (
                id TEXT PRIMARY KEY,
                kind INTEGER NOT NULL,
                properties JSONB NOT NULL
            );

            CREATE TABLE edges (
                from_id INTEGER NOT NULL,
                to_id INTEGER NOT NULL,
                kind INTEGER NOT NULL,
                properties JSONB NOT NULL
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

        cmd.CommandText = "INSERT INTO nodes (id, kind, properties) VALUES (@id, @kind, jsonb(@props))";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@kind", GraphKindCodec.EncodeNodeKind(kind));
        cmd.Parameters.AddWithValue("@props", JsonSerializer.Serialize(props));
        cmd.ExecuteNonQuery();
    }

    private void InsertEdge(string fromId, string toId, string kind, Dictionary<string, object>? props = null)
    {
        using var cmd = _conn.CreateCommand();

        cmd.CommandText = "INSERT INTO edges (from_id, to_id, kind, properties) VALUES ((SELECT rowid FROM nodes WHERE id = @from), (SELECT rowid FROM nodes WHERE id = @to), @kind, jsonb(@props))";
        cmd.Parameters.AddWithValue("@from", fromId);
        cmd.Parameters.AddWithValue("@to", toId);
        cmd.Parameters.AddWithValue("@kind", GraphKindCodec.EncodeRelKind(kind));
        cmd.Parameters.AddWithValue("@props", props != null ? JsonSerializer.Serialize(props) : "{}");
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
        compiled.Sql.Should().Contain($"LEFT JOIN nodes ep ON 1=1 AND ep.kind = {GraphKindCodec.EncodeNodeKind("Endpoint")} AND (json_extract(ep.properties, '$.path') = json_extract(f.properties, '$.path'))");

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
        compiled.Sql.Should().Contain($"LEFT JOIN nodes es ON 1=1 AND es.kind = {GraphKindCodec.EncodeNodeKind("ExternalService")} AND (json_extract(es.properties, '$.file_path') = json_extract(f.properties, '$.path'))");

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
    public void Test_Length_On_VariableLengthRelationship()
    {
        var cypher = """
            MATCH (a:Function)-[r:CALLS*1..2]->(b:Function)
            WHERE a.id = 'fn:process'
            RETURN b.id AS to_id, length(r) AS hops, size(r) AS relSize
            ORDER BY hops
            """;
        var rows = ExecuteCypher(cypher);
        rows.Should().NotBeEmpty();
        rows[0]["hops"].Should().Be(1L);
        rows[0]["relSize"].Should().Be(1L);
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

        relJson.Should().Contain("\"type\":\"CALLS\"");
        relJson.Should().Contain("\"from\":\"fn:process\"");
        relJson.Should().Contain("\"to\":\"fn:save\"");
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
            $"INSERT INTO edges (from_id, to_id, kind, properties) VALUES ((SELECT rowid FROM nodes WHERE id = 'fn:process'), (SELECT rowid FROM nodes WHERE id = 'fn:notify'), {GraphKindCodec.EncodeRelKind("TRIGGERS")}, '{{\"via\":\"rabbitmq\",\"call_chain\":\"process->notify\"}}')";
        cmd.ExecuteNonQuery();

        // 1. Direct access to relationship properties: r.via, r.call_chain
        var rowsDirect =
            ExecuteCypher(
                "MATCH (a:Function)-[r:TRIGGERS]->(b:Function) RETURN r.via AS via, r.call_chain AS chain");
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
            MATCH (a:Function)-[r:TRIGGERS]->(b:Function)
            WITH r, count(b) AS cnt
            RETURN type(r) AS t, r.via AS via, cnt
        ");
        rowsWithRel.Should().HaveCount(1);
        rowsWithRel[0]["t"].Should().Be("TRIGGERS");
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
        compiled.Sql.Should().Contain($"s1.kind = {GraphKindCodec.EncodeNodeKind("Service")}");
        compiled.Sql.Should().Contain($"s2.kind = {GraphKindCodec.EncodeNodeKind("Service")}");
        compiled.Sql.Should().Contain($"d.kind = {GraphKindCodec.EncodeNodeKind("Database")}");
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

        compiled.Sql.Should().Contain("FROM nodes _src_anc CROSS JOIN edges e ON e.from_id = _src_anc.rowid");
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

        compiled.Sql.Should().Contain("FROM nodes _src_anc CROSS JOIN edges e ON e.to_id = _src_anc.rowid");
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

    [Test]
    public void Test_OptionalMatch_MultiHop_WithConjunctsAcrossMultipleHops_DoesNotReferenceRightTablesInOnClause()
    {
        InsertNode("proj:1", "Project", new() { ["name"] = "ModA" });
        InsertNode("proj:2", "Project", new() { ["name"] = "ModB" });
        InsertNode("proj:abs", "Project", new() { ["name"] = "Abstractions" });
        InsertEdge("proj:1", "proj:abs", "DEPENDS_ON");
        InsertEdge("proj:2", "proj:abs", "DEPENDS_ON");

        InsertNode("type:mgr", "Type", new() { ["name"] = "IContentManager" });
        InsertNode("fn:get", "Function", new() { ["name"] = "GetAsync" });
        InsertEdge("type:mgr", "fn:get", "HAS_METHOD");

        InsertNode("fn:caller1", "Function", new() { ["name"] = "ActionA", ["project_name"] = "ModA", ["is_test"] = false });
        InsertEdge("fn:caller1", "fn:get", "CALLS");

        var cypher = @"
            MATCH (p:Project)-[:DEPENDS_ON]->(target:Project {name: 'Abstractions'})
            OPTIONAL MATCH (caller:Function)-[:CALLS]->(cm:Function)<-[:HAS_METHOD]-(t:Type {name: 'IContentManager'})
            WHERE caller.project_name = p.name AND caller.is_test = false AND cm.name IN ['GetAsync', 'CreateAsync']
            WITH p.name AS mod, count(caller) AS callCount
            WHERE callCount = 0
            RETURN mod
        ";

        var rows = ExecuteCypher(cypher);
        rows.Should().HaveCount(1);
        rows[0]["mod"].Should().Be("ModB");

        var reverseCypher = @"
            MATCH (p:Project)-[:DEPENDS_ON]->(target:Project {name: 'Abstractions'})
            OPTIONAL MATCH (t:Type {name: 'IContentManager'})-[:HAS_METHOD]->(cm:Function)<-[:CALLS]-(caller:Function)
            WHERE cm.name IN ['GetAsync', 'CreateAsync'] AND caller.project_name = p.name AND caller.is_test = false
            WITH p.name AS mod, count(DISTINCT caller.id) AS callCount
            WHERE callCount = 0
            RETURN count(mod) AS totalZero, collect(mod) AS mods
        ";

        var revRows = ExecuteCypher(reverseCypher);
        revRows.Should().HaveCount(1);
        Convert.ToInt64(revRows[0]["totalZero"]).Should().Be(1);
        ((string)revRows[0]["mods"]!).Should().Contain("ModB");
    }

    [Test]
    public void Test_UnaliasedReturnItems_UseCanonicalCypherColumnNames()
    {
        var cypher = @"
            MATCH (c:Type)-[r:HAS_METHOD]->(m:Function)
            WHERE c.name = 'OrderService'
            RETURN c.name, labels(c), type(r), count(DISTINCT m)
        ";

        var rows = ExecuteCypher(cypher);
        rows.Should().HaveCount(1);
        rows[0].Keys.Should().BeEquivalentTo(["c.name", "labels(c)", "type(r)", "count(DISTINCT m)"]);
        rows[0]["c.name"].Should().Be("OrderService");
        rows[0]["type(r)"].Should().Be("HAS_METHOD");
        Convert.ToInt64(rows[0]["count(DISTINCT m)"]).Should().Be(3);
    }

    [Test]
    public void Test_ExistsAndCountBraceSubqueries_WithVariableLengthPaths()
    {
        InsertNode("type:baseCtrl", "Type", new() { ["name"] = "ControllerBase", ["extends"] = "Object" });
        InsertNode("type:adminCtrl", "Type", new() { ["name"] = "AdminController", ["extends"] = "ControllerBase" });
        InsertNode("type:itemCtrl", "Type", new() { ["name"] = "ItemController", ["extends"] = "AdminController" });
        InsertNode("fn:edit", "Function", new() { ["name"] = "Edit" });
        InsertEdge("type:itemCtrl", "type:adminCtrl", "INHERITS_FROM");
        InsertEdge("type:adminCtrl", "type:baseCtrl", "INHERITS_FROM");
        InsertEdge("type:itemCtrl", "fn:edit", "HAS_METHOD");

        var cypher = @"
            MATCH (c:Type)-[:HAS_METHOD]->(fn:Function)
            WHERE EXISTS {
                MATCH (c)-[:INHERITS_FROM*1..5]->(b:Type)
                WHERE b.name IN ['Controller', 'ControllerBase'] OR b.extends CONTAINS 'Controller'
            }
            RETURN c.name AS cls, fn.name AS method, COUNT { (c)-[:HAS_METHOD]->(:Function) } AS methodCount
        ";

        var rows = ExecuteCypher(cypher);
        rows.Should().HaveCount(1);
        rows[0]["cls"].Should().Be("ItemController");
        rows[0]["method"].Should().Be("Edit");
        Convert.ToInt64(rows[0]["methodCount"]).Should().Be(1);
    }

    [Test]
    public void Test_WithCollectAncestors_WhereAnyQuantifier_PreservesRelationshipProperties()
    {
        InsertNode("type:baseDriver", "Type", new() { ["name"] = "DisplayDriverBase" });
        InsertNode("type:partDriver", "Type", new() { ["name"] = "ContentPartDisplayDriver", ["extends"] = "DisplayDriverBase" });
        InsertNode("type:concreteDriver", "Type", new() { ["name"] = "AutoroutePartDisplayDriver", ["extends"] = "ContentPartDisplayDriver", ["project_name"] = "OrchardCore.Autoroute" });
        InsertNode("fn:update", "Function", new() { ["name"] = "UpdateAsync" });
        InsertNode("mem:session", "Member", new() { ["name"] = "_session", ["type"] = "YesSql.ISession" });

        InsertEdge("type:concreteDriver", "type:partDriver", "INHERITS_FROM");
        InsertEdge("type:partDriver", "type:baseDriver", "INHERITS_FROM");
        InsertEdge("type:concreteDriver", "fn:update", "HAS_METHOD");
        InsertEdge("mem:session", "fn:update", "ACCESSED_BY", new() { ["operation"] = "QueryIndex, SaveAsync" });

        var cypher = @"
            MATCH (mem:Member)-[r:ACCESSED_BY]->(fn:Function)<-[:HAS_METHOD]-(c:Type)
            WHERE mem.type CONTAINS 'ISession'
            OPTIONAL MATCH (c)-[:INHERITS_FROM|IMPLEMENTS*1..6]->(anc:Type)
            WITH c, fn, mem, r, collect(DISTINCT anc.name) AS ancestors
            WHERE c.extends CONTAINS 'Controller'
               OR any(a IN ancestors WHERE a IN ['Controller', 'ControllerBase'] OR a CONTAINS 'DisplayDriver')
            RETURN c.project_name AS module, c.name AS class_name, fn.name AS method_name, mem.name AS member_name, r.operation AS operations
        ";

        var rows = ExecuteCypher(cypher);
        rows.Should().HaveCount(1);
        rows[0]["module"].Should().Be("OrchardCore.Autoroute");
        rows[0]["class_name"].Should().Be("AutoroutePartDisplayDriver");
        rows[0]["method_name"].Should().Be("UpdateAsync");
        rows[0]["member_name"].Should().Be("_session");
        rows[0]["operations"].Should().Be("QueryIndex, SaveAsync");
    }

    [Test]
    public void Test_ReturnListSliceOverCollect_UngroupedAndGrouped_ExecutesCleanly()
    {
        for (var i = 1; i <= 7; i++)
        {
            InsertNode($"proj:s{i}", "Project", new()
            {
                ["name"] = $"Proj{i}",
                ["path"] = $"src/Proj{i}/Proj{i}.csproj",
                ["is_test"] = i >= 6
            });
        }

        var ungroupedRows = ExecuteCypher(@"
            MATCH (p:Project)
            WHERE p.name STARTS WITH 'Proj'
            RETURN count(p) AS total,
                   sum(CASE WHEN p.is_test = true THEN 1 ELSE 0 END) AS tests,
                   collect(p.name)[0..5] AS sample_names,
                   collect(p.path)[0..3] AS sample_paths
        ");

        ungroupedRows.Should().HaveCount(1);
        Convert.ToInt64(ungroupedRows[0]["total"]).Should().Be(7);
        Convert.ToInt64(ungroupedRows[0]["tests"]).Should().Be(2);
        System.Text.Json.JsonSerializer.Deserialize<string[]>((string)ungroupedRows[0]["sample_names"]!)!.Should().HaveCount(5);
        System.Text.Json.JsonSerializer.Deserialize<string[]>((string)ungroupedRows[0]["sample_paths"]!)!.Should().HaveCount(3);

        InsertNode("type:sliceCtrl", "Type", new() { ["name"] = "SliceController" });
        for (var i = 1; i <= 4; i++)
        {
            InsertNode($"fn:sliceM{i}", "Function", new() { ["name"] = $"Action{i}" });
            InsertEdge("type:sliceCtrl", $"fn:sliceM{i}", "HAS_METHOD");
        }

        var groupedRows = ExecuteCypher(@"
            MATCH (c:Type {name: 'SliceController'})-[:HAS_METHOD]->(m:Function)
            RETURN c.name AS cls, collect(m.name)[0..2] AS sample_methods
        ");

        groupedRows.Should().HaveCount(1);
        groupedRows[0]["cls"].Should().Be("SliceController");
        System.Text.Json.JsonSerializer.Deserialize<string[]>((string)groupedRows[0]["sample_methods"]!)!.Should().HaveCount(2);
    }

    [Test]
    public void Test_DecomposedOptionalMatch_PreservesRelVariablesVarLenAndTailAnchors()
    {
        InsertNode("type:authCtrl", "Type", new() { ["name"] = "WorkflowTypeController", ["attributes"] = "Authorize" });
        InsertNode("fn:index", "Function", new() { ["name"] = "Index", ["visibility"] = "public", ["attributes"] = "HttpGet" });
        InsertNode("fn:helper", "Function", new() { ["name"] = "EnsurePermissionAsync", ["visibility"] = "private" });
        InsertNode("fn:authTarget", "Function", new() { ["name"] = "AuthorizeAsync" });
        InsertNode("mem:authSvc1", "Member", new() { ["name"] = "_authorizationService" });
        InsertNode("mem:authSvc2", "Member", new() { ["name"] = "_helperAuthService" });

        InsertEdge("type:authCtrl", "fn:index", "HAS_METHOD");
        InsertEdge("fn:index", "fn:helper", "CALLS");
        InsertEdge("fn:helper", "fn:authTarget", "CALLS");
        InsertEdge("mem:authSvc1", "fn:index", "ACCESSED_BY", new() { ["operation"] = "AuthorizeAsync" });
        InsertEdge("mem:authSvc2", "fn:helper", "ACCESSED_BY", new() { ["operation"] = "AuthorizeAsync" });

        var cypher = @"
            MATCH (c:Type)-[:HAS_METHOD]->(fn:Function)
            WHERE c.name = 'WorkflowTypeController' AND fn.visibility = 'public'
            OPTIONAL MATCH (fn)-[:CALLS*1..2]->(callee:Function)
            WHERE callee.name = 'AuthorizeAsync'
            OPTIONAL MATCH (m:Member)-[acc:ACCESSED_BY]->(fn)
            WHERE acc.operation CONTAINS 'AuthorizeAsync'
            OPTIONAL MATCH (fn)-[:CALLS]->(h:Function)<-[acc2:ACCESSED_BY]-(m2:Member)
            WHERE acc2.operation CONTAINS 'AuthorizeAsync'
            RETURN c.name AS ctrl,
                   fn.name AS method,
                   collect(DISTINCT callee.name) AS called_fn,
                   collect(DISTINCT acc.operation) AS direct_mem_op,
                   collect(DISTINCT h.name + ':' + acc2.operation) AS hop2_mem_op,
                   count(DISTINCT acc) AS direct_acc_count
        ";

        var rows = ExecuteCypher(cypher);
        rows.Should().HaveCount(1);
        rows[0]["ctrl"].Should().Be("WorkflowTypeController");
        rows[0]["method"].Should().Be("Index");
        rows[0]["called_fn"].Should().Be("[\"AuthorizeAsync\"]");
        rows[0]["direct_mem_op"].Should().Be("[\"AuthorizeAsync\"]");
        rows[0]["hop2_mem_op"].Should().Be("[\"EnsurePermissionAsync:AuthorizeAsync\"]");
        Convert.ToInt64(rows[0]["direct_acc_count"]).Should().Be(1);
    }

    [Test]
    public void Test_ListConcatenationPlusOperator_WithCollectAndAnyQuantifier()
    {
        InsertNode("type:acctBase", "Type", new() { ["name"] = "AccountBaseController", ["extends"] = "Controller", ["project_name"] = "OrchardCore.Users" });
        InsertNode("type:extAuth", "Type", new() { ["name"] = "ExternalAuthenticationsController", ["extends"] = "AccountBaseController", ["project_name"] = "OrchardCore.Users" });
        InsertNode("type:tenantApi", "Type", new() { ["name"] = "TenantApiController", ["extends"] = "ControllerBase", ["attributes"] = "ApiController", ["project_name"] = "OrchardCore.Tenants" });
        InsertNode("type:plainDto", "Type", new() { ["name"] = "LoginViewModel", ["project_name"] = "OrchardCore.Users" });

        InsertEdge("type:extAuth", "type:acctBase", "INHERITS_FROM");

        var rows = ExecuteCypher(@"
            MATCH (t:Type)
            WHERE t.project_name IN ['OrchardCore.Users', 'OrchardCore.Tenants']
            OPTIONAL MATCH (t)-[:INHERITS_FROM*1..5]->(b:Type)
            WITH t, collect(b.extends) + [t.extends] AS all_ext, collect(b.attributes) + [t.attributes] AS all_attr
            WHERE any(x IN all_ext WHERE x CONTAINS 'Controller')
               OR any(x IN all_attr WHERE x CONTAINS 'Controller')
            RETURN t.name AS cls
            ORDER BY cls
        ");

        rows.Select(r => (string)r["cls"]!).Should().Equal(
            "AccountBaseController",
            "ExternalAuthenticationsController",
            "TenantApiController");

        var litRows = ExecuteCypher(@"MATCH (t:Type {name: 'AccountBaseController'}) RETURN ['a', 'b'] + ['c'] AS l1, ['x'] + 'y' AS l2");
        litRows.Should().HaveCount(1);
        litRows[0]["l1"].Should().Be("[\"a\",\"b\",\"c\"]");
        litRows[0]["l2"].Should().Be("[\"x\",\"y\"]");
    }

    [Test]
    public void Test_NonAggregatingWithFollowedByReturnCollect_GroupsByReturnKeysOnly()
    {
        InsertNode("type:ctrl", "Type", new() { ["name"] = "AdminController", ["project_name"] = "ModA" });
        InsertNode("fn:a1", "Function", new() { ["name"] = "CreatePost" });
        InsertNode("fn:a2", "Function", new() { ["name"] = "DeletePost" });
        InsertNode("fn:helper", "Function", new() { ["name"] = "AuthorizeAsync" });

        InsertEdge("type:ctrl", "fn:a1", "HAS_METHOD");
        InsertEdge("type:ctrl", "fn:a2", "HAS_METHOD");
        InsertEdge("fn:a1", "fn:helper", "CALLS");

        var rows = ExecuteCypher(@"
            MATCH (c:Type {project_name: 'ModA'})-[:HAS_METHOD]->(m:Function)
            WITH c, m, EXISTS { MATCH (m)-[:CALLS]->(:Function {name: 'AuthorizeAsync'}) } AS hasAuth
            RETURN c.project_name AS mod, c.name AS cls, collect(m.name + ':' + toString(hasAuth)) AS actions
        ");

        rows.Should().HaveCount(1);
        rows[0]["cls"].Should().Be("AdminController");
        rows[0]["actions"].Should().Be("[\"CreatePost:1\",\"DeletePost:0\"]");
    }

    [Test]
    public void Test_VarLenCte_AppliesMaxPathTraversalsLimit()
    {
        InsertNode("fn:p1", "Function", new() { ["name"] = "Chain1" });
        InsertNode("fn:p2", "Function", new() { ["name"] = "Chain2" });
        InsertNode("fn:p3", "Function", new() { ["name"] = "Chain3" });
        InsertNode("fn:p4", "Function", new() { ["name"] = "Chain4" });
        InsertEdge("fn:p1", "fn:p2", "CALLS");
        InsertEdge("fn:p2", "fn:p3", "CALLS");
        InsertEdge("fn:p3", "fn:p4", "CALLS");

        var cypher = "MATCH (a:Function {name: 'Chain1'})-[:CALLS*1..3]->(b:Function) RETURN b.name AS name";
        var ast = CypherQueryParser.Parse(cypher);

        // Compile with MaxPathTraversals = 2
        var compiled = SqliteCompiler.Compile(ast, maxPathTraversals: 2);
        compiled.Sql.Should().Contain("LIMIT 2");

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = compiled.Sql;
        foreach (var (k, v) in compiled.Parameters)
        {
            cmd.Parameters.AddWithValue("@" + k.TrimStart('@'), v ?? DBNull.Value);
        }
        using var reader = cmd.ExecuteReader();
        var count = 0;
        while (reader.Read()) count++;

        count.Should().Be(2);
    }

    [Test]
    public void Test_VarLenCte_PrefersForwardTraversalWhenStartDeclared()
    {
        InsertNode("fn:s1", "Function", new() { ["name"] = "PublishingAsync" });
        InsertNode("fn:d1", "Function", new() { ["name"] = "DownstreamWorker" });
        InsertEdge("fn:s1", "fn:d1", "CALLS");

        // When start node is declared in previous MATCH, it should not walk backwards from all :Function nodes
        var cypher = @"
            MATCH (m:Function) WHERE m.name = 'PublishingAsync'
            MATCH (m)-[:CALLS*1..2]->(downstream:Function)
            RETURN m.name AS caller, downstream.name AS callee";
        var ast = CypherQueryParser.Parse(cypher);
        var compiled = SqliteCompiler.Compile(ast);

        // It should traverse forward from start_id, not walk backwards from end_id
        compiled.Sql.Should().NotContain("FROM nodes _src_anc CROSS JOIN edges e ON e.to_id = _src_anc.rowid");
        compiled.Sql.Should().Contain("FROM nodes _src_anc CROSS JOIN edges e ON e.from_id = _src_anc.rowid");
    }
}