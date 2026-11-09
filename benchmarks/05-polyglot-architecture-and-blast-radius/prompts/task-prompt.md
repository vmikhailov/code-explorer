# Benchmark Task Prompt: Polyglot Architecture Discovery & Blast Radius Analysis

Perform a precise architectural and impact analysis on the `code-explorer` repository and answer all three questions below with 100% completeness (do not guess or omit items; provide exact class names, method names, and relative file paths):

1. **Interface Implementations across the Codebase**:
   List every class that implements:
   - `IComponentLibraryParser`
   - `IMcpTargetAdapter`
   - `ILibraryConfigurationDescriptor`
   - `ISyntaxEnricher`
   For each class, provide its exact class name and the relative file path where it is declared.

2. **Blast Radius & Call Chain of `GateExecutionEngine`**:
   Identify all **production (non-test)** classes and methods that directly call `CheckGateAsync` or `CheckSuiteAsync` on `GateExecutionEngine`. For each caller, also identify one hop further upstream (which HTTP endpoint, WebSocket message type, CLI command, or MCP tool triggers that caller).

3. **Polyglot `ISemanticExtension` Breakdown**:
   Count the exact number of classes implementing `ISemanticExtension` in each of the following parser projects:
   - `CodeExplorer.Parser.CSharp`
   - `CodeExplorer.Parser.TypeScript`
   - `CodeExplorer.Parser.Python`
   - `CodeExplorer.Parser.Go`
   - `CodeExplorer.Parser.Java`
   - `CodeExplorer.Parser.Android`
   And list the exact class names of all `ISemanticExtension` implementations in `CodeExplorer.Parser.Java` and `CodeExplorer.Parser.Android`.
