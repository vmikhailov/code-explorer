# CodeExplorer.Core

Core ontology model, layered indexing pipeline (Layers 1–3), unified Tree-sitter AST abstractions, and SQLite graph storage engine for [CodeExplorer](https://github.com/vmikhailov/code-explorer).

## Overview

`CodeExplorer.Core` is the foundational SDK library of CodeExplorer. It defines the 5-layer code ontology graph, physical and project boundary scanners, zero-allocation span-based Tree-sitter syntax wrappers, incremental workspace indexing, and the embedded SQLite graph repository.

## Key Features

- **5-Layer Code Ontology**: Structured nodes and relationships across Physical (`Layer 1`), Project Boundaries (`Layer 2`), Syntactic AST (`Layer 3`), Semantic Workloads (`Layer 4`), and Global Cross-References (`Layer 5`).
- **Zero-Allocation Syntax Engine**: Span-backed `SyntaxNode` and `SyntaxDocument` abstractions over Tree-sitter.
- **Embedded SQLite Graph Store**: High-throughput batch persistence, incremental AST snapshotting, and OpenCypher query execution.
- **Plugin Parser Abstractions**: Extensible `IProjectParser`, `IFileParser`, and `ISemanticExtension` contracts.

## Installation

```bash
dotnet add package CodeExplorer.Core
```

## Links

- **Documentation & Website**: https://code-explorer.mikhailov.cc
- **Source Repository**: https://github.com/vmikhailov/code-explorer
- **License**: MIT
