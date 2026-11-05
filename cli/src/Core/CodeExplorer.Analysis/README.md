# CodeExplorer.Analysis

Semantic workload synthesis (Layer 4), global cross-reference resolution (Layer 5), C4 architecture views, and impact analysis services for [CodeExplorer](https://github.com/vmikhailov/code-explorer).

## Overview

`CodeExplorer.Analysis` provides the high-level semantic enrichment and architectural query services built on top of `CodeExplorer.Core`. It synthesizes services, endpoints, databases, message queues, data lineage, call chains, and blast-radius impact reports.

## Key Features

- **Layer 4 Semantic Enrichment**: Automatic discovery of services, REST/gRPC/GraphQL endpoints, ORM entities, databases, and external API calls.
- **Layer 5 Global Linking**: Cross-project symbol resolution, late-bound inter-service communication edges, and database canonicalization.
- **Architecture & Impact Services**: C2/C3 architecture maps, call chain tracing, blast-radius analysis, and data lineage inspection.

## Installation

```bash
dotnet add package CodeExplorer.Analysis
```

## Links

- **Documentation & Website**: https://code-explorer.mikhailov.cc
- **Source Repository**: https://github.com/vmikhailov/code-explorer
- **License**: MIT
