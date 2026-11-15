namespace CodeExplorer.Configuration;

public static class StandardRules
{
    public const string HeaderMarker = "## Codebase Intelligence & Exploration (CodeExplorer MCP)";

    public static string GenerateRuleContent()
    {
        return $"""
{HeaderMarker}
When working in this repository where CodeExplorer is available (CLI `ce` or MCP):
- **ALWAYS prioritize CodeExplorer CLI & MCP tools** over manual `grep_search`, `find_files`, or reading raw source files when exploring architecture, dependencies, or code structure.
- **Rules of Tool Selection (1-to-1 Equivalent Across CLI & MCP):**
  1. **Architecture & Solution Overview**: ALWAYS use `ce map` (or MCP `get_architecture_map`) to inspect component boundaries, project languages, databases, endpoints, and C1/C2/C3 topologies.
  2. **Project Dependencies & Inbound Links**: ALWAYS use `ce deps [project]` (or MCP `get_project_dependencies`) to inspect incoming/outgoing references, packages, and runtime vs build dependencies.
  3. **Symbol Discovery & File Outlines**: ALWAYS use `ce symbol <name>` / `ce search <query>` (or MCP `find_symbol`) and `ce outline <path>` (or MCP `get_file_outline`) before falling back to text grep; read targeted line ranges via `ce snippets <file:start-end>` (or MCP `fetch_code_snippets`).
  4. **Callers, DI Implementations & Call Chains**: ALWAYS use `ce callers <target> [-m <method>]` (or MCP `get_callers`), `ce trace <startFn> <endFn>` (or MCP `get_call_chain`), and `ce impact <symbol>` (or MCP `analyze_code_impact`) before refactoring.
  5. **Service Contracts, Distributed Flows & Data Lineage**: Use `ce contracts <service> [--direction all|ingress|egress|entry_points|flow]` (or MCP `get_service_contracts`) and `ce lineage <table>` (or MCP `inspect_data_lineage`).
  6. **Affected Tests, Gates & Custom Cypher**: Use `ce test` (or MCP `get_affected_tests`), `ce gate check` (or MCP `check_semantic_gates`), and `ce query "<cypher>"` (or MCP `execute_custom_read_cypher`).
- **Fallback**: Only use standard filesystem tools (`grep`, `find`, raw reads) if CodeExplorer is unavailable or returns an error.
""";
    }
}
