using System.Threading.Channels;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.TypeScript;
using CodeExplorer.Tests.Shared;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class MessagingParsersTests
{
    private static List<EntryPointNode> FindEntryPointNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<EntryPointNode>();
        foreach (var node in nodes)
        {
            if (node is EntryPointNode e) result.Add(e);
            result.AddRange(FindEntryPointNodes(node.Children));
        }
        return result;
    }

    private static List<ExternalServiceNode> FindExternalServiceNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<ExternalServiceNode>();
        foreach (var node in nodes)
        {
            if (node is ExternalServiceNode e) result.Add(e);
            result.AddRange(FindExternalServiceNodes(node.Children));
        }
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
    public async Task Test_MassTransitLibraryParser_ConsumerAndPublish()
    {
        var parser = new CSharpParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_masstransit_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "OrderConsumer.cs");

        var code = @"
using MassTransit;
using System.Threading.Tasks;

namespace MyApp.Consumers;

public record SubmitOrder(string OrderId);
public record OrderSubmitted(string OrderId);

public class SubmitOrderConsumer : IConsumer<SubmitOrder>
{
    public async Task Consume(ConsumeContext<SubmitOrder> context)
    {
        await context.Publish<OrderSubmitted>(new OrderSubmitted(context.Message.OrderId));
    }
}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            Assert.That(fileNode, Is.Not.Null);

            var entryPoints = FindEntryPointNodes(fileNode.Children);
            var externalServices = FindExternalServiceNodes(fileNode.Children);
            var refs = FindReferences(fileNode.Children);

            // Verify EntryPoint created for IConsumer Consume
            var consumerEp = entryPoints.FirstOrDefault(e => e.EntryType == "Consumer");
            Assert.That(consumerEp, Is.Not.Null, "Expected MassTransit EntryPointNode for IConsumer");
            Assert.That(consumerEp!.Name, Does.Contain("SubmitOrder"));

            // Verify SubscribesTo relationship to SubmitOrder
            var subRel = refs.FirstOrDefault(r => r.Kind == "SUBSCRIBES_TO" && r.TargetName == "masstransit:SubmitOrder");
            Assert.That(subRel, Is.Not.Null, "Expected SUBSCRIBES_TO reference for SubmitOrder message");

            // Verify PublishesTo relationship to OrderSubmitted
            var pubRel = refs.FirstOrDefault(r => r.Kind == "PUBLISHES_TO" && r.TargetName == "masstransit:OrderSubmitted");
            Assert.That(pubRel, Is.Not.Null, "Expected PUBLISHES_TO reference for OrderSubmitted message");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_MediatRLibraryParser_RequestHandlerAndNotificationHandler()
    {
        var parser = new CSharpParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_mediatr_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "CreateUserHandler.cs");

        var code = @"
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace MyApp.Features;

public record CreateUserCommand(string Username) : IRequest<string>;
public record UserCreatedEvent(string Username) : INotification;

public class CreateUserHandler : IRequestHandler<CreateUserCommand, string>
{
    private readonly IMediator _mediator;

    public CreateUserHandler(IMediator mediator) => _mediator = mediator;

    public async Task<string> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        await _mediator.Publish(new UserCreatedEvent(request.Username), cancellationToken);
        return ""ok"";
    }
}

public class UserCreatedNotificationHandler : INotificationHandler<UserCreatedEvent>
{
    public Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            Assert.That(fileNode, Is.Not.Null);

            var entryPoints = FindEntryPointNodes(fileNode.Children);
            var externalServices = FindExternalServiceNodes(fileNode.Children);
            var refs = FindReferences(fileNode.Children);

            // Verify EntryPoint created for IRequestHandler and INotificationHandler
            var handlers = entryPoints.Where(e => e.EntryType == "Handler").ToList();
            Assert.That(handlers.Count, Is.GreaterThanOrEqualTo(2), "Expected at least 2 MediatR Handler EntryPointNodes");

            // Verify SubscribesTo relationships
            var subCmd = refs.FirstOrDefault(r => r.Kind == "SUBSCRIBES_TO" && r.TargetName == "mediatr:CreateUserCommand");
            Assert.That(subCmd, Is.Not.Null, "Expected SUBSCRIBES_TO reference for CreateUserCommand");

            var subNotification = refs.FirstOrDefault(r => r.Kind == "SUBSCRIBES_TO" && r.TargetName == "mediatr:UserCreatedEvent");
            Assert.That(subNotification, Is.Not.Null, "Expected SUBSCRIBES_TO reference for UserCreatedEvent");

            // Verify PublishesTo relationship
            var pubRel = refs.FirstOrDefault(r => r.Kind == "PUBLISHES_TO" && r.TargetName == "mediatr:UserCreatedEvent");
            Assert.That(pubRel, Is.Not.Null, "Expected PUBLISHES_TO reference for UserCreatedEvent");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_KafkaFlowLibraryParser_ConsumerAndProduce()
    {
        var parser = new CSharpParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_kafkaflow_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "OrderDirectiveHandler.cs");

        var code = @"
using KafkaFlow;
using System.Threading.Tasks;

namespace MyApp.Handlers;

public record OrderDirectiveMessage(string DirectiveId);

public class OrderDirectiveHandler : IMessageHandler<OrderDirectiveMessage>
{
    public async Task Handle(IMessageContext context, OrderDirectiveMessage message)
    {
        await Task.CompletedTask;
    }
}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            Assert.That(fileNode, Is.Not.Null);

            var entryPoints = FindEntryPointNodes(fileNode.Children);
            var refs = FindReferences(fileNode.Children);

            // Verify EntryPoint created for IMessageHandler
            var consumerEp = entryPoints.FirstOrDefault(e => e.EntryType == "Consumer");
            Assert.That(consumerEp, Is.Not.Null, "Expected KafkaFlow EntryPointNode for IMessageHandler");
            Assert.That(consumerEp!.Name, Does.Contain("OrderDirectiveMessage"));

            // Verify SubscribesTo relationship to OrderDirectiveMessage
            var subRel = refs.FirstOrDefault(r => r.Kind == "SUBSCRIBES_TO" && r.TargetName == "kafka:OrderDirectiveMessage");
            Assert.That(subRel, Is.Not.Null, "Expected SUBSCRIBES_TO reference for OrderDirectiveMessage");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_GoRabbitMqParser_PublishAndConsume()
    {
        var parser = new GoParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_gorabbit_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "rabbit_service.go");

        var code = @"
package main

import (
    ""context""
    amqp ""github.com/rabbitmq/amqp091-go""
)

func RunRabbit(ctx context.Context, ch *amqp.Channel) {
    ch.Publish("""", ""orders_queue"", false, false, amqp.Publishing{})
    ch.Consume(""orders_queue"", """", true, false, false, false, nil)
    consumeRabbit(ctx, ch, ""impression_queue"", nil)
}

func consumeRabbit(ctx context.Context, ch *amqp.Channel, q string, h any) {}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            Assert.That(fileNode, Is.Not.Null);

            var refs = FindReferences(fileNode.Children);
            var pubRel = refs.FirstOrDefault(r => r.Kind == "PUBLISHES_TO" && r.TargetName == "rabbitmq:orders_queue");
            Assert.That(pubRel, Is.Not.Null, "Expected PUBLISHES_TO reference for rabbitmq:orders_queue");

            var subRel = refs.FirstOrDefault(r => r.Kind == "SUBSCRIBES_TO" && r.TargetName == "rabbitmq:orders_queue");
            Assert.That(subRel, Is.Not.Null, "Expected SUBSCRIBES_TO reference for rabbitmq:orders_queue");

            var helperSub = refs.FirstOrDefault(r => r.Kind == "SUBSCRIBES_TO" && r.TargetName == "rabbitmq:impression_queue");
            Assert.That(helperSub, Is.Not.Null, "Expected SUBSCRIBES_TO reference for rabbitmq:impression_queue via consumeRabbit helper");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_GoPubSubParser_PublishAndSubscribe()
    {
        var parser = new GoParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_gopubsub_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "pubsub_service.go");

        var code = @"
package main

import (
    ""context""
    ""cloud.google.com/go/pubsub""
)

type WorkerDef struct {
    TopicID string
    SubID   string
}

func RunPubSub(ctx context.Context, client *pubsub.Client) {
    topic := client.Topic(""events_topic"")
    topic.Publish(ctx, &pubsub.Message{Data: []byte(""hello"")})
    sub := client.Subscription(""events_sub"")
    _ = sub

    worker := WorkerDef{
        TopicID: ""streaming_topic"",
        SubID:   ""streaming_sub"",
    }
    _ = worker
}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            Assert.That(fileNode, Is.Not.Null);

            var refs = FindReferences(fileNode.Children);
            var pubRel = refs.FirstOrDefault(r => r.Kind == "PUBLISHES_TO" && r.TargetName == "gcp:events_topic");
            Assert.That(pubRel, Is.Not.Null, "Expected PUBLISHES_TO reference for gcp:events_topic");

            var subRel = refs.FirstOrDefault(r => r.Kind == "SUBSCRIBES_TO" && r.TargetName == "gcp:events_sub");
            Assert.That(subRel, Is.Not.Null, "Expected SUBSCRIBES_TO reference for gcp:events_sub");

            var workerSub = refs.FirstOrDefault(r => r.Kind == "SUBSCRIBES_TO" && r.TargetName == "gcp:streaming_topic");
            Assert.That(workerSub, Is.Not.Null, "Expected SUBSCRIBES_TO reference for gcp:streaming_topic from WorkerDef");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_TypeScriptRabbitMq_WrapperAndSendListen()
    {
        var parser = new TypeScriptParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_tsrabbit_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "rabbit-controller.ts");

        var code = @"
import { Rabbit } from './rabbit/rabbit';
import { RabbitQueue } from './rabbit/queue.rabbit';

const PA_PARTNER_QUEUE = 'PA_PARTNER_QUEUE';
let paPartnerQueue: RabbitQueue;

export async function rabbitKeepAlive() {
    const rabbit = await Rabbit.connect();
    paPartnerQueue = await rabbit.createQueue(PA_PARTNER_QUEUE);
    paPartnerQueue.listen((msg: string) => {});
}

export async function sendPartner() {
    await paPartnerQueue.send('hello');
}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            Assert.That(fileNode, Is.Not.Null);

            var refs = FindReferences(fileNode.Children);
            var subRel = refs.FirstOrDefault(r => r.Kind == "SUBSCRIBES_TO" && r.TargetName == "rabbitmq:PA_PARTNER_QUEUE");
            Assert.That(subRel, Is.Not.Null, "Expected SUBSCRIBES_TO reference for rabbitmq:PA_PARTNER_QUEUE");

            var pubRel = refs.FirstOrDefault(r => r.Kind == "PUBLISHES_TO" && r.TargetName == "rabbitmq:PA_PARTNER_QUEUE");
            Assert.That(pubRel, Is.Not.Null, "Expected PUBLISHES_TO reference for rabbitmq:PA_PARTNER_QUEUE via send");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_TypeScriptGcpPubSub_PublishAndSubscribe()
    {
        var parser = new TypeScriptParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_tsgcp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "pubsub-service.ts");

        var code = @"
import { PubSubService } from '@atsorganization/internal-commons-library/pubsub';
import { Message } from '@google-cloud/pubsub';

export const EVENT_BUS_TOPIC_NAME = 'event-bus-topic';

export class AppService {
    constructor(private pubSubService: PubSubService) {}

    async start() {
        await this.pubSubService.publishMessage({
            topicName: EVENT_BUS_TOPIC_NAME,
            data: { test: true }
        });

        this.pubSubService.subscribeToMessages('event-bus-topic-SUB', (msg: Message) => {});
        this.pubSubService.listenSubscription('CHANGE_DOMAIN_SUBSCRIPTION_NAME', (msg: any) => {});
    }
}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            Assert.That(fileNode, Is.Not.Null);

            var refs = FindReferences(fileNode.Children);
            var pubRel = refs.FirstOrDefault(r => r.Kind == "PUBLISHES_TO" && (r.TargetName == "gcp:event-bus-topic" || r.TargetName == "gcp:EVENT_BUS_TOPIC_NAME"));
            Assert.That(pubRel, Is.Not.Null, "Expected PUBLISHES_TO reference for gcp:event-bus-topic");

            var subRel = refs.FirstOrDefault(r => r.Kind == "SUBSCRIBES_TO" && r.TargetName == "gcp:event-bus-topic-SUB");
            Assert.That(subRel, Is.Not.Null, "Expected SUBSCRIBES_TO reference for gcp:event-bus-topic-SUB");

            var listenSub = refs.FirstOrDefault(r => r.Kind == "SUBSCRIBES_TO" && r.TargetName == "gcp:CHANGE_DOMAIN_SUBSCRIPTION_NAME");
            Assert.That(listenSub, Is.Not.Null, "Expected SUBSCRIBES_TO reference for gcp:CHANGE_DOMAIN_SUBSCRIPTION_NAME via listenSubscription");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

