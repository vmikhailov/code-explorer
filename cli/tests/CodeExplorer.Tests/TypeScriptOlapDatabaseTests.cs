using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class TypeScriptOlapDatabaseTests
{
    private sealed class TestWorkspace : IDisposable
    {
        public string DirectoryPath { get; }
        public string FilePath { get; }
        public SyntaxTree SyntaxTree { get; }
        public FileNode FileNode => SyntaxTree.FileNode;
        public ParsingContext Context { get; }

        private TestWorkspace(string dir, string file, SyntaxTree tree, ParsingContext ctx)
        {
            DirectoryPath = dir;
            FilePath = file;
            SyntaxTree = tree;
            Context = ctx;
        }

        public static async Task<TestWorkspace> CreateAsync(string code, string fileName = "database.ts")
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ts_olap_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, fileName);
            await File.WriteAllTextAsync(filePath, code);

            var parser = new TypeScriptParser();
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            return new TestWorkspace(tempDir, filePath, syntaxTree, ctx);
        }

        public void Dispose()
        {
            SyntaxTree.Dispose();
            try { Directory.Delete(DirectoryPath, true); } catch { }
        }
    }

    private static List<T> FindNodes<T>(IEnumerable<IOntologyNode> nodes) where T : IOntologyNode
    {
        var result = new List<T>();
        foreach (var node in nodes)
        {
            if (node is T match) result.Add(match);
            result.AddRange(FindNodes<T>(node.Children));
        }
        return result;
    }

    private static List<Reference> FindAllReferences(FileNode fileNode)
    {
        var result = new List<Reference>(fileNode.References);
        result.AddRange(FindReferences(fileNode.Children));
        return result;
    }

    private static List<Reference> FindReferences(IEnumerable<IOntologyNode> nodes)
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
    public async Task Test_ClickHouse_QueryAndCommandAndInsert()
    {
        var code = """
                   import { createClient } from '@clickhouse/client';

                   const client = createClient({
                       url: 'http://localhost:8123',
                       database: 'analytics'
                   });

                   export async function processAnalytics() {
                       // 1. SELECT query via options object
                       const resultSet = await client.query({
                           query: 'SELECT event_id, event_name FROM analytics.events WHERE status = 1',
                           format: 'JSONEachRow'
                       });

                       // 2. Command statement (ALTER TABLE)
                       await client.command({
                           query: 'ALTER TABLE analytics.events DELETE WHERE event_id = 42'
                       });

                       // 3. Direct table insert
                       await client.insert({
                           table: 'user_metrics',
                           values: [{ user_id: 1, metric: 99 }]
                       });

                       // 4. Direct SQL string query
                       await client.query('SELECT * FROM realtime_feed');
                   }
                   """;

        using var ws = await TestWorkspace.CreateAsync(code);

        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().NotBeEmpty();

        var allReferences = FindAllReferences(ws.FileNode);

        // 1. SELECT query
        var selectQuery = queries.FirstOrDefault(q => q.QueryText.Contains("analytics.events") || q.Name.Contains("events"));
        selectQuery.Should().NotBeNull();
        allReferences.Should().Contain(r => r.TargetName == "events" && r.Kind == "DEPENDS_ON");

        // 2. ALTER TABLE command
        var alterQuery = queries.FirstOrDefault(q => q.QueryText.Contains("ALTER TABLE") || q.Name.Contains("ALTER") || q.References.Any(r => r.TargetName == "events"));
        alterQuery.Should().NotBeNull();

        // 3. Insert into user_metrics
        var insertQuery = queries.FirstOrDefault(q => q.Name.Contains("user_metrics") || q.References.Any(r => r.TargetName == "user_metrics"));
        insertQuery.Should().NotBeNull();
        allReferences.Should().Contain(r => r.TargetName == "user_metrics" && r.Kind == "DEPENDS_ON");

        // 4. Direct realtime_feed query
        var feedQuery = queries.FirstOrDefault(q => q.QueryText.Contains("realtime_feed") || q.Name.Contains("realtime_feed"));
        feedQuery.Should().NotBeNull();
        allReferences.Should().Contain(r => r.TargetName == "realtime_feed" && r.Kind == "DEPENDS_ON");
    }

    [Test]
    public async Task Test_BigQuery_QueryAndJobAndTableInsert()
    {
        var code = """
                   import { BigQuery } from '@google-cloud/bigquery';

                   const bigquery = new BigQuery();

                   export async function analyzeData() {
                       // 1. Direct query with backticks
                       const [rows] = await bigquery.query('SELECT user_id, amount FROM `finance.transactions` WHERE amount > 100');

                       // 2. createQueryJob with options object
                       const [job] = await bigquery.createQueryJob({
                           query: 'SELECT order_id, total FROM `store_orders` WHERE created_at > "2026-01-01"'
                       });

                       // 3. Dataset and table insert
                       const dataset = bigquery.dataset('analytics_warehouse');
                       const table = dataset.table('click_logs');
                       await table.insert([{ click_id: 1, url: 'https://example.com' }]);
                   }
                   """;

        using var ws = await TestWorkspace.CreateAsync(code);

        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().NotBeEmpty();

        var allReferences = FindAllReferences(ws.FileNode);

        // 1. Direct transactions query
        var txQuery = queries.FirstOrDefault(q => q.QueryText.Contains("transactions") || q.Name.Contains("transactions"));
        txQuery.Should().NotBeNull();
        allReferences.Should().Contain(r => r.TargetName == "transactions" && r.Kind == "DEPENDS_ON");

        // 2. store_orders job query
        var orderQuery = queries.FirstOrDefault(q => q.QueryText.Contains("store_orders") || q.Name.Contains("store_orders"));
        orderQuery.Should().NotBeNull();
        allReferences.Should().Contain(r => r.TargetName == "store_orders" && r.Kind == "DEPENDS_ON");

        // 3. Table insert
        var clickInsert = queries.FirstOrDefault(q => q.Name.Contains("click_logs") || q.References.Any(r => r.TargetName == "click_logs"));
        clickInsert.Should().NotBeNull();
        allReferences.Should().Contain(r => r.TargetName == "click_logs" && r.Kind == "DEPENDS_ON");
    }
}
