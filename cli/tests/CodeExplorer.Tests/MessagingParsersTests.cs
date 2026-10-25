using System.Threading.Channels;
using CodeExplorer.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[NonParallelizable]
public class MessagingParsersTests
{
    private static List<EndpointNode> FindEndpointNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<EndpointNode>();
        foreach (var node in nodes)
        {
            if (node is EndpointNode e) result.Add(e);
            result.AddRange(FindEndpointNodes(node.Children));
        }
        return result;
    }

    private static List<TypeNode> FindTypeNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<TypeNode>();
        foreach (var node in nodes)
        {
            if (node is TypeNode t) result.Add(t);
            result.AddRange(FindTypeNodes(node.Children));
        }
        return result;
    }

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

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var endpoints = FindEndpointNodes(fileNode.Children);
            var externalServices = FindExternalServiceNodes(fileNode.Children);
            var refs = FindReferences(fileNode.Children);

            // Verify EndpointNode created for IConsumer Consume
            var consumerEp = endpoints.FirstOrDefault(e => e.Protocol == "MSG");
            consumerEp.Should().NotBeNull("Expected MassTransit EndpointNode for IConsumer");
            consumerEp!.Name.Should().Contain("SubmitOrder");

            // Verify SubscribesTo relationship to SubmitOrder
            var subRel = refs.FirstOrDefault(r =>
                r.Kind == "SUBSCRIBES_TO" && r.TargetName == "masstransit:SubmitOrder");
            subRel.Should().NotBeNull("Expected SUBSCRIBES_TO reference for SubmitOrder message");

            // Verify PublishesTo relationship to OrderSubmitted
            var pubRel = refs.FirstOrDefault(r =>
                r.Kind == "PUBLISHES_TO" && r.TargetName == "masstransit:OrderSubmitted");
            pubRel.Should().NotBeNull("Expected PUBLISHES_TO reference for OrderSubmitted message");
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

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var types = FindTypeNodes(fileNode.Children);
            var externalServices = FindExternalServiceNodes(fileNode.Children);
            var refs = FindReferences(fileNode.Children);

            // Verify TypeNodes created for IRequestHandler and INotificationHandler (in-process MediatR handlers are TypeNodes, not EntryPoints)
            var handlers = types.Where(t => t.Name is "CreateUserHandler" or "UserCreatedNotificationHandler").ToList();

            handlers.Count.Should().BeGreaterThanOrEqualTo(2, "Expected at least 2 MediatR Handler TypeNodes");

            // Verify SubscribesTo relationships
            var subCmd = refs.FirstOrDefault(r =>
                r.Kind == "SUBSCRIBES_TO" && r.TargetName == "mediatr:CreateUserCommand");
            subCmd.Should().NotBeNull("Expected SUBSCRIBES_TO reference for CreateUserCommand");

            var subNotification = refs.FirstOrDefault(r =>
                r.Kind == "SUBSCRIBES_TO" && r.TargetName == "mediatr:UserCreatedEvent");
            subNotification.Should().NotBeNull("Expected SUBSCRIBES_TO reference for UserCreatedEvent");

            // Verify PublishesTo relationship
            var pubRel = refs.FirstOrDefault(r =>
                r.Kind == "PUBLISHES_TO" && r.TargetName == "mediatr:UserCreatedEvent");
            pubRel.Should().NotBeNull("Expected PUBLISHES_TO reference for UserCreatedEvent");
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
using AwesomeAssertions;

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

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var endpoints = FindEndpointNodes(fileNode.Children);
            var refs = FindReferences(fileNode.Children);

            // Verify EndpointNode created for IMessageHandler
            var consumerEp = endpoints.FirstOrDefault(e => e.Protocol == "MSG");
            consumerEp.Should().NotBeNull("Expected KafkaFlow EndpointNode for IMessageHandler");
            consumerEp!.Name.Should().Contain("OrderDirectiveMessage");

            // Verify SubscribesTo relationship to OrderDirectiveMessage
            var subRel = refs.FirstOrDefault(r =>
                r.Kind == "SUBSCRIBES_TO" && r.TargetName == "kafka:OrderDirectiveMessage");
            subRel.Should().NotBeNull("Expected SUBSCRIBES_TO reference for OrderDirectiveMessage");
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

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var refs = FindReferences(fileNode.Children);

            var pubRel = refs.FirstOrDefault(r =>
                r.Kind == "PUBLISHES_TO" && r.TargetName == "rabbitmq:orders_queue");
            pubRel.Should().NotBeNull("Expected PUBLISHES_TO reference for rabbitmq:orders_queue");

            var subRel = refs.FirstOrDefault(r =>
                r.Kind == "SUBSCRIBES_TO" && r.TargetName == "rabbitmq:orders_queue");
            subRel.Should().NotBeNull("Expected SUBSCRIBES_TO reference for rabbitmq:orders_queue");

            var helperSub = refs.FirstOrDefault(r =>
                r.Kind == "SUBSCRIBES_TO" && r.TargetName == "rabbitmq:impression_queue");

            helperSub.Should().NotBeNull("Expected SUBSCRIBES_TO reference for rabbitmq:impression_queue via consumeRabbit helper");
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

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var refs = FindReferences(fileNode.Children);

            var pubRel =
                refs.FirstOrDefault(r => r.Kind == "PUBLISHES_TO" && r.TargetName == "gcp:events_topic");
            pubRel.Should().NotBeNull("Expected PUBLISHES_TO reference for gcp:events_topic");

            var subRel =
                refs.FirstOrDefault(r => r.Kind == "SUBSCRIBES_TO" && r.TargetName == "gcp:events_sub");
            subRel.Should().NotBeNull("Expected SUBSCRIBES_TO reference for gcp:events_sub");

            var workerSub = refs.FirstOrDefault(r =>
                r.Kind == "SUBSCRIBES_TO" && r.TargetName == "gcp:streaming_topic");

            workerSub.Should().NotBeNull("Expected SUBSCRIBES_TO reference for gcp:streaming_topic from WorkerDef");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_GoPubSubParser_EnsureSubscription_DoesNotPublish()
    {
        var parser = new GoParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_gopubsub_nopub_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "subscriber.go");

        var code = @"
package main

import (
    ""context""
    ""cloud.google.com/go/pubsub""
)

func EnsureSubscription(ctx context.Context, client *pubsub.Client) {
    topic := client.Topic(""incoming_events"")
    sub := client.Subscription(""incoming_sub"")
    _, _ = client.CreateSubscription(ctx, ""incoming_sub"", pubsub.SubscriptionConfig{
        Topic: topic,
    })
    _ = sub
}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();

            await using var client = new InMemoryGraphClient();

            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var refs = FindReferences(fileNode.Children);

            // Must NOT have any PUBLISHES_TO because client.Topic was only retrieved for subscription setup, never published to!
            var pubRel = refs.FirstOrDefault(r => r.Kind == "PUBLISHES_TO");
            pubRel.Should().BeNull("Expected NO PUBLISHES_TO relationship when .Publish() is never called");

            // Should have SUBSCRIBES_TO for subscription
            var subRel = refs.FirstOrDefault(r => r.Kind == "SUBSCRIBES_TO" && r.TargetName == "gcp:incoming_sub");
            subRel.Should().NotBeNull("Expected SUBSCRIBES_TO reference for gcp:incoming_sub");
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

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var refs = FindReferences(fileNode.Children);

            var subRel = refs.FirstOrDefault(r =>
                r.Kind == "SUBSCRIBES_TO" && r.TargetName == "rabbitmq:PA_PARTNER_QUEUE");
            subRel.Should().NotBeNull("Expected SUBSCRIBES_TO reference for rabbitmq:PA_PARTNER_QUEUE");

            var pubRel = refs.FirstOrDefault(r =>
                r.Kind == "PUBLISHES_TO" && r.TargetName == "rabbitmq:PA_PARTNER_QUEUE");

            pubRel.Should().NotBeNull("Expected PUBLISHES_TO reference for rabbitmq:PA_PARTNER_QUEUE via send");
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
import { PubSub, Message } from '@google-cloud/pubsub';

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

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var refs = FindReferences(fileNode.Children);

            var pubRel = refs.FirstOrDefault(r =>
                r.Kind == "PUBLISHES_TO" && (r.TargetName == "gcp:event-bus-topic" ||
                                             r.TargetName == "gcp:EVENT_BUS_TOPIC_NAME"));
            pubRel.Should().NotBeNull("Expected PUBLISHES_TO reference for gcp:event-bus-topic");

            var subRel = refs.FirstOrDefault(r =>
                r.Kind == "SUBSCRIBES_TO" && r.TargetName == "gcp:event-bus-topic-SUB");
            subRel.Should().NotBeNull("Expected SUBSCRIBES_TO reference for gcp:event-bus-topic-SUB");

            var listenSub = refs.FirstOrDefault(r =>
                r.Kind == "SUBSCRIBES_TO" && r.TargetName == "gcp:CHANGE_DOMAIN_SUBSCRIPTION_NAME");

            listenSub.Should().NotBeNull("Expected SUBSCRIBES_TO reference for gcp:CHANGE_DOMAIN_SUBSCRIPTION_NAME via listenSubscription");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_TypeScriptGcpPubSub_ConstructorConfigAndFieldResolution()
    {
        var parser = new TypeScriptParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_tsgcp_config_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "rule-tree.service.ts");

        var code = @"
import { PubSub, Topic } from '@google-cloud/pubsub';

export class RuleTreeService {
    private readonly ruleTreeTopic: Topic;
    private readonly topicName: string;

    constructor(
        private readonly pubsub: PubSub,
        private readonly configService: any
    ) {
        this.ruleTreeTopic = this.pubsub.topic(this.configService.getString('RULE_TREE_TOPIC'));
        this.topicName = this.configService.getString('EVENT_BUS_TOPIC_NAME');
    }

    async updateRules(payload: any) {
        await this.ruleTreeTopic.publishMessage({ json: payload });
        const customTopic = this.pubsub.topic(this.topicName);
        await customTopic.publish(Buffer.from(JSON.stringify(payload)));
    }
}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();

            await using var client = new InMemoryGraphClient();

            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var refs = FindReferences(fileNode.Children);

            // Assert no bogus references to type annotations
            (refs.Any(r => r.TargetName != null && (r.TargetName.Contains(": Topic") ||
                                                       r.TargetName.Contains(": string") ||
                                                       r.TargetName == "gcp:Topic" ||
                                                       r.TargetName == "gcp:string"))).Should().BeFalse("Type annotations must never become topic names");

            // Assert resolved or config-extracted topics
            var ruleTreeRef = refs.FirstOrDefault(r =>
                r.Kind == "PUBLISHES_TO" && (r.TargetName == "gcp:RULE_TREE_TOPIC" ||
                                             r.TargetName == "gcp:rule-tree-topic"));
            ruleTreeRef.Should().NotBeNull("Expected reference for RULE_TREE_TOPIC");

            var eventBusRef = refs.FirstOrDefault(r =>
                r.Kind == "PUBLISHES_TO" && (r.TargetName == "gcp:EVENT_BUS_TOPIC_NAME" ||
                                             r.TargetName == "gcp:event-bus-topic"));
            eventBusRef.Should().NotBeNull("Expected reference for EVENT_BUS_TOPIC_NAME");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_TypeScriptRabbitMq_IgnoresTypeAnnotationsAndUrls()
    {
        var parser = new TypeScriptParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_tsrabbit_bogus_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "legacy-queue.ts");

        var code = @"
import { connect } from 'amqplib';

export class LegacyQueueService {
    private readonly queueName: string;

    constructor() {
        this.queueName = 'legacy_orders_queue';
    }

    async send() {
        const client = await connect('amqp://localhost');
        client.sendToQueue('https://wrong-url/queue', Buffer.from('hi'));
        client.sendToQueue(this.queueName, Buffer.from('valid'));
    }
}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();

            await using var client = new InMemoryGraphClient();

            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var refs = FindReferences(fileNode.Children);

            // Assert no URLs or type annotations
            (refs.Any(r =>
                    r.TargetName != null &&
                    (r.TargetName.Contains("http://") || r.TargetName.Contains("https://")))).Should().BeFalse("URLs must never become queue names");

            (refs.Any(r => r.TargetName != null && r.TargetName.Contains(": string"))).Should().BeFalse("Type annotations must not become queue names");

            // Assert valid queue is captured
            var validRef = refs.FirstOrDefault(r =>
                r.Kind == "PUBLISHES_TO" && r.TargetName == "rabbitmq:legacy_orders_queue");
            validRef.Should().NotBeNull("Expected PUBLISHES_TO reference for legacy_orders_queue");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_TypeScriptMessaging_NeverCreatesTopicsFromMethodParameters()
    {
        var parser = new TypeScriptParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_param_reject_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "helper.service.ts");

        var code = @"
import { connect } from 'amqplib';

export class HelperService {
    async setupQueue(queueName: string, options: any) {
        const client = await connect('amqp://localhost');
        client.sendToQueue(queueName, Buffer.from('payload'));
    }
}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();

            await using var client = new InMemoryGraphClient();

            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var refs = FindReferences(fileNode.Children);

            (refs.Any(r =>
                    r.TargetName == "rabbitmq:queueName" || r.TargetName == "rabbitmq:queue" ||
                    r.TargetName == "rabbitmq:string")).Should().BeFalse("Method parameters must NEVER become queue names");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_TypeScriptMessaging_ResolvesExportedConstantFromSharedFile()
    {
        using var _ = await TestRegistryLock.AcquireAsync();
        var parser = new TypeScriptParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_crossfile_const_" + Guid.NewGuid().ToString("N"));
        var sharedDir = Path.Combine(tempDir, "shared");
        Directory.CreateDirectory(sharedDir);

        var constFilePath = Path.Combine(sharedDir, "topics.ts");
        var constCode = "export const ORDER_COMPLETED_TOPIC = 'orders-completed-v2';\n";
        await File.WriteAllTextAsync(constFilePath, constCode);
        ConstantRegistry.ScanAndRegister(constFilePath, constCode, "MyProject");
        ConstantRegistry.Register(null, "ORDER_COMPLETED_TOPIC", "orders-completed-v2");

        var serviceFilePath = Path.Combine(tempDir, "publisher.ts");
        var serviceCode = @"
import { PubSub } from '@google-cloud/pubsub';
import { ORDER_COMPLETED_TOPIC } from './shared/topics';

export class OrderPublisher {
    constructor(private pubsub: PubSub) {}

    async publish() {
        await this.pubsub.topic(ORDER_COMPLETED_TOPIC).publishMessage({ json: {} });
    }
}
";
        await File.WriteAllTextAsync(serviceFilePath, serviceCode);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();

            await using var client = new InMemoryGraphClient();

            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(serviceFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var refs = FindReferences(fileNode.Children);

            var pubRel = refs.FirstOrDefault(r =>
                r.Kind == "PUBLISHES_TO" && r.TargetName == "gcp:orders-completed-v2");

            pubRel.Should().NotBeNull("Expected cross-file exported constant to resolve to 'gcp:orders-completed-v2'");
        }
        finally
        {
            ConstantRegistry.Clear();
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_EventBusLibraryParser_ConsumerAndPublish()
    {
        var parser = new CSharpParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_eventbus_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "OrderEvents.cs");

        var code = @"
using System.Threading.Tasks;

namespace MyApp.Events;

public record OrderStartedIntegrationEvent(string OrderId);

public interface IEventBus
{
    Task PublishAsync(object @event);
}

public interface IIntegrationEventHandler<in TIntegrationEvent>
{
    Task Handle(TIntegrationEvent @event);
}

public class OrderStartedIntegrationEventHandler : IIntegrationEventHandler<OrderStartedIntegrationEvent>
{
    private readonly IEventBus _eventBus;

    public OrderStartedIntegrationEventHandler(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task Handle(OrderStartedIntegrationEvent @event)
    {
        var eventMessage = new OrderStartedIntegrationEvent(@event.OrderId);
        await _eventBus.PublishAsync(eventMessage);
    }
}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();

            await using var client = new InMemoryGraphClient();

            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var endpoints = FindEndpointNodes(fileNode.Children);
            var refs = FindReferences(fileNode.Children);

            // Verify EndpointNode created for IIntegrationEventHandler
            var consumerEp = endpoints.FirstOrDefault(e => e.Protocol == "MSG");
            consumerEp.Should().NotBeNull("Expected EventBus EndpointNode for IIntegrationEventHandler");
            consumerEp!.Name.Should().Contain("OrderStartedIntegrationEvent");

            // Verify SubscribesTo relationship to OrderStartedIntegrationEvent
            var subRel = refs.FirstOrDefault(r =>
                r.Kind == "SUBSCRIBES_TO" && r.TargetName == "eventbus:OrderStartedIntegrationEvent");
            subRel.Should().NotBeNull("Expected SUBSCRIBES_TO reference for OrderStartedIntegrationEvent message");

            // Verify PublishesTo relationship to OrderStartedIntegrationEvent
            var pubRel = refs.FirstOrDefault(r =>
                r.Kind == "PUBLISHES_TO" && r.TargetName == "eventbus:OrderStartedIntegrationEvent");
            pubRel.Should().NotBeNull("Expected PUBLISHES_TO reference for OrderStartedIntegrationEvent message");

            // Verify Layer5 TopicNode creation:
            var newTopicNodes = Layer5AnalysisParser.ResolveMessagingReferenceForTest(pubRel!, ctx);

            newTopicNodes.Should().ContainSingle();
            var topic = newTopicNodes[0];
            topic.Name.Should().Be("OrderStartedIntegrationEvent");
            topic.BrokerType.Should().Be("eventbus");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_EventBusLibraryParser_AddSubscriptionAndTernaryPublish()
    {
        var parser = new CSharpParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_eventbus_ternary_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "OrderProcessor.cs");

        var code = @"
using System.Threading.Tasks;

namespace MyApp.Events;

public class IntegrationEvent
{
    public string Id { get; set; } = """";
}

public class OrderPaymentSucceededIntegrationEvent : IntegrationEvent
{
    public string OrderId { get; set; }
    public OrderPaymentSucceededIntegrationEvent(string orderId) => OrderId = orderId;
}

public class OrderPaymentFailedIntegrationEvent : IntegrationEvent
{
    public string OrderId { get; set; }
    public OrderPaymentFailedIntegrationEvent(string orderId) => OrderId = orderId;
}

public class OrderStockConfirmedIntegrationEvent : IntegrationEvent
{
    public string OrderId { get; set; }
    public OrderStockConfirmedIntegrationEvent(string orderId) => OrderId = orderId;
}

public interface IIntegrationEventHandler<in TIntegrationEvent>
{
    Task Handle(TIntegrationEvent @event);
}

public interface IEventBus
{
    Task PublishAsync(object @event);
}

public interface IEventBusBuilder
{
    IEventBusBuilder AddSubscription<T, TH>();
}

public class OrderStatusChangedToStockConfirmedIntegrationEventHandler : IIntegrationEventHandler<OrderStatusChangedToStockConfirmedIntegrationEvent>
{
    private readonly IEventBus _eventBus;

    public OrderStatusChangedToStockConfirmedIntegrationEventHandler(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public void Configure(IEventBusBuilder builder)
    {
        builder.AddSubscription<OrderStockConfirmedIntegrationEvent, OrderStatusChangedToStockConfirmedIntegrationEventHandler>();
    }

    public async Task Handle(OrderStatusChangedToStockConfirmedIntegrationEvent @event)
    {
        IntegrationEvent orderPaymentIntegrationEvent;

        if (true)
        {
            orderPaymentIntegrationEvent = new OrderPaymentSucceededIntegrationEvent(@event.OrderId);
        }
        else
        {
            orderPaymentIntegrationEvent = new OrderPaymentFailedIntegrationEvent(@event.OrderId);
        }

        await _eventBus.PublishAsync(orderPaymentIntegrationEvent);
    }
}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var refs = FindReferences(fileNode.Children);

            // SubscribesTo OrderStockConfirmedIntegrationEvent via AddSubscription / IIntegrationEventHandler
            refs.Any(r => r.Kind == "SUBSCRIBES_TO" && r.TargetName == "eventbus:OrderStockConfirmedIntegrationEvent")
                .Should().BeTrue("Expected AddSubscription to emit SUBSCRIBES_TO");

            // PublishesTo both branch outcomes: OrderPaymentSucceededIntegrationEvent AND OrderPaymentFailedIntegrationEvent
            refs.Any(r => r.Kind == "PUBLISHES_TO" && r.TargetName == "eventbus:OrderPaymentSucceededIntegrationEvent")
                .Should().BeTrue("Expected success branch to emit PUBLISHES_TO");
            refs.Any(r => r.Kind == "PUBLISHES_TO" && r.TargetName == "eventbus:OrderPaymentFailedIntegrationEvent")
                .Should().BeTrue("Expected failed branch to emit PUBLISHES_TO");

            await OntologyUploader.UploadNodeTreeAsync(fileNode, "parent-id", ctx);

            ctx.GlobalReferences.Any(r => r.Kind == "PUBLISHES_TO" && r.TargetName == "eventbus:OrderPaymentSucceededIntegrationEvent")
                .Should().BeTrue("Expected OntologyUploader to preserve OrderPaymentSucceededIntegrationEvent in ctx.GlobalReferences");
            ctx.GlobalReferences.Any(r => r.Kind == "PUBLISHES_TO" && r.TargetName == "eventbus:OrderPaymentFailedIntegrationEvent")
                .Should().BeTrue("Expected OntologyUploader to preserve OrderPaymentFailedIntegrationEvent in ctx.GlobalReferences");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}


