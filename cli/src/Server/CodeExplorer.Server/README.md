# CodeExplorer.Server

Model Context Protocol (MCP) server and HTTP/SSE/Stdio host for [CodeExplorer](https://github.com/vmikhailov/code-explorer).

## Overview

`CodeExplorer.Server` exposes CodeExplorer's architectural intelligence, symbol search, call chain analysis, data lineage, OpenCypher queries, and Semantic Invariant Gates to AI coding agents (Claude Code, Cursor, GitHub Copilot, Windsurf) and IDE extensions via the Model Context Protocol (MCP).

## Key Features

- **MCP Tools**: Built-in tools for `get_architecture_map`, `find_symbol`, `get_call_chain`, `analyze_code_impact`, `inspect_data_lineage`, `query_cypher`, and `evaluate_gates`.
- **Multi-Transport Hosting**: Supports both `stdio` and HTTP/SSE MCP transports with background file watching and incremental re-indexing.

## Installation

```bash
dotnet add package CodeExplorer.Server
```

## Links

- **Documentation & Website**: https://code-explorer.mikhailov.cc
- **Source Repository**: https://github.com/vmikhailov/code-explorer
- **License**: MIT
