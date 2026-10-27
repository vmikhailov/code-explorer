using System.Threading.Channels;
using CodeExplorer.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.Java;
using NUnit.Framework;

namespace CodeExplorer.Tests;

public class JavaMessagingTests
{
    private static List<EndpointNode> FindEndpointNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<EndpointNode>();
        foreach (var node in nodes)
        {
            if (node is EndpointNode ep)
            {
                result.Add(ep);
            }
            result.AddRange(FindEndpointNodes(node.Children));
        }
        return result;
    }

    private static List<Reference> CollectAllReferences(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<Reference>();
        foreach (var node in nodes)
        {
            if (node is CompositeNode cn && cn.References != null)
            {
                result.AddRange(cn.References);
            }
            result.AddRange(CollectAllReferences(node.Children));
        }
        return result;
    }

    [Test]
    public async Task SpringKafka_ListenerAndTemplate_ExtractsEntryPointAndReferences()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-kafka-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "OrderConsumer.java");

        var code = @"package com.example;

import org.springframework.kafka.annotation.KafkaListener;
import org.springframework.kafka.core.KafkaTemplate;

public class OrderConsumer {
    private KafkaTemplate<String, String> template;

    @KafkaListener(topics = ""orders-topic"")
    public void handleOrder(String message) {
        template.send(""order-confirmations"", message);
    }
}
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new JavaParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var entryPoints = FindEndpointNodes(fileNode.Children);
            entryPoints.Count.Should().BeGreaterThanOrEqualTo(1);

            var kafkaEp = entryPoints.FirstOrDefault(ep => ep.Name.Contains("orders-topic"));
            kafkaEp.Should().NotBeNull();

            var allRefs = CollectAllReferences(fileNode.Children);
            allRefs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "kafka:orders-topic").Should().BeTrue();
            allRefs.Any(r => r.Kind == OntologyConstants.Relationships.PublishesTo && r.TargetName == "kafka:order-confirmations").Should().BeTrue();
            allRefs.Any(r => r.Kind == OntologyConstants.Relationships.Triggers && r.TargetName == "handleOrder").Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task SpringRabbitMq_ListenerAndTemplate_ExtractsEntryPointAndReferences()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-rabbitmq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "OrderRabbitConsumer.java");

        var code = @"package com.example;

import org.springframework.amqp.rabbit.annotation.RabbitListener;
import org.springframework.amqp.rabbit.core.RabbitTemplate;

public class OrderRabbitConsumer {
    private RabbitTemplate rabbitTemplate;

    @RabbitListener(queues = ""incoming-orders-queue"")
    public void onOrder(String data) {
        rabbitTemplate.convertAndSend(""orders-exchange"", ""order-shipped"", data);
    }
}
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new JavaParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var entryPoints = FindEndpointNodes(fileNode.Children);
            entryPoints.Count.Should().BeGreaterThanOrEqualTo(1);

            var rabbitEp = entryPoints.FirstOrDefault(ep => ep.Name.Contains("incoming-orders-queue"));
            rabbitEp.Should().NotBeNull();

            var allRefs = CollectAllReferences(fileNode.Children);
            allRefs.Any(r => r.Kind == OntologyConstants.Relationships.SubscribesTo && r.TargetName == "rabbitmq:incoming-orders-queue").Should().BeTrue();
            allRefs.Any(r => r.Kind == OntologyConstants.Relationships.PublishesTo && r.TargetName == "rabbitmq:order-shipped").Should().BeTrue();
            allRefs.Any(r => r.Kind == OntologyConstants.Relationships.Triggers && r.TargetName == "onOrder").Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
