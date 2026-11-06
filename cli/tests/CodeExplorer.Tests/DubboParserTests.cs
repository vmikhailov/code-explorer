using System.Threading.Channels;
using CodeExplorer.Core.Analysis.Layers.Layer4_Semantic.Analyzers;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.Java;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class DubboParserTests
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
        IFileParser parser, string code, string fileName)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_dubbo_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, fileName);
        await File.WriteAllTextAsync(filePath, code);

        var channel = Channel.CreateUnbounded<Func<Task>>();
        var client = new InMemoryGraphRepository();
        var ctx = new ParsingContext(tempDir, tempDir, client, channel);

        var syntaxTree = await parser.ParseAsync(filePath, "proj:root", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        return (syntaxTree, ctx, tempDir);
    }

    [Test]
    public async Task Test_Dubbo_Provider_Generates_Rpc_Endpoints()
    {
        var parser = new JavaParser();

        var serverCode = """
        package com.example.dubbo;

        import org.apache.dubbo.config.annotation.DubboService;

        @DubboService
        public class DemoServiceImpl implements DemoService {
            @Override
            public String sayHello(String name) {
                return "Hello " + name;
            }

            @Override
            public String sayGoodbye(String name) {
                return "Bye " + name;
            }
        }
        """;

        var (tree, ctx, dir) = await ParseCodeAsync(parser, serverCode, "DemoServiceImpl.java");
        try
        {
            EndpointSemanticAnalyzer.Enrich(tree, new ProjectNode("proj:provider", "provider", dir, "java"), ctx);

            var endpoints = new List<EndpointNode>();
            FindAllNodes(tree.FileNode.Children, endpoints);

            endpoints.Should().HaveCount(2);
            endpoints.Should().Contain(e => e.Name == "RPC:/DemoService/sayHello" && e.RouteTemplate == "/DemoService/sayHello" && e.Protocol == "dubbo");
            endpoints.Should().Contain(e => e.Name == "RPC:/DemoService/sayGoodbye" && e.RouteTemplate == "/DemoService/sayGoodbye" && e.Protocol == "dubbo");
        }
        finally
        {
            tree.Dispose();
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Test]
    public async Task Test_Dubbo_Consumer_Generates_ExternalService_And_Calls()
    {
        var parser = new JavaParser();

        var clientCode = """
        package com.example.dubbo;

        import org.apache.dubbo.config.annotation.DubboReference;
        import org.springframework.stereotype.Component;

        @Component
        public class Task {
            @DubboReference
            private DemoService demoService;

            public void run() {
                var res = demoService.sayHello("world");
            }
        }
        """;

        var (tree, ctx, dir) = await ParseCodeAsync(parser, clientCode, "Task.java");
        try
        {
            EndpointSemanticAnalyzer.Enrich(tree, new ProjectNode("proj:consumer", "consumer", dir, "java"), ctx);

            var extServices = new List<ExternalServiceNode>();
            FindAllNodes(tree.FileNode.Children, extServices);

            extServices.Should().HaveCountGreaterThanOrEqualTo(2);
            extServices.Should().Contain(e => e.Protocol == "dubbo" && e.DomainOrService.Equals("DemoService", StringComparison.OrdinalIgnoreCase) && e.Path == "/DemoService");
            extServices.Should().Contain(e => e.Protocol == "dubbo" && e.DomainOrService.Equals("DemoService", StringComparison.OrdinalIgnoreCase) && e.Path == "/sayHello");
        }
        finally
        {
            tree.Dispose();
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Test]
    public async Task Test_Dubbo_Triple_Protocol_Detection()
    {
        var parser = new JavaParser();

        var tripleServerCode = """
        package com.example.dubbo;

        import org.apache.dubbo.config.annotation.DubboService;

        @DubboService(protocol = "tri")
        public class GreeterImpl extends GreeterImplBase {
            public GreeterReply greet(GreeterRequest request) {
                return null;
            }
        }
        """;

        var (tree, ctx, dir) = await ParseCodeAsync(parser, tripleServerCode, "GreeterImpl.java");
        try
        {
            EndpointSemanticAnalyzer.Enrich(tree, new ProjectNode("proj:provider", "provider", dir, "java"), ctx);

            var endpoints = new List<EndpointNode>();
            FindAllNodes(tree.FileNode.Children, endpoints);

            endpoints.Should().HaveCount(1);
            var ep = endpoints[0];
            ep.Name.Should().Be("RPC:/Greeter/greet");
            ep.RouteTemplate.Should().Be("/Greeter/greet");
            ep.Protocol.Should().Be("tri");
        }
        finally
        {
            tree.Dispose();
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Test]
    public async Task Test_Dubbo_LateBinding_Matches_Consumer_To_Provider()
    {
        var parser = new JavaParser();

        var serverCode = """
        package com.example.dubbo;

        import org.apache.dubbo.config.annotation.DubboService;

        @DubboService
        public class DemoServiceImpl implements DemoService {
            public String sayHello(String name) { return "Hello"; }
        }
        """;

        var clientCode = """
        package com.example.dubbo;

        import org.apache.dubbo.config.annotation.DubboReference;

        public class Task {
            @DubboReference
            private DemoService demoService;

            public void run() {
                demoService.sayHello("world");
            }
        }
        """;

        var (serverTree, serverCtx, serverDir) = await ParseCodeAsync(parser, serverCode, "DemoServiceImpl.java");
        var (clientTree, clientCtx, clientDir) = await ParseCodeAsync(parser, clientCode, "Task.java");
        try
        {
            var serverProj = new ProjectNode("proj:provider", "dubbo-provider", serverDir, "java");
            var clientProj = new ProjectNode("proj:consumer", "dubbo-consumer", clientDir, "java");

            EndpointSemanticAnalyzer.Enrich(serverTree, serverProj, serverCtx);
            EndpointSemanticAnalyzer.Enrich(clientTree, clientProj, clientCtx);

            var endpoints = new List<EndpointNode>();
            FindAllNodes(serverTree.FileNode.Children, endpoints);
            var ep = endpoints.First(e => e.RouteTemplate == "/DemoService/sayHello");

            var extServices = new List<ExternalServiceNode>();
            FindAllNodes(clientTree.FileNode.Children, extServices);
            var esMethod = extServices.First(e => e.Path == "/sayHello");
            var esService = extServices.First(e => e.Path == "/DemoService");

            var layer5 = new Layer5AnalysisParser();
            layer5.IsMatch(esMethod, ep, serverProj).Should().BeTrue("method invocation must match Dubbo RPC endpoint");
            layer5.IsMatch(esService, ep, serverProj).Should().BeTrue("service reference must match Dubbo RPC endpoint");
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
    public async Task Test_Dubbo_Xml_Config_Parser()
    {
        var parser = new DubboXmlParser();

        var xmlContent = """
        <?xml version="1.0" encoding="UTF-8"?>
        <beans xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
               xmlns:dubbo="http://dubbo.apache.org/schema/dubbo"
               xmlns="http://www.springframework.org/schema/beans">
            <dubbo:service interface="org.apache.dubbo.samples.GreetingService" ref="greetingService"/>
            <dubbo:reference id="greetingService" interface="org.apache.dubbo.samples.GreetingService"/>
        </beans>
        """;

        var tempDir = Path.Combine(Path.GetTempPath(), "ce_dubbo_xml_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var xmlPath = Path.Combine(tempDir, "dubbo-config.xml");
        await File.WriteAllTextAsync(xmlPath, xmlContent);

        try
        {
            parser.CanParseFile(xmlPath).Should().BeTrue();

            var tree = await parser.ParseAsync(xmlPath, "parent", "test-ws", tempDir);
            tree.FileNode.Children.Should().HaveCount(2);

            var ep = tree.FileNode.Children.OfType<EndpointNode>().FirstOrDefault();
            ep.Should().NotBeNull();
            ep!.Name.Should().Be("RPC:/GreetingService");
            ep.RouteTemplate.Should().Be("/GreetingService");

            var es = tree.FileNode.Children.OfType<ExternalServiceNode>().FirstOrDefault();
            es.Should().NotBeNull();
            es!.DomainOrService.Should().Be("GreetingService");
            es.Protocol.Should().Be("dubbo");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
