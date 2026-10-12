using AwesomeAssertions;
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
        id.Should().Be("ws1:res:db:relational:orders_db");

        var specialCharsId = ResourceReconciliationService.BuildCanonicalDatabaseId("ws1", "cache", "my.redis-instance@1");
        specialCharsId.Should().Be("ws1:res:db:cache:my_redis-instance_1");
    }

    [Test]
    public void BuildCanonicalTopicId_FormatsConsistentUrn()
    {
        var id = ResourceReconciliationService.BuildCanonicalTopicId("ws1", "rabbitmq", "orders.created");
        id.Should().Be("ws1:res:topic:rabbitmq:orders.created");
    }

    [Test]
    public void BuildCanonicalServiceId_FormatsConsistentUrn()
    {
        var id = ResourceReconciliationService.BuildCanonicalServiceId("ws1", "external", "api.stripe.com");
        id.Should().Be("ws1:res:service:external:api.stripe.com");
    }

    [Test]
    public void NormalizeResourceName_StripsGenericKeys()
    {
        ResourceReconciliationService.NormalizeResourceName("DefaultConnection", "PostgreSQL").Should().Be("PostgreSQL");
        ResourceReconciliationService.NormalizeResourceName("ConnectionString", "SQL Server").Should().Be("SQL Server");
        ResourceReconciliationService.NormalizeResourceName("spring.datasource.url", "MySQL").Should().Be("MySQL");
        ResourceReconciliationService.NormalizeResourceName("database", "Redis").Should().Be("Redis");

        ResourceReconciliationService.NormalizeResourceName("orders_db", "PostgreSQL").Should().Be("orders_db");
        ResourceReconciliationService.NormalizeResourceName("PaymentServiceDb", "SQL Server").Should().Be("PaymentServiceDb");
        ResourceReconciliationService.NormalizeResourceName("BillingConnectionString", "PostgreSQL").Should().Be("Billing");
        ResourceReconciliationService.NormalizeResourceName("OrdersDbContext", "SQL Server").Should().Be("Orders");
        ResourceReconciliationService.NormalizeResourceName("CatalogConnection", "MySQL").Should().Be("Catalog");
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

        res1.Id.Should().Be("ws1:res:db:relational:orders_db");
        res1.Name.Should().Be("orders_db");

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

        res2.Id.Should().Be(res1.Id, "Must resolve to identical canonical resource ID");
        _service.AllResources.Count.Should().Be(1, "Resource count must remain 1 after re-registration");

        // Lookup by aliases
        var byDefaultConn = _service.ResolveResource("DefaultConnection");
        byDefaultConn.Should().NotBeNull();
        byDefaultConn!.Id.Should().Be(res1.Id);

        var byUrl = _service.ResolveResource("postgres://localhost:5432/orders_db");
        byUrl.Should().NotBeNull();
        byUrl!.Id.Should().Be(res1.Id);

        var byName = _service.ResolveResource("orders_db");
        byName.Should().NotBeNull();
        byName!.Id.Should().Be(res1.Id);
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
        resolved.Should().NotBeNull();
        resolved!.Name.Should().Be("redis_cache");
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
        queryNode.Should().NotBeNull();

        var dbNode = queryNode!.Children.OfType<DatabaseNode>().FirstOrDefault();
        dbNode.Should().NotBeNull();
        dbNode!.Name.Should().Be("orders_db", "SQL query without explicit DB prefix must bind to canonical relational DB");
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

        dbRel.Kind.Should().Be(OntologyConstants.Relationships.UsesDb);
        dbRel.Properties.Should().NotBeNull();
        dbRel.Properties!["via"]?.ToString().Should().Be("TypeORM");
        dbRel.Properties!["provider"]?.ToString().Should().Be("typeorm");
        dbRel.Properties!["is_orm"]?.ToString().Should().Be("true");
    }

    [Test]
    public void CleanProjectNameToDomain_CleansMicroservicePrefixesAndSuffixes()
    {
        SyntaxEnricher.CleanProjectNameToDomain("internal-service-networks").Should().Be("networks");
        SyntaxEnricher.CleanProjectNameToDomain("internal-service-landers").Should().Be("landers");
        SyntaxEnricher.CleanProjectNameToDomain("internal-service-sources").Should().Be("sources");
        SyntaxEnricher.CleanProjectNameToDomain("internal-service-splits").Should().Be("splits");
        SyntaxEnricher.CleanProjectNameToDomain("internal-service-action-scheduler").Should().Be("action-scheduler");
        SyntaxEnricher.CleanProjectNameToDomain("integration-service-postback-partner").Should().Be("postback-partner");
        SyntaxEnricher.CleanProjectNameToDomain("integration-service-smart-cpa").Should().Be("smart-cpa");
        SyntaxEnricher.CleanProjectNameToDomain("internal-bundle-scheduler").Should().Be("bundle-scheduler");
        SyntaxEnricher.CleanProjectNameToDomain("tournament-service").Should().Be("tournament");
        SyntaxEnricher.CleanProjectNameToDomain("orders-api").Should().Be("orders");
        SyntaxEnricher.CleanProjectNameToDomain("Lidoma.Tournament").Should().Be("tournament");
        SyntaxEnricher.CleanProjectNameToDomain("service-billing").Should().Be("billing");
        SyntaxEnricher.CleanProjectNameToDomain("srv-users").Should().Be("users");
        SyntaxEnricher.CleanProjectNameToDomain("internal--service-networks").Should().Be("networks");
        SyntaxEnricher.CleanProjectNameToDomain("integration--smart-cpa").Should().Be("smart-cpa");
    }

    [Test]
    public void NormalizeServiceName_StrictStrippingAndLowercase()
    {
        WorkspaceConventions.NormalizeServiceName("internal--service-networks").Should().Be("networks");
        WorkspaceConventions.NormalizeServiceName("internal--networks").Should().Be("networks");
        WorkspaceConventions.NormalizeServiceName("integration--smart-cpa").Should().Be("smart-cpa");
        WorkspaceConventions.NormalizeServiceName("integration--service-smart-cpa").Should().Be("smart-cpa");
        WorkspaceConventions.NormalizeServiceName("service-billing").Should().Be("billing");
        WorkspaceConventions.NormalizeServiceName("ATSAnalyticsDepartment").Should().Be("atsanalyticsdepartment");
        WorkspaceConventions.NormalizeServiceName("internal-service-networks").Should().Be("networks");
        WorkspaceConventions.NormalizeServiceName("internal-service-action-scheduler").Should().Be("action-scheduler");
        WorkspaceConventions.NormalizeServiceName("internal--service-bff").Should().Be("bff");
        WorkspaceConventions.NormalizeServiceName("service-").Should().Be("service-");
        WorkspaceConventions.NormalizeServiceName("").Should().Be("");
    }

    [Test]
    public void BigQuery_HandlesCanonicalDefaultSchema()
    {
        // 1. NestedSqlParser in BigQuery context without schema defaults to 'default'
        var q1 = NestedSqlParser.ParseNestedSql("SELECT * FROM networks", "ws:q:1", "bq-calc/src/repo.ts");
        q1.Should().NotBeNull();
        var dbNode1 = q1!.Children.OfType<DatabaseNode>().FirstOrDefault();
        dbNode1.Should().NotBeNull();
        dbNode1!.Name.Should().Be("BigQuery.default");
        dbNode1.Extensions!["schema"].Should().Be("default");

        // 2. NestedSqlParser in BigQuery context with explicit 'default.' stays 'default'
        var q2 = NestedSqlParser.ParseNestedSql("SELECT * FROM default.networks", "ws:q:2", "bq-calc/src/repo.ts");
        q2.Should().NotBeNull();
        var dbNode2 = q2!.Children.OfType<DatabaseNode>().FirstOrDefault();
        dbNode2.Should().NotBeNull();
        dbNode2!.Name.Should().Be("BigQuery.default");
        dbNode2.Extensions!["schema"].Should().Be("default");

        // 3. NestedSqlParser in BigQuery context with explicit 'defaults.' stays 'defaults'
        var q3 = NestedSqlParser.ParseNestedSql("SELECT * FROM defaults.networks", "ws:q:3", "bq-calc/src/repo.ts");
        q3.Should().NotBeNull();
        var dbNode3 = q3!.Children.OfType<DatabaseNode>().FirstOrDefault();
        dbNode3.Should().NotBeNull();
        dbNode3!.Name.Should().Be("BigQuery.defaults");
        dbNode3.Extensions!["schema"].Should().Be("defaults");

        // 4. PostIndexAnalyzer CanonicalizeDatabase BigQuery.default -> BigQuery.default
        var (cName1, _, cKey1) = PostIndexAnalyzer.CanonicalizeDatabase("BigQuery", "analytics", rawSchema: "default");
        cName1.Should().Be("BigQuery.default");
        cKey1.Should().Be("bigquery:default");

        var (cName2, _, cKey2) = PostIndexAnalyzer.CanonicalizeDatabase("BigQuery.default", "analytics");
        cName2.Should().Be("BigQuery.default");
        cKey2.Should().Be("bigquery:default");
    }

    [Test]
    public void MultipleServices_DoNotConflateIntoSingleDatabaseEngine()
    {
        var db1 = _service.RegisterResource(
            "ws1",
            "PostgreSQL.action-scheduler",
            "PostgreSQL",
            "relational",
            OntologyConstants.NodeLabels.Database,
            "services/action-scheduler/src/orm.ts",
            projectId: "proj:action-scheduler",
            aliases: ["action-scheduler"]
        );

        var db2 = _service.RegisterResource(
            "ws1",
            "PostgreSQL.billing",
            "PostgreSQL",
            "relational",
            OntologyConstants.NodeLabels.Database,
            "services/billing/src/orm.ts",
            projectId: "proj:billing",
            aliases: ["billing"]
        );

        _service.AllResources.Count.Should().Be(2);
        db1.Id.Should().NotBe(db2.Id);

        // Looking up by specific schema/alias resolves to correct DB
        var resolvedAction = _service.ResolveResource("action-scheduler");
        resolvedAction.Should().NotBeNull();
        resolvedAction!.Id.Should().Be(db1.Id);

        var resolvedBilling = _service.ResolveResource("billing");
        resolvedBilling.Should().NotBeNull();
        resolvedBilling!.Id.Should().Be(db2.Id);

        // Generic engine lookup must NOT guess or collapse when multiple databases exist
        var ambiguous = _service.ResolveResource("PostgreSQL");
        ambiguous.Should().BeNull("Ambiguous engine lookup across multiple databases must not guess");

        var ambiguousWithExpected = _service.ResolveResource(null, expectedDbType: "relational", expectedEngine: "PostgreSQL");
        ambiguousWithExpected.Should().BeNull("Ambiguous engine lookup across multiple databases must not guess");
    }

    [Test]
    public void GenericPlaceholders_AreNotStolenAcrossProjects()
    {
        var placeholderA = _service.RegisterResource(
            "ws1",
            "Database",
            "relational",
            "relational",
            OntologyConstants.NodeLabels.Database,
            "services/service-a/src/repo.ts",
            projectId: "proj:service-a"
        );

        var concreteB = _service.RegisterResource(
            "ws1",
            "PostgreSQL.service-b",
            "PostgreSQL",
            "relational",
            OntologyConstants.NodeLabels.Database,
            "services/service-b/src/orm.ts",
            projectId: "proj:service-b",
            aliases: ["service-b"]
        );

        _service.AllResources.Count.Should().Be(2, "Concrete DB in project B must not retire placeholder in project A");
        _service.ResolveResource(placeholderA.Id).Should().Be(placeholderA);
        _service.ResolveResource("service-b").Should().Be(concreteB);
    }
}
