using System.Text;
using System.Text.Json;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Mcp;

public partial class CodeExplorerRepository
{
    private const string ParamTarget = "target";
    private const string ParamClassName = "className";
    private const string ParamMethodName = "methodName";
    private const string ParamIncludeTests = "includeTests";
    private const string QueryGetCallers = "get_callers";
    private const string SingleQuote = "'";
    private const string EscapedSingleQuote = "''";
    private const string CommaSep = ", ";
    private const string SymPrefix = ":sym:";
    private const string EscapedQuoteUnicode = "\\u0022";
    private static readonly char[] EvidenceDelimiters = ['|', '"', ',', '[', ']'];

    private const string FastQualCallersQuery =
        "MATCH (owner:Type)-[:HAS_METHOD]->(target:Function) " +
        "WHERE target.name IS NOT NULL AND target.name = $methodName AND owner.name IS NOT NULL AND owner.name = $className " +
        "MATCH (caller)-[r:CALLS|SERVICE_CALL|CALLS_ENDPOINT|USES_TYPE|USES_DB|QUERIES_DB|PUBLISHES_TO|SUBSCRIBES_TO|TRIGGERS|IMPLEMENTS|INHERITS_FROM]->(target) " +
        "WHERE $includeTests OR NOT (COALESCE(caller.is_test, false) = true OR COALESCE(caller.project_name, '') CONTAINS '.Test' OR COALESCE(caller.path, caller.file_path, '') CONTAINS '/tests/' OR COALESCE(caller.path, caller.file_path, '') CONTAINS '/test/' OR COALESCE(caller.path, caller.file_path, '') ENDS WITH 'Tests.cs' OR COALESCE(caller.path, caller.file_path, '') ENDS WITH '.spec.ts' OR COALESCE(caller.path, caller.file_path, '') ENDS WITH '.test.ts' OR COALESCE(caller.path, caller.file_path, '') ENDS WITH '_test.go') " +
        "OPTIONAL MATCH (callerOwner:Type)-[:HAS_METHOD]->(caller) " +
        "OPTIONAL MATCH (caller)-[:DECLARED_IN]->(f:File) " +
        "RETURN target.name AS targetName, labels(target)[0] AS targetKind, CASE WHEN callerOwner IS NOT NULL THEN callerOwner.name + '.' + caller.name ELSE caller.name END AS callerName, labels(caller)[0] AS callerKind, type(r) AS relKind, COALESCE(caller.project_name, '') AS projectName, CASE WHEN COALESCE(caller.path, caller.file_path, f.path) IS NOT NULL AND caller.start_line IS NOT NULL THEN COALESCE(caller.path, caller.file_path, f.path) + ':' + toString(caller.start_line) WHEN COALESCE(caller.path, caller.file_path, f.path) IS NOT NULL THEN COALESCE(caller.path, caller.file_path, f.path) ELSE '' END AS location ORDER BY targetName, callerName";

    private const string FastExactCallersQuery =
        "MATCH (target) WHERE target.name IS NOT NULL AND target.name = $target " +
        "MATCH (caller)-[r:CALLS|SERVICE_CALL|CALLS_ENDPOINT|USES_TYPE|USES_DB|QUERIES_DB|PUBLISHES_TO|SUBSCRIBES_TO|TRIGGERS|IMPLEMENTS|INHERITS_FROM]->(target) " +
        "WHERE $includeTests OR NOT (COALESCE(caller.is_test, false) = true OR COALESCE(caller.project_name, '') CONTAINS '.Test' OR COALESCE(caller.path, caller.file_path, '') CONTAINS '/tests/' OR COALESCE(caller.path, caller.file_path, '') CONTAINS '/test/' OR COALESCE(caller.path, caller.file_path, '') ENDS WITH 'Tests.cs' OR COALESCE(caller.path, caller.file_path, '') ENDS WITH '.spec.ts' OR COALESCE(caller.path, caller.file_path, '') ENDS WITH '.test.ts' OR COALESCE(caller.path, caller.file_path, '') ENDS WITH '_test.go') " +
        "OPTIONAL MATCH (callerOwner:Type)-[:HAS_METHOD]->(caller) " +
        "OPTIONAL MATCH (caller)-[:DECLARED_IN]->(f:File) " +
        "RETURN target.name AS targetName, labels(target)[0] AS targetKind, CASE WHEN callerOwner IS NOT NULL THEN callerOwner.name + '.' + caller.name ELSE caller.name END AS callerName, labels(caller)[0] AS callerKind, type(r) AS relKind, COALESCE(caller.project_name, '') AS projectName, CASE WHEN COALESCE(caller.path, caller.file_path, f.path) IS NOT NULL AND caller.start_line IS NOT NULL THEN COALESCE(caller.path, caller.file_path, f.path) + ':' + toString(caller.start_line) WHEN COALESCE(caller.path, caller.file_path, f.path) IS NOT NULL THEN COALESCE(caller.path, caller.file_path, f.path) ELSE '' END AS location ORDER BY targetName, callerName";

    private const string ReverseSemanticCallersQuery =
        "MATCH (target) WHERE target.name = $target OR target.id = $target OR target.symbol = $target " +
        "MATCH (target)-[r:PUBLISHED_BY|SUBSCRIBED_BY|ACCESSED_BY|CALLED_BY|TRANSITIVELY_CALLS|EXPOSED_BY|IMPLEMENTED_BY]->(caller) " +
        "WHERE (type(r) <> 'IMPLEMENTED_BY' OR labels(caller)[0] IN ['Type', 'Function']) " +
        "AND ($includeTests OR NOT (COALESCE(caller.is_test, false) = true OR COALESCE(caller.project_name, '') CONTAINS '.Test' OR COALESCE(caller.path, caller.file_path, '') CONTAINS '/tests/' OR COALESCE(caller.path, caller.file_path, '') CONTAINS '/test/' OR COALESCE(caller.path, caller.file_path, '') ENDS WITH 'Tests.cs' OR COALESCE(caller.path, caller.file_path, '') ENDS WITH '.spec.ts' OR COALESCE(caller.path, caller.file_path, '') ENDS WITH '.test.ts' OR COALESCE(caller.path, caller.file_path, '') ENDS WITH '_test.go')) " +
        "OPTIONAL MATCH (callerOwner:Type)-[:HAS_METHOD]->(caller) " +
        "OPTIONAL MATCH (caller)-[:DECLARED_IN]->(f:File) " +
        "RETURN target.name AS targetName, labels(target)[0] AS targetKind, CASE WHEN callerOwner IS NOT NULL THEN callerOwner.name + '.' + caller.name ELSE caller.name END AS callerName, labels(caller)[0] AS callerKind, type(r) AS relKind, COALESCE(caller.project_name, '') AS projectName, CASE WHEN COALESCE(caller.path, caller.file_path, f.path) IS NOT NULL AND caller.start_line IS NOT NULL THEN COALESCE(caller.path, caller.file_path, f.path) + ':' + toString(caller.start_line) WHEN COALESCE(caller.path, caller.file_path, f.path) IS NOT NULL THEN COALESCE(caller.path, caller.file_path, f.path) ELSE '' END AS location ORDER BY targetName, callerName";

    private const string ServiceCallEvidenceQuery =
        "MATCH (svc)-[r:SERVICE_CALL]->(target) " +
        "WHERE (target.name = $target OR target.id = $target) AND r.evidence_ids IS NOT NULL " +
        "RETURN target.name AS targetName, labels(target)[0] AS targetKind, r.evidence_ids AS evidenceIds";

    private const string ResolveFunctionsByIdsPrefix =
        "MATCH (caller:Function) WHERE caller.id IN [";

    private const string ResolveFunctionsByIdsMid =
        "] OPTIONAL MATCH (callerOwner:Type)-[:HAS_METHOD]->(caller) " +
        "OPTIONAL MATCH (caller)-[:DECLARED_IN]->(f:File) RETURN ";

    private const string ResolveFunctionsByIdsSuffix =
        ", CASE WHEN callerOwner IS NOT NULL THEN callerOwner.name + '.' + caller.name ELSE caller.name END AS callerName, labels(caller)[0] AS callerKind, 'SERVICE_CALL' AS relKind, COALESCE(caller.project_name, '') AS projectName, CASE WHEN COALESCE(caller.path, caller.file_path, f.path) IS NOT NULL AND caller.start_line IS NOT NULL THEN COALESCE(caller.path, caller.file_path, f.path) + ':' + toString(caller.start_line) WHEN COALESCE(caller.path, caller.file_path, f.path) IS NOT NULL THEN COALESCE(caller.path, caller.file_path, f.path) ELSE '' END AS location ORDER BY callerName";

    public async Task<string> GetCallersAsync(
        string target,
        bool includeTests = false,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        var res = await QueryAllCallersAsync(client, target, includeTests, cancellationToken);
        return ToonYamlSerializer.SerializeYaml(res);
    }

    private static async Task<CypherQueryResult> QueryAllCallersAsync(
        IGraphClient client,
        string target,
        bool includeTests,
        CancellationToken ct)
    {
        var parameters = new Dictionary<string, object?>
        {
            [ParamTarget] = target,
            [ParamIncludeTests] = includeTests
        };

        var res = await TryFastInboundCallersAsync(client, target, includeTests, parameters, ct);
        if (res == null || res.Count == 0)
        {
            res = await ExecuteFallbackCallersAsync(client, target, parameters, ct);
        }

        return await MergeReverseAndServiceEvidenceCallersAsync(client, res, parameters, ct);
    }

    private static async Task<CypherQueryResult?> TryFastInboundCallersAsync(
        IGraphClient client,
        string target,
        bool includeTests,
        Dictionary<string, object?> parameters,
        CancellationToken ct)
    {
        if (target.Contains(':') || target.Contains('/')) return null;

        if (!target.Contains('.'))
        {
            return await client.ExecuteCypherAsync(FastExactCallersQuery, parameters, ct);
        }

        var dotIdx = target.LastIndexOf('.');
        var className = target[..dotIdx];
        if (className.Contains('.')) className = className[(className.LastIndexOf('.') + 1)..];
        var qualParams = new Dictionary<string, object?>
        {
            [ParamClassName] = className,
            [ParamMethodName] = target[(dotIdx + 1)..],
            [ParamIncludeTests] = includeTests
        };
        return await client.ExecuteCypherAsync(FastQualCallersQuery, qualParams, ct);
    }

    private static async Task<CypherQueryResult> ExecuteFallbackCallersAsync(
        IGraphClient client,
        string target,
        Dictionary<string, object?> parameters,
        CancellationToken ct)
    {
        var query = Queries.Get(QueryGetCallers);
        var res = await client.ExecuteCypherAsync(query, parameters, ct);
        if (res.Count > 0 || client is not SqliteGraphClient sqliteClient)
        {
            return res;
        }

        try
        {
            var engine = new Analysis.IdentifierSearchEngine();
            var resolved = await engine.ResolveIdentifierAsync(sqliteClient.Connection, target, ct: ct);
            if (resolved is { Score: >= 0.70f } && resolved.NodeId != target && resolved.Name != target)
            {
                parameters[ParamTarget] = resolved.NodeId;
                var retryRes = await client.ExecuteCypherAsync(query, parameters, ct);
                if (retryRes.Count > 0) return retryRes;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ignore fuzzy resolution failure and return primary result
        }

        return res;
    }

    private static async Task<CypherQueryResult> MergeReverseAndServiceEvidenceCallersAsync(
        IGraphClient client,
        CypherQueryResult primaryRes,
        Dictionary<string, object?> parameters,
        CancellationToken ct)
    {
        var columns = primaryRes.Columns;
        var rows = new List<CypherRow>(primaryRes.Rows);

        var reverseRes = await client.ExecuteCypherAsync(ReverseSemanticCallersQuery, parameters, ct);
        if (columns.Count == 0 && reverseRes.Columns.Count > 0) columns = reverseRes.Columns;
        rows.AddRange(reverseRes.Rows);

        var evidenceRes = await client.ExecuteCypherAsync(ServiceCallEvidenceQuery, parameters, ct);
        if (evidenceRes.Count > 0)
        {
            var (targetName, targetKind, funcIds) = ExtractEvidenceFunctionIds(evidenceRes);
            if (funcIds.Count > 0)
            {
                var idListLiteral = string.Join(CommaSep, funcIds.Select(QuoteLiteral));
                var targetProjection = $"{QuoteLiteral(targetName)} AS targetName, {QuoteLiteral(targetKind)} AS targetKind";
                var fnQuery = ResolveFunctionsByIdsPrefix + idListLiteral + ResolveFunctionsByIdsMid + targetProjection + ResolveFunctionsByIdsSuffix;
                var fnRes = await client.ExecuteCypherAsync(fnQuery, null, ct);
                if (columns.Count == 0 && fnRes.Columns.Count > 0) columns = fnRes.Columns;
                rows.AddRange(fnRes.Rows);
            }
        }

        return new CypherQueryResult(columns, rows);
    }

    private static string QuoteLiteral(string val) =>
        SingleQuote + val.Replace(SingleQuote, EscapedSingleQuote) + SingleQuote;

    private static (string TargetName, string TargetKind, HashSet<string> FuncIds) ExtractEvidenceFunctionIds(CypherQueryResult evidenceRes)
    {
        var funcIds = new HashSet<string>(StringComparer.Ordinal);
        var targetName = string.Empty;
        var targetKind = string.Empty;

        foreach (var row in evidenceRes.Rows)
        {
            if (string.IsNullOrEmpty(targetName))
            {
                targetName = row[0].AsString();
                targetKind = row[1].AsString();
            }

            AddFunctionIdsFromRawString(row[2].AsString(), funcIds);
        }

        return (targetName, targetKind, funcIds);
    }

    private static void AddFunctionIdsFromRawString(string raw, HashSet<string> funcIds)
    {
        if (string.IsNullOrEmpty(raw)) return;
        var cleaned = raw.Replace(EscapedQuoteUnicode, string.Empty);
        foreach (var part in cleaned.Split(EvidenceDelimiters, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Contains(SymPrefix, StringComparison.Ordinal))
            {
                funcIds.Add(part);
            }
        }
    }
}
