using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Parser.Layers;

/// <summary>
/// Detects nested project manifests that contain no source code beneath them and belong to an enclosing project.
/// </summary>
internal static class SourcelessManifestDetector
{
    private static readonly HashSet<string> SourceCodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ts", ".tsx", ".mts", ".cts", ".js", ".jsx", ".mjs", ".cjs", ".vue", ".svelte",
        ".cs", ".fs", ".vb", ".py", ".go", ".java", ".kt", ".kts", ".scala", ".rs", ".rb", ".php",
        ".swift", ".dart", ".sql", ".cfc", ".cfm", ".graphql", ".gql", ".proto"
    };

    private static readonly HashSet<string> NonSourceDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", "dist", "build", "out", "bin", "obj", ".next", ".turbo", "coverage"
    };

    /// <summary>
    /// Returns the nearest enclosing project when <paramref name="dir"/> declares a project manifest but contains
    /// no source files beneath it; otherwise <c>null</c>. The workspace root itself is never absorbed.
    /// </summary>
    public static ProjectNode? FindOwner(string dir, string[] filesInDir, List<ProjectNode> projects, ParsingContext ctx)
    {
        if (projects.Count == 0) return null;

        var hasManifest = filesInDir.Any(f =>
        {
            var name = Path.GetFileName(f);
            return WorkspaceIndexer._projectParsers.Any(p => !p.IsFallback && p.IsManifestFile(name));
        });
        if (!hasManifest) return null;

        var normDir = dir.NormalizeFullPath(trimTrailingSlash: true);
        ProjectNode? owner = null;
        var ownerDepth = -1;
        foreach (var project in projects)
        {
            var projectDir = Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, project.Path ?? "")).NormalizeFullPath(trimTrailingSlash: true);
            if (projectDir.Equals(normDir, StringComparison.OrdinalIgnoreCase)) return null;
            if (normDir.StartsWith(projectDir + "/", StringComparison.OrdinalIgnoreCase) && projectDir.Length > ownerDepth)
            {
                owner = project;
                ownerDepth = projectDir.Length;
            }
        }

        if (owner == null || ContainsSourceFiles(dir)) return null;
        return owner;
    }

    private static bool ContainsSourceFiles(string dir)
    {
        var pending = new Stack<string>();
        pending.Push(dir);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            IEnumerable<string> files;
            IEnumerable<string> subDirs;
            try
            {
                files = Directory.EnumerateFiles(current);
                subDirs = Directory.EnumerateDirectories(current);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Unreadable directory: treat as "may contain sources" so the project is kept rather than silently dropped.
                return true;
            }

            if (files.Any(f => SourceCodeExtensions.Contains(Path.GetExtension(f)))) return true;
            foreach (var sub in subDirs)
            {
                if (!NonSourceDirectoryNames.Contains(Path.GetFileName(sub))) pending.Push(sub);
            }
        }

        return false;
    }
}
