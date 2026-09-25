using CodeExplorer.Core.Analysis;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ProjectLayerClassifierTests
{
    [Test]
    public void Classify_CategorizesMonorepoProjectsIntoCleanLayers()
    {
        var projects = new List<ProjectClassifierItem>
        {
            new() { Id = "p1", Name = "CodeExplorer", FilePath = "cli/src/UI/CodeExplorer/CodeExplorer.csproj", Framework = "net10.0" },
            new() { Id = "p2", Name = "CodeExplorer.Core", FilePath = "cli/src/Core/CodeExplorer.Core/CodeExplorer.Core.csproj", Framework = "net10.0" },
            new() { Id = "p3", Name = "CodeExplorer.Cypher", FilePath = "cli/src/Cypher/CodeExplorer.Cypher/CodeExplorer.Cypher.csproj", Framework = "net10.0" },
            new() { Id = "p4", Name = "CodeExplorer.Parser.CSharp", FilePath = "cli/src/Parsers/CodeExplorer.Parser.CSharp/CodeExplorer.Parser.CSharp.csproj", Framework = "net10.0" },
            new() { Id = "p5", Name = "CodeExplorer.Tests", FilePath = "cli/tests/CodeExplorer.Tests/CodeExplorer.Tests.csproj", Framework = "net10.0" }
        };

        var dependencies = new List<DependencyItem>
        {
            new() { SourceId = "p1", TargetId = "p2" }, // CLI -> Core
            new() { SourceId = "p2", TargetId = "p3" }, // Core -> Cypher
            new() { SourceId = "p2", TargetId = "p4" }, // Core -> Parser
            new() { SourceId = "p5", TargetId = "p1" }, // Tests -> CLI
            new() { SourceId = "p5", TargetId = "p2" }  // Tests -> Core
        };

        var layers = ProjectLayerClassifier.Classify(projects, dependencies);

        Assert.That(layers["p1"].LayerId, Is.EqualTo(StandardLayers.Ingress.LayerId));
        Assert.That(layers["p2"].LayerId, Is.EqualTo(StandardLayers.Components.LayerId));
        Assert.That(layers["p3"].LayerId, Is.EqualTo(StandardLayers.Components.LayerId));
        Assert.That(layers["p4"].LayerId, Is.EqualTo(StandardLayers.Components.LayerId));
        Assert.That(layers["p5"].LayerId, Is.EqualTo(StandardLayers.Tests.LayerId));
    }

    [Test]
    public void Classify_CategorizesMicroservicesArchitectureIntoIngressComponentsEgressFoundation()
    {
        var projects = new List<ProjectClassifierItem>
        {
            new() { Id = "s1", Name = "bff", FilePath = "src/services/bff/package.json" },
            new() { Id = "s2", Name = "bundle", FilePath = "src/services/bundle/package.json" },
            new() { Id = "s3", Name = "bundle-update-adapter", FilePath = "src/services/bundle-update-adapter/package.json" },
            new() { Id = "s4", Name = "library", FilePath = "src/services/library/package.json" },
            new() { Id = "s5", Name = "domain-tests", FilePath = "src/services/domain-tests/package.json" },
        };

        var dependencies = new List<DependencyItem>
        {
            new() { SourceId = "s1", TargetId = "s2" }, // BFF -> bundle
            new() { SourceId = "s2", TargetId = "s3" }, // bundle -> bundle-update-adapter
            new() { SourceId = "s2", TargetId = "s4" }, // bundle -> library
        };

        var layers = ProjectLayerClassifier.Classify(projects, dependencies);

        Assert.That(layers["s1"].LayerId, Is.EqualTo(StandardLayers.Ingress.LayerId));
        Assert.That(layers["s2"].LayerId, Is.EqualTo(StandardLayers.Components.LayerId));
        Assert.That(layers["s3"].LayerId, Is.EqualTo(StandardLayers.Egress.LayerId));
        Assert.That(layers["s4"].LayerId, Is.EqualTo(StandardLayers.Foundation.LayerId));
        Assert.That(layers["s5"].LayerId, Is.EqualTo(StandardLayers.Tests.LayerId));
    }

    [Test]
    public void Classify_PrioritizesLibrariesOverIngressAndEgress()
    {
        var projects = new List<ProjectClassifierItem>
        {
            // UI Library inside /ui/ path: Should be Foundation, not Ingress
            new() { Id = "lib1", Name = "button", FilePath = "fe/projects/ui/src/lib/button/package.json", IsLibrary = true, Role = "SharedLibrary" },
            // Common nest library inside integrations path: Should be Foundation, not Egress
            new() { Id = "lib2", Name = "common-nest", FilePath = "integrations/libs/common-nest/package.json", IsLibrary = true },
            // Project with /src/lib/ path convention: Should be Foundation
            new() { Id = "lib3", Name = "core-widgets", FilePath = "fe/projects/ui/src/lib/widgets/package.json" }
        };

        var layers = ProjectLayerClassifier.Classify(projects, new List<DependencyItem>());

        Assert.That(layers["lib1"].LayerId, Is.EqualTo(StandardLayers.Foundation.LayerId));
        Assert.That(layers["lib2"].LayerId, Is.EqualTo(StandardLayers.Foundation.LayerId));
        Assert.That(layers["lib3"].LayerId, Is.EqualTo(StandardLayers.Foundation.LayerId));
    }

    [Test]
    public void Classify_CategorizesWorkersAsComponents()
    {
        var projects = new List<ProjectClassifierItem>
        {
            // Explicit Role == "Worker"
            new() { Id = "w1", Name = "adhub-cf-worker", FilePath = "src/services/adhub-cf-worker/package.json", Role = "Worker" },
            // Suffix "-worker"
            new() { Id = "w2", Name = "orders-worker", FilePath = "src/services/orders-worker/package.json" },
            // Suffix ".worker"
            new() { Id = "w3", Name = "billing.worker", FilePath = "src/services/billing.worker/package.json" }
        };

        var layers = ProjectLayerClassifier.Classify(projects, new List<DependencyItem>());

        Assert.That(layers["w1"].LayerId, Is.EqualTo(StandardLayers.Components.LayerId));
        Assert.That(layers["w2"].LayerId, Is.EqualTo(StandardLayers.Components.LayerId));
        Assert.That(layers["w3"].LayerId, Is.EqualTo(StandardLayers.Components.LayerId));
    }

    [Test]
    public void Classify_CategorizesIntegrationServicesAsEgress()
    {
        var projects = new List<ProjectClassifierItem>
        {
            new() { Id = "e1", Name = "integration-service-bundle-cpm-controller", FilePath = "src/services/integration-service-bundle-cpm-controller/package.json" },
            new() { Id = "e2", Name = "integration-priority", FilePath = "src/services/integration-priority/package.json" },
            new() { Id = "e3", Name = "integration_nrt", FilePath = "src/services/integration_nrt/package.json" }
        };

        var layers = ProjectLayerClassifier.Classify(projects, new List<DependencyItem>());

        Assert.That(layers["e1"].LayerId, Is.EqualTo(StandardLayers.Egress.LayerId));
        Assert.That(layers["e2"].LayerId, Is.EqualTo(StandardLayers.Egress.LayerId));
        Assert.That(layers["e3"].LayerId, Is.EqualTo(StandardLayers.Egress.LayerId));
    }

    [Test]
    public void Classify_StrictInDegreeZeroDoesNotDefaultToIngressForBackendServices()
    {
        var projects = new List<ProjectClassifierItem>
        {
            // Backend service with inDegree == 0 (no incoming edges, but calls DB or other services)
            new() { Id = "s1", Name = "ATSAnalyticsDepartment", FilePath = "src/services/ATSAnalyticsDepartment/package.json", Framework = "NodeJS" },
            new() { Id = "s2", Name = "partnerstat", FilePath = "src/services/partnerstat/package.json", Framework = "NodeJS" },
            new() { Id = "s3", Name = "netstatend", FilePath = "src/services/netstatend/package.json", Framework = "NodeJS" }
        };

        var dependencies = new List<DependencyItem>
        {
            new() { SourceId = "s1", TargetId = "db1" },
            new() { SourceId = "s2", TargetId = "db2" }
        };

        var layers = ProjectLayerClassifier.Classify(projects, dependencies);

        Assert.That(layers["s1"].LayerId, Is.EqualTo(StandardLayers.Components.LayerId));
        Assert.That(layers["s2"].LayerId, Is.EqualTo(StandardLayers.Components.LayerId));
        Assert.That(layers["s3"].LayerId, Is.EqualTo(StandardLayers.Components.LayerId));
    }

    [Test]
    public void Classify_EvaluatesEvidenceMatrixAccurately()
    {
        var projects = new List<ProjectClassifierItem>
        {
            // 1. Ingress via CLI EntryPoint / has_cli_bin
            new()
            {
                Id = "p_cli",
                Name = "DataMigrator",
                FilePath = "src/tools/DataMigrator/package.json",
                EntryPoints = [new("ep1", "migrate", "src/tools/DataMigrator", "cli")],
                Extensions = new Dictionary<string, string> { ["has_cli_bin"] = "true", ["manifest_type"] = "cli" }
            },
            // 2. Ingress via Web SDK + Endpoints
            new()
            {
                Id = "p_web",
                Name = "PublicGateway",
                FilePath = "src/services/PublicGateway/PublicGateway.csproj",
                EndpointsCount = 12,
                Extensions = new Dictionary<string, string> { ["sdk"] = "Microsoft.NET.Sdk.Web" }
            },
            // 3. Components via Worker SDK + queue listener EntryPoint
            new()
            {
                Id = "p_worker",
                Name = "EventConsumer",
                FilePath = "src/services/EventConsumer/EventConsumer.csproj",
                EntryPoints = [new("ep2", "order-subscriber", "src/services/EventConsumer", "queue-listener")],
                Extensions = new Dictionary<string, string> { ["sdk"] = "Microsoft.NET.Sdk.Worker", ["manifest_type"] = "worker" }
            },
            // 4. Egress via ExternalServicesCount > 0 and 0 endpoints
            new()
            {
                Id = "p_egress",
                Name = "StripeClientAdapter",
                FilePath = "src/adapters/StripeClientAdapter/package.json",
                ExternalServicesCount = 3,
                EndpointsCount = 0
            },
            // 5. Foundation via manifest_type == "library"
            new()
            {
                Id = "p_lib",
                Name = "CommonContracts",
                FilePath = "src/contracts/package.json",
                Extensions = new Dictionary<string, string> { ["manifest_type"] = "library" }
            }
        };

        var layers = ProjectLayerClassifier.Classify(projects, new List<DependencyItem>());

        Assert.That(layers["p_cli"].LayerId, Is.EqualTo(StandardLayers.Ingress.LayerId));
        Assert.That(layers["p_web"].LayerId, Is.EqualTo(StandardLayers.Ingress.LayerId));
        Assert.That(layers["p_worker"].LayerId, Is.EqualTo(StandardLayers.Components.LayerId));
        Assert.That(layers["p_egress"].LayerId, Is.EqualTo(StandardLayers.Egress.LayerId));
        Assert.That(layers["p_lib"].LayerId, Is.EqualTo(StandardLayers.Foundation.LayerId));
    }
}
