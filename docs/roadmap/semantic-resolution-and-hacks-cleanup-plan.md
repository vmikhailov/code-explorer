# CodeExplorer: Universal Semantic Value Resolution & Zero-Hack Cleanup Plan 🎯

> **Status:** Implemented & Verified in v1.11.10 ✅  
> **Target Release:** v1.11.10  
> **Reference Document:** [semantic_analysis_and_hacks_audit.md](file:///C:/Users/viach/.gemini/antigravity-ide/brain/c2895c5c-0d94-4e3f-9ccb-9c30cbaa962f/semantic_analysis_and_hacks_audit.md)  
> **Tracking Epic:** [docs/roadmap/todo.md](./todo.md) (EPIC 0)

---

## 1. Executive Summary

A deep audit of the CodeExplorer (`ce`) codebase revealed recurring semantic recognition failures for:
1. **Message Topics and Queues** (Kafka, RabbitMQ, GCP Pub/Sub, MassTransit, KafkaFlow)
2. **Database Schemas & Tables** (EF Core, Dapper, TypeORM, Raw SQL, BigQuery)
3. **External Service URLs & Names** (HttpClient, Axios, Angular Http, Fetch, gRPC, Go/Python clients)

### Root Cause
CodeExplorer currently lacks a **unified constant propagation and data-flow evaluation engine**:
* TreeSitter generates ASTs without symbol resolution or expression evaluation.
* `ConfigurationParser.cs` operates late in **Layer 4** (after AST parsing in Layer 3 is already finished) and never populates `ConstantRegistry`.
* `ConstantRegistry` relies on naive regexes and scans only files matching magic filename keywords (`const`, `route`, `config`).
* Expressions (`A + B`, `$"{Prefix}.orders"`, ternary operators, enums) fail to evaluate, leading to discarded database tables (`NestedSqlParser.cs`), fabricated fallback routes (`HttpClientLibraryParser.cs`), and ad-hoc project-specific wrapper methods (`paPartnerQueue`, `createQueue`, `getPubSubTopic`).

---

## 2. Target Architectur```
                       TARGET SEMANTIC VALUE RESOLUTION & UAST PIPELINE
                       
 ┌────────────────────────────────────────────────────────────────────────┐
 │ PHASE 0: Unified ConfigStore & Library Descriptors (Layer 2 / Early L3)│
 │ • Hierarchical parser for appsettings*.json, .env*, application*.yml   │
 │ • Flat Key-Path indexing: "Kafka:Topics:Orders" = "orders-v1"          │
 │ • ILibraryConfigurationDescriptor: pluggable framework keys (Spring,  │
 │   ASP.NET, NestJS) cleanly isolated in language parser packages        │
 └───────────────────────────────────┬────────────────────────────────────┘
                                     │
 ┌───────────────────────────────────▼────────────────────────────────────┐
 │ PHASE 1: Universal AST (UAST) & Two-Pass Layer 3                       │
 │ • Universal LanguageSyntaxProfile: maps each language CST into a common│
 │   syntax coordinate system (literals, interpolations, bin-ops, accesses)│
 │ • Pass 1 (Declarations Discovery): all ASTs parsed in parallel, types, │
 │   constants & enums registered into ConstantRegistry                   │
 │ • Topological partial evaluation: resolves dependent constants         │
 │ • Full elimination of regex pre-scans and File.ReadAllText heuristics  │
 └───────────────────────────────────┬────────────────────────────────────┘
                                     │
 ┌───────────────────────────────────▼────────────────────────────────────┐
 │ PHASE 2: Universal AstValueResolver (Pass 2: Invocations & References) │
 │ • Evaluates expressions via UAST coordinate system without language    │
 │   switches or file extension checks                                    │
 │ • Resolves configs: config["Key"], process.env.X -> ConfigStore        │
 │ • Local Data-Flow: Reaching Definitions for local variables in scope   │
 └───────────────────────────────────┬────────────────────────────────────┘
                                     │
 ┌───────────────────────────────────▼────────────────────────────────────┐
 │ PHASE 3: Clean Library Parsers & Zero-Hack Contracts                   │
 │ • Clean SDK contracts: Topic(arg0), ToTable(arg0, arg1)                │
 │ • Delegates expression evaluation to AstValueResolver                  │
 │ • Complete removal of ad-hoc project wrapper hacks & fake URLs         │
 └───────────────────────────────────┬────────────────────────────────────┘
                                     │
 ┌───────────────────────────────────▼────────────────────────────────────┐
 │ PHASE 4: Decouple Domain Classification from Core Engine               │
 │ • Remove ad-tech hardcoding from ArchitectureViewEngine.cs             │
 │ • Graph connectivity clustering / SLM Intent / .codeexplorer/domains   │
 └───────────────────────────────────┬────────────────────────────────────┘
                                     │
 ┌───────────────────────────────────▼────────────────────────────────────┐
 │ PHASE 5: Evidence-Based Entity Classification & Dynamic Role Engine    │
 │ • Two-Tier Taxonomy: ProjectEntityKind (Manifest/Topology) vs          │
 │   ProjectRole (Ingress, Worker, Aggregator, CoreDomain, Adapter)       │
 │ • Multi-layer evidence accumulation (Manifest, AST, Config, Graph-Flow)│
 │ • Elimination of name/path string heuristics across the engine         │
 └────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Detailed Phase Breakdown

### Phase 0: Unified Workspace ConfigStore
* [x] **0.1 Relocate Configuration Parsing**: Move config file discovery (`appsettings*.json`, `.env*`, `application*.yml/properties`, `docker-compose.yml`) to Layer 2 / Layer 3 before AST file scanning begins.
* [x] **0.2 Hierarchical Key-Path Normalization**: Index configuration keys into standard formats:
  - C# format: `ConnectionStrings:DefaultConnection`, `Kafka:Topics:Orders`, `Services:Billing:Url`
  - Environment format: `CONNECTIONSTRINGS__DEFAULTCONNECTION`, `KAFKA_TOPICS_ORDERS`, `SERVICES_BILLING_URL`
* [x] **0.3 Generic URL & Service Target Discovery**:
  - Automatically detect service-to-service URLs across any section (`UrlsSettings`, `Services`, `Endpoints`, `Clients`, `Apis`, `Gateways`) by inspecting value patterns (`http://...`, `https://...`) instead of hardcoding section names.
* [x] **0.4 Integration with `ConstantRegistry`**: Automatically register all discovered configuration key-value pairs into `ConstantRegistry` scoped to project and workspace.

### Phase 1: Universal AST-Based Constant & Symbol Graph
* [x] **1.1 Remove Magic Filename Filters**: Eliminate the selective name filter in `Layer3SyntacticParser.cs` (`if (file.Name.Contains("const") || ...)`) so all project files contribute symbol declarations in parallel pass 1.
* [x] **1.2 AST Symbol Extraction (Replace Regex)**:
  - **C#**: `FieldDeclaration` (`const`, `static readonly`), `EnumDeclaration`, expression-bodied properties (`=> "..."`).
  - **TypeScript / JavaScript**: `VariableDeclarator` (`const`, `as const`), `EnumDeclaration`, `const enum`, object literals with static properties.
  - **Go**: `ConstSpec`, `VarSpec`.
  - **Python**: Upper-case module assignments, `Enum` subclasses.
* [x] **1.3 Expression Trees & Topological Evaluation**:
  - Store unevaluated expressions when symbols reference other symbols (`Prefix + "orders"`, `$"{Namespace}.Users"`).
  - Perform topological sort and partial evaluation on demand with cycle detection and caching.

### Phase 2: Universal `AstValueResolver`
* [x] **2.1 Core Resolver Engine (`CodeExplorer.Core.Parser.AstValueResolver`)**:
  - Implement language-agnostic evaluator for AST nodes:
    - String and numeric literals
    - Binary concatenations (`+`, `.` in PHP/Perl)
    - Template literals and string interpolations (`$"{A}.{B}"`, `` `${A}.${B}` ``)
    - Member access chains (`Constants.Topics.OrderCreated`)
* [x] **2.2 Configuration Resolution Hooks**:
  - C#: `_configuration["Key"]`, `_configuration.GetValue<string>("Key")`, `_configuration.GetConnectionString("Name")`.
  - TS/JS: `process.env.KEY`, `process.env['KEY']`, `configService.get('KEY')`.
  - Go: `os.Getenv("KEY")`, `viper.GetString("key")`.
  - Python: `os.getenv("KEY")`, `os.environ.get("KEY")`.
* [x] **2.3 Local Scope Data-Flow (Reaching Definitions)**:
  - When an argument is a local variable (e.g. `await producer.ProduceAsync(topicName, msg)`), trace backwards in the enclosing method/block to find the definition of `topicName` and resolve its value.

### Phase 3: Clean Library Parsers & Removal of Hacks
* [x] **3.1 Clean `ConfigurationParser.cs`**:
  - Remove hardcoded `ParseCloudPayments` (`"https://api.cloudpayments.ru"`, `"CloudPayments"`).
  - Decouple framework-specific Spring properties into pluggable `ILibraryConfigurationDescriptor` (`SpringFrameworkConfigurationDescriptor` in `CodeExplorer.Parser.Java`).
  - Replace hardcoded `ParseUrlsSettings` with generic service-to-service URL discovery (from Phase 0.3).
* [x] **3.2 Clean `RabbitMqLibraryParser.cs`**:
  - Remove hardcoded `paPartnerQueue` variable lookup.
  - Remove synthetic `createQueue` method matching.
  - Rely on amqplib / RabbitMQ SDK method signatures with `AstValueResolver`.
* [x] **3.3 Clean `GcpLibraryParser.cs`**:
  - Support canonical `@google-cloud/pubsub` API (`pubsub.topic(name)`, `topic.publishMessage(...)`).
* [x] **3.4 Clean `HttpClientLibraryParser.cs`**:
  - Remove fake route generation fallback: `return $"api/v1/{propName.ToLowerInvariant()}";`.
  - Use `AstValueResolver` to resolve URL paths, query parameters, and base addresses.
* [x] **3.5 Clean `NestedSqlParser.cs`**:
  - Remove `IsVariable` table dropping heuristic. Tables with dynamic interpolations that evaluate via `AstValueResolver` are preserved in the graph.
* [x] **3.6 Clean `GoAstHelper.cs` & `PostIndexAnalyzer.cs`**:
  - Remove ad-hoc selector matching (`r.config.impressionQueue`, `ImpressionTopic`, `TopicID`, `SubID`).

### Phase 4: Decouple Domain Classification from Core Engine
* [x] **4.1 Purge Ad-Tech Ontology in `ArchitectureViewEngine.cs`**:
  - Remove hardcoded terms: `rules_bundling`, `rates_analytics`, `domain_management`, `tracking_postbacks`, `campaign_advertising`, `traffic_routing`, `cpm`, `epm`, `dynadot`, `smartcpa`, `tbmap`, `lander`, `popunder`.
* [x] **4.2 Pluggable Domain Ontologies**:
  - Read domain classifications from `.codeexplorer/domains.json` and `.codeexplorer/conventions.json` if present in workspace.
  - Fall back to community graph clustering (Louvain/Leiden algorithm over materialized runtime macro-edges) or intent classification.

### Phase 5: Evidence-Based Entity Classification & Dynamic Role Engine (Zero-Hack Engine)

#### 5.1 The Two-Tier Architectural Taxonomy
We strictly decouple **what an entity is (Entity Kind)** from **how it functions in the system topology (Architectural Role)**:

1. **Tier 1: Intrinsic Entity Kind (Ontology Node Kind)**
   - What the project/module actually is at the lifecycle and deployment level:
     - `Library`: Non-executable package exporting types, interfaces, schemas, contracts, or utility functions. Has no daemon lifecycle.
     - `Service`: Daemon process exposing incoming network endpoints/APIs (HTTP/REST, gRPC, GraphQL, WebSocket, TCP).
     - `Worker`: Daemon process driven by asynchronous events (Kafka, RabbitMQ, SQS), scheduled timers, or cron triggers. Does not expose public HTTP listener endpoints.
     - `Function`: Serverless event handler execution unit (AWS Lambda, Azure Function, Cloud Function).
     - `App`: Client application running on user devices / user agents:
       - `FrontendApp`: Web SPA/SSR (React, Angular, Vue, Next.js, Svelte).
       - `MobileApp`: iOS, Android, Flutter, React Native, MAUI.
       - `DesktopApp`: Electron, WPF, Avalonia, Qt.
     - `CliTool`: Interactive or batch terminal command-line tool (`OutputType=Exe` with CLI argument parsers like `System.CommandLine`, `yargs`, `cobra`, `argparse`).
     - `MigrationTool`: Schema definition and database migration runner (Flyway, Liquibase, EF Core migrations, Goose).
     - `Resource`: Infrastructure entity (Managed Database, Message Broker, Cache, Cloud Bucket).

2. **Tier 2: Architectural Role (Topological & Behavioral Role)**
   - Assigned dynamically after the semantic graph is constructed (Layer 5 PostIndex), reflecting runtime responsibility:
     - `Ingress` / `Gateway` / `BFF`: Public or edge-facing entry point receiving external client traffic and orchestrating internal downstream services.
     - `CoreDomain`: Autonomous domain service encapsulating bounded context business rules and entities.
     - `IntegrationAdapter`: Outbound integration service bridging internal domain to external 3rd-party SaaS or partner APIs.
     - `AsyncProcessor`: Event consumer or background batch processor.
     - `Persistence` / `Storage`: Databases, schema stores, data pipelines.
     - `Foundation` / `Shared`: Cross-cutting technical infrastructure, shared contracts, common models.

#### 5.2 Multi-Layer Evidence Accumulator (No Project/Path Name Heuristics)
Instead of guessing roles at Layer 2 based on strings like `Contains("/lib/")` or `EndsWith(".tests")` or `HasProtocolTokens()`, the engine accumulates **typed facts across all analysis layers**:

```
                       CROSS-LAYER EVIDENCE PIPELINE
                       
 ┌────────────────────────────────────────────────────────────────────────┐
 │ LAYER 1 & 2: Structural & Manifest Evidence (Packaging & Output)       │
 │ • OutputType: Executable (Exe/bin) vs Packable Library (nuget/npm/gem) │
 │ • Build Targets: Dockerfile, bundle config (Vite/Webpack), lambda spec │
 └───────────────────────────────────┬────────────────────────────────────┘
                                     │
 ┌───────────────────────────────────▼────────────────────────────────────┐
 │ LAYER 3: Syntactic & AST Evidence (Code Signatures & Entry Points)     │
 │ • Listeners: Listen(), app.Run(), http.ListenAndServe(), createServer()│
 │ • Routing Contracts: [ApiController], @Controller, gRPC stubs, GraphQL │
 │ • Consumer Loops: IConsumer<T>, @EventPattern, amqp.consume, Celery    │
 │ • CLI Parsers: System.CommandLine, yargs, commander, cobra, clap       │
 │ • UI Bootstrap: ReactDOM.render, @Component, createApp, FlutterApp     │
 │ • Test Frameworks: [Test], [Fact], describe(), it(), pytest runner     │
 └───────────────────────────────────┬────────────────────────────────────┘
                                     │
 ┌───────────────────────────────────▼────────────────────────────────────┐
 │ LAYER 4: Configuration & Infrastructure Evidence                       │
 │ • Network Bindings: EXPOSE, server.port, PORT, ConnectionStrings       │
 │ • Infrastructure Roles: docker-compose service definitions, k8s charts │
 └───────────────────────────────────┬────────────────────────────────────┘
                                     │
 ┌───────────────────────────────────▼────────────────────────────────────┐
 │ LAYER 5: Topological / Graph Flow Evidence (Runtime Relationships)     │
 │ • In-Degree / Out-Degree of SERVICE_CALL, PUBLISHES_TO, SUBSCRIBES_TO  │
 │ • Upstream Caller Context (Called by user / called by gateway / edge)  │
 └────────────────────────────────────────────────────────────────────────┘
```

#### 5.3 Deterministic Evidence Scoring Matrix
* [x] **5.1 Test Detection**:
  - Facts: Imports testing framework SDK (`xunit`, `nunit`, `jest`, `pytest`, `testing`) **AND** test project conventions. Handled via `TestEvidenceClassifier` in `ProjectEntityClassifierRegistry`.
* [x] **5.2 Service vs. Library Distinction**:
  - `ProjectEntityKind.Library` vs `ProjectEntityKind.Service`: OutputType / Manifest / Sdk / Package exports without heuristics. Handled via `LibraryEvidenceClassifier` and `ManifestEvidenceClassifier`.
* [x] **5.3 Service vs. Worker Distinction**:
  - `ProjectEntityKind.Worker`: Worker SDK / Cloudflare worker runtime / Queue listeners. Handled via `WorkerEvidenceClassifier`.
* [x] **5.4 App & CLI Distinction**:
  - `ProjectEntityKind.FrontendApp`: UI configs (Vite, Next, Nuxt, Angular) and client dependencies. Handled via `FrontendEvidenceClassifier`.
  - `ProjectEntityKind.CliTool`: `has_cli_bin` and CLI entrypoints. Handled via `CliEvidenceClassifier`.
* [x] **5.5 Dynamic Role Assignment in Layer 5**:
  - Derived cleanly in `ProjectLayerClassifier` using `ProjectClassifierItem.EntryPoints`, `EndpointsCount`, `inDegree`, `outDegree`, and `ExternalServicesCount`.
* [x] **5.6 Purge String & Path Heuristics**:
  - Purged `common-nest`, `projects/ui/src/lib/`, `fe/projects/`, `-lib-`, `.lib.`, `ontologygen`, `decision`, `calculator`, `calculation`, `rule-tree`, `configurator`, `sources`, `kv`, `landing`, `-fe` from `ProjectRoleDetector.cs` and `ProjectLayerClassifier.cs`.

---

## 4. Verification & Validation Strategy

1. **Unit & Integration Tests**:
   - `dotnet test cli/tests/CodeExplorer.Tests/CodeExplorer.Tests.csproj`
   - `dotnet test cli/tests/CodeExplorer.Cypher.Tests/CodeExplorer.Cypher.Tests.csproj`
   - Zero compilation warnings with `/warnaserror`.
2. **Semantic Extraction Tests**:
   - Add targeted test cases for:
     - Multi-level constant concatenation: `A + B + C`
     - String interpolations with imported constants
     - Config binding: `appsettings.json` -> `IConfiguration["Topic"]` -> `ProduceAsync(topic)`
     - Local variable reaching definitions in C#, TypeScript, and Go.
3. **Evidence-Based Role Classification Tests**:
   - Verify that projects named arbitrarily (e.g. `Alpha`, `ProjectX`, `Omega`) are unambiguously classified as `Service`, `Worker`, `Library`, or `CliTool` solely by AST listeners, entrypoints, and packaging outputs without name clues.
4. **Public Sample Benchmarks**:
   - Validate against standard public repositories (eShopOnContainers, Jellyfin, ASP.NET Core samples) to ensure zero spurious domain tags, correct role assignments, or phantom URLs.
