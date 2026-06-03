using System.IO;
using System.Threading.Channels;
using NUnit.Framework;
using CodeExplorer.Parser;
using CodeExplorer.Common;
using CodeExplorer.Database;

namespace CodeExplorer.Tests;

[TestFixture]
public class ParserValidationTests
{
    [Test]
    public async Task Test_TypeScriptParser_WithExamples()
    {
        var parser = new TypeScriptParser();
        var workspacePath = "/Users/slava/Projects/ATS/src/services";
        
        var channel = Channel.CreateUnbounded<Func<Task>>();
        await using var client = new MemgraphClient("bolt://127.0.0.1:7687", "", "");
        var ctx = new ParsingContext(workspacePath, client, channel);
        
        var filesToTest = new[]
        {
            "/Users/slava/Projects/ATS/src/services/bq-routes-calculation/src/cron/cron.service.ts",
            "/Users/slava/Projects/ATS/src/services/bq-routes-calculation/src/services/calibrate-min-roi.service.ts",
            "/Users/slava/Projects/ATS/src/services/calc-epm/src/interfaces/configs/config.models.ts"
        };
        
        foreach (var file in filesToTest)
        {
            if (!File.Exists(file))
            {
                Assert.Warn($"Example file not found: {file}");
                continue;
            }
            
            var fileNode = await parser.ParseAsync(file, "parent-id", ctx);
            Assert.That(fileNode, Is.Not.Null);
            
            Console.WriteLine($"\n================== PARSED FILE: {fileNode.Name} ==================");
            PrintNodes(fileNode.Children, "  ");
        }
    }
    
    private void PrintNodes(IEnumerable<IOntologyNode> nodes, string indent)
    {
        foreach (var node in nodes)
        {
            var name = GetNodeName(node);
            Console.WriteLine($"{indent}- Node ID: {node.Id}, Kind: {node.Kind}, Name: {name}");
            if (node.References.Any())
            {
                Console.WriteLine($"{indent}  References:");
                foreach (var r in node.References)
                {
                    Console.WriteLine($"{indent}    * ScopeSymbolId: {r.ScopeSymbolId}, TargetName: {r.TargetName}, Kind: {r.Kind}");
                }
            }
            PrintNodes(node.Children, indent + "  ");
        }
    }

    private string GetNodeName(IOntologyNode node)
    {
        return node switch
        {
            FileNode f => f.Name,
            ClassNode c => c.Name,
            InterfaceNode i => i.Name,
            FunctionNode fn => fn.Name,
            VariableNode v => v.Name,
            _ => node.GetType().GetProperty("Name")?.GetValue(node) as string ?? "Unknown"
        };
    }
}
