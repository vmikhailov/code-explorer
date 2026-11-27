using System.Text.Json;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Relationships;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer2ProjectParser
{
    private static void ApplyExternalPackageMetadata(PackageNode pkgNode, ProducedPackageInfo extPack)
    {
        pkgNode.DependencyScope = string.IsNullOrEmpty(extPack.Scope) ? "runtime" : extPack.Scope;
        var ver = extPack.Version ?? string.Empty;
        if (!string.IsNullOrEmpty(ver))
        {
            pkgNode.RawVersion = ver;
        }
        if (ver.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            ver.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            ver.StartsWith("git+", StringComparison.OrdinalIgnoreCase) ||
            ver.StartsWith("github:", StringComparison.OrdinalIgnoreCase) ||
            ver.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            pkgNode.IsExternalTarball = true;
        }
    }

    private static void PopulatePackageLifecycleAndEntryPoints(
        Layer1Result l1Result,
        List<ProjectNode> projects,
        List<PackageNode> packages,
        List<Relationship> dependencies,
        Layer2Context ctx)
    {
        var workspaceFiles = l1Result.Files;
        var fileByRelPath = new Dictionary<string, FileNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in workspaceFiles)
        {
            if (!string.IsNullOrEmpty(f.Path))
            {
                fileByRelPath[f.Path.Replace('\\', '/').Trim('/')] = f;
            }
        }

        foreach (var project in projects)
        {
            var meta = project.TryGetSignals()?.ManifestMetadata;
            if (meta == null) continue;

            foreach (var pkgNode in project.Children.OfType<PackageNode>().Where(p => p.IsInternal))
            {
                if (!string.IsNullOrWhiteSpace(meta.Publisher)) pkgNode.Publisher = meta.Publisher;
                if (meta.HasVsCodeEngine) pkgNode.HasVsCodeEngine = true;
                if (!string.IsNullOrWhiteSpace(meta.ActivationEvents)) pkgNode.ActivationEvents = meta.ActivationEvents;
            }

            var normProj = project.Path.Replace('\\', '/').Trim('/', '.');

            if (meta.LifecycleHooks is { Count: > 0 })
            {
                foreach (var hook in meta.LifecycleHooks)
                {
                    var manifestFile = string.IsNullOrEmpty(hook.ManifestFileName) ? "package.json" : hook.ManifestFileName;
                    var relManifestPath = string.IsNullOrEmpty(normProj)
                        ? manifestFile
                        : $"{normProj}/{manifestFile}";

                    var entryId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.EntryPoint}:{hook.EntryType}:{project.Path}:{hook.Name}";
                    var entryNode = new EntryPointNode(
                        Id: entryId,
                        Name: hook.Name,
                        Path: relManifestPath,
                        EntryType: hook.EntryType)
                    {
                        Command = hook.Command,
                        HookKind = hook.HookKind
                    };

                    project.Children.Add(entryNode);
                    dependencies.Add(new Relationship(project.Id, entryId, OntologyConstants.Relationships.Contains));
                    dependencies.Add(new Relationship(entryId, project.Id, OntologyConstants.Relationships.BelongsTo));

                    foreach (var candidateFile in ResolveReferencedScriptFiles(hook.Command, project.Path, fileByRelPath))
                    {
                        dependencies.Add(new Relationship(entryId, candidateFile.Id, OntologyConstants.Relationships.Triggers));
                    }
                }
            }

            if (meta.ExportTargets is { Count: > 0 })
            {
                var seenResolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var targetRel in meta.ExportTargets)
                {
                    var resolvedFile = TryMatchProjectFile(project.Path, targetRel, fileByRelPath);
                    if (resolvedFile != null && seenResolved.Add(resolvedFile.Path))
                    {
                        var expId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.EntryPoint}:package-export:{resolvedFile.Path}";
                        var expNode = new EntryPointNode(expId, Path.GetFileName(resolvedFile.Path), resolvedFile.Path, "package-export");
                        project.Children.Add(expNode);
                        dependencies.Add(new Relationship(project.Id, expId, OntologyConstants.Relationships.Contains));
                        dependencies.Add(new Relationship(expId, resolvedFile.Id, OntologyConstants.Relationships.DeclaredIn));
                    }
                }
            }
        }

        PopulateWorkspaceManifestsAndDrivers(l1Result, projects, packages, dependencies, ctx);
    }

    private static IEnumerable<FileNode> ResolveReferencedScriptFiles(
        string command,
        string projectPath,
        Dictionary<string, FileNode> fileByRelPath)
    {
        if (string.IsNullOrWhiteSpace(command)) yield break;
        var tokens = command.Split([' ', '\t', '"', '\'', ';', '&', '|'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (token.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith(".cjs", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith(".mjs", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith(".sh", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            {
                var matched = TryMatchProjectFile(projectPath, token, fileByRelPath);
                if (matched != null)
                {
                    yield return matched;
                }
            }
        }
    }

    private static FileNode? TryMatchProjectFile(
        string projectPath,
        string relativeCandidate,
        Dictionary<string, FileNode> fileByRelPath)
    {
        var clean = relativeCandidate.Replace('\\', '/').Trim();
        while (clean.StartsWith("./", StringComparison.Ordinal)) clean = clean[2..];
        clean = clean.Trim('/');
        if (string.IsNullOrEmpty(clean)) return null;

        var normProjPath = projectPath.Replace('\\', '/').Trim('/', '.');
        var fullRel = string.IsNullOrEmpty(normProjPath)
            ? clean
            : $"{normProjPath}/{clean}";

        if (fileByRelPath.TryGetValue(fullRel, out var direct)) return direct;
        foreach (var ext in new[] { ".js", ".ts", ".mjs", ".cjs", "/index.js", "/index.ts" })
        {
            if (fileByRelPath.TryGetValue(fullRel + ext, out var withExt)) return withExt;
        }
        return null;
    }

    private static void PopulateWorkspaceManifestsAndDrivers(
        Layer1Result l1Result,
        List<ProjectNode> projects,
        List<PackageNode> packages,
        List<Relationship> dependencies,
        Layer2Context ctx)
    {
        var ws = l1Result.Workspace;
        var wsSignals = ws.Signals();

        // 1. Root manifest metadata on WorkspaceNode (from single-pass ParsedProjectManifestMetadata)
        foreach (var meta in wsSignals.WorkspaceManifestMetadata)
        {
            if (string.IsNullOrWhiteSpace(ws.PackageName) && !string.IsNullOrWhiteSpace(meta.PackageName))
                ws.PackageName = meta.PackageName;
            if (string.IsNullOrWhiteSpace(ws.PackageVersion) && !string.IsNullOrWhiteSpace(meta.PackageVersion))
                ws.PackageVersion = meta.PackageVersion;
            if (string.IsNullOrWhiteSpace(ws.Publisher) && !string.IsNullOrWhiteSpace(meta.Publisher))
                ws.Publisher = meta.Publisher;
            if (meta.HasVsCodeEngine)
                ws.HasVsCodeEngine = true;
            if (meta.HasContributes)
                ws.HasContributes = true;
            if (string.IsNullOrWhiteSpace(ws.ActivationEvents) && !string.IsNullOrWhiteSpace(meta.ActivationEvents))
                ws.ActivationEvents = meta.ActivationEvents;

            if (meta.TsConfigPaths != null)
            {
                foreach (var (k, v) in meta.TsConfigPaths)
                {
                    ctx.TsConfigPaths.TryAdd(k, v);
                }
            }
        }

        // 2. .vscode/tasks.json folderOpen autorun tasks (parsed once in Layer 1)
        if (ctx.DiscoveredVsCodeFolderOpenTasks.Count > 0)
        {
            var rootProj = projects.FirstOrDefault(p => string.IsNullOrEmpty(p.Path)) ?? projects.FirstOrDefault();
            var folderOpenTasks = new List<Dictionary<string, string>>(ctx.DiscoveredVsCodeFolderOpenTasks.Count);
            foreach (var (label, cmd) in ctx.DiscoveredVsCodeFolderOpenTasks)
            {
                folderOpenTasks.Add(new Dictionary<string, string>
                {
                    [OntologyConstants.ManifestFields.Label] = label,
                    [OntologyConstants.ManifestFields.Command] = cmd
                });

                if (rootProj != null)
                {
                    var entryId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.EntryPoint}:vscode-task-folder-open:{label}";
                    var entryNode = new EntryPointNode(
                        Id: entryId,
                        Name: $"vscode.tasks.folderOpen:{label}",
                        Path: OntologyConstants.ManifestFields.TasksJsonRelPath,
                        EntryType: OntologyConstants.ManifestFields.FolderOpenTaskType)
                    {
                        Command = cmd,
                        HookKind = "install",
                        TaskLabel = label
                    };
                    rootProj.Children.Add(entryNode);
                    dependencies.Add(new Relationship(rootProj.Id, entryId, OntologyConstants.Relationships.Contains));
                    dependencies.Add(new Relationship(entryId, rootProj.Id, OntologyConstants.Relationships.BelongsTo));
                }
            }

            wsSignals.VsCodeFolderOpenTasksJson = JsonSerializer.Serialize(folderOpenTasks);
        }

        // 3. .nuspec manifest metadata & dependencies (already extracted once by CSharpProjectParser)
        var allManifestMetadata = wsSignals.WorkspaceManifestMetadata
            .Concat(projects.Select(p => p.TryGetSignals()?.ManifestMetadata).OfType<ParsedProjectManifestMetadata>())
            .Distinct();
        var ownerProj = projects.FirstOrDefault(p => string.IsNullOrEmpty(p.Path)) ?? projects.FirstOrDefault();
        foreach (var meta in allManifestMetadata)
        {
            if (!string.IsNullOrEmpty(meta.NuspecId)) wsSignals.NuspecId = meta.NuspecId;
            if (!string.IsNullOrEmpty(meta.NuspecVersion)) wsSignals.NuspecVersion = meta.NuspecVersion;

            if (meta.NuspecDependencies is not { Count: > 0 }) continue;
            foreach (var nuspecDep in meta.NuspecDependencies)
            {
                if (string.IsNullOrWhiteSpace(nuspecDep.Name)) continue;
                var depVer = string.IsNullOrWhiteSpace(nuspecDep.Version) ? "*" : nuspecDep.Version;
                var pkgNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Package}:nuget:{nuspecDep.Name.ToLowerInvariant()}";
                if (!packages.Any(p => p.Id.Equals(pkgNodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    var pkgNode = new PackageNode(
                        Id: pkgNodeId,
                        Name: nuspecDep.Name,
                        Version: depVer,
                        IsInternal: false,
                        Ecosystem: OntologyConstants.ManifestFields.NuGetEcosystem,
                        Path: string.Empty,
                        Archetype: OntologyConstants.Archetypes.PureUtility)
                    {
                        DependencyScope = "runtime",
                        RawVersion = depVer
                    };
                    packages.Add(pkgNode);
                    if (ownerProj != null)
                    {
                        ownerProj.Children.Add(pkgNode);
                        var depRel = Relationship.FromRelationship(new DependsOnRelationship(
                            ownerProj.Id,
                            pkgNodeId,
                            new() { ["dependency_type"] = "package", ["category"] = "library" }));
                        dependencies.Add(depRel);
                    }
                }
            }
        }

        // 4. Pre-compute project database drivers from Layer 2 descriptors, packages, & SharedFileEnricher enrichments
        PopulateProjectDatabaseDrivers(projects, dependencies, ctx);
    }

    private static void PopulateProjectDatabaseDrivers(
        List<ProjectNode> projects,
        List<Relationship> dependencies,
        Layer2Context ctx)
    {
        var projectById = projects
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var dependsOnTargetsBySource = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < dependencies.Count; i++)
        {
            var dep = dependencies[i];
            if (dep.Kind == RelationshipKind.DependsOn)
            {
                if (!dependsOnTargetsBySource.TryGetValue(dep.From, out var list))
                {
                    list = [];
                    dependsOnTargetsBySource[dep.From] = list;
                }
                list.Add(dep.To);
            }
        }

        var memoizedByProjectId = new Dictionary<string, (string? Engine, string? DbType)>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in projects)
        {
            var projSignals = project.Signals();
            projSignals.DbDriverChecked = true;
            var (engine, dbType) = DetectDriverForProjectInLayer2(
                project,
                projectById,
                dependsOnTargetsBySource,
                memoizedByProjectId,
                ctx,
                []);
            if (!string.IsNullOrEmpty(engine) && !string.IsNullOrEmpty(dbType))
            {
                projSignals.DbDriverEngine = engine;
                projSignals.DbDriverFamily = dbType;
            }
        }
    }

    private static (string? Engine, string? DbType) DetectDriverForProjectInLayer2(
        ProjectNode projectNode,
        Dictionary<string, ProjectNode> projectById,
        Dictionary<string, List<string>> dependsOnTargetsBySource,
        Dictionary<string, (string? Engine, string? DbType)> memoizedByProjectId,
        Layer2Context ctx,
        HashSet<string> visited)
    {
        if (memoizedByProjectId.TryGetValue(projectNode.Id, out var cachedDriver))
        {
            return cachedDriver;
        }
        if (!visited.Add(projectNode.Id)) return (null, null);

        foreach (var pkg in projectNode.Children.OfType<PackageNode>())
        {
            var pLower = pkg.Name.ToLowerInvariant();
            if (pLower.Contains("npgsql") || pLower is "pg" or "pg-promise" or "@types/pg" or "postgres" or "tokio-postgres" || pLower.Contains("psycopg") || pLower.Contains("asyncpg") || pLower.Contains("pgx") || pLower.Contains("lib/pq") || pLower.Contains("pgdriver") || pLower.Contains("postgres"))
                return memoizedByProjectId[projectNode.Id] = ("PostgreSQL", "relational");
            if (pLower.Contains("sqlclient") || pLower.Contains("entityframeworkcore.sqlserver") || pLower is "mssql" or "tedious" || pLower.Equals("entityframework") || pLower.Equals("system.data.entity"))
                return memoizedByProjectId[projectNode.Id] = ("SQL Server", "relational");
            if (pLower.Contains("mysql") || pLower.Contains("pomelo") || pLower.Contains("pymysql"))
                return memoizedByProjectId[projectNode.Id] = ("MySQL", "relational");
            if (pLower.Contains("sqlite"))
                return memoizedByProjectId[projectNode.Id] = ("SQLite", "relational");
            if (pLower.Contains("oracle"))
                return memoizedByProjectId[projectNode.Id] = ("Oracle", "relational");
            if (pLower.Contains("mongodb"))
                return memoizedByProjectId[projectNode.Id] = ("MongoDB", "document");
            if (pLower is "redis" or "ioredis" || pLower.Contains("redis") || pLower.Contains("stackexchange"))
                return memoizedByProjectId[projectNode.Id] = ("Redis", "cache");
        }

        var ownMeta = projectNode.TryGetSignals()?.ManifestMetadata;
        if (!string.IsNullOrEmpty(ownMeta?.DatabaseDriverEngine) && !string.IsNullOrEmpty(ownMeta.DatabaseDriverFamily))
        {
            return memoizedByProjectId[projectNode.Id] = (ownMeta.DatabaseDriverEngine, ownMeta.DatabaseDriverFamily);
        }

        // Transitive local project dependencies already resolved in Layer 2
        if (dependsOnTargetsBySource.TryGetValue(projectNode.Id, out var targets))
        {
            foreach (var targetId in targets)
            {
                if (projectById.TryGetValue(targetId, out var targetProj))
                {
                    var subRes = DetectDriverForProjectInLayer2(
                        targetProj,
                        projectById,
                        dependsOnTargetsBySource,
                        memoizedByProjectId,
                        ctx,
                        visited);
                    if (subRes.Engine != null)
                    {
                        return memoizedByProjectId[projectNode.Id] = subRes;
                    }
                }
            }
        }

        // Check ancestor directory manifest driver hints (Directory.Packages.props, Directory.Build.props, root package.json, virtual Cargo.toml)
        var projectAbsDir = string.IsNullOrEmpty(projectNode.Path)
            ? ctx.AbsoluteWorkspacePath
            : (Path.IsPathRooted(projectNode.Path) ? projectNode.Path : Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, projectNode.Path)));
        var normRoot = ctx.AbsoluteWorkspacePath.NormalizeFullPath(trimTrailingSlash: true);
        var curr = projectAbsDir.NormalizeFullPath(trimTrailingSlash: true);
        while (!string.IsNullOrEmpty(curr))
        {
            if (ctx.DirectoryManifestDrivers.TryGetValue(curr, out var dirDriver))
            {
                return memoizedByProjectId[projectNode.Id] = (dirDriver.Engine, dirDriver.Family);
            }

            if (string.Equals(curr, normRoot, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var slashIdx = curr.LastIndexOf('/');
            if (slashIdx <= 0) break;
            curr = curr[..slashIdx];
        }

        // Check appsettings*.json enrichments already parsed once by SharedFileEnricher
        foreach (var cfg in projectNode.Enrichments.OfType<ConfigurationFileInformation>())
        {
            var fName = Path.GetFileName(cfg.SourceFilePath);
            if (!fName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) ||
                !fName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var kv in cfg.Values)
            {
                var val = kv.Value;
                if (string.IsNullOrEmpty(val)) continue;
                if (val.Contains("port=5432", StringComparison.OrdinalIgnoreCase) ||
                    val.Contains("username=postgres", StringComparison.OrdinalIgnoreCase) ||
                    val.Contains("npgsql", StringComparison.OrdinalIgnoreCase) ||
                    val.Contains("database=postgres", StringComparison.OrdinalIgnoreCase))
                {
                    return memoizedByProjectId[projectNode.Id] = ("PostgreSQL", "relational");
                }
                if (val.Contains("initial catalog=", StringComparison.OrdinalIgnoreCase) ||
                    val.Contains("trusted_connection=", StringComparison.OrdinalIgnoreCase) ||
                    val.Contains("server=localhost;database=", StringComparison.OrdinalIgnoreCase))
                {
                    return memoizedByProjectId[projectNode.Id] = ("SQL Server", "relational");
                }
                if (val.Contains("port=3306", StringComparison.OrdinalIgnoreCase) ||
                    val.Contains("uid=root", StringComparison.OrdinalIgnoreCase) ||
                    val.Contains("user id=root", StringComparison.OrdinalIgnoreCase))
                {
                    return memoizedByProjectId[projectNode.Id] = ("MySQL", "relational");
                }
                if (val.Contains("data source=", StringComparison.OrdinalIgnoreCase) &&
                    (val.Contains(".db", StringComparison.OrdinalIgnoreCase) || val.Contains(".sqlite", StringComparison.OrdinalIgnoreCase)))
                {
                    return memoizedByProjectId[projectNode.Id] = ("SQLite", "relational");
                }
            }
        }

        return memoizedByProjectId[projectNode.Id] = (null, null);
    }
}
