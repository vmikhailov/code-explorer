using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeExplorer.Core.Analysis;

public record IdentifierMatch(
    string NodeId,
    string Name,
    string FullName,
    string Kind,
    string? Path,
    int StartLine,
    float Score);

public class IdentifierSearchEngine
{
    private readonly ILogger _logger;

    public IdentifierSearchEngine(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task EnsureIndexAsync(SqliteConnection conn, CancellationToken ct = default)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS identifier_index (
                node_id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                full_name TEXT NOT NULL,
                kind TEXT NOT NULL,
                path TEXT,
                start_line INTEGER DEFAULT 0,
                embedding BLOB NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_ididx_name ON identifier_index(name);
            CREATE INDEX IF NOT EXISTS idx_ididx_fullname ON identifier_index(full_name);
            CREATE INDEX IF NOT EXISTS idx_ididx_kind ON identifier_index(kind);

            CREATE VIRTUAL TABLE IF NOT EXISTS identifier_fts USING fts5(
                name,
                full_name,
                path,
                node_id UNINDEXED,
                kind UNINDEXED,
                tokenize='trigram'
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct);

        // Check if index needs populating
        cmd.CommandText = "SELECT COUNT(*) FROM identifier_index;";
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        if (count == 0)
        {
            await RebuildIndexAsync(conn, ct);
        }
    }

    public async Task RebuildIndexAsync(SqliteConnection conn, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await using var initCmd = conn.CreateCommand();
        initCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS identifier_index (
                node_id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                full_name TEXT NOT NULL,
                kind TEXT NOT NULL,
                path TEXT,
                start_line INTEGER DEFAULT 0,
                embedding BLOB NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_ididx_name ON identifier_index(name);
            CREATE INDEX IF NOT EXISTS idx_ididx_fullname ON identifier_index(full_name);
            CREATE INDEX IF NOT EXISTS idx_ididx_kind ON identifier_index(kind);

            CREATE VIRTUAL TABLE IF NOT EXISTS identifier_fts USING fts5(
                name,
                full_name,
                path,
                node_id UNINDEXED,
                kind UNINDEXED,
                tokenize='trigram'
            );
            DELETE FROM identifier_index;
            DELETE FROM identifier_fts;
            DROP INDEX IF EXISTS idx_ididx_name;
            DROP INDEX IF EXISTS idx_ididx_fullname;
            DROP INDEX IF EXISTS idx_ididx_kind;
            DELETE FROM identifier_fts;
            """;
        await initCmd.ExecuteNonQueryAsync(ct);

        // Extract candidate nodes: Type, Function, Member, Endpoint, Table, Topic, Project, ExternalService
        // Use correlated subquery for owner_name to ensure SQLite uses primary key index lookup on nodes(id)
        // rather than full table scanning nodes across tens/hundreds of thousands of candidate rows.
        var candidateQuery = """
            SELECT n.id, n.kind,
                   COALESCE(json_extract(n.properties, '$.name'), json_extract(n.properties, '$.route_template'), json_extract(n.properties, '$.symbol'), n.id) AS name,
                   COALESCE(json_extract(n.properties, '$.path'), json_extract(n.properties, '$.file_path')) AS path,
                   COALESCE(CAST(json_extract(n.properties, '$.start_line') AS INTEGER), 0) AS start_line,
                   (SELECT json_extract(o.properties, '$.name') FROM nodes o WHERE o.id = e.from_id AND o.kind = 'Type') as owner_name
            FROM nodes n
            LEFT JOIN edges e ON (e.to_id = n.id AND e.kind IN ('HAS_METHOD', 'HAS_MEMBER'))
            WHERE n.kind IN ('Type', 'Function', 'Member', 'Endpoint', 'Table', 'Topic', 'Project', 'ExternalService');
            """;

        await using var selectCmd = conn.CreateCommand();
        selectCmd.CommandText = candidateQuery;
        await using var reader = await selectCmd.ExecuteReaderAsync(ct);

        var rawRows = new List<(string NodeId, string Name, string FullName, string Kind, string? Path, int StartLine)>();

        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetString(0);
            var kind = reader.GetString(1);
            var name = reader.IsDBNull(2) ? id : reader.GetString(2);
            var path = reader.IsDBNull(3) ? null : reader.GetString(3);
            var startLine = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
            var ownerName = reader.IsDBNull(5) ? null : reader.GetString(5);
            var fullName = !string.IsNullOrEmpty(ownerName) && !name.Contains('.') ? $"{ownerName}.{name}" : name;
            rawRows.Add((id, name, fullName, kind, path, startLine));
            if (false)
            {
            /*

            string name = id;
            string? path = null;
            int startLine = 0;

            try
            {
                using var doc = JsonDocument.Parse(propsJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("name", out var np)) name = np.GetString() ?? id;
                else if (root.TryGetProperty("route_template", out var rp)) name = rp.GetString() ?? id;
                else if (root.TryGetProperty("symbol", out var sp)) name = sp.GetString() ?? id;

                if (root.TryGetProperty("path", out var pp)) path = pp.GetString();
                else if (root.TryGetProperty("file_path", out var fpp)) path = fpp.GetString();

                if (root.TryGetProperty("start_line", out var slp) && slp.TryGetInt32(out var sl)) startLine = sl;
            }
            catch
            {
                // Ignore parse errors on corrupted JSON
            }

            var fullName = !string.IsNullOrEmpty(ownerName) && !name.Contains('.')
                ? $"{ownerName}.{name}"
                : name;

            // Combine name, full name, and path for vector representation
            var textToEmbed = $"{fullName} {name} {path ?? ""}";
            var vec = IdentifierVectorEncoder.Encode(textToEmbed);
            */

            }
        }
        var entries = new (string NodeId, string Name, string FullName, string Kind, string? Path, int StartLine, byte[] Embedding)[rawRows.Count];
        Parallel.For(0, rawRows.Count, i =>
        {
            var r = rawRows[i];
            var vec = IdentifierVectorEncoder.Encode($"{r.FullName} {r.Name} {r.Path ?? ""}");
            entries[i] = (r.NodeId, r.Name, r.FullName, r.Kind, r.Path, r.StartLine, IdentifierVectorEncoder.ToBytes(vec));
        });
        {
        }

        // Batch insert into identifier_index using a single prepared statement
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);
        await using (var insertCmd = conn.CreateCommand())
        {
            insertCmd.Transaction = tx;
            insertCmd.CommandText =
                "INSERT OR REPLACE INTO identifier_index (node_id, name, full_name, kind, path, start_line, embedding) VALUES (@id, @nm, @fn, @kd, @pt, @sl, @eb);";
            var pId = insertCmd.Parameters.Add("@id", SqliteType.Text);
            var pNm = insertCmd.Parameters.Add("@nm", SqliteType.Text);
            var pFn = insertCmd.Parameters.Add("@fn", SqliteType.Text);
            var pKd = insertCmd.Parameters.Add("@kd", SqliteType.Text);
            var pPt = insertCmd.Parameters.Add("@pt", SqliteType.Text);
            var pSl = insertCmd.Parameters.Add("@sl", SqliteType.Integer);
            var pEb = insertCmd.Parameters.Add("@eb", SqliteType.Blob);
            insertCmd.Prepare();

            foreach (var item in entries)
            {
                pId.Value = item.NodeId;
                pNm.Value = item.Name;
                pFn.Value = item.FullName;
                pKd.Value = item.Kind;
                pPt.Value = (object?)item.Path ?? DBNull.Value;
                pSl.Value = item.StartLine;
                pEb.Value = item.Embedding;
                insertCmd.ExecuteNonQuery();
            }
        }

        // Populate identifier_fts in a single statement directly from identifier_index (vastly faster than per-row string batches)
        await using var ftsCmd = conn.CreateCommand();
        ftsCmd.Transaction = tx;
        ftsCmd.CommandText = """
            INSERT INTO identifier_fts (name, full_name, path, node_id, kind)
            SELECT name, full_name, coalesce(path, ''), node_id, kind
            FROM identifier_index;
            CREATE INDEX IF NOT EXISTS idx_ididx_name ON identifier_index(name);
            CREATE INDEX IF NOT EXISTS idx_ididx_fullname ON identifier_index(full_name);
            CREATE INDEX IF NOT EXISTS idx_ididx_kind ON identifier_index(kind);
            """;
        await ftsCmd.ExecuteNonQueryAsync(ct);

        await tx.CommitAsync(ct);
        sw.Stop();
        _logger.LogDebug("[Database   ] Indexed {Count} identifiers into vector/trigram index in {ElapsedMs:F1}ms", entries.Length, sw.Elapsed.TotalMilliseconds);
    }

    public async Task<IReadOnlyList<IdentifierMatch>> SearchAsync(
        SqliteConnection conn,
        string query,
        string? kindFilter = null,
        int limit = 10,
        float minScore = 0.35f,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<IdentifierMatch>();
        }

        await EnsureIndexAsync(conn, ct);

        var queryTrim = query.Trim();
        var queryVec = IdentifierVectorEncoder.Encode(queryTrim);

        // 1. Exact match check
        await using var exactCmd = conn.CreateCommand();
        exactCmd.CommandText = """
            SELECT node_id, name, full_name, kind, path, start_line
            FROM identifier_index
            WHERE (name = @q OR full_name = @q OR node_id = @q OR full_name LIKE '%' || @q)
              AND (@kind IS NULL OR kind = @kind)
            LIMIT 5;
            """;
        exactCmd.Parameters.AddWithValue("@q", queryTrim);
        exactCmd.Parameters.AddWithValue("@kind", (object?)kindFilter ?? DBNull.Value);

        var exactMatches = new List<IdentifierMatch>();
        await using (var r = await exactCmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                exactMatches.Add(new IdentifierMatch(
                    r.GetString(0),
                    r.GetString(1),
                    r.GetString(2),
                    r.GetString(3),
                    r.IsDBNull(4) ? null : r.GetString(4),
                    r.GetInt32(5),
                    1.0f));
            }
        }

        if (exactMatches.Count > 0 && exactMatches.Any(m => m.Name.Equals(queryTrim, StringComparison.OrdinalIgnoreCase) || m.FullName.Equals(queryTrim, StringComparison.OrdinalIgnoreCase)))
        {
            return exactMatches;
        }

        // 2. Trigram FTS candidates
        var tokens = IdentifierVectorEncoder.Tokenize(queryTrim);
        var trigrams = new List<string>();
        foreach (var t in tokens)
        {
            if (t.Length >= 3)
            {
                trigrams.Add($"\"{t}\"");
            }
        }

        var candidateNodeIds = new HashSet<string>(exactMatches.Select(m => m.NodeId));
        if (trigrams.Count > 0)
        {
            var ftsExpr = string.Join(" OR ", trigrams);
            await using var ftsCmd = conn.CreateCommand();
            ftsCmd.CommandText = """
                SELECT node_id FROM identifier_fts
                WHERE identifier_fts MATCH @match
                  AND (@kind IS NULL OR kind = @kind)
                LIMIT 100;
                """;
            ftsCmd.Parameters.AddWithValue("@match", ftsExpr);
            ftsCmd.Parameters.AddWithValue("@kind", (object?)kindFilter ?? DBNull.Value);

            try
            {
                await using var ftsReader = await ftsCmd.ExecuteReaderAsync(ct);
                while (await ftsReader.ReadAsync(ct))
                {
                    candidateNodeIds.Add(ftsReader.GetString(0));
                }
            }
            catch
            {
                // Fallback to vector scan on FTS syntax error
            }
        }

        // 3. Fetch candidates (or all if candidate set is small) and compute CosineSimilarity
        var matches = new List<IdentifierMatch>(exactMatches);
        var selectSql = candidateNodeIds.Count > 0 && candidateNodeIds.Count < 200
            ? $"""
               SELECT node_id, name, full_name, kind, path, start_line, embedding
               FROM identifier_index
               WHERE node_id IN ({string.Join(",", candidateNodeIds.Select(id => $"'{EscapeSql(id)}'"))})
                 AND (@kind IS NULL OR kind = @kind);
               """
            : """
               SELECT node_id, name, full_name, kind, path, start_line, embedding
               FROM identifier_index
               WHERE (@kind IS NULL OR kind = @kind);
               """;

        await using var scoreCmd = conn.CreateCommand();
        scoreCmd.CommandText = selectSql;
        scoreCmd.Parameters.AddWithValue("@kind", (object?)kindFilter ?? DBNull.Value);

        await using var scoreReader = await scoreCmd.ExecuteReaderAsync(ct);
        while (await scoreReader.ReadAsync(ct))
        {
            var id = scoreReader.GetString(0);
            if (matches.Any(m => m.NodeId == id)) continue;

            var name = scoreReader.GetString(1);
            var fullName = scoreReader.GetString(2);
            var kind = scoreReader.GetString(3);
            var path = scoreReader.IsDBNull(4) ? null : scoreReader.GetString(4);
            var startLine = scoreReader.GetInt32(5);

            var blobLength = (int)scoreReader.GetBytes(6, 0, null, 0, 0);
            var blob = new byte[blobLength];
            scoreReader.GetBytes(6, 0, blob, 0, blobLength);

            var emb = IdentifierVectorEncoder.FromBytes(blob);
            var score = IdentifierVectorEncoder.CosineSimilarity(queryVec, emb);

            // Boost prefix or exact case matches
            if (name.Contains(queryTrim, StringComparison.OrdinalIgnoreCase) ||
                fullName.Contains(queryTrim, StringComparison.OrdinalIgnoreCase))
            {
                score = Math.Max(score, 0.85f);
            }

            if (score >= minScore)
            {
                matches.Add(new IdentifierMatch(id, name, fullName, kind, path, startLine, score));
            }
        }

        return matches
            .OrderByDescending(m => m.Score)
            .Take(limit)
            .ToList();
    }

    public async Task<IdentifierMatch?> ResolveIdentifierAsync(
        SqliteConnection conn,
        string target,
        string? kindFilter = null,
        CancellationToken ct = default)
    {
        var results = await SearchAsync(conn, target, kindFilter, limit: 1, minScore: 0.65f, ct);
        return results.Count > 0 ? results[0] : null;
    }

    private static string EscapeSql(string value) => value.Replace("'", "''");
}
