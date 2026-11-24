using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Cypher.Common;
using CodeExplorer.Parsers.CSharp;
using CodeExplorer.Parsers.TypeScript;
using CodeExplorer.Parsers.TypeScript.Components;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class CatchClauseExtractionTests
{
    private static async Task<List<ScopeNode>> ExtractTsScopesAsync(string code, string fileName = "sample.service.ts")
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_catch_ts_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, fileName);
            await File.WriteAllTextAsync(filePath, code);
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var ctx = new ParsingContext(tempDir, tempDir, new InMemoryGraphRepository(), channel);
            using var syntaxTree = await new TypeScriptFileParser().ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var isTest = WorkspaceConventions.IsTestFilePath(filePath) || new JavaScriptTestRunnerComponentParser().IsTestSource(syntaxTree);
            syntaxTree.FileNode!.IsTest = isTest;
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, isTest: isTest);
            return CollectScopes(syntaxTree.FileNode.Children);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static async Task<(List<ScopeNode> Scopes, List<FunctionNode> Functions)> ExtractCsScopesAndFunctionsAsync(string code, string fileName = "SampleService.cs")
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_catch_cs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, fileName);
            await File.WriteAllTextAsync(filePath, code);
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var ctx = new ParsingContext(tempDir, tempDir, new InMemoryGraphRepository(), channel);
            using var syntaxTree = await new CSharpFileParser().ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var isTest = WorkspaceConventions.IsTestFilePath(filePath);
            syntaxTree.FileNode!.IsTest = isTest;
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, isTest: isTest);
            return (CollectScopes(syntaxTree.FileNode.Children), CollectFunctions(syntaxTree.FileNode.Children));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static async Task<List<ScopeNode>> ExtractTsCatchClausesAsync(string code, string fileName = "sample.service.ts") =>
        (await ExtractTsScopesAsync(code, fileName)).Where(s => s.ScopeKind == ScopeKind.Catch).ToList();

    private static async Task<List<ScopeNode>> ExtractCsCatchClausesAsync(string code, string fileName = "SampleService.cs") =>
        (await ExtractCsScopesAndFunctionsAsync(code, fileName)).Scopes.Where(s => s.ScopeKind == ScopeKind.Catch).ToList();

    private static List<ScopeNode> CollectScopes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<ScopeNode>();
        foreach (var node in nodes)
        {
            if (node is ScopeNode sn)
            {
                result.Add(sn);
            }
            result.AddRange(CollectScopes(node.Children));
        }
        return result;
    }

    private static List<FunctionNode> CollectFunctions(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<FunctionNode>();
        foreach (var node in nodes)
        {
            if (node is FunctionNode fn)
            {
                result.Add(fn);
            }
            result.AddRange(CollectFunctions(node.Children));
        }
        return result;
    }

    [Test]
    public async Task TypeScript_EmptyCatch_IsFlaggedAsEmptyAndNoComment()
    {
        var code = """
            export class Worker {
                run() {
                    try {
                        doSomething();
                    } catch (e) {
                    }
                }
            }
            """;

        var catches = await ExtractTsCatchClausesAsync(code);
        catches.Should().HaveCount(1);
        catches[0].ScopeKind.Should().Be(ScopeKind.Catch);
        catches[0].IsEmpty.Should().BeTrue();
        catches[0].HasComment.Should().BeFalse();
        catches[0].StatementCount.Should().Be(0);
    }

    [Test]
    public async Task TypeScript_EmptyCatch_WithComment_HasCommentIsTrue()
    {
        var code = """
            export class Worker {
                run() {
                    try {
                        doSomething();
                    } catch (e) {
                        // ignored: expected fallback
                    }
                }
            }
            """;

        var catches = await ExtractTsCatchClausesAsync(code);
        catches.Should().HaveCount(1);
        catches[0].IsEmpty.Should().BeTrue();
        catches[0].HasComment.Should().BeTrue();
    }

    [Test]
    public async Task TypeScript_NonEmptyCatch_IsEmptyIsFalse()
    {
        var code = """
            export class Worker {
                run() {
                    try {
                        doSomething();
                    } catch (e) {
                        console.error(e);
                    }
                }
            }
            """;

        var catches = await ExtractTsCatchClausesAsync(code);
        catches.Should().HaveCount(1);
        catches[0].IsEmpty.Should().BeFalse();
        catches[0].StatementCount.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task TypeScript_SpecFile_DoesNotProduceCatchClauses()
    {
        var code = """
            describe('Worker', () => {
                it('catches error', () => {
                    try {
                        doSomething();
                    } catch (e) {
                    }
                });
            });
            """;

        var catches = await ExtractTsCatchClausesAsync(code, "worker.service.spec.ts");
        catches.Should().BeEmpty();
    }

    [Test]
    public async Task CSharp_EmptyCatch_IsFlaggedAsEmptyAndNoComment()
    {
        var code = """
            public class Worker
            {
                public void Run()
                {
                    try
                    {
                        DoSomething();
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
            """;

        var catches = await ExtractCsCatchClausesAsync(code);
        catches.Should().HaveCount(1);
        catches[0].ScopeKind.Should().Be(ScopeKind.Catch);
        catches[0].TargetType.Should().Contain("Exception");
        catches[0].IsEmpty.Should().BeTrue();
        catches[0].HasComment.Should().BeFalse();
        catches[0].StatementCount.Should().Be(0);
    }

    [Test]
    public async Task CSharp_EmptyCatch_WithComment_HasCommentIsTrue()
    {
        var code = """
            public class Worker
            {
                public void Run()
                {
                    try
                    {
                        DoSomething();
                    }
                    catch (Exception)
                    {
                        /* ignored: transient network spike */
                    }
                }
            }
            """;

        var catches = await ExtractCsCatchClausesAsync(code);
        catches.Should().HaveCount(1);
        catches[0].IsEmpty.Should().BeTrue();
        catches[0].HasComment.Should().BeTrue();
    }

    [Test]
    public async Task CSharp_ScopeTree_ExtractsTryCatchFinallyLoopResourceAndComputesComplexityAndAsyncLoopCost()
    {
        var code = """
            using System;
            using System.Threading.Tasks;

            public class OrderBatchProcessor
            {
                public async Task ProcessOrdersAsync(string[] orderIds)
                {
                    using (var conn = OpenConnection())
                    {
                        foreach (var id in orderIds)
                        {
                            try
                            {
                                if (id.Length > 0)
                                {
                                    await SaveOrderAsync(id);
                                    await AuditOrderAsync(id);
                                }
                            }
                            catch (InvalidOperationException ex)
                            {
                                LogError(ex);
                            }
                            finally
                            {
                                CleanupItem(id);
                            }
                        }
                    }
                }
            }
            """;

        var (scopes, functions) = await ExtractCsScopesAndFunctionsAsync(code);
        functions.Should().ContainSingle(f => f.Name == "ProcessOrdersAsync");
        var fn = functions.Single(f => f.Name == "ProcessOrdersAsync");

        scopes.Should().Contain(s => s.ScopeKind == ScopeKind.Resource);
        scopes.Should().Contain(s => s.ScopeKind == ScopeKind.Loop && s.LoopDepth == 1);
        scopes.Should().Contain(s => s.ScopeKind == ScopeKind.Try && s.LoopDepth == 1);
        scopes.Should().Contain(s => s.ScopeKind == ScopeKind.Branch && s.LoopDepth == 1 && s.AwaitCount == 2);
        scopes.Should().Contain(s => s.ScopeKind == ScopeKind.Catch && s.TargetType.Contains("InvalidOperationException"));
        scopes.Should().Contain(s => s.ScopeKind == ScopeKind.Finally);

        fn.MaxLoopDepth.Should().Be(1);
        fn.LoopAwaitCount.Should().Be(2);
        fn.CyclomaticComplexity.Should().BeGreaterThanOrEqualTo(4);
        fn.CognitiveComplexity.Should().BeGreaterThanOrEqualTo(6);
        fn.MaxNestingDepth.Should().BeGreaterThanOrEqualTo(4);
    }

    [Test]
    public async Task ScopeStarterGates_EvaluateAsyncInLoop_CognitiveComplexity_And_ResourceInLoop_OnIndexedWorkspace()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_scope_gates_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dbFile = Path.Combine(tempDir, "graph.db");

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "Billing.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

            await File.WriteAllTextAsync(Path.Combine(tempDir, "OrderSyncService.cs"), """
                using System;
                using System.IO;
                using System.Threading.Tasks;

                public class OrderSyncService
                {
                    public async Task SyncAllOrdersAsync(string[] orderIds)
                    {
                        foreach (var orderId in orderIds)
                        {
                            using (var stream = new MemoryStream())
                            {
                                if (orderId.Length > 0)
                                {
                                    await FetchOrderAsync(orderId);
                                    await SaveOrderAsync(orderId);
                                }
                            }
                        }
                    }

                    private Task FetchOrderAsync(string id) => Task.CompletedTask;
                    private Task SaveOrderAsync(string id) => Task.CompletedTask;
                }
                """);

            await using var client = new SqliteGraphClient(dbFile);
            WorkspaceIndexer.AutoRegisterParsers();
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempDir, clear: true);

            // 1. SIG-SCOPE-001: Sequential await inside loop
            var asyncLoopRes = await client.ExecuteCypherAsync("""
                MATCH (fn:Function)-[:DECLARES|CONTAINS*1..6]->(s:Scope)
                WHERE NOT coalesce(fn.is_test, false)
                  AND s.loop_depth > 0
                  AND s.await_count > 0
                RETURN fn.name AS fnName, fn.loop_await_count AS totalLoopAwaits, s.kind AS scopeKind, s.line AS line, s.loop_depth AS loopDepth, s.await_count AS awaitCount
                """);
            asyncLoopRes.Rows.Should().ContainSingle();
            asyncLoopRes.Rows[0].GetStringProp("fnName").Should().Be("SyncAllOrdersAsync");
            asyncLoopRes.Rows[0].GetIntProp("totalLoopAwaits").Should().Be(2);
            asyncLoopRes.Rows[0].GetIntProp("awaitCount").Should().Be(2);
            asyncLoopRes.Rows[0].GetIntProp("loopDepth").Should().Be(1);

            // 2. SIG-SCOPE-003: Resource allocation (using) inside loop
            var resInLoop = await client.ExecuteCypherAsync("""
                MATCH (fn:Function)-[:DECLARES|CONTAINS*1..6]->(s:Scope)
                WHERE NOT coalesce(fn.is_test, false)
                  AND s.kind = 'resource' AND s.loop_depth > 0
                RETURN fn.name AS fnName, s.kind AS kind, s.target_type AS targetType, s.line AS line, s.loop_depth AS loopDepth
                """);
            resInLoop.Rows.Should().ContainSingle();
            resInLoop.Rows[0].GetStringProp("fnName").Should().Be("SyncAllOrdersAsync");
            resInLoop.Rows[0].GetStringProp("targetType").Should().Be("MemoryStream");
            resInLoop.Rows[0].GetIntProp("loopDepth").Should().Be(1);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Test]
    public async Task SyntaxError_InSourceFile_RecordsScanIssueNodeAndAttachesToFileNode()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_scan_issue_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var brokenCode = "export class BrokenService { public run( { const x = ; } }";
            await File.WriteAllTextAsync(Path.Combine(tempDir, "package.json"), "{\"name\":\"test-pkg\"}");
            var filePath = Path.Combine(tempDir, "broken.service.ts");
            await File.WriteAllTextAsync(filePath, brokenCode);
            var dbClient = new InMemoryGraphRepository();
            var indexer = new WorkspaceIndexer(dbClient);
            var (nodesCount, _, nodesByKind) = await indexer.IndexAsync(tempDir, clear: true);

            nodesCount.Should().BeGreaterThan(0);
            nodesByKind.Should().ContainKey(OntologyConstants.NodeLabels.ScanIssue);
            nodesByKind[OntologyConstants.NodeLabels.ScanIssue].Should().BeGreaterThan(0);

            var issueNodes = dbClient.UploadedNodes.Where(n => n.Kind == NodeKind.ScanIssue).ToList();
            issueNodes.Should().NotBeEmpty();
            issueNodes[0].Properties["category"].ToString().Should().Be("syntax_error");
            issueNodes[0].Properties["stage"].ToString().Should().Be("Layer3_Syntactic");
            issueNodes[0].Properties["path"].ToString().Should().EndWith("broken.service.ts");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
