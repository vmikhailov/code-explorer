using System.Text.Json;
using AwesomeAssertions;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Cypher.Common;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class CrossProjectPackageDependencyTests
{
    [SetUp]
    public void SetUp()
    {
        WorkspaceIndexer.AutoRegisterParsers();
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

            nodesCount.Should().BeGreaterThan(0);

            // 1. Verify Project -> Project DEPENDS_ON exists in database
            var query =
                "MATCH (p1:Project {name: 'service-b'})-[r:DEPENDS_ON]->(p2:Project {name: 'service-a'}) RETURN count(r) AS cnt";
            var json = await db.ExecuteQueryAsync(query);

            using var doc = JsonDocument.Parse(json);

            var cnt = doc.RootElement[0].GetProperty("cnt").GetInt64();

            cnt.Should().Be(1, "service-b should depend on service-a via workspace module resolution");

            // 2. Verify ArchitectureViewEngine includes project_type == 'go'
            var graph = await new ArchitectureViewEngine(db).GetSystemContextViewAsync(includeLibraries: true);
            var nodeB = graph.Nodes.FirstOrDefault(n => n.Name is "service-b" or "b");
            var nodeA = graph.Nodes.FirstOrDefault(n => n.Name is "service-a" or "a");

            nodeB.Should().NotBeNull();
            nodeA.Should().NotBeNull();
            nodeB!.Properties?.GetValueOrDefault("project_type").Should().Be("go");
            nodeA!.Properties?.GetValueOrDefault("project_type").Should().Be("go");

            // 3. Verify graph edge exists in architecture view output
            var edge = graph.Edges.FirstOrDefault(e => e.Source == nodeB.Id && e.Target == nodeA.Id);

            edge.Should().NotBeNull("ArchitectureViewEngine should output edge from service-b to service-a");

            // 4. Verify neighborhood query for service-b
            var hood = await new ArchitectureViewEngine(db).GetServiceFlowViewAsync("service-b",
                includeLibraries: true);

            (hood.Nodes.Any(n => n.Name is "service-a" or "a")).Should().BeTrue("service-a should appear in neighborhood outbound of service-b");
            var centerNode = hood.Nodes.FirstOrDefault(n => n.Name is "service-b" or "b");
            centerNode?.Properties?.GetValueOrDefault("project_type").Should().Be("go");
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

            nodesCount.Should().BeGreaterThan(0);

            // Verify Project -> Project DEPENDS_ON exists in database
            var query =
                "MATCH (p1:Project {name: 'web-app'})-[r:DEPENDS_ON]->(p2:Project {name: 'common-lib'}) RETURN count(r) AS cnt";
            var json = await db.ExecuteQueryAsync(query);

            using var doc = JsonDocument.Parse(json);

            var cnt = doc.RootElement[0].GetProperty("cnt").GetInt64();

            cnt.Should().Be(1, "web-app should depend on common-lib via @myorg/common-lib package mapping");

            // Verify architecture view includes edge and proper project_type == 'typescript'
            var graph = await new ArchitectureViewEngine(db).GetSystemContextViewAsync(includeLibraries: true);
            var nodeApp = graph.Nodes.FirstOrDefault(n => n.Name == "web-app");
            var nodeLib = graph.Nodes.FirstOrDefault(n => n.Name == "common-lib");

            nodeApp.Should().NotBeNull();
            nodeLib.Should().NotBeNull();
            nodeApp!.Properties?.GetValueOrDefault("project_type").Should().Be("typescript");
            nodeLib!.Properties?.GetValueOrDefault("project_type").Should().Be("typescript");

            var edge = graph.Edges.FirstOrDefault(e => e.Source == nodeApp.Id && e.Target == nodeLib.Id);

            edge.Should().NotBeNull("ArchitectureViewEngine should output edge from web-app to common-lib");
            edge!.Kind.Should().Be("LIBRARY");
            edge.Properties?.GetValueOrDefault("dependency_type").Should().Be("library");
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

            // Seed Projects & Database
            using var db = new SqliteGraphClient(dbPath);

            var nodes = new List<Node>
            {
                new Node("proj:client", "Project",
                    new Dictionary<string, object>
                    {
                        ["name"] = "ClientSvc", ["path"] = "/src/client", ["project_type"] = "csharp"
                    }),
                new Node("proj:server", "Project",
                    new Dictionary<string, object>
                    {
                        ["name"] = "ServerSvc", ["path"] = "/src/server", ["project_type"] = "csharp"
                    }),
                new Node("proj:common", "Project",
                    new Dictionary<string, object>
                    {
                        ["name"] = "CommonLib", ["path"] = "/src/common", ["project_type"] = "library"
                    }),
                new Node("db:main", "Database",
                    new Dictionary<string, object> { ["name"] = "AppDb", ["db_type"] = "PostgreSQL" }),
            };
            await db.UploadNodesAsync(nodes);

            // Seed relationships with dependency_type
            var rels = new List<Relationship>
            {
                new Relationship("proj:client", "proj:server", "DEPENDS_ON",
                    new Dictionary<string, object>
                    {
                        ["dependency_type"] = "service_call", ["kind"] = "DEPENDS_ON"
                    }),
                new Relationship("proj:client", "proj:common", "DEPENDS_ON",
                    new Dictionary<string, object>
                    {
                        ["dependency_type"] = "library", ["kind"] = "DEPENDS_ON"
                    }),
                new Relationship("proj:client", "db:main", "USES_DB",
                    new Dictionary<string, object> { ["kind"] = "USES_DB" }),
            };
            await db.UploadRelationshipsAsync(rels);

            // Test Architecture Graph
            var arch = await new ArchitectureViewEngine(db).GetSystemContextViewAsync(includeLibraries: true);

            var svcEdge = arch.Edges.FirstOrDefault(e => e.Source == "proj:client" && e.Target == "proj:server");
            svcEdge.Should().NotBeNull();
            svcEdge!.Kind.Should().Be("SERVICE_CALL");
            svcEdge.Properties?.GetValueOrDefault("dependency_type").Should().Be("service_call");

            var libEdge = arch.Edges.FirstOrDefault(e => e.Source == "proj:client" && e.Target == "proj:common");
            libEdge.Should().NotBeNull();
            libEdge!.Kind.Should().Be("LIBRARY");
            libEdge.Properties?.GetValueOrDefault("dependency_type").Should().Be("library");

            var dbEdge = arch.Edges.FirstOrDefault(e => e.Source == "proj:client" && e.Target == "db:main");
            dbEdge.Should().NotBeNull();
            dbEdge!.Kind.Should().Be("USES_DB");
            dbEdge.Properties?.GetValueOrDefault("dependency_type").Should().Be("database");

            // Test Neighborhood Graph for ClientSvc
            var hood = await new ArchitectureViewEngine(db).GetServiceFlowViewAsync("proj:client",
                includeLibraries: true);

            var hoodSvcEdge =
                hood.Edges.FirstOrDefault(e => e.Source == "proj:client" && e.Target == "proj:server");
            hoodSvcEdge.Should().NotBeNull();
            hoodSvcEdge!.Kind.Should().Be("SERVICE_CALL");
            hoodSvcEdge.Properties?.GetValueOrDefault("dependency_type").Should().Be("service_call");

            var hoodLibEdge =
                hood.Edges.FirstOrDefault(e => e.Source == "proj:client" && e.Target == "proj:common");
            hoodLibEdge.Should().NotBeNull();
            hoodLibEdge!.Kind.Should().Be("LIBRARY");
            hoodLibEdge.Properties?.GetValueOrDefault("dependency_type").Should().Be("library");

            var hoodDbEdge = hood.Edges.FirstOrDefault(e => e.Source == "proj:client" && e.Target == "db:main");
            hoodDbEdge.Should().NotBeNull();
            hoodDbEdge!.Kind.Should().Be("USES_DB");
            hoodDbEdge.Properties?.GetValueOrDefault("dependency_type").Should().Be("database");
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
    public async Task Test_DotNet_ProjectReference_DependencyType_Detected_As_Library()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "DotNetRefTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempWorkspace);
        try
        {
            var dbPath = Path.Combine(tempWorkspace, "graph.db").Replace('\\', '/');

            // Simulate Dedalos-like solution: API project references Core and Models
            using var db = new SqliteGraphClient(dbPath);

            var nodes = new List<Node>
            {
                new Node("workspace:project:Dobco.PACSONWEB3.API:", "Project",
                    new Dictionary<string, object>
                    {
                        ["name"] = "Dobco.PACSONWEB3.API",
                        ["path"] = "Dobco.PACSONWEB3.API",
                        ["project_type"] = "csharp",
                        ["framework"] = "ASP.NET Core"
                    }),
                new Node("workspace:project:Dobco.PACSONWEB3.Core:", "Project", new Dictionary<string, object>
                {
                    ["name"] = "Dobco.PACSONWEB3.Core",
                    ["path"] = "Dobco.PACSONWEB3.Core",
                    ["project_type"] = "csharp",
                    ["framework"] = "ASP.NET Core" // even with legacy misassigned framework
                }),
                new Node("workspace:project:Dobco.PACSONWEB3.Models:", "Project",
                    new Dictionary<string, object>
                    {
                        ["name"] = "Dobco.PACSONWEB3.Models",
                        ["path"] = "Dobco.PACSONWEB3.Models",
                        ["project_type"] = "csharp"
                    }),
            };
            await db.UploadNodesAsync(nodes);

            // Seed direct project references (even without dependency_type specified, simulating older scans)
            var rels = new List<Relationship>
            {
                new Relationship("workspace:project:Dobco.PACSONWEB3.API:",
                    "workspace:project:Dobco.PACSONWEB3.Core:", "DEPENDS_ON",
                    new Dictionary<string, object> { ["kind"] = "DEPENDS_ON" }),
                new Relationship("workspace:project:Dobco.PACSONWEB3.API:",
                    "workspace:project:Dobco.PACSONWEB3.Models:", "DEPENDS_ON",
                    new Dictionary<string, object> { ["kind"] = "DEPENDS_ON" }),
            };
            await db.UploadRelationshipsAsync(rels);

            // Test Project Flow / Neighborhood Graph for API project
            var flowGraph =
                await new ArchitectureViewEngine(db).GetServiceFlowViewAsync(
                    "workspace:project:Dobco.PACSONWEB3.API:", includeLibraries: true);

            var coreEdge =
                flowGraph.Edges.FirstOrDefault(e => e.Target == "workspace:project:Dobco.PACSONWEB3.Core:");
            coreEdge.Should().NotBeNull("Edge to Dobco.PACSONWEB3.Core should exist");

            coreEdge!.Kind.Should().Be("LIBRARY", "Edge to Core must be classified as LIBRARY, not SERVICE_CALL");
            coreEdge.Properties?.GetValueOrDefault("dependency_type").Should().Be("library");

            var modelsEdge =
                flowGraph.Edges.FirstOrDefault(e => e.Target == "workspace:project:Dobco.PACSONWEB3.Models:");
            modelsEdge.Should().NotBeNull("Edge to Dobco.PACSONWEB3.Models should exist");

            modelsEdge!.Kind.Should().Be("LIBRARY", "Edge to Models must be classified as LIBRARY, not SERVICE_CALL");
            modelsEdge.Properties?.GetValueOrDefault("dependency_type").Should().Be("library");
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
    public async Task Test_ExternalPackages_AreSurfacedInNeighborhoodAndArchGraph()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_ext_pkg_test_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            var dbPath = Path.Combine(tempWorkspace, "graph.db").Replace('\\', '/');

            using var db = new SqliteGraphClient(dbPath);

            var nodes = new List<Node>
            {
                new Node("workspace:project:Admin:", "Project",
                    new Dictionary<string, object>
                    {
                        ["name"] = "lidoma-admin-application",
                        ["path"] = "Admin",
                        ["project_type"] = "typescript"
                    }),
                new Node("workspace:package:@angular/core", "Package",
                    new Dictionary<string, object>
                    {
                        ["name"] = "@angular/core", ["version"] = "^13.0.1", ["type"] = "npm"
                    }),
                new Node("workspace:package:rxjs", "Package",
                    new Dictionary<string, object>
                    {
                        ["name"] = "rxjs", ["version"] = "^7.5.5", ["type"] = "npm"
                    })
            };
            await db.UploadNodesAsync(nodes);

            var rels = new List<Relationship>
            {
                new Relationship("workspace:project:Admin:", "workspace:package:@angular/core", "DEPENDS_ON",
                    new Dictionary<string, object> { ["kind"] = "DEPENDS_ON" }),
                new Relationship("workspace:project:Admin:", "workspace:package:rxjs", "DEPENDS_ON",
                    new Dictionary<string, object> { ["kind"] = "DEPENDS_ON" })
            };
            await db.UploadRelationshipsAsync(rels);

            // 1. Verify Neighborhood Graph contains external packages
            var hood = await new ArchitectureViewEngine(db).GetServiceFlowViewAsync("lidoma-admin-application",
                includeLibraries: true);
            hood.Nodes.Count.Should().Be(3, "Should have center project + 2 packages");

            var angularNode = hood.Nodes.FirstOrDefault(n => n.Id == "workspace:package:@angular/core");
            angularNode.Should().NotBeNull("@angular/core should exist in neighborhood nodes");
            angularNode!.Kind.Should().Be(NodeKind.Package);
            angularNode.DisplayName.Should().Be("@angular/core@^13.0.1");
            angularNode.Properties?.GetValueOrDefault("column").Should().Be("right");
            angularNode.Properties?.GetValueOrDefault("is_library").Should().Be("true");
            angularNode.Properties?.GetValueOrDefault("package_type").Should().Be("npm");

            var angularEdge = hood.Edges.FirstOrDefault(e => e.Target == "workspace:package:@angular/core");
            angularEdge.Should().NotBeNull("Edge to @angular/core should exist");
            angularEdge!.Kind.Should().Be("LIBRARY");
            angularEdge.Category.Should().Be("library");

            var centerNode = hood.Nodes.FirstOrDefault(n => n.Id == "workspace:project:Admin:");
            centerNode?.Properties?.GetValueOrDefault("package_count").Should().Be("2");

            // 2. Verify Architecture Graph contains package_count on project and package nodes/edges
            var arch = await new ArchitectureViewEngine(db).GetSystemContextViewAsync(includeLibraries: true);
            var archProj = arch.Nodes.FirstOrDefault(n => n.Id == "workspace:project:Admin:");
            archProj?.Properties?.GetValueOrDefault("package_count").Should().Be("2");

            (arch.Nodes.Any(n => n.Kind == NodeKind.Package)).Should().BeTrue("Architecture graph should contain package nodes");

            (arch.Edges.Any(e => e.Target == "workspace:package:@angular/core" && e.Kind == "LIBRARY")).Should().BeTrue("Architecture graph should contain LIBRARY edge to package");
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
