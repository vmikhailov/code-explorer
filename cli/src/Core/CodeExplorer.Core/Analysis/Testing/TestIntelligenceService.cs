using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Database;
using Microsoft.Data.Sqlite;

namespace CodeExplorer.Core.Analysis.Testing;

/// <summary>
/// Domain-agnostic service for static test reachability coverage and test impact analysis (TIA).
/// </summary>
public class TestIntelligenceService
{
    private readonly SqliteGraphClient? _client;
    private readonly SqliteConnection? _connection;

    public TestIntelligenceService(SqliteGraphClient client)
    {
        _client = client;
    }

    public TestIntelligenceService(SqliteConnection connection)
    {
        _connection = connection;
    }

    private async Task<T> WithConnectionAsync<T>(Func<SqliteConnection, Task<T>> action, CancellationToken ct = default)
    {
        if (_connection != null)
        {
            return await action(_connection);
        }

        if (_client != null)
        {
            return await _client.ExecuteRawAsync(action, ct);
        }

        throw new InvalidOperationException("TestIntelligenceService has no valid database client or connection.");
    }

    #region Test Coverage Analysis

    public async Task<TestCoverageReport> AnalyzeCoverageAsync(
        TestCoverageFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await WithConnectionAsync(async conn =>
        {
            // 1. Load all Type nodes (classes, records, structs) and Function nodes
            var (prodTypes, prodMethods, testMethods, typeToMethods) = await LoadCodebaseSymbolsAsync(conn, cancellationToken);

            // Apply project / path prefix filter if specified
            if (!string.IsNullOrWhiteSpace(filter.Project))
            {
                prodTypes =
                [
                    .. prodTypes.Where(t =>
                        string.Equals(t.Project, filter.Project, StringComparison.OrdinalIgnoreCase))
                ];
                prodMethods =
                [
                    .. prodMethods.Where(m =>
                        string.Equals(m.Project, filter.Project, StringComparison.OrdinalIgnoreCase))
                ];
            }

            if (!string.IsNullOrWhiteSpace(filter.PathPrefix))
            {
                var normPrefix = filter.PathPrefix.Replace('\\', '/').TrimStart('/');
                prodTypes =
                [
                    .. prodTypes.Where(t =>
                        t.FilePath.Replace('\\', '/').Contains(normPrefix, StringComparison.OrdinalIgnoreCase))
                ];
                prodMethods =
                [
                    .. prodMethods.Where(m =>
                        m.FilePath.Replace('\\', '/').Contains(normPrefix, StringComparison.OrdinalIgnoreCase))
                ];
            }

            // 2. Compute reachability from test methods using recursive CTE
            var reachedNodes = await ComputeTestReachabilityAsync(conn, [.. testMethods.Select(t => t.Id)], cancellationToken);

            // 3. Classify Methods
            var coveredMethods = new List<CoveredMethodInfo>();
            var uncoveredMethods = new List<UncoveredMethodInfo>();

            foreach (var m in prodMethods)
            {
                if (reachedNodes.TryGetValue(m.Id, out var reach))
                {
                    coveredMethods.Add(new CoveredMethodInfo(
                        m.Name,
                        m.Symbol,
                        m.ClassName,
                        m.FilePath,
                        m.StartLine,
                        m.EndLine,
                        m.Project,
                        reach.MinDepth,
                        reach.CoveringTests
                    ));
                }
                else
                {
                    uncoveredMethods.Add(new UncoveredMethodInfo(
                        m.Name,
                        m.Symbol,
                        m.ClassName,
                        m.FilePath,
                        m.StartLine,
                        m.EndLine,
                        m.Project
                    ));
                }
            }

            // 4. Classify Classes / Types
            var coveredClasses = new List<CoveredClassInfo>();
            var uncoveredClasses = new List<UncoveredClassInfo>();

            foreach (var t in prodTypes)
            {
                var methodsOfClass = typeToMethods.GetValueOrDefault(t.Id) ?? [];
                var coveredCount = methodsOfClass.Count(m => reachedNodes.ContainsKey(m.Id));
                var totalMethods = methodsOfClass.Count;

                var isDirectlyReached = reachedNodes.TryGetValue(t.Id, out var typeReach);
                var coveringTests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (isDirectlyReached && typeReach != null)
                {
                    foreach (var test in typeReach.CoveringTests) coveringTests.Add(test);
                }

                foreach (var m in methodsOfClass)
                {
                    if (reachedNodes.TryGetValue(m.Id, out var mr))
                    {
                        foreach (var test in mr.CoveringTests) coveringTests.Add(test);
                    }
                }

                var isCovered = coveredCount > 0 || isDirectlyReached;
                var pct = totalMethods > 0 ? (coveredCount * 100.0 / totalMethods) : (isCovered ? 100.0 : 0.0);

                if (isCovered)
                {
                    coveredClasses.Add(new CoveredClassInfo(
                        t.Name,
                        t.Symbol,
                        t.FilePath,
                        t.Project,
                        coveredCount,
                        totalMethods,
                        Math.Round(pct, 1),
                        coveringTests.Count
                    ));
                }
                else
                {
                    uncoveredClasses.Add(new UncoveredClassInfo(
                        t.Name,
                        t.Symbol,
                        t.FilePath,
                        t.StartLine,
                        t.EndLine,
                        t.Project,
                        totalMethods
                    ));
                }
            }

            // Summary metrics
            var totalClassesCount = prodTypes.Count;
            var coveredClassesCount = coveredClasses.Count;
            var uncoveredClassesCount = uncoveredClasses.Count;
            var classPct = totalClassesCount > 0 ? (coveredClassesCount * 100.0 / totalClassesCount) : 0.0;

            var totalMethodsCount = prodMethods.Count;
            var coveredMethodsCount = coveredMethods.Count;
            var uncoveredMethodsCount = uncoveredMethods.Count;
            var methodPct = totalMethodsCount > 0 ? (coveredMethodsCount * 100.0 / totalMethodsCount) : 0.0;

            var summary = new CoverageSummary(
                totalClassesCount,
                coveredClassesCount,
                uncoveredClassesCount,
                Math.Round(classPct, 1),
                totalMethodsCount,
                coveredMethodsCount,
                uncoveredMethodsCount,
                Math.Round(methodPct, 1)
            );

            // Apply status filter if requested
            if (string.Equals(filter.Status, "covered", StringComparison.OrdinalIgnoreCase))
            {
                uncoveredClasses = [];
                uncoveredMethods = [];
            }
            else if (string.Equals(filter.Status, "uncovered", StringComparison.OrdinalIgnoreCase))
            {
                coveredClasses = [];
                coveredMethods = [];
            }

            if (filter.Limit.HasValue && filter.Limit.Value > 0)
            {
                coveredClasses = [.. coveredClasses.Take(filter.Limit.Value)];
                uncoveredClasses = [.. uncoveredClasses.Take(filter.Limit.Value)];
                coveredMethods = [.. coveredMethods.Take(filter.Limit.Value)];
                uncoveredMethods = [.. uncoveredMethods.Take(filter.Limit.Value)];
            }

            return new TestCoverageReport(summary, coveredClasses, uncoveredClasses, coveredMethods, uncoveredMethods);
        }, cancellationToken);
    }

    private record RawTypeSymbol(string Id, string Name, string Symbol, string FilePath, int StartLine, int EndLine, string? Project);
    private record RawFunctionSymbol(string Id, string Name, string Symbol, string? ClassName, string FilePath, int StartLine, int EndLine, string? Project);
    private record RawTestFunction(string Id, string Name, string Symbol, string FilePath, string? Framework);
    private record ReachInfo(int MinDepth, List<string> CoveringTests);

    private static async Task<(
        List<RawTypeSymbol> Types,
        List<RawFunctionSymbol> Methods,
        List<RawTestFunction> TestMethods,
        Dictionary<string, List<RawFunctionSymbol>> TypeToMethods)>
        LoadCodebaseSymbolsAsync(SqliteConnection conn, CancellationToken ct)
    {
        var types = new List<RawTypeSymbol>();
        var methods = new List<RawFunctionSymbol>();
        var testMethods = new List<RawTestFunction>();
        var typeToMethods = new Dictionary<string, List<RawFunctionSymbol>>();

        // Query HAS_METHOD edges to map classes to methods
        var methodToType = new Dictionary<string, (string TypeId, string TypeName)>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT e.from_id, e.to_id, json_extract(n.properties, '$.name')
                FROM edges e
                JOIN nodes n ON e.from_id = n.id
                WHERE e.kind = 'HAS_METHOD';
                """;

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var typeId = reader.GetString(0);
                var methodId = reader.GetString(1);
                var typeName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                methodToType[methodId] = (typeId, typeName);
            }
        }

        // Query Nodes
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, kind, properties FROM nodes WHERE kind IN ('Type', 'Function');";

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetString(0);
                var kind = reader.GetString(1);
                var propsJson = reader.GetString(2);

                using var doc = JsonDocument.Parse(propsJson);
                var root = doc.RootElement;

                var filePath = ResolveBestFilePath(root);
                var name = root.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";
                var symbol = root.TryGetProperty("symbol", out var sProp) ? sProp.GetString() ?? name : name;
                var sLine = root.TryGetProperty("start_line", out var slProp) ? slProp.GetInt32() : 0;
                var eLine = root.TryGetProperty("end_line", out var elProp) ? elProp.GetInt32() : sLine;
                var project = root.TryGetProperty("project", out var prProp) ? prProp.GetString() : null;
                var isTestProp = root.TryGetProperty("is_test", out var itProp) && string.Equals(itProp.GetString(), "true", StringComparison.OrdinalIgnoreCase);

                var normPath = filePath.Replace('\\', '/').ToLowerInvariant();
                var isTestPath = normPath.Contains("/test/") || normPath.Contains("/tests/") ||
                                 normPath.EndsWith("_test.go") || normPath.EndsWith(".test.ts") || normPath.EndsWith(".test.js") ||
                                 normPath.EndsWith(".spec.ts") || normPath.EndsWith(".spec.js") ||
                                 normPath.StartsWith("test_") || normPath.Contains("/test_") ||
                                 normPath.Contains("testdata") || normPath.Contains("fixtures");

                var isTest = isTestProp || isTestPath;

                if (kind == "Function")
                {
                    if (isTest)
                    {
                        var fw = root.TryGetProperty("test_framework", out var fwProp) ? fwProp.GetString() : null;
                        testMethods.Add(new RawTestFunction(id, name, symbol, filePath, fw));
                    }
                    else
                    {
                        var className = methodToType.TryGetValue(id, out var tInfo) ? tInfo.TypeName : null;
                        var funcSymbol = new RawFunctionSymbol(id, name, symbol, className, filePath, sLine, eLine, project);
                        methods.Add(funcSymbol);

                        if (methodToType.TryGetValue(id, out var tRef))
                        {
                            if (!typeToMethods.TryGetValue(tRef.TypeId, out var list))
                            {
                                list = [];
                                typeToMethods[tRef.TypeId] = list;
                            }
                            list.Add(funcSymbol);
                        }
                    }
                }
                else if (kind == "Type")
                {
                    var typeKind = root.TryGetProperty("kind", out var tkProp) ? tkProp.GetString() : "class";
                    if (!isTest && !string.Equals(typeKind, "interface", StringComparison.OrdinalIgnoreCase))
                    {
                        types.Add(new RawTypeSymbol(id, name, symbol, filePath, sLine, eLine, project));
                    }
                }
            }
        }

        return (types, methods, testMethods, typeToMethods);
    }

    private static async Task<Dictionary<string, ReachInfo>> ComputeTestReachabilityAsync(
        SqliteConnection conn,
        List<string> testMethodIds,
        CancellationToken ct)
    {
        var result = new Dictionary<string, ReachInfo>(StringComparer.Ordinal);
        if (testMethodIds.Count == 0) return result;

        // Use temporary table for test IDs to avoid sqlite variable limits
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE TEMP TABLE IF NOT EXISTS temp_test_ids (id TEXT PRIMARY KEY); DELETE FROM temp_test_ids;";
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var tx = conn.BeginTransaction())
        {
            await BulkInsertTempIdsAsync(conn, tx, "temp_test_ids", testMethodIds, ct);
            tx.Commit();
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                WITH RECURSIVE reach AS (
                    -- Anchor: direct calls or uses from test methods
                    SELECT
                        t.id AS test_id,
                        COALESCE(json_extract(n.properties, '$.name'), t.id) AS test_name,
                        e.to_id AS target_id,
                        1 AS depth
                    FROM temp_test_ids t
                    JOIN nodes n ON t.id = n.id
                    JOIN edges e ON t.id = e.from_id
                    WHERE e.kind IN ('CALLS', 'USES_TYPE')

                    UNION

                    -- Recursive step: follow further calls
                    SELECT
                        r.test_id,
                        r.test_name,
                        e.to_id AS target_id,
                        r.depth + 1
                    FROM reach r
                    JOIN edges e ON r.target_id = e.from_id
                    WHERE e.kind IN ('CALLS', 'USES_TYPE')
                      AND r.depth < 12
                )
                SELECT
                    target_id,
                    MIN(depth) AS min_depth,
                    GROUP_CONCAT(DISTINCT test_name) AS covering_tests
                FROM reach
                GROUP BY target_id;
                """;

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var targetId = reader.GetString(0);
                var minDepth = reader.GetInt32(1);
                var testsStr = reader.IsDBNull(2) ? "" : reader.GetString(2);
                var testList = testsStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

                result[targetId] = new ReachInfo(minDepth, testList);
            }
        }

        return result;
    }

    #endregion

    #region Test Impact Analysis (TIA)

    public async Task<TestImpactReport> AnalyzeImpactAsync(
        TestImpactRequest request,
        CancellationToken cancellationToken = default)
    {
        return await WithConnectionAsync(async conn =>
        {
            var changedHunks = new List<ChangedHunk>();
            var changedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Resolve changed files / hunks
            if (!string.IsNullOrWhiteSpace(request.GitDiff))
            {
                changedHunks = GitDiffHelper.ParseDiffHunks(request.GitDiff);
                foreach (var h in changedHunks) changedFiles.Add(h.File);
            }
            else if (request.ChangedFiles != null && request.ChangedFiles.Count > 0)
            {
                foreach (var f in request.ChangedFiles)
                {
                    var clean = f.Trim().Replace('\\', '/');
                    if (!string.IsNullOrEmpty(clean)) changedFiles.Add(clean);
                }
            }
            else
            {
                // Attempt automatic git diff detection
                var diff = await GitDiffHelper.GetGitDiffAsync(request.WorkspaceRoot, request.GitBase, cancellationToken);
                if (!string.IsNullOrWhiteSpace(diff))
                {
                    changedHunks = GitDiffHelper.ParseDiffHunks(diff);
                    foreach (var h in changedHunks) changedFiles.Add(h.File);
                }
                else
                {
                    var gitFiles = await GitDiffHelper.GetGitChangedFilesAsync(request.WorkspaceRoot, request.GitBase, cancellationToken);
                    foreach (var f in gitFiles) changedFiles.Add(f);
                }

                // Also include newly created (untracked) files in working directory
                var untracked = await GitDiffHelper.GetGitUntrackedFilesAsync(request.WorkspaceRoot, cancellationToken);
                foreach (var u in untracked) changedFiles.Add(u);
            }

            // 2. Identify modified AST symbols in the database
            var (changedSymbols, changedIds) = await FindChangedSymbolsAsync(conn, changedFiles, changedHunks, request.SymbolNames, cancellationToken);

            var affectedTestMap = new Dictionary<string, AffectedTestMethod>(StringComparer.OrdinalIgnoreCase);
            var prodChangedIds = new HashSet<string>(StringComparer.Ordinal);

            // 2. Identify directly modified test code vs production code
            foreach (var sym in changedSymbols)
            {
                var normPath = sym.FilePath.Replace('\\', '/').ToLowerInvariant();
                var isTestFile = normPath.Contains("/test/") || normPath.Contains("/tests/") ||
                                 normPath.EndsWith("_test.go") || normPath.EndsWith(".test.ts") || normPath.EndsWith(".spec.ts");

                if (isTestFile)
                {
                    if (sym.Kind == "Function")
                    {
                        affectedTestMap[sym.Name] = new AffectedTestMethod(
                            sym.Name,
                            sym.Symbol,
                            ExtractClassNameFromSymbol(sym.Symbol),
                            sym.FilePath,
                            sym.StartLine,
                            DetectFramework(sym.FilePath),
                            "Directly modified test code",
                            0, [sym.Name],
                            sym.Symbol,
                            sym.FilePath
                        );
                    }
                }
                else
                {
                    prodChangedIds.Add(sym.Id);
                }
            }

            if (prodChangedIds.Count > 0)
            {
                // 3. Traverse reverse graph up to test methods
                var reverseAffected = await TraverseReverseToTestsAsync(conn, prodChangedIds, request.MaxDepth, cancellationToken);
                foreach (var t in reverseAffected)
                {
                    if (!affectedTestMap.TryGetValue(t.TestMethodName, out var existing) || t.Depth < existing.Depth)
                    {
                        affectedTestMap[t.TestMethodName] = t;
                    }
                }
            }

            var affectedList = affectedTestMap.Values
                .OrderBy(t => t.TestFilePath)
                .ThenBy(t => t.Depth)
                .ThenBy(t => t.TestMethodName)
                .ToList();

            // 4. Build smart test groups by class / test file
            var (updatedTests, groups) = await BuildAffectedTestGroupsAsync(conn, affectedList, cancellationToken);

            // 5. Generate CLI runner commands (taking groups into account)
            var runnerCommands = BuildRunnerCommands(updatedTests, groups);

            return new TestImpactReport(updatedTests, changedSymbols, runnerCommands, groups);
        }, cancellationToken);
    }

    private static async Task<(List<ChangedSymbolInfo> Symbols, HashSet<string> Ids)>
        FindChangedSymbolsAsync(
            SqliteConnection conn,
            HashSet<string> changedFiles,
            List<ChangedHunk> changedHunks,
            IReadOnlyList<string>? explicitSymbols,
            CancellationToken ct)
    {
        var symbols = new List<ChangedSymbolInfo>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var hunkMap = changedHunks.GroupBy(h => h.File, StringComparer.OrdinalIgnoreCase)
                                  .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var explicitSet = explicitSymbols != null && explicitSymbols.Count > 0
            ? new HashSet<string>(explicitSymbols, StringComparer.OrdinalIgnoreCase)
            : null;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, kind, properties FROM nodes WHERE kind IN ('Function', 'Type', 'Member', 'class', 'interface');";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetString(0);
            var kind = reader.GetString(1);
            var propsJson = reader.GetString(2);

            using var doc = JsonDocument.Parse(propsJson);
            var root = doc.RootElement;

            var filePath = ResolveBestFilePath(root);
            var name = root.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";
            var symbol = root.TryGetProperty("symbol", out var sProp) ? sProp.GetString() ?? name : name;
            var sLine = root.TryGetProperty("start_line", out var slProp) ? slProp.GetInt32() : 0;
            var eLine = root.TryGetProperty("end_line", out var elProp) ? elProp.GetInt32() : sLine;

            var normPath = filePath.Replace('\\', '/').TrimStart('/');

            // Check if matches explicit symbols
            if (explicitSet != null && (explicitSet.Contains(name) || explicitSet.Contains(symbol)))
            {
                if (ids.Add(id))
                {
                    symbols.Add(new ChangedSymbolInfo(id, name, symbol, kind, filePath, sLine, eLine));
                }
                continue;
            }

            // Check if matches changed files / hunks
            foreach (var cf in changedFiles)
            {
                var normCf = cf.Replace('\\', '/').TrimStart('/');
                if (normPath.Equals(normCf, StringComparison.OrdinalIgnoreCase) ||
                    normPath.EndsWith("/" + normCf, StringComparison.OrdinalIgnoreCase) ||
                    normCf.EndsWith("/" + normPath, StringComparison.OrdinalIgnoreCase))
                {
                    // If we have line hunks for this file, match line ranges
                    if ((hunkMap.TryGetValue(cf, out var hunks) || hunkMap.TryGetValue(normPath, out hunks)) && hunks != null)
                    {
                        var overlaps = hunks.Any(h => sLine <= h.EndLine && eLine >= h.StartLine);
                        if (overlaps)
                        {
                            if (ids.Add(id))
                            {
                                symbols.Add(new ChangedSymbolInfo(id, name, symbol, kind, filePath, sLine, eLine));
                            }
                        }
                    }
                    else
                    {
                        // No specific hunk line range, entire file is marked changed
                        if (ids.Add(id))
                        {
                            symbols.Add(new ChangedSymbolInfo(id, name, symbol, kind, filePath, sLine, eLine));
                        }
                    }
                    break;
                }
            }
        }

        return (symbols, ids);
    }

    private static async Task<List<AffectedTestMethod>> TraverseReverseToTestsAsync(
        SqliteConnection conn,
        HashSet<string> changedIds,
        int maxDepth,
        CancellationToken ct)
    {
        var result = new List<AffectedTestMethod>();
        if (changedIds.Count == 0) return result;

        var paths = new Dictionary<string, (string InitialTargetName, string? InitialTargetFile, List<string> Path)>(StringComparer.Ordinal);
        var visited = new HashSet<string>(changedIds, StringComparer.Ordinal);
        var testResults = new Dictionary<string, AffectedTestMethod>(StringComparer.OrdinalIgnoreCase);

        // Preload names and files of changed nodes
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE TEMP TABLE IF NOT EXISTS temp_frontier (id TEXT PRIMARY KEY); DELETE FROM temp_frontier;";
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var tx = conn.BeginTransaction())
        {
            await BulkInsertTempIdsAsync(conn, tx, "temp_frontier", changedIds, ct);
            tx.Commit();
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT f.id,
                       COALESCE(json_extract(n.properties, '$.name'), f.id) AS name,
                       COALESCE(json_extract(n.properties, '$.path'), json_extract(n.properties, '$.file_path')) AS file_path
                FROM temp_frontier f
                LEFT JOIN nodes n ON f.id = n.id;
                """;
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetString(0);
                var name = reader.GetString(1);
                var file = reader.IsDBNull(2) ? null : reader.GetString(2);
                paths[id] = (name, file, [name]);
            }
        }

        var currentFrontier = new List<string>(changedIds);

        for (var depth = 1; depth <= maxDepth && currentFrontier.Count > 0; depth++)
        {
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM temp_frontier;";
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await using (var tx = conn.BeginTransaction())
            {
                await BulkInsertTempIdsAsync(conn, tx, "temp_frontier", currentFrontier, ct);
                tx.Commit();
            }

            var nextFrontier = new List<string>();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT e.from_id,
                           e.to_id,
                           e.kind,
                           fn.properties,
                           COALESCE(json_extract(fn.properties, '$.name'), e.from_id) AS caller_name
                    FROM temp_frontier f
                    JOIN edges e ON e.to_id = f.id
                    JOIN nodes fn ON e.from_id = fn.id
                    WHERE e.kind IN ('CALLS', 'USES_TYPE', 'IMPLEMENTS', 'INHERITS_FROM', 'HAS_METHOD');
                    """;

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var callerId = reader.GetString(0);
                    var toId = reader.GetString(1);
                    var edgeKind = reader.GetString(2);
                    var propsJson = reader.GetString(3);
                    var callerName = reader.GetString(4);

                    if (!paths.TryGetValue(toId, out var targetPath))
                    {
                        targetPath = (toId, null, [toId]);
                    }

                    var newPath = new List<string>(targetPath.Path.Count + 1) { callerName };
                    newPath.AddRange(targetPath.Path);

                    using var doc = JsonDocument.Parse(propsJson);
                    var root = doc.RootElement;

                    var name = root.TryGetProperty("name", out var np) ? np.GetString() ?? callerName : callerName;
                    var symbol = root.TryGetProperty("symbol", out var sp) ? sp.GetString() ?? name : name;
                    var filePath = ResolveBestFilePath(root);
                    var sLine = root.TryGetProperty("start_line", out var slp) ? slp.GetInt32() : 0;
                    var framework = root.TryGetProperty("test_framework", out var tfp) ? tfp.GetString() : DetectFramework(filePath);

                    var isTest = false;
                    if (root.TryGetProperty("is_test", out var itp) && string.Equals(itp.GetString(), "true", StringComparison.OrdinalIgnoreCase))
                    {
                        isTest = true;
                    }
                    else
                    {
                        var normPath = filePath.Replace('\\', '/').ToLowerInvariant();
                        if (normPath.Contains("/test/") || normPath.Contains("/tests/") ||
                            normPath.EndsWith("_test.go") || normPath.EndsWith(".test.ts") || normPath.EndsWith(".spec.ts"))
                        {
                            isTest = true;
                        }
                    }

                    if (isTest)
                    {
                        var reason = depth == 1
                            ? $"Direct {edgeKind} to {targetPath.InitialTargetName}"
                            : $"Transitive {edgeKind} (depth {depth}) to {targetPath.InitialTargetName}";

                        var testMethod = new AffectedTestMethod(
                            name,
                            symbol,
                            ExtractClassNameFromSymbol(symbol),
                            filePath,
                            sLine,
                            framework,
                            reason,
                            depth,
                            newPath,
                            targetPath.InitialTargetName,
                            targetPath.InitialTargetFile
                        );

                        if (!testResults.TryGetValue(name, out var existing) || depth < existing.Depth)
                        {
                            testResults[name] = testMethod;
                        }
                    }

                    if (visited.Add(callerId))
                    {
                        paths[callerId] = (targetPath.InitialTargetName, targetPath.InitialTargetFile, newPath);
                        nextFrontier.Add(callerId);
                    }
                }
            }

            currentFrontier = nextFrontier;
        }

        return [.. testResults.Values.OrderBy(t => t.Depth).ThenBy(t => t.TestMethodName)];
    }

    private static string? ExtractClassNameFromSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return null;

        if (symbol.StartsWith("ws:"))
        {
            var colonParts = symbol.Split(':');
            var rawName = colonParts[^1];
            if (rawName.Contains('.'))
            {
                var dotParts = rawName.Split('.');
                if (dotParts.Length >= 2)
                {
                    var c = dotParts[^2];
                    return (c.Contains('/') || c.Contains('\\')) ? Path.GetFileNameWithoutExtension(c.Replace('\\', '/')) : c;
                }
            }
            return null;
        }

        var parts = symbol.Split('.');
        if (parts.Length >= 2)
        {
            var candidate = parts[^2];
            if (candidate.Contains('/') || candidate.Contains('\\'))
            {
                candidate = Path.GetFileNameWithoutExtension(candidate.Replace('\\', '/'));
            }
            return candidate;
        }
        return null;
    }

    private static string DetectFramework(string filePath)
    {
        var norm = filePath.Replace('\\', '/').ToLowerInvariant();
        if (norm.EndsWith(".cs")) return "dotnet";
        if (norm.EndsWith(".go")) return "go-testing";
        if (norm.EndsWith(".py")) return "pytest";
        if (norm.EndsWith(".ts") || norm.EndsWith(".js")) return "jest";
        if (norm.EndsWith(".java")) return "junit";
        return "generic";
    }

    private static string ResolveBestFilePath(JsonElement root)
    {
        var path = root.TryGetProperty("path", out var pp) ? pp.GetString() ?? "" : "";
        var filePath = root.TryGetProperty("file_path", out var fp) ? fp.GetString() ?? "" : "";

        if (path.Contains('/') || path.Contains('\\'))
        {
            return path;
        }

        if (filePath.Contains('/') || filePath.Contains('\\'))
        {
            return filePath;
        }

        return !string.IsNullOrEmpty(path) ? path : filePath;
    }

    private static string? DeriveClassNameFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;
        var norm = filePath.Replace('\\', '/');
        var fileName = Path.GetFileNameWithoutExtension(norm);
        if (fileName.EndsWith("Test", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith("Tests", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("Test", StringComparison.OrdinalIgnoreCase))
        {
            return fileName;
        }
        return null;
    }

    private static async Task<(List<AffectedTestMethod> UpdatedTests, List<AffectedTestGroup> Groups)>
        BuildAffectedTestGroupsAsync(
            SqliteConnection conn,
            List<AffectedTestMethod> tests,
            CancellationToken ct)
    {
        if (tests.Count == 0) return ([], []);

        // 1. Query test functions and their parent types (classes/records/structs) in SQLite
        var classTotalCounts = new Dictionary<(string FilePath, string ClassName), int>();
        var classTotalByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var fileTotalCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var methodToClass = new Dictionary<(string FilePath, string MethodName), string>();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT 
                    COALESCE(json_extract(f.properties, '$.name'), '') AS name,
                    COALESCE(json_extract(f.properties, '$.symbol'), '') AS symbol,
                    COALESCE(json_extract(f.properties, '$.path'), json_extract(f.properties, '$.file_path'), '') AS file_path,
                    COALESCE(json_extract(p.properties, '$.name'), '') AS class_name
                FROM nodes f
                LEFT JOIN edges e ON e.to_id = f.id AND e.kind IN ('HAS_METHOD', 'CONTAINS')
                LEFT JOIN nodes p ON e.from_id = p.id AND p.kind IN ('Type', 'Class', 'Record', 'class', 'interface')
                WHERE f.kind IN ('Function', 'Method')
                  AND (
                      json_extract(f.properties, '$.is_test') = 'true'
                      OR f.properties LIKE '%"is_test"%"true"%'
                      OR f.properties LIKE '%"is_test":true%'
                      OR REPLACE(COALESCE(json_extract(f.properties, '$.path'), json_extract(f.properties, '$.file_path'), ''), '\', '/') LIKE '%/test/%'
                      OR REPLACE(COALESCE(json_extract(f.properties, '$.path'), json_extract(f.properties, '$.file_path'), ''), '\', '/') LIKE '%/tests/%'
                      OR REPLACE(COALESCE(json_extract(f.properties, '$.path'), json_extract(f.properties, '$.file_path'), ''), '\', '/') LIKE '%test%'
                  );
                """;

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var name = reader.GetString(0);
                var symbol = reader.GetString(1);
                var filePath = reader.GetString(2);
                var className = reader.GetString(3);

                var normFile = filePath.Replace('\\', '/').TrimStart('/');

                if (string.IsNullOrWhiteSpace(className))
                {
                    className = ExtractClassNameFromSymbol(symbol) ?? DeriveClassNameFromFile(normFile) ?? "";
                }

                if (!string.IsNullOrWhiteSpace(className) && (className.Contains('/') || className.Contains('\\')))
                {
                    className = Path.GetFileNameWithoutExtension(className.Replace('\\', '/'));
                }

                if (!string.IsNullOrWhiteSpace(className))
                {
                    var classKey = (normFile, className);
                    classTotalCounts[classKey] = classTotalCounts.GetValueOrDefault(classKey, 0) + 1;
                    classTotalByName[className] = classTotalByName.GetValueOrDefault(className, 0) + 1;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        methodToClass[(normFile, name)] = className;
                    }
                }

                fileTotalCounts[normFile] = fileTotalCounts.GetValueOrDefault(normFile, 0) + 1;
            }
        }

        // 2. Resolve class name for all affected tests and update records
        var updatedTests = new List<AffectedTestMethod>(tests.Count);
        foreach (var t in tests)
        {
            var normFile = t.TestFilePath.Replace('\\', '/').TrimStart('/');
            var resolvedClass = t.TestClassName;

            if (string.IsNullOrWhiteSpace(resolvedClass) && methodToClass.TryGetValue((normFile, t.TestMethodName), out var mc))
            {
                resolvedClass = mc;
            }

            if (string.IsNullOrWhiteSpace(resolvedClass))
            {
                resolvedClass = ExtractClassNameFromSymbol(t.TestSymbol);
            }

            if (string.IsNullOrWhiteSpace(resolvedClass))
            {
                resolvedClass = DeriveClassNameFromFile(t.TestFilePath);
            }

            if (!string.IsNullOrWhiteSpace(resolvedClass) && (resolvedClass.Contains('/') || resolvedClass.Contains('\\')))
            {
                resolvedClass = Path.GetFileNameWithoutExtension(resolvedClass.Replace('\\', '/'));
            }

            updatedTests.Add(t with { TestClassName = resolvedClass });
        }

        // 3. Group by (FilePath, ClassName)
        var groups = new List<AffectedTestGroup>();
        var grouped = updatedTests.GroupBy(t => (
            NormFile: t.TestFilePath.Replace('\\', '/').TrimStart('/'),
            ClassName: t.TestClassName ?? ""
        ));

        foreach (var g in grouped)
        {
            var groupMethods = g.ToList();
            var first = groupMethods[0];
            var normFile = g.Key.NormFile;
            var className = string.IsNullOrWhiteSpace(g.Key.ClassName) ? null : g.Key.ClassName;
            var affectedCount = groupMethods.Count;

            int totalCount;
            if (className != null)
            {
                totalCount = classTotalCounts.GetValueOrDefault((normFile, className), 0);
                if (totalCount == 0 && classTotalByName.TryGetValue(className, out var byName))
                {
                    totalCount = byName;
                }
                if (totalCount == 0) totalCount = affectedCount;
            }
            else
            {
                totalCount = fileTotalCounts.GetValueOrDefault(normFile, affectedCount);
                if (totalCount == 0) totalCount = affectedCount;
            }

            if (totalCount < affectedCount) totalCount = affectedCount;

            var allAffected = totalCount > 0 && affectedCount >= totalCount;
            var groupName = className ?? Path.GetFileName(first.TestFilePath);

            groups.Add(new AffectedTestGroup(
                GroupName: groupName,
                ClassName: className,
                FilePath: first.TestFilePath,
                TestFramework: first.TestFramework ?? DetectFramework(first.TestFilePath),
                AffectedTestCount: affectedCount,
                TotalTestCount: totalCount,
                AllTestsAffected: allAffected,
                Methods: groupMethods
            ));
        }

        return (updatedTests, groups);
    }

    public static Dictionary<string, string> BuildRunnerCommands(
        IReadOnlyList<AffectedTestMethod> tests,
        IReadOnlyList<AffectedTestGroup>? groups = null)
    {
        var commands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (tests.Count == 0) return commands;

        if (groups == null || groups.Count == 0)
        {
            var byFramework = tests.GroupBy(t => t.TestFramework ?? DetectFramework(t.TestFilePath));
            foreach (var group in byFramework)
            {
                var fw = group.Key.ToLowerInvariant();
                if (fw is "dotnet" or "nunit" or "xunit" or "mstest" or "csharp")
                {
                    var filters = group.Select(t => $"FullyQualifiedName~{t.TestMethodName}").Distinct();
                    commands["dotnet"] = $"dotnet test --filter \"{string.Join("|", filters)}\"";
                }
                else if (fw is "go-testing" or "go" or "gotest")
                {
                    var names = group.Select(t => t.TestMethodName).Distinct();
                    commands["go"] = $"go test ./... -run \"^({string.Join("|", names)})$\"";
                }
                else if (fw is "pytest" or "python" or "unittest")
                {
                    var testNames = group.Select(t => t.TestMethodName).Distinct();
                    var files = group.Select(t => t.TestFilePath).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct().ToList();
                    var filePrefix = files.Count > 0 ? string.Join(" ", files) + " " : "";
                    commands["pytest"] = $"pytest {filePrefix}-k \"{string.Join(" or ", testNames)}\"";
                }
                else if (fw is "jest" or "vitest" or "js" or "ts" or "mocha")
                {
                    var titles = group.Select(t => Regex.Escape(t.TestMethodName)).Distinct();
                    var files = group.Select(t => t.TestFilePath).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct().ToList();
                    var filePrefix = files.Count > 0 ? string.Join(" ", files) + " " : "";
                    commands["jest"] = $"npm test -- {filePrefix}-t \"{string.Join("|", titles)}\"";
                }
                else if (fw is "junit" or "java" or "testng")
                {
                    var testNames = group.Select(t => t.TestMethodName).Distinct();
                    commands["junit"] = $"mvn test -Dtest=\"{string.Join(",", testNames)}\"";
                }
            }
            return commands;
        }

        var groupedFw = groups.GroupBy(g => g.TestFramework ?? DetectFramework(g.FilePath));

        foreach (var fwGroup in groupedFw)
        {
            var fw = fwGroup.Key.ToLowerInvariant();
            if (fw is "dotnet" or "nunit" or "xunit" or "mstest" or "csharp")
            {
                var filters = new List<string>();
                foreach (var g in fwGroup)
                {
                    if (g.AllTestsAffected && !string.IsNullOrWhiteSpace(g.ClassName))
                    {
                        filters.Add($"FullyQualifiedName~{g.ClassName}");
                    }
                    else
                    {
                        foreach (var m in g.Methods)
                        {
                            filters.Add($"FullyQualifiedName~{m.TestMethodName}");
                        }
                    }
                }
                commands["dotnet"] = $"dotnet test --filter \"{string.Join("|", filters.Distinct())}\"";
            }
            else if (fw is "go-testing" or "go" or "gotest")
            {
                var names = fwGroup.SelectMany(g => g.Methods.Select(m => m.TestMethodName)).Distinct();
                commands["go"] = $"go test ./... -run \"^({string.Join("|", names)})$\"";
            }
            else if (fw is "pytest" or "python" or "unittest")
            {
                var files = fwGroup.Select(g => g.FilePath).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct().ToList();
                var filePrefix = files.Count > 0 ? string.Join(" ", files) + " " : "";

                var patterns = new List<string>();
                var allFilesWhole = fwGroup.All(g => g.AllTestsAffected && string.IsNullOrEmpty(g.ClassName));
                if (allFilesWhole && files.Count > 0)
                {
                    commands["pytest"] = $"pytest {filePrefix.TrimEnd()}";
                }
                else
                {
                    foreach (var g in fwGroup)
                    {
                        if (g.AllTestsAffected && !string.IsNullOrWhiteSpace(g.ClassName))
                        {
                            patterns.Add(g.ClassName);
                        }
                        else
                        {
                            patterns.AddRange(g.Methods.Select(m => m.TestMethodName));
                        }
                    }
                    commands["pytest"] = $"pytest {filePrefix}-k \"{string.Join(" or ", patterns.Distinct())}\"";
                }
            }
            else if (fw is "jest" or "vitest" or "js" or "ts" or "mocha")
            {
                var files = fwGroup.Select(g => g.FilePath).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct().ToList();
                var filePrefix = files.Count > 0 ? string.Join(" ", files) + " " : "";

                var allWhole = fwGroup.All(g => g.AllTestsAffected);
                if (allWhole && files.Count > 0)
                {
                    commands["jest"] = $"npm test -- {filePrefix.TrimEnd()}";
                }
                else
                {
                    var titles = fwGroup.SelectMany(g => g.Methods.Select(m => Regex.Escape(m.TestMethodName))).Distinct();
                    commands["jest"] = $"npm test -- {filePrefix}-t \"{string.Join("|", titles)}\"";
                }
            }
            else if (fw is "junit" or "java" or "testng")
            {
                var patterns = new List<string>();
                foreach (var g in fwGroup)
                {
                    if (g.AllTestsAffected && !string.IsNullOrWhiteSpace(g.ClassName))
                    {
                        patterns.Add(g.ClassName);
                    }
                    else if (!string.IsNullOrWhiteSpace(g.ClassName))
                    {
                        var mNames = string.Join("+", g.Methods.Select(m => m.TestMethodName).Distinct());
                        patterns.Add($"{g.ClassName}#{mNames}");
                    }
                    else
                    {
                        patterns.AddRange(g.Methods.Select(m => m.TestMethodName));
                    }
                }
                commands["junit"] = $"mvn test -Dtest=\"{string.Join(",", patterns.Distinct())}\"";
            }
        }

        return commands;
    }

    #endregion

    #region Markdown & Console Formatting Helpers

    public static string FormatCoverageMarkdown(TestCoverageReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# 🧪 CodeExplorer Static Test Coverage Report");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Metric | Covered | Uncovered | Total | Coverage % |");
        sb.AppendLine("|---|---|---|---|---|");
        sb.AppendLine($"| **Classes / Types** | {report.Summary.CoveredClasses} | {report.Summary.UncoveredClasses} | {report.Summary.TotalClasses} | **{report.Summary.ClassCoveragePercentage:F1}%** |");
        sb.AppendLine($"| **Methods / Functions** | {report.Summary.CoveredMethods} | {report.Summary.UncoveredMethods} | {report.Summary.TotalMethods} | **{report.Summary.MethodCoveragePercentage:F1}%** |");
        sb.AppendLine();

        if (report.UncoveredClasses.Count > 0)
        {
            sb.AppendLine("## ⚠️ Uncovered Classes (Zero Test Paths)");
            sb.AppendLine();
            sb.AppendLine("| Class | File | Lines | Project | Methods |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var c in report.UncoveredClasses)
            {
                sb.AppendLine($"| `{c.Name}` | `{c.FilePath}` | L{c.StartLine}-L{c.EndLine} | {c.Project ?? "-"} | {c.MethodCount} |");
            }
            sb.AppendLine();
        }

        if (report.UncoveredMethods.Count > 0)
        {
            sb.AppendLine("## ⚠️ Uncovered Methods");
            sb.AppendLine();
            sb.AppendLine("| Method | Class | File | Lines | Project |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var m in report.UncoveredMethods)
            {
                sb.AppendLine($"| `{m.Name}` | `{m.ClassName ?? "-"}` | `{m.FilePath}` | L{m.StartLine}-L{m.EndLine} | {m.Project ?? "-"} |");
            }
            sb.AppendLine();
        }

        if (report.CoveredClasses.Count > 0)
        {
            sb.AppendLine("## ✅ Covered Classes");
            sb.AppendLine();
            sb.AppendLine("| Class | Methods Covered | Coverage % | Covering Tests | Project |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var c in report.CoveredClasses)
            {
                sb.AppendLine($"| `{c.Name}` | {c.CoveredMethodsCount}/{c.TotalMethodsCount} | {c.MethodCoveragePercentage:F1}% | {c.TotalCoveringTests} | {c.Project ?? "-"} |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string FormatImpactMarkdown(TestImpactReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# 🎯 Test Impact Analysis (TIA)");
        sb.AppendLine();

        if (report.ChangedSymbols.Count > 0)
        {
            sb.AppendLine($"### ✏️ Modified Symbols ({report.ChangedSymbols.Count})");
            sb.AppendLine();
            foreach (var sym in report.ChangedSymbols)
            {
                sb.AppendLine($"- **[{sym.Kind}]** `{sym.Name}` (`{sym.FilePath}:{sym.StartLine}-{sym.EndLine}`)");
            }
            sb.AppendLine();
        }

        var suiteCount = report.Groups?.Count ?? 0;
        var suiteText = suiteCount > 0 ? $" across {suiteCount} test suite{(suiteCount == 1 ? "" : "s")}" : "";
        sb.AppendLine($"### 🧪 Impacted Tests ({report.AffectedTestMethods.Count} methods{suiteText})");
        sb.AppendLine();

        if (report.AffectedTestMethods.Count == 0)
        {
            sb.AppendLine("> No test cases are affected by the changed files.");
            return sb.ToString();
        }

        if (report.Groups != null && report.Groups.Count > 0)
        {
            sb.AppendLine("| Test Suite / Class | File | Affected | Total | Status |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var g in report.Groups.OrderByDescending(x => x.AllTestsAffected).ThenBy(x => x.GroupName))
            {
                var status = g.AllTestsAffected
                    ? "🟢 **All tests affected**"
                    : $"🟡 Partial ({g.AffectedTestCount}/{g.TotalTestCount})";
                var displayName = g.ClassName != null ? $"`{g.ClassName}`" : $"`{g.GroupName}`";
                sb.AppendLine($"| {displayName} | `{g.FilePath}` | {g.AffectedTestCount} | {g.TotalTestCount} | {status} |");
            }
            sb.AppendLine();

            sb.AppendLine("### 📋 Breakdown by Test Class / File");
            sb.AppendLine();

            foreach (var g in report.Groups)
            {
                var headerName = g.ClassName != null ? $"`{g.ClassName}`" : $"`{g.GroupName}`";
                if (g.AllTestsAffected)
                {
                    sb.AppendLine($"#### 🟢 {headerName} (`{g.FilePath}`)");
                    sb.AppendLine($"> **All {g.TotalTestCount} tests in this class are affected.** Running the class directly is recommended.");
                    sb.AppendLine();
                    sb.AppendLine("<details>");
                    sb.AppendLine($"<summary>View all {g.AffectedTestCount} impacted methods</summary>");
                    sb.AppendLine();
                    sb.AppendLine("| Test Method | Impact Reason | Depth | Call Chain |");
                    sb.AppendLine("|---|---|---|---|");
                    foreach (var t in g.Methods.OrderBy(x => x.Depth).ThenBy(x => x.TestMethodName))
                    {
                        var chain = string.Join(" → ", t.CallChain);
                        sb.AppendLine($"| **`{t.TestMethodName}`** | {t.ImpactReason} | {t.Depth} | `{chain}` |");
                    }
                    sb.AppendLine();
                    sb.AppendLine("</details>");
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine($"#### 🟡 {headerName} (`{g.FilePath}`) - {g.AffectedTestCount}/{g.TotalTestCount} tests affected");
                    sb.AppendLine();
                    sb.AppendLine("| Test Method | Impact Reason | Depth | Call Chain |");
                    sb.AppendLine("|---|---|---|---|");
                    foreach (var t in g.Methods.OrderBy(x => x.Depth).ThenBy(x => x.TestMethodName))
                    {
                        var chain = string.Join(" → ", t.CallChain);
                        sb.AppendLine($"| **`{t.TestMethodName}`** | {t.ImpactReason} | {t.Depth} | `{chain}` |");
                    }
                    sb.AppendLine();
                }
            }
        }
        else
        {
            var grouped = report.AffectedTestMethods.GroupBy(t => t.TestFilePath).OrderBy(g => g.Key);
            foreach (var group in grouped)
            {
                sb.AppendLine($"#### 📄 `{group.Key}`");
                sb.AppendLine();
                sb.AppendLine("| Test Method | Class | Impact Reason | Depth | Call Chain |");
                sb.AppendLine("|---|---|---|---|---|");
                foreach (var t in group.OrderBy(x => x.Depth).ThenBy(x => x.TestMethodName))
                {
                    var chain = string.Join(" → ", t.CallChain);
                    sb.AppendLine($"| **`{t.TestMethodName}`** | `{t.TestClassName ?? "-"}` | {t.ImpactReason} | {t.Depth} | `{chain}` |");
                }
                sb.AppendLine();
            }
        }

        if (report.RunnerCommands.Count > 0)
        {
            sb.AppendLine("### 🚀 Ready-to-Run Test Commands");
            sb.AppendLine();
            foreach (var (fw, cmd) in report.RunnerCommands)
            {
                sb.AppendLine($"**{fw.ToUpperInvariant()}**:");
                sb.AppendLine("```bash");
                sb.AppendLine(cmd);
                sb.AppendLine("```");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static async Task BulkInsertTempIdsAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        string tableName,
        IEnumerable<string> ids,
        CancellationToken ct)
    {
        await using var ins = conn.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = $"INSERT OR IGNORE INTO {tableName} VALUES ($id);";
        var param = ins.Parameters.Add("$id", SqliteType.Text);

        foreach (var id in ids)
        {
            param.Value = id;
            await ins.ExecuteNonQueryAsync(ct);
        }
    }

    #endregion
}
