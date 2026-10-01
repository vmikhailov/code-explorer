using CodeExplorer.Cypher.Common;
using NUnit.Framework;

namespace CodeExplorer.Cypher.Tests;

[TestFixture]
public class EnumsTests
{
    [Test]
    public void NodeKind_ToCypherLabel_CoversAllKnownValues()
    {
        Assert.That(NodeKind.Service.ToCypherLabel(), Is.EqualTo("Service"));
        Assert.That(NodeKind.App.ToCypherLabel(), Is.EqualTo("App"));
        Assert.That(NodeKind.Worker.ToCypherLabel(), Is.EqualTo("Worker"));
        Assert.That(NodeKind.CliTool.ToCypherLabel(), Is.EqualTo("CliTool"));
        Assert.That(NodeKind.Library.ToCypherLabel(), Is.EqualTo("Library"));
        Assert.That(NodeKind.SharedLibrary.ToCypherLabel(), Is.EqualTo("SharedLibrary"));
        Assert.That(NodeKind.Database.ToCypherLabel(), Is.EqualTo("Database"));
        Assert.That(NodeKind.Topic.ToCypherLabel(), Is.EqualTo("Topic"));
        Assert.That(NodeKind.ExternalService.ToCypherLabel(), Is.EqualTo("ExternalService"));
        Assert.That(NodeKind.Project.ToCypherLabel(), Is.EqualTo("Project"));
        Assert.That(NodeKind.Endpoint.ToCypherLabel(), Is.EqualTo("Endpoint"));
        Assert.That(NodeKind.EntryPoint.ToCypherLabel(), Is.EqualTo("EntryPoint"));
        Assert.That(NodeKind.Table.ToCypherLabel(), Is.EqualTo("Table"));
        Assert.That(NodeKind.Query.ToCypherLabel(), Is.EqualTo("Query"));
        Assert.That(NodeKind.Type.ToCypherLabel(), Is.EqualTo("Type"));
        Assert.That(NodeKind.Function.ToCypherLabel(), Is.EqualTo("Function"));
        Assert.That(NodeKind.Member.ToCypherLabel(), Is.EqualTo("Member"));
        Assert.That(NodeKind.File.ToCypherLabel(), Is.EqualTo("File"));
        Assert.That(NodeKind.Folder.ToCypherLabel(), Is.EqualTo("Folder"));
        Assert.That(NodeKind.Package.ToCypherLabel(), Is.EqualTo("Package"));
        Assert.That(NodeKind.DataSet.ToCypherLabel(), Is.EqualTo("DataSet"));
        Assert.That(NodeKind.CloudService.ToCypherLabel(), Is.EqualTo("CloudService"));
        Assert.That(NodeKind.ApiInUse.ToCypherLabel(), Is.EqualTo("ApiInUse"));
        Assert.That(NodeKind.TestSuite.ToCypherLabel(), Is.EqualTo("TestSuite"));
        Assert.That(NodeKind.Test.ToCypherLabel(), Is.EqualTo("Test"));
        Assert.That(NodeKind.Procedure.ToCypherLabel(), Is.EqualTo("Procedure"));
        Assert.That(NodeKind.Workspace.ToCypherLabel(), Is.EqualTo("Workspace"));
    }

    [TestCase("Service", NodeKind.Service)]
    [TestCase("service", NodeKind.Service)]
    [TestCase("SERVICE", NodeKind.Service)]
    [TestCase("App", NodeKind.App)]
    [TestCase("FrontendApp", NodeKind.App)]
    [TestCase("ingress", NodeKind.App)]
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
        Assert.That(EnumExtensions.ParseNodeKind(label), Is.EqualTo(expected));
    }

    [Test]
    public void RelationshipKind_ToCypherType_CoversAllKnownValues()
    {
        Assert.That(RelationshipKind.Calls.ToCypherType(), Is.EqualTo("CALLS"));
        Assert.That(RelationshipKind.DependsOn.ToCypherType(), Is.EqualTo("DEPENDS_ON"));
        Assert.That(RelationshipKind.ServiceCall.ToCypherType(), Is.EqualTo("SERVICE_CALL"));
        Assert.That(RelationshipKind.UsesDb.ToCypherType(), Is.EqualTo("USES_DB"));
        Assert.That(RelationshipKind.Produces.ToCypherType(), Is.EqualTo("PRODUCES"));
        Assert.That(RelationshipKind.Consumes.ToCypherType(), Is.EqualTo("CONSUMES"));
        Assert.That(RelationshipKind.CallsEndpoint.ToCypherType(), Is.EqualTo("CALLS_ENDPOINT"));
        Assert.That(RelationshipKind.Contains.ToCypherType(), Is.EqualTo("CONTAINS"));
        Assert.That(RelationshipKind.Implements.ToCypherType(), Is.EqualTo("IMPLEMENTS"));
        Assert.That(RelationshipKind.WritesTo.ToCypherType(), Is.EqualTo("WRITES_TO"));
        Assert.That(RelationshipKind.ReadsFrom.ToCypherType(), Is.EqualTo("READS_FROM"));
        Assert.That(RelationshipKind.Exposes.ToCypherType(), Is.EqualTo("EXPOSES"));
        Assert.That(RelationshipKind.AttributedTo.ToCypherType(), Is.EqualTo("ATTRIBUTED_TO"));
        Assert.That(RelationshipKind.Configures.ToCypherType(), Is.EqualTo("CONFIGURES"));
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
        Assert.That(EnumExtensions.ParseRelationshipKind(rel), Is.EqualTo(expected));
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
        Assert.That(EnumExtensions.ParseProjectRole(role), Is.EqualTo(expected));
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
        Assert.That(EnumExtensions.ParseDatabaseType(dbType), Is.EqualTo(expected));
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
        Assert.That(EnumExtensions.ParseNetworkProtocol(protocol), Is.EqualTo(expected));
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
        Assert.That(EnumExtensions.ParseArchitectureViewType(viewType), Is.EqualTo(expected));
    }
}
