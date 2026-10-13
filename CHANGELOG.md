# Changelog

All notable changes to the CodeExplorer project (CLI, MCP Server, and VS Code Extension) are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.37.35] - 2026-10-13

### Added
- **Strongly-Typed Dependency Categories (`DependencyCategory` Enum)**:
  - Introduced `DependencyCategory` enum (`UnidentifiedDependency`, `Library`, `ServiceCall`, `Database`, `Messaging`, `Configuration`) and `DependencyCategoryExtensions` with canonical string mapping and case-insensitive parsing.
  - Replaced ad-hoc string comparisons for ontology node labels (`OntologyConstants.NodeLabels.Package`, `Library`, `Database`, `Topic`, `ServiceCall`).
- **Semantic Invariant Gate `SIG-C-C-001` (Disallow Stringly-Typed Taxonomies and Classifications)**:
  - Added new architectural gate `SIG-C-C-001` prohibiting primitive string types (`string`, `String`, `string?`, `String?`) on class/record properties and fields ending in `Type`, `Kind`, or `Category`.
  - Registered `DisallowStringlyTypedTaxonomiesGate` in `BuiltInGateCatalog.cs` (`GetDefaultGates()`, `GetExampleGates()`), manifest at `.codeexplorer/gates/architectural/sig-c-c-001-disallow-stringly-typed-taxonomies.yaml`, and baseline at `.codeexplorer/baselines/SIG-C-C-001.json`.
- **Property & Field Syntax Enrichment in AST**:
  - `CSharpParser` and `CSharpFileVisitor` now extract `memberKind` (`property`, `field`) and `typeName` (`data_type`) for C# property declarations, field declarations, and record primary constructor parameters into `RawVariable` and `MemberNode`.
  - `SyntaxEnricher` attaches `data_type` and classifies members directly into the property graph.

### Fixed
- **Edge Normalization Fallback Fixed**:
  - In `PostIndexAnalyzer.NormalizeEdgeCategory`, replaced fallback to `"service_call"` with `DependencyCategory.UnidentifiedDependency` (`"unidentified_dependency"`) and normalized edge kind to `DEPENDS_ON`.
  - Unknown or generic inter-project dependencies no longer masquerade as service calls and will not appear in API outbound call ports.
  - In `ArchitectureViewEngine`, `unidentified_dependency` falls back cleanly to `normalizedKind` (`DEPENDS_ON`), never generating synthetic `SERVICE_CALL` edges.

---

## [1.37.34] - 2026-10-13

### Fixed
- **Package Dependencies Classified as Libraries**:
  - Fixed an issue where external package dependencies (npm, NuGet, pip, etc.) were erroneously classified as `service_call` instead of `library` due to `Package` nodes missing from `NormalizeEdgesAsync` kind filter.
  - Added strict priority in `PostIndexAnalyzer.NormalizeEdgeCategory`: edges to `Package` or libraries are unconditionally classified as `library`, overriding any stale properties.
  - Added URN prefix fallback (`:pkg:`) in `NormalizeEdgesAsync` for robust package identification.
  - In `ArchitectureViewEngine`, forced `category = "library"` and `kind = "LIBRARY"` for all package targets so they always bind to the `LIBRARIES: uses` handle and green styling instead of the `API: calls` handle.
- **VS Code Extension Icon Optimization**:
  - Replaced extension icon with high-quality quantized `icon_512_quant.png` (34 KB) preserving 8-bit alpha transparency.
- **CI/CD & Publishing Resilience**:
  - Added retry loop and configurable timeout to NuGet package push in release workflow.
  - Reordered release workflow steps so GitHub Releases are published before NuGet push.
  - Isolated test fixtures touching `ConstantRegistry` (`Epic2ParserIntelligenceTests`) with `[NonParallelizable]`.

---

## [1.37.20] - 2026-10-12

### Fixed & Performance
- **PostIndexAnalyzer & Database Upload Optimization (Eliminated 1.15 Billion Redundant Comparisons & Silent Freezes)**:
  - **Eliminated 1.15 Billion Nested String Comparisons**: Resolved severe nested $O(\text{Files} \times \text{Projects} \times \text{Projects})$ loops in `MaterializeDirectProjectRelationships` and `CanonicalizeDatabasesInMemoryAsync` where `projects.FirstOrDefault(pr => IsEnclosedInProject(...))` evaluated the full project list for every single project candidate. Replaced with `Layer2ProjectParser.BuildFileToProjectMap` built once in $O(\text{Files} \log \text{Projects})$ and shared across canonical database linking and macro project edge materialization.
  - **Live Real-Time Database Upload Progress (`UploadRelationshipsAsync` / `UploadNodesAsync`)**: Doubled batch sizes from 100 to 200 statements per transaction and introduced live continuous progress reporting (`[DB:Edges] Saved: {Processed}/{Total} relationships ({Pct}%)...`) firing every 3 seconds and at milestones for large relationship lists (e.g. 34,894 late-bound relationships or 100,000+ reference relationships).
  - **Global Cross-Reference Resolution Heartbeat**: In `ResolveAndUploadGlobalReferencesAsync`, added periodic progress reporting (`[Layer5] Resolving global cross-references: {N}/{Total} ({pct}%)...`) updating every 25,000 items or every 3 seconds, eliminating silence during reference resolution passes.
  - **PostIndexAnalyzer Sub-Phase Transparency**: Added explicit, visible info-level progress logs throughout `RunInMemoryAsync` covering graph adjacency construction, reachability analysis, canonical database linking, direct and transitive relationship uploads, architectural layer classification, and edge normalization.
  - **Optimized Edge Normalization (`NormalizeEdgesAsync`)**: Restricted the initial node query from all nodes (`MATCH (n)`) to only high-level architectural kinds (`WHERE n.kind IN ['Project', 'Database', 'Topic', 'Endpoint', 'EntryPoint', 'ExternalService', 'CloudService']`), reducing JSON deserialization from 180,000+ nodes to ~5,000 nodes. Batched edge normalization updates into chunks of 100 rather than single-statement autocommit writes.
  - **Batched Phantom External Service Purge & $O(1)$ Edge Orphan Deletion**: Batched `PurgePhantomExternalServicesAsync` deletions in chunks of 100 instead of row-by-row deletions, and replaced the catastrophic $O(E \times N)$ `NOT IN (SELECT id FROM nodes)` table scan in `DeduplicateProjectNodesAsync` with indexed B-tree `NOT EXISTS (SELECT 1 FROM nodes WHERE id = ...)` primary key seeks.

---

## [1.37.19] - 2026-10-12

### Fixed & Performance
- **Layer 5 Late Binding Optimization & Real-Time Progress Feedback (Indefinite Freeze to 18 Seconds)**:
  - **Memoized Lookups & Fast Early Rejection**: Pre-parsed and normalized routes, domains, and path segments once per endpoint/service, eliminating over 21 million redundant regex evaluations and path splits inside `PerformLateBindingAsync`.
  - **Memoized Project-to-Domain Scoring**: Cached project-service affinity evaluations, avoiding millions of repeated fuzzy Levenshtein calculations.
  - **Live Continuous Progress Reporting**: Added real-time progress indicators (`[Layer5] Late-binding progress: {N}/{Total} external services ({pct}%)...`) firing every 100 external services and every 3 seconds, keeping the user informed throughout late binding. Reduced late-binding time on ATS (3,915 services, 5,500 endpoints) from >7 minutes of silence down to 18 seconds.
- **Fast Subpath and Workspace Clearing (5-Minute Bottleneck Eliminated)**:
  - **Single-Workspace Fast Wipe**: When clearing a target path that covers all indexed projects and files in a single workspace database, SQLite executes a fast table truncate rather than millions of row-by-row index deletes.
  - **Fast Bulk Edge Truncation**: When deleting all or nearly all nodes in the database (`deletedCount >= totalNodes - 1`), directly truncates the `edges` table instead of evaluating hundreds of thousands of individual `IN (SELECT id ...)` lookups across 5 secondary indexes.
  - **Instant Schema Wiping**: Automatically invokes `ClearDatabaseAsync()` immediately upon detecting an outdated schema version before rescanning.
- **Layer 4 Semantic Parser Optimization & Live Progress Reporting**:
  - **Eliminated 1.72 Billion Redundant String Comparisons**: Replaced the nested quadratic $O(\text{Projects} \times \text{Files} \times \text{Projects})$ file matching loop with a single $O(\text{Files})$ pre-grouping pass (`filesByProjectId` and `treesByProjectId`), reducing Layer 4 file-to-project assignment time from tens of seconds to < 5ms on large monorepos (e.g. ATS with 204 projects and 13,782 files).
  - **Continuous Info-Level Progress Updates**: Replaced hidden `LogDebug` calls with visible info logs and periodic progress triggers (`[Layer4] Enriched {N}/{Total} projects ({pct}%) - {Name}...`), updating on project 1, every 10 projects, whenever $\ge 3$ seconds elapse, and at completion.
  - **Workload & Persistence Logging**: Added explicit info-level logs before semantic workload materialization and tree persistence queuing (`Queued {N} nodes and {M} relationships to persistence pipeline`).
- **Database Persistence Heartbeat & Flush**:
  - Added a 3-second heartbeat threshold to `ReportNodesProgressIfNeeded` and `ReportRelationshipsProgressIfNeeded`, ensuring users receive continuous database write feedback even when batch sizes are below 10,000.
  - Added `FlushPersistenceProgressReport()` to ensure the exact final node and edge counts are printed when the persistence queue is drained or disposed.
- **Layer 5 Targeted Preloading SQLite Indexing (22-Minute Bottleneck Eliminated)**:
  - Added `idx_nodes_name_kind` on `nodes(json_extract(properties, '$.name'), kind)` in SQLite, eliminating full table scans across hundreds of thousands of rows during `LoadSymbolsByNamesAsync` and cutting targeted preloading during subtree scans from 22 minutes to milliseconds.

---

## [1.37.18] - 2026-10-12

### Added
- **TypeScript / JavaScript Testing Framework Semantic Extensions (Jest, Vitest, Mocha, Cypress, Playwright)**:
  - Added AST semantic extensions for all major JavaScript and TypeScript test runners and frameworks: `jest`, `vitest`, `mocha`, `cypress`, and `@playwright/test`.
  - **Jest & Vitest**: Identifies test suites (`describe`, `suite`) and test cases (`test`, `it`, `.only`, `.skip`, `.each`, `.concurrent`) as first-class architectural entry points with `is_test: "true"` metadata; extracts mock module dependencies from `jest.mock(...)` and `vi.mock(...)` with `:DEPENDS_ON` edges.
  - **Mocha**: Detects BDD and TDD test suites (`describe`, `context`, `suite`) and test cases (`it`, `specify`, `test`, `.only`, `.skip`) with suite bindings and testing annotations.
  - **Cypress**: Maps E2E test suites and specs (`describe`, `it`, `context`), and resolves browser navigation (`cy.visit(...)`) and network requests (`cy.request(...)`) into `:CALLS_ENDPOINT` edges and `ExternalService` targets.
  - **Playwright**: Parses `test.describe(...)` suites, individual browser tests (`test(...)`), browser page navigations (`page.goto(...)`), and API context requests (`request.get(...)`, `request.post(...)`), connecting E2E flows to backend service endpoints.
  - **100% Backlog Cleared**: Fully completed all commented-out package descriptors across `TypeScriptParser`, completing Epic 7.

---

## [1.37.17] - 2026-10-12

### Fixed & Performance
- **Subtree & Workspace Clear Performance (5-Minute Bottleneck Eliminated)**:
  - **Recursive CTE Index-Directed Traversal**: Resolved a severe SQLite query plan regression in `DeleteSubpathHierarchyAsync` and `DeleteWorkspaceHierarchyAsync` where unindexed recursive CTE joins (`edges e JOIN del_nodes d`) forced quadratic full table scans over hundreds of thousands of `CONTAINS` edges, freezing the CLI for up to 5 minutes. Switched to `del_rec d JOIN edges e INDEXED BY idx_edges_from_kind_to ON e.from_id = d.id AND e.kind = 'CONTAINS'`.
  - **Deterministic ID-Indexed Matching**: Introduced direct index-backed prefix matching for file, folder, project, endpoint, symbol (`sym:`), and literal (`lit:`) URNs directly in SQLite ($O(\log N)$ B-tree index seeks), collecting hundreds of thousands of nodes in under 1 second.
  - **Container-Scoped Recursion**: Restricted recursive edge expansions in Pass 3 to container nodes (`Folder`, `Project`, `File`), skipping leaf AST symbols and speeding up collection to under 1 second.
  - **Complete Subpath Deletion**: Ensured that clearing a subpath surgically deletes all associated AST symbols, types, functions, methods, and call literals rather than leaving orphan syntax nodes.

---

## [1.37.16] - 2026-10-12

### Added
- **TypeScript Frontend Framework Semantic Extensions (React, Vue, Angular Core, Svelte, Vite)**:
  - Added AST semantic extensions for major frontend application frameworks and build tooling: `react`, `react-dom`, `vue`, `@angular/core`, `svelte`, and `vite`.
  - **React**: Detects root rendering entry points (`createRoot`, `ReactDOM.render`), resolving root JSX elements and `React.createElement` references; recursively extracts dynamically imported components inside `React.lazy(() => import(...))` with `:DEPENDS_ON` edges.
  - **Vue**: Discovers application mount entry points (`createApp().mount`, `new Vue().$mount`) with root component dependencies, and parses dynamic chunk boundaries via `defineAsyncComponent(() => import(...))`.
  - **Angular Core**: Identifies `@Component` and `@NgModule` decorators linked to class declarations via `:TRIGGERS` edges, resolves external template files via raw `templateUrl` relative path dependencies, and tracks standalone/module bootstrapping (`bootstrapApplication`, `bootstrapModule`).
  - **Svelte**: Recognizes modern Svelte 5 (`mount(App, ...)`) and legacy Svelte 3/4 (`new App(...)`) application mounting entry points with root component relationships.
  - **Vite**: Analyzes `defineConfig(...)` build configurations as architectural entry points and registers configured build plugins (e.g., `react()`, `vue()`) as configuration dependencies.

---

## [1.37.15] - 2026-10-12

### Fixed
- **Generic Monorepo Project Classification & Discovery**:
  - **Angular Secondary Entry Point Filtering**: Implemented generic contract-based detection in both `TypeScriptParser` and `JavaScriptParser` for Angular secondary entry points (`ngPackage` configuration without top-level package names, child directories in Angular workspaces) without hardcoded project paths, eliminating phantom library projects.
  - **Standard npm Library Contract Recognition**: Packages declaring explicit exports (`exports`, `types`, `typings`, `main`, `peerDependencies`, `publishConfig`, or publish scripts) without application launch scripts (`scripts.start`, `scripts.serve`, `scripts.start:prod`) are classified as `Library` (`manifest_type = "library"` and `is_library = "true"`), preventing shared utility or NestJS libraries from being misclassified as runnable web services.
  - **Dominant Role Guard for Shared Libraries with Capabilities**: Updated `ProjectComponentProfile` so shared libraries equipped with unit test runners (e.g., Jest, Mocha), background queue utilities, or HTTP helpers preserve their `SharedLibrary` role rather than being converted into standalone services or test framework projects.
  - **Cloudflare Worker & Wrangler Detection**: Added generic detection for Cloudflare Workers via current or parent directory `wrangler.toml`, `@cloudflare/` dependencies, and worker script patterns.
  - **QA Test Directory Recognition**: Added `/qa/` path convention to `ProjectEntityClassifierRegistry` alongside `/test/`, `/tests/`, and `/e2e/`.

---

## [1.37.14] - 2026-10-12

### Added
- **TypeScript Cloud SDK Semantic Extensions (Stripe, AWS SDK, Azure SDK)**:
  - Added AST semantic extensions for major cloud SDKs: `stripe` (`@stripe/*`), AWS SDK v2/v3 (`aws-sdk`, `@aws-sdk/*`), and Azure SDK (`@azure/*`).
  - **Stripe**: Resolves dynamic API resource invocations (`stripe.paymentIntents.create`, `charges.create`, `checkout.sessions.create`, etc.) to canonical `api.stripe.com` endpoints with `:CALLS_ENDPOINT` edges.
  - **AWS SDK**: Extracts SQS queue URLs and names (`SendMessageCommand`, `ReceiveMessageCommand`), emitting `:PUBLISHES_TO` / `:SUBSCRIBES_TO` edges; detects S3 bucket operations (`PutObjectCommand`); and indexes DynamoDB tables (`PutItemCommand`, `GetItemCommand`) as `Query` nodes.
  - **Azure SDK**: Extracts Service Bus queue/topic bindings (`createSender`, `createReceiver`) with bidirectional pub/sub edges, tracks Blob Storage containers (`getContainerClient`), and maps Cosmos DB container operations (`.container(...)`) to semantic queries.

---

## [1.37.13] - 2026-10-12

### Added
- **TypeScript Analytical Database Semantic Extensions (ClickHouse & BigQuery)**:
  - Added AST semantic extensions for analytical / OLAP databases: `@clickhouse/client`, `@clickhouse/client-web`, `clickhouse`, and `@google-cloud/bigquery`.
  - Parses SQL queries (`query`, `command`, `exec`), extracts target tables, handles DDL statements (`ALTER TABLE`, `OPTIMIZE TABLE`, `TRUNCATE TABLE`), and resolves direct table inserts (`client.insert({ table: ... })` and `dataset.table(...).insert(...)`).
  - Generates `Query` nodes with table data lineage (`:DEPENDS_ON` and `:USES_DB` relationships) and connects OLAP pipeline topologies.

---

## [1.37.12] - 2026-10-12

### Added
- **TypeScript / Node.js HTTP Client Semantic Extensions (request, undici, urllib)**:
  - Added AST semantic extensions for legacy and modern Node.js HTTP clients: `request` (direct invocations, `.get(...)`, `.post(...)`, URI options), `undici` (direct `request(...)`, `stream(...)`, `Client` / `Pool` instantiation with base URLs, chained `.request({ path: ... })`), and `urllib` (`urllib.request(...)`, `urllib.curl(...)`, `HttpClient` calls).
  - Automatically resolves external endpoints and base URLs, emitting `:CALLS_ENDPOINT` relationships to `ExternalService` nodes.
  - Refined `AxiosLibraryParser` dispatch guards to seamlessly fall through on non-Axios options objects.

---

## [1.37.11] - 2026-10-12

### Performance
- **Indexed Preloading Optimization (3500x Faster)**:
  - Replaced unindexed `json_each` CTE virtual table scans in `SqliteGraphClient.LoadSymbolsByNamesAsync` and `LoadImplementationsForTypesAsync` with temporary indexed B-tree tables (`temp_target_names`, `temp_target_types`).
  - Eliminates $O(N \times M)$ nested-loop scans on large workspaces (e.g. ATS with 85,000+ referenced symbol names), reducing preloading duration from 22 minutes 47 seconds to 386 milliseconds.
  - Prevents database semaphore lock starvation, ensuring uninterrupted parallel persistence in `DatabasePersistenceWriter`.

---

## [1.37.10] - 2026-10-12

### Added
- **Java Messaging Semantic Extensions (Spring Kafka & Spring RabbitMQ)**:
  - Added AST semantic extensions for `KafkaTemplate` producer calls and `@KafkaListener` consumer annotations.
  - Added AST semantic extensions for `RabbitTemplate` / `AmqpTemplate` publisher calls and `@RabbitListener` consumer annotations.
  - Generates bidirectional messaging relationships (`:SENDS_MESSAGE_TO` / `:CONSUMES_MESSAGE_FROM`) to track asynchronous event choreography across Java microservices.
- **Automated VS Code Extension Changelog Sync**:
  - Integrated automatic synchronization of repository root `CHANGELOG.md` into `vscode-extension/` during local packaging (`package-extension.mjs`) and GitHub Actions release workflows (`release.yml`).
- **Semantic Invariant Gates (SIG) Documentation**:
  - Updated repository `README.md` with complete documentation for the Fractal Orthogonal Invariant Matrix (FOIM), zero-match contract, ratchet baselines, YAML schema, and CI/CD quality gate enforcement.

---

## [1.37.9] - 2026-10-12

### Added
- **Python HTTP Client Semantic Extensions (requests, httpx, aiohttp)**:
  - Added AST semantic extensions for synchronous and asynchronous Python HTTP clients: `requests`, `httpx` (sync & async client sessions), and `aiohttp.ClientSession`.
  - Extracts target URLs, HTTP methods (GET, POST, PUT, DELETE, etc.), and generates outgoing `:CALLS_ENDPOINT` edges.

---

## [1.37.8] - 2026-10-12

### Added
- **Python Web Framework Semantic Extensions (Flask & Django)**:
  - Implemented semantic analysis for Flask route decorators (`@app.route(...)`, `@bp.route(...)`).
  - Implemented semantic analysis for Django URL patterns (`path(...)`, `re_path(...)` in `urlpatterns`).
  - Extracts HTTP verbs, route paths, URL parameters, and handler methods into `:Endpoint` nodes.

---

## [1.37.7] - 2026-10-12

### Added
- **Go Egress and Database Semantic Extensions**:
  - Added HTTP egress semantic extensions for standard library `net/http` and `go-resty/resty`.
  - Added database semantic extension for PostgreSQL driver `jackc/pgx` (`pgxpool.Pool`, `pgx.Conn`).
  - Extracts SQL queries, table references, and external HTTP calls into the visual knowledge graph.

---

## [1.37.6] - 2026-10-12

### Added
- **Go Web Framework AST Semantic Extensions**:
  - Implemented semantic extensions for **Gin** (`gin-gonic/gin`), **Fiber** (`gofiber/fiber/v2`), and **Echo** (`labstack/echo/v4`).
  - Extracts HTTP endpoints, route groups, URL parameters, and handlers into `:Endpoint` graph nodes.
- **SampleProjects Snapshot Test Suite**:
  - Converted baseline tests into a full-workspace scan against `expected.json` snapshots, ensuring automated coverage for all 35 sample projects across all supported languages.
- **Performance Optimization**:
  - Scaled down Layer 5 synthetic benchmark to 100,000 references, reducing execution time by 11x (from 5s to 420ms) while preserving algorithmic coverage and complexity verification.

---

## [1.37.5] - 2026-10-12

### Added
- **Extensible Semantic Extensions Architecture**:
  - Decoupled package descriptors into pluggable `ISemanticExtension` interfaces across all language parsers (C#, Go, Java, TypeScript/JavaScript).
  - Dynamic discovery of semantic extensions without hardcoded registry arrays.

### Fixed
- **Parser Discovery Diagnostics**:
  - Eliminated swallowed exceptions during parser discovery, replacing them with explicit fail-fast logging.

---

## [1.37.3] - 2026-10-12

### Fixed
- **Frontend SQL False Positive Suppression**:
  - Prevented non-SQL strings and template literals in frontend files (Angular, React, Vue) from being falsely parsed as SQL tables.
- **Angular Monorepo Secondary Entry Points**:
  - Suppressed nested secondary entry points (`ng-package.json` without package name) in library folders to avoid duplicate project registrations.

---

## [1.37.2] - 2026-10-12

### Fixed
- **VS Code Extension Lifecycle**:
  - Fixed Cytoscape instance cleanup and memory leak on panel reload.
  - Widened concentric orbit radii for high-density microservice topologies.
- **FloatingPanel Support**:
  - Added resizable, corner-dockable floating inspector panel with smooth dragging.

---

## [1.37.1] - 2026-10-12

### Added
- **Webview UI Modernization**:
  - Replaced raster icons with lightweight vector SVGs.
  - Added HUD controls for neighborhood hop depth, bezier curvature tuning, and filter presets.

---

## [1.37.0] - 2026-10-12

### Added
- **Polyglot Sample Projects Matrix**:
  - Added 35 ground-truth sample projects spanning C#, Go, Python, Java, Kotlin, TypeScript, and ColdFusion.
  - Invariant reflection tests ensuring all registered project parsers have test fixtures and coverage.

---

## [1.36.0] - 2026-10-11

### Added
- **Dynamic Table Template Literals**:
  - Normalized BigQuery table shard wildcards (`table_*`) and dynamic template interpolations.
- **Fast SQLite Subpath Clear**:
  - Migrated subtree deletion to Dapper recursive CTE queries for sub-second workspace resets.

---

## [1.35.0] - 2026-10-11

### Added
- **Semantic Invariant Gates (SIG)**:
  - Introduced the Fractal Orthogonal Invariant Matrix (FOIM: Scopes System/Service/Module/Class/Method $\times$ Dimensions Contract/Rules/Resources/Boundaries).
  - Enforce architectural rules using OpenCypher queries with a Zero-Match contract (0 rows = PASS).
  - Ratchet baselines (`.codeexplorer/baselines/*.json`) to tolerate existing legacy technical debt while preventing new regressions in CI.
  - Clickable 2D terminal output (`file:line:col`) for instant IDE navigation to violating code.
  - Support for `routing-controllers` decorators in TypeScript.

---

## [1.34.6] - 2026-10-11

### Fixed
- **Updated Node Removal During Rescan**:
  - Automatically prune stale graph nodes and edges (`ClearWorkspaceAsync`) for modified files with structural changes before re-indexing, ensuring deleted or renamed symbols do not linger as ghost nodes in SQLite.

### Changed
- **Log Noise Reduction During Rescan**:
  - Removed 1-second interval timer persistence logging in `ParsingContext`, restricting persistence progress reports to 10,000-item milestones.
  - Demoted per-project parsing (`Layer3`) and enrichment (`Layer4`) loops, per-endpoint late-binding synthesis (`Layer5`), and intermediate database materialization logs to debug level.
  - Truncated long file/manifest lists in `WorkspaceIndexer` and demoted verbose structural diff reasons to debug level while maintaining concise status reporting at information level.

---

## [1.34.5] - 2026-10-11

### Changed
- **CLI Clean Version Formatting**:
  - Configured `<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>` in `Directory.Build.props` so `ce --version` prints clean semantic version numbers without appended source commit hashes.
- **Architecture View Engine Documentation**:
  - Corrected XML doc comment placement on `GetSystemContextViewAsync`.

---

## [1.34.4] - 2026-10-11

### Refactored
- **Architectural Gate Remediation: Method Length Ceiling (`SIG-M-R-001`)**:
  - Eliminated root causes for architectural gate `SIG-M-R-001` across all workspace components (all methods now strictly <= 500 LOC, achieving 0 gate violations):
    - **`ArchitectureViewEngine.cs:GetSystemContextViewAsync`**: Reduced from 831 LOC to 55 LOC by decomposing node discovery, candidate resolution, node canonicalization, package count enrichment, edge processing, and database filtering into dedicated, focused sub-routines (`DiscoverSystemContextCandidatesAsync`, `PopulateSystemContextNodes`, `PopulatePackageCountsAsync`, `PopulateSystemContextEdgesAsync`, `FilterSharedDatabases`).
    - **`Layer5AnalysisParser.cs:ResolveAndUploadGlobalReferencesAsync`**: Reduced from 631 LOC to 76 LOC by extracting symbol dictionary builders, inheritance resolution, type binding indexes, and specialized reference dispatch handlers (`ResolveCallsReference`, `ResolveDependsOnReference`, `ResolveTriggersReference`, `ResolveMessagingReference`, `ResolvePersistedInReference`).
    - **`vscode-extension/src/extension.ts:activate`**: Reduced from 917 LOC to 88 LOC by modularizing command registration into `commands.ts` (`registerGraphCommands`, `registerWorkspaceCommands`, `registerIntentCommands`).

---

## [1.34.3] - 2026-10-11

### Fixed
- **Gate File Path Resolution (`coalesce(f.path, f.file_path)`)**:
  - Prioritized `f.path` over `f.file_path` in `SIG-M-R-001`, `SIG-M-I-004`, and `SIG-A-C-001` queries to ensure violations emit full workspace-relative paths rather than bare file names.
  - Fixes clickable 2D IDE file:line:col navigation and ensures unique, non-colliding SHA-256 baseline fingerprints across subdirectories.

---

## [1.34.2] - 2026-10-11

### Added
- **Dogfooding Gate: Method Length Ceiling (`SIG-M-R-001`)**:
  - Added `sig-m-r-001-method-length.yaml` to `examples/gates/code-explorer/` to enforce that methods and functions must not exceed 500 lines of code.
  - Registered `SIG-M-R-001` (Scope: `Method`, Dimension: `Resources`) in `BuiltInGateCatalog.cs`.
  - Added `Gate_SIG_M_R_001_DetectsMethodsExceeding500Lines` test in `SemanticInvariantGateTests.cs`.

### Fixed
- **Method Length Property in Resources Gate**:
  - Corrected query in `examples/gates/resources/sig-m-r-001-method-length.yaml` from invalid property `f.line_count` to canonical `coalesce(f.loc, f.end_line - f.start_line + 1)`.

---

## [1.34.1] - 2026-10-11

### Added
- **Component-Driven Test Source Classification (`IComponentLibraryParser.IsTestSource`)**:
  - Added `IsTestSource(SyntaxTree syntaxTree)` to `IComponentLibraryParser`, enabling framework parsers to classify test sources authoritatively before AST symbol mapping.
  - Implemented `IsTestSource` in `JavaScriptTestRunnerComponentParser` for Jest, Vitest, Mocha, Cypress, Playwright, Jasmine, and Karma, checking standard test suffixes (`.spec.ts`, `.test.ts`, `.spec.js`, etc.) and top-level runner invocations (`describe`, `it`, `test`, `suite`).
  - Added package dependencies to `KnownPackages` in `JavaScriptTestRunnerComponentParser`: `"jasmine-core"`, `"@types/jasmine"`, `"karma"`.

### Fixed
- **Component Parser Package Dependencies in Layer 3 Syntactic Parser**:
  - Fixed `ProjectContext` initialization in Pass 2 of `Layer3SyntacticParser` to supply actual Layer 2 package dependencies instead of an empty list (`[]`), restoring component parser evaluation (`CanHandle`) across projects.
  - Automatically sets `file.IsTest = true` and `syntaxTree.FileNode.IsTest = true` prior to visitor processing when recognized by any active test component parser, ensuring all symbols, calls, and call literals in spec files inherit `is_test = true`.

---

## [1.34.0] - 2026-10-11

### Added
- **Extended Semantic Classification for Call Literals & Attributes**:
  - Added specialized semantic roles (`FormAccess`, `Storage`, `WebAPI`, `TestIdentifier`, `State`) to `CallSemanticClassifier`.
  - Automatically classifies Angular Reactive Forms (`this.form.get('field')`, `fb.group`), browser storage (`localStorage.getItem`, `sessionStorage`), browser Web APIs (`window.open`, `document.querySelector`), test attributes/IDs (`data-testid`, `testId`), and state signals (`signal`, `computed`).
  - Drastically suppresses remaining framework noise in `SIG-M-I-001` (Disallow Hardcoded Arguments) by isolating business logic from UI/browser primitives.
- **Internal Egress Attribution (`is_internal`)**:
  - Sets boolean `is_internal = true` on `ExternalServiceNode` for relative endpoints (`/api/...`) and loopback bindings (`localhost`, `127.0.0.1`).

### Fixed
- **Cypher Parser Single-Node Pattern Expressions & Pattern Predicates**:
  - Fixed OpenCypher grammar in `GraphPatternExpression` to accept anonymous single-node pattern expressions in `WHERE` and `EXISTS` clauses (e.g. `NOT (:Endpoint { path: es.path })`), while preserving single-identifier label checks (`(n:Function)`).
- **Dead Internal Egress Invariant Gate (`SIG-A-C-001`)**:
  - Fixed Cypher query syntax and directionality in `examples/gates/contract/sig-a-c-001-dead-internal-egress.yaml` to inspect `(es:ExternalService)-[:CALLED_BY]->(caller)` against active endpoints.
- **Top-Level `export const` Variable Resolution in TypeScript AST**:
  - Fixed `AstHelper.FindVariableInitializerInAst` to unwrap `export_statement` nodes, ensuring exported URL/route constants (`export const DEFAULT_CATALOG_URL = ...`) are resolved across modules regardless of workspace structure.
  - Forwarded project context through `FetchLibraryParser` and `AngularHttpLibraryParser`.

---

## [1.33.1] - 2026-10-11

### Added
- **Catch Clause Layer 3 AST Modeling & `SIG-M-I-005` (No Empty Catch Blocks)**:
  - Introduced `CatchClauseNode` into Layer 3 syntactic AST across C#, TypeScript/JavaScript, Java, and Python.
  - Automatically detects empty catch/except blocks (`is_empty`) while intelligently recognizing explanatory comments (`has_comment`) like `// ignored: expected fallback` or `/* expected */`.
  - Added architectural invariant gate `SIG-M-I-005` in `examples/gates/rules/sig-m-i-005-no-empty-catch.yaml` to prevent silent exception swallowing without documentation or handling.
- **Fail-Visible Gate Loading & Duplicate ID Diagnostics in `FileGateRepository`**:
  - Emits descriptive warnings to `Console.Error` when YAML deserialization fails or when duplicate gate IDs are detected, eliminating silent gate drops.

### Fixed
- **Test File Exclusion from `CallLiteralNode` and `CatchClauseNode`**:
  - Enhanced `IsTestSourcePath` to reliably exclude `*.spec.ts`, `/e2e/`, `/__mocks__/`, `/cypress/` alongside path-based test markers, eliminating 1,600+ false-positive literal violations from spec titles in NestJS/Angular.
- **Gate ID Collision & YAML Formatting in `examples/gates/`**:
  - Renamed `sig-s-b-001-database-ownership.yaml` to canonical `SIG-A-B-002` (`boundaries/sig-a-b-002-database-ownership.yaml`) adhering to FOIM System scope.
  - Quoted description in `sig-p-b-001-pipeline-layer-hierarchy.yaml` to ensure strict YAML compliance.

---

## [1.33.0] - 2026-10-11

### Added
- **Semantic Role Classification for Call Literals & Attributes**:
  - Introduced `CallSemanticClassifier` to categorize AST call argument literals and attributes into semantic roles (`Routing`, `ParamBinding`, `Config`, `Logging`, `QueryBuilder`, `DI`, `Security`, `OrmMapping`, `Validation`, `Formatting`, `Exception`, `Business`).
  - Stored `role` and `is_decorator` directly on `CallLiteralNode` in Layer 3 syntactic AST graph.
  - Symmetrically extracted C# attributes alongside TypeScript/NestJS/Angular decorators.
  - Refactored `SIG-M-I-001` (Disallow Hardcoded Arguments) to target business literals (`coalesce(lit.role, 'Business') = 'Business'`), eliminating noise from framework decorators, config getters (`config.getOrThrow`), query builders (`createQueryBuilder`), and loggers.
- **Categorized Starter Gate Catalog in `examples/gates/`**:
  - Decoupled starter gates from the default `ce gate check` engine execution; organized starter examples into `examples/gates/architectural/` and `examples/gates/platform/`.
  - Repositories now evaluate only explicitly configured repository gates in `.codeexplorer/gates/`.

### Fixed
- **TypeScript Arrow Function Caller Provenance & Callback Filtering**:
  - Resolved function attribution for arrow callbacks in class field initializers and object literal pairs (`rxResource({ loader: () => ... })`, `plansResource.loader`, `load = () => ...`).
  - Suppressed spurious function node creation for anonymous callbacks (`ids.map(id => ...)`, `filter(el => ...)`), eliminating hundreds of false-positive function scopes (`Function:id`, `Function:el`) and misattributed `CALLS` edges.
  - Resolved false-positive unanchored endpoint warnings in `SIG-A-B-001`.
- **FOIM Dimension Alignment**:
  - Unified the 4 canonical FOIM dimensions across CLI options, MCP documentation, and `.agents/skills/semantic-invariant-gates/SKILL.md`: `Contract` (C), `Rules` (I), `Resources` (R), `Boundaries` (B).

---

## [1.31.0] - 2026-10-11

### Added
- **Semantic Invariant Gates (SIG) & Architectural Ratchets (`ce gate`)**:
  - Declarative architectural invariant guardrails evaluated via sub-millisecond OpenCypher queries directly over SQLite `graph.db`.
  - Monotonic Technical Debt Ratchets: baselines (`.codeexplorer/baselines/*.json`) enforce that technical debt strictly decreases ($N_{t+1} \le N_t$), blocking new architectural violations while tolerating legacy debt.
  - Fractal Orthogonal Invariant Matrix (FOIM) taxonomy spanning 5 Scopes (Method, Class, Module, Service, System) and 4 Dimensions (Contract, Rules, Resources, Boundaries).
  - Dual-tier gate categorization: `Architectural` (language-agnostic boundaries and layering) vs. `Platform` (runtime/language-specific idioms for .NET, TypeScript, Go, Python, Java, Kotlin, SQL, ColdFusion).
  - New CLI commands:
    - `ce gate check` (`ce gate`): Runs zero-match invariant checks with colored Spectre.Console violation tables, formatted separators, and actionable remediation steps. Supports `--category`, `--platform`, `--format table|json|sarif`, `--group-by`, `--limit`, and `--offset`.
    - `ce gate list`: Lists all active gates, scopes, categories, and tolerated debt counts.
    - `ce gate ratchet [--update]`: Inspects debt ratchets and burns down baselines automatically when debt is resolved.
    - `ce gate init`: Scaffolds organized starter gates in `.codeexplorer/gates/architectural/` and `.codeexplorer/gates/platform/<platform>/`.
  - Autonomous AI-Agent MCP Tool `check_semantic_gates`: Enables AI coding agents to self-verify architectural invariants before committing changes.
  - Shipped built-in starter catalog: `SIG-P-B-001` (Pipeline Layer Hierarchy), `SIG-P-B-002` (Database Driver Isolation), `SIG-S-B-001` (Storage Driver Abstraction), `SIG-C-I-001` (In-Memory AST Visitor IO Purity), `SIG-P-C-001` (Project Identity Bijection), `SIG-A-B-001` (Call Lineage Provenance), `SIG-P-B-003` (Transport Purity), `SIG-P-I-001` (Circular Package Dependency Shield), `SIG-M-I-001` (Disallow Hardcoded Arguments).
  - Published comprehensive "Gates on Growth" catalog with 34 polyglot gates across 8 platforms in `docs/proposals/semantic-invariant-gates-proposal.md`.

---

## [1.19.0] - 2026-10-02

### Added
- **Typed Cypher Queries & LINQ Provider (`GraphContext`)**:
  - Introduced zero-allocation, strongly-typed Cypher query execution via `GraphContext` and `CypherQueryable<T>`.
  - Added LINQ expression tree compiler (`CypherExpressionVisitor`) translating C# LINQ expressions (`client.Graph.Services.Where(...)`) into optimized OpenCypher ASTs.
  - Implemented `ICypherQueryExecutor` interface and integrated typed graph access directly into `IGraphClient.Graph`.
  - Added strongly-typed graph entity models: `ServiceEntity`, `AppEntity`, `WorkerEntity`, `CliToolEntity`, `DatabaseEntity`, `TopicEntity`, `ExternalServiceEntity`, `LibraryEntity`, `EndpointEntity`, and `GraphEntity`.
  - Added full test suites for LINQ AST translation and SQLite execution in `CodeExplorer.Cypher.Tests/LinqTests.cs` and `LinqSqliteExecutionTests.cs`.
- **Intelligent Endpoint Scoring Engine**:
  - Score-based resolution for ambiguous cross-service endpoints and domain routing.
- **Node Classification Enums**:
  - Replaced raw string literals with typed enums across ontology node entities.

---

## [1.18.10] - 2026-10-02

### Fixed
- **Test Impact Analysis (`ce test`)**:
  - Filtered out fixture lifecycle methods (`SetUp`, `TearDown`, `OneTimeSetUp`, `OneTimeTearDown`, `Dispose`) and helper routines (`RunCliAsync`, etc.) from runnable test results and total test counts.
  - Non-test helpers now serve cleanly as transparent call-graph bridges during reverse reachability BFS without polluting test results or commands.
  - Suppressed automatic git diff inspection when explicit `--symbols` are provided.
  - Sanitized class name formatting in test grouping to eliminate leading path separators.
  - In `ce test -f list`, output clean, raw class/method names without text annotations (such as `(all N tests)`) for direct compatibility with shell pipelines and `xargs`.

### Added
- **Architecture Diagram Exports**:
  - Added `--include-libraries` flag support to CLI export command, MCP endpoints, and WebSocket protocol for C4 and domain diagrams.

## [1.18.9] - 2026-10-02

### Added
- **Smart Test Grouping & Runner Command Collapsing**:
  - Automatically groups impacted test methods by test class and test suite in Test Impact Analysis (`ce test`).
  - Automatically collapses test runner commands to the class level (`FullyQualifiedName~ClassName`, `pytest -k "ClassName"`, etc.) when all test methods in a class are impacted, dramatically reducing CLI argument length and preventing command-line buffer overflows.
  - When only a subset of tests in a class are affected, targets only those specific methods.
  - Added `AffectedTestGroup` model and `Groups` collection to `TestImpactReport`.
  - In Markdown reports, grouped test classes show a clean summary status with collapsible `<details>` for impacted methods in fully-affected classes.
  - In `ce test -f list`, outputs `ClassName (all N tests)` when all tests in a class are affected.

## [1.18.2] - 2026-10-01

### Fixed
- **Go GCP Pub/Sub & AST Parser**:
  - Refined composite literal node type matching for worker definitions using `TreeSitterSyntax.Go.CompositeLiteral`.
  - Added explicit namespace imports in `PubSubGoLibraryParser.cs` and `GoAstHelper.cs`.

## [1.18.1] - 2026-10-01

### Fixed
- **Database Reconciliation & Schema Isolation**:
  - Prevented database engine names (e.g. `PostgreSQL`, `Redis`) and generic ORM identifiers (e.g. `TypeORM`, `Prisma`) from being claimed as resource-specific aliases, eliminating cross-service database hijacking.
  - Restricted generic database placeholder retirement to the originating project ID, preventing one service's concrete database from collapsing other services' databases.
  - Engine-based fallback resolution now strictly returns `null` when multiple databases share the same engine to avoid ambiguous guessing.
- **Go GCP Pub/Sub Parser**:
  - Fixed false-positive topic publishing references created on `client.Topic(topicID)` lookups that do not invoke `.Publish(...)`.
- **Domain Architecture & Flow Views**:
  - Corrected message directionality on subscriber service nodes (subscribers now display received messages under "Receives Messages").
  - Fixed edge deduplication and zoom sensitivity normalization in webview.

## [1.18.0] - 2026-10-01

### Added
- **Static Test Reachability Coverage (`ce coverage`)**:
  - Analyzes static test reachability across AST symbols (`Function`, `Type`, `Member`) via graph relationships (`HAS_METHOD`, `DECLARES`, `CALLS`, `USES_TYPE`, `IMPLEMENTS`).
  - Reports granular metrics and lists for covered vs. uncovered classes and methods.
  - Filtering by `--project`, `--path`, `--uncovered-only`, `--covered-only`.
  - CI test coverage gating via `--threshold <percent>` (exits with code 1 if threshold is not met).
  - Formats in GitHub-flavored Markdown or structured JSON.
  - Exposed via MCP tool `get_test_coverage` and built-in Cypher query `get_test_coverage`.
- **Test Impact Analysis (`ce test`)**:
  - Analyzes changed files, raw patches, or current `git diff` (working tree or branch comparison via `--git-base`).
  - AST-aware hunk mapping: resolves git unified diff line ranges directly to affected functions, types, and members.
  - Reverse reachability traversal to find exact **test methods** (not just test classes) impacted by code changes.
  - Test runner command generation: emits ready-to-run commands for .NET (`dotnet test --filter`), Python (`pytest`), Go (`go test -run`), JavaScript/TypeScript (`npm test -- -t`), and Java (`mvn test -Dtest=`).
  - Output formats: `markdown` (with full call chain traces), `list` (test names for piping), `commands` (executable shell commands), `json`.
  - Enhanced MCP tool `get_affected_tests` to support `filePaths`, `gitDiff`, and `maxDepth`.
- **MCP Client Auto-Configuration (`ce mcp configure`)**:
  - Automatically detects installed AI coding tools (Claude Desktop, Cursor, Antigravity, Cline, Roo Code, Windsurf) and registers CodeExplorer MCP server.
  - Added `ce mcp test` to verify stdio and HTTP server health.

### Changed
- **Optimized Reverse Graph Traversal**: Replaced recursive CTE queries with bounded breadth-first search (BFS) level-by-level traversal with cycle detection, eliminating exponential path explosion and memory pressure on large enterprise codebases.

---

## [1.16.0] - 2026-09-30

### Added
- **On-Demand Engine Downloader**: The VS Code extension now automatically downloads, extracts, and runs the native CodeExplorer engine from GitHub Releases on first launch.
- **Smart Engine Probing**: Dynamic version resolution that probes GitHub releases for matching `major.minor.*` releases and verifies that host platform assets (`ce-win-x64.zip`, `ce-linux-x64.tar.gz`, `ce-osx-arm64.tar.gz`, etc.) exist before initiating download.
- **Windows ARM64 Support**: Added `win-arm64` cross-compilation target to the release matrix.
- **Engine Configuration**: Added `codeExplorer.engineVersion` setting allowing users to pin exact versions or specify custom semver ranges (`1.16.*`, `^1.16.0`, `latest`).
- **New Commands**:
  - `CodeExplorer: Check & Update Engine` (`codeExplorer.updateEngine`) to check for engine updates on demand.
  - `CodeExplorer: What's New` (`codeExplorer.whatsNew`) to open release notes directly inside the editor.
- **Update Notification**: Automatic in-editor notification on extension update with quick access to the release notes.

### Changed
- **Lightweight Universal VSIX**: Package size dramatically reduced from **~130 MB (per platform) / ~780 MB (universal)** down to **2.86 MB**!
- **Project Icon Overhaul**: Replaced the opaque light-gray background with a continuous-curvature superellipse ($n = 6.8$) with 4x supersampling and Lanczos anti-aliasing for a transparent background that renders cleanly in both light and dark themes.
- **Synchronized Versioning**: CLI, NuGet package, and VS Code extension releases are now unified under identical version numbers.

---

## [1.15.0] - 2026-09-30

### Added
- **Pluggable Component Parsers**: Decoupled layer classification heuristics into specialized, extensible component parsers.
- **Multi-Role Profile Support**: Layer classifier now assigns both primary and secondary architectural roles to components.
- **Last Updated Status**: Added last-updated timestamp to the `ce status` CLI command.

### Changed
- **Console UTF-8 Encoding**: Default CLI console output configured for UTF-8 cross-platform Unicode rendering.
- **Worker Node Palette**: Updated worker node colors to fuchsia for clear contrast with messaging topics.

### Fixed
- Hardened messaging topic and queue validation and scope constants resolution.
- Eliminated duplicate projects in CLI status summaries.

---

## [1.14.0] - 2026-09-30

### Added
- **100k-Node Synthetic Benchmark**: Validated SQLite graph query throughput, BFS traversals, and indexing SLAs at enterprise scale.
- **Parser Audit Suite**: Comprehensive regression test suite validating parser accuracy across all supported frameworks.
- **31 Library Parser Stubs**: Expanded ecosystem coverage across C#, Go, and Python libraries.

### Changed
- **CTE Index Optimization**: Optimized recursive Common Table Expression (CTE) queries for deep dependency tree traversals.

---

## [1.13.0] - 2026-09-30

### Added
- **Incremental Indexing (Epic 4.1)**: SHA-256 content hashing and AST diffing to re-index only changed files, bypassing unchanged codebases.
- **File Watcher Mode**: Background file system watcher performing sub-second delta updates in the SQLite graph database.
- **High-Fidelity Lineage**: Refined Layer 5 cross-file `CALLS` and `USES_TYPE` relationship resolution.
- **Cypher Transpiler Enhancements**: Structured SQL template rendering (`SqlTemplates.RenderListComprehension` and `RenderCollectedArray`).

---

## [1.11.0] - 2026-09-29

### Added
- **Docked Studio HUD**: Two-tier balanced control bar with font size slider and collapsible floating side panels.
- **Avoid-Inner Edge Routing**: Curve deflection algorithm preventing edges from intersecting inner graph nodes.
- **Transitive Edge Styles**: Distinct dashed rendering and category-specific coloring for indirect dependencies.

### Changed
- Unified visual naming and taxonomy (Project Flow) across the webview and sidebar.

### Fixed
- Normalized CRLF and LF line endings during content hash computation.
- Cleaned up Layer 4 ontology tree: removed redundant DataSet categories and refined fallback icons.

---

## [1.10.0] - 2026-09-28

### Added
- **AI Intent Distillation**: Small language model (SLM) integration for two-stage top-down architectural intent distillation.
- **AI Bounded Context Map**: Visual mapping of domain boundaries, aggregate roots, and subsystem responsibilities.
- **AI Model Management**: In-extension model downloader (`vmikhailov77` Hugging Face repository) and local inference status.
- **New MCP Tools**: Added `distill_architectural_intents` and `get_bounded_context_map` tools for AI pair programming.

### Changed
- Decoupled domain and bounded contexts routing in repository and graph view handlers.

---

## [1.9.0] - 2026-09-27

### Added
- **Code-Intent-Distill Analyzer**: Post-indexing pipeline extracting architectural capabilities, target entities, and emitted events.
- **Data-Flow Constant Resolution**: Evaluates compile-time constants to resolve dynamic configuration keys, route patterns, and topic names.

### Fixed
- Hardened TypeORM import resolution and channel persistence synchronization.
- Removed ambiguous docker-compose heuristics to eliminate false-positive service dependencies.

---

## [1.7.0] - 2026-09-24

### Added
- **ArchitectureViewEngine**: Centralized domain modeling and graph projection engine replacing legacy data converters.
- Optimized multi-project Cypher query performance and reduced memory allocation.

### Changed
- Reworked 5-layer ontology graph schema with explicit project-level semantic boundaries.

---

## [1.5.0] - 2026-09-23

### Added
- **First-Class Semantic Entities**: Promoted Service, App, and Library to first-class citizens in the ontology graph.
- **ColdFusion Parser**: Added full parser support for CFML, CFC components, and CFScript.
- **Interactive Undo/Redo**: Added command-based history stack (`CommandManager`) in the React Flow graph webview.

---

## [1.4.0] - 2026-09-21

### Added
- **VS Code Extension Launch**: Initial release of the CodeExplorer VS Code visualizer extension.
- **Frontend & RPC Parsers**: Added support for Angular HttpClient, OIDC authentication, Refit, SignalR hubs, and GraphQL clients.
- **DAG Topological Ranking**: Implemented longest-path DAG topological ranking to eliminate same-level edge crossings.
- **Rescan & Re-index**: Added `ce scan <folder>`, workspace rescan, and full re-index commands.

---

## [1.3.0] - 2026-09-16

### Added
- **Distributed Actors & Persistence**: Support for Microsoft Orleans virtual actor grains, MongoDB collections, and MediatR pipelines.
- **CQRS & Event Sagas**: Cross-service event publishing and subscriber tracing.

---

## [1.2.0] - 2026-09-15

### Added
- **API Contract Extraction**: Automatic request/response DTO schema extraction and endpoint cataloging.
- **Gateway Ingress/Egress Tracing**: Tracing client calls through reverse proxies and API gateways to downstream services.
- Streamlined console logging output and shortened layer prefixes.

---

## [1.1.0] - 2026-09-15

### Added
- **Unified MCP Server**: Feature parity with CLI for AI agent pair-programming (`get_architecture_map`, `find_symbol`, `get_call_chain`, `inspect_data_lineage`, etc.).
- **Diagram Export**: Added `ce export` command supporting C4 and Mermaid format generation.
- Enforced `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` globally for zero-warning compiler builds.

---

## [1.0.0] - 2026-09-15

### Added
- **`ce` CLI**: Single-file, self-contained cross-platform native binaries for Windows, Linux, and macOS.
- **.NET Global Tool**: Published as `codeexplorer.cli` on NuGet.org with Trusted Publishing (OIDC).
- **Embedded SQLite Graph**: Lightweight embedded graph storage engine with custom Cypher-to-SQL transpiler.
- **Multi-Language AST Parsers**: Tree-sitter powered syntactic parsing for C#, TypeScript/JavaScript, Python, Go, Java, and SQL.
- **5-Layer Architecture Ontology**: Physical, Boundary, Syntactic, Semantic, and Intent layers.
