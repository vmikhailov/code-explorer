using System.Threading.Channels;
using AwesomeAssertions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.Python;
using NUnit.Framework;

namespace CodeExplorer.Tests;

public class PythonHttpClientTests
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

    [Test]
    public async Task Requests_CallsAndSessions_ExtractsExternalServices()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-requests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "client.py");

        var code = @"import requests

def fetch_data():
    r1 = requests.get('https://api.github.com/users')
    r2 = requests.post(url='https://httpbin.org/post', json={'key': 'val'})

    with requests.Session() as session:
        r3 = session.get('https://api.example.com/items')

    # Dict get should NOT be classified as external service
    headers = {'Authorization': 'Bearer xxx'}
    token = headers.get('Authorization')
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new PythonParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var services = FindExternalServiceNodes(fileNode.Children);
            services.Count.Should().Be(3);

            services.Any(s => s.DomainOrService == "api.github.com" && s.Path == "/users").Should().BeTrue();
            services.Any(s => s.DomainOrService == "httpbin.org" && s.Path == "/post").Should().BeTrue();
            services.Any(s => s.DomainOrService == "api.example.com" && s.Path == "/items").Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Httpx_CallsAndClients_ExtractsExternalServices()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-httpx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "httpx_client.py");

        var code = @"import httpx

def sync_httpx():
    r1 = httpx.get('https://api.stripe.com/v1/charges')

    with httpx.Client() as client:
        r2 = client.get('https://api.stripe.com/v1/customers')

async def async_httpx():
    async with httpx.AsyncClient() as client:
        r3 = await client.post('https://api.stripe.com/v1/refunds', json={'amount': 100})
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new PythonParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var services = FindExternalServiceNodes(fileNode.Children);
            services.Count.Should().Be(3);

            services.Any(s => s.DomainOrService == "api.stripe.com" && s.Path == "/v1/charges").Should().BeTrue();
            services.Any(s => s.DomainOrService == "api.stripe.com" && s.Path == "/v1/customers").Should().BeTrue();
            services.Any(s => s.DomainOrService == "api.stripe.com" && s.Path == "/v1/refunds").Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task AioHttp_ClientSession_ExtractsExternalServices()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-aiohttp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "aio_client.py");

        var code = @"import aiohttp

async def run_aio():
    async with aiohttp.ClientSession() as session:
        async with session.get('https://api.weather.gov/alerts') as resp:
            data = await resp.json()

    resp2 = await aiohttp.ClientSession().post('https://api.weather.gov/stations')
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new PythonParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var services = FindExternalServiceNodes(fileNode.Children);
            services.Count.Should().Be(2);

            services.Any(s => s.DomainOrService == "api.weather.gov" && s.Path == "/alerts").Should().BeTrue();
            services.Any(s => s.DomainOrService == "api.weather.gov" && s.Path == "/stations").Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
