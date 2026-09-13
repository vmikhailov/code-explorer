# Architectural Audit & Improvements Roadmap

This document captures the architectural evolution of **CodeExplorer**, tracks implemented milestones, and outlines the active engineering roadmap.

---

## 1. Implemented Milestones & Delivered Solutions

### ✓ Milestone 1: Embedded SQLite & Cypher Compiler (Completed)
* **Problem**: Heavy dependency on external Memgraph Docker containers, high setup friction.
* **Delivered Solution**:
  * Migrated 100% to an embedded, in-process **SQLite** database with WAL mode and B-Tree indexes.
  * Implemented an in-house AST-based **Cypher-to-SQL Compiler** (`CodeExplorer.Cypher`) translating MATCH, OPTIONAL MATCH, WITH, WHERE, pattern comprehensions, and subqueries into high-performance SQLite Recursive CTEs.
  * Packaged into a self-contained, single-file cross-platform executable (`ce` / `ce.exe`, ~58 MB) for Windows, Linux, and macOS.

### ✓ Milestone 2: Targeted Subtree Ingestion & Clearing (Completed)
* **Problem**: Scanning even small changes required full workspace re-indexing.
* **Delivered Solution**:
  * Added targeted subtree scanning: `ce scan <folder>` (and `ce clear <folder>`).
  * `ParsingContext.IsSubtreeScan` preserves workspace-relative URN paths, generates intermediate `FolderNode` hierarchies up to the root, and isolates updates to the target folder.
  * Enforced unique indexes on `edges(from_id, to_id, kind)` with `ON CONFLICT DO UPDATE` for safe idempotent re-scans.

### ✓ Milestone 3: Query Traversal Bounding & Timeout Resilience (Completed)
* **Problem**: Unbounded Cypher paths (`[:CONTAINS*0..]`, `[:DEFINES|DECLARES*1..]`) caused exponential recursive CTE explosion (>10-15s timeouts) on large graphs (200k+ nodes).
* **Delivered Solution**:
  * Bounded all recursive Cypher patterns (`[:CONTAINS*0..3]`, `[:CONTAINS*1..5]`, `[:DEFINES|DECLARES*1..4]`).
  * Replaced full-table scan `f.path ENDS WITH $filePath` in `get_file_outline` with normalized index lookups.
  * Implemented 15-second `CommandTimeout` in `SqliteGraphClient` and end-to-end `CancellationToken` propagation across MCP handlers, throwing descriptive `TimeoutException` and returning actionable error messages instead of deadlocks.
  * Added lightweight `get_architecture_overview` MCP tool with semantic layer classification (Core, Services, UI, Data, Tests).

### ✓ Milestone 4: Project-Specific Query Extensibility (Completed)
* **Delivered Solution**:
  * Dynamic query discovery and persistence in `<workspaceRoot>/.codeexplorer/queries/*.cypher` and `*.json`.
  * MCP tools `list_project_queries`, `save_project_query`, `execute_project_query`.

---

## 2. Active Engineering Roadmap

```
┌────────────────────────────────────────────────────────────────────────┐
│                        ACTIVE: PARSER INTELLIGENCE                     │
│  [C# Ingress / Egress] ──> [Message Queues] ──> [DI Resolution]        │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                     PLANNED: MULTI-LANGUAGE PARITY                     │
│  [NestJS / TypeScript] ──> [Go Gin/Chi] ──> [Python FastAPI/SQLA]      │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│              FUTURE: VS CODE / ANTIGRAVITY LSP COMPANION               │
│  [Sidecar Bridge] ──> [VS Code Language API] ──> [Cytoscape.js HUD]    │
└────────────────────────────────────────────────────────────────────────┘
```

---

### Phase 1: High-Fidelity Ingress & Egress Parsers (In Progress)

#### 1.1 C# ASP.NET Routing & Minimal APIs
- [ ] **Hierarchical Route Composition**: Automatically combine Controller-level `[Route("api/[controller]")]` or `[Route("api/v{version}/[controller]")]` with Action-level attributes (`[HttpGet("{id}")]`, `[HttpPost]`), resolving token replacements (`[controller]`, `[action]`) into fully qualified routes: `GET:/api/Orders/{id}`.
- [ ] **Minimal API Route Groups**: Track `app.MapGroup("/api/v1/orders")` instances and prepend group prefixes to chained `group.MapGet("/{id}", ...)` declarations.
- [ ] **SignalR Hubs**: Detect `app.MapHub<THub>("/path")` and capture the underlying Hub class as an entry point.

#### 1.2 C# HTTP Clients & External Services (Egress)
- [ ] **HttpClient & RestSharp URI Resolution**:
  - Combine base URLs configured in constructors or factories (`new RestClient("https://api.example.com")`, `c.BaseAddress = ...`) with relative paths in requests (`new RestRequest("/v1/items")`).
  - Extract service targets from `new Uri(...)`, interpolated strings, and `HttpRequestMessage`.
- [ ] **Declarative HTTP Clients (Refit / RestEase)**:
  - Parse interface-based HTTP contracts:
    ```csharp
    public interface IBillingApi {
        [Post("/api/v1/charge")]
        Task<ChargeResult> ChargeAsync([Body] ChargeRequest req);
    }
    ```
  - Register as `ExternalService` egress points without requiring method bodies.

#### 1.3 Message-Driven Ingress & Egress (Event Architecture)
- [ ] **Message Ingress (Consumers & Handlers)**:
  - MassTransit: `IConsumer<TMessage>`, `IJobConsumer<T>`
  - MediatR: `IRequestHandler<TRequest, TResponse>`, `INotificationHandler<T>`
  - NServiceBus / Rebus / Wolverine / Kafka / RabbitMQ handlers.
- [ ] **Message Egress (Publishers & Producers)**:
  - Detect `_publishEndpoint.Publish<T>`, `_bus.Send<T>`, `_mediator.Send(new Command())`, `_kafkaProducer.ProduceAsync(...)`.
  - Connect publishing callers to consumer handlers via `[:PUBLISHES_EVENT]` and `[:CONSUMES_EVENT]` edges.

---

### Phase 2: Dependency Injection & Intra-Class Call Linking

#### 2.1 Constructor Dependency Injection Mapping
- [ ] Map constructor parameters `(IOrderService orderService)` and primary constructor parameters to private fields `_orderService`.
- [ ] When a method invokes `_orderService.CreateOrder()`, resolve the caller to the interface method `IOrderService.CreateOrder`.
- [ ] Connect with existing `[:IMPLEMENTS]` edges to trace through to concrete implementation classes.

#### 2.2 Data Lineage (EF Core & Dapper)
- [ ] **EF Core**: Extract table names from `DbSet<T>` properties and Fluent API configurations (`modelBuilder.Entity<Order>().ToTable("orders")`).
- [ ] **Dapper & Raw SQL**: Extract SQL string constants defined in separate query files or classes.

---

### Phase 3: Multi-Language Parity

- [ ] **TypeScript / JavaScript**:
  - Parse NestJS `@Controller()`, `@Injectable()`, `@Inject()` DI bindings.
  - Parse Express, Fastify, and Next.js App Router (`route.ts`) endpoints.
  - Parse Axios, Fetch, and TanStack Query egress calls.
- [ ] **Go**:
  - Parse Gin (`r.GET(...)`, `r.Group(...)`), Chi, and Fiber routes.
  - Parse `database/sql` and GORM table queries.
- [ ] **Python**:
  - Parse FastAPI (`@app.get(...)`, `APIRouter`), Flask Blueprints, and Django urls.
  - Parse SQLAlchemy models and tables.

---

### Phase 4: VS Code & Antigravity Extension (Companion & LSP Bridge)

*(Full design specification: [vscode-extension-design.md](vscode-extension-design.md))*
- [ ] **Sidecar Integration**: Package extension to launch embedded `ce.exe` and communicate via JSON-RPC / MCP.
- [ ] **LSP Bridge**: Leverage `vscode.executeDefinitionProvider` and `vscode.executeImplementationProvider` to resolve late-bound symbols on demand.
- [ ] **Cytoscape.js Cockpit**: Interactive visual graph panel in VS Code with DAG hierarchical layouts and CodeLens navigation.
