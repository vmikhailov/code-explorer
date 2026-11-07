using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.Java;
using CodeExplorer.Parser.Python;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[NonParallelizable]
public class Phase1LibraryExpansionTests
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

    private static async Task<List<Reference>> ParseAndCollectReferencesAsync(IFileParser parser, string code, string fileName)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "phase1_lib_test_" + Guid.NewGuid().ToString("N"));
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
            return FindAllReferences(syntaxTree.FileNode);
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
    public async Task Python_Celery_And_Dramatiq_Emit_PublishesTo_And_SubscribesTo()
    {
        const string code = """
            from celery import shared_task
            import dramatiq

            @shared_task
            def send_invoice_email(order_id: int):
                pass

            @dramatiq.actor
            def process_refund(refund_id: str):
                pass

            def checkout():
                send_invoice_email.delay(42)
                process_refund.send("ref_1")
            """;

        var refs = await ParseAndCollectReferencesAsync(new PythonParser(), code, "tasks.py");
        Assert.Multiple(() =>
        {
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "celery:send_invoice_email"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.PublishesTo && r.TargetName == "celery:send_invoice_email"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "dramatiq:process_refund"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.PublishesTo && r.TargetName == "dramatiq:process_refund"), Is.True);
        });
    }

    [Test]
    public async Task Python_Rq_And_RedisStreams_Emit_PublishesTo_And_SubscribesTo()
    {
        const string code = """
            import rq

            def dispatch_events(queue, redis_client):
                queue.enqueue("sync_inventory")
                redis_client.xadd("orders-stream", {"id": "1"})
                redis_client.xread({"orders-stream": "0-0"})
            """;

        var refs = await ParseAndCollectReferencesAsync(new PythonParser(), code, "streams.py");
        Assert.Multiple(() =>
        {
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.PublishesTo && r.TargetName == "rq:sync_inventory"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.PublishesTo && r.TargetName == "redis-stream:orders-stream"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "redis-stream:orders-stream"), Is.True);
        });
    }

    [Test]
    public async Task CSharp_Hangfire_Quartz_Wolverine_Cap_And_MassTransitSaga_Emit_Edges()
    {
        const string code = """
            using Hangfire;
            using Quartz;
            using Wolverine;
            using DotNetCore.CAP;
            using MassTransit;

            public record OrderPlaced(string Id);

            public class InvoiceJob : IJob
            {
                [Queue("billing")]
                public void ExecuteJob()
                {
                    BackgroundJob.Enqueue<InvoiceJob>(x => x.ExecuteJob());
                }
            }

            public class OrderHandler
            {
                public void Handle(OrderPlaced cmd, IMessageBus bus, ICapPublisher capPublisher)
                {
                    bus.PublishAsync(new OrderPlaced("1"));
                    capPublisher.Publish("orders.created", cmd);
                }

                [CapSubscribe("orders.created")]
                public void OnOrderCreated(OrderPlaced evt) { }
            }

            public class OrderStateMachine : MassTransitStateMachine<OrderState>
            {
                public Event<OrderPlaced> OrderPlacedEvent { get; private set; }
            }
            """;

        var refs = await ParseAndCollectReferencesAsync(new CSharpParser(), code, "Messaging.cs");
        Assert.Multiple(() =>
        {
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "hangfire:billing"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.PublishesTo && r.TargetName == "hangfire:InvoiceJob"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "quartz:InvoiceJob"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "wolverine:OrderPlaced"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.PublishesTo && r.TargetName == "wolverine:OrderPlaced"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "cap:orders.created"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.PublishesTo && r.TargetName == "cap:orders.created"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "masstransit:OrderPlaced"), Is.True);
        });
    }

    [Test]
    public async Task TypeScript_Inngest_And_BullLegacy_Emit_Edges()
    {
        const string code = """
            import { Inngest } from "inngest";
            import Queue from "bull";

            export const syncUser = inngest.createFunction(
              { id: "sync-user" },
              { event: "app/user.created" },
              async ({ step }) => {
                await inngest.send({ name: "app/user.synced" });
              }
            );

            emailQueue.process("welcome-email", async (job) => {});
            """;

        var refs = await ParseAndCollectReferencesAsync(new TypeScriptParser(), code, "jobs.ts");
        Assert.Multiple(() =>
        {
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "inngest:app/user.created"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.PublishesTo && r.TargetName == "inngest:app/user.synced"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "bull:welcome-email"), Is.True);
        });
    }

    [Test]
    public async Task Go_Watermill_And_Asynq_Emit_Edges()
    {
        const string code = """
            package worker

            import (
                "github.com/ThreeDotsLabs/watermill/message"
                "github.com/hibiken/asynq"
            )

            func setup(router *message.Router, mux *asynq.ServeMux) {
                router.AddHandler("order_handler", "orders.incoming", sub, "orders.processed", pub, handleOrder)
                task := asynq.NewTask("email:deliver", payload)
                mux.HandleFunc("email:deliver", handleEmail)
            }
            """;

        var refs = await ParseAndCollectReferencesAsync(new GoParser(), code, "worker.go");
        Assert.Multiple(() =>
        {
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "watermill:orders.incoming"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.PublishesTo && r.TargetName == "watermill:orders.processed"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.PublishesTo && r.TargetName == "asynq:email:deliver"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "asynq:email:deliver"), Is.True);
        });
    }

    [Test]
    public async Task Java_Camel_Pulsar_And_Axon_Emit_Edges()
    {
        const string code = """
            package com.example;

            import org.apache.camel.Consume;
            import org.springframework.pulsar.annotation.PulsarListener;
            import org.axonframework.commandhandling.CommandHandler;

            public class MessagingRoutes {
                @Consume("jms:queue:orders")
                public void onCamelOrder(String body) {
                    producerTemplate.sendBody("jms:queue:audit", body);
                }

                @PulsarListener(topics = "persistent://public/default/payments")
                public void onPayment(String msg) {
                    pulsarTemplate.send("persistent://public/default/receipts", msg);
                }

                @CommandHandler
                public void handle(CreateOrderCommand cmd) {
                    eventGateway.publish(new OrderCreatedEvent());
                }
            }
            """;

        var refs = await ParseAndCollectReferencesAsync(new JavaParser(), code, "MessagingRoutes.java");
        Assert.Multiple(() =>
        {
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "camel:jms:queue:orders"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.PublishesTo && r.TargetName == "camel:jms:queue:audit"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "pulsar:persistent://public/default/payments"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.PublishesTo && r.TargetName == "pulsar:persistent://public/default/receipts"), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName.StartsWith("axon:")), Is.True);
            Assert.That(refs.Any(r => r.Kind == OntologyConstants.Relationships.PublishesTo && r.TargetName == "axon:OrderCreatedEvent"), Is.True);
        });
    }
}
