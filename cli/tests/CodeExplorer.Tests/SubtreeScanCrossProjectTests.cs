using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[Category("Integration")]
public class SubtreeScanCrossProjectTests
{
    [Test]
    public async Task Test_SubtreeScan_ResolvesExternalMethodCallsAndLateBinding()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_subtreescan_test_" + Guid.NewGuid())
            .Replace('\\', '/');
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            // Project A: Service and Endpoint provider
            var projADir = Path.Combine(tempWorkspace, "ProjectA").Replace('\\', '/');
            Directory.CreateDirectory(projADir);

            await File.WriteAllTextAsync(Path.Combine(projADir, "ProjectA.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

            var projAFile = Path.Combine(projADir, "OrderService.cs").Replace('\\', '/');
            var projACode = @"
            namespace Services;
            public class OrderService {
                public void ProcessOrder() {
                }
            }";
            await File.WriteAllTextAsync(projAFile, projACode);

            // Project B: TypeScript endpoint provider
            var projBDir = Path.Combine(tempWorkspace, "ProjectB").Replace('\\', '/');
            Directory.CreateDirectory(projBDir);
            await File.WriteAllTextAsync(Path.Combine(projBDir, "package.json"), "{}");

            var projBFile = Path.Combine(projBDir, "server.ts").Replace('\\', '/');
            var projBCode = @"
            import { Controller, Post } from '@nestjs/common';
            @Controller('orders')
            export class OrdersController {
                @Post('charge')
                async charge() {}
            }";
            await File.WriteAllTextAsync(projBFile, projBCode);

            // Project C: Consumer that calls OrderService and HTTP endpoint
            var projCDir = Path.Combine(tempWorkspace, "ProjectC").Replace('\\', '/');
            Directory.CreateDirectory(projCDir);

            await File.WriteAllTextAsync(Path.Combine(projCDir, "ProjectC.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

            var projCFile = Path.Combine(projCDir, "OrderConsumer.cs").Replace('\\', '/');
            var projCCode = @"
            using System.Net.Http;
            using Services;
            public class OrderConsumer {
                public void Run(OrderService service) {
                    service.ProcessOrder();
                    var client = new HttpClient();
                    client.GetAsync(""http://localhost:8085/api/orders/charge"");
                }
            }";
            await File.WriteAllTextAsync(projCFile, projCCode);

            var dbPath = Path.Combine(tempWorkspace, "test_graph.db");

            await using var client = new SqliteGraphClient(dbPath);

            WorkspaceIndexer.AutoRegisterParsers();

            var indexer = new WorkspaceIndexer(client);

            // 1. Initial full scan
            var fullResults = await indexer.IndexAsync(tempWorkspace, tempWorkspace, clear: true);
            fullResults.NodesCount.Should().BeGreaterThan(0);

            var wsId = await client.GetOrCreateWorkspaceIdAsync(tempWorkspace);

            // Verify cross-project CALLS and CALLS_ENDPOINT exist after full scan
            var callsJson = await client.ExecuteQueryAsync(
                $"MATCH (f1:Function)-[:CALLS]->(f2:Function {{name: 'ProcessOrder'}}) WHERE f1.id STARTS WITH '{wsId}:' RETURN count(f1) AS count");
            callsJson.Should().Contain("\"count\": 1");

            var lateBoundJson = await client.ExecuteQueryAsync(
                $"MATCH (es:ExternalService)-[:CALLS_ENDPOINT]->(ep:Endpoint) WHERE es.id STARTS WITH '{wsId}:' RETURN count(es) AS count");
            lateBoundJson.Should().Contain("\"count\": 1");

            // 2. Subtree scan ONLY ProjectC (incremental scan of a single project)
            var subtreeResults = await indexer.IndexAsync(projCDir, tempWorkspace, clear: false);
            subtreeResults.NodesCount.Should().BeGreaterThan(0);

            // Verify cross-project CALLS to OrderService.ProcessOrder is still resolved!
            var subtreeCallsJson = await client.ExecuteQueryAsync(
                $"MATCH (f1:Function)-[:CALLS]->(f2:Function {{name: 'ProcessOrder'}}) WHERE f1.id STARTS WITH '{wsId}:' RETURN count(f1) AS count");
            subtreeCallsJson.Should().Contain("\"count\": 1");

            // Verify late binding CALLS_ENDPOINT is still resolved!
            var subtreeLateBoundJson = await client.ExecuteQueryAsync(
                $"MATCH (es:ExternalService)-[:CALLS_ENDPOINT]->(ep:Endpoint) WHERE es.id STARTS WITH '{wsId}:' RETURN count(es) AS count");
            subtreeLateBoundJson.Should().Contain("\"count\": 1");

            var allEdgesJson =
                await client.ExecuteQueryAsync(
                    "MATCH (a)-[r]->(b) RETURN a.id AS from_id, type(r) AS kind, b.id AS to_id");
            allEdgesJson.Should().NotBeNull();

            // Verify transitive calls / post-indexing analysis wasn't wiped out across workspace
            var tcJson = await client.ExecuteQueryAsync(
                "MATCH ()-[r:TRANSITIVELY_CALLS]->() RETURN count(r) AS count");
            tcJson.Should().Contain("\"count\": 1");
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                try { Directory.Delete(tempWorkspace, true); } catch { }
            }
        }
    }

    [Test]
    public async Task Test_SubtreeScan_ResolvesPolymorphicInterfaceImplementations()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_poly_test_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            // Project A: Interface
            var projADir = Path.Combine(tempWorkspace, "ProjectA").Replace('\\', '/');
            Directory.CreateDirectory(projADir);
            await File.WriteAllTextAsync(Path.Combine(projADir, "ProjectA.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            await File.WriteAllTextAsync(Path.Combine(projADir, "IPaymentGateway.cs"), @"
            namespace Domain;
            public interface IPaymentGateway {
                void Pay();
            }");

            // Project B: Concrete Implementation
            var projBDir = Path.Combine(tempWorkspace, "ProjectB").Replace('\\', '/');
            Directory.CreateDirectory(projBDir);
            await File.WriteAllTextAsync(Path.Combine(projBDir, "ProjectB.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            await File.WriteAllTextAsync(Path.Combine(projBDir, "StripeGateway.cs"), @"
            using Domain;
            namespace Infrastructure;
            public class StripeGateway : IPaymentGateway {
                public void Pay() {}
            }");

            // Project C: Consumer
            var projCDir = Path.Combine(tempWorkspace, "ProjectC").Replace('\\', '/');
            Directory.CreateDirectory(projCDir);
            await File.WriteAllTextAsync(Path.Combine(projCDir, "ProjectC.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            await File.WriteAllTextAsync(Path.Combine(projCDir, "CheckoutService.cs"), @"
            using Domain;
            namespace Application;
            public class CheckoutService {
                public void Checkout(IPaymentGateway gateway) {
                    gateway.Pay();
                }
            }");

            var dbPath = Path.Combine(tempWorkspace, "poly_graph.db");

            await using var client = new SqliteGraphClient(dbPath);

            WorkspaceIndexer.AutoRegisterParsers();
            var indexer = new WorkspaceIndexer(client);

            // Step 1: Scan Project A and Project B only
            await indexer.IndexAsync(projADir, tempWorkspace, clear: true);
            await indexer.IndexAsync(projBDir, tempWorkspace, clear: false);

            // Verify IMPLEMENTS relationship exists in DB
            var implJson = await client.ExecuteQueryAsync(
                "MATCH (t:Type {name: 'StripeGateway'})-[:IMPLEMENTS]->(i:Type {name: 'IPaymentGateway'}) RETURN count(t) AS count");
            implJson.Should().Contain("\"count\": 1");

            // Step 2: Now do an incremental subtree scan of Project C alone
            var resC = await indexer.IndexAsync(projCDir, tempWorkspace, clear: false);
            resC.NodesCount.Should().BeGreaterThan(0);

            // Verify that CheckoutService.Checkout calls StripeGateway.Pay through polymorphic resolution!
            var callJson = await client.ExecuteQueryAsync(
                "MATCH (f1:Function {name: 'Checkout'})-[:CALLS]->(f2:Function) RETURN f2.name AS target");
            callJson.Should().Contain("Pay");
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                try { Directory.Delete(tempWorkspace, true); } catch { }
            }
        }
    }

    [Test]
    public async Task Test_SubtreeScan_ServerScannedAfterClient_BindsLateEndpoint()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_serverafterclient_test_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            // Project A: Client (HttpClient call)
            var projADir = Path.Combine(tempWorkspace, "ClientProj").Replace('\\', '/');
            Directory.CreateDirectory(projADir);
            await File.WriteAllTextAsync(Path.Combine(projADir, "ClientProj.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            await File.WriteAllTextAsync(Path.Combine(projADir, "WebClient.cs"), @"
            using System.Net.Http;
            public class WebClient {
                public void FetchData() {
                    var client = new HttpClient();
                    client.GetAsync(""http://localhost:9090/api/v1/products"");
                }
            }");

            // Project B: Server (NestJS Controller)
            var projBDir = Path.Combine(tempWorkspace, "ServerProj").Replace('\\', '/');
            Directory.CreateDirectory(projBDir);
            await File.WriteAllTextAsync(Path.Combine(projBDir, "package.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(projBDir, "products.controller.ts"), @"
            import { Controller, Get } from '@nestjs/common';
            @Controller('api/v1/products')
            export class ProductsController {
                @Get()
                async getProducts() {}
            }");

            var dbPath = Path.Combine(tempWorkspace, "serverafterclient.db");

            await using var client = new SqliteGraphClient(dbPath);

            WorkspaceIndexer.AutoRegisterParsers();
            var indexer = new WorkspaceIndexer(client);

            // Step 1: Scan ONLY the client project first
            await indexer.IndexAsync(projADir, tempWorkspace, clear: true);

            // At this point, no Endpoint exists yet
            var epCheck = await client.ExecuteQueryAsync("MATCH (ep:Endpoint) RETURN count(ep) AS count");
            epCheck.Should().Contain("\"count\": 0");

            var lateBoundBefore =
                await client.ExecuteQueryAsync("MATCH ()-[r:CALLS_ENDPOINT]->() RETURN count(r) AS count");
            lateBoundBefore.Should().Contain("\"count\": 0");

            // Step 2: Now do an incremental subtree scan of the server project
            await indexer.IndexAsync(projBDir, tempWorkspace, clear: false);

            // Verify that CALLS_ENDPOINT was successfully created!
            var lateBoundAfter = await client.ExecuteQueryAsync(
                "MATCH (es:ExternalService)-[:CALLS_ENDPOINT]->(ep:Endpoint) RETURN count(es) AS count");
            lateBoundAfter.Should().Contain("\"count\": 1");
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                try { Directory.Delete(tempWorkspace, true); } catch { }
            }
        }
    }

    [Test]
    public async Task Test_SubtreeScan_CodeUpdate_ResolvesNewCallsWithoutBreakingExisting()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_codeupdate_test_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            // Project A: Service provider with two methods
            var projADir = Path.Combine(tempWorkspace, "ServiceProject").Replace('\\', '/');
            Directory.CreateDirectory(projADir);
            await File.WriteAllTextAsync(Path.Combine(projADir, "ServiceProject.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            await File.WriteAllTextAsync(Path.Combine(projADir, "Worker.cs"), @"
            namespace Core;
            public class Worker {
                public void TaskOne() {}
                public void TaskTwo() {}
            }");

            // Project B: Consumer initially calling TaskOne
            var projBDir = Path.Combine(tempWorkspace, "ConsumerProject").Replace('\\', '/');
            Directory.CreateDirectory(projBDir);
            await File.WriteAllTextAsync(Path.Combine(projBDir, "ConsumerProject.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            var consumerFile = Path.Combine(projBDir, "Consumer.cs");
            await File.WriteAllTextAsync(consumerFile, @"
            using Core;
            public class Consumer {
                public void Execute(Worker worker) {
                    worker.TaskOne();
                }
            }");

            var dbPath = Path.Combine(tempWorkspace, "codeupdate.db");

            await using var client = new SqliteGraphClient(dbPath);

            WorkspaceIndexer.AutoRegisterParsers();
            var indexer = new WorkspaceIndexer(client);

            // Step 1: Initial full scan
            await indexer.IndexAsync(tempWorkspace, tempWorkspace, clear: true);

            var callsV1 =
                await client.ExecuteQueryAsync(
                    "MATCH (c:Function {name: 'Execute'})-[:CALLS]->(w:Function) RETURN count(w) AS count");
            callsV1.Should().Contain("\"count\": 1");

            // Step 2: Update Consumer.cs to call both TaskOne and TaskTwo
            await File.WriteAllTextAsync(consumerFile, @"
            using Core;
            public class Consumer {
                public void Execute(Worker worker) {
                    worker.TaskOne();
                    worker.TaskTwo();
                }
            }");

            // Step 3: Rescan ConsumerProject incrementally
            await indexer.IndexAsync(projBDir, tempWorkspace, clear: false);

            var callsV2 = await client.ExecuteQueryAsync(
                "MATCH (c:Function {name: 'Execute'})-[:CALLS]->(w:Function) RETURN w.name AS calledMethod ORDER BY calledMethod");
            callsV2.Should().Contain("TaskOne");
            callsV2.Should().Contain("TaskTwo");
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                try { Directory.Delete(tempWorkspace, true); } catch { }
            }
        }
    }
}
