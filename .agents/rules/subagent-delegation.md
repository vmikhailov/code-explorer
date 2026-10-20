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
   - Read only targeted line ranges (`StartLine` / `EndLine`), run filtered tests (`dotnet test --filter ...`), and limit query outputs (`LIMIT`, concise projections) to prevent token overspending.

