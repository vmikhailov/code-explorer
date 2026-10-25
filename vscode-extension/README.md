# CodeExplorer — Make AI Coding Agents Production-Safe

**AI agents see files, not architecture. CodeExplorer gives your agents (`Claude Code`, `Cursor`, `Windsurf`, `GitHub Copilot`, `Roo Code`) and engineers a live cross-service map of every API, queue, and database in VS Code & MCP—plus hard architectural guardrails in CI.**

**✓ Live Architecture Map in VS Code** • **✓ Surgical MCP Context (7.5× Faster, 40× Fewer Tokens)** • **✓ CI Architecture Gates** • **✓ 100% Local SQLite Graph**

CodeExplorer indexes polyglot repositories, microservices, and monorepos into a single local SQLite graph database (`.codeexplorer/graph.db`) powered by an **OpenCypher 9 query compiler**, **Semantic Invariant Gates (SIG)** with ratchet baselines, a **23-tool Model Context Protocol (MCP) server**, and an **interactive VS Code architecture cockpit**.

It runs **100% locally** via a bundled native engine (`ce`) with zero cloud dependencies, zero containers, and zero telemetry.

<p align="center">
  <img src="https://raw.githubusercontent.com/vmikhailov/code-explorer/main/docs/all_services.png" alt="CodeExplorer Macro Service Topology & Concentric Orbits" width="100%" />
</p>
<p align="center">
  <em>Macro service topology projected across concentric architectural tiers with transitive message-broker routing.</em>
</p>

---

## Why CodeExplorer? (Plain AI + `ripgrep` vs. CodeExplorer Graph)

As codebases grow past ~150k LOC, AI coding agents hit a context saturation wall: they run dozens of blind `grep` searches, burn millions of tokens, hallucinate boundaries, and cut architectural corners in 1,000-line PRs.

CodeExplorer replaces blind text search with a **deterministic 34-node architectural knowledge graph** and **executable architectural gates**.

### Enterprise Benchmark (324 Microservices, ~14,000 Files)

| Metric | Plain AI Agent (`ripgrep` + file reads) | AI Agent + **CodeExplorer MCP** | Improvement |
| :--- | :--- | :--- | :--- |
| **Analysis Time** | 30 min | **< 4 min** | **7.6× faster** |
| **Token Consumption** | 17.2M tokens | **393K tokens** | **44× fewer tokens** |
| **Agent Effort** | 142 shell commands, 114 file reads, 14 scripts | **18 graph queries, 0 file reads** | **Deterministic** |
| **Blast-Radius Recall** | 82% *(missed 9 services behind env vars)* | **100% recall** *(all services & DB tables found)* | **Zero missed services** |

---

## 1. Model Context Protocol (MCP) Server for AI Agents

When you run **`CodeExplorer: Initialize & Scan Workspace`**, the extension automatically indexes your repository and provisions agent configurations (`.mcp.json`, `CLAUDE.md`, `.cursor/mcp.json`, `.github/copilot-instructions.md`) so your AI coding assistant immediately gains architectural vision:

| MCP Tool | What It Gives Your AI Agent |
| :--- | :--- |
| `get_architecture_map` | Instant helicopter view of all services, projects, databases, message queues, and ingress/egress endpoints in `<1,000` tokens. |
| `analyze_code_impact` | Multi-hop downstream blast radius before refactoring an interface, class, endpoint, or contract. |
| `inspect_data_lineage` | Traces every service, ORM entity (EF Core, Dapper, Prisma, JPA, SQLAlchemy), and raw SQL query reading or writing a database table. |
| `get_call_chain` | End-to-end call graph across methods, controllers, gRPC handlers, and background consumers. |
| `get_project_dependencies` | Inbound and outbound project references, NuGet/npm/Maven/Go packages, and layer dependencies. |
| `execute_cypher_query` | Executes arbitrary read-only **OpenCypher 9** queries (`MATCH (s:Service)-[:WRITES_TO]->(t:SqlTable) RETURN s, t`) directly over SQLite. |
| `save_project_query` | Saves reusable, parameterized domain queries in `.codeexplorer/` for project-specific routing or audit patterns. |

---

## 2. Semantic Invariant Gates (SIG) & Ratchet Baselines

Human reviewers cannot manually verify architectural purity across 1,000-line AI-generated Pull Requests. **Semantic Invariant Gates (SIG)** enforce executable architectural rules written in OpenCypher over the codebase graph — both live inside the VS Code **Gates** dock and in CI/CD (`ce gate check`).

* **35 Built-In Enterprise Gates** across 5 dimensions:
  * **Boundaries (`SIG-P-B-*`)**: Clean Architecture layer isolation, cyclic dependency detection, cross-domain database access prevention.
  * **Integrity & Security (`SIG-M-I-*`, `SIG-S-*`)**: Hardcoded credentials/endpoints, unauthenticated routes, SQL injection sinks, missing resilience policies.
  * **Data Lineage (`SIG-D-*`)**: Orphaned tables, shared-database coupling across microservices, dual-write anti-patterns.
  * **Runtime Parity (`SIG-R-*`)**: Dead endpoints, unconsumed message broker topics, DI registration mismatches.
* **Ratchet Baselines (Adopt on Legacy Codebases in 10 Seconds)**:
  * Baseline existing technical debt with one click in the **Gates** panel (`Update Baseline`).
  * CodeExplorer tracks `baselined_violations` vs. `new_violations`: legacy debt won't block your build, **any new violation introduced by an AI agent or developer immediately fails the gate**, and fixing old violations automatically tightens the ratchet.

```yaml
id: SIG-CUSTOM-001
name: No Direct Controller-to-Database Access
severity: error
description: Controllers must not bypass Application/Domain services to query SQL tables directly.
query: |
  MATCH (c:Class)-[:DECLARES]->(m:Method)-[:CALLS*1..2]->(q)-[:READS_FROM|WRITES_TO]->(t:SqlTable)
  WHERE c.name ENDS WITH 'Controller'
  RETURN c.name AS controller, m.name AS method, t.name AS table, c.file_path AS file
```

<p align="center">
  <img src="https://raw.githubusercontent.com/vmikhailov/code-explorer/main/docs/selected_services.png" alt="Focused Sub-Graph Neighborhood & Custom Orbits" width="100%" />
</p>
<p align="center">
  <em>Focused sub-graph inspection isolating a service neighborhood, custom orbits, and transitive dependencies.</em>
</p>

---

## 3. Interactive Architecture Cockpit in VS Code

* **Multiple Topology Projections**:
  * **Domain Microservice Map**: 2D interactive graph with Concentric Orbits, Swimlanes, Domain Islands, Hive Plot, Grid, and Dependency Matrix layouts.
  * **Architecture Tiers**: Automatic Clean/Hexagonal layering into Presentation, Application, Domain, and Infrastructure tiers.
  * **Project Flow & C1 Context**: Focused upstream/downstream dependency columns for any selected service or library, surfacing HTTP/gRPC endpoints, databases, external APIs, and message topics.
  * **Bounded Context Map**: Visualizes domain boundaries, capabilities, and domain entities.
* **Unified Right-Hand Dock Panel**:
  * **Info**: Inspects inbound/outbound calls, SQL table lineage, message topics, and exact file/line references.
  * **Orbits**: Drag-and-drop concentric ring ordering and tier visibility toggles.
  * **Hidden**: Searchable list of filtered nodes with one-click restore.
  * **Gates**: Live evaluation of Semantic Invariant Gates, new vs. baselined violation diffs, one-click ratchet baseline updates, and click-to-source navigation.
* **Transitive Broker Contraction**: When intermediate Kafka/RabbitMQ/SQS broker nodes are hidden, CodeExplorer synthesizes dashed producer-to-consumer edges so async event flows stay visible.
* **OpenCypher Query Bar**: Filter and highlight live subgraphs using OpenCypher queries directly from the cockpit toolbar.

<p align="center">
  <img src="https://raw.githubusercontent.com/vmikhailov/code-explorer/main/docs/project_flow.png" alt="Project Flow, Service Cards & C1 Context" width="100%" />
</p>
<p align="center">
  <em>Project Flow view showing upstream callers, exposed endpoints, downstream services, and database/broker lineage.</em>
</p>

---

## Getting Started

1. Open any workspace folder in VS Code, Cursor, Windsurf, or VSCodium.
2. Open the **CodeExplorer** view from the Activity Bar or click **`Code Graph`** in the status bar.
3. Click **`Initialize & Scan Workspace`** — CodeExplorer bundles the native `ce` binary for Windows (`x64`/`arm64`), macOS (`arm64`/`x64`), and Linux (`x64`/`arm64`), builds `.codeexplorer/graph.db`, and configures MCP for your AI agents.
4. Explore interactive diagrams under **Architecture Diagrams** or enforce architectural rules under **Semantic Invariant Gates**.

---

## Polyglot Language & Framework Coverage

* **C# / .NET** (`.cs`, `.csproj`, `.sln`, `.slnx`): Roslyn AST, ASP.NET Core, Minimal APIs, gRPC, EF Core, Dapper, MassTransit, MediatR, Azure Functions
* **TypeScript / JavaScript** (`.ts`, `.tsx`, `.js`, `.jsx`): NestJS, Express, Next.js, React, Vue, Angular, Svelte, Prisma, TypeORM, Drizzle, AWS/Azure SDKs
* **Java** (`.java`, `pom.xml`, `build.gradle`): Spring Boot, Spring Cloud, Feign, Kafka, RabbitMQ, Hibernate/JPA, JDBC
* **Kotlin & Android** (`.kt`, `.kts`, `AndroidManifest.xml`): Spring Boot, Ktor, Android Components & Intents, Retrofit, Room, gRPC, Hilt/Koin, Jetpack Compose
* **Python** (`.py`, `pyproject.toml`, `requirements.txt`): FastAPI, Flask, Django, SQLAlchemy, Celery, `requests`, `httpx`, `aiohttp`
* **Go** (`.go`, `go.mod`): Gin, Fiber, Echo, Chi, `net/http`, gRPC, `database/sql`, `pgx`, GORM
* **SQL & Data Lineage** (`.sql` & embedded SQL strings): T-SQL, PostgreSQL, MySQL, SQLite, Oracle, ClickHouse, BigQuery
* **ColdFusion / CFML** (`.cfc`, `.cfm`): Components, `cfquery` SQL lineage, `cfhttp`, wirebox DI

---

## Extension Settings

| Setting | Default | Description |
| :--- | :--- | :--- |
| `codeExplorer.executablePath` | `""` | Custom path to the `ce` binary. Leave empty to use the bundled or globally installed binary. |
| `codeExplorer.serverPort` | `0` | Port for the local CodeExplorer server (`0` assigns an ephemeral port automatically). |
| `codeExplorer.idleTimeout` | `60` | Seconds of inactivity before the background server process stops when no panels are open. |

---

## Links & Documentation

* **Website & Interactive Playground**: [https://code-explorer.mikhailov.cc](https://code-explorer.mikhailov.cc)
* **GitHub Repository & Benchmarks**: [https://github.com/vmikhailov/code-explorer](https://github.com/vmikhailov/code-explorer)
* **CLI NuGet Package**: [`CodeExplorer.Cli` on NuGet.org](https://www.nuget.org/packages/CodeExplorer.Cli)
* **License**: MIT

