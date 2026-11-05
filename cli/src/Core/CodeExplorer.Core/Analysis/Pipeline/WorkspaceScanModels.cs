using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Specifies the target ontology layer up to which the scanner should execute.
/// </summary>
public enum AnalysisLayer
{
    /// <summary>Physical files, directories, content hashes, and ArtifactRole (source/dist/minified/script).</summary>
    Layer1_Physical = 1,

    /// <summary>Project/package manifests (package.json, .csproj, pyproject.toml, .nuspec), dependencies, and EntryPoints/lifecycle hooks.</summary>
    Layer2_Boundaries = 2,

    /// <summary>Polyglot Tree-sitter UAST SyntaxTrees, SyntaxDocuments, RawImports, and constant folding.</summary>
    Layer3_Syntactic = 3,

    /// <summary>Semantic workloads, services, datastores, and library projections.</summary>
    Layer4_Semantic = 4,

    /// <summary>Global cross-references, late binding, and post-index graph reconciliation.</summary>
    Layer5_Global = 5
}

public sealed record WorkspaceScanOptions
{
    /// <summary>Highest layer to execute (inclusive). Defaults to Layer3_Syntactic for in-memory scans.</summary>
    public AnalysisLayer MaxLayer { get; init; } = AnalysisLayer.Layer3_Syntactic;

    /// <summary>
    /// When false, disables all IGraphClient / Channel / FileRegistry persistence.
    /// EnqueueUploadNodesAsync and EnqueueUploadRelationshipsAsync become zero-allocation no-ops.
    /// </summary>
    public bool PersistToGraphDb { get; init; } = false;

    /// <summary>
    /// Enables lightweight AST scanning in Layer 3 (skips heavy cross-project symbol tables and component library parsers).
    /// </summary>
    public bool LightweightAstMode { get; init; } = false;

    /// <summary>Includes dist/, build/, and .min.js artifacts during Layer 1 discovery.</summary>
    public bool IncludeDistAndMinifiedArtifacts { get; init; } = false;

    /// <summary>Optional custom directory names to exclude during Layer 1 physical scanning.</summary>
    public IReadOnlyList<string>? ExcludedDirectories { get; init; }

    /// <summary>Optional file exclusion predicate (fullPath, relativePath) => shouldExclude.</summary>
    public Func<string, string, bool>? FileExclusionPredicate { get; init; }

    /// <summary>Optional per-project file selector for Layer 3 AST parsing.</summary>
    public Func<IReadOnlyList<FileNode>, ProjectNode, Layer2Result, IReadOnlyList<FileNode>>? ProjectFileFilter { get; init; }
}

public sealed record LayerScanTimings(
    long L1Ms,
    long L2Ms,
    long L3Ms,
    long L4Ms,
    long L5Ms,
    long TotalMs,
    int NodeCount = 0,
    int RelationshipCount = 0);

/// <summary>
/// Holds the in-memory results of a partial or full workspace scan and disposes native Tree-sitter SyntaxTrees.
/// </summary>
public sealed class WorkspaceScanResult : IDisposable
{
    public required Layer1Result Layer1 { get; init; }
    public Layer2Result? Layer2 { get; init; }
    public Layer3Result? Layer3 { get; init; }
    public Layer4Result? Layer4 { get; init; }
    public Layer5Result? Layer5 { get; init; }
    public required LayerScanTimings Timings { get; init; }

    public IReadOnlyList<FileNode> Files => Layer1.Files;
    public IReadOnlyList<ProjectNode> Projects => Layer2?.Projects ?? [];
    public IReadOnlyList<PackageNode> Packages => Layer2?.Packages ?? [];
    public IReadOnlyList<SyntaxTree> SyntaxTrees => Layer3?.SyntaxTrees ?? [];

    public void Dispose()
    {
        if (Layer3?.SyntaxTrees is { } trees)
        {
            foreach (var tree in trees)
            {
                tree.Dispose();
            }
        }
    }
}
