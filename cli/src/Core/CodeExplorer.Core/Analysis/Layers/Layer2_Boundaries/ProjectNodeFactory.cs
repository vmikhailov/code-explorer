using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Database;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Analysis;

/// <summary>
/// Factory that determines the architectural role of a project at index time (Layer 2)
/// and instantiates the appropriate first-class semantic entity (ServiceNode, AppNode,
/// LibraryNode, WorkerNode, CliToolNode, or base ProjectNode).
/// </summary>
public static class ProjectNodeFactory
{
    private const string KeyPrimaryRole = "primary_role";
    private const string KeySecondaryRoles = "secondary_roles";
    private const string KeyAllRoles = "all_roles";
    private const string KeyEntityKind = "entity_kind";
    private const string KeySubKind = "sub_kind";
    private const string KeyFramework = "framework";
    private const string KeyIsBuildOrchestrator = "is_build_orchestrator";
    private const string KeyExcludedSubpaths = "excluded_subpaths";
    private const string KeyAbsorbedSubpaths = "absorbed_subpaths";
    private const string KeyArchetype = "archetype";
    private const string KeyCargoManifestDir = "cargo_manifest_dir";
    private const string BoolTrueStr = "true";

    public static ProjectNode Create(
        string id,
        string projectName,
        string relativeProjectDir,
        string projectType,
        string directoryPath,
        string[] filesInDirectory,
        IReadOnlyList<string>? externalPackages = null,
        Dictionary<string, string>? manifestProps = null)
    {
        manifestProps ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var (role, isLibrary, classification) = ProjectRoleDetector.Detect(
            directoryPath,
            filesInDirectory,
            relativeProjectDir,
            projectName,
            projectType,
            externalPackages,
            manifestProps);

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
                CodeExplorer.Core.Common.ProjectRole.FrontendApp => "app",
                CodeExplorer.Core.Common.ProjectRole.Worker => "worker",
                CodeExplorer.Core.Common.ProjectRole.CliTool or CodeExplorer.Core.Common.ProjectRole.ScriptProject or CodeExplorer.Core.Common.ProjectRole.DatabaseMigration => "cli",
                CodeExplorer.Core.Common.ProjectRole.Test => "test",
                CodeExplorer.Core.Common.ProjectRole.SharedLibrary => "library",
                _ => isLibrary ? "library" : "service"
            }
        };

        var canonicalPrimaryRole = NormalizeRoleName(manifestProps.GetValueOrDefault(KeyPrimaryRole))
            ?? NormalizeRoleName(classification.Kind.ToString())
            ?? NormalizeRoleName(role.ToString())
            ?? (isLibrary ? "Library" : "Service");

        var secRolesList = new List<string>();
        if (manifestProps.TryGetValue(KeySecondaryRoles, out var secJson) && !string.IsNullOrWhiteSpace(secJson))
        {
            foreach (var item in CodeExplorer.Core.Database.CypherValue.TryParseJsonText(secJson).AsStringList())
            {
                var norm = NormalizeRoleName(item);
                if (norm != null && norm != canonicalPrimaryRole && !secRolesList.Contains(norm))
                {
                    secRolesList.Add(norm);
                }
            }
        }

        var secondaryRolesJson = System.Text.Json.JsonSerializer.Serialize(secRolesList);
        var allRolesJson = System.Text.Json.JsonSerializer.Serialize(new[] { canonicalPrimaryRole }.Concat(secRolesList).Distinct());

        manifestProps[KeyPrimaryRole] = canonicalPrimaryRole;
        manifestProps[KeySecondaryRoles] = secondaryRolesJson;
        manifestProps[KeyAllRoles] = allRolesJson;
        manifestProps[KeyEntityKind] = (classification.Kind != ProjectEntityKind.Unknown ? classification.Kind.ToString() : role.ToString());
        if (classification.SubKind != ProjectEntitySubKind.None)
        {
            manifestProps[KeySubKind] = classification.SubKind.ToString();
        }

        var projectNode = new ProjectNode(id, projectName, relativeProjectDir, projectType, role.ToString(), isLibrary)
        {
            EntityKind = classification.Kind,
            SubKind = classification.SubKind,
            EntityType = entityType,
            PrimaryRole = canonicalPrimaryRole,
            SecondaryRoles = secondaryRolesJson,
            AllRoles = allRolesJson,
            Framework = manifestProps.GetValueOrDefault(KeyFramework),
            IsBuildOrchestrator = string.Equals(manifestProps.GetValueOrDefault(KeyIsBuildOrchestrator), BoolTrueStr, StringComparison.OrdinalIgnoreCase) ? true : null,
            ExcludedSubpaths = manifestProps.GetValueOrDefault(KeyExcludedSubpaths),
            AbsorbedSubpaths = manifestProps.GetValueOrDefault(KeyAbsorbedSubpaths)
        };

        if (manifestProps.TryGetValue(KeyArchetype, out var archStr) && !string.IsNullOrWhiteSpace(archStr))
        {
            projectNode.Archetype = TaxonomyEnumExtendedExtensions.ParseProjectArchetype(archStr);
        }

        if (manifestProps.TryGetValue(KeyCargoManifestDir, out var cargoDir))
        {
            projectNode.Signals().CargoManifestDir = cargoDir;
        }

        projectNode.Signals().ManifestHints = manifestProps;
        return projectNode;
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

    public static bool AreCompatibleEcosystems(ProjectLanguage parentType, ProjectLanguage childType)
    {
        if (parentType == ProjectLanguage.Unspecified || childType == ProjectLanguage.Unspecified) return false;
        if (parentType == childType) return true;
        if (parentType is ProjectLanguage.TypeScript or ProjectLanguage.JavaScript &&
            childType is ProjectLanguage.TypeScript or ProjectLanguage.JavaScript)
        {
            return true;
        }

        if (parentType is ProjectLanguage.Java or ProjectLanguage.Kotlin or ProjectLanguage.Scala &&
            childType is ProjectLanguage.Java or ProjectLanguage.Kotlin or ProjectLanguage.Scala)
        {
            return true;
        }

        return parentType is ProjectLanguage.C or ProjectLanguage.Cpp &&
               childType is ProjectLanguage.C or ProjectLanguage.Cpp;
    }
}
