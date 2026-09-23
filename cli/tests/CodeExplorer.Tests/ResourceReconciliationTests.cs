using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ResourceReconciliationTests
{
    private ResourceReconciliationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new ResourceReconciliationService();
    }

    [Test]
    public void BuildCanonicalDatabaseId_FormatsConsistentUrn()
    {
        var id = ResourceReconciliationService.BuildCanonicalDatabaseId("ws1", "relational", "Orders_Db");
        Assert.That(id, Is.EqualTo("ws1:res:db:relational:orders_db"));

        var specialCharsId = ResourceReconciliationService.BuildCanonicalDatabaseId("ws1", "cache", "my.redis-instance@1");
        Assert.That(specialCharsId, Is.EqualTo("ws1:res:db:cache:my_redis-instance_1"));
    }

    [Test]
    public void BuildCanonicalTopicId_FormatsConsistentUrn()
    {
        var id = ResourceReconciliationService.BuildCanonicalTopicId("ws1", "rabbitmq", "orders.created");
        Assert.That(id, Is.EqualTo("ws1:res:topic:rabbitmq:orders.created"));
    }

    [Test]
    public void BuildCanonicalServiceId_FormatsConsistentUrn()
    {
        var id = ResourceReconciliationService.BuildCanonicalServiceId("ws1", "external", "api.stripe.com");
        Assert.That(id, Is.EqualTo("ws1:res:service:external:api.stripe.com"));
    }

    [Test]
    public void NormalizeResourceName_StripsGenericKeys()
    {
        Assert.That(ResourceReconciliationService.NormalizeResourceName("DefaultConnection", "PostgreSQL"), Is.EqualTo("PostgreSQL"));
        Assert.That(ResourceReconciliationService.NormalizeResourceName("ConnectionString", "SQL Server"), Is.EqualTo("SQL Server"));
        Assert.That(ResourceReconciliationService.NormalizeResourceName("spring.datasource.url", "MySQL"), Is.EqualTo("MySQL"));
        Assert.That(ResourceReconciliationService.NormalizeResourceName("database", "Redis"), Is.EqualTo("Redis"));

        Assert.That(ResourceReconciliationService.NormalizeResourceName("orders_db", "PostgreSQL"), Is.EqualTo("orders_db"));
        Assert.That(ResourceReconciliationService.NormalizeResourceName("PaymentServiceDb", "SQL Server"), Is.EqualTo("PaymentServiceDb"));
    }

    [Test]
    public void RegisterResource_MergesAliasesAndPreventsDuplicates()
    {
        var res1 = _service.RegisterResource(
            "ws1",
            "orders_db",
            "PostgreSQL",
            "relational",
            OntologyConstants.NodeLabels.Database,
            "docker-compose.yml",
            aliases: ["postgres", "postgres:5432/orders_db"]
        );

        Assert.That(res1.Id, Is.EqualTo("ws1:res:db:relational:orders_db"));
        Assert.That(res1.Name, Is.EqualTo("orders_db"));

        // Register from appsettings.json with an alias
        var res2 = _service.RegisterResource(
            "ws1",
            "orders_db",
            "PostgreSQL",
            "relational",
            OntologyConstants.NodeLabels.Database,
            "src/Backend/appsettings.json",
            aliases: ["DefaultConnection", "MainDb"]
        );

        Assert.That(res2.Id, Is.EqualTo(res1.Id), "Must resolve to identical canonical resource ID");
        Assert.That(_service.AllResources.Count, Is.EqualTo(1), "Resource count must remain 1 after re-registration");

        // Lookup by aliases
        var byDefaultConn = _service.ResolveResource("DefaultConnection");
        Assert.That(byDefaultConn, Is.Not.Null);
        Assert.That(byDefaultConn!.Id, Is.EqualTo(res1.Id));

        var byUrl = _service.ResolveResource("postgres://localhost:5432/orders_db");
        Assert.That(byUrl, Is.Not.Null);
        Assert.That(byUrl!.Id, Is.EqualTo(res1.Id));

        var byName = _service.ResolveResource("orders_db");
        Assert.That(byName, Is.Not.Null);
        Assert.That(byName!.Id, Is.EqualTo(res1.Id));
    }

    [Test]
    public void ResolveResource_MatchesByEngineWhenOnlyOneAvailable()
    {
        _service.RegisterResource(
            "ws1",
            "redis_cache",
            "Redis",
            "cache",
            OntologyConstants.NodeLabels.Database,
            "docker-compose.yml",
            aliases: ["redis"]
        );

        var resolved = _service.ResolveResource("redis_client", expectedEngine: "Redis");
        Assert.That(resolved, Is.Not.Null);
        Assert.That(resolved!.Name, Is.EqualTo("redis_cache"));
    }
}
