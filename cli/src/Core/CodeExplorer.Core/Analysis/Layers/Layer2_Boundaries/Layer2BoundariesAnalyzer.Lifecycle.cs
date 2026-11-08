using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer2ProjectParser
{
    private static readonly HashSet<string> InstallHookNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "preinstall", "install", "postinstall", "preuninstall", "postuninstall", "prepublish"
    };

    private static readonly HashSet<string> BuildHookNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "build", "prepare", "prepack", "compile"
    };

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

    private static void PopulatePackageLifecycleAndEntryPoints(Layer1Result l1Result, List<ProjectNode> projects, List<PackageNode> packages, List<Relationship> dependencies, ParsingContext ctx)




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
            var projDir = string.IsNullOrEmpty(project.Path)
                ? ctx.AbsoluteWorkspacePath
                : Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, project.Path));
            var pkgJsonPath = Path.Combine(projDir, "package.json");
            var pkgJsonText = PhysicalFileStore.ReadAllText(pkgJsonPath);
            if (string.IsNullOrEmpty(pkgJsonText)) continue;

            var normProj = project.Path.Replace('\\', '/').Trim('/', '.');
            var relManifestPath = string.IsNullOrEmpty(normProj)
                ? "package.json"
                : $"{normProj}/package.json";

            try
            {
                var root = CypherValue.TryParseJsonText(pkgJsonText);
                if (root.Kind != CypherValueKind.Object) continue;
                PopulatePackageJsonProjectExtensions(project, root);

                if (root.TryGetProperty("scripts", out var scriptsProp) && scriptsProp.Kind == CypherValueKind.Object)
                {
                    foreach (var script in scriptsProp.AsObject())
                    {
                        var cmd = script.Value.AsString();
                        var isInstallHook = InstallHookNames.Contains(script.Key);
                        var isBuildHook = !isInstallHook && BuildHookNames.Contains(script.Key);
                        if (!isInstallHook && !isBuildHook) continue;

                        var entryType = isInstallHook ? "lifecycle-hook" : "build-hook";
                        var entryId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.EntryPoint}:{entryType}:{project.Path}:{script.Key}";
                        var entryNode = new EntryPointNode(
                            Id: entryId,
                            Name: script.Key,
                            Path: relManifestPath,
                            EntryType: entryType)
                        {
                            Command = cmd,
                            HookKind = isInstallHook ? "install" : "build"
                        };

                        project.Children.Add(entryNode);
                        dependencies.Add(new Relationship(project.Id, entryId, OntologyConstants.Relationships.Contains));
                        dependencies.Add(new Relationship(entryId, project.Id, OntologyConstants.Relationships.BelongsTo));

                        // Link lifecycle hook to any local script FileNode it triggers (e.g., `node setup_bun.js`)
                        foreach (var candidateFile in ResolveReferencedScriptFiles(cmd, project.Path, fileByRelPath))
                        {
                            dependencies.Add(new Relationship(entryId, candidateFile.Id, OntologyConstants.Relationships.Triggers));
                        }
                    }
                }

                // Record package export entrypoints (`main`, `module`, `exports`, `bin`)
                var exportTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (root.TryGetProperty("main", out var mainProp) && mainProp.Kind == CypherValueKind.String)
                {
                    var mainVal = mainProp.AsNullableString();
                    if (!string.IsNullOrWhiteSpace(mainVal)) exportTargets.Add(mainVal);
                }
                if (root.TryGetProperty("module", out var modProp) && modProp.Kind == CypherValueKind.String)
                {
                    var modVal = modProp.AsNullableString();
                    if (!string.IsNullOrWhiteSpace(modVal)) exportTargets.Add(modVal);
                }
                if (root.TryGetProperty("exports", out var expProp))
                {
                    CollectExportTargetStrings(expProp, exportTargets);
                }

                foreach (var targetRel in exportTargets)
                {
                    var resolvedFile = TryMatchProjectFile(project.Path, targetRel, fileByRelPath);
                    if (resolvedFile != null)
                    {
                        var expId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.EntryPoint}:package-export:{resolvedFile.Path}";
                        var expNode = new EntryPointNode(expId, Path.GetFileName(resolvedFile.Path), resolvedFile.Path, "package-export");
                        project.Children.Add(expNode);
                        dependencies.Add(new Relationship(project.Id, expId, OntologyConstants.Relationships.Contains));
                        dependencies.Add(new Relationship(expId, resolvedFile.Id, OntologyConstants.Relationships.DeclaredIn));
                    }
                }
            }
            catch
            {
                // Ignore malformed package.json
            }
        }
        PopulateWorkspaceManifestsAndDrivers(l1Result, projects, packages, dependencies, ctx);
        {
            {
            }
        }
    }

    private static void CollectExportTargetStrings(CypherValue elem, HashSet<string> targets)
    {
        if (elem.Kind == CypherValueKind.String)
        {
            var s = elem.AsNullableString();
            if (!string.IsNullOrWhiteSpace(s) && !s.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase))
            {
                targets.Add(s);
            }
        }
        else if (elem.Kind == CypherValueKind.Object)
        {
            foreach (var p in elem.AsObject())
            {
                if (p.Key.Equals("types", StringComparison.OrdinalIgnoreCase)) continue;
                CollectExportTargetStrings(p.Value, targets);
            }
        }
        else if (elem.Kind == CypherValueKind.Array)
        {
            foreach (var item in elem.AsArray())
            {
                CollectExportTargetStrings(item, targets);
            }
        }
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

    private static readonly char[] PathWildcardTrimChars = ['*', '/'];

    private static void PopulatePackageJsonProjectExtensions(ProjectNode project, CypherValue root)
    {
        var hints = project.Signals().ManifestHints ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string? pkgName = null;
        if (root.TryGetProperty(OntologyConstants.ManifestFields.Name, out var nameProp) && nameProp.Kind == CypherValueKind.String)
        {
            pkgName = nameProp.AsNullableString();
            if (!string.IsNullOrWhiteSpace(pkgName))
            {
                project.PackageName = pkgName;
                hints[OntologyConstants.ExtensionKeys.PackageName] = pkgName;
            }
        }

        if (root.TryGetProperty(OntologyConstants.ManifestFields.Version, out var verProp) && verProp.Kind == CypherValueKind.String)
        {
            var ver = verProp.AsNullableString();
            if (!string.IsNullOrWhiteSpace(ver))
            {
                project.PackageVersion = ver;
                hints[OntologyConstants.ExtensionKeys.PackageVersion] = ver;
            }
        }

        string? publisher = null;
        if (root.TryGetProperty(OntologyConstants.ManifestFields.Publisher, out var pubProp) && pubProp.Kind == CypherValueKind.String)
        {
            publisher = pubProp.AsNullableString();
        }
        else if (!string.IsNullOrEmpty(pkgName) && pkgName.StartsWith('@') && pkgName.Contains('/'))
        {
            publisher = pkgName[1..pkgName.IndexOf('/')];
        }
        if (!string.IsNullOrWhiteSpace(publisher))
        {
            project.Publisher = publisher;
            hints[OntologyConstants.ExtensionKeys.Publisher] = publisher;
        }

        if (root.TryGetProperty(OntologyConstants.ManifestFields.Main, out var mainProp) && mainProp.Kind == CypherValueKind.String)
        {
            var mainVal = mainProp.AsNullableString();
            if (!string.IsNullOrWhiteSpace(mainVal))
            {
                project.MainFile = mainVal;
                hints[OntologyConstants.ExtensionKeys.MainFile] = mainVal;
            }
        }

        if (root.TryGetProperty(OntologyConstants.ManifestFields.Module, out var modProp) && modProp.Kind == CypherValueKind.String)
        {
            var modVal = modProp.AsNullableString();
            if (!string.IsNullOrWhiteSpace(modVal))
            {
                project.ModuleFile = modVal;
                hints[OntologyConstants.ExtensionKeys.ModuleFile] = modVal;
            }
        }

        if (root.TryGetProperty(OntologyConstants.ManifestFields.Description, out var descProp) && descProp.Kind == CypherValueKind.String)
        {
            var desc = descProp.AsNullableString();
            if (!string.IsNullOrWhiteSpace(desc))
            {
                project.Description = desc;
                hints[OntologyConstants.ExtensionKeys.Description] = desc;
            }
        }

        if (root.TryGetProperty(OntologyConstants.ManifestFields.Bin, out var binProp) &&
            ((binProp.Kind == CypherValueKind.String && !string.IsNullOrWhiteSpace(binProp.AsNullableString())) ||
             (binProp.Kind == CypherValueKind.Object && binProp.AsObject().Count > 0)))
        {
            project.HasBin = true;
            hints[OntologyConstants.ExtensionKeys.HasBin] = OntologyConstants.ExtensionKeys.TrueValue;
        }

        if (root.TryGetProperty(OntologyConstants.ManifestFields.Contributes, out var contribProp) && contribProp.Kind == CypherValueKind.Object)
        {
            project.HasContributes = true;
            hints[OntologyConstants.ExtensionKeys.HasContributes] = OntologyConstants.ExtensionKeys.TrueValue;
        }

        if (root.TryGetProperty(OntologyConstants.ManifestFields.Engines, out var engProp) && engProp.Kind == CypherValueKind.Object &&
            engProp.TryGetProperty(OntologyConstants.ManifestFields.VsCode, out _))
        {
            project.HasVsCodeEngine = true;
            hints[OntologyConstants.ExtensionKeys.HasVsCodeEngine] = OntologyConstants.ExtensionKeys.TrueValue;
        }

        if (root.TryGetProperty(OntologyConstants.ManifestFields.Keywords, out var kwProp) && kwProp.Kind == CypherValueKind.Array)
        {
            var kws = kwProp.AsStringList().Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            if (kws.Length > 0)
            {
                var kwJson = JsonSerializer.Serialize(kws);
                project.KeywordsJson = kwJson;
                hints[OntologyConstants.ExtensionKeys.KeywordsJson] = kwJson;
            }
        }

        var extDeps = new List<string>();
        foreach (var propName in new[] { "extensionDependencies", "extensionPack" })
        {
            if (root.TryGetProperty(propName, out var arr) && arr.Kind == CypherValueKind.Array)
            {
                foreach (var val in arr.AsStringList())
                {
                    if (!string.IsNullOrWhiteSpace(val)) extDeps.Add(val);
                }
            }
        }
        if (extDeps.Count > 0)
        {
            var extDepsJson = JsonSerializer.Serialize(extDeps);
            project.ExtensionDependenciesJson = extDepsJson;
            hints["extension_dependencies_json"] = extDepsJson;
        }

        string? activationJoined = null;
        if (root.TryGetProperty(OntologyConstants.ManifestFields.ActivationEvents, out var actProp) && actProp.Kind == CypherValueKind.Array)
        {
            var acts = actProp.AsStringList().Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            if (acts.Length > 0)
            {
                activationJoined = string.Join(",", acts);
                project.ActivationEvents = activationJoined;
                hints["activation_events"] = activationJoined;
            }
        }

        foreach (var pkgNode in project.Children.OfType<PackageNode>().Where(p => p.IsInternal))
        {
            if (!string.IsNullOrWhiteSpace(publisher)) pkgNode.Publisher = publisher;
            if (project.HasVsCodeEngine == true) pkgNode.HasVsCodeEngine = true;
            if (!string.IsNullOrWhiteSpace(activationJoined)) pkgNode.ActivationEvents = activationJoined;
        }
    }

    private static readonly Regex ComposeParticipantRegex = new(
        @"\b(?<name>[A-Za-z0-9-]+_participant_\d+)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static void PopulateWorkspaceManifestsAndDrivers(
        Layer1Result l1Result,
        List<ProjectNode> projects,
        List<PackageNode> packages,
        List<Relationship> dependencies,
        ParsingContext ctx)
    {
        var ws = l1Result.Workspace;
        var wsSignals = ws.Signals();

        // 1. Root package.json fallback on WorkspaceNode (when no ProjectNode exists or for root package lookup)
        var rootPkgPath = Path.Combine(ctx.AbsoluteWorkspacePath, "package.json");
        var rootPkgText = PhysicalFileStore.ReadAllText(rootPkgPath);
        if (!string.IsNullOrEmpty(rootPkgText))
        {
            try
            {
                var root = CypherValue.TryParseJsonText(rootPkgText);
                if (root.TryGetProperty(OntologyConstants.ManifestFields.Name, out var n) && n.Kind == CypherValueKind.String && !string.IsNullOrWhiteSpace(n.AsNullableString()))
                    ws.PackageName = n.AsString();
                if (root.TryGetProperty(OntologyConstants.ManifestFields.Version, out var v) && v.Kind == CypherValueKind.String && !string.IsNullOrWhiteSpace(v.AsNullableString()))
                    ws.PackageVersion = v.AsString();
                if (root.TryGetProperty(OntologyConstants.ManifestFields.Publisher, out var pub) && pub.Kind == CypherValueKind.String && !string.IsNullOrWhiteSpace(pub.AsNullableString()))
                    ws.Publisher = pub.AsString();
                if (root.TryGetProperty(OntologyConstants.ManifestFields.Engines, out var eng) && eng.Kind == CypherValueKind.Object && eng.TryGetProperty(OntologyConstants.ManifestFields.VsCode, out _))
                    ws.HasVsCodeEngine = true;
                if (root.TryGetProperty(OntologyConstants.ManifestFields.Contributes, out var contrib) && contrib.Kind == CypherValueKind.Object)
                    ws.HasContributes = true;
                if (root.TryGetProperty(OntologyConstants.ManifestFields.ActivationEvents, out var act) && act.Kind == CypherValueKind.Array)
                {
                    var acts = act.AsStringList().Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                    if (acts.Length > 0) ws.ActivationEvents = string.Join(",", acts);
                }
            }
            catch
            {
                // ignored: malformed root package.json
            }
        }

        // 2. .vscode/tasks.json folderOpen autorun tasks
        var tasksJsonPath = Path.Combine(ctx.AbsoluteWorkspacePath, ".vscode", "tasks.json");
        var tasksText = PhysicalFileStore.ReadAllText(tasksJsonPath);
        if (!string.IsNullOrEmpty(tasksText))
        {
            try
            {
                var tRoot = CypherValue.TryParseJsonText(tasksText);
                if (tRoot.TryGetProperty(OntologyConstants.ManifestFields.Tasks, out var tasksArr) && tasksArr.Kind == CypherValueKind.Array)
                {
                    var rootProj = projects.FirstOrDefault(p => string.IsNullOrEmpty(p.Path)) ?? projects.FirstOrDefault();
                    var folderOpenTasks = new List<Dictionary<string, string>>();
                    foreach (var task in tasksArr.AsArray())
                    {
                        if (task.Kind != CypherValueKind.Object) continue;
                        const string folderOpenLiteral = "folderOpen";
                        var isFolderOpen = task.TryGetProperty(OntologyConstants.ManifestFields.RunOptions, out var ro) &&
                                           ro.Kind == CypherValueKind.Object &&
                                           ro.TryGetProperty(OntologyConstants.ManifestFields.RunOn, out var runOn) &&
                                           string.Equals(runOn.AsNullableString(), folderOpenLiteral, StringComparison.OrdinalIgnoreCase);
                        if (!isFolderOpen) continue;

                        var label = task.TryGetProperty(OntologyConstants.ManifestFields.Label, out var lbl) ? lbl.AsString(folderOpenLiteral) : folderOpenLiteral;
                        var cmd = task.TryGetProperty(OntologyConstants.ManifestFields.Command, out var c) ? c.AsString() : "";
                        folderOpenTasks.Add(new Dictionary<string, string> { [OntologyConstants.ManifestFields.Label] = label, [OntologyConstants.ManifestFields.Command] = cmd });

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
                    if (folderOpenTasks.Count > 0)
                    {
                        wsSignals.VsCodeFolderOpenTasksJson = JsonSerializer.Serialize(folderOpenTasks);
                    }
                }
            }
            catch
            {
                // ignored: malformed .vscode/tasks.json
            }
        }

        // 3. .nuspec manifest metadata & dependencies
        foreach (var (_, (manifestPath, _, _, _)) in ctx.DiscoveredManifestFiles)
        {
            if (!manifestPath.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)) continue;
            var nuspecXml = PhysicalFileStore.ReadAllText(manifestPath);
            if (string.IsNullOrEmpty(nuspecXml)) continue;

            try
            {
                var xdoc = XDocument.Parse(nuspecXml);
                var metadata = xdoc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("metadata", StringComparison.OrdinalIgnoreCase));
                var nuspecId = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("id", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
                var nuspecVer = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("version", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();

                if (!string.IsNullOrEmpty(nuspecId)) wsSignals.NuspecId = nuspecId;
                if (!string.IsNullOrEmpty(nuspecVer)) wsSignals.NuspecVersion = nuspecVer;

                var ownerProj = projects.FirstOrDefault(p => string.IsNullOrEmpty(p.Path)) ?? projects.FirstOrDefault();
                foreach (var depElem in xdoc.Descendants().Where(e => e.Name.LocalName.Equals("dependency", StringComparison.OrdinalIgnoreCase)))
                {
                    var depId = depElem.Attribute(OntologyConstants.ManifestFields.Id)?.Value?.Trim();
                    var depVer = depElem.Attribute(OntologyConstants.ManifestFields.Version)?.Value?.Trim() ?? "*";
                    if (string.IsNullOrWhiteSpace(depId)) continue;

                    var pkgNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Package}:nuget:{depId.ToLowerInvariant()}";
                    if (!packages.Any(p => p.Id.Equals(pkgNodeId, StringComparison.OrdinalIgnoreCase)))
                    {
                        var pkgNode = new PackageNode(
                            Id: pkgNodeId,
                            Name: depId,
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
                            var depRel = Relationship.FromRelationship(new DependsOnRelationship(ownerProj.Id, pkgNodeId, new() { ["dependency_type"] = "package", ["category"] = "library" }));
                            dependencies.Add(depRel);
                        }
                    }
                }
            }
            catch
            {
                // ignored: malformed .nuspec XML
            }
        }

        // 4. tsconfig.json / tsconfig.base.json compilerOptions.paths
        foreach (var tsConfigName in new[] { "tsconfig.base.json", "tsconfig.json" })
        {
            var tsConfigPath = Path.Combine(ctx.AbsoluteWorkspacePath, tsConfigName);
            var tsConfigText = PhysicalFileStore.ReadAllText(tsConfigPath);
            if (string.IsNullOrEmpty(tsConfigText)) continue;

            try
            {
                var tsRoot = CypherValue.TryParseJsonText(tsConfigText);
                if (tsRoot.TryGetProperty(OntologyConstants.ManifestFields.CompilerOptions, out var compOpts) &&
                    compOpts.Kind == CypherValueKind.Object &&
                    compOpts.TryGetProperty(OntologyConstants.ManifestFields.Paths, out var pathsProp) &&
                    pathsProp.Kind == CypherValueKind.Object)
                {
                    foreach (var p in pathsProp.AsObject())
                    {
                        if (p.Value.Kind == CypherValueKind.Array && p.Value.AsArray() is { Count: > 0 } arr)
                        {
                            var rawTarget = arr[0].AsNullableString();
                            if (!string.IsNullOrEmpty(rawTarget))
                            {
                                ctx.TsConfigPaths[p.Key] = rawTarget.Replace('\\', '/');
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignored: malformed tsconfig.json
            }
        }

        // 5. Docker compose participants for Layer 4 PeerMesh synthesis
        var participants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in l1Result.Files)
        {
            var fileName = Path.GetFileName(file.Path);
            var ext = Path.GetExtension(fileName);
            if ((ext.Equals(".yml", StringComparison.OrdinalIgnoreCase) || ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase)) &&
                (fileName.StartsWith("docker-compose", StringComparison.OrdinalIgnoreCase) || fileName.StartsWith("compose", StringComparison.OrdinalIgnoreCase)) &&
                !fileName.Contains(".test.", StringComparison.OrdinalIgnoreCase))
            {
                var content = file.PreloadedContent ?? PhysicalFileStore.ReadAllText(file.FullPath);
                if (!string.IsNullOrEmpty(content))
                {
                    foreach (Match m in ComposeParticipantRegex.Matches(content))
                    {
                        participants.Add(m.Groups["name"].Value);
                    }
                    if (participants.Count > 0)
                    {
                        file.Signals().ComposeParticipants = string.Join(" ", participants);
                    }
                }
            }
        }
        if (participants.Count > 0)
        {
            wsSignals.ComposeParticipants = string.Join(",", participants);
        }

        // 6. Pre-compute project database drivers from Layer 1/2 manifests & packages
        PopulateProjectDatabaseDrivers(projects, dependencies, ctx);
    }

    private static void PopulateProjectDatabaseDrivers(
        List<ProjectNode> projects,
        List<Relationship> dependencies,
        ParsingContext ctx)
    {
        var projectById = projects
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var project in projects)
        {
            var projSignals = project.Signals();
            projSignals.DbDriverChecked = true;
            var (engine, dbType) = DetectDriverForProjectInLayer2(project, projectById, dependencies, ctx, []);
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
        List<Relationship> dependencies,
        ParsingContext ctx,
        HashSet<string> visited)
    {
        if (!visited.Add(projectNode.Id)) return (null, null);

        foreach (var pkg in projectNode.Children.OfType<PackageNode>())
        {
            var pLower = pkg.Name.ToLowerInvariant();
            if (pLower.Contains("npgsql") || pLower is "pg" or "pg-promise" or "@types/pg" or "postgres" or "tokio-postgres" || pLower.Contains("psycopg") || pLower.Contains("asyncpg"))
                return ("PostgreSQL", "relational");
            if (pLower.Contains("sqlclient") || pLower.Contains("entityframeworkcore.sqlserver") || pLower is "mssql" or "tedious" || pLower.Equals("entityframework") || pLower.Equals("system.data.entity"))
                return ("SQL Server", "relational");
            if (pLower.Contains("mysql") || pLower.Contains("pomelo") || pLower.Contains("pymysql"))
                return ("MySQL", "relational");
            if (pLower.Contains("sqlite"))
                return ("SQLite", "relational");
            if (pLower.Contains("oracle"))
                return ("Oracle", "relational");
            if (pLower.Contains("mongodb"))
                return ("MongoDB", "document");
            if (pLower is "redis" or "ioredis" || pLower.Contains("redis") || pLower.Contains("stackexchange"))
                return ("Redis", "cache");
        }

        var projectAbsDir = string.IsNullOrEmpty(projectNode.Path)
            ? ctx.AbsoluteWorkspacePath
            : (Path.IsPathRooted(projectNode.Path) ? projectNode.Path : Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, projectNode.Path)));

        // Transitive local project dependencies already resolved in Layer 2
        foreach (var dep in dependencies.Where(d => d.From.Equals(projectNode.Id, StringComparison.OrdinalIgnoreCase) && d.Kind == CodeExplorer.Cypher.Common.RelationshipKind.DependsOn))
        {
            if (projectById.TryGetValue(dep.To, out var targetProj))
            {
                var subRes = DetectDriverForProjectInLayer2(targetProj, projectById, dependencies, ctx, visited);
                if (subRes.Engine != null) return subRes;
            }
        }

        // Check Directory.Packages.props / Directory.Build.props via PhysicalFileStore
        var curr = projectAbsDir;
        while (!string.IsNullOrEmpty(curr))
        {
            foreach (var propFile in new[] { "Directory.Packages.props", "Directory.Build.props" })
            {
                var content = PhysicalFileStore.ReadAllText(Path.Combine(curr, propFile)).ToLowerInvariant();
                if (!string.IsNullOrEmpty(content))
                {
                    if (content.Contains("npgsql")) return ("PostgreSQL", "relational");
                    if (content.Contains("sqlclient") || content.Contains("entityframeworkcore.sqlserver")) return ("SQL Server", "relational");
                    if (content.Contains("mysql") || content.Contains("pomelo")) return ("MySQL", "relational");
                    if (content.Contains("sqlite")) return ("SQLite", "relational");
                    if (content.Contains("oracle")) return ("Oracle", "relational");
                }
            }
            if (string.Equals(curr.TrimEnd('/', '\\'), ctx.AbsoluteWorkspacePath.TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase))
                break;
            curr = Path.GetDirectoryName(curr);
        }

        // Check appsettings*.json in project directory via PhysicalFileStore
        foreach (var f in PhysicalFileStore.GetDirectoryFiles(projectAbsDir))
        {
            var fName = Path.GetFileName(f);
            if (fName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) && fName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var content = PhysicalFileStore.ReadAllText(f).ToLowerInvariant();
                if (content.Contains("port=5432") || content.Contains("username=postgres") || content.Contains("npgsql") || content.Contains("database=postgres"))
                    return ("PostgreSQL", "relational");
                if (content.Contains("initial catalog=") || content.Contains("trusted_connection=") || content.Contains("server=localhost;database="))
                    return ("SQL Server", "relational");
                if (content.Contains("port=3306") || content.Contains("uid=root") || content.Contains("user id=root"))
                    return ("MySQL", "relational");
                if (content.Contains("data source=") && (content.Contains(".db") || content.Contains(".sqlite")))
                    return ("SQLite", "relational");
            }
        }

        // Fallback for monorepos: workspace root package.json or Cargo.toml
        var rootPkg = PhysicalFileStore.ReadAllText(Path.Combine(ctx.AbsoluteWorkspacePath, "package.json")).ToLowerInvariant();
        if (!string.IsNullOrEmpty(rootPkg))
        {
            if (rootPkg.Contains("\"pg\"") || rootPkg.Contains("\"pg-promise\"") || rootPkg.Contains("\"postgres\""))
                return ("PostgreSQL", "relational");
        }

        foreach (var cargoDir in new[] { projectAbsDir, ctx.AbsoluteWorkspacePath })
        {
            var cargoLower = PhysicalFileStore.ReadAllText(Path.Combine(cargoDir, "Cargo.toml")).ToLowerInvariant();
            if (!string.IsNullOrEmpty(cargoLower) &&
                (cargoLower.Contains("sqlx") || cargoLower.Contains("sea-orm") || cargoLower.Contains("diesel") || cargoLower.Contains("tokio-postgres")))
            {
                if (cargoLower.Contains("postgres") || cargoLower.Contains("pgpool") || cargoLower.Contains("pq"))
                    return ("PostgreSQL", "relational");
                if (cargoLower.Contains("mysql"))
                    return ("MySQL", "relational");
                if (cargoLower.Contains("sqlite"))
                    return ("SQLite", "relational");
                return ("PostgreSQL", "relational");
            }
        }

        return (null, null);
    }
}
