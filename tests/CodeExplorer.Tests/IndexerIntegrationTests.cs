using NUnit.Framework;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.TypeScript;

namespace CodeExplorer.Tests;

[TestFixture]
[Category("Integration")]
public class IndexerIntegrationTests
{
    [Test]
    public async Task Test_WorkspaceLevelParser_DynamicDetectionAndLateBinding()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "codeexplorer_test_workspace_" + Guid.NewGuid())
            .Replace('\\', '/');
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            // Project A: C# project
            var projADir = Path.Combine(tempWorkspace, "ProjectA").Replace('\\', '/');
            Directory.CreateDirectory(projADir);

            await File.WriteAllTextAsync(Path.Combine(projADir, "ProjectA.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

            var projAFile = Path.Combine(projADir, "Client.cs").Replace('\\', '/');

            var projACode = @"
            using System.Net.Http;
            class Client {
                void Call() {
                    var client = new HttpClient();
                    client.GetAsync(""http://localhost:8085/api/orders/charge"");
                }
            }";
            await File.WriteAllTextAsync(projAFile, projACode);

            // Project B: TypeScript project
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

            // Setup parsing
            var dbPath = Path.Combine(tempWorkspace, "test_graph.db");
            await using var client = new SqliteGraphClient(dbPath);

            // Register parsers if they aren't already registered
            WorkspaceIndexer.Register(new CSharpParser());
            WorkspaceIndexer.Register(new TypeScriptParser());            // Run scanner
            var parser = new WorkspaceIndexer(client);
            var results = await parser.IndexAsync(tempWorkspace, tempWorkspace, clear: true);

            Assert.That(results.NodesCount, Is.GreaterThan(0));
            Assert.That(results.RelationshipsCount, Is.GreaterThan(0));

            // Verify Endpoint and CALLS_ENDPOINT in the database
            var wsId = await client.GetOrCreateWorkspaceIdAsync(tempWorkspace);

            var endpointCountJson = await client.ExecuteQueryAsync(
                $"MATCH (ep:Endpoint) WHERE toString(ep.id) STARTS WITH '{wsId}:' RETURN count(ep) AS count");
            Assert.That(endpointCountJson, Contains.Substring("\"count\": 2"));

            var implByJson = await client.ExecuteQueryAsync(
                $"MATCH (ep:Endpoint)-[:EXPOSED_BY]->(f:Function {{name: 'charge'}}) WHERE f.id STARTS WITH '{wsId}:' RETURN ep.id AS id");
            Assert.That(implByJson, Contains.Substring(":endpoint:POST:/orders/charge"));

            var lateBoundJson = await client.ExecuteQueryAsync(
                $"MATCH (es:ExternalService)-[:CALLS_ENDPOINT]->(ep:Endpoint) WHERE es.id STARTS WITH '{wsId}:' RETURN ep.id AS id");
            Assert.That(lateBoundJson, Contains.Substring(":endpoint:POST:/orders/charge"));
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                Directory.Delete(tempWorkspace, true);
            }
        }
    }

    [Test]
    public async Task Test_TypeBindingsAndCallsResolution()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "codeexplorer_typebinding_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            var projDir = Path.Combine(tempWorkspace, "ProjectA");
            Directory.CreateDirectory(projDir);
            await File.WriteAllTextAsync(Path.Combine(projDir, "package.json"), "{}");

            var orderServiceFile = Path.Combine(projDir, "OrderService.ts");

            var orderServiceCode = @"
export class PaymentService {
    async charge() {}
}

export class OrderService {
    constructor(private paymentService: PaymentService) {}
    async process() {
        await this.paymentService.charge();
    }
}";
            await File.WriteAllTextAsync(orderServiceFile, orderServiceCode);

            // Setup parsing
            var dbPath = Path.Combine(tempWorkspace, "test_graph.db");
            await using var client = new SqliteGraphClient(dbPath);
            // Register parsers if they aren't already registered
            WorkspaceIndexer.Register(new TypeScriptParser());
            // Run scanner
            var parser = new WorkspaceIndexer(client);
            var results = await parser.IndexAsync(tempWorkspace, tempWorkspace, clear: true);

            Assert.That(results.NodesCount, Is.GreaterThan(0));

            // Verify using database query that Function 'process' CALLS Function 'charge' in Type 'PaymentService'
            var callsQuery =
                $"MATCH (c:Type {{name: 'PaymentService'}})-[:HAS_METHOD]->(f2:Function {{name: 'charge'}})<-[:CALLS]-(f1:Function {{name: 'process'}}) RETURN f1.name AS f1Name, f2.name AS f2Name";
            var queryResult = await client.ExecuteQueryAsync(callsQuery);

            Assert.That(queryResult, Contains.Substring("\"f1Name\": \"process\""));
            Assert.That(queryResult, Contains.Substring("\"f2Name\": \"charge\""));
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                Directory.Delete(tempWorkspace, true);
            }
        }
    }

    [Test]
    public async Task Test_NestedProjectsResolution()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "codeexplorer_nested_project_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            var projADir = Path.Combine(tempWorkspace, "ProjectA");
            Directory.CreateDirectory(projADir);
            await File.WriteAllTextAsync(Path.Combine(projADir, "package.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(projADir, "serviceA.ts"), "export class ServiceA {}");

            var projBDir = Path.Combine(projADir, "ProjectB");
            Directory.CreateDirectory(projBDir);
            await File.WriteAllTextAsync(Path.Combine(projBDir, "package.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(projBDir, "serviceB.ts"), "export class ServiceB {}");

            // Setup parsing
            var dbPath = Path.Combine(tempWorkspace, "test_graph.db");
            await using var client = new SqliteGraphClient(dbPath);
            // Register parsers if they aren't already registered
            WorkspaceIndexer.Register(new TypeScriptParser());
            // Run scanner
            var parser = new WorkspaceIndexer(client);
            var results = await parser.IndexAsync(tempWorkspace, tempWorkspace, clear: true);

            Assert.That(results.NodesCount, Is.GreaterThan(0));

            // Verify using database queries
            // ProjectB should be nested under ProjectA via their Folder locations
            var projectAQuery = "MATCH (p:Project {name: 'ProjectA'}) RETURN p.id AS id";
            var projectBQuery = "MATCH (p:Project {name: 'ProjectB'}) RETURN p.id AS id";

            var containsQuery =
                "MATCH (p1:Project {name: 'ProjectA'})-[:LOCATED_IN]->(f1:Folder)-[:CONTAINS]->(f2:Folder)<-[:LOCATED_IN]-(p2:Project {name: 'ProjectB'}) RETURN p1.name AS p1Name, p2.name AS p2Name";

            var resA = await client.ExecuteQueryAsync(projectAQuery);
            var resB = await client.ExecuteQueryAsync(projectBQuery);
            var resContains = await client.ExecuteQueryAsync(containsQuery);

            Assert.That(resA, Contains.Substring("ProjectA"));
            Assert.That(resB, Contains.Substring("ProjectB"));
            Assert.That(resContains, Contains.Substring("\"p1Name\": \"ProjectA\""));
            Assert.That(resContains, Contains.Substring("\"p2Name\": \"ProjectB\""));
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                Directory.Delete(tempWorkspace, true);
            }
        }
    }
}
