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
        extensions ??= new Dictionary<string, string>();

        var (role, isLibrary, classification) = ProjectRoleDetector.Detect(
            directoryPath,
            filesInDirectory,
            relativeProjectDir,
            projectName,
            projectType,
            externalPackages,
            extensions);

        var kindStr = classification.Kind != ProjectEntityKind.Unknown 
            ? classification.Kind.ToString() 
            : role.ToString();

        var entityType = classification.Kind switch
        {
            ProjectEntityKind.App => "app",
            ProjectEntityKind.Service or ProjectEntityKind.FunctionApp => "service",
            ProjectEntityKind.Worker => "worker",
            ProjectEntityKind.CliTool or ProjectEntityKind.ScriptProject or ProjectEntityKind.DatabaseMigration => "cli",
            ProjectEntityKind.Test => "test",
            ProjectEntityKind.Library => "library",
            _ => role switch
            {
                ProjectRole.FrontendApp => "app",
                ProjectRole.Worker => "worker",
                ProjectRole.CliTool or ProjectRole.ScriptProject or ProjectRole.DatabaseMigration => "cli",
                ProjectRole.Test => "test",
                ProjectRole.SharedLibrary => "library",
                _ => isLibrary ? "library" : "service"
            }
        };

        extensions["role"] = role.ToString();
        extensions["is_library"] = isLibrary ? "true" : "false";
        extensions["entity_type"] = entityType;
        extensions["entity_kind"] = kindStr;
        extensions["kind"] = kindStr;
        extensions["sub_kind"] = classification.SubKind.ToString();
        extensions["language"] = projectType;
        extensions["project_type"] = projectType;

        var canonicalPrimaryRole = NormalizeRoleName(extensions.GetValueOrDefault("primary_role"))
            ?? NormalizeRoleName(classification.Kind.ToString())
            ?? NormalizeRoleName(role.ToString())
            ?? (isLibrary ? "Library" : "Service");

        extensions["primary_role"] = canonicalPrimaryRole;

        var secRolesList = new List<string>();
        if (extensions.TryGetValue("secondary_roles", out var secJson) && !string.IsNullOrWhiteSpace(secJson))
        {
            try
            {
                using var sDoc = System.Text.Json.JsonDocument.Parse(secJson);
                if (sDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in sDoc.RootElement.EnumerateArray())
                    {
                        var norm = NormalizeRoleName(item.GetString());
                        if (norm != null && norm != canonicalPrimaryRole && !secRolesList.Contains(norm))
                        {
                            secRolesList.Add(norm);
                        }
                    }
                }
            }
            catch
            {
                // Fallback
            }
        }

        extensions["secondary_roles"] = System.Text.Json.JsonSerializer.Serialize(secRolesList);
        extensions["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { canonicalPrimaryRole }.Concat(secRolesList).Distinct());

        return new ProjectNode(id, projectName, relativeProjectDir, projectType, role.ToString(), isLibrary, extensions)
        {
            EntityKind = classification.Kind,
            SubKind = classification.SubKind
        };
    }

    public static string? NormalizeRoleName(string? r) => r switch
    {
        "App" or "FrontendApp" or "WebApp" or "MobileApp" or "DesktopApp" or "FrontendFramework" => "App",
        "Service" or "FunctionApp" or "WebService" or "ApiGateway" => "Service",
        "Worker" or "WorkerService" or "Scheduler" or "QueueWorker" => "Worker",
        "CliTool" or "CliApp" or "ScriptProject" or "DatabaseMigration" or "CliFramework" => "CliTool",
        "Test" or "TestSuite" or "TestFramework" => "Test",
        "Library" or "SharedLibrary" or "UiComponentLibrary" => "Library",
        _ => null
    };
}
