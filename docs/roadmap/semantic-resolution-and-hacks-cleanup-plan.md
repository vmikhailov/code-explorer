# CodeExplorer: Universal Semantic Value Resolution & Zero-Hack Cleanup Plan 🎯

> **Status:** Approved / Ready for Implementation  
> **Target Release:** v1.5.0+  
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

## 2. Target Architecture

```
                       TARGET SEMANTIC VALUE RESOLUTION PIPELINE
                       
 ┌────────────────────────────────────────────────────────────────────────┐
 │ PHASE 0: Unified ConfigStore (Runs early, in Layer 2 / Layer 3)        │
 │ • Hierarchical parser for appsettings*.json, .env*, application*.yml   │
 │ • Flat Key-Path indexing: "Kafka:Topics:Orders" = "orders-v1"          │
 │ • Bidirectional casing lookup: ScreamingSnake <-> C# colon hierarchy   │
 └───────────────────────────────────┬────────────────────────────────────┘
                                     │
 ┌───────────────────────────────────▼────────────────────────────────────┐
 │ PHASE 1: Universal Constant & Symbol Graph (AST-driven, no regex)      │
 │ • Full-repo scanning across ALL files without filename filters         │
 │ • Stores Expression Trees (bin-ops, string interpolations, enums)     │
 │ • Topological partial evaluation: resolves dependent constants        │
 └───────────────────────────────────┬────────────────────────────────────┘
                                     │
 ┌───────────────────────────────────▼────────────────────────────────────┐
 │ PHASE 2: AstValueResolver (Universal semantic expression evaluator)   │
 │ • Evaluates AST nodes across languages (C#, TypeScript, Go, Python)   │
 │ • Resolves configs: config["Key"], process.env.X -> ConfigStore       │
 │ • Local Data-Flow: Reaching Definitions for local variables in scope  │
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
 └────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Detailed Phase Breakdown

### Phase 0: Unified Workspace ConfigStore
* [ ] **0.1 Relocate Configuration Parsing**: Move config file discovery (`appsettings*.json`, `.env*`, `application*.yml/properties`, `docker-compose.yml`) to Layer 2 / Layer 3 before AST file scanning begins.
* [ ] **0.2 Hierarchical Key-Path Normalization**: Index configuration keys into standard formats:
  - C# format: `ConnectionStrings:DefaultConnection`, `Kafka:Topics:Orders`, `Services:Billing:Url`
  - Environment format: `CONNECTIONSTRINGS__DEFAULTCONNECTION`, `KAFKA_TOPICS_ORDERS`, `SERVICES_BILLING_URL`
* [ ] **0.3 Integration with `ConstantRegistry`**: Automatically register all discovered configuration key-value pairs into `ConstantRegistry` scoped to project and workspace.

### Phase 1: Universal AST-Based Constant & Symbol Graph
* [ ] **1.1 Remove Magic Filename Filters**: Eliminate the selective name filter in `Layer3SyntacticParser.cs` (`if (file.Name.Contains("const") || ...)`) so all project files contribute symbol declarations.
* [ ] **1.2 AST Symbol Extraction (Replace Regex)**:
  - **C#**: `FieldDeclaration` (`const`, `static readonly`), `EnumDeclaration`, expression-bodied properties (`=> "..."`).
  - **TypeScript / JavaScript**: `VariableDeclarator` (`const`, `as const`), `EnumDeclaration`, `const enum`, object literals with static properties.
  - **Go**: `ConstSpec`, `VarSpec`.
  - **Python**: Upper-case module assignments, `Enum` subclasses.
* [ ] **1.3 Expression Trees & Topological Evaluation**:
  - Store unevaluated expressions when symbols reference other symbols (`Prefix + "orders"`, `$"{Namespace}.Users"`).
  - Perform topological sort and partial evaluation on demand with cycle detection and caching.

### Phase 2: Universal `AstValueResolver`
* [ ] **2.1 Core Resolver Engine (`CodeExplorer.Core.Parser.AstValueResolver`)**:
  - Implement language-agnostic evaluator for AST nodes:
    - String and numeric literals
    - Binary concatenations (`+`, `.` in PHP/Perl)
    - Template literals and string interpolations (`$"{A}.{B}"`, `` `${A}.${B}` ``)
    - Member access chains (`Constants.Topics.OrderCreated`)
* [ ] **2.2 Configuration Resolution Hooks**:
  - C#: `_configuration["Key"]`, `_configuration.GetValue<string>("Key")`, `_configuration.GetConnectionString("Name")`.
  - TS/JS: `process.env.KEY`, `process.env['KEY']`, `configService.get('KEY')`.
  - Go: `os.Getenv("KEY")`, `viper.GetString("key")`.
  - Python: `os.getenv("KEY")`, `os.environ.get("KEY")`.
* [ ] **2.3 Local Scope Data-Flow (Reaching Definitions)**:
  - When an argument is a local variable (e.g. `await producer.ProduceAsync(topicName, msg)`), trace backwards in the enclosing method/block to find the definition of `topicName` and resolve its value.

### Phase 3: Clean Library Parsers & Removal of Hacks
* [ ] **3.1 Clean `RabbitMqLibraryParser.cs`**:
  - Remove hardcoded `paPartnerQueue` variable lookup.
  - Remove synthetic `createQueue` method matching.
  - Rely exclusively on amqplib / RabbitMQ SDK method signatures with `AstValueResolver`.
* [ ] **3.2 Clean `GcpLibraryParser.cs`**:
  - Remove project-specific wrapper methods: `getPubSubTopic`, `createNetworkTopic`, `sendMessageToTopicWithAttributes`, `sendMessageToNetwork`, `publishToTopic`, `sendMessageToTopic`, `subscribeToMessages`, `initPubSub`.
  - Support canonical `@google-cloud/pubsub` API (`pubsub.topic(name)`, `topic.publishMessage(...)`).
* [ ] **3.3 Clean `HttpClientLibraryParser.cs`**:
  - Remove fake route generation fallback: `return $"api/v1/{propName.ToLowerInvariant()}";`.
  - Remove hardcoded proprietary clients: `HttpClientResource`, `GetRequest`, `PostRequest`.
  - Use `AstValueResolver` to resolve URL paths, query parameters, and base addresses.
* [ ] **3.4 Clean `NestedSqlParser.cs`**:
  - Remove `IsVariable` table dropping heuristic. Tables with dynamic interpolations that evaluate via `AstValueResolver` must be preserved in the graph.
* [ ] **3.5 Clean `GoAstHelper.cs` & `PostIndexAnalyzer.cs`**:
  - Remove ad-hoc selector matching (`r.config.impressionQueue`, `ImpressionTopic`, `TopicID`, `SubID`).
  - Remove hardcoded BigQuery schema rename (`default` -> `defaults`).

### Phase 4: Decouple Domain Classification from Core Engine
* [ ] **4.1 Purge Ad-Tech Ontology in `ArchitectureViewEngine.cs`**:
  - Remove hardcoded terms: `rules_bundling`, `rates_analytics`, `domain_management`, `tracking_postbacks`, `campaign_advertising`, `traffic_routing`, `cpm`, `epm`, `dynadot`, `smartcpa`, `tbmap`, `lander`, `popunder`.
* [ ] **4.2 Pluggable Domain Ontologies**:
  - Read domain classifications from `.codeexplorer/domains.json` if present in workspace.
  - Fall back to community graph clustering (Louvain/Leiden algorithm over materialized runtime macro-edges) or intent classification.

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
3. **Public Sample Benchmarks**:
   - Validate against standard public repositories (eShopOnContainers, Jellyfin, ASP.NET Core samples) to ensure zero spurious domain tags or phantom URLs.
