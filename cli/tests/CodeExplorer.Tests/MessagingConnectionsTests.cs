using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class MessagingConnectionsTests
{
    [Test]
    public void Test_FindOwningProject_WithArbitraryWorkspacePrefix()
    {
        var projects = new List<ProjectNode>
        {
            new("custom_ws:project:services/order:", "OrderService", "services/order", "csharp", "Service", false),
            new("custom_ws:project:services/billing:", "BillingService", "services/billing", "csharp", "Service", false)
        };

        // Symbol from OrderService with custom workspace ID
        var symId = "custom_ws:symbol:services/order/Controllers/OrderController.cs:Method:PostOrder:42";
        var owner = PostIndexAnalyzer.FindOwningProjectForId(symId, projects);

        Assert.That(owner, Is.Not.Null);
        Assert.That(owner!.Name, Is.EqualTo("OrderService"));

        // File from BillingService with custom workspace ID
        var fileId = "custom_ws:file:services/billing/Consumers/PaymentConsumer.cs";
        var fileOwner = PostIndexAnalyzer.FindOwningProjectForId(fileId, projects);

        Assert.That(fileOwner, Is.Not.Null);
        Assert.That(fileOwner!.Name, Is.EqualTo("BillingService"));
    }

    [Test]
    public void Test_FindOwningProject_RootProjectFallback()
    {
        var rootProject = new ProjectNode("ws1:project:root:", "RootApp", "", "csharp", "App", false);
        var projects = new List<ProjectNode> { rootProject };

        // Symbol anywhere in root project
        var symId = "ws1:symbol:src/Handlers/EventHandler.cs:Method:Handle:15";
        var owner = PostIndexAnalyzer.FindOwningProjectForId(symId, projects);

        Assert.That(owner, Is.Not.Null);
        Assert.That(owner!.Name, Is.EqualTo("RootApp"));
    }

    [Test]
    public void Test_FindOwningProject_DoesNotFalselyMatchForeignFilesWhenSingleProjectPassed()
    {
        var singleProject = new List<ProjectNode>
        {
            new("ws:project:adhub/adhub-cf-worker:", "adhub-cf-worker", "adhub/adhub-cf-worker", "typescript", "App", false)
        };

        var foreignFileId = "ws:file:cpm-streaming-aggregator/internal/repository/clickhouse_analytics.go";
        var owner = PostIndexAnalyzer.FindOwningProjectForId(foreignFileId, singleProject);

        Assert.That(owner, Is.Null, "Foreign files outside project directory must not match just because single project was passed");
    }

    [Test]
    public void Test_NormalizeEdges_PreservesIncomingAndOutgoingMessaging()
    {
        var (inCat, inDep, inKind) = PostIndexAnalyzer.NormalizeEdgeCategory("TRIGGERS", "Topic", "Project", null, null, false);
        Assert.That(inCat, Is.EqualTo("messaging"));
        Assert.That(inKind, Is.EqualTo("SUBSCRIBES_TO"));
        Assert.That(inDep, Is.EqualTo("messaging"));

        var (outCat, outDep, outKind) = PostIndexAnalyzer.NormalizeEdgeCategory("PUBLISHES_TO", "Project", "Topic", null, null, false);
        Assert.That(outCat, Is.EqualTo("messaging"));
        Assert.That(outKind, Is.EqualTo("PUBLISHES_TO"));
        Assert.That(outDep, Is.EqualTo("messaging"));
    }

    [Test]
    public void Test_LiftTransitiveSemanticRelations_InboundAndOutboundMessagingThroughLibrary()
    {
        var projects = new List<ProjectNode>
        {
            new("service1", "OrderService", "services/order", "csharp", "Service", false),
            new("lib1", "OrderCommonLib", "libs/order-common", "csharp", "SharedLibrary", true)
        };

        var rels = new List<Relationship>
        {
            new("service1", "lib1", OntologyConstants.Relationships.DependsOn, new()),
            new("inTopic", "lib1", OntologyConstants.Relationships.SubscribesTo, new()),
            new("lib1", "outTopic", OntologyConstants.Relationships.PublishesTo, new())
        };

        var lifted = PostIndexAnalyzer.LiftTransitiveSemanticRelations(projects, rels);

        // Verify inbound message lifted to service as SUBSCRIBES_TO
        var liftedIn = lifted.FirstOrDefault(e => e.From == "service1" && e.To == "inTopic");
        Assert.That(liftedIn, Is.Not.Null);
        Assert.That(liftedIn!.Kind, Is.EqualTo("SUBSCRIBES_TO"));
        Assert.That(liftedIn.Properties.GetValueOrDefault("semantic_lifted")?.ToString(), Is.EqualTo("true"));

        // Verify outbound message lifted to service as PUBLISHES_TO
        var liftedOut = lifted.FirstOrDefault(e => e.From == "service1" && e.To == "outTopic");
        Assert.That(liftedOut, Is.Not.Null);
        Assert.That(liftedOut!.Kind, Is.EqualTo("PUBLISHES_TO"));
        Assert.That(liftedOut.Properties.GetValueOrDefault("semantic_lifted")?.ToString(), Is.EqualTo("true"));
    }

    [Test]
    public void Test_MaterializeDirectProjectRelationships_PublishedBy_And_SubscribedBy()
    {
        var proj = new ProjectNode("ws:project:services/publisher:", "PublisherService", "services/publisher", "typescript", "Service", false);
        var file = new FileNode("ws:file:services/publisher/app.ts", "app.ts", "services/publisher/app.ts", "c:\\test\\services\\publisher\\app.ts");
        var symbolId = "ws:symbol:services/publisher/app.ts:Method:send:10";
        var topicPubId = "ws:res:topic:gcp:event-bus-topic";
        var topicSubId = "ws:res:topic:gcp:event-journal-topic";

        var rels = new List<Relationship>
        {
            new(file.Id, symbolId, OntologyConstants.Relationships.Declares, new()),
            new(topicPubId, symbolId, OntologyConstants.Relationships.PublishedBy, new()),
            new(topicSubId, symbolId, OntologyConstants.Relationships.SubscribedBy, new())
        };

        var nodeKinds = new Dictionary<string, string>
        {
            [topicPubId] = OntologyConstants.NodeLabels.Topic,
            [topicSubId] = OntologyConstants.NodeLabels.Topic
        };

        var materialized = PostIndexAnalyzer.MaterializeDirectProjectRelationships(
            [proj],
            [file],
            rels,
            nodeKindsById: nodeKinds
        );

        var pubRel = materialized.FirstOrDefault(r => r.From == proj.Id && r.To == topicPubId && r.Kind == OntologyConstants.Relationships.PublishesTo);
        Assert.That(pubRel, Is.Not.Null, "Expected project-level PUBLISHES_TO edge from publisher project to topic");

        var subRel = materialized.FirstOrDefault(r => r.From == proj.Id && r.To == topicSubId && r.Kind == OntologyConstants.Relationships.SubscribesTo);
        Assert.That(subRel, Is.Not.Null, "Expected project-level SUBSCRIBES_TO edge from publisher project to topic");

        var trigRel = materialized.FirstOrDefault(r => r.Kind == OntologyConstants.Relationships.Triggers);
        Assert.That(trigRel, Is.Null, "Duplicate TRIGGERS edge must not be materialized on macro level");
    }
}

