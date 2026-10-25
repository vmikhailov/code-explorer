using CodeExplorer.Core.Analysis.ProjectMap;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ProjectMapBuilderTests
{
    private static ProjectMapFile F(string path, bool test = false) => new(path, test, 10);

    private static ProjectMapSymbolEdge Call(string from, string to) => new(from, to, "CALLS", "a", "b", 1);

    [Test]
    public void Partition_DescendsIntoDominantSourceRoot()
    {
        var files = new[]
        {
            F("svc/src/main/java/com/acme/billing/controllers/A.java"),
            F("svc/src/main/java/com/acme/billing/services/B.java"),
            F("svc/src/main/java/com/acme/billing/repository/C.java"),
            F("svc/src/main/java/com/acme/billing/domain/D.java")
        };

        var result = ProjectMapPartitioner.Partition("svc", files);

        result.SourceRoot.Should().Be("src/main/java/com/acme/billing/");
        result.Granularity.Should().Be(ProjectMapGranularity.Module);
        result.Modules.Select(m => m.Name).Should().BeEquivalentTo("controllers", "services", "repository", "domain");
        result.Modules.Single(m => m.Name == "services").Path.Should().Be("svc/src/main/java/com/acme/billing/services");
    }

    [Test]
    public void Partition_RootFilesGetOwnModule()
    {
        var files = new[] { F("app/index.ts"), F("app/mcp/a.ts"), F("app/db/b.ts"), F("app/graph/c.ts") };

        var result = ProjectMapPartitioner.Partition("app", files);

        result.Modules.Should().Contain(m => m.IsRootFiles && m.Files.Contains("app/index.ts"));
        result.Modules.Count(m => !m.IsRootFiles).Should().Be(3);
    }

    [Test]
    public void Partition_FlatProject_FallsBackToFileGranularity()
    {
        var files = Enumerable.Range(0, 8).Select(i => F($"tool/file{i}.go")).ToArray();

        var result = ProjectMapPartitioner.Partition("tool", files);

        result.Granularity.Should().Be(ProjectMapGranularity.File);
        result.Modules.Should().HaveCount(8);
        result.Modules.Should().OnlyContain(m => m.Path.StartsWith("tool/file"));
    }

    [Test]
    public void Partition_FewTopFolders_UsesSubModules()
    {
        var files = new[]
        {
            F("p/src/core/a/x.ts"), F("p/src/core/b/y.ts"), F("p/src/core/c/z.ts"), F("p/src/api/w.ts")
        };

        var result = ProjectMapPartitioner.Partition("p", files);

        result.Granularity.Should().Be(ProjectMapGranularity.SubModule);
        result.Modules.Select(m => m.Name).Should().Contain(new[] { "core/a", "core/b", "core/c" });
    }

    [Test]
    public void Partition_TooManyFolders_MergesOverflowIntoOther()
    {
        var files = Enumerable.Range(0, ProjectMapPartitioner.MaxModules + 10)
            .Select(i => F($"p/m{i:D2}/f.cs"))
            .ToArray();

        var result = ProjectMapPartitioner.Partition("p", files);

        result.Modules.Should().HaveCount(ProjectMapPartitioner.MaxModules);
        result.Modules.Should().ContainSingle(m => m.IsMerged);
    }

    [Test]
    public void Build_ExcludesTestFiles_AndLiftsCrossModuleEdgesOnly()
    {
        var input = new ProjectMapInput
        {
            ProjectPath = "p",
            Files = [F("p/a/1.cs"), F("p/a/2.cs"), F("p/b/1.cs"), F("p/c/1.cs"), F("p/tests/t.cs", test: true)],
            SymbolEdges =
            [
                Call("p/a/1.cs", "p/b/1.cs"),
                Call("p/a/2.cs", "p/b/1.cs"),
                Call("p/a/1.cs", "p/a/2.cs"),
                Call("p/tests/t.cs", "p/a/1.cs"),
                Call("other/x.cs", "p/a/1.cs")
            ]
        };

        var result = ProjectMapBuilder.Build(input);

        result.TestFilesExcluded.Should().Be(1);
        result.Modules.Should().NotContain(m => m.Name == "tests");
        var edge = result.Edges.Should().ContainSingle().Subject;
        edge.SourceId.Should().Be("pm:p/a");
        edge.TargetId.Should().Be("pm:p/b");
        edge.Count.Should().Be(2);
        edge.Files.Should().HaveCount(3);
        edge.Samples.Should().HaveCount(2);
    }

    [Test]
    public void Build_DetectsThreeModuleCycle_ViaScc()
    {
        var input = new ProjectMapInput
        {
            ProjectPath = "p",
            Files = [F("p/a/1.cs"), F("p/b/1.cs"), F("p/c/1.cs"), F("p/d/1.cs")],
            SymbolEdges =
            [
                Call("p/a/1.cs", "p/b/1.cs"),
                Call("p/b/1.cs", "p/c/1.cs"),
                Call("p/c/1.cs", "p/a/1.cs"),
                Call("p/d/1.cs", "p/a/1.cs")
            ]
        };

        var result = ProjectMapBuilder.Build(input);

        result.Cycles.Should().ContainSingle()
            .Which.Should().BeEquivalentTo("pm:p/a", "pm:p/b", "pm:p/c");
        result.Edges.Where(e => e.IsCircular).Should().HaveCount(3);
        result.Edges.Single(e => e.SourceId == "pm:p/d").IsCircular.Should().BeFalse();
        result.Modules.Single(m => m.Name == "d").CycleGroup.Should().BeNull();
    }

    [Test]
    public void Build_ClassifiesTiersFromTopology()
    {
        var input = new ProjectMapInput
        {
            ProjectPath = "p",
            Files =
            [
                F("p/handlers/1.cs"), F("p/orchestrator/1.cs"), F("p/engine/1.cs"),
                F("p/persistence/1.cs"), F("p/utils/1.cs")
            ],
            SymbolEdges =
            [
                Call("p/handlers/1.cs", "p/orchestrator/1.cs"),
                Call("p/handlers/1.cs", "p/utils/1.cs"),
                Call("p/orchestrator/1.cs", "p/engine/1.cs"),
                Call("p/orchestrator/1.cs", "p/persistence/1.cs"),
                Call("p/orchestrator/1.cs", "p/utils/1.cs"),
                Call("p/engine/1.cs", "p/utils/1.cs"),
                Call("p/persistence/1.cs", "p/utils/1.cs")
            ],
            ResourceLinks = [new ProjectMapResourceLink("p/persistence/1.cs", "tbl:orders", "Table", "orders", "WRITES_TO")],
            EntryPointsByFile = new Dictionary<string, int> { ["p/handlers/1.cs"] = 2 }
        };

        var result = ProjectMapBuilder.Build(input);
        ProjectMapTier TierOf(string name) => result.Modules.Single(m => m.Name == name).Tier;

        TierOf("handlers").Should().Be(ProjectMapTier.Ingress);
        TierOf("orchestrator").Should().Be(ProjectMapTier.Application);
        TierOf("engine").Should().Be(ProjectMapTier.Core);
        TierOf("persistence").Should().Be(ProjectMapTier.Data);
        TierOf("utils").Should().Be(ProjectMapTier.Foundation);

        result.Resources.Should().ContainSingle(r => r.Id == "tbl:orders");
        result.Edges.Should().Contain(e => e.IsResourceLink && e.SourceId == "pm:p/persistence" && e.TargetId == "tbl:orders");
    }

    [Test]
    public void Classifier_FolderNamesOnlyBreakTies()
    {
        // "db" folder with no data evidence and clearly unstable topology must not be forced into the Data ring.
        var input = new ProjectMapInput
        {
            ProjectPath = "p",
            Files = [F("p/db/1.cs"), F("p/x/1.cs"), F("p/y/1.cs"), F("p/z/1.cs")],
            SymbolEdges =
            [
                Call("p/x/1.cs", "p/db/1.cs"),
                Call("p/db/1.cs", "p/y/1.cs"),
                Call("p/db/1.cs", "p/z/1.cs"),
                Call("p/db/1.cs", "p/x/1.cs")
            ]
        };

        var result = ProjectMapBuilder.Build(input);

        result.Modules.Single(m => m.Name == "db").Tier.Should().Be(ProjectMapTier.Application);
    }

    [Test]
    public void GraphMapper_ReusesExistingNodeKinds_AndEmitsMetadata()
    {
        var input = new ProjectMapInput
        {
            ProjectId = "ws:p:p:",
            ProjectName = "p",
            ProjectPath = "p",
            Files = [F("p/a/1.cs"), F("p/b/1.cs"), F("p/c/1.cs")],
            SymbolEdges = [Call("p/a/1.cs", "p/b/1.cs"), Call("p/b/1.cs", "p/a/1.cs")]
        };
        var graph = new CodeExplorer.Core.Protocol.GraphDataDto { Metadata = new Dictionary<string, string>() };

        ProjectMapGraphMapper.Populate(graph, ProjectMapBuilder.Build(input));

        graph.Nodes.Should().OnlyContain(n => n.Kind == CodeExplorer.Cypher.Common.NodeKind.Folder);
        graph.Metadata!["cycle_count"].Should().Be("1");
        graph.Metadata["granularity"].Should().Be("module");
        graph.Edges.Should().OnlyContain(e => e.Properties!["is_circular"] == "true");
    }

    [Test]
    public void Build_ParentProjectWithSubProjectsAndBuildOrchestrator_ShowsSubProjectsAndExcludesPublicSrcAndGulpScripts()
    {
        var input = new ProjectMapInput
        {
            ProjectId = "ws:p:src/services/cf-worker:",
            ProjectName = "cf-worker",
            ProjectPath = "src/services/cf-worker",
            IsBuildOrchestrator = true,
            ExcludedSubpaths = ["public_src"],
            Files =
            [
                F("src/services/cf-worker/gulpfile.js"),
                F("src/services/cf-worker/minify-html.js"),
                F("src/services/cf-worker/workers/tracker-worker/public_src/land_backends/1110/index.js")
            ],
            SubProjects =
            [
                new ProjectMapSubProject(
                    "ws:p:src/services/cf-worker/workers/tracker-worker:",
                    "tracker-worker",
                    "src/services/cf-worker/workers/tracker-worker",
                    "Worker",
                    [
                        F("src/services/cf-worker/workers/tracker-worker/workers-site/index.js"),
                        F("src/services/cf-worker/workers/tracker-worker/workers-site/src/save-impression.js")
                    ]),
                new ProjectMapSubProject(
                    "ws:p:src/services/cf-worker/workers/queue-consumer-worker:",
                    "queue-consumer-worker",
                    "src/services/cf-worker/workers/queue-consumer-worker",
                    "Worker",
                    [
                        F("src/services/cf-worker/workers/queue-consumer-worker/workers-site/index.js")
                    ])
            ],
            SymbolEdges =
            [
                Call(
                    "src/services/cf-worker/workers/queue-consumer-worker/workers-site/index.js",
                    "src/services/cf-worker/workers/tracker-worker/workers-site/src/save-impression.js")
            ]
        };

        var result = ProjectMapBuilder.Build(input);

        result.Modules.Select(m => m.Name).Should().BeEquivalentTo("tracker-worker", "queue-consumer-worker");
        result.Modules.Should().OnlyContain(m => m.IsSubProject);
        result.Edges.Should().ContainSingle(e =>
            e.SourceId == "pm:sub:queue-consumer-worker" &&
            e.TargetId == "pm:sub:tracker-worker");
    }
}
