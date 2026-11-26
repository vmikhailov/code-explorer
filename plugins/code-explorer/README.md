# CodeExplorer Agent Plugin

Deterministic polyglot **5-layer architecture graph**, cross-service call & SQL/ORM data lineage, blast-radius analysis, and OpenCypher **Semantic Invariant Gates (`SIG-*`)** for AI coding agents (**C#, TypeScript/JS, Go, Python, Java, Kotlin, Rust, PHP, SQL, Terraform**).

---

## Bundled Components

| Component | Included | Purpose |
| :--- | :--- | :--- |
| **MCP Server** | `codeexplorer` | Zero-config local `stdio` MCP server (`ce mcp -q`). Reuses an existing `ce` installation or automatically downloads & SHA-256-verifies the native single-file release (`win-x64`, `win-arm64`, `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`) on first run. |
| **Skills** | `code-explorer`, `semantic-invariant-gates` | Guided workflows for architecture onboarding, call/data lineage, blast-radius impact analysis, and authoring/ratcheting Cypher invariant gates. |
| **Agent** | `architecture-auditor` | Read-only specialist agent for multi-service architectural reviews, CQRS/DDD boundary checks, and AuthZ/data-sensitivity security audits. |
| **Rules** | `rules/AGENTS.md` | Always-on routing guidelines teaching the agent when to query the graph vs perform direct single-file edits. |

---

## One-Command Installation

### Google Antigravity
Once published in the Antigravity Marketplace (**Customizations > Marketplace**), click **Add** on the **CodeExplorer** card, or install from the Antigravity CLI:

```bash
# Partner Marketplace (once published)
agy plugin install code-explorer@antigravity-plugins-partners

# Direct install from local checkout
agy plugin install ./plugins/code-explorer
```

### Claude Code
```bash
/plugin marketplace add vmikhailov/code-explorer
/plugin install code-explorer@code-explorer
```

### Gemini CLI
```bash
gemini extensions install https://github.com/vmikhailov/code-explorer
```

### Cursor / Codex / VS Code
Install directly from `vmikhailov/code-explorer` or install the **CodeExplorer** extension from the VS Code / Open VSX Marketplace (which automatically downloads the native `ce` engine and registers the MCP server on startup).

---

## Links

- **Website & Benchmarks**: [https://code-explorer.app](https://code-explorer.app)
- **Repository**: [https://github.com/vmikhailov/code-explorer](https://github.com/vmikhailov/code-explorer)
- **License**: MIT
