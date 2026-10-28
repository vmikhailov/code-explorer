using CodeExplorer.Core.Gates.Engine;
using CodeExplorer.Core.Gates.Models;
using CodeExplorer.Core.Gates.Storage;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class EvolutionGateTests
{
    private string _tempWorkspace = null!;

    [SetUp]
    public void SetUp()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_evo_gates_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempWorkspace);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_tempWorkspace, true); } catch (IOException) { /* temp cleanup is best effort */ }
    }

    private static Node Fn(string id, string name, string path, bool isTest = false) =>
        new(id, OntologyConstants.NodeLabels.Function, new()
        {
            ["name"] = name,
            ["path"] = path,
            ["start_line"] = 10,
            ["is_test"] = isTest
        });

    private static Node Proj(string name) =>
        new("ws:p:" + name, OntologyConstants.NodeLabels.Project, new() { ["name"] = name, ["path"] = "cli/" + name });

    private async Task<GateCheckResult> RunAsync(SemanticGate gate, IEnumerable<Node> nodes, IEnumerable<Relationship> rels)
    {
        var db = Path.Combine(_tempWorkspace, Guid.NewGuid().ToString("N") + ".db");
        await using var client = new SqliteGraphClient(db);
        await client.CreateIndicesAsync();
        await client.UploadNodesAsync(nodes.ToList());
        await client.UploadRelationshipsAsync(rels.ToList());
        var engine = new GateExecutionEngine(client, new FileGateRepository(_tempWorkspace));
        return await engine.CheckGateAsync(gate);
    }

    private static Relationship Calls(string from, string to) => new(from, to, OntologyConstants.Relationships.Calls);

    private const string L3 = "src/Analysis/Layers/Layer3_Syntactic/A.cs";
    private const string L4 = "src/Analysis/Layers/Layer4_Semantic/B.cs";

    [Test]
    public async Task PipelineLayerOrder_UpwardCall_Fails()
    {
        var res = await RunAsync(BuiltInGateCatalog.PipelineLayerOrderGate,
            [Fn("a", "Parse", L3), Fn("b", "Resolve", L4)], [Calls("a", "b")]);

        res.Status.Should().Be(GateCheckStatus.FailedNew);
        res.Violations.Should().ContainSingle().Which.Message.Should().Contain("higher-order layer");
    }

    [Test]
    public async Task PipelineLayerOrder_DownwardTestAndAmbiguousCalls_Pass()
    {
        var res = await RunAsync(BuiltInGateCatalog.PipelineLayerOrderGate,
            [Fn("a", "Parse", L3), Fn("b", "Resolve", L4), Fn("c", "Push", "src/Analysis/Layers/Layer5_Global/C.cs"), Fn("t", "TestIt", L3, isTest: true)],
            [Calls("b", "a"), Calls("a", "c"), Calls("t", "b")]);

        res.Status.Should().Be(GateCheckStatus.Passed);
    }

    [Test]
    public async Task ParserProjectIsolation_SiblingParserDependency_Fails()
    {
        var res = await RunAsync(BuiltInGateCatalog.ParserProjectIsolationGate,
            [Proj("CodeExplorer.Parser.Java"), Proj("CodeExplorer.Parser.Android")],
            [new("ws:p:CodeExplorer.Parser.Java", "ws:p:CodeExplorer.Parser.Android", OntologyConstants.Relationships.DependsOn)]);

        res.Status.Should().Be(GateCheckStatus.FailedNew);
        res.Violations.Should().ContainSingle();
    }

    [Test]
    public async Task ParserProjectIsolation_CoreAndSharedDependencies_Pass()
    {
        var res = await RunAsync(BuiltInGateCatalog.ParserProjectIsolationGate,
            [Proj("CodeExplorer.Parser.Java"), Proj("CodeExplorer.Core"), Proj("CodeExplorer.Parser.Shared")],
            [
                new("ws:p:CodeExplorer.Parser.Java", "ws:p:CodeExplorer.Core", OntologyConstants.Relationships.DependsOn),
                new("ws:p:CodeExplorer.Parser.Java", "ws:p:CodeExplorer.Parser.Shared", OntologyConstants.Relationships.DependsOn),
                new("ws:p:CodeExplorer.Parser.Shared", "ws:p:CodeExplorer.Core", OntologyConstants.Relationships.DependsOn)
            ]);

        res.Status.Should().Be(GateCheckStatus.Passed);
    }

    [Test]
    public async Task ParserProjectIsolation_CoreDependingOnParser_Fails()
    {
        var res = await RunAsync(BuiltInGateCatalog.ParserProjectIsolationGate,
            [Proj("CodeExplorer.Core"), Proj("CodeExplorer.Parser.Go")],
            [new("ws:p:CodeExplorer.Core", "ws:p:CodeExplorer.Parser.Go", OntologyConstants.Relationships.DependsOn)]);

        res.Status.Should().Be(GateCheckStatus.FailedNew);
    }

    [Test]
    public async Task QuerySideSeparation_ServiceCallingPipeline_Fails()
    {
        var res = await RunAsync(BuiltInGateCatalog.QuerySideIndexingSeparationGate,
            [
                Fn("s", "BuildView", "cli/src/Core/CodeExplorer.Core/Analysis/Services/View.cs"),
                Fn("l", "CleanName", "cli/src/Core/CodeExplorer.Core/Analysis/Layers/Layer4_Semantic/E.cs")
            ],
            [Calls("s", "l")]);

        res.Status.Should().Be(GateCheckStatus.FailedNew);
    }

    [Test]
    public async Task QuerySideSeparation_ServiceCallingCommon_Passes()
    {
        var res = await RunAsync(BuiltInGateCatalog.QuerySideIndexingSeparationGate,
            [
                Fn("s", "BuildView", "cli/src/Core/CodeExplorer.Core/Analysis/Services/View.cs"),
                Fn("c", "CleanName", "cli/src/Core/CodeExplorer.Core/Common/Naming.cs")
            ],
            [Calls("s", "c")]);

        res.Status.Should().Be(GateCheckStatus.Passed);
    }

    [Test]
    public async Task SharedFileSingleRead_LaterLayerReparsing_FailsButEnricherPasses()
    {
        const string registry = "cli/src/Core/CodeExplorer.Core/Parser/Shared/SharedFileParserRegistry.cs";
        var nodes = new[]
        {
            Fn("reg", "ParseFile", registry),
            Fn("enr", "Enrich", "cli/src/Core/CodeExplorer.Core/Analysis/Layers/Layer2_Boundaries/SharedFileEnricher.cs"),
            Fn("bad", "ReadEnv", L4)
        };

        var ok = await RunAsync(BuiltInGateCatalog.SharedFileSingleReadGate, nodes, [Calls("enr", "reg")]);
        ok.Status.Should().Be(GateCheckStatus.Passed);

        var bad = await RunAsync(BuiltInGateCatalog.SharedFileSingleReadGate, nodes, [Calls("enr", "reg"), Calls("bad", "reg")]);
        bad.Status.Should().Be(GateCheckStatus.FailedNew);
        bad.Violations.Should().ContainSingle();
    }
}
