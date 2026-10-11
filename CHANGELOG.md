# Changelog

All notable changes to the CodeExplorer project (CLI, MCP Server, and VS Code Extension) are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.32.0] - 2026-10-11

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
