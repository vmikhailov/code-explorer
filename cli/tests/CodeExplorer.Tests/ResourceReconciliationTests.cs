using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
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
        Assert.That(ResourceReconciliationService.NormalizeResourceName("BillingConnectionString", "PostgreSQL"), Is.EqualTo("Billing"));
        Assert.That(ResourceReconciliationService.NormalizeResourceName("OrdersDbContext", "SQL Server"), Is.EqualTo("Orders"));
        Assert.That(ResourceReconciliationService.NormalizeResourceName("CatalogConnection", "MySQL"), Is.EqualTo("Catalog"));
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

    [Test]
    public void NestedSqlParser_ResolvesCanonicalDatabase_InsteadOfDefault()
    {
        _service.RegisterResource(
            "ws1",
            "orders_db",
            "PostgreSQL",
            "relational",
            OntologyConstants.NodeLabels.Database,
            "docker-compose.yml",
            aliases: ["orders_db"]
        );

        var ctx = new ParsingContext("ws1", "ws1", _service);

        var queryNode = NestedSqlParser.ParseNestedSql("SELECT id, name FROM customers WHERE active = 1", "ws1:query:1", "repo/query.sql", ctx);
        Assert.That(queryNode, Is.Not.Null);

        var dbNode = queryNode!.Children.OfType<DatabaseNode>().FirstOrDefault();
        Assert.That(dbNode, Is.Not.Null);
        Assert.That(dbNode!.Name, Is.EqualTo("orders_db"), "SQL query without explicit DB prefix must bind to canonical relational DB");
    }

    [Test]
    public void UsesDbRelationship_RetainsViaAndProviderProperties()
    {
        var relExt = new Dictionary<string, string>
        {
            ["via"] = "TypeORM",
            ["provider"] = "typeorm",
            ["is_orm"] = "true"
        };

        var rel = new UsesDbRelationship("from:1", "to:1", relExt);
        var dbRel = Relationship.FromRelationship(rel);

        Assert.That(dbRel.Kind, Is.EqualTo(OntologyConstants.Relationships.UsesDb));
        Assert.That(dbRel.Properties, Is.Not.Null);
        Assert.That(dbRel.Properties!["via"]?.ToString(), Is.EqualTo("TypeORM"));
        Assert.That(dbRel.Properties!["provider"]?.ToString(), Is.EqualTo("typeorm"));
        Assert.That(dbRel.Properties!["is_orm"]?.ToString(), Is.EqualTo("true"));
    }

    [Test]
    public void CleanProjectNameToDomain_CleansMicroservicePrefixesAndSuffixes()
    {
        Assert.That(SyntaxEnricher.CleanProjectNameToDomain("internal-service-networks"), Is.EqualTo("networks"));
        Assert.That(SyntaxEnricher.CleanProjectNameToDomain("internal-service-landers"), Is.EqualTo("landers"));
        Assert.That(SyntaxEnricher.CleanProjectNameToDomain("internal-service-sources"), Is.EqualTo("sources"));
        Assert.That(SyntaxEnricher.CleanProjectNameToDomain("internal-service-splits"), Is.EqualTo("splits"));
        Assert.That(SyntaxEnricher.CleanProjectNameToDomain("internal-service-action-scheduler"), Is.EqualTo("action-scheduler"));
        Assert.That(SyntaxEnricher.CleanProjectNameToDomain("integration-service-postback-partner"), Is.EqualTo("postback-partner"));
        Assert.That(SyntaxEnricher.CleanProjectNameToDomain("integration-service-smart-cpa"), Is.EqualTo("smart-cpa"));
        Assert.That(SyntaxEnricher.CleanProjectNameToDomain("internal-bundle-scheduler"), Is.EqualTo("bundle-scheduler"));
        Assert.That(SyntaxEnricher.CleanProjectNameToDomain("tournament-service"), Is.EqualTo("tournament"));
        Assert.That(SyntaxEnricher.CleanProjectNameToDomain("orders-api"), Is.EqualTo("orders"));
        Assert.That(SyntaxEnricher.CleanProjectNameToDomain("Lidoma.Tournament"), Is.EqualTo("tournament"));
        Assert.That(SyntaxEnricher.CleanProjectNameToDomain("service-billing"), Is.EqualTo("billing"));
        Assert.That(SyntaxEnricher.CleanProjectNameToDomain("srv-users"), Is.EqualTo("users"));
        Assert.That(SyntaxEnricher.CleanProjectNameToDomain("internal--service-networks"), Is.EqualTo("networks"));
        Assert.That(SyntaxEnricher.CleanProjectNameToDomain("integration--smart-cpa"), Is.EqualTo("smart-cpa"));
    }

    [Test]
    public void NormalizeServiceName_StrictStrippingAndLowercase()
    {
        Assert.That(WorkspaceConventions.NormalizeServiceName("internal--service-networks"), Is.EqualTo("networks"));
        Assert.That(WorkspaceConventions.NormalizeServiceName("internal--networks"), Is.EqualTo("networks"));
        Assert.That(WorkspaceConventions.NormalizeServiceName("integration--smart-cpa"), Is.EqualTo("smart-cpa"));
        Assert.That(WorkspaceConventions.NormalizeServiceName("integration--service-smart-cpa"), Is.EqualTo("smart-cpa"));
        Assert.That(WorkspaceConventions.NormalizeServiceName("service-billing"), Is.EqualTo("billing"));
        Assert.That(WorkspaceConventions.NormalizeServiceName("ATSAnalyticsDepartment"), Is.EqualTo("atsanalyticsdepartment"));
        Assert.That(WorkspaceConventions.NormalizeServiceName("internal-service-networks"), Is.EqualTo("networks"));
        Assert.That(WorkspaceConventions.NormalizeServiceName("internal-service-action-scheduler"), Is.EqualTo("action-scheduler"));
        Assert.That(WorkspaceConventions.NormalizeServiceName("internal--service-bff"), Is.EqualTo("bff"));
        Assert.That(WorkspaceConventions.NormalizeServiceName("service-"), Is.EqualTo("service-"));
        Assert.That(WorkspaceConventions.NormalizeServiceName(""), Is.EqualTo(""));
    }

    [Test]
    public void BigQuery_HandlesCanonicalDefaultSchema()
    {
        // 1. NestedSqlParser in BigQuery context without schema defaults to 'default'
        var q1 = NestedSqlParser.ParseNestedSql("SELECT * FROM networks", "ws:q:1", "bq-calc/src/repo.ts");
        Assert.That(q1, Is.Not.Null);
        var dbNode1 = q1!.Children.OfType<DatabaseNode>().FirstOrDefault();
        Assert.That(dbNode1, Is.Not.Null);
        Assert.That(dbNode1!.Name, Is.EqualTo("BigQuery.default"));
        Assert.That(dbNode1.Extensions!["schema"], Is.EqualTo("default"));

        // 2. NestedSqlParser in BigQuery context with explicit 'default.' stays 'default'
        var q2 = NestedSqlParser.ParseNestedSql("SELECT * FROM default.networks", "ws:q:2", "bq-calc/src/repo.ts");
        Assert.That(q2, Is.Not.Null);
        var dbNode2 = q2!.Children.OfType<DatabaseNode>().FirstOrDefault();
        Assert.That(dbNode2, Is.Not.Null);
        Assert.That(dbNode2!.Name, Is.EqualTo("BigQuery.default"));
        Assert.That(dbNode2.Extensions!["schema"], Is.EqualTo("default"));

        // 3. NestedSqlParser in BigQuery context with explicit 'defaults.' stays 'defaults'
        var q3 = NestedSqlParser.ParseNestedSql("SELECT * FROM defaults.networks", "ws:q:3", "bq-calc/src/repo.ts");
        Assert.That(q3, Is.Not.Null);
        var dbNode3 = q3!.Children.OfType<DatabaseNode>().FirstOrDefault();
        Assert.That(dbNode3, Is.Not.Null);
        Assert.That(dbNode3!.Name, Is.EqualTo("BigQuery.defaults"));
        Assert.That(dbNode3.Extensions!["schema"], Is.EqualTo("defaults"));

        // 4. PostIndexAnalyzer CanonicalizeDatabase BigQuery.default -> BigQuery.default
        var (cName1, _, cKey1) = PostIndexAnalyzer.CanonicalizeDatabase("BigQuery", "analytics", rawSchema: "default");
        Assert.That(cName1, Is.EqualTo("BigQuery.default"));
        Assert.That(cKey1, Is.EqualTo("bigquery:default"));

        var (cName2, _, cKey2) = PostIndexAnalyzer.CanonicalizeDatabase("BigQuery.default", "analytics");
        Assert.That(cName2, Is.EqualTo("BigQuery.default"));
        Assert.That(cKey2, Is.EqualTo("bigquery:default"));
    }
}
