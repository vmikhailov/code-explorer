using CodeExplorer.Cypher.Ast;

namespace CodeExplorer.Cypher.Linq;

public interface ICypherQueryExecutor
{
    Task<string> ExecuteQueryAsync(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken ct = default);
    Task<string> ExecuteRawAsync(string cypher, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken ct = default);
}
