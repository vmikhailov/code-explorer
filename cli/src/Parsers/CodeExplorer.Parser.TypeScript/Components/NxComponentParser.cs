using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Components;

namespace CodeExplorer.Parser.TypeScript.Components;

/// <summary>
/// Component parser for Nx Monorepos.
/// Handles standalone project.json manifests, nx.json workspace layouts,
/// and single-package.json monorepo project hierarchies.
/// </summary>
public class NxComponentParser : IComponentLibraryParser
{
    public string Id => "nx";
    public string Name => "Nx";

    public bool CanHandle(ProjectContext context)
    {
        return context.FilesInDirectory.Any(f =>
                   Path.GetFileName(f).Equals("nx.json", StringComparison.OrdinalIgnoreCase) ||
                   Path.GetFileName(f).Equals("project.json", StringComparison.OrdinalIgnoreCase)) ||
               context.Dependencies.Any(d => d.StartWithIC("@nx/") || d.Equals("nx", StringComparison.OrdinalIgnoreCase));
    }

    public ComponentAnalysisResult AnalyzeManifest(ProjectContext context)
    {
        return new ComponentAnalysisResult
        {
            ComponentId = Id,
            ComponentName = Name,
            Role = LibraryRole.Utility,
            Capabilities = ComponentCapabilities.None
        };
    }

    public static void ExtractWorkspaceSubProjects(
        string directoryPath,
        IProjectParser projectParser,
        List<string> sourceRoots,
        List<string> excludedSubpaths,
        List<ParsedProjectDescriptor> subProjects)
    {
        var nxJsonPath = Path.Combine(directoryPath, "nx.json");
        var workspaceJsonPath = Path.Combine(directoryPath, "workspace.json");

        if (!File.Exists(nxJsonPath) && !File.Exists(workspaceJsonPath))
        {
            return;
        }

        try
        {
            // 1. Check declarative projects in workspace.json if present (older Nx workspaces)
            if (File.Exists(workspaceJsonPath))
            {
                AngularComponentParser.ExtractDeclarativeSubProjects(
                    directoryPath, "workspace.json", isFrontendWorkspace: true, projectParser,
                    sourceRoots, excludedSubpaths, subProjects);
            }

            // 2. Scan for standalone project.json files across the workspace
            var discoveredDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in subProjects)
            {
                discoveredDirs.Add(existing.DirectoryPath.Replace('\\', '/').TrimEnd('/'));
            }

            foreach (var projectJsonPath in EnumerateStandaloneProjectJsonFiles(directoryPath))
            {
                var childDir = Path.GetDirectoryName(projectJsonPath);
                if (string.IsNullOrEmpty(childDir) || childDir.Equals(directoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var normChildDir = childDir.Replace('\\', '/').TrimEnd('/');
                if (discoveredDirs.Contains(normChildDir))
                {
                    continue;
                }

                var childFiles = Directory.GetFiles(childDir);
                var childName = projectParser.GetProjectName(childDir, childFiles);
                var childProps = projectParser.ExtractManifestProperties(childDir, childFiles);

                subProjects.Add(new ParsedProjectDescriptor(
                    Name: childName,
                    DirectoryPath: childDir,
                    ProjectType: projectParser.ProjectType,
                    ManifestProperties: childProps));

                discoveredDirs.Add(normChildDir);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // Best-effort workspace scan
        }
    }

    private static IEnumerable<string> EnumerateStandaloneProjectJsonFiles(string rootDir)
    {
        var stack = new Stack<string>();
        stack.Push(rootDir);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(current);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var subDir in subDirs)
            {
                var dirName = Path.GetFileName(subDir);
                if (WorkspaceFileFilter.Default.IsExcludedDirectory(dirName) ||
                    dirName.Equals(".next", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals(".turbo", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals(".yarn", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals(".nx", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var projectJson = Path.Combine(subDir, "project.json");
                if (File.Exists(projectJson))
                {
                    yield return projectJson;
                }

                stack.Push(subDir);
            }
        }
    }
}
