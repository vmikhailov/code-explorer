using System.Threading.Channels;
using NUnit.Framework;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Components.Parsers;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.TypeScript;
using AwesomeAssertions;

namespace CodeExplorer.Tests;

/// <summary>
/// `*.spec.ts` sources must not produce CallLiteral nodes (SIG-M-I-001 noise: every describe/it title
/// became a "hardcoded literal"); the same code in a regular source file still does.
/// </summary>
[TestFixture]
public class SpecFileCallLiteralExclusionTests
{
    private const string Code = """
        describe('TargetUrlGeneratorHelper', () => {
          it('builds the link', () => {
            expect(helper.generateLink('campaign-42')).toBeDefined();
          });
        });
        """;

    private static async Task<int> CountCallLiteralsAsync(string relDir, string fileName, string? code = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_spec_test_" + Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(tempDir, relDir);
        Directory.CreateDirectory(dir);
        try
        {
            var filePath = Path.Combine(dir, fileName);
            await File.WriteAllTextAsync(filePath, code ?? Code);
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var ctx = new ParsingContext(tempDir, tempDir, new InMemoryGraphClient(), channel);
            using var syntaxTree = await new TypeScriptParser().ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var isTest = WorkspaceConventions.IsTestFilePath(filePath) || new JavaScriptTestRunnerComponentParser().IsTestSource(syntaxTree);
            syntaxTree.FileNode!.IsTest = isTest;
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, isTest: isTest);
            return Count(syntaxTree.FileNode.Children);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static int Count(IEnumerable<IOntologyNode> nodes) =>
        nodes.Sum(n => (n is CallLiteralNode ? 1 : 0) + Count(n.Children));

    [Test]
    public async Task RegularSource_ProducesCallLiterals()
    {
        const string prodCode = "const link = helper.generateLink('campaign-42');";
        (await CountCallLiteralsAsync("src/helpers", "target-url-generator.helper.ts", prodCode)).Should().BeGreaterThan(0);
    }

    [Test]
    public async Task SpecSource_ProducesNoCallLiterals()
    {
        (await CountCallLiteralsAsync("src/helpers", "target-url-generator.helper.spec.ts")).Should().Be(0);
    }
}
