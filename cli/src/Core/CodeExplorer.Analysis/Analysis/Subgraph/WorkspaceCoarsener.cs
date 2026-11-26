using System.Diagnostics;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Analysis.Subgraph;

internal static class WorkspaceCoarsener
{
    private const string ColId = "id";
    private const string ColName = "name";
    private const string ColPath = "path";
    private const string ColLang = "lang";
    private const string ColRole = "role";
    private const string ColIsLib = "is_lib";
    private const string ColIsTest = "is_test";
    private const string ColFrom = "src";
    private const string ColTo = "dst";
    private const string ColDep = "dep";
    private const string ColReason = "reason";
    private const string ColPkg = "pkg";
    private const string ColTopic = "topic";
    private const string ColTable = "tbl";
    private const string ColRoute = "route";
    private const string RepoUnitPrefix = "repo:";
    private const string ProjUnitPrefix = "proj:";
    private const string RoleLibrary = "Library";
    private const string UnknownLanguage = "unknown";
    private const string DefaultServiceCallLabel = "service_call";
    private const string SlashSep = "/";

    private const string QueryProjects =
        "MATCH (p:Project) RETURN p.id AS id, coalesce(p.name, '') AS name, coalesce(p.path, '') AS path, coalesce(p.language, '') AS lang, coalesce(p.role, '') AS role, coalesce(p.is_library, false) AS is_lib, coalesce(p.is_test, false) AS is_test";

    private const string QueryProjectContains =
        "MATCH (parent:Project)-[:CONTAINS]->(child:Project) RETURN parent.id AS src, child.id AS dst";

    private const string QueryGitRepoCount =
        "MATCH (g:GitSettings) RETURN count(g) AS cnt";
    private const string ColCnt = "cnt";

    private const string QueryDirectProjectDeps =
        "MATCH (a:Project)-[r:DEPENDS_ON]->(b:Project) RETURN a.id AS src, b.id AS dst, coalesce(r.source_dep, '') AS dep, coalesce(r.reason, 'package_dep') AS reason";

    private const string QueryInternalPackageDeps =
        "MATCH (consumer:Project)-[:DEPENDS_ON]->(pkg:Package)<-[:PRODUCES]-(producer:Project) WHERE consumer.id <> producer.id RETURN consumer.id AS src, producer.id AS dst, coalesce(pkg.name, '') AS pkg";

    private const string QueryDirectServiceCalls =
        "MATCH (a)-[r:SERVICE_CALL]->(b) WHERE (a:Project OR a:Workload) AND (b:Project OR b:Workload) RETURN a.id AS src, b.id AS dst, coalesce(r.matched_endpoint, r.url, '') AS route, coalesce(a.path, '') AS path, coalesce(b.path, '') AS dep";

    private const string QueryTopicPublishers =
        "MATCH (w)-[:PUBLISHES_TO]->(t:Topic) WHERE w:Project OR w:Workload OR w:Module RETURN coalesce(w.path, w.name, '') AS path, coalesce(t.name, '') AS topic";

    private const string QueryTopicSubscribers =
        "MATCH (w)-[:SUBSCRIBES_TO|TRIGGERS]-(t:Topic) WHERE (w:Project OR w:Workload OR w:Module) RETURN coalesce(w.path, w.name, '') AS path, coalesce(t.name, '') AS topic";

    internal const string QueryTableAccess =
        "MATCH (n)-[:READS_FROM|WRITES_TO|USES_DB]->(t:Table) RETURN coalesce(n.file_path, n.path, '') AS path, coalesce(t.name, '') AS tbl";

    private static readonly HashSet<string> MonorepoBucketFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "src", "services", "apps", "packages", "libs", "libraries", "modules",
        "projects", "backend", "frontend", "clients", "workers", "tools",
        "tests", "test", "core", "shared", "common", "internal", "cmd", "pkg"
    };

    private static readonly HashSet<string> IgnoredInfraTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "flyway_schema_history", "databasechangelog", "databasechangeloglock",
        "__efmigrationshistory", "schema_migrations", "alembic_version", "typeorm_migrations"
    };

    private static readonly HashSet<string> IgnoredGenericTopics = new(StringComparer.OrdinalIgnoreCase)
    {
        "default", "dlq", "dead-letter", "health", "metrics", "telemetry", "logs", "audit"
    };

    internal sealed record RawProj(
        string Id,
        string Name,
        string NormPath,
        string Language,
        string Role,
        bool IsLibrary,
        bool IsTest);

    public static async Task<CoarsenedWorkspaceGraph> BuildAsync(
        IGraphClient db,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var rawProjects = await LoadRawProjectsAsync(db, ct);
        if (rawProjects.Count == 0)
        {
            return new CoarsenedWorkspaceGraph
            {
                RawProjectCount = 0,
                Units = [],
                Edges = [],
                ElapsedMs = sw.Elapsed.TotalMilliseconds
            };
        }

        var projById = rawProjects.ToDictionary(p => p.Id, p => p, StringComparer.OrdinalIgnoreCase);
        var parentByChild = await LoadParentProjectMapAsync(db, projById, ct);
        var gitRes = await db.ExecuteCypherAsync(QueryGitRepoCount, cancellationToken: ct);
        var gitRepoCount = gitRes.Rows.Count > 0 ? gitRes.Rows[0][ColCnt].AsInt64() : 0L;
        var (units, unitsByKey, projToUnitKey, groupByTopRepoFolder) = GroupProjectsIntoUnits(rawProjects, parentByChild, projById, gitRepoCount);

        var sortedProjectsByPath = rawProjects
            .Where(p => !string.IsNullOrEmpty(p.NormPath))
            .OrderByDescending(p => p.NormPath.Length)
            .ToList();

        string? ResolveUnitByPathOrName(string? pathOrName) =>
            ResolveUnitCore(pathOrName, sortedProjectsByPath, rawProjects, projToUnitKey, unitsByKey, groupByTopRepoFolder);

        var edgeWeights = new Dictionary<(string From, string To), double>();
        var edgeReasons = new Dictionary<(string From, string To), HashSet<string>>();

        void AccumulateEdge(string? fromUnit, string? toUnit, double weight, string reason)
        {
            if (string.IsNullOrEmpty(fromUnit) || string.IsNullOrEmpty(toUnit) ||
                string.Equals(fromUnit, toUnit, StringComparison.OrdinalIgnoreCase) ||
                weight <= 0)
            {
                return;
            }
            if (unitsByKey.TryGetValue(fromUnit, out var fu) && fu.IsTestOnly) return;

            var key = (fromUnit, toUnit);
            edgeWeights[key] = edgeWeights.GetValueOrDefault(key) + weight;
            if (!edgeReasons.TryGetValue(key, out var rSet))
            {
                rSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                edgeReasons[key] = rSet;
            }
            if (rSet.Count < 6 && !string.IsNullOrWhiteSpace(reason))
            {
                rSet.Add(reason);
            }
        }

        await AccumulateProjectAndServiceEdgesAsync(db, projById, projToUnitKey, ResolveUnitByPathOrName, AccumulateEdge, ct);
        await AccumulateTopicSignalsAsync(db, ResolveUnitByPathOrName, AccumulateEdge, ct);
        await AccumulateTableSignalsAsync(db, ResolveUnitByPathOrName, AccumulateEdge, ct);

        var edges = edgeWeights
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key.From, StringComparer.Ordinal)
            .ThenBy(kv => kv.Key.To, StringComparer.Ordinal)
            .Select(kv => new SubgraphWeightedEdge
            {
                FromUnitId = kv.Key.From,
                ToUnitId = kv.Key.To,
                Weight = Math.Round(kv.Value, 4),
                Reasons = edgeReasons.TryGetValue(kv.Key, out var rs) ? rs.OrderBy(x => x, StringComparer.Ordinal).ToList() : []
            })
            .ToList();

        sw.Stop();
        return new CoarsenedWorkspaceGraph
        {
            RawProjectCount = rawProjects.Count,
            Units = units,
            Edges = edges,
            ElapsedMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2)
        };
    }

    private static async Task<List<RawProj>> LoadRawProjectsAsync(IGraphClient db, CancellationToken ct)
    {
        var projRes = await db.ExecuteCypherAsync(QueryProjects, cancellationToken: ct);
        var rawProjects = new List<RawProj>(projRes.Rows.Count);
        foreach (var row in projRes.Rows)
        {
            var id = row[ColId].AsString();
            if (string.IsNullOrEmpty(id)) continue;
            var path = row[ColPath].AsString().Replace('\\', '/').Trim('/');
            rawProjects.Add(new RawProj(
                id,
                row[ColName].AsString(),
                path,
                row[ColLang].AsString(),
                row[ColRole].AsString(),
                row[ColIsLib].AsBool(),
                row[ColIsTest].AsBool()));
        }
        return rawProjects;
    }

    private static async Task<Dictionary<string, string>> LoadParentProjectMapAsync(
        IGraphClient db,
        Dictionary<string, RawProj> projById,
        CancellationToken ct)
    {
        var containsRes = await db.ExecuteCypherAsync(QueryProjectContains, cancellationToken: ct);
        var parentByChild = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in containsRes.Rows)
        {
            var parentId = row[ColFrom].AsString();
            var childId = row[ColTo].AsString();
            if (projById.TryGetValue(parentId, out var parentProj) &&
                !string.IsNullOrEmpty(parentProj.NormPath) &&
                projById.ContainsKey(childId))
            {
                parentByChild[childId] = parentId;
            }
        }
        return parentByChild;
    }

    private static (List<SubgraphUnit> Units, Dictionary<string, SubgraphUnit> UnitsByKey, Dictionary<string, string> ProjToUnitKey, bool GroupByRepo)
        GroupProjectsIntoUnits(
            List<RawProj> rawProjects,
            Dictionary<string, string> parentByChild,
            Dictionary<string, RawProj> projById,
            long gitRepoCount)
    {
        var distinctTopRepoFolders = rawProjects
            .Select(p => ExtractFirstSegment(p.NormPath))
            .Where(s => !string.IsNullOrEmpty(s) && !MonorepoBucketFolders.Contains(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var groupByTopRepoFolder = gitRepoCount switch
        {
            1 => false,
            >= 2 => true,
            _ => distinctTopRepoFolders >= 2
        };

        var projToUnitKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var unitMembers = new Dictionary<string, List<RawProj>>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in rawProjects)
        {
            var rootProj = ResolveTopmostProjectAncestor(p, parentByChild, projById);
            var topSeg = ExtractFirstSegment(rootProj.NormPath);
            var unitKey = groupByTopRepoFolder && !string.IsNullOrEmpty(topSeg) && !MonorepoBucketFolders.Contains(topSeg)
                ? RepoUnitPrefix + topSeg.ToLowerInvariant()
                : ProjUnitPrefix + rootProj.Id;

            projToUnitKey[p.Id] = unitKey;
            if (!unitMembers.TryGetValue(unitKey, out var list))
            {
                list = [];
                unitMembers[unitKey] = list;
            }
            list.Add(p);
        }

        var units = new List<SubgraphUnit>(unitMembers.Count);
        var unitsByKey = new Dictionary<string, SubgraphUnit>(StringComparer.OrdinalIgnoreCase);
        foreach (var (uKey, members) in unitMembers.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            var unit = BuildUnitFromMembers(uKey, members);
            units.Add(unit);
            unitsByKey[uKey] = unit;
        }

        return (units, unitsByKey, projToUnitKey, groupByTopRepoFolder);
    }

    private static string? ResolveUnitCore(
        string? pathOrName,
        List<RawProj> sortedProjectsByPath,
        List<RawProj> rawProjects,
        Dictionary<string, string> projToUnitKey,
        Dictionary<string, SubgraphUnit> unitsByKey,
        bool groupByTopRepoFolder)
    {
        if (string.IsNullOrWhiteSpace(pathOrName)) return null;
        var norm = pathOrName.Replace('\\', '/').Trim('/');
        foreach (var p in sortedProjectsByPath)
        {
            if (norm.Equals(p.NormPath, StringComparison.OrdinalIgnoreCase) ||
                norm.StartsWith(p.NormPath + SlashSep, StringComparison.OrdinalIgnoreCase))
            {
                return projToUnitKey.GetValueOrDefault(p.Id);
            }
        }
        if (groupByTopRepoFolder)
        {
            var repoKey = RepoUnitPrefix + ExtractFirstSegment(norm).ToLowerInvariant();
            if (unitsByKey.ContainsKey(repoKey)) return repoKey;
        }
        var byName = rawProjects.FirstOrDefault(p => string.Equals(p.Name, norm, StringComparison.OrdinalIgnoreCase));
        return byName != null ? projToUnitKey.GetValueOrDefault(byName.Id) : null;
    }

    private static async Task AccumulateProjectAndServiceEdgesAsync(
        IGraphClient db,
        Dictionary<string, RawProj> projById,
        Dictionary<string, string> projToUnitKey,
        Func<string?, string?> resolveUnitByPath,
        Action<string?, string?, double, string> accumulate,
        CancellationToken ct)
    {
        var depRes = await db.ExecuteCypherAsync(QueryDirectProjectDeps, cancellationToken: ct);
        foreach (var row in depRes.Rows)
        {
            var srcId = row[ColFrom].AsString();
            var dstId = row[ColTo].AsString();
            if (!projById.TryGetValue(srcId, out var srcProj) || !projById.TryGetValue(dstId, out var dstProj)) continue;
            if (srcProj.IsTest) continue;
            var depLabel = row[ColDep].AsString();
            var rsn = row[ColReason].AsString();
            var reasonText = !string.IsNullOrEmpty(depLabel) ? $"{rsn}:{depLabel}" : $"{rsn}:{dstProj.Name}";
            accumulate(projToUnitKey.GetValueOrDefault(srcId), projToUnitKey.GetValueOrDefault(dstId), 3.0, reasonText);
        }

        var pkgRes = await db.ExecuteCypherAsync(QueryInternalPackageDeps, cancellationToken: ct);
        var pkgConsumers = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var pkgTriples = new List<(string ConsumerUnit, string ProducerUnit, string PkgName)>();
        foreach (var row in pkgRes.Rows)
        {
            var cu = projToUnitKey.GetValueOrDefault(row[ColFrom].AsString());
            var pu = projToUnitKey.GetValueOrDefault(row[ColTo].AsString());
            var pkgName = row[ColPkg].AsString();
            if (string.IsNullOrEmpty(cu) || string.IsNullOrEmpty(pu) || string.Equals(cu, pu, StringComparison.OrdinalIgnoreCase)) continue;
            if (!pkgConsumers.TryGetValue(pkgName, out var cSet))
            {
                cSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                pkgConsumers[pkgName] = cSet;
            }
            cSet.Add(cu);
            pkgTriples.Add((cu, pu, pkgName));
        }
        foreach (var (cu, pu, pkgName) in pkgTriples)
        {
            var deg = pkgConsumers.TryGetValue(pkgName, out var s) ? s.Count : 1;
            accumulate(cu, pu, 3.0 / Math.Log2(2.0 + deg), $"internal_pkg:{pkgName}");
        }

        var svcRes = await db.ExecuteCypherAsync(QueryDirectServiceCalls, cancellationToken: ct);
        foreach (var row in svcRes.Rows)
        {
            var uFrom = projToUnitKey.GetValueOrDefault(row[ColFrom].AsString()) ?? resolveUnitByPath(row[ColPath].AsString());
            var uTo = projToUnitKey.GetValueOrDefault(row[ColTo].AsString()) ?? resolveUnitByPath(row[ColDep].AsString());
            var route = row[ColRoute].AsString();
            var label = !string.IsNullOrEmpty(route) ? $"service_call:{route}" : DefaultServiceCallLabel;
            accumulate(uFrom, uTo, 2.5, label);
        }
    }

    private static async Task AccumulateTopicSignalsAsync(
        IGraphClient db,
        Func<string?, string?> resolveUnitByPath,
        Action<string?, string?, double, string> accumulate,
        CancellationToken ct)
    {
        var pubs = await db.ExecuteCypherAsync(QueryTopicPublishers, cancellationToken: ct);
        var subs = await db.ExecuteCypherAsync(QueryTopicSubscribers, cancellationToken: ct);

        var pubUnitsByTopic = GroupUnitsByResource(pubs, ColTopic, IgnoredGenericTopics, resolveUnitByPath);
        var subUnitsByTopic = GroupUnitsByResource(subs, ColTopic, IgnoredGenericTopics, resolveUnitByPath);

        foreach (var (topic, pubSet) in pubUnitsByTopic)
        {
            if (!subUnitsByTopic.TryGetValue(topic, out var subSet)) continue;
            var totalParticipants = pubSet.Union(subSet, StringComparer.OrdinalIgnoreCase).Count();
            if (totalParticipants < 2) continue;
            var w = 2.5 / Math.Log2(2.0 + totalParticipants);
            foreach (var pu in pubSet)
            {
                foreach (var su in subSet)
                {
                    if (!string.Equals(pu, su, StringComparison.OrdinalIgnoreCase))
                    {
                        accumulate(su, pu, w, $"shared_topic:{topic}");
                    }
                }
            }
        }
    }

    private static async Task AccumulateTableSignalsAsync(
        IGraphClient db,
        Func<string?, string?> resolveUnitByPath,
        Action<string?, string?, double, string> accumulate,
        CancellationToken ct)
    {
        var tblRes = await db.ExecuteCypherAsync(QueryTableAccess, cancellationToken: ct);
        var unitsByTable = GroupUnitsByResource(tblRes, ColTable, IgnoredInfraTables, resolveUnitByPath);

        foreach (var (tbl, uSet) in unitsByTable)
        {
            if (uSet.Count is < 2 or > 12) continue;
            var w = 2.0 / Math.Log2(2.0 + uSet.Count);
            var list = uSet.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                for (var j = i + 1; j < list.Count; j++)
                {
                    accumulate(list[i], list[j], w, $"shared_table:{tbl}");
                }
            }
        }
    }

    private static Dictionary<string, HashSet<string>> GroupUnitsByResource(
        CypherQueryResult res,
        string resourceCol,
        HashSet<string> ignored,
        Func<string?, string?> resolveUnitByPath)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in res.Rows)
        {
            var name = row[resourceCol].AsString().Trim();
            if (string.IsNullOrEmpty(name) || ignored.Contains(name)) continue;
            var u = resolveUnitByPath(row[ColPath].AsString());
            if (string.IsNullOrEmpty(u)) continue;
            if (!map.TryGetValue(name, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                map[name] = set;
            }
            set.Add(u);
        }
        return map;
    }

    private static RawProj ResolveTopmostProjectAncestor(
        RawProj start,
        Dictionary<string, string> parentByChild,
        Dictionary<string, RawProj> projById)
    {
        var curr = start;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { curr.Id };
        while (parentByChild.TryGetValue(curr.Id, out var parentId) &&
               visited.Add(parentId) &&
               projById.TryGetValue(parentId, out var parentProj) &&
               !string.IsNullOrEmpty(parentProj.NormPath))
        {
            curr = parentProj;
        }
        return curr;
    }

    private static SubgraphUnit BuildUnitFromMembers(string unitKey, List<RawProj> members)
    {
        var nonTest = members.Where(m => !m.IsTest).ToList();
        var effective = nonTest.Count > 0 ? nonTest : members;

        var primaryLang = effective
            .Where(m => !string.IsNullOrWhiteSpace(m.Language))
            .GroupBy(m => m.Language.ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? UnknownLanguage;

        var langs = effective
            .Select(m => m.Language.ToLowerInvariant())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
            .ToList();

        string name;
        string rootPath;
        if (unitKey.StartsWith(RepoUnitPrefix, StringComparison.Ordinal))
        {
            var repoFolder = ExtractFirstSegment(members[0].NormPath);
            name = repoFolder;
            rootPath = repoFolder;
        }
        else
        {
            var shortest = members.OrderBy(m => m.NormPath.Length).First();
            name = shortest.Name;
            rootPath = shortest.NormPath;
        }

        return new SubgraphUnit
        {
            Id = unitKey,
            Name = name,
            RootPath = rootPath,
            PrimaryLanguage = primaryLang,
            Languages = langs,
            MemberProjectCount = members.Count,
            MemberProjects = members.Select(m => m.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            IsLibraryOnly = nonTest.Count > 0 && nonTest.All(m => m.IsLibrary || string.Equals(m.Role, RoleLibrary, StringComparison.OrdinalIgnoreCase)),
            IsTestOnly = nonTest.Count == 0
        };
    }

    internal static string ExtractFirstSegment(string normPath)
    {
        if (string.IsNullOrEmpty(normPath)) return string.Empty;
        var slash = normPath.IndexOf('/');
        return slash > 0 ? normPath[..slash] : normPath;
    }
}
