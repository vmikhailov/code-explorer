using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Java;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class PayloadAndProtocolExtractionTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "codeexplorer_payload_test_" + Guid.NewGuid()).Replace('\\', '/');
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
    public async Task Test_CSharp_AspNetCore_Payload_And_Protocol_Extraction()
    {
        var projDir = Path.Combine(_tempDir, "Services", "RideService").Replace('\\', '/');
        Directory.CreateDirectory(projDir);
        await File.WriteAllTextAsync(Path.Combine(projDir, "RideService.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\"/>");

        var code = @"
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Lightning.Services.Ride;

public record StartRideRequest(string UserId, string PickupLocation);
public record RideDto(string RideId, string Status);

[ApiController]
[Route(""api/[controller]"")]
public class RideController : ControllerBase
{
    [HttpPost(""start"")]
    public async Task<ActionResult<RideDto>> StartRide([FromBody] StartRideRequest request)
    {
        return Ok(new RideDto(""123"", ""Started""));
    }
}";
        await File.WriteAllTextAsync(Path.Combine(projDir, "RideController.cs"), code);

        var dbPath = Path.Combine(_tempDir, "graph.db");
        await using var client = new SqliteGraphClient(dbPath);
        WorkspaceIndexer.Register(new CSharpParser());
        var indexer = new WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempDir, _tempDir, clear: true);

        var result = await client.ExecuteQueryAsync("MATCH (ep:Endpoint) RETURN ep.name AS name, ep.http_method AS method, ep.protocol AS protocol, ep.request_type AS req, ep.response_type AS resp");
        Assert.That(result, Does.Contain("POST"));
        Assert.That(result, Does.Contain("REST"));
        Assert.That(result, Does.Contain("StartRideRequest"));
        Assert.That(result, Does.Contain("RideDto"));
    }

    [Test]
    public async Task Test_CSharp_HotChocolate_GraphQL_Extraction()
    {
        var projDir = Path.Combine(_tempDir, "Gateways", "GraphQlGateway").Replace('\\', '/');
        Directory.CreateDirectory(projDir);
        await File.WriteAllTextAsync(Path.Combine(projDir, "GraphQlGateway.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"/>");

        var code = @"
using HotChocolate;
using HotChocolate.Types;
using System.Threading.Tasks;

namespace Lightning.Gateways.GraphQL;

public record StartRideInput(string UserId, string Pickup);
public record RidePayload(string RideId, string Status);

[ExtendObjectType(""Mutation"")]
public class RideMutations
{
    [Mutation]
    public async Task<RidePayload> StartRideAsync(StartRideInput input)
    {
        return new RidePayload(""123"", ""Started"");
    }
}";
        await File.WriteAllTextAsync(Path.Combine(projDir, "RideMutations.cs"), code);

        var dbPath = Path.Combine(_tempDir, "graph.db");
        await using var client = new SqliteGraphClient(dbPath);
        WorkspaceIndexer.Register(new CSharpParser());
        var indexer = new WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempDir, _tempDir, clear: true);

        var result = await client.ExecuteQueryAsync("MATCH (ep:Endpoint) RETURN ep.name AS name, ep.protocol AS protocol, ep.operation_type AS op, ep.request_type AS req, ep.response_type AS resp");
        Assert.That(result, Does.Contain("GraphQL"));
        Assert.That(result, Does.Contain("Mutation"));
        Assert.That(result, Does.Contain("StartRideInput"));
        Assert.That(result, Does.Contain("RidePayload"));
    }

    [Test]
    public async Task Test_CSharp_Grpc_Extraction()
    {
        var projDir = Path.Combine(_tempDir, "Services", "OrderGrpc").Replace('\\', '/');
        Directory.CreateDirectory(projDir);
        await File.WriteAllTextAsync(Path.Combine(projDir, "OrderGrpc.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"/>");

        var code = @"
using Grpc.Core;
using System.Threading.Tasks;

namespace Lightning.Services.Order;

public class CreateOrderRequest { public string ItemId { get; set; } }
public class OrderResponse { public string OrderId { get; set; } }

public class OrderGrpcService : OrderServiceBase
{
    public override async Task<OrderResponse> CreateOrder(CreateOrderRequest request, ServerCallContext context)
    {
        return new OrderResponse { OrderId = ""order-1"" };
    }
}";
        await File.WriteAllTextAsync(Path.Combine(projDir, "OrderGrpcService.cs"), code);

        var dbPath = Path.Combine(_tempDir, "graph.db");
        await using var client = new SqliteGraphClient(dbPath);
        WorkspaceIndexer.Register(new CSharpParser());
        var indexer = new WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempDir, _tempDir, clear: true);

        var result = await client.ExecuteQueryAsync("MATCH (ep:Endpoint) RETURN ep.name AS name, ep.protocol AS protocol, ep.operation_type AS op, ep.request_type AS req, ep.response_type AS resp");
        Assert.That(result, Does.Contain("gRPC"));
        Assert.That(result, Does.Contain("Unary"));
        Assert.That(result, Does.Contain("CreateOrderRequest"));
        Assert.That(result, Does.Contain("OrderResponse"));
    }

    [Test]
    public async Task Test_Java_Spring_Payload_And_GraphQL_Extraction()
    {
        var projDir = Path.Combine(_tempDir, "RideServiceJava").Replace('\\', '/');
        Directory.CreateDirectory(projDir);
        await File.WriteAllTextAsync(Path.Combine(projDir, "pom.xml"), "<project><modelVersion>4.0.0</modelVersion><artifactId>ride-service</artifactId></project>");

        var codeRest = @"
package com.lightning.ride;
import org.springframework.web.bind.annotation.*;
import org.springframework.http.ResponseEntity;

@RestController
@RequestMapping(""/api/rides"")
public class RideController {
    @PostMapping(""/start"")
    public ResponseEntity<RideDto> startRide(@RequestBody StartRideRequest request) {
        return ResponseEntity.ok(new RideDto());
    }
}";
        await File.WriteAllTextAsync(Path.Combine(projDir, "RideController.java"), codeRest);

        var codeGraphQl = @"
package com.lightning.ride;
import org.springframework.graphql.data.method.annotation.*;

@Controller
public class RideGraphQlController {
    @MutationMapping
    public RideDto startRideMutation(@Argument StartRideRequest request) {
        return new RideDto();
    }
}";
        await File.WriteAllTextAsync(Path.Combine(projDir, "RideGraphQlController.java"), codeGraphQl);

        var dbPath = Path.Combine(_tempDir, "graph.db");
        await using var client = new SqliteGraphClient(dbPath);
        WorkspaceIndexer.Register(new JavaParser());
        var indexer = new WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempDir, _tempDir, clear: true);

        var result = await client.ExecuteQueryAsync("MATCH (ep:Endpoint) RETURN ep.name AS name, ep.protocol AS protocol, ep.request_type AS req, ep.response_type AS resp");
        Assert.That(result, Does.Contain("REST"));
        Assert.That(result, Does.Contain("GraphQL"));
        Assert.That(result, Does.Contain("StartRideRequest"));
        Assert.That(result, Does.Contain("RideDto"));
    }

    [Test]
    public async Task Test_TypeScript_NestJs_Payload_GraphQL_And_Grpc_Extraction()
    {
        var projDir = Path.Combine(_tempDir, "NestGateway").Replace('\\', '/');
        Directory.CreateDirectory(projDir);
        await File.WriteAllTextAsync(Path.Combine(projDir, "package.json"), "{\"name\":\"nest-gateway\"}");

        var code = @"
import { Controller, Post, Body } from '@nestjs/common';
import { Resolver, Mutation } from '@nestjs/graphql';
import { GrpcMethod } from '@nestjs/microservices';

class StartRideDto { userId: string; }
class RideResultDto { rideId: string; }

@Controller('rides')
export class RidesController {
    @Post('start')
    async startRide(@Body() dto: StartRideDto): Promise<RideResultDto> {
        return { rideId: '123' };
    }
}

@Resolver('Ride')
export class RidesResolver {
    @Mutation()
    async createRide(@Body() dto: StartRideDto): Promise<RideResultDto> {
        return { rideId: '123' };
    }
}

export class RidesGrpcService {
    @GrpcMethod('RidesService', 'StartRide')
    startRide(dto: StartRideDto): RideResultDto {
        return { rideId: '123' };
    }
}
";
        await File.WriteAllTextAsync(Path.Combine(projDir, "rides.controller.ts"), code);

        var dbPath = Path.Combine(_tempDir, "graph.db");
        await using var client = new SqliteGraphClient(dbPath);
        WorkspaceIndexer.Register(new TypeScriptParser());
        var indexer = new WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempDir, _tempDir, clear: true);

        var result = await client.ExecuteQueryAsync("MATCH (ep:Endpoint) RETURN ep.name AS name, ep.protocol AS protocol, ep.request_type AS req, ep.response_type AS resp ORDER BY ep.protocol");
        Assert.That(result, Does.Contain("REST"));
        Assert.That(result, Does.Contain("GraphQL"));
        Assert.That(result, Does.Contain("gRPC"));
        Assert.That(result, Does.Contain("StartRideDto"));
        Assert.That(result, Does.Contain("RideResultDto"));
    }
}
