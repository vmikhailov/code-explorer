using System.Threading.Channels;
using NUnit.Framework;
using CodeExplorer.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
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
        await using var client = new MemgraphClient(McpIntegrationTests.GetBoltUrl(), "", "");
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

            using var syntaxTree =
                await parser.ParseAsync(file, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            ProjectProcessor.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var fileNode = syntaxTree.FileNode;
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

            using var syntaxTree =
                await parser.ParseAsync(tempFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            ProjectProcessor.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var fileNode = syntaxTree.FileNode;
            Assert.That(fileNode, Is.Not.Null);

            var queryNodes = FindQueryNodes(fileNode.Children);
            Assert.That(queryNodes, Is.Not.Empty, "Should have detected the embedded SQL query");

            var sqlQuery = queryNodes[0];
            Assert.That(sqlQuery.Name, Is.EqualTo("DELETE Query"));

            Assert.That(sqlQuery.QueryText,
                Contains.Substring("DELETE FROM tracking.data_all_leads WHERE bundle_id in"));

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

            using var syntaxTree =
                await parser.ParseAsync(tempFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            ProjectProcessor.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var fileNode = syntaxTree.FileNode;
            var queryNodes = FindQueryNodes(fileNode.Children);
            Assert.That(queryNodes, Is.Not.Empty);
            var sqlQuery = queryNodes[0];
            Assert.That(sqlQuery.Name, Is.EqualTo("SELECT Query"));
            Assert.That(sqlQuery.QueryText, Contains.Substring("SELECT id, name FROM users"));
            var dependsOn = sqlQuery.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName).ToList();
            Assert.That(dependsOn, Contains.Item("users"));

            AssertSqlHierarchy(sqlQuery, "default", "dbo", "users");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
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

            using var syntaxTree =
                await parser.ParseAsync(tempFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            ProjectProcessor.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var fileNode = syntaxTree.FileNode;
            var queryNodes = FindQueryNodes(fileNode.Children);
            Assert.That(queryNodes, Is.Not.Empty);
            var sqlQuery = queryNodes[0];
            Assert.That(sqlQuery.Name, Is.EqualTo("INSERT Query"));
            Assert.That(sqlQuery.QueryText, Contains.Substring("INSERT INTO logs"));
            var dependsOn = sqlQuery.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName).ToList();
            Assert.That(dependsOn, Contains.Item("logs"));

            AssertSqlHierarchy(sqlQuery, "default", "dbo", "logs");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
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

            using var syntaxTree =
                await parser.ParseAsync(tempFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            ProjectProcessor.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var fileNode = syntaxTree.FileNode;
            var queryNodes = FindQueryNodes(fileNode.Children);
            Assert.That(queryNodes, Is.Not.Empty);
            var sqlQuery = queryNodes[0];
            Assert.That(sqlQuery.Name, Is.EqualTo("UPDATE Query"));
            Assert.That(sqlQuery.QueryText, Contains.Substring("UPDATE transactions SET status"));
            var dependsOn = sqlQuery.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName).ToList();
            Assert.That(dependsOn, Contains.Item("transactions"));

            AssertSqlHierarchy(sqlQuery, "default", "dbo", "transactions");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private void AssertSqlHierarchy(QueryNode queryNode, string expectedDb, string expectedSchema, string expectedTable)
    {
        var dbNode = queryNode.Children.OfType<DatabaseNode>()
            .FirstOrDefault(d => d.Name.Equals(expectedDb, StringComparison.OrdinalIgnoreCase));
        Assert.That(dbNode, Is.Not.Null, $"Should contain DB node: {expectedDb}");
        Assert.That(dbNode.DbType, Is.EqualTo("relational"));

        var schemaNode = dbNode.Children.OfType<DataSetNode>()
            .FirstOrDefault(s => s.Name.Equals(expectedSchema, StringComparison.OrdinalIgnoreCase));
        Assert.That(schemaNode, Is.Not.Null, $"Should contain Schema/DataSet node: {expectedSchema}");

        var tableNode = schemaNode.Children.OfType<TableNode>()
            .FirstOrDefault(t => t.Name.Equals(expectedTable, StringComparison.OrdinalIgnoreCase));
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

            using var syntaxTree =
                await parser.ParseAsync(tempFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            ProjectProcessor.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var fileNode = syntaxTree.FileNode;

            var queryNodes = FindQueryNodes(fileNode.Children);
            Assert.That(queryNodes, Is.Not.Empty);
            var sqlQuery = queryNodes[0];
            Assert.That(sqlQuery.Name, Is.EqualTo("SELECT Query"));

            // Since tableName is a variable, it should be skipped and no database node hierarchy should be created for it.
            var hasDbNode = sqlQuery.Children.OfType<DatabaseNode>().Any();

            Assert.That(hasDbNode, Is.False,
                "Should have skipped tableName because it is a template variable placeholder.");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
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
                    Console.WriteLine(
                        $"{indent}    * ScopeSymbolId: {r.ScopeSymbolId}, TargetName: {r.TargetName}, Kind: {r.Kind}");
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
            TypeNode t => t.Name,
            FunctionNode fn => fn.Name,
            MemberNode m => m.Name,
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

    private List<EndpointNode> FindEndpointNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<EndpointNode>();

        foreach (var node in nodes)
        {
            if (node is EndpointNode e) result.Add(e);
            result.AddRange(FindEndpointNodes(node.Children));
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

            using var syntaxTree =
                await parser.ParseAsync(tempFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            ProjectProcessor.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var fileNode = syntaxTree.FileNode;

            var endpoints = FindEndpointNodes(fileNode.Children);
            Assert.That(endpoints, Is.Not.Empty);
            var ep = endpoints.FirstOrDefault(e => e.RouteTemplate.Contains("charge"));
            Assert.That(ep, Is.Not.Null);
            Assert.That(ep.HttpMethod, Is.EqualTo("POST"));

            var externalServices = FindExternalServiceNodes(fileNode.Children);
            Assert.That(externalServices, Is.Not.Empty);
            var es = externalServices.FirstOrDefault(e => e.DomainOrService == "api.stripe.com");
            Assert.That(es, Is.Not.Null);
            Assert.That(es.Protocol, Is.EqualTo("http"));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
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

            using var syntaxTree =
                await parser.ParseAsync(tempFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            ProjectProcessor.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var fileNode = syntaxTree.FileNode;

            var endpoints = FindEndpointNodes(fileNode.Children);
            Assert.That(endpoints, Is.Not.Empty);
            var ep = endpoints.FirstOrDefault(e => e.RouteTemplate.Contains("charge"));
            Assert.That(ep, Is.Not.Null);
            Assert.That(ep.HttpMethod, Is.EqualTo("POST"));

            var entryPoints = FindEntryPointNodes(fileNode.Children);
            var wsEp = entryPoints.FirstOrDefault(e => e.EntryType == "queue-listener");
            Assert.That(wsEp, Is.Not.Null);
            Assert.That(wsEp.Name, Is.EqualTo("ping"));

            var externalServices = FindExternalServiceNodes(fileNode.Children);
            Assert.That(externalServices, Is.Not.Empty);
            var es = externalServices.FirstOrDefault(e => e.DomainOrService == "api.stripe.com");
            Assert.That(es, Is.Not.Null);
            Assert.That(es.Protocol, Is.EqualTo("http"));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
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
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "codeexplorer_test_workspace_" + Guid.NewGuid())
            .Replace('\\', '/');
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            // Project A: C# project
            var projADir = Path.Combine(tempWorkspace, "ProjectA").Replace('\\', '/');
            Directory.CreateDirectory(projADir);

            await File.WriteAllTextAsync(Path.Combine(projADir, "ProjectA.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

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
            WorkspaceIndexer.Register(new CSharpParser());
            WorkspaceIndexer.Register(new TypeScriptParser());

            // Run scanner
            var parser = new WorkspaceIndexer(client);
            var results = await parser.IndexAsync(tempWorkspace, tempWorkspace, clear: true);

            Assert.That(results.NodesCount, Is.GreaterThan(0));
            Assert.That(results.RelationshipsCount, Is.GreaterThan(0));

            // Verify Endpoint and CALLS_ENDPOINT in the database
            var wsId = await client.GetOrCreateWorkspaceIdAsync(tempWorkspace);

            var debugNodes = await client.ExecuteQueryAsync(
                $"MATCH (n) WHERE toString(n.id) STARTS WITH '{wsId}:' OR toString(n.id) = '{wsId}' RETURN labels(n)[0] AS label, n.id AS id, n.name AS name");
            Console.WriteLine($"[DEBUG NODES] {debugNodes}");

            var debugRels = await client.ExecuteQueryAsync(
                $"MATCH (n)-[r]->(m) WHERE toString(n.id) STARTS WITH '{wsId}:' OR toString(n.id) = '{wsId}' RETURN labels(n)[0] AS from_label, n.name AS from_name, type(r) AS rel_type, labels(m)[0] AS to_label, m.name AS to_name");
            Console.WriteLine($"[DEBUG RELS] {debugRels}");

            var endpointCountJson = await client.ExecuteQueryAsync(
                $"MATCH (ep:Endpoint) WHERE toString(ep.id) STARTS WITH '{wsId}:' RETURN count(ep) AS count");
            Assert.That(endpointCountJson, Contains.Substring("\"count\": 2"));

            var implByJson = await client.ExecuteQueryAsync(
                $"MATCH (ep:Endpoint)-[:EXPOSED_BY]->(f:Function {{name: 'charge'}}) WHERE f.id STARTS WITH '{wsId}:' RETURN ep.id AS id");
            Assert.That(implByJson, Contains.Substring(":endpoint:POST:charge"));

            var lateBoundJson = await client.ExecuteQueryAsync(
                $"MATCH (es:ExternalService)-[:CALLS_ENDPOINT]->(ep:Endpoint) WHERE es.id STARTS WITH '{wsId}:' RETURN ep.id AS id");
            Assert.That(lateBoundJson, Contains.Substring(":endpoint:POST:charge"));

            Console.WriteLine(
                $"[IntegrationTest] Parsed {results.NodesCount} nodes and {results.RelationshipsCount} relationships successfully.");
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
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "semantic_test_workspace_" + Guid.NewGuid())
            .Replace('\\', '/');
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            // Project A: C# project using database package (Dapper) and containing configuration + constants + empty subfolder
            var projADir = Path.Combine(tempWorkspace, "ProjectA").Replace('\\', '/');
            Directory.CreateDirectory(projADir);

            await File.WriteAllTextAsync(Path.Combine(projADir, "ProjectA.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\">\n" + "  <ItemGroup>\n" +
                "    <PackageReference Include=\"Dapper\" Version=\"1.0.0\" />\n" +
                "    <PackageReference Include=\"Stripe.net\" Version=\"1.0.0\" />\n" +
                "    <PackageReference Include=\"Microsoft.AspNetCore.App\" Version=\"1.0.0\" />\n" +
                "  </ItemGroup>\n" + "</Project>");

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

            await File.WriteAllTextAsync(Path.Combine(projBDir, "ProjectB.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

            // Setup parsing context
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new MemgraphClient("bolt://127.0.0.1:7687", "", "");

            var ctx = new ParsingContext(tempWorkspace, tempWorkspace, client, channel);
            ctx.WorkspaceId = "1";

            // Register CSharp parser
            WorkspaceIndexer.Register(new CSharpParser());

            var scanParser = new WorkspaceParser(ctx);
            var workspaceNode = new WorkspaceNode("1", "TestWorkspace", tempWorkspace);

            // Replicate initialization done in ParseAsync:
            var filesNodeId = "1:files_structure";
            var workspaceFilesStructure = new FilesStructureNode(filesNodeId, "FilesStructure", tempWorkspace);
            workspaceNode.Children.Add(workspaceFilesStructure);

            var projectsNodeId = "1:projects_structure";
            var workspaceProjectsStructure = new ProjectsStructureNode(projectsNodeId, "ProjectsStructure", tempWorkspace);
            workspaceNode.Children.Add(workspaceProjectsStructure);
            ctx.ProjectsStructure = workspaceProjectsStructure;

            var syntaxNodeId = "1:syntax_structure";
            var workspaceSyntaxStructure = new SyntaxStructureNode(syntaxNodeId, "SyntaxStructure", tempWorkspace);
            workspaceNode.Children.Add(workspaceSyntaxStructure);
            ctx.SyntaxStructure = workspaceSyntaxStructure;

            var semanticNodeId = "1:semantic_structure";
            var workspaceSemanticStructure = new SemanticStructureNode(semanticNodeId, "SemanticStructure", tempWorkspace);
            workspaceNode.Children.Add(workspaceSemanticStructure);
            ctx.SemanticStructure = workspaceSemanticStructure;

            // Invoke ScanDirectoryAsync via reflection
            var rootScan = typeof(WorkspaceParser).GetMethod("ScanDirectoryAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(rootScan, Is.Not.Null);
            await (Task)rootScan.Invoke(scanParser, [tempWorkspace, workspaceNode])!;

            // Run the enrichment pass since we split it
            foreach (var (projProcessor, projNode) in ctx.ProjectsToEnrich)
            {
                await projProcessor.EnrichAsync(projNode);
            }

            ctx.ProjectsToEnrich.Clear();

            var projectsStructure = workspaceNode.Children.OfType<ProjectsStructureNode>().FirstOrDefault();
            Assert.That(projectsStructure, Is.Not.Null);

            var filesStructure = workspaceNode.Children.OfType<FilesStructureNode>().FirstOrDefault();
            Assert.That(filesStructure, Is.Not.Null);

            // Before pruning, ProjectB and EmptyFolder are in the tree
            Assert.That(projectsStructure.Children.Any(c => c is ProjectNode pn && pn.Name == "ProjectB"), Is.True);

            var projectA = projectsStructure.Children.FirstOrDefault(c => c is ProjectNode pn && pn.Name == "ProjectA") as ProjectNode;
            Assert.That(projectA, Is.Not.Null);

            var projectAFolder = filesStructure.Children.OfType<FolderNode>().FirstOrDefault(f => f.Name == "ProjectA");
            Assert.That(projectAFolder, Is.Not.Null);
            Assert.That(projectAFolder.Children.Any(c => c is FolderNode pfn && pfn.Name == "EmptyFolder"), Is.True);

            // 2. Perform pruning
            OntologyPruner.PruneEmptyFolders(workspaceNode);

            // After pruning:
            // ProjectB is removed because it is an empty project
            Assert.That(projectsStructure.Children.Any(c => c is ProjectNode pn && pn.Name == "ProjectB"), Is.False);

            // EmptyFolder is removed
            var folderA =
                projectAFolder.Children.FirstOrDefault(c => c is FolderNode pfn && pfn.Name == "EmptyFolder");
            Assert.That(folderA, Is.Null);

            // Verify project framework detection
            Assert.That(projectA.Extensions, Is.Not.Null);
            Assert.That(projectA.Extensions.ContainsKey("framework"), Is.True);
            Assert.That(projectA.Extensions["framework"], Is.EqualTo("ASP.NET Core"));

            // Verify SemanticStructure node grouping
            var semanticNode = workspaceNode.Children.OfType<SemanticStructureNode>().FirstOrDefault();
            Assert.That(semanticNode, Is.Not.Null);

            // Verify external packages are inside ProjectNode directly (Layer 1)
            var extPackages = projectA.Children.OfType<PackageNode>().Where(p => p.Name != "ProjectA").ToList();
            Assert.That(extPackages, Has.Count.EqualTo(3));
            Assert.That(extPackages.Any(p => p.Name == "Dapper"), Is.True);
            Assert.That(extPackages.Any(p => p.Name == "Stripe.net"), Is.True);
            Assert.That(extPackages.Any(p => p.Name == "Microsoft.AspNetCore.App"), Is.True);

            // Verify semanticNode does NOT contain those external packages as children
            var semPackages = semanticNode.Children.OfType<PackageNode>().ToList();
            Assert.That(semPackages.Any(p => p.Name == "Dapper"), Is.False);
            Assert.That(semPackages.Any(p => p.Name == "Stripe.net"), Is.False);
            Assert.That(semPackages.Any(p => p.Name == "Microsoft.AspNetCore.App"), Is.False);

            // Verify projectA contains the produced package directly as child (Layer 1)
            var directPackages = projectA.Children.OfType<PackageNode>().ToList();
            Assert.That(directPackages.Any(p => p.Name == "ProjectA"), Is.True);

            var fileNode = projectAFolder.Children.OfType<FileNode>().FirstOrDefault(f => f.Name == "Repository.cs");
            Assert.That(fileNode, Is.Not.Null);

            // Check Repository.cs extensions (file-level extensions are removed)
            if (fileNode.Extensions != null)
            {
                Assert.That(fileNode.Extensions.ContainsKey("db_type"), Is.False);
                Assert.That(fileNode.Extensions.ContainsKey("cloud_service"), Is.False);
            }

            // Check if DatabaseNode child was added at the project level (under SemanticStructureNode)
            var dbNode = semanticNode.Children.OfType<DatabaseNode>().FirstOrDefault();
            Assert.That(dbNode, Is.Not.Null);
            Assert.That(dbNode.Name, Is.EqualTo("Dapper"));
            Assert.That(dbNode.DbType, Is.EqualTo("relational"));

            // Check if CloudServiceNode child was added at the project level (under SemanticStructureNode)
            var cloudNode = semanticNode.Children.OfType<CloudServiceNode>().FirstOrDefault();
            Assert.That(cloudNode, Is.Not.Null);
            Assert.That(cloudNode.Name, Is.EqualTo("Stripe"));

            // Check that file-to-library relationships are created in the context
            var usesDb =
                ctx.GlobalProjectDependencies.FirstOrDefault(r => r.From == fileNode.Id && r.Kind == "USES_DB");
            Assert.That(usesDb, Is.Not.Null);
            Assert.That(usesDb.To, Is.EqualTo(dbNode.Id));

            var usesCloud =
                ctx.GlobalProjectDependencies.FirstOrDefault(r => r.From == fileNode.Id && r.Kind == "USES_CLOUD");
            Assert.That(usesCloud, Is.Not.Null);
            Assert.That(usesCloud.To, Is.EqualTo(cloudNode.Id));

            // Check member nodes under TypeNode (Repository)
            var classNode = fileNode.Children.OfType<TypeNode>().FirstOrDefault();
            Assert.That(classNode, Is.Not.Null);

            var variables = classNode.Children.OfType<MemberNode>().ToList();
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

            using var pySyntax =
                await pythonParser.ParseAsync(pyFile, "parent", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            ProjectProcessor.ProcessVisitor(pySyntax, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var pyNode = pySyntax.FileNode;
            Assert.That(pyNode, Is.Not.Null);

            var pyEndpoints = FindEndpointNodes(pyNode.Children);
            Assert.That(pyEndpoints, Has.Count.EqualTo(1));
            Assert.That(pyEndpoints[0].RouteTemplate, Is.EqualTo("/charge"));
            Assert.That(pyEndpoints[0].HttpMethod, Is.EqualTo("POST"));

            var pyExtServices = FindExternalServiceNodes(pyNode.Children);
            Assert.That(pyExtServices, Has.Count.EqualTo(1));
            Assert.That(pyExtServices[0].Name, Is.EqualTo("api.stripe.com"));

            // Verify reference from process_payment function to Endpoint POST:/charge is collected
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

            using var goSyntax =
                await goParser.ParseAsync(goFile, "parent", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            ProjectProcessor.ProcessVisitor(goSyntax, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var goNode = goSyntax.FileNode;
            Assert.That(goNode, Is.Not.Null);

            var goEndpoints = FindEndpointNodes(goNode.Children);
            Assert.That(goEndpoints, Has.Count.EqualTo(1));
            Assert.That(goEndpoints[0].RouteTemplate, Is.EqualTo("/api/v1/users"));
            Assert.That(goEndpoints[0].HttpMethod, Is.EqualTo("GET"));

            // Verify Go references
            var goRefs = FindReferences(goNode.Children);

            Assert.That(
                goRefs.Any(r =>
                    r.TargetName == "GET /api/v1/users" && r.Kind == "IMPLEMENTS" && r.ScopeSymbolId == "GetUsers"),
                Is.True);

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

            using var sqlSyntax =
                await sqlParser.ParseAsync(sqlFile, "parent", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var sqlNode = sqlSyntax.FileNode;
            Assert.That(sqlNode, Is.Not.Null);

            // Schema and DB hierarchy check
            var dbNode = sqlNode.Children.OfType<DatabaseNode>().FirstOrDefault();
            Assert.That(dbNode, Is.Not.Null);
            Assert.That(dbNode.DbType, Is.EqualTo("relational"));

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
    public async Task Test_SemanticModel_DbTypeMapping()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "db_type_mapping_test_workspace_" + Guid.NewGuid());
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new MemgraphClient("bolt://127.0.0.1:7687", "", "");
            var ctx = new ParsingContext(tempWorkspace, tempWorkspace, client, channel);
            ctx.WorkspaceId = "1";
            ctx.SemanticStructure = new SemanticStructureNode("1:semantic_structure", "SemanticStructure", tempWorkspace);

            // Test C# Semantic Analyzer with relational (EntityFramework)
            // Test C# Semantic Model with relational (EntityFramework)
            var csProj = new ProjectNode("cs_project", "cs_project", "cs_project", "csharp");
            var csFile = new FileNode("cs_file", "Repository.cs", "Repository.cs", tempWorkspace + "/Repository.cs");
            csProj.Children.Add(csFile);
            var csParser = new CSharpParser();

            var csSyntaxTree = new SyntaxTree(csFile.FullPath, csFile.Path, null, null, null, csFile, csParser,
                [new RawImport("Microsoft.EntityFrameworkCore", "Repository.cs") { Type = ImportType.External }], [],
                []);
            var csModel = csParser.GetSyntaxEnricher(csSyntaxTree);
            await csModel.EnrichAsync(csProj, ctx);

            var csDbGroup = ctx.SemanticStructure;
            Assert.That(csDbGroup, Is.Not.Null);
            var csDbNode = csDbGroup.Children.OfType<DatabaseNode>().FirstOrDefault(d => d.Name == "Microsoft.EntityFrameworkCore");
            Assert.That(csDbNode, Is.Not.Null);
            Assert.That(csDbNode.Name, Is.EqualTo("Microsoft.EntityFrameworkCore"));
            Assert.That(csDbNode!.DbType, Is.EqualTo("relational"));

            Assert.That(
                ctx.GlobalProjectDependencies.Any(r =>
                    r.From == csFile.Id && r.To == csDbNode.Id && r.Kind == "USES_DB"), Is.True);

            // Test C# Semantic Model with graph (Neo4j.Driver)
            var csGraphProj = new ProjectNode("cs_graph_project", "cs_graph_project", "cs_graph_project", "csharp");
            var csGraphFile = new FileNode("cs_graph_file", "MemgraphClient.cs", "MemgraphClient.cs", tempWorkspace + "/MemgraphClient.cs");
            csGraphProj.Children.Add(csGraphFile);

            var csGraphSyntaxTree = new SyntaxTree(csGraphFile.FullPath, csGraphFile.Path, null, null, null, csGraphFile, csParser,
                [new RawImport("Neo4j.Driver", "MemgraphClient.cs") { Type = ImportType.External }], [],
                []);
            var csGraphModel = csParser.GetSyntaxEnricher(csGraphSyntaxTree);
            await csGraphModel.EnrichAsync(csGraphProj, ctx);

            var csGraphDbGroup = ctx.SemanticStructure;
            Assert.That(csGraphDbGroup, Is.Not.Null);
            var csGraphDbNode = csGraphDbGroup.Children.OfType<DatabaseNode>().FirstOrDefault(d => d.Name == "Neo4j");
            Assert.That(csGraphDbNode, Is.Not.Null);
            Assert.That(csGraphDbNode.Name, Is.EqualTo("Neo4j"));
            Assert.That(csGraphDbNode!.DbType, Is.EqualTo("graph"));

            Assert.That(
                ctx.GlobalProjectDependencies.Any(r =>
                    r.From == csGraphFile.Id && r.To == csGraphDbNode.Id && r.Kind == "USES_DB"), Is.True);

            // Test TypeScript Semantic Model with document (mongoose)
            var tsProj = new ProjectNode("ts_project", "ts_project", "ts_project", "typescript");
            var tsFile = new FileNode("ts_file", "index.ts", "index.ts", tempWorkspace + "/index.ts");
            tsProj.Children.Add(tsFile);
            var tsParser = new TypeScriptParser();

            var tsSyntaxTree = new SyntaxTree(tsFile.FullPath, tsFile.Path, null, null, null, tsFile, tsParser,
                [new RawImport("mongoose", "index.ts") { Type = ImportType.External }], [], []);
            var tsModel = tsParser.GetSyntaxEnricher(tsSyntaxTree);
            await tsModel.EnrichAsync(tsProj, ctx);

            var tsDbGroup = ctx.SemanticStructure;
            Assert.That(tsDbGroup, Is.Not.Null);
            var tsDbNode = tsDbGroup.Children.OfType<DatabaseNode>().FirstOrDefault(d => d.Name == "MongoDB");
            Assert.That(tsDbNode, Is.Not.Null);
            Assert.That(tsDbNode.Name, Is.EqualTo("MongoDB"));
            Assert.That(tsDbNode!.DbType, Is.EqualTo("document"));

            Assert.That(
                ctx.GlobalProjectDependencies.Any(r =>
                    r.From == tsFile.Id && r.To == tsDbNode.Id && r.Kind == "USES_DB"), Is.True);

            // Test Python Semantic Model with keyvalue (redis)
            var pyProj = new ProjectNode("py_project", "py_project", "py_project", "python");
            var pyFile = new FileNode("py_file", "main.py", "main.py", tempWorkspace + "/main.py");
            pyProj.Children.Add(pyFile);
            var pyParser = new PythonParser();

            var pySyntaxTree = new SyntaxTree(pyFile.FullPath, pyFile.Path, null, null, null, pyFile, pyParser,
                [new RawImport("redis", "main.py") { Type = ImportType.External }], [], []);
            var pyModel = pyParser.GetSyntaxEnricher(pySyntaxTree);
            await pyModel.EnrichAsync(pyProj, ctx);

            var pyDbGroup = ctx.SemanticStructure;
            Assert.That(pyDbGroup, Is.Not.Null);
            var pyDbNode = pyDbGroup.Children.OfType<DatabaseNode>().FirstOrDefault(d => d.Name == "Redis");
            Assert.That(pyDbNode, Is.Not.Null);
            Assert.That(pyDbNode.Name, Is.EqualTo("Redis"));
            Assert.That(pyDbNode!.DbType, Is.EqualTo("keyvalue"));

            Assert.That(
                ctx.GlobalProjectDependencies.Any(r =>
                    r.From == pyFile.Id && r.To == pyDbNode.Id && r.Kind == "USES_DB"), Is.True);
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

            using var csSyntaxTree = await SyntaxTree.ParseAsync(csFilePath, "Service.cs", "1", csFileParser,
                ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            ProjectProcessor.ProcessVisitor(csSyntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var csFileNode = csSyntaxTree.FileNode;

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

            using var tsSyntaxTree = await SyntaxTree.ParseAsync(tsFilePath, "app.ts", "1", tsFileParser,
                ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            ProjectProcessor.ProcessVisitor(tsSyntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var tsFileNode = tsSyntaxTree.FileNode;

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

        var parserFirebaseSpecific =
            new GenericLibraryParser("firebaseadmin", "FirebaseAdmin", "cloud", ["firebase-admin"]);
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

    [Test]
    public async Task Test_TypeBindingsAndCallsResolution()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "codeexplorer_typebinding_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            var projDir = Path.Combine(tempWorkspace, "ProjectA");
            Directory.CreateDirectory(projDir);
            await File.WriteAllTextAsync(Path.Combine(projDir, "package.json"), "{}");

            var orderServiceFile = Path.Combine(projDir, "OrderService.ts");

            var orderServiceCode = @"
export class PaymentService {
    async charge() {}
}

export class OrderService {
    constructor(private paymentService: PaymentService) {}
    async process() {
        await this.paymentService.charge();
    }
}";
            await File.WriteAllTextAsync(orderServiceFile, orderServiceCode);

            // Setup parsing
            await using var client = new MemgraphClient("bolt://127.0.0.1:7687", "", "");

            // Register parsers if they aren't already registered
            WorkspaceIndexer.Register(new TypeScriptParser());

            // Run scanner
            var parser = new WorkspaceIndexer(client);
            var results = await parser.IndexAsync(tempWorkspace, tempWorkspace, clear: true);

            Assert.That(results.NodesCount, Is.GreaterThan(0));

            // Verify using Memgraph query that Function 'process' CALLS Function 'charge' in Type 'PaymentService'
            var callsQuery =
                $"MATCH (c:Type {{name: 'PaymentService'}})-[:HAS_METHOD]->(f2:Function {{name: 'charge'}})<-[:CALLS]-(f1:Function {{name: 'process'}}) RETURN f1.name AS f1Name, f2.name AS f2Name";
            var queryResult = await client.ExecuteQueryAsync(callsQuery);

            Assert.That(queryResult, Contains.Substring("\"f1Name\": \"process\""));
            Assert.That(queryResult, Contains.Substring("\"f2Name\": \"charge\""));
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
    public async Task Test_NestedProjectsResolution()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "codeexplorer_nested_project_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            var projADir = Path.Combine(tempWorkspace, "ProjectA");
            Directory.CreateDirectory(projADir);
            await File.WriteAllTextAsync(Path.Combine(projADir, "package.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(projADir, "serviceA.ts"), "export class ServiceA {}");

            var projBDir = Path.Combine(projADir, "ProjectB");
            Directory.CreateDirectory(projBDir);
            await File.WriteAllTextAsync(Path.Combine(projBDir, "package.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(projBDir, "serviceB.ts"), "export class ServiceB {}");

            // Setup parsing
            await using var client = new MemgraphClient(McpIntegrationTests.GetBoltUrl(), "", "");

            // Register parsers if they aren't already registered
            WorkspaceIndexer.Register(new TypeScriptParser());

            // Run scanner
            var parser = new WorkspaceIndexer(client);
            var results = await parser.IndexAsync(tempWorkspace, tempWorkspace, clear: true);

            Assert.That(results.NodesCount, Is.GreaterThan(0));

            // Verify using Memgraph queries
            // ProjectB should be nested under ProjectA via their Folder locations
            var projectAQuery = "MATCH (p:Project {name: 'ProjectA'}) RETURN p.id AS id";
            var projectBQuery = "MATCH (p:Project {name: 'ProjectB'}) RETURN p.id AS id";

            var containsQuery =
                "MATCH (p1:Project {name: 'ProjectA'})-[:LOCATED_IN]->(f1:Folder)-[:CONTAINS]->(f2:Folder)<-[:LOCATED_IN]-(p2:Project {name: 'ProjectB'}) RETURN p1.name AS p1Name, p2.name AS p2Name";

            var resA = await client.ExecuteQueryAsync(projectAQuery);
            var resB = await client.ExecuteQueryAsync(projectBQuery);
            var resContains = await client.ExecuteQueryAsync(containsQuery);

            Assert.That(resA, Contains.Substring("ProjectA"));
            Assert.That(resB, Contains.Substring("ProjectB"));
            Assert.That(resContains, Contains.Substring("\"p1Name\": \"ProjectA\""));
            Assert.That(resContains, Contains.Substring("\"p2Name\": \"ProjectB\""));
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

