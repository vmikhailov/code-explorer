using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;

namespace CodeExplorer.Tests;

[TestFixture]
public class InMemoryGraphTests
{
    private string _tempDbFile = null!;
    private SqliteGraphClient _client = null!;
    private InMemoryCodeExplorerRepository _repository = null!;

    [SetUp]
    public async Task Setup()
    {
        _tempDbFile = Path.Combine(Path.GetTempPath(), $"codeexplorer_test_{Guid.NewGuid()}.db");
        _client = new SqliteGraphClient(_tempDbFile);
        await _client.CreateIndicesAsync();
        _repository = new InMemoryCodeExplorerRepository(_client);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        if (File.Exists(_tempDbFile))
        {
            try
            {
                File.Delete(_tempDbFile);
            }
            catch
            {
                // Ignore
            }
        }
    }

    [Test]
    public async Task Test_SqlitePersist_And_GraphTraversal()
    {
        // 1. Create a workspace
        var wsId = await _client.GetOrCreateWorkspaceIdAsync("/host/my-workspace");
        Assert.That(wsId, Is.EqualTo("1"));

        await _client.SaveEmptyWorkspaceNodeAsync(wsId, "/host/my-workspace");

        // 2. Add files, types, functions, queries and tables
        var nodes = new List<Node>
        {
            new Node("1:file:src/Program.cs", OntologyConstants.NodeLabels.File, new() { ["name"] = "Program.cs", ["path"] = "src/Program.cs" }),
            new Node("1:file:src/Service.cs", OntologyConstants.NodeLabels.File, new() { ["name"] = "Service.cs", ["path"] = "src/Service.cs" }),

            new Node("1:symbol:src/Program.cs:Type:Program:5", OntologyConstants.NodeLabels.Type, new() { ["name"] = "Program", ["kind"] = "class", ["symbol"] = "Program" }),
            new Node("1:symbol:src/Service.cs:Type:IService:3", OntologyConstants.NodeLabels.Type, new() { ["name"] = "IService", ["kind"] = "interface", ["symbol"] = "IService" }),
            new Node("1:symbol:src/Service.cs:Type:Service:10", OntologyConstants.NodeLabels.Type, new() { ["name"] = "Service", ["kind"] = "class", ["symbol"] = "Service" }),

            new Node("1:symbol:src/Program.cs:Function:Main:6", OntologyConstants.NodeLabels.Function, new() { ["name"] = "Main", ["symbol"] = "Program.Main", ["start_line"] = 6, ["end_line"] = 12 }),
            new Node("1:symbol:src/Service.cs:Function:DoWork:12", OntologyConstants.NodeLabels.Function, new() { ["name"] = "DoWork", ["symbol"] = "Service.DoWork", ["start_line"] = 12, ["end_line"] = 18 }),
            new Node("1:symbol:src/Service.cs:Function:SaveData:20", OntologyConstants.NodeLabels.Function, new() { ["name"] = "SaveData", ["symbol"] = "Service.SaveData", ["start_line"] = 20, ["end_line"] = 24 }),

            new Node("1:db:sqlite:users_db", OntologyConstants.NodeLabels.Database, new() { ["name"] = "users_db", ["db_type"] = "sqlite" }),
            new Node("1:symbol:src/Service.cs:Query:InsertUser:22", OntologyConstants.NodeLabels.Query, new() { ["name"] = "InsertUser", ["query_text"] = "INSERT INTO users (name) VALUES ($name)", ["path"] = "src/Service.cs" }),
            new Node("1:symbol:table:users", OntologyConstants.NodeLabels.Table, new() { ["name"] = "users" }),
            
            new Node("1:project:src/", OntologyConstants.NodeLabels.Project, new() { ["name"] = "MyProject", ["project_type"] = "csharp" }),
            new Node("1:folder:/host/my-workspace/src", OntologyConstants.NodeLabels.Folder, new() { ["name"] = "src" })
        };

        await _client.UploadNodesAsync(nodes);

        var rels = new List<Relationship>
        {
            // Project structure
            new Relationship("1:project:src/", "1:folder:/host/my-workspace/src", OntologyConstants.Relationships.LocatedIn, new()),
            new Relationship("1:folder:/host/my-workspace/src", "1:file:src/Program.cs", OntologyConstants.Relationships.Contains, new()),
            new Relationship("1:folder:/host/my-workspace/src", "1:file:src/Service.cs", OntologyConstants.Relationships.Contains, new()),

            // File declarations
            new Relationship("1:symbol:src/Program.cs:Type:Program:5", "1:file:src/Program.cs", OntologyConstants.Relationships.DeclaredIn, new()),
            new Relationship("1:symbol:src/Service.cs:Type:IService:3", "1:file:src/Service.cs", OntologyConstants.Relationships.DeclaredIn, new()),
            new Relationship("1:symbol:src/Service.cs:Type:Service:10", "1:file:src/Service.cs", OntologyConstants.Relationships.DeclaredIn, new()),

            new Relationship("1:symbol:src/Program.cs:Function:Main:6", "1:file:src/Program.cs", OntologyConstants.Relationships.DeclaredIn, new()),
            new Relationship("1:symbol:src/Service.cs:Function:DoWork:12", "1:file:src/Service.cs", OntologyConstants.Relationships.DeclaredIn, new()),
            new Relationship("1:symbol:src/Service.cs:Function:SaveData:20", "1:file:src/Service.cs", OntologyConstants.Relationships.DeclaredIn, new()),

            // Classes methods
            new Relationship("1:symbol:src/Program.cs:Type:Program:5", "1:symbol:src/Program.cs:Function:Main:6", OntologyConstants.Relationships.HasMethod, new()),
            new Relationship("1:symbol:src/Service.cs:Type:Service:10", "1:symbol:src/Service.cs:Function:DoWork:12", OntologyConstants.Relationships.HasMethod, new()),
            new Relationship("1:symbol:src/Service.cs:Type:Service:10", "1:symbol:src/Service.cs:Function:SaveData:20", OntologyConstants.Relationships.HasMethod, new()),

            // Interface implements
            new Relationship("1:symbol:src/Service.cs:Type:Service:10", "1:symbol:src/Service.cs:Type:IService:3", OntologyConstants.Relationships.Implements, new()),

            // Call graph
            new Relationship("1:symbol:src/Program.cs:Function:Main:6", "1:symbol:src/Service.cs:Function:DoWork:12", OntologyConstants.Relationships.Calls, new()),
            new Relationship("1:symbol:src/Service.cs:Function:DoWork:12", "1:symbol:src/Service.cs:Function:SaveData:20", OntologyConstants.Relationships.Calls, new()),

            // Database relations
            new Relationship("1:symbol:src/Service.cs:Function:SaveData:20", "1:symbol:src/Service.cs:Query:InsertUser:22", OntologyConstants.Relationships.Contains, new()),
            new Relationship("1:symbol:src/Service.cs:Query:InsertUser:22", "1:symbol:table:users", OntologyConstants.Relationships.DependsOn, new()),
            new Relationship("1:file:src/Service.cs", "1:db:sqlite:users_db", OntologyConstants.Relationships.UsesDb, new())
        };

        await _client.UploadRelationshipsAsync(rels);

        // 3. Test File Outline
        var outlineJson = await _repository.GetFileOutlineAsync("src/Service.cs", "/host/my-workspace");
        using (var doc = JsonDocument.Parse(outlineJson))
        {
            var results = doc.RootElement.GetProperty("results");
            Assert.That(results.GetArrayLength(), Is.EqualTo(4)); // IService, Service, DoWork, SaveData
            Assert.That(results[0].GetProperty("name").GetString(), Is.EqualTo("IService"));
            Assert.That(results[0].GetProperty("type").GetString(), Is.EqualTo("Interface"));
        }

        // 4. Test Symbol Search
        var searchJson = await _repository.FindSymbolAsync("Main", "Function", "/host/my-workspace");
        using (var doc = JsonDocument.Parse(searchJson))
        {
            var results = doc.RootElement.GetProperty("results");
            Assert.That(results.GetArrayLength(), Is.EqualTo(1));
            Assert.That(results[0].GetProperty("fullName").GetString(), Is.EqualTo("Program.Main"));
        }

        // 5. Test Call Chain BFS Pathfinder (Main -> DoWork -> SaveData)
        var chainJson = await _repository.GetCallChainAsync("Program.Main", "Service.SaveData", 5, "/host/my-workspace");
        using (var doc = JsonDocument.Parse(chainJson))
        {
            var results = doc.RootElement.GetProperty("results");
            Assert.That(results.GetArrayLength(), Is.EqualTo(1));
            var chain = results[0].GetProperty("chain");
            Assert.That(chain.GetArrayLength(), Is.EqualTo(3));
            Assert.That(chain[0].GetProperty("props").GetProperty("name").GetString(), Is.EqualTo("Main"));
            Assert.That(chain[1].GetProperty("props").GetProperty("name").GetString(), Is.EqualTo("DoWork"));
            Assert.That(chain[2].GetProperty("props").GetProperty("name").GetString(), Is.EqualTo("SaveData"));
        }

        // 6. Test Polymorphism DI Resolution
        var resolveJson = await _repository.ResolveCallTargetAsync("IService", "DoWork", "/host/my-workspace");
        using (var doc = JsonDocument.Parse(resolveJson))
        {
            var results = doc.RootElement.GetProperty("results");
            Assert.That(results.GetArrayLength(), Is.EqualTo(1));
            Assert.That(results[0].GetProperty("className").GetString(), Is.EqualTo("Service"));
            Assert.That(results[0].GetProperty("methodName").GetString(), Is.EqualTo("DoWork"));
        }

        // 7. Test Blast Radius Impact Analysis (Impact of changing Service.DoWork -> Program.Main should be affected)
        var impactJson = await _repository.AnalyzeCodeImpactAsync("Service.DoWork", "/host/my-workspace");
        using (var doc = JsonDocument.Parse(impactJson))
        {
            var results = doc.RootElement.GetProperty("results");
            Assert.That(results.GetArrayLength(), Is.EqualTo(1));
            Assert.That(results[0].GetProperty("dependentName").GetString(), Is.EqualTo("Main"));
        }

        // 8. Test Data Lineage (Trace table users -> InsertUser query -> SaveData method -> calls)
        var lineageJson = await _repository.InspectDataLineageAsync("users", "/host/my-workspace");
        using (var doc = JsonDocument.Parse(lineageJson))
        {
            var results = doc.RootElement.GetProperty("results");
            Assert.That(results.GetArrayLength(), Is.EqualTo(1));
            Assert.That(results[0].GetProperty("queryName").GetString(), Is.EqualTo("InsertUser"));
            Assert.That(results[0].GetProperty("parentName")[0].GetString(), Is.EqualTo("SaveData"));
            var callers = results[0].GetProperty("callingSymbols");
            Assert.That(callers.GetArrayLength(), Is.EqualTo(2)); // DoWork, Main
            Assert.That(callers[0].GetString(), Is.EqualTo("DoWork").Or.EqualTo("Main"));
        }

        // 9. Test Ingress/Entry Points
        var entryPointsJson = await _repository.GetProjectEntryPointsAsync("MyProject", "/host/my-workspace");
        using (var doc = JsonDocument.Parse(entryPointsJson))
        {
            var results = doc.RootElement.GetProperty("results");
            Assert.That(results.GetArrayLength(), Is.EqualTo(0)); // No handler/controller suffix in example
        }

        // 10. Test Refactoring Opportunities (Main and IService have callers. DoWork, SaveData, Program have callers. Wait, table, queries, etc. aren't checked. Let's see: Program is dead code because 0 incoming CALLS/USES_TYPE).
        var refactorJson = await _repository.FindRefactoringOpportunitiesAsync("MyProject", "dead_code", "/host/my-workspace");
        using (var doc = JsonDocument.Parse(refactorJson))
        {
            var results = doc.RootElement.GetProperty("results");
            Assert.That(results.GetArrayLength(), Is.GreaterThan(0));
            // Verify anomaly
            var deadNode = results[0];
            Assert.That(deadNode.GetProperty("anomalyType").GetString(), Is.EqualTo("dead_code"));
        }
    }
}
