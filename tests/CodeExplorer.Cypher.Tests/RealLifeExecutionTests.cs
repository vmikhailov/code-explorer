using System.Text.Json;
using CodeExplorer.Cypher.Compiler;
using CodeExplorer.Cypher.Parser;
using CodeExplorer.Cypher.Tests.Shared;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace CodeExplorer.Cypher.Tests;

[TestFixture]
public class RealLifeExecutionTests
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

        SeedRealisticGraph();
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

    private void InsertEdge(string fromId, string toId, string kind, Dictionary<string, object?>? props = null)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO edges (from_id, to_id, kind, properties) VALUES (@from, @to, @kind, @props)";
        cmd.Parameters.AddWithValue("@from", fromId);
        cmd.Parameters.AddWithValue("@to", toId);
        cmd.Parameters.AddWithValue("@kind", kind);
        cmd.Parameters.AddWithValue("@props", JsonSerializer.Serialize(props ?? new()));
        cmd.ExecuteNonQuery();
    }

    private void SeedRealisticGraph()
    {
        // 1. Workspace
        InsertNode("ws:1", "Workspace", new()
        {
            ["name"] = "ShopApp",
            ["path"] = "c:/work/shop"
        });

        // 2. Projects
        InsertNode("ws:1:proj:orders", "Project", new()
        {
            ["name"] = "OrdersService",
            ["project_type"] = "csharp"
        });
        InsertNode("ws:1:proj:payments", "Project", new()
        {
            ["name"] = "PaymentsService",
            ["project_type"] = "typescript"
        });

        // Project dependency: OrdersService -> PaymentsService
        InsertEdge("ws:1:proj:orders", "ws:1:proj:payments", "DEPENDS_ON");

        // 3. Folders
        InsertNode("ws:1:folder:orders_root", "Folder", new() { ["name"] = "OrdersService" });
        InsertNode("ws:1:folder:ctrl", "Folder", new() { ["name"] = "Controllers" });
        InsertNode("ws:1:folder:svc", "Folder", new() { ["name"] = "Services" });
        InsertNode("ws:1:folder:repo", "Folder", new() { ["name"] = "Repositories" });

        InsertEdge("ws:1:proj:orders", "ws:1:folder:orders_root", "LOCATED_IN");
        InsertEdge("ws:1:folder:orders_root", "ws:1:folder:ctrl", "CONTAINS");
        InsertEdge("ws:1:folder:orders_root", "ws:1:folder:svc", "CONTAINS");
        InsertEdge("ws:1:folder:orders_root", "ws:1:folder:repo", "CONTAINS");

        // 4. Files
        InsertNode("ws:1:file:orders_ctrl", "File", new()
        {
            ["name"] = "OrdersController.cs",
            ["path"] = "Controllers/OrdersController.cs",
            ["file_path"] = "Controllers/OrdersController.cs"
        });
        InsertNode("ws:1:file:order_svc", "File", new()
        {
            ["name"] = "OrderService.cs",
            ["path"] = "Services/OrderService.cs",
            ["file_path"] = "Services/OrderService.cs"
        });
        InsertNode("ws:1:file:order_repo", "File", new()
        {
            ["name"] = "OrderRepository.cs",
            ["path"] = "Repositories/OrderRepository.cs",
            ["file_path"] = "Repositories/OrderRepository.cs"
        });

        InsertEdge("ws:1", "ws:1:file:orders_ctrl", "CONTAINS");
        InsertEdge("ws:1", "ws:1:file:order_svc", "CONTAINS");
        InsertEdge("ws:1", "ws:1:file:order_repo", "CONTAINS");

        InsertEdge("ws:1:folder:ctrl", "ws:1:file:orders_ctrl", "CONTAINS");
        InsertEdge("ws:1:folder:svc", "ws:1:file:order_svc", "CONTAINS");
        InsertEdge("ws:1:folder:repo", "ws:1:file:order_repo", "CONTAINS");

        InsertEdge("ws:1:proj:orders", "ws:1:file:orders_ctrl", "CONTAINS");
        InsertEdge("ws:1:proj:orders", "ws:1:file:order_svc", "CONTAINS");
        InsertEdge("ws:1:proj:orders", "ws:1:file:order_repo", "CONTAINS");

        // 5. Database, External Service, Endpoints
        InsertNode("ws:1:db:postgres", "Database", new()
        {
            ["name"] = "PostgreSQL",
            ["db_type"] = "relational"
        });
        InsertEdge("ws:1:file:order_repo", "ws:1:db:postgres", "USES_DB");

        InsertNode("ws:1:es:stripe", "ExternalService", new()
        {
            ["name"] = "api.stripe.com",
            ["file_path"] = "Controllers/OrdersController.cs"
        });

        InsertNode("ws:1:ep:create_order", "Endpoint", new()
        {
            ["name"] = "POST /api/orders",
            ["path"] = "Controllers/OrdersController.cs",
            ["http_method"] = "POST"
        });
        InsertNode("ws:1:ep:queue_consumer", "EntryPoint", new()
        {
            ["name"] = "orders-queue-consumer",
            ["path"] = "Controllers/OrdersController.cs"
        });

        // 6. Types (Classes & Interfaces)
        InsertNode("ws:1:type:orders_ctrl", "Type", new()
        {
            ["name"] = "OrdersController",
            ["kind"] = "class",
            ["symbol"] = "Shop.OrdersController",
            ["start_line"] = 10,
            ["end_line"] = 50
        });
        InsertNode("ws:1:type:order_svc", "Type", new()
        {
            ["name"] = "OrderService",
            ["kind"] = "class",
            ["symbol"] = "Shop.OrderService",
            ["start_line"] = 5,
            ["end_line"] = 80
        });
        InsertNode("ws:1:type:i_order_repo", "Type", new()
        {
            ["name"] = "IOrderRepository",
            ["kind"] = "interface",
            ["symbol"] = "Shop.IOrderRepository",
            ["start_line"] = 5,
            ["end_line"] = 20
        });
        InsertNode("ws:1:type:order_repo", "Type", new()
        {
            ["name"] = "OrderRepository",
            ["kind"] = "class",
            ["symbol"] = "Shop.OrderRepository",
            ["start_line"] = 22,
            ["end_line"] = 90
        });

        InsertEdge("ws:1:file:orders_ctrl", "ws:1:type:orders_ctrl", "DEFINES");
        InsertEdge("ws:1:type:orders_ctrl", "ws:1:file:orders_ctrl", "DECLARED_IN");

        InsertEdge("ws:1:file:order_svc", "ws:1:type:order_svc", "DEFINES");
        InsertEdge("ws:1:type:order_svc", "ws:1:file:order_svc", "DECLARED_IN");

        InsertEdge("ws:1:file:order_repo", "ws:1:type:i_order_repo", "DEFINES");
        InsertEdge("ws:1:type:i_order_repo", "ws:1:file:order_repo", "DECLARED_IN");

        InsertEdge("ws:1:file:order_repo", "ws:1:type:order_repo", "DEFINES");
        InsertEdge("ws:1:type:order_repo", "ws:1:file:order_repo", "DECLARED_IN");

        // Interface implementation
        InsertEdge("ws:1:type:order_repo", "ws:1:type:i_order_repo", "IMPLEMENTS");

        // 7. Functions & Call Chain
        // OrdersController.CreateOrder -> OrderService.ProcessPayment -> OrderRepository.SaveOrder
        InsertNode("ws:1:fn:create_order", "Function", new()
        {
            ["name"] = "CreateOrder",
            ["symbol"] = "Shop.OrdersController.CreateOrder",
            ["file_path"] = "Controllers/OrdersController.cs",
            ["start_line"] = 15,
            ["end_line"] = 30
        });
        InsertEdge("ws:1:type:orders_ctrl", "ws:1:fn:create_order", "DECLARES");
        InsertEdge("ws:1:type:orders_ctrl", "ws:1:fn:create_order", "HAS_METHOD");
        InsertEdge("ws:1:fn:create_order", "ws:1:file:orders_ctrl", "DECLARED_IN");

        InsertNode("ws:1:fn:process_pmt", "Function", new()
        {
            ["name"] = "ProcessPayment",
            ["symbol"] = "Shop.OrderService.ProcessPayment",
            ["file_path"] = "Services/OrderService.cs",
            ["start_line"] = 20,
            ["end_line"] = 45
        });
        InsertEdge("ws:1:type:order_svc", "ws:1:fn:process_pmt", "DECLARES");
        InsertEdge("ws:1:type:order_svc", "ws:1:fn:process_pmt", "HAS_METHOD");
        InsertEdge("ws:1:fn:process_pmt", "ws:1:file:order_svc", "DECLARED_IN");

        InsertNode("ws:1:fn:save_order", "Function", new()
        {
            ["name"] = "SaveOrder",
            ["symbol"] = "Shop.OrderRepository.SaveOrder",
            ["file_path"] = "Repositories/OrderRepository.cs",
            ["start_line"] = 25,
            ["end_line"] = 40
        });
        InsertEdge("ws:1:type:order_repo", "ws:1:fn:save_order", "DECLARES");
        InsertEdge("ws:1:type:order_repo", "ws:1:fn:save_order", "HAS_METHOD");
        InsertEdge("ws:1:fn:save_order", "ws:1:file:order_repo", "DECLARED_IN");

        // CALLS edges
        InsertEdge("ws:1:fn:create_order", "ws:1:fn:process_pmt", "CALLS");
        InsertEdge("ws:1:fn:process_pmt", "ws:1:fn:save_order", "CALLS");

        // 8. Database Table & Query Lineage
        InsertNode("ws:1:table:orders", "Table", new()
        {
            ["name"] = "orders"
        });
        InsertNode("ws:1:query:insert_orders", "Query", new()
        {
            ["name"] = "INSERT orders",
            ["query_text"] = "INSERT INTO orders (id, amount) VALUES (@id, @amount)",
            ["path"] = "Repositories/OrderRepository.cs",
            ["start_line"] = 32,
            ["end_line"] = 35
        });
        InsertEdge("ws:1:query:insert_orders", "ws:1:table:orders", "DEPENDS_ON");
        InsertEdge("ws:1:fn:save_order", "ws:1:query:insert_orders", "DEFINES");

        // 9. Dead Code Item (Function with no callers)
        InsertNode("ws:1:fn:unused_helper", "Function", new()
        {
            ["name"] = "UnusedLegacyHelper",
            ["symbol"] = "Shop.OrdersController.UnusedLegacyHelper",
            ["file_path"] = "Controllers/OrdersController.cs",
            ["start_line"] = 42,
            ["end_line"] = 48
        });
        InsertEdge("ws:1:type:orders_ctrl", "ws:1:fn:unused_helper", "DECLARES");
        InsertEdge("ws:1:type:orders_ctrl", "ws:1:fn:unused_helper", "HAS_METHOD");
        InsertEdge("ws:1:fn:unused_helper", "ws:1:file:orders_ctrl", "DECLARED_IN");

        // 10. God Object (Type with > 15 members)
        InsertNode("ws:1:type:god_class", "Type", new()
        {
            ["name"] = "MonolithicGodClass",
            ["kind"] = "class",
            ["symbol"] = "Shop.MonolithicGodClass",
            ["start_line"] = 1,
            ["end_line"] = 500
        });
        InsertEdge("ws:1:file:orders_ctrl", "ws:1:type:god_class", "DEFINES");
        InsertEdge("ws:1:type:god_class", "ws:1:file:orders_ctrl", "DECLARED_IN");

        for (int i = 1; i <= 18; i++)
        {
            var memberId = $"ws:1:member:god_m_{i}";
            InsertNode(memberId, "Function", new()
            {
                ["name"] = $"Method_{i}",
                ["symbol"] = $"Shop.MonolithicGodClass.Method_{i}"
            });
            InsertEdge("ws:1:type:god_class", memberId, "HAS_METHOD");
        }
    }

    private static string LoadRealLifeQuery(string fileName)
    {
        var baseDir = TestContext.CurrentContext.TestDirectory;
        var direct = Path.Combine(baseDir, "TestData", "Queries", "RealLifeQueries", fileName);
        if (File.Exists(direct)) return File.ReadAllText(direct);

        var curr = new DirectoryInfo(baseDir);
        while (curr != null && !File.Exists(Path.Combine(curr.FullName, "CodeExplorer.slnx")))
        {
            curr = curr.Parent;
        }

        if (curr != null)
        {
            var inSource = Path.Combine(curr.FullName, "tests", "CodeExplorer.Cypher.Tests", "TestData", "Queries", "RealLifeQueries", fileName);
            if (File.Exists(inSource)) return File.ReadAllText(inSource);
        }

        throw new FileNotFoundException($"Could not locate test query fixture: '{fileName}'");
    }

    private List<Dictionary<string, object?>> ExecuteQuery(string queryFileName, Dictionary<string, object?>? parameters = null)
    {
        var rawText = LoadRealLifeQuery(queryFileName);
        var ast = CypherQueryParser.Parse(rawText);
        var compiled = SqliteCompiler.Compile(ast, parameters);

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = compiled.Sql;
        foreach (var (k, v) in compiled.Parameters)
        {
            var paramName = "@" + k.TrimStart('@');
            cmd.Parameters.AddWithValue(paramName, v ?? DBNull.Value);
        }

        // Add dummy values for any query-level parameters not yet bound
        var matches = System.Text.RegularExpressions.Regex.Matches(compiled.Sql, @"@[a-zA-Z0-9_]+");
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var pName = match.Value;
            if (!cmd.Parameters.Contains(pName))
            {
                object val = pName.Contains("skip", StringComparison.OrdinalIgnoreCase) ||
                             pName.Contains("limit", StringComparison.OrdinalIgnoreCase)
                    ? 10
                    : "dummy_val";
                cmd.Parameters.AddWithValue(pName, val);
            }
        }

        var results = new List<Dictionary<string, object?>>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }
        return results;
    }

    [Test]
    public void Test_01_GetWorkspaces_ReturnsSeededWorkspace()
    {
        var rows = ExecuteQuery("get_all_workspaces.cypher");

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["id"], Is.EqualTo("ws:1"));
        Assert.That(rows[0]["path"], Is.EqualTo("c:/work/shop"));
    }

    [Test]
    public void Test_02_GetArchitectureMap_ReturnsProjectsIngressEgressAndDbs()
    {
        var rows = ExecuteQuery("get_architecture_map_project.cypher", new()
        {
            ["projectName"] = "OrdersService",
            ["wsIdPrefix"] = "ws:1:"
        });

        Assert.That(rows, Has.Count.EqualTo(1));
        var row = rows[0];

        Assert.That(row["project"], Is.EqualTo("OrdersService"));
        Assert.That(row["language"], Is.EqualTo("csharp"));

        var folders = row["folders"]?.ToString() ?? "";
        Assert.That(folders, Contains.Substring("Controllers"));
        Assert.That(folders, Contains.Substring("Repositories"));

        var deps = row["dependencies"]?.ToString() ?? "";
        Assert.That(deps, Contains.Substring("PaymentsService"));

        var dbs = row["databases"]?.ToString() ?? "";
        Assert.That(dbs, Contains.Substring("PostgreSQL"));

        var ingress = row["ingress"]?.ToString() ?? "";
        Assert.That(ingress, Contains.Substring("POST /api/orders"));
        Assert.That(ingress, Contains.Substring("orders-queue-consumer"));

        var egress = row["egress"]?.ToString() ?? "";
        Assert.That(egress, Contains.Substring("api.stripe.com"));
    }

    [Test]
    public void Test_03_GetProjectDependencies_IdentifiesCrossProjectLinks()
    {
        var rows = ExecuteQuery("get_project_dependencies_all.cypher", new()
        {
            ["wsIdPrefix"] = "ws:1:"
        });

        Assert.That(rows, Has.Count.GreaterThanOrEqualTo(1));
        var dep = rows.Find(r => (string?)r["project"] == "OrdersService");
        Assert.That(dep, Is.Not.Null);
        Assert.That(dep!["dependency"], Is.EqualTo("PaymentsService"));
        Assert.That(dep["dependencyType"], Is.EqualTo("Project"));
    }

    [Test]
    public void Test_04_GetFileOutline_ReturnsTypesMethodsAndSymbols()
    {
        var rows = ExecuteQuery("get_file_outline.cypher", new()
        {
            ["filePath"] = "Controllers/OrdersController.cs",
            ["wsIdPrefix"] = "ws:1:"
        });

        Assert.That(rows, Has.Count.GreaterThanOrEqualTo(2));

        var classItem = rows.Find(r => (string?)r["name"] == "OrdersController");
        Assert.That(classItem, Is.Not.Null);
        Assert.That(classItem!["type"], Is.EqualTo("Class"));
        Assert.That(classItem["symbol"], Is.EqualTo("Shop.OrdersController"));

        var methodItem = rows.Find(r => (string?)r["name"] == "CreateOrder");
        Assert.That(methodItem, Is.Not.Null);
        Assert.That(methodItem!["type"], Is.EqualTo("Function"));
        Assert.That(methodItem["symbol"], Is.EqualTo("Shop.OrdersController.CreateOrder"));
    }

    [Test]
    public void Test_05_FindSymbol_LocatesTargetClassesAndFunctions()
    {
        var rows = ExecuteQuery("find_symbol_function.cypher", new()
        {
            ["name"] = "Order",
            ["wsIdPrefix"] = "ws:1:"
        });

        Assert.That(rows, Has.Count.GreaterThanOrEqualTo(2));
        var names = rows.ConvertAll(r => (string?)r["name"]);
        Assert.That(names, Contains.Item("CreateOrder"));
        Assert.That(names, Contains.Item("SaveOrder"));
    }

    [Test]
    public void Test_06_GetCallChain_TraversesMultiHopCalls()
    {
        var rows = ExecuteQuery("get_call_chain.cypher", new()
        {
            ["startFunction"] = "Shop.OrdersController.CreateOrder",
            ["endFunction"] = "Shop.OrderRepository.SaveOrder",
            ["wsIdPrefix"] = "ws:1:"
        });

        Assert.That(rows, Has.Count.EqualTo(1));
        var chain = rows[0]["chain"]?.ToString() ?? "";
        Assert.That(chain, Contains.Substring("ws:1:fn:create_order"));
        Assert.That(chain, Contains.Substring("ws:1:fn:process_pmt"));
        Assert.That(chain, Contains.Substring("ws:1:fn:save_order"));
    }

    [Test]
    public void Test_07_ResolveCallTarget_FindsCalledFunction()
    {
        var rows = ExecuteQuery("resolve_call_target.cypher", new()
        {
            ["interfaceName"] = "IOrderRepository",
            ["methodName"] = "SaveOrder",
            ["wsIdPrefix"] = "ws:1:"
        });

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["className"], Is.EqualTo("OrderRepository"));
        Assert.That(rows[0]["methodName"], Is.EqualTo("SaveOrder"));
        Assert.That(rows[0]["methodSymbol"], Is.EqualTo("Shop.OrderRepository.SaveOrder"));
    }

    [Test]
    public void Test_08_AnalyzeCodeImpact_BlastRadiusTraversal()
    {
        var rows = ExecuteQuery("analyze_code_impact.cypher", new()
        {
            ["symbolName"] = "Shop.OrderRepository.SaveOrder",
            ["wsIdPrefix"] = "ws:1:"
        });

        Assert.That(rows, Has.Count.GreaterThanOrEqualTo(1));
        var dependent = rows.Find(r => (string?)r["dependentName"] == "ProcessPayment");
        Assert.That(dependent, Is.Not.Null);
        Assert.That(dependent!["dependentType"], Is.EqualTo("Function"));
    }

    [Test]
    public void Test_09_InspectDataLineage_TracksTableAccess()
    {
        var rows = ExecuteQuery("inspect_data_lineage.cypher", new()
        {
            ["tableName"] = "orders",
            ["wsIdPrefix"] = "ws:1:"
        });

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["tableName"], Is.EqualTo("orders"));
        Assert.That(rows[0]["queryName"], Is.EqualTo("INSERT orders"));

        var parent = rows[0]["parentName"]?.ToString() ?? "";
        Assert.That(parent, Contains.Substring("SaveOrder"));

        var callers = rows[0]["callingSymbols"]?.ToString() ?? "";
        Assert.That(callers.Contains("CreateOrder") || callers.Contains("ProcessPayment"), Is.True);
    }

    [Test]
    public void Test_10_FindRefactorOpportunities_DeadCodeAndGodObjects()
    {
        // 1. Dead Code detection
        var deadCodeRows = ExecuteQuery("find_refactor_dead_code.cypher", new()
        {
            ["projectName"] = "OrdersService",
            ["wsIdPrefix"] = "ws:1:"
        });

        Assert.That(deadCodeRows, Has.Count.GreaterThanOrEqualTo(1));
        var deadFunc = deadCodeRows.Find(r => (string?)r["name"] == "UnusedLegacyHelper");
        Assert.That(deadFunc, Is.Not.Null);
        Assert.That(deadFunc!["anomalyType"], Is.EqualTo("dead_code"));

        // 2. God Object detection (> 15 members)
        var godObjectRows = ExecuteQuery("find_refactor_god_objects.cypher", new()
        {
            ["projectName"] = "OrdersService",
            ["wsIdPrefix"] = "ws:1:"
        });

        Assert.That(godObjectRows, Has.Count.GreaterThanOrEqualTo(1));
        var godClass = godObjectRows.Find(r => (string?)r["name"] == "MonolithicGodClass");
        Assert.That(godClass, Is.Not.Null);
        Assert.That(godClass!["anomalyType"], Is.EqualTo("god_object"));
        Assert.That(Convert.ToInt64(godClass["metricValue"]), Is.GreaterThan(15));
    }

    [Test]
    public void Test_MapLiteral_WithCollectedList_SerializesAsJsonArrayNotEscapedString()
    {
        var cypher = """
            MATCH (n:Type)
            WITH collect(n.name) AS typeNames
            RETURN { types: typeNames } AS result
            """;
        var ast = CypherQueryParser.Parse(cypher);
        var compiled = SqliteCompiler.Compile(ast);
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = compiled.Sql;
        using var reader = cmd.ExecuteReader();
        Assert.That(reader.Read(), Is.True);
        var rawJson = reader.GetString(0);
        using var doc = JsonDocument.Parse(rawJson);
        var typesProp = doc.RootElement.GetProperty("types");
        Assert.That(typesProp.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(typesProp.GetArrayLength(), Is.GreaterThan(0));
    }

    [Test]
    public void Test_GetArchitectureMap_DatabasesIsJsonArray()
    {
        InsertNode("ws:1:ps", "ProjectsStructure", new() { ["name"] = "Projects" });
        InsertEdge("ws:1", "ws:1:ps", "CONTAINS");
        InsertEdge("ws:1:proj:orders", "ws:1:ps", "LOCATED_IN");
        InsertNode("ws:1:proj:orders:db:sql", "Database", new() { ["name"] = "OrdersDb" });

        var rows = ExecuteQuery("get_architecture_map_workspace.cypher", new()
        {
            ["workspaceId"] = "ws:1"
        });

        Assert.That(rows, Has.Count.EqualTo(1));
        var projectsRaw = rows[0]["projects"]?.ToString() ?? "";
        using var doc = JsonDocument.Parse(projectsRaw);
        var proj = doc.RootElement[0];
        var dbs = proj.GetProperty("databases");
        Assert.That(dbs.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(dbs.GetArrayLength(), Is.GreaterThanOrEqualTo(1));
    }
}
