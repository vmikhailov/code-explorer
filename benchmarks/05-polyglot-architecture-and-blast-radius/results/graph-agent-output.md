# Polyglot Architecture Discovery & Blast Radius Analysis

---

## 1. Interface Implementations across the Codebase

### `IComponentLibraryParser` (16 implementations)
Declared in [`IComponentLibraryParser.cs`](file:///c:/Work/Personal/code-explorer/cli/src/Core/CodeExplorer.Core/Analysis/Layers/Layer2_Boundaries/Components/IComponentLibraryParser.cs):

| Class Name | Project | Relative File Path |
| :--- | :--- | :--- |
| `AngularComponentParser` | `CodeExplorer.Parser.TypeScript` | `cli/src/Parsers/CodeExplorer.Parser.TypeScript/Components/AngularComponentParser.cs` |
| `AspNetCoreComponentParser` | `CodeExplorer.Parser.CSharp` | `cli/src/Parsers/CodeExplorer.Parser.CSharp/Components/AspNetCoreComponentParser.cs` |
| `DotNetLibraryComponentParser` | `CodeExplorer.Parser.CSharp` | `cli/src/Parsers/CodeExplorer.Parser.CSharp/Components/DotNetLibraryComponentParser.cs` |
| `DotNetSchedulerComponentParser` | `CodeExplorer.Parser.CSharp` | `cli/src/Parsers/CodeExplorer.Parser.CSharp/Components/DotNetSchedulerComponentParser.cs` |
| `DotNetTestComponentParser` | `CodeExplorer.Parser.CSharp` | `cli/src/Parsers/CodeExplorer.Parser.CSharp/Components/DotNetTestComponentParser.cs` |
| `DotNetWorkerComponentParser` | `CodeExplorer.Parser.CSharp` | `cli/src/Parsers/CodeExplorer.Parser.CSharp/Components/DotNetWorkerComponentParser.cs` |
| `ExpressFastifyComponentParser` | `CodeExplorer.Parser.TypeScript` | `cli/src/Parsers/CodeExplorer.Parser.TypeScript/Components/ExpressFastifyComponentParser.cs` |
| `FastApiComponentParser` | `CodeExplorer.Parser.Python` | `cli/src/Parsers/CodeExplorer.Parser.Python/Components/FastApiComponentParser.cs` |
| `GoTestComponentParser` | `CodeExplorer.Parser.Go` | `cli/src/Parsers/CodeExplorer.Parser.Go/Components/GoTestComponentParser.cs` |
| `JavaScriptTestRunnerComponentParser` | `CodeExplorer.Parser.TypeScript` | `cli/src/Parsers/CodeExplorer.Parser.TypeScript/Components/JavaScriptTestRunnerComponentParser.cs` |
| `JavaTestComponentParser` | `CodeExplorer.Parser.Java` | `cli/src/Parsers/CodeExplorer.Parser.Java/Components/JavaTestComponentParser.cs` |
| `NestJsComponentParser` | `CodeExplorer.Parser.TypeScript` | `cli/src/Parsers/CodeExplorer.Parser.TypeScript/Components/NestJsComponentParser.cs` |
| `NodeWorkerSchedulerComponentParser` | `CodeExplorer.Parser.TypeScript` | `cli/src/Parsers/CodeExplorer.Parser.TypeScript/Components/NodeWorkerSchedulerComponentParser.cs` |
| `PythonTestComponentParser` | `CodeExplorer.Parser.Python` | `cli/src/Parsers/CodeExplorer.Parser.Python/Components/PythonTestComponentParser.cs` |
| `ReactNextComponentParser` | `CodeExplorer.Parser.TypeScript` | `cli/src/Parsers/CodeExplorer.Parser.TypeScript/Components/ReactNextComponentParser.cs` |
| `YarpGatewayComponentParser` | `CodeExplorer.Parser.CSharp` | `cli/src/Parsers/CodeExplorer.Parser.CSharp/Components/YarpGatewayComponentParser.cs` |

---

### `IMcpTargetAdapter` (8 implementations)
Declared in [`IMcpTargetAdapter.cs`](file:///c:/Work/Personal/code-explorer/cli/src/UI/CodeExplorer/Configuration/IMcpTargetAdapter.cs):

| Class Name | Project | Relative File Path |
| :--- | :--- | :--- |
| `AntigravityAdapter` | `CodeExplorer` | `cli/src/UI/CodeExplorer/Configuration/Adapters/AntigravityAdapter.cs` |
| `ClaudeCodeAdapter` | `CodeExplorer` | `cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeCodeAdapter.cs` |
| `ClaudeDesktopAdapter` | `CodeExplorer` | `cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeDesktopAdapter.cs` |
| `ClineAdapter` | `CodeExplorer` | `cli/src/UI/CodeExplorer/Configuration/Adapters/ClineAdapter.cs` |
| `CursorAdapter` | `CodeExplorer` | `cli/src/UI/CodeExplorer/Configuration/Adapters/CursorAdapter.cs` |
| `VsCodeAdapter` | `CodeExplorer` | `cli/src/UI/CodeExplorer/Configuration/Adapters/VsCodeAdapter.cs` |
| `WindsurfAdapter` | `CodeExplorer` | `cli/src/UI/CodeExplorer/Configuration/Adapters/WindsurfAdapter.cs` |
| `ZedAdapter` | `CodeExplorer` | `cli/src/UI/CodeExplorer/Configuration/Adapters/ZedAdapter.cs` |

---

### `ILibraryConfigurationDescriptor` (5 implementations)
Declared in [`ILibraryConfigurationDescriptor.cs`](file:///c:/Work/Personal/code-explorer/cli/src/Core/CodeExplorer.Core/Analysis/Layers/Layer4_Semantic/Configuration/ILibraryConfigurationDescriptor.cs):

| Class Name | Project | Relative File Path |
| :--- | :--- | :--- |
| `CloudServicesConfigurationDescriptor` | `CodeExplorer.Core` | `cli/src/Core/CodeExplorer.Core/Analysis/Layers/Layer4_Semantic/Configuration/StandardConfigurationDescriptors.cs` |
| `DatabaseConfigurationDescriptor` | `CodeExplorer.Core` | `cli/src/Core/CodeExplorer.Core/Analysis/Layers/Layer4_Semantic/Configuration/StandardConfigurationDescriptors.cs` |
| `MessageBrokerConfigurationDescriptor` | `CodeExplorer.Core` | `cli/src/Core/CodeExplorer.Core/Analysis/Layers/Layer4_Semantic/Configuration/StandardConfigurationDescriptors.cs` |
| `SpringFrameworkConfigurationDescriptor` | `CodeExplorer.Parser.Java` | `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringFrameworkConfigurationDescriptor.cs` |
| `SpringFrameworkConfigurationDescriptor` | `CodeExplorer.Parser.Shared` | `cli/src/Parsers/CodeExplorer.Parser.Shared/Configuration/SpringFrameworkConfigurationDescriptor.cs` |

---

### `ISyntaxEnricher` (0 implementations)
- **Count**: **0** (Neither the `ISyntaxEnricher` interface nor any implementing class exists in the codebase; syntactic/file-signal enrichment is performed by static classes `Layer3FileSignalEnricher` in `cli/src/Core/CodeExplorer.Core/Analysis/Layers/Layer3_Syntactic/Layer3FileSignalEnricher.cs` and `SharedFileEnricher` in `cli/src/Core/CodeExplorer.Core/Parser/Shared/SharedFileEnricher.cs`).

---

## 2. Blast Radius & Call Chain of `GateExecutionEngine`

Target class: `GateExecutionEngine` (`cli/src/Core/CodeExplorer.Gates/Engine/GateExecutionEngine.cs`).

### A. Direct Production Callers of `GateExecutionEngine.CheckGateAsync`

1. **`GateExecutionEngine.CheckSuiteAsync`** (`cli/src/Core/CodeExplorer.Gates/Engine/GateExecutionEngine.cs`)
   - *Internal self-call*: `CheckSuiteAsync` iterates over the active gates in a suite and delegates each gate's evaluation to `CheckGateAsync`.
   - **Upstream callers (1 hop further)**:
     - `CodeExplorerRepository.CheckSemanticGatesAsync` (`cli/src/Core/CodeExplorer.Analysis/Mcp/CodeExplorerRepository.Queries.cs`)
     - `GateCommandHandler.HandleAsync` (`cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs`)
     - `WebSocketServerHandler.EvaluateGatesSuiteAsync` (`cli/src/Server/CodeExplorer.Server/WebSocketServerHandler.Endpoints.cs`)
     - `WebSocketServerHandler.RatchetGatesSuiteAsync` (`cli/src/Server/CodeExplorer.Server/WebSocketServerHandler.Endpoints.cs`)

2. **`CodeExplorerRepository.CheckSemanticGatesAsync`** (`cli/src/Core/CodeExplorer.Analysis/Mcp/CodeExplorerRepository.Queries.cs`)
   - Calls `CheckGateAsync` directly when a single `gateId` filter is specified (and `CheckSuiteAsync` when evaluating all gates).
   - **Upstream Triggers (1 hop further)**:
     - **MCP Tool**: `MCP:check_semantic_gates` via `McpGraphHandler.CheckSemanticGatesAsync` (`cli/src/Server/CodeExplorer.Server/Mcp/McpGraphHandler.Tools.cs`)

3. **`GateCommandHandler.HandleAsync`** (`cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs`)
   - Calls `CheckGateAsync` directly when `--gate <id>` is passed on the CLI (and `CheckSuiteAsync` otherwise).
   - **Upstream Triggers (1 hop further)**:
     - **CLI Command**: `CLI:ce gate (GateOptions)` (`ce gate check` / `ce gate details`) dispatched from `Program.Main` (`cli/src/UI/CodeExplorer/Program.cs`)

---

### B. Direct Production Callers of `GateExecutionEngine.CheckSuiteAsync`

1. **`CodeExplorerRepository.CheckSemanticGatesAsync`** (`cli/src/Core/CodeExplorer.Analysis/Mcp/CodeExplorerRepository.Queries.cs`)
   - **Upstream Triggers (1 hop further)**:
     - **MCP Tool**: `MCP:check_semantic_gates` handled by `McpGraphHandler.CheckSemanticGatesAsync` (`cli/src/Server/CodeExplorer.Server/Mcp/McpGraphHandler.Tools.cs`)

2. **`GateCommandHandler.HandleAsync`** (`cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs`)
   - **Upstream Triggers (1 hop further)**:
     - **CLI Command**: `CLI:ce gate (GateOptions)` (`ce gate check` / `ce gate details`) dispatched from `Program.Main` (`cli/src/UI/CodeExplorer/Program.cs`)

3. **`WebSocketServerHandler.EvaluateGatesSuiteAsync`** (`cli/src/Server/CodeExplorer.Server/WebSocketServerHandler.Endpoints.cs`)
   - **Upstream Triggers (1 hop further)**:
     - **HTTP Endpoints** (registered in `ServerRouteBuilderExtensions.MapCodeExplorerApi`, `cli/src/Server/CodeExplorer.Server/ServerRouteBuilderExtensions.cs`):
       - `GET:/api/gates` (`GET /api/gates`)
       - `POST:/api/gates/check` (`POST /api/gates/check`)
     - **WebSocket Message Types** (dispatched in `WebSocketServerHandler.ProcessMessageAsync`, `cli/src/Server/CodeExplorer.Server/WebSocketServerHandler.cs`):
       - `WS:GET_GATES_REQUEST` (`GET_GATES_REQUEST`)
       - `WS:EVALUATE_GATES_REQUEST` (`EVALUATE_GATES_REQUEST`)
     - **Sibling Methods on `WebSocketServerHandler`** (`cli/src/Server/CodeExplorer.Server/WebSocketServerHandler.Endpoints.cs`) that call `EvaluateGatesSuiteAsync` after mutating gate definitions:
       - `WebSocketServerHandler.InitStarterGatesAsync` (triggered by HTTP `POST:/api/gates/init` and WebSocket `WS:INIT_GATES_REQUEST`)
       - `WebSocketServerHandler.SaveGateAsync` (triggered by HTTP `POST:/api/gates/save` and WebSocket `WS:SAVE_GATE_REQUEST`)

4. **`WebSocketServerHandler.RatchetGatesSuiteAsync`** (`cli/src/Server/CodeExplorer.Server/WebSocketServerHandler.Endpoints.cs`)
   - **Upstream Triggers (1 hop further)**:
     - **HTTP Endpoint** (registered in `ServerRouteBuilderExtensions.MapCodeExplorerApi`, `cli/src/Server/CodeExplorer.Server/ServerRouteBuilderExtensions.cs`):
       - `POST:/api/gates/ratchet` (`POST /api/gates/ratchet`)
     - **WebSocket Message Type** (dispatched in `WebSocketServerHandler.ProcessMessageAsync`, `cli/src/Server/CodeExplorer.Server/WebSocketServerHandler.cs`):
       - `WS:RATCHET_GATES_REQUEST` (`RATCHET_GATES_REQUEST`)

---

## 3. Polyglot `ISemanticExtension` Breakdown

### Exact Count by Parser Project

| Parser Project | `ISemanticExtension` Implementations Count |
| :--- | :---: |
| `CodeExplorer.Parser.CSharp` | **48** |
| `CodeExplorer.Parser.TypeScript` | **73** |
| `CodeExplorer.Parser.Python` | **41** |
| `CodeExplorer.Parser.Go` | **48** |
| `CodeExplorer.Parser.Java` | **38** |
| `CodeExplorer.Parser.Android` | **0** |

*(Note: Across the entire repository, the other projects containing `ISemanticExtension` implementations are `CodeExplorer.Parser.Kotlin` with 19, `CodeExplorer.Parser.Rust` with 14, `CodeExplorer.Core` with 1 [`DeclarativeSemanticExtension`], `CodeExplorer.Analysis` with 1 [`PackageSemanticExtensionAdapter`], and `CodeExplorer.Tests` with 2 test mocks.)*

---

### `ISemanticExtension` Implementations in `CodeExplorer.Parser.Java` (38 classes)

All 38 classes are located under `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/`:

1. `AkkaPekkoLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/AkkaPekkoLibraryParser.cs`)
2. `ApacheCamelLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/ApacheCamelLibraryParser.cs`)
3. `ApachePulsarLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/ApachePulsarLibraryParser.cs`)
4. `AwsSdkJavaLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/AwsSdkJavaLibraryParser.cs`)
5. `AxonFrameworkLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/AxonFrameworkLibraryParser.cs`)
6. `DropwizardJerseyLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/DropwizardJerseyLibraryParser.cs`)
7. `DubboLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/DubboLibraryParser.cs`)
8. `GrpcJavaLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/GrpcJavaLibraryParser.cs`)
9. `HttpClientJavaLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/HttpClientJavaLibraryParser.cs`)
10. `JavaTestLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JavaTestLibraryParser.cs`)
11. `JdbcTemplateLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JdbcTemplateLibraryParser.cs`)
12. `JdbiLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JdbiLibraryParser.cs`)
13. `JooqLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JooqLibraryParser.cs`)
14. `JpaLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JpaLibraryParser.cs`)
15. `LangChain4jSpringAiLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/LangChain4jSpringAiLibraryParser.cs`)
16. `MicronautLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/MicronautLibraryParser.cs`)
17. `MyBatisLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/MyBatisLibraryParser.cs`)
18. `NettyLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/NettyLibraryParser.cs`)
19. `OpenFeignRetrofitLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/OpenFeignRetrofitLibraryParser.cs`)
20. `PanacheOrmLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/PanacheOrmLibraryParser.cs`)
21. `QuarkusJaxRsLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/QuarkusJaxRsLibraryParser.cs`)
22. `QuarkusMessagingLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/QuarkusMessagingLibraryParser.cs`)
23. `R2dbcLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/R2dbcLibraryParser.cs`)
24. `RedissonCacheJavaLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/RedissonCacheJavaLibraryParser.cs`)
25. `Resilience4jLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/Resilience4jLibraryParser.cs`)
26. `SpringBatchLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringBatchLibraryParser.cs`)
27. `SpringCloudGatewayLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringCloudGatewayLibraryParser.cs`)
28. `SpringDataElasticsearchLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringDataElasticsearchLibraryParser.cs`)
29. `SpringDataMongoLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringDataMongoLibraryParser.cs`)
30. `SpringDataRedisLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringDataRedisLibraryParser.cs`)
31. `SpringEventsLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringEventsLibraryParser.cs`)
32. `SpringGraphQlLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringGraphQlLibraryParser.cs`)
33. `SpringIntegrationLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringIntegrationLibraryParser.cs`)
34. `SpringKafkaLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringKafkaLibraryParser.cs`)
35. `SpringMvcLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringMvcLibraryParser.cs`)
36. `SpringRabbitMqLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringRabbitMqLibraryParser.cs`)
37. `TemporalJavaLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/TemporalJavaLibraryParser.cs`)
38. `VertxLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/VertxLibraryParser.cs`)

---

### `ISemanticExtension` Implementations in `CodeExplorer.Parser.Android` (0 classes)

- **None (0 classes)**. `CodeExplorer.Parser.Android` contains only `AndroidManifestFileParser` (`cli/src/Parsers/CodeExplorer.Parser.Android/AndroidManifestFileParser.cs`), which implements `IFileParser` directly and defines `public IReadOnlyList<ISemanticExtension> SemanticExtensions => [];`.
