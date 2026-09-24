using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Analysis;

/// <summary>
/// Factory that determines the architectural role of a project at index time (Layer 2)
/// and instantiates the appropriate first-class semantic entity (ServiceNode, AppNode,
/// LibraryNode, WorkerNode, CliToolNode, or base ProjectNode).
/// </summary>
public static class ProjectNodeFactory
{
    public static ProjectNode Create(
        string id,
        string projectName,
        string relativeProjectDir,
        string projectType,
        string directoryPath,
        string[] filesInDirectory,
        IReadOnlyList<string>? externalPackages = null,
        Dictionary<string, string>? extensions = null)
    {
        var (role, isLibrary) = ProjectRoleDetector.DetectRole(
            directoryPath,
            filesInDirectory,
            relativeProjectDir,
            projectName,
            projectType,
            externalPackages);

        extensions ??= new Dictionary<string, string>();
        extensions["role"] = role.ToString();
        extensions["is_library"] = isLibrary ? "true" : "false";
        extensions["entity_type"] = isLibrary ? "library" : "service";

        return role switch
        {
            ProjectRole.Service => new ServiceNode(id, projectName, relativeProjectDir, projectType, extensions)
            {
                Role = role.ToString(),
                IsLibrary = isLibrary
            },
            ProjectRole.FrontendApp => new AppNode(id, projectName, relativeProjectDir, projectType, extensions)
            {
                Role = role.ToString(),
                IsLibrary = isLibrary
            },
            ProjectRole.SharedLibrary => new LibraryNode(id, projectName, relativeProjectDir, projectType, extensions)
            {
                Role = role.ToString(),
                IsLibrary = isLibrary
            },
            ProjectRole.Worker => new WorkerNode(id, projectName, relativeProjectDir, projectType, extensions)
            {
                Role = role.ToString(),
                IsLibrary = isLibrary
            },
            ProjectRole.CliTool => new CliToolNode(id, projectName, relativeProjectDir, projectType, extensions)
            {
                Role = role.ToString(),
                IsLibrary = isLibrary
            },
            _ => isLibrary
                ? new LibraryNode(id, projectName, relativeProjectDir, projectType, extensions)
                {
                    Role = role.ToString(),
                    IsLibrary = isLibrary
                }
                : new ProjectNode(id, projectName, relativeProjectDir, projectType, role.ToString(), isLibrary, extensions)
        };
    }
}
