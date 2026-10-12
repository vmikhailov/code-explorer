# CodeExplorer Active Backlog & Engineering Roadmap (`roadmap/todo`) 📋

This document tracks active epics, immediate architectural refactoring plans, and the master engineering checklist for **CodeExplorer (`ce`)**.

---

## 🚨 IMMEDIATE FOCUS: Structural Graph Invariants & Gate Hardening (v1.32.0)

> Context & Detailed Specification: [**`semantic-invariant-gates-plan.md`**](./semantic-invariant-gates-plan.md) (Phase 6)

### 1. Architectural Goal
1. **Eliminate brittle string heuristics** (`WHERE NOT file_path CONTAINS 'Test'`) across all gates by propagating `is_test: bool`, `project_kind: string`, and `project_name: string` onto `FileNode`, `CallLiteralNode`, `TypeNode`, and `FunctionNode`.
2. **Connect projects directly to files** via `(p:Project)-[:CONTAINS]->(f:File)` and `(f:File)-[:BELONGS_TO]->(p:Project)`.
3. **Connect call literals to files** via `(lit:CallLiteral)-[:DECLARED_IN]->(f:File)`.
4. **Order pipeline layers (`stage_order`)** to replace $O(N^2)$ combinatorial string queries in `SIG-P-B-001` with direct scalar comparison (`WHERE src.stage_order < tgt.stage_order`).

### 2. Immediate Tasks
- [x] **6.7.1 Node Properties & Serialization**: Add `is_test`, `project_kind`, `project_name` to `FileNode`, `CallLiteralNode`, `TypeNode`, `FunctionNode`.
- [x] **6.7.2 Layer 3 Ingestion**: Emit `(Project)-[:CONTAINS]->(File)`, `(File)-[:BELONGS_TO]->(Project)`, and `(CallLiteral)-[:DECLARED_IN]->(File)` using `filesByProjectId`.
- [x] **6.7.3 Pipeline Stage Ordering**: Add `GetPipelineStageOrder` in `WorkspaceConventions` and attach `stage_order` to stage types.
- [x] **6.8.1 Gate Queries Refactoring**: Simplify `SIG-P-B-001`, `SIG-M-I-001`, `SIG-P-B-002`, `SIG-S-B-001` in `BuiltInGateCatalog.cs` and `.codeexplorer/gates/`.
- [x] **6.8.2 Verification**: Run `dotnet test`, rebuild index, and run `ce gate check`.

---

## 🎯 EPIC 0: Universal Semantic Value Resolution & Zero-Hack Cleanup ✅ DELIVERED

> **Detailed Specification:** See [**`semantic-resolution-and-hacks-cleanup-plan.md`**](../archive/semantic-resolution-and-hacks-cleanup-plan.md).

### 0.1 - 0.5: First-Class Semantic Entities (`Service`, `App`, `Library`, `Worker`, `CliTool`)
- [x] **0.1 Base `ProjectNode`**: Preserve common fields `Id`, `Name`, `Path`, `ProjectType`, `Extensions`.
- [x] **0.2 New Node Classes**: `ServiceNode`, `AppNode`, `LibraryNode`, `WorkerNode`, `CliToolNode` with dedicated relationships.
- [x] **0.3 SQLite Persistence**: Native indexing on `kind` column (`CREATE INDEX idx_nodes_kind ON nodes(kind)`).
- [x] **0.4 Cypher Polymorphism**: Transparent fallback `MATCH (p:Project)` matching any subtype, and direct `MATCH (s:Service)`.
- [x] **0.5 Projection & Taxonomy**: Simplified C1/C2/C3 projections in `ArchitectureViewEngine` and auto-generated `ontology.md`.

### 0.6 - 0.12: Value Resolution & Core Cleanups

- [x] **0.6 Unified ConfigStore in Layer 2/3**:
  - Relocate configuration parsing (`appsettings*.json`, `.env*`, `application*.yml/properties`) before AST file parsing.
  - Index keys in both hierarchical and screaming-snake notations; auto-register in `ConstantRegistry`.
  - Generic service-to-service URL discovery across any section (`UrlsSettings`, `Services`, `Endpoints`, `Clients`, `Gateways`).
- [x] **0.7 Universal AST Symbol & Constant Graph**:
  - Remove filename filter heuristic (`const`, `route`, etc.) in `Layer3SyntacticParser`.
  - Extract constants, enums, and static fields via TreeSitter CST/AST across all files.
  - Store Expression Trees and perform topological partial evaluation for dependent constants (`A + B`, `$"{Prefix}.orders"`).
- [x] **0.8 Universal `AstValueResolver`**:
  - Implement language-agnostic expression evaluator (literals, bin-ops, template strings, member access).
  - Add standard config readers: C# `config["Key"]`, TS `process.env.KEY`, Go `os.Getenv`, Python `os.getenv`.
  - Add local scope reaching definitions (trace argument identifiers back to local assignments).
- [x] **0.9 Clean Library Parsers & Remove Ad-Hoc Hacks**:
  - Clean `ConfigurationParser` (remove hardcoded `ParseCloudPayments` and generalize `ParseUrlsSettings`).
  - Clean `RabbitMqLibraryParser` (remove `paPartnerQueue` and synthetic `createQueue`).
  - Clean `GcpLibraryParser` (remove project-specific wrappers `getPubSubTopic`, `createNetworkTopic`, etc.).
  - Clean `HttpClientLibraryParser` (remove fake fallback URL generator `$"api/v1/{propName.ToLowerInvariant()}"`).
  - Clean `NestedSqlParser` (stop dropping tables with dynamic identifiers in `IsVariable`).
  - Clean `GoAstHelper` and BigQuery hardcoded schema rewrites.
- [x] **0.10 Decouple Domain Classification from Core Engine**:
  - Purge ad-tech ontology terms (`rules_bundling`, `rates_analytics`, `dynadot`, `cpm`, `tbmap`) from `ArchitectureViewEngine.cs`.
  - Provide domain classification via `.codeexplorer/domains.json`, graph clustering, or SLM Intent Distillation.
- [x] **0.11 Evidence-Based Entity Classification & Dynamic Role Engine**:
  - Two-tier taxonomy: decouple intrinsic Entity Kind (`Service`, `App`, `Worker`, `Library`, `CliTool`, `Function`, `Resource`) from runtime Architectural Role (`Ingress`, `CoreDomain`, `Gateway`, `Integration`, `AsyncProcessor`, `Foundation`).
  - Evidence accumulator across Layers 1-5 (Packaging, AST listeners, consumer loops, config bindings, graph-flow degrees).
  - Purge string/name/path heuristics (`Contains("/lib/")`, `HasProtocolTokens()`, `.Contains("worker")`).
- [x] **0.12 Polymorphic AST Declaration Extraction Across Language Parsers**:
  - Extend `IFileParser` with `ExtractDeclarations(rootNode, registerDeclaration)` to keep CST traversal in native language parser assemblies.
  - Implement `CSharpDeclarationExtractor`, `TypeScriptDeclarationExtractor`, `GoDeclarationExtractor`, `PythonDeclarationExtractor`, and `JavaDeclarationExtractor`.
  - Purge all language name switches (`switch (langName)`), file extension switches (`ext switch`), and CST node checks from `AstConstantExtractor.cs`.

---

## 🚀 EPIC 1: Agent Semantic & Project Architecture (MCP & CLI Parity)

- [x] **1.1 `get_architecture_view` MCP Tool**:
  - Connect MCP directly to `ArchitectureViewEngine` for C1 (System Context), C2 (Service Flow), C3 (Component Drill-Down).
  - Support parameters: `viewType`, `scope`, `includeLibraries`, `format`.
- [x] **1.2 Dependency Filtering in `get_project_dependencies`**:
  - Add parameter `--type runtime|build|all`:
    - `runtime` / `semantic`: network calls only (`SERVICE_CALL`), message queues (`PUBLISHES_TO`, `SUBSCRIBES_TO`), databases (`USES_DB`).
    - `build` / `structural`: project references (`PROJECT_REFERENCE`) and packages (`DEPENDS_ON`).
- [x] **1.3 `get_service_contracts` MCP Tool**:
  - Extract service contracts: ingress (endpoints, consumers) and egress (HTTP clients, publishers, databases).
- [x] **1.4 `trace_cross_service_flow` MCP Tool**:
  - End-to-end call tracing across service boundaries (Controller -> Queue -> Consumer -> Database).
- [x] **1.5 CLI Command Parity**:
  - `ce view architecture`, `ce dependencies --type`, `ce contracts`, `ce trace flow`.
- [x] **1.6 Token-Efficient Serializers**:
  - Serializers for TOON (Token-Oriented Object Notation), Mermaid (`flowchart LR`), and compact Markdown for minimal LLM context usage.

---

## 🔍 EPIC 2: Parser Intelligence & High-Fidelity Lineage

- [x] **2.1 ASP.NET Core Hierarchical Route Composition**:
  - Stitch `[Route("api/[controller]")]` with action methods `[HttpGet("{id}")]`, token replacement (`[controller]`, `[action]`).
  - Support Minimal API chaining: `app.MapGroup("/api/v1")`.
- [x] **2.2 C# Constructor Dependency Injection Mapping**:
  - Bind constructor parameters to private fields (`_orderService`).
  - Resolve interface calls via `[:IMPLEMENTS]` to concrete implementation classes in Layer 5.
- [x] **2.3 Declarative HTTP Clients and Egress**:
  - Extract URLs and routes from `HttpClient`, `RestSharp`, `Refit`, `RestEase`.
- [x] **2.4 ORM Data Lineage Mapping (EF Core & Dapper)**:
  - Extract tables from `DbSet<T>` and Fluent API `ToTable("...")`.
  - Bind raw SQL queries directly to DB table nodes in `inspect_data_lineage`.

---

## ⚡ EPIC 3: Cypher Query Engine & Transpiler Expansion

- [x] **3.1 OpenCypher Relationship Functions**:
  - Support relationship functions: `type(r)`, `properties(r)`, `startNode(r)`, `endNode(r)` in `WITH` and aggregations.
  - Direct access to edge attributes (`r.via`, `r.call_chain`).
- [x] **3.2 Cartesian Product Decomposition in `OPTIONAL MATCH`**:
  - Decompose independent `OPTIONAL MATCH` branches into isolated correlated subqueries/CTEs, eliminating intermediate row explosion $O(N \cdot M \cdot K)$.
- [x] **3.3 Path and List Predicates**:
  - Transpile `WHERE EXISTS((n)-[:REL]->(m))` to SQL `EXISTS`.
  - Quantifiers `any()`, `all()`, `none()`, `single()` over JSON arrays with `json_valid` guards.

---

## 🛡️ EPIC 4: Concurrency & Incremental Watcher

- [x] **4.1 Incremental File Watcher & Semantic AST Diffing (`ce index --watch`)**:
  - Fine-grained semantic AST diffing (`SemanticGraphDiffer`, `SymbolFootprint`, `FileGraphSnapshot`).
  - Logic-only modifications (e.g. `2+2`, internal statements) produce 0 graph rewiring and skip LLM intent distillation.
  - Stable symbol URN identity decoupled from line numbers (`#overloadIndex`).
  - Two-tier fast changeset detection (timestamps + SHA-256) via `FileRegistry`.
  - Configurable debounced file watching via `FileWatcher`.
  - SQLite database optimization (5 redundant index pruning, -34.5% DB size reduction, incremental `auto_vacuum`).
- [ ] **4.2 Read-Only Connection Pool for MCP Tools**:
  - Enforce `Mode=ReadOnly;Cache=Shared;` mode to eliminate reader/writer locks in SQLite.
  - Thread-safe in-memory cache for `SystemContext` and `Taxonomy` with auto-invalidation on changes.

---

## 🧪 EPIC 5: CI/CD Benchmark Fixtures

- [x] **5.1 Synthetic 100k-Node Benchmark Graph**:
  - Deterministic in-memory graph generator for CI (10k files, 25k classes, 70k methods, 300k relationships).
  - Automated SLA checks in tests (`find_symbol` < 25ms, `get_call_chain` < 100ms, `inspect_data_lineage` < 50ms).

---

## 🛡️ EPIC 6: Semantic Invariant Gates (SIG) & Architectural Ratchets

> Full specification: [semantic-invariant-gates-plan.md](semantic-invariant-gates-plan.md) | Architectural Theory: [../architecture/fractal-orthogonal-invariant-matrix.md](../architecture/fractal-orthogonal-invariant-matrix.md) | Polyglot Proposal & Catalog: [docs/proposals/semantic-invariant-gates-proposal.md](../proposals/semantic-invariant-gates-proposal.md)

- [x] **6.1 Core Models & Serialization (`CodeExplorer.Core`)**:
  - `SemanticGate`, `GateViolation`, `GateCheckResult`, `GateBaseline` models.
  - Line-shift immune `GateFingerprintGenerator` (`SHA256(gate_id + path + symbol + target)`).
  - YAML gate loader (`.codeexplorer/gates/*.yaml`) and JSON baseline loader.
- [x] **6.2 Gate Engine & Monotonic Ratchet (`CodeExplorer.Core`)**:
  - `GateExecutionEngine`: Execute Cypher queries via `CodeExplorerRepository` with Zero-Match contract (0 rows = PASS).
  - `RatchetService`: Detect new violations vs tolerated debt; detect stale baseline entries.
- [x] **6.3 CLI `ce gate` Command (`CodeExplorer.UI`)**:
  - Subcommands: `ce gate check`, `ce gate list`, `ce gate ratchet --update`, `ce gate init`.
  - ANSI formatted violation tables + JSON/SARIF output + proper exit codes for CI/pre-commit.
- [x] **6.4 Agent MCP Tool `check_semantic_gates` (`Core/Mcp`)**:
  - Expose fast gate checks to AI agents via `ce mcp` with actionable remediation guidance.
- [x] **6.5 Standard Built-in Gate Library**:
  - Clean Architecture transport purity, mutation chokepoints, dead endpoints, circular dependencies, AST visitor IO purity, and hardcoded call arguments (`SIG-M-I-001`).
- [x] **6.6 Gate Taxonomy & Category Architecture (`CodeExplorer.Core`)**:
  - Introduced `Category` (`Architectural` vs `Platform`) and `Platform` (`agnostic`, `dotnet`, `typescript`, etc.).
  - Organized starter gates into `.codeexplorer/gates/architectural/` and `.codeexplorer/gates/platform/<platform>/`.
  - Added `--category` and `--platform` flags to CLI and MCP tool.
- [ ] **6.7 Structural Graph Links & Semantic Flags (`Project` $\leftrightarrow$ `File`, `is_test`, `project_kind`)**:
  - Emit `(p:Project)-[:CONTAINS]->(f:File)` and `(f:File)-[:BELONGS_TO]->(p:Project)` during Layer 3 indexing.
  - Propagate `is_test` and `project_kind` to replace fragile string `CONTAINS 'Test'` heuristics.
- [ ] **6.8 Ordered Layer & Pipeline Stage Invariants (`LayerNode`, `[:PRECEDES]`, `stage_order`)**:
  - Replace combinatorial $O(N^2)$ `OR` clauses in `SIG-P-B-001` with clean `WHERE src.stage_order < tgt.stage_order`.

---

## 📋 Master Task Checklist

| ID | Area | Task | Priority | Status |
| :--- | :--- | :--- | :--- | :--- |
| **0.1** | Core/Entities | Create `ServiceNode`, `AppNode`, `LibraryNode`, `WorkerNode`, `CliToolNode` | 🚨 Urgent | ✅ Completed |
| **0.2** | Scanner | `ProjectNodeFactory` factory and writing actual `kind` to SQLite | 🚨 Urgent | ✅ Completed |
| **0.3** | Cypher | Polymorphism: `MATCH (p:Project)` -> `kind IN (...)`, `MATCH (s:Service)` -> `kind = 'Service'` | 🚨 Urgent | ✅ Completed |
| **0.4** | ViewEngine | Clean up C1/C2/C3 Cypher queries for new node types | 🚨 Urgent | ✅ Completed |
| **0.5** | OntologyGen | Regenerate `ontology.md` with dedicated sections for Service/App/Library | 🚨 Urgent | ✅ Completed |
| **0.6** | ConfigStore | Relocate config parsing (`appsettings`, `.env`, `yaml`) to Layer 2/3 and register in `ConstantRegistry` | 🚨 Urgent | ✅ Completed |
| **0.7** | SymbolGraph | Universal AST Symbol & Constant Graph across all files with Expression Trees & Topological Eval | 🚨 Urgent | ✅ Completed |
| **0.8** | Semantics | Universal `AstValueResolver` for multi-language AST expressions, configs, and reaching defs | 🚨 Urgent | ✅ Completed |
| **0.9** | Parsers/Clean | Clean library parsers (RabbitMQ, GCP, HttpClient, NestedSql, BigQuery) & purge ad-hoc hacks | 🚨 Urgent | ✅ Completed |
| **0.10** | Domain/Clean | Decouple ad-tech ontology from `ArchitectureViewEngine` -> `.codeexplorer/domains.json` / graph clustering | High | ✅ Completed |
| **0.11** | EntityKind/Role | Evidence-based entity classification & dynamic role engine (purge name/path heuristics) | 🚨 Urgent | ✅ Completed |
| **1.1** | Agent/MCP | Implement `get_architecture_view` MCP tool via `ArchitectureViewEngine` | High | ✅ Completed |
| **1.2** | Agent/MCP | Add `--type runtime\|build\|all` filter to `get_project_dependencies` | High | ✅ Completed |
| **1.3** | Agent/MCP | Implement `get_service_contracts` MCP tool (ingress / egress) | High | ✅ Completed |
| **1.4** | Agent/MCP | Implement `trace_cross_service_flow` MCP tool | High | ✅ Completed |
| **1.5** | CLI | Add commands `ce view architecture`, `ce dependencies --type`, `ce contracts` | High | ✅ Completed |
| **1.6** | Formats | Add TOON, Mermaid, and Markdown serializers to `ArchitectureViewEngine` | High | ✅ Completed |
| **2.1** | Parsers | ASP.NET Core route composition and Minimal API `MapGroup` | Medium | ✅ Completed |
| **2.2** | Parsers | C# constructor DI mapping and resolution to implementations via `[:IMPLEMENTS]` | Medium | ✅ Completed |
| **2.3** | Parsers | Declarative HTTP clients (Refit/RestEase) and URI resolution | Medium | ✅ Completed |
| **2.4** | Parsers | EF Core `DbSet<T>` / `ToTable` and Dapper SQL lineage | Medium | ✅ Completed |
| **3.1** | Cypher | Relationship functions (`type(r)`, `properties(r)`, `startNode`, `endNode`) | Medium | ✅ Completed |
| **3.2** | Cypher | Cartesian product decomposition in `OPTIONAL MATCH` | Medium | ✅ Completed |
| **3.3** | Cypher | Path predicates (`EXISTS((a)->(b))`) and list quantifiers | Low | ✅ Completed |
| **4.1** | Concurrency | Incremental file watcher & semantic AST differ (`ce index --watch`) | Medium | ✅ Completed |
| **4.2** | Concurrency | Read-only connection pool for MCP tools | Medium | ⏳ Pending |
| **5.1** | Testing | Synthetic 100k-node benchmark graph for CI | Low | ✅ Completed |
| **6.1** | Core/Gates | Create `SemanticGate`, `GateViolation`, YAML/JSON models & fingerprinting | 🚨 Urgent | ✅ Completed |
| **6.2** | Core/Gates | Implement `GateExecutionEngine` & `RatchetService` (monotonic baseline) | 🚨 Urgent | ✅ Completed |
| **6.3** | CLI | Implement `ce gate check/list/ratchet/init` CLI commands | High | ✅ Completed |
| **6.4** | Agent/MCP | Implement `check_semantic_gates` MCP tool with actionable remediation | High | ✅ Completed |
| **6.5** | Library | Built-in starter gates (transport purity, chokepoint, dead endpoints, hardcoded call strings) | Medium | ✅ Completed |
| **6.6** | Core/Gates | Gate taxonomy (`Category`, `Platform`, `--category`, `--platform`, organized directories) | High | ✅ Completed |
| **6.7** | Core/Graph | Structural `Project` $\leftrightarrow$ `File` edges, `is_test`, and `project_kind` propagation | High | ⏳ In Progress |
| **6.8** | Core/Graph | Ordered layer & pipeline stage relationships (`LayerNode`, `[:PRECEDES]`, `stage_order`) | Medium | ⏳ In Progress |
| **7.1** | Go/Ingress | Go Web Frameworks (`gin`, `fiber`, `echo`) with route groups & sample projects | 🚨 Urgent | ✅ Completed |
| **7.2** | Go/Egress | Go HTTP Clients (`net/http`, `resty`) & Postgres Driver (`pgx`) | High | ✅ Completed |
| **7.3** | Python/Ingress | Python Web Frameworks (`flask`, `django`) route & blueprint mapping | High | ⏳ In Progress |
| **7.4** | Python/Egress | Python HTTP Clients (`requests`, `httpx`, `aiohttp`) service-to-service calls | High | ⏳ In Progress |
| **7.5** | Java/Messaging | Java Messaging (`kafka` / Spring Kafka, `rabbitmq` / Spring AMQP) | High | ⏳ In Progress |

---

## 🌐 EPIC 7: Multi-Language Semantic Extensions & Ingress/Egress Parity

> **Target Releases:** `v1.37.x` series.  
> **Goal:** Close semantic extraction gaps across Go, Python, and Java. Ensure full topological graph fidelity (Ingress endpoints, Egress HTTP clients, Message broker topologies, and Database operations) with dedicated test suites and sample projects.

### Phase 7.1: Go Web Frameworks (Ingress) — Target `v1.37.6` ✅ DELIVERED
- [x] **Gin Web Framework (`github.com/gin-gonic/gin`)**:
  - `GinLibraryParser`: HTTP method routes (`GET`, `POST`, `PUT`, `DELETE`, `PATCH`, `OPTIONS`, `HEAD`, `Any`), router groups (`r.Group("/prefix")`) with nested routes.
- [x] **Fiber Web Framework (`github.com/gofiber/fiber`)**:
  - `FiberLibraryParser`: HTTP routes (`app.Get`, `app.Post`, etc.), group route chaining (`app.Group("/prefix")`).
- [x] **Echo Web Framework (`github.com/labstack/echo`)**:
  - `EchoLibraryParser`: HTTP routes (`e.GET`, `e.POST`, etc.), group route chaining (`e.Group("/prefix")`).
- [x] **Artifacts & Verification**:
  - Sample projects: `cli/tests/SampleProjects/go/gin-service`, `fiber-service`, `echo-service`.
  - Comprehensive unit & integration tests in `CodeExplorer.Tests`.

### Phase 7.2: Go Egress & Data Access — Target `v1.37.7` ✅ DELIVERED
- [x] **Go HTTP Clients (`net/http`, `resty`)**:
  - `GoHttpClientLibraryParser`: standard library `http.Get`, `http.Post`, `http.NewRequest` URI string / constant extraction.
  - `RestyLibraryParser`: `resty.New()`, `.R().Get(url)`, `.Post(url)`.
- [x] **Go Database Drivers (`pgx`)**:
  - `PgxLibraryParser`: `pgxpool`, `pgx.Connect`, SQL queries, table references.
- [x] **Artifacts & Verification**:
  - Sample project: `cli/tests/SampleProjects/go/pgx-resty-service`.
  - Comprehensive unit & integration tests in `GoEgressAndDatabaseTests.cs`.

### Phase 7.3: Python Web Frameworks (Ingress) — Target `v1.37.8`
- [ ] **Flask (`flask`)**:
  - `FlaskLibraryParser`: `@app.route(...)`, `@app.get(...)`, `@app.post(...)`, `Blueprint` route prefixes.
- [ ] **Django (`django`)**:
  - `DjangoLibraryParser`: `urlpatterns = [path('...', ...), re_path('...', ...)]`, `include(...)`, DRF router bindings.
- [ ] **Artifacts & Verification**:
  - Sample projects: `cli/tests/SampleProjects/python/flask-service`, `django-service`.
  - Test suite.

### Phase 7.4: Python Egress (HTTP Clients) — Target `v1.37.9`
- [ ] **Python HTTP Clients (`requests`, `httpx`, `aiohttp`)**:
  - `RequestsPythonLibraryParser`: `requests.get(...)`, `requests.post(...)`, `session.get(...)`.
  - `HttpxPythonLibraryParser`: `httpx.get(...)`, `httpx.AsyncClient().post(...)`.
  - `AioHttpPythonLibraryParser`: `aiohttp.ClientSession().get(...)`.
- [ ] **Artifacts & Verification**:
  - Sample projects & test suite.

### Phase 7.5: Java Messaging & Event-Driven Egress/Ingress — Target `v1.37.10`
- [ ] **Apache Kafka / Spring Kafka (`org.apache.kafka`, `org.springframework.kafka`)**:
  - `SpringKafkaLibraryParser`: `@KafkaListener(topics = "...")`, `KafkaTemplate.send("topic", ...)`.
- [ ] **RabbitMQ / Spring AMQP (`com.rabbitmq`, `org.springframework.amqp`)**:
  - `SpringRabbitMqLibraryParser`: `@RabbitListener(queues = "...")`, `RabbitTemplate.convertAndSend(...)`.
- [ ] **Artifacts & Verification**:
  - Sample projects: `cli/tests/SampleProjects/java/kafka-service`, `rabbitmq-service`.
  - Test suite.
