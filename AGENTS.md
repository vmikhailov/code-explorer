# Agent Guide - CodeExplorer

This document provides essential guidelines, architecture conventions, and workflows for AI coding agents working on the CodeExplorer repository.

---

## 🚀 Build and Test Workflows

### Build Commands
Compile the entire solution with the following command:
```bash
dotnet build
```

### Running Tests
Execute the unit and integration test suite:
```bash
dotnet test
```

### Running the Application CLI
The main CLI entry point is located in `UI/CodeExplorer/CodeExplorer.csproj`.
- **Ingest/Index a Workspace**:
  ```bash
  dotnet run --project UI/CodeExplorer/CodeExplorer.csproj -- ingest --dir "/path/to/target" --clear
  ```
- **Execute custom Cypher queries**:
  ```bash
  dotnet run --project UI/CodeExplorer/CodeExplorer.csproj -- query --query "MATCH (n:Project) RETURN n.name"
  ```
- **Start the MCP (Model Context Protocol) Server**:
  ```bash
  dotnet run --project UI/CodeExplorer/CodeExplorer.csproj -- mcp --port 8085
  ```

---

## 📂 Project Structure

- `Core/CodeExplorer.Core`: Core parsing infrastructure, database connections, and MCP tools/handlers.
- `Parsers/`: Custom syntax tree parsers powered by Tree-sitter and SQL ScriptDom:
  - `CodeExplorer.Parser.CSharp` (C# Parser and Visitors)
  - `CodeExplorer.Parser.Go` (Go Parser and Visitors)
  - `CodeExplorer.Parser.Python` (Python Parser and Visitors)
  - `CodeExplorer.Parser.SQL` (T-SQL DOM Parser)
  - `CodeExplorer.Parser.TypeScript` (TypeScript and JavaScript Parsers)
- `UI/CodeExplorer`: CLI host and SSE API endpoint.
- `Tests/CodeExplorer.Tests`: Comprehensive integration and parser validation tests.

---

## 🏛️ Core Architecture & Conventions

### 1. Two-Layer Semantic Pipeline
- **Layer 1 (Structural Graph)**: Extracts syntax nodes, files, local variables, and package metadata directly from ASTs. Uploads nodes and edges into Memgraph via batch channels.
- **Layer 2 (Semantic Graph)**: Triggers only after Layer 1 ingestion completes. Resolves cross-file and late-bound connections (like resolving API controllers to endpoints or HttpClient targets) via Cypher queries and `PostIndexAnalyzer`.
- **Constraint**: Do not execute external subprocesses (e.g., launching `git`) to retrieve build/metadata during parser runs. Fallback to static configuration files or return `"unknown"`.

### 2. AST Visitor Pattern
All syntax-level analyzers (except SQL) utilize a type-safe Roslyn-style visitor pattern.

#### A. In-Memory Isolation (No Disk I/O)
- **Rule**: AST visitor classes **MUST NOT perform filesystem reads or directory searches** (no `using System.IO;` imports). 
- All paths must be constructed via in-memory string manipulation:
  ```csharp
  // Construct absolute file path in BaseParserVisitor constructor
  var relativePath = filePath.Replace('\\', '/').Trim('/');
  var workspacePathClean = absoluteWorkspacePath.Replace('\\', '/').TrimEnd('/');
  var absoluteFilePath = $"{workspacePathClean}/{relativePath}";
  ```
- Project name resolution must be calculated purely from path segments rather than calling `File.Exists` or `Directory.GetFiles`:
  ```csharp
  private static string GetProjectNameFromRelativePath(string relativePath)
  {
      var cleanPath = relativePath.Replace('\\', '/').Trim('/');
      var parts = cleanPath.Split('/');
      if (parts.Length == 0) return "default";

      if (parts.Length >= 2 && (parts[0] is "Core" or "Parsers" or "Tests"))
      {
          return parts[1];
      }
      return parts[0];
  }
  ```

#### B. Class Hierarchy & Routing
- Language-specific visitors (e.g., `CSharpFileVisitor`) inherit from `BaseParserVisitor` (which inherits from `TreeSitterAstVisitor`).
- `BaseParserVisitor` defines a unified `Dispatch` method representing the union of all switch-case node types across languages.
- Derived subclasses must override proper typed virtual visitor methods (such as `VisitClassDeclaration`, `VisitMethodDeclaration`, `VisitParameter`) rather than implementing custom routing loops.

#### C. SyntaxTree Binding
- Bind semantic models and enrichers to a single `SyntaxTree` instance on-demand via `IProjectParser.GetSyntaxEnricher(SyntaxTree syntaxTree)` rather than passing multiple files or pre-instantiating models globally.

### 3. Library Parsers
- Frame and API-specific rules (e.g., NestJS, Express, Axios, HttpClient, ASP.NET Core) are isolated in separate classes implementing `ILibraryParser` in the `Libraries/` directory of their respective parser project.
- Parsers delegate to these library implementations dynamically during traversal to map nodes and capture dependency relationships.
