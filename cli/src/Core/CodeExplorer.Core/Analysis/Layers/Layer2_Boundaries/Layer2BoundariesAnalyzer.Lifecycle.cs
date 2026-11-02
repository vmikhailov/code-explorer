using System.Text.Json;
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

    private static void PopulatePackageLifecycleAndEntryPoints(
        List<ProjectNode> projects,
        List<FileNode> workspaceFiles,
        List<Relationship> dependencies,
        ParsingContext ctx)
    {
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
            if (!File.Exists(pkgJsonPath)) continue;

            var relManifestPath = string.IsNullOrEmpty(project.Path)
                ? "package.json"
                : $"{project.Path.Trim('/')}/package.json";

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(pkgJsonPath));
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
}
