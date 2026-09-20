using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.CSharp;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class GatewayTracingTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "codeexplorer_gateway_test_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Test]
    public async Task Test_Gateway_To_Downstream_Ingress_Egress_Tracing()
    {
        // 1. Gateway Project
        var gwDir = Path.Combine(_tempDir, "Gateways", "WebAppGateway").Replace('\\', '/');
        Directory.CreateDirectory(gwDir);
        await File.WriteAllTextAsync(Path.Combine(gwDir, "WebAppGateway.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\"/>");

        var gwCode = @"
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;

namespace Lightning.Gateways.WebAppGateway;

public record StartRideRequest(string UserId);
public record RideDto(string Id);

[ApiController]
[Route(""api/[controller]"")]
public class RideGatewayController : ControllerBase
{
    private readonly HttpClient _httpClient;
    public RideGatewayController(HttpClient httpClient) => _httpClient = httpClient;

    [HttpPost(""start"")]
    public async Task<ActionResult<RideDto>> StartRide([FromBody] StartRideRequest request)
    {
        var response = await _httpClient.GetAsync(""http://rideservice/api/rides/start"");
        return Ok(new RideDto(""123""));
    }
}";
        await File.WriteAllTextAsync(Path.Combine(gwDir, "RideGatewayController.cs"), gwCode);

        // 2. Index workspace
        var dbPath = Path.Combine(_tempDir, "graph.db");
        await using var client = new SqliteGraphClient(dbPath);
        WorkspaceIndexer.Register(new CSharpParser());
        var indexer = new WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempDir, _tempDir, clear: true);

        // 3. Execute trace_ingress_to_egress query
        var cypher = Queries.Get("trace_ingress_to_egress");
        var result = await client.ExecuteQueryAsync(cypher);

        Assert.That(result, Does.Contain("WebAppGateway"));
        Assert.That(result, Does.Contain("POST"));
        Assert.That(result, Does.Contain("StartRide"));
        Assert.That(result, Does.Contain("StartRideRequest"));
        Assert.That(result, Does.Contain("RideDto"));
    }
}
