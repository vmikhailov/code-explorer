using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.Java;
using CodeExplorer.Parser.Shared;
using CodeExplorer.Tests.Shared;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class QuarkusParserTests
{
    private const string SampleJaxRs = @"
package io.quarkus.sample;

import jakarta.ws.rs.GET;
import jakarta.ws.rs.POST;
import jakarta.ws.rs.Path;
import io.quarkus.websockets.next.WebSocket;

@Path(""/api/heroes"")
public class HeroesResource {
    @GET
    @Path(""/random"")
    public String getRandomHero() {
        return ""Superman"";
    }

    @GET
    public String getAllHeroes() {
        return ""All"";
    }

    @POST
    @Path(""/create"")
    public void createHero(String hero) {
    }
}

@WebSocket(path = ""/stats/team"")
public class TeamStatsWebSocket {
}
";

    private const string SampleMessagingAndPanache = @"
package io.quarkus.sample;

import io.quarkus.mongodb.panache.common.MongoEntity;
import io.quarkus.mongodb.panache.reactive.ReactivePanacheMongoEntity;
import io.quarkus.hibernate.orm.panache.PanacheRepository;
import io.smallrye.reactive.messaging.MutinyEmitter;
import org.eclipse.microprofile.reactive.messaging.Channel;
import org.eclipse.microprofile.reactive.messaging.Incoming;
import org.eclipse.microprofile.reactive.messaging.Outgoing;

@MongoEntity(collection = ""Fights"")
public class Fight extends ReactivePanacheMongoEntity {
    public String winner;
}

public class HeroRepository implements PanacheRepository<Hero> {
}

public class StatsService {
    static final String FIGHTS_CHANNEL = ""fights"";

    private final MutinyEmitter<String> emitter;

    public StatsService(@Channel(FIGHTS_CHANNEL) MutinyEmitter<String> emitter) {
        this.emitter = emitter;
    }

    @Incoming(FIGHTS_CHANNEL)
    @Outgoing(""winner-stats"")
    public String processFight(String fight) {
        return fight;
    }
}
";

    private const string SampleOpenApi = @"
openapi: 3.1.0
info:
  title: Super Heroes API
  version: 1.0.0
paths:
  /api/fights:
    get:
      summary: Get all fights
      operationId: getAllFights
    post:
      summary: Perform a fight
      operationId: performFight
  /api/fights/random:
    get:
      summary: Get random fighters
      operationId: getRandomFighters
";

    [Test]
    public async Task Test_Quarkus_JaxRs_And_WebSocket_Endpoints()
    {
        var parser = new JavaParser();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "HeroesResource.java");
            await File.WriteAllTextAsync(filePath, SampleJaxRs);

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", "test-ws", tempDir);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, "test-ws", tempDir);

            var fileNode = syntaxTree.FileNode;
            Assert.That(fileNode, Is.Not.Null);

            var endpoints = FindNodes<EndpointNode>(fileNode.Children);
            Assert.That(endpoints.Count, Is.EqualTo(4));

            var routes = endpoints.Select(e => $"{e.HttpMethod}:{e.RouteTemplate}").ToList();
            Assert.That(routes, Does.Contain("GET:/api/heroes/random"));
            Assert.That(routes, Does.Contain("GET:/api/heroes"));
            Assert.That(routes, Does.Contain("POST:/api/heroes/create"));
            Assert.That(routes, Does.Contain("WS:/stats/team"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_Quarkus_Panache_And_Messaging()
    {
        var parser = new JavaParser();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "Services.java");
            await File.WriteAllTextAsync(filePath, SampleMessagingAndPanache);

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", "test-ws", tempDir);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, "test-ws", tempDir);

            var fileNode = syntaxTree.FileNode;
            Assert.That(fileNode, Is.Not.Null);

            var types = FindNodes<TypeNode>(fileNode.Children);
            var typeNames = types.Select(t => t.Name).ToList();
            Assert.That(typeNames, Does.Contain("Fight"));
            Assert.That(typeNames, Does.Contain("HeroRepository"));
            Assert.That(typeNames, Does.Contain("StatsService"));

            // References check
            var references = fileNode.References.Concat(FindReferences(fileNode.Children)).ToList();
            var persistedIn = references.Where(r => r.Kind == OntologyConstants.Relationships.PersistedIn).ToList();
            Assert.That(persistedIn.Any(r => r.TargetName == "Fights"), Is.True);

            var accessedBy = references.Where(r => r.Kind == OntologyConstants.Relationships.AccessedBy).ToList();
            Assert.That(accessedBy.Any(r => r.TargetName == "Hero"), Is.True);

            var subscribesTo = references.Where(r => r.Kind == OntologyConstants.Relationships.SubscribesTo).ToList();
            Assert.That(subscribesTo.Any(r => r.TargetName == "kafka:fights"), Is.True);

            var publishesTo = references.Where(r => r.Kind == OntologyConstants.Relationships.PublishesTo).ToList();
            Assert.That(publishesTo.Any(r => r.TargetName == "kafka:winner-stats"), Is.True);
            Assert.That(publishesTo.Any(r => r.TargetName == "kafka:fights"), Is.True);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_Quarkus_OpenApi_Parser()
    {
        var parser = new OpenApiFileParser();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "openapi.yml");
            await File.WriteAllTextAsync(filePath, SampleOpenApi);

            Assert.That(parser.CanParseFile(filePath), Is.True);

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", "test-ws", tempDir);
            var fileNode = syntaxTree.FileNode;
            Assert.That(fileNode, Is.Not.Null);

            var endpoints = FindNodes<EndpointNode>(fileNode.Children);
            Assert.That(endpoints.Count, Is.EqualTo(3));

            var operations = endpoints.Select(e => $"{e.HttpMethod}:{e.RouteTemplate}").ToList();
            Assert.That(operations, Does.Contain("GET:/api/fights"));
            Assert.That(operations, Does.Contain("POST:/api/fights"));
            Assert.That(operations, Does.Contain("GET:/api/fights/random"));

            var functions = FindNodes<FunctionNode>(fileNode.Children);
            var funcNames = functions.Select(f => f.Name).ToList();
            Assert.That(funcNames, Does.Contain("getAllFights"));
            Assert.That(funcNames, Does.Contain("performFight"));
            Assert.That(funcNames, Does.Contain("getRandomFighters"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    private static List<Reference> FindReferences(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<Reference>();
        foreach (var node in nodes)
        {
            result.AddRange(node.References);
            if (node.Children.Count > 0)
                result.AddRange(FindReferences(node.Children));
        }
        return result;
    }

    private static List<T> FindNodes<T>(IEnumerable<IOntologyNode> nodes) where T : IOntologyNode
    {
        var result = new List<T>();
        foreach (var node in nodes)
        {
            if (node is T match)
                result.Add(match);
            if (node.Children.Count > 0)
                result.AddRange(FindNodes<T>(node.Children));
        }
        return result;
    }
}
