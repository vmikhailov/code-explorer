using System.Threading.Channels;
using AwesomeAssertions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.Rust;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class RustSemanticLibraryTests
{
    private static async Task<IOntologyNode> ParseRustSnippetAsync(string code, string? fileName = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_rust_lib_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, fileName ?? "main.rs");
        await File.WriteAllTextAsync(filePath, code);

        var channel = Channel.CreateUnbounded<Func<Task>>();
        await using var client = new InMemoryGraphClient();
        var ctx = new ParsingContext(tempDir, tempDir, client, channel);
        var parser = new RustParser();

        using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        try
        {
            Directory.Delete(tempDir, true);
        }
        catch { }

        return syntaxTree.FileNode!;
    }

    private static List<T> FindNodes<T>(IEnumerable<IOntologyNode> nodes) where T : IOntologyNode
    {
        var result = new List<T>();
        foreach (var node in nodes)
        {
            if (node is T match)
                result.Add(match);
            if (node.Children.Count > 0)
                result.AddRange(FindNodes<T>(node.Children));
        }
        return result;
    }

    [Test]
    public async Task Axum_RouterRoutesAndHandlers_ExtractsEndpointsAndTriggers()
    {
        var code = """
            use axum::{routing::{get, post, delete}, Router};

            async fn list_users() {}
            async fn create_user() {}
            async fn delete_user() {}

            pub fn app() -> Router {
                Router::new()
                    .route("/users", get(list_users))
                    .route("/users", post(create_user))
                    .route("/users/:id", delete(delete_user))
            }
            """;

        var root = await ParseRustSnippetAsync(code);
        var endpoints = FindNodes<EndpointNode>(root.Children);

        endpoints.Should().HaveCount(3);
        var getEp = endpoints.FirstOrDefault(e => e.HttpMethod == "GET" && e.RouteTemplate == "/users");
        getEp.Should().NotBeNull();
        getEp!.References.Any(r => r.Kind == "TRIGGERS" && r.TargetName == "list_users").Should().BeTrue();

        var postEp = endpoints.FirstOrDefault(e => e.HttpMethod == "POST" && e.RouteTemplate == "/users");
        postEp.Should().NotBeNull();
        postEp!.References.Any(r => r.Kind == "TRIGGERS" && r.TargetName == "create_user").Should().BeTrue();

        var deleteEp = endpoints.FirstOrDefault(e => e.HttpMethod == "DELETE" && e.RouteTemplate == "/users/:id");
        deleteEp.Should().NotBeNull();
        deleteEp!.References.Any(r => r.Kind == "TRIGGERS" && r.TargetName == "delete_user").Should().BeTrue();
    }

    [Test]
    public async Task ActixWeb_Attributes_ExtractsEndpoints()
    {
        var code = """
            use actix_web::{get, post, HttpResponse, Responder};

            #[get("/api/items")]
            async fn get_items() -> impl Responder {
                HttpResponse::Ok()
            }

            #[post("/api/items")]
            async fn create_item() -> impl Responder {
                HttpResponse::Created()
            }
            """;

        var root = await ParseRustSnippetAsync(code);
        var endpoints = FindNodes<EndpointNode>(root.Children);

        endpoints.Should().HaveCount(2);
        endpoints.Any(e => e.HttpMethod == "GET" && e.RouteTemplate == "/api/items").Should().BeTrue();
        endpoints.Any(e => e.HttpMethod == "POST" && e.RouteTemplate == "/api/items").Should().BeTrue();
    }

    [Test]
    public async Task Rocket_Attributes_ExtractsEndpoints()
    {
        var code = """
            #[get("/rockets")]
            fn list_rockets() -> &'static str {
                "rockets"
            }

            #[post("/rockets")]
            fn launch_rocket() -> &'static str {
                "launched"
            }
            """;

        var root = await ParseRustSnippetAsync(code);
        var endpoints = FindNodes<EndpointNode>(root.Children);

        endpoints.Should().HaveCount(2);
        endpoints.Any(e => e.HttpMethod == "GET" && e.RouteTemplate == "/rockets").Should().BeTrue();
        endpoints.Any(e => e.HttpMethod == "POST" && e.RouteTemplate == "/rockets").Should().BeTrue();
    }

    [Test]
    public async Task Tonic_ServerAndClient_ExtractsGrpcEndpointAndClient()
    {
        var code = """
            use tonic::{Request, Response, Status};

            #[tonic::async_trait]
            impl Greeter for MyGreeter {
                async fn say_hello(&self, request: Request<HelloRequest>) -> Result<Response<HelloReply>, Status> {
                    Ok(Response::new(HelloReply::default()))
                }
            }

            pub async fn client_call() -> Result<(), Box<dyn std::error::Error>> {
                let mut client = GreeterClient::connect("http://127.0.0.1:50051").await?;
                Ok(())
            }
            """;

        var root = await ParseRustSnippetAsync(code);
        var endpoints = FindNodes<EndpointNode>(root.Children);
        var externalServices = FindNodes<ExternalServiceNode>(root.Children);

        endpoints.Any(e => e.Protocol == "gRPC" && e.Name.Contains("say_hello")).Should().BeTrue();
        externalServices.Any(es => es.DomainOrService.Contains("127.0.0.1") || es.Name.Contains("GreeterClient")).Should().BeTrue();
    }

    [Test]
    public async Task Reqwest_OutboundClientCalls_ExtractsExternalServices()
    {
        var code = """
            pub async fn fetch_data() -> Result<(), reqwest::Error> {
                let client = reqwest::Client::new();
                let res = client.get("https://api.stripe.com/v1/charges")
                    .send()
                    .await?;

                let res2 = reqwest::get("https://api.github.com/repos").await?;
                Ok(())
            }
            """;

        var root = await ParseRustSnippetAsync(code);
        var externalServices = FindNodes<ExternalServiceNode>(root.Children);

        externalServices.Should().HaveCountGreaterThanOrEqualTo(2);
        externalServices.Any(es => es.DomainOrService == "api.stripe.com" || es.Name.Contains("api.stripe.com")).Should().BeTrue();
        externalServices.Any(es => es.DomainOrService == "api.github.com" || es.Name.Contains("api.github.com")).Should().BeTrue();
    }

    [Test]
    public async Task Hyper_RequestBuilder_ExtractsExternalService()
    {
        var code = """
            use hyper::Request;

            pub fn build_request() {
                let req = Request::builder()
                    .uri("https://example.com/api/v1/health")
                    .body(())
                    .unwrap();
            }
            """;

        var root = await ParseRustSnippetAsync(code);
        var externalServices = FindNodes<ExternalServiceNode>(root.Children);

        externalServices.Any(es => es.DomainOrService == "example.com" || es.Name.Contains("example.com")).Should().BeTrue();
    }

    [Test]
    public async Task Sqlx_QueryMacrosAndCalls_ExtractsQueryAndTableNodes()
    {
        var code = """
            pub async fn get_user(pool: &PgPool, user_id: i64) {
                let user = sqlx::query!(
                    "SELECT id, name, email FROM users WHERE id = $1 AND active = true",
                    user_id
                ).fetch_one(pool).await;

                let _ = sqlx::query("INSERT INTO orders (user_id, total) VALUES ($1, $2)")
                    .execute(pool).await;
            }
            """;

        var root = await ParseRustSnippetAsync(code);
        var queries = FindNodes<QueryNode>(root.Children);
        var tables = FindNodes<TableNode>(root.Children);

        queries.Should().NotBeEmpty();
        queries.Any(q => q.QueryText.Contains("SELECT") && q.QueryText.Contains("users")).Should().BeTrue();

        tables.Any(t => t.Name == "users").Should().BeTrue();
        tables.Any(t => t.Name == "orders").Should().BeTrue();
    }

    [Test]
    public async Task SeaOrm_DeriveEntityModel_ExtractsTableNode()
    {
        var code = """
            use sea_orm::entity::prelude::*;

            #[derive(Clone, Debug, PartialEq, DeriveEntityModel)]
            #[sea_orm(table_name = "customers", schema_name = "sales")]
            pub struct Model {
                #[sea_orm(primary_key)]
                pub id: i32,
                pub name: String,
            }
            """;

        var root = await ParseRustSnippetAsync(code);
        var tables = FindNodes<TableNode>(root.Children);

        tables.Should().NotBeEmpty();
        var customerTable = tables.FirstOrDefault(t => t.Name == "customers");
        customerTable.Should().NotBeNull();
        customerTable!.Extensions?.GetValueOrDefault("schema_name").Should().Be("sales");
    }

    [Test]
    public async Task Diesel_TableMacro_ExtractsTableNode()
    {
        var code = """
            table! {
                orders (id) {
                    id -> Integer,
                    amount -> Double,
                }
            }
            """;

        var root = await ParseRustSnippetAsync(code);
        var tables = FindNodes<TableNode>(root.Children);

        tables.Should().NotBeEmpty();
        tables.Any(t => t.Name == "orders").Should().BeTrue();
    }

    [Test]
    public async Task RdKafka_ProducerAndConsumer_ExtractsTopicNodes()
    {
        var code = """
            pub async fn send_event(producer: &FutureProducer) {
                let record = FutureRecord::to("order-events")
                    .payload("test");
                let _ = producer.send(record, 0).await;
            }

            pub fn setup_consumer(consumer: &StreamConsumer) {
                consumer.subscribe(&["order-events"]).unwrap();
            }
            """;

        var root = await ParseRustSnippetAsync(code);
        var topics = FindNodes<TopicNode>(root.Children);

        topics.Should().NotBeEmpty();
        var orderEvents = topics.FirstOrDefault(t => t.Name == "order-events");
        orderEvents.Should().NotBeNull();
        orderEvents!.BrokerType.Should().Be("kafka");
    }

    [Test]
    public async Task Lapin_BasicPublishAndConsume_ExtractsTopicNodes()
    {
        var code = """
            pub async fn publish(channel: &Channel) {
                channel.basic_publish(
                    "exchange",
                    "orders.created",
                    BasicPublishOptions::default(),
                    b"payload",
                    BasicProperties::default(),
                ).await.unwrap();
            }
            """;

        var root = await ParseRustSnippetAsync(code);
        var topics = FindNodes<TopicNode>(root.Children);

        topics.Should().NotBeEmpty();
        var topic = topics.FirstOrDefault(t => t.Name == "orders.created");
        topic.Should().NotBeNull();
        topic!.BrokerType.Should().Be("rabbitmq");
    }

    [Test]
    public async Task AsyncNats_PublishAndSubscribe_ExtractsTopicNodes()
    {
        var code = """
            pub async fn publish_event(client: &async_nats::Client) {
                client.publish("telemetry.v1", "payload".into()).await.unwrap();
            }
            """;

        var root = await ParseRustSnippetAsync(code);
        var topics = FindNodes<TopicNode>(root.Children);

        topics.Should().NotBeEmpty();
        var topic = topics.FirstOrDefault(t => t.Name == "telemetry.v1");
        topic.Should().NotBeNull();
        topic!.BrokerType.Should().Be("nats");
    }
}
