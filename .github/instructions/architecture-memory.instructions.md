---
description: Architecture guidelines and decisions for the CodeExplorer semantic pipeline
applyTo:
  - "**/*.cs"
  - "**/*.md"
---
# Architecture Memory

Core architectural patterns, constraints, and pipeline designs for CodeExplorer.

## Two-Layer Semantic Ingestion Pipeline

- **Use a Two-Layer Graph Architecture** instead of local LLMs or in-memory C# data-flow for cross-file semantic extraction.
- **Layer 1 (Structural Graph Ingestion)**: Parses code ASTs, maps structures, and uploads nodes/edges into Memgraph. Resolves global cross-references (e.g., `CALLS`, `INHERITS_FROM`).
- **Layer 2 (Semantic Graph Enrichment)**: Executes pure Cypher queries against Memgraph *only after Layer 1 is fully complete* across all projects. Derives deep meaning like `TRANSITIVELY_CALLS` and `ATTRIBUTED_TO`.
- **Rationale**: Relying on local LLMs per-method introduces severe performance bottlenecks and latency. Graph traversal via Cypher is deterministic, scales effortlessly to large codebases, and ensures perfect language agnosticism.

## AST Visitor Pattern & Ontology Isolation

- **Isolate AST Visitors from Ontology & I/O**: Language-specific visitor classes (e.g., `CSharpFileVisitor`, `TypeScriptFileVisitor`) must inherit from `BaseParserVisitor` and operate on pure in-memory `SyntacticSymbol` trees. They must NOT reference database ontology nodes (`IOntologyNode` derivatives) or perform filesystem/I/O actions.
- **Ontology Mapping Pass**: Perform database ontology node mapping (`FileNode`, `ClassNode`, `QueryNode`, etc.) in the parser runner (`TreeSitterFileParser.cs`) *after* the visitor has completed AST traversal and returned the syntactic symbol tree.
- **Safe Node Traversal**: Always use `NodeExtensions` methods (`IsValid()`, `GetChildFieldText()`, and `GetFunctionNode()`) to handle Tree-sitter nodes safely, avoiding verbose manual null and `IntPtr.Zero` checks.

## Semantic Models & SyntaxTree Binding

- **Bind Semantic Models to a Single SyntaxTree**: Design all `ISemanticModel` implementations to represent the analysis of a singular `SyntaxTree` (one file's AST), similar to Roslyn's `SemanticModel`. Avoid passing lists of files/syntax trees to a single model instance.
- **On-Demand Instantiation**: Resolve semantic models dynamically on-demand using `IProjectParser.GetSemanticModel(SyntaxTree syntaxTree)` during processing, rather than pre-instantiating them globally or at the parser constructor level.

## Parser Spawning Constraints

- **Avoid External Process Spawning**: Do not execute shell commands or launch external subprocesses (e.g., calling `git describe`) to extract build or version metadata during parser runs. Spawning external processes is slow and non-portable. Fallback to reading static config/version files or default to `"unknown"`.
