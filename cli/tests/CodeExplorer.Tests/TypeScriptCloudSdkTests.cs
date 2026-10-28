using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class TypeScriptCloudSdkTests
{
    private sealed class TestWorkspace : IDisposable
    {
        public string DirectoryPath { get; }
        public string FilePath { get; }
        public SyntaxTree SyntaxTree { get; }
        public FileNode FileNode => SyntaxTree.FileNode;
        public ParsingContext Context { get; }

        private TestWorkspace(string dir, string file, SyntaxTree tree, ParsingContext ctx)
        {
            DirectoryPath = dir;
            FilePath = file;
            SyntaxTree = tree;
            Context = ctx;
        }

        public static async Task<TestWorkspace> CreateAsync(string code, string fileName = "cloud.ts")
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ts_cloud_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, fileName);
            await File.WriteAllTextAsync(filePath, code);

            var parser = new TypeScriptParser();
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            return new TestWorkspace(tempDir, filePath, syntaxTree, ctx);
        }

        public void Dispose()
        {
            SyntaxTree.Dispose();
            try { Directory.Delete(DirectoryPath, true); } catch { }
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
    public async Task Test_Stripe_PaymentIntentsAndChargesAndSessions()
    {
        var code = """
                   import Stripe from 'stripe';

                   const stripe = new Stripe('sk_test_123');

                   export async function processPayments() {
                       const payment = await stripe.paymentIntents.create({
                           amount: 2500,
                           currency: 'usd'
                       });

                       const charge = await stripe.charges.create({
                           amount: 1500,
                           currency: 'usd',
                           source: 'tok_visa'
                       });

                       const session = await stripe.checkout.sessions.create({
                           mode: 'payment'
                       });
                   }
                   """;

        using var ws = await TestWorkspace.CreateAsync(code);

        var extServices = FindNodes<ExternalServiceNode>(ws.FileNode.Children);
        extServices.Should().NotBeEmpty();

        extServices.Should().Contain(s => s.DomainOrService == "api.stripe.com" && s.Path == "/v1/payment_intents");
        extServices.Should().Contain(s => s.DomainOrService == "api.stripe.com" && s.Path == "/v1/charges");
        extServices.Should().Contain(s => s.DomainOrService == "api.stripe.com" && s.Path == "/v1/checkout/sessions");

        var allReferences = FindAllReferences(ws.FileNode);
        allReferences.Should().Contain(r => r.TargetName == "api.stripe.com" && r.Kind == "CALLS_ENDPOINT");
    }

    [Test]
    public async Task Test_AwsSdk_SqsS3DynamoDb()
    {
        var code = """
                   import { SQSClient, SendMessageCommand } from '@aws-sdk/client-sqs';
                   import { S3Client, PutObjectCommand } from '@aws-sdk/client-s3';
                   import { DynamoDBClient, PutItemCommand } from '@aws-sdk/client-dynamodb';

                   const sqs = new SQSClient({});
                   const s3 = new S3Client({});
                   const dynamo = new DynamoDBClient({});

                   export async function executeAwsOps() {
                       await sqs.send(new SendMessageCommand({
                           QueueUrl: 'https://sqs.us-east-1.amazonaws.com/123456789012/order-events',
                           MessageBody: '{}'
                       }));

                       await s3.send(new PutObjectCommand({
                           Bucket: 'asset-storage-bucket',
                           Key: 'document.pdf',
                           Body: 'data'
                       }));

                       await dynamo.send(new PutItemCommand({
                           TableName: 'UserAccountsTable',
                           Item: {}
                       }));
                   }
                   """;

        using var ws = await TestWorkspace.CreateAsync(code);

        var allReferences = FindAllReferences(ws.FileNode);

        // 1. SQS queue publish
        allReferences.Should().Contain(r => r.TargetName == "order-events" && r.Kind == "PUBLISHES_TO");

        // 2. S3 bucket dependency
        allReferences.Should().Contain(r => r.TargetName == "asset-storage-bucket" && r.Kind == "DEPENDS_ON");

        // 3. DynamoDB table query
        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().Contain(q => q.Name.Contains("UserAccountsTable"));
        allReferences.Should().Contain(r => r.TargetName == "UserAccountsTable" && r.Kind == "DEPENDS_ON");
    }

    [Test]
    public async Task Test_AzureSdk_ServiceBusBlobCosmos()
    {
        var code = """
                   import { ServiceBusClient } from '@azure/service-bus';
                   import { BlobServiceClient } from '@azure/storage-blob';
                   import { CosmosClient } from '@azure/cosmos';

                   const sbClient = new ServiceBusClient('conn');
                   const blobClient = BlobServiceClient.fromConnectionString('conn');
                   const cosmosClient = new CosmosClient({ endpoint: 'url', key: 'key' });

                   export async function executeAzureOps() {
                       const sender = sbClient.createSender('notification-queue');
                       const receiver = sbClient.createReceiver('incoming-queue');

                       const container = blobClient.getContainerClient('invoice-pdfs');

                       const db = cosmosClient.database('warehouse_db');
                       const cosmosContainer = db.container('product_catalog');
                   }
                   """;

        using var ws = await TestWorkspace.CreateAsync(code);

        var allReferences = FindAllReferences(ws.FileNode);

        // 1. Service Bus publish & subscribe
        allReferences.Should().Contain(r => r.TargetName == "notification-queue" && r.Kind == "PUBLISHES_TO");
        allReferences.Should().Contain(r => r.TargetName == "incoming-queue" && r.Kind == "SUBSCRIBES_TO");

        // 2. Blob Storage container
        allReferences.Should().Contain(r => r.TargetName == "invoice-pdfs" && r.Kind == "DEPENDS_ON");

        // 3. Cosmos DB container query
        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().Contain(q => q.Name.Contains("product_catalog"));
        allReferences.Should().Contain(r => r.TargetName == "product_catalog" && r.Kind == "DEPENDS_ON");
    }
}
