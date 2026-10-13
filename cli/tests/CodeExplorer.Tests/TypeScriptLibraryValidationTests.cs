using System.Threading.Channels;
using AwesomeAssertions;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[NonParallelizable]
public class TypeScriptLibraryValidationTests
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

        public static async Task<TestWorkspace> CreateAsync(string code, string fileName = "index.ts")
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ts_val_test_" + Guid.NewGuid().ToString("N"));
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
    public async Task Test_Pg_QueriesAndDependencies()
    {
        var code = @"
import { Pool } from 'pg';
const pool = new Pool();
async function getUsers() {
    return await pool.query('SELECT id, email FROM users WHERE active = true');
}
async function insertAudit() {
    return await pool.query('INSERT INTO audit_logs (action) VALUES ($1)', ['login']);
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().NotBeEmpty();

        var selectQuery = queries.FirstOrDefault(q => q.Name.Contains("SELECT") && q.Name.Contains("users"));
        selectQuery.Should().NotBeNull();

        var selectDeps = selectQuery!.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName)
            .ToList();
        selectDeps.Should().Contain("users");

        var insertQuery = queries.FirstOrDefault(q => q.Name.Contains("INSERT") && q.Name.Contains("audit_logs"));
        insertQuery.Should().NotBeNull();

        var insertDeps = insertQuery!.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName)
            .ToList();
        insertDeps.Should().Contain("audit_logs");
    }

    [Test]
    public async Task Test_Mysql2_QueryAndExecute()
    {
        var code = @"
import mysql from 'mysql2/promise';
async function test() {
    const conn = await mysql.createConnection({});
    await conn.query('SELECT name, balance FROM accounts');
    await conn.execute('UPDATE orders SET status = 1 WHERE id = 5');
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().NotBeEmpty();

        var selectQuery = queries.FirstOrDefault(q =>
            (q.Name.Contains("accounts") || q.QueryText.Contains("accounts")) &&
            (q.Name.Contains("SELECT") || q.QueryText.Contains("SELECT")));
        selectQuery.Should().NotBeNull();

        (selectQuery!.References.Any(r => r.TargetName == "accounts" && r.Kind == "DEPENDS_ON")).Should().BeTrue();

        var updateQuery = queries.FirstOrDefault(q =>
            (q.Name.Contains("orders") || q.QueryText.Contains("orders")) &&
            (q.Name.Contains("UPDATE") || q.QueryText.Contains("UPDATE")));
        updateQuery.Should().NotBeNull();
        (updateQuery!.References.Any(r => r.TargetName == "orders" && r.Kind == "DEPENDS_ON")).Should().BeTrue();
    }

    [Test]
    public async Task Test_Sqlite3_PrepareAndRun()
    {
        var code = @"
import sqlite3 from 'sqlite3';
const db = new sqlite3.Database(':memory:');
function runOps() {
    db.prepare('SELECT id, title FROM tasks WHERE done = 0');
    db.run('DELETE FROM expired_tokens WHERE exp < datetime()');
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().NotBeEmpty();

        var selectQuery = queries.FirstOrDefault(q => q.Name.Contains("SELECT") && q.Name.Contains("tasks"));
        selectQuery.Should().NotBeNull();
        (selectQuery!.References.Any(r => r.TargetName == "tasks")).Should().BeTrue();

        var deleteQuery =
            queries.FirstOrDefault(q => q.Name.Contains("DELETE") && q.Name.Contains("expired_tokens"));
        deleteQuery.Should().NotBeNull();
        (deleteQuery!.References.Any(r => r.TargetName == "expired_tokens")).Should().BeTrue();
    }

    [Test]
    public async Task Test_Knex_QueryBuilderAndRaw()
    {
        var code = @"
import knex from 'knex';
const k = knex({});
async function getProducts() {
    await k('products').select('*');
    await k.raw('SELECT count(*) FROM system_events');
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().NotBeEmpty();

        var knexTableQuery = queries.FirstOrDefault(q => q.Name.Contains("products"));
        knexTableQuery.Should().NotBeNull();

        (knexTableQuery!.References.Any(r => r.TargetName == "products" && r.Kind == "USES_DB")).Should().BeTrue();

        var rawQuery = queries.FirstOrDefault(q => q.Name.Contains("SELECT") && q.Name.Contains("system_events"));
        rawQuery.Should().NotBeNull();

        (rawQuery!.References.Any(r => r.TargetName == "system_events" && r.Kind == "DEPENDS_ON")).Should().BeTrue();
    }

    [Test]
    public async Task Test_TypeOrm_FindAndQuery()
    {
        var code = @"
import { DataSource } from 'typeorm';
const ds = new DataSource({});
async function run() {
    const userRepo = ds.getRepository('User');
    await userRepo.find();
    await ds.query('SELECT * FROM transactions WHERE amount > 1000');
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().NotBeEmpty();

        var findQuery = queries.FirstOrDefault(q => q.Name.Contains("find"));
        findQuery.Should().NotBeNull();
        findQuery!.Name.Should().Contain("TypeORM");

        var rawQuery = queries.FirstOrDefault(q =>
            (q.Name.Contains("SELECT") || q.QueryText.Contains("SELECT")) &&
            (q.Name.Contains("transactions") || q.QueryText.Contains("transactions")));
        rawQuery.Should().NotBeNull();
        (rawQuery!.References.Any(r => r.TargetName == "transactions")).Should().BeTrue();
    }

    [Test]
    public async Task Test_Sequelize_ModelAndQuery()
    {
        var code = @"
import { Sequelize } from 'sequelize';
const sequelize = new Sequelize();
async function test() {
    await User.findAll();
    await sequelize.query('SELECT id, hash FROM passwords');
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().NotBeEmpty();

        var findAllQuery = queries.FirstOrDefault(q => q.Name.Contains("User.findAll"));
        findAllQuery.Should().NotBeNull();
        (findAllQuery!.References.Any(r => r.TargetName == "User" && r.Kind == "USES_DB")).Should().BeTrue();

        var rawQuery = queries.FirstOrDefault(q => q.Name.Contains("SELECT") && q.Name.Contains("passwords"));
        rawQuery.Should().NotBeNull();
        (rawQuery!.References.Any(r => r.TargetName == "passwords")).Should().BeTrue();
    }

    [Test]
    public async Task Test_Prisma_ModelOperations()
    {
        var code = @"
import { PrismaClient } from '@prisma/client';
const prisma = new PrismaClient();
async function main() {
    const users = await prisma.user.findMany({ where: { active: true } });
    const order = await prisma.order.create({ data: { total: 100 } });
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().NotBeEmpty();

        var userFind = queries.FirstOrDefault(q => q.Name.Contains("user.findMany"));
        userFind.Should().NotBeNull();
        (userFind!.References.Any(r => r.TargetName == "user" && r.Kind == "USES_DB")).Should().BeTrue();

        var orderCreate = queries.FirstOrDefault(q => q.Name.Contains("order.create"));
        orderCreate.Should().NotBeNull();
        (orderCreate!.References.Any(r => r.TargetName == "order" && r.Kind == "USES_DB")).Should().BeTrue();
    }

    [Test]
    public async Task Test_Prisma_TaggedTemplateQueryRaw()
    {
        var code = @"
import { PrismaClient } from '@prisma/client';
const prisma = new PrismaClient();
async function queryDb() {
    const res = await prisma.$queryRaw`SELECT * FROM metrics_log WHERE timestamp > NOW()`;
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().NotBeEmpty();

        var rawQuery = queries.FirstOrDefault(q =>
            (q.Name.Contains("metrics_log") || q.QueryText.Contains("metrics_log")) &&
            q.References.Any(r => r.TargetName == "metrics_log"));
        rawQuery.Should().NotBeNull();

        (rawQuery!.References.Any(r => r.TargetName == "metrics_log" && r.Kind == "DEPENDS_ON")).Should().BeTrue();
    }

    [Test]
    public async Task Test_Drizzle_SelectAndInsert()
    {
        var code = @"
import { drizzle } from 'drizzle-orm/node-postgres';
const db = drizzle();
async function run() {
    await db.select().from(usersTable);
    await db.insert(ordersTable).values({ amount: 50 });
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().NotBeEmpty();

        var selectOp = queries.FirstOrDefault(q => q.Name.Contains("usersTable"));
        selectOp.Should().NotBeNull();
        (selectOp!.References.Any(r => r.TargetName == "usersTable" && r.Kind == "USES_DB")).Should().BeTrue();

        var insertOp = queries.FirstOrDefault(q => q.Name.Contains("ordersTable"));
        insertOp.Should().NotBeNull();
        (insertOp!.References.Any(r => r.TargetName == "ordersTable" && r.Kind == "USES_DB")).Should().BeTrue();
    }

    [Test]
    public async Task Test_Drizzle_TaggedTemplateSql()
    {
        var code = @"
import { sql } from 'drizzle-orm';
const query = sql`SELECT id, created_at FROM audit_trail WHERE severity = 'high'`;
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().NotBeEmpty();

        var q = queries.FirstOrDefault(q => q.Name.Contains("audit_trail") || q.QueryText.Contains("audit_trail"));
        q.Should().NotBeNull();
        (q!.References.Any(r => r.TargetName == "audit_trail" && r.Kind == "DEPENDS_ON")).Should().BeTrue();
    }

    [Test]
    public async Task Test_Mongodb_FindAndUpdate()
    {
        var code = @"
import { MongoClient } from 'mongodb';
const client = new MongoClient('mongodb://localhost:27017');
async function test() {
    const db = client.db('mydb');
    await db.collection('customers').find({ active: true });
    await db.collection('orders').updateOne({ id: 1 }, { $set: { status: 'done' } });
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().NotBeEmpty();

        var findQuery = queries.FirstOrDefault(q => q.Name.Contains("customers.find"));
        findQuery.Should().NotBeNull();

        var updateQuery = queries.FirstOrDefault(q => q.Name.Contains("orders.updateOne"));
        updateQuery.Should().NotBeNull();
    }

    [Test]
    public async Task Test_Elasticsearch_SearchAndIndex()
    {
        var code = @"
import { Client } from '@elastic/elasticsearch';
const client = new Client({ node: 'http://localhost:9200' });
async function search() {
    await client.search({ index: 'articles', query: { match_all: {} } });
    await client.index({ index: 'server_logs', document: { message: 'started' } });
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().NotBeEmpty();

        var searchOp = queries.FirstOrDefault(q => q.Name.Contains("articles"));
        searchOp.Should().NotBeNull();
        searchOp!.Name.Should().Be("Elasticsearch: search (articles)");

        var indexOp = queries.FirstOrDefault(q => q.Name.Contains("server_logs"));
        indexOp.Should().NotBeNull();
        indexOp!.Name.Should().Be("Elasticsearch: index (server_logs)");
    }

    [Test]
    public async Task Test_Neo4j_RunCypher()
    {
        var code = @"
import neo4j from 'neo4j-driver';
const driver = neo4j.driver('neo4j://localhost:7687');
const session = driver.session();
async function queryGraph() {
    await session.run('MATCH (p:Person)-[:FRIENDS_WITH]->(f:Person) RETURN p, f');
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().NotBeEmpty();

        var neoQuery = queries.FirstOrDefault(q => q.Name.Contains("Neo4j"));
        neoQuery.Should().NotBeNull();
        neoQuery!.Name.Should().Contain("MATCH");
    }

    [Test]
    public async Task Test_InfluxDb_QueryAndWrite()
    {
        var code = @"
import { InfluxDB, Point } from '@influxdata/influxdb-client';
const client = new InfluxDB({ url: 'http://localhost:8086' });
const queryApi = client.getQueryApi('my-org');
const writeApi = client.getWriteApi('my-org', 'my-bucket');
async function test() {
    await queryApi.queryRows('from(bucket: ""telegraf"") |> range(start: -1h)', {});
    writeApi.writePoint(new Point('mem'));
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().NotBeEmpty();

        var queryCall = queries.FirstOrDefault(q => q.Name.Contains("queryRows"));
        queryCall.Should().NotBeNull();

        var writeCall = queries.FirstOrDefault(q => q.Name.Contains("writePoint"));
        writeCall.Should().NotBeNull();
    }

    [Test]
    public async Task Test_Fastify_HttpRoutes()
    {
        var code = @"
import fastify from 'fastify';
const app = fastify();
app.get('/api/v1/health', async (req, reply) => ({ ok: true }));
app.post('/api/v1/users', async (req, reply) => ({ created: true }));
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var endpoints = FindNodes<EndpointNode>(ws.FileNode.Children);
        endpoints.Should().HaveCount(2);

        var getEp = endpoints.FirstOrDefault(e => e.HttpMethod == "GET");
        getEp.Should().NotBeNull();
        getEp!.RouteTemplate.Should().Be("/api/v1/health");

        var postEp = endpoints.FirstOrDefault(e => e.HttpMethod == "POST");
        postEp.Should().NotBeNull();
        postEp!.RouteTemplate.Should().Be("/api/v1/users");
    }

    [Test]
    public async Task Test_Fastify_RouteConfigObject()
    {
        var code = @"
import fastify from 'fastify';
const server = fastify();
server.route({
    method: 'DELETE',
    url: '/api/v1/items/:id',
    handler: async () => ({ deleted: true })
});
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var endpoints = FindNodes<EndpointNode>(ws.FileNode.Children);
        endpoints.Should().HaveCount(1);

        var ep = endpoints[0];
        ep.HttpMethod.Should().Be("DELETE");
        ep.RouteTemplate.Should().Be("/api/v1/items/:id");
    }

    [Test]
    public async Task Test_Koa_RouterRoutes()
    {
        var code = @"
import Router from '@koa/router';
const router = new Router();
router.get('/users/:id', async ctx => { ctx.body = {}; });
router.post('/checkout', async ctx => { ctx.body = {}; });
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var endpoints = FindNodes<EndpointNode>(ws.FileNode.Children);
        endpoints.Should().HaveCount(2);

        var getEp = endpoints.FirstOrDefault(e => e.HttpMethod == "GET");
        getEp.Should().NotBeNull();
        getEp!.RouteTemplate.Should().Be("/users/:id");

        var postEp = endpoints.FirstOrDefault(e => e.HttpMethod == "POST");
        postEp.Should().NotBeNull();
        postEp!.RouteTemplate.Should().Be("/checkout");
    }

    [Test]
    public async Task Test_NextJs_AppRouterEndpoints()
    {
        var code = @"
import { NextResponse } from 'next/server';
export async function GET(request: Request) {
    return NextResponse.json({ ok: true });
}
export async function POST(request: Request) {
    return NextResponse.json({ created: true });
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var endpoints = FindNodes<EndpointNode>(ws.FileNode.Children);
        endpoints.Should().HaveCount(2);

        var getEp = endpoints.FirstOrDefault(e => e.HttpMethod == "GET");
        getEp.Should().NotBeNull();

        var postEp = endpoints.FirstOrDefault(e => e.HttpMethod == "POST");
        postEp.Should().NotBeNull();
    }

    [Test]
    public async Task Test_Got_HttpClientCalls()
    {
        var code = @"
import got from 'got';
async function fetchRemote() {
    const res = await got('https://api.paymentservice.com/v1/charge');
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var extServices = FindNodes<ExternalServiceNode>(ws.FileNode.Children);
        extServices.Should().HaveCount(1);
        extServices[0].Name.Should().Be("api.paymentservice.com");

        var refs = FindAllReferences(ws.FileNode);
        (refs.Any(r => r.TargetName == "api.paymentservice.com" && r.Kind == "CALLS")).Should().BeTrue();
    }

    [Test]
    public async Task Test_Ky_HttpClientCalls()
    {
        var code = @"
import ky from 'ky';
async function fetchRemote() {
    const data = await ky.get('https://inventory.internal.net/items');
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var extServices = FindNodes<ExternalServiceNode>(ws.FileNode.Children);
        extServices.Should().HaveCount(1);
        extServices[0].Name.Should().Be("inventory.internal.net");

        var refs = FindAllReferences(ws.FileNode);
        (refs.Any(r => r.TargetName == "inventory.internal.net" && r.Kind == "CALLS")).Should().BeTrue();
    }

    [Test]
    public async Task Test_KafkaJs_PublishAndSubscribe()
    {
        var code = @"
import { Kafka } from 'kafkajs';
const kafka = new Kafka({ clientId: 'test-app', brokers: ['localhost:9092'] });
const producer = kafka.producer();
const consumer = kafka.consumer({ groupId: 'test-group' });

async function messaging() {
    await producer.send({ topic: 'orders-topic', messages: [{ value: 'order-1' }] });
    await consumer.subscribe({ topic: 'payment-events', fromBeginning: true });
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var refs = FindAllReferences(ws.FileNode);

        var pubRef = refs.FirstOrDefault(r => r.TargetName == "kafka:orders-topic" && r.Kind == "PUBLISHES_TO");
        pubRef.Should().NotBeNull("Should capture Kafka publisher reference");

        var subRef = refs.FirstOrDefault(r => r.TargetName == "kafka:payment-events" && r.Kind == "SUBSCRIBES_TO");
        subRef.Should().NotBeNull("Should capture Kafka consumer reference");
    }

    [Test]
    public async Task Test_BullMq_WorkerAndQueue()
    {
        var code = @"
import { Queue, Worker } from 'bullmq';
const emailQueue = new Queue('mail-queue');

async function schedule() {
    await emailQueue.add('send-invoice', { invoiceId: 42 });
}

const worker = new Worker('mail-queue', async job => {
    console.log(job.data);
});
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var refs = FindAllReferences(ws.FileNode);

        var subRef = refs.FirstOrDefault(r => r.TargetName == "bullmq:mail-queue" && r.Kind == "SUBSCRIBES_TO");
        subRef.Should().NotBeNull("Should capture BullMQ worker subscription");

        var pubRef = refs.FirstOrDefault(r => r.TargetName.Contains("send-invoice") && r.Kind == "PUBLISHES_TO");
        pubRef.Should().NotBeNull("Should capture BullMQ queue.add job publishing");
    }

    [Test]
    public async Task Test_Express_ExtendedHttpMethods()
    {
        var code = @"
import express from 'express';
const app = express();
app.patch('/api/v1/users/:id', (req, res) => res.json({}));
app.delete('/api/v1/sessions', (req, res) => res.json({}));
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var endpoints = FindNodes<EndpointNode>(ws.FileNode.Children);
        endpoints.Should().HaveCount(2);

        var patchEp = endpoints.FirstOrDefault(e => e.HttpMethod == "PATCH");
        patchEp.Should().NotBeNull();
        patchEp!.RouteTemplate.Should().Be("/api/v1/users/:id");

        var deleteEp = endpoints.FirstOrDefault(e => e.HttpMethod == "DELETE");
        deleteEp.Should().NotBeNull();
        deleteEp!.RouteTemplate.Should().Be("/api/v1/sessions");
    }

    [Test]
    public async Task Test_NestJs_AppGetConfigService_NotTreatedAsExpressEndpoint()
    {
        var code = @"
import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';

async function bootstrap() {
  const app = await NestFactory.create(AppModule);
  app.setGlobalPrefix('api/v1');
  const configService = app.get(ConfigService);
  await app.listen(3000);
}
";

        using var ws = await TestWorkspace.CreateAsync(code, "main.ts");

        var endpoints = FindNodes<EndpointNode>(ws.FileNode.Children);
        endpoints.Should().BeEmpty();
    }

    [Test]
    public async Task Test_NestJs_ControllerAndRoutePrefixes()
    {
        var code = @"
import { Controller, Get, Post } from '@nestjs/common';

@Controller('users')
export class UsersController {
    @Get('all')
    getUsers() {}

    @Post('create')
    createUser() {}
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var endpoints = FindNodes<EndpointNode>(ws.FileNode.Children);
        endpoints.Should().HaveCount(2);

        var getEp = endpoints.FirstOrDefault(e => e.HttpMethod == "GET" && e.RouteTemplate == "/users/all");
        getEp.Should().NotBeNull();

        var postEp = endpoints.FirstOrDefault(e => e.HttpMethod == "POST" && e.RouteTemplate == "/users/create");
        postEp.Should().NotBeNull();
    }

    [Test]
    public async Task Test_NestJs_DestructuredRoutePrefix()
    {
        var configCode = @"
export const HTTP_API_PREFIX_CONFIG = {
  API_GLOBAL_PREFIX: 'api/v1',
  SMART_CPA: {
    GLOBAL_PREFIX: 'smart-cpa',
    START_PREFIX: 'campaigns/start',
    STOP_PREFIX: 'campaigns/stop',
    UPDATE_PREFIX: 'campaigns/update',
  },
};
";
        var controllerCode = @"
import { Controller, Post } from '@nestjs/common';
import { HTTP_API_PREFIX_CONFIG } from './config';

const { GLOBAL_PREFIX, START_PREFIX, STOP_PREFIX, UPDATE_PREFIX } =
  HTTP_API_PREFIX_CONFIG.SMART_CPA;

@Controller(GLOBAL_PREFIX)
export class SmartCpaController {
  @Post(START_PREFIX)
  async start() {}

  @Post(STOP_PREFIX)
  async stop() {}

  @Post(UPDATE_PREFIX)
  async update() {}
}
";
        var tempDir = Path.Combine(Path.GetTempPath(), "ts_val_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var configFile = Path.Combine(tempDir, "config.ts");
            await File.WriteAllTextAsync(configFile, configCode);
            var controllerFile = Path.Combine(tempDir, "smart-cpa.controller.ts");
            await File.WriteAllTextAsync(controllerFile, controllerCode);

            WorkspaceIndexer.AutoRegisterParsers();

            // Pre-register constants from config.ts with project name
            AstConstantExtractor.ExtractAndRegister(configFile, configCode, "integration-service-helper-nest");

            var parser = new TypeScriptParser();
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            var syntaxTree = await parser.ParseAsync(controllerFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var endpoints = FindNodes<EndpointNode>(syntaxTree.FileNode.Children);
            foreach (var ep in endpoints)
            {
                TestContext.WriteLine($"EP: {ep.Id} | {ep.HttpMethod} | {ep.RouteTemplate}");
            }

            // Must NOT create an entry point for @Controller
            (endpoints.Any(e => e.HttpMethod == "GET" && e.RouteTemplate == "/smart-cpa")).Should().BeFalse("Controller decorator itself should not be an Endpoint");

            var startEp = endpoints.FirstOrDefault(e => e.RouteTemplate.Contains("start"));
            startEp.Should().NotBeNull("Should find start endpoint");
            startEp!.RouteTemplate.Should().Be("/smart-cpa/campaigns/start");

            var stopEp = endpoints.FirstOrDefault(e => e.RouteTemplate.Contains("stop"));
            stopEp.Should().NotBeNull("Should find stop endpoint");
            stopEp!.RouteTemplate.Should().Be("/smart-cpa/campaigns/stop");

            var updateEp = endpoints.FirstOrDefault(e => e.RouteTemplate.Contains("update"));
            updateEp.Should().NotBeNull("Should find update endpoint");
            updateEp!.RouteTemplate.Should().Be("/smart-cpa/campaigns/update");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Test]
    public async Task Test_NestJs_MultipleControllers_DestructuringAndInterServiceLinking()
    {
        var configCode = @"
export const HTTP_API_PREFIX_CONFIG = {
  API_GLOBAL_PREFIX: 'api/v1',
  OTHER: {
    GLOBAL_PREFIX: 'other',
    START_PREFIX: 'start',
    STOP_PREFIX: 'stop',
    UPDATE_PREFIX: 'update',
  },
  SMART_CPA: {
    GLOBAL_PREFIX: 'smart-cpa',
    START_PREFIX: 'campaigns/start',
    STOP_PREFIX: 'campaigns/stop',
    UPDATE_PREFIX: 'campaigns/update',
  },
};
";
        var otherControllerCode = @"
import { Controller, Post } from '@nestjs/common';
import { HTTP_API_PREFIX_CONFIG } from './config';

const { GLOBAL_PREFIX, START_PREFIX } = HTTP_API_PREFIX_CONFIG.OTHER;

@Controller(GLOBAL_PREFIX)
export class OtherController {
  @Post(START_PREFIX)
  async start() {}
}
";
        var smartCpaControllerCode = @"
import { Controller, Post } from '@nestjs/common';
import { HTTP_API_PREFIX_CONFIG } from './config';

const { GLOBAL_PREFIX, START_PREFIX, STOP_PREFIX, UPDATE_PREFIX } =
  HTTP_API_PREFIX_CONFIG.SMART_CPA;

@Controller(GLOBAL_PREFIX)
export class SmartCpaController {
  @Post(START_PREFIX)
  async start() {}

  @Post(STOP_PREFIX)
  async stop() {}

  @Post(UPDATE_PREFIX)
  async update() {}
}
";
        var tempDir = Path.Combine(Path.GetTempPath(), "ts_multictrl_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var configFile = Path.Combine(tempDir, "config.ts");
            await File.WriteAllTextAsync(configFile, configCode);
            var otherControllerFile = Path.Combine(tempDir, "other.controller.ts");
            await File.WriteAllTextAsync(otherControllerFile, otherControllerCode);
            var smartCpaControllerFile = Path.Combine(tempDir, "smart-cpa.controller.ts");
            await File.WriteAllTextAsync(smartCpaControllerFile, smartCpaControllerCode);

            WorkspaceIndexer.AutoRegisterParsers();

            var projectName = "integration-service-helper-nest";
            AstConstantExtractor.ExtractAndRegister(configFile, configCode, projectName);

            var parser = new TypeScriptParser();
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            // Register global prefix for the project
            RouteDictionaryRegistry.RegisterGlobalPrefix(projectName, "api/v1");

            // Process other.controller.ts first
            var otherTree = await parser.ParseAsync(otherControllerFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(otherTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, ctx, projectName);

            // Then process smart-cpa.controller.ts
            var smartCpaTree = await parser.ParseAsync(smartCpaControllerFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(smartCpaTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, ctx, projectName);

            var otherEndpoints = FindNodes<EndpointNode>(otherTree.FileNode.Children);
            var smartCpaEndpoints = FindNodes<EndpointNode>(smartCpaTree.FileNode.Children);

            // Verify other controller routes
            var otherStart = otherEndpoints.FirstOrDefault(e => e.HttpMethod == "POST");
            otherStart.Should().NotBeNull();
            otherStart!.RouteTemplate.Should().Be("/api/v1/other/start");

            // Verify smart-cpa controller routes - MUST NOT be /other/... or /start
            var startEp = smartCpaEndpoints.FirstOrDefault(e => e.RouteTemplate.Contains("start"));
            startEp.Should().NotBeNull();
            startEp!.RouteTemplate.Should().Be("/api/v1/smart-cpa/campaigns/start");

            var stopEp = smartCpaEndpoints.FirstOrDefault(e => e.RouteTemplate.Contains("stop"));
            stopEp.Should().NotBeNull();
            stopEp!.RouteTemplate.Should().Be("/api/v1/smart-cpa/campaigns/stop");

            var updateEp = smartCpaEndpoints.FirstOrDefault(e => e.RouteTemplate.Contains("update"));
            updateEp.Should().NotBeNull();
            updateEp!.RouteTemplate.Should().Be("/api/v1/smart-cpa/campaigns/update");

            // Verify Inter-Service linking: ExternalService call from bundle to helper-nest
            var extService = new ExternalServiceNode(
                "bundle:es:http:id_helper_nest",
                "id_helper_nest",
                "http",
                "id_helper_nest",
                "/api/v1/smart-cpa/campaigns/start"
            );
            var helperNestProject = new CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode(
                "integration-service-helper-nest",
                "integration-service-helper-nest",
                "src/integrations/services/helper-nest",
                "npm"
            );

            var l5Parser = new Layer5AnalysisParser();
            var doesMatchProjMethod = typeof(Layer5AnalysisParser).GetMethod("DoesProjectMatchServiceDomain", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, [typeof(CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode), typeof(string)])!;
            var projMatches = (bool)doesMatchProjMethod.Invoke(null, [helperNestProject, extService.DomainOrService])!;
            projMatches.Should().BeTrue("id_helper_nest should match project integration-service-helper-nest");

            var isMatchMethod = typeof(Layer5AnalysisParser).GetMethod("IsMatch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, [typeof(ExternalServiceNode), typeof(EndpointNode), typeof(CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode)])!;
            var endpointMatches = (bool)isMatchMethod.Invoke(l5Parser, [extService, startEp, helperNestProject])!;
            endpointMatches.Should().BeTrue("ExternalService /api/v1/smart-cpa/campaigns/start should match smart-cpa endpoint");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Test]
    public async Task Test_NestJs_VersionDecorator()
    {
        var code = @"
import { Controller, Get, Post, Version } from '@nestjs/common';

@Controller('kv')
export class TrafficRoutingRulesController {
    @Version('1')
    @Post('/form-full-rule')
    formFullRule() {}

    @Version(['1', '2'])
    @Get('/rules')
    getRules() {}
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var endpoints = FindNodes<EndpointNode>(ws.FileNode.Children);
        var postEp = endpoints.FirstOrDefault(e => e.HttpMethod == "POST" && e.RouteTemplate == "/v1/kv/form-full-rule");
        postEp.Should().NotBeNull("Expected /v1/kv/form-full-rule endpoint with POST method");

        var getEp = endpoints.FirstOrDefault(e => e.HttpMethod == "GET" && e.RouteTemplate == "/v1/kv/rules");
        getEp.Should().NotBeNull("Expected /v1/kv/rules endpoint with GET method");
    }

    [Test]
    public async Task Test_TypeScript_CommonJsRequire()
    {
        var code = @"
const { Pool } = require('pg');
const pool = new Pool();
async function test() {
    await pool.query('SELECT * FROM accounts WHERE status = 1');
}
";

        using var ws = await TestWorkspace.CreateAsync(code);

        var queries = FindNodes<QueryNode>(ws.FileNode.Children);
        queries.Should().NotBeEmpty();

        var q = queries.FirstOrDefault(q => q.Name.Contains("accounts") || q.QueryText.Contains("accounts"));
        q.Should().NotBeNull();
        (q!.References.Any(r => r.TargetName == "accounts" && r.Kind == "DEPENDS_ON")).Should().BeTrue();
    }

    [Test]
    public async Task Test_Angular_HttpClient_DirectCalls()
    {
        RouteDictionaryRegistry.Clear();
        var code = @"
import { Component } from '@angular/core';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-games',
  template: '<div>Games</div>'
})
export class GamesComponent {
  constructor(private httpClient: HttpClient) {}

  loadGames() {
    this.httpClient.get<any>('http://localhost:8060/api/v1/games').subscribe();
    this.httpClient.post<any>('/api/v1/profiles', { name: 'Player1' }).subscribe();
  }
}
";

        using var ws = await TestWorkspace.CreateAsync(code, "games.component.ts");

        var extServices = FindNodes<ExternalServiceNode>(ws.FileNode.Children);
        extServices.Should().HaveCount(2);

        var gamesSvc = extServices.FirstOrDefault(s => s.Path == "/api/v1/games");
        gamesSvc.Should().NotBeNull();
        gamesSvc!.DomainOrService.Should().Be("localhost");

        var profileSvc = extServices.FirstOrDefault(s => s.Path == "/api/v1/profiles");
        profileSvc.Should().NotBeNull();
    }

    [Test]
    public async Task Test_Angular_HttpClient_EnvironmentAndGetterFunctions()
    {
        RouteDictionaryRegistry.Clear();

        // 1. Simulate environment.ts and urlConstructor.ts scanning
        var envCode = @"
export const environment = {
  production: false,
  players: 'http://localhost:8050/api/v1/profiles',
  tournament: 'http://localhost:8060/api/v1/tournaments',
  games: 'http://localhost:8060/api/v1/games',
  genres: 'http://localhost:8060/api/v1/genres',
  users: 'https://localhost:8020/identity/api/v1/users',
  identity: 'https://localhost:8020/identity'
};
";
        RouteDictionaryRegistry.ScanAndRegister(envCode);

        var urlConstructorCode = @"
import { environment as env } from './environment';

export const getGamesListUrl = () => env.games;
export const getPlayerUrl = () => env.players;
export const getUsersFromIdentityUrl = () => env.users;
export const getIdentityUrl = () => env.identity;
";
        RouteDictionaryRegistry.ScanAndRegister(urlConstructorCode);

        // 2. Parse Angular components using these getters
        var componentCode = @"
import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { getGamesListUrl, getPlayerUrl, getUsersFromIdentityUrl } from './urlConstructor';

@Component({
  selector: 'app-admin',
  template: '<div>Admin</div>'
})
export class AdminComponent implements OnInit {
  constructor(private httpClient: HttpClient) {}

  ngOnInit() {
    this.httpClient.get<any>(getGamesListUrl()).subscribe();
    this.httpClient.post<any>(getPlayerUrl(), { name: 'NewPlayer' }).subscribe();
    this.httpClient.get<any>(getUsersFromIdentityUrl()).subscribe();
  }
}
";

        using var ws = await TestWorkspace.CreateAsync(componentCode, "admin.component.ts");

        var extServices = FindNodes<ExternalServiceNode>(ws.FileNode.Children);
        extServices.Should().HaveCount(3);

        var gamesSvc = extServices.FirstOrDefault(s => s.Path == "/api/v1/games");
        gamesSvc.Should().NotBeNull();

        var playerSvc = extServices.FirstOrDefault(s => s.Path == "/api/v1/profiles");
        playerSvc.Should().NotBeNull();

        var userSvc = extServices.FirstOrDefault(s => s.Path.EndsWith("/api/v1/users"));
        userSvc.Should().NotBeNull();
    }

    [Test]
    public async Task Test_Angular_HttpClient_HeuristicGetterResolution()
    {
        RouteDictionaryRegistry.Clear();

        // Only environment is scanned; getter functions are NOT explicitly registered in dictionary
        var envCode = @"
export const environment = {
  games: 'http://localhost:8060/api/v1/games',
  players: 'http://localhost:8050/api/v1/profiles'
};
";
        RouteDictionaryRegistry.ScanAndRegister(envCode);

        var componentCode = @"
import { Component } from '@angular/core';
import { HttpClient } from '@angular/common/http';

@Component({ selector: 'app-test', template: '' })
export class TestComponent {
  constructor(private httpClient: HttpClient) {}

  fetch() {
    this.httpClient.get(getGamesListUrl()).subscribe();
    this.httpClient.post(getPlayerUrl(), {}).subscribe();
  }
}
";

        using var ws = await TestWorkspace.CreateAsync(componentCode, "test.component.ts");

        var extServices = FindNodes<ExternalServiceNode>(ws.FileNode.Children);
        extServices.Should().HaveCount(2);

        var gamesSvc = extServices.FirstOrDefault(s => s.Path == "/api/v1/games");
        gamesSvc.Should().NotBeNull();

        var playerSvc = extServices.FirstOrDefault(s => s.Path == "/api/v1/profiles");
        playerSvc.Should().NotBeNull();
    }

    [Test]
    public async Task Test_TypeScript_AngularOidc()
    {
        RouteDictionaryRegistry.Clear();

        var authConfigCode = @"
import { AuthConfig } from 'angular-oauth2-oidc';

export const authCodeFlowConfig: AuthConfig = {
  issuer: 'https://localhost:8020/identity',
  redirectUri: 'http://localhost:4200/index.html',
  clientId: 'admin-application',
  responseType: 'code',
  scope: 'openid profile email roles',
  showDebugInformation: true
};
";

        using var wsConfig = await TestWorkspace.CreateAsync(authConfigCode, "auth.config.ts");

        var extServicesConfig = FindNodes<ExternalServiceNode>(wsConfig.FileNode.Children);
        extServicesConfig.Should().HaveCountGreaterThan(0);

        var oidcConfigEs =
            extServicesConfig.FirstOrDefault(s => s.Path.Contains(".well-known/openid-configuration"));
        oidcConfigEs.Should().NotBeNull();
        oidcConfigEs!.DomainOrService.Should().Be("localhost");

        var authServiceCode = @"
import { Injectable } from '@angular/core';
import { OAuthService } from 'angular-oauth2-oidc';

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(private oauthService: OAuthService) {}

  public async init() {
    await this.oauthService.loadDiscoveryDocumentAndTryLogin();
  }
}
";

        using var wsService = await TestWorkspace.CreateAsync(authServiceCode, "auth.service.ts");

        var extServicesService = FindNodes<ExternalServiceNode>(wsService.FileNode.Children);
        extServicesService.Should().HaveCountGreaterThan(0);

        var oidcCallEs =
            extServicesService.FirstOrDefault(s => s.Path.Contains(".well-known/openid-configuration"));
        oidcCallEs.Should().NotBeNull();
    }

    [Test]
    public async Task Test_TypeScript_SignalR()
    {
        RouteDictionaryRegistry.Clear();
        var code = @"
import * as signalR from '@microsoft/signalr';

export class RealtimeService {
  private hubConnection: signalR.HubConnection;

  public start() {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:8060/hub/games')
      .build();
  }
}
";

        using var ws = await TestWorkspace.CreateAsync(code, "realtime.service.ts");

        var extServices = FindNodes<ExternalServiceNode>(ws.FileNode.Children);
        extServices.Should().HaveCount(1);

        var hubSvc = extServices.First();
        hubSvc.Protocol.Should().Be("ws");
        hubSvc.Path.Should().Be("/hub/games");
    }

    [Test]
    public async Task Test_TypeScript_GraphQL()
    {
        RouteDictionaryRegistry.Clear();
        var code = @"
import { ApolloClient, InMemoryCache } from '@apollo/client';

const client = new ApolloClient({
  uri: 'http://localhost:8060/graphql',
  cache: new InMemoryCache()
});
";

        using var ws = await TestWorkspace.CreateAsync(code, "apollo.ts");

        var extServices = FindNodes<ExternalServiceNode>(ws.FileNode.Children);
        extServices.Should().HaveCount(1);

        var gqlSvc = extServices.First();
        gqlSvc.Path.Should().Be("/graphql");
    }

    [Test]
    public async Task Test_TypeScript_TemplateString_VariableTrace_And_CanonicalIds()
    {
        RouteDictionaryRegistry.Clear();
        var code = @"
export const CLOUDFLARE_API_BASE_URL = 'https://api.cloudflare.com/client/v4';

export class CloudflareApiClient {
  private readonly baseUrl = CLOUDFLARE_API_BASE_URL;

  async executeQuery(id: string) {
    const url = `${this.baseUrl}/accounts/12345/d1/database/${id}/query`;
    await fetch(url);
  }

  async putKv(key: string) {
    const url = `${this.baseUrl}/accounts/12345/storage/kv/values/${key}`;
    await fetch(url);
  }
}

export class TelemetryReporter {
  public static DEFAULT_HUB_URL = 'https://hub.at-systems.biz';
  public static getHubUrl(env?: any): string {
    return env?.HUB_INGEST_URL || TelemetryReporter.DEFAULT_HUB_URL;
  }

  static async report(env?: any) {
    const hubUrl = TelemetryReporter.getHubUrl(env);
    await fetch(`${hubUrl}/impression/push`);
  }
}

export async function executeImport(options: any = {}) {
  const domain = options.domain || 'at-systems.pro';
  const apiUrl = options.apiUrl || `https://bundles.${domain}`;
  await fetchTrafficBacks(apiUrl);
}

export async function fetchTrafficBacks(baseUrl: string) {
  const url = `${baseUrl.replace(/\/+$/, '')}/api/trafficbacks/`;
  await fetch(url);
}
";

        using var ws = await TestWorkspace.CreateAsync(code, "cfApi.ts");

        var extServices = FindNodes<ExternalServiceNode>(ws.FileNode.Children);

        // Verify that ExternalService IDs are canonical (no :line:col row suffix)
        foreach (var es in extServices)
        {
            es.Id.Should().NotMatchRegex(@":\d+$",
                "ExternalService ID should be canonical and not end with row number");
        }

        // Verify CF API resolution: both executeQuery and putKv should resolve to api.cloudflare.com
        var cfSvc = extServices.FirstOrDefault(s => s.DomainOrService == "api.cloudflare.com");
        cfSvc.Should().NotBeNull("Should resolve this.baseUrl to api.cloudflare.com");
        cfSvc!.Protocol.Should().Be("https");

        // Verify Hub Ingest resolution: should resolve to hub.at-systems.biz
        var hubSvc = extServices.FirstOrDefault(s => s.DomainOrService == "hub.at-systems.biz");
        hubSvc.Should().NotBeNull("Should resolve getHubUrl() to hub.at-systems.biz");
        hubSvc!.Protocol.Should().Be("https");

        // Verify Bundles resolution: should resolve options.apiUrl || https://bundles.${domain} to bundles
        var bundlesSvc = extServices.FirstOrDefault(s => s.DomainOrService == "bundles");
        bundlesSvc.Should().NotBeNull("Should resolve baseUrl to bundles");
        bundlesSvc!.Protocol.Should().Be("https");
    }

    [Test]
    public async Task Test_PhysicalParser_SkipsScratchAndDebugFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_scratch_test_" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
        Directory.CreateDirectory(tempDir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "scratch_debug.ts"), "export const x = 1;");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "temp_worker.ts"), "export const y = 2;");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "feature_debug.ts"), "export const z = 3;");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "worker.scratch.ts"), "export const w = 4;");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "main.ts"), "export const real = 5;");

            var channel = Channel.CreateUnbounded<Func<Task>>();

            await using var client = new InMemoryGraphClient();

            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            WorkspaceIndexer.AutoRegisterParsers();

            var l1Result = await new Layer1PhysicalParser().ParseAsync(ctx);
            var files = l1Result.Files;

            var fileNames = files.Select(f => f.Name).ToList();
            fileNames.Should().Contain("main.ts");
            fileNames.Should().NotContain("scratch_debug.ts");
            fileNames.Should().NotContain("temp_worker.ts");
            fileNames.Should().NotContain("feature_debug.ts");
            fileNames.Should().NotContain("worker.scratch.ts");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_Axios_BaseUrlResolution_FromClass_AndDirectFunctionCallsEndpoint()
    {
        var serviceCode = @"
import axios from 'axios';

export class IdHelperService {
  private readonly _baseUrl: string;
  private client: any;

  constructor(private readonly config: any) {
    this._baseUrl = config.get('ID_HELPER_NEST');
    this.client = axios.create({ baseURL: this._baseUrl });
  }

  async startCampaign(data: any) {
    return this.client.post('/api/v1/smart-cpa/campaigns/start', data);
  }
}
";
        var tempDir = Path.Combine(Path.GetTempPath(), "ts_axios_baseurl_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var serviceFile = Path.Combine(tempDir, "id-helper.service.ts");
            await File.WriteAllTextAsync(serviceFile, serviceCode);

            WorkspaceIndexer.AutoRegisterParsers();

            var parser = new TypeScriptParser();
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            var tree = await parser.ParseAsync(serviceFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(tree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, ctx, "listener");

            var extServices = FindNodes<ExternalServiceNode>(tree.FileNode.Children);
            extServices.Should().HaveCount(1);

            var es = extServices[0];
            es.DomainOrService.Should().Be("id_helper_nest", "BaseURL from this._baseUrl and config.get('ID_HELPER_NEST') must be resolved");
            es.Path.Should().Be("/api/v1/smart-cpa/campaigns/start");
            es.Extensions.Should().NotBeNull();
            es.Extensions!.ContainsKey("caller_symbol_id").Should().BeTrue();
            es.Extensions["caller_symbol_id"].Should().Contain("startCampaign");

            // Test LateBinding generates direct CALLS_ENDPOINT from calling function
            var targetEp = new EndpointNode(
                "helper-nest:ep:POST:/api/v1/smart-cpa/campaigns/start",
                "POST /api/v1/smart-cpa/campaigns/start",
                "src/smart-cpa/smart-cpa.controller.ts",
                "POST",
                "/api/v1/smart-cpa/campaigns/start"
            );
            var helperNestProj = new CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode(
                "integration-service-helper-nest",
                "integration-service-helper-nest",
                "src/integrations/services/helper-nest",
                "npm"
            );

            var l5Parser = new Layer5AnalysisParser();
            var isMatchMethod = typeof(Layer5AnalysisParser).GetMethod("IsMatch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, [typeof(ExternalServiceNode), typeof(EndpointNode), typeof(CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode)])!;
            var matches = (bool)isMatchMethod.Invoke(l5Parser, [es, targetEp, helperNestProj])!;
            matches.Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_Layer5_IndexedBinding_EmptyScopeId_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ts_l5_emptyscope_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            // Add raw type binding with empty ScopeId (e.g. from ColdFusion or anonymous scope)
            ctx.RawTypeBindings.Add(new RawTypeBinding("myService", "MyService", "test.ts", ""));
            ctx.AddGlobalReferences([
                new Reference("ws:sym:test.ts:Function:testCaller:1", "myService.doWork", OntologyConstants.Relationships.Calls)
            ]);

            var l5Parser = new Layer5AnalysisParser();
            var resolveMethod = typeof(Layer5AnalysisParser).GetMethod("ResolveAndUploadGlobalReferencesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            
            // Must complete without throwing ArgumentNullException (value cannot be null in String.IndexOf)
            Assert.DoesNotThrowAsync(async () =>
            {
                var rels = (List<Relationship>)await (Task<List<Relationship>>)resolveMethod.Invoke(l5Parser, [ctx])!;
                rels.Should().NotBeNull();
            });
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_GetCallChain_ResolvesShortFunctionNames()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"ce_test_callchain_{Guid.NewGuid():N}.db");
        try
        {
            using var client = new SqliteGraphClient(tempDb);
            var fn1 = new FunctionNode(
                "ws:sym:links.ts:Function:getWeightedPayoutToPostbackLinks:10",
                "getWeightedPayoutToPostbackLinks",
                "ws:sym:links.ts:Function:getWeightedPayoutToPostbackLinks:10",
                "links.ts",
                "links.ts",
                10, 20, 0, 0
            );
            var fn2 = new FunctionNode(
                "ws:sym:links.ts:Function:getPostbackLink:30",
                "getPostbackLink",
                "ws:sym:links.ts:Function:getPostbackLink:30",
                "links.ts",
                "links.ts",
                30, 40, 0, 0
            );
            await client.UploadNodesAsync([Node.FromNode(fn1), Node.FromNode(fn2)]);
            await client.UploadRelationshipsAsync([
                new Relationship(fn1.Id, fn2.Id, OntologyConstants.Relationships.Calls, [])
            ]);

            var repo = new Core.Mcp.CodeExplorerRepository(client);
            var result = await repo.GetCallChainAsync("getWeightedPayoutToPostbackLinks", "getPostbackLink", format: "json");
            
            result.Should().Contain("getWeightedPayoutToPostbackLinks");
            result.Should().Contain("getPostbackLink");
            result.Should().NotContain("No call chain found");
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    [Test]
    public async Task Test_SetGlobalPrefix_ResolvesTemplateStringWithLocalConstant()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ts_global_prefix_test_" + Guid.NewGuid().ToString("N"));
        var srcDir = Path.Combine(tempDir, "src");
        Directory.CreateDirectory(srcDir);
        try
        {
            var mainCode = @"
import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';

async function bootstrap() {
  const app = await NestFactory.create(AppModule);
  const apiVersion = 'v1';
  app.setGlobalPrefix(`api/${apiVersion}`);
  await app.listen(3000);
}
bootstrap();
";
            var controllerCode = @"
import { Controller, Post } from '@nestjs/common';

@Controller('campaigns/min-bid')
export class CampaignsController {
  @Post()
  async updateMinBid() {}
}
";
            await File.WriteAllTextAsync(Path.Combine(tempDir, "package.json"), "{\"name\": \"adsterra-integration\"}");
            await File.WriteAllTextAsync(Path.Combine(srcDir, "main.ts"), mainCode);
            await File.WriteAllTextAsync(Path.Combine(srcDir, "campaigns.controller.ts"), controllerCode);

            WorkspaceIndexer.AutoRegisterParsers();

            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            var l1 = await new Layer1PhysicalParser().ParseAsync(ctx);
            var l2 = await new Layer2ProjectParser().ParseAsync(l1, ctx);
            var l3 = await new Layer3SyntacticParser().ParseAsync(l2, ctx);

            var ctrlTree = l3.SyntaxTrees.First(t => t.FilePath.Contains("campaigns.controller.ts"));
            var endpoints = FindNodes<EndpointNode>(ctrlTree.FileNode.Children);
            endpoints.Should().HaveCount(1);

            var ep = endpoints[0];
            ep.RouteTemplate.Should().Be("/api/v1/campaigns/min-bid");
            ep.RouteTemplate.Should().NotContain("${apiVersion}");
            ep.Name.Should().Be("POST:/api/v1/campaigns/min-bid");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_Layer2_CopiedManifestName_AlignsWithRepositoryFolder()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "ts_l2_proj_test_" + Guid.NewGuid().ToString("N"));
        var repoDir = Path.Combine(tempWorkspace, "integration-service-adsterra-sc-nest");
        Directory.CreateDirectory(repoDir);
        try
        {
            // Manifest has copied template name
            await File.WriteAllTextAsync(Path.Combine(repoDir, "package.json"), "{\"name\": \"integration-service-adsterra-nest\"}");

            WorkspaceIndexer.AutoRegisterParsers();

            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempWorkspace, tempWorkspace, client, channel);

            var l1 = await new Layer1PhysicalParser().ParseAsync(ctx);
            var l2 = await new Layer2ProjectParser().ParseAsync(l1, ctx);

            l2.Projects.Should().HaveCount(1);
            var proj = l2.Projects[0];
            // Must align to actual folder name instead of stale template manifest name
            proj.Name.Should().Be("integration-service-adsterra-sc-nest");
        }
        finally
        {
            if (Directory.Exists(tempWorkspace)) Directory.Delete(tempWorkspace, true);
        }
    }

    [Test]
    public async Task Test_Adsterra_SingleRepoScan_ProjectAndEndpoints()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "adsterra-sc-nest");
        var srcDir = Path.Combine(tempWorkspace, "src");
        Directory.CreateDirectory(srcDir);
        try
        {
            var mainCode = @"
import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';

async function bootstrap() {
  const app = await NestFactory.create(AppModule);
  const apiVersion = 'v1';
  app.setGlobalPrefix(`api/${apiVersion}`);
  await app.listen(3000);
}
bootstrap();
";
            var controllerCode = @"
import { Controller, Post } from '@nestjs/common';

@Controller('campaigns/min-bid')
export class CampaignsController {
  @Post()
  async updateMinBid() {}
}
";
            await File.WriteAllTextAsync(Path.Combine(tempWorkspace, "package.json"), "{\"name\": \"integration-service-adsterra-nest\"}");
            await File.WriteAllTextAsync(Path.Combine(srcDir, "main.ts"), mainCode);
            await File.WriteAllTextAsync(Path.Combine(srcDir, "campaigns.controller.ts"), controllerCode);

            WorkspaceIndexer.AutoRegisterParsers();

            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempWorkspace, tempWorkspace, client, channel);

            var l1 = await new Layer1PhysicalParser().ParseAsync(ctx);
            var l2 = await new Layer2ProjectParser().ParseAsync(l1, ctx);
            var l3 = await new Layer3SyntacticParser().ParseAsync(l2, ctx);
            var l4 = await new Layer4SemanticParser().ParseAsync(l3, ctx);

            l2.Projects.Should().HaveCount(1);
            var proj = l2.Projects[0];
            proj.Name.Should().Be("adsterra-sc-nest");

            var endpoints = l3.SyntaxTrees.SelectMany(t => FindNodes<EndpointNode>(t.FileNode.Children)).ToList();
            endpoints.Should().HaveCount(1);
            var ep = endpoints[0];
            ep.RouteTemplate.Should().Be("/api/v1/campaigns/min-bid");
            ep.RouteTemplate.Should().NotContain("${apiVersion}");
            ep.Id.Should().Contain("adsterra-sc-nest");
            ep.Id.Should().NotContain("integration-service-adsterra-nest");
        }
        finally
        {
            if (Directory.Exists(tempWorkspace)) Directory.Delete(tempWorkspace, true);
        }
    }

    [Test]
    public async Task Test_Adsterra_NestedProjectFolder_AlignsNameAndEndpoints()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "ats_monorepo_" + Guid.NewGuid().ToString("N"));
        var nestedRepoDir = Path.Combine(tempWorkspace, "services", "adsterra-sc-nest");
        var srcDir = Path.Combine(nestedRepoDir, "src");
        Directory.CreateDirectory(srcDir);
        try
        {
            var mainCode = @"
import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';

async function bootstrap() {
  const app = await NestFactory.create(AppModule);
  const apiVersion = process.env.API_VERSION || 'v1';
  app.setGlobalPrefix(`api/${apiVersion}`);
  await app.listen(3000);
}
bootstrap();
";
            var controllerCode = @"
import { Controller, Post } from '@nestjs/common';

@Controller('campaigns/min-bid')
export class CampaignsController {
  @Post()
  async updateMinBid() {}
}
";
            // Manifest has old copied template name without '-sc-'
            await File.WriteAllTextAsync(Path.Combine(nestedRepoDir, "package.json"), "{\"name\": \"integration-service-adsterra-nest\"}");
            await File.WriteAllTextAsync(Path.Combine(srcDir, "main.ts"), mainCode);
            await File.WriteAllTextAsync(Path.Combine(srcDir, "campaigns.controller.ts"), controllerCode);

            WorkspaceIndexer.AutoRegisterParsers();

            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempWorkspace, tempWorkspace, client, channel);

            var l1 = await new Layer1PhysicalParser().ParseAsync(ctx);
            var l2 = await new Layer2ProjectParser().ParseAsync(l1, ctx);
            var l3 = await new Layer3SyntacticParser().ParseAsync(l2, ctx);

            l2.Projects.Should().HaveCount(1);
            var proj = l2.Projects[0];
            // Must align to folder name 'adsterra-sc-nest' despite being nested at services/adsterra-sc-nest
            proj.Name.Should().Be("adsterra-sc-nest");

            var endpoints = l3.SyntaxTrees.SelectMany(t => FindNodes<EndpointNode>(t.FileNode.Children)).ToList();
            endpoints.Should().HaveCount(1);
            var ep = endpoints[0];
            ep.RouteTemplate.Should().Be("/api/v1/campaigns/min-bid");
            ep.RouteTemplate.Should().NotContain("${apiVersion}");
            ep.Id.Should().Contain("adsterra-sc-nest");
        }
        finally
        {
            if (Directory.Exists(tempWorkspace)) Directory.Delete(tempWorkspace, true);
        }
    }

    [Test]
    public async Task Test_Adsterra_DoubleQuoteOrUnexpanded_ResolvesToV1()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "adsterra_quote_test_" + Guid.NewGuid().ToString("N"));
        var srcDir = Path.Combine(tempWorkspace, "src");
        Directory.CreateDirectory(srcDir);
        try
        {
            var mainCode = @"
import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';

async function bootstrap() {
  const app = await NestFactory.create(AppModule);
  app.setGlobalPrefix('api/${apiVersion}');
  await app.listen(3000);
}
bootstrap();
";
            var controllerCode = @"
import { Controller, Post } from '@nestjs/common';

@Controller('campaigns/min-bid')
export class CampaignsController {
  @Post()
  async updateMinBid() {}
}
";
            await File.WriteAllTextAsync(Path.Combine(tempWorkspace, "package.json"), "{\"name\": \"adsterra-sc-nest\"}");
            await File.WriteAllTextAsync(Path.Combine(srcDir, "main.ts"), mainCode);
            await File.WriteAllTextAsync(Path.Combine(srcDir, "campaigns.controller.ts"), controllerCode);

            WorkspaceIndexer.AutoRegisterParsers();

            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempWorkspace, tempWorkspace, client, channel);

            var l1 = await new Layer1PhysicalParser().ParseAsync(ctx);
            var l2 = await new Layer2ProjectParser().ParseAsync(l1, ctx);
            var l3 = await new Layer3SyntacticParser().ParseAsync(l2, ctx);

            var endpoints = l3.SyntaxTrees.SelectMany(t => FindNodes<EndpointNode>(t.FileNode.Children)).ToList();
            endpoints.Should().HaveCount(1);
            var ep = endpoints[0];
            ep.RouteTemplate.Should().Be("/api/v1/campaigns/min-bid");
            ep.RouteTemplate.Should().NotContain("${apiVersion}");
        }
        finally
        {
            if (Directory.Exists(tempWorkspace)) Directory.Delete(tempWorkspace, true);
        }
    }

    [Test]
    public async Task Test_Angular_RxResource_ArrowFunction_CallerProvenance()
    {
        var code = @"
import { HttpClient } from '@angular/common/http';
import { rxResource } from '@angular/core';

export class TrackerApiService {
  constructor(private http: HttpClient) {}

  readonly cpmStats = rxResource({
    loader: () => this.http.get('https://bff.at-systems.biz/cpm-stats')
  });
}
";
        using var ws = await TestWorkspace.CreateAsync(code);

        var functions = FindNodes<FunctionNode>(ws.FileNode.Children);
        var loaderFunc = functions.FirstOrDefault(f => f.Name == "cpmStats.loader");
        loaderFunc.Should().NotBeNull("Expected 'cpmStats.loader' to be registered as a Function symbol");

        var extServices = FindNodes<ExternalServiceNode>(ws.FileNode.Children);
        extServices.Should().NotBeEmpty();

        var bffService = extServices.FirstOrDefault(e => e.Name.Contains("bff.at-systems.biz"));
        bffService.Should().NotBeNull();
        bffService!.Path.Should().Contain("cpm-stats");
        bffService.Extensions.Should().NotBeNull();
        bffService.Extensions!.ContainsKey("caller_symbol_id").Should().BeTrue();
        bffService.Extensions["caller_symbol_id"].Should().Contain("cpmStats.loader");
    }

    [Test]
    public async Task Test_CallLiteral_ExcludesDecoratorsLoggersConfigAndQueryBuilders()
    {
        var code = @"
import { Controller, Get, Param } from '@nestjs/common';

@Controller('campaigns')
export class CampaignsController {
  @Get(':id')
  async getCampaign(@Param('id') id: string) {
    clog('fetching campaign');
    config.getOrThrow('DATABASE_URL');
    dataSource.createQueryBuilder('c');
    orderService.processOrder('HARDCODED_BUSINESS_STATUS');
  }
}
";
        using var ws = await TestWorkspace.CreateAsync(code);

        var literals = FindNodes<CallLiteralNode>(ws.FileNode.Children);

        // Decorators @Controller, @Get and @Param are classified into Routing and ParamBinding roles
        var controllerLit = literals.FirstOrDefault(l => l.Value == "campaigns");
        controllerLit.Should().NotBeNull();
        controllerLit!.Role.Should().Be(CallSemanticRole.Routing);
        controllerLit.IsDecorator.Should().BeTrue();

        var getLit = literals.FirstOrDefault(l => l.Value == ":id");
        getLit.Should().NotBeNull();
        getLit!.Role.Should().Be(CallSemanticRole.Routing);
        getLit.IsDecorator.Should().BeTrue();

        var paramLit = literals.FirstOrDefault(l => l.Value == "id");
        paramLit.Should().NotBeNull();
        paramLit!.Role.Should().Be(CallSemanticRole.ParamBinding);
        paramLit.IsDecorator.Should().BeTrue();

        // Logger, Config and QueryBuilder literals are classified into their respective roles
        var logLit = literals.FirstOrDefault(l => l.Value == "fetching campaign");
        logLit.Should().NotBeNull();
        logLit!.Role.Should().Be(CallSemanticRole.Logging);

        var configLit = literals.FirstOrDefault(l => l.Value == "DATABASE_URL");
        configLit.Should().NotBeNull();
        configLit!.Role.Should().Be(CallSemanticRole.Config);

        var qbLit = literals.FirstOrDefault(l => l.Value == "c");
        qbLit.Should().NotBeNull();
        qbLit!.Role.Should().Be(CallSemanticRole.QueryBuilder);

        // Only the legitimate business argument literal is classified as Business
        var businessLiterals = literals.Where(l => l.Role == CallSemanticRole.Business).ToList();
        businessLiterals.Should().HaveCount(1);
        businessLiterals[0].Value.Should().Be("HARDCODED_BUSINESS_STATUS");
        businessLiterals[0].Callee.Should().Contain("processOrder");
    }

    [Test]
    public async Task Test_SemanticClassification_FormAccess_Storage_WebAPI_TestId_State()
    {
        var code = @"
export class SampleComponent {
  private count = signal('default_state');

  submit() {
    const email = this.form.get('email');
    const token = localStorage.getItem('auth_token');
    window.open('https://example.com/help');
    const el = menuTestId('navbar_menu_id');
    this.orderService.doWork('VALID_BUSINESS_ID');
  }
}
";
        using var ws = await TestWorkspace.CreateAsync(code);
        var literals = FindNodes<CallLiteralNode>(ws.FileNode.Children);

        var formLit = literals.FirstOrDefault(l => l.Value == "email");
        formLit.Should().NotBeNull();
        formLit!.Role.Should().Be(CallSemanticRole.FormAccess);

        var storageLit = literals.FirstOrDefault(l => l.Value == "auth_token");
        storageLit.Should().NotBeNull();
        storageLit!.Role.Should().Be(CallSemanticRole.Storage);

        var webApiLit = literals.FirstOrDefault(l => l.Value == "https://example.com/help");
        webApiLit.Should().NotBeNull();
        webApiLit!.Role.Should().Be(CallSemanticRole.WebAPI);

        var testIdLit = literals.FirstOrDefault(l => l.Value == "navbar_menu_id");
        testIdLit.Should().NotBeNull();
        testIdLit!.Role.Should().Be(CallSemanticRole.TestIdentifier);

        var stateLit = literals.FirstOrDefault(l => l.Value == "default_state");
        stateLit.Should().NotBeNull();
        stateLit!.Role.Should().Be(CallSemanticRole.State);

        var businessLits = literals.Where(l => l.Role == CallSemanticRole.Business).ToList();
        businessLits.Should().HaveCount(1);
        businessLits[0].Value.Should().Be("VALID_BUSINESS_ID");
    }

    [Test]
    public async Task Test_ExportConst_ResolvedInFetchCall()
    {
        var code = @"
export const DEFAULT_CATALOG_URL = 'http://localhost:3000';

export async function importScenarios() {
  const response = await fetch(`${DEFAULT_CATALOG_URL}/api/v1/config/scenarios`);
  return response.json();
}
";
        using var ws = await TestWorkspace.CreateAsync(code);
        var extServices = FindNodes<ExternalServiceNode>(ws.FileNode.Children);
        extServices.Should().HaveCount(1);
        var es = extServices[0];
        es.DomainOrService.Should().Be("localhost");
        es.Path.Should().Be("/api/v1/config/scenarios");
        es.Extensions?["is_internal"].Should().Be("true");
    }
}
