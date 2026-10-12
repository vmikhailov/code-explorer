using System.Threading.Channels;
using AwesomeAssertions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
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
}
