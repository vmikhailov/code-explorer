using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Database;

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
        Dictionary<string, string>? manifestProps = null,
        ParsedProjectManifestMetadata? manifestMetadata = null)
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
                Common.ProjectRole.FrontendApp => "app",
                Common.ProjectRole.Worker => "worker",
                Common.ProjectRole.CliTool or Common.ProjectRole.ScriptProject or Common.ProjectRole.DatabaseMigration => "cli",
                Common.ProjectRole.Test => "test",
                Common.ProjectRole.SharedLibrary => "library",
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
            foreach (var item in CypherValue.TryParseJsonText(secJson).AsStringList())
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

        if (manifestMetadata != null)
        {
            ApplyManifestMetadataToProject(projectNode, manifestProps, manifestMetadata);
        }

        projectNode.Signals().ManifestHints = manifestProps;
        return projectNode;
    }

    private static void ApplyManifestMetadataToProject(
        ProjectNode project,
        Dictionary<string, string> hints,
        ParsedProjectManifestMetadata meta)
    {
        project.Signals().ManifestMetadata = meta;

        if (!string.IsNullOrWhiteSpace(meta.PackageName))
        {
            project.PackageName = meta.PackageName;
            hints[OntologyConstants.ExtensionKeys.PackageName] = meta.PackageName;
        }
        if (!string.IsNullOrWhiteSpace(meta.PackageVersion))
        {
            project.PackageVersion = meta.PackageVersion;
            hints[OntologyConstants.ExtensionKeys.PackageVersion] = meta.PackageVersion;
        }
        if (!string.IsNullOrWhiteSpace(meta.Publisher))
        {
            project.Publisher = meta.Publisher;
            hints[OntologyConstants.ExtensionKeys.Publisher] = meta.Publisher;
        }
        if (!string.IsNullOrWhiteSpace(meta.MainFile))
        {
            project.MainFile = meta.MainFile;
            hints[OntologyConstants.ExtensionKeys.MainFile] = meta.MainFile;
        }
        if (!string.IsNullOrWhiteSpace(meta.ModuleFile))
        {
            project.ModuleFile = meta.ModuleFile;
            hints[OntologyConstants.ExtensionKeys.ModuleFile] = meta.ModuleFile;
        }
        if (!string.IsNullOrWhiteSpace(meta.Description))
        {
            project.Description = meta.Description;
            hints[OntologyConstants.ExtensionKeys.Description] = meta.Description;
        }
        if (meta.HasBin)
        {
            project.HasBin = true;
            hints[OntologyConstants.ExtensionKeys.HasBin] = OntologyConstants.ExtensionKeys.TrueValue;
        }
        if (meta.HasContributes)
        {
            project.HasContributes = true;
            hints[OntologyConstants.ExtensionKeys.HasContributes] = OntologyConstants.ExtensionKeys.TrueValue;
        }
        if (meta.HasVsCodeEngine)
        {
            project.HasVsCodeEngine = true;
            hints[OntologyConstants.ExtensionKeys.HasVsCodeEngine] = OntologyConstants.ExtensionKeys.TrueValue;
        }
        if (!string.IsNullOrWhiteSpace(meta.KeywordsJson))
        {
            project.KeywordsJson = meta.KeywordsJson;
            hints[OntologyConstants.ExtensionKeys.KeywordsJson] = meta.KeywordsJson;
        }
        if (!string.IsNullOrWhiteSpace(meta.ExtensionDependenciesJson))
        {
            project.ExtensionDependenciesJson = meta.ExtensionDependenciesJson;
            hints["extension_dependencies_json"] = meta.ExtensionDependenciesJson;
        }
        if (!string.IsNullOrWhiteSpace(meta.ActivationEvents))
        {
            project.ActivationEvents = meta.ActivationEvents;
            hints["activation_events"] = meta.ActivationEvents;
        }
        if (meta.FriendAssemblies is { Count: > 0 })
        {
            var friends = project.Signals().FriendAssemblies ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < meta.FriendAssemblies.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(meta.FriendAssemblies[i]))
                {
                    friends.Add(meta.FriendAssemblies[i]);
                }
            }
        }
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

    private static readonly HashSet<string> GenericPackageOrModuleShortNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "core", "common", "commons", "src", "source", "lib", "libs", "library",
        "app", "apps", "application", "api", "apis", "service", "services",
        "server", "client", "clients", "utils", "util", "shared", "model", "models",
        "dto", "dtos", "domain", "data", "db", "database", "main", "root",
        "web", "ui", "frontend", "backend", "worker", "workers", "cli",
        "tools", "tool", "scripts", "script", "config", "configuration",
        "pkg", "internal", "base", "sdk", "spi", "test", "tests", "testing",
        "unit-test", "integration-test", "spec", "specs", "docs"
    };

    public static bool IsGenericPackageShortName(string? name) =>
        string.IsNullOrWhiteSpace(name) || GenericPackageOrModuleShortNames.Contains(name.Trim());

    public static string GetCanonicalPackageEcosystem(ProjectLanguage lang) => lang switch
    {
        ProjectLanguage.TypeScript or ProjectLanguage.JavaScript => "npm",
        ProjectLanguage.Java or ProjectLanguage.Kotlin or ProjectLanguage.Scala => "jvm",
        ProjectLanguage.Python => "pypi",
        ProjectLanguage.CSharp => "nuget",
        ProjectLanguage.Go => "go",
        ProjectLanguage.Rust => "cargo",
        ProjectLanguage.Php => "composer",
        ProjectLanguage.Perl => "cpan",
        ProjectLanguage.Ruby => "rubygems",
        ProjectLanguage.Terraform => "terraform",
        ProjectLanguage.C or ProjectLanguage.Cpp => "native",
        ProjectLanguage.Swift => "swiftpm",
        _ => lang.ToCanonicalString()
    };

    public static string NormalizePackageEcosystem(string? rawEcosystem, ProjectLanguage fallbackLang)
    {
        if (!string.IsNullOrWhiteSpace(rawEcosystem))
        {
            var lower = rawEcosystem.Trim().ToLowerInvariant();
            return lower switch
            {
                "npm" or "yarn" or "pnpm" or "node" or "typescript" or "javascript" => "npm",
                "maven" or "gradle" or "jvm" or "java" or "kotlin" or "scala" or "groovy" => "jvm",
                "pip" or "pypi" or "poetry" or "uv" or "conda" or "python" => "pypi",
                "nuget" or "dotnet" or "csharp" => "nuget",
                "go" or "gomod" => "go",
                "cargo" or "crates" or "rust" => "cargo",
                "composer" or "packagist" or "php" => "composer",
                "cpan" or "perl" => "cpan",
                "gem" or "rubygems" or "bundler" or "ruby" => "rubygems",
                "terraform" or "hcl" or "opentofu" => "terraform",
                _ => lower
            };
        }
        return GetCanonicalPackageEcosystem(fallbackLang);
    }

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
