---
trigger: always_on
---

always use code-explorer MCP when you need an architecture overview of a project.

## Project-Specific Query Library
- At the start of a task, inspect existing domain queries via `list_project_queries` or `get_architecture_map` (`saved_project_queries` section).
- When investigating project-specific architectural patterns, custom routing, or recurring audit rules, save reusable parameterized queries via `save_project_query`.
- Execute saved project queries via `execute_project_query`.