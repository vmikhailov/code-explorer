# Architectural & Blast Radius Analysis Report for `code-explorer`

---

## 1. Interface Implementations across the Codebase

### `IComponentLibraryParser` (16 classes)
All 16 classes implement [`IComponentLibraryParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/IComponentLibraryParser.cs):

| Class Name | Relative File Path |
| :--- | :--- |
| [`AngularComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/AngularComponentParser.cs) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/AngularComponentParser.cs` |
| [`AspNetCoreComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/AspNetCoreComponentParser.cs) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/AspNetCoreComponentParser.cs` |
| [`DotNetLibraryComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetLibraryComponentParser.cs) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetLibraryComponentParser.cs` |
| [`DotNetSchedulerComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetSchedulerComponentParser.cs) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetSchedulerComponentParser.cs` |
| [`DotNetTestComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetTestComponentParser.cs) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetTestComponentParser.cs` |
| [`DotNetWorkerComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetWorkerComponentParser.cs) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetWorkerComponentParser.cs` |
| [`ExpressFastifyComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/ExpressFastifyComponentParser.cs) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/ExpressFastifyComponentParser.cs` |
| [`FastApiComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/FastApiComponentParser.cs) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/FastApiComponentParser.cs` |
| [`GoTestComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/GoTestComponentParser.cs) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/GoTestComponentParser.cs` |
| [`JavaScriptTestRunnerComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/JavaScriptTestRunnerComponentParser.cs) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/JavaScriptTestRunnerComponentParser.cs` |
| [`JavaTestComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/JavaTestComponentParser.cs) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/JavaTestComponentParser.cs` |
| [`NestJsComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/NestJsComponentParser.cs) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/NestJsComponentParser.cs` |
| [`NodeWorkerSchedulerComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/NodeWorkerSchedulerComponentParser.cs) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/NodeWorkerSchedulerComponentParser.cs` |
| [`PythonTestComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/PythonTestComponentParser.cs) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/PythonTestComponentParser.cs` |
| [`ReactNextComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/ReactNextComponentParser.cs) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/ReactNextComponentParser.cs` |
| [`YarpGatewayComponentParser`](file:///cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/YarpGatewayComponentParser.cs) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/YarpGatewayComponentParser.cs` |

---

### `IMcpTargetAdapter` (8 classes)
All 8 classes implement [`IMcpTargetAdapter`](file:///cli/src/UI/CodeExplorer/Configuration/IMcpTargetAdapter.cs):

| Class Name | Relative File Path |
| :--- | :--- |
| [`AntigravityAdapter`](file:///cli/src/UI/CodeExplorer/Configuration/Adapters/AntigravityAdapter.cs) | `cli/src/UI/CodeExplorer/Configuration/Adapters/AntigravityAdapter.cs` |
| [`ClaudeCodeAdapter`](file:///cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeCodeAdapter.cs) | `cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeCodeAdapter.cs` |
| [`ClaudeDesktopAdapter`](file:///cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeDesktopAdapter.cs) | `cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeDesktopAdapter.cs` |
| [`ClineAdapter`](file:///cli/src/UI/CodeExplorer/Configuration/Adapters/ClineAdapter.cs) | `cli/src/UI/CodeExplorer/Configuration/Adapters/ClineAdapter.cs` |
| [`CursorAdapter`](file:///cli/src/UI/CodeExplorer/Configuration/Adapters/CursorAdapter.cs) | `cli/src/UI/CodeExplorer/Configuration/Adapters/CursorAdapter.cs` |
| [`VsCodeAdapter`](file:///cli/src/UI/CodeExplorer/Configuration/Adapters/VsCodeAdapter.cs) | `cli/src/UI/CodeExplorer/Configuration/Adapters/VsCodeAdapter.cs` |
| [`WindsurfAdapter`](file:///cli/src/UI/CodeExplorer/Configuration/Adapters/WindsurfAdapter.cs) | `cli/src/UI/CodeExplorer/Configuration/Adapters/WindsurfAdapter.cs` |
| [`ZedAdapter`](file:///cli/src/UI/CodeExplorer/Configuration/Adapters/ZedAdapter.cs) | `cli/src/UI/CodeExplorer/Configuration/Adapters/ZedAdapter.cs` |

---

### `ILibraryConfigurationDescriptor` (4 classes)
All 4 classes implement [`ILibraryConfigurationDescriptor`](file:///cli/src/Core/CodeExplorer.Core/Parser/ILibraryConfigurationDescriptor.cs):

| Class Name | Relative File Path |
| :--- | :--- |
| [`CloudServicesConfigurationDescriptor`](file:///cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs) | `cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs` |
| [`DatabaseConfigurationDescriptor`](file:///cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs) | `cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs` |
| [`MessageBrokerConfigurationDescriptor`](file:///cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs) | `cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs` |
| [`SpringFrameworkConfigurationDescriptor`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringFrameworkConfigurationDescriptor.cs) | `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringFrameworkConfigurationDescriptor.cs` |

---

### `ISyntaxEnricher` (3 classes)
All 3 classes implement [`ISyntaxEnricher`](file:///cli/src/Core/CodeExplorer.Core/Parser/ISyntaxEnricher.cs):

| Class Name | Relative File Path |
| :--- | :--- |
| [`ColdFusionSyntaxEnricher`](file:///cli/src/Parsers/CodeExplorer.Parser.ColdFusion/ColdFusionSyntaxEnricher.cs) | `cli/src/Parsers/CodeExplorer.Parser.ColdFusion/ColdFusionSyntaxEnricher.cs` |
| [`SqlSyntaxEnricher`](file:///cli/src/Parsers/CodeExplorer.Parser.SQL/SqlSyntaxEnricher.cs) | `cli/src/Parsers/CodeExplorer.Parser.SQL/SqlSyntaxEnricher.cs` |
| [`SyntaxEnricher`](file:///cli/src/Core/CodeExplorer.Core/Parser/SyntaxEnricher.cs) | `cli/src/Core/CodeExplorer.Core/Parser/SyntaxEnricher.cs` |

---

## 2. Blast Radius & Call Chain of `GateExecutionEngine`

Target methods on [`GateExecutionEngine`](file:///cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs):
- [`CheckGateAsync`](file:///cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs#L23) (evaluates an individual semantic gate against the workspace graph)
- [`CheckSuiteAsync`](file:///cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs#L69) (evaluates a filtered or complete suite of semantic gates)

### Production (Non-Test) Direct Callers & One-Hop Upstream Triggers

1. **[`GateExecutionEngine.CheckSuiteAsync`](file:///cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs#L69)** (`cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs`)
   - **Direct Call**: Directly calls [`CheckGateAsync`](file:///cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs#L101) internally for each gate in the filtered suite.
   - **One Hop Upstream**: Internal engine orchestrator called directly by `CodeExplorerRepository.CheckSemanticGatesAsync`, `GateCommandHandler.HandleAsync`, `WebSocketServerHandler.EvaluateGatesSuiteAsync`, and `WebSocketServerHandler.RatchetGatesSuiteAsync` (each detailed below).

2. **[`CodeExplorerRepository.CheckSemanticGatesAsync`](file:///cli/src/Core/CodeExplorer.Core/Mcp/CodeExplorerRepository.cs#L1878-L1932)** (`cli/src/Core/CodeExplorer.Core/Mcp/CodeExplorerRepository.cs`)
   - **Direct Calls**:
     - [`engine.CheckGateAsync(singleGate, cancellationToken)`](file:///cli/src/Core/CodeExplorer.Core/Mcp/CodeExplorerRepository.cs#L1920) when a specific `gateId` is provided.
     - [`engine.CheckSuiteAsync(...)`](file:///cli/src/Core/CodeExplorer.Core/Mcp/CodeExplorerRepository.cs#L1924) when evaluating a suite of gates.
   - **One Hop Upstream Trigger**:
     - **MCP Tool**: `check_semantic_gates` via [`McpGraphHandler.CheckSemanticGatesAsync`](file:///cli/src/Core/CodeExplorer.Core/Mcp/McpGraphHandler.cs#L652-L667) (`cli/src/Core/CodeExplorer.Core/Mcp/McpGraphHandler.cs`), decorated with `[McpServerTool]`.

3. **[`GateCommandHandler.HandleAsync`](file:///cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L17-L244)** (`cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs`)
   - **Direct Calls**:
     - [`engine.CheckGateAsync(gate)`](file:///cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L196) (when `action is "violations" or "show"` with `--gate`) and [line 222](file:///cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L222) (when `action == "check"` with a single `--gate`).
     - [`engine.CheckSuiteAsync(targetGates, ...)`](file:///cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L145) (when `action == "ratchet"`), [line 202](file:///cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L202) (when `action is "violations" or "show"` without `--gate`), [line 238](file:///cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L238) (when `action == "check"` with multiple comma-separated `--gate` IDs), and [line 242](file:///cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L242) (when `action == "check"` for full suite).
   - **One Hop Upstream Trigger**:
     - **CLI Command**: `ce gate [check|ratchet|violations|show]` via [`Program.Main`](file:///cli/src/UI/CodeExplorer/Program.cs#L89) (`cli/src/UI/CodeExplorer/Program.cs`) dispatching [`GateOptions`](file:///cli/src/UI/CodeExplorer/Options/GateOptions.cs#L5) (`cli/src/UI/CodeExplorer/Options/GateOptions.cs`, decorated with `[Verb("gate")]`).

4. **[`WebSocketServerHandler.EvaluateGatesSuiteAsync`](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L670-L760)** (`cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs`)
   - **Direct Calls**:
     - [`engine.CheckSuiteAsync(null, cancellationToken: cancellationToken)`](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L684) (full suite evaluation).
     - [`engine.CheckSuiteAsync(targetGates, cancellationToken: cancellationToken)`](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L695) (single-gate evaluation).
   - **One Hop Upstream Triggers**:
     - **WebSocket Message Types** (handled in [`WebSocketServerHandler.ProcessMessageAsync`](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L451-L463)):
       - `WsMessageTypes.GetGatesRequest` (`"GET_GATES_REQUEST"`) / `"GET_GATES"`
       - `WsMessageTypes.EvaluateGatesRequest` (`"EVALUATE_GATES_REQUEST"`) / `"EVALUATE_GATES"`
     - **HTTP Endpoints** (mapped in [`ServerRouteBuilderExtensions.MapCodeExplorerApi`](file:///cli/src/UI/CodeExplorer/Server/ServerRouteBuilderExtensions.cs#L315-L349)):
       - `GET /api/gates` (line 315; invokes `wsHandler.EvaluateGatesSuiteAsync` at line 326)
       - `POST /api/gates/check` (line 336; invokes `wsHandler.EvaluateGatesSuiteAsync` at line 341)
     - **Internal Server Method**: [`WebSocketServerHandler.InitStarterGatesAsync`](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L819-L833) (line 823), which is triggered by:
       - WebSocket Message Type: `WsMessageTypes.InitGatesRequest` (`"INIT_GATES_REQUEST"`) / `"INIT_GATES"` (lines 474–480)
       - HTTP Endpoint: `POST /api/gates/init` (lines 366–379)

5. **[`WebSocketServerHandler.RatchetGatesSuiteAsync`](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L762-L817)** (`cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs`)
   - **Direct Calls**:
     - [`engine.CheckSuiteAsync(targetGates, cancellationToken: cancellationToken)`](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L783) (evaluates target gates for ratcheting debt calculations).
     - [`engine.CheckSuiteAsync(cancellationToken: cancellationToken)`](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L805) (re-evaluates full suite after saving baselines to refresh the server cache).
   - **One Hop Upstream Triggers**:
     - **WebSocket Message Type** (handled in [`WebSocketServerHandler.ProcessMessageAsync`](file:///cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L465-L472)):
       - `WsMessageTypes.RatchetGatesRequest` (`"RATCHET_GATES_REQUEST"`) / `"RATCHET_GATES"`
     - **HTTP Endpoint** (mapped in [`ServerRouteBuilderExtensions.MapCodeExplorerApi`](file:///cli/src/UI/CodeExplorer/Server/ServerRouteBuilderExtensions.cs#L351-L364)):
       - `POST /api/gates/ratchet` (line 351; invokes `wsHandler.RatchetGatesSuiteAsync` at line 356)

---

## 3. Polyglot `ISemanticExtension` Breakdown

### Exact Implementation Counts by Parser Project

| Parser Project | Exact Count of `ISemanticExtension` Implementations |
| :--- | :---: |
| [`CodeExplorer.Parser.CSharp`](file:///cli/src/Parsers/CodeExplorer.Parser.CSharp/CodeExplorer.Parser.CSharp.csproj) | **25** |
| [`CodeExplorer.Parser.TypeScript`](file:///cli/src/Parsers/CodeExplorer.Parser.TypeScript/CodeExplorer.Parser.TypeScript.csproj) | **51** |
| [`CodeExplorer.Parser.Python`](file:///cli/src/Parsers/CodeExplorer.Parser.Python/CodeExplorer.Parser.Python.csproj) | **18** |
| [`CodeExplorer.Parser.Go`](file:///cli/src/Parsers/CodeExplorer.Parser.Go/CodeExplorer.Parser.Go.csproj) | **19** |
| [`CodeExplorer.Parser.Java`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/CodeExplorer.Parser.Java.csproj) | **9** |
| [`CodeExplorer.Parser.Android`](file:///cli/src/Parsers/CodeExplorer.Parser.Android/CodeExplorer.Parser.Android.csproj) | **0** |

---

### `ISemanticExtension` Implementations in `CodeExplorer.Parser.Java` (9 classes)

1. [`GrpcJavaLibraryParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/GrpcJavaLibraryParser.cs) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/GrpcJavaLibraryParser.cs`)
2. [`HttpClientJavaLibraryParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/HttpClientJavaLibraryParser.cs) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/HttpClientJavaLibraryParser.cs`)
3. [`JdbcTemplateLibraryParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JdbcTemplateLibraryParser.cs) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JdbcTemplateLibraryParser.cs`)
4. [`JpaLibraryParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JpaLibraryParser.cs) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JpaLibraryParser.cs`)
5. [`SpringEventsLibraryParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringEventsLibraryParser.cs) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringEventsLibraryParser.cs`)
6. [`SpringGraphQlLibraryParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringGraphQlLibraryParser.cs) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringGraphQlLibraryParser.cs`)
7. [`SpringKafkaLibraryParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringKafkaLibraryParser.cs) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringKafkaLibraryParser.cs`)
8. [`SpringMvcLibraryParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringMvcLibraryParser.cs) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringMvcLibraryParser.cs`)
9. [`SpringRabbitMqLibraryParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringRabbitMqLibraryParser.cs) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringRabbitMqLibraryParser.cs`)

*(Note: [`SpringFrameworkConfigurationDescriptor`](file:///cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringFrameworkConfigurationDescriptor.cs) in `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringFrameworkConfigurationDescriptor.cs` implements [`ILibraryConfigurationDescriptor`](file:///cli/src/Core/CodeExplorer.Core/Parser/ILibraryConfigurationDescriptor.cs), not `ISemanticExtension`.)*

---

### `ISemanticExtension` Implementations in `CodeExplorer.Parser.Android` (0 classes)

- **None (0 classes)**.
  The `CodeExplorer.Parser.Android` project contains the following components, none of which implement `ISemanticExtension`:
  - [`AndroidManifestFileParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Android/AndroidManifestFileParser.cs) (implements `IFileParser`)
  - [`AndroidManifestParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Android/AndroidManifestParser.cs)
  - [`GradleBuildScriptParser`](file:///cli/src/Parsers/CodeExplorer.Parser.Android/GradleBuildScriptParser.cs)
  - Data transfer & AST record models: [`AndroidManifestInfo`](file:///cli/src/Parsers/CodeExplorer.Parser.Android/AndroidManifestInfo.cs), [`AndroidActivityInfo`](file:///cli/src/Parsers/CodeExplorer.Parser.Android/AndroidManifestInfo.cs), [`AndroidDeepLinkInfo`](file:///cli/src/Parsers/CodeExplorer.Parser.Android/AndroidManifestInfo.cs), and [`GradleBuildInfo`](file:///cli/src/Parsers/CodeExplorer.Parser.Android/GradleBuildScriptParser.cs)