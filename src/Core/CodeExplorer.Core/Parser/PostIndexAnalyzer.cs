using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public class PostIndexAnalyzer(IDatabaseClient db)
{
    public async Task RunAsync(string workspaceId)
    {
        if (!db.IsCypherSupported)
        {
            return; // Skip Cypher-based post-indexing optimizations on SQLite/In-memory backend
        }

        var widPrefix = workspaceId + ":";

        await WriteTransitivelyCallsAsync(widPrefix);
        await WriteAttributedToAsync(widPrefix);
        await WriteProjectApiAnnotationsAsync(widPrefix);
    }

    private Task WriteTransitivelyCallsAsync(string widPrefix) => db.ExecuteWriteAsync("""
        MATCH path = (caller:Function)-[:CALLS*1..15]->(sink)
        WHERE caller.id STARTS WITH $widPrefix AND (sink:ExternalService OR sink:DB OR sink:Query)
        WITH caller, sink, min(length(path)) AS hops
        MERGE (caller)-[r:TRANSITIVELY_CALLS]->(sink)
        SET r.hops = hops
        """, new { widPrefix });

    private Task WriteAttributedToAsync(string widPrefix) => db.ExecuteWriteAsync("""
        MATCH path = (ep:EntryPoint)<-[:IMPLEMENTS]-(fn:Function)-[:CALLS*0..15]->(sink)
        WHERE ep.id STARTS WITH $widPrefix AND (sink:ExternalService OR sink:DB OR sink:Query)
        WITH ep, sink, min(length(path)) AS hops, labels(sink)[0] AS sinkKind
        MERGE (ep)-[r:ATTRIBUTED_TO]->(sink)
        SET r.hops = hops, r.sink_kind = sinkKind
        """, new { widPrefix });

    private Task WriteProjectApiAnnotationsAsync(string widPrefix) => db.ExecuteWriteAsync("""
        MATCH (p:Project)-[:CONTAINS|EXPOSES*1..2]->(ep:EntryPoint)-[:ATTRIBUTED_TO]->(es:ExternalService)
        WHERE p.id STARTS WITH $widPrefix
        WITH p, collect(DISTINCT es.domain_or_service) AS domains
        SET p.external_apis = domains
        """, new { widPrefix });
}
