using AwesomeAssertions;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ReleaseV14613BugFixTests
{
    [Test]
    public void SyntaxEnricher_DetectDeclaredSchema_DoesNotGuessFromFileNameOrType()
    {
        var file1 = new FileNode("workspace:file:ICustomersDbContext.cs", "ICustomersDbContext.cs", "ICustomersDbContext.cs", "csharp");
        SyntaxEnricher.DetectDeclaredSchema(null, file1, null).Should().BeNull();

        var file2 = new FileNode("workspace:file:CustomersReadDbContext.cs", "CustomersReadDbContext.cs", "CustomersReadDbContext.cs", "csharp");
        SyntaxEnricher.DetectDeclaredSchema(null, file2, null).Should().BeNull();

        var file3 = new FileNode("workspace:file:ICustomerDbContext.cs", "ICustomerDbContext.cs", "ICustomerDbContext.cs", "csharp");
        SyntaxEnricher.DetectDeclaredSchema(null, file3, null).Should().BeNull();

        var file4 = new FileNode("workspace:file:CustomersWriteDbContext.cs", "CustomersWriteDbContext.cs", "CustomersWriteDbContext.cs", "csharp");
        SyntaxEnricher.DetectDeclaredSchema(null, file4, null).Should().BeNull();
    }

    [Test]
    public void ConfigurationParser_InferAndCreateServiceFromConnectionString_DetectsPostgreSQL_NotSqlServer()
    {
        const string connStr = "Server=localhost;Port=5432;Database=customers;User Id=postgres;Password=postgres;Include Error Detail=true";
        const string key = "PostgresOptions:ConnectionString";

        var workspaceId = "test_ws";
        var container = new ProjectNode("test_ws:project:customers", "Customers", "src/Customers", "csharp");
        var relationships = new List<Relationship>();
        var ctx = new ParsingContext("test_ws", "C:/test", new ResourceReconciliationService());

        ConfigurationParser.InferAndCreateServiceFromConnectionString(
            key,
            connStr,
            "appsettings.development.json",
            "file_1",
            workspaceId,
            container,
            relationships,
            ctx);

        var dbNode = container.Children.OfType<DatabaseNode>().FirstOrDefault();
        dbNode.Should().NotBeNull();
        dbNode!.Extensions.Should().ContainKey("engine");
        dbNode.Extensions["engine"].Should().Be("PostgreSQL");
        dbNode.Name.Should().Be("customers");
        dbNode.DbType.Should().Be("relational");
    }

    [Test]
    public void PostIndexAnalyzer_CanonicalizeDatabase_UnifiesConfigAndAstNodes()
    {
        // Config-created PostgreSQL node
        var (configPgName, configPgType, configPgKey) = PostIndexAnalyzer.CanonicalizeDatabase(
            rawName: "customers",
            rawDbType: "relational",
            rawEngine: "PostgreSQL");

        // AST-created PostgreSQL node
        var (astPgName, astPgType, astPgKey) = PostIndexAnalyzer.CanonicalizeDatabase(
            rawName: "PostgreSQL.customers",
            rawDbType: "relational",
            rawEngine: "PostgreSQL");

        configPgName.Should().Be("PostgreSQL.customers");
        astPgName.Should().Be("PostgreSQL.customers");
        configPgKey.Should().Be(astPgKey);

        // Config-created MongoDB node
        var (configMongoName, configMongoType, configMongoKey) = PostIndexAnalyzer.CanonicalizeDatabase(
            rawName: "customers",
            rawDbType: "document",
            rawEngine: "MongoDB");

        // AST-created MongoDB node
        var (astMongoName, astMongoType, astMongoKey) = PostIndexAnalyzer.CanonicalizeDatabase(
            rawName: "MongoDB.customers",
            rawDbType: "document",
            rawEngine: "MongoDB");

        configMongoName.Should().Be("MongoDB.customers");
        astMongoName.Should().Be("MongoDB.customers");
        configMongoKey.Should().Be(astMongoKey);
    }

    [Test]
    public void PostIndexAnalyzer_ResolveDatabaseConsolidations_MergesSingularPluralAndGenericConcrete()
    {
        // 1. Singular vs Plural: PostgreSQL.customer -> PostgreSQL.customers
        var candidatePg = new List<(string Id, string Name, string Engine, string Schema)>
        {
            ("pg:customer", "PostgreSQL.customer", "PostgreSQL", "customer"),
            ("pg:customers", "PostgreSQL.customers", "PostgreSQL", "customers")
        };

        var pgRemap = PostIndexAnalyzer.ResolveDatabaseConsolidations(candidatePg);
        pgRemap.Should().ContainKey("pg:customer");
        pgRemap["pg:customer"].Should().Be("pg:customers");

        // 2. Generic engine vs Concrete schema: MongoDB -> MongoDB.customers
        var candidateMongo = new List<(string Id, string Name, string Engine, string Schema)>
        {
            ("mongo:generic", "MongoDB", "MongoDB", ""),
            ("mongo:customers", "MongoDB.customers", "MongoDB", "customers")
        };

        var mongoRemap = PostIndexAnalyzer.ResolveDatabaseConsolidations(candidateMongo);
        mongoRemap.Should().ContainKey("mongo:generic");
        mongoRemap["mongo:generic"].Should().Be("mongo:customers");
    }
}
