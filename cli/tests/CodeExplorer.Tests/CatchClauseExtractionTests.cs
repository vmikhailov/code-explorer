using System.Threading.Channels;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Components.Parsers;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class CatchClauseExtractionTests
{
    private static async Task<List<CatchClauseNode>> ExtractTsCatchClausesAsync(string code, string fileName = "sample.service.ts")
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_catch_ts_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, fileName);
            await File.WriteAllTextAsync(filePath, code);
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var ctx = new ParsingContext(tempDir, tempDir, new InMemoryGraphClient(), channel);
            using var syntaxTree = await new TypeScriptParser().ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var isTest = WorkspaceConventions.IsTestFilePath(filePath) || new JavaScriptTestRunnerComponentParser().IsTestSource(syntaxTree);
            syntaxTree.FileNode!.IsTest = isTest;
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, isTest: isTest);
            return Collect(syntaxTree.FileNode.Children);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static async Task<List<CatchClauseNode>> ExtractCsCatchClausesAsync(string code, string fileName = "SampleService.cs")
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_catch_cs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, fileName);
            await File.WriteAllTextAsync(filePath, code);
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var ctx = new ParsingContext(tempDir, tempDir, new InMemoryGraphClient(), channel);
            using var syntaxTree = await new CSharpParser().ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var isTest = WorkspaceConventions.IsTestFilePath(filePath);
            syntaxTree.FileNode!.IsTest = isTest;
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, isTest: isTest);
            return Collect(syntaxTree.FileNode.Children);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static List<CatchClauseNode> Collect(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<CatchClauseNode>();
        foreach (var node in nodes)
        {
            if (node is CatchClauseNode ccn)
            {
                result.Add(ccn);
            }
            result.AddRange(Collect(node.Children));
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
}
