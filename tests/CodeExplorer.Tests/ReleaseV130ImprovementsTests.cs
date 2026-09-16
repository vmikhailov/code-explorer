using System.Text.Json;
using CodeExplorer.Cypher.Parser;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.CSharp;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ReleaseV130ImprovementsTests
{
    [Test]
    public void Test_CypherSecurity_AllowsForbiddenWordsInStringLiterals()
    {
        // Must NOT throw when mutating words appear inside quotes
        Assert.DoesNotThrow(() => CypherSecurityValidator.ValidateReadOnly("MATCH (q:Query) WHERE q.name = 'DELETE Query' RETURN count(q)"));
        Assert.DoesNotThrow(() => CypherSecurityValidator.ValidateReadOnly("MATCH (q:Query) WHERE q.name = \"CREATE Index\" RETURN q"));
        Assert.DoesNotThrow(() => CypherSecurityValidator.ValidateReadOnly("MATCH (n) WHERE n.action = 'drop table' RETURN n"));
        Assert.DoesNotThrow(() => CypherSecurityValidator.ValidateReadOnly("MATCH (n) WHERE n.mode = 'set default' RETURN n"));
    }

    [Test]
    public void Test_CypherSecurity_BlocksRealMutations()
    {
        Assert.Throws<InvalidOperationException>(() => CypherSecurityValidator.ValidateReadOnly("MATCH (n) DELETE n"));
        Assert.Throws<InvalidOperationException>(() => CypherSecurityValidator.ValidateReadOnly("MATCH (n) DETACH DELETE n"));
        Assert.Throws<InvalidOperationException>(() => CypherSecurityValidator.ValidateReadOnly("CREATE (n:Test)"));
        Assert.Throws<InvalidOperationException>(() => CypherSecurityValidator.ValidateReadOnly("MATCH (n) SET n.name = 'test'"));
    }

    [Test]
    public async Task Test_OrleansAndMongoAndEfCore_Indexing()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_v130_test_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            var projectDir = Path.Combine(tempWorkspace, "SampleApp").Replace('\\', '/');
            Directory.CreateDirectory(projectDir);
            await File.WriteAllTextAsync(Path.Combine(projectDir, "SampleApp.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

            var code = @"
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using Orleans;

namespace SampleApp;

// 1. Orleans Grain
public interface IGameSessionGrain : IGrainWithGuidKey
{
    Task ConnectAsync();
}

public class GameSessionGrain : Grain, IGameSessionGrain
{
    public Task ConnectAsync() => Task.CompletedTask;
}

// 2. Orleans Caller
public class GameLauncher
{
    private readonly IGrainFactory _factory;
    public GameLauncher(IGrainFactory factory) => _factory = factory;
    public void Launch(Guid id) => _factory.GetGrain<IGameSessionGrain>(id);
}

// 3. EF Core: GenericRepository has DbSet<TEntity> property that should NOT be a table
public class GenericRepository<TEntity> where TEntity : class
{
    protected DbSet<TEntity> DbSet { get; set; }
}

// Real DbContext has Orders table
public class AppDbContext : DbContext
{
    public DbSet<Order> Orders { get; set; }
}
public class Order { public int Id { get; set; } }

// 4. MongoDB Collection
public class MongoRepo
{
    public void Setup(IMongoDatabase db)
    {
        var col = db.GetCollection<Order>(""orders_collection"");
    }
}

// 5. ASP.NET Controller with Async suffix and class route
[ApiController]
[Route(""api/[controller]"")]
public class GameController : ControllerBase
{
    [HttpPost(""connect"")]
    public Task SessionConnectAsync() => Task.CompletedTask;

    [HttpGet]
    public Task PlayAsync() => Task.CompletedTask;
}
";
            await File.WriteAllTextAsync(Path.Combine(projectDir, "Services.cs"), code);

            var dbPath = Path.Combine(tempWorkspace, "v130_graph.db");
            await using var client = new SqliteGraphClient(dbPath);

            WorkspaceIndexer.Register(new CSharpParser());
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempWorkspace, tempWorkspace, clear: true);

            // Verify Orleans Grain EntryPoint
            var grainRes = await client.ExecuteQueryAsync("MATCH (e:EntryPoint) WHERE e.name CONTAINS 'GameSessionGrain' RETURN e.name AS name");
            using var grainDoc = JsonDocument.Parse(grainRes);
            Assert.That(grainDoc.RootElement.GetArrayLength(), Is.GreaterThan(0), "Orleans Grain GameSessionGrain should be detected as EntryPoint");

            // Verify no phantom controller endpoint for class
            var phantomRes = await client.ExecuteQueryAsync("MATCH (e:Endpoint) WHERE e.name = 'GET:/api/Game' RETURN e.name AS name");
            using var phantomDoc = JsonDocument.Parse(phantomRes);
            Assert.That(phantomDoc.RootElement.GetArrayLength(), Is.EqualTo(0), "Phantom GET:/api/Game endpoint should NOT exist");

            // Verify real endpoint with trimmed Async
            var methodEndpointRes = await client.ExecuteQueryAsync("MATCH (e:Endpoint) WHERE e.name CONTAINS 'connect' RETURN e.name AS name");
            using var methodDoc = JsonDocument.Parse(methodEndpointRes);
            Assert.That(methodDoc.RootElement.GetArrayLength(), Is.GreaterThan(0), "Endpoint for connect should exist");

            // Verify no Table named 'DbSet'
            var dbSetTableRes = await client.ExecuteQueryAsync("MATCH (t:Table) WHERE t.name = 'DbSet' OR t.name = 'IDbSet' RETURN t.name AS name");
            using var dbSetDoc = JsonDocument.Parse(dbSetTableRes);
            Assert.That(dbSetDoc.RootElement.GetArrayLength(), Is.EqualTo(0), "Table named DbSet should NOT exist");

            // Verify real Table 'Orders' exists
            var ordersTableRes = await client.ExecuteQueryAsync("MATCH (t:Table) WHERE t.name = 'Orders' RETURN t.name AS name");
            using var ordersDoc = JsonDocument.Parse(ordersTableRes);
            Assert.That(ordersDoc.RootElement.GetArrayLength(), Is.GreaterThan(0), "Table Orders should exist");

            // Verify MongoDB collection table
            var mongoTableRes = await client.ExecuteQueryAsync("MATCH (t:Table) WHERE t.name = 'orders_collection' RETURN t.name AS name");
            using var mongoDoc = JsonDocument.Parse(mongoTableRes);
            Assert.That(mongoDoc.RootElement.GetArrayLength(), Is.GreaterThan(0), "MongoDB table orders_collection should exist");
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
