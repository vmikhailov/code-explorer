using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Analysis.Layers.Layer4_Semantic.Analyzers;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Canonical;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Cypher.Common;
using CodeExplorer.Parsers.CSharp;
using CodeExplorer.Parsers.Go;
using CodeExplorer.Parsers.Java;
using CodeExplorer.Parsers.Python;
using CodeExplorer.Parsers.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class RouteCanonicalBijectionTests
{
    [TestCase("/users/{id:guid}/orders/{orderId:int}", RouteDialect.AspNet, "/users/{id:uuid}/orders/{orderId:int}")]
    [TestCase("/users/{id}/orders", RouteDialect.AspNet, "/users/{id}/orders")]
    [TestCase("/files/{*slug}", RouteDialect.AspNet, "/files/{*slug:path}")]
    [TestCase("/api/v1/campaigns/{id:bool}/items/{price:float}", RouteDialect.AspNet, "/api/v1/campaigns/{id:bool}/items/{price:float}")]
    [TestCase("/events/{dt:datetime}/{slug:alpha}", RouteDialect.AspNet, "/events/{dt:date}/{slug:string}")]
    [TestCase("/users/:id/orders/:orderId", RouteDialect.ColonParam, "/users/{id}/orders/{orderId}")]
    [TestCase("/users/:id(int)/orders/:orderId(uuid)", RouteDialect.ColonParam, "/users/{id:int}/orders/{orderId:uuid}")]
    [TestCase("/files/*filepath", RouteDialect.ColonParam, "/files/{*filepath:path}")]
    [TestCase("/users/<int:user_id>/orders/<uuid:order_id>", RouteDialect.FlaskAngle, "/users/{user_id:int}/orders/{order_id:uuid}")]
    [TestCase("/users/<user_id>", RouteDialect.FlaskAngle, "/users/{user_id}")]
    [TestCase("/static/<path:subpath>", RouteDialect.FlaskAngle, "/static/{*subpath:path}")]
    [TestCase("/metrics/<float:threshold>/<string:tag>", RouteDialect.FlaskAngle, "/metrics/{threshold:float}/{tag:string}")]
    [TestCase("/users/[id]/orders/[orderId]", RouteDialect.NextBracket, "/users/{id}/orders/{orderId}")]
    [TestCase("/docs/[...slug]", RouteDialect.NextBracket, "/docs/{*slug:path}")]
    [TestCase("/items/[id:int]", RouteDialect.NextBracket, "/items/{id:int}")]
    [TestCase("/users/42/orders", RouteDialect.OutboundClient, "/users/{=42:int}/orders")]
    [TestCase("/users/550e8400-e29b-41d4-a716-446655440000/orders", RouteDialect.OutboundClient, "/users/{=550e8400-e29b-41d4-a716-446655440000:uuid}/orders")]
    [TestCase("/users/${userId}/orders/42", RouteDialect.OutboundClient, "/users/{userId}/orders/{=42:int}")]
    [TestCase("/reports/2026-11-25/active/true/score/98.5", RouteDialect.OutboundClient, "/reports/{=2026-11-25:date}/active/{=true:bool}/score/{=98.5:float}")]
    [TestCase("/users/{id:uuid}/orders/{orderId:int}", RouteDialect.Canonical, "/users/{id:uuid}/orders/{orderId:int}")]
    public void ParseAndFormat_AcrossAllDialects_IsStrictlyBijective(
        string rawDialectRoute,
        RouteDialect dialect,
        string expectedCanonical)
    {
        // 1. Parse raw dialect route -> Canonical
        var parsed = RouteCanonicalParser.Parse(rawDialectRoute, dialect);
        parsed.CanonicalTemplate.Should().Be(expectedCanonical);

        // 2. Format CanonicalRoute struct back to source dialect == rawDialectRoute
        var restoredFromStruct = RouteCanonicalParser.Format(parsed, dialect);
        restoredFromStruct.Should().Be(rawDialectRoute);

        // 3. String-to-string bijection: FromCanonical(ToCanonical(raw, dialect), dialect) == raw
        var canonicalStr = RouteCanonicalParser.ToCanonical(rawDialectRoute, dialect);
        canonicalStr.Should().Be(expectedCanonical);

        var restoredFromString = RouteCanonicalParser.FromCanonical(canonicalStr, dialect);
        restoredFromString.Should().Be(rawDialectRoute);

        // 4. Reverse direction: ToCanonical(FromCanonical(canonical, dialect), dialect) == canonical
        var roundTrippedCanonical = RouteCanonicalParser.ToCanonical(restoredFromString, dialect);
        roundTrippedCanonical.Should().Be(expectedCanonical);
    }

    [Test]
    public void Parse_StaticSegments_NeverMutatesSingularPluralOrDelimiters()
    {
        // /users and /user are distinct routes in real APIs and must NEVER be collapsed
        var pluralRoute = RouteCanonicalParser.Parse("/users/:id/orders", RouteDialect.ColonParam);
        var singularRoute = RouteCanonicalParser.Parse("/user/:id/orders", RouteDialect.ColonParam);

        pluralRoute.CanonicalTemplate.Should().Be("/users/{id}/orders");
        singularRoute.CanonicalTemplate.Should().Be("/user/{id}/orders");

        pluralRoute.CanonicalShape.Should().Be("/users/{}/orders");
        singularRoute.CanonicalShape.Should().Be("/user/{}/orders");
        pluralRoute.CanonicalShape.Should().NotBe(singularRoute.CanonicalShape);

        // Delimiters, casing, and version tokens (v1, min-bid) remain untouched
        var hyphenRoute = RouteCanonicalParser.Parse("/api/v1/user-profiles/:profile_id/min-bid", RouteDialect.ColonParam);
        hyphenRoute.CanonicalTemplate.Should().Be("/api/v1/user-profiles/{profile_id}/min-bid");
        hyphenRoute.CanonicalShape.Should().Be("/api/v1/user-profiles/{}/min-bid");
        RouteCanonicalParser.FromCanonical(hyphenRoute.CanonicalTemplate, RouteDialect.ColonParam)
            .Should().Be("/api/v1/user-profiles/:profile_id/min-bid");
    }

    [Test]
    public void Parse_CrossDialectSameRoute_ProducesIdenticalCanonicalRouteAndShape()
    {
        var aspNet = RouteCanonicalParser.Parse("/users/{id:int}/orders", RouteDialect.AspNet);
        var express = RouteCanonicalParser.Parse("/users/:id(int)/orders", RouteDialect.ColonParam);
        var flask = RouteCanonicalParser.Parse("/users/<int:id>/orders", RouteDialect.FlaskAngle);
        var nextJs = RouteCanonicalParser.Parse("/users/[id:int]/orders", RouteDialect.NextBracket);
        var clientCall = RouteCanonicalParser.Parse("/users/42/orders", RouteDialect.OutboundClient);

        aspNet.CanonicalTemplate.Should().Be("/users/{id:int}/orders");
        express.CanonicalTemplate.Should().Be("/users/{id:int}/orders");
        flask.CanonicalTemplate.Should().Be("/users/{id:int}/orders");
        nextJs.CanonicalTemplate.Should().Be("/users/{id:int}/orders");
        clientCall.CanonicalTemplate.Should().Be("/users/{=42:int}/orders");

        // All 5 share the exact same structural shape and typed shape!
        aspNet.CanonicalShape.Should().Be("/users/{}/orders");
        express.CanonicalShape.Should().Be("/users/{}/orders");
        flask.CanonicalShape.Should().Be("/users/{}/orders");
        nextJs.CanonicalShape.Should().Be("/users/{}/orders");
        clientCall.CanonicalShape.Should().Be("/users/{}/orders");

        aspNet.TypedShape.Should().Be("/users/{:int}/orders");
        express.TypedShape.Should().Be("/users/{:int}/orders");
        flask.TypedShape.Should().Be("/users/{:int}/orders");
        nextJs.TypedShape.Should().Be("/users/{:int}/orders");
        clientCall.TypedShape.Should().Be("/users/{:int}/orders");
    }

    [Test]
    public void Parse_WithHandlerParameterTypes_EnrichesUntypedRouteParameters()
    {
        var handlerTypes = new Dictionary<string, RouteParamType>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = RouteCanonicalParser.MapLanguageTypeToRouteParamType("System.Guid"),
            ["orderId"] = RouteCanonicalParser.MapLanguageTypeToRouteParamType("int64")
        };

        var parsed = RouteCanonicalParser.Parse("/users/:id/orders/:orderId", RouteDialect.ColonParam, handlerTypes);
        parsed.CanonicalTemplate.Should().Be("/users/{id:uuid}/orders/{orderId:int}");
        parsed.TypedShape.Should().Be("/users/{:uuid}/orders/{:int}");
    }

    [Test]
    public void TypeCompatibility_DisambiguatesTypedEndpointOverloads()
    {
        var epInt = new EndpointMatchCandidate("ep_int", "/users/{id:int}", "GET", "p1");
        var epUuid = new EndpointMatchCandidate("ep_uuid", "/users/{id:guid}", "GET", "p1");

        // Outbound call with integer literal /users/42 matches {id:int} and rejects {id:guid}
        var scoreIntCallAgainstIntEp = EndpointScoringEngine.ScoreEndpointMatch("/users/42", "GET", epInt);
        var scoreIntCallAgainstUuidEp = EndpointScoringEngine.ScoreEndpointMatch("/users/42", "GET", epUuid);

        scoreIntCallAgainstIntEp.Should().BeGreaterThan(75.0);
        scoreIntCallAgainstUuidEp.Should().Be(0.0);

        // Outbound call with UUID literal matches {id:guid} and rejects {id:int}
        const string uuidCall = "/users/550e8400-e29b-41d4-a716-446655440000";
        var scoreUuidCallAgainstUuidEp = EndpointScoringEngine.ScoreEndpointMatch(uuidCall, "GET", epUuid);
        var scoreUuidCallAgainstIntEp = EndpointScoringEngine.ScoreEndpointMatch(uuidCall, "GET", epInt);

        scoreUuidCallAgainstUuidEp.Should().BeGreaterThan(75.0);
        scoreUuidCallAgainstIntEp.Should().Be(0.0);

        // Layer 5 IsMatch also disambiguates typed endpoints
        var l5 = new Layer5AnalysisParser();
        var extInt = new ExternalServiceNode("es_int", "user-service", "http", "user-service", "/users/42")
        {
            CanonicalRoute = "/users/{=42:int}",
            CanonicalShape = "/users/{}"
        };
        var nodeEpInt = new EndpointNode("ep_int", "GET:/users/{id:int}", "UserController.cs", "GET", "/users/{id:int}")
        {
            CanonicalRoute = "/users/{id:int}",
            CanonicalShape = "/users/{}"
        };
        var nodeEpUuid = new EndpointNode("ep_uuid", "GET:/users/{id:guid}", "UserController.cs", "GET", "/users/{id:guid}")
        {
            CanonicalRoute = "/users/{id:uuid}",
            CanonicalShape = "/users/{}"
        };

        l5.IsMatch(extInt, nodeEpInt, null).Should().BeTrue();
        l5.IsMatch(extInt, nodeEpUuid, null).Should().BeFalse();
    }

    private static readonly Regex LegacyRouteParamRegex = new(@"^(:[a-zA-Z0-9_]+|\{[a-zA-Z0-9_]+\})$", RegexOptions.Compiled);
    private static readonly Regex LegacyDelimiterRegex = new(@"[-_.]", RegexOptions.Compiled);

    private static double LegacyScoreEndpointMatch(string route, string? httpMethod, EndpointMatchCandidate endpoint)
    {
        var cleanCallRoute = route.Trim().Split('?')[0].Trim('/');
        var cleanEpRoute = endpoint.RouteTemplate.Trim().Split('?')[0].Trim('/');

        var callSegs = cleanCallRoute.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var epSegs = cleanEpRoute.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (callSegs.Length == 0 || epSegs.Length == 0) return 0.0;

        int matchCount = 0;
        int minLen = Math.Min(callSegs.Length, epSegs.Length);
        for (int i = 0; i < minLen; i++)
        {
            var cSeg = callSegs[i];
            var eSeg = epSegs[i];
            if (LegacyRouteParamRegex.IsMatch(eSeg) || LegacyRouteParamRegex.IsMatch(cSeg))
            {
                matchCount++;
            }
            else if (string.Equals(cSeg, eSeg, StringComparison.OrdinalIgnoreCase) ||
                     LegacyDelimiterRegex.Replace(cSeg.ToLowerInvariant(), "") == LegacyDelimiterRegex.Replace(eSeg.ToLowerInvariant(), ""))
            {
                matchCount++;
            }
        }

        var jaccard = (double)matchCount / (callSegs.Length + epSegs.Length - matchCount);
        var score = jaccard * 60.0;
        if (cleanCallRoute.Equals(cleanEpRoute, StringComparison.OrdinalIgnoreCase)) score = 70.0;
        if (!string.IsNullOrEmpty(httpMethod) && !string.IsNullOrEmpty(endpoint.HttpMethod))
        {
            score += string.Equals(httpMethod, endpoint.HttpMethod, StringComparison.OrdinalIgnoreCase) ? 20.0 : -20.0;
        }
        return Math.Max(0.0, score);
    }

    [Test]
    public void PerformanceBenchmark_CanonicalParserAndHashJoin_Vs_LegacyRegexAndLinearScan()
    {
        const int endpointCount = 2000;
        const int callCount = 1000;

        var scoringEndpoints = new List<EndpointMatchCandidate>(endpointCount);
        var l5 = new Layer5AnalysisParser();
        var prepEndpoints = new List<Layer5AnalysisParser.PreparedEndpoint>(endpointCount);
        var endpointsByShape = new Dictionary<string, List<Layer5AnalysisParser.PreparedEndpoint>>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < endpointCount; i++)
        {
            var svcIndex = i % 50;
            var proj = new ProjectNode($"p_{svcIndex}", $"service-{svcIndex}", $"services/service-{svcIndex}", "CSharp", "Service", false);
            var routeTemplate = $"/api/v1/resource-{i}/{{id:int}}/items/{{itemId:guid}}";
            scoringEndpoints.Add(new EndpointMatchCandidate($"ep_{i}", routeTemplate, "GET", proj.Id));

            var epNode = new EndpointNode($"ep_{i}", $"GET:{routeTemplate}", $"Controller{i}.cs", "GET", routeTemplate);
            var prepEp = Layer5AnalysisParser.PrepareEndpoint(epNode, proj);
            prepEndpoints.Add(prepEp);
            if (!endpointsByShape.TryGetValue(prepEp.Canonical.CanonicalShape, out var list))
            {
                list = new List<Layer5AnalysisParser.PreparedEndpoint>(2);
                endpointsByShape[prepEp.Canonical.CanonicalShape] = list;
            }
            list.Add(prepEp);
        }

        var callRoutes = new string[callCount];
        var prepCalls = new List<Layer5AnalysisParser.PreparedExternalService>(callCount);
        for (int i = 0; i < callCount; i++)
        {
            var targetIdx = (i * 2) % endpointCount;
            var svcIndex = targetIdx % 50;
            var callPath = $"/api/v1/resource-{targetIdx}/{i + 1}/items/550e8400-e29b-41d4-a716-446655440000";
            callRoutes[i] = callPath;

            var extNode = new ExternalServiceNode($"es_{i}", $"service-{svcIndex}", "http", $"service-{svcIndex}", callPath);
            prepCalls.Add(Layer5AnalysisParser.PrepareExternalService(extNode, null));
        }

        // Warmup JIT
        _ = LegacyScoreEndpointMatch(callRoutes[0], "GET", scoringEndpoints[0]);
        var warmCall = RouteCanonicalParser.Parse(callRoutes[0], RouteDialect.OutboundClient);
        _ = EndpointScoringEngine.ScoreEndpointMatch(warmCall, "GET", scoringEndpoints[0]);
        foreach (var ep in scoringEndpoints) _ = ep.GetParsedCanonical();

        // 1. Benchmark EndpointScoringEngine: 250 calls x 2,000 endpoints = 500,000 evaluations
        const int scoringCalls = 250;
        var swLegacyScoring = Stopwatch.StartNew();
        double legacySum = 0;
        for (int c = 0; c < scoringCalls; c++)
        {
            var r = callRoutes[c];
            for (int e = 0; e < endpointCount; e++)
            {
                legacySum += LegacyScoreEndpointMatch(r, "GET", scoringEndpoints[e]);
            }
        }
        swLegacyScoring.Stop();

        var swCanonicalScoring = Stopwatch.StartNew();
        double canonicalSum = 0;
        for (int c = 0; c < scoringCalls; c++)
        {
            var parsedCall = RouteCanonicalParser.Parse(callRoutes[c], RouteDialect.OutboundClient);
            for (int e = 0; e < endpointCount; e++)
            {
                canonicalSum += EndpointScoringEngine.ScoreEndpointMatch(parsedCall, "GET", scoringEndpoints[e]);
            }
        }
        swCanonicalScoring.Stop();

        // 2. Benchmark Layer 5 Late-Binding: 1,000 calls x 2,000 endpoints = 2,000,000 pairs
        var swLinearL5 = Stopwatch.StartNew();
        int linearMatches = 0;
        foreach (var ext in prepCalls)
        {
            foreach (var ep in prepEndpoints)
            {
                if (string.Equals(ext.ServicePathNorm, ep.RouteNorm, StringComparison.OrdinalIgnoreCase) ||
                    ext.Canonical.CanonicalShape == ep.Canonical.CanonicalShape)
                {
                    if (l5.IsMatch(ext.Service, ep.Endpoint, ep.TargetProj))
                    {
                        linearMatches++;
                    }
                }
            }
        }
        swLinearL5.Stop();

        var swHashJoinL5 = Stopwatch.StartNew();
        int hashMatches = 0;
        foreach (var ext in prepCalls)
        {
            if (ext.HasStaticSegment && endpointsByShape.TryGetValue(ext.Canonical.CanonicalShape, out var candidates))
            {
                foreach (var ep in candidates)
                {
                    if (!ext.HasTypedParameter || !ep.HasTypedParameter ||
                        RouteCanonicalParser.AreRoutesTypeCompatible(ext.Canonical, ep.Canonical))
                    {
                        hashMatches++;
                    }
                }
            }
        }
        swHashJoinL5.Stop();

        var scoringSpeedup = (double)swLegacyScoring.Elapsed.TotalMilliseconds / Math.Max(0.01, swCanonicalScoring.Elapsed.TotalMilliseconds);
        var l5Speedup = (double)swLinearL5.Elapsed.TotalMilliseconds / Math.Max(0.01, swHashJoinL5.Elapsed.TotalMilliseconds);

        TestContext.Out.WriteLine($"[Perf 1: EndpointScoringEngine 500K comparisons] Legacy Regex+Split: {swLegacyScoring.Elapsed.TotalMilliseconds:F2} ms vs Canonical Pre-Parsed: {swCanonicalScoring.Elapsed.TotalMilliseconds:F2} ms ({scoringSpeedup:F2}x faster)");
        TestContext.Out.WriteLine($"[Perf 2: Layer 5 Late-Binding 1000 calls x 2000 endpoints (2M pairs)] Linear Scan: {swLinearL5.Elapsed.TotalMilliseconds:F2} ms ({linearMatches} matches) vs CanonicalShape O(1) Hash-Join: {swHashJoinL5.Elapsed.TotalMilliseconds:F2} ms ({hashMatches} matches) ({l5Speedup:F1}x faster)");

        legacySum.Should().BeGreaterThan(0);
        canonicalSum.Should().BeGreaterThan(0);
        hashMatches.Should().Be(callCount);
        swCanonicalScoring.Elapsed.Should().BeLessThan(swLegacyScoring.Elapsed);
        swHashJoinL5.Elapsed.Should().BeLessThan(swLinearL5.Elapsed);
    }

    private static async Task<List<ExternalServiceNode>> ParseExternalServicesAsync(IFileParser parser, string code, string fileName)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_can_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, fileName);
            await File.WriteAllTextAsync(filePath, code);
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphRepository();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var list = new List<ExternalServiceNode>();
            CollectExternalServices(syntaxTree.FileNode.Children, list);
            return list;
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static void CollectExternalServices(IEnumerable<IOntologyNode> nodes, List<ExternalServiceNode> result)
    {
        foreach (var n in nodes)
        {
            if (n is ExternalServiceNode es) result.Add(es);
            CollectExternalServices(n.Children, result);
        }
    }

    [Test]
    public async Task Phase2_MultiLanguageOutboundAst_PreservesVariableNamesAndTypesInCanonicalRoute()
    {
        RouteDictionaryRegistry.Clear();

        // 1. Python f-string with typed parameter (bundle_id: UUID)
        var pyServices = await ParseExternalServicesAsync(
            new PythonFileParser(),
            """
            import requests
            from uuid import UUID

            def get_bundle(bundle_id: UUID):
                url = f"http://bundle-service/api/v1/bundles/{bundle_id}"
                return requests.get(url)
            """,
            "client.py");
        pyServices.Should().NotBeEmpty();
        pyServices[0].Path.Should().Be("/api/v1/bundles/*");
        pyServices[0].CanonicalRoute.Should().Be("/api/v1/bundles/{bundle_id:uuid}");
        pyServices[0].CanonicalShape.Should().Be("/api/v1/bundles/{}");

        // 2. Go fmt.Sprintf with typed parameter (bundleId string & orderId int64)
        var goServices = await ParseExternalServicesAsync(
            new GoFileParser(),
            """
            package client
            import (
                "fmt"
                "net/http"
            )
            func GetOrder(bundleId string, orderId int64) (*http.Response, error) {
                url := fmt.Sprintf("%s/api/v1/bundles/%s/orders/%d", "http://bundle-service", bundleId, orderId)
                return http.Get(url)
            }
            """,
            "client.go");
        goServices.Should().NotBeEmpty();
        goServices[0].Path.Should().Be("/api/v1/bundles/*/orders/*");
        goServices[0].CanonicalRoute.Should().Be("/api/v1/bundles/{bundleId:string}/orders/{orderId:int}");
        goServices[0].CanonicalShape.Should().Be("/api/v1/bundles/{}/orders/{}");

        // 3. C# Interpolated string with Guid parameter
        var csServices = await ParseExternalServicesAsync(
            new CSharpFileParser(),
            """
            using System;
            using System.Net.Http;
            using System.Threading.Tasks;

            public class BundleClient
            {
                private readonly IHttpClientFactory _factory;
                public BundleClient(IHttpClientFactory factory) => _factory = factory;

                public async Task<string> GetBundleAsync(Guid bundleId)
                {
                    var client = _factory.CreateClient("bundle-service");
                    var response = await client.GetAsync($"/api/v1/bundles/{bundleId}");
                    return await response.Content.ReadAsStringAsync();
                }
            }
            """,
            "BundleClient.cs");
        csServices.Should().NotBeEmpty();
        csServices[0].Path.Should().Be("/api/v1/bundles/*");
        csServices[0].CanonicalRoute.Should().Be("/api/v1/bundles/{bundleId:uuid}");
        csServices[0].CanonicalShape.Should().Be("/api/v1/bundles/{}");

        // 4. Java RestTemplate binary concatenation with String parameter
        var javaServices = await ParseExternalServicesAsync(
            new JavaFileParser(),
            """
            package com.example.client;
            import org.springframework.web.client.RestTemplate;

            public class OrderClient {
                private RestTemplate restTemplate;

                public String getOrder(String orderId) {
                    String url = "http://order-service/api/v1/orders/" + orderId;
                    return restTemplate.getForObject(url, String.class);
                }
            }
            """,
            "OrderClient.java");
        javaServices.Should().NotBeEmpty();
        javaServices[0].Path.Should().Be("/api/v1/orders/*");
        javaServices[0].CanonicalRoute.Should().Be("/api/v1/orders/{orderId:string}");
        javaServices[0].CanonicalShape.Should().Be("/api/v1/orders/{}");

        // 5. TypeScript template string with number parameter
        var tsServices = await ParseExternalServicesAsync(
            new TypeScriptFileParser(),
            """
            import axios from 'axios';

            export async function fetchUserOrders(userId: number) {
                return axios.get(`http://user-service/users/${userId}/orders`);
            }
            """,
            "client.ts");
        tsServices.Should().NotBeEmpty();
        tsServices[0].Path.Should().Be("/users/*/orders");
        tsServices[0].CanonicalRoute.Should().Be("/users/{userId:int}/orders");
        tsServices[0].CanonicalShape.Should().Be("/users/{}/orders");
    }

    [TestCase("orders.created", TopicDialect.Plain, "topic:orders.created")]
    [TestCase("projects/acme-prod/topics/orders.created", TopicDialect.GcpPubSubTopic, "topic://gcp/acme-prod/topics/orders.created")]
    [TestCase("projects/acme-prod/subscriptions/orders-worker", TopicDialect.GcpPubSubSubscription, "topic://gcp/acme-prod/subscriptions/orders-worker")]
    [TestCase("arn:aws:sns:us-east-1:123456789012:orders-created", TopicDialect.AwsArn, "topic://aws/sns/us-east-1/123456789012/orders-created")]
    [TestCase("topic://gcp/{projectId:string}/topics/orders.created", TopicDialect.Canonical, "topic://gcp/{projectId:string}/topics/orders.created")]
    public void Phase4_TopicCanonicalParser_AcrossAllDialects_IsStrictlyBijective(
        string rawTopic,
        TopicDialect dialect,
        string expectedCanonical)
    {
        var canonical = TopicCanonicalParser.ToCanonical(rawTopic, dialect);
        canonical.Should().Be(expectedCanonical);

        var restored = TopicCanonicalParser.FromCanonical(canonical, dialect);
        restored.Should().Be(rawTopic);

        var reCanonical = TopicCanonicalParser.ToCanonical(restored, dialect);
        reCanonical.Should().Be(canonical);
    }

    [TestCase("[dbo].[UserOrders]", TableDialect.TSqlBracket, "table:dbo.UserOrders")]
    [TestCase("[UserOrders]", TableDialect.TSqlBracket, "table:UserOrders")]
    [TestCase("\"public\".\"user_orders\"", TableDialect.AnsiDoubleQuote, "table:public.user_orders")]
    [TestCase("`shop_db`.`user_orders`", TableDialect.MySqlBacktick, "table:shop_db.user_orders")]
    [TestCase("public.user_orders", TableDialect.Plain, "table:public.user_orders")]
    [TestCase("table:dbo.UserOrders", TableDialect.Canonical, "table:dbo.UserOrders")]
    public void Phase4_TableCanonicalParser_AcrossAllDialects_IsStrictlyBijective(
        string rawTable,
        TableDialect dialect,
        string expectedCanonical)
    {
        var canonical = TableCanonicalParser.ToCanonical(rawTable, dialect);
        canonical.Should().Be(expectedCanonical);

        var restored = TableCanonicalParser.FromCanonical(canonical, dialect);
        restored.Should().Be(rawTable);

        var reCanonical = TableCanonicalParser.ToCanonical(restored, dialect);
        reCanonical.Should().Be(canonical);
    }

    [TestCase("postgresql://app_user@db.internal:5432/orders_db", DsnDialect.Uri, "dsn://postgresql/app_user@db.internal:5432/orders_db")]
    [TestCase("jdbc:postgresql://db.internal:5432/orders_db", DsnDialect.Jdbc, "dsn://postgresql/db.internal:5432/orders_db")]
    [TestCase("Host=db.internal;Port=5432;Database=orders_db;Username=app_user", DsnDialect.AdoNet, "dsn://postgresql/app_user@db.internal:5432/orders_db")]
    public void Phase4_DsnCanonicalParser_AcrossAllDialects_IsStrictlyBijective(
        string rawDsn,
        DsnDialect dialect,
        string expectedCanonical)
    {
        var canonical = DsnCanonicalParser.ToCanonical(rawDsn, dialect);
        canonical.Should().Be(expectedCanonical);

        var restored = DsnCanonicalParser.FromCanonical(canonical, dialect);
        restored.Should().Be(rawDsn);

        var reCanonical = DsnCanonicalParser.ToCanonical(restored, dialect);
        reCanonical.Should().Be(canonical);
    }

    [TestCase("/orders.v1.OrderService/CreateOrder", RpcDialect.GrpcPath, "rpc://orders.v1/OrderService/CreateOrder")]
    [TestCase("orders.v1.OrderService.CreateOrder", RpcDialect.DotQualified, "rpc://orders.v1/OrderService/CreateOrder")]
    [TestCase("orders.v1.OrderService:CreateOrder", RpcDialect.ColonQualified, "rpc://orders.v1/OrderService/CreateOrder")]
    [TestCase("rpc://orders.v1/OrderService/CreateOrder", RpcDialect.Canonical, "rpc://orders.v1/OrderService/CreateOrder")]
    public void Phase4_RpcCanonicalParser_AcrossAllDialects_IsStrictlyBijective(
        string rawRpc,
        RpcDialect dialect,
        string expectedCanonical)
    {
        var canonical = RpcCanonicalParser.ToCanonical(rawRpc, dialect);
        canonical.Should().Be(expectedCanonical);

        var restored = RpcCanonicalParser.FromCanonical(canonical, dialect);
        restored.Should().Be(rawRpc);

        var reCanonical = RpcCanonicalParser.ToCanonical(restored, dialect);
        reCanonical.Should().Be(canonical);
    }

    [Test]
    public async Task Phase5_CrossServiceEndpointSlotDataflow_EmitsParameterToParameterFlowsToEdge()
    {
        var callerFn = new FunctionNode("fn:caller", "GetBundleAsync", "GetBundleAsync", "Client.cs", "Client.cs", 10, 20, 1, 1);
        var handlerFn = new FunctionNode("fn:handler", "GetById", "GetById", "BundlesController.cs", "BundlesController.cs", 30, 45, 1, 1);

        var extService = new ExternalServiceNode(
            "ext:1",
            "GET /api/v1/bundles/*",
            "HTTP",
            "bundle-service",
            "/api/v1/bundles/*")
        {
            Url = "http://bundle-service/api/v1/bundles/{bundleId}",
            Method = "GET",
            FilePath = "Client.cs",
            StartLine = 14,
            CallerSymbolId = callerFn.Id,
            CanonicalRoute = "/api/v1/bundles/{bundleId:uuid}",
            CanonicalShape = "/api/v1/bundles/{}"
        };

        var endpoint = new EndpointNode(
            "ep:1",
            "GET /api/v1/bundles/{id:guid}",
            "BundlesController.cs",
            "GET",
            "/api/v1/bundles/{id:guid}",
            "REST")
        {
            FilePath = "BundlesController.cs",
            StartLine = 30,
            CanonicalRoute = "/api/v1/bundles/{id:uuid}",
            CanonicalShape = "/api/v1/bundles/{}"
        };
        endpoint.Children.Add(handlerFn);

        var ws = new WorkspaceNode("ws0", "TestWs", ".");
        var filesStructure = new FilesStructureNode("ws0:files", "FilesStructure", ".");
        var projectsStructure = new ProjectsStructureNode("ws0:projects", "ProjectsStructure", ".");
        var syntaxStructure = new SyntaxStructureNode("ws0:syntax", "SyntaxStructure", ".");
        syntaxStructure.Children.Add(callerFn);
        syntaxStructure.Children.Add(handlerFn);

        var semanticStructure = new SemanticStructureNode("ws0:sem", "SemanticStructure", ".");
        var l1Result = new Layer1Result(ws, filesStructure, [], []);
        var l2Result = new Layer2Result(l1Result, projectsStructure, [], [], []);
        var l3Result = new Layer3Result(l2Result, syntaxStructure, [], [], [], [], [], new Dictionary<(string Kind, string Name), string>());
        var l4Result = new Layer4Result(
            l3Result,
            semanticStructure,
            [extService, endpoint],
            []);

        var channel = Channel.CreateUnbounded<Func<Task>>();
        var client = new InMemoryGraphRepository();
        var ctx = new ParsingContext(".", ".", client, channel);

        var lateBoundRels = new List<Relationship>
        {
            Relationship.FromRelationship(new CallsEndpointRelationship(extService.Id, endpoint.Id))
        };

        await SemanticArgumentPropagator.PropagateAndGroundAsync(
            l4Result,
            ctx,
            [],
            lateBoundRels);

        channel.Writer.Complete();
        while (channel.Reader.TryRead(out var work))
        {
            await work();
        }

        var allRels = client.UploadedRelationships;
        allRels.Should().Contain(r =>
            r.Kind == RelationshipKind.FlowsTo &&
            r.From.Contains("bundleId") &&
            r.To.Contains("id"));
    }
}
