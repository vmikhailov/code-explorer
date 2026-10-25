# CodeExplorer (`ce`) — Make AI Coding Agents Production-Safe

[![Website](https://img.shields.io/badge/Website-code--explorer.app-8b5cf6.svg)](https://code-explorer.app)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/vmikhailov/code-explorer/blob/main/LICENSE)
[![Homebrew](https://img.shields.io/badge/Homebrew-vmikhailov%2Ftap%2Fce-orange.svg)](https://github.com/vmikhailov/homebrew-tap)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/download)
[![NuGet](https://img.shields.io/nuget/v/CodeExplorer.Cli.svg)](https://www.nuget.org/packages/CodeExplorer.Cli)
[![VS Marketplace](https://vsmarketplacebadges.dev/version/vmikhailov.code-explorer-vscode.svg?label=Marketplace)](https://marketplace.visualstudio.com/items?itemName=vmikhailov.code-explorer-vscode)
[![Open VSX](https://img.shields.io/open-vsx/v/vmikhailov/code-explorer-vscode?label=Open%20VSX)](https://open-vsx.org/extension/vmikhailov/code-explorer-vscode)

**AI agents see files, not architecture.** CodeExplorer (`ce`) gives your AI coding agents (`Claude Code`, `Cursor`, `GitHub Copilot`, `Windsurf`, `Cline`) and engineering teams a **live cross-service map of every API, queue, and database in VS Code & MCP—plus hard architectural guardrails in CI**.

**✓ Live Architecture Map in VS Code** • **✓ Surgical MCP Context (7.5× Faster, 40× Fewer Tokens)** • **✓ CI Architecture Gates** • **✓ 100% Local SQLite Graph**

It indexes polyglot source files, project manifests, HTTP/gRPC/GraphQL endpoints, message broker topologies, and SQL schemas into a single local SQLite graph (`.codeexplorer/graph.db`) powered by an embedded OpenCypher-to-SQL compiler:

1. **Live Architecture Map in VS Code**: Interactive C1/C2/C3 service topologies, concentric architectural tiers, async broker flows, and database lineage with click-to-source navigation.
2. **Surgical MCP Context for AI Agents**: Gives coding agents instant access to call chains, cross-service dependencies, data lineage, and blast-radius impact analysis without burning millions of tokens on blind `grep` searches.
3. **CI Architecture Gates (Semantic Invariant Gates)**: Enforces architectural boundaries in the IDE and CI (`ce gate check`) using declarative OpenCypher rules and SHA-256 ratchet baselines.

### Enterprise Benchmark: Plain AI Agent (`ripgrep`) vs. AI Agent + CodeExplorer MCP
*(Measured on a 450-module enterprise monorepo: 324 microservices, 93 shared libraries, 33 workers — [full benchmark details](./benchmarks/02-enterprise-microservices-deep-audit/README.md))*

| Metric | Plain AI Agent (`ripgrep` + file reads) | AI Agent + **CodeExplorer MCP** | Improvement |
| :--- | :--- | :--- | :--- |
| **Analysis Time** | 29m 52s | **3m 56s** | **7.59× faster** |
| **Cumulative Input Tokens** | 17.19M tokens | **393K tokens** | **43.7× fewer tokens** |
| **Agent Effort** | 285 turns (142 shell commands, 114 file reads) | **23 turns (18 graph queries, 0 file reads)** | **12.4× fewer turns** |
| **Blast-Radius Recall** | 82% *(missed 9 RTB services behind env constants)* | **100% recall** *(all services & DB tables found)* | **Zero blind spots** |

<p align="center">
  <img src="https://raw.githubusercontent.com/vmikhailov/code-explorer/main/docs/all_services.png" alt="CodeExplorer Service Topology" width="100%" />
</p>

<p align="center">
  <em>Service topology projected across concentric architectural tiers (Ingress, Public Services, Message Brokers, Domain Services, and Databases).</em>
</p>

<p align="center">
  <a href="#installation">Installation</a> •
  <a href="#quick-start">Quick Start</a> •
  <a href="#mcp-server">MCP Server</a> •
  <a href="#semantic-invariant-gates">Invariant Gates</a> •
  <a href="#vs-code-extension">VS Code Extension</a> •
  <a href="#supported-languages--frameworks">Languages</a> •
  <a href="#graph-ontology">Ontology</a> •
  <a href="#cli-reference">CLI Reference</a> •
  <a href="#mcp-tools">MCP Tools</a>
</p>

---

## Installation

CodeExplorer is distributed as a self-contained single-file binary (`ce` / `ce.exe`) with embedded Tree-sitter grammars and SQLite, and as a VS Code extension that bundles the native binary for your platform.

### Homebrew (macOS & Linux)

```bash
brew install vmikhailov/tap/ce
```

### VS Code Extension

Install from the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=vmikhailov.code-explorer-vscode) or [Open VSX Registry](https://open-vsx.org/extension/vmikhailov/code-explorer-vscode):

```bash
code --install-extension vmikhailov.code-explorer-vscode
```

### Install Script

**macOS & Linux:**
```bash
curl -fsSL https://raw.githubusercontent.com/vmikhailov/code-explorer/main/cli/scripts/install.sh | bash
```

**Windows (PowerShell):**
```powershell
irm https://raw.githubusercontent.com/vmikhailov/code-explorer/main/cli/scripts/install.ps1 | iex
```

### .NET Global Tool

```bash
dotnet tool install -g CodeExplorer.Cli
```

### Prebuilt Binaries (Native Standalone)

Standalone archives are available on [GitHub Releases](https://github.com/vmikhailov/code-explorer/releases/latest):

| OS | Architecture | Asset |
| :--- | :--- | :--- |
| macOS | Apple Silicon (`arm64`) | `ce-osx-arm64.tar.gz` |
| Windows | x64 | `ce-win-x64.zip` |

*Note: For other architectures (Linux x64/ARM64, macOS Intel, Windows ARM64), install the lightweight universal .NET tool: `dotnet tool install -g CodeExplorer.Cli`.*


---

## Quick Start

```bash
# 1. Initialize .codeexplorer/ in your repository
ce init

# 2. Parse ASTs, project references, endpoints, and cross-service links
ce scan

# 3. Inspect workspace summary and node/edge counts
ce status

# 4. Run a built-in or ad-hoc OpenCypher query
ce query -n get_architecture_map_workspace
ce query "MATCH (s:Service)-[:CALLS]->(t:Service) RETURN s.name, t.name"

# 5. Evaluate architectural gates
ce gate check

# 6. Start the MCP server (stdio)
ce mcp
```

---

## MCP Server

Text search (`grep`, `ripgrep`) works well for finding literals, but it cannot reliably trace interface implementations, cross-service HTTP/gRPC calls, message queue publishers/consumers, or SQL table usage across projects. Reading dozens of files to reconstruct call graphs consumes context window budget and degrades agent accuracy.

Running `ce mcp` exposes the indexed SQLite graph over the Model Context Protocol:
* **Architecture & Entry Points**: Query projects, dependencies, HTTP/gRPC/GraphQL endpoints, and message consumers via `get_architecture_map` and `get_project_entry_points`.
* **Low-Token File Outlines**: Inspect classes, methods, and line ranges in a file via `get_file_outline` before reading full source text.
* **Impact & Lineage Analysis**: Trace downstream callers of a symbol (`analyze_code_impact`) or all SQL/ORM readers and writers of a database table (`inspect_data_lineage`).
* **OpenCypher Queries**: Execute read-only Cypher queries (`execute_custom_read_cypher`) and save reusable project queries in `.codeexplorer/queries/`.

### Client Configuration

Add `ce` to your MCP client configuration (`.cursor/mcp.json`, `claude_desktop_config.json`, or VS Code MCP settings):

```json
{
  "mcpServers": {
    "code-explorer": {
      "command": "ce",
      "args": ["mcp"]
    }
  }
}
```

Or auto-provision your workspace MCP configuration with a single command:
```bash
ce mcp configure
```

### Recommended `AGENTS.md` / `CLAUDE.md` Snippet

```markdown
## CodeExplorer (`ce`) MCP Guidelines
- Start architectural exploration with `get_architecture_map` and `get_project_dependencies` instead of broad file searches.
- Use `find_symbol` and `get_file_outline` to locate declarations and line ranges before reading large files.
- Run `analyze_code_impact` or `get_call_chain` before changing shared interfaces or methods.
- Run `inspect_data_lineage` before modifying database tables or ORM entities.
- Use `execute_custom_read_cypher` for multi-hop structural queries.
```

---

## Semantic Invariant Gates

Linters validate syntax and style within single files. Semantic Invariant Gates (SIG) validate structural and architectural rules across the entire repository by executing OpenCypher queries against the graph.

### How Gates Work

Every gate follows a **Zero-Match Contract**:
* **0 rows returned**: Gate passes.
* **1 or more rows returned**: Each returned row is a violation with `file:line:column` coordinates, offending `symbol`, `target`, and remediation message.

```text
FAIL: SIG-P-B-001 [Clean Architecture Layer Isolation]
  src/Core/Services/OrderService.cs:42:15: OrderService -> SqliteConnection
    Domain Core module directly references Infrastructure driver.
```

### Defining a Gate (`.codeexplorer/gates/*.yaml`)

```yaml
id: SIG-P-B-001
name: Clean Architecture Layer Isolation
category: Architectural
platform: agnostic
scope: Module
dimension: Boundaries
severity: Error
description: Domain Core projects must not reference Infrastructure or UI projects.
remediation: Invert the dependency by introducing an interface in Core and implementing it in Infrastructure.
query: |
  MATCH (source:Project)-[:DEPENDS_ON]->(target:Project)
  WHERE source.name CONTAINS "Core" AND target.name CONTAINS "Infrastructure"
  RETURN
    coalesce(source.path, "workspace") AS file,
    1 AS line,
    1 AS column,
    source.name AS symbol,
    target.name AS target,
    "Core project '" + source.name + "' cannot depend on '" + target.name + "'" AS message
```

### Ratchet Baselines for Existing Codebases

To introduce strict rules into an existing repository without fixing every legacy violation upfront, gates support **ratchet baselines** (`.codeexplorer/baselines/*.json`):
1. **Baseline Existing Debt**: `ce gate ratchet --update` records SHA-256 fingerprints of current violations as tolerated baseline debt.
2. **Block Regressions**: `ce gate check` passes if only baselined violations exist, and fails immediately if any new violation is introduced.
3. **Ratchet Down**: When legacy violations are fixed, running `ce gate ratchet --update` tightens the baseline so debt cannot be reintroduced.

### CI Usage

```yaml
- name: Run Architectural Gates
  run: |
    ce scan
    ce gate check --format markdown >> $GITHUB_STEP_SUMMARY
```

---

## VS Code Extension

The VS Code extension provides an interactive workspace for exploring the graph, inspecting dependencies, and managing architectural gates.

<p align="center">
  <img src="https://raw.githubusercontent.com/vmikhailov/code-explorer/main/docs/selected_services.png" alt="Focused Sub-Graph and Inspector" width="100%" />
</p>
<p align="center">
  <em>Focused service neighborhood with transitive broker links and the unified right-hand inspector panel.</em>
</p>

### Capabilities

* **Multiple Views**:
  * **Domain Microservice Map**: Interactive graph with Concentric Orbits, Swimlanes, Domain Islands, Hive Plot, Grid, and Dependency Matrix modes.
  * **Architecture Tiers**: Layered view across Presentation, Application, Domain, and Infrastructure tiers.
  * **Project Flow**: Upstream/downstream dependency columns for a selected project, showing endpoints, database tables, and message topics.
  * **Bounded Context Map**: Visualizes domain groupings, capabilities, and domain entities.
* **Transitive Broker Contraction**: When message broker nodes (Kafka, RabbitMQ, SQS) are hidden, the graph automatically synthesizes dashed producer-to-consumer edges (`PUBLISHES_TO (via Topic)`) so asynchronous flows remain visible.
* **Unified Right-Hand Dock Panel**:
  * **Info**: Displays inbound/outbound calls, database dependencies, topics, and source links for the selected node or edge.
  * **Orbits**: Reorders concentric tiers and toggles tier visibility.
  * **Hidden**: Lists filtered nodes with search and one-click restore.
  * **Gates**: Evaluates Semantic Invariant Gates, displays new vs. tolerated violations, ratchets baselines, and jumps directly to offending lines of code.
* **Sidebar TreeView**: Browse Architecture Diagrams, Ontology Layers (Layers 1–5), Domains & Bounded Contexts, Semantic Invariant Gates, and Server/Graph status from the VS Code activity bar.

<p align="center">
  <img src="https://raw.githubusercontent.com/vmikhailov/code-explorer/main/docs/project_flow.png" alt="Project Flow View" width="100%" />
</p>
<p align="center">
  <em>Project Flow view showing service cards, inbound/outbound dependencies, database links, and source navigation.</em>
</p>

---

## Supported Languages & Frameworks

CodeExplorer combines Tree-sitter grammars, Microsoft SQL ScriptDom, and framework-specific semantic analyzers (`ISemanticExtension`):

| Language | Files & Manifests | Extracted Framework Semantics |
| :--- | :--- | :--- |
| **C#** | `.cs`, `.csproj`, `.sln`, `.slnx` | ASP.NET Core Controllers & Minimal APIs, gRPC services/clients, HttpClient, EF Core (`DbContext`, entities, migrations), Dapper, MassTransit, MediatR |
| **TypeScript / JS** | `.ts`, `.tsx`, `.js`, `.jsx`, `package.json` | NestJS, Express, Next.js, React, Vue, Angular, Svelte, Axios/fetch/undici clients, Prisma/TypeORM, AWS SDK (SQS, S3, DynamoDB), Azure SDK, Stripe, Jest/Vitest/Playwright/Cypress |
| **Java** | `.java`, `pom.xml`, `build.gradle` | Spring Boot REST (`@RestController`, `@RequestMapping`), Spring Kafka (`KafkaTemplate`, `@KafkaListener`), Spring AMQP / RabbitMQ, JPA / Hibernate |
| **Kotlin & Android** | `.kt`, `.kts`, `build.gradle.kts`, `AndroidManifest.xml` | Android Activities/Services/BroadcastReceivers/ContentProviders, Intents, Retrofit, OkHttp, Ktor, Room DAO/Entities, gRPC/Protobuf, Hilt/Dagger/Koin DI, Jetpack Compose, Navigation |
| **Go** | `.go`, `go.mod` | Gin, Fiber, Echo, `net/http` handlers & clients, Resty, gRPC, `pgx` / `database/sql` |
| **Python** | `.py`, `pyproject.toml`, `requirements.txt` | FastAPI, Flask, Django routes & ORM models, `requests`, `httpx`, `aiohttp`, Celery / Kafka consumers |
| **SQL** | `.sql` & embedded SQL strings | T-SQL, PostgreSQL, MySQL, SQLite, ClickHouse, BigQuery DDL & CRUD lineage (`READS_FROM`, `WRITES_TO`) |
| **ColdFusion** | `.cfc`, `.cfm` | Components, functions, `cfquery` SQL extraction, HTTP calls |

---

## Graph Ontology

The graph schema is organized into five decoupled layers (see [`docs/ontology.md`](https://github.com/vmikhailov/code-explorer/blob/main/docs/ontology.md) for the full node and relationship specification):

1. **Layer 1 — Physical**: `Workspace`, `Folder`, `File`, `ConfigurationFile`, `Document`, `Script`.
2. **Layer 2 — Project**: `Project`, `Service`, `App`, `Worker`, `Library`, `Package`, `ExternalDependencies`.
3. **Layer 3 — Syntactic**: `Namespace`, `Type` (class, interface, struct, record, enum), `Function` (method, constructor), `Variable`, `Field`, `CallLiteral`.
4. **Layer 4 — Semantic**: `Endpoint` (REST, gRPC, GraphQL, WebSocket), `ExternalService`, `Database`, `Table`, `Query`, `Topic`, `Queue`.
5. **Layer 5 — Cross-Project & Analytical**: Late-bound cross-project `CALLS`, `IMPLEMENTS`, `USES_TYPE`, `PUBLISHES_TO`, `SUBSCRIBES_TO`, `READS_FROM`, `WRITES_TO`, and optional LLM-distilled `Domain` / `BoundedContext` classifications.

---

## CLI Reference

| Command | Description |
| :--- | :--- |
| `ce init [name]` | Initialize `.codeexplorer/` workspace directory and SQLite schema. |
| `ce scan [path]` | Parse source files, build AST and semantic layers, and resolve cross-project edges (`--clear` for full rebuild). |
| `ce status` | Show workspace statistics, database size, project counts by language, and node/edge totals (`--json` for JSON). |
| `ce query <cypher>` | Run an ad-hoc OpenCypher query (`-n <name>` for saved queries, `-j` for JSON output). |
| `ce queries` | List built-in and workspace-saved OpenCypher queries. |
| `ce gate check` | Evaluate Semantic Invariant Gates (`-g <id>` for a single gate, `-f markdown|json|console`). |
| `ce gate ratchet --update` | Create or tighten violation baseline files in `.codeexplorer/baselines/`. |
| `ce gate init` | Generate a starter gate template in `.codeexplorer/gates/`. |
| `ce export` | Export architecture, C4, data lineage, or event pipeline diagrams (`--format mermaid|c4`, `--type lineage|cqrs`). |
| `ce mcp` | Start the local MCP server over `stdio` JSON-RPC (or run `ce mcp configure` to set up IDE agents). |
| `ce intent` | Classify files into business domains and bounded contexts (supports local GGUF via `llama.cpp` or OpenAI-compatible APIs). |
| `ce model [status\|download]` | Check or download the optional local GGUF model for offline intent classification. |
| `ce clear [path]` | Clear indexed graph data for a path or reset the workspace graph (`-y`). |

---

## MCP Tools

| Tool | Description |
| :--- | :--- |
| `get_architecture_map` | High-level workspace architecture: projects, services, databases, ingress endpoints, and egress calls. |
| `get_project_dependencies` | Direct and transitive project/package dependencies (`projectFilter` optional). |
| `get_project_entry_points` | HTTP endpoints, gRPC methods, GraphQL resolvers, CLI commands, and message consumers for a project. |
| `find_symbol` | Locate types, interfaces, methods, or functions by name pattern. |
| `get_file_outline` | Return classes, methods, fields, and line ranges for a file without reading the full file. |
| `get_call_chain` | Trace invocation paths between two functions up to `maxDepth`. |
| `resolve_call_target` | Resolve concrete class implementations of an interface or abstract method. |
| `analyze_code_impact` | Compute downstream callers and files affected by changing a symbol. |
| `inspect_data_lineage` | Find all SQL queries, ORM entities, methods, and services reading from or writing to a table. |
| `find_refactoring_opportunities` | Identify unreferenced symbols, dead code, or high-coupling types. |
| `execute_custom_read_cypher` | Execute a read-only OpenCypher (`MATCH`) query against the graph. |
| `list_project_queries` | List saved parameterized queries in `.codeexplorer/queries/`. |
| `save_project_query` | Validate and save a reusable OpenCypher query to `.codeexplorer/queries/`. |
| `execute_project_query` | Run a saved or built-in query by name with parameter binding. |
| `export_architecture_diagram` | Export Mermaid or C4 diagrams for architecture, data lineage, or CQRS flows. |
| `fetch_code_snippets` | Read source snippets by file and line range from graph node results. |
| `get_taxonomy` | Return active node labels, relationship types, and counts in the workspace graph. |
| `get_node_definition` | Return schema documentation for a specific ontology node kind. |
| `init_workspace` / `scan_workspace` | Initialize or re-index the workspace graph from an MCP client. |
| `get_workspace_status` | Return index health, schema version, and node/edge counts. |

---

## Repository Layout

```text
├── cli/
│   ├── src/
│   │   ├── Core/CodeExplorer.Core/        # Graph storage (SQLite), 5-layer indexer, SIG engine, MCP tools
│   │   ├── Cypher/CodeExplorer.Cypher/    # OpenCypher parser, AST, and SQLite SQL compiler
│   │   ├── Parsers/                       # Language parsers (C#, TS/JS, Java, Kotlin, Android, Go, Python, SQL, CF)
│   │   ├── Tools/CodeExplorer.OntologyGen/# Generates docs/ontology.md from C# node/edge attributes
│   │   └── UI/CodeExplorer/               # 'ce' CLI, WebSocket/HTTP server, and MCP host
│   ├── tests/
│   │   ├── CodeExplorer.Cypher.Tests/     # OpenCypher parser & SQL compiler test suite
│   │   └── CodeExplorer.Tests/            # Indexing, cross-service, gate, and MCP integration tests
│   └── CodeExplorer.slnx
├── vscode-extension/                      # VS Code extension (TreeView, Webview UI, bundled binary manager)
├── proto/                                 # Shared WebSocket & REST protocol contracts
└── docs/                                  # Auto-generated ontology reference and diagrams
```

---

## License

[MIT License](LICENSE)
