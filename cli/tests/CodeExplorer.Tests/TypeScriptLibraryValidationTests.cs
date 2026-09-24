using System.Threading.Channels;
using NUnit.Framework;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.TypeScript;
using CodeExplorer.Tests.Shared;
using TreeSitter;

namespace CodeExplorer.Tests;

[TestFixture]
public class TypeScriptLibraryValidationTests
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
        Assert.That(queries, Is.Not.Empty);

        var selectQuery = queries.FirstOrDefault(q => q.Name.Contains("SELECT") && q.Name.Contains("users"));
        Assert.That(selectQuery, Is.Not.Null);
        var selectDeps = selectQuery!.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName).ToList();
        Assert.That(selectDeps, Contains.Item("users"));

        var insertQuery = queries.FirstOrDefault(q => q.Name.Contains("INSERT") && q.Name.Contains("audit_logs"));
        Assert.That(insertQuery, Is.Not.Null);
        var insertDeps = insertQuery!.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName).ToList();
        Assert.That(insertDeps, Contains.Item("audit_logs"));
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
        Assert.That(queries, Is.Not.Empty);

        var selectQuery = queries.FirstOrDefault(q => (q.Name.Contains("accounts") || q.QueryText.Contains("accounts")) && (q.Name.Contains("SELECT") || q.QueryText.Contains("SELECT")));
        Assert.That(selectQuery, Is.Not.Null);
        Assert.That(selectQuery!.References.Any(r => r.TargetName == "accounts" && r.Kind == "DEPENDS_ON"), Is.True);

        var updateQuery = queries.FirstOrDefault(q => (q.Name.Contains("orders") || q.QueryText.Contains("orders")) && (q.Name.Contains("UPDATE") || q.QueryText.Contains("UPDATE")));
        Assert.That(updateQuery, Is.Not.Null);
        Assert.That(updateQuery!.References.Any(r => r.TargetName == "orders" && r.Kind == "DEPENDS_ON"), Is.True);
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
        Assert.That(queries, Is.Not.Empty);

        var selectQuery = queries.FirstOrDefault(q => q.Name.Contains("SELECT") && q.Name.Contains("tasks"));
        Assert.That(selectQuery, Is.Not.Null);
        Assert.That(selectQuery!.References.Any(r => r.TargetName == "tasks"), Is.True);

        var deleteQuery = queries.FirstOrDefault(q => q.Name.Contains("DELETE") && q.Name.Contains("expired_tokens"));
        Assert.That(deleteQuery, Is.Not.Null);
        Assert.That(deleteQuery!.References.Any(r => r.TargetName == "expired_tokens"), Is.True);
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
        Assert.That(queries, Is.Not.Empty);

        var knexTableQuery = queries.FirstOrDefault(q => q.Name.Contains("products"));
        Assert.That(knexTableQuery, Is.Not.Null);
        Assert.That(knexTableQuery!.References.Any(r => r.TargetName == "products" && r.Kind == "USES_DB"), Is.True);

        var rawQuery = queries.FirstOrDefault(q => q.Name.Contains("SELECT") && q.Name.Contains("system_events"));
        Assert.That(rawQuery, Is.Not.Null);
        Assert.That(rawQuery!.References.Any(r => r.TargetName == "system_events" && r.Kind == "DEPENDS_ON"), Is.True);
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
        Assert.That(queries, Is.Not.Empty);

        var findQuery = queries.FirstOrDefault(q => q.Name.Contains("find"));
        Assert.That(findQuery, Is.Not.Null);
        Assert.That(findQuery!.Name, Does.Contain("TypeORM"));

        var rawQuery = queries.FirstOrDefault(q => (q.Name.Contains("SELECT") || q.QueryText.Contains("SELECT")) && (q.Name.Contains("transactions") || q.QueryText.Contains("transactions")));
        Assert.That(rawQuery, Is.Not.Null);
        Assert.That(rawQuery!.References.Any(r => r.TargetName == "transactions"), Is.True);
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
        Assert.That(queries, Is.Not.Empty);

        var findAllQuery = queries.FirstOrDefault(q => q.Name.Contains("User.findAll"));
        Assert.That(findAllQuery, Is.Not.Null);
        Assert.That(findAllQuery!.References.Any(r => r.TargetName == "User" && r.Kind == "USES_DB"), Is.True);

        var rawQuery = queries.FirstOrDefault(q => q.Name.Contains("SELECT") && q.Name.Contains("passwords"));
        Assert.That(rawQuery, Is.Not.Null);
        Assert.That(rawQuery!.References.Any(r => r.TargetName == "passwords"), Is.True);
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
        Assert.That(queries, Is.Not.Empty);

        var userFind = queries.FirstOrDefault(q => q.Name.Contains("user.findMany"));
        Assert.That(userFind, Is.Not.Null);
        Assert.That(userFind!.References.Any(r => r.TargetName == "user" && r.Kind == "USES_DB"), Is.True);

        var orderCreate = queries.FirstOrDefault(q => q.Name.Contains("order.create"));
        Assert.That(orderCreate, Is.Not.Null);
        Assert.That(orderCreate!.References.Any(r => r.TargetName == "order" && r.Kind == "USES_DB"), Is.True);
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
        Assert.That(queries, Is.Not.Empty);

        var rawQuery = queries.FirstOrDefault(q => (q.Name.Contains("metrics_log") || q.QueryText.Contains("metrics_log")) && q.References.Any(r => r.TargetName == "metrics_log"));
        Assert.That(rawQuery, Is.Not.Null);
        Assert.That(rawQuery!.References.Any(r => r.TargetName == "metrics_log" && r.Kind == "DEPENDS_ON"), Is.True);
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
        Assert.That(queries, Is.Not.Empty);

        var selectOp = queries.FirstOrDefault(q => q.Name.Contains("usersTable"));
        Assert.That(selectOp, Is.Not.Null);
        Assert.That(selectOp!.References.Any(r => r.TargetName == "usersTable" && r.Kind == "USES_DB"), Is.True);

        var insertOp = queries.FirstOrDefault(q => q.Name.Contains("ordersTable"));
        Assert.That(insertOp, Is.Not.Null);
        Assert.That(insertOp!.References.Any(r => r.TargetName == "ordersTable" && r.Kind == "USES_DB"), Is.True);
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
        Assert.That(queries, Is.Not.Empty);

        var q = queries.FirstOrDefault(q => q.Name.Contains("audit_trail") || q.QueryText.Contains("audit_trail"));
        Assert.That(q, Is.Not.Null);
        Assert.That(q!.References.Any(r => r.TargetName == "audit_trail" && r.Kind == "DEPENDS_ON"), Is.True);
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
        Assert.That(queries, Is.Not.Empty);

        var findQuery = queries.FirstOrDefault(q => q.Name.Contains("customers.find"));
        Assert.That(findQuery, Is.Not.Null);

        var updateQuery = queries.FirstOrDefault(q => q.Name.Contains("orders.updateOne"));
        Assert.That(updateQuery, Is.Not.Null);
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
        Assert.That(queries, Is.Not.Empty);

        var searchOp = queries.FirstOrDefault(q => q.Name.Contains("articles"));
        Assert.That(searchOp, Is.Not.Null);
        Assert.That(searchOp!.Name, Is.EqualTo("Elasticsearch: search (articles)"));

        var indexOp = queries.FirstOrDefault(q => q.Name.Contains("server_logs"));
        Assert.That(indexOp, Is.Not.Null);
        Assert.That(indexOp!.Name, Is.EqualTo("Elasticsearch: index (server_logs)"));
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
        Assert.That(queries, Is.Not.Empty);

        var neoQuery = queries.FirstOrDefault(q => q.Name.Contains("Neo4j"));
        Assert.That(neoQuery, Is.Not.Null);
        Assert.That(neoQuery!.Name, Does.Contain("MATCH"));
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
        Assert.That(queries, Is.Not.Empty);

        var queryCall = queries.FirstOrDefault(q => q.Name.Contains("queryRows"));
        Assert.That(queryCall, Is.Not.Null);

        var writeCall = queries.FirstOrDefault(q => q.Name.Contains("writePoint"));
        Assert.That(writeCall, Is.Not.Null);
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
        Assert.That(endpoints, Has.Count.EqualTo(2));

        var getEp = endpoints.FirstOrDefault(e => e.HttpMethod == "GET");
        Assert.That(getEp, Is.Not.Null);
        Assert.That(getEp!.RouteTemplate, Is.EqualTo("/api/v1/health"));

        var postEp = endpoints.FirstOrDefault(e => e.HttpMethod == "POST");
        Assert.That(postEp, Is.Not.Null);
        Assert.That(postEp!.RouteTemplate, Is.EqualTo("/api/v1/users"));
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
        Assert.That(endpoints, Has.Count.EqualTo(1));

        var ep = endpoints[0];
        Assert.That(ep.HttpMethod, Is.EqualTo("DELETE"));
        Assert.That(ep.RouteTemplate, Is.EqualTo("/api/v1/items/:id"));
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
        Assert.That(endpoints, Has.Count.EqualTo(2));

        var getEp = endpoints.FirstOrDefault(e => e.HttpMethod == "GET");
        Assert.That(getEp, Is.Not.Null);
        Assert.That(getEp!.RouteTemplate, Is.EqualTo("/users/:id"));

        var postEp = endpoints.FirstOrDefault(e => e.HttpMethod == "POST");
        Assert.That(postEp, Is.Not.Null);
        Assert.That(postEp!.RouteTemplate, Is.EqualTo("/checkout"));
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
        Assert.That(endpoints, Has.Count.EqualTo(2));

        var getEp = endpoints.FirstOrDefault(e => e.HttpMethod == "GET");
        Assert.That(getEp, Is.Not.Null);

        var postEp = endpoints.FirstOrDefault(e => e.HttpMethod == "POST");
        Assert.That(postEp, Is.Not.Null);
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
        Assert.That(extServices, Has.Count.EqualTo(1));
        Assert.That(extServices[0].Name, Is.EqualTo("api.paymentservice.com"));

        var refs = FindAllReferences(ws.FileNode);
        Assert.That(refs.Any(r => r.TargetName == "api.paymentservice.com" && r.Kind == "CALLS"), Is.True);
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
        Assert.That(extServices, Has.Count.EqualTo(1));
        Assert.That(extServices[0].Name, Is.EqualTo("inventory.internal.net"));

        var refs = FindAllReferences(ws.FileNode);
        Assert.That(refs.Any(r => r.TargetName == "inventory.internal.net" && r.Kind == "CALLS"), Is.True);
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
        Assert.That(pubRef, Is.Not.Null, "Should capture Kafka publisher reference");

        var subRef = refs.FirstOrDefault(r => r.TargetName == "kafka:payment-events" && r.Kind == "SUBSCRIBES_TO");
        Assert.That(subRef, Is.Not.Null, "Should capture Kafka consumer reference");
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
        Assert.That(subRef, Is.Not.Null, "Should capture BullMQ worker subscription");

        var pubRef = refs.FirstOrDefault(r => r.TargetName.Contains("send-invoice") && r.Kind == "PUBLISHES_TO");
        Assert.That(pubRef, Is.Not.Null, "Should capture BullMQ queue.add job publishing");
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
        Assert.That(endpoints, Has.Count.EqualTo(2));

        var patchEp = endpoints.FirstOrDefault(e => e.HttpMethod == "PATCH");
        Assert.That(patchEp, Is.Not.Null);
        Assert.That(patchEp!.RouteTemplate, Is.EqualTo("/api/v1/users/:id"));

        var deleteEp = endpoints.FirstOrDefault(e => e.HttpMethod == "DELETE");
        Assert.That(deleteEp, Is.Not.Null);
        Assert.That(deleteEp!.RouteTemplate, Is.EqualTo("/api/v1/sessions"));
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
        Assert.That(endpoints, Has.Count.EqualTo(3));

        var controllerEp = endpoints.FirstOrDefault(e => e.RouteTemplate == "/users");
        Assert.That(controllerEp, Is.Not.Null);

        var getEp = endpoints.FirstOrDefault(e => e.HttpMethod == "GET" && e.RouteTemplate == "/users/all");
        Assert.That(getEp, Is.Not.Null);

        var postEp = endpoints.FirstOrDefault(e => e.HttpMethod == "POST" && e.RouteTemplate == "/users/create");
        Assert.That(postEp, Is.Not.Null);
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
        Assert.That(queries, Is.Not.Empty);

        var q = queries.FirstOrDefault(q => q.Name.Contains("accounts") || q.QueryText.Contains("accounts"));
        Assert.That(q, Is.Not.Null);
        Assert.That(q!.References.Any(r => r.TargetName == "accounts" && r.Kind == "DEPENDS_ON"), Is.True);
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
        Assert.That(extServices, Has.Count.EqualTo(2));

        var gamesSvc = extServices.FirstOrDefault(s => s.Path == "/api/v1/games");
        Assert.That(gamesSvc, Is.Not.Null);
        Assert.That(gamesSvc!.DomainOrService, Is.EqualTo("localhost"));

        var profileSvc = extServices.FirstOrDefault(s => s.Path == "/api/v1/profiles");
        Assert.That(profileSvc, Is.Not.Null);
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
        Assert.That(extServices, Has.Count.EqualTo(3));

        var gamesSvc = extServices.FirstOrDefault(s => s.Path == "/api/v1/games");
        Assert.That(gamesSvc, Is.Not.Null);

        var playerSvc = extServices.FirstOrDefault(s => s.Path == "/api/v1/profiles");
        Assert.That(playerSvc, Is.Not.Null);

        var userSvc = extServices.FirstOrDefault(s => s.Path.EndsWith("/api/v1/users"));
        Assert.That(userSvc, Is.Not.Null);
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
        Assert.That(extServices, Has.Count.EqualTo(2));

        var gamesSvc = extServices.FirstOrDefault(s => s.Path == "/api/v1/games");
        Assert.That(gamesSvc, Is.Not.Null);

        var playerSvc = extServices.FirstOrDefault(s => s.Path == "/api/v1/profiles");
        Assert.That(playerSvc, Is.Not.Null);
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
        Assert.That(extServicesConfig, Has.Count.GreaterThan(0));

        var oidcConfigEs = extServicesConfig.FirstOrDefault(s => s.Path.Contains(".well-known/openid-configuration"));
        Assert.That(oidcConfigEs, Is.Not.Null);
        Assert.That(oidcConfigEs!.DomainOrService, Is.EqualTo("localhost"));

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
        Assert.That(extServicesService, Has.Count.GreaterThan(0));
        var oidcCallEs = extServicesService.FirstOrDefault(s => s.Path.Contains(".well-known/openid-configuration"));
        Assert.That(oidcCallEs, Is.Not.Null);
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
        Assert.That(extServices, Has.Count.EqualTo(1));

        var hubSvc = extServices.First();
        Assert.That(hubSvc.Protocol, Is.EqualTo("ws"));
        Assert.That(hubSvc.Path, Is.EqualTo("/hub/games"));
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
        Assert.That(extServices, Has.Count.EqualTo(1));

        var gqlSvc = extServices.First();
        Assert.That(gqlSvc.Path, Is.EqualTo("/graphql"));
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
            Assert.That(es.Id, Does.Not.Match(@":\d+$"), "ExternalService ID should be canonical and not end with row number");
        }

        // Verify CF API resolution: both executeQuery and putKv should resolve to api.cloudflare.com
        var cfSvc = extServices.FirstOrDefault(s => s.DomainOrService == "api.cloudflare.com");
        Assert.That(cfSvc, Is.Not.Null, "Should resolve this.baseUrl to api.cloudflare.com");
        Assert.That(cfSvc!.Protocol, Is.EqualTo("https"));

        // Verify Hub Ingest resolution: should resolve to hub.at-systems.biz
        var hubSvc = extServices.FirstOrDefault(s => s.DomainOrService == "hub.at-systems.biz");
        Assert.That(hubSvc, Is.Not.Null, "Should resolve getHubUrl() to hub.at-systems.biz");
        Assert.That(hubSvc!.Protocol, Is.EqualTo("https"));

        // Verify Bundles resolution: should resolve options.apiUrl || https://bundles.${domain} to bundles
        var bundlesSvc = extServices.FirstOrDefault(s => s.DomainOrService == "bundles");
        Assert.That(bundlesSvc, Is.Not.Null, "Should resolve baseUrl to bundles");
        Assert.That(bundlesSvc!.Protocol, Is.EqualTo("https"));
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

            var channel = System.Threading.Channels.Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            var l1Result = await new CodeExplorer.Core.Parser.Layers.Layer1PhysicalParser().ParseAsync(ctx);
            var files = l1Result.Files;

            var fileNames = files.Select(f => f.Name).ToList();
            Assert.That(fileNames, Does.Contain("main.ts"));
            Assert.That(fileNames, Does.Not.Contain("scratch_debug.ts"));
            Assert.That(fileNames, Does.Not.Contain("temp_worker.ts"));
            Assert.That(fileNames, Does.Not.Contain("feature_debug.ts"));
            Assert.That(fileNames, Does.Not.Contain("worker.scratch.ts"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
