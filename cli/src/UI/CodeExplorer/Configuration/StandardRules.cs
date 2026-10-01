namespace CodeExplorer.Configuration;

public static class StandardRules
{
    public const string HeaderMarker = "## Codebase Intelligence & Exploration (CodeExplorer MCP)";

    public static string GenerateRuleContent()
    {
        return $"""
{HeaderMarker}
When working in this repository where CodeExplorer is configured via MCP:
- **ALWAYS prioritize CodeExplorer MCP tools** over manual `grep_search`, `find_files`, or reading raw source files when exploring architecture, dependencies, or code structure.
- **Rules of Tool Selection:**
  1. **Architecture & Solution Overview**: Use `get_architecture_map` to understand component boundaries, project languages, databases, and ingress/egress points.
  2. **Project Dependencies & Inbound Links**: Use `get_project_dependencies` to inspect incoming/outgoing references, NuGet packages, and blast radius before modifying projects.
  3. **Symbol Search**: Use `find_symbol` first; only fall back to raw grep if the symbol is not in the graph or is dynamically generated.
  4. **Call Chains & Refactoring Impact**: Use `get_call_chain` or `analyze_code_impact` before refactoring or debugging execution flows.
  5. **Data Lineage**: Use `inspect_data_lineage` to trace raw SQL / ORM table usage across projects.
  6. **Ad-Hoc Cypher**: Use `execute_cypher` to run openCypher graph queries directly against the embedded SQLite knowledge graph.
- **Fallback**: Only use standard filesystem tools if CodeExplorer MCP is unavailable or returns an error.
""";
    }
}
