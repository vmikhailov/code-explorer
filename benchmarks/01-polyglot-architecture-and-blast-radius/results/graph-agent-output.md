Here is the complete architectural and impact analysis for the `code-explorer` repository:

---

## 1. Interface Implementations across the Codebase

### `IComponentLibraryParser` (16 classes)
| Class Name | Relative File Path |
| :--- | :--- |
| `AngularComponentParser` | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/AngularComponentParser.cs` |
| `AspNetCoreComponentParser` | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/AspNetCoreComponentParser.cs` |
| `DotNetLibraryComponentParser` | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetLibraryComponentParser.cs` |
| `DotNetSchedulerComponentParser` | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetSchedulerComponentParser.cs` |
| `DotNetTestComponentParser` | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetTestComponentParser.cs` |
| `DotNetWorkerComponentParser` | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/DotNetWorkerComponentParser.cs` |
| `ExpressFastifyComponentParser` | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/ExpressFastifyComponentParser.cs` |
| `FastApiComponentParser` | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/FastApiComponentParser.cs` |
| `GoTestComponentParser` | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/GoTestComponentParser.cs` |
| `JavaScriptTestRunnerComponentParser` | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/JavaScriptTestRunnerComponentParser.cs` |
| `JavaTestComponentParser` | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/JavaTestComponentParser.cs` |
| `NestJsComponentParser` | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/NestJsComponentParser.cs` |
| `NodeWorkerSchedulerComponentParser` | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/NodeWorkerSchedulerComponentParser.cs` |
| `PythonTestComponentParser` | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/PythonTestComponentParser.cs` |
| `ReactNextComponentParser` | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/ReactNextComponentParser.cs` |
| `YarpGatewayComponentParser` | `cli/src/Core/CodeExplorer.Core/Parser/Components/Parsers/YarpGatewayComponentParser.cs` |

### `IMcpTargetAdapter` (8 classes)
| Class Name | Relative File Path |
| :--- | :--- |
| `AntigravityAdapter` | `cli/src/UI/CodeExplorer/Configuration/Adapters/AntigravityAdapter.cs` |
| `ClaudeCodeAdapter` | `cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeCodeAdapter.cs` |
| `ClaudeDesktopAdapter` | `cli/src/UI/CodeExplorer/Configuration/Adapters/ClaudeDesktopAdapter.cs` |
| `ClineAdapter` | `cli/src/UI/CodeExplorer/Configuration/Adapters/ClineAdapter.cs` |
| `CursorAdapter` | `cli/src/UI/CodeExplorer/Configuration/Adapters/CursorAdapter.cs` |
| `VsCodeAdapter` | `cli/src/UI/CodeExplorer/Configuration/Adapters/VsCodeAdapter.cs` |
| `WindsurfAdapter` | `cli/src/UI/CodeExplorer/Configuration/Adapters/WindsurfAdapter.cs` |
| `ZedAdapter` | `cli/src/UI/CodeExplorer/Configuration/Adapters/ZedAdapter.cs` |

### `ILibraryConfigurationDescriptor` (4 classes)
| Class Name | Relative File Path |
| :--- | :--- |
| `CloudServicesConfigurationDescriptor` | `cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs` |
| `DatabaseConfigurationDescriptor` | `cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs` |
| `MessageBrokerConfigurationDescriptor` | `cli/src/Core/CodeExplorer.Core/Parser/StandardConfigurationDescriptors.cs` |
| `SpringFrameworkConfigurationDescriptor` | `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringFrameworkConfigurationDescriptor.cs` |

### `ISyntaxEnricher` (3 classes)
| Class Name | Relative File Path |
| :--- | :--- |
| `ColdFusionSyntaxEnricher` | `cli/src/Parsers/CodeExplorer.Parser.ColdFusion/ColdFusionSyntaxEnricher.cs` |
| `SqlSyntaxEnricher` | `cli/src/Parsers/CodeExplorer.Parser.SQL/SqlSyntaxEnricher.cs` |
| `SyntaxEnricher` | `cli/src/Core/CodeExplorer.Core/Parser/SyntaxEnricher.cs` |

---

## 2. Blast Radius & Call Chain of `GateExecutionEngine`

Target methods on `GateExecutionEngine` (`cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs`):
- `CheckGateAsync` (line 23)
- `CheckSuiteAsync` (line 69)

### Production (Non-Test) Direct Callers & One-Hop Upstream Triggers

1. **`GateExecutionEngine.CheckSuiteAsync`** (`cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs`, line 69)
   - **Direct Call**: Calls `CheckGateAsync` internally (line 101) for each gate in the filtered suite.
   - **One Hop Upstream**: Called by `CodeExplorerRepository.CheckSemanticGatesAsync`, `GateCommandHandler.HandleAsync`, `WebSocketServerHandler.EvaluateGatesSuiteAsync`, and `WebSocketServerHandler.RatchetGatesSuiteAsync` (detailed below).

2. **`CodeExplorerRepository.CheckSemanticGatesAsync`** (`cli/src/Core/CodeExplorer.Core/Mcp/CodeExplorerRepository.cs`, line 1878)
   - **Direct Calls**:
     - `engine.CheckGateAsync` (line 1920) when evaluating a single `gateId`.
     - `engine.CheckSuiteAsync` (line 1924) when evaluating a suite of gates.
   - **One Hop Upstream (MCP Tool)**:
     - **`McpGraphHandler.CheckSemanticGatesAsync`** (`cli/src/Core/CodeExplorer.Core/Mcp/McpGraphHandler.cs`, line 649), decorated with `[McpServerTool]` (MCP tool `check_semantic_gates` / `CheckSemanticGatesAsync`).

3. **`GateCommandHandler.HandleAsync`** (`cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs`, line 17)
   - **Direct Calls**:
     - `engine.CheckGateAsync` at line 196 (`action is "violations" or "show"` with `--gate`) and line 222 (`action == "check"` with a single `--gate` ID).
     - `engine.CheckSuiteAsync` at line 145 (`action == "ratchet"`), line 202 (`action is "violations" or "show"` without `--gate`), line 238 (`action == "check"` with multiple comma-separated `--gate` IDs), and line 242 (`action == "check"` for the full suite).
   - **One Hop Upstream (CLI Command)**:
     - **`Program.Main`** (`cli/src/UI/CodeExplorer/Program.cs`, line 16, dispatch at line 89) via `GateOptions` (`cli/src/UI/CodeExplorer/Options/GateOptions.cs`, `[Verb("gate")]`): CLI command `ce gate [check|ratchet|violations|show]`.

4. **`WebSocketServerHandler.EvaluateGatesSuiteAsync`** (`cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs`, line 670)
   - **Direct Calls**:
     - `engine.CheckSuiteAsync` at line 684 (full suite evaluation) and line 695 (single-gate evaluation).
   - **One Hop Upstream**:
     - **WebSocket Message Types** (handled in `WebSocketServerHandler.ProcessMessageAsync`, `cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs`, line 192, at lines 451–463):
       - `WsMessageTypes.GetGatesRequest` (`"GET_GATES_REQUEST"`) / `"GET_GATES"`
       - `WsMessageTypes.EvaluateGatesRequest` (`"EVALUATE_GATES_REQUEST"`) / `"EVALUATE_GATES"`
     - **HTTP Endpoints** (mapped in `ServerRouteBuilderExtensions.MapCodeExplorerApi`, `cli/src/UI/CodeExplorer/Server/ServerRouteBuilderExtensions.cs`):
       - `GET /api/gates` (line 315; invokes `wsHandler.EvaluateGatesSuiteAsync` at line 326)
       - `POST /api/gates/check` (line 336; invokes `wsHandler.EvaluateGatesSuiteAsync` at line 341)
     - **Internal Server Caller `WebSocketServerHandler.InitStarterGatesAsync`** (`cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs`, line 819; invokes `EvaluateGatesSuiteAsync` at line 823), which is triggered by:
       - **WebSocket Message Type**: `WsMessageTypes.InitGatesRequest` (`"INIT_GATES_REQUEST"`) / `"INIT_GATES"` (in `WebSocketServerHandler.ProcessMessageAsync`, lines 474–480)
       - **HTTP Endpoint**: `POST /api/gates/init` (in `ServerRouteBuilderExtensions.MapCodeExplorerApi`, lines 366–379)

5. **`WebSocketServerHandler.RatchetGatesSuiteAsync`** (`cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs`, line 762)
   - **Direct Calls**:
     - `engine.CheckSuiteAsync` at line 783 (evaluates target gates for ratcheting) and line 805 (re-evaluates the full suite to refresh cache after updating baselines).
   - **One Hop Upstream**:
     - **WebSocket Message Type** (handled in `WebSocketServerHandler.ProcessMessageAsync`, `cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.cs`, line 192, at lines 465–472):
       - `WsMessageTypes.RatchetGatesRequest` (`"RATCHET_GATES_REQUEST"`) / `"RATCHET_GATES"`
     - **HTTP Endpoint** (mapped in `ServerRouteBuilderExtensions.MapCodeExplorerApi`, `cli/src/UI/CodeExplorer/Server/ServerRouteBuilderExtensions.cs`):
       - `POST /api/gates/ratchet` (line 351; invokes `wsHandler.RatchetGatesSuiteAsync` at line 356)

---

## 3. Polyglot `ISemanticExtension` Breakdown

### Implementation Counts by Parser Project
| Parser Project | Exact Count of `ISemanticExtension` Implementations |
| :--- | :---: |
| `CodeExplorer.Parser.CSharp` | **25** |
| `CodeExplorer.Parser.TypeScript` | **51** |
| `CodeExplorer.Parser.Python` | **18** |
| `CodeExplorer.Parser.Go` | **19** |
| `CodeExplorer.Parser.Java` | **9** |
| `CodeExplorer.Parser.Android` | **0** |

### `ISemanticExtension` Implementations in `CodeExplorer.Parser.Java` (9 classes)
1. `GrpcJavaLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/GrpcJavaLibraryParser.cs`)
2. `HttpClientJavaLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/HttpClientJavaLibraryParser.cs`)
3. `JdbcTemplateLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JdbcTemplateLibraryParser.cs`)
4. `JpaLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/JpaLibraryParser.cs`)
5. `SpringEventsLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringEventsLibraryParser.cs`)
6. `SpringGraphQlLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringGraphQlLibraryParser.cs`)
7. `SpringKafkaLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringKafkaLibraryParser.cs`)
8. `SpringMvcLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringMvcLibraryParser.cs`)
9. `SpringRabbitMqLibraryParser` (`cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringRabbitMqLibraryParser.cs`)

*(Note: `SpringFrameworkConfigurationDescriptor` in `cli/src/Parsers/CodeExplorer.Parser.Java/Libraries/SpringFrameworkConfigurationDescriptor.cs` implements `ILibraryConfigurationDescriptor`, not `ISemanticExtension`.)*

### `ISemanticExtension` Implementations in `CodeExplorer.Parser.Android` (0 classes)
- **None (0 classes)**. `CodeExplorer.Parser.Android` contains `AndroidManifestFileParser` (which implements `IFileParser`), along with `AndroidManifestParser`, `GradleBuildScriptParser`, `AndroidManifestInfo`, `AndroidActivityInfo`, `AndroidDeepLinkInfo`, and `GradleBuildInfo`, none of which implement `ISemanticExtension`.
