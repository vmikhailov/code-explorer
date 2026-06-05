using System.Threading.Channels;
using NUnit.Framework;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.Python;
using CodeExplorer.Parser.TypeScript;

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
        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

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
            var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

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
            var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);
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
            var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);
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
            var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);
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
        Assert.That(dbNode.Extensions, Is.Not.Null);
        Assert.That(dbNode.Extensions.ContainsKey("db_type"), Is.True);
        Assert.That(dbNode.Extensions["db_type"], Is.EqualTo("relational"));

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
            var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);
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

    private List<Reference> FindReferences(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<Reference>();
        foreach (var node in nodes)
        {
            result.AddRange(node.References);
            result.AddRange(FindReferences(node.Children));
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
            var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);
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
            PrintTsAst(tree!.RootNode, "");
            Console.WriteLine("--- TS AST END ---");

            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new MemgraphClient("bolt://127.0.0.1:7687", "", "");
            var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);
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
            var parser = new WorkspaceParser(tempWorkspace, tempWorkspace, client, clear: true);
            var results = await parser.IndexAsync();

            Assert.That(results.NodesCount, Is.GreaterThan(0));
            Assert.That(results.RelationshipsCount, Is.GreaterThan(0));

            // Verify EntryPoints grouping in the database
            var wsPathQuery = CodeExplorer.Core.Common.PathTools.NormalizeToHostPath(tempWorkspace).Replace("\\", "\\\\");
            var entryPointsCountBJson = await client.ExecuteQueryAsync($"MATCH (w:Workspace {{path: '{wsPathQuery}'}})-[:CONTAINS*1..]->(p:Project {{name: 'ProjectB'}})-[:CONTAINS]->(d:EntryPoints) RETURN count(d) AS count");
            Assert.That(entryPointsCountBJson, Contains.Substring("\"count\": 1"));

            var entryPointsCountAJson = await client.ExecuteQueryAsync($"MATCH (w:Workspace {{path: '{wsPathQuery}'}})-[:CONTAINS*1..]->(p:Project {{name: 'ProjectA'}})-[:CONTAINS]->(d:EntryPoints) RETURN count(d) AS count");
            Assert.That(entryPointsCountAJson, Contains.Substring("\"count\": 0"));

            var containsEpJson = await client.ExecuteQueryAsync($"MATCH (w:Workspace {{path: '{wsPathQuery}'}})-[:CONTAINS*1..]->(p:Project {{name: 'ProjectB'}})-[:CONTAINS]->(eps:EntryPoints)-[:EXPOSES]->(ep:EntryPoint {{name: 'POST charge'}}) RETURN ep.name");
            Assert.That(containsEpJson, Contains.Substring("POST charge"));

            var implByJson = await client.ExecuteQueryAsync($"MATCH (w:Workspace {{path: '{wsPathQuery}'}})-[:CONTAINS*1..]->(p:Project)-[:CONTAINS]->(eps:EntryPoints)-[:EXPOSES]->(ep:EntryPoint {{name: 'POST charge'}})-[:IMPLEMENTED_BY]->(f:Function {{name: 'charge'}}) RETURN f.name");
            Assert.That(implByJson, Contains.Substring("charge"));

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

    [Test]
    public async Task Test_SemanticAnalysisAndOntologyEnrichment()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "semantic_test_workspace_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            // Project A: C# project using database package (Dapper) and containing configuration + constants + empty subfolder
            var projADir = Path.Combine(tempWorkspace, "ProjectA").Replace('\\', '/');
            Directory.CreateDirectory(projADir);
            await File.WriteAllTextAsync(Path.Combine(projADir, "ProjectA.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
                "  <ItemGroup>\n" +
                "    <PackageReference Include=\"Dapper\" Version=\"1.0.0\" />\n" +
                "    <PackageReference Include=\"Stripe.net\" Version=\"1.0.0\" />\n" +
                "    <PackageReference Include=\"Microsoft.AspNetCore.App\" Version=\"1.0.0\" />\n" +
                "  </ItemGroup>\n" +
                "</Project>");

            // Empty folder in Project A to verify pruning
            var emptySubDir = Path.Combine(projADir, "EmptyFolder").Replace('\\', '/');
            Directory.CreateDirectory(emptySubDir);

            var projAFile = Path.Combine(projADir, "Repository.cs").Replace('\\', '/');
            var projACode = @"
            using System;
            using Dapper;
            using Stripe;
            class Repository {
                private const string CONNECTION_STRING_URL = ""Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;"";
                public static readonly int MAX_RETRIES = 5;
                void RunQuery() {
                    var sql = ""SELECT * FROM Users"";
                    int timeoutSeconds = 30;
                }
            }";
            await File.WriteAllTextAsync(projAFile, projACode);

            // Project B: Empty project (should be pruned from graph completely)
            var projBDir = Path.Combine(tempWorkspace, "ProjectB").Replace('\\', '/');
            Directory.CreateDirectory(projBDir);
            await File.WriteAllTextAsync(Path.Combine(projBDir, "ProjectB.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

            // Setup parsing context
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new MemgraphClient("bolt://127.0.0.1:7687", "", "");

            var ctx = new ParsingContext(tempWorkspace, tempWorkspace, client, channel);
            ctx.WorkspaceId = "1";

            // Register CSharp parser
            WorkspaceParser.Register(new CSharpParser());

            var scanParser = new WorkspaceLevelParser(ctx);
            var workspaceNode = new WorkspaceNode("1", "TestWorkspace", tempWorkspace);

            // Invoke ScanDirectoryAsync via reflection
            var rootScan = typeof(WorkspaceLevelParser).GetMethod("ScanDirectoryAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(rootScan, Is.Not.Null);
            await (Task)rootScan.Invoke(scanParser, [tempWorkspace, workspaceNode])!;

            // Before pruning, ProjectB and EmptyFolder are in the tree
            Assert.That(workspaceNode.Children.Any(c => c is ProjectNode pn && pn.Name == "ProjectB"), Is.True);

            var projectA = workspaceNode.Children.FirstOrDefault(c => c is ProjectNode pn && pn.Name == "ProjectA");
            Assert.That(projectA, Is.Not.Null);
            var filesNode = projectA.Children.OfType<FilesNode>().FirstOrDefault();
            Assert.That(filesNode, Is.Not.Null);
            Assert.That(filesNode.Children.Any(c => c is ProjectFolderNode pfn && pfn.Name == "EmptyFolder"), Is.True);

            // 2. Perform pruning
            OntologyPruner.PruneEmptyFolders(workspaceNode);

            // After pruning:
            // ProjectB is removed because it is an empty project
            Assert.That(workspaceNode.Children.Any(c => c is ProjectNode pn && pn.Name == "ProjectB"), Is.False);
            // EmptyFolder is removed
            var folderA = filesNode.Children.FirstOrDefault(c => c is ProjectFolderNode pfn && pfn.Name == "EmptyFolder");
            Assert.That(folderA, Is.Null);

            // Verify project framework detection
            Assert.That(projectA.Extensions, Is.Not.Null);
            Assert.That(projectA.Extensions.ContainsKey("framework"), Is.True);
            Assert.That(projectA.Extensions["framework"], Is.EqualTo("ASP.NET Core"));

            // Verify Dependencies node grouping
            var depsNode = projectA.Children.OfType<DependenciesNode>().FirstOrDefault();
            Assert.That(depsNode, Is.Not.Null);
            Assert.That(depsNode.Name, Is.EqualTo("Dependencies"));
            Assert.That(depsNode.Kind, Is.EqualTo(OntologyConstants.NodeLabels.Dependencies));

            // Verify external packages are inside DependenciesNode
            var extPackages = depsNode.Children.OfType<PackageNode>().ToList();
            Assert.That(extPackages, Has.Count.EqualTo(3));
            Assert.That(extPackages.Any(p => p.Name == "Dapper"), Is.True);
            Assert.That(extPackages.Any(p => p.Name == "Stripe.net"), Is.True);
            Assert.That(extPackages.Any(p => p.Name == "Microsoft.AspNetCore.App"), Is.True);

            // Verify projectA does NOT directly contain those external packages as children
            var directPackages = projectA.Children.OfType<PackageNode>().ToList();
            Assert.That(directPackages.Any(p => p.Name == "Dapper"), Is.False);
            Assert.That(directPackages.Any(p => p.Name == "Stripe.net"), Is.False);
            Assert.That(directPackages.Any(p => p.Name == "Microsoft.AspNetCore.App"), Is.False);

            // Verify projectA directly contains the produced package
            Assert.That(directPackages.Any(p => p.Name == "ProjectA"), Is.True);

            var fileNode = filesNode.Children.OfType<FileNode>().FirstOrDefault(f => f.Name == "Repository.cs");
            Assert.That(fileNode, Is.Not.Null);

            // Check Repository.cs extensions (file-level extensions are removed)
            if (fileNode.Extensions != null)
            {
                Assert.That(fileNode.Extensions.ContainsKey("db_type"), Is.False);
                Assert.That(fileNode.Extensions.ContainsKey("cloud_service"), Is.False);
            }

            // Check if DbNode child was added at the project level (under DataBasesNode)
            var databasesNode = projectA.Children.OfType<DataBasesNode>().FirstOrDefault();
            Assert.That(databasesNode, Is.Not.Null);
            var dbNode = databasesNode.Children.OfType<DbNode>().FirstOrDefault();
            Assert.That(dbNode, Is.Not.Null);
            Assert.That(dbNode.Name, Is.EqualTo("Dapper"));
            Assert.That(dbNode.Extensions, Is.Not.Null);
            Assert.That(dbNode.Extensions.ContainsKey("db_type"), Is.True);
            Assert.That(dbNode.Extensions["db_type"], Is.EqualTo("relational"));

            // Check if CloudServiceNode child was added at the project level (under CloudServicesNode)
            var cloudServicesNode = projectA.Children.OfType<CloudServicesNode>().FirstOrDefault();
            Assert.That(cloudServicesNode, Is.Not.Null);
            var cloudNode = cloudServicesNode.Children.OfType<CloudServiceNode>().FirstOrDefault();
            Assert.That(cloudNode, Is.Not.Null);
            Assert.That(cloudNode.Name, Is.EqualTo("Stripe"));

            // Check that file-to-library relationships are created in the context
            var usesDb = ctx.GlobalProjectDependencies.FirstOrDefault(r => r.From == fileNode.Id && r.Kind == "USES_DB");
            Assert.That(usesDb, Is.Not.Null);
            Assert.That(usesDb.To, Is.EqualTo(dbNode.Id));

            var usesCloud = ctx.GlobalProjectDependencies.FirstOrDefault(r => r.From == fileNode.Id && r.Kind == "USES_CLOUD");
            Assert.That(usesCloud, Is.Not.Null);
            Assert.That(usesCloud.To, Is.EqualTo(cloudNode.Id));

            // Check variable nodes under ClassNode (Repository)
            var classNode = fileNode.Children.OfType<ClassNode>().FirstOrDefault();
            Assert.That(classNode, Is.Not.Null);

            var variables = classNode.Children.OfType<VariableNode>().ToList();
            Assert.That(variables.Any(v => v.Name == "CONNECTION_STRING_URL"), Is.True);
            Assert.That(variables.Any(v => v.Name == "MAX_RETRIES"), Is.True);
            Assert.That(variables.Any(v => v.Name == "timeoutSeconds"), Is.False); // local non-config is ignored

            var connStrVar = variables.First(v => v.Name == "CONNECTION_STRING_URL");
            Assert.That(connStrVar.Extensions, Is.Not.Null);
            Assert.That(connStrVar.Extensions["variable_type"], Contains.Substring("config"));
            Assert.That(connStrVar.Extensions["variable_type"], Contains.Substring("constant"));
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                Directory.Delete(tempWorkspace, true);
            }
        }
    }

    [Test]
    public async Task Test_NewParserFeatures()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "new_features_test_workspace_" + Guid.NewGuid());
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new MemgraphClient("bolt://127.0.0.1:7687", "", "");
            var ctx = new ParsingContext(tempWorkspace, tempWorkspace, client, channel);
            ctx.WorkspaceId = "1";

            // 1. Python Parser - Flask & HTTP calls
            var pythonParser = new PythonParser();
            var pyFile = Path.Combine(tempWorkspace, "app.py");
            var pyCode = @"
@app.route('/charge', methods=['POST'])
def process_payment():
    requests.post('https://api.stripe.com/v3/charges')
";
            await File.WriteAllTextAsync(pyFile, pyCode);
            var pyNode = await pythonParser.ParseAsync(pyFile, "parent", ctx);
            Assert.That(pyNode, Is.Not.Null);

            var pyEntryPoints = FindEntryPointNodes(pyNode.Children);
            Assert.That(pyEntryPoints, Has.Count.EqualTo(1));
            Assert.That(pyEntryPoints[0].Name, Is.EqualTo("POST /charge"));

            var pyExtServices = FindExternalServiceNodes(pyNode.Children);
            Assert.That(pyExtServices, Has.Count.EqualTo(1));
            Assert.That(pyExtServices[0].Name, Is.EqualTo("api.stripe.com"));

            // Verify reference from process_payment function to EntryPoint POST:/charge is collected
            var pyRefs = FindReferences(pyNode.Children);
            Assert.That(pyRefs.Any(r => r.TargetName == "POST /charge" && r.Kind == "IMPLEMENTS"), Is.True);

            // 2. Go Parser - Gin & HTTP Get calls
            var goParser = new GoParser();
            var goFile = Path.Combine(tempWorkspace, "main.go");
            var goCode = @"
package main
import ""net/http""
func Register(r *gin.Engine) {
    r.GET(""/api/v1/users"", GetUsers)
}
";
            await File.WriteAllTextAsync(goFile, goCode);
            // Debug: print the Go AST
            var goSourceText = await File.ReadAllTextAsync(goFile);
            using var goLang = new TreeSitter.Language("go");
            using var goTsParser = new TreeSitter.Parser(goLang);
            using var goTree = goTsParser.Parse(goSourceText);
            Console.WriteLine("--- GO AST START ---");
            PrintTsAst(goTree!.RootNode, "");
            Console.WriteLine("--- GO AST END ---");

            var goNode = await goParser.ParseAsync(goFile, "parent", ctx);
            Assert.That(goNode, Is.Not.Null);

            var goEntryPoints = FindEntryPointNodes(goNode.Children);
            Assert.That(goEntryPoints, Has.Count.EqualTo(1));
            Assert.That(goEntryPoints[0].Name, Is.EqualTo("GET /api/v1/users"));

            // Verify Go references
            var goRefs = FindReferences(goNode.Children);
            Assert.That(goRefs.Any(r => r.TargetName == "GET /api/v1/users" && r.Kind == "IMPLEMENTS" && r.ScopeSymbolId == "GetUsers"), Is.True);

            // 3. SQL Parser - CREATE OR REPLACE PROCEDURE, IF NOT EXISTS, backticks
            var sqlParser = new Parser.SQL.SqlParser();
            var sqlFile = Path.Combine(tempWorkspace, "sp.sql");
            var sqlCode = @"
CREATE OR REPLACE PROCEDURE `my_schema`.`my_proc`()
BEGIN
    CREATE TABLE IF NOT EXISTS `my_schema`.`my_table` (id INT);
    EXEC `my_schema`.`another_proc`;
END;
";
            await File.WriteAllTextAsync(sqlFile, sqlCode);
            var sqlNode = await sqlParser.ParseAsync(sqlFile, "parent", ctx);
            Assert.That(sqlNode, Is.Not.Null);

            // Schema and DB hierarchy check
            var dbNode = sqlNode.Children.OfType<DbNode>().FirstOrDefault();
            Assert.That(dbNode, Is.Not.Null);
            Assert.That(dbNode.Extensions, Is.Not.Null);
            Assert.That(dbNode.Extensions.ContainsKey("db_type"), Is.True);
            Assert.That(dbNode.Extensions["db_type"], Is.EqualTo("relational"));

            var schemaNode = dbNode.Children.OfType<DataSetNode>().FirstOrDefault(s => s.Name == "my_schema");
            Assert.That(schemaNode, Is.Not.Null);

            var procNode = schemaNode.Children.OfType<ProcedureNode>().FirstOrDefault(p => p.Name == "my_proc");
            Assert.That(procNode, Is.Not.Null);

            var tableNode = schemaNode.Children.OfType<TableNode>().FirstOrDefault(t => t.Name == "my_table");
            Assert.That(tableNode, Is.Not.Null);

            var queryNode = procNode.Children.OfType<QueryNode>().FirstOrDefault();
            Assert.That(queryNode, Is.Not.Null);
            Assert.That(queryNode.References.Any(r => r.TargetName == "another_proc" && r.Kind == "CALLS"), Is.True);
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                Directory.Delete(tempWorkspace, true);
            }
        }
    }

    [Test]
    public async Task Test_SemanticAnalyzer_DbTypeMapping()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "db_type_mapping_test_workspace_" + Guid.NewGuid());
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new MemgraphClient("bolt://127.0.0.1:7687", "", "");
            var ctx = new ParsingContext(tempWorkspace, tempWorkspace, client, channel);
            ctx.WorkspaceId = "1";

            // Test C# Semantic Analyzer with relational (EntityFramework)
            var csAnalyzer = new CSharpSemanticAnalyzer();
            var csProj = new ProjectNode("cs_project", "cs_project", "cs_project", "csharp");
            var csFile = new FileNode("cs_file", "Repository.cs", "Repository.cs", tempWorkspace + "/Repository.cs");
            csProj.Children.Add(csFile);
            ctx.RawImports.Add(new RawImport("Microsoft.EntityFrameworkCore", "Repository.cs"));
            await csAnalyzer.AnalyzeAndEnrichAsync(csProj, ctx);

            var csDbGroup = csProj.Children.OfType<DataBasesNode>().FirstOrDefault();
            Assert.That(csDbGroup, Is.Not.Null);
            var csDbNode = csDbGroup.Children.OfType<DbNode>().FirstOrDefault();
            Assert.That(csDbNode, Is.Not.Null);
            Assert.That(csDbNode.Name, Is.EqualTo("Microsoft.EntityFrameworkCore"));
            Assert.That(csDbNode!.Extensions!["db_type"], Is.EqualTo("relational"));
            Assert.That(ctx.GlobalProjectDependencies.Any(r => r.From == csFile.Id && r.To == csDbNode.Id && r.Kind == "USES_DB"), Is.True);

            // Test TypeScript Semantic Analyzer with document (mongoose)
            var tsAnalyzer = new TypeScriptSemanticAnalyzer();
            var tsProj = new ProjectNode("ts_project", "ts_project", "ts_project", "typescript");
            var tsFile = new FileNode("ts_file", "index.ts", "index.ts", tempWorkspace + "/index.ts");
            tsProj.Children.Add(tsFile);
            ctx.RawImports.Add(new RawImport("mongoose", "index.ts"));
            await tsAnalyzer.AnalyzeAndEnrichAsync(tsProj, ctx);

            var tsDbGroup = tsProj.Children.OfType<DataBasesNode>().FirstOrDefault();
            Assert.That(tsDbGroup, Is.Not.Null);
            var tsDbNode = tsDbGroup.Children.OfType<DbNode>().FirstOrDefault();
            Assert.That(tsDbNode, Is.Not.Null);
            Assert.That(tsDbNode.Name, Is.EqualTo("MongoDB"));
            Assert.That(tsDbNode!.Extensions!["db_type"], Is.EqualTo("document"));
            Assert.That(ctx.GlobalProjectDependencies.Any(r => r.From == tsFile.Id && r.To == tsDbNode.Id && r.Kind == "USES_DB"), Is.True);

            // Test Python Semantic Analyzer with keyvalue (redis)
            var pyAnalyzer = new PythonSemanticAnalyzer();
            var pyProj = new ProjectNode("py_project", "py_project", "py_project", "python");
            var pyFile = new FileNode("py_file", "main.py", "main.py", tempWorkspace + "/main.py");
            pyProj.Children.Add(pyFile);
            ctx.RawImports.Add(new RawImport("redis", "main.py"));
            await pyAnalyzer.AnalyzeAndEnrichAsync(pyProj, ctx);

            var pyDbGroup = pyProj.Children.OfType<DataBasesNode>().FirstOrDefault();
            Assert.That(pyDbGroup, Is.Not.Null);
            var pyDbNode = pyDbGroup.Children.OfType<DbNode>().FirstOrDefault();
            Assert.That(pyDbNode, Is.Not.Null);
            Assert.That(pyDbNode.Name, Is.EqualTo("Redis"));
            Assert.That(pyDbNode!.Extensions!["db_type"], Is.EqualTo("keyvalue"));
            Assert.That(ctx.GlobalProjectDependencies.Any(r => r.From == pyFile.Id && r.To == pyDbNode.Id && r.Kind == "USES_DB"), Is.True);
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                Directory.Delete(tempWorkspace, true);
            }
        }
    }

    [Test]
    public async Task Test_LibraryParsers_CSharpAndTS()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "lib_parsers_test_workspace_" + Guid.NewGuid());
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new MemgraphClient("bolt://127.0.0.1:7687", "", "");
            var ctx = new ParsingContext(tempWorkspace, tempWorkspace, client, channel);
            ctx.WorkspaceId = "1";

            // 1. C# file parsing test (Dapper and Flurl)
            var csFilePath = Path.Combine(tempWorkspace, "Service.cs");
            var csContent = @"
using Dapper;
using Flurl.Http;

public class Service
{
    public void RunDapper(System.Data.IDbConnection conn)
    {
        conn.Query(""SELECT name FROM users WHERE id = @id"");
    }

    public async System.Threading.Tasks.Task RunFlurl()
    {
        await ""http://api.github.com/v3"".AppendPathSegment(""users"").GetJsonAsync();
    }
}";
            await File.WriteAllTextAsync(csFilePath, csContent);

            var csFileParser = new CSharpParser();
            var csFileNode = await TreeSitterFileParser.ParseFileAsync(csFilePath, "Service.cs", "1", csFileParser, ctx);

            // Verify Dapper query extraction
            var csQueries = FindQueryNodes([csFileNode]);
            var dapperNode = csQueries.FirstOrDefault(q => q.Name.Contains("SELECT"));
            Assert.That(dapperNode, Is.Not.Null);
            Assert.That(dapperNode.Name, Is.EqualTo("SELECT Query: SELECT name FROM users WHERE id = @id"));

            // Verify Flurl external service extraction
            var csExtServices = FindExternalServiceNodes([csFileNode]);
            var flurlNode = csExtServices.FirstOrDefault();
            Assert.That(flurlNode, Is.Not.Null);
            Assert.That(flurlNode.Name, Is.EqualTo("api.github.com"));

            // 2. TS file parsing test (Mongoose and Redis)
            var tsFilePath = Path.Combine(tempWorkspace, "app.ts");
            var tsContent = @"
import mongoose from 'mongoose';
import redis from 'redis';

const schema = new mongoose.Schema({});
const Product = mongoose.model('Product', schema);

async function testDb(client: any) {
    await Product.find();
    await client.set('foo', 'bar');
}";
            await File.WriteAllTextAsync(tsFilePath, tsContent);

            var tsFileParser = new TypeScriptParser();
            var tsFileNode = await TreeSitterFileParser.ParseFileAsync(tsFilePath, "app.ts", "1", tsFileParser, ctx);

            var tsQueries = FindQueryNodes([tsFileNode]);

            // Verify Mongoose model & query extraction
            var modelNode = tsQueries.FirstOrDefault(q => q.Name.Contains("Mongoose Model"));
            Assert.That(modelNode, Is.Not.Null);
            Assert.That(modelNode.Name, Is.EqualTo("Mongoose Model: Product"));

            var findNode = tsQueries.FirstOrDefault(q => q.Name.Contains("Mongoose: Product.find"));
            Assert.That(findNode, Is.Not.Null);

            // Verify Redis query extraction
            var redisNode = tsQueries.FirstOrDefault(q => q.Name.Contains("Redis: client.set"));
            Assert.That(redisNode, Is.Not.Null);
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                Directory.Delete(tempWorkspace, true);
            }
        }
    }


    [Test]
    public void Test_LibraryTrieRegistry_Matching()
    {
        var parserNest = new GenericLibraryParser("nestjs", "NestJS", "framework", ["@nestjs/*"]);
        var parserFirebaseGeneric = new GenericLibraryParser("firebase", "Firebase", "cloud", ["firebase*"]);
        var parserFirebaseSpecific = new GenericLibraryParser("firebaseadmin", "FirebaseAdmin", "cloud", ["firebase-admin"]);
        var parserSql = new GenericLibraryParser("sqlclient", "SqlClient", "db", ["System.Data"]);
        var parserGoogleCloud = new GenericLibraryParser("gcp", "GCP", "cloud", ["Google.Cloud."]);

        var registry = new LibraryTrieRegistry([
            parserNest,
            parserFirebaseGeneric,
            parserFirebaseSpecific,
            parserSql,
            parserGoogleCloud
        ]);

        // Scoped wildcard /*
        Assert.That(registry.Match("@nestjs/common"), Is.SameAs(parserNest));
        Assert.That(registry.Match("@nestjs/core"), Is.SameAs(parserNest));
        Assert.That(registry.Match("@nestjs"), Is.SameAs(parserNest));

        // Prefix priority matching
        Assert.That(registry.Match("firebase-admin"), Is.SameAs(parserFirebaseSpecific));
        Assert.That(registry.Match("firebase"), Is.SameAs(parserFirebaseGeneric));

        // Fallback namespace match
        Assert.That(registry.Match("System.Data.SqlClient"), Is.SameAs(parserSql));
        Assert.That(registry.Match("Google.Cloud.Translation"), Is.SameAs(parserGoogleCloud));

        // Unmatched
        Assert.That(registry.Match("stripe"), Is.Null);
    }
}

