using Microsoft.Extensions.DependencyInjection;

namespace CodeExplorer.Server;

public static class ServiceRegistrationExtensions
{
    public const string DefaultServerInstructions = """
        CodeExplorer provides a deterministic polyglot AST & architecture knowledge graph with OpenCypher querying.
        1. Schema & Parallel Batching:
           - Call `get_taxonomy` or `get_architecture_map` first (pass `workspacePath` when targeting a specific repository).
           - Batch independent `execute_custom_read_cypher` queries in parallel tool calls within the same turn rather than working sequentially one query or sub-task per turn.
        2. Ontology Semantics (AST-Verified):
           - `Type.attributes`, `Function.attributes`, `Member.attributes`: exact AST annotations/decorators declared directly on that symbol (comma-separated; not inherited from base types or merged from child methods).
           - `Type.extends` / `Type.implements`: declared base type and interface names (including external framework types); `(sub:Type)-[:INHERITS_FROM|IMPLEMENTS]->(base:Type)` edges connect types defined inside the repository.
           - `(caller:Function)-[:CALLS]->(callee:Function)` links callers to the resolved overload's declaring workspace `Type` (`(Type)-[:HAS_METHOD]->(callee)`, distinguishing interface methods from static extension/helper methods); `(m:Member)-[r:ACCESSED_BY]->(fn:Function)` is emitted only when operations are invoked on `m` (`m.data_type`, comma-separated `r.operation`; unused injections or plain argument passing do not emit `ACCESSED_BY`). Trust AST-verified graph edges without redundant manual file verification.
        3. Inline Response Budget (`<10 KB`) & Source Snippets:
           - MCP hosts spill tool outputs above `~10 KB` (`~120` rows) to external files, costing extra read turns. Match exact symbol names (`IN [...]` instead of broad `CONTAINS`), filter all hops strictly in `WHERE` (including transitive inheritance via `EXISTS { MATCH (t)-[:INHERITS_FROM|IMPLEMENTS*1..5]->(b:Type) WHERE ... }`), aggregate with `count(...)` / `collect(DISTINCT ...)` per module or class, and omit verbose `id` or `file_path` columns unless fetching snippets.
           - If source line inspection is needed, batch ranges in one `fetch_code_snippets` call (`nodes` YAML list of `{file_path, start_line, end_line}`, `workspacePath`) rather than per-file reads.
        """;
    public static void RegisterCommonServices(this IServiceCollection services, SqliteGraphClient client, string? workspaceRoot = null)
    {
        services.AddSingleton<IGraphClient>(client);
        services.AddSingleton<IArchitectureQueryService>(sp =>
            new ArchitectureQueryService(sp.GetRequiredService<IGraphClient>()));
        services.AddSingleton<ProjectQueryManager>();
        services.AddSingleton(sp => new CodeExplorerRepository(
            sp.GetRequiredService<IGraphClient>(),
            sp.GetRequiredService<ProjectQueryManager>(),
            workspaceRoot
        ));
        services.AddSingleton<WorkspaceIndexer>();
        services.AddHttpContextAccessor();
    }

    public static void ConfigureMcpWebServices(this IServiceCollection services, SqliteGraphClient client, string? workspaceRoot = null)
    {
        services.AddCors(options => options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
        services.RegisterCommonServices(client, workspaceRoot);

#pragma warning disable MCP9004, MCPEXP002
        services.AddMcpServer(o => o.ServerInstructions = DefaultServerInstructions).WithHttpTransport(o =>
        {
            o.Stateless = false;
            o.EnableLegacySse = true;
        }).WithTools<McpGraphHandler>();
#pragma warning restore MCP9004, MCPEXP002
    }
}
