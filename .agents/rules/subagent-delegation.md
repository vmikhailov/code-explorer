---
trigger: always_on
---

# Subagent Delegation & Token Efficiency Policy

1. **Single-Task Direct Execution (No Subagent)**:
   - If you have **just one task**, **DO NOT spawn any subagent** (`self` or `research`). Execute the task directly in the main agent to avoid context duplication and token overhead.
   - Only spawn subagents when there are multiple genuinely independent tasks that benefit from parallel execution.

2. **No Model Switching**:
   - Do **not** switch or override the `Model` parameter when invoking subagents (use default `"inherit"`), as model switching does not take effect.

3. **Token & Scanning Discipline**:
   - **Avoid massive source scanning** and full-repo re-indexing unless strictly required.
   - Read only targeted line ranges (`StartLine` / `EndLine`), limit query outputs (`LIMIT`, concise projections), and use filtered tests (`dotnet test --filter ...`) **only during inner-loop iteration** — never as the final pre-commit/pre-push verification gate (always run `get_affected_tests` on all modified files or the full `dotnet test cli/CodeExplorer.slnx -c Release --no-build -m:1` before finishing/committing).

