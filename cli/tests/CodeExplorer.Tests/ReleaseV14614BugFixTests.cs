using AwesomeAssertions;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ReleaseV14614BugFixTests
{
    [Test]
    public void ConfigurationParser_StampsFromConfigAndConfigKey()
    {
        const string connStr = "Server=localhost;Port=5432;Database=customers;User Id=postgres;Password=postgres;";
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
        dbNode!.Extensions.Should().ContainKey("from_config");
        dbNode.Extensions["from_config"].Should().Be("true");
        dbNode.Extensions.Should().ContainKey("config_key");
        dbNode.Extensions["config_key"].Should().Be(key);
    }

    [Test]
    public void IsAspireOrchestrator_DetectsAppHost()
    {
        ArchitectureViewEngine.IsAspireOrchestrator("FoodDelivery.AppHost").Should().BeTrue();
        ArchitectureViewEngine.IsAspireOrchestrator("AppHost").Should().BeTrue();
        ArchitectureViewEngine.IsAspireOrchestrator("MyService.AppHost.csproj").Should().BeTrue();
        ArchitectureViewEngine.IsAspireOrchestrator("CustomerService").Should().BeFalse();
    }
}
