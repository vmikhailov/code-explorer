namespace CodeExplorer.Core.Mcp;

internal static partial class McpContextPackBuilder
{
    private const int MaxCandidateServicesForModules = 8;
    private const int MaxModuleCycleDepth = 8;
    private const string ColSvcName = "svcName";
    private const string ColModName = "modName";
    private const string ColFromMod = "fromMod";
    private const string ColToMod = "toMod";

    private const string FindOwningModulesPrefix =
        "MATCH (mod:Type)-[:USES_TYPE]->(svc:Type) WHERE mod.name ENDS WITH 'Module' AND svc.name IN [";

    private const string FindOwningModulesSuffix =
        "] OPTIONAL MATCH (mod)-[:DECLARED_IN]->(f:File) " +
        "RETURN svc.name AS svcName, mod.name AS modName, coalesce(f.path, mod.file_path, mod.path, '') AS filePath LIMIT 30";

    private const string FetchModuleDependencyGraphQuery =
        "MATCH (a:Type)-[:DEPENDS_ON]->(b:Type) WHERE a.name ENDS WITH 'Module' AND b.name ENDS WITH 'Module' " +
        "RETURN a.name AS fromMod, b.name AS toMod LIMIT 4000";

    private static async Task<object?> BuildModuleWiringAndCycleSafetyAsync(
        IGraphClient client,
        HashSet<string> candidateServiceNames,
        HashSet<string> subgraphFiles,
        string? workspaceRoot,
        CancellationToken ct)
    {
        if (candidateServiceNames.Count == 0)
        {
            return null;
        }

        var nameLiteral = BuildIdList(candidateServiceNames.Take(MaxCandidateServicesForModules));
        var owningRes = await client.ExecuteCypherAsync(FindOwningModulesPrefix + nameLiteral + FindOwningModulesSuffix, null, ct);
        if (owningRes.Rows.Count == 0)
        {
            return null;
        }

        var modulesByName = new Dictionary<string, (string FilePath, HashSet<string> Services)>(StringComparer.Ordinal);
        foreach (var r in owningRes.Rows)
        {
            var modName = r.GetStringProp(ColModName);
            var svcName = r.GetStringProp(ColSvcName);
            if (string.IsNullOrEmpty(modName) || string.IsNullOrEmpty(svcName)) continue;

            var cleanPath = McpResultFormatter.CleanRelativePath(r.GetStringProp(ColFilePath), workspaceRoot);
            if (IsNoiseOrTestPath(cleanPath, modName)) continue;

            if (!modulesByName.TryGetValue(modName, out var entry))
            {
                entry = (cleanPath, new HashSet<string>(StringComparer.Ordinal));
                modulesByName[modName] = entry;
            }
            entry.Services.Add(svcName);
            if (!string.IsNullOrEmpty(cleanPath))
            {
                subgraphFiles.Add(cleanPath);
            }
        }

        if (modulesByName.Count == 0)
        {
            return null;
        }

        var adj = await LoadModuleDependencyGraphAsync(client, ct);
        var moduleEntries = modulesByName
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new
            {
                module = kv.Key,
                filePath = kv.Value.FilePath,
                provides = kv.Value.Services.OrderBy(s => s, StringComparer.Ordinal).ToList(),
                directModuleImports = adj.TryGetValue(kv.Key, out var deps)
                    ? deps.OrderBy(d => d, StringComparer.Ordinal).ToList()
                    : new List<string>()
            })
            .ToList();

        var cycleChecks = BuildPairwiseModuleSafetyVerdicts(modulesByName.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(), adj);
        return new
        {
            owningModules = moduleEntries,
            importSafetyVerdicts = cycleChecks
        };
    }

    private static async Task<Dictionary<string, HashSet<string>>> LoadModuleDependencyGraphAsync(
        IGraphClient client,
        CancellationToken ct)
    {
        var adj = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var depRes = await client.ExecuteCypherAsync(FetchModuleDependencyGraphQuery, null, ct);
        foreach (var r in depRes.Rows)
        {
            var fromMod = r.GetStringProp(ColFromMod);
            var toMod = r.GetStringProp(ColToMod);
            if (string.IsNullOrEmpty(fromMod) || string.IsNullOrEmpty(toMod) || string.Equals(fromMod, toMod, StringComparison.Ordinal))
            {
                continue;
            }
            if (!adj.TryGetValue(fromMod, out var targets))
            {
                targets = new HashSet<string>(StringComparer.Ordinal);
                adj[fromMod] = targets;
            }
            targets.Add(toMod);
        }
        return adj;
    }

    private static List<string> BuildPairwiseModuleSafetyVerdicts(
        List<string> moduleNames,
        Dictionary<string, HashSet<string>> adj)
    {
        var verdicts = new List<string>();
        if (moduleNames.Count < 2)
        {
            return verdicts;
        }

        for (var i = 0; i < moduleNames.Count; i++)
        {
            for (var j = 0; j < moduleNames.Count; j++)
            {
                if (i == j) continue;
                var consumerMod = moduleNames[i];
                var targetMod = moduleNames[j];

                if (adj.TryGetValue(consumerMod, out var directDeps) && directDeps.Contains(targetMod))
                {
                    verdicts.Add($"ALREADY_IMPORTED: {consumerMod} already imports {targetMod}.");
                    continue;
                }

                var reversePath = FindShortestModulePath(adj, targetMod, consumerMod);
                if (reversePath != null)
                {
                    verdicts.Add($"CYCLE_WARNING: Importing {targetMod} into {consumerMod} creates a circular dependency ({string.Join(" -> ", reversePath)} -> {targetMod}). Extract or import a cycle-free sub-module instead.");
                }
                else
                {
                    verdicts.Add($"SAFE_DIRECT_IMPORT: {consumerMod} can import {targetMod} directly (0 reverse paths in module graph; no need to inspect other *.module.ts files).");
                }
            }
        }

        return verdicts;
    }

    private static List<string>? FindShortestModulePath(
        Dictionary<string, HashSet<string>> adj,
        string startMod,
        string goalMod)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal) { startMod };
        var parentByNode = new Dictionary<string, string>(StringComparer.Ordinal);
        var depthByNode = new Dictionary<string, int>(StringComparer.Ordinal) { [startMod] = 0 };
        var queue = new Queue<string>();
        queue.Enqueue(startMod);

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            var depth = depthByNode[curr];
            if (depth >= MaxModuleCycleDepth) continue;
            if (!adj.TryGetValue(curr, out var neighbors)) continue;

            foreach (var next in neighbors)
            {
                if (!visited.Add(next)) continue;
                parentByNode[next] = curr;
                if (string.Equals(next, goalMod, StringComparison.Ordinal))
                {
                    return ReconstructPath(parentByNode, startMod, goalMod);
                }
                depthByNode[next] = depth + 1;
                queue.Enqueue(next);
            }
        }

        return null;
    }

    private static List<string> ReconstructPath(
        Dictionary<string, string> parentByNode,
        string startMod,
        string goalMod)
    {
        var path = new List<string>();
        var curr = goalMod;
        while (true)
        {
            path.Add(curr);
            if (string.Equals(curr, startMod, StringComparison.Ordinal) || !parentByNode.TryGetValue(curr, out var prev))
            {
                break;
            }
            curr = prev;
        }
        path.Reverse();
        return path;
    }
}
