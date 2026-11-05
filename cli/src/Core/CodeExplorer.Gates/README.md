# CodeExplorer.Gates

Semantic Invariant Gates (SIG) evaluation engine, ratchet baseline manager, and built-in architectural rule catalog for [CodeExplorer](https://github.com/vmikhailov/code-explorer).

## Overview

`CodeExplorer.Gates` enforces architectural guardrails, layer isolation, and security invariants over the CodeExplorer ontology graph using declarative YAML manifests and OpenCypher queries.

## Key Features

- **Declarative Gate Manifests**: Define architectural rules in YAML backed by OpenCypher graph queries.
- **Ratchet Baselines**: Lock down existing technical debt and fail CI builds only when new violations are introduced.
- **Built-In Gate Catalog**: Preconfigured architectural, boundary, and purity invariants for polyglot repositories.

## Installation

```bash
dotnet add package CodeExplorer.Gates
```

## Links

- **Documentation & Website**: https://code-explorer.mikhailov.cc
- **Source Repository**: https://github.com/vmikhailov/code-explorer
- **License**: MIT
