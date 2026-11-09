# Architectural and Impact Analysis Report: `code-explorer`

## 1. Interface Implementations across the Codebase

### `IComponentLibraryParser` (16 classes)
All implementations are declared under `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/`:

| Class Name | Relative File Path |
| :--- | :--- |
| [`AngularComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/AngularComponentParser.cs#L8) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/AngularComponentParser.cs` |
| [`AspNetCoreComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/AspNetCoreComponentParser.cs#L6) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/AspNetCoreComponentParser.cs` |
| [`DotNetLibraryComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetLibraryComponentParser.cs#L6) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetLibraryComponentParser.cs` |
| [`DotNetSchedulerComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetSchedulerComponentParser.cs#L6) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetSchedulerComponentParser.cs` |
| [`DotNetTestComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetTestComponentParser.cs#L8) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetTestComponentParser.cs` |
| [`DotNetWorkerComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetWorkerComponentParser.cs#L6) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetWorkerComponentParser.cs` |
| [`ExpressFastifyComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/ExpressFastifyComponentParser.cs#L6) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/ExpressFastifyComponentParser.cs` |
| [`FastApiComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/FastApiComponentParser.cs#L6) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/FastApiComponentParser.cs` |
| [`GoTestComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/GoTestComponentParser.cs#L8) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/GoTestComponentParser.cs` |
| [`JavaScriptTestRunnerComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/JavaScriptTestRunnerComponentParser.cs#L8) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/JavaScriptTestRunnerComponentParser.cs` |
| [`JavaTestComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/JavaTestComponentParser.cs#L8) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/JavaTestComponentParser.cs` |
| [`NestJsComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/NestJsComponentParser.cs#L8) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/NestJsComponentParser.cs` |
| [`NodeWorkerSchedulerComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/NodeWorkerSchedulerComponentParser.cs#L7) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/NodeWorkerSchedulerComponentParser.cs` |
| [`PythonTestComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/PythonTestComponentParser.cs#L8) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/PythonTestComponentParser.cs` |
| [`ReactNextComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/ReactNextComponentParser.cs#L7) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/ReactNextComponentParser.cs` |
| [`YarpGatewayComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/YarpGatewayComponentParser.cs#L6) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/YarpGatewayComponentParser.cs` |

---

### `IMcpTargetAdapter` (8 classes)
All implementations are declared under `cli/src/UI/CodeExplorer/Configuration/Adapters/`:

| Class Name | Relative File Path |
| :--- | :--- |
| [`AntigravityAdapter`](file:///cli/src/UI/CodeExplorer/Configuration/Adapters/AntigravityAdapter.cs#L5) | `cli/src/UI/CodeExplorer/Configuration/Adapters/AntigravityAdapter.cs` |
| [`ClaudeCodeAdapter`](file:///cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeCodeAdapter.cs#L5) | `cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeCodeAdapter.cs` |
| [`ClaudeDesktopAdapter`](file:///cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeDesktopAdapter.cs#L5) | `cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeDesktopAdapter.cs` |
| [`ClineAdapter`](file:///cli/src/UI/CodeExplorer/Configuration/Adapters/ClineAdapter.cs#L5) | `cli/src/UI/CodeExplorer/Configuration/Adapters/ClineAdapter.cs` |
| [`CursorAdapter`](file:///cli/src/UI/CodeExplorer/Configuration/Adapters/CursorAdapter.cs#L5) | `cli/src/UI/CodeExplorer/Configuration/Adapters/CursorAdapter.cs` |
| [`VsCodeAdapter`](file:///cli/src/UI/CodeExplorer/Configuration/Adapters/VsCodeAdapter.cs#L5) | `cli/src/UI/CodeExplorer/Configuration/Adapters/VsCodeAdapter.cs` |
| [`WindsurfAdapter`](file:///cli/src/UI/CodeExplorer/Configuration/Adapters/WindsurfAdapter.cs#L5) | `cli/src/UI/CodeExplorer/Configuration/Adapters/WindsurfAdapter.cs` |
| [`ZedAdapter`](file:///cli/src/UI/CodeExplorer/Configuration/Adapters/ZedAdapter.cs#L5) | `cli/src/UI/CodeExplorer/Configuration/Adapters/ZedAdapter.cs` |

---

### `ILibraryConfigurationDescriptor` (4 classes)

| Class Name | Relative File Path |
| :--- | :--- |
| [`CloudServicesConfigurationDescriptor`](file:///cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs#L141) | `cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs` |
| [`DatabaseConfigurationDescriptor`](file:///cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs#L9) | `cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs` |
| [`MessageBrokerConfigurationDescriptor`](file:///cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs#L85) | `cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs` |
| [`SpringFrameworkConfigurationDescriptor`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringFrameworkConfigurationDescriptor.cs#L11) | `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringFrameworkConfigurationDescriptor.cs` |

---

### `ISyntaxEnricher` (3 classes)

| Class Name | Relative File Path |
| :--- | :--- |
| [`ColdFusionSyntaxEnricher`](file:///cli/src/Parsers/CodeExplorer.Parser.ColdFusion/ColdFusionSyntaxEnricher.cs#L10) | `cli/src/Parsers/CodeExplorer.Parser.ColdFusion/ColdFusionSyntaxEnricher.cs` |
| [`SqlSyntaxEnricher`](file:///cli/src/Parsers/CodeExplorer.Parser.SQL/SqlSyntaxEnricher.cs#L9) | `cli/src/Parsers/CodeExplorer.Parser.SQL/SqlSyntaxEnricher.cs` |
| [`SyntaxEnricher`](file:///cli/src/Core/CodeExplorer.Core/Parser/SyntaxEnricher.cs#L15) | `cli/src/Core/CodeExplorer.Core/Parser/SyntaxEnricher.cs` |

---

## 2. Blast Radius & Call Chain of `GateExecutionEngine`

Target methods on [`GateExecutionEngine`](file:///cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs):
- [`CheckGateAsync`](file:///cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs#L23) (line 23)
- [`CheckSuiteAsync`](file:///cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs#L69) (line 69)

### Production (Non-Test) Direct Callers & One Hop Upstream Triggers

1. **[`GateExecutionEngine.CheckSuiteAsync`](file:///cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs#L69)** (`cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs:69`)
   - **Direct Call**:
     - Calls [`CheckGateAsync`](file:///cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs#L101) internally at line 101 for each gate in the filtered suite.
   - **One Hop Upstream**:
     - Called directly by callers 2, 3, 4, and 5 below.

2. **[`CodeExplorerRepository.CheckSemanticGatesAsync`](file:///cli/src/Core/CodeExplorer.Core/Mcp/CodeExplorerRepository.cs#L1878)** (`cli/src/Core/CodeExplorer.Core/Mcp/CodeExplorerRepository.cs:1878`)
   - **Direct Calls**:
     - `engine.CheckGateAsync(singleGate, cancellationToken)` at [line 1920](file:///cli/src/Core/CodeExplorer.Core/Mcp/CodeExplorerRepository.cs#L1920) (when a single `gateId` is specified).
     - `engine.CheckSuiteAsync(null, ...)` at [line 1924](file:///cli/src/Core/CodeExplorer.Core/Mcp/CodeExplorerRepository.cs#L1924) (when evaluating a suite/filter).
   - **One Hop Upstream (MCP Tool)**:
     - Triggered by [`McpGraphHandler.CheckSemanticGatesAsync`](file:///cli/src/Core/CodeExplorer.Core/Mcp/McpGraphHandler.cs#L652) (at line 666), decorated with `[McpServerTool]` attribute (exposing tool `check_semantic_gates`).

3. **[`GateCommandHandler.HandleAsync`](file:///cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L17)** (`cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs:17`)
   - **Direct Calls**:
     - `engine.CheckGateAsync(gate)` at [line 196](file:///cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L196) (`action is "violations" or "show"` with `--gate`) and [line 222](file:///cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L222) (`action == "check"` with single `--gate`).
     - `engine.CheckSuiteAsync(...)` at:
       - [Line 145](file:///cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L145) (`action == "ratchet"`)
       - [Line 202](file:///cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L202) (`action is "violations" or "show"` without `--gate`)
       - [Line 238](file:///cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L238) (`action == "check"` with multiple comma-separated `--gate` IDs)
       - [Line 242](file:///cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L242) (`action == "check"` for full suite)
   - **One Hop Upstream (CLI Command)**:
     - Triggered by [`Program.Main`](file:///cli/src/UI/CodeExplorer/Program.cs#L89) (`cli/src/UI/CodeExplorer/Program.cs:89`) via command line options [`GateOptions`](file:///cli/src/UI/CodeExplorer/Options/GateOptions.cs#L6) decorated with [`[Verb("gate")]`](file:///cli/src/UI/CodeExplorer/Options/GateOptions.cs#L5): CLI command `ce gate [check|ratchet|violations|show]`.

4. **[`WebSocketServerHandler.EvaluateGatesSuiteAsync`](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L670)** (`cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs:670`)
   - **Direct Calls**:
     - `engine.CheckSuiteAsync(null, ...)` at [line 684](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L684) (full suite evaluation).
     - `engine.CheckSuiteAsync(targetGates, ...)` at [line 695](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L695) (single-gate evaluation).
   - **One Hop Upstream**:
     - **WebSocket Message Types** (dispatched in [`WebSocketServerHandler.ProcessMessageAsync`](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L451-L463), lines 451–463):
       - `WsMessageTypes.GetGatesRequest` (`"GET_GATES_REQUEST"`) / `"GET_GATES"`
       - `WsMessageTypes.EvaluateGatesRequest` (`"EVALUATE_GATES_REQUEST"`) / `"EVALUATE_GATES"`
     - **HTTP Endpoints** (mapped in [`ServerRouteBuilderExtensions.MapCodeExplorerApi`](file:///cli/src/UI/CodeExplorer/Server/ServerRouteBuilderExtensions.cs)):
       - `GET /api/gates` at [line 315](file:///cli/src/UI/CodeExplorer/Server/ServerRouteBuilderExtensions.cs#L315) (invokes `wsHandler.EvaluateGatesSuiteAsync` at line 326)
       - `POST /api/gates/check` at [line 336](file:///cli/src/UI/CodeExplorer/Server/ServerRouteBuilderExtensions.cs#L336) (invokes `wsHandler.EvaluateGatesSuiteAsync` at line 341)
     - **Internal Server Caller [`WebSocketServerHandler.InitStarterGatesAsync`](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L819)** (line 823), triggered by:
       - **WebSocket Message Type**: `WsMessageTypes.InitGatesRequest` (`"INIT_GATES_REQUEST"`) / `"INIT_GATES"` ([lines 474–481](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L474-L481))
       - **HTTP Endpoint**: `POST /api/gates/init` at [lines 366–379](file:///cli/src/UI/CodeExplorer/Server/ServerRouteBuilderExtensions.cs#L366-L379)

5. **[`WebSocketServerHandler.RatchetGatesSuiteAsync`](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L762)** (`cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs:762`)
   - **Direct Calls**:
     - `engine.CheckSuiteAsync(targetGates, ...)` at [line 783](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L783) (evaluates target gates before ratcheting).
     - `engine.CheckSuiteAsync(...)` at [line 805](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L805) (re-evaluates full suite to refresh cache after saving new baselines).
   - **One Hop Upstream**:
     - **WebSocket Message Type** (dispatched in [`WebSocketServerHandler.ProcessMessageAsync`](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L465-L472), lines 465–472):
       - `WsMessageTypes.RatchetGatesRequest` (`"RATCHET_GATES_REQUEST"`) / `"RATCHET_GATES"`
     - **HTTP Endpoint** (mapped in [`ServerRouteBuilderExtensions.MapCodeExplorerApi`](file:///cli/src/UI/CodeExplorer/Server/ServerRouteBuilderExtensions.cs)):
       - `POST /api/gates/ratchet` at [line 351](file:///cli/src/UI/CodeExplorer/Server/ServerRouteBuilderExtensions.cs#L351) (invokes `wsHandler.RatchetGatesSuiteAsync` at line 356)

---

## 3. Polyglot `ISemanticExtension` Breakdown

### Exact Implementation Counts by Parser Project

| Parser Project | Exact Count of `ISemanticExtension` Implementations |
| :--- | :---: |
| `CodeExplorer.Parser.CSharp` | **25** |
| `CodeExplorer.Parser.TypeScript` | **51** |
| `CodeExplorer.Parser.Python` | **18** |
| `CodeExplorer.Parser.Go` | **19** |
| `CodeExplorer.Parser.Java` | **9** |
| `CodeExplorer.Parser.Android` | **0** |

---

### Exact Class Names in `CodeExplorer.Parser.Java` (9 classes)
All 9 classes are located under `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/`:

1. [`GrpcJavaLibraryParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/GrpcJavaLibraryParser.cs#L8) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/GrpcJavaLibraryParser.cs`)
2. [`HttpClientJavaLibraryParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/HttpClientJavaLibraryParser.cs#L8) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/HttpClientJavaLibraryParser.cs`)
3. [`JdbcTemplateLibraryParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JdbcTemplateLibraryParser.cs#L8) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JdbcTemplateLibraryParser.cs`)
4. [`JpaLibraryParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JpaLibraryParser.cs#L8) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JpaLibraryParser.cs`)
5. [`SpringEventsLibraryParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringEventsLibraryParser.cs#L8) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringEventsLibraryParser.cs`)
6. [`SpringGraphQlLibraryParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringGraphQlLibraryParser.cs#L8) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringGraphQlLibraryParser.cs`)
7. [`SpringKafkaLibraryParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringKafkaLibraryParser.cs#L8) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringKafkaLibraryParser.cs`)
8. [`SpringMvcLibraryParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringMvcLibraryParser.cs#L8) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringMvcLibraryParser.cs`)
9. [`SpringRabbitMqLibraryParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringRabbitMqLibraryParser.cs#L8) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringRabbitMqLibraryParser.cs`)

> [!NOTE]
> `SpringFrameworkConfigurationDescriptor` in `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringFrameworkConfigurationDescriptor.cs` implements [`ILibraryConfigurationDescriptor`](file:///cli/src/Core/CodeExplorer.Core/Parser/ILibraryConfigurationDescriptor.cs#L10), not `ISemanticExtension`.

---

### Exact Class Names in `CodeExplorer.Parser.Android` (0 classes)
- **None (0 classes)** implement `ISemanticExtension` in `CodeExplorer.Parser.Android`.
- The project contains:
  - [`AndroidManifestFileParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Android/AndroidManifestFileParser.cs#L11) (implements `IFileParser` with `SemanticExtensions => []` at line 17)
  - [`AndroidManifestParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Android/AndroidManifestParser.cs#L5)
  - [`AndroidManifestInfo`](file:///cli/src/Parsers/CodeExplorer.Parser.Android/AndroidManifestInfo.cs#L3)
  - [`GradleBuildScriptParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Android/GradleBuildScriptParser.cs#L23)
  None of these implement `ISemanticExtension`.
