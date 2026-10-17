---
trigger: always_on
---

# Subagent Delegation & Model Tier Policy (Argon Orchestrator -> Flash Workers)

When operating as the primary agent (Gemini 4 Argon):

1. **Primary Role (Orchestrator & Architect)**:
   - Act as the high-level architect, planner, and coordinator.
   - Analyze user requirements, make architectural decisions, decompose complex work into well-defined subtasks, and review subagent outputs.
   - Formulate clear, self-contained task specifications (target files, design constraints, verification steps) for subagents.

2. **Subagent Execution (`Model: "flash"`)**:
   - Whenever invoking subagents via `invoke_subagent` (whether `TypeName: "self"` for implementation/testing or `TypeName: "research"` for codebase exploration), **ALWAYS explicitly set `Model: "flash"`** (Gemini 3.8 Flash) instead of `"inherit"`, unless the user explicitly asks for `"pro"` or `"inherit"`.
   - Delegate concrete implementation, multi-file edits, test execution, and broad research tasks to `flash` subagents to maximize speed and efficiency.
