using CodeExplorer.Core.Common;

namespace CodeExplorer.Core.Analysis;

/// <summary>
/// Detects architectural roles (Service, SharedLibrary, FrontendApp, Worker, DatabaseMigration, CliTool, Test)
/// for projects at indexing time (Layer 2) using manifests, path heuristics, and dependency signals.
/// </summary>
public static class ProjectRoleDetector
{
    public static (ProjectRole Role, bool IsLibrary) DetectRole(
        string directoryPath,
        string[] filesInDirectory,
        string relativeProjectDir,
        string projectName,
        string projectType,
        IReadOnlyList<string>? dependencies = null,
        IReadOnlyDictionary<string, string>? extensions = null)
    {
        var (role, isLib, _) = Detect(directoryPath, filesInDirectory, relativeProjectDir, projectName, projectType, dependencies, extensions);
        return (role, isLib);
    }

    public static (ProjectRole Role, bool IsLibrary, ProjectClassification Classification) Detect(
        string directoryPath,
        string[] filesInDirectory,
        string relativeProjectDir,
        string projectName,
        string projectType,
        IReadOnlyList<string>? dependencies = null,
        IReadOnlyDictionary<string, string>? extensions = null)
    {
        var classification = ProjectEntityClassifierRegistry.Classify(
            directoryPath,
            filesInDirectory,
            relativeProjectDir,
            projectName,
            projectType,
            dependencies,
            extensions);

        var (role, isLib) = classification.Kind switch
        {
            ProjectEntityKind.Library => (ProjectRole.SharedLibrary, true),
            ProjectEntityKind.Test => (ProjectRole.Test, true),
            ProjectEntityKind.CliTool => (ProjectRole.CliTool, false),
            ProjectEntityKind.App => classification.SubKind switch
            {
                ProjectEntitySubKind.Cli => (ProjectRole.CliTool, false),
                _ => (ProjectRole.FrontendApp, false)
            },
            ProjectEntityKind.Worker => (ProjectRole.Worker, false),
            ProjectEntityKind.DatabaseMigration => (ProjectRole.DatabaseMigration, false),
            ProjectEntityKind.FunctionApp => (ProjectRole.Service, false),
            ProjectEntityKind.Service => (ProjectRole.Service, false),
            _ => (ProjectRole.Service, false)
        };

        return (role, isLib, classification);
    }

    public static bool HasProtocolTokens(string name, string relPath)
    {
        var lowerName = (name ?? "").ToLowerInvariant();
        var normalizedPath = (relPath ?? "").Replace('\\', '/').ToLowerInvariant();

        if (lowerName.EndsWith(".graphql") || lowerName.EndsWith(".grapql") ||
            lowerName.EndsWith(".grpc") || lowerName.EndsWith(".gateway") ||
            lowerName.EndsWith(".bff") || lowerName.EndsWith(".endpoint") ||
            lowerName.EndsWith(".endpoints") || lowerName.EndsWith("-graphql") ||
            lowerName.EndsWith("-grapql") || lowerName.EndsWith("-grpc") ||
            lowerName.EndsWith("-gateway") || lowerName.EndsWith("-bff") ||
            lowerName.EndsWith("-mqtt") || lowerName.EndsWith("-endpoint"))
        {
            return true;
        }

        if (normalizedPath.Contains("/graphql/") || normalizedPath.Contains("/grapql/") ||
            normalizedPath.Contains("/grpc/") || normalizedPath.Contains("/gateway/") ||
            normalizedPath.Contains("/gateways/") || normalizedPath.Contains("/bff/") ||
            normalizedPath.Contains("/mqtt/") || normalizedPath.Contains("/endpoint/") ||
            normalizedPath.Contains("/endpoints/"))
        {
            return true;
        }

        var parts = lowerName.Split('.', '-', '_');
        var protocolTokens = new[] { "graphql", "grapql", "grpc", "gateway", "gateways", "bff", "mqtt", "endpoint", "endpoints" };
        if (parts.Any(p => protocolTokens.Contains(p)))
        {
            return true;
        }

        return false;
    }
}
