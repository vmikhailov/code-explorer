using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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

    private static Dictionary<string, string> BuildExternalPackageExtensions(ProducedPackageInfo extPack)
    {
        var ext = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["dependency_scope"] = string.IsNullOrEmpty(extPack.Scope) ? "runtime" : extPack.Scope
        };
        var ver = extPack.Version ?? string.Empty;
        if (!string.IsNullOrEmpty(ver))
        {
            ext["raw_version"] = ver;
        }
        if (ver.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            ver.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            ver.StartsWith("git+", StringComparison.OrdinalIgnoreCase) ||
            ver.StartsWith("github:", StringComparison.OrdinalIgnoreCase) ||
            ver.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            ext["is_external_tarball"] = "true";
        }
        return ext;
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

            var relManifestPath = string.IsNullOrEmpty(project.Path)
                ? "package.json"
                : $"{project.Path.Trim('/')}/package.json";

            try
            {
                using var doc = JsonDocument.Parse(pkgJsonText, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                PopulatePackageJsonProjectExtensions(project, doc.RootElement);
                var root = doc.RootElement;

                if (root.TryGetProperty("scripts", out var scriptsProp) && scriptsProp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var script in scriptsProp.EnumerateObject())
                    {
                        var cmd = script.Value.GetString() ?? string.Empty;
                        var isInstallHook = InstallHookNames.Contains(script.Name);
                        var isBuildHook = !isInstallHook && BuildHookNames.Contains(script.Name);
                        if (!isInstallHook && !isBuildHook) continue;

                        var entryType = isInstallHook ? "lifecycle-hook" : "build-hook";
                        var entryId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.EntryPoint}:{entryType}:{project.Path}:{script.Name}";
                        var entryNode = new EntryPointNode(
                            Id: entryId,
                            Name: script.Name,
                            Path: relManifestPath,
                            EntryType: entryType,
                            Extensions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["command"] = cmd,
                                ["hook_kind"] = isInstallHook ? "install" : "build"
                            });

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
                if (root.TryGetProperty("main", out var mainProp) && mainProp.ValueKind == JsonValueKind.String)
                {
                    var mainVal = mainProp.GetString();
                    if (!string.IsNullOrWhiteSpace(mainVal)) exportTargets.Add(mainVal);
                }
                if (root.TryGetProperty("module", out var modProp) && modProp.ValueKind == JsonValueKind.String)
                {
                    var modVal = modProp.GetString();
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

    private static void CollectExportTargetStrings(JsonElement elem, HashSet<string> targets)
    {
        if (elem.ValueKind == JsonValueKind.String)
        {
            var s = elem.GetString();
            if (!string.IsNullOrWhiteSpace(s) && !s.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase))
            {
                targets.Add(s);
            }
        }
        else if (elem.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in elem.EnumerateObject())
            {
                if (p.Name.Equals("types", StringComparison.OrdinalIgnoreCase)) continue;
                CollectExportTargetStrings(p.Value, targets);
            }
        }
        else if (elem.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in elem.EnumerateArray())
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

        var fullRel = string.IsNullOrEmpty(projectPath)
            ? clean
            : $"{projectPath.Replace('\\', '/').Trim('/')}/{clean}";

        if (fileByRelPath.TryGetValue(fullRel, out var direct)) return direct;
        foreach (var ext in new[] { ".js", ".ts", ".mjs", ".cjs", "/index.js", "/index.ts" })
        {
            if (fileByRelPath.TryGetValue(fullRel + ext, out var withExt)) return withExt;
        }
        return null;
    }

    private const string PropName = "name";
    private const string PropVersion = "version";
    private const string PropPublisher = "publisher";
    private const string PropMain = "main";
    private const string PropModule = "module";
    private const string PropDescription = "description";
    private const string PropBin = "bin";
    private const string PropContributes = "contributes";
    private const string PropEngines = "engines";
    private const string PropVsCode = "vscode";
    private const string PropKeywords = "keywords";
    private const string PropActivationEvents = "activationEvents";
    private const string ExtHasVsCodeEngine = "has_vscode_engine";
    private const string PropTasks = "tasks";
    private const string PropRunOptions = "runOptions";
    private const string PropRunOn = "runOn";
    private const string PropLabel = "label";
    private const string PropCommand = "command";
    private const string TasksJsonRelPath = ".vscode/tasks.json";
    private const string FolderOpenTaskType = "vscode-task-folder-open";
    private const string AttrId = "id";
    private const string AttrVersion = "version";
    private const string NuGetEcosystem = "nuget";
    private const string PropCompilerOptions = "compilerOptions";
    private const string PropPaths = "paths";
    private static readonly char[] PathWildcardTrimChars = ['*', '/'];

    private static void PopulatePackageJsonProjectExtensions(ProjectNode project, JsonElement root)
    {
        project.Extensions ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ext = project.Extensions;

        string? pkgName = null;
        if (root.TryGetProperty(PropName, out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
        {
            pkgName = nameProp.GetString();
            if (!string.IsNullOrWhiteSpace(pkgName))
            {
                ext["package_name"] = pkgName;
            }
        }

        if (root.TryGetProperty(PropVersion, out var verProp) && verProp.ValueKind == JsonValueKind.String)
        {
            var ver = verProp.GetString();
            if (!string.IsNullOrWhiteSpace(ver))
            {
                ext["package_version"] = ver;
            }
        }

        string? publisher = null;
        if (root.TryGetProperty(PropPublisher, out var pubProp) && pubProp.ValueKind == JsonValueKind.String)
        {
            publisher = pubProp.GetString();
        }
        else if (!string.IsNullOrEmpty(pkgName) && pkgName.StartsWith('@') && pkgName.Contains('/'))
        {
            publisher = pkgName[1..pkgName.IndexOf('/')];
        }
        if (!string.IsNullOrWhiteSpace(publisher))
        {
            ext["publisher"] = publisher;
        }

        if (root.TryGetProperty(PropMain, out var mainProp) && mainProp.ValueKind == JsonValueKind.String)
        {
            var mainVal = mainProp.GetString();
            if (!string.IsNullOrWhiteSpace(mainVal))
            {
                ext["main_file"] = mainVal;
            }
        }

        if (root.TryGetProperty(PropModule, out var modProp) && modProp.ValueKind == JsonValueKind.String)
        {
            var modVal = modProp.GetString();
            if (!string.IsNullOrWhiteSpace(modVal))
            {
                ext["module_file"] = modVal;
            }
        }

        if (root.TryGetProperty(PropDescription, out var descProp) && descProp.ValueKind == JsonValueKind.String)
        {
            var desc = descProp.GetString();
            if (!string.IsNullOrWhiteSpace(desc))
            {
                ext["description"] = desc;
            }
        }

        if (root.TryGetProperty(PropBin, out var binProp) &&
            ((binProp.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(binProp.GetString())) ||
             (binProp.ValueKind == JsonValueKind.Object && binProp.EnumerateObject().Any())))
        {
            ext["has_bin"] = "true";
        }

        if (root.TryGetProperty(PropContributes, out var contribProp) && contribProp.ValueKind == JsonValueKind.Object)
        {
            ext["has_contributes"] = "true";
        }

        if (root.TryGetProperty(PropEngines, out var engProp) && engProp.ValueKind == JsonValueKind.Object &&
            engProp.TryGetProperty(PropVsCode, out _))
        {
            ext[ExtHasVsCodeEngine] = "true";
        }

        if (root.TryGetProperty(PropKeywords, out var kwProp) && kwProp.ValueKind == JsonValueKind.Array)
        {
            var kws = kwProp.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();
            if (kws.Length > 0)
            {
                ext["keywords_json"] = JsonSerializer.Serialize(kws);
            }
        }

        var extDeps = new List<string>();
        foreach (var propName in new[] { "extensionDependencies", "extensionPack" })
        {
            if (root.TryGetProperty(propName, out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var val = item.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) extDeps.Add(val);
                    }
                }
            }
        }
        if (extDeps.Count > 0)
        {
            ext["extension_dependencies_json"] = JsonSerializer.Serialize(extDeps);
        }

        string? activationJoined = null;
        if (root.TryGetProperty(PropActivationEvents, out var actProp) && actProp.ValueKind == JsonValueKind.Array)
        {
            var acts = actProp.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();
            if (acts.Length > 0)
            {
                activationJoined = string.Join(",", acts);
                ext["activation_events"] = activationJoined;
            }
        }

        foreach (var pkgNode in project.Children.OfType<PackageNode>().Where(p => p.IsInternal))
        {
            pkgNode.Extensions ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(publisher)) pkgNode.Extensions["publisher"] = publisher;
            if (ext.TryGetValue(ExtHasVsCodeEngine, out var vsc)) pkgNode.Extensions[ExtHasVsCodeEngine] = vsc;
            if (!string.IsNullOrWhiteSpace(activationJoined)) pkgNode.Extensions["activation_events"] = activationJoined;
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
        var wsExt = l1Result.Workspace.Extensions ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1. Root package.json fallback on WorkspaceNode (when no ProjectNode exists or for root package lookup)
        var rootPkgPath = Path.Combine(ctx.AbsoluteWorkspacePath, "package.json");
        var rootPkgText = PhysicalFileStore.ReadAllText(rootPkgPath);
        if (!string.IsNullOrEmpty(rootPkgText))
        {
            try
            {
                using var doc = JsonDocument.Parse(rootPkgText, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                var root = doc.RootElement;
                if (root.TryGetProperty(PropName, out var n) && n.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(n.GetString()))
                    wsExt["package_name"] = n.GetString()!;
                if (root.TryGetProperty(PropVersion, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()))
                    wsExt["package_version"] = v.GetString()!;
                if (root.TryGetProperty(PropPublisher, out var pub) && pub.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(pub.GetString()))
                    wsExt["publisher"] = pub.GetString()!;
                if (root.TryGetProperty(PropEngines, out var eng) && eng.ValueKind == JsonValueKind.Object && eng.TryGetProperty(PropVsCode, out _))
                    wsExt[ExtHasVsCodeEngine] = "true";
                if (root.TryGetProperty(PropContributes, out var contrib) && contrib.ValueKind == JsonValueKind.Object)
                    wsExt["has_contributes"] = "true";
                if (root.TryGetProperty(PropActivationEvents, out var act) && act.ValueKind == JsonValueKind.Array)
                {
                    var acts = act.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                    if (acts.Length > 0) wsExt["activation_events"] = string.Join(",", acts);
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
                using var tDoc = JsonDocument.Parse(tasksText, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (tDoc.RootElement.TryGetProperty(PropTasks, out var tasksArr) && tasksArr.ValueKind == JsonValueKind.Array)
                {
                    var rootProj = projects.FirstOrDefault(p => string.IsNullOrEmpty(p.Path)) ?? projects.FirstOrDefault();
                    var folderOpenTasks = new List<Dictionary<string, string>>();
                    foreach (var task in tasksArr.EnumerateArray())
                    {
                        if (task.ValueKind != JsonValueKind.Object) continue;
                        var isFolderOpen = task.TryGetProperty(PropRunOptions, out var ro) &&
                                           ro.ValueKind == JsonValueKind.Object &&
                                           ro.TryGetProperty(PropRunOn, out var runOn) &&
                                           string.Equals(runOn.GetString(), "folderOpen", StringComparison.OrdinalIgnoreCase);
                        if (!isFolderOpen) continue;

                        var label = task.TryGetProperty(PropLabel, out var lbl) ? lbl.GetString() ?? "folderOpen" : "folderOpen";
                        var cmd = task.TryGetProperty(PropCommand, out var c) ? c.GetString() ?? "" : "";
                        folderOpenTasks.Add(new Dictionary<string, string> { ["label"] = label, ["command"] = cmd });

                        if (rootProj != null)
                        {
                            var entryId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.EntryPoint}:vscode-task-folder-open:{label}";
                            var entryNode = new EntryPointNode(
                                Id: entryId,
                                Name: $"vscode.tasks.folderOpen:{label}",
                                Path: TasksJsonRelPath,
                                EntryType: FolderOpenTaskType,
                                Extensions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["command"] = cmd,
                                    ["hook_kind"] = "install",
                                    ["task_label"] = label
                                });
                            rootProj.Children.Add(entryNode);
                            dependencies.Add(new Relationship(rootProj.Id, entryId, OntologyConstants.Relationships.Contains));
                            dependencies.Add(new Relationship(entryId, rootProj.Id, OntologyConstants.Relationships.BelongsTo));
                        }
                    }
                    if (folderOpenTasks.Count > 0)
                    {
                        wsExt["vscode_folder_open_tasks_json"] = JsonSerializer.Serialize(folderOpenTasks);
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

                if (!string.IsNullOrEmpty(nuspecId)) wsExt["nuspec_id"] = nuspecId;
                if (!string.IsNullOrEmpty(nuspecVer)) wsExt["nuspec_version"] = nuspecVer;

                var ownerProj = projects.FirstOrDefault(p => string.IsNullOrEmpty(p.Path)) ?? projects.FirstOrDefault();
                foreach (var depElem in xdoc.Descendants().Where(e => e.Name.LocalName.Equals("dependency", StringComparison.OrdinalIgnoreCase)))
                {
                    var depId = depElem.Attribute(AttrId)?.Value?.Trim();
                    var depVer = depElem.Attribute(AttrVersion)?.Value?.Trim() ?? "*";
                    if (string.IsNullOrWhiteSpace(depId)) continue;

                    var pkgNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Package}:nuget:{depId.ToLowerInvariant()}";
                    if (!packages.Any(p => p.Id.Equals(pkgNodeId, StringComparison.OrdinalIgnoreCase)))
                    {
                        var pkgNode = new PackageNode(
                            Id: pkgNodeId,
                            Name: depId,
                            Version: depVer,
                            IsInternal: false,
                            Ecosystem: NuGetEcosystem,
                            Path: string.Empty,
                            Archetype: OntologyConstants.Archetypes.PureUtility,
                            Extensions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["dependency_scope"] = "runtime",
                                ["raw_version"] = depVer
                            });
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
                using var doc = JsonDocument.Parse(tsConfigText, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (doc.RootElement.TryGetProperty(PropCompilerOptions, out var compOpts) &&
                    compOpts.ValueKind == JsonValueKind.Object &&
                    compOpts.TryGetProperty(PropPaths, out var pathsProp) &&
                    pathsProp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in pathsProp.EnumerateObject())
                    {
                        if (p.Value.ValueKind == JsonValueKind.Array && p.Value.GetArrayLength() > 0)
                        {
                            var rawTarget = p.Value[0].GetString();
                            if (!string.IsNullOrEmpty(rawTarget))
                            {
                                ctx.TsConfigPaths[p.Name] = rawTarget.Replace('\\', '/');
                            }
                        }
                        var aliasKey = p.Name.TrimEnd(PathWildcardTrimChars);
                        if (string.IsNullOrEmpty(aliasKey)) continue;
                        if (p.Value.ValueKind == JsonValueKind.Array && p.Value.GetArrayLength() > 0)
                        {
                            var firstTarget = p.Value[0].GetString()?.TrimEnd(PathWildcardTrimChars);
                            if (!string.IsNullOrEmpty(firstTarget))
                            {
                                var absTarget = Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, firstTarget)).Replace('\\', '/');
                                // Stored raw in ctx.TsConfigPaths above
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
                        file.Extensions ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        file.Extensions["compose_participants"] = string.Join(" ", participants);
                    }
                }
            }
        }
        if (participants.Count > 0)
        {
            wsExt["compose_participants"] = string.Join(",", participants);
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
            project.Extensions ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            project.Extensions["db_driver_checked"] = "true";
            var (engine, dbType) = DetectDriverForProjectInLayer2(project, projectById, dependencies, ctx, []);
            if (!string.IsNullOrEmpty(engine) && !string.IsNullOrEmpty(dbType))
            {
                project.Extensions ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                project.Extensions["db_driver_engine"] = engine;
                project.Extensions["db_driver_type"] = dbType;
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
            if (pLower is "redis" or "ioredis" || pLower.Contains("stackexchange.redis"))
                return ("Redis", "cache");
        }

        var projectAbsDir = string.IsNullOrEmpty(projectNode.Path)
            ? ctx.AbsoluteWorkspacePath
            : (Path.IsPathRooted(projectNode.Path) ? projectNode.Path : Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, projectNode.Path)));

        // Transitive local project dependencies already resolved in Layer 2
        foreach (var dep in dependencies.Where(d => d.From.Equals(projectNode.Id, StringComparison.OrdinalIgnoreCase) && d.Kind == OntologyConstants.Relationships.DependsOn))
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
