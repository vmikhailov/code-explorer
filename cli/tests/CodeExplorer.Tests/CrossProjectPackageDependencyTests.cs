using System.Text.Json;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class CrossProjectPackageDependencyTests
{
    [SetUp]
    public void SetUp()
    {
        WorkspaceIndexer.Register(new GoParser());
        WorkspaceIndexer.Register(new TypeScriptParser());
        WorkspaceIndexer.Register(new JavaScriptParser());
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

    [Test]
    public async Task Test_CrossProject_TypeScript_Npm_PrivateAndScoped_AreLinked()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_cross_ts_test_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            // Project 1: common-lib (producer with private: true and scoped name)
            var dirLib = Path.Combine(tempWorkspace, "common-lib").Replace('\\', '/');
            Directory.CreateDirectory(dirLib);
            await File.WriteAllTextAsync(Path.Combine(dirLib, "package.json"), @"
{
  ""name"": ""@myorg/common-lib"",
  ""version"": ""1.2.0"",
  ""private"": true
}");
            await File.WriteAllTextAsync(Path.Combine(dirLib, "index.ts"), "export const foo = 42;\n");

            // Project 2: web-app (consumer referencing scoped name via workspace:*)
            var dirApp = Path.Combine(tempWorkspace, "web-app").Replace('\\', '/');
            Directory.CreateDirectory(dirApp);
            await File.WriteAllTextAsync(Path.Combine(dirApp, "package.json"), @"
{
  ""name"": ""web-app"",
  ""version"": ""0.1.0"",
  ""dependencies"": {
    ""@myorg/common-lib"": ""workspace:*""
  }
}");
            await File.WriteAllTextAsync(Path.Combine(dirApp, "main.ts"), "import { foo } from '@myorg/common-lib';\n");

            var dbPath = Path.Combine(tempWorkspace, "graph.db").Replace('\\', '/');
            using var db = new SqliteGraphClient(dbPath);

            var indexer = new WorkspaceIndexer(db);
            var (nodesCount, relsCount, nodesByKind) = await indexer.IndexAsync(tempWorkspace, clear: true);

            Assert.That(nodesCount, Is.GreaterThan(0));

            // Verify Project -> Project DEPENDS_ON exists in database
            var query = "MATCH (p1:Project {name: 'web-app'})-[r:DEPENDS_ON]->(p2:Project {name: 'common-lib'}) RETURN count(r) AS cnt";
            var json = await db.ExecuteQueryAsync(query);
            using var doc = JsonDocument.Parse(json);
            var cnt = doc.RootElement[0].GetProperty("cnt").GetInt64();
            Assert.That(cnt, Is.EqualTo(1), "web-app should depend on common-lib via @myorg/common-lib package mapping");

            // Verify GraphDataConverter includes edge and proper project_type == 'typescript'
            var graph = await GraphDataConverter.GetArchitectureGraphAsync(db);
            var nodeApp = graph.Nodes.FirstOrDefault(n => n.Name == "web-app");
            var nodeLib = graph.Nodes.FirstOrDefault(n => n.Name == "common-lib");

            Assert.That(nodeApp, Is.Not.Null);
            Assert.That(nodeLib, Is.Not.Null);
            Assert.That(nodeApp!.Properties?.GetValueOrDefault("project_type"), Is.EqualTo("typescript"));
            Assert.That(nodeLib!.Properties?.GetValueOrDefault("project_type"), Is.EqualTo("typescript"));

            var edge = graph.Edges.FirstOrDefault(e => e.Source == nodeApp.Id && e.Target == nodeLib.Id);
            Assert.That(edge, Is.Not.Null, "GraphDataConverter should output edge from web-app to common-lib");
            Assert.That(edge!.Kind, Is.EqualTo("LIBRARY"));
            Assert.That(edge.Properties?.GetValueOrDefault("dependency_type"), Is.EqualTo("library"));
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                try { Directory.Delete(tempWorkspace, true); } catch { }
            }
        }
    }

    [Test]
    public async Task Test_DependencyType_Classification_Differentiates_ServiceCall_And_Library()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_dep_type_test_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            var dbPath = Path.Combine(tempWorkspace, "graph.db").Replace('\\', '/');
            using var db = new SqliteGraphClient(dbPath);

            // Seed Projects & Database
            var nodes = new List<CodeExplorer.Core.Database.Node>
            {
                new CodeExplorer.Core.Database.Node("proj:client", "Project", new Dictionary<string, object> { ["name"] = "ClientSvc", ["path"] = "/src/client", ["project_type"] = "csharp" }),
                new CodeExplorer.Core.Database.Node("proj:server", "Project", new Dictionary<string, object> { ["name"] = "ServerSvc", ["path"] = "/src/server", ["project_type"] = "csharp" }),
                new CodeExplorer.Core.Database.Node("proj:common", "Project", new Dictionary<string, object> { ["name"] = "CommonLib", ["path"] = "/src/common", ["project_type"] = "library" }),
                new CodeExplorer.Core.Database.Node("db:main", "Database", new Dictionary<string, object> { ["name"] = "AppDb", ["db_type"] = "PostgreSQL" }),
            };
            await db.UploadNodesAsync(nodes);

            // Seed relationships with dependency_type
            var rels = new List<CodeExplorer.Core.Database.Relationship>
            {
                new CodeExplorer.Core.Database.Relationship("proj:client", "proj:server", "DEPENDS_ON", new Dictionary<string, object> { ["dependency_type"] = "service_call", ["kind"] = "DEPENDS_ON" }),
                new CodeExplorer.Core.Database.Relationship("proj:client", "proj:common", "DEPENDS_ON", new Dictionary<string, object> { ["dependency_type"] = "library", ["kind"] = "DEPENDS_ON" }),
                new CodeExplorer.Core.Database.Relationship("proj:client", "db:main", "USES_DB", new Dictionary<string, object> { ["kind"] = "USES_DB" }),
            };
            await db.UploadRelationshipsAsync(rels);

            // Test Architecture Graph
            var arch = await GraphDataConverter.GetArchitectureGraphAsync(db);

            var svcEdge = arch.Edges.FirstOrDefault(e => e.Source == "proj:client" && e.Target == "proj:server");
            Assert.That(svcEdge, Is.Not.Null);
            Assert.That(svcEdge!.Kind, Is.EqualTo("SERVICE_CALL"));
            Assert.That(svcEdge.Properties?.GetValueOrDefault("dependency_type"), Is.EqualTo("service_call"));

            var libEdge = arch.Edges.FirstOrDefault(e => e.Source == "proj:client" && e.Target == "proj:common");
            Assert.That(libEdge, Is.Not.Null);
            Assert.That(libEdge!.Kind, Is.EqualTo("LIBRARY"));
            Assert.That(libEdge.Properties?.GetValueOrDefault("dependency_type"), Is.EqualTo("library"));

            var dbEdge = arch.Edges.FirstOrDefault(e => e.Source == "proj:client" && e.Target == "db:main");
            Assert.That(dbEdge, Is.Not.Null);
            Assert.That(dbEdge!.Kind, Is.EqualTo("USES_DB"));
            Assert.That(dbEdge.Properties?.GetValueOrDefault("dependency_type"), Is.EqualTo("database"));

            // Test Neighborhood Graph for ClientSvc
            var hood = await GraphDataConverter.GetProjectNeighborhoodAsync(db, "proj:client");

            var hoodSvcEdge = hood.Edges.FirstOrDefault(e => e.Source == "proj:client" && e.Target == "proj:server");
            Assert.That(hoodSvcEdge, Is.Not.Null);
            Assert.That(hoodSvcEdge!.Kind, Is.EqualTo("SERVICE_CALL"));
            Assert.That(hoodSvcEdge.Properties?.GetValueOrDefault("dependency_type"), Is.EqualTo("service_call"));

            var hoodLibEdge = hood.Edges.FirstOrDefault(e => e.Source == "proj:client" && e.Target == "proj:common");
            Assert.That(hoodLibEdge, Is.Not.Null);
            Assert.That(hoodLibEdge!.Kind, Is.EqualTo("LIBRARY"));
            Assert.That(hoodLibEdge.Properties?.GetValueOrDefault("dependency_type"), Is.EqualTo("library"));

            var hoodDbEdge = hood.Edges.FirstOrDefault(e => e.Source == "proj:client" && e.Target == "db:main");
            Assert.That(hoodDbEdge, Is.Not.Null);
            Assert.That(hoodDbEdge!.Kind, Is.EqualTo("USES_DB"));
            Assert.That(hoodDbEdge.Properties?.GetValueOrDefault("dependency_type"), Is.EqualTo("database"));
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
