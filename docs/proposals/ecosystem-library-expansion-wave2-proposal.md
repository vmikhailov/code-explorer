# Proposal: Ecosystem Library Expansion Wave 2 (Next 60 Polyglot Libraries)

**Status:** Complete (All 4 phases / 60 libraries shipped on `DeclarativeSemanticExtension`, `v1.74.0`+)  
**Created:** 2026-11-07  
**Target Layer:** Layer 4 (Semantic Structure) & Layer 2 (Host Components)  
**Related Proposals:** [`semantic-archetypes-and-yaml-projections-proposal.md`](./semantic-archetypes-and-yaml-projections-proposal.md), [`uast-performance-and-loop-semantics-proposal.md`](./uast-performance-and-loop-semantics-proposal.md)

---

## 1. Executive Summary

Following the delivery of Wave 1 (`v1.73.0`), which added 59 framework and library parsers across C#, Java, Kotlin, TypeScript, Python, and Go, **Wave 2** targets the **next Top 10 popular, architecturally significant libraries per language (60 libraries total)**.

Each parser maps framework-specific AST idioms into CodeExplorer's unified semantic ontology (`EndpointNode`, `MessageBrokerNode`, `DatabaseNode`, `TableNode`, `ExternalServiceNode`, `TestSuite`/`TestCase`, `PUBLISHES_TO`, `SUBSCRIBES_TO`, `QUERIES_TABLE`, `RESOLVES_TO`, `CALLS_ENDPOINT`).

---

## 2. Top 10 Next Libraries per Language (60 Total)

### 2.1. C# / .NET (Next Top 10)

| # | Library / Package | Category | AST Patterns | Graph Output |
|---|---|---|---|---|
| 1 | **FastEndpoints** (`FastEndpoints`) | Web API (REPR) | `Endpoint<TReq, TRes>`, `Get("/...")`, `Post("/...")`, `Put("/...")`, `Delete("/...")` inside `Configure()` | `EndpointNode` (`REST`), route binding |
| 2 | **NServiceBus** (`NServiceBus`) | Enterprise Bus / Saga | `IHandleMessages<T>`, `Saga<T>`, `IAmStartedByMessages<T>`, `IMessageSession.Send` / `Publish` | `MessageBrokerNode` (`nservicebus`), `PUBLISHES_TO` / `SUBSCRIBES_TO` |
| 3 | **Rebus** (`Rebus`) | Queue Messaging Bus | `IHandleMessages<T>`, `IBus.Send`, `IBus.Publish`, `IBus.Reply` | `MessageBrokerNode` (`rebus`), `PUBLISHES_TO` / `SUBSCRIBES_TO` |
| 4 | **Dapr .NET SDK** (`Dapr.Client`, `Dapr.AspNetCore`) | Distributed Runtime | `[Topic("pubsub", "topic")]`, `DaprClient.PublishEventAsync`, `InvokeMethodAsync`, `GetStateAsync` / `SaveStateAsync` | `MessageBrokerNode` (`dapr-pubsub`), `ExternalServiceNode` (`dapr-rpc`), `DatabaseNode` (`dapr-state`) |
| 5 | **Carter** (`Carter`) | Minimal API Modules | `: ICarterModule`, `AddRoutes(IEndpointRouteBuilder app)` with `MapGet` / `MapPost` / `MapGroup` | `EndpointNode` (`REST`) |
| 6 | **YARP** (`Yarp.ReverseProxy`) | API Gateway / Proxy | `MapReverseProxy()`, `AddReverseProxy()`, `LoadFromConfig`, `LoadFromMemory`, `IHttpForwarder.SendAsync` | `ExternalServiceNode` (`yarp-proxy`), gateway routing |
| 7 | **Linq2Db** (`linq2db`) | High-Performance ORM | `[Table("...")]`, `DataConnection`, `GetTable<T>()`, `Insert`, `Update`, `Delete` | `DatabaseNode`, `TableNode`, `QUERIES_TABLE` |
| 8 | **NHibernate** (`NHibernate`, `FluentNHibernate`) | Enterprise ORM | `ClassMap<T>`, `Table("...")`, `ISession.Query<T>()`, `SaveAsync`, `UpdateAsync`, `CreateSQLQuery` | `TableNode`, `QUERIES_TABLE` |
| 9 | **Azure SDK for .NET** (`Azure.Messaging.ServiceBus`, `Azure.Storage.Blobs`, `Azure.Cosmos`) | Cloud Data & Messaging | `ServiceBusClient.CreateSender` / `CreateProcessor`, `BlobContainerClient`, `CosmosClient.GetContainer` | `MessageBrokerNode` (`azure-servicebus`), `DatabaseNode` (`cosmosdb`), `ExternalServiceNode` (`azure:blob`) |
| 10 | **Semantic Kernel & Microsoft.Extensions.AI** (`Microsoft.SemanticKernel`, `Microsoft.Extensions.AI`) | AI Agents & Tooling | `[KernelFunction]`, `IChatClient.GetResponseAsync`, `Kernel.InvokePromptAsync` | `ExternalServiceNode` (`llm:semantic-kernel`), AI tool entrypoints |

### 2.2. Java (Next Top 10)

| # | Library / Package | Category | AST Patterns | Graph Output |
|---|---|---|---|---|
| 1 | **Eclipse Vert.x** (`io.vertx.core`, `io.vertx.ext.web`) | Reactive EventBus & Web | `AbstractVerticle`, `router.get("/...")`, `eventBus().publish(...)`, `eventBus().consumer(...)` | `EndpointNode` (`REST`), `MessageBrokerNode` (`vertx-eventbus`) |
| 2 | **jOOQ** (`org.jooq`) | Type-Safe SQL DSL | `DSLContext.selectFrom(TABLE)`, `.insertInto(TABLE)`, `.update(TABLE)`, `.deleteFrom(TABLE)` | `TableNode`, `QUERIES_TABLE` |
| 3 | **Akka / Apache Pekko** (`akka.actor`, `org.apache.pekko.actor`) | Actor System | `AbstractBehavior<T>`, `AbstractActor`, `ActorRef.tell(...)`, `Patterns.ask(...)` | `MessageBrokerNode` (`actor-system`), `PUBLISHES_TO` / `SUBSCRIBES_TO` |
| 4 | **Spring Batch** (`org.springframework.batch`) | Batch Processing | `@EnableBatchProcessing`, `JobBuilder`, `StepBuilder`, `ItemReader`, `ItemProcessor`, `ItemWriter` | `EntryPoint` (`batch-job`), pipeline links |
| 5 | **Spring Integration** (`org.springframework.integration`) | Enterprise Integration | `@MessagingGateway`, `@ServiceActivator`, `@Transformer`, `IntegrationFlow.from(...)` | `MessageBrokerNode` (`spring-integration`), channel flow |
| 6 | **Spring Cloud Gateway** (`org.springframework.cloud.gateway`) | API Gateway | `RouteLocatorBuilder.routes().route(...)`, `.path("/...")`, `.uri("lb://...")` | `EndpointNode` (`REST`), `ExternalServiceNode` |
| 7 | **Netty** (`io.netty.channel`) | Network Server / RPC | `ServerBootstrap`, `ChannelInboundHandlerAdapter`, `SimpleChannelInboundHandler<T>` | `EndpointNode` (`TCP`), handler pipeline |
| 8 | **Dropwizard / Jersey JAX-RS** (`io.dropwizard`, `org.glassfish.jersey`) | REST Microservices | `Application<T>`, `@Path`, `@GET`, `@POST`, `@PUT`, `@DELETE` | `EndpointNode` (`REST`) |
| 9 | **Redisson & Caffeine Cache** (`org.redisson`, `com.github.benmanes.caffeine`) | Distributed Cache & PubSub | `RedissonClient.getMap` / `getBucket` / `getTopic`, `@Cacheable`, `@CacheEvict` | `DatabaseNode` (`redis`), `MessageBrokerNode` (`redisson-topic`) |
| 10 | **LangChain4j & Spring AI** (`dev.langchain4j`, `org.springframework.ai`) | AI Engineering | `@AiService`, `@UserMessage`, `@Tool`, `ChatClient.prompt()` | `ExternalServiceNode` (`llm:langchain4j` / `llm:spring-ai`) |

### 2.3. Kotlin (Next Top 10)

| # | Library / Package | Category | AST Patterns | Graph Output |
|---|---|---|---|---|
| 1 | **SQLDelight** (`app.cash.sqldelight`) | Multiplatform Type-Safe SQL | `*Queries`, `.executeAsList()`, `.executeAsOne()`, `.transaction {}` | `TableNode`, `QUERIES_TABLE` |
| 2 | **http4k** (`org.http4k.core`, `org.http4k.routing`) | Functional HTTP | `routes("/path" bind Method.GET to ...)`, `HttpHandler` | `EndpointNode` (`REST`), `ExternalServiceNode` |
| 3 | **Kotlinx Coroutines Channels & SharedFlow** (`kotlinx.coroutines.channels`, `kotlinx.coroutines.flow`) | Reactive Event Streams | `Channel<T>()`, `MutableSharedFlow<T>()`, `.send(...)`, `.emit(...)`, `.collect` | `MessageBrokerNode` (`coroutines-flow`), `PUBLISHES_TO` / `SUBSCRIBES_TO` |
| 4 | **Dagger / Hilt / Kotlin-Inject** (`dagger.hilt`, `me.tatarka.inject`) | Dependency Injection | `@HiltViewModel`, `@AndroidEntryPoint`, `@Inject`, `@Provides`, `@Binds` | `RESOLVES_TO` DI bindings |
| 5 | **Kodein-DI** (`org.kodein.di`) | Multiplatform DI | `DI.Module`, `bind<T>() with singleton`, `bindSingleton<T>` | `RESOLVES_TO` DI bindings |
| 6 | **Apollo GraphQL Kotlin** (`com.apollographql.apollo3`) | GraphQL Client | `ApolloClient`, `.query(...)`, `.mutation(...)`, `.subscription(...)` | `ExternalServiceNode` (`graphql-client`) |
| 7 | **gRPC-Kotlin** (`io.grpc.kotlin`) | Coroutine gRPC | `*CoroutineImplBase`, `*CoroutineStub` | `EndpointNode` (`gRPC`), `ExternalServiceNode` (`gRPC`) |
| 8 | **Kotest & MockK** (`io.kotest`, `io.mockk`) | Testing & Mocking | `FunSpec`, `StringSpec`, `BehaviorSpec`, `every { ... }`, `coEvery { ... }` | `TestSuite`, `TestCase`, `TESTS` |
| 9 | **Voyager & Decompose** (`cafe.adriel.voyager`, `com.arkivanov.decompose`) | Compose Multiplatform | `Screen`, `ScreenModel`, `ComponentContext`, `navigator.push(...)` | UI navigation & `RESOLVES_TO` |
| 10 | **Supabase-kt & Firebase Kotlin** (`io.github.jan-tennert.supabase`, `dev.gitlive.firebase`) | Cloud BaaS & DB | `supabase.from("...")`, `firestore.collection("...")` | `DatabaseNode`, `TableNode`, `QUERIES_TABLE` |

### 2.4. TypeScript / JavaScript (Next Top 10)

| # | Library / Package | Category | AST Patterns | Graph Output |
|---|---|---|---|---|
| 1 | **SvelteKit / Astro / Nitro (H3)** (`@sveltejs/kit`, `astro`, `h3`) | Full-Stack Server Routes | `defineEventHandler`, `export const GET/POST`, `Actions` | `EndpointNode` (`REST`) |
| 2 | **Cloudflare Workers & D1/KV/Queues** (`@cloudflare/workers-types`) | Edge Serverless | `env.DB.prepare(...)`, `env.KV.get/put`, `env.QUEUE.send`, `fetch(req, env)` | `EndpointNode`, `DatabaseNode` (`cloudflare-d1`), `MessageBrokerNode` (`cloudflare-queues`) |
| 3 | **Supabase JS & Firebase Firestore** (`@supabase/supabase-js`, `firebase-admin`) | Cloud Databases | `supabase.from('...').select()`, `firestore.collection('...')` | `DatabaseNode`, `TableNode`, `QUERIES_TABLE` |
| 4 | **TanStack Query & SWR** (`@tanstack/react-query`, `swr`) | Client Data Fetching | `useQuery`, `useMutation`, `useSWR('/api/...')` | `ExternalServiceNode` / `CALLS_ENDPOINT` |
| 5 | **Effect-TS** (`effect`, `@effect/platform`) | Functional Effect & HTTP | `Context.Tag`, `Layer.succeed`, `HttpRouter.get('/...')` | `EndpointNode` (`REST`), `RESOLVES_TO` |
| 6 | **NATS.js & MQTT.js** (`nats`, `mqtt`) | Messaging / IoT | `nc.publish`, `nc.subscribe`, `mqttClient.publish`, `mqttClient.subscribe` | `MessageBrokerNode` (`nats` / `mqtt`), `PUBLISHES_TO` / `SUBSCRIBES_TO` |
| 7 | **Apollo Client & urql** (`@apollo/client`, `urql`) | GraphQL Client | `useQuery(gql...)`, `useMutation(gql...)`, `client.query` | `ExternalServiceNode` (`graphql-client`) |
| 8 | **Auth.js (NextAuth) / Better-Auth / Clerk** (`next-auth`, `better-auth`, `@clerk/nextjs`) | Auth & Identity | `NextAuth(...)`, `betterAuth(...)`, ` clerkMiddleware()` | `EndpointNode` (`/api/auth`), `ExternalServiceNode` (`auth-provider`) |
| 9 | **Playwright & Cypress** (`@playwright/test`, `cypress`) | E2E Testing | `page.goto('/...')`, `request.get('/api/...')`, `cy.visit('/...')`, `cy.intercept` | `TestSuite`, `TestCase`, `CALLS_ENDPOINT` |
| 10 | **Encore.ts & SST** (`encore.dev`, `sst`) | Infrastructure-from-Code | `api({ method: ..., path: ... })`, `new Topic(...)`, `new Subscription(...)`, `new SQLDatabase(...)` | `EndpointNode`, `MessageBrokerNode`, `DatabaseNode` |

### 2.5. Python (Next Top 10)

| # | Library / Package | Category | AST Patterns | Graph Output |
|---|---|---|---|---|
| 1 | **FastStream** (`faststream`) | Unified Async Messaging | `@broker.subscriber("...")`, `@broker.publisher("...")`, `broker.publish(...)` | `MessageBrokerNode` (`faststream`), `PUBLISHES_TO` / `SUBSCRIBES_TO` |
| 2 | **Confluent Kafka & AIOKafka** (`confluent_kafka`, `aiokafka`) | Kafka Streaming | `producer.produce("...")`, `consumer.subscribe(["..."])`, `send_and_wait` | `MessageBrokerNode` (`kafka`), `PUBLISHES_TO` / `SUBSCRIBES_TO` |
| 3 | **Pika & aio-pika** (`pika`, `aio_pika`) | RabbitMQ / AMQP | `basic_publish`, `basic_consume`, `exchange.publish`, `queue.consume` | `MessageBrokerNode` (`rabbitmq`), `PUBLISHES_TO` / `SUBSCRIBES_TO` |
| 4 | **asyncpg & psycopg v3** (`asyncpg`, `psycopg`) | Async PostgreSQL | `conn.fetch("SELECT ...")`, `conn.execute("INSERT ...")`, `cur.execute("...")` | `DatabaseNode` (`postgres`), `TableNode`, `QUERIES_TABLE` |
| 5 | **ClickHouse Connect / Driver** (`clickhouse_connect`, `clickhouse_driver`) | Columnar OLAP DB | `client.query("SELECT ...")`, `client.insert("table", ...)`, `client.execute("...")` | `DatabaseNode` (`clickhouse`), `TableNode`, `QUERIES_TABLE` |
| 6 | **Qdrant / Milvus / Weaviate** (`qdrant_client`, `pymilvus`, `weaviate`) | Vector Databases | `client.search(collection_name=...)`, `client.upsert(collection_name=...)`, `Collection("...")` | `DatabaseNode` (`vector-db`), `TableNode`, `QUERIES_TABLE` |
| 7 | **APScheduler / Prefect / Airflow** (`apscheduler`, `prefect`, `airflow`) | Schedulers & Orchestration | `@scheduler.scheduled_job`, `add_job`, `@flow`, `@task`, `@dag` | `EntryPoint` (`schedule` / `workflow`), `PUBLISHES_TO` / `SUBSCRIBES_TO` |
| 8 | **Sanic / Tornado / Falcon** (`sanic`, `tornado`, `falcon`) | Async Web Frameworks | `@app.route("/...")`, `@app.get("/...")`, `add_route("/...", ...)` | `EndpointNode` (`REST`) |
| 9 | **Pydantic AI / CrewAI / AutoGen** (`pydantic_ai`, `crewai`, `autogen`) | Multi-Agent AI | `Agent(...)`, `@agent.tool`, `Crew(...)`, `AssistantAgent(...)` | `ExternalServiceNode` (`llm:agent`), tool bindings |
| 10 | **Dependency Injector & Dishka** (`dependency_injector`, `dishka`) | DI Containers | `DeclarativeContainer`, `providers.Factory(...)`, `providers.Singleton(...)`, `@provide` | `RESOLVES_TO` DI bindings |

### 2.6. Go (Next Top 10)

| # | Library / Package | Category | AST Patterns | Graph Output |
|---|---|---|---|---|
| 1 | **ConnectRPC & Twirp** (`connectrpc.com/connect`, `github.com/twitchtv/twirp`) | Protobuf / HTTP RPC | `New*Handler(...)`, `New*Client(...)`, `connect.NewRequest(...)` | `EndpointNode` (`ConnectRPC`), `ExternalServiceNode` |
| 2 | **Go-Kit & Go-Kratos** (`github.com/go-kit/kit`, `github.com/go-kratos/kratos/v2`) | Microservice Toolkits | `httptransport.NewServer`, `grpctransport.NewServer`, `kratos.New(...)` | `EndpointNode` (`REST` / `gRPC`) |
| 3 | **sqlc** (Generated SQL Queries) | Type-Safe SQL | `-- name: ...`, `Queries` methods calling `db.QueryRowContext(ctx, queryConst, ...)` | `DatabaseNode`, `TableNode`, `QUERIES_TABLE` |
| 4 | **Segmentio Kafka-Go & Franz-Go** (`github.com/segmentio/kafka-go`, `github.com/twmb/franz-go`) | Kafka Streaming | `kafka.NewReader`, `kafka.NewWriter`, `WriteMessages`, `kgo.ConsumeTopics` | `MessageBrokerNode` (`kafka`), `PUBLISHES_TO` / `SUBSCRIBES_TO` |
| 5 | **Casbin** (`github.com/casbin/casbin/v2`) | Authorization / RBAC | `casbin.NewEnforcer`, `Enforce(...)`, `AddPolicy(...)` | `ExternalServiceNode` (`auth:casbin`) |
| 6 | **Uber Fx & Google Wire** (`go.uber.org/fx`, `github.com/google/wire`) | Dependency Injection | `fx.Provide(...)`, `fx.Invoke(...)`, `wire.Build(...)`, `wire.Bind(...)` | `RESOLVES_TO` DI bindings |
| 7 | **Machinery & River Queue** (`github.com/RichardKnop/machinery`, `github.com/riverqueue/river`) | Distributed Job Queues | `RegisterTask`, `SendTask`, `river.AddWorker`, `client.Insert` | `MessageBrokerNode` (`machinery` / `river`), `PUBLISHES_TO` / `SUBSCRIBES_TO` |
| 8 | **Gorilla Mux & HttpRouter** (`github.com/gorilla/mux`, `github.com/julienschmidt/httprouter`) | HTTP Routers | `HandleFunc("/...", h).Methods("...")`, `PathPrefix("/...")`, `router.GET("/...", h)` | `EndpointNode` (`REST`) |
| 9 | **Dapr Go SDK** (`github.com/dapr/go-sdk`) | Distributed Runtime | `PublishEvent`, `InvokeMethod`, `AddTopicEventHandler`, `SaveState` / `GetState` | `MessageBrokerNode` (`dapr-pubsub`), `ExternalServiceNode` (`dapr-rpc`), `DatabaseNode` (`dapr-state`) |
| 10 | **OpenTelemetry Go & Prometheus** (`go.opentelemetry.io/otel`, `github.com/prometheus/client_golang`) | Observability | `tracer.Start(ctx, "...")`, `promhttp.Handler()` | `EndpointNode` (`/metrics`), telemetry span references |

---

## 3. Phased Delivery Plan

1. **Phase 1 — Messaging, Event Streams, Actors & Job Queues**:
   - C#: `NServiceBusLibraryParser`, `RebusLibraryParser`, `DaprDotNetLibraryParser`
   - Java: `VertxLibraryParser`, `AkkaPekkoLibraryParser`, `SpringIntegrationLibraryParser`, `SpringBatchLibraryParser`
   - Kotlin: `CoroutinesFlowChannelLibraryParser`
   - TypeScript: `NatsMqttTsLibraryParser`, `EncoreSstLibraryParser`
   - Python: `FastStreamLibraryParser`, `KafkaPythonLibraryParser`, `PikaRabbitMqLibraryParser`, `SchedulerOrchestrationPythonLibraryParser`
   - Go: `KafkaGoLibraryParser`, `MachineryRiverGoLibraryParser`, `DaprGoLibraryParser`

2. **Phase 2 — Web Routers, API Gateways, Meta-Frameworks & RPC**:
   - C#: `FastEndpointsLibraryParser`, `CarterLibraryParser`, `YarpReverseProxyLibraryParser`
   - Java: `SpringCloudGatewayLibraryParser`, `DropwizardJerseyLibraryParser`, `NettyLibraryParser`
   - Kotlin: `Http4kLibraryParser`, `GrpcKotlinLibraryParser`, `ApolloKotlinLibraryParser`
   - TypeScript: `MetaFrameworkRoutesLibraryParser` (SvelteKit/Astro/H3), `CloudflareWorkersLibraryParser`, `EffectTsLibraryParser`, `GraphQlClientTsLibraryParser`, `TanStackQuerySwrLibraryParser`, `AuthTsLibraryParser`
   - Python: `AsyncWebPythonLibraryParser` (Sanic/Tornado/Falcon)
   - Go: `ConnectTwirpGoLibraryParser`, `GoKitKratosLibraryParser`, `GorillaMuxHttpRouterLibraryParser`

3. **Phase 3 — Databases, ORMs, Type-Safe SQL, BaaS & Vector Stores**:
   - C#: `Linq2DbNHibernateLibraryParser`, `AzureSdkDotNetLibraryParser`
   - Java: `JooqLibraryParser`, `RedissonCacheJavaLibraryParser`
   - Kotlin: `SqlDelightLibraryParser`, `SupabaseFirebaseKotlinLibraryParser`
   - TypeScript: `SupabaseFirebaseTsLibraryParser`
   - Python: `AsyncPgPsycopg3LibraryParser`, `ClickHousePythonLibraryParser`, `VectorDbPythonLibraryParser` (Qdrant/Milvus/Weaviate)
   - Go: `SqlcGoLibraryParser`

4. **Phase 4 — DI Containers, AI Agents, Security, Observability & E2E Testing**:
   - C#: `SemanticKernelAiDotNetLibraryParser`
   - Java: `LangChain4jSpringAiLibraryParser`
   - Kotlin: `DaggerHiltKodeinLibraryParser`, `KotestMockkLibraryParser`, `ComposeNavigationKotlinLibraryParser`
   - TypeScript: `PlaywrightCypressLibraryParser`
   - Python: `AgenticAiPythonLibraryParser` (PydanticAI/CrewAI/AutoGen), `DiContainerPythonLibraryParser` (Dependency Injector/Dishka)
   - Go: `UberFxWireGoLibraryParser`, `CasbinGoLibraryParser`, `OtelPrometheusGoLibraryParser`
