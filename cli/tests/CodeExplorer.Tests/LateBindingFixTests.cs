using System.Reflection;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Layers;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class LateBindingFixTests
{
    private Layer5AnalysisParser _parser = null!;
    private MethodInfo _matchPathsMethod = null!;
    private MethodInfo _isMatchEndpointMethod = null!;

    private MethodInfo _isMatchEndpointWithProjectMethod = null!;

    [SetUp]
    public void SetUp()
    {
        _parser = new Layer5AnalysisParser();
        _matchPathsMethod = typeof(Layer5AnalysisParser).GetMethod("MatchPaths", BindingFlags.NonPublic | BindingFlags.Instance, [typeof(string), typeof(string)])!;
        _isMatchEndpointMethod = typeof(Layer5AnalysisParser).GetMethod("IsMatch", BindingFlags.NonPublic | BindingFlags.Instance, [typeof(ExternalServiceNode), typeof(EndpointNode)])!;
        _isMatchEndpointWithProjectMethod = typeof(Layer5AnalysisParser).GetMethod("IsMatch", BindingFlags.NonPublic | BindingFlags.Instance, [typeof(ExternalServiceNode), typeof(EndpointNode), typeof(CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode)])!;
    }

    private bool InvokeMatchPaths(string pathA, string pathB)
    {
        return (bool)_matchPathsMethod.Invoke(_parser, [pathA, pathB])!;
    }

    private bool InvokeIsMatch(ExternalServiceNode extService, EndpointNode endpoint)
    {
        return (bool)_isMatchEndpointMethod.Invoke(_parser, [extService, endpoint])!;
    }

    private bool InvokeIsMatch(ExternalServiceNode extService, EndpointNode endpoint, CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode? targetProj)
    {
        return (bool)_isMatchEndpointWithProjectMethod.Invoke(_parser, [extService, endpoint, targetProj])!;
    }

    [Test]
    public void MatchPaths_WildcardAgainstParam_DoesNotMatch()
    {
        // Previously "*" matched "/:source_id" because :source_id was turned to *
        Assert.That(InvokeMatchPaths("*", ":source_id"), Is.False);
        Assert.That(InvokeMatchPaths("anything", ":source_id"), Is.False);
        Assert.That(InvokeMatchPaths("*", "logs"), Is.False);
    }

    [Test]
    public void MatchPaths_ConcreteMatchingSegments_Matches()
    {
        Assert.That(InvokeMatchPaths("/api/users/:id", "/api/users/123"), Is.True);
        Assert.That(InvokeMatchPaths("/api/users/123", "/api/users/:id"), Is.True);
        Assert.That(InvokeMatchPaths("/api/users/:id", "/api/orders/:id"), Is.False);
    }

    [Test]
    public void IsMatch_WildcardOrGarbageExternalService_DoesNotMatchParameterizedRoute()
    {
        var extService = new ExternalServiceNode(
            "5:externalservice:http:*",
            "*",
            "http",
            "*",
            "/",
            null);

        var endpoint = new EndpointNode(
            "5:endpoint:PUT:/:source_id",
            "PUT /:source_id",
            "controllers/source.controller.ts",
            "PUT",
            "/:source_id");

        Assert.That(InvokeIsMatch(extService, endpoint), Is.False);
    }

    [Test]
    public void IsMatch_UnknownService_DoesNotMatchRandomEndpoints()
    {
        var extService = new ExternalServiceNode(
            "5:externalservice:http:unknown-service",
            "unknown-service",
            "http",
            "unknown-service",
            "/",
            null);

        var endpoint = new EndpointNode(
            "5:endpoint:GET:/ping",
            "GET /ping",
            "controllers/ping.controller.ts",
            "GET",
            "/ping");

        Assert.That(InvokeIsMatch(extService, endpoint), Is.False);
    }

    [Test]
    public void IsMatch_ValidServiceCall_MatchesEndpoint()
    {
        var extService = new ExternalServiceNode(
            "5:externalservice:http:orders-service",
            "orders-service",
            "http",
            "orders-service",
            "/api/orders/charge",
            null);

        var endpoint = new EndpointNode(
            "5:endpoint:POST:orders/charge",
            "POST orders/charge",
            "orders.controller.ts",
            "POST",
            "orders/charge");

        Assert.That(InvokeIsMatch(extService, endpoint), Is.True);
    }

    [Test]
    public void MatchPaths_TrailingSuffixWithParameterVariance_Matches()
    {
        Assert.That(InvokeMatchPaths("/api/bundles/:bundleId/single-stages", "/:bundle_id/single-stages"), Is.True);
        Assert.That(InvokeMatchPaths("/api/v1/timetables/negative-profit-history", "/timetables/negative-profit-history"), Is.True);
    }

    [Test]
    public void IsMatch_TrailingSuffixWithParameterVariance_Matches()
    {
        var extService = new ExternalServiceNode(
            "5:externalservice:http:bundles",
            "bundles",
            "http",
            "bundles",
            "/api/bundles/:bundleId/single-stages",
            null);

        var endpoint = new EndpointNode(
            "5:endpoint:GET:/:bundle_id/single-stages",
            "GET /:bundle_id/single-stages",
            "routes/bundles.route.ts",
            "GET",
            "/:bundle_id/single-stages");

        Assert.That(InvokeIsMatch(extService, endpoint), Is.True);
    }

    [Test]
    public void RouteDictionaryRegistry_ScansAndResolvesServiceAndPath()
    {
        var code = """
        export const API_ROUTES = {
          BUNDLES: [
            { route: 'SINGLE_STAGE', path: '/api/bundles/:bundleId/single-stages' },
            { route: 'DETAILS', path: '/api/bundles/:bundleId/details' }
          ],
          DOMAINS_V2: [
            { route: 'BUY_DOMAIN', path: '/api/v1/domains/buy-domain' }
          ]
        } as const;

        export const SERVICE_DOMAINS = {
          BUNDLES: 'bundles',
          DOMAINS_V2: 'domain-v2'
        };
        """;

        CodeExplorer.Core.Parser.RouteDictionaryRegistry.ScanAndRegister(code);

        Assert.That(CodeExplorer.Core.Parser.RouteDictionaryRegistry.TryResolve("SINGLE_STAGE", out var path, out var service), Is.True);
        Assert.That(path, Is.EqualTo("/api/bundles/:bundleId/single-stages"));
        Assert.That(service, Is.EqualTo("bundles"));

        Assert.That(CodeExplorer.Core.Parser.RouteDictionaryRegistry.TryResolve("BUY_DOMAIN", out var buyPath, out var buyService), Is.True);
        Assert.That(buyPath, Is.EqualTo("/api/v1/domains/buy-domain"));
        Assert.That(buyService, Is.EqualTo("domain-v2"));
    }

    [Test]
    public void IsMatch_WithTargetProject_RejectsMismatchedDomain()
    {
        // Front-end calls BFF for /bundles
        var bffCall = new ExternalServiceNode(
            "ws:es:http:environment.bff",
            "environment.bff",
            "http",
            "environment.bff",
            "/bundles",
            null);

        // cpm-streaming-aggregator exposes /api/v1/bundles
        var cpmEndpoint = new EndpointNode(
            "ws:ep:GET:/api/v1/bundles",
            "GET /api/v1/bundles",
            "cpm-streaming-aggregator/internal/api/api.go",
            "GET",
            "/api/v1/bundles");

        var cpmProject = new CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode(
            "ws:p:cpm-streaming-aggregator:",
            "cpm-streaming-aggregator",
            "cpm-streaming-aggregator",
            "go");

        var bffProject = new CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode(
            "ws:p:bff:",
            "internal-service-bff",
            "bff",
            "go");

        // Should NOT match cpm-streaming-aggregator because domain is 'environment.bff'
        Assert.That(InvokeIsMatch(bffCall, cpmEndpoint, cpmProject), Is.False);

        // Should match bff project
        Assert.That(InvokeIsMatch(bffCall, cpmEndpoint, bffProject), Is.True);
    }

    [Test]
    public void IsMatch_ThirdPartyDomain_NeverMatchesInternalProject()
    {
        var cloudflareCall = new ExternalServiceNode(
            "ws:es:http:api.cloudflare.com",
            "api.cloudflare.com",
            "http",
            "api.cloudflare.com",
            "/client/v4/accounts/*/storage/kv/namespaces",
            null);

        var cfGatewayEndpoint = new EndpointNode(
            "ws:ep:GET:/namespaces",
            "GET /namespaces",
            "cf-gateway/src/modules/namespaces/namespaces.controller.ts",
            "GET",
            "/namespaces");

        var cfGatewayProj = new CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode(
            "ws:p:cf-gateway:",
            "internal-service-cf-gateway",
            "cf-gateway",
            "ts");

        Assert.That(InvokeIsMatch(cloudflareCall, cfGatewayEndpoint, cfGatewayProj), Is.False);
    }

    [Test]
    public void MatchPaths_ArbitraryPathSubresource_DoesNotMatch()
    {
        // /add should NOT match /lander-skins/add
        Assert.That(InvokeMatchPaths("/add", "/lander-skins/add"), Is.False);
        Assert.That(InvokeMatchPaths("/pause", "/:bundle_ids/pause"), Is.False);
        Assert.That(InvokeMatchPaths("/status", "/safebrowsing/status"), Is.False);
    }
}
