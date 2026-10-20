using System.Threading.Channels;
using CodeExplorer.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class TypeScriptHttpClientTests
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

        public static async Task<TestWorkspace> CreateAsync(string code, string fileName = "client.ts")
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ts_client_test_" + Guid.NewGuid().ToString("N"));
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
    public async Task Test_Request_DirectAndMethodCalls()
    {
        var code = @"
import request from 'request';

export function callGithubUsers() {
    request('https://api.github.com/users', (err, res, body) => {
        console.log(body);
    });
}

export function createRepo() {
    request.post('https://api.github.com/repos', { json: true }, (err, res, body) => {
        console.log(body);
    });
}

export function requestWithOptions() {
    request({ url: 'https://api.payments.com/v1/charge', method: 'POST' });
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var extServices = FindNodes<ExternalServiceNode>(ws.FileNode.Children);
        extServices.Should().HaveCount(3);

        extServices.Should().Contain(s => s.DomainOrService == "api.github.com" && s.Path == "/users");
        extServices.Should().Contain(s => s.DomainOrService == "api.github.com" && s.Path == "/repos");
        extServices.Should().Contain(s => s.DomainOrService == "api.payments.com" && s.Path == "/v1/charge");

        var refs = FindAllReferences(ws.FileNode);
        refs.Should().Contain(r => r.TargetName == "api.github.com" && r.Kind == "CALLS");
        refs.Should().Contain(r => r.TargetName == "api.payments.com" && r.Kind == "CALLS");
    }

    [Test]
    public async Task Test_Undici_DirectAndClientCalls()
    {
        var code = @"
import undici from 'undici';
import { Client, Pool } from 'undici';

export async function fetchUndiciOrders() {
    const res = await undici.request('https://api.undici.com/v1/orders', { method: 'GET' });
    return res;
}

export async function clientRequest() {
    const client = new Client('https://api.undici-client.com');
    const res = await client.request({ path: '/v1/users', method: 'GET' });
    return res;
}

export async function poolRequest() {
    const pool = new Pool('https://api.undici-pool.com');
    const res = await pool.request({ path: '/v1/status', method: 'GET' });
    return res;
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var extServices = FindNodes<ExternalServiceNode>(ws.FileNode.Children);
        extServices.Should().HaveCountGreaterThanOrEqualTo(3);

        extServices.Should().Contain(s => s.DomainOrService == "api.undici.com");
        extServices.Should().Contain(s => s.DomainOrService == "api.undici-client.com");
        extServices.Should().Contain(s => s.DomainOrService == "api.undici-pool.com");

        var refs = FindAllReferences(ws.FileNode);
        refs.Should().Contain(r => r.TargetName == "api.undici.com" && r.Kind == "CALLS");
        refs.Should().Contain(r => r.TargetName == "api.undici-client.com" && r.Kind == "CALLS");
        refs.Should().Contain(r => r.TargetName == "api.undici-pool.com" && r.Kind == "CALLS");
    }

    [Test]
    public async Task Test_Urllib_DirectAndCurlCalls()
    {
        var code = @"
import urllib from 'urllib';

export async function fetchUrllibItems() {
    const res = await urllib.request('https://api.urllib.com/v1/items', { method: 'GET' });
    return res;
}

export async function curlUrllibProducts() {
    const res = await urllib.curl('https://api.urllib.com/v1/products', { method: 'POST' });
    return res;
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var extServices = FindNodes<ExternalServiceNode>(ws.FileNode.Children);
        extServices.Should().HaveCount(2);

        extServices.Should().Contain(s => s.DomainOrService == "api.urllib.com" && s.Path == "/v1/items");
        extServices.Should().Contain(s => s.DomainOrService == "api.urllib.com" && s.Path == "/v1/products");

        var refs = FindAllReferences(ws.FileNode);
        refs.Should().Contain(r => r.TargetName == "api.urllib.com" && r.Kind == "CALLS");
    }

    [Test]
    public async Task Test_AngularHttpClient_ServiceHealthGuard_InterpolatedUrl_CreatesUnknownServiceWithPing()
    {
        var code = """
import { HttpClient } from '@angular/common/http';
import { inject } from '@angular/core';

export function serviceHealthGuard(requiredServices: string[]) {
    const http = inject(HttpClient);
    return requiredServices.map(serviceUrl => http.head(`${serviceUrl}/ping`));
}
""";

        using var ws = await TestWorkspace.CreateAsync(code);

        var extServices = FindNodes<ExternalServiceNode>(ws.FileNode.Children);
        extServices.Should().HaveCount(1);
        var svc = extServices.First();
        svc.DomainOrService.Should().Be("unknown-service");
        svc.Path.Should().Be("/ping");
    }

    [Test]
    public async Task Test_CyclicAndSuffixClassFields_DoNotStackOverflow()
    {
        var code = """
import axios from 'axios';

export class DedalosApiClient {
    private url: string;
    private baseUrl: string;
    private apiUrl: string;

    constructor(options: any) {
        const baseUrl = this.url || options.baseUrl;
        this.url = options.url || this.baseUrl || `${this.apiUrl}/v1`;
        this.baseUrl = this.url || this.apiUrl;
        this.apiUrl = `${this.baseUrl}/service` || this.url;
    }

    public async fetchItems() {
        const requestUrl = `${this.url}/items`;
        return await axios.get(requestUrl);
    }
}
""";

        using var ws = await TestWorkspace.CreateAsync(code);
        ws.FileNode.Should().NotBeNull();
    }
}
