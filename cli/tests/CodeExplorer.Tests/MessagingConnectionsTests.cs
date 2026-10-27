using CodeExplorer.Core.Analysis;
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

        owner.Should().NotBeNull();
        owner!.Name.Should().Be("OrderService");

        // File from BillingService with custom workspace ID
        var fileId = "custom_ws:file:services/billing/Consumers/PaymentConsumer.cs";
        var fileOwner = PostIndexAnalyzer.FindOwningProjectForId(fileId, projects);

        fileOwner.Should().NotBeNull();
        fileOwner!.Name.Should().Be("BillingService");
    }

    [Test]
    public void Test_FindOwningProject_RootProjectFallback()
    {
        var rootProject = new ProjectNode("ws1:project:root:", "RootApp", "", "csharp", "App", false);
        var projects = new List<ProjectNode> { rootProject };

        // Symbol anywhere in root project
        var symId = "ws1:symbol:src/Handlers/EventHandler.cs:Method:Handle:15";
        var owner = PostIndexAnalyzer.FindOwningProjectForId(symId, projects);

        owner.Should().NotBeNull();
        owner!.Name.Should().Be("RootApp");
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

        owner.Should().BeNull("Foreign files outside project directory must not match just because single project was passed");
    }

    [Test]
    public void Test_FindOwningProject_DoesNotMatchPackageNodes_EvenWithRootProject()
    {
        var rootProject = new ProjectNode("ws:project:root:", "RootApp", "", "csharp", "App", false);
        var projects = new List<ProjectNode> { rootProject };

        var packageId = "ws:pkg:gradle:com.android.tools:desugar_jdk_libs";
        var owner = PostIndexAnalyzer.FindOwningProjectForId(packageId, projects);

        owner.Should().BeNull("Package URNs must never be resolved to an internal project");

        var graphProjects = new List<Core.Protocol.GraphNodeDto>
        {
            new() { Id = "ws:project:root:", Name = "RootApp", FilePath = "" }
        };
        var graphOwner = ArchitectureViewEngine.FindOwningProject(packageId, graphProjects);
        graphOwner.Should().BeNull("ArchitectureViewEngine must never map a package node to an internal project");
    }

    [Test]
    public void Test_FindOwningProject_DoesNotMatchExternalServices_EvenWithRootProject()
    {
        var rootProject = new ProjectNode("ws:project:root:", "RootApp", "", "csharp", "App", false);
        var projects = new List<ProjectNode> { rootProject };

        var extId = "ws:externalservice:http:api.stripe.com";
        var owner = PostIndexAnalyzer.FindOwningProjectForId(extId, projects);
        owner.Should().BeNull("External services must never be resolved to an internal project");

        var graphProjects = new List<Core.Protocol.GraphNodeDto>
        {
            new() { Id = "ws:project:root:", Name = "RootApp", FilePath = "" }
        };
        var graphOwner = ArchitectureViewEngine.FindOwningProject(extId, graphProjects);
        graphOwner.Should().BeNull();
    }

    [Test]
    public void Test_FindMatchingServiceProject_RejectsAppAndApiKeywords()
    {
        var projects = new List<ProjectNode>
        {
            new("ws:project:flutter/android/app:", "HearAI", "flutter/android/app", "kotlin", "App", false),
            new("ws:project:backend:", "Backend", "backend", "python", "Service", false)
        };

        var appMatch = PostIndexAnalyzer.FindMatchingServiceProject("app", projects);
        appMatch.Should().BeNull("Keyword 'app' must never match an internal app subfolder");

        var apiMatch = PostIndexAnalyzer.FindMatchingServiceProject("api", projects);
        apiMatch.Should().BeNull("Keyword 'api' must never match generic projects");

        PostIndexAnalyzer.IsGarbageExternalService("app").Should().BeTrue();
    }

    [Test]
    public void Test_NormalizeEdges_PreservesIncomingAndOutgoingMessaging()
    {
        var (inCat, inDep, inKind) = PostIndexAnalyzer.NormalizeEdgeCategory("TRIGGERS", "Topic", "Project", null, null, false);
        inCat.Should().Be(DependencyCategory.Messaging);
        inKind.Should().Be("SUBSCRIBED_BY");
        inDep.Should().Be("messaging");

        var (outCat, outDep, outKind) = PostIndexAnalyzer.NormalizeEdgeCategory("PUBLISHES_TO", "Project", "Topic", null, null, false);
        outCat.Should().Be(DependencyCategory.Messaging);
        outKind.Should().Be("PUBLISHES_TO");
        outDep.Should().Be("messaging");
    }

    [Test]
    public void Test_NormalizeEdges_PackageDependency_AlwaysClassifiedAsLibrary()
    {
        // 1. Target is Package, no existing properties
        var (cat1, dep1, kind1) = PostIndexAnalyzer.NormalizeEdgeCategory("DEPENDS_ON", "Project", "Package", null, null, false);
        cat1.Should().Be(DependencyCategory.Library);
        dep1.Should().Be("library");
        kind1.Should().Be("DEPENDS_ON");

        // 2. Target is Package, even if existingCategory was mistakenly 'service_call'
        var (cat2, dep2, kind2) = PostIndexAnalyzer.NormalizeEdgeCategory("DEPENDS_ON", "Project", "Package", "service_call", "service_call", false);
        cat2.Should().Be(DependencyCategory.Library);
        dep2.Should().Be("library");
        kind2.Should().Be("DEPENDS_ON");

        // 3. isTargetLibrary is true, existing category is empty
        var (cat3, dep3, kind3) = PostIndexAnalyzer.NormalizeEdgeCategory("DEPENDS_ON", "Project", "Project", null, null, true);
        cat3.Should().Be(DependencyCategory.Library);
        dep3.Should().Be("library");
        kind3.Should().Be("DEPENDS_ON");

        // 4. existingDepType is 'package'
        var (cat4, dep4, kind4) = PostIndexAnalyzer.NormalizeEdgeCategory("DEPENDS_ON", "Project", null, null, "package", false);
        cat4.Should().Be(DependencyCategory.Library);
        dep4.Should().Be("library");
        kind4.Should().Be("DEPENDS_ON");
    }

    [Test]
    public void Test_NormalizeEdges_UnidentifiedDependency_FallbackDoesNotMasqueradeAsServiceCall()
    {
        // Inter-project dependency without HTTP/API evidence must fall back to UnidentifiedDependency, not ServiceCall
        var (cat, dep, kind) = PostIndexAnalyzer.NormalizeEdgeCategory("DEPENDS_ON", "Project", "Project", null, null, false);
        cat.Should().Be(DependencyCategory.UnidentifiedDependency);
        dep.Should().Be("unidentified_dependency");
        kind.Should().Be("DEPENDS_ON");

        var (unknownCat, unknownDep, unknownKind) = PostIndexAnalyzer.NormalizeEdgeCategory("SOME_UNKNOWN_EDGE", "Project", "Project", null, null, false);
        unknownCat.Should().Be(DependencyCategory.UnidentifiedDependency);
        unknownDep.Should().Be("unidentified_dependency");
        unknownKind.Should().Be("DEPENDS_ON");
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
        liftedIn.Should().NotBeNull();
        liftedIn!.Kind.Should().Be("SUBSCRIBES_TO");
        liftedIn.Properties.GetValueOrDefault("semantic_lifted")?.ToString().Should().Be("true");

        // Verify outbound message lifted to service as PUBLISHES_TO
        var liftedOut = lifted.FirstOrDefault(e => e.From == "service1" && e.To == "outTopic");
        liftedOut.Should().NotBeNull();
        liftedOut!.Kind.Should().Be("PUBLISHES_TO");
        liftedOut.Properties.GetValueOrDefault("semantic_lifted")?.ToString().Should().Be("true");
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
        pubRel.Should().NotBeNull("Expected project-level PUBLISHES_TO edge from publisher project to topic");

        var subRel = materialized.FirstOrDefault(r => r.From == proj.Id && r.To == topicSubId && r.Kind == OntologyConstants.Relationships.SubscribesTo);
        subRel.Should().NotBeNull("Expected project-level SUBSCRIBES_TO edge from publisher project to topic");

        var trigRel = materialized.FirstOrDefault(r => r.Kind == OntologyConstants.Relationships.Triggers);
        trigRel.Should().BeNull("Duplicate TRIGGERS edge must not be materialized on macro level");
    }

    [Test]
    public async Task AtsPubSubSampleProject_ExtractsAllSubscriptionsAndMatchesTopics_WithoutFalsePublishEdges()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        string? sampleProjectsRoot = null;
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "cli", "tests", "SampleProjects");
            if (Directory.Exists(candidate))
            {
                sampleProjectsRoot = candidate;
                break;
            }
            var direct = Path.Combine(dir.FullName, "SampleProjects");
            if (Directory.Exists(direct))
            {
                sampleProjectsRoot = direct;
                break;
            }
            dir = dir.Parent;
        }

        sampleProjectsRoot.Should().NotBeNull("SampleProjects root must be found");
        var workspacePath = Path.Combine(sampleProjectsRoot!, "typescript", "ats-pubsub-workspace");
        Directory.Exists(workspacePath).Should().BeTrue($"Workspace path must exist: {workspacePath}");

        var tempDir = Path.Combine(Path.GetTempPath(), "ats_pubsub_e2e_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var dbPath = Path.Combine(tempDir, "graph.db").Replace('\\', '/');
            await using var client = new SqliteGraphClient(dbPath);
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(workspacePath, clear: false);

            var engine = new ArchitectureViewEngine(client);
            var domain = await engine.GetDomainArchitectureAsync();

            // 1. Query raw graph edges for publisher and subscriber projects
            var queryResult = await client.ExecuteQueryAsync(
                """
                MATCH (s)-[r:PUBLISHES_TO|SUBSCRIBES_TO|SUBSCRIBED_BY]->(t:Topic)
                RETURN s.name AS service, type(r) AS rel, t.name AS topic
                """
            );

            using var doc = System.Text.Json.JsonDocument.Parse(queryResult);
            var edgeList = new List<(string Service, string Rel, string Topic)>();
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var elem in doc.RootElement.EnumerateArray())
                {
                    var s = elem.GetProperty("service").GetString() ?? "";
                    var r = elem.GetProperty("rel").GetString() ?? "";
                    var t = elem.GetProperty("topic").GetString() ?? "";
                    edgeList.Add((s, r, t));
                }
            }

            // Also check undirected SUBSCRIBES edges
            var reverseQuery = await client.ExecuteQueryAsync(
                """
                MATCH (t:Topic)-[r:SUBSCRIBES_TO|SUBSCRIBED_BY]->(s)
                RETURN s.name AS service, type(r) AS rel, t.name AS topic
                """
            );
            using var revDoc = System.Text.Json.JsonDocument.Parse(reverseQuery);
            if (revDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var elem in revDoc.RootElement.EnumerateArray())
                {
                    var s = elem.GetProperty("service").GetString() ?? "";
                    var r = elem.GetProperty("rel").GetString() ?? "";
                    var t = elem.GetProperty("topic").GetString() ?? "";
                    edgeList.Add((s, r, t));
                }
            }
            TestContext.WriteLine("ALL EDGES: " + string.Join(", ", edgeList.Select(e => $"{e.Service} -{e.Rel}-> {e.Topic}")));

            // Assert 1: publisher-service has PUBLISHES_TO edges to event-tb-updated-topic, event-bus-topic, journal-topic
            var pubEdges = edgeList.Where(e => e.Service.Equals("publisher-service", StringComparison.OrdinalIgnoreCase) &&
                                               (e.Rel == "PUBLISHES_TO" || e.Rel == "PUBLISHES")).ToList();
            pubEdges.Select(e => e.Topic).Should().Contain("event-tb-updated-topic");
            pubEdges.Select(e => e.Topic).Should().Contain("event-bus-topic");
            pubEdges.Select(e => e.Topic).Should().Contain("journal-topic");

            // Assert 2: hub-subscriber has SUBSCRIBES_TO edge to event-tb-updated-topic and ZERO publishes
            var hubPublishes = edgeList.Where(e => e.Service.Equals("hub-subscriber", StringComparison.OrdinalIgnoreCase) &&
                                                  (e.Rel == "PUBLISHES_TO" || e.Rel == "PUBLISHES")).ToList();
            hubPublishes.Should().BeEmpty("hub-subscriber must NOT have any false PUBLISHES edges");

            var hubSubscribes = edgeList.Where(e => e.Service.Equals("hub-subscriber", StringComparison.OrdinalIgnoreCase) &&
                                                   (e.Rel == "SUBSCRIBES_TO" || e.Rel == "SUBSCRIBED_BY" || e.Rel == "SUBSCRIBES")).ToList();
            hubSubscribes.Select(e => e.Topic).Should().Contain("event-tb-updated-topic");

            // Assert 3: kv-subscriber has SUBSCRIBES_TO edge to event-bus-topic and ZERO publishes
            var kvPublishes = edgeList.Where(e => e.Service.Equals("kv-subscriber", StringComparison.OrdinalIgnoreCase) &&
                                                 (e.Rel == "PUBLISHES_TO" || e.Rel == "PUBLISHES")).ToList();
            kvPublishes.Should().BeEmpty("kv-subscriber must NOT have any false PUBLISHES edges");

            var kvSubscribes = edgeList.Where(e => e.Service.Equals("kv-subscriber", StringComparison.OrdinalIgnoreCase) &&
                                                  (e.Rel == "SUBSCRIBES_TO" || e.Rel == "SUBSCRIBED_BY" || e.Rel == "SUBSCRIBES")).ToList();
            kvSubscribes.Select(e => e.Topic).Should().Contain("event-bus-topic");

            // Assert 4: adapter-subscriber has SUBSCRIBES_TO edges to event-bus-topic and journal-topic
            var adapterSubscribes = edgeList.Where(e => e.Service.Equals("adapter-subscriber", StringComparison.OrdinalIgnoreCase) &&
                                                        (e.Rel == "SUBSCRIBES_TO" || e.Rel == "SUBSCRIBED_BY" || e.Rel == "SUBSCRIBES")).ToList();
            adapterSubscribes.Select(e => e.Topic).Should().Contain("event-bus-topic");
            adapterSubscribes.Select(e => e.Topic).Should().Contain("journal-topic");

            // Assert 5: In domain architecture, domain edges contain both PUBLISHES and SUBSCRIBES
            domain.Edges.Any(e => e.Label == "PUBLISHES").Should().BeTrue("Domain architecture must contain PUBLISHES edges");
            domain.Edges.Any(e => e.Label == "SUBSCRIBES").Should().BeTrue("Domain architecture must contain SUBSCRIBES edges");

            // Verify ServiceGraphExplorer on hub-subscriber
            var explorer = new ServiceGraphExplorer(client);
            var hubSurface = await explorer.GetServiceSurfaceAsync("hub-subscriber");
            hubSurface.Subscribes.Should().Contain("event-tb-updated-topic");
            hubSurface.Publishes.Should().BeEmpty();

            var kvSurface = await explorer.GetServiceSurfaceAsync("kv-subscriber");
            kvSurface.Subscribes.Should().Contain("event-bus-topic");
            kvSurface.Publishes.Should().BeEmpty();
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}

