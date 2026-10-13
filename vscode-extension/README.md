# CodeExplorer for VS Code

CodeExplorer indexes polyglot repositories, microservices, and monorepos into a local SQLite graph database (`graph.db`) and renders interactive architecture diagrams inside Visual Studio Code.

It runs entirely locally with a bundled single-file engine (`ce`) and requires no external services or containers.

<p align="center">
  <img src="https://raw.githubusercontent.com/vmikhailov/code-explorer/main/docs/all_services.png" alt="CodeExplorer Service Topology" width="100%" />
</p>
<p align="center">
  <em>Service topology projected across concentric architectural tiers with transitive dependency routing.</em>
</p>

---

## Features

* **Interactive Architecture Diagrams**:
  * **Domain Microservice Map**: 2D graph with Concentric Orbits, Swimlanes, Domain Islands, Hive Plot, Grid, and Dependency Matrix modes.
  * **Architecture Tiers**: Layered view grouping projects into Presentation, Application, Domain, and Infrastructure tiers.
  * **Project Flow**: Focused upstream/downstream dependency columns for any service or library, displaying HTTP endpoints, databases, and message topics.
  * **Bounded Context Map**: Visualizes domain boundaries, capabilities, and domain entities.
* **Unified Right-Hand Dock Panel**:
  * **Info**: Inspects inbound/outbound calls, database dependencies, message topics, and source locations for the selected node or edge.
  * **Orbits**: Reorders concentric tiers via drag-and-drop and toggles tier visibility.
  * **Hidden**: Lists filtered nodes with search and one-click restore.
  * **Gates**: Evaluates Semantic Invariant Gates (`.codeexplorer/gates/*.yaml`), displays new vs. baselined violations, updates ratchet baselines, and jumps to offending lines of code.
* **Transitive Broker Contraction**: When intermediate message broker nodes (Kafka, RabbitMQ, SQS) are hidden, CodeExplorer automatically synthesizes dashed producer-to-consumer links so asynchronous flows remain clear.
* **Click-to-Source Navigation**: Double-click any node or click a file reference in the inspector or gate violation list to open the exact file and line in the editor.
* **OpenCypher Query Bar**: Run read-only OpenCypher queries directly from the toolbar (e.g. `MATCH (n:Service)-[r]->(m) RETURN n,r,m`).
* **Bundled Native Engine**: Automatically manages the native `ce` binary for Windows (`x64`, `arm64`), macOS (`arm64`, `x64`), and Linux (`x64`, `arm64`).

<p align="center">
  <img src="https://raw.githubusercontent.com/vmikhailov/code-explorer/main/docs/selected_services.png" alt="Focused Sub-Graph Inspection" width="100%" />
</p>

---

## Getting Started

1. Open a workspace folder in VS Code.
2. Open the **CodeExplorer** view from the Activity Bar or click **`Code Graph`** in the status bar.
3. Click **Initialize & Scan Workspace** to parse your repository and build `.codeexplorer/graph.db`.
4. Select any diagram under **Architecture Diagrams**, inspect services in the graph, or evaluate rules under **Semantic Invariant Gates**.

---

## Supported Languages

* **C#** (`.cs`, `.csproj`, `.sln`, `.slnx`): ASP.NET Core, gRPC, EF Core, Dapper, MassTransit, MediatR
* **TypeScript / JavaScript** (`.ts`, `.tsx`, `.js`, `.jsx`): NestJS, Express, Next.js, React, Vue, Angular, Svelte, Prisma/TypeORM, AWS/Azure SDKs
* **Java** (`.java`, `pom.xml`, `build.gradle`): Spring Boot, Spring Kafka, RabbitMQ, JPA
* **Kotlin & Android** (`.kt`, `.kts`, `AndroidManifest.xml`): Android components & Intents, Retrofit, Room, Ktor, gRPC, Hilt/Koin, Jetpack Compose
* **Go** (`.go`, `go.mod`): Gin, Fiber, Echo, `net/http`, gRPC, `pgx`
* **Python** (`.py`): FastAPI, Flask, Django, `requests`, `httpx`, `aiohttp`
* **SQL** (`.sql` & embedded SQL): T-SQL, PostgreSQL, MySQL, SQLite, ClickHouse, BigQuery
* **ColdFusion** (`.cfc`, `.cfm`)

---

## Extension Settings

| Setting | Default | Description |
| :--- | :--- | :--- |
| `codeExplorer.executablePath` | `""` | Custom path to the `ce` binary. Leave empty to use the bundled or globally installed binary. |
| `codeExplorer.serverPort` | `0` | Port for the local CodeExplorer server (`0` assigns an ephemeral port automatically). |
| `codeExplorer.idleTimeout` | `60` | Seconds of inactivity before the background server process stops when no panels are open. |
