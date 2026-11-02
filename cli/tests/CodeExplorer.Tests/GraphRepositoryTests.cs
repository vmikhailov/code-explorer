using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Cypher.Common;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class GraphRepositoryTests
{
    [Test]
    public async Task InMemoryGraphRepository_UpsertAndQuery_IndexedCorrectly()
    {
        var repo = new InMemoryGraphRepository();

        var node1 = new Node("file:1", "File", new Dictionary<string, object> { ["name"] = "index.ts", ["path"] = "src/index.ts" });
        var node2 = new Node("fn:1", "Function", new Dictionary<string, object> { ["name"] = "main", ["path"] = "src/index.ts" });
        var node3 = new Node("fn:2", "Function", new Dictionary<string, object> { ["name"] = "helper", ["path"] = "src/index.ts" });

        await repo.UpsertNodesAsync([node1, node2, node3]);

        var n1 = await repo.GetNodeByIdAsync("file:1");
        Assert.That(n1, Is.Not.Null);
        Assert.That(n1!.Kind, Is.EqualTo("File"));
        Assert.That(n1.Properties["name"], Is.EqualTo("index.ts"));

        var functions = await repo.GetNodesByKindAsync("Function");
        Assert.That(functions.Count, Is.EqualTo(2));

        var rel1 = new Relationship("fn:1", "fn:2", "CALLS", new Dictionary<string, object> { ["line"] = 10 });
        var rel2 = new Relationship("file:1", "fn:1", "CONTAINS", []);
        await repo.UpsertRelationshipsAsync([rel1, rel2]);

        var outgoing = await repo.GetOutgoingRelationshipsAsync("fn:1");
        Assert.That(outgoing.Count, Is.EqualTo(1));
        Assert.That(outgoing[0].To, Is.EqualTo("fn:2"));

        var incoming = await repo.GetIncomingRelationshipsAsync("fn:2", "CALLS");
        Assert.That(incoming.Count, Is.EqualTo(1));
        Assert.That(incoming[0].From, Is.EqualTo("fn:1"));

        var symbols = await repo.LoadSymbolsByNamesAsync(["main", "nonexistent"], "");
        Assert.That(symbols.ContainsKey(("Function", "main")), Is.True);
        Assert.That(symbols[("Function", "main")], Is.EqualTo("fn:1"));

        await repo.ClearAsync();
        Assert.That(await repo.GetAllNodesAsync(), Is.Empty);
        Assert.That(await repo.GetAllRelationshipsAsync(), Is.Empty);
    }

    [Test]
    public async Task PersistedGraphRepository_WrapsClient_DelegatesCorrectly()
    {
        var inMemory = new InMemoryGraphRepository();
        var persisted = new PersistedGraphRepository(inMemory);

        var node = new Node("type:User", "Type", new Dictionary<string, object> { ["name"] = "User" });
        await persisted.UpsertNodesAsync([node]);

        var all = await persisted.GetAllNodesAsync();
        Assert.That(all.Count, Is.EqualTo(1));
        Assert.That(all[0].Properties["name"], Is.EqualTo("User"));

        var symbols = await persisted.LoadSymbolsByNamesAsync(["User"], "");
        Assert.That(symbols.ContainsKey(("Type", "User")), Is.True);
    }

    [Test]
    public async Task StronglyTypedEnums_InMemoryAndPersisted_QueryCorrectly()
    {
        var repo = new InMemoryGraphRepository();

        var node1 = new Node("srv:1", "Service", new Dictionary<string, object> { ["name"] = "AuthService" });
        var node2 = new Node("db:1", "Database", new Dictionary<string, object> { ["name"] = "UserDb" });
        await repo.UpsertNodesAsync([node1, node2]);

        var services = await repo.GetNodesByKindAsync(NodeKind.Service);
        Assert.That(services.Count, Is.EqualTo(1));
        Assert.That(services[0].Id, Is.EqualTo("srv:1"));

        var databases = await repo.GetNodesByKindAsync(NodeKind.Database);
        Assert.That(databases.Count, Is.EqualTo(1));
        Assert.That(databases[0].Id, Is.EqualTo("db:1"));

        var rel = new Relationship("srv:1", "db:1", "QUERIES_DB", []);
        await repo.UpsertRelationshipsAsync([rel]);

        var outgoing = await repo.GetOutgoingRelationshipsAsync("srv:1", RelationshipKind.QueriesDb);
        Assert.That(outgoing.Count, Is.EqualTo(1));
        Assert.That(outgoing[0].To, Is.EqualTo("db:1"));

        var incoming = await repo.GetIncomingRelationshipsAsync("db:1", RelationshipKind.QueriesDb);
        Assert.That(incoming.Count, Is.EqualTo(1));
        Assert.That(incoming[0].From, Is.EqualTo("srv:1"));

        // SymbolKey and RelationshipKey tests
        var symKey1 = new SymbolKey(NodeKind.Service, "AuthService");
        var symKey2 = new SymbolKey(NodeKind.Service, "AuthService");
        Assert.That(symKey1, Is.EqualTo(symKey2));
        Assert.That(symKey1.Kind, Is.EqualTo(NodeKind.Service));

        var relKey1 = new RelationshipKey("srv:1", "db:1", RelationshipKind.QueriesDb);
        var relKey2 = new RelationshipKey("srv:1", "db:1", RelationshipKind.QueriesDb);
        Assert.That(relKey1, Is.EqualTo(relKey2));

        var symKeyFromString = new SymbolKey("service", "AuthService");
        Assert.That(symKeyFromString.Kind, Is.EqualTo(NodeKind.Service));

        var relKeyFromString = new RelationshipKey("srv:1", "db:1", "queries_db");
        Assert.That(relKeyFromString.Kind, Is.EqualTo(RelationshipKind.QueriesDb));

        var typedNode = new Node("srv:2", NodeKind.Service, new Dictionary<string, object> { ["name"] = "BillingService" });
        Assert.That(typedNode.NodeKind, Is.EqualTo(NodeKind.Service));
        Assert.That(typedNode.Kind, Is.EqualTo("Service"));

        var typedRel = new Relationship("srv:2", "db:1", RelationshipKind.QueriesDb);
        Assert.That(typedRel.RelationshipKind, Is.EqualTo(RelationshipKind.QueriesDb));
        Assert.That(typedRel.Kind, Is.EqualTo("QUERIES_DB"));

        var reference = new CodeExplorer.Common.Reference("sym:1", "BillingService", RelationshipKind.Calls);
        Assert.That(reference.RelationshipKind, Is.EqualTo(RelationshipKind.Calls));
        Assert.That(reference.Kind, Is.EqualTo("CALLS"));
    }
}
