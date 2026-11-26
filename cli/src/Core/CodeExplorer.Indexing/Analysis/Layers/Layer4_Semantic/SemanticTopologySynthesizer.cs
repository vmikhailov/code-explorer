using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

namespace CodeExplorer.Core.Parser.Layers;

internal static class SemanticTopologySynthesizer
{
    // Lines that only declare names (struct fields, field initializers/destructuring, attributes, fn signatures).
    // Shared config crates declare every resource field; declaring a field is not evidence that a workload uses it.
    private static readonly Regex DeclarationLinePattern = new(
        @"^[ \t]*(?:#\[.*|(?:pub(?:\([^)]*\))?[ \t]+)?[A-Za-z_][A-Za-z0-9_]*[ \t]*(?::[^:].*|,[ \t]*(?://.*)?\r?)|(?:pub(?:\([^)]*\))?[ \t]+)?(?:async[ \t]+)?fn[ \t]+.*)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public static void Synthesize(
        IReadOnlyList<ProjectNode> projects,
        IReadOnlyDictionary<string, List<FileNode>> filesByProjectId,
        IReadOnlyList<Relationship> projectDependencies,
        IReadOnlyList<PackageNode> packages,
        IReadOnlyDictionary<string, string> workloadByProjectId,
        IReadOnlyList<FileNode> workspaceFiles,
        SemanticStructureNode semanticStructureNode,
        List<Relationship> semanticRelationships,
        HashSet<(string From, string To, string Kind)> existingRelKeys,
        ParsingContext ctx)
    {
        var nonTestRustProjects = projects
            .Where(project => project.ProjectType == CodeExplorer.Cypher.Common.ProjectLanguage.Rust && !project.IsTest)
            .ToList();
        if (nonTestRustProjects.Count == 0)
        {
            return;
        }

        var packageById = packages
            .GroupBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var projectById = projects
            .GroupBy(project => project.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var fileByFullPath = workspaceFiles
            .Where(f => !string.IsNullOrEmpty(f.FullPath))
            .GroupBy(f => f.FullPath.NormalizeFullPath(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var sourceCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string GetTopologySummary(string fullPath)
        {
            if (!sourceCache.TryGetValue(fullPath, out var text))
            {
                var normFull = fullPath.NormalizeFullPath();
                text = fileByFullPath.TryGetValue(normFull, out var fn)
                    ? (fn.TryGetSignals()?.RustTopologySummary ?? string.Empty)
                    : string.Empty;
                sourceCache[fullPath] = text;
            }
            return text;
        }

        var directPackageNames = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var localDependencyIds = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var dependency in projectDependencies)
        {
            if (packageById.TryGetValue(dependency.To, out var package))
            {
                if (!directPackageNames.TryGetValue(dependency.From, out var names))
                {
                    names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    directPackageNames[dependency.From] = names;
                }
                names.Add(package.Name);
            }
            else if (projectById.ContainsKey(dependency.To))
            {
                if (!localDependencyIds.TryGetValue(dependency.From, out var targets))
                {
                    targets = [];
                    localDependencyIds[dependency.From] = targets;
                }
                targets.Add(dependency.To);
            }
        }

        foreach (var project in nonTestRustProjects)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            var sources = filesByProjectId.GetValueOrDefault(project.Id) ?? [];
            var ownFiles = sources
                .Where(file => file.Name.EndsWith(".rs", StringComparison.OrdinalIgnoreCase) &&
                              !WorkspaceConventions.IsTestFilePath(file.Path))
                .Select(file => file.FullPath)
                .ToList();
            var source = string.Join('\n', ownFiles.Select(GetTopologySummary));

            // Only long-running workloads inherit evidence from reachable dependency code; for CLIs the import-level
            // reachability is too coarse (importing a type pulls in its whole module) to attribute infrastructure.
            var isRuntimeWorkload = workloadByProjectId.TryGetValue(project.Id, out var workloadId) &&
                                    semanticStructureNode.Children.Any(node =>
                                        node.Id.Equals(workloadId, StringComparison.OrdinalIgnoreCase) &&
                                        node is ServiceNode or WorkerNode or AppNode);
            var reachableFiles = isRuntimeWorkload
                ? GetReachableDependencyFiles(project, ctx.AbsoluteWorkspacePath)
                : [];
            var reachableSources = string.Join('\n', reachableFiles
                .Select(file =>
                {
                    var normFull = file.NormalizeFullPath();
                    return fileByFullPath.TryGetValue(normFull, out var fn)
                        ? (fn.TryGetSignals()?.RustTopologyBodySummary ?? string.Empty)
                        : DeclarationLinePattern.Replace(GetTopologySummary(file), string.Empty);
                }));
            var effectiveSource = reachableSources.Length == 0 ? source : source + "\n" + reachableSources;

            // Packages reachable through local workspace dependencies count too: a crate that only calls
            // `common::kms_dh` still talks to KMS through the dependency's aws-sdk-kms client.
            var packageNames = CollectEffectivePackageNames(project.Id, directPackageNames, localDependencyIds);

            var langParser = WorkspaceIndexer.GetParserByLanguage(project.ProjectType.ToCanonicalString());
            if (langParser != null)
            {
                workloadByProjectId.TryGetValue(project.Id, out var projWorkloadId);
                for (int e = 0; e < langParser.SemanticExtensions.Count; e++)
                {
                    langParser.SemanticExtensions[e].SynthesizeCloudResources(
                        project,
                        effectiveSource,
                        packageNames,
                        projWorkloadId,
                        semanticStructureNode,
                        semanticRelationships,
                        existingRelKeys,
                        ctx);
                }
            }
        }
    }

    private static IReadOnlyList<string> GetReachableDependencyFiles(ProjectNode project, string workspaceRoot)
    {
        var json = project.TryGetSignals()?.SourceReachability;
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        Dictionary<string, string[]>? reachability;
        try
        {
            reachability = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string[]>>(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }

        return reachability is null
            ? []
            : reachability.Values
                .SelectMany(files => files)
                .Where(file => !WorkspaceConventions.IsTestFilePath(file))
                .Select(file => Path.GetFullPath(Path.Combine(workspaceRoot, file)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    private static HashSet<string> CollectEffectivePackageNames(
        string projectId,
        IReadOnlyDictionary<string, HashSet<string>> directPackageNames,
        IReadOnlyDictionary<string, List<string>> localDependencyIds)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(projectId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            if (directPackageNames.TryGetValue(current, out var names))
            {
                result.UnionWith(names);
            }

            foreach (var dependencyId in localDependencyIds.GetValueOrDefault(current) ?? [])
            {
                queue.Enqueue(dependencyId);
            }
        }

        return result;
    }
}
