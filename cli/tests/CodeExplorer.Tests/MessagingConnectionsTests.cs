using CodeExplorer.Core.Protocol;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class MessagingConnectionsTests
{
    [Test]
    public void Test_FindOwningProject_WithArbitraryWorkspacePrefix()
    {
        var projects = new List<GraphNodeDto>
        {
            new() { Id = "custom_ws:project:services/order:", Name = "OrderService", FilePath = "services/order" },
            new() { Id = "custom_ws:project:services/billing:", Name = "BillingService", FilePath = "services/billing" }
        };

        // Symbol from OrderService with custom workspace ID
        var symId = "custom_ws:symbol:services/order/Controllers/OrderController.cs:Method:PostOrder:42";
        var owner = GraphDataConverter.FindOwningProject(symId, projects);

        Assert.That(owner, Is.Not.Null);
        Assert.That(owner!.Name, Is.EqualTo("OrderService"));

        // File from BillingService with custom workspace ID
        var fileId = "custom_ws:file:services/billing/Consumers/PaymentConsumer.cs";
        var fileOwner = GraphDataConverter.FindOwningProject(fileId, projects);

        Assert.That(fileOwner, Is.Not.Null);
        Assert.That(fileOwner!.Name, Is.EqualTo("BillingService"));
    }

    [Test]
    public void Test_FindOwningProject_RootProjectFallback()
    {
        var rootProject = new GraphNodeDto
        {
            Id = "ws1:project:root:",
            Name = "RootApp",
            FilePath = ""
        };

        var projects = new List<GraphNodeDto> { rootProject };

        // Symbol anywhere in root project
        var symId = "ws1:symbol:src/Handlers/EventHandler.cs:Method:Handle:15";
        var owner = GraphDataConverter.FindOwningProject(symId, projects);

        Assert.That(owner, Is.Not.Null);
        Assert.That(owner!.Name, Is.EqualTo("RootApp"));
    }

    [Test]
    public void Test_NormalizeEdges_PreservesIncomingAndOutgoingMessaging()
    {
        var graph = new GraphDataDto
        {
            Nodes = new List<GraphNodeDto>
            {
                new() { Id = "proj1", Kind = "Project", Name = "OrderService" },
                new() { Id = "topic1", Kind = "Topic", Name = "orders.v1" }
            },
            Edges = new List<GraphEdgeDto>
            {
                // Inbound to project from topic
                new()
                {
                    Id = "topic1->proj1:TRIGGERS",
                    Source = "topic1",
                    Target = "proj1",
                    Kind = "TRIGGERS",
                    Category = "messaging"
                },
                // Outbound from project to topic
                new()
                {
                    Id = "proj1->topic1:TRIGGERS",
                    Source = "proj1",
                    Target = "topic1",
                    Kind = "TRIGGERS",
                    Category = "messaging"
                }
            }
        };

        GraphDataConverter.NormalizeEdges(graph);

        var inEdge = graph.Edges.FirstOrDefault(e => e.Source == "topic1" && e.Target == "proj1");
        Assert.That(inEdge, Is.Not.Null);
        Assert.That(inEdge!.Category, Is.EqualTo("messaging"));
        Assert.That(inEdge.Kind, Is.EqualTo("TRIGGERS"));
        Assert.That(inEdge.Properties!["dependency_type"], Is.EqualTo("messaging"));

        var outEdge = graph.Edges.FirstOrDefault(e => e.Source == "proj1" && e.Target == "topic1");
        Assert.That(outEdge, Is.Not.Null);
        Assert.That(outEdge!.Category, Is.EqualTo("messaging"));
        Assert.That(outEdge.Kind, Is.EqualTo("TRIGGERS"));
        Assert.That(outEdge.Properties!["dependency_type"], Is.EqualTo("messaging"));
    }

    [Test]
    public void Test_LiftTransitiveSemanticRelations_InboundAndOutboundMessagingThroughLibrary()
    {
        var graph = new GraphDataDto
        {
            Nodes = new List<GraphNodeDto>
            {
                new() { Id = "service1", Kind = "Project", Name = "OrderService", Properties = new() { ["project_type"] = "service" } },
                new() { Id = "lib1", Kind = "Project", Name = "OrderCommonLib", Properties = new() { ["is_library"] = "true", ["project_type"] = "library" } },
                new() { Id = "inTopic", Kind = "Topic", Name = "events.orders.in" },
                new() { Id = "outTopic", Kind = "Topic", Name = "events.orders.out" }
            },
            Edges = new List<GraphEdgeDto>
            {
                // Service uses Library
                new() { Id = "s->lib", Source = "service1", Target = "lib1", Kind = "LIBRARY", Category = "library" },
                // Topic connects into Library (subscriber in library)
                new() { Id = "in->lib", Source = "inTopic", Target = "lib1", Kind = "TRIGGERS", Category = "messaging" },
                // Library connects to Topic (publisher in library)
                new() { Id = "lib->out", Source = "lib1", Target = "outTopic", Kind = "TRIGGERS", Category = "messaging" }
            }
        };

        GraphDataConverter.LiftTransitiveSemanticRelations(graph);

        // Verify inbound message lifted to service
        var liftedIn = graph.Edges.FirstOrDefault(e => e.Source == "inTopic" && e.Target == "service1");
        Assert.That(liftedIn, Is.Not.Null);
        Assert.That(liftedIn!.Kind, Is.EqualTo("TRIGGERS"));
        Assert.That(liftedIn.Category, Is.EqualTo("messaging"));
        Assert.That(liftedIn.Properties, Is.Not.Null);
        Assert.That(liftedIn.Properties!.GetValueOrDefault("semantic_lifted"), Is.EqualTo("true"));

        // Verify outbound message lifted to service
        var liftedOut = graph.Edges.FirstOrDefault(e => e.Source == "service1" && e.Target == "outTopic");
        Assert.That(liftedOut, Is.Not.Null);
        Assert.That(liftedOut!.Kind, Is.EqualTo("TRIGGERS"));
        Assert.That(liftedOut.Category, Is.EqualTo("messaging"));
        Assert.That(liftedOut.Properties, Is.Not.Null);
        Assert.That(liftedOut.Properties!.GetValueOrDefault("semantic_lifted"), Is.EqualTo("true"));
    }
}
