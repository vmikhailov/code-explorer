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

---

### Backlog & Planned Initiatives
- [ ] **Ingress & Egress Parsers (C#)**:
  - Full ASP.NET Core route composition: Controller-level `[Route("api/[controller]")]` + Action-level `[HttpGet("{id}")]` with token substitutions (`[controller]`, `[action]`).
  - Minimal API `app.MapGroup(...)` prefix concatenation.
  - Target URL/path resolution for `HttpClient`, `RestSharp`, and `Refit` declarative interfaces.
- [ ] **C# Constructor Dependency Injection Resolution**: Map constructor parameters to private fields and trace interface calls through `[:IMPLEMENTS]`.
- [ ] **EF Core & Dapper Data Lineage**: Extract table names from `DbSet<T>` and Fluent API `ToTable("...")` to link C# code directly to database tables in `inspect_data_lineage`.
- [ ] **VS Code & Antigravity IDE Extension**: Interactive Cytoscape.js webview cockpit, bi-directional code navigation, and live LSP bridge using `vscode.executeDefinitionProvider`.
- [ ] **Incremental File Watcher (`ce watch`)**: Real-time graph synchronization during active coding sessions.

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
     - Cache static graph structures (such as `get_architecture_map` and `get_taxonomy`) in memory with an invalidation trigger on `ce scan` / `ce clear`.
  3. **Cartesian Explosion Detection**:
     - In `SqliteCompiler`, detect multiple independent `OPTIONAL MATCH` clauses and translate them into separate sequential joins or correlated subqueries rather than a single Cartesian product CTE.

---

### 2.4 Concurrency & Incremental Synchronization (Priority: Medium)

#### 2.4.1 SQLite Single-Writer Lock Mitigation
1. **Read-Only Connections for MCP**:
   - Open SQLite connections for MCP tool calls with `Mode=ReadOnly;` to prevent lock escalation.
2. **Busy Timeout & WAL Tuning**:
   - Set `PRAGMA busy_timeout = 5000;` and `PRAGMA synchronous = NORMAL;` to allow smooth concurrent reads while background indexing executes.

#### 2.4.2 Incremental File Watching
1. **File Watcher (`ce watch`)**:
   - Implement `FileSystemWatcher` tracking file modifications in active workspaces.
   - Selectively trigger `ParsingContext.IsSubtreeScan` on modified files only, keeping the graph synchronized in real-time during development without requiring manual full scans.

---

## 3. Master Implementation Checklist

- [ ] **1.1**: Implement `WorkspaceLocator.FindWithFallbacks` with environment variable checks (`WORKSPACE_ROOT`, `CE_WORKSPACE`, `VSCODE_WORKSPACE`).
- [ ] **1.2**: Implement MCP Standby mode in `Program.cs` without throwing on startup when no workspace is detected.
- [ ] **1.3**: Set `WriteIndented = false` by default for compact JSON in `ExecuteAndFormatQueryAsync`.
- [ ] **1.4**: Add `format: "markdown" | "mermaid" | "json"` parameter to `GetProjectDependenciesAsync`.
- [ ] **1.5**: Add Mermaid sequence / flowchart formatter for `GetCallChainAsync`.
- [ ] **1.6**: Add compact Markdown table formatters for `FindSymbolAsync` and `GetFileOutlineAsync`.
- [ ] **2.1**: Enhance `GitIgnoreMatcher` to recursively load nested `.gitignore` files in monorepo subdirectories.
- [ ] **2.2**: Add default directory exclusions (`node_modules`, `dist`, `bin`, `obj`, `.next`, `coverage`).
- [ ] **2.3**: Add minified bundle heuristic check to skip obfuscated JS/TS chunks.
- [x] **2.4**: Implement NestJS `@Module`, `@Injectable()`, and `@Controller` AST parsers in `TypeScriptParser`.
- [x] **2.5**: Complete BullMQ, KafkaJS, and Elasticsearch library parsers for TypeScript.
- [ ] **3.1**: Verify and optimize compound indexes on SQLite `edges` table.
- [ ] **3.2**: Add query result caching for static graph structures (`get_architecture_map`, `get_taxonomy`).
- [ ] **4.1**: Ensure MCP client connection uses `Mode=ReadOnly` with WAL concurrency tuning.
- [ ] **4.2**: Implement `ce watch` file watcher for live incremental indexing.
