using System.Threading.Channels;
using NUnit.Framework;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.Java;
using CodeExplorer.Parser.Python;
using CodeExplorer.Parser.TypeScript;
using CodeExplorer.Tests.Shared;

namespace CodeExplorer.Tests;

[TestFixture]
public class MultiLanguageHttpResolutionTests
{
    [SetUp]
    public void SetUp()
    {
        RouteDictionaryRegistry.Clear();
    }

    private static async Task<List<ExternalServiceNode>> ParseAndGetExternalServicesAsync(IFileParser parser, string code, string fileName)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_http_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, fileName);
            await File.WriteAllTextAsync(filePath, code);

            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var extServices = new List<ExternalServiceNode>();
            FindNodes(syntaxTree.FileNode.Children, extServices);
            return extServices;
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static void FindNodes(IEnumerable<IOntologyNode> nodes, List<ExternalServiceNode> result)
    {
        foreach (var node in nodes)
        {
            if (node is ExternalServiceNode es) result.Add(es);
            FindNodes(node.Children, result);
        }
    }

    // ==========================================
    // Python Tests
    // ==========================================

    [Test]
    public async Task Python_Requests_FString_And_VariableAssignment_ResolvesExternalService()
    {
        var code = """
        import requests

        def get_bundle(bundle_id):
            url = f"http://bundle-service/api/v1/bundles/{bundle_id}"
            resp = requests.get(url)
            return resp.json()
        """;

        var services = await ParseAndGetExternalServicesAsync(new PythonParser(), code, "client.py");

        Assert.That(services, Has.Count.GreaterThan(0));
        var svc = services.First();
        Assert.That(svc.DomainOrService, Is.EqualTo("bundle-service"));
        Assert.That(svc.Path, Is.EqualTo("/api/v1/bundles/*"));
    }

    [Test]
    public async Task Python_Requests_RouteDictionaryResolution_ResolvesRegisteredRoute()
    {
        RouteDictionaryRegistry.Register("GET_ORDER", "/api/v1/orders/{orderId}", "order-service");

        var code = """
        import requests

        def fetch_order(order_id):
            url = GET_ORDER
            resp = requests.get(url)
            return resp.json()
        """;

        var services = await ParseAndGetExternalServicesAsync(new PythonParser(), code, "client.py");

        Assert.That(services, Has.Count.GreaterThan(0));
        var svc = services.First();
        Assert.That(svc.DomainOrService, Is.EqualTo("order-service"));
        Assert.That(svc.Path, Is.EqualTo("/api/v1/orders/*"));
    }

    [Test]
    public async Task Python_Httpx_BinaryConcat_ResolvesExternalService()
    {
        var code = """
        import httpx

        def fetch_items():
            base = "http://catalog-service"
            resp = httpx.get(base + "/api/v1/items")
            return resp.json()
        """;

        var services = await ParseAndGetExternalServicesAsync(new PythonParser(), code, "client.py");

        Assert.That(services, Has.Count.GreaterThan(0));
        var svc = services.First();
        Assert.That(svc.Path, Is.EqualTo("/api/v1/items"));
    }

    // ==========================================
    // Go Tests
    // ==========================================

    [Test]
    public async Task Go_Http_Sprintf_And_VariableAssignment_ResolvesExternalService()
    {
        var code = """
        package client

        import (
            "fmt"
            "net/http"
        )

        func GetBundle(bundleId string) (*http.Response, error) {
            url := fmt.Sprintf("%s/api/v1/bundles/%s", "http://bundle-service", bundleId)
            return http.Get(url)
        }
        """;

        var services = await ParseAndGetExternalServicesAsync(new GoParser(), code, "client.go");

        Assert.That(services, Has.Count.GreaterThan(0));
        var svc = services.First();
        Assert.That(svc.DomainOrService, Is.EqualTo("bundle-service"));
        Assert.That(svc.Path, Is.EqualTo("/api/v1/bundles/*"));
    }

    [Test]
    public async Task Go_Http_NewRequestWithContext_ResolvesExternalService()
    {
        var code = """
        package client

        import (
            "context"
            "net/http"
        )

        func FetchUsers(ctx context.Context) (*http.Response, error) {
            req, err := http.NewRequestWithContext(ctx, "GET", "http://user-service/api/v1/users", nil)
            if err != nil { return nil, err }
            client := &http.Client{}
            return client.Do(req)
        }
        """;

        var services = await ParseAndGetExternalServicesAsync(new GoParser(), code, "client.go");

        Assert.That(services, Has.Count.GreaterThan(0));
        var svc = services.First();
        Assert.That(svc.DomainOrService, Is.EqualTo("user-service"));
        Assert.That(svc.Path, Is.EqualTo("/api/v1/users"));
    }

    // ==========================================
    // C# Tests
    // ==========================================

    [Test]
    public async Task CSharp_NamedHttpClientFactory_ResolvesExternalService()
    {
        var code = """
        using System.Net.Http;
        using System.Threading.Tasks;

        public class BundleClient
        {
            private readonly IHttpClientFactory _factory;
            public BundleClient(IHttpClientFactory factory) => _factory = factory;

            public async Task<string> GetBundleAsync(string id)
            {
                var client = _factory.CreateClient("bundle-service");
                var response = await client.GetAsync($"/api/v1/bundles/{id}");
                return await response.Content.ReadAsStringAsync();
            }
        }
        """;

        var services = await ParseAndGetExternalServicesAsync(new CSharpParser(), code, "BundleClient.cs");

        Assert.That(services, Has.Count.GreaterThan(0));
        var svc = services.First();
        Assert.That(svc.DomainOrService, Is.EqualTo("bundle-service"));
        Assert.That(svc.Path, Is.EqualTo("/api/v1/bundles/*"));
    }

    [Test]
    public async Task CSharp_HttpClient_RouteDictionaryResolution_ResolvesExternalService()
    {
        RouteDictionaryRegistry.Register("GetOrdersRoute", "/api/v1/orders/{orderId}", "order-service");

        var code = """
        using System.Net.Http;
        using System.Threading.Tasks;

        public class OrderClient
        {
            public async Task FetchOrders(HttpClient client)
            {
                await client.GetAsync(GetOrdersRoute);
            }
        }
        """;

        var services = await ParseAndGetExternalServicesAsync(new CSharpParser(), code, "OrderClient.cs");

        Assert.That(services, Has.Count.GreaterThan(0));
        var svc = services.First();
        Assert.That(svc.DomainOrService, Is.EqualTo("order-service"));
        Assert.That(svc.Path, Is.EqualTo("/api/v1/orders/*"));
    }

    // ==========================================
    // Java Tests
    // ==========================================

    [Test]
    public async Task Java_FeignClient_Interface_ResolvesExternalService()
    {
        var code = """
        package com.example.client;

        import org.springframework.cloud.openfeign.FeignClient;

        @FeignClient(name = "bundle-service", path = "/api/bundles")
        public interface BundleClient {
        }
        """;

        var services = await ParseAndGetExternalServicesAsync(new JavaParser(), code, "BundleClient.java");

        Assert.That(services, Has.Count.GreaterThan(0));
        var svc = services.First();
        Assert.That(svc.DomainOrService, Is.EqualTo("bundle-service"));
        Assert.That(svc.Path, Is.EqualTo("/api/bundles"));
    }

    [Test]
    public async Task Java_RestTemplate_VariableAssignment_ResolvesExternalService()
    {
        var code = """
        package com.example.client;

        import org.springframework.web.client.RestTemplate;

        public class OrderClient {
            private RestTemplate restTemplate;

            public String getOrder(String id) {
                String url = "http://order-service/api/v1/orders/" + id;
                return restTemplate.getForObject(url, String.class);
            }
        }
        """;

        var services = await ParseAndGetExternalServicesAsync(new JavaParser(), code, "OrderClient.java");

        Assert.That(services, Has.Count.GreaterThan(0));
        var svc = services.First();
        Assert.That(svc.DomainOrService, Is.EqualTo("order-service"));
        Assert.That(svc.Path, Is.EqualTo("/api/v1/orders/*"));
    }

    // ==========================================
    // Cross-Language Late-Binding Integration Tests
    // ==========================================

    [Test]
    public async Task CrossLanguage_PythonClient_Matches_GoEndpoint()
    {
        var pythonCode = """
        import requests

        BASE_URL = "http://bundle-service"

        def get_bundle(bundle_id):
            url = f"{BASE_URL}/api/v1/bundles/{bundle_id}"
            return requests.get(url)
        """;

        var services = await ParseAndGetExternalServicesAsync(new PythonParser(), pythonCode, "client.py");
        Assert.That(services, Has.Count.GreaterThan(0));
        var pythonSvc = services.First();

        var goEndpoint = new EndpointNode(
            "go-bundle:endpoint:GET:/api/v1/bundles/:id",
            "GET /api/v1/bundles/:id",
            "server.go",
            "GET",
            "/api/v1/bundles/:id"
        );

        var layer5 = new Layer5AnalysisParser();
        var isMatchMethod = typeof(Layer5AnalysisParser).GetMethod("IsMatch",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            [typeof(ExternalServiceNode), typeof(EndpointNode)])!;

        var isMatch = (bool)isMatchMethod.Invoke(layer5, [pythonSvc, goEndpoint])!;
        Assert.That(isMatch, Is.True, "Python client external service should late-bind to Go endpoint");
    }

    [Test]
    public async Task CrossLanguage_JavaRestTemplate_Matches_CSharpEndpoint()
    {
        var javaCode = """
        package com.example.client;
        import org.springframework.web.client.RestTemplate;

        public class OrderClient {
            private RestTemplate restTemplate;

            public String getOrder(String id) {
                String url = "http://order-service/api/v1/orders/" + id;
                return restTemplate.getForObject(url, String.class);
            }
        }
        """;

        var services = await ParseAndGetExternalServicesAsync(new JavaParser(), javaCode, "OrderClient.java");
        Assert.That(services, Has.Count.GreaterThan(0));
        var javaSvc = services.First();

        var csharpEndpoint = new EndpointNode(
            "cs-order:endpoint:GET:/api/v1/orders/{orderId}",
            "GET /api/v1/orders/{orderId}",
            "OrdersController.cs",
            "GET",
            "/api/v1/orders/{orderId}"
        );

        var layer5 = new Layer5AnalysisParser();
        var isMatchMethod = typeof(Layer5AnalysisParser).GetMethod("IsMatch",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            [typeof(ExternalServiceNode), typeof(EndpointNode)])!;

        var isMatch = (bool)isMatchMethod.Invoke(layer5, [javaSvc, csharpEndpoint])!;
        Assert.That(isMatch, Is.True, "Java RestTemplate external service should late-bind to C# endpoint");
    }

    [Test]
    public async Task CrossLanguage_GoClient_Matches_TypeScriptEndpoint()
    {
        var goCode = """
        package client
        import (
            "context"
            "net/http"
        )

        func FetchUsers(ctx context.Context) (*http.Response, error) {
            req, err := http.NewRequestWithContext(ctx, "GET", "http://user-service/api/v1/users", nil)
            if err != nil { return nil, err }
            return (&http.Client{}).Do(req)
        }
        """;

        var services = await ParseAndGetExternalServicesAsync(new GoParser(), goCode, "client.go");
        Assert.That(services, Has.Count.GreaterThan(0));
        var goSvc = services.First();

        var tsEndpoint = new EndpointNode(
            "ts-user:endpoint:GET:/api/v1/users",
            "GET /api/v1/users",
            "user.controller.ts",
            "GET",
            "/api/v1/users"
        );

        var layer5 = new Layer5AnalysisParser();
        var isMatchMethod = typeof(Layer5AnalysisParser).GetMethod("IsMatch",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            [typeof(ExternalServiceNode), typeof(EndpointNode)])!;

        var isMatch = (bool)isMatchMethod.Invoke(layer5, [goSvc, tsEndpoint])!;
        Assert.That(isMatch, Is.True, "Go HTTP client external service should late-bind to TypeScript endpoint");
    }

    [Test]
    public async Task CrossLanguage_AngularHttpClient_Matches_DotNetEndpoint()
    {
        RouteDictionaryRegistry.Clear();
        var envCode = """
        export const environment = {
            games: 'http://localhost:8060/api/v1/games',
            players: 'http://localhost:8050/api/v1/profiles'
        };
        """;
        RouteDictionaryRegistry.ScanAndRegister(envCode);

        var urlConstructor = """
        import { environment as env } from './environment';
        export const getGamesListUrl = () => env.games;
        """;
        RouteDictionaryRegistry.ScanAndRegister(urlConstructor);

        var angularCode = """
        import { Component } from '@angular/core';
        import { HttpClient } from '@angular/common/http';
        import { getGamesListUrl } from './urlConstructor';

        @Component({ selector: 'app-games', template: '' })
        export class GamesComponent {
            constructor(private httpClient: HttpClient) {}
            getGames() {
                this.httpClient.get<any>(getGamesListUrl()).subscribe();
            }
        }
        """;

        var services = await ParseAndGetExternalServicesAsync(new TypeScriptParser(), angularCode, "games.component.ts");
        Assert.That(services, Has.Count.GreaterThan(0));
        var angularSvc = services.First();

        var dotNetEndpoint = new EndpointNode(
            "cs-tournament:endpoint:GET:/api/v1/games",
            "GET /api/v1/games",
            "GamesController.cs",
            "GET",
            "/api/v1/games"
        );

        var layer5 = new Layer5AnalysisParser();
        var isMatchMethod = typeof(Layer5AnalysisParser).GetMethod("IsMatch",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            [typeof(ExternalServiceNode), typeof(EndpointNode)])!;

        var isMatch = (bool)isMatchMethod.Invoke(layer5, [angularSvc, dotNetEndpoint])!;
        Assert.That(isMatch, Is.True, "Angular HttpClient external service should late-bind to .NET backend endpoint");
    }

    [Test]
    public async Task CrossLanguage_LidomaAdmin_Matches_DotNetMicroservices()
    {
        RouteDictionaryRegistry.Clear();

        // 1. Prod environment with top-level constants and concatenation
        var envProdCode = """
        const apiRoot = 'https://api.HOSTNAME/api/v1';
        const identityRoot = 'https://api.HOSTNAME/identity';

        export const environment = {
          production: true,
          allowedUrls: [ apiRoot, identityRoot ],
          players: apiRoot + '/profiles',
          tournament: apiRoot + '/tournaments',
          games: apiRoot + '/games',
          genres: apiRoot + '/genres',
          users: identityRoot + '/api/v1/users',
          identity: identityRoot,
          baseUrl: 'https://HOSTNAME'
        };
        """;
        RouteDictionaryRegistry.ScanAndRegister(envProdCode);

        // 2. urlConstructor getters
        var urlConstructor = """
        import { environment as env } from './environment';
        export const getGamesListUrl = () => env.games;
        export const getPlayerUrl = () => env.players;
        export const getUsersFromIdentityUrl = () => env.users;
        export const getIdentityUrl = () => env.identity;
        """;
        RouteDictionaryRegistry.ScanAndRegister(urlConstructor);

        // 3. AccountsComponent (Identity)
        var accountsCode = """
        import { Component } from '@angular/core';
        import { HttpClient } from '@angular/common/http';
        import { getUsersFromIdentityUrl } from './urlConstructor';

        @Component({ selector: 'app-accounts', template: '' })
        export class AccountsComponent {
            constructor(protected httpClient: HttpClient) {}
            getUsers() {
                this.httpClient.get<any>(getUsersFromIdentityUrl()).subscribe();
            }
        }
        """;
        var identityServices = await ParseAndGetExternalServicesAsync(new TypeScriptParser(), accountsCode, "accounts.component.ts");
        Assert.That(identityServices, Has.Count.EqualTo(1));
        var identitySvc = identityServices.First();

        // 4. PlayersComponent (Player)
        var playersCode = """
        import { Component } from '@angular/core';
        import { HttpClient } from '@angular/common/http';
        import { getPlayerUrl } from './urlConstructor';

        @Component({ selector: 'app-players', template: '' })
        export class PlayersComponent {
            constructor(protected httpClient: HttpClient) {}
            addPlayer() {
                this.httpClient.post<any>(getPlayerUrl(), {}).subscribe();
            }
        }
        """;
        var playerServices = await ParseAndGetExternalServicesAsync(new TypeScriptParser(), playersCode, "players.component.ts");
        Assert.That(playerServices, Has.Count.EqualTo(1));
        var playerSvc = playerServices.First();

        // 5. GamesComponent (Tournament)
        var gamesCode = """
        import { Component } from '@angular/core';
        import { HttpClient } from '@angular/common/http';
        import { getGamesListUrl } from './urlConstructor';

        @Component({ selector: 'app-games', template: '' })
        export class GamesComponent {
            constructor(private httpClient: HttpClient) {}
            async getGames(): Promise<any> {
                const response = await this.httpClient.get<any>(getGamesListUrl());
                return response;
            }
        }
        """;
        var gameServices = await ParseAndGetExternalServicesAsync(new TypeScriptParser(), gamesCode, "games.component.ts");
        Assert.That(gameServices, Has.Count.EqualTo(1));
        var gameSvc = gameServices.First();

        // Verify Late-Binding to real .NET controllers (which have action names in route)
        var layer5 = new Layer5AnalysisParser();
        var isMatchMethod = typeof(Layer5AnalysisParser).GetMethod("IsMatch",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            [typeof(ExternalServiceNode), typeof(EndpointNode)])!;

        var identityEndpoint = new EndpointNode("id:ep1", "GET:/api/v1/Users/GetUser", "UsersController.cs", "GET", "/api/v1/Users/GetUser");
        var playerEndpoint = new EndpointNode("id:ep2", "GET:/api/v1/Profiles/GetAllProfiles", "ProfilesController.cs", "GET", "/api/v1/Profiles/GetAllProfiles");
        var tournamentEndpoint = new EndpointNode("id:ep3", "GET:/api/v1/Games/GetGames", "GamesController.cs", "GET", "/api/v1/Games/GetGames");

        Assert.That((bool)isMatchMethod.Invoke(layer5, [identitySvc, identityEndpoint])!, Is.True, "Admin AccountsComponent should match Lidoma.Services.Identity");
        Assert.That((bool)isMatchMethod.Invoke(layer5, [playerSvc, playerEndpoint])!, Is.True, "Admin PlayersComponent should match Lidoma.Services.Player");
        Assert.That((bool)isMatchMethod.Invoke(layer5, [gameSvc, tournamentEndpoint])!, Is.True, "Admin GamesComponent should match Lidoma.Services.Tournament");
    }
}
