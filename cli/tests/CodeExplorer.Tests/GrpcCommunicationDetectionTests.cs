using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.CSharp;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class GrpcCommunicationDetectionTests
{
    private static void FindAllNodes<T>(IEnumerable<IOntologyNode> nodes, List<T> result) where T : IOntologyNode
    {
        foreach (var node in nodes)
        {
            if (node is T match) result.Add(match);
            FindAllNodes(node.Children, result);
        }
    }

    private static async Task<(SyntaxTree Tree, ParsingContext Context, string TempDir)> ParseCodeAsync(
        IFileParser parser, string code, string fileName, string language)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ce_grpc_test_{language}_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, fileName);
        await File.WriteAllTextAsync(filePath, code);

        var channel = Channel.CreateUnbounded<Func<Task>>();
        var client = new InMemoryGraphClient();
        var ctx = new ParsingContext(tempDir, tempDir, client, channel);

        var syntaxTree = await parser.ParseAsync(filePath, "proj:root", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        return (syntaxTree, ctx, tempDir);
    }

    [Test]
    public async Task Test_CSharp_Grpc_Server_Detects_Endpoint_And_Ignores_Extensions()
    {
        var parser = new CSharpParser();

        // 1. Grpc Server Service
        var serverCode = """
        using System.Threading.Tasks;
        using Grpc.Core;

        namespace eShop.Basket.API.Grpc;

        public class BasketService : Basket.BasketBase
        {
            public override async Task<CustomerBasketResponse> GetBasket(GetBasketRequest request, ServerCallContext context)
            {
                return new CustomerBasketResponse();
            }

            public override async Task<CustomerBasketResponse> UpdateBasket(UpdateBasketRequest request, ServerCallContext context)
            {
                return new CustomerBasketResponse();
            }
        }
        """;

        var (serverTree, serverCtx, serverDir) = await ParseCodeAsync(parser, serverCode, "BasketService.cs", "cs");
        try
        {
            var endpoints = new List<EndpointNode>();
            FindAllNodes(serverTree.FileNode.Children, endpoints);

            endpoints.Should().HaveCount(2);
            endpoints.Should().Contain(e => e.Name == "RPC:/BasketService/GetBasket" && e.RouteTemplate == "/BasketService/GetBasket" && e.Protocol == "gRPC");
            endpoints.Should().Contain(e => e.Name == "RPC:/BasketService/UpdateBasket" && e.RouteTemplate == "/BasketService/UpdateBasket" && e.Protocol == "gRPC");
        }
        finally
        {
            serverTree.Dispose();
            try { Directory.Delete(serverDir, true); } catch { }
        }

        // 2. ServerCallContext extension methods should NOT be detected as RPC endpoints
        var extMethodCode = """
        using Grpc.Core;

        namespace eShop.Basket.API.Extensions;

        internal static class ServerCallContextIdentityExtensions
        {
            public static string? GetUserIdentity(this ServerCallContext context) => "test-user";
            public static string? GetUserName(this ServerCallContext context) => "test-name";
        }
        """;

        var (extTree, extCtx, extDir) = await ParseCodeAsync(parser, extMethodCode, "ServerCallContextIdentityExtensions.cs", "cs");
        try
        {
            var endpoints = new List<EndpointNode>();
            FindAllNodes(extTree.FileNode.Children, endpoints);

            endpoints.Should().BeEmpty("Extension methods on ServerCallContext must not be treated as gRPC service endpoints");
        }
        finally
        {
            extTree.Dispose();
            try { Directory.Delete(extDir, true); } catch { }
        }
    }

    [Test]
    public async Task Test_CSharp_Grpc_Client_Detects_Invocations()
    {
        var parser = new CSharpParser();

        // 1. Direct client injection (WebApp style: basketClient.GetBasketAsync)
        var webAppClientCode = """
        using eShop.Basket.API.Grpc;
        using GrpcBasketClient = eShop.Basket.API.Grpc.Basket.BasketClient;

        namespace eShop.WebApp.Services;

        public class BasketService(GrpcBasketClient basketClient)
        {
            public async Task GetBasketAsync()
            {
                await basketClient.GetBasketAsync(new());
                await basketClient.DeleteBasketAsync(new DeleteBasketRequest());
                await basketClient.UpdateBasketAsync(new UpdateBasketRequest());
            }
        }
        """;

        var (clientTree, clientCtx, clientDir) = await ParseCodeAsync(parser, webAppClientCode, "BasketService.cs", "cs");
        try
        {
            var extServices = new List<ExternalServiceNode>();
            FindAllNodes(clientTree.FileNode.Children, extServices);

            extServices.Should().HaveCountGreaterThan(0);
            extServices.Should().Contain(e => e.Protocol == "grpc" && e.DomainOrService == "basket" && e.Path == "/GetBasket");
            extServices.Should().Contain(e => e.Protocol == "grpc" && e.DomainOrService == "basket" && e.Path == "/DeleteBasket");
            extServices.Should().Contain(e => e.Protocol == "grpc" && e.DomainOrService == "basket" && e.Path == "/UpdateBasket");
        }
        finally
        {
            clientTree.Dispose();
            try { Directory.Delete(clientDir, true); } catch { }
        }

        // 2. Factory method client invocation (ClientApp style: GetBasketClient().GetBasketAsync)
        var clientAppCode = """
        using eShop.ClientApp.BasketGrpcClient;
        using Grpc.Core;
        using Grpc.Net.Client;

        namespace eShop.ClientApp.Services.Basket;

        public class BasketService
        {
            private BasketGrpcClient.Basket.BasketClient _basketClient;

            private BasketGrpcClient.Basket.BasketClient GetBasketClient() => _basketClient;

            public async Task GetBasketAsync()
            {
                await GetBasketClient().GetBasketAsync(new GetBasketRequest());
                await GetBasketClient().UpdateBasketAsync(new UpdateBasketRequest());
                await GetBasketClient().DeleteBasketAsync(new DeleteBasketRequest());
            }
        }
        """;

        var (clientAppTree, clientAppCtx, clientAppDir) = await ParseCodeAsync(parser, clientAppCode, "BasketService.cs", "cs");
        try
        {
            var extServices = new List<ExternalServiceNode>();
            FindAllNodes(clientAppTree.FileNode.Children, extServices);

            extServices.Should().HaveCountGreaterThan(0);
            extServices.Should().Contain(e => e.Protocol == "grpc" && e.DomainOrService == "basket" && e.Path == "/GetBasket");
            extServices.Should().Contain(e => e.Protocol == "grpc" && e.DomainOrService == "basket" && e.Path == "/UpdateBasket");
            extServices.Should().Contain(e => e.Protocol == "grpc" && e.DomainOrService == "basket" && e.Path == "/DeleteBasket");
        }
        finally
        {
            clientAppTree.Dispose();
            try { Directory.Delete(clientAppDir, true); } catch { }
        }
    }

    [Test]
    public async Task Test_CSharp_Grpc_LateBinding_Resolves_CallsEndpoint_And_ServiceCall()
    {
        var parser = new CSharpParser();

        // 1. Parse Server Service in Basket.API
        var serverCode = """
        using System.Threading.Tasks;
        using Grpc.Core;

        namespace eShop.Basket.API.Grpc;

        public class BasketService : Basket.BasketBase
        {
            public override async Task<CustomerBasketResponse> GetBasket(GetBasketRequest request, ServerCallContext context)
            {
                return new CustomerBasketResponse();
            }
        }
        """;

        // 2. Parse Client in WebApp
        var clientCode = """
        using eShop.Basket.API.Grpc;
        using GrpcBasketClient = eShop.Basket.API.Grpc.Basket.BasketClient;

        namespace eShop.WebApp.Services;

        public class BasketService(GrpcBasketClient basketClient)
        {
            public async Task<CustomerBasketResponse> GetBasketAsync()
            {
                return await basketClient.GetBasketAsync(new());
            }
        }
        """;

        var (serverTree, serverCtx, serverDir) = await ParseCodeAsync(parser, serverCode, "BasketService.cs", "cs");
        var (clientTree, clientCtx, clientDir) = await ParseCodeAsync(parser, clientCode, "BasketService.cs", "cs");
        try
        {
            var endpoints = new List<EndpointNode>();
            FindAllNodes(serverTree.FileNode.Children, endpoints);
            var ep = endpoints.First(e => e.RouteTemplate == "/BasketService/GetBasket");

            var extServices = new List<ExternalServiceNode>();
            FindAllNodes(clientTree.FileNode.Children, extServices);
            var es = extServices.First(e => e.Path == "/GetBasket");

            // Projects
            var serverProj = new ProjectNode("proj:Basket.API", "Basket.API", "/src/Basket.API", "csharp");
            var clientProj = new ProjectNode("proj:WebApp", "WebApp", "/src/WebApp", "csharp");

            var prepEp = Layer5AnalysisParser.PrepareEndpoint(ep, serverProj);
            var prepExt = Layer5AnalysisParser.PrepareExternalService(es, clientProj);

            // Match test
            var isMatch = prepExt.Segments[^1].Equals(prepEp.Segments[^1], StringComparison.OrdinalIgnoreCase);
            isMatch.Should().BeTrue();

            var layer5 = new Layer5AnalysisParser();
            var isMatched = layer5.IsMatch(es, ep, serverProj);
            isMatched.Should().BeTrue("grpc://basket/GetBasket must match RPC:/BasketService/GetBasket in Basket.API");
        }
        finally
        {
            serverTree.Dispose();
            clientTree.Dispose();
            try { Directory.Delete(serverDir, true); } catch { }
            try { Directory.Delete(clientDir, true); } catch { }
        }
    }

    [Test]
    public async Task Test_CSharpParser_Classifies_Maui_And_Conditional_OutputType()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_maui_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var csprojContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0-android;net10.0-ios;net10.0</TargetFrameworks>
                <OutputType Condition="'$(TargetFramework)' != 'net10.0'">Exe</OutputType>
                <UseMaui>true</UseMaui>
                <SingleProject>true</SingleProject>
              </PropertyGroup>
            </Project>
            """;
            var csprojPath = Path.Combine(tempDir, "ClientApp.csproj");
            await File.WriteAllTextAsync(csprojPath, csprojContent);

            var parser = new CSharpParser();
            var props = parser.ExtractManifestProperties(tempDir, [csprojPath]);

            props["manifest_type"].Should().Be("application");
            props["role"].Should().Be("FrontendApp");
            props["kind"].Should().Be("App");
            props["is_library"].Should().Be("false");
            props["output_type"].Should().Be("Exe");

            var classification = ProjectEntityClassifierRegistry.Classify(
                tempDir,
                ["ClientApp.csproj"],
                "src/ClientApp",
                "ClientApp",
                "csharp",
                null,
                props);

            classification.Kind.Should().Be(ProjectEntityKind.App);

            var (role, isLib, _) = ProjectRoleDetector.Detect(
                tempDir,
                ["ClientApp.csproj"],
                "src/ClientApp",
                "ClientApp",
                "csharp",
                null,
                props);

            role.Should().Be(ProjectRole.FrontendApp);
            isLib.Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
