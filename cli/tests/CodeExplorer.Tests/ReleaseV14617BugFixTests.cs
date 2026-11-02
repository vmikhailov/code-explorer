using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Layers;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ReleaseV14617BugFixTests
{
    [Test]
    public void ResourceReconciliationService_StripDatabaseSuffix_StripsExpectedSuffixes()
    {
        ResourceReconciliationService.StripDatabaseSuffix("orderingdb").Should().Be("ordering");
        ResourceReconciliationService.StripDatabaseSuffix("catalogdb").Should().Be("catalog");
        ResourceReconciliationService.StripDatabaseSuffix("identitydb").Should().Be("identity");
        ResourceReconciliationService.StripDatabaseSuffix("webhooksdb").Should().Be("webhooks");
        ResourceReconciliationService.StripDatabaseSuffix("OrderingDB").Should().Be("Ordering");
        ResourceReconciliationService.StripDatabaseSuffix("customers_db").Should().Be("customers");
        ResourceReconciliationService.StripDatabaseSuffix("orders-db").Should().Be("orders");
        ResourceReconciliationService.StripDatabaseSuffix("app_database").Should().Be("app");

        // Preserves short stems (< 3 characters) and engine names
        ResourceReconciliationService.StripDatabaseSuffix("mongodb").Should().Be("mongodb");
        ResourceReconciliationService.StripDatabaseSuffix("cosmosdb").Should().Be("cosmosdb");
        ResourceReconciliationService.StripDatabaseSuffix("db").Should().Be("db");
        ResourceReconciliationService.StripDatabaseSuffix("adb").Should().Be("adb");
    }

    [Test]
    public void CanonicalizeDatabase_OrderingDbAndSchemaOrdering_ProducesIdenticalPostgreSqlOrdering()
    {
        // 1. Config DB node ("OrderingDB" with engine "PostgreSQL")
        var (cfgName, cfgType, cfgKey) = PostIndexAnalyzer.CanonicalizeDatabase(
            rawName: "OrderingDB",
            rawDbType: "relational",
            rawEngine: "PostgreSQL");

        cfgName.Should().Be("PostgreSQL.ordering");
        cfgType.Should().Be("relational");
        cfgKey.Should().Be("postgresql:ordering");

        // 2. EF Core AST DB node ("PostgreSQL.ordering" from HasDefaultSchema("ordering"))
        var (astName, astType, astKey) = PostIndexAnalyzer.CanonicalizeDatabase(
            rawName: "PostgreSQL.ordering",
            rawDbType: "relational",
            rawEngine: "PostgreSQL");

        astName.Should().Be("PostgreSQL.ordering");
        astType.Should().Be("relational");
        astKey.Should().Be("postgresql:ordering");

        // 3. Both produce the identical canonical database ID
        var cfgId = PostIndexAnalyzer.BuildCanonicalDatabaseId("ws:project:Ordering.API:db:OrderingDB", cfgType, cfgKey, "ws");
        var astId = PostIndexAnalyzer.BuildCanonicalDatabaseId("ws:file:OrderingContext.cs:db:ordering", astType, astKey, "ws");

        cfgId.Should().Be(astId);
        cfgId.Should().Be("ws:db:relational:postgresql:ordering");
    }

    [Test]
    public void ResolveDatabaseConsolidations_MergesStemAndStemDbCandidates()
    {
        var candidates = new List<(string Id, string Name, string Engine, string Schema)>
        {
            ("pg:ordering", "PostgreSQL.ordering", "PostgreSQL", "ordering"),
            ("pg:orderingdb", "PostgreSQL.orderingdb", "PostgreSQL", "orderingdb"),
            ("pg:catalog", "PostgreSQL.catalog", "PostgreSQL", "catalog"),
            ("pg:catalogdb", "PostgreSQL.catalogdb", "PostgreSQL", "catalogdb"),
            ("pg:generic", "PostgreSQL", "PostgreSQL", "")
        };

        var remap = PostIndexAnalyzer.ResolveDatabaseConsolidations(candidates);

        remap.Should().ContainKey("pg:orderingdb");
        remap["pg:orderingdb"].Should().Be("pg:ordering");

        remap.Should().ContainKey("pg:catalogdb");
        remap["pg:catalogdb"].Should().Be("pg:catalog");

        remap.Should().ContainKey("pg:generic");
    }

    [Test]
    public async Task CanonicalizeDatabasesInMemoryAsync_OrderingDbConfigAndEfCoreSchema_ConsolidatesToOneDatabaseNode()
    {
        const string wsId = "workspace";
        var ctx = new ParsingContext(wsId, "C:/test", new ResourceReconciliationService());

        var wsNode = new WorkspaceNode(wsId, "TestWs", "C:/test");
        var filesStructure = new FilesStructureNode($"{wsId}:files", "Files", "C:/test");
        var fileNode = new FileNode($"{wsId}:file:OrderingContext.cs", "OrderingContext.cs", "src/Ordering.Infrastructure/OrderingContext.cs", "csharp");
        var l1 = new Layer1Result(wsNode, filesStructure, [fileNode], []);

        var projStructure = new ProjectsStructureNode($"{wsId}:projects", "Projects", "C:/test");
        var svcProject = new ProjectNode($"{wsId}:project:Ordering.API", "Ordering.API", "src/Ordering.API", "csharp")
        {
            Role = "Service"
        };
        var libProject = new ProjectNode($"{wsId}:project:Ordering.Infrastructure", "Ordering.Infrastructure", "src/Ordering.Infrastructure", "csharp")
        {
            Role = "Library"
        };

        var dependsOnRel = new Relationship(svcProject.Id, libProject.Id, OntologyConstants.Relationships.DependsOn);
        var l2 = new Layer2Result(l1, projStructure, [svcProject, libProject], [], [dependsOnRel]);

        var syntaxStructure = new SyntaxStructureNode($"{wsId}:syntax", "Syntax", "C:/test");
        var l3 = new Layer3Result(l2, syntaxStructure, [], [], [], [], [], new Dictionary<(string Kind, string Name), string>());

        var semanticStructure = new SemanticStructureNode($"{wsId}:semantic_structure", "Semantic", "C:/test");
        ctx.SemanticStructure = semanticStructure;

        // 1. Config DB node from Ordering.API
        var configDbNode = new DatabaseNode(
            $"{wsId}:project:Ordering.API:db:OrderingDB",
            "OrderingDB",
            "src/Ordering.API/appsettings.json",
            "relational",
            new Dictionary<string, string>
            {
                ["engine"] = "PostgreSQL",
                ["from_config"] = "true",
                ["config_key"] = "ConnectionStrings:OrderingDB"
            });

        // 2. AST DB node from OrderingContext.cs (EF Core HasDefaultSchema("ordering"))
        var astDbNode = new DatabaseNode(
            $"{wsId}:file:OrderingContext.cs:db:ordering",
            "PostgreSQL.ordering",
            "src/Ordering.Infrastructure/OrderingContext.cs",
            "relational",
            new Dictionary<string, string>
            {
                ["engine"] = "PostgreSQL",
                ["schema"] = "ordering"
            });

        // 3. Table node "ordering.orders" (e.g. from SQL / EF Core entity)
        var tableNode = new TableNode(
            $"{wsId}:table:ordering.orders",
            "ordering.orders",
            "src/Ordering.Infrastructure/OrderingContext.cs",
            new Dictionary<string, string>
            {
                ["schema"] = "ordering",
                ["table"] = "orders"
            });
        astDbNode.Children.Add(tableNode);

        svcProject.Children.Add(configDbNode);
        libProject.Children.Add(astDbNode);

        var allRelationships = new List<Relationship>
        {
            dependsOnRel,
            new(svcProject.Id, configDbNode.Id, OntologyConstants.Relationships.UsesDb),
            new(fileNode.Id, astDbNode.Id, OntologyConstants.Relationships.UsesDb),
            new(astDbNode.Id, tableNode.Id, OntologyConstants.Relationships.Contains)
        };

        var l4 = new Layer4Result(l3, semanticStructure, [configDbNode, astDbNode, tableNode], allRelationships);

        var fileToProject = new Dictionary<string, ProjectNode>(StringComparer.OrdinalIgnoreCase)
        {
            [fileNode.Id] = libProject
        };

        var (canonicalRels, rawToCanonical) = await PostIndexAnalyzer.CanonicalizeDatabasesInMemoryAsync(
            ctx, l4, allRelationships, fileToProject);

        // Verify both raw DB node IDs map to the single canonical database
        const string expectedCanonicalId = $"{wsId}:db:relational:postgresql:ordering";
        rawToCanonical.Should().ContainKey(configDbNode.Id);
        rawToCanonical[configDbNode.Id].Should().Be(expectedCanonicalId);

        rawToCanonical.Should().ContainKey(astDbNode.Id);
        rawToCanonical[astDbNode.Id].Should().Be(expectedCanonicalId);

        // Verify that the canonical DB in semantic structure is singular and contains the table
        var dbNodesInSemantic = semanticStructure.Children.OfType<DatabaseNode>().ToList();
        dbNodesInSemantic.Count.Should().Be(1);

        var canonicalDb = dbNodesInSemantic[0];
        canonicalDb.Id.Should().Be(expectedCanonicalId);
        canonicalDb.Name.Should().Be("PostgreSQL.ordering");
        canonicalDb.Children.OfType<TableNode>().Should().Contain(t => t.Name == "ordering.orders");
    }
}
