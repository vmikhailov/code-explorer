# CodeExplorer Backlog & Roadmap Specifications

## 1. High-Level Status & Milestones

### Completed
- [x] **Line numbers in symbol search**: `find_symbol` returns `file_path`, `start_line`, and `end_line` for direct source navigation.
- [x] **Graph schema discovery for Cypher**: `get_taxonomy` and `get_node_definition` tools expose complete ontology labels, relationship types, and node properties.
- [x] **Single database per workspace & `ce` CLI**: Streamlined embedded architecture with SQLite and self-contained single-file binary.
- [x] **Cypher query depth bounding & timeout resilience**: Eliminated unbounded graph traversal hangs, added 15s `CommandTimeout` and `CancellationToken` support.
- [x] **TypeScript & Node.js Library Parsers**:
  - Library trie registry matching with subpath support for popular database, queue, and framework packages.
  - Comprehensive library parsers: NestJS, Express, Fastify, Koa, Next.js App Router, BullMQ, KafkaJS, Elasticsearch, Sequelize, TypeORM, Prisma, Drizzle, Pg, Mysql2, Sqlite3, Neo4j, InfluxDB, Got/Ky.
  - Complete eradication of AST magic strings across all parsers (TypeScript, Go, Python, C#) with typed TreeSitterSyntax.

---

- [x] **Message-driven ingress and egress detection**: MassTransit consumers/publishers, MediatR handlers/requests.
- [x] **MCP Multi-Source CWD & Standby Mode**: Multi-source resolution order, standby graceful degradation mode, dynamic per-call workspacePath.
- [x] **Token-Efficient Multi-Format Outputs**: Native Mermaid diagram generation (`graph TD`), compact Markdown tables, compact JSON (`WriteIndented = false`), YAML, and TOON (Token-Oriented Object Notation).
- [x] **Monorepo & Nested `.gitignore` Support**: Recursive scoped ignore loading and minified/vendor bundle heuristic filtering.
- [x] **Token-Based Mutating Query Security Validator**: Enforced word boundaries (`\b`) and regex token isolation for mutating Cypher statements (`SET`, `CREATE`, `DELETE`, etc.), eliminating false positive rejections on benign identifiers like `databaseType`.
- [x] **Data Lineage Traversal Optimization**: Resolved latency bottlenecks on high-degree node lineage queries (`inspect_data_lineage`), dropping multi-minute traversals on large production graphs to sub-20ms.
- [x] **Zero-Warning & Zero-Noise Test Suite**: Added `--quiet` flag to `ce mcp`, silenced diagnostic loggers in test runners, and achieved 100% clean test execution (382/382 passed, 0 warnings, 0 errors with `/warnaserror`).
- [x] **Real-World Multi-Project Scale Verification (Dedalos & ATS)**:
  - Fixed SQLite constraint violation (Error 19) in `PostIndexAnalyzer` and `SqliteGraphClient` during large-scale post-indexing.
  - Successfully indexed Dedalos (`pow3`): 146 C# projects, 149,078 nodes, 655,095 edges.
  - Fixed Cypher compiler implicit `GROUP BY` for `WITH` clauses containing aggregations (e.g. `WITH labels(n)[0] AS kind, count(n) AS count`).
  - Added CLI query parameter support (`-p` / `--param key=value`) for parameterized built-in queries.
  - Eliminated variable-depth folder recursion bottlenecks in `get_architecture_map_project.cypher`.
  - Filtered C#/TypeScript code expression noise from `ExternalService` discovery.

---

### Backlog & Planned Initiatives
- [ ] **OpenCypher Edge Navigation & Transpiler Expansion**:
  - Full edge function support: `type(r)`, `properties(r)`, `startNode(r)`, `endNode(r)` across `WITH`, `UNWIND`, and aggregations.
  - Multi-branch `OPTIONAL MATCH` Cartesian product decomposition (correlated subqueries / discrete CTEs).
  - Path and list predicates: `WHERE EXISTS((n)-[:REL]->(m))` and quantifiers `all()`, `any()`, `none()`.
- [ ] **SQLite Concurrency & In-Memory Macro Structure Caching**:
  - In-memory caching for static workspace maps (`get_architecture_map_workspace`, `get_taxonomy`) in `CodeExplorerRepository` with invalidation triggers on `ce scan` / `ce clear`.
  - SQLite PRAGMA concurrency tuning: `PRAGMA busy_timeout = 5000;`, `PRAGMA journal_mode = WAL;`, `PRAGMA synchronous = NORMAL;`, `PRAGMA cache_size = -64000;`, `PRAGMA temp_store = MEMORY;` and enforcing `Mode=ReadOnly` on tool connection strings.
- [ ] **Ingress & Egress Parsers (C#)**:
  - Full ASP.NET Core route composition: Controller-level `[Route("api/[controller]")]` + Action-level `[HttpGet("{id}")]` with token substitutions (`[controller]`, `[action]`).
  - Minimal API `app.MapGroup(...)` prefix concatenation.
  - Target URL/path resolution for `HttpClient`, `RestSharp`, and `Refit` declarative interfaces.
- [ ] **C# Constructor Dependency Injection Resolution**: Map constructor parameters and primary constructor parameters to private fields and trace interface calls through `[:IMPLEMENTS]` to concrete service classes on Layer 5.
- [ ] **EF Core & Dapper Data Lineage**: Extract table names from `DbSet<T>` and Fluent API `ToTable("...")` to link C# code directly to database tables in `inspect_data_lineage`.
- [ ] **Incremental File Watcher (`ce watch`)**: Real-time graph synchronization during active coding sessions via debounced `ParsingContext.IsSubtreeScan`.
- [ ] **Self-Contained CI/CD Benchmark Fixture**: Portable synthetic 100k-node graph fixture for automated performance regression testing in CI, decoupling tests from local machine paths.
- [ ] **VS Code & Antigravity IDE Extension**: Interactive Cytoscape.js webview cockpit, bi-directional code navigation, and live LSP bridge using `vscode.executeDefinitionProvider`.

---

## 2. Detailed Technical Specifications & Architectural Recommendations

### 2.1 MCP Lifecycle & Client Ergonomics (Priority: High)

#### 2.1.1 Multi-Source CWD & Workspace Resolution (Eliminate Boot Crash)
* **Problem**: Currently, `ce mcp` calls `WorkspaceLocator.FindOrThrow(opts.Root)` at startup. If `--root` is omitted and the process is spawned with `cwd: /` (the default behavior of many IDE extension hosts, language servers, and subagent executors), `ce mcp` crashes immediately with `InvalidOperationException`.
* **Technical Solution**:
  1. **Multi-Source Fallback Strategy** in `WorkspaceLocator.cs`:
     Implement `WorkspaceLocator.FindWithFallbacks(string? explicitPath = null)` with the following resolution order:
     - **Explicit Argument**: `opts.Root` or `opts.DbPath` passed via CLI.
     - **Environment Variables**: Inspect `WORKSPACE_ROOT`, `CE_WORKSPACE`, or `VSCODE_WORKSPACE`.
     - **Current Working Directory**: Walk up from `Directory.GetCurrentDirectory()`.
     - **User Home Directory / Registry**: Check for previously registered workspaces in `~/.codeexplorer/workspaces.json` if available.
  2. **Standby / Graceful Degradation Mode** in `Program.cs` (`HandleMcpAsync`):
     - If no workspace is found during startup, do **not** crash the process.
     - Boot `RunMcpStdioHostAsync` or `RunMcpWebServerAsync` in a **Standby State** with `client = null`.
     - When a tool is invoked without a bound workspace, return a structured, actionable MCP error response guiding the user or assistant to bind via `--root` or environment variables.
  3. **Dynamic Workspace Parameter**:
     - Allow MCP tool requests to pass an optional `workspacePath` or `root` parameter on individual tool calls to dynamically bind to a workspace on the fly.

#### 2.1.2 Format Switching & Context-Efficient Output (Reduce LLM Token Bloat)
* **Problem**:
  - Every tool currently serializes its output with `WriteIndented = true`, wasting 35%–45% of LLM context window tokens on whitespace and newlines.
  - Relational queries like `get_project_dependencies` and `get_call_chain` return massive JSON structures (50KB+) that require the LLM to spend extra turns parsing and correlating node IDs.
* **Technical Solution**:
  1. **Add `format` Parameter across Graph Tools**:
     Add `format: "markdown" | "mermaid" | "json"` (default: `"markdown"` for human/LLM readability, `"json"` for programmatic consumption):
     ```csharp
     [McpServerTool]
     public async Task<CallToolResult> GetProjectDependenciesAsync(
         [Description("Optional project filter")] string? projectFilter = null,
         [Description("Output format: 'markdown', 'mermaid', or 'json'")] string format = "markdown",
         [Description("Maximum results to return")] int limit = 50,
         CancellationToken cancellationToken = default);
     ```
  2. **Native Mermaid Diagram Generation**:
     - **`get_project_dependencies`**: Output a `graph TD` block showing directed edges:
       ```mermaid
       graph TD
           adhub-cf-worker --> rule-tree
           rule-tree --> redis
       ```
     - **`get_call_chain`**: Output a Mermaid sequence or flowchart trace (`funcA --> funcB --> funcC`).
     - Reduces token count by **up to 80%** compared to raw JSON arrays.
  3. **Compact Markdown Tables**:
     - **`find_symbol`**: Return `| Kind | Name | File | Line |` table.
     - **`get_file_outline`**: Return a concise hierarchical bullet list or Markdown outline instead of deep JSON objects.
  4. **Compact JSON Default**:
     - When `format == "json"`, set `WriteIndented = false` to save thousands of prompt tokens.
  5. **Pagination & Guardrails**:
     - Introduce `limit` (default: 50) and `offset` (default: 0) parameters to prevent flooding LLM context windows on large enterprise graphs.

---

### 2.2 Parser Intelligence & Monorepo Filtering (Priority: High)

#### 2.2.1 Multi-Level Monorepo `.gitignore` & Ignore Policies
* **Problem**: `GitIgnoreMatcher` currently only checks `.gitignore` at the `workspaceRoot`. In enterprise monorepos (e.g. ATS), subdirectories and services define their own localized `.gitignore` files. Without a root `.gitignore`, build artifacts (`dist/`, `obj/`, `node_modules/`) get scanned, creating hundreds of thousands of noisy nodes.
* **Technical Solution**:
  1. **Hierarchical `.gitignore` Loading**:
     - During `Layer1PhysicalParser` traversal, check for `.gitignore` in each encountered folder and apply rules scoped to that sub-tree.
  2. **Built-in Safe Defaults**:
     - Automatically ignore standard build and package manager directories even if no `.gitignore` exists:
       `node_modules/`, `bin/`, `obj/`, `dist/`, `.next/`, `coverage/`, `.git/`, `.turbo/`, `.cache/`, `.Build/`.
  3. **Minified / Obfuscated Bundle Heuristic Filter**:
     - Check file characteristics before parsing: single-line files > 50KB, average line length > 1,000 characters, or high density of single-character mangled identifiers.
     - Skip these files to prevent garbage tokens (e.g., `K.Zr(...)`) from corrupting the `LateBinding` phase.

#### 2.2.2 TypeScript & NestJS Framework Parity
* **Problem**: Over 90% of real-world microservice codebases (such as ATS) are written in TypeScript, whereas CodeExplorer's deepest library enrichers were initially C#-specific.
* **Technical Solution**:
  1. **NestJS Dependency Injection & Architecture**:
     - Parse `@Module({ imports, controllers, providers, exports })` declarations to establish `[:CONTAINS]` and `[:DEPENDS_ON]` edges between modules and services.
     - Parse `@Injectable()` classes and constructor injection parameters (`@Inject(...)`).
     - Resolve interface tokens to concrete implementation providers.
  2. **TypeScript Routing & Ingress**:
     - NestJS `@Controller("path")` + `@Get()`, `@Post()`, `@Put()`, `@Delete()`, `@Patch()`.
     - Express / Fastify `app.get("/path", ...)`, `router.use("/api", ...)`.
     - Next.js App Router (`app/**/route.ts`).
  3. **TypeScript Egress & Message Clients**:
     - HTTP: Axios (`axios.get`, `axios.create({ baseURL })`), `fetch()`.
     - Queues: BullMQ (`new Queue()`, `new Worker()`), KafkaJS (`producer.send()`, `consumer.run()`).
     - Storage / Search: Elasticsearch (`client.search()`), Redis (`ioredis`, `redis`).

#### 2.2.3 C# Ingress, Egress & Dependency Injection
* **Technical Solution**:
  1. **ASP.NET Core Hierarchical Route Composition**:
     - Combine Controller-level `[Route("api/[controller]")]` with Action-level attributes (`[HttpGet("{id}")]`), resolving token substitutions (`[controller]`, `[action]`).
     - Track Minimal API `app.MapGroup("/api/v1")` prefixes.
  2. **C# Constructor DI Mapping**:
     - Map constructor parameters and primary constructors to private fields (e.g., `_orderService`).
     - Link method calls (`_orderService.Create()`) through `[:IMPLEMENTS]` to concrete classes.
  3. **Data Lineage (EF Core & Dapper)**:
     - Extract table names from `DbSet<T>` properties and Fluent API `ToTable("...")` in EF Core.
     - Match Dapper SQL constants to database entities in `inspect_data_lineage`.

---

### 2.3 Cypher Query Engine & Performance (Priority: Medium)

#### 2.3.1 Recursive CTE Guardrails & Query Optimization
* **Problem**: Translating unbounded or deep graph traversals into SQLite Recursive CTEs causes exponential joins and query timeouts on large graphs (100k+ nodes).
* **Technical Solution**:
  1. **Index Hints & Pre-Filtered Subqueries**:
     - Ensure the `edges` table has compound covering indexes:
       - `CREATE INDEX idx_edges_from_kind ON edges(from_id, kind);`
       - `CREATE INDEX idx_edges_to_kind ON edges(to_id, kind);`
       - `CREATE INDEX idx_edges_kind ON edges(kind);`
  2. **Query Result Caching**:
     - Cache static graph structures (such as `get_architecture_map_workspace` and `get_taxonomy`) in memory with an invalidation trigger on `ce scan` / `ce clear`.
  3. **Cartesian Explosion Detection**:
     - In `SqliteCompiler`, detect multiple independent `OPTIONAL MATCH` clauses and translate them into separate sequential joins or correlated subqueries rather than a single Cartesian product CTE.

#### 2.3.2 OpenCypher Edge Navigation & Function Compatibility
* **Problem**:
  - `SqliteCompiler` currently treats edges as anonymous join conditions without first-class expression semantics.
  - Queries using OpenCypher relationship functions (`type(r)`, `properties(r)`, `startNode(r)`, `endNode(r)`) or edge variable projections (e.g. `MATCH (a)-[r:CALLS]->(b) RETURN type(r), properties(r), r.weight`) fail during transpilation or throw unsupported function exceptions.
* **Technical Solution**:
  1. **Edge Variable Scoping**:
     - Expose relationship variables `r` in the symbol table with bindings to `edges.kind` (`type(r)`), `edges.properties_json` (`properties(r)`), `edges.from_id` (`startNode(r)`), and `edges.to_id` (`endNode(r)`).
  2. **Relational Attribute Access**:
     - Support accessing property fields directly on edge variables (e.g., `r.weight`, `r.route`) using `json_extract(r.properties_json, '$.weight')`.
  3. **Multi-Hop Edge Aggregations & WITH Clauses**:
     - Allow edge variables to pass cleanly through intermediate `WITH` clauses and aggregations (e.g., `WITH r, count(r) AS rel_count`).

#### 2.3.3 Multi-Branch OPTIONAL MATCH Decomposition (Cartesian Product Elimination)
* **Problem**:
  - Complex architectural queries frequently query multiple orthogonal relationships from a single central entity (e.g. finding a table's upstream writers, downstream readers, and schema definitions).
  - Translating multiple `OPTIONAL MATCH` clauses into chained relational `LEFT JOIN`s causes an $O(N \cdot M \cdot K)$ Cartesian row expansion before grouping or `DISTINCT`, generating millions of intermediate rows and stalling SQLite.
* **Technical Solution**:
  1. **Independent Branch Isolation**:
     - Analyze the query AST to detect when separate `OPTIONAL MATCH` branches only correlate back to the root node variable.
  2. **Correlated Subqueries / Discrete CTEs**:
     - Transpile independent branches into isolated correlated subqueries (e.g., `SELECT json_group_array(...) FROM edges ... WHERE from_id = root.id`) or discrete CTEs aggregated by root node ID before joining to the projection query.
     - Keeps computational complexity strictly linear: $O(N + M + K)$.

#### 2.3.4 Path & List Predicates in Cypher Transpiler
* **Problem**:
  - Expressive pattern conditions like `WHERE EXISTS((n)-[:IMPLEMENTS]->(:Interface))` or list predicates (`all(x IN list WHERE ...)`, `any(...)`, `none(...)`) are not yet transpiled.
* **Technical Solution**:
  1. **EXISTS Pattern Predicate**:
     - Map `EXISTS((a)-[r:KIND]->(b))` into SQL `EXISTS (SELECT 1 FROM edges e WHERE e.from_id = a.id AND e.kind = 'KIND' ...)`.
  2. **List Quantifiers**:
     - Implement `any()`, `all()`, and `none()` functions over JSON arrays via `json_each()` subqueries.

---

### 2.4 Concurrency & Incremental Synchronization (Priority: Medium)

#### 2.4.1 SQLite Single-Writer Lock Mitigation & WAL Concurrency
* **Problem**:
  - Background indexing or concurrent tool invocations can trigger `SQLite Error: database is locked` if connection parameters and transaction locks are not explicitly tuned for high-concurrency reader/writer isolation.
* **Technical Solution**:
  1. **Read-Only Connections for MCP**:
     - Open read connections in `CodeExplorerRepository` with `Data Source=...;Mode=ReadOnly;Cache=Shared;`.
  2. **WAL Tuning & Connection PRAGMAs**:
     - On connection initialization, execute:
       ```sql
       PRAGMA busy_timeout = 5000;
       PRAGMA journal_mode = WAL;
       PRAGMA synchronous = NORMAL;
       PRAGMA cache_size = -64000;
       PRAGMA temp_store = MEMORY;
       ```

#### 2.4.2 In-Memory Macro Structure Caching
* **Problem**:
  - Macro-level queries such as `get_architecture_map_workspace` and `get_taxonomy` traverse entire workspace node graphs (hundreds of thousands of rows). Repeating these queries during multi-step assistant sessions creates unnecessary I/O and latency.
* **Technical Solution**:
  1. **Thread-Safe Repository Cache**:
     - Add `ConcurrentDictionary` or `MemoryCache` in `CodeExplorerRepository` for macro results keyed by workspace root and options.
  2. **Invalidation Hooks**:
     - Invalidate caches on mutation operations: `ce scan`, `ce clear`, or upon detecting file modification events via watcher.

#### 2.4.3 Incremental File Watching (`ce watch`)
* **Problem**:
  - Developers currently have to run `ce scan` manually after refactoring or editing files.
* **Technical Solution**:
  1. **File Watcher (`ce watch`)**:
     - Implement `FileSystemWatcher` tracking file modifications in active workspaces with a 500ms debounce buffer.
     - Selectively trigger `ParsingContext.IsSubtreeScan` on modified files only, updating the SQLite graph incrementally in sub-second time without blocking concurrent tool queries.

---

### 2.5 Test Automation & Benchmark Fixtures (Priority: Low / Medium)

#### 2.5.1 Portable Synthetic 100k-Node CI Benchmark Fixture
* **Problem**:
  - `Layer5BenchmarkTests` currently relies on a machine-local graph path (`/Users/slava/Projects/ATS/src/.codeexplorer/graph.db`) and is skipped if the local database does not exist. This prevents CI/CD pipelines from automatically catching performance regressions on large graphs.
* **Technical Solution**:
  1. **Deterministic In-Memory Synthetic Graph Generator**:
     - Implement a benchmark fixture that programmatically seeds a temporary SQLite database with a realistic enterprise topology:
       - 10,000 files, 25,000 classes/structs, 70,000 methods/functions, 500 API endpoints, 200 DB tables.
       - 300,000 edges (`CONTAINS`, `CALLS`, `IMPLEMENTS`, `WRITES_TO`, `READS_FROM`).
  2. **Automated SLA Assertion Suite**:
     - Run benchmarks as part of the test suite verifying:
       - `find_symbol`: < 25ms.
       - `get_call_chain` (3 hops): < 100ms.
       - `inspect_data_lineage`: < 50ms.
       - OpenCypher multi-branch traversals: < 150ms.

---

## 3. Master Implementation Checklist

- [x] **1.1**: Implement `WorkspaceLocator.FindWithFallbacks` with environment variable checks (`WORKSPACE_ROOT`, `CE_WORKSPACE`, `VSCODE_WORKSPACE`).
- [x] **1.2**: Implement MCP Standby mode in `Program.cs` without throwing on startup when no workspace is detected.
- [x] **1.3**: Set `WriteIndented = false` by default for compact JSON in `ExecuteAndFormatQueryAsync`.
- [x] **1.4**: Add `format: "markdown" | "mermaid" | "json" | "yaml" | "toon"` parameter to `GetProjectDependenciesAsync`.
- [x] **1.5**: Add Mermaid sequence / flowchart formatter for `GetCallChainAsync`.
- [x] **1.6**: Add compact Markdown table formatters for `FindSymbolAsync` and `GetFileOutlineAsync`.
- [x] **2.1**: Enhance `GitIgnoreMatcher` to recursively load nested `.gitignore` files in monorepo subdirectories.
- [x] **2.2**: Add default directory exclusions (`node_modules`, `dist`, `bin`, `obj`, `.next`, `coverage`).
- [x] **2.3**: Add minified bundle heuristic check to skip obfuscated JS/TS chunks.
- [x] **2.4**: Implement NestJS `@Module`, `@Injectable()`, and `@Controller` AST parsers in `TypeScriptParser`.
- [x] **2.5**: Complete BullMQ, KafkaJS, and Elasticsearch library parsers for TypeScript.
- [x] **3.1**: Verify and optimize compound indexes on SQLite `edges` table.
- [ ] **3.2**: Add in-memory query result caching for static graph structures (`get_architecture_map_workspace`, `get_taxonomy`) with invalidation hooks.
- [ ] **3.3**: Support OpenCypher relationship functions (`type(r)`, `properties(r)`, `startNode(r)`, `endNode(r)`) and edge variable bindings in `SqliteCompiler`.
- [ ] **3.4**: Implement multi-branch `OPTIONAL MATCH` Cartesian product decomposition into correlated subqueries/CTEs.
- [ ] **3.5**: Transpile path predicates (`WHERE EXISTS((n)-[:REL]->(m))`) and list quantifiers (`all()`, `any()`, `none()`) in Cypher compiler.
- [ ] **4.1**: Configure SQLite connections with `Mode=ReadOnly`, WAL tuning (`PRAGMA busy_timeout = 5000`, `cache_size = -64000`).
- [ ] **4.2**: Implement `ce watch` file watcher with debounced subtree scanning (`ParsingContext.IsSubtreeScan`).
- [ ] **5.1**: Implement ASP.NET Core hierarchical route composition (`[Route]` + `[HttpGet]`) and Minimal API `MapGroup` in `CSharpParser`.
- [ ] **5.2**: Implement C# constructor DI resolution and interface call late binding in Layer 5.
- [ ] **5.3**: Implement EF Core `DbSet<T>` / `ToTable` and Dapper data lineage mapping in `CSharpParser`.
- [ ] **6.1**: Implement self-contained synthetic 100k-node graph benchmark fixture for CI/CD in `Layer5BenchmarkTests`.
