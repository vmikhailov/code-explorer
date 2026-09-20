using System.Text.Json;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Java;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class CqrsPipelineTracingTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ce_cqrs_test_" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Test]
    public async Task Test_CSharp_MediatR_PipelineTracing()
    {
        var projDir = Path.Combine(_tempDir, "CqrsApp").Replace('\\', '/');
        Directory.CreateDirectory(projDir);
        await File.WriteAllTextAsync(Path.Combine(projDir, "CqrsApp.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><PackageReference Include=\"MediatR\" Version=\"12.0.0\" /></ItemGroup></Project>");

        var code = """
        using System.Threading;
        using System.Threading.Tasks;
        using MediatR;

        public record CreateOrderCommand(string CustomerId) : IRequest<string>;
        public record OrderCreatedEvent(string OrderId) : INotification;

        public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, string>
        {
            private readonly IMediator _mediator;
            public CreateOrderCommandHandler(IMediator mediator) => _mediator = mediator;

            public async Task<string> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
            {
                await _mediator.Publish(new OrderCreatedEvent("order-123"));
                return "order-123";
            }
        }

        public class OrderCreatedEventHandler : INotificationHandler<OrderCreatedEvent>
        {
            public async Task Handle(OrderCreatedEvent notification, CancellationToken cancellationToken)
            {
                // send email / notify inventory
            }
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(projDir, "OrderHandlers.cs"), code);

        var dbPath = Path.Combine(_tempDir, "test_mediatr.db").Replace('\\', '/');
        await using var client = new SqliteGraphClient(dbPath);

        WorkspaceIndexer.Register(new CSharpParser());
        var indexer = new WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempDir, _tempDir, clear: true);

        // Execute trace_cqrs_pipeline query
        var query = """
        MATCH (topic:Topic)-[:PUBLISHED_BY]->(pub), (topic)-[:SUBSCRIBED_BY]->(sub)
        RETURN pub.name AS producer, topic.name AS message, topic.broker_type AS broker, sub.name AS consumer
        """;
        var resultJson = await client.ExecuteQueryAsync(query);
        using var doc = JsonDocument.Parse(resultJson);
        var rows = doc.RootElement.EnumerateArray().ToList();

        Assert.That(rows.Count, Is.GreaterThan(0), "MediatR message pipeline should be traceable");
        Assert.That(rows.Any(r => r.GetProperty("message").GetString() == "OrderCreatedEvent"), Is.True, "OrderCreatedEvent topic should connect publisher to subscriber");
    }

    [Test]
    public async Task Test_Java_SpringEvents_PipelineTracing()
    {
        var projDir = Path.Combine(_tempDir, "SpringApp").Replace('\\', '/');
        Directory.CreateDirectory(projDir);
        await File.WriteAllTextAsync(Path.Combine(projDir, "pom.xml"), "<project></project>");

        var code = """
        package com.example;
        import org.springframework.context.ApplicationEventPublisher;
        import org.springframework.context.event.EventListener;
        import org.springframework.stereotype.Service;

        class OrderCreatedEvent {
            public String orderId;
        }

        @Service
        public class OrderService {
            private ApplicationEventPublisher publisher;
            public void createOrder() {
                publisher.publishEvent(new OrderCreatedEvent());
            }
        }

        @Service
        public class EmailNotificationService {
            @EventListener
            public void onOrderCreated(OrderCreatedEvent event) {
                // send email
            }
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(projDir, "OrderServices.java"), code);

        var dbPath = Path.Combine(_tempDir, "test_spring_events.db").Replace('\\', '/');
        await using var client = new SqliteGraphClient(dbPath);

        WorkspaceIndexer.Register(new JavaParser());
        var indexer = new WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempDir, _tempDir, clear: true);

        var query = """
        MATCH (topic:Topic)-[:PUBLISHED_BY]->(pub), (topic)-[:SUBSCRIBED_BY]->(sub)
        RETURN pub.name AS producer, topic.name AS message, topic.broker_type AS broker, sub.name AS consumer
        """;
        var resultJson = await client.ExecuteQueryAsync(query);
        using var doc = JsonDocument.Parse(resultJson);
        var rows = doc.RootElement.EnumerateArray().ToList();

        Assert.That(rows.Count, Is.GreaterThan(0), "Spring Events pipeline should connect publisher to @EventListener subscriber");
        Assert.That(rows.Any(r => r.GetProperty("message").GetString() == "OrderCreatedEvent" && r.GetProperty("broker").GetString() == "spring"), Is.True);
    }
}
