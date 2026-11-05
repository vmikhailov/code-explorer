# CodeExplorer.Cypher

OpenCypher query parser, AST compiler, and strongly-typed LINQ-to-Cypher builder for [CodeExplorer](https://github.com/vmikhailov/code-explorer).

## Overview

`CodeExplorer.Cypher` provides an embedded OpenCypher parser and SQLite-backed graph query transpiler used by the CodeExplorer engine to query polyglot code ontology graphs without external graph database servers.

## Key Features

- **OpenCypher Parser & AST**: Supports `MATCH`, `OPTIONAL MATCH`, `WHERE`, `WITH`, `UNWIND`, `RETURN`, `ORDER BY`, `SKIP`, `LIMIT`, and variable-length path patterns (`*1..N`).
- **SQLite Graph Transpilation**: Compiles OpenCypher queries into parameterized SQLite queries with recursive CTE support.
- **Fluent LINQ-to-Cypher Builder**: Type-safe query construction for .NET applications.

## Installation

```bash
dotnet add package CodeExplorer.Cypher
```

## Links

- **Documentation & Website**: https://code-explorer.mikhailov.cc
- **Source Repository**: https://github.com/vmikhailov/code-explorer
- **License**: MIT
