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

        Assert.That(lifted, Has.Count.EqualTo(1));
        var rel = lifted[0];
        Assert.That(rel.From, Is.EqualTo("proj:svc_a"));
        Assert.That(rel.To, Is.EqualTo("proj:svc_d"));
        Assert.That(rel.Kind, Is.EqualTo(OntologyConstants.Relationships.ServiceCall));
        Assert.That(rel.Properties!["via_library"]?.ToString(), Is.EqualTo("LibB"));
        Assert.That(rel.Properties!["call_chain"]?.ToString(), Is.EqualTo("LibB -> LibC"));
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

        Assert.That(lifted, Has.Count.EqualTo(1));
        var rel = lifted[0];
        Assert.That(rel.From, Is.EqualTo("proj:svc_a"));
        Assert.That(rel.To, Is.EqualTo("ws1:res:db:relational:orders_db"));
        Assert.That(rel.Kind, Is.EqualTo(OntologyConstants.Relationships.UsesDb));
        Assert.That(rel.Properties!["via_library"]?.ToString(), Is.EqualTo("LibData"));
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

        Assert.That(lifted, Has.Count.EqualTo(1));
        var rel = lifted[0];
        Assert.That(rel.From, Is.EqualTo("ws1:res:topic:kafka:orders"));
        Assert.That(rel.To, Is.EqualTo("proj:svc_a"));
        Assert.That(rel.Kind, Is.EqualTo(OntologyConstants.Relationships.Triggers));
        Assert.That(rel.Properties!["via_library"]?.ToString(), Is.EqualTo("LibConsumer"));
    }
}
