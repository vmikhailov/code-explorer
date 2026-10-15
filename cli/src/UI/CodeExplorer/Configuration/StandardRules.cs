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
- **Rules of Tool Selection (CLI First, Universal Across All AI Coding Environments):**
  1. **Callers & Inbound References**: ALWAYS use `ce callers <target>` (or MCP `get_callers`) to discover callers of functions, microservices, database tables, and messaging topics with exact `file:line` locations. Never regex-grep across dozens of files for callers.
  2. **Blast Radius & Impact Analysis**: ALWAYS use MCP `analyze_code_impact` before refactoring or modifying a symbol to inspect all dependents.
  3. **Architecture & Solution Overview**: Use `ce view` or `ce query` (or MCP `get_architecture_map`) to understand component boundaries, project languages, databases, and ingress/egress points.
  4. **Project Dependencies & Inbound Links**: Use `ce dependencies` (or MCP `get_project_dependencies`) to inspect incoming/outgoing references, NuGet packages, and blast radius before modifying projects.
  5. **Cross-Service Flow & Tracing**: Use `ce trace flow --from <service>` (or MCP `trace_cross_service_flow` / `get_call_chain`) to follow end-to-end execution across microservices.
  6. **Ad-Hoc Cypher**: Use `ce query "<cypher>"` (or MCP `execute_cypher`) to query the knowledge graph directly.
- **Fallback**: Only use standard filesystem tools (`grep`, `find`, raw reads) if CodeExplorer is unavailable or returns an error.
""";
    }
}
