---
trigger: always_on
---

always use code-explorer MCP when you need an architecture overview of a project.

## When to Use CodeExplorer vs When NOT to Use CodeExplorer

Use CodeExplorer **only where graph structure beats direct file reading**:

### DO Use CodeExplorer When:
1. **Symptom-Based / Root-Cause Debugging ("Something is broken, figure out why")**:
   - The prompt describes a runtime symptom, stale state, permission leak, or unexpected failure **without** giving you the exact file and method to edit.
   - Use `find_symbol` $\to$ `get_call_chain` / `get_callers` to trace multi-hop execution across modules, and `inspect_data_lineage` to see all readers/writers/caches for a database table.
2. **Cross-Module Blast Radius & All-Callers Discovery**:
   - You need to find every caller of a method (`get_callers`), every consumer of a changed contract (`analyze_code_impact`), every implementation of a base class/interface (`execute_custom_read_cypher`), or every service writing to a table (`inspect_data_lineage`).
3. **Architecture & Boundary Onboarding**:
   - Starting in an unfamiliar codebase or checking project/module boundaries (`get_architecture_map`, `get_project_dependencies`).
4. **Post-Edit Architectural Verification**:
   - Running `check_semantic_gates` **once** after all code edits are complete to verify zero architectural regressions (`SIG-*`).

### DO NOT Use CodeExplorer When:
1. **Localized Single-File Edits with Known Coordinates**:
   - The user already gave you the exact file path, class name, or method name and the change is self-contained inside that file. Read and edit the file directly (`read_file` $\to$ `edit_file`) — do not waste turns calling `get_architecture_map`, `find_symbol`, or `get_file_outline` on a file you already know.
2. **Repeated Re-Indexing During Intermediate Edits**:
   - Never call `scan_workspace` / `check_semantic_gates` after every single line edit; finish your code changes first and verify once at the end.
3. **Website, Docs, Benchmarks & Agent Config Edits (`site/`, `docs/`, `benchmarks/`, `.agents/`, `README.md`)**:
   - Do **NOT** run `ce scan`, `ce gate check`, or `check_semantic_gates` for changes confined to the marketing site, documentation, benchmark reports/scripts, or agent rules. Use their own lightweight checks instead (`npm --prefix site run typecheck`, `sync-benchmarks.mjs --check`, `sloplint --render`).

## Semantic Invariant Gates (Zero Failed Gates Policy)
- **Verify Semantic Invariant Gates on Shipped Code Only**: Run `ce scan` and `ce gate check` / `check_semantic_gates` after modifying shipped product code (`cli/`, `vscode-extension/`, `proto/`) or gate definitions (`.codeexplorer/gates/`). Skip it for `site/`, `docs/`, `benchmarks/`, and `.agents/` edits.
- **ALWAYS fix all failed gates (`[FAIL]`) before completing a task**. Never finish or report task completion while any gate is failing.
- Fix real architectural or implementation violations directly in code (e.g., extracting constants for `SIG-M-I-001`, documenting/handling catch blocks for `SIG-M-I-005`, splitting oversized methods/classes for `SIG-M-R-001` / `SIG-C-R-001`, keeping IO within Layers 1–3 for `SIG-P-B-011`).

## Project-Specific Query Library
- At the start of a task, inspect existing domain queries via `execute_custom_read_cypher` (`listSaved: true`) or `get_architecture_map` (`saved_project_queries` section).
- When investigating project-specific architectural patterns, custom routing, or recurring audit rules, store reusable parameterized queries in `.codeexplorer/queries/`.
- Execute saved project queries via `execute_custom_read_cypher` (`savedQuery` + `parameters`).