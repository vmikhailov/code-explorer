# CodeExplorer Roadmap & Engineering Specs 🗺️

Welcome to the **CodeExplorer (`ce`)** engineering roadmap directory. This directory tracks the architectural evolution, backlog, technical debt remediation, and detailed implementation specifications for the project.

---

## 📚 Document Index

| Document | Purpose | Audience | Status |
| :--- | :--- | :--- | :--- |
| [**`agent-mcp-and-cli-architecture.md`**](./agent-mcp-and-cli-architecture.md) | **Active Engineering Spec:** Exposing unified Semantic (Runtime) & Project (Build) architecture views to AI Agents via MCP and CLI | AI Agents, Core Contributors | 🚀 **Active Spec** |
| [**`todo.md`**](./todo.md) | **Active Backlog & Checklist:** Master checklist of pending epics, parser improvements, Cypher transpiler tasks, and test fixtures | All Contributors | 📋 **Active Backlog** |
| [**`improvements.md`**](./improvements.md) | **Strategic Roadmap & History:** Chronological record of delivered milestones and high-level 5-phase strategic vision | Architects, Contributors | 🗺️ **Reference** |
| [**`tech-debt.md`**](./tech-debt.md) | **Technical Debt Audit Log:** Record of the 6-phase zero-hack semantic architecture remediation (Phases 1–6 Completed) | Core Developers | ✅ **Audit Record** |

---

## 🧭 Current Strategic Focus

The primary active initiative is **closing the gap between the VS Code Webview and AI Agents/CLI**:
1. Connect MCP directly to the newly delivered **`ArchitectureViewEngine`** (`get_architecture_view`).
2. Clearly separate **Runtime / Semantic** dependencies (`SERVICE_CALL`, `PUBLISHES_TO`, `SUBSCRIBES_TO`, `USES_DB`) from **Build / Structural** dependencies (`PROJECT_REFERENCE`, `DEPENDS_ON`).
3. Add service contract inspection (`get_service_contracts`) and cross-service flow tracing (`trace_cross_service_flow`).
4. Ensure 1:1 parity across MCP tools and CLI commands with token-efficient serializers (Markdown, TOON, Mermaid).

For the complete implementation prompt and architecture guide, refer to [**`agent-mcp-and-cli-architecture.md`**](./agent-mcp-and-cli-architecture.md).
