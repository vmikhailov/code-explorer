using System.Text.Json;
using NUnit.Framework;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.CSharp;

namespace CodeExplorer.Tests;

[TestFixture]
[Category("Integration")]
public class Epic2ParserIntelligenceTests
{
    private string _tempWorkspace = null!;
    private string _dbPath = null!;
    private SqliteGraphClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "codeexplorer_epic2_test_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(_tempWorkspace);

        var projDir = Path.Combine(_tempWorkspace, "Epic2Service").Replace('\\', '/');
        Directory.CreateDirectory(projDir);
        await File.WriteAllTextAsync(Path.Combine(projDir, "Epic2Service.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");

        // 1. Controller with route tokens & inheritance (Epic 2.1) + Constructor DI (Epic 2.2)
        var controllerCode = @"
using Microsoft.AspNetCore.Mvc;

namespace Epic2Service.Controllers;

[Route(""api/[controller]"")]
public abstract class BaseApiController : ControllerBase
{
}

public class OrdersController : BaseApiController
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet(""{id}"")]
    public async Task<IActionResult> GetOrder(int id)
    {
        return Ok();
    }

    [HttpPost(""[action]"")]
    public async Task<IActionResult> CreateOrder()
    {
        await _orderService.ProcessOrderAsync();
        return Ok();
    }
}

public interface IOrderService
{
    Task ProcessOrderAsync();
}

public class OrderService : IOrderService
{
    public async Task ProcessOrderAsync()
    {
    }
}
";
        await File.WriteAllTextAsync(Path.Combine(projDir, "OrdersController.cs"), controllerCode);

        // 2. Minimal API with MapGroup (Epic 2.1)
        var programCode = @"
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var v1 = app.MapGroup(""/api/v1"");
v1.MapGet(""/products"", () => ""products"");

app.MapGroup(""/api/v2"").MapPost(""/checkout"", () => ""checkout"");

app.Run();
";
        await File.WriteAllTextAsync(Path.Combine(projDir, "Program.cs"), programCode);

        // 3. Declarative HTTP Client with BasePath & AstValueResolver (Epic 2.3)
        var clientCode = @"
using RestEase;

namespace Epic2Service.Clients;

public static class ClientEndpoints
{
    public const string UserDetails = ""users/{id}/details"";
}

[BasePath(""api/v3"")]
public interface IUserApiClient
{
    [Get(ClientEndpoints.UserDetails)]
    Task<string> GetUserDetailsAsync(string id);
}
";
        await File.WriteAllTextAsync(Path.Combine(projDir, "IUserApiClient.cs"), clientCode);

        // 4. Dapper with constants & variables for raw SQL lineage (Epic 2.4)
        var repoCode = @"
using Dapper;
using System.Data;

namespace Epic2Service.Repositories;

public static class OrderQueries
{
    public const string SelectInventory = ""SELECT * FROM warehouse_inventory WHERE product_id = @Id"";
}

public class InventoryRepository
{
    private readonly IDbConnection _db;

    public InventoryRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task CheckStockAsync(int id)
    {
        var sql = OrderQueries.SelectInventory;
        await _db.QueryAsync(sql, new { Id = id });
    }
}
";
        await File.WriteAllTextAsync(Path.Combine(projDir, "InventoryRepository.cs"), repoCode);

        _dbPath = Path.Combine(_tempWorkspace, "epic2_test.db");
        _client = new SqliteGraphClient(_dbPath);

        WorkspaceIndexer.Register(new CSharpParser());

        var indexer = new WorkspaceIndexer(_client);
        await indexer.IndexAsync(_tempWorkspace, _tempWorkspace, clear: true);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _client.DisposeAsync();
        if (Directory.Exists(_tempWorkspace))
        {
            try { Directory.Delete(_tempWorkspace, recursive: true); } catch { }
        }
    }

    private async Task<List<Dictionary<string, string>>> QueryAsync(string cypher)
    {
        var json = await _client.ExecuteQueryAsync(cypher);

        using (var doc = JsonDocument.Parse(json))
        {
            var result = new List<Dictionary<string, string>>();

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var prop in item.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.ToString();
                }

                result.Add(dict);
            }

            return result;
        }
    }

    [Test]
    public async Task Epic2_1_ControllerRouteComposition_And_MapGroup_AreExtracted()
    {
        // Check Controller endpoints
        var getOrder = await QueryAsync(
            "MATCH (e:Endpoint) WHERE toLower(e.route_template) = '/api/orders/{id}' RETURN e.http_method AS method, e.route_template AS route");
        Assert.That(getOrder, Has.Count.EqualTo(1));
        Assert.That(getOrder[0]["method"], Is.EqualTo("GET"));

        var createOrder = await QueryAsync(
            "MATCH (e:Endpoint) WHERE toLower(e.route_template) = '/api/orders/createorder' RETURN e.http_method AS method, e.route_template AS route");
        Assert.That(createOrder, Has.Count.EqualTo(1));
        Assert.That(createOrder[0]["method"], Is.EqualTo("POST"));

        // Check Minimal API MapGroup endpoints
        var v1Products = await QueryAsync(
            "MATCH (e:Endpoint) WHERE e.route_template = '/api/v1/products' RETURN e.http_method AS method, e.route_template AS route");
        Assert.That(v1Products, Has.Count.EqualTo(1));

        var v2Checkout = await QueryAsync(
            "MATCH (e:Endpoint) WHERE e.route_template = '/api/v2/checkout' RETURN e.http_method AS method, e.route_template AS route");
        Assert.That(v2Checkout, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Epic2_2_ConstructorDI_ResolvesInterfaceCallsToConcreteImplementation()
    {
        // In OrdersController.CreateOrder(), _orderService.ProcessOrderAsync() is called.
        // It must link CreateOrder to OrderService.ProcessOrderAsync via CALLS!
        var calls = await QueryAsync(@"
            MATCH (caller:Function {name: 'CreateOrder'})-[:CALLS]->(callee:Function {name: 'ProcessOrderAsync'})
            MATCH (parent:Type)-[:HAS_METHOD]->(callee)
            RETURN caller.name AS callerName, callee.id AS calleeId, parent.name AS parentName");

        Assert.That(calls, Has.Count.GreaterThanOrEqualTo(1), "Expected CreateOrder to call ProcessOrderAsync");
        var hasConcreteOrderService = calls.Any(r => r["parentName"] == "OrderService");
        Assert.That(hasConcreteOrderService, Is.True, "Call should resolve through interface to concrete OrderService implementation");
    }

    [Test]
    public async Task Epic2_3_DeclarativeClient_WithBasePath_And_Constant_IsExtracted()
    {
        // RestEase/Refit interface IUserApiClient with [BasePath("api/v3")] and [Get(ClientEndpoints.UserDetails)]
        // Expected ExternalService route: http:user/api/v3/users/{id}/details
        var services = await QueryAsync(@"
            MATCH (es:ExternalService)
            RETURN es.name AS name, es.id AS id, es.path AS path, es.domain_or_service AS domain_or_service");

        foreach (var s in services) TestContext.WriteLine($"Service: {s["name"]} -> {s["id"]} (path: {s["path"]})");
        foreach (var kvp in ConstantRegistry.GlobalConstants) TestContext.WriteLine($"GlobalConst: {kvp.Key} = {kvp.Value}");

        Assert.That(services, Has.Count.GreaterThanOrEqualTo(1));
        var match = services.FirstOrDefault(r => r["path"] == "/api/v3/users/{id}/details");
        Assert.That(match, Is.Not.Null, "Expected ExternalService with composed BasePath and endpoint");
        Assert.That(match!["domain_or_service"], Is.EqualTo("user"));
    }

    [Test]
    public async Task Epic2_4_DapperRawSql_WithConstantsAndVariables_CreatesTableDependency()
    {
        // InventoryRepository.CheckStockAsync() runs Dapper query referencing OrderQueries.SelectInventory ("warehouse_inventory")
        var lineage = await QueryAsync(@"
            MATCH (t:Table {name: 'warehouse_inventory'})
            OPTIONAL MATCH (t)-[:QUERIED_BY]->(func:Function)
            RETURN t.name AS tableName, func.name AS functionName");

        Assert.That(lineage, Has.Count.GreaterThanOrEqualTo(1), "Expected Table 'warehouse_inventory' to be in the graph");
        var hasQueriedBy = lineage.Any(r => r["functionName"] == "CheckStockAsync");
        Assert.That(hasQueriedBy, Is.True, "Expected CheckStockAsync to be linked to warehouse_inventory table");
    }
}
