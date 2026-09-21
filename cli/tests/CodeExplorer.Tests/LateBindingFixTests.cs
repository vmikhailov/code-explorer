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

    [SetUp]
    public void SetUp()
    {
        _parser = new Layer5AnalysisParser();
        _matchPathsMethod = typeof(Layer5AnalysisParser).GetMethod("MatchPaths", BindingFlags.NonPublic | BindingFlags.Instance)!;
        _isMatchEndpointMethod = typeof(Layer5AnalysisParser).GetMethod("IsMatch", BindingFlags.NonPublic | BindingFlags.Instance, [typeof(ExternalServiceNode), typeof(EndpointNode)])!;
    }

    private bool InvokeMatchPaths(string pathA, string pathB)
    {
        return (bool)_matchPathsMethod.Invoke(_parser, [pathA, pathB])!;
    }

    private bool InvokeIsMatch(ExternalServiceNode extService, EndpointNode endpoint)
    {
        return (bool)_isMatchEndpointMethod.Invoke(_parser, [extService, endpoint])!;
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
}
