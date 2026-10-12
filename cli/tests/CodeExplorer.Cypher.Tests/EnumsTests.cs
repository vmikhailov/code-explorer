using AwesomeAssertions;
using CodeExplorer.Cypher.Common;
using NUnit.Framework;

namespace CodeExplorer.Cypher.Tests;

[TestFixture]
public class EnumsTests
{
    [Test]
    public void NodeKind_ToCypherLabel_CoversAllKnownValues()
    {
        NodeKind.Service.ToCypherLabel().Should().Be("Service");
        NodeKind.App.ToCypherLabel().Should().Be("App");
        NodeKind.Worker.ToCypherLabel().Should().Be("Worker");
        NodeKind.CliTool.ToCypherLabel().Should().Be("CliTool");
        NodeKind.Library.ToCypherLabel().Should().Be("Library");
        NodeKind.SharedLibrary.ToCypherLabel().Should().Be("SharedLibrary");
        NodeKind.Database.ToCypherLabel().Should().Be("Database");
        NodeKind.Topic.ToCypherLabel().Should().Be("Topic");
        NodeKind.ExternalService.ToCypherLabel().Should().Be("ExternalService");
        NodeKind.Project.ToCypherLabel().Should().Be("Project");
        NodeKind.Endpoint.ToCypherLabel().Should().Be("Endpoint");
        NodeKind.EntryPoint.ToCypherLabel().Should().Be("EntryPoint");
        NodeKind.Table.ToCypherLabel().Should().Be("Table");
        NodeKind.Query.ToCypherLabel().Should().Be("Query");
        NodeKind.Type.ToCypherLabel().Should().Be("Type");
        NodeKind.Function.ToCypherLabel().Should().Be("Function");
        NodeKind.Member.ToCypherLabel().Should().Be("Member");
        NodeKind.File.ToCypherLabel().Should().Be("File");
        NodeKind.Folder.ToCypherLabel().Should().Be("Folder");
        NodeKind.Package.ToCypherLabel().Should().Be("Package");
        NodeKind.DataSet.ToCypherLabel().Should().Be("DataSet");
        NodeKind.CloudService.ToCypherLabel().Should().Be("CloudService");
        NodeKind.ApiInUse.ToCypherLabel().Should().Be("ApiInUse");
        NodeKind.TestSuite.ToCypherLabel().Should().Be("TestSuite");
        NodeKind.Test.ToCypherLabel().Should().Be("Test");
        NodeKind.Procedure.ToCypherLabel().Should().Be("Procedure");
        NodeKind.Workspace.ToCypherLabel().Should().Be("Workspace");
        NodeKind.Domain.ToCypherLabel().Should().Be("Domain");
        NodeKind.BoundedContext.ToCypherLabel().Should().Be("BoundedContext");
        NodeKind.Ingress.ToCypherLabel().Should().Be("Ingress");
    }

    [TestCase("Domain", NodeKind.Domain)]
    [TestCase("domain", NodeKind.Domain)]
    [TestCase("BoundedContext", NodeKind.BoundedContext)]
    [TestCase("boundedcontext", NodeKind.BoundedContext)]
    [TestCase("context", NodeKind.BoundedContext)]
    [TestCase("Ingress", NodeKind.Ingress)]
    [TestCase("ingress", NodeKind.Ingress)]
    [TestCase("Service", NodeKind.Service)]
    [TestCase("service", NodeKind.Service)]
    [TestCase("SERVICE", NodeKind.Service)]
    [TestCase("App", NodeKind.App)]
    [TestCase("FrontendApp", NodeKind.App)]
    [TestCase("Worker", NodeKind.Worker)]
    [TestCase("worker", NodeKind.Worker)]
    [TestCase("CliTool", NodeKind.CliTool)]
    [TestCase("cli", NodeKind.CliTool)]
    [TestCase("Library", NodeKind.Library)]
    [TestCase("SharedLibrary", NodeKind.SharedLibrary)]
    [TestCase("Database", NodeKind.Database)]
    [TestCase("db", NodeKind.Database)]
    [TestCase("Topic", NodeKind.Topic)]
    [TestCase("queue", NodeKind.Topic)]
    [TestCase("broker", NodeKind.Topic)]
    [TestCase("ExternalService", NodeKind.ExternalService)]
    [TestCase("external", NodeKind.ExternalService)]
    [TestCase("Endpoint", NodeKind.Endpoint)]
    [TestCase("EntryPoint", NodeKind.EntryPoint)]
    [TestCase("Table", NodeKind.Table)]
    [TestCase("Query", NodeKind.Query)]
    [TestCase("Type", NodeKind.Type)]
    [TestCase("class", NodeKind.Type)]
    [TestCase("interface", NodeKind.Type)]
    [TestCase("Function", NodeKind.Function)]
    [TestCase("method", NodeKind.Function)]
    [TestCase("Member", NodeKind.Member)]
    [TestCase("field", NodeKind.Member)]
    [TestCase("property", NodeKind.Member)]
    [TestCase("File", NodeKind.File)]
    [TestCase("Folder", NodeKind.Folder)]
    [TestCase("Package", NodeKind.Package)]
    [TestCase("DataSet", NodeKind.DataSet)]
    [TestCase("CloudService", NodeKind.CloudService)]
    [TestCase("ApiInUse", NodeKind.ApiInUse)]
    [TestCase("TestSuite", NodeKind.TestSuite)]
    [TestCase("Test", NodeKind.Test)]
    [TestCase("Procedure", NodeKind.Procedure)]
    [TestCase("Workspace", NodeKind.Workspace)]
    [TestCase("", NodeKind.Unspecified)]
    [TestCase(null, NodeKind.Unspecified)]
    [TestCase("NonExistentLabel", NodeKind.Unspecified)]
    public void ParseNodeKind_MapsCorrectly(string? label, NodeKind expected)
    {
        EnumExtensions.ParseNodeKind(label).Should().Be(expected);
    }

    [Test]
    public void RelationshipKind_ToCypherType_CoversAllKnownValues()
    {
        RelationshipKind.Calls.ToCypherType().Should().Be("CALLS");
        RelationshipKind.DependsOn.ToCypherType().Should().Be("DEPENDS_ON");
        RelationshipKind.ServiceCall.ToCypherType().Should().Be("SERVICE_CALL");
        RelationshipKind.UsesDb.ToCypherType().Should().Be("USES_DB");
        RelationshipKind.Produces.ToCypherType().Should().Be("PRODUCES");
        RelationshipKind.Consumes.ToCypherType().Should().Be("CONSUMES");
        RelationshipKind.CallsEndpoint.ToCypherType().Should().Be("CALLS_ENDPOINT");
        RelationshipKind.Contains.ToCypherType().Should().Be("CONTAINS");
        RelationshipKind.Implements.ToCypherType().Should().Be("IMPLEMENTS");
        RelationshipKind.WritesTo.ToCypherType().Should().Be("WRITES_TO");
        RelationshipKind.ReadsFrom.ToCypherType().Should().Be("READS_FROM");
        RelationshipKind.Exposes.ToCypherType().Should().Be("EXPOSES");
        RelationshipKind.AttributedTo.ToCypherType().Should().Be("ATTRIBUTED_TO");
        RelationshipKind.Configures.ToCypherType().Should().Be("CONFIGURES");
    }

    [TestCase("CALLS", RelationshipKind.Calls)]
    [TestCase("calls", RelationshipKind.Calls)]
    [TestCase("DEPENDS_ON", RelationshipKind.DependsOn)]
    [TestCase("dependson", RelationshipKind.DependsOn)]
    [TestCase("SERVICE_CALL", RelationshipKind.ServiceCall)]
    [TestCase("servicecall", RelationshipKind.ServiceCall)]
    [TestCase("USES_DB", RelationshipKind.UsesDb)]
    [TestCase("usesdb", RelationshipKind.UsesDb)]
    [TestCase("PRODUCES", RelationshipKind.Produces)]
    [TestCase("CONSUMES", RelationshipKind.Consumes)]
    [TestCase("CALLS_ENDPOINT", RelationshipKind.CallsEndpoint)]
    [TestCase("CONTAINS", RelationshipKind.Contains)]
    [TestCase("IMPLEMENTS", RelationshipKind.Implements)]
    [TestCase("WRITES_TO", RelationshipKind.WritesTo)]
    [TestCase("READS_FROM", RelationshipKind.ReadsFrom)]
    [TestCase("EXPOSES", RelationshipKind.Exposes)]
    [TestCase("ATTRIBUTED_TO", RelationshipKind.AttributedTo)]
    [TestCase("CONFIGURES", RelationshipKind.Configures)]
    [TestCase("", RelationshipKind.Unspecified)]
    [TestCase(null, RelationshipKind.Unspecified)]
    [TestCase("UNKNOWN_REL", RelationshipKind.Unspecified)]
    public void ParseRelationshipKind_MapsCorrectly(string? rel, RelationshipKind expected)
    {
        EnumExtensions.ParseRelationshipKind(rel).Should().Be(expected);
    }

    [TestCase("service", ProjectRole.Service)]
    [TestCase("app", ProjectRole.App)]
    [TestCase("ingress", ProjectRole.App)]
    [TestCase("worker", ProjectRole.Worker)]
    [TestCase("cli", ProjectRole.CliTool)]
    [TestCase("library", ProjectRole.SharedLibrary)]
    [TestCase("sharedlibrary", ProjectRole.SharedLibrary)]
    [TestCase("test", ProjectRole.Test)]
    [TestCase("general", ProjectRole.General)]
    [TestCase("", ProjectRole.Unspecified)]
    [TestCase(null, ProjectRole.Unspecified)]
    public void ParseProjectRole_MapsCorrectly(string? role, ProjectRole expected)
    {
        EnumExtensions.ParseProjectRole(role).Should().Be(expected);
    }

    [TestCase("relational", DatabaseType.Relational)]
    [TestCase("sql", DatabaseType.Relational)]
    [TestCase("rdbms", DatabaseType.Relational)]
    [TestCase("document", DatabaseType.Document)]
    [TestCase("mongodb", DatabaseType.Document)]
    [TestCase("nosql", DatabaseType.Document)]
    [TestCase("keyvalue", DatabaseType.KeyValue)]
    [TestCase("kv", DatabaseType.KeyValue)]
    [TestCase("search", DatabaseType.Search)]
    [TestCase("elasticsearch", DatabaseType.Search)]
    [TestCase("cache", DatabaseType.Cache)]
    [TestCase("redis", DatabaseType.Cache)]
    [TestCase("vector", DatabaseType.Vector)]
    [TestCase("graph", DatabaseType.Graph)]
    [TestCase("", DatabaseType.Unspecified)]
    [TestCase(null, DatabaseType.Unspecified)]
    public void ParseDatabaseType_MapsCorrectly(string? dbType, DatabaseType expected)
    {
        EnumExtensions.ParseDatabaseType(dbType).Should().Be(expected);
    }

    [TestCase("http", NetworkProtocol.Http)]
    [TestCase("https", NetworkProtocol.Https)]
    [TestCase("grpc", NetworkProtocol.Grpc)]
    [TestCase("ws", NetworkProtocol.Ws)]
    [TestCase("wss", NetworkProtocol.Wss)]
    [TestCase("amqp", NetworkProtocol.Amqp)]
    [TestCase("kafka", NetworkProtocol.Kafka)]
    [TestCase("soap", NetworkProtocol.Soap)]
    [TestCase("", NetworkProtocol.Unspecified)]
    [TestCase(null, NetworkProtocol.Unspecified)]
    public void ParseNetworkProtocol_MapsCorrectly(string? protocol, NetworkProtocol expected)
    {
        EnumExtensions.ParseNetworkProtocol(protocol).Should().Be(expected);
    }

    [TestCase("systemcontext", ArchitectureViewType.SystemContext)]
    [TestCase("c1", ArchitectureViewType.SystemContext)]
    [TestCase("domainarchitecture", ArchitectureViewType.DomainArchitecture)]
    [TestCase("domain", ArchitectureViewType.DomainArchitecture)]
    [TestCase("macro", ArchitectureViewType.DomainArchitecture)]
    [TestCase("serviceflow", ArchitectureViewType.ServiceFlow)]
    [TestCase("c2", ArchitectureViewType.ServiceFlow)]
    [TestCase("boundedcontexts", ArchitectureViewType.BoundedContexts)]
    [TestCase("tiered", ArchitectureViewType.Tiered)]
    [TestCase("nodegrid", ArchitectureViewType.NodeGrid)]
    [TestCase("grid", ArchitectureViewType.NodeGrid)]
    [TestCase("", ArchitectureViewType.Unspecified)]
    [TestCase(null, ArchitectureViewType.Unspecified)]
    public void ParseArchitectureViewType_MapsCorrectly(string? viewType, ArchitectureViewType expected)
    {
        EnumExtensions.ParseArchitectureViewType(viewType).Should().Be(expected);
    }
}
