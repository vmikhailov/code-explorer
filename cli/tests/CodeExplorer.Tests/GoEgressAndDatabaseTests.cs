using System.Threading.Channels;
using CodeExplorer.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.Go;
using NUnit.Framework;

namespace CodeExplorer.Tests;

public class GoEgressAndDatabaseTests
{
    private static List<ExternalServiceNode> FindExternalServiceNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<ExternalServiceNode>();
        foreach (var node in nodes)
        {
            if (node is ExternalServiceNode es)
            {
                result.Add(es);
            }
            result.AddRange(FindExternalServiceNodes(node.Children));
        }
        return result;
    }

    private static List<QueryNode> FindQueryNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<QueryNode>();
        foreach (var node in nodes)
        {
            if (node is QueryNode q)
            {
                result.Add(q);
            }
            result.AddRange(FindQueryNodes(node.Children));
        }
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
    public async Task Pgx_QueriesAndContext_ExtractsQueryNodesAndDatabaseReferences()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-pgx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "main.go");

        var code = @"package main

import (
	""context""
	""github.com/jackc/pgx/v5/pgxpool""
)

func queryDatabase(ctx context.Context, pool *pgxpool.Pool) {
	_ = pool.QueryRow(ctx, ""SELECT id, name, email FROM users WHERE id = $1"", 1)
	_, _ = pool.Exec(ctx, ""DELETE FROM orders WHERE status = 'cancelled'"")
}
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new GoParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var queries = FindQueryNodes(fileNode.Children);
            queries.Count.Should().BeGreaterThanOrEqualTo(2);

            var selectQuery = queries.FirstOrDefault(q => q.QueryText.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
            selectQuery.Should().NotBeNull();
            selectQuery!.References.Any(r => r.Kind == OntologyConstants.Relationships.UsesDb && r.TargetName == "postgres").Should().BeTrue();
            selectQuery.References.Any(r => r.TargetName.Equals("users", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();

            var deleteQuery = queries.FirstOrDefault(q => q.QueryText.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
            deleteQuery.Should().NotBeNull();
            deleteQuery!.References.Any(r => r.Kind == OntologyConstants.Relationships.UsesDb && r.TargetName == "postgres").Should().BeTrue();
            deleteQuery.References.Any(r => r.TargetName.Equals("orders", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Resty_EgressCalls_ExtractsExternalServices()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-resty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "client.go");

        var code = @"package main

import (
	""github.com/go-resty/resty/v2""
)

func makeCalls(client *resty.Client) {
	_, _ = client.R().Get(""https://api.stripe.com/v1/charges"")
	_, _ = client.R().SetHeader(""Auth"", ""Bearer xyz"").Post(""https://api.github.com/repos"")
}
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new GoParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var services = FindExternalServiceNodes(fileNode.Children);
            services.Count.Should().BeGreaterThanOrEqualTo(2);

            var stripe = services.FirstOrDefault(s => s.DomainOrService.Contains("stripe", StringComparison.OrdinalIgnoreCase));
            stripe.Should().NotBeNull();
            stripe!.DomainOrService.Should().Be("api.stripe.com");

            var github = services.FirstOrDefault(s => s.DomainOrService.Contains("github", StringComparison.OrdinalIgnoreCase));
            github.Should().NotBeNull();
            github!.DomainOrService.Should().Be("api.github.com");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task NetHttp_EgressCalls_ExtractsExternalServices()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-nethttp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "http.go");

        var code = @"package main

import (
	""net/http""
)

func sendRequests() {
	_, _ = http.Get(""https://status.example.com/health"")
	_, _ = http.NewRequest(""POST"", ""https://auth.company.org/token"", nil)
}
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new GoParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var services = FindExternalServiceNodes(fileNode.Children);
            services.Count.Should().BeGreaterThanOrEqualTo(2);

            var status = services.FirstOrDefault(s => s.DomainOrService.Contains("example.com", StringComparison.OrdinalIgnoreCase));
            status.Should().NotBeNull();

            var auth = services.FirstOrDefault(s => s.DomainOrService.Contains("company.org", StringComparison.OrdinalIgnoreCase));
            auth.Should().NotBeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ClickHouse_QueriesAndBatch_ExtractsQueryNodesAndDatabaseReferences()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-clickhouse-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "analytics.go");

        var code = @"package repository

import (
	""context""
	""github.com/ClickHouse/clickhouse-go/v2""
	""github.com/ClickHouse/clickhouse-go/v2/lib/driver""
)

type Repo struct {
	conn driver.Conn
}

func (r *Repo) Init(ctx context.Context) error {
	_ = r.conn.Exec(ctx, ""CREATE TABLE IF NOT EXISTS bundle_placements (id UInt32) ENGINE = MergeTree() ORDER BY id"")
	batch, _ := r.conn.PrepareBatch(ctx, ""INSERT INTO bundle_snapshots (bundle_id, data) VALUES (?, ?)"")
	_ = batch.Send()
	return nil
}
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new GoParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var queries = FindQueryNodes(fileNode.Children);
            queries.Count.Should().BeGreaterThanOrEqualTo(2);

            var createTableQuery = queries.FirstOrDefault(q => q.QueryText.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase));
            createTableQuery.Should().NotBeNull();
            createTableQuery!.References.Any(r => r.Kind == OntologyConstants.Relationships.UsesDb && r.TargetName == "clickhouse").Should().BeTrue();
            createTableQuery.References.Any(r => r.TargetName.Equals("bundle_placements", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();

            var insertQuery = queries.FirstOrDefault(q => q.QueryText.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase));
            insertQuery.Should().NotBeNull();
            insertQuery!.References.Any(r => r.Kind == OntologyConstants.Relationships.UsesDb && r.TargetName == "clickhouse").Should().BeTrue();
            insertQuery.References.Any(r => r.TargetName.Equals("bundle_snapshots", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task BigQuery_QueriesAndTables_ExtractsQueryNodesAndDatabaseReferences()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-bigquery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "bq.go");

        var code = @"package repository

import (
	""context""
	""cloud.google.com/go/bigquery""
)

type BqRepo struct {
	client *bigquery.Client
}

type EventRow struct {
	BundleID uint32 `bigquery:""bundle_id""`
}

func (r *BqRepo) QueryData(ctx context.Context) error {
	q := r.client.Query(""SELECT bundle_id, cost FROM tracking.historical_events WHERE bundle_id = 1"")
	_, _ = q.Read(ctx)
	table := r.client.Dataset(""analytics"").Table(""event_stream"")
	_ = table.Inserter()
	return nil
}
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new GoParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var queries = FindQueryNodes(fileNode.Children);
            queries.Count.Should().BeGreaterThanOrEqualTo(1);

            var selectQuery = queries.FirstOrDefault(q => q.QueryText.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
            selectQuery.Should().NotBeNull();
            selectQuery!.References.Any(r => r.Kind == OntologyConstants.Relationships.UsesDb && r.TargetName == "bigquery").Should().BeTrue();
            selectQuery.References.Any(r => r.TargetName.Equals("historical_events", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();

            var allRefs = fileNode.References.Concat(FindReferences(fileNode.Children)).ToList();
            allRefs.Any(r => r.TargetName.Equals("event_stream", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
            allRefs.Any(r => r.Kind == OntologyConstants.Relationships.UsesDb && r.TargetName == "bigquery").Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task PubSub_WorkerDefAndConstants_ExtractsNormalizedTopics()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-pubsub-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "ingest.go");

        var code = @"package ingest

import (
	""context""
	""cloud.google.com/go/pubsub""
)

const (
	TopicImpression = ""IMPRESSION_TOPIC""
	TopicConversion = ""CONVERSION_TOPIC""
)

type ConversionV1 struct {
	ImpressionID string `json:""impression_id"" event_type:""TYPE_CPV1""`
}

type WorkerDef struct {
	TopicID string
	SubID   string
}

func StartIngest(ctx context.Context) {
	_ = []WorkerDef{
		{
			TopicID: ""ImpressionTopic"",
			SubID:   ""aggregator-impressions-sub"",
		},
		{
			TopicID: ""ConversionTopic"",
			SubID:   ""aggregator-conversions-sub"",
		},
	}
}
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new GoParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var allRefs = fileNode.References.Concat(FindReferences(fileNode.Children)).ToList();

            // Literal topic names are preserved as written
            allRefs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "gcp:IMPRESSION_TOPIC").Should().BeTrue();
            allRefs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "gcp:CONVERSION_TOPIC").Should().BeTrue();
            allRefs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "gcp:TYPE_CPV1").Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
