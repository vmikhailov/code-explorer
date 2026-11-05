---
trigger: always_on
---

always use code-explorer MCP when you need an architecture overview of a project.

## Semantic Invariant Gates (Zero Failed Gates Policy)
- **ALWAYS verify Semantic Invariant Gates** (`ce gate check` / `run_gates`) after making code changes.
- **ALWAYS fix all failed gates (`[FAIL]`) before completing a task**. Never finish or report task completion while any gate is failing.
- Fix real architectural or implementation violations directly in code (e.g., extracting constants for `SIG-M-I-001`, documenting/handling catch blocks for `SIG-M-I-005`, splitting oversized methods/classes for `SIG-M-R-001` / `SIG-C-R-001`, keeping IO within Layers 1–3 for `SIG-P-B-011`).

## Project-Specific Query Library
- At the start of a task, inspect existing domain queries via `list_project_queries` or `get_architecture_map` (`saved_project_queries` section).
- When investigating project-specific architectural patterns, custom routing, or recurring audit rules, save reusable parameterized queries via `save_project_query`.
- Execute saved project queries via `execute_project_query`.