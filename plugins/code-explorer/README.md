# CodeExplorer Agent Plugin

Deterministic polyglot architecture graph, cross-service call & SQL data lineage, blast-radius analysis, and Semantic Invariant Gates for AI coding agents (**C#, TypeScript/JS, Go, Python, Java, Kotlin, Rust, SQL**).

## One-Command Installation

### Claude Code
```bash
/plugin marketplace add vmikhailov/code-explorer
/plugin install code-explorer@code-explorer
```

### Gemini CLI
```bash
gemini extensions install https://github.com/vmikhailov/code-explorer
```

### Cursor / Codex / Antigravity
Install directly from `vmikhailov/code-explorer` or install the **CodeExplorer** extension from the VS Code / Open VSX Marketplace (which automatically downloads the native `ce` engine and registers the MCP server on startup).

## Zero-Config Engine Bootstrap
On first launch, `mcp/launch.mjs` automatically locates an existing `ce` binary (`PATH`, `~/.dotnet/tools`, or IDE extension storage) or downloads the latest native single-file release from GitHub Releases into `~/.codeexplorer/bin/`.
