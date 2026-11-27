using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser.Layers;

public record Layer2Result(
    ProjectsStructureNode ProjectsStructure,
    List<ProjectNode> Projects,
    List<PackageNode> Packages,
    List<Relationship> ProjectDependencies,
    List<IProjectEnrichment>? WorkspaceEnrichments = null,
    Dictionary<string, string>? TsConfigPaths = null,
    Dictionary<string, string>? WorkspaceGoModules = null,
    Dictionary<string, (string Engine, string Family)>? DirectoryManifestDrivers = null);
