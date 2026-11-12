# CodeExplorer Architecture Intelligence Rules

When CodeExplorer MCP tools (`codeexplorer`) are available:
1. **Architecture & Topology First**: Call `get_architecture_map` (and `init_workspace` + `scan_workspace` if unindexed) before broad directory or manifest scanning.
2. **Resolved Graph Over Grep**: Prefer `find_symbol`, `get_callers`, `get_call_chain`, `get_project_dependencies`, `analyze_code_impact`, and `inspect_data_lineage` over raw text search for structural code questions.
3. **Cross-Service & Data Lineage**: Use `trace_cross_service_flow` and `inspect_data_lineage` when tracing REST/gRPC endpoints, message queues, or SQL/ORM table readers and writers.
4. **Architectural Gates**: After code edits in an indexed workspace, run `scan_workspace` and `check_semantic_gates` to ensure zero regressions against Semantic Invariant Gates.
