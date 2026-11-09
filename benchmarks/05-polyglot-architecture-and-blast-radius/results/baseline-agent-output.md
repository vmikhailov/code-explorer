# Polyglot Architecture Discovery & Blast Radius Analysis

---

## 1. Interface Implementations across the Codebase

### `IComponentLibraryParser` (16 classes)
Declared in `cli/src/Core/CodeExplorer.Core/Analysis/Layers/Layer2_Boundaries/Components/IComponentLibraryParser.cs`:
1. **`AspNetCoreComponentParser`** — `cli/src/Parsers/CodeExplorer.Parser.CSharp/Components/AspNetCoreComponentParser.cs`
2. **`DotNetLibraryComponentParser`** — `cli/src/Parsers/CodeExplorer.Parser.CSharp/Components/DotNetLibraryComponentParser.cs`
3. **`DotNetSchedulerComponentParser`** — `cli/src/Parsers/CodeExplorer.Parser.CSharp/Components/DotNetSchedulerComponentParser.cs`
4. **`DotNetTestComponentParser`** — `cli/src/Parsers/CodeExplorer.Parser.CSharp/Components/DotNetTestComponentParser.cs`
5. **`DotNetWorkerComponentParser`** — `cli/src/Parsers/CodeExplorer.Parser.CSharp/Components/DotNetWorkerComponentParser.cs`
6. **`YarpGatewayComponentParser`** — `cli/src/Parsers/CodeExplorer.Parser.CSharp/Components/YarpGatewayComponentParser.cs`
7. **`GoTestComponentParser`** — `cli/src/Parsers/CodeExplorer.Parser.Go/Components/GoTestComponentParser.cs`
8. **`JavaTestComponentParser`** — `cli/src/Parsers/CodeExplorer.Parser.Java/Components/JavaTestComponentParser.cs`
9. **`FastApiComponentParser`** — `cli/src/Parsers/CodeExplorer.Parser.Python/Components/FastApiComponentParser.cs`
10. **`PythonTestComponentParser`** — `cli/src/Parsers/CodeExplorer.Parser.Python/Components/PythonTestComponentParser.cs`
11. **`AngularComponentParser`** — `cli/src/Parsers/CodeExplorer.Parser.TypeScript/Components/AngularComponentParser.cs`
12. **`ExpressFastifyComponentParser`** — `cli/src/Parsers/CodeExplorer.Parser.TypeScript/Components/ExpressFastifyComponentParser.cs`
13. **`JavaScriptTestRunnerComponentParser`** — `cli/src/Parsers/CodeExplorer.Parser.TypeScript/Components/JavaScriptTestRunnerComponentParser.cs`
14. **`NestJsComponentParser`** — `cli/src/Parsers/CodeExplorer.Parser.TypeScript/Components/NestJsComponentParser.cs`
15. **`NodeWorkerSchedulerComponentParser`** — `cli/src/Parsers/CodeExplorer.Parser.TypeScript/Components/NodeWorkerSchedulerComponentParser.cs`
16. **`ReactNextComponentParser`** — `cli/src/Parsers/CodeExplorer.Parser.TypeScript/Components/ReactNextComponentParser.cs`

### `IMcpTargetAdapter` (8 classes)
Declared in `cli/src/UI/CodeExplorer/Configuration/IMcpTargetAdapter.cs`:
1. **`AntigravityAdapter`** — `cli/src/UI/CodeExplorer/Configuration/Adapters/AntigravityAdapter.cs`
2. **`ClaudeCodeAdapter`** — `cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeCodeAdapter.cs`
3. **`ClaudeDesktopAdapter`** — `cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeDesktopAdapter.cs`
4. **`ClineAdapter`** — `cli/src/UI/CodeExplorer/Configuration/Adapters/ClineAdapter.cs`
5. **`CursorAdapter`** — `cli/src/UI/CodeExplorer/Configuration/Adapters/CursorAdapter.cs`
6. **`VsCodeAdapter`** — `cli/src/UI/CodeExplorer/Configuration/Adapters/VsCodeAdapter.cs`
7. **`WindsurfAdapter`** — `cli/src/UI/CodeExplorer/Configuration/Adapters/WindsurfAdapter.cs`
8. **`ZedAdapter`** — `cli/src/UI/CodeExplorer/Configuration/Adapters/ZedAdapter.cs`

### `ILibraryConfigurationDescriptor` (4 direct classes + 1 derived subclass = 5 total)
Declared in `cli/src/Core/CodeExplorer.Core/Analysis/Layers/Layer4_Semantic/Configuration/ILibraryConfigurationDescriptor.cs`:
1. **`DatabaseConfigurationDescriptor`** — `cli/src/Core/CodeExplorer.Core/Analysis/Layers/Layer4_Semantic/Configuration/StandardConfigurationDescriptors.cs`
2. **`MessageBrokerConfigurationDescriptor`** — `cli/src/Core/CodeExplorer.Core/Analysis/Layers/Layer4_Semantic/Configuration/StandardConfigurationDescriptors.cs`
3. **`CloudServicesConfigurationDescriptor`** — `cli/src/Core/CodeExplorer.Core/Analysis/Layers/Layer4_Semantic/Configuration/StandardConfigurationDescriptors.cs`
4. **`SpringFrameworkConfigurationDescriptor`** (`CodeExplorer.Parser.Shared.Configuration.SpringFrameworkConfigurationDescriptor`) — `cli/src/Parsers/CodeExplorer.Parser.Shared/Configuration/SpringFrameworkConfigurationDescriptor.cs`
5. **`SpringFrameworkConfigurationDescriptor`** (`CodeExplorer.Parser.Java.Libraries.SpringFrameworkConfigurationDescriptor`, inherits from `CodeExplorer.Parser.Shared.Configuration.SpringFrameworkConfigurationDescriptor`) — `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringFrameworkConfigurationDescriptor.cs`

### `ISyntaxEnricher` (0 classes)
- **None (0 implementations)**: `ISyntaxEnricher` does not exist in the codebase (syntax/file enrichment was refactored into static helper classes `Layer3FileSignalEnricher` in `cli/src/Core/CodeExplorer.Core/Analysis/Layers/Layer3_Syntactic/Layer3FileSignalEnricher.cs` and `SharedFileEnricher` in `cli/src/Core/CodeExplorer.Core/Parser/Shared/SharedFileEnricher.cs`).

---

## 2. Blast Radius & Call Chain of `GateExecutionEngine`

`GateExecutionEngine` is declared in `cli/src/Core/CodeExplorer.Gates/Engine/GateExecutionEngine.cs`. Below are all **production (non-test)** classes and methods that directly call `CheckGateAsync` or `CheckSuiteAsync`, along with their upstream callers one hop further:

### 1. `GateExecutionEngine.CheckSuiteAsync` (Self-Call)
- **File**: `cli/src/Core/CodeExplorer.Gates/Engine/GateExecutionEngine.cs` (line 101)
- **Direct Call**: Calls `CheckGateAsync(gate, cancellationToken)` in a loop for each gate in the suite.
- **One Hop Upstream**: Called by `CodeExplorerRepository.CheckSemanticGatesAsync`, `GateCommandHandler.HandleAsync`, `WebSocketServerHandler.EvaluateGatesSuiteAsync`, and `WebSocketServerHandler.RatchetGatesSuiteAsync` (detailed below).

### 2. `CodeExplorerRepository.CheckSemanticGatesAsync`
- **File**: `cli/src/Core/CodeExplorer.Analysis/Mcp/CodeExplorerRepository.Queries.cs`
- **Direct Calls**:
  - `engine.CheckGateAsync(singleGate, cancellationToken)` (line 440, when `gateId` is specified)
  - `engine.CheckSuiteAsync(null, scopeFilter, dimFilter, categoryFilter, platformFilter, cancellationToken)` (line 444, when evaluating a suite)
- **One Hop Upstream**:
  - **MCP Tool**: `McpGraphHandler.CheckSemanticGatesAsync` (decorated with `[McpServerTool]`, tool name `check_semantic_gates`) in `cli/src/Server/CodeExplorer.Server/Mcp/McpGraphHandler.Tools.cs` (lines 351–366).

### 3. `GateCommandHandler.HandleAsync`
- **File**: `cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs`
- **Direct Calls**:
  - `engine.CheckSuiteAsync(targetGates, scopeFilter, dimFilter, categoryFilter, platformFilter)` (line 143, `action == "ratchet"`)
  - `engine.CheckGateAsync(gate)` (line 194, `action is "details" or "violations" or "show"` with specific gate IDs)
  - `engine.CheckSuiteAsync(null, scopeFilter, dimFilter, categoryFilter, platformFilter)` (line 200, `action is "details" or "violations" or "show"` without specific gate ID)
  - `engine.CheckGateAsync(gate)` (line 225, `action == "check"` with a single gate ID)
  - `engine.CheckSuiteAsync(targetGates, scopeFilter, dimFilter, categoryFilter, platformFilter)` (line 241, `action == "check"` with multiple comma-separated gate IDs)
  - `engine.CheckSuiteAsync(null, scopeFilter, dimFilter, categoryFilter, platformFilter)` (line 245, `action == "check"` for full suite)
- **One Hop Upstream**:
  - **CLI Command**: `ce gate` (`[Verb("gate")]` on `GateOptions` in `cli/src/UI/CodeExplorer/Options/GateOptions.cs`), dispatched from `Program.RunCommand` in `cli/src/UI/CodeExplorer/Program.cs` (line 95: `GateOptions opts => await GateCommandHandler.HandleAsync(opts)`). Supports sub-actions `check` (default), `ratchet`, `details`, `violations` / `show`, `list`, `init`.

### 4. `WebSocketServerHandler.EvaluateGatesSuiteAsync`
- **File**: `cli/src/Server/CodeExplorer.Server/WebSocketServerHandler.Endpoints.cs`
- **Direct Calls**:
  - `engine.CheckSuiteAsync(null, cancellationToken: cancellationToken)` (line 195, full suite evaluation)
  - `engine.CheckSuiteAsync(targetGates, cancellationToken: cancellationToken)` (line 206, single gate evaluation)
- **One Hop Upstream**:
  - **HTTP Endpoints** (in `ServerRouteBuilderExtensions.MapCodeExplorerApi`, `cli/src/Server/CodeExplorer.Server/ServerRouteBuilderExtensions.cs`):
    - `GET /api/gates` (line 388: `await wsHandler.EvaluateGatesSuiteAsync(gateId, ct, force: false)`)
    - `POST /api/gates/check` (line 403: `await wsHandler.EvaluateGatesSuiteAsync(req?.GateId, ct, force: true)`)
  - **WebSocket Message Types** (in `WebSocketServerHandler.ProcessMessageAsync`, `cli/src/Server/CodeExplorer.Server/WebSocketServerHandler.cs`, lines 448–455):
    - `GET_GATES_REQUEST` (`WsMessageTypes.GetGatesRequest`) / `"GET_GATES"`
    - `EVALUATE_GATES_REQUEST` (`WsMessageTypes.EvaluateGatesRequest`) / `"EVALUATE_GATES"`
  - **Sibling Server Endpoint Methods** (in `WebSocketServerHandler`, `cli/src/Server/CodeExplorer.Server/WebSocketServerHandler.Endpoints.cs`):
    - `WebSocketServerHandler.InitStarterGatesAsync` (line 365) — triggered by `POST /api/gates/init` (`ServerRouteBuilderExtensions.cs` line 433) and WebSocket message `INIT_GATES_REQUEST` (`WsMessageTypes.InitGatesRequest`) / `"INIT_GATES"` (`WebSocketServerHandler.cs` line 476).
    - `WebSocketServerHandler.SaveGateAsync` (line 439) — triggered by `POST /api/gates/save` (`ServerRouteBuilderExtensions.cs` line 453) and WebSocket message `SAVE_GATE_REQUEST` (`WsMessageTypes.SaveGateRequest`) / `"SAVE_GATE"` (`WebSocketServerHandler.cs` line 490).

### 5. `WebSocketServerHandler.RatchetGatesSuiteAsync`
- **File**: `cli/src/Server/CodeExplorer.Server/WebSocketServerHandler.Endpoints.cs`
- **Direct Calls**:
  - `engine.CheckSuiteAsync(targetGates, cancellationToken: cancellationToken)` (line 325, initial check before updating baselines)
  - `engine.CheckSuiteAsync(cancellationToken: cancellationToken)` (line 347, post-ratchet suite refresh)
- **One Hop Upstream**:
  - **HTTP Endpoint** (in `ServerRouteBuilderExtensions.MapCodeExplorerApi`, `cli/src/Server/CodeExplorer.Server/ServerRouteBuilderExtensions.cs`):
    - `POST /api/gates/ratchet` (line 418: `await wsHandler.RatchetGatesSuiteAsync(req?.GateId, req?.UpdateAll ?? false, ct)`)
  - **WebSocket Message Types** (in `WebSocketServerHandler.ProcessMessageAsync`, `cli/src/Server/CodeExplorer.Server/WebSocketServerHandler.cs`, lines 462–467):
    - `RATCHET_GATES_REQUEST` (`WsMessageTypes.RatchetGatesRequest`) / `"RATCHET_GATES"`

---

## 3. Polyglot `ISemanticExtension` Breakdown

All library semantic extensions in the parser projects inherit from `DeclarativeSemanticExtension` (`cli/src/Core/CodeExplorer.Core/Analysis/Layers/Layer4_Semantic/Extensions/Declarative/DeclarativeSemanticExtension.cs`), which implements `ISemanticExtension`.

### Exact Counts per Parser Project
- **`CodeExplorer.Parser.CSharp`**: **48**
- **`CodeExplorer.Parser.TypeScript`**: **73**
- **`CodeExplorer.Parser.Python`**: **41**
- **`CodeExplorer.Parser.Go`**: **48**
- **`CodeExplorer.Parser.Java`**: **38**
- **`CodeExplorer.Parser.Android`**: **0**

### `ISemanticExtension` Implementations in `CodeExplorer.Parser.Java` (38 classes)
All declared under `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/`:
1. **`AkkaPekkoLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/AkkaPekkoLibraryParser.cs`)
2. **`ApacheCamelLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/ApacheCamelLibraryParser.cs`)
3. **`ApachePulsarLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/ApachePulsarLibraryParser.cs`)
4. **`AwsSdkJavaLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/AwsSdkJavaLibraryParser.cs`)
5. **`AxonFrameworkLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/AxonFrameworkLibraryParser.cs`)
6. **`DropwizardJerseyLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/DropwizardJerseyLibraryParser.cs`)
7. **`DubboLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/DubboLibraryParser.cs`)
8. **`GrpcJavaLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/GrpcJavaLibraryParser.cs`)
9. **`HttpClientJavaLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/HttpClientJavaLibraryParser.cs`)
10. **`JavaTestLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JavaTestLibraryParser.cs`)
11. **`JdbcTemplateLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JdbcTemplateLibraryParser.cs`)
12. **`JdbiLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JdbiLibraryParser.cs`)
13. **`JooqLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JooqLibraryParser.cs`)
14. **`JpaLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JpaLibraryParser.cs`)
15. **`LangChain4jSpringAiLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/LangChain4jSpringAiLibraryParser.cs`)
16. **`MicronautLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/MicronautLibraryParser.cs`)
17. **`MyBatisLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/MyBatisLibraryParser.cs`)
18. **`NettyLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/NettyLibraryParser.cs`)
19. **`OpenFeignRetrofitLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/OpenFeignRetrofitLibraryParser.cs`)
20. **`PanacheOrmLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/PanacheOrmLibraryParser.cs`)
21. **`QuarkusJaxRsLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/QuarkusJaxRsLibraryParser.cs`)
22. **`QuarkusMessagingLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/QuarkusMessagingLibraryParser.cs`)
23. **`R2dbcLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/R2dbcLibraryParser.cs`)
24. **`RedissonCacheJavaLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/RedissonCacheJavaLibraryParser.cs`)
25. **`Resilience4jLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/Resilience4jLibraryParser.cs`)
26. **`SpringBatchLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringBatchLibraryParser.cs`)
27. **`SpringCloudGatewayLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringCloudGatewayLibraryParser.cs`)
28. **`SpringDataElasticsearchLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringDataElasticsearchLibraryParser.cs`)
29. **`SpringDataMongoLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringDataMongoLibraryParser.cs`)
30. **`SpringDataRedisLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringDataRedisLibraryParser.cs`)
31. **`SpringEventsLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringEventsLibraryParser.cs`)
32. **`SpringGraphQlLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringGraphQlLibraryParser.cs`)
33. **`SpringIntegrationLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringIntegrationLibraryParser.cs`)
34. **`SpringKafkaLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringKafkaLibraryParser.cs`)
35. **`SpringMvcLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringMvcLibraryParser.cs`)
36. **`SpringRabbitMqLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringRabbitMqLibraryParser.cs`)
37. **`TemporalJavaLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/TemporalJavaLibraryParser.cs`)
38. **`VertxLibraryParser`** (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/VertxLibraryParser.cs`)

### `ISemanticExtension` Implementations in `CodeExplorer.Parser.Android` (0 classes)
- **None (0 classes)**: `CodeExplorer.Parser.Android` only contains `AndroidManifestFileParser` (`cli/src/Parsers/CodeExplorer.Parser.Android/AndroidManifestFileParser.cs`), which implements `IFileParser` with `SemanticExtensions => []`.
