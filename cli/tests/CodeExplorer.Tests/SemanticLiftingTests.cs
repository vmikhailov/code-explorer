using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class SemanticLiftingTests
{
    [Test]
    public void LiftTransitiveSemanticRelations_LiftsThroughLibraryChainToTargetService()
    {
        // Service A -> Lib B -> Lib C -> Service D
        var svcA = new ProjectNode("proj:svc_a", "ServiceA", "/src/a", "csharp", "Service", false);
        var libB = new ProjectNode("proj:lib_b", "LibB", "/src/libs/b", "csharp", "SharedLibrary", true);
        var libC = new ProjectNode("proj:lib_c", "LibC", "/src/libs/c", "csharp", "SharedLibrary", true);
        var svcD = new ProjectNode("proj:svc_d", "ServiceD", "/src/d", "csharp", "Service", false);

        var projects = new List<ProjectNode> { svcA, libB, libC, svcD };

        var relationships = new List<Relationship>
        {
            new("proj:svc_a", "proj:lib_b", OntologyConstants.Relationships.DependsOn, new()),
            new("proj:lib_b", "proj:lib_c", OntologyConstants.Relationships.DependsOn, new()),
            new("proj:lib_c", "proj:svc_d", OntologyConstants.Relationships.CallsEndpoint, new())
        };

        var lifted = PostIndexAnalyzer.LiftTransitiveSemanticRelations(projects, relationships);

        lifted.Should().HaveCount(1);
        var rel = lifted[0];
        rel.From.Should().Be("proj:svc_a");
        rel.To.Should().Be("proj:svc_d");
        rel.Kind.Should().Be(OntologyConstants.Relationships.ServiceCall);
        rel.Properties!["via_library"]?.ToString().Should().Be("LibB");
        rel.Properties!["call_chain"]?.ToString().Should().Be("LibB -> LibC");
    }

    [Test]
    public void LiftTransitiveSemanticRelations_LiftsThroughLibraryToDatabase()
    {
        // Service A -> Lib Data -> Database (orders_db)
        var svcA = new ProjectNode("proj:svc_a", "ServiceA", "/src/a", "csharp", "Service", false);
        var libData = new ProjectNode("proj:lib_data", "LibData", "/src/libs/data", "csharp", "SharedLibrary", true);

        var projects = new List<ProjectNode> { svcA, libData };

        var relationships = new List<Relationship>
        {
            new("proj:svc_a", "proj:lib_data", OntologyConstants.Relationships.DependsOn, new()),
            new("proj:lib_data", "ws1:res:db:relational:orders_db", OntologyConstants.Relationships.UsesDb, new())
        };

        var lifted = PostIndexAnalyzer.LiftTransitiveSemanticRelations(projects, relationships);

        lifted.Should().HaveCount(1);
        var rel = lifted[0];
        rel.From.Should().Be("proj:svc_a");
        rel.To.Should().Be("ws1:res:db:relational:orders_db");
        rel.Kind.Should().Be(OntologyConstants.Relationships.UsesDb);
        rel.Properties!["via_library"]?.ToString().Should().Be("LibData");
    }

    [Test]
    public void LiftTransitiveSemanticRelations_LiftsInboundTopicTriggerToService()
    {
        // Topic (orders_topic) -> Lib Consumer <- Service A
        var svcA = new ProjectNode("proj:svc_a", "ServiceA", "/src/a", "csharp", "Service", false);
        var libConsumer = new ProjectNode("proj:lib_consumer", "LibConsumer", "/src/libs/consumer", "csharp", "SharedLibrary", true);

        var projects = new List<ProjectNode> { svcA, libConsumer };

        var relationships = new List<Relationship>
        {
            new("proj:svc_a", "proj:lib_consumer", OntologyConstants.Relationships.DependsOn, new()),
            new("ws1:res:topic:kafka:orders", "proj:lib_consumer", OntologyConstants.Relationships.Triggers, new())
        };

        var lifted = PostIndexAnalyzer.LiftTransitiveSemanticRelations(projects, relationships);

        lifted.Should().HaveCount(1);
        var rel = lifted[0];
        rel.From.Should().Be("proj:svc_a");
        rel.To.Should().Be("ws1:res:topic:kafka:orders");
        rel.Kind.Should().Be(OntologyConstants.Relationships.SubscribesTo);
        rel.Properties!["via_library"]?.ToString().Should().Be("LibConsumer");
    }

    [Test]
    public void LiftTransitiveSemanticRelations_SkipsLibraryTopicsFromModulesUnreachableByWorkload()
    {
        var server = new ProjectNode("ws:p:bins/server.rs:", "server", "bins/server.rs", "rust", "Service", false)
        {
            Extensions = new()
            {
                [OntologyConstants.PropertyNames.SourceReachability] =
                    """{"core":["core/src/server.rs","core/src/services/batch.rs"]}"""
            }
        };
        var client = new ProjectNode("ws:p:bins/client.rs:", "client", "bins/client.rs", "rust", "CliTool", false)
        {
            Extensions = new()
            {
                [OntologyConstants.PropertyNames.SourceReachability] = """{"core":["core/src/client/e2e.rs"]}"""
            }
        };
        var core = new ProjectNode("ws:p:core:", "core", "core", "rust", "SharedLibrary", true);
        const string requests = "ws:top:sqs:requests-queue";
        const string requestTopic = "ws:top:sns:request-topic";

        var relationships = new List<Relationship>
        {
            new(server.Id, core.Id, OntologyConstants.Relationships.DependsOn, new()),
            new(client.Id, core.Id, OntologyConstants.Relationships.DependsOn, new()),
            new(core.Id, requests, OntologyConstants.Relationships.SubscribesTo, new()),
            new(core.Id, requestTopic, OntologyConstants.Relationships.PublishesTo, new()),
            new(requests, "ws:sym:core/src/services/batch.rs:Function:receive_batch", OntologyConstants.Relationships.SubscribedBy, new()),
            new(requestTopic, "ws:sym:core/src/client/e2e.rs:Function:send_request", OntologyConstants.Relationships.PublishedBy, new())
        };

        var lifted = PostIndexAnalyzer.LiftTransitiveSemanticRelations([server, client, core], relationships);

        lifted.Select(r => (r.From, r.Kind, r.To)).Should().BeEquivalentTo(new[]
        {
            (server.Id, OntologyConstants.Relationships.SubscribesTo, requests),
            (client.Id, OntologyConstants.Relationships.PublishesTo, requestTopic)
        });
    }
}
