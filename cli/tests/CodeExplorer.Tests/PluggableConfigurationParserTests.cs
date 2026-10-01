using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.Java;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class PluggableConfigurationParserTests
{
    [OneTimeSetUp]
    public void Setup()
    {
        // Ensure JavaParser is registered/loaded
        _ = new JavaParser();
    }

    [Test]
    public void SpringFrameworkDescriptor_HandlesDatasourceProperties()
    {
        var container = new ProjectNode("ws:project:order-service:", "order-service", "services/order-service", "java", "Service", false);
        var relationships = new List<Relationship>();
        var ctx = new ParsingContext("ws", "/workspace", new ResourceReconciliationService());

        var handled = LibraryConfigurationRegistry.TryProcess(
            "spring.datasource.url",
            "jdbc:postgresql://localhost:5432/orders_db",
            "services/order-service/application.properties",
            "ws:file:services/order-service/application.properties",
            "ws",
            container,
            relationships,
            ctx);

        Assert.That(handled, Is.True);
        Assert.That(container.Children.OfType<DatabaseNode>().Any(c => c.Name.Equals("orders_db", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public void SpringFrameworkDescriptor_HandlesRabbitMqHost()
    {
        var container = new ProjectNode("ws:project:worker-service:", "worker-service", "services/worker-service", "java", "Worker", false);
        var relationships = new List<Relationship>();
        var ctx = new ParsingContext("ws", "/workspace", new ResourceReconciliationService());

        var handled = LibraryConfigurationRegistry.TryProcess(
            "spring.rabbitmq.host",
            "rabbitmq.internal",
            "services/worker-service/application.properties",
            "ws:file:services/worker-service/application.properties",
            "ws",
            container,
            relationships,
            ctx);

        Assert.That(handled, Is.True);
        Assert.That(container.Children.OfType<TopicNode>().Any(c => c.Name.Equals("default", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public void SpringFrameworkDescriptor_HandlesYamlDatasourceUrl()
    {
        var container = new ProjectNode("ws:project:inventory:", "inventory", "services/inventory", "java", "Service", false);
        var relationships = new List<Relationship>();
        var ctx = new ParsingContext("ws", "/workspace", new ResourceReconciliationService());

        var handled = LibraryConfigurationRegistry.TryProcess(
            "url",
            "jdbc:mysql://localhost:3306/inventory_db",
            "services/inventory/application.yml",
            "ws:file:services/inventory/application.yml",
            "ws",
            container,
            relationships,
            ctx);

        Assert.That(handled, Is.True);
        Assert.That(container.Children.OfType<DatabaseNode>().Any(c => c.Name.Equals("inventory_db", StringComparison.OrdinalIgnoreCase)), Is.True);
    }
}
