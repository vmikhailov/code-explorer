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

    [Test]
    public async Task Test_TypeScriptParser_EmbeddedSql()
    {
        var parser = new TypeScriptParser();
        var workspacePath = Path.GetTempPath();
        
        var tempFile = Path.Combine(workspacePath, "embedded_sql_test.ts");
        var code = @"
async function clearDataAllLeads(bundle_ids: string) {
    const query = `DELETE FROM tracking.data_all_leads WHERE bundle_id in (${bundle_ids})`;
    await BQ.executeQuery(query);
}
";
        await File.WriteAllTextAsync(tempFile, code);
        
        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new MemgraphClient("bolt://127.0.0.1:7687", "", "");
            var ctx = new ParsingContext(workspacePath, client, channel);
            
            var fileNode = await parser.ParseAsync(tempFile, "parent-id", ctx);
            Assert.That(fileNode, Is.Not.Null);
            
            var queryNodes = FindQueryNodes(fileNode.Children);
            Assert.That(queryNodes, Is.Not.Empty, "Should have detected the embedded SQL query");
            
            var sqlQuery = queryNodes[0];
            Assert.That(sqlQuery.Name, Is.EqualTo("DELETE Query"));
            Assert.That(sqlQuery.QueryText, Contains.Substring("DELETE FROM tracking.data_all_leads WHERE bundle_id in"));
            
            var dependsOn = sqlQuery.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName).ToList();
            Assert.That(dependsOn, Contains.Item("tracking"));
            Assert.That(dependsOn, Contains.Item("data_all_leads"));

            AssertSqlHierarchy(sqlQuery, "default", "tracking", "data_all_leads");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Test]
    public async Task Test_CSharpParser_EmbeddedSql()
    {
        var parser = new CSharpParser();
        var workspacePath = Path.GetTempPath();
        var tempFile = Path.Combine(workspacePath, "embedded_sql_test.cs");
        var code = """
class Test {
    void Clean() {
        string query = "SELECT id, name FROM users WHERE active = 1";
    }
}
""";
        await File.WriteAllTextAsync(tempFile, code);
        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new MemgraphClient("bolt://127.0.0.1:7687", "", "");
            var ctx = new ParsingContext(workspacePath, client, channel);
            var fileNode = await parser.ParseAsync(tempFile, "parent-id", ctx);
            var queryNodes = FindQueryNodes(fileNode.Children);
            Assert.That(queryNodes, Is.Not.Empty);
            var sqlQuery = queryNodes[0];
            Assert.That(sqlQuery.Name, Is.EqualTo("SELECT Query"));
            Assert.That(sqlQuery.QueryText, Contains.Substring("SELECT id, name FROM users"));
            var dependsOn = sqlQuery.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName).ToList();
            Assert.That(dependsOn, Contains.Item("users"));

            AssertSqlHierarchy(sqlQuery, "default", "dbo", "users");
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Test]
    public async Task Test_PythonParser_EmbeddedSql()
    {
        var parser = new PythonParser();
        var workspacePath = Path.GetTempPath();
        var tempFile = Path.Combine(workspacePath, "embedded_sql_test.py");
        var code = """
def clean_db():
    query = 'INSERT INTO logs (message, created_at) VALUES ("test", 123)'
""";
        await File.WriteAllTextAsync(tempFile, code);
        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new MemgraphClient("bolt://127.0.0.1:7687", "", "");
            var ctx = new ParsingContext(workspacePath, client, channel);
            var fileNode = await parser.ParseAsync(tempFile, "parent-id", ctx);
            var queryNodes = FindQueryNodes(fileNode.Children);
            Assert.That(queryNodes, Is.Not.Empty);
            var sqlQuery = queryNodes[0];
            Assert.That(sqlQuery.Name, Is.EqualTo("INSERT Query"));
            Assert.That(sqlQuery.QueryText, Contains.Substring("INSERT INTO logs"));
            var dependsOn = sqlQuery.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName).ToList();
            Assert.That(dependsOn, Contains.Item("logs"));

            AssertSqlHierarchy(sqlQuery, "default", "dbo", "logs");
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Test]
    public async Task Test_GoParser_EmbeddedSql()
    {
        var parser = new GoParser();
        var workspacePath = Path.GetTempPath();
        var tempFile = Path.Combine(workspacePath, "embedded_sql_test.go");
        var code = """
package main
func clean() {
    query := `UPDATE transactions SET status = 'failed' WHERE id = 1`
}
""";
        await File.WriteAllTextAsync(tempFile, code);
        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new MemgraphClient("bolt://127.0.0.1:7687", "", "");
            var ctx = new ParsingContext(workspacePath, client, channel);
            var fileNode = await parser.ParseAsync(tempFile, "parent-id", ctx);
            var queryNodes = FindQueryNodes(fileNode.Children);
            Assert.That(queryNodes, Is.Not.Empty);
            var sqlQuery = queryNodes[0];
            Assert.That(sqlQuery.Name, Is.EqualTo("UPDATE Query"));
            Assert.That(sqlQuery.QueryText, Contains.Substring("UPDATE transactions SET status"));
            var dependsOn = sqlQuery.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName).ToList();
            Assert.That(dependsOn, Contains.Item("transactions"));

            AssertSqlHierarchy(sqlQuery, "default", "dbo", "transactions");
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }
    
    private void AssertSqlHierarchy(QueryNode queryNode, string expectedDb, string expectedSchema, string expectedTable)
    {
        var dbNode = queryNode.Children.OfType<DbNode>().FirstOrDefault(d => d.Name.Equals(expectedDb, StringComparison.OrdinalIgnoreCase));
        Assert.That(dbNode, Is.Not.Null, $"Should contain DB node: {expectedDb}");

        var schemaNode = dbNode.Children.OfType<DataSetNode>().FirstOrDefault(s => s.Name.Equals(expectedSchema, StringComparison.OrdinalIgnoreCase));
        Assert.That(schemaNode, Is.Not.Null, $"Should contain Schema/DataSet node: {expectedSchema}");

        var tableNode = schemaNode.Children.OfType<TableNode>().FirstOrDefault(t => t.Name.Equals(expectedTable, StringComparison.OrdinalIgnoreCase));
        Assert.That(tableNode, Is.Not.Null, $"Should contain Table node: {expectedTable}");
    }

    [Test]
    public void Test_NestedSqlParser_CleanQueryText_NestedQuotes()
    {
        var input = "\"'SELECT * FROM my_table'\"";
        var cleaned = NestedSqlParser.CleanQueryText(input);
        Assert.That(cleaned, Is.EqualTo("SELECT * FROM my_table"));
    }

    [Test]
    public async Task Test_TypeScriptParser_EmbeddedSql_ComplexTemplate()
    {
        var parser = new TypeScriptParser();
        var workspacePath = Path.GetTempPath();
        var tempFile = Path.Combine(workspacePath, "complex_template_test.ts");
        var code = @"
async function getStages(tableName: string, bundle_id: number, site_id: string) {
    const query = `
        SELECT *
        FROM \`${tableName}\`
        WHERE bundle_id = ${bundle_id} AND site_id = '${site_id}'
        ORDER BY stage DESC
    `;
    await BQ.executeQuery(query);
}
";
        await File.WriteAllTextAsync(tempFile, code);
        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new MemgraphClient("bolt://127.0.0.1:7687", "", "");
            var ctx = new ParsingContext(workspacePath, client, channel);
            var fileNode = await parser.ParseAsync(tempFile, "parent-id", ctx);
            
            var queryNodes = FindQueryNodes(fileNode.Children);
            Assert.That(queryNodes, Is.Not.Empty);
            var sqlQuery = queryNodes[0];
            Assert.That(sqlQuery.Name, Is.EqualTo("SELECT Query"));
            
            // Since tableName is a variable, it should be skipped and no database node hierarchy should be created for it.
            var hasDbNode = sqlQuery.Children.OfType<DbNode>().Any();
            Assert.That(hasDbNode, Is.False, "Should have skipped tableName because it is a template variable placeholder.");
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
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
            QueryNode q => q.Name,
            _ => node.GetType().GetProperty("Name")?.GetValue(node) as string ?? "Unknown"
        };
    }

    private List<QueryNode> FindQueryNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<QueryNode>();
        foreach (var node in nodes)
        {
            if (node is QueryNode q) result.Add(q);
            result.AddRange(FindQueryNodes(node.Children));
        }
        return result;
    }

    private List<EntryPointNode> FindEntryPointNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<EntryPointNode>();
        foreach (var node in nodes)
        {
            if (node is EntryPointNode e) result.Add(e);
            result.AddRange(FindEntryPointNodes(node.Children));
        }
        return result;
    }

    private List<ExternalServiceNode> FindExternalServiceNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<ExternalServiceNode>();
        foreach (var node in nodes)
        {
            if (node is ExternalServiceNode e) result.Add(e);
            result.AddRange(FindExternalServiceNodes(node.Children));
        }
        return result;
    }

    [Test]
    public async Task Test_CSharpParser_ApiIngressEgress()
    {
        var parser = new CSharpParser();
        var workspacePath = Path.GetTempPath();
        var tempFile = Path.Combine(workspacePath, "csharp_api_test.cs");
        var code = """
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    [HttpPost("charge")]
    public async Task<IActionResult> ChargeOrder()
    {
        var client = new HttpClient();
        await client.PostAsync("http://api.stripe.com/v1/charges", null);
        return Ok();
    }
}
""";
        await File.WriteAllTextAsync(tempFile, code);
        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new MemgraphClient("bolt://127.0.0.1:7687", "", "");
            var ctx = new ParsingContext(workspacePath, client, channel);
            var fileNode = await parser.ParseAsync(tempFile, "parent-id", ctx);
            
            var entryPoints = FindEntryPointNodes(fileNode.Children);
            Assert.That(entryPoints, Is.Not.Empty);
            var ep = entryPoints.FirstOrDefault(e => e.RouteOrTopic == "charge");
            Assert.That(ep, Is.Not.Null);
            Assert.That(ep.Protocol, Is.EqualTo("http"));
            
            var externalServices = FindExternalServiceNodes(fileNode.Children);
            Assert.That(externalServices, Is.Not.Empty);
            var es = externalServices.FirstOrDefault(e => e.DomainOrService == "api.stripe.com");
            Assert.That(es, Is.Not.Null);
            Assert.That(es.Protocol, Is.EqualTo("http"));
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Test]
    public async Task Test_TypeScriptParser_ApiIngressEgress()
    {
        var parser = new TypeScriptParser();
        var workspacePath = Path.GetTempPath();
        var tempFile = Path.Combine(workspacePath, "typescript_api_test.ts");
        var code = @"
import { Controller, Post, Get } from '@nestjs/common';
import axios from 'axios';

@Controller('orders')
export class OrdersController {
    @Post('charge')
    async chargeOrder() {
        await axios.post('http://api.stripe.com/v1/charges', {});
    }

    @SubscribeMessage('ping')
    onPing() {
        return 'pong';
    }
}
";
        await File.WriteAllTextAsync(tempFile, code);
        try
        {
            // Debug: print the Tree-sitter AST nodes
            var sourceText = await File.ReadAllTextAsync(tempFile);
            using var language = new TreeSitter.Language("typescript");
            using var tsParser = new TreeSitter.Parser(language);
            using var tree = tsParser.Parse(sourceText);
            Console.WriteLine("--- TS AST START ---");
            PrintTsAst(tree.RootNode, "");
            Console.WriteLine("--- TS AST END ---");

            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new MemgraphClient("bolt://127.0.0.1:7687", "", "");
            var ctx = new ParsingContext(workspacePath, client, channel);
            var fileNode = await parser.ParseAsync(tempFile, "parent-id", ctx);
            
            var entryPoints = FindEntryPointNodes(fileNode.Children);
            Assert.That(entryPoints, Is.Not.Empty);
            var ep = entryPoints.FirstOrDefault(e => e.RouteOrTopic == "charge");
            Assert.That(ep, Is.Not.Null);
            Assert.That(ep.Protocol, Is.EqualTo("http"));

            var wsEp = entryPoints.FirstOrDefault(e => e.Protocol == "ws");
            Assert.That(wsEp, Is.Not.Null);
            Assert.That(wsEp.RouteOrTopic, Is.EqualTo("ping"));
            
            var externalServices = FindExternalServiceNodes(fileNode.Children);
            Assert.That(externalServices, Is.Not.Empty);
            var es = externalServices.FirstOrDefault(e => e.DomainOrService == "api.stripe.com");
            Assert.That(es, Is.Not.Null);
            Assert.That(es.Protocol, Is.EqualTo("http"));
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    private void PrintTsAst(TreeSitter.Node node, string indent)
    {
        Console.WriteLine($"{indent}Type: {node.Type}, Text: {node.Text.Replace("\n", " ")}");
        foreach (var child in node.Children)
        {
            PrintTsAst(child, indent + "  ");
        }
    }

    [Test]
    public async Task Test_WorkspaceLevelParser_DynamicDetectionAndLateBinding()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "codeexplorer_test_workspace_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(tempWorkspace);
        
        try
        {
            // Project A: C# project
            var projADir = Path.Combine(tempWorkspace, "ProjectA").Replace('\\', '/');
            Directory.CreateDirectory(projADir);
            await File.WriteAllTextAsync(Path.Combine(projADir, "ProjectA.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            
            var projAFile = Path.Combine(projADir, "Client.cs").Replace('\\', '/');
            var projACode = @"
            using System.Net.Http;
            class Client {
                void Call() {
                    var client = new HttpClient();
                    client.GetAsync(""http://localhost:8085/api/orders/charge"");
                }
            }";
            await File.WriteAllTextAsync(projAFile, projACode);

            // Project B: TypeScript project
            var projBDir = Path.Combine(tempWorkspace, "ProjectB").Replace('\\', '/');
            Directory.CreateDirectory(projBDir);
            await File.WriteAllTextAsync(Path.Combine(projBDir, "package.json"), "{}");
            
            var projBFile = Path.Combine(projBDir, "server.ts").Replace('\\', '/');
            var projBCode = @"
            import { Controller, Post } from '@nestjs/common';
            @Controller('orders')
            export class OrdersController {
                @Post('charge')
                async charge() {}
            }";
            await File.WriteAllTextAsync(projBFile, projBCode);

            // Setup parsing
            await using var client = new MemgraphClient("bolt://127.0.0.1:7687", "", "");
            
            // Register parsers if they aren't already registered
            WorkspaceParser.Register(new CSharpParser());
            WorkspaceParser.Register(new TypeScriptParser());

            // Run scanner
            var parser = new WorkspaceParser(tempWorkspace, client, clear: true);
            var results = await parser.IndexAsync();

            Assert.That(results.NodesCount, Is.GreaterThan(0));
            Assert.That(results.RelationshipsCount, Is.GreaterThan(0));
            
            Console.WriteLine($"[IntegrationTest] Parsed {results.NodesCount} nodes and {results.RelationshipsCount} relationships successfully.");
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                Directory.Delete(tempWorkspace, true);
            }
        }
    }
}

