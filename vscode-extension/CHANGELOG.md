# Changelog

All notable changes to the CodeExplorer project (CLI, MCP Server, and VS Code Extension) are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.57.0] - 2026-10-24

### Added
- **Universal `<BaseGraphMap />` & Migrated `ProjectMapView`**:
  - Extracted universal `<BaseGraphMap />` component (`BaseGraphMap.tsx`) unifying layout engines, HUD controls, search, multi-select grouping, orbit management, and right-docked sidebar across `Architecture Map` and `Project Map`.
  - Migrated `ProjectMapView` onto `<BaseGraphMap />` with `Concentric: Barycentric` layout, `√Weight` node sizing, and `Icon + Metric` (`19f`, `7f`) content mode by default.
- **Standalone `Architecture Matrix` View**:
  - Extracted **Architecture Matrix** (`codeExplorer.openArchitectureMatrix`) as a dedicated view in the left **Architecture Diagrams** sidebar tree, removing the legacy `Grid` mode (`DomainGridView`) and `Gates` tab from the map sidebar.
- **Declarative 6-Zone Inspector (`<DeclarativeInspector />`) & Tier Explainability**:
  - Added schema-driven 6-zone node inspector (`inspectorSchema.ts`, `DeclarativeInspector.tsx`) with `tierReason` explainability under the Tier badge explaining why each node was placed on its orbit.
- **Semantic Node Shapes, `Concentric: Barycentric` Layout & Split HUD Menus**:
  - Added semantic Cytoscape shapes (`ellipse` for executable/code nodes and modules vs `round-rectangle` for databases, tables, procedures, topics, and external services).
  - Added `Node Content` (`Icon Only`, `Icon + Code`, `Icon + Metric`) and `Node Size` (`Uniform`, `√Weight`, `Connections`) modes, plus `🪐 Concentric: Barycentric` layout.
  - Split the HUD display menu into two dedicated menus: **`📐 View`** (layout & visual representation types) and **`🎛️ Parameters`** (sliders & tuning), fixing left-edge clipping.

### Fixed
- **Project Map Module Partitioning, Visual Differentiation & Edge Lifting**:
  - Fixed path normalization in `ArchitectureViewEngine.ProjectMap` so `EntryPoint`, `Endpoint`, `Member`, and `Query` nodes with absolute paths are matched alongside workspace-relative `File`, `Type`, and `Function` nodes.
  - Included `TRIGGERS` edges and synthesized `USES_TYPE` edges from `Type->Member` (`m.data_type`) and `EntryPoint` handlers (capturing C# constructor DI and MediatR command/handler dependencies).
  - Filtered out non-code, boilerplate, and generated files (`GlobalUsings.cs`, `appsettings*.json`, `Migrations/`, `*.Designer.cs`, `*ModelSnapshot.cs`, `wwwroot/`) and internal MediatR pseudo-topics (`ws:top:mediatr:*`).
  - Added automatic depth-2 submodule expansion when a single top-level folder holds $\ge 45\%$ of project files (e.g. expanding `Application/` into `Application/Commands`, `Application/Queries`, `Application/DomainEventHandlers`, etc.).
  - Differentiated Project Map modules visually by architectural tier (`Ingress`, `AppLogic`, `Core`, `DataAccess`, `Foundation`) with distinct colors, icons, and tags.

---

## [1.56.0] - 2026-10-22

### Added
- **Project Map (C3 internal module view)**:
  - New `project-map` architecture view (`ArchitectureViewType.ProjectMap`) for a single project. It groups non-test files into folder modules: it detects the source root (e.g. `src/main/java/com/acme/billing`), drops to sub-folder or file granularity for flat projects, and merges anything beyond 30 modules into `(other)`.
  - Lifts `CALLS` / `USES_TYPE` / `IMPLEMENTS` / `INHERITS_FROM` symbol edges into module→module dependencies with reference counts, distinct files and up to 8 caller→callee evidence samples (`file:line`).
  - Links modules to the tables, topics, external services and databases their files access, including `Query`→`Table` hops.
  - Rings are classified from topology first: endpoint/entry-point evidence, fan-in/fan-out, instability `I = Ce / (Ca + Ce)` and data-access share. Folder names are only used to break ties.
  - Detects dependency cycles of any length with strongly connected components (Tarjan), not just A↔B pairs.
  - Available as `ce view -l project-map -s <project>`, MCP `get_architecture_view(level: "project-map")`, `GET /api/view?view=project-map&scope=<project>` and WebSocket `GET_VIEW`.
  - View-only projection: no new node labels, relationship types or persisted properties. Module nodes reuse the existing `Folder` / `File` kinds.
- **VS Code: Project Map panel**:
  - New **Project Map** entry right below **Architecture Map** in the sidebar.
  - Concentric rings (Ingress → Application → Core → Data → Foundation → Resources) with neighbour-aware ordering, pan/zoom, project selector, and **Utilities** / **Resources** toggles.
  - A **⚠️ N module cycles** filter isolates cyclic modules; cyclic edges are drawn as amber dashed arcs.
  - Inspector shows module metrics, the reason for its ring, dependencies in both directions, its files, and click-to-code evidence for every dependency.
  - **Open in Project Map** action in the Architecture Map inspector, and a suggestion banner on single-project workspaces.

---

## [1.55.1] - 2026-10-22

### Fixed
- **C# & TypeScript Library Parser Recursion & Cycle Guards (`StackOverflowException`)**:
  - Fixed unbounded mutual recursion (`TryResolveVariableUri` $\leftrightarrow$ `FindUriInScope` $\leftrightarrow$ `ExtractUriFromExpression`) in `HttpClientLibraryParser`, `RestSharpLibraryParser`, and `AspNetCoreLibraryParser.MinimalApis` when resolving self-referential or circular variable assignments (`url = $"{url}/..."`, `url = string.Format(url, ...)`).
  - Threaded hop depth limits (`depth <= 8`) and visited variable cycle tracking (`HashSet<string>`) across all variable/scope URI and route group resolvers.
  - Guarded expression unwrapping loops in `GcpLibraryParser` against self-referential fallback nodes.

---

## [1.55.0] - 2026-10-22

### Added
- **Strict 5-Layer Ontology Hierarchy & Zero Upward Edges**:
  - Standardized ontology layer taxonomy: `Layer 1: File Structure`, `Layer 2: Project Structure`, `Layer 3: Syntax Structure`, `Layer 4: Semantic Structure`, `Layer 5: Logical Structure`.
  - Enforced strict unidirectional ontology layer invariant (Zero Upward Edges): edges originate at their source node layer and may only point horizontally (intra-layer) or downward (grounding to lower layers).
  - Persisted Level 5 Logical Structure nodes (`LogicalStructureNode`, `DomainNode`, `BoundedContextNode`) directly in the graph database (`SIG-O-B-003`).
- **Decoupled Syntactic & Semantic Analyzers**:
  - Eliminated `ISyntaxEnricher` and parser-specific enrichers; AST member and variable extraction is handled cleanly in `Layer3SyntacticAnalyzer`, and semantic resource extraction in `ResourceSemanticAnalyzer`.
- **UI & Diagram Enhancements**:
  - Updated `/api/ontology/layers` and VS Code TreeView to report the standardized 5 ontology layers with Layer 5 logical domain entities (`Domain`, `BoundedContext`).
  - Reordered Architecture Diagrams in the VS Code sidebar from High Level to Low Level: `Bounded Context Map` › `Architecture Map` › `Architecture Tiers` › `Project Flow`.
  - Added node presentation badges and icons for `Domain` (`🌐`) and `BoundedContext` (`🧩`).

---

## [1.54.1] - 2026-10-22

### Fixed
- **Domain Topology & Semantic Runtime Refinements**:
  - Refined semantic runtime correlation and domain topology resolution across bounded contexts.

---

## [1.54.0] - 2026-10-22

### Improved
- **Architecture Graph Group Sizing**:
  - Increased group node card dimensions on the architecture canvas for improved label readability and badge layout.

---

## [1.53.5] - 2026-10-21

### Fixed & Improved
- **Domain Map & Inspector Refinements**:
  - Materialized library macro-edges in `Layer5GlobalAnalyzer` (`1.53.5`).
  - Enabled `Library` and `Test` workload categories in the Domain Architecture Map (`1.53.4`).
  - Added configurable orbit layout reordering, merging, and splitting with domain filtering (`1.53.3`).
  - Added business domain sectors, center UI hub, minimum orbit node distance, wrap-around separation, and 2-opt link crossing optimization (`1.53.2`).
  - Fixed inspector header overlap, converted evidence sections to collapsible cards, and fixed evidence layout scrollbars (`1.53.1`).

---

## [1.53.0] - 2026-10-21

### Added
- **Universal Edge & Node Code Evidence Inspector**:
  - Added `GET /api/architecture/edge-evidence` (including grouped macro-edges) and `GET /api/architecture/node-evidence` endpoints returning unified `ArchitectureEvidenceItemDto` items.
  - Added `EdgeEvidenceSection` and `NodeEvidenceSection` in the VS Code architecture inspector UI to trace any architectural node or arrow directly to the exact source files, lines, symbols, and configuration entries that produced it.

---

## [1.52.1] - 2026-10-21

### Fixed
- **Shared File Parser Registry & Topic Case Preservation**:
  - Centralized shared file parser registry across language analyzers, preserved canonical broker topic ID casing, and improved scoped constant resolution.

---

## [1.52.0] - 2026-10-21

### Added
- **Architectural Evolution & Topology Gates**:
  - Added built-in Semantic Invariant Gates `SIG-P-B-007` (Orphaned Project Modules), `SIG-P-B-008` (Unbounded Fan-Out), and `SIG-P-B-009` (Cross-Domain Data Coupling), expanding the `BuiltInGateCatalog` to 31 polyglot and architectural gates.
  - Enhanced database evidence scoping (`EvidenceScope`) and two-hop `Table` / `Procedure` traversal.

---

## [1.51.4] - 2026-10-21

### Performance & Fixed
- **Cypher Syntax Highlighting & Indexer Optimization**:
  - Replaced regex-based Cypher highlighting in the VS Code extension with a single-pass tokenizer to prevent markup corruption (`1.51.4`).
  - Eliminated Phase 0b regex backtracking and optimized post-index symbol queries (`1.51.3`).
  - Introduced `ProjectParserRole.Fallback` for `ScriptProjectParser` and deduplicated `WorkspaceFileFilter` extensions (`1.51.1` – `1.51.2`).

---

## [1.51.0] - 2026-10-20

### Added
- **Dynamic Platform-Driven Workspace File Filter**:
  - Replaced hardcoded file extension lists with a dynamic `WorkspaceFileFilter` that queries connected platform and file parsers at runtime.

---

## [1.50.0] - 2026-10-20

### Added
- **Enterprise .NET Legacy Support (WCF, EF6, XML Config)**:
  - Added `WcfLibraryParser` for `[ServiceContract]`, `[OperationContract]`, and WCF client channel resolution.
  - Added `.NET` XML configuration parsing (`web.config` / `app.config` connection strings, WCF bindings/endpoints, appSettings) and Entity Framework 6 (`DbContext` / `ObjectContext` / EDMX) support.
  - Added desktop application workload classification (`WinForms`, `WPF`).
- **Unified Hybrid Release Distribution**:
  - Streamlined GitHub Actions release pipeline to build native standalone binaries (`win-x64`, `osx-arm64`) alongside the universal `codeexplorer.cli` NuGet tool package fallback.

---

## [1.49.4] - 2026-10-20

### Fixed
- **TypeScript AST Guards & Pub/Sub Resolution**:
  - Added recursion depth guards across TypeScript `AstHelper` functions (`1.49.4`).
  - Preserved hop depth through TypeScript template expressions (`1.49.3`).
  - Resolved GCP Pub/Sub topic names from subscription config paths and prevented path concatenation on URL prefixes (`1.49.2`).
  - Formatted server logs cleanly in the VS Code Output channel (`1.49.1`).

---

## [1.49.0] - 2026-10-20

### Added
- **Rust Workspace Resolution & AWS SDK for Rust**:
  - Added Cargo workspace member resolution, `AwsSdkRustLibraryParser` (S3, SQS, SNS, DynamoDB, Lambda, Kinesis), and gate invariance decomposition.

---

## [1.48.7] - 2026-10-20

### Fixed & Improved
- **Webview Group Persistence, Smooth Transitions & God Class Decomposition**:
  - Eliminated loading flash and node flicker when toggling custom groups (`1.48.7`).
  - Preserved webview UI state in-memory across tab switches (`1.48.6`) and revived webview panels across VS Code restarts (`1.48.4`).
  - Added deterministic workspace port derivation (`1.48.5`).
  - Added `SIG-C-R-001` (God Class Prohibition gate) and decomposed large classes across Core, Cypher, Parsers, and Extension into focused partial/modular files (`1.48.1` – `1.48.3`).

---

## [1.48.0] - 2026-10-19

### Added
- **Visual Node Grouping & Edge Bundling in Architecture Map**:
  - Added collapsible visual node groups, custom user-defined groups, automatic edge bundling across group boundaries, and internal connection extraction in the Domain Architecture View.

---

## [1.47.2] - 2026-10-19

### Added & Fixed
- **Database Decision Evidence & Strict Evidence-Based DB Creation**:
  - Enforced strict evidence-based database node creation (`SIG-O-B-002`), singular/plural DB consolidation, and `Redis.<domain>` naming (`1.47.2`).
  - Added Database Decision & Query Evidence inspector in the Architecture Map (`1.47.1`).

---

## [1.47.0] - 2026-10-19

### Added
- **Full 5-Layer Rust Language Support (`CodeExplorer.Parser.Rust`)**:
  - Delivered `RustSyntaxExtractor` (`tree-sitter-rust`), `CargoProjectParser`, `RustTypeResolver`, and 12 Rust ecosystem library parsers (`axum`, `actix-web`, `rocket`, `tonic`, `reqwest`, `sqlx`, `sea-orm`, `diesel`, `rdkafka`, `lapin`, `async-nats`, `tokio`).
  - Added 4 Rust Semantic Invariant Gates: `SIG-R-S-001` (Unsafe Block Audit), `SIG-R-A-001` (Async Lock Across Await), `SIG-R-C-001` (Unwrap/Expect in Production), and `SIG-R-D-001` (Raw SQL Injection Guard).

---

## [1.46.22] - 2026-10-19

### Added & Fixed
- **Workload Roles (`PrimaryRole` + `SecondaryRoles`), Circular Badges & Parity Gates**:
  - Introduced `PrimaryRole` + `SecondaryRoles` workload classification so hybrid services (e.g., API + Background Worker) preserve all runtime personas (`1.46.15`).
  - Added `SIG-O-B-001` enforcing 1-to-1 parity between Layer 4 runtime entities and the Domain Architecture Map (`1.46.16`).
  - Redesigned graph nodes with circular SVG role badges (`SVC`, `DB`, `MSG`, `EXT`, `APP`, `WRK`, `CLI`, `LIB`), dynamic text wrapping, and radial group clustering (`1.46.17` – `1.46.22`).
  - Unified `ServerTransport` injection across `GraphPanel` and Webviews, eliminating race conditions during startup (`1.46.7` – `1.46.14`).

---

## [1.46.6] - 2026-10-17

### Fixed
- **Webview Transport Event Loop Lifecycle**:
  - Resolved hanging test suite in Node 24 where native `WebSocket` instantiation triggered indefinite reconnection timers without `.unref()`.
  - Added `MAX_RECONNECT_ATTEMPTS = 8` cap in `WebviewServerTransport`, marking state as `error` on exhaustion instead of retrying indefinitely.
  - Unrefed all reconnect and keep-alive timers (`unref()`) to prevent holding the Node.js event loop open during unit testing or teardown.
  - Restricted native WebSocket instantiation in `connect()` strictly to browser webview environments (`window !== undefined`) or explicit test mock factories (`wsFactory`).
  - Added explicit `disconnect()` / `dispose()` teardown in unit tests and enforced a 15-minute timeout on CI and Release workflows.

---

## [1.46.0] - 2026-10-17

### Added
- **Visual Semantic Invariant Gate Editor (`GateEditorView`)**:
  - Added a full-view interactive Semantic Invariant Gate editor in the VS Code webview with live OpenCypher syntax highlighting, dry-run evaluation preview, and YAML schema validation.

---

## [1.45.0] - 2026-10-16

### Added & Improved
- **First-Class TestSuite Workloads & Decoupled View Generators**:
  - Added first-class `TestSuite` workload detection with beaker icons (`🧪`) and dedicated visibility toggles (`1.45.0` – `1.45.2`).
  - Fixed $O(N \times M)$ scan bottleneck in `IdentifierSearchEngine` (`1.45.3`).

---

## [1.44.0] - 2026-10-15

### Added
- **Webview Visibility Panel & Compact/TOON Outputs**:
  - Added interactive Visibility filter panel in the Architecture Webview to toggle node categories, workload roles, and edge kinds.
  - Defaulted CLI and MCP query outputs to token-efficient compact/TOON formats.

---

## [1.43.0] - 2026-10-15

### Added
- **CLI `ce callers` & `ce impact` Commands**:
  - Added first-class `ce callers` and `ce impact` CLI commands with cross-service route resolution.

---

## [1.42.0] - 2026-10-15

### Added
- **In-Process Identifier Vector Search**:
  - Added fast in-process identifier and semantic token search across indexed graph symbols.

---

## [1.41.8] - 2026-10-15

### Added
- **Workspace Scan Exclusions (`.codeexplorer/settings.json`)**:
  - Added support for gitignore/glob-style workspace exclusion patterns in `.codeexplorer/settings.json` (`{ "exclude": [...] }`), automatically created by `ce init`.
  - `GitIgnoreMatcher` and `WorkspaceIndexer` now load `.codeexplorer/settings.json` exclusions and automatically prune stale `Project` and symbol nodes when manifests are excluded during incremental rescans.
  - Added **Settings** item under `Management` in the CodeExplorer sidebar (`codeExplorer.openWorkspaceSettings`) and **Exclude from Scan** (`codeExplorer.excludeFromScan`) in the VS Code Explorer context menu, with automatic incremental rescan on save.

---

## [1.41.7] - 2026-10-15

### Added
- **Unified Server Transport Layer (`ServerTransport`)**:
  - Centralized all extension-to-server HTTP communication (`get`, `post`), lifecycle management, and event subscriptions into `ServerTransport`.
  - **Lifetime Sticky Port Allocation**: Allocates a dedicated free TCP port once per VS Code window lifetime and retains it across server restarts, ensuring WebSocket and HTTP endpoints never drift when multiple VS Code windows are open.
  - **Pub/Sub Event Bus**: Added typed state events (`onDidChangeState`, `onDidServerStart`, `onDidServerStop`) and topic-based subscriptions (`subscribe`/`publish`).
  - **Liveness Watchdog & Auto-Restart with Circuit Breaker**: Periodic HTTP health checks, automatic exponential-backoff restart on unexpected server exit, sliding-window crash circuit breaker, and transparent retry on transient socket errors.

### Fixed
- **Multi-Window Connection Loss & Concurrent Starter Gate Race**:
  - Disabled premature 60s server self-termination (`--idle-timeout 0`) when a second VS Code window is in the background without an active WebSocket tab.
  - Fixed concurrent `IOException` race in `FileGateRepository.InitStarterGatesAsync` when multiple processes initialize starter gate files simultaneously.

---

## [1.41.4] - 2026-10-14

### Added & Improved
- **Direct Gate YAML Editing**:
  - Added an **«Edit»** button opposite each Semantic Invariant Gate in the webview (`DomainGatesPanel`), opening the gate's `.yaml` file directly in the VS Code editor.
  - Added an **«Edit YAML»** action button in the expanded details section of each gate card next to Evaluate and Ratchet.
  - Added an inline edit pencil icon (`codeExplorer.openGateYaml`) and context menu action for gate items in the Left Sidebar TreeView (`codeExplorerView`).
  - Added command `codeExplorer.openGateYaml`: resolves gate YAML paths from `.codeexplorer/gates/**`, automatically generating a starter YAML template if editing a built-in gate that hasn't been extracted to disk yet.

---

## [1.41.3] - 2026-10-14

### Changed & Improved
- **Explicit On-Demand Gate Evaluation**:
  - Eliminated automatic execution of Semantic Invariant Gates on extension activation, tree view loading, or panel opening. Gates are now evaluated **strictly upon explicit user request** (e.g. clicking "Check" in the header, clicking "Evaluate" on an individual gate card, or executing the evaluation command).
  - Updated backend `GET /api/gates` to return lightweight gate definition metadata (manifest schemas and Pending status in ~1ms without Cypher queries) whenever `evaluate != true` and no cached evaluation exists.
  - Refactored `GateStateManager.loadSuite()` to retrieve definitions via `GET /api/gates` without triggering background or automatic Cypher graph executions.
  - Enhanced UI for non-evaluated gate suites:
    - Left Sidebar TreeView: displays `{total} configured · Not evaluated` on the root section and clean neutral circle-outline icons for pending gates.
    - Right Webview (`DomainGatesPanel`): displays neutral `{total} gates` badge, informative `{total} gates configured · Click "Check" to evaluate` subtitle, and `NOT CHECKED` badges with explicit "Evaluate" buttons on cards.

---

## [1.41.2] - 2026-10-14

### Added
- **Centralized Gate State Manager (`GateStateManager`)**:
  - Implemented a unified single-source-of-truth service (`GateStateManager`) in the VS Code extension host managing gate suite summaries, evaluation lifecycles, and ratchet operations.
  - Eliminated duplicate gate calculations: both the Left Sidebar TreeView and Right Webview (`DomainGatesPanel`) query through `GateStateManager` rather than dispatching independent HTTP evaluation requests.
  - Added in-flight request deduplication: concurrent requests for gate evaluations attach to the single active calculation promise, preventing redundant Cypher query executions.
  - Integrated bidirectional event broadcasting (`onDidChangeSuite` -> `GATES_UPDATED`), ensuring all open webviews and sidebar tree items reactively update in real time with synchronized pass/fail metrics, spinners, and violation details.

### Performance & Caching
- **Backend Gate Suite In-Memory Caching (`ce serve`)**:
  - Added thread-safe in-memory caching (`SemaphoreSlim` + `_cachedGateSuite`) in `WebSocketServerHandler`.
  - `GET /api/gates` now serves cached gate results in sub-millisecond time.
  - Caches are automatically invalidated on incremental scans (`ce scan`), explicit check requests (`POST /api/gates/check`), ratchet updates (`POST /api/gates/ratchet`), or starter gate initialization (`POST /api/gates/init`).

---

## [1.41.1] - 2026-10-14

### Fixed
- **Domain Gates Panel Layout & Card Flexbox Compression**:
  - Fixed flex-shrink squishing on collapsed gate cards by enforcing `flexShrink: 0`, `minHeight: 38px`, and dedicated icon wrapper spans in `DomainGatesPanel.tsx`, preventing card headers, chevrons, and status badges from clipping.
  - Added `no-body-padding` and `overflowX: 'hidden'` to `DomainGatesPanel` to eliminate double scrollbars and nested flexbox container compression.
  - Aligned panel header title to `Invariant Gates` with full tooltip to prevent truncated ellipsis (`Semantic Invariant G...`) in docked sidebar layouts.
  - Capped rendered violations in the webview to the first 100 items with an overflow indicator to safeguard UI performance on large violation sets.
- **Call Semantic Classifier & Hardcoded Arguments Gate (SIG-M-I-001)**:
  - Enhanced `CallSemanticClassifier` to categorize standard string manipulations (`Replace`, `ReplaceAll`, `Split`, `Substring`, `Slice`, `Trim`, `ToLower`, `ToUpper`, etc.), JSON serialization, client storage (`Cookie.get`/`Cookie.set`), URL parameters (`URLSearchParams`), and browser DOM methods into `Formatting`, `Storage`, and `WebAPI` semantic roles.
  - Updated `SIG-M-I-001` YAML gate manifests in `.codeexplorer/gates/` to ensure `AND coalesce(lit.role, 'Business') = 'Business'` is consistently evaluated, eliminating thousands of false positives on platform utilities.

---

## [1.41.0] - 2026-10-14

### Added
- **Asynchronous & Individual Semantic Invariant Gate Evaluation**:
  - Added fast gate definitions retrieval (`GET /api/gates?evaluate=false`) allowing the VS Code extension tree provider to display the full gate suite immediately without blocking.
  - Implemented asynchronous background evaluation queue in the VS Code TreeView with real-time per-gate progress indicators (clock/watch icon, "Evaluating..." state, dynamic count of evaluated gates in the root item).
  - Supported `Pending` and `Evaluating` statuses across the VS Code TreeView and DomainGatesPanel webview.

### Performance
- **Cypher Subquery Optimization (Subquery JOIN vs CROSS JOIN)**:
  - Eliminated `CROSS JOIN` from `BuildSubqueryPath` in `SqliteCompiler.Expressions.cs`, allowing the SQLite query optimizer to dynamically reorder correlated subquery tables and utilize edge indexes (`idx_edges_to_kind_from`, `idx_edges_from_kind_to`).
  - Achieved up to 1,300x faster evaluation on complex pattern expression gates (e.g. `SIG-A-B-001` Call Lineage Provenance reduced from 14.8s to 15ms).

### Fixed
- **Messaging Flow Direction and Pub/Sub Topic Normalization**:
  - Enforced canonical data flow direction (`Topic -> Service` for subscriptions, `Service -> Topic` for publications) across `ArchitectureViewEngine` and `ProjectFlowView`.
  - Subscriber services now strictly attach subscription edges to their incoming socket (`receives`), preventing inverted outbound dashed arrows from their `sends` socket into topics.
  - Added broker-aware topic name normalization in `Layer5AnalysisParser`, canonicalizing broker topics to kebab-case while preserving in-memory event class names (`mediatr`, `cqrs`, `spring`).

---

## [1.40.0] - 2026-10-14

### Added
- **BigQuery and ClickHouse Go Semantic Parsers**:
  - Added `BigQueryGoLibraryParser` to extract BigQuery datasets, tables, query strings, and `USES_DB` relationships to `bigquery` from Go services using `cloud.google.com/go/bigquery`.
  - Added `ClickHouseGoLibraryParser` to extract ClickHouse tables, batch operations, DDL/DML queries, and `USES_DB` relationships to `clickhouse` from Go services using `github.com/ClickHouse/clickhouse-go/v2`.
- **GCP Pub/Sub Go Messaging Parser & Normalized Ingest Pipelines**:
  - Enhanced `PubSubGoLibraryParser` to extract topics and subscriptions across composite literals (`WorkerDef` streaming pipelines), struct event tags (`event_type`), topic constants, and client publishing/receiving.
  - Normalized GCP Pub/Sub topics to kebab-case while preserving snake_case raw identifiers for backwards compatibility.
- **Workload Semantic Edge Lifting & Architecture Views**:
  - Fixed symbol-level and project-level edge lifting in `ArchitectureViewEngine` when projects are upgraded to `Service`, `Worker`, or `App` workload nodes, ensuring edges to topics, databases, and external services are properly re-anchored to the upgraded workload node.
  - Preserved project directory paths and metadata during workload node promotion.

---

## [1.39.0] - 2026-10-13

### Added
- **Pothos Code-First GraphQL Schema Support**:
  - Added `PothosLibraryParser` semantic extension supporting single-field (`queryField`, `mutationField`, `subscriptionField`) and batch (`queryFields`, `mutationFields`, `subscriptionFields`, `queryType`, `mutationType`, `subscriptionType`) root definitions across any `SchemaBuilder` instance (`clientBuilder`, `adminBuilder`, `monitoringBuilder`, etc.).
  - Inline `resolve` and `subscribe` arrow functions are automatically qualified with their owning field name (e.g. `publishedItems.resolve`, `auditLogs.resolve`) and linked to the `Endpoint` via `TRIGGERS` and `EXPOSED_BY`.
- **Native `.graphql` / `.gql` Parser & Apollo Client Operations**:
  - Added `GraphQLFileParser` and `GraphQLDocumentExtractor` to parse executable GraphQL operations (`query`, `mutation`, `subscription`) and SDL root types (`type Query`, `type Mutation`, `type Subscription`), resolving field aliases, inline fragments, and multi-root-field selection sets without relying on hook naming heuristics.
  - Enhanced `GraphQLClientLibraryParser` to extract operation definitions and root field selections from both `gql` template literals and compiled `TypedDocumentNode` AST objects in TypeScript/JavaScript codegen files, linking `useQuery(<Op>Document)` and `client.query({ query: <Op>Document })` calls.
- **Generic DI Container Type Resolution in TypeScript**:
  - Inferred target class bindings from generic type arguments in both chained calls (`ctx.container.resolve<AuditLogRepo>('auditLogRepo').list(...)`) and local variable assignments (`const repo = ctx.container.resolve<PublishedItemRepo>(...)`).
- **End-to-End GraphQL Late Binding & MCP Query Support**:
  - Connected frontend GraphQL operations and hooks directly to backend Pothos resolvers (`Component -> use...Query -> <field>.resolve -> Repo.method`).
  - Enabled direct lookup by `graphql_field` and `graphql_operation` in `find_symbol`, `get_project_entry_points`, `get_call_chain`, and `analyze_code_impact`.

---

## [1.38.0] - 2026-10-13

### Added
- **Semantic Invariant Gates UI in VS Code Extension**:
  - Added interactive **Semantic Invariant Gates** tree view in the VS Code sidebar and a dedicated dockable Gates panel in the architecture webview.
  - Added one-click gate execution, ratchet baseline updates, and clickable violation navigation directly to `file:line:col` in the editor.
- **Multi-Occurrence Violation Fingerprinting & Gate API Endpoints**:
  - Added occurrence-indexed SHA-256 baseline fingerprinting so multiple violations of the same rule within a single symbol are tracked deterministically.
  - Exposed HTTP and WebSocket API endpoints (`/api/gates`, `/api/gates/check`, `/api/gates/ratchet`) for real-time IDE integration.

### Performance
- **Cypher Recursive CTE Predicate Pushdown**:
  - Pushed `WHERE` predicates directly into recursive CTE anchor queries and added support for undirected variable-length path patterns.
- **Single-Pass File Hashing**:
  - Eliminated double file reads during indexing by computing SHA-256 content hashes in a single pass and isolated architecture view cache keys per workspace.

### Changed
- **Core Engine Modularization**:
  - Decomposed `ArchitectureViewEngine`, `SqliteGraphClient`, and `PostIndexAnalyzer` into focused partial class modules for maintainability and adherence to method-length invariants.

---

## [1.37.0] - 2026-10-13

### Added
- **Unified Right-Hand Inspector Sidebar in Domain Architecture Map**:
  - Consolidated `Info`, `Orbits`, and `Hidden` panels into a single right-hand sidebar with a segmented tab switcher, maximizing canvas area from the left edge.
  - Added a dedicated **Fit** button to the Project Flow toolbar and preserved manual viewport zoom/pan when expanding cards or connection handles.
- **Expanded Polyglot Semantic Extensions (35 Sample Projects Matrix)**:
  - **TypeScript / JavaScript Testing**: Added AST semantic extensions for **Jest**, **Vitest**, **Mocha**, **Cypress**, and **Playwright**.
  - **Frontend Frameworks**: Added AST semantic extensions for **React**, **Vue**, **Angular Core**, **Svelte**, and **Vite** (`React.lazy`, `defineAsyncComponent`, `@Component`, `@NgModule`).
  - **Cloud & Analytical SDKs**: Added semantic extensions for **Stripe**, **AWS SDK** (SQS, S3, DynamoDB), **Azure SDK** (Service Bus, Blob Storage, Cosmos DB), **ClickHouse**, and **BigQuery**.
  - **HTTP & Web Frameworks**: Added Node.js HTTP client extensions (`request`, `undici`, `urllib`), Python web/HTTP extensions (**Flask**, **Django**, `requests`, `httpx`, `aiohttp`), Go web/database extensions (**Gin**, **Fiber**, **Echo**, `net/http`, `resty`, `pgx`), and Java messaging extensions (**Spring Kafka**, **Spring RabbitMQ**).
- **Strongly-Typed Dependency Categories & Gate `SIG-C-C-001`**:
  - Introduced `DependencyCategory` enum (`Library`, `ServiceCall`, `Database`, `Messaging`, `Configuration`, `UnidentifiedDependency`) and gate `SIG-C-C-001` (Disallow Stringly-Typed Taxonomies).
- **CLI Ergonomics (`ce scan` & `ce gate`)**:
  - Added `--auto-init`, `--auto-scan`, and `--clear` options to streamline one-command workspace onboarding and CI checks.

### Fixed
- **TypeScript Module Import Call Resolution & JS/TS Monorepo `packages/` Support**:
  - Resolved function calls across ES module imports and workspace packages (closing #3).
  - Preserved `packages/` directories in JS/TS monorepos (`pnpm-workspace.yaml`, `lerna.json`, `turbo.json`) while still excluding NuGet `packages/` caches (closing #2).
- **Library vs. Service Call Classification**:
  - Fixed external package dependencies (`Package` nodes) being misclassified as `service_call` instead of `library`.

### Performance
- **Massive Indexing, Preloading & Subpath Clear Speedups**:
  - Replaced unindexed `json_each` virtual table scans in `SqliteGraphClient` with temporary indexed B-tree tables and added `idx_nodes_name_kind`, reducing Layer 5 symbol preloading on large monorepos from 22 minutes to under 400ms.
  - Eliminated over 2.8 billion redundant string comparisons in Layer 4, Layer 5 late binding, and `PostIndexAnalyzer` via `O(Files)` project pre-grouping and route memoization.
  - Optimized `DeleteSubpathHierarchyAsync` with index-directed recursive CTE joins (`INDEXED BY idx_edges_from_kind_to`), cutting subtree clear time from 5 minutes to under 1 second.

---

## [1.36.0] - 2026-10-12

### Added
- **Dynamic Table Template Literals**:
  - Normalized BigQuery table shard wildcards (`table_*`) and dynamic template interpolations.
- **Fast SQLite Subpath Clear**:
  - Migrated subtree deletion to Dapper recursive CTE queries for sub-second workspace resets.

---

## [1.35.0] - 2026-10-12

### Added
- **Dapper Graph Persistence & `routing-controllers` Support**:
  - Migrated `SqliteGraphClient` queries to Dapper and deconstructed hardcoded workspace conventions.
  - Added TypeScript semantic support for `routing-controllers` decorators (`@JsonController`, `@Get`, `@Post`, etc.).

---

## [1.34.0] - 2026-10-11

### Added
- **Extended Semantic Roles for Call Literals & Attributes**:
  - Added `FormAccess`, `Storage`, `WebAPI`, `TestIdentifier`, and `State` semantic roles to `CallSemanticClassifier` to separate UI/browser primitives from business literals in `SIG-M-I-001`.
  - Added `IComponentLibraryParser.IsTestSource` for authoritative test file classification across Jest, Vitest, Mocha, Cypress, Playwright, Jasmine, and Karma.
  - Added dogfooding gate `SIG-M-R-001` (Method Length Ceiling <= 500 LOC) and refactored core engine methods to achieve zero violations.

### Fixed
- **Stale Node Pruning on Incremental Rescan**:
  - Automatically removes outdated AST nodes and edges when modified files undergo structural changes.
- **OpenCypher Single-Node Pattern Predicates**:
  - Fixed Cypher parser support for anonymous single-node pattern expressions in `WHERE` and `EXISTS` clauses (e.g. `NOT (:Endpoint { path: es.path })`).

---

## [1.33.0] - 2026-10-11

### Added
- **Catch Clause AST Modeling & Gate `SIG-M-I-005` (No Empty Catch Blocks)**:
  - Introduced `CatchClauseNode` in Layer 3 AST across C#, TypeScript/JavaScript, Java, and Python, tracking empty blocks (`is_empty`) and explanatory comments (`has_comment`).
- **Call Literal Semantic Classification**:
  - Added `CallSemanticClassifier` categorizing call arguments and decorators into `Routing`, `ParamBinding`, `Config`, `Logging`, `QueryBuilder`, `DI`, `Security`, `OrmMapping`, `Validation`, `Formatting`, `Exception`, and `Business`.

### Fixed
- **TypeScript Arrow Function Caller Attribution**:
  - Attributed calls inside object-literal property arrows (`rxResource({ loader: () => ... })`) and class field arrows (`load = () => ...`) to named `Function` scopes while keeping `map`/`filter` callbacks anonymous.

---

## [1.31.0] - 2026-10-11

### Added
- **Semantic Invariant Gates (SIG) & Architectural Ratchets (`ce gate`)**:
  - Introduced declarative architectural guardrails evaluated via OpenCypher queries over SQLite `graph.db` with a Zero-Match contract (0 rows = PASS).
  - **Monotonic Technical Debt Ratchets**: Baselines (`.codeexplorer/baselines/*.json`) ensure technical debt never increases (`N(t+1) <= N(t)`), blocking new regressions while tolerating legacy debt.
  - **Fractal Orthogonal Invariant Matrix (FOIM)**: Organized invariants across 5 Scopes (`Method`, `Class`, `Module`, `Service`, `System`) and 4 Dimensions (`Contract`, `Rules`, `Resources`, `Boundaries`).
  - Added CLI commands `ce gate check`, `ce gate list`, `ce gate ratchet [--update]`, `ce gate init`, and the `check_semantic_gates` MCP tool.

---

## [1.28.0] - 2026-10-08

### Fixed
- **Template String Global Prefixes & Copied Manifest Alignment**:
  - Resolved NestJS/Express global route prefixes defined via template literals and aligned project names when `package.json` manifests are copied into build output directories.

---

## [1.27.0] - 2026-10-08

### Fixed
- **Short Symbol Call Chains & Direct Caller Endpoint Edges**:
  - Fixed `ScopeMarker` null handling during indexing, improved resolution of short function names in `get_call_chain`, and emitted direct caller-to-endpoint edges.

---

## [1.26.0] - 2026-10-08

### Added
- **Lexical AST Destructuring Constant Resolution**:
  - Added support for resolving object-destructured route and configuration constants across TypeScript files and improved inter-service call linking.

---

## [1.25.0] - 2026-10-06

### Changed
- **Modular Engine Manager in VS Code Extension**:
  - Refactored engine discovery, version probing, and binary lifecycle management into modular components and removed platform-specific workarounds.

---

## [1.24.0] - 2026-10-05

### Added
- **Cached Engine Resolution & Collapsible Tree Hierarchy**:
  - Cached resolved CLI binary paths in the VS Code extension, improved engine version probing over `stderr`, and defaulted sidebar tree nodes to collapsed state for faster navigation.

---

## [1.21.0] - 2026-10-04

### Fixed
- **NestJS Destructured Route Prefixes & Controller Decorators**:
  - Resolved controller route prefixes imported via nested object destructuring and standardized CI test fixtures.

---

## [1.20.0] - 2026-10-03

### Added
- **Project-Scoped Endpoint IDs & Compact MCP Schemas**:
  - Scoped `EndpointNode` identifiers by project, applied global route prefixes consistently, filtered configuration placeholder topics, and compacted MCP tool descriptions to reduce token overhead.

---

## [1.19.0] - 2026-10-02

### Added
- **Typed Cypher Queries & LINQ Provider (`GraphContext`)**:
  - Added strongly-typed Cypher query execution via `GraphContext` and `CypherQueryable<T>`, translating C# LINQ expressions into OpenCypher ASTs.
- **Intelligent Endpoint Scoring Engine**:
  - Added score-based resolution for ambiguous cross-service endpoints and domain routing.

---

## [1.18.0] - 2026-10-01

### Added
- **Static Test Reachability Coverage (`ce coverage` & `get_test_coverage`)**:
  - Analyzes static test reachability across AST symbols (`Function`, `Type`, `Member`) with `--threshold <percent>` CI gating.
- **Test Impact Analysis (`ce test` & `get_affected_tests`)**:
  - Maps `git diff` hunks to affected AST symbols and traverses reverse reachability via bounded BFS to identify impacted test methods and generate runner commands (`.NET`, `pytest`, `go test`, `npm test`, `mvn test`).
  - Smart test grouping automatically collapses runner commands to the class level when all tests in a class are impacted and filters out fixture lifecycle methods (`SetUp`, `TearDown`).
- **MCP Client Auto-Configuration (`ce mcp configure`)**:
  - Automatically registers CodeExplorer MCP server in Claude Desktop, Cursor, Antigravity, Cline, Roo Code, and Windsurf.

### Fixed
- **Database Reconciliation & Schema Isolation**:
  - Prevented generic database engine names (`PostgreSQL`, `Redis`, `TypeORM`, `Prisma`) from being claimed as project-specific aliases across services.

---

## [1.17.0] - 2026-10-01

### Added
- **Config-Driven Enterprise Domain Modeling & Pluggable LLM Providers**:
  - Canonicalized macro-domains to enterprise DDD problem spaces, eliminated service/table domain fragmentation, and added pluggable LLM provider configuration.

---

## [1.16.0] - 2026-09-30

### Added
- **On-Demand Engine Downloader & Smart Version Probing**:
  - The VS Code extension automatically downloads, extracts, and updates the native CodeExplorer CLI binary (`win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`) from GitHub Releases.
  - Added `CodeExplorer: Check & Update Engine` (`codeExplorer.updateEngine`) and `CodeExplorer: What's New` (`codeExplorer.whatsNew`) commands.

### Changed
- **Lightweight Universal VSIX**:
  - Reduced VS Code extension package size from ~130 MB down to under 3 MB, with synchronized versioning across CLI, NuGet, and VS Code Marketplace.

---

## [1.15.0] - 2026-09-30

### Added
- **Pluggable Component Parsers & Multi-Role Layer Classification**:
  - Decoupled architectural layer classification into extensible component parsers assigning primary and secondary roles.
  - Added last-updated timestamp to `ce status` and updated worker node palette to fuchsia for high visual contrast.

---

## [1.14.0] - 2026-09-30

### Added
- **100k-Node Synthetic Benchmark & Parser Audit Suite**:
  - Validated SQLite graph query throughput, BFS traversals, and indexing SLAs at 100k-node enterprise scale, and optimized recursive CTE index usage.

---

## [1.13.0] - 2026-09-30

### Added
- **Incremental Indexing & File Watcher Mode**:
  - Added SHA-256 content hashing and AST diffing to re-index only modified files, plus a background file watcher performing sub-second delta updates in SQLite.

---

## [1.11.0] - 2026-09-29

### Added
- **Docked Studio HUD & Avoid-Inner Edge Routing**:
  - Added two-tier control bar with font size slider, curve deflection routing around inner nodes, and dashed styling for transitive dependencies.

---

## [1.10.0] - 2026-09-28

### Added
- **AI Intent Distillation & Bounded Context Map**:
  - Integrated small language model (SLM) two-stage architectural intent distillation, Bounded Context mapping, and `distill_architectural_intents` / `get_bounded_context_map` MCP tools.

---

## [1.9.0] - 2026-09-27

### Added
- **Code-Intent-Distill Analyzer & Data-Flow Constant Resolution**:
  - Added post-indexing capability extraction and compile-time constant evaluation for dynamic route patterns and messaging topics.

---

## [1.7.0] - 2026-09-24

### Added
- **ArchitectureViewEngine & 5-Layer Ontology Schema**:
  - Centralized domain modeling and graph projection engine over the 5-layer ontology schema.

---

## [1.5.0] - 2026-09-23

### Added
- **First-Class Semantic Entities & ColdFusion Parser**:
  - Promoted `Service`, `App`, and `Library` to first-class graph entities, added CFML/CFC/CFScript parser support, and added interactive Undo/Redo in the webview.

---

## [1.4.0] - 2026-09-21

### Added
- **VS Code Extension Launch & Frontend/RPC Parsers**:
  - Initial release of the CodeExplorer VS Code extension with DAG topological ranking, workspace rescan commands, and parsers for Angular HttpClient, OIDC, Refit, SignalR, and GraphQL clients.

---

## [1.3.0] - 2026-09-16

### Added
- **Distributed Actors & CQRS Event Sagas**:
  - Added support for Microsoft Orleans virtual actor grains, MongoDB collections, MediatR pipelines, and cross-service event publishing/subscribing.

---

## [1.2.0] - 2026-09-15

### Added
- **API Contract Extraction & Gateway Tracing**:
  - Automatic request/response DTO schema extraction and ingress-to-egress tracing through API gateways.

---

## [1.1.0] - 2026-09-15

### Added
- **Unified MCP Server & C4/Mermaid Diagram Export**:
  - Full MCP server tool suite (`get_architecture_map`, `find_symbol`, `get_call_chain`, `inspect_data_lineage`) and `ce export` command.

---

## [1.0.0] - 2026-09-15

### Added
- **Initial Release of CodeExplorer (`ce`)**:
  - Single-file cross-platform native CLI and `.NET` Global Tool (`codeexplorer.cli`), embedded SQLite graph engine with OpenCypher-to-SQL transpiler, and Tree-sitter AST parsers for C#, TypeScript/JavaScript, Python, Go, Java, and SQL.
