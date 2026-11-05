# Proposal: Partial Layer & Pure In-Memory Scanning API (`WorkspaceIndexer.ScanInMemoryAsync`)

**Status:** Proposed (Design Approved)  
**Repository:** `code-explorer` (`CodeExplorer.Core`)  
**Related Proposals:** [`uast-completion-and-lightweight-flow-analysis-proposal.md`](./uast-completion-and-lightweight-flow-analysis-proposal.md)

---

## 1. Motivation & Problem Statement

Currently, [`WorkspaceIndexer`](../../cli/src/Core/CodeExplorer.Core/Analysis/Pipeline/WorkspaceIndexer.cs) exposes only a monolithic `IndexAsync(...)` pipeline that:
1. Always executes all layers up through `PostSyntacticPipelineHook` (Layers 1–5);
2. Requires an `IGraphClient` database and spawns a background `Channel<Func<Task>>` + `DatabasePersistenceWriter`;
3. Runs `SyncFileRegistryAsync`, `SetSchemaVersionAsync`, and `CreateIndicesAsync` at the end of every run.

Because `CodeExplorer.Core` lacked a first-class **partial layer & pure in-memory scanning API**, downstream consumers like **CodePassport** had to:
- Manually instantiate `InMemoryGraphRepository`, `Channel<Func<Task>>`, `ParsingContext`, `Layer1PhysicalParser`, `Layer2ProjectParser`, and `Layer3SyntacticParser`;
- Inject Passport-specific callback properties (`CustomLayer4Enricher`, `CustomLayer5Analyzer`, `PipelineState`) directly into `CodeExplorer.Core`'s [`ParsingContext`](../../cli/src/Core/CodeExplorer.Core/Analysis/Pipeline/ParsingContext.cs).

### Architectural Goal
`CodeExplorer.Core` must natively provide **configurable partial-layer scanning (`MaxLayer = Layer1..Layer5`)** and **zero-overhead pure in-memory execution (`PersistToGraphDb = false`)** directly on `WorkspaceIndexer`. Downstream tools (such as `CodePassport`, CLI quick-inspectors, or lightweight AST tools) simply invoke `WorkspaceIndexer.ScanInMemoryAsync(path, options, ct)`.

---

## 2. Public API Design in `CodeExplorer.Core`

### 2.1. `AnalysisLayer` & `WorkspaceScanOptions`

```csharp
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
```

### 2.2. `WorkspaceScanResult` & `LayerScanTimings`

```csharp
namespace CodeExplorer.Core.Parser;

public sealed record LayerScanTimings(
    long L1Ms,
    long L2Ms,
    long L3Ms,
    long L4Ms,
    long L5Ms,
    long TotalMs);

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
```

### 2.3. Entry Point on `WorkspaceIndexer`

```csharp
public partial class WorkspaceIndexer
{
    /// <summary>
    /// Executes a partial or full scan up to <see cref="WorkspaceScanOptions.MaxLayer"/> directly in memory
    /// without requiring an <see cref="IGraphClient"/> database instance.
    /// </summary>
    public static Task<WorkspaceScanResult> ScanInMemoryAsync(
        string workspacePath,
        WorkspaceScanOptions? options = null,
        CancellationToken cancellationToken = default,
        IProgress<IndexingProgress>? progress = null,
        ILogger? logger = null);
}
```

---

## 3. Step-by-Step Implementation Specification & Strict Invariants (Execution Guide)

> [!IMPORTANT]
> Follow the steps below in exact order. Do **not** rewrite entire files; use targeted surgical edits (`edit_file`) and verify with `dotnet build cli/CodeExplorer.slnx -c Release` (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` is enforced).

### Step 1: Add `AnalysisLayer`, `WorkspaceScanOptions`, `LayerScanTimings`, and `WorkspaceScanResult`
Create a new file `cli/src/Core/CodeExplorer.Core/Analysis/Pipeline/WorkspaceScanModels.cs` in namespace `CodeExplorer.Core.Parser` containing:
- `enum AnalysisLayer` (`Layer1_Physical = 1`, `Layer2_Boundaries = 2`, `Layer3_Syntactic = 3`, `Layer4_Semantic = 4`, `Layer5_Global = 5`).
- `sealed record WorkspaceScanOptions` (with properties `MaxLayer`, `PersistToGraphDb`, `LightweightAstMode`, `IncludeDistAndMinifiedArtifacts`, `ExcludedDirectories`, `FileExclusionPredicate`, `ProjectFileFilter`).
- `sealed record LayerScanTimings(long L1Ms, long L2Ms, long L3Ms, long L4Ms, long L5Ms, long TotalMs, int NodeCount = 0, int RelationshipCount = 0)`.
- `sealed class WorkspaceScanResult : IDisposable` holding `Layer1`, `Layer2`, `Layer3`, `Layer4`, `Layer5`, and `Timings`, and disposing `Layer3?.SyntaxTrees` in `Dispose()`.

### Step 2: Update `ParsingContext.cs` for `PersistToGraphDb = false` (No `NullGraphClient` Needed)
In [`cli/src/Core/CodeExplorer.Core/Analysis/Pipeline/ParsingContext.cs`](../../cli/src/Core/CodeExplorer.Core/Analysis/Pipeline/ParsingContext.cs):
1. Add property:
   ```csharp
   public bool PersistToGraphDb { get; init; } = true;
   ```
2. In `EnqueueUploadNodesAsync(List<Node> nodes)` (around line 258):
   ```csharp
   if (nodes.Count == 0) return;
   if (!PersistToGraphDb)
   {
       RecordNodesPersisted(nodes.Count);
       return;
   }
   ```
3. In `EnqueueUploadRelationshipsAsync(List<Relationship> rels)` (around line 277):
   ```csharp
   if (rels.Count == 0) return;
   if (!PersistToGraphDb)
   {
       RecordRelationshipsPersisted(rels.Count);
       return;
   }
   ```
4. In `WaitForQueueDrainedAsync()` (around line 295):
   ```csharp
   if (!PersistToGraphDb) return;
   ```
5. In `FlushScanIssuesAsync()` (around line 510):
   ```csharp
   if (!PersistToGraphDb || DbClient == null || ScanIssues.IsEmpty) return;
   ```
6. In the constructor `ParsingContext(...)` (around line 349), allow `dbClient` and `sharedChannel` to be `null!` when `PersistToGraphDb == false` without allocating `InMemoryGraphRepository` unless needed:
   ```csharp
   Repository = dbClient switch
   {
       IGraphRepository repo => repo,
       null => null!,
       _ => new PersistedGraphRepository(dbClient)
   };
   ```

### Step 3: Guard Direct `ctx.DbClient` Calls in `Layer1PhysicalAnalyzer.cs`
In [`cli/src/Core/CodeExplorer.Core/Analysis/Layers/Layer1_Physical/Layer1PhysicalAnalyzer.cs`](../../cli/src/Core/CodeExplorer.Core/Analysis/Layers/Layer1_Physical/Layer1PhysicalAnalyzer.cs) (lines 12–47), `Layer1PhysicalParser.ParseAsync` is the **only** place in Layers 1–3 that calls `ctx.DbClient` directly. Guard those 3 calls with `ctx.PersistToGraphDb && ctx.DbClient != null`:
```csharp
var wsId = ctx.PersistToGraphDb && ctx.DbClient != null
    ? await ctx.DbClient.GetOrCreateWorkspaceIdAsync(ctx.HostWorkspacePath)
    : "ws0";
ctx.WorkspaceId = wsId;
ctx.CancellationToken.ThrowIfCancellationRequested();

if (ctx.PersistToGraphDb && ctx.DbClient != null)
{
    await ctx.DbClient.SaveEmptyWorkspaceNodeAsync(wsId, ctx.HostWorkspacePath);
    ctx.CancellationToken.ThrowIfCancellationRequested();
}
```
And guard the `ctx.DbClient.ExecuteQueryAsync("MATCH (w:Workspace)...")` block (lines 25–47) with `if (ctx.PersistToGraphDb && ctx.DbClient != null)`.

### Step 4: Implement `WorkspaceIndexer.ScanInMemoryAsync`
In [`cli/src/Core/CodeExplorer.Core/Analysis/Pipeline/WorkspaceIndexer.cs`](../../cli/src/Core/CodeExplorer.Core/Analysis/Pipeline/WorkspaceIndexer.cs), add:
```csharp
public static async Task<WorkspaceScanResult> ScanInMemoryAsync(
    string workspacePath,
    WorkspaceScanOptions? options = null,
    CancellationToken cancellationToken = default,
    IProgress<IndexingProgress>? progress = null,
    ILogger? logger = null)
{
    options ??= new WorkspaceScanOptions();
    if (_fileParsers.Count == 0 || _projectParsers.Count == 0)
    {
        AutoRegisterParsers();
    }

    var fullDir = Path.GetFullPath(workspacePath).Replace('\\', '/');
    if (!Directory.Exists(fullDir))
    {
        throw new DirectoryNotFoundException($"Directory '{fullDir}' does not exist.");
    }

    var fileFilter = new WorkspaceFileFilter(
        _projectParsers,
        _fileParsers,
        options.ExcludedDirectories)
    {
        IncludeDistAndMinifiedArtifacts = options.IncludeDistAndMinifiedArtifacts
    };

    var ctx = new ParsingContext(
        absoluteWorkspacePath: fullDir,
        hostWorkspacePath: fullDir,
        dbClient: null!,
        sharedChannel: null!,
        clear: true,
        cancellationToken: cancellationToken,
        progress: progress,
        logger: logger)
    {
        PersistToGraphDb = false,
        LightweightAstScanMode = options.LightweightAstMode,
        CustomFileExclusionPredicate = options.FileExclusionPredicate,
        CustomProjectFileFilter = options.ProjectFileFilter,
        FileFilter = fileFilter
    };

    var totalSw = System.Diagnostics.Stopwatch.StartNew();
    var sw = System.Diagnostics.Stopwatch.StartNew();

    var l1Result = await new Layer1PhysicalParser().ParseAsync(ctx);
    var l1Ms = sw.ElapsedMilliseconds;

    Layer2Result? l2Result = null;
    long l2Ms = 0;
    if (options.MaxLayer >= AnalysisLayer.Layer2_Boundaries)
    {
        sw.Restart();
        l2Result = await new Layer2ProjectParser().ParseAsync(l1Result, ctx);
        l2Ms = sw.ElapsedMilliseconds;
    }

    Layer3Result? l3Result = null;
    long l3Ms = 0;
    if (options.MaxLayer >= AnalysisLayer.Layer3_Syntactic && l2Result != null)
    {
        sw.Restart();
        l3Result = await new Layer3SyntacticParser().ParseAsync(l2Result, ctx);
        l3Ms = sw.ElapsedMilliseconds;
    }

    totalSw.Stop();
    return new WorkspaceScanResult
    {
        Layer1 = l1Result,
        Layer2 = l2Result,
        Layer3 = l3Result,
        Timings = new LayerScanTimings(
            L1Ms: l1Ms,
            L2Ms: l2Ms,
            L3Ms: l3Ms,
            L4Ms: 0,
            L5Ms: 0,
            TotalMs: totalSw.ElapsedMilliseconds,
            NodeCount: ctx.TotalNodesCount,
            RelationshipCount: ctx.TotalRelsCount)
    };
}
```

### Step 5: Remove Legacy Passport Callbacks from `ParsingContext` & Layer 4/5
1. In [`ParsingContext.cs`](../../cli/src/Core/CodeExplorer.Core/Analysis/Pipeline/ParsingContext.cs), remove lines 29–31 (`CustomLayer4Enricher`, `CustomLayer5Analyzer`, `PipelineState`).
2. In [`Layer4SemanticAnalyzer.cs`](../../cli/src/Core/CodeExplorer.Analysis/Analysis/Layers/Layer4_Semantic/Layer4SemanticAnalyzer.cs) (lines 63–66), remove `if (ctx.CustomLayer4Enricher != null) ... else`.
3. In [`Layer5GlobalAnalyzer.cs`](../../cli/src/Core/CodeExplorer.Analysis/Analysis/Layers/Layer5_Global/Layer5GlobalAnalyzer.cs) (lines 53–57), remove `if (ctx.CustomLayer5Analyzer != null) ...`.
4. Verify build & tests:
   ```powershell
   dotnet build cli/CodeExplorer.slnx -c Release
   dotnet test cli/tests/CodeExplorer.Tests/CodeExplorer.Tests.csproj --filter "FullyQualifiedName~WorkspaceIndexer"
   ```
5. Pack local NuGet packages so `code-passport` can consume the updated `CodeExplorer.Core`:
   - Do **NOT** bump `<Version>` unless explicitly instructed by the user. Pack into the local feed used by `code-passport` (`dotnet pack cli/CodeExplorer.slnx -c Release -o ./cli/.Packages`).

---

## 4. Consumer Example (`CodePassport`)

With `WorkspaceIndexer.ScanInMemoryAsync`, `CodePassport`'s `PassportGenerator` no longer touches `Layer1PhysicalParser`, `Layer2ProjectParser`, `Layer3SyntacticParser`, `InMemoryGraphRepository`, or `Channel`:

```csharp
using var scan = await WorkspaceIndexer.ScanInMemoryAsync(
    fullDir,
    new WorkspaceScanOptions
    {
        MaxLayer = AnalysisLayer.Layer3_Syntactic,
        PersistToGraphDb = false,
        LightweightAstMode = true,
        IncludeDistAndMinifiedArtifacts = true,
        ExcludedDirectories = PassportExcludedDirectories,
        FileExclusionPredicate = IsPassportIgnoredFile,
        ProjectFileFilter = SelectPackageFiles
    },
    ct);

// Direct 4-Stage Capability & Provenance analysis over scan.Layer2 and scan.SyntaxTrees:
var passport = AnalyzePackage(scan.Layer2!, scan.SyntaxTrees, baselinePatterns);
```
