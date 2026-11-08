using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
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
public class Phase3To5LibraryExpansionTests
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
        var tempDir = Path.Combine(Path.GetTempPath(), "phase3_5_lib_test_" + Guid.NewGuid().ToString("N"));
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
    public async Task Phase3_Orms_And_SqlMappers_Emit_Database_References()
    {
        const string pyCode = """
            from tortoise import models
            import motor.motor_asyncio

            class Account(models.Model):
                pass

            async def load_data(db):
                await Account.filter(active=True)
                await db.users.find_one({"id": 1})
            """;

        var (_, pyRefs) = await ParseFileAsync(new PythonParser(), pyCode, "models.py");

        const string goCode = """
            package repo

            import (
                "github.com/jmoiron/sqlx"
                "github.com/uptrace/bun"
            )

            func query(db *sqlx.DB, bunDb *bun.DB) {
                db.Select(&users, "SELECT * FROM accounts")
                bunDb.NewSelect()
            }
            """;

        var (_, goRefs) = await ParseFileAsync(new GoParser(), goCode, "repo.go");

        const string csCode = """
            using Marten;

            public class OrderRepo
            {
                public void Save(IDocumentSession session)
                {
                    session.Store(new object());
                    session.Events.Append("stream-1", new object());
                }
            }
            """;

        var (_, csRefs) = await ParseFileAsync(new CSharpParser(), csCode, "OrderRepo.cs");

        const string tsCode = """
            import { Kysely } from "kysely";
            import { EntityManager } from "@mikro-orm/core";

            export async function fetchInvoices(db: Kysely<any>, em: EntityManager) {
                await db.selectFrom("invoices").execute();
                await em.find("Customer", {});
            }
            """;

        var (_, tsRefs) = await ParseFileAsync(new TypeScriptParser(), tsCode, "repo.ts");

        Assert.Multiple(() =>
        {
            Assert.That(pyRefs.Any(r => r.Kind == RelationshipKind.PersistedIn && r.TargetName == "accounts"), Is.True);
            Assert.That(pyRefs.Any(r => r.Kind == RelationshipKind.UsesDb && r.TargetName == "mongodb"), Is.True);
            Assert.That(goRefs.Any(r => r.Kind == RelationshipKind.DependsOn && r.TargetName == "accounts"), Is.True);
            Assert.That(goRefs.Any(r => r.Kind == RelationshipKind.UsesDb && r.TargetName == "bun:sql"), Is.True);
            Assert.That(csRefs.Any(r => r.Kind == RelationshipKind.UsesDb && r.TargetName == "marten:postgres"), Is.True);
            Assert.That(csRefs.Any(r => r.Kind == RelationshipKind.PublishesTo && r.TargetName == "marten-events:Append"), Is.True);
            Assert.That(tsRefs.Any(r => r.Kind == RelationshipKind.UsesDb && r.TargetName == "invoices"), Is.True);
            Assert.That(tsRefs.Any(r => r.Kind == RelationshipKind.UsesDb && r.TargetName == "mikro-orm"), Is.True);
        });
    }

    [Test]
    public async Task Phase4_CloudSdks_Resilience_And_Observability_Emit_Edges()
    {
        const string csCode = """
            using Amazon.S3;
            using Polly;
            using OpenTelemetry;

            public class CloudWorker
            {
                public void Execute(IAmazonS3 s3, ActivitySource source)
                {
                    s3.PutObjectAsync(null);
                    policy.WaitAndRetryAsync(3);
                    source.StartActivity("op");
                }
            }
            """;

        var (_, csRefs) = await ParseFileAsync(new CSharpParser(), csCode, "CloudWorker.cs");

        const string pyCode = """
            import boto3

            def upload(sqs_client):
                s3 = boto3.client("s3")
                sqs_client.send_message(QueueUrl="q", MessageBody="hi")
            """;

        var (_, pyRefs) = await ParseFileAsync(new PythonParser(), pyCode, "cloud.py");

        const string goCode = """
            package cloud

            import "github.com/aws/aws-sdk-go-v2/service/s3"

            func put(client *s3.Client) {
                client.PutObject(ctx, nil)
            }
            """;

        var (_, goRefs) = await ParseFileAsync(new GoParser(), goCode, "cloud.go");

        Assert.Multiple(() =>
        {
            Assert.That(csRefs.Any(r => r.Kind == RelationshipKind.DependsOn && r.TargetName == "aws:s3"), Is.True);
            Assert.That(csRefs.Any(r => r.Kind == RelationshipKind.Calls && r.TargetName == "polly:WaitAndRetryAsync"), Is.True);
            Assert.That(csRefs.Any(r => r.Kind == RelationshipKind.Calls && r.TargetName == "otel:StartActivity"), Is.True);
            Assert.That(pyRefs.Any(r => r.Kind == RelationshipKind.DependsOn && r.TargetName == "aws:s3"), Is.True);
            Assert.That(pyRefs.Any(r => r.Kind == RelationshipKind.PublishesTo && r.TargetName == "aws:sqs"), Is.True);
            Assert.That(goRefs.Any(r => r.Kind == RelationshipKind.DependsOn && r.TargetName == "aws:s3"), Is.True);
        });
    }

    [Test]
    public async Task Phase5_Temporal_And_AiSdks_Emit_Workflow_And_Llm_Edges()
    {
        const string tsCode = """
            import { proxyActivities, executeChild } from "@temporalio/workflow";
            import { generateText } from "ai";

            export async function orderWorkflow() {
                const acts = proxyActivities();
                await executeChild("paymentWorkflow");
                await generateText({ model: "gpt-4o", prompt: "hi" });
            }
            """;

        var (_, tsRefs) = await ParseFileAsync(new TypeScriptParser(), tsCode, "workflow.ts");

        const string pyCode = """
            from temporalio import activity, workflow
            import openai

            @activity.defn
            async def charge_card() -> None:
                pass

            async def run_agent(client):
                await workflow.execute_activity(charge_card)
                await client.chat.completions.create(model="gpt-4o")
            """;

        var (_, pyRefs) = await ParseFileAsync(new PythonParser(), pyCode, "workflow.py");

        const string goCode = """
            package wf

            import "go.temporal.io/sdk/workflow"

            func Run(ctx workflow.Context, w Worker) {
                w.RegisterActivity(ChargeCard)
                workflow.ExecuteActivity(ctx, ChargeCard)
            }
            """;

        var (_, goRefs) = await ParseFileAsync(new GoParser(), goCode, "workflow.go");

        Assert.Multiple(() =>
        {
            Assert.That(tsRefs.Any(r => r.Kind == RelationshipKind.PublishesTo && r.TargetName == "temporal:activities"), Is.True);
            Assert.That(tsRefs.Any(r => r.Kind == RelationshipKind.PublishesTo && r.TargetName == "temporal:paymentWorkflow"), Is.True);
            Assert.That(tsRefs.Any(r => r.Kind == RelationshipKind.Calls && r.TargetName == "llm:generateText"), Is.True);
            Assert.That(pyRefs.Any(r => r.Kind == RelationshipKind.SubscribesTo && r.TargetName == "temporal:charge_card"), Is.True);
            Assert.That(pyRefs.Any(r => r.Kind == RelationshipKind.PublishesTo && r.TargetName == "temporal:charge_card"), Is.True);
            Assert.That(pyRefs.Any(r => r.Kind == RelationshipKind.Calls && r.TargetName == "llm:openai"), Is.True);
            Assert.That(goRefs.Any(r => r.Kind == RelationshipKind.SubscribesTo && r.TargetName == "temporal:ChargeCard"), Is.True);
            Assert.That(goRefs.Any(r => r.Kind == RelationshipKind.PublishesTo && r.TargetName == "temporal:ChargeCard"), Is.True);
        });
    }
}
