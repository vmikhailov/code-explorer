# CodeExplorer Active Backlog & Engineering Roadmap (`roadmap/todo`) 📋

This document tracks active epics, immediate architectural refactoring plans, and the master engineering checklist for **CodeExplorer (`ce`)**.

---

## 🚨 IMMEDIATE PRIORITY: First-Class Semantic Entities (`Service`, `App`, `Library`, `Worker`, `CliTool`)

### 1. Architectural Goal
Replace abstract `Project` with a `role` field in JSON with specialized first-class node types in SQLite (`kind = 'Service'`, `kind = 'App'`, `kind = 'Library'`, `kind = 'Worker'`, `kind = 'CliTool'`).
At the same time, ensure transparent polymorphism in the Cypher compiler: `MATCH (p:Project)` matches any of them.

```
                  ┌──────────────────────┐
                  │     ProjectNode      │ (base type / unclassified)
                  └──────────┬───────────┘
         ┌─────────────┬─────┴───────┬─────────────┬─────────────┐
         ▼             ▼             ▼             ▼             ▼
   ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌───────────┐
   │ServiceNode│ │  AppNode  │ │LibraryNode│ │WorkerNode │ │CliToolNode│
   └───────────┘ └───────────┘ └───────────┘ └───────────┘ └───────────┘
   kind:Service  kind:App      kind:Library  kind:Worker   kind:CliTool
```

### 2. Step-by-Step Implementation Plan

#### Phase 1. Data Model & C# Node Classes (`CodeExplorer.Core`)
- [x] **1.1 Base `ProjectNode`**:
  - Preserve common fields: `Id`, `Name`, `Path`, `ProjectType` (language: csharp, typescript, etc.), `Extensions`.
- [x] **1.2 New Node Classes in `Common/Nodes/Layer2_Boundaries/`**:
  - `ServiceNode.cs` (`[OntologyNode(label: "Service")]`):
    - Relationships: `SERVICE_CALL`, `PUBLISHES_TO`, `SUBSCRIBES_TO`, `USES_DB`, `CONTAINS (Endpoint, EntryPoint, ApiInUse, CloudService)`.
  - `AppNode.cs` (`[OntologyNode(label: "App")]`):
    - Relationships: `SERVICE_CALL`, `CONTAINS (Endpoint, EntryPoint, ApiInUse)`.
  - `LibraryNode.cs` (`[OntologyNode(label: "Library")]`):
    - Relationships: `DEPENDS_ON`, `CONTAINS (Type, Function)`.
  - `WorkerNode.cs` (`[OntologyNode(label: "Worker")]`):
    - Relationships: `SUBSCRIBES_TO`, `PUBLISHES_TO`, `USES_DB`.
  - `CliToolNode.cs` (`[OntologyNode(label: "CliTool")]`):
    - Relationships: `USES_DB`, `DEPENDS_ON`.
- [x] **1.3 Constants**:
  - Lock names in `OntologyConstants.NodeLabels`.

#### Phase 2. Factory and Classification During Scanning (`Layer2` & `PostIndexAnalyzer`)
- [x] **2.1 Project Factory `ProjectNodeFactory`**:
  - In `Layer2ProjectParser`, detect role upon discovering a project (via `ProjectRoleDetector`) and immediately instantiate the concrete type (`ServiceNode`, `AppNode`, `LibraryNode`, `WorkerNode`, `CliToolNode`).
- [x] **2.2 SQLite Persistence (`kind`)**:
  - In the `nodes` table, write the actual type into the `kind` column (`Service`, `App`, `Library`, `Worker`, `CliTool`).
  - Type lookups become native via SQLite B-Tree index (`CREATE INDEX idx_nodes_kind ON nodes(kind)`).

#### Phase 3. Polymorphism in Cypher Compiler (`CodeExplorer.Cypher`)
- [x] **3.1 Direct Fast Lookup**:
  - Translate `MATCH (s:Service)` into pure SQL: `WHERE s.kind = 'Service'` (no calls to `json_extract(properties, '$.role')`).
- [x] **3.2 Hierarchical Polymorphism for `Project`**:
  - Translate `MATCH (p:Project)` into:
    `WHERE p.kind IN ('Project', 'Service', 'App', 'Library', 'Worker', 'CliTool')`.
- [x] **3.3 Function `labels(n)`**:
  - If `n.kind IN ('Service', 'App', 'Library', 'Worker', 'CliTool')`, return `json_array(n.kind, 'Project')`.

#### Phase 4. Projection Engine & Queries (`ArchitectureViewEngine`)
- [x] **4.1 Simplify C1 / C2 / C3 Queries**:
  - Replace compound checks `WHERE p.kind = 'Project' AND json_extract(...)` with direct type matching: `MATCH (s:Service)`, `MATCH (l:Library)`.
  - In C1 System Context, display only `Service`, `App`, `Worker`, `Database`, `Topic` (cleanly excluding `Library`).

#### Phase 5. Ontology Auto-Generation (`OntologyGen`) & Tests
- [x] **5.1 Regenerate `ontology.md`**:
  - Run `OntologyGen`. Automatically generates separate sections with diagrams and properties for `Service`, `App`, `Library`, `Worker`, `CliTool`.
- [x] **5.2 Tests**:
  - Test suite in `CodeExplorer.Cypher.Tests`:
    - `MATCH (s:Service)` matches only services.
    - `MATCH (p:Project)` polymorphically matches all projects.
  - Regression tests in `CodeExplorer.Tests`.

---

## 🎯 EPIC 0: Universal Semantic Value Resolution & Zero-Hack Cleanup

> **Detailed Specification:** See [**`semantic-resolution-and-hacks-cleanup-plan.md`**](./semantic-resolution-and-hacks-cleanup-plan.md) and [audit artifact](file:///C:/Users/viach/.gemini/antigravity-ide/brain/c2895c5c-0d94-4e3f-9ccb-9c30cbaa962f/semantic_analysis_and_hacks_audit.md).

- [ ] **0.6 Unified ConfigStore in Layer 2/3**:
  - Relocate configuration parsing (`appsettings*.json`, `.env*`, `application*.yml/properties`) before AST file parsing.
  - Index keys in both hierarchical and screaming-snake notations; auto-register in `ConstantRegistry`.
- [ ] **0.7 Universal AST Symbol & Constant Graph**:
  - Remove filename filter heuristic (`const`, `route`, etc.) in `Layer3SyntacticParser`.
  - Extract constants, enums, and static fields via TreeSitter CST/AST across all files.
  - Store Expression Trees and perform topological partial evaluation for dependent constants (`A + B`, `$"{Prefix}.orders"`).
- [ ] **0.8 Universal `AstValueResolver`**:
  - Implement language-agnostic expression evaluator (literals, bin-ops, template strings, member access).
  - Add standard config readers: C# `config["Key"]`, TS `process.env.KEY`, Go `os.Getenv`, Python `os.getenv`.
  - Add local scope reaching definitions (trace argument identifiers back to local assignments).
- [ ] **0.9 Clean Library Parsers & Remove Ad-Hoc Hacks**:
  - Clean `RabbitMqLibraryParser` (remove `paPartnerQueue` and synthetic `createQueue`).
  - Clean `GcpLibraryParser` (remove project-specific wrappers `getPubSubTopic`, `createNetworkTopic`, etc.).
  - Clean `HttpClientLibraryParser` (remove fake fallback URL generator `$"api/v1/{propName.ToLowerInvariant()}"`).
  - Clean `NestedSqlParser` (stop dropping tables with dynamic identifiers in `IsVariable`).
  - Clean `GoAstHelper` and BigQuery hardcoded schema rewrites.
- [ ] **0.10 Decouple Domain Classification from Core Engine**:
  - Purge ad-tech ontology terms (`rules_bundling`, `rates_analytics`, `dynadot`, `cpm`, `tbmap`) from `ArchitectureViewEngine.cs`.
  - Provide domain classification via `.codeexplorer/domains.json`, graph clustering, or SLM Intent Distillation.

---

## 🚀 EPIC 1: Agent Semantic & Project Architecture (MCP & CLI Parity)

- [ ] **1.1 `get_architecture_view` MCP Tool**:
  - Connect MCP directly to `ArchitectureViewEngine` for C1 (System Context), C2 (Service Flow), C3 (Component Drill-Down).
  - Support parameters: `viewType`, `scope`, `includeLibraries`, `format`.
- [ ] **1.2 Dependency Filtering in `get_project_dependencies`**:
  - Add parameter `--type runtime|build|all`:
    - `runtime` / `semantic`: network calls only (`SERVICE_CALL`), message queues (`PUBLISHES_TO`, `SUBSCRIBES_TO`), databases (`USES_DB`).
    - `build` / `structural`: project references (`PROJECT_REFERENCE`) and packages (`DEPENDS_ON`).
- [ ] **1.3 `get_service_contracts` MCP Tool**:
  - Extract service contracts: ingress (endpoints, consumers) and egress (HTTP clients, publishers, databases).
- [ ] **1.4 `trace_cross_service_flow` MCP Tool**:
  - End-to-end call tracing across service boundaries (Controller -> Queue -> Consumer -> Database).
- [ ] **1.5 CLI Command Parity**:
  - `ce view architecture`, `ce dependencies --type`, `ce contracts`, `ce trace flow`.
- [ ] **1.6 Token-Efficient Serializers**:
  - Serializers for TOON (Token-Oriented Object Notation), Mermaid (`flowchart LR`), and compact Markdown for minimal LLM context usage.

---

## 🔍 EPIC 2: Parser Intelligence & High-Fidelity Lineage

- [ ] **2.1 ASP.NET Core Hierarchical Route Composition**:
  - Stitch `[Route("api/[controller]")]` with action methods `[HttpGet("{id}")]`, token replacement (`[controller]`, `[action]`).
  - Support Minimal API chaining: `app.MapGroup("/api/v1")`.
- [ ] **2.2 C# Constructor Dependency Injection Mapping**:
  - Bind constructor parameters to private fields (`_orderService`).
  - Resolve interface calls via `[:IMPLEMENTS]` to concrete implementation classes in Layer 5.
- [ ] **2.3 Declarative HTTP Clients and Egress**:
  - Extract URLs and routes from `HttpClient`, `RestSharp`, `Refit`, `RestEase`.
- [ ] **2.4 ORM Data Lineage Mapping (EF Core & Dapper)**:
  - Extract tables from `DbSet<T>` and Fluent API `ToTable("...")`.
  - Bind raw SQL queries directly to DB table nodes in `inspect_data_lineage`.

---

## ⚡ EPIC 3: Cypher Query Engine & Transpiler Expansion

- [ ] **3.1 OpenCypher Relationship Functions**:
  - Support relationship functions: `type(r)`, `properties(r)`, `startNode(r)`, `endNode(r)` in `WITH` and aggregations.
  - Direct access to edge attributes (`r.via`, `r.call_chain`).
- [ ] **3.2 Cartesian Product Decomposition in `OPTIONAL MATCH`**:
  - Decompose independent `OPTIONAL MATCH` branches into isolated correlated subqueries/CTEs, eliminating intermediate row explosion $O(N \cdot M \cdot K)$.
- [ ] **3.3 Path and List Predicates**:
  - Transpile `WHERE EXISTS((n)-[:REL]->(m))` to SQL `EXISTS`.
  - Quantifiers `any()`, `all()`, `none()` over JSON arrays.

---

## 🛡️ EPIC 4: Concurrency & Incremental Watcher

- [ ] **4.1 Incremental File Watcher (`ce watch`)**:
  - File change tracking with 500ms debounce.
  - Targeted execution of `ParsingContext.IsSubtreeScan` on changed files.
- [ ] **4.2 Read-Only Connection Pool for MCP Tools**:
  - Enforce `Mode=ReadOnly;Cache=Shared;` mode to eliminate reader/writer locks in SQLite.
- [ ] **4.3 Macro-Structure Caching**:
  - Thread-safe in-memory cache for `SystemContext` and `Taxonomy` with auto-invalidation on changes.

---

## 🧪 EPIC 5: CI/CD Benchmark Fixtures

- [ ] **5.1 Synthetic 100k-Node Benchmark Graph**:
  - Deterministic in-memory graph generator for CI (10k files, 25k classes, 70k methods, 300k relationships).
  - Automated SLA checks in tests (`find_symbol` < 25ms, `get_call_chain` < 100ms, `inspect_data_lineage` < 50ms).

---

## 📋 Master Task Checklist

| ID | Area | Task | Priority | Status |
| :--- | :--- | :--- | :--- | :--- |
| **0.1** | Core/Entities | Create `ServiceNode`, `AppNode`, `LibraryNode`, `WorkerNode`, `CliToolNode` | 🚨 Urgent | ✅ Completed |
| **0.2** | Scanner | `ProjectNodeFactory` factory and writing actual `kind` to SQLite | 🚨 Urgent | ✅ Completed |
| **0.3** | Cypher | Polymorphism: `MATCH (p:Project)` -> `kind IN (...)`, `MATCH (s:Service)` -> `kind = 'Service'` | 🚨 Urgent | ✅ Completed |
| **0.4** | ViewEngine | Clean up C1/C2/C3 Cypher queries for new node types | 🚨 Urgent | ✅ Completed |
| **0.5** | OntologyGen | Regenerate `ontology.md` with dedicated sections for Service/App/Library | 🚨 Urgent | ✅ Completed |
| **0.6** | ConfigStore | Relocate config parsing (`appsettings`, `.env`, `yaml`) to Layer 2/3 and register in `ConstantRegistry` | 🚨 Urgent | ⏳ Pending |
| **0.7** | SymbolGraph | Universal AST Symbol & Constant Graph across all files with Expression Trees & Topological Eval | 🚨 Urgent | ⏳ Pending |
| **0.8** | Semantics | Universal `AstValueResolver` for multi-language AST expressions, configs, and reaching defs | 🚨 Urgent | ⏳ Pending |
| **0.9** | Parsers/Clean | Clean library parsers (RabbitMQ, GCP, HttpClient, NestedSql, BigQuery) & purge ad-hoc hacks | 🚨 Urgent | ⏳ Pending |
| **0.10** | Domain/Clean | Decouple ad-tech ontology from `ArchitectureViewEngine` -> `.codeexplorer/domains.json` / graph clustering | High | ⏳ Pending |
| **1.1** | Agent/MCP | Implement `get_architecture_view` MCP tool via `ArchitectureViewEngine` | High | ⏳ Pending |
| **1.2** | Agent/MCP | Add `--type runtime\|build\|all` filter to `get_project_dependencies` | High | ⏳ Pending |
| **1.3** | Agent/MCP | Implement `get_service_contracts` MCP tool (ingress / egress) | High | ⏳ Pending |
| **1.4** | Agent/MCP | Implement `trace_cross_service_flow` MCP tool | High | ⏳ Pending |
| **1.5** | CLI | Add commands `ce view architecture`, `ce dependencies --type`, `ce contracts` | High | ⏳ Pending |
| **1.6** | Formats | Add TOON, Mermaid, and Markdown serializers to `ArchitectureViewEngine` | High | ⏳ Pending |
| **2.1** | Parsers | ASP.NET Core route composition and Minimal API `MapGroup` | Medium | ⏳ Pending |
| **2.2** | Parsers | C# constructor DI mapping and resolution to implementations via `[:IMPLEMENTS]` | Medium | ⏳ Pending |
| **2.3** | Parsers | Declarative HTTP clients (Refit/RestEase) and URI resolution | Medium | ⏳ Pending |
| **2.4** | Parsers | EF Core `DbSet<T>` / `ToTable` and Dapper SQL lineage | Medium | ⏳ Pending |
| **3.1** | Cypher | Relationship functions (`type(r)`, `properties(r)`, `startNode`, `endNode`) | Medium | ⏳ Pending |
| **3.2** | Cypher | Cartesian product decomposition in `OPTIONAL MATCH` | Medium | ⏳ Pending |
| **3.3** | Cypher | Path predicates (`EXISTS((a)->(b))`) and list quantifiers | Low | ⏳ Pending |
| **4.1** | Concurrency | Incremental file watcher `ce watch` with debounce | Medium | ⏳ Pending |
| **4.2** | Concurrency | Read-only connection pool for MCP tools | Medium | ⏳ Pending |
| **5.1** | Testing | Synthetic 100k-node benchmark graph for CI | Low | ⏳ Pending |
