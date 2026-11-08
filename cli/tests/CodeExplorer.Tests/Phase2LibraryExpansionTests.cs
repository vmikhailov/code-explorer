using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.Java;
using CodeExplorer.Parser.Kotlin;
using CodeExplorer.Parser.Python;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[NonParallelizable]
public class Phase2LibraryExpansionTests
{
    private IDisposable? _lock;

    [SetUp]
    public async Task SetUp()
    {
        _lock = await TestRegistryLock.AcquireAsync();
    }

    [TearDown]
    public void TearDown()
    {
        _lock?.Dispose();
        _lock = null;
    }

    private static async Task<(FileNode FileNode, List<Reference> References)> ParseFileAsync(IFileParser parser, string code, string fileName)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "phase2_lib_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, fileName);
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphRepository();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            return (syntaxTree.FileNode, FindAllReferences(syntaxTree.FileNode));
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
                // Ignore temp cleanup error
            }
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
    public async Task TypeScript_Hono_Trpc_Remix_And_GraphQlYoga_Create_Endpoints_And_Calls()
    {
        const string code = """
            import { Hono } from "hono";
            import { initTRPC } from "@trpc/server";
            import { json } from "@remix-run/node";
            import { createYoga } from "graphql-yoga";

            const app = new Hono();
            app.get("/api/health", handleHealth);

            export const appRouter = router({
              getUser: publicProcedure.query(() => null),
            });

            export async function loader() {
              trpc.user.byId.useQuery("1");
              return json({});
            }

            const yoga = createYoga({});
            """;

        var (fileNode, refs) = await ParseFileAsync(new TypeScriptParser(), code, "routes.ts");
        var endpoints = FindNodes<EndpointNode>(fileNode.Children);

        Assert.Multiple(() =>
        {
            Assert.That(endpoints.Any(e => e.Name.Contains("/api/health")), Is.True);
            Assert.That(endpoints.Any(e => e.Name.Contains("/trpc/getUser")), Is.True);
            Assert.That(endpoints.Any(e => e.Name.Contains("/loader")), Is.True);
            Assert.That(endpoints.Any(e => e.Name.Contains("GRAPHQL:/graphql")), Is.True);
            Assert.That(refs.Any(r => r.Kind == RelationshipKind.Calls && r.TargetName == "trpc:user.byId"), Is.True);
        });
    }

    [Test]
    public async Task Python_Litestar_And_StrawberryGraphQl_Create_Endpoints()
    {
        const string code = """
            from litestar import get
            import strawberry

            @get("/books")
            async def list_books() -> list[str]:
                return []

            @strawberry.type
            class Query:
                @strawberry.field
                def current_user(self) -> str:
                    return "alice"
            """;

        var (fileNode, refs) = await ParseFileAsync(new PythonParser(), code, "api.py");
        var endpoints = FindNodes<EndpointNode>(fileNode.Children);

        Assert.Multiple(() =>
        {
            Assert.That(endpoints.Any(e => e.Name.Contains("/books")), Is.True);
            Assert.That(endpoints.Any(e => e.Name.Contains("GRAPHQL:FIELD:current_user")), Is.True);
            Assert.That(refs.Any(r => r.Kind == RelationshipKind.Triggers && r.TargetName == "list_books"), Is.True);
        });
    }

    [Test]
    public async Task Go_GoZero_GqlGen_And_Centrifuge_Emit_Endpoints_And_Messages()
    {
        const string code = """
            package main

            import (
                "github.com/zeromicro/go-zero/zrpc"
                "github.com/99designs/gqlgen/graphql/handler"
                "github.com/centrifugal/centrifuge"
            )

            func boot(node *centrifuge.Node) {
                srv := handler.NewDefaultServer(schema)
                cli := zrpc.MustNewClient(conf)
                node.Publish("chat:general", data)
            }
            """;

        var (fileNode, refs) = await ParseFileAsync(new GoParser(), code, "main.go");
        var endpoints = FindNodes<EndpointNode>(fileNode.Children);

        Assert.Multiple(() =>
        {
            Assert.That(endpoints.Any(e => e.Name.Contains("GRAPHQL:/graphql")), Is.True);
            Assert.That(refs.Any(r => r.Kind == RelationshipKind.Calls && r.TargetName == "zrpc:client"), Is.True);
            Assert.That(refs.Any(r => r.Kind == RelationshipKind.PublishesTo && r.TargetName == "ws:chat:general"), Is.True);
        });
    }

    [Test]
    public async Task CSharp_AzureFunctions_And_Java_OpenFeign_Emit_Nodes_And_Edges()
    {
        const string csCode = """
            using Microsoft.Azure.Functions.Worker;

            public class OrderFunctions
            {
                [Function("ProcessOrder")]
                public void Run([ServiceBusTrigger("orders-queue")] string msg)
                {
                }
            }
            """;

        var (csFile, csRefs) = await ParseFileAsync(new CSharpParser(), csCode, "OrderFunctions.cs");
        var csEntryPoints = FindNodes<EndpointNode>(csFile.Children);

        const string javaCode = """
            package com.example;
            import org.springframework.cloud.openfeign.FeignClient;

            @FeignClient(name = "billing-service", path = "/v1/invoices")
            public interface BillingClient {
            }
            """;

        var (javaFile, _) = await ParseFileAsync(new JavaParser(), javaCode, "BillingClient.java");
        var extServices = FindNodes<ExternalServiceNode>(javaFile.Children);

        var ktParser = new KotlinProjectParser();

        Assert.Multiple(() =>
        {
            Assert.That(csEntryPoints.Any(e => e.Name.Contains("/azure-func/ProcessOrder")), Is.True);
            Assert.That(csRefs.Any(r => r.Kind == RelationshipKind.SubscribesTo && r.TargetName == "servicebus:orders-queue"), Is.True);
            Assert.That(extServices.Any(s => s.Name.Contains("billing-service")), Is.True);
            Assert.That(ktParser.SemanticExtensions.Any(e => e.Id == "ktor-server"), Is.True);
            Assert.That(ktParser.SemanticExtensions.Any(e => e.Id == "graphql-kotlin"), Is.True);
        });
    }
}
