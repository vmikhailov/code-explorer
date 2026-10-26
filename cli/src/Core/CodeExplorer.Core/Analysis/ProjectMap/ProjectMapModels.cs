namespace CodeExplorer.Core.Analysis.ProjectMap;

/// <summary>
/// A source file belonging to the selected project (workspace-relative path, forward slashes).
/// </summary>
public sealed record ProjectMapFile(string Path, bool IsTest, int? LineCount = null);

/// <summary>
/// A symbol-level relationship between two files in the project (e.g. CALLS, IMPLEMENTS).
/// </summary>
public sealed record ProjectMapSymbolEdge(
    string FromFile,
    string ToFile,
    string Kind,
    string? FromSymbol = null,
    string? ToSymbol = null,
    int? FromLine = null);

/// <summary>
/// A link between a symbol declared in a project file and a Layer 4 resource node (Table, Topic, ExternalService, ...).
/// </summary>
public sealed record ProjectMapResourceLink(
    string File,
    string ResourceId,
    string ResourceKind,
    string ResourceName,
    string Kind,
    string? Symbol = null,
    int? Line = null);

/// <summary>
/// A nested child project (SubProject) contained inside a parent project.
/// </summary>
public sealed record ProjectMapSubProject(
    string ProjectId,
    string Name,
    string Path,
    string Kind,
    IReadOnlyList<ProjectMapFile> Files);

/// <summary>
/// An Endpoint or EntryPoint node exposed by the project (Orbit 0).
/// </summary>
public sealed record ProjectMapEntryPointNode(
    string Id,
    string Name,
    string Kind,
    string FilePath,
    int? Line = null,
    string? SubKind = null,
    IReadOnlyList<string>? TargetSymbols = null,
    IReadOnlyList<string>? TargetFiles = null);

/// <summary>
/// A method/function belonging to a ProjectMap module or type.
/// </summary>
public sealed record ProjectMapMethod(string Name, string? FilePath = null, int? Line = null)
{
    public static implicit operator ProjectMapMethod(string name) => new(name);
}

/// <summary>
/// A Type declaration (class, interface, struct, record, enum) in the project.
/// </summary>
public sealed record ProjectMapTypeNode(
    string Id,
    string Name,
    string SubKind,
    string FilePath,
    int? StartLine = null,
    int? LineCount = null,
    bool IsTest = false,
    IReadOnlyList<ProjectMapMethod>? Methods = null);

/// <summary>
/// A Function/Method declaration in the project.
/// </summary>
public sealed record ProjectMapFunctionNode(
    string Id,
    string Name,
    string FilePath,
    int? StartLine = null,
    int? LineCount = null,
    bool IsTest = false,
    string? EnclosingTypeName = null);

/// <summary>
/// A type-level or symbol-level relationship (CALLS, USES_TYPE, IMPLEMENTS, INHERITS_FROM) resolved to enclosing types/modules.
/// </summary>
public sealed record ProjectMapTypeEdge(
    string FromSymbol,
    string FromFile,
    string ToSymbol,
    string ToFile,
    string Kind,
    int? Line = null,
    bool IsExternalCaller = false);

/// <summary>
/// Raw, already-scoped inputs for a single project. Produced by the view engine from existing graph nodes/edges.
/// </summary>
public sealed class ProjectMapInput
{
    public string ProjectId { get; init; } = "";
    public string ProjectName { get; init; } = "";
    public string ProjectPath { get; init; } = "";
    public bool IsBuildOrchestrator { get; init; }
    public IReadOnlyList<string> ExcludedSubpaths { get; init; } = [];
    public IReadOnlyList<ProjectMapSubProject> SubProjects { get; init; } = [];
    public IReadOnlyList<ProjectMapFile> Files { get; init; } = [];
    public IReadOnlyList<ProjectMapSymbolEdge> SymbolEdges { get; init; } = [];
    public IReadOnlyList<ProjectMapResourceLink> ResourceLinks { get; init; } = [];

    /// <summary>Number of Endpoint/EntryPoint nodes that trigger or are exposed by symbols in each file.</summary>
    public IReadOnlyDictionary<string, int> EntryPointsByFile { get; init; } = new Dictionary<string, int>();

    public IReadOnlyList<ProjectMapEntryPointNode> EntryPointNodes { get; init; } = [];
    public IReadOnlyList<ProjectMapTypeNode> Types { get; init; } = [];
    public IReadOnlyList<ProjectMapFunctionNode> Functions { get; init; } = [];
    public IReadOnlyList<ProjectMapTypeEdge> TypeEdges { get; init; } = [];
}

public enum ProjectMapGranularity
{
    Module,
    SubModule,
    File,
    Class
}

/// <summary>
/// Ring classification. Values double as the orbit index (0 = center).
/// </summary>
public enum ProjectMapTier
{
    Ingress = 0,
    Application = 1,
    Core = 2,
    Data = 3,
    Foundation = 4,
    Resource = 5,
    Database = 6
}

public sealed record ProjectMapEvidenceSample(string? FromSymbol, string? ToSymbol, string File, int? Line, string Kind);

public sealed class ProjectMapModule
{
    public required string Id { get; init; }
    public required string Name { get; set; }

    /// <summary>Workspace-relative folder (or file) path backing this module.</summary>
    public required string Path { get; init; }

    public string? NodeKindOverride { get; init; }
    public string? PmType { get; init; }
    public string? SubKind { get; init; }
    public int? StartLine { get; set; }
    public int? BfsDepth { get; set; }
    public List<ProjectMapMethod> Methods { get; } = [];
    public List<string> ImplementedInterfaces { get; } = [];
    public bool IsRootFiles { get; init; }
    public bool IsMerged { get; init; }
    public bool IsSubProject { get; init; }
    public string? SubProjectId { get; init; }
    public string? SubProjectKind { get; init; }
    public List<string> Files { get; } = [];
    public int LineCount { get; set; }
    public int EntryPointCount { get; set; }
    public int FanIn { get; set; }
    public int FanOut { get; set; }
    public double Instability { get; set; }
    public bool TouchesData { get; set; }
    public int DataFileCount { get; set; }
    public ProjectMapTier Tier { get; set; } = ProjectMapTier.Core;
    public string TierReason { get; set; } = "";
    public int? CycleGroup { get; set; }
}

public sealed class ProjectMapResource
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    public required string Name { get; init; }
    public ProjectMapTier Tier { get; init; } = ProjectMapTier.Resource;
}

public sealed class ProjectMapEdge
{
    public required string SourceId { get; init; }
    public required string TargetId { get; init; }
    public int Count { get; set; }
    public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> Kinds { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ProjectMapEvidenceSample> Samples { get; } = [];
    public bool IsResourceLink { get; init; }
    public bool IsCircular { get; set; }
}

public sealed class ProjectMapResult
{
    public required ProjectMapInput Input { get; init; }
    public string SourceRoot { get; set; } = "";
    public ProjectMapGranularity Granularity { get; set; } = ProjectMapGranularity.Module;
    public List<ProjectMapModule> Modules { get; } = [];
    public List<ProjectMapResource> Resources { get; } = [];
    public List<ProjectMapEdge> Edges { get; } = [];

    /// <summary>Strongly connected components with two or more modules.</summary>
    public List<List<string>> Cycles { get; } = [];

    public int TestFilesExcluded { get; set; }
}
