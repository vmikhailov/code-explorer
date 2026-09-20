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

        Assert.That(layers["p1"].LayerId, Is.EqualTo(StandardLayers.Presentation.LayerId));
        Assert.That(layers["p2"].LayerId, Is.EqualTo(StandardLayers.ApplicationCore.LayerId));
        Assert.That(layers["p3"].LayerId, Is.EqualTo(StandardLayers.DomainServices.LayerId));
        Assert.That(layers["p4"].LayerId, Is.EqualTo(StandardLayers.DomainServices.LayerId));
        Assert.That(layers["p5"].LayerId, Is.EqualTo(StandardLayers.Tests.LayerId));
    }
}
