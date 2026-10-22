# Architectural & Impact Analysis Report

### 1. Interface Implementations across the Codebase

#### `IComponentLibraryParser` (16 Implementations)
All implementations are declared under `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/`:
1. [`AngularComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/AngularComponentParser.cs) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/AngularComponentParser.cs`
2. [`AspNetCoreComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/AspNetCoreComponentParser.cs) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/AspNetCoreComponentParser.cs`
3. [`DotNetLibraryComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetLibraryComponentParser.cs) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetLibraryComponentParser.cs`
4. [`DotNetSchedulerComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetSchedulerComponentParser.cs) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetSchedulerComponentParser.cs`
5. [`DotNetTestComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetTestComponentParser.cs) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetTestComponentParser.cs`
6. [`DotNetWorkerComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetWorkerComponentParser.cs) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetWorkerComponentParser.cs`
7. [`ExpressFastifyComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/ExpressFastifyComponentParser.cs) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/ExpressFastifyComponentParser.cs`
8. [`FastApiComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/FastApiComponentParser.cs) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/FastApiComponentParser.cs`
9. [`GoTestComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/GoTestComponentParser.cs) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/GoTestComponentParser.cs`
10. [`JavaScriptTestRunnerComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/JavaScriptTestRunnerComponentParser.cs) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/JavaScriptTestRunnerComponentParser.cs`
11. [`JavaTestComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/JavaTestComponentParser.cs) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/JavaTestComponentParser.cs`
12. [`NestJsComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/NestJsComponentParser.cs) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/NestJsComponentParser.cs`
13. [`NodeWorkerSchedulerComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/NodeWorkerSchedulerComponentParser.cs) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/NodeWorkerSchedulerComponentParser.cs`
14. [`PythonTestComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/PythonTestComponentParser.cs) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/PythonTestComponentParser.cs`
15. [`ReactNextComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/ReactNextComponentParser.cs) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/ReactNextComponentParser.cs`
16. [`YarpGatewayComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/YarpGatewayComponentParser.cs) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/YarpGatewayComponentParser.cs`

#### `IMcpTargetAdapter` (8 Implementations)
All implementations are declared under `cli/src/UI/CodeExplorer/Configuration/Adapters/`:
1. [`AntigravityAdapter`](../../../cli/src/UI/CodeExplorer/Configuration/Adapters/AntigravityAdapter.cs) &mdash; `cli/src/UI/CodeExplorer/Configuration/Adapters/AntigravityAdapter.cs`
2. [`ClaudeCodeAdapter`](../../../cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeCodeAdapter.cs) &mdash; `cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeCodeAdapter.cs`
3. [`ClaudeDesktopAdapter`](../../../cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeDesktopAdapter.cs) &mdash; `cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeDesktopAdapter.cs`
4. [`ClineAdapter`](../../../cli/src/UI/CodeExplorer/Configuration/Adapters/ClineAdapter.cs) &mdash; `cli/src/UI/CodeExplorer/Configuration/Adapters/ClineAdapter.cs`
5. [`CursorAdapter`](../../../cli/src/UI/CodeExplorer/Configuration/Adapters/CursorAdapter.cs) &mdash; `cli/src/UI/CodeExplorer/Configuration/Adapters/CursorAdapter.cs`
6. [`VsCodeAdapter`](../../../cli/src/UI/CodeExplorer/Configuration/Adapters/VsCodeAdapter.cs) &mdash; `cli/src/UI/CodeExplorer/Configuration/Adapters/VsCodeAdapter.cs`
7. [`WindsurfAdapter`](../../../cli/src/UI/CodeExplorer/Configuration/Adapters/WindsurfAdapter.cs) &mdash; `cli/src/UI/CodeExplorer/Configuration/Adapters/WindsurfAdapter.cs`
8. [`ZedAdapter`](../../../cli/src/UI/CodeExplorer/Configuration/Adapters/ZedAdapter.cs) &mdash; `cli/src/UI/CodeExplorer/Configuration/Adapters/ZedAdapter.cs`

#### `ILibraryConfigurationDescriptor` (4 Implementations)
1. [`DatabaseConfigurationDescriptor`](../../../cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs#L9) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs`
2. [`MessageBrokerConfigurationDescriptor`](../../../cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs#L85) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs`
3. [`CloudServicesConfigurationDescriptor`](../../../cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs#L141) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs`
4. [`SpringFrameworkConfigurationDescriptor`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringFrameworkConfigurationDescriptor.cs#L11) &mdash; `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringFrameworkConfigurationDescriptor.cs`

#### `ISyntaxEnricher` (3 Implementations)
1. [`SyntaxEnricher`](../../../cli/src/Core/CodeExplorer.Core/Parser/SyntaxEnricher.cs#L15) &mdash; `cli/src/Core/CodeExplorer.Core/Parser/SyntaxEnricher.cs`
2. [`ColdFusionSyntaxEnricher`](../../../cli/src/Parsers/CodeExplorer.Parser.ColdFusion/ColdFusionSyntaxEnricher.cs#L10) &mdash; `cli/src/Parsers/CodeExplorer.Parser.ColdFusion/ColdFusionSyntaxEnricher.cs`
3. [`SqlSyntaxEnricher`](../../../cli/src/Parsers/CodeExplorer.Parser.SQL/SqlSyntaxEnricher.cs#L9) &mdash; `cli/src/Parsers/CodeExplorer.Parser.SQL/SqlSyntaxEnricher.cs`

---

### 2. Blast Radius & Call Chain of `GateExecutionEngine`

The production (non-test) classes and methods directly calling [`CheckGateAsync`](../../../cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs#L23) or [`CheckSuiteAsync`](../../../cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs#L69) on [`GateExecutionEngine`](../../../cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs), along with their one-hop upstream triggers:

1. **Class**: [`CodeExplorer.Commands.GateCommandHandler`](../../../cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs)
   - **Method**: [`HandleAsync(GateOptions opts)`](../../../cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L17) (`cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs`)
   - **Direct Calls**:
     - `engine.CheckGateAsync(...)` (lines 196, 222)
     - `engine.CheckSuiteAsync(...)` (lines 145, 202, 238, 242)
   - **One Hop Upstream Trigger**:
     - **CLI Command**: `ce gate` (subcommands: `check`, `ratchet`, `violations`, `show`), routed from [`Program.Main / Program.HandleAsync`](../../../cli/src/UI/CodeExplorer/Program.cs#L92) via `GateOptions`.

2. **Class**: [`CodeExplorer.Core.Mcp.CodeExplorerRepository`](../../../cli/src/Core/CodeExplorer.Core/Mcp/CodeExplorerRepository.cs)
   - **Method**: [`CheckSemanticGatesAsync(...)`](../../../cli/src/Core/CodeExplorer.Core/Mcp/CodeExplorerRepository.cs#L1996) (`cli/src/Core/CodeExplorer.Core/Mcp/CodeExplorerRepository.cs`)
   - **Direct Calls**:
     - `engine.CheckGateAsync(singleGate, cancellationToken)` (line 2038)
     - `engine.CheckSuiteAsync(...)` (line 2042)
   - **One Hop Upstream Trigger**:
     - **MCP Tool**: `CheckSemanticGatesAsync` (attribute `[McpServerTool]` on method [`McpGraphHandler.CheckSemanticGatesAsync`](../../../cli/src/Core/CodeExplorer.Core/Mcp/McpGraphHandler.cs#L670) in `cli/src/Core/CodeExplorer.Core/Mcp/McpGraphHandler.cs`).

3. **Class**: [`CodeExplorer.Server.WebSocketServerHandler`](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs)
   - **Method**: [`EvaluateGatesSuiteAsync(string? gateId, CancellationToken cancellationToken, bool force)`](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L670) (`cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs`)
   - **Direct Calls**:
     - `engine.CheckSuiteAsync(...)` (lines 684, 695)
   - **One Hop Upstream Triggers**:
     - **HTTP Endpoints**:
       - `GET /api/gates` (in [`ServerRouteBuilderExtensions.cs`](../../../cli/src/UI/CodeExplorer/Server/ServerRouteBuilderExtensions.cs#L315))
       - `POST /api/gates/check` (in [`ServerRouteBuilderExtensions.cs`](../../../cli/src/UI/CodeExplorer/Server/ServerRouteBuilderExtensions.cs#L336))
       - `POST /api/gates/init` (in [`ServerRouteBuilderExtensions.cs`](../../../cli/src/UI/CodeExplorer/Server/ServerRouteBuilderExtensions.cs#L366), calls [`InitStarterGatesAsync`](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L819) which triggers `EvaluateGatesSuiteAsync`)
     - **WebSocket Message Types**:
       - `WsMessageTypes.GetGatesRequest` (`"GET_GATES_REQUEST"` / `"GET_GATES"`) ([`WebSocketServerHandler.cs`](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L451))
       - `WsMessageTypes.EvaluateGatesRequest` (`"EVALUATE_GATES_REQUEST"` / `"EVALUATE_GATES"`) ([`WebSocketServerHandler.cs`](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L453))
       - `WsMessageTypes.InitGatesRequest` (`"INIT_GATES_REQUEST"` / `"INIT_GATES"`) (via [`InitStarterGatesAsync`](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L474))

4. **Class**: [`CodeExplorer.Server.WebSocketServerHandler`](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs)
   - **Method**: [`RatchetGatesSuiteAsync(string? gateId, bool updateAll, CancellationToken cancellationToken)`](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L762) (`cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs`)
   - **Direct Calls**:
     - `engine.CheckSuiteAsync(...)` (lines 783, 805)
   - **One Hop Upstream Triggers**:
     - **HTTP Endpoint**: `POST /api/gates/ratchet` (in [`ServerRouteBuilderExtensions.cs`](../../../cli/src/UI/CodeExplorer/Server/ServerRouteBuilderExtensions.cs#L351))
     - **WebSocket Message Type**: `WsMessageTypes.RatchetGatesRequest` (`"RATCHET_GATES_REQUEST"` / `"RATCHET_GATES"`) ([`WebSocketServerHandler.cs`](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L465))

5. **Internal Engine Call**:
   - **Class**: [`CodeExplorer.Core.Gates.Engine.GateExecutionEngine`](../../../cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs)
   - **Method**: [`CheckSuiteAsync(...)`](../../../cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs#L69)
   - **Direct Call**: `CheckGateAsync(gate, cancellationToken)` (line 101) inside the evaluation loop.
   - **Upstream Triggers**: The upstream callers of `CheckSuiteAsync` listed above.

---

### 3. Polyglot `ISemanticExtension` Breakdown

#### Class Counts by Parser Project:
| Parser Project | Count |
| :--- | :--- |
| `CodeExplorer.Parser.CSharp` | **25** |
| `CodeExplorer.Parser.TypeScript` | **51** |
| `CodeExplorer.Parser.Python` | **18** |
| `CodeExplorer.Parser.Go` | **19** |
| `CodeExplorer.Parser.Java` | **9** |
| `CodeExplorer.Parser.Android` | **0** |

#### Implementations in `CodeExplorer.Parser.Java` (9 Classes):
All declared in `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/`:
1. [`GrpcJavaLibraryParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/GrpcJavaLibraryParser.cs) &mdash; `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/GrpcJavaLibraryParser.cs`
2. [`HttpClientJavaLibraryParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/HttpClientJavaLibraryParser.cs) &mdash; `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/HttpClientJavaLibraryParser.cs`
3. [`JdbcTemplateLibraryParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JdbcTemplateLibraryParser.cs) &mdash; `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JdbcTemplateLibraryParser.cs`
4. [`JpaLibraryParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JpaLibraryParser.cs) &mdash; `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JpaLibraryParser.cs`
5. [`SpringEventsLibraryParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringEventsLibraryParser.cs) &mdash; `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringEventsLibraryParser.cs`
6. [`SpringGraphQlLibraryParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringGraphQlLibraryParser.cs) &mdash; `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringGraphQlLibraryParser.cs`
7. [`SpringKafkaLibraryParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringKafkaLibraryParser.cs) &mdash; `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringKafkaLibraryParser.cs`
8. [`SpringMvcLibraryParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringMvcLibraryParser.cs) &mdash; `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringMvcLibraryParser.cs`
9. [`SpringRabbitMqLibraryParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringRabbitMqLibraryParser.cs) &mdash; `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringRabbitMqLibraryParser.cs`

#### Implementations in `CodeExplorer.Parser.Android` (0 Classes):
- **None** (`0` implementations).
- The project `CodeExplorer.Parser.Android` contains [`AndroidManifestFileParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Android/AndroidManifestFileParser.cs) (implements `IFileParser`), `AndroidManifestInfo`, `AndroidManifestParser`, `GradleBuildInfo`, and `GradleBuildScriptParser`, none of which implement `ISemanticExtension` (the parser exposes `SemanticExtensions => []`).
