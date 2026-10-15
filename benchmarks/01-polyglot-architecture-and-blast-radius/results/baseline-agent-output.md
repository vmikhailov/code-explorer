Here is the complete architectural and impact analysis for the `code-explorer` repository:

---

## 1. Interface Implementations Across the Codebase

### A. `IComponentLibraryParser` (16 implementations)
Interface declared in [`IComponentLibraryParser.cs`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/IComponentLibraryParser.cs) and registered in [`ComponentLibraryParserRegistry.cs`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/ComponentLibraryParserRegistry.cs):

| # | Class Name | Relative File Path |
| :- | :--- | :--- |
| 1 | [`AngularComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/AngularComponentParser.cs#L13-L197) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/AngularComponentParser.cs` |
| 2 | [`AspNetCoreComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/AspNetCoreComponentParser.cs#L12-L90) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/AspNetCoreComponentParser.cs` |
| 3 | [`DotNetLibraryComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetLibraryComponentParser.cs#L12-L54) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetLibraryComponentParser.cs` |
| 4 | [`DotNetSchedulerComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetSchedulerComponentParser.cs#L12-L77) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetSchedulerComponentParser.cs` |
| 5 | [`DotNetTestComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetTestComponentParser.cs#L13-L119) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetTestComponentParser.cs` |
| 6 | [`DotNetWorkerComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetWorkerComponentParser.cs#L12-L99) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetWorkerComponentParser.cs` |
| 7 | [`ExpressFastifyComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/ExpressFastifyComponentParser.cs#L12-L108) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/ExpressFastifyComponentParser.cs` |
| 8 | [`FastApiComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/FastApiComponentParser.cs#L12-L67) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/FastApiComponentParser.cs` |
| 9 | [`GoTestComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/GoTestComponentParser.cs#L12-L81) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/GoTestComponentParser.cs` |
| 10 | [`JavaScriptTestRunnerComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/JavaScriptTestRunnerComponentParser.cs#L12-L143) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/JavaScriptTestRunnerComponentParser.cs` |
| 11 | [`JavaTestComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/JavaTestComponentParser.cs#L12-L91) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/JavaTestComponentParser.cs` |
| 12 | [`NestJsComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/NestJsComponentParser.cs#L13-L253) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/NestJsComponentParser.cs` |
| 13 | [`NodeWorkerSchedulerComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/NodeWorkerSchedulerComponentParser.cs#L12-L106) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/NodeWorkerSchedulerComponentParser.cs` |
| 14 | [`PythonTestComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/PythonTestComponentParser.cs#L12-L97) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/PythonTestComponentParser.cs` |
| 15 | [`ReactNextComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/ReactNextComponentParser.cs#L12-L163) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/ReactNextComponentParser.cs` |
| 16 | [`YarpGatewayComponentParser`](../../../cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/YarpGatewayComponentParser.cs#L12-L82) | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/YarpGatewayComponentParser.cs` |

---

### B. `IMcpTargetAdapter` (8 implementations)
Interface declared in [`IMcpTargetAdapter.cs`](../../../cli/src/UI/CodeExplorer/Configuration/IMcpTargetAdapter.cs) and registered in [`ToolDetector.cs`](../../../cli/src/UI/CodeExplorer/Configuration/ToolDetector.cs):

| # | Class Name | Relative File Path |
| :- | :--- | :--- |
| 1 | [`AntigravityAdapter`](../../../cli/src/UI/CodeExplorer/Configuration/Adapters/AntigravityAdapter.cs#L5-L34) | `cli/src/UI/CodeExplorer/Configuration/Adapters/AntigravityAdapter.cs` |
| 2 | [`ClaudeCodeAdapter`](../../../cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeCodeAdapter.cs#L5-L36) | `cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeCodeAdapter.cs` |
| 3 | [`ClaudeDesktopAdapter`](../../../cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeDesktopAdapter.cs#L5-L45) | `cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeDesktopAdapter.cs` |
| 4 | [`ClineAdapter`](../../../cli/src/UI/CodeExplorer/Configuration/Adapters/ClineAdapter.cs#L5-L46) | `cli/src/UI/CodeExplorer/Configuration/Adapters/ClineAdapter.cs` |
| 5 | [`CursorAdapter`](../../../cli/src/UI/CodeExplorer/Configuration/Adapters/CursorAdapter.cs#L5-L36) | `cli/src/UI/CodeExplorer/Configuration/Adapters/CursorAdapter.cs` |
| 6 | [`VsCodeAdapter`](../../../cli/src/UI/CodeExplorer/Configuration/Adapters/VsCodeAdapter.cs#L6-L52) | `cli/src/UI/CodeExplorer/Configuration/Adapters/VsCodeAdapter.cs` |
| 7 | [`WindsurfAdapter`](../../../cli/src/UI/CodeExplorer/Configuration/Adapters/WindsurfAdapter.cs#L5-L36) | `cli/src/UI/CodeExplorer/Configuration/Adapters/WindsurfAdapter.cs` |
| 8 | [`ZedAdapter`](../../../cli/src/UI/CodeExplorer/Configuration/Adapters/ZedAdapter.cs#L6-L58) | `cli/src/UI/CodeExplorer/Configuration/Adapters/ZedAdapter.cs` |

---

### C. `ILibraryConfigurationDescriptor` (4 implementations)
Interface declared in [`ILibraryConfigurationDescriptor.cs`](../../../cli/src/Core/CodeExplorer.Core/Parser/ILibraryConfigurationDescriptor.cs) and registered in [`LibraryConfigurationRegistry.cs`](../../../cli/src/Core/CodeExplorer.Core/Parser/LibraryConfigurationRegistry.cs) / [`JavaProjectParser.cs`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/JavaProjectParser.cs#L18-L21):

| # | Class Name | Relative File Path |
| :- | :--- | :--- |
| 1 | [`DatabaseConfigurationDescriptor`](../../../cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs#L9-L80) | `cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs` |
| 2 | [`MessageBrokerConfigurationDescriptor`](../../../cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs#L85-L136) | `cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs` |
| 3 | [`CloudServicesConfigurationDescriptor`](../../../cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs#L141-L184) | `cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs` |
| 4 | [`SpringFrameworkConfigurationDescriptor`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringFrameworkConfigurationDescriptor.cs#L11-L58) | `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringFrameworkConfigurationDescriptor.cs` |

---

### D. `ISyntaxEnricher` (3 implementations)
Interface declared in [`ISyntaxEnricher.cs`](../../../cli/src/Core/CodeExplorer.Core/Parser/ISyntaxEnricher.cs):

| # | Class Name | Relative File Path |
| :- | :--- | :--- |
| 1 | [`SyntaxEnricher`](../../../cli/src/Core/CodeExplorer.Core/Parser/SyntaxEnricher.cs#L13-L662) | `cli/src/Core/CodeExplorer.Core/Parser/SyntaxEnricher.cs` |
| 2 | [`SqlSyntaxEnricher`](../../../cli/src/Parsers/CodeExplorer.Parser.SQL/SqlSyntaxEnricher.cs#L9-L66) | `cli/src/Parsers/CodeExplorer.Parser.SQL/SqlSyntaxEnricher.cs` |
| 3 | [`ColdFusionSyntaxEnricher`](../../../cli/src/Parsers/CodeExplorer.Parser.ColdFusion/ColdFusionSyntaxEnricher.cs#L10-L56) | `cli/src/Parsers/CodeExplorer.Parser.ColdFusion/ColdFusionSyntaxEnricher.cs` |

---

## 2. Blast Radius & Call Chain of `GateExecutionEngine`

[`GateExecutionEngine`](../../../cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs#L14-L163) (`cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs`) exposes:
- [`CheckGateAsync(SemanticGate gate, CancellationToken cancellationToken = default)`](../../../cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs#L22-L71)
- [`CheckSuiteAsync(...)`](../../../cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs#L76-L115) (which internally calls `this.CheckGateAsync(gate, cancellationToken)` at [line 101](../../../cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs#L101))

### Production (Non-Test) Direct Callers & 1-Hop Upstream Triggers

#### 1. [`GateCommandHandler.HandleAsync(GateOptions opts)`](../../../cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L17-L250)
- **File**: `cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs`
- **Direct Calls**:
  - `engine.CheckSuiteAsync(...)`:
    - [Line 145](../../../cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L145) — when `action == "ratchet"`
    - [Line 202](../../../cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L202) — when `action is "violations" or "show"` and `opts.GateId` is empty
    - [Line 238](../../../cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L238) — when `action == "check"` and comma-separated `opts.GateId` values are provided
    - [Line 242](../../../cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L242) — when `action == "check"` and `opts.GateId` is empty
  - `engine.CheckGateAsync(...)`:
    - [Line 196](../../../cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L196) — when `action is "violations" or "show"` for each specified `GateId`
    - [Line 222](../../../cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs#L222) — when `action == "check"` and a single `opts.GateId` is provided
- **1 Hop Upstream**:
  - **CLI Command**: `ce gate` (`[Verb("gate")]` on [`GateOptions`](../../../cli/src/UI/CodeExplorer/Options/GateOptions.cs#L6-L56) in `cli/src/UI/CodeExplorer/Options/GateOptions.cs` for subcommands `ce gate check` [default], `ce gate ratchet`, `ce gate violations`, and `ce gate show`), dispatched by [`Program.Main(string[] args)`](../../../cli/src/UI/CodeExplorer/Program.cs#L89) (`cli/src/UI/CodeExplorer/Program.cs`, line 89).

#### 2. [`CodeExplorerRepository.CheckSemanticGatesAsync(...)`](../../../cli/src/Core/CodeExplorer.Core/Mcp/CodeExplorerRepository.cs#L1878-L1932)
- **File**: `cli/src/Core/CodeExplorer.Core/Mcp/CodeExplorerRepository.cs`
- **Direct Calls**:
  - `engine.CheckGateAsync(singleGate, cancellationToken)` at [line 1920](../../../cli/src/Core/CodeExplorer.Core/Mcp/CodeExplorerRepository.cs#L1920) (when `gateId` is specified)
  - `engine.CheckSuiteAsync(null, scopeFilter, dimFilter, categoryFilter, platformFilter, cancellationToken)` at [lines 1924–1930](../../../cli/src/Core/CodeExplorer.Core/Mcp/CodeExplorerRepository.cs#L1924-L1930) (when `gateId` is null/whitespace)
- **1 Hop Upstream**:
  - **MCP Tool**: `check_semantic_gates` — [`McpGraphHandler.CheckSemanticGatesAsync(...)`](../../../cli/src/Core/CodeExplorer.Core/Mcp/McpGraphHandler.cs#L652-L667) (`cli/src/Core/CodeExplorer.Core/Mcp/McpGraphHandler.cs`, line 666), decorated with `[McpServerTool]`.

#### 3. [`WebSocketServerHandler.EvaluateGatesSuiteAsync(...)`](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L670-L760) & [`WebSocketServerHandler.RatchetGatesSuiteAsync(...)`](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L762-L817)
- **File**: `cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs`
- **Direct Calls**:
  - In `EvaluateGatesSuiteAsync(string? gateId = null, CancellationToken cancellationToken = default, bool force = false)`:
    - `engine.CheckSuiteAsync(null, cancellationToken: cancellationToken)` at [line 684](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L684) (full suite evaluation)
    - `engine.CheckSuiteAsync(targetGates, cancellationToken: cancellationToken)` at [line 695](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L695) (targeted gate evaluation)
  - In `RatchetGatesSuiteAsync(string? gateId = null, bool updateAll = false, CancellationToken cancellationToken = default)`:
    - `engine.CheckSuiteAsync(targetGates, cancellationToken: cancellationToken)` at [line 783](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L783) (evaluate before updating baseline)
    - `engine.CheckSuiteAsync(cancellationToken: cancellationToken)` at [line 805](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L805) (refresh suite after saving baseline)
  - *(Note: [`WebSocketServerHandler.InitStarterGatesAsync(...)`](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L819-L833) also delegates to `EvaluateGatesSuiteAsync(null, cancellationToken, force: true)` at [line 823](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L823)).*
- **1 Hop Upstream**:
  - **WebSocket Message Types** (dispatched in [`WebSocketServerHandler.ProcessMessageAsync`](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L451-L481), constants defined in [`WsMessageTypes`](../../../cli/src/Core/CodeExplorer.Core/Protocol/ProtocolDtos.cs#L31-L39)):
    - `"GET_GATES"` (`WsMessageTypes.GetGatesRequest`) $\rightarrow$ `EvaluateGatesSuiteAsync` ([line 451 / 458](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L451-L458))
    - `"EVALUATE_GATES"` (`WsMessageTypes.EvaluateGatesRequest`) $\rightarrow$ `EvaluateGatesSuiteAsync` ([line 452 / 458](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L452-L458))
    - `"RATCHET_GATES"` (`WsMessageTypes.RatchetGatesRequest`) $\rightarrow$ `RatchetGatesSuiteAsync` ([line 462 / 470](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L462-L470))
    - `"INIT_GATES"` (`WsMessageTypes.InitGatesRequest`) $\rightarrow$ `InitStarterGatesAsync` $\rightarrow$ `EvaluateGatesSuiteAsync` ([line 474 / 479](../../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs#L474-L479))
  - **HTTP REST Endpoints** (registered via `app.MapCodeExplorerApi(...)` in [`ServeCommandHandler.CreateWebApplication`](../../../cli/src/UI/CodeExplorer/Commands/ServeCommandHandler.cs#L152) when running `ce serve` and consumed by [`GateStateManager`](../../../vscode-extension/src/gateStateManager.ts#L132-L361)):
    - `GET /api/gates` (when query parameter `evaluate=true` is passed) $\rightarrow$ `EvaluateGatesSuiteAsync`
    - `POST /api/gates/check` $\rightarrow$ `EvaluateGatesSuiteAsync`
    - `POST /api/gates/ratchet` $\rightarrow$ `RatchetGatesSuiteAsync`
    - `POST /api/gates/init` $\rightarrow$ `InitStarterGatesAsync` $\rightarrow$ `EvaluateGatesSuiteAsync`

---

## 3. Polyglot `ISemanticExtension` Breakdown

### A. Exact Counts per Parser Project

| Parser Project | `ISemanticExtension` Count | Registration File |
| :--- | :---: | :--- |
| `CodeExplorer.Parser.CSharp` | **25** | [`CSharpParser.cs`](../../../cli/src/Parsers/CodeExplorer.Parser.CSharp/CSharpParser.cs#L26-L53) |
| `CodeExplorer.Parser.TypeScript` | **51** | [`TypeScriptParser.cs`](../../../cli/src/Parsers/CodeExplorer.Parser.TypeScript/TypeScriptParser.cs#L32-L85) |
| `CodeExplorer.Parser.Python` | **18** | [`PythonParser.cs`](../../../cli/src/Parsers/CodeExplorer.Parser.Python/PythonParser.cs#L51-L71) |
| `CodeExplorer.Parser.Go` | **19** | [`GoParser.cs`](../../../cli/src/Parsers/CodeExplorer.Parser.Go/GoParser.cs#L25-L46) |
| `CodeExplorer.Parser.Java` | **9** | [`JavaParser.cs`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/JavaParser.cs#L41-L52) |
| `CodeExplorer.Parser.Android` | **0** | [`AndroidManifestFileParser.cs`](../../../cli/src/Parsers/CodeExplorer.Parser.Android/AndroidManifestFileParser.cs#L17) (`SemanticExtensions => []`) |

---

### B. Exact Class Names in `CodeExplorer.Parser.Java` (9 classes)
All 9 classes are declared in `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/` and registered in [`JavaParser.cs`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/JavaParser.cs#L41-L52):
1. [`GrpcJavaLibraryParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/GrpcJavaLibraryParser.cs#L13-L108) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/GrpcJavaLibraryParser.cs`)
2. [`HttpClientJavaLibraryParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/HttpClientJavaLibraryParser.cs#L13-L131) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/HttpClientJavaLibraryParser.cs`)
3. [`JdbcTemplateLibraryParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JdbcTemplateLibraryParser.cs#L12-L77) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JdbcTemplateLibraryParser.cs`)
4. [`JpaLibraryParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JpaLibraryParser.cs#L12-L67) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JpaLibraryParser.cs`)
5. [`SpringEventsLibraryParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringEventsLibraryParser.cs#L13-L94) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringEventsLibraryParser.cs`)
6. [`SpringGraphQlLibraryParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringGraphQlLibraryParser.cs#L13-L92) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringGraphQlLibraryParser.cs`)
7. [`SpringKafkaLibraryParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringKafkaLibraryParser.cs#L13-L120) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringKafkaLibraryParser.cs`)
8. [`SpringMvcLibraryParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringMvcLibraryParser.cs#L13-L141) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringMvcLibraryParser.cs`)
9. [`SpringRabbitMqLibraryParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringRabbitMqLibraryParser.cs#L13-L115) (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringRabbitMqLibraryParser.cs`)

---

### C. Exact Class Names in `CodeExplorer.Parser.Android` (0 classes)
- **`CodeExplorer.Parser.Android` has `0` classes implementing `ISemanticExtension`**.
- Specifically, `CodeExplorer.Parser.Android` contains only [`AndroidManifestFileParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Android/AndroidManifestFileParser.cs#L11-L141) (which implements `IFileParser` with `UsesTreeSitter => false` and `public IReadOnlyList<ISemanticExtension> SemanticExtensions => [];` at [line 17](../../../cli/src/Parsers/CodeExplorer.Parser.Android/AndroidManifestFileParser.cs#L17)), [`AndroidManifestParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Android/AndroidManifestParser.cs#L5-L193), [`AndroidManifestInfo`](../../../cli/src/Parsers/CodeExplorer.Parser.Android/AndroidManifestInfo.cs#L3-L26), and [`GradleBuildScriptParser`](../../../cli/src/Parsers/CodeExplorer.Parser.Android/GradleBuildScriptParser.cs#L23-L243).
- *(Note for completeness: Android-ecosystem `ISemanticExtension` implementations live in `CodeExplorer.Parser.Kotlin` under `cli/src/Parsers/CodeExplorer.Parser.Kotlin/Libraries/`, which contains 14 `ISemanticExtension` classes in total: `AndroidPlatformLibraryParser`, `FirebaseMessagingLibraryParser`, `NavigationComposeLibraryParser`, `RetrofitLibraryParser`, `RoomLibraryParser`, `WorkManagerLibraryParser`, `ExposedLibraryParser`, `GrpcKotlinLibraryParser`, `KafkaKotlinLibraryParser`, `KtorClientLibraryParser`, `KtorServerLibraryParser`, `OkHttpLibraryParser`, `SpringBootKotlinLibraryParser`, and `SqlDelightLibraryParser`.)*
