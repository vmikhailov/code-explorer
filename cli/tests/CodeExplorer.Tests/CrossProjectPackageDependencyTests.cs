using System.Text.Json;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Parser.Go;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class CrossProjectPackageDependencyTests
{
    [SetUp]
    public void SetUp()
    {
        WorkspaceIndexer.Register(new GoParser());
    }

    [Test]
    public async Task Test_CrossProject_GoModules_AreLinkedAndHaveProjectType()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_cross_proj_test_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            // Project 1: service-a (producer)
            var dirA = Path.Combine(tempWorkspace, "service-a").Replace('\\', '/');
            Directory.CreateDirectory(dirA);
            await File.WriteAllTextAsync(Path.Combine(dirA, "go.mod"), "module example.com/service-a\n\ngo 1.22\n");
            await File.WriteAllTextAsync(Path.Combine(dirA, "main.go"), "package main\n\nfunc main() {}\n");

            // Project 2: service-b (consumer of service-a)
            var dirB = Path.Combine(tempWorkspace, "service-b").Replace('\\', '/');
            Directory.CreateDirectory(dirB);
            await File.WriteAllTextAsync(Path.Combine(dirB, "go.mod"), @"module example.com/service-b

go 1.22

require (
    example.com/service-a v1.0.0
)
");
            await File.WriteAllTextAsync(Path.Combine(dirB, "main.go"), "package main\n\nfunc main() {}\n");

            var dbPath = Path.Combine(tempWorkspace, "graph.db").Replace('\\', '/');
            using var db = new SqliteGraphClient(dbPath);

            var indexer = new WorkspaceIndexer(db);
            var (nodesCount, relsCount, nodesByKind) = await indexer.IndexAsync(tempWorkspace, clear: true);

            Assert.That(nodesCount, Is.GreaterThan(0));

            // 1. Verify Project -> Project DEPENDS_ON exists in database
            var query = "MATCH (p1:Project {name: 'service-b'})-[r:DEPENDS_ON]->(p2:Project {name: 'service-a'}) RETURN count(r) AS cnt";
            var json = await db.ExecuteQueryAsync(query);
            using var doc = JsonDocument.Parse(json);
            var cnt = doc.RootElement[0].GetProperty("cnt").GetInt64();
            Assert.That(cnt, Is.EqualTo(1), "service-b should depend on service-a via workspace module resolution");

            // 2. Verify GraphDataConverter includes project_type == 'go'
            var graph = await GraphDataConverter.GetArchitectureGraphAsync(db);
            var nodeB = graph.Nodes.FirstOrDefault(n => n.Name == "service-b");
            var nodeA = graph.Nodes.FirstOrDefault(n => n.Name == "service-a");

            Assert.That(nodeB, Is.Not.Null);
            Assert.That(nodeA, Is.Not.Null);
            Assert.That(nodeB!.Properties?.GetValueOrDefault("project_type"), Is.EqualTo("go"));
            Assert.That(nodeA!.Properties?.GetValueOrDefault("project_type"), Is.EqualTo("go"));

            // 3. Verify graph edge exists in GraphDataConverter output
            var edge = graph.Edges.FirstOrDefault(e => e.Source == nodeB.Id && e.Target == nodeA.Id);
            Assert.That(edge, Is.Not.Null, "GraphDataConverter should output edge from service-b to service-a");

            // 4. Verify neighborhood query for service-b
            var hood = await GraphDataConverter.GetProjectNeighborhoodAsync(db, "service-b");
            Assert.That(hood.Nodes.Any(n => n.Name == "service-a"), Is.True, "service-a should appear in neighborhood outbound of service-b");
            var centerNode = hood.Nodes.FirstOrDefault(n => n.Name == "service-b");
            Assert.That(centerNode?.Properties?.GetValueOrDefault("project_type"), Is.EqualTo("go"));
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                try { Directory.Delete(tempWorkspace, true); } catch { }
            }
        }
    }
}
