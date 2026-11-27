using CodeExplorer.Core.Gates.Models;

namespace CodeExplorer.Core.Gates.Storage;

/// <summary>
/// Gates that keep the layered pipeline, the parser plug-in model and the read/write split intact as the solution evolves.
/// Call edges are resolved by name, so method names that collide with common BCL members are excluded to avoid false positives.
/// </summary>
public static partial class BuiltInGateCatalog
{
    private const string AmbiguousCalleeNames =
        "['Push', 'Pop', 'Match', 'Select', 'Stop', 'Start', 'Clear', 'Add', 'Remove', 'Contains', 'Dispose', 'Close', 'Flush', 'Reset']";

    public static readonly SemanticGate PipelineLayerOrderGate = new()
    {
        Id = "SIG-P-B-001",
        Category = GateCategory.Architectural,
        Platform = "agnostic",
        Scope = InvariantScope.Module,
        Dimension = InvariantDimension.Boundaries,
        Name = "Pipeline Layer Strict Hierarchy",
        Severity = GateSeverity.Error,
        Description = "Enforces unidirectional pipeline flow: code in analysis layer N must not call into higher layers. Layers are identified by their folder (Layer1_Physical .. Layer5_Global).",
        Remediation = "Move the shared helper into a layer-neutral location (Core/Common or Core/Parser/Shared), or invert the dependency with an abstraction or pipeline-context data.",
        Message = "Pipeline stage violation: {src.name} calls {tgt.name} declared in a higher-order layer ({tgt.path})",
        Query = $"""
            MATCH (src:Function)-[r:CALLS]->(tgt:Function)
            WHERE NOT coalesce(src.is_test, false)
              AND NOT tgt.name IN {AmbiguousCalleeNames}
              AND (
                (src.path CONTAINS 'Layer1_Physical' AND (tgt.path CONTAINS 'Layer2_Boundaries' OR tgt.path CONTAINS 'Layer3_Syntactic' OR tgt.path CONTAINS 'Layer4_Semantic' OR tgt.path CONTAINS 'Layer5_Global'))
                OR (src.path CONTAINS 'Layer2_Boundaries' AND (tgt.path CONTAINS 'Layer3_Syntactic' OR tgt.path CONTAINS 'Layer4_Semantic' OR tgt.path CONTAINS 'Layer5_Global'))
                OR (src.path CONTAINS 'Layer3_Syntactic' AND (tgt.path CONTAINS 'Layer4_Semantic' OR tgt.path CONTAINS 'Layer5_Global'))
                OR (src.path CONTAINS 'Layer4_Semantic' AND tgt.path CONTAINS 'Layer5_Global')
              )
            RETURN src, r, tgt
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-P-B-001.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate ParserProjectIsolationGate = new()
    {
        Id = "SIG-P-B-007",
        Category = GateCategory.Architectural,
        Platform = "dotnet",
        Scope = InvariantScope.Module,
        Dimension = InvariantDimension.Boundaries,
        Name = "Parser Plug-in Project Isolation",
        Severity = GateSeverity.Error,
        Description = "Language parsers are independent plug-ins: they may depend on Core, TreeSitter, and Parsers.Shared only. Core, Cypher, TreeSitter, and Parsers.Shared must not depend on any parser or on the host application.",
        Remediation = "Move the shared code into CodeExplorer.Parsers.Shared (or Core) and reference it from both parsers; never reference a sibling parser or the host.",
        Message = "Project dependency {p.name} -> {d.name} breaks the parser plug-in / Core-isolation contract",
        Query = """
            MATCH (p:Project)-[r:DEPENDS_ON]->(d:Project)
            WHERE d.name <> p.name
              AND (
                (p.name STARTS WITH 'CodeExplorer.Parsers.' AND p.name <> 'CodeExplorer.Parsers.Shared'
                  AND d.name STARTS WITH 'CodeExplorer.Parsers.' AND d.name <> 'CodeExplorer.Parsers.Shared')
                OR (p.name IN ['CodeExplorer.Core', 'CodeExplorer.Cypher', 'CodeExplorer.TreeSitter', 'CodeExplorer.Parsers.Shared']
                  AND ((d.name STARTS WITH 'CodeExplorer.Parsers.' AND d.name <> 'CodeExplorer.Parsers.Shared') OR d.name = 'CodeExplorer'))
                OR (p.name IN ['CodeExplorer.Cypher', 'CodeExplorer.TreeSitter'] AND d.name STARTS WITH 'CodeExplorer')
                OR (p.name = 'CodeExplorer.Core' AND d.name IN ['CodeExplorer.Storage', 'CodeExplorer.Indexing', 'CodeExplorer.Analysis', 'CodeExplorer.Gates', 'CodeExplorer.Server'])
                OR (p.name IN ['CodeExplorer.Storage', 'CodeExplorer.Indexing', 'CodeExplorer.Gates'] AND d.name IN ['CodeExplorer.Analysis', 'CodeExplorer.Server', 'CodeExplorer'])
                OR (p.name = 'CodeExplorer.Indexing' AND d.name = 'CodeExplorer.Storage')
              )
            RETURN p, r, d
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-P-B-007.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate QuerySideIndexingSeparationGate = new()
    {
        Id = "SIG-P-B-008",
        Category = GateCategory.Architectural,
        Platform = "dotnet",
        Scope = InvariantScope.Module,
        Dimension = InvariantDimension.Boundaries,
        Name = "Query-Side / Indexing-Pipeline Separation",
        Severity = GateSeverity.Warning,
        Description = "Read-model services (Analysis/Services) answer questions from the persisted graph and must not call into the indexing pipeline (Analysis/Layers, Analysis/Pipeline). Anything both sides need belongs in a neutral module.",
        Remediation = "Extract the shared helper (naming, canonicalisation, classification) into Core/Common and call it from both sides, or persist the derived value in the graph during indexing.",
        Message = "Query-side service {src.name} depends on indexing-pipeline code {tgt.name} ({tgt.path})",
        Query = $"""
            MATCH (src:Function)-[r:CALLS]->(tgt:Function)
            WHERE src.path CONTAINS 'Analysis/Services/'
              AND (tgt.path CONTAINS 'Analysis/Layers/' OR tgt.path CONTAINS 'Analysis/Pipeline/')
              AND NOT tgt.name IN {AmbiguousCalleeNames}
              AND NOT coalesce(src.is_test, false)
            RETURN src, r, tgt
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-P-B-008.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate SharedFileSingleReadGate = new()
    {
        Id = "SIG-P-B-009",
        Category = GateCategory.Architectural,
        Platform = "dotnet",
        Scope = InvariantScope.Module,
        Dimension = InvariantDimension.Rules,
        Name = "Shared File Parsers: Read Once",
        Severity = GateSeverity.Error,
        Description = "Non-source files (.env, appsettings, yaml, properties, ...) are read and parsed exactly once, by SharedFileEnricher in Layer 2, and attached to the project as enrichments. Later layers consume the enrichments and must not invoke shared file parsers again.",
        Remediation = "Read the data from ProjectNode.Enrichments / ConfigurationIndex instead of calling SharedFileParserRegistry.Parse or ParseFile.",
        Message = "{caller.name} re-parses a file through the shared parser registry instead of consuming project enrichments",
        Query = """
            MATCH (caller:Function)-[r:CALLS]->(parse:Function)
            WHERE parse.name IN ['Parse', 'ParseFile']
              AND parse.path CONTAINS 'Parser/Shared/SharedFileParserRegistry'
              AND NOT caller.path CONTAINS 'SharedFileEnricher'
              AND NOT caller.path CONTAINS 'Parser/Shared/'
              AND NOT coalesce(caller.is_test, false)
            RETURN caller, r, parse
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = false
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate NoFileReadAboveLayer3Gate = new()
    {
        Id = "SIG-P-B-011",
        Category = GateCategory.Architectural,
        Platform = "agnostic",
        Scope = InvariantScope.Module,
        Dimension = InvariantDimension.Boundaries,
        Name = "Pipeline IO Boundary: No File Reads Above Layer 3",
        Severity = GateSeverity.Error,
        Description = "All source, manifest, and configuration files must be read and parsed in Layers 1-3 (Physical, Boundaries, Syntactic). Layers 4 (Semantic) and 5 (Global / PostIndex) must operate strictly on the in-memory ontology graph, AST, and enrichments without reading files or enumerating directories.",
        Remediation = "Extract the required metadata, symbols, or enrichments in Layer 1, Layer 2, or Layer 3 and attach them to FileNode, ProjectNode, SyntaxTree, or the layer context instead of reading files in Layer 4 or Layer 5.",
        Message = "Layer 4/5 function '{caller.name}' performs prohibited file/store read via '{target.name}' ({target.path})",
        Query = """
            MATCH (caller:Function)-[r:CALLS]->(target:Function)
            WHERE (caller.path CONTAINS 'Layer4_' OR caller.path CONTAINS 'Layer5_')
              AND NOT coalesce(caller.is_test, false)
              AND (
                target.path CONTAINS 'FileStreamReader'
                OR target.path CONTAINS 'PhysicalFileStore'
                OR target.path CONTAINS 'WorkspaceFileFilter'
                OR target.path CONTAINS 'SharedFileParserRegistry'
                OR target.name IN [
                  'ReadTextOnce', 'ReadTextOnceAsync', 'ReadLinesOnce',
                  'ReadAllText', 'ReadAllTextAsync',
                  'ReadAllBytes', 'ReadAllBytesAsync',
                  'ReadAllLines', 'ReadAllLinesAsync',
                  'ReadLines', 'OpenRead', 'ReadSource',
                  'ScanSourceTextForSchema',
                  'StreamAndAnalyzeFileAsync',
                  'EnumerateFiles', 'EnumerateCandidateFiles',
                  'GetFiles', 'ParseFile'
                ]
              )
            RETURN caller, r, target
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = false
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate ProjectManifestSinglePassOwnershipGate = new()
    {
        Id = "SIG-P-B-013",
        Category = GateCategory.Architectural,
        Platform = "agnostic",
        Scope = InvariantScope.Module,
        Dimension = InvariantDimension.Boundaries,
        Name = "Project Manifest Single-Pass Ownership",
        Severity = GateSeverity.Error,
        Description = "Project manifests must be read and parsed once inside the owning IProjectParser into ParsedProjectDescriptor and ParsedProjectManifestMetadata. Layer 2 boundary orchestration and classifiers (Layer2_Boundaries) must consume ParsedProjectDescriptor, ProjectNode, and SharedFileEnricher enrichments without reading files directly.",
        Remediation = "Extract the manifest data inside IProjectParser.TryParse / InspectDirectoryManifestMetadata into ParsedProjectDescriptor or ParsedProjectManifestMetadata and read it from ProjectNode in Layer 2.",
        Message = "Layer 2 function '{caller.name}' reads files directly via '{target.name}' ({target.path}) instead of consuming ParsedProjectDescriptor",
        Query = """
            MATCH (caller:Function)-[r:CALLS]->(target:Function)
            WHERE caller.path CONTAINS 'Layer2_Boundaries'
              AND NOT caller.path CONTAINS 'SharedFileEnricher'
              AND NOT coalesce(caller.is_test, false)
              AND (
                target.path CONTAINS 'FileStreamReader'
                OR target.path CONTAINS 'PhysicalFileStore'
                OR target.name IN [
                  'ReadTextOnce', 'ReadTextOnceAsync', 'ReadLinesOnce',
                  'ReadAllText', 'ReadAllTextAsync',
                  'ReadAllBytes', 'ReadAllBytesAsync',
                  'ReadAllLines', 'ReadAllLinesAsync',
                  'ReadLines', 'OpenRead',
                  'StreamAndAnalyzeFileAsync'
                ]
              )
            RETURN caller, r, target
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = false
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate PerProjectAstEvictionGate = new()
    {
        Id = "SIG-P-B-014",
        Category = GateCategory.Architectural,
        Platform = "agnostic",
        Scope = InvariantScope.Module,
        Dimension = InvariantDimension.Boundaries,
        Name = "Per-Project AST & Scope Body Eviction Boundary",
        Severity = GateSeverity.Error,
        Description = "Project syntax trees (SyntaxTree) and intra-function control-flow scopes (ScopeNode) must be streamed to storage and evicted upon completing each project. Layer 5 global resolution and post-indexing must consume flat ProjectScanSummary stubs and symbol headers without referencing SyntaxTree or ScopeNode.",
        Remediation = "Extract any required cross-project metadata into ProjectScanSummary, AddressableSymbolStub, or FunctionFlowSummary during the per-project pass instead of retaining SyntaxTree or ScopeNode into Layer 5.",
        Message = "Layer 5 component '{src.name}' ({src.path}) illegally references per-project AST/body type '{tgt.name}'",
        Query = """
            MATCH (src)-[r:USES_TYPE|CALLS]->(tgt:Type)
            WHERE (src.path CONTAINS 'Layer5_Global' OR src.file_path CONTAINS 'Layer5_Global')
              AND NOT coalesce(src.is_test, false)
              AND tgt.name IN ['SyntaxTree', 'ScopeNode', 'SyntaxDocument']
            RETURN src, r, tgt
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = false
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate ModuleCycleProhibitionGate = new()
    {
        Id = "SIG-C-B-004",
        Category = GateCategory.Architectural,
        Platform = "agnostic",
        Scope = InvariantScope.Module,
        Dimension = InvariantDimension.Boundaries,
        Name = "Module Dependency Cycle Prohibition",
        Severity = GateSeverity.Error,
        Description = "Enforces acyclic architecture: projects and modules must not form cyclic dependency loops (A -> B -> A).",
        Remediation = "Break the circular dependency by extracting shared interfaces into a shared library or using dependency inversion.",
        Message = "Cyclic dependency detected between project '{p1.name}' and '{p2.name}'",
        Query = """
            MATCH (p1:Project)-[r1:DEPENDS_ON]->(p2:Project)-[r2:DEPENDS_ON]->(p1:Project)
            WHERE p1.id < p2.id
              AND NOT coalesce(p1.is_test, false)
              AND NOT coalesce(p2.is_test, false)
            RETURN p1, r1, p2, r2
            """,
            Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-C-B-004.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate NoLinearScansInLoopsGate = new()
    {
        Id = "SIG-M-R-003",
        Category = GateCategory.Architectural,
        Platform = "agnostic",
        Scope = InvariantScope.Method,
        Dimension = InvariantDimension.Resources,
        Name = "Disallow Linear Collection Scans Inside Loops",
        Severity = GateSeverity.Error,
        Description = "Methods must not execute O(N) linear collection scans (e.g. FirstOrDefault, Any, Where, SingleOrDefault, Find, RemoveAll, or List.Contains) inside non-bounded loops, which turns O(N) iterations into hidden O(N^2) bottlenecks.",
        Remediation = "Pre-index the searched collection into a HashSet or Dictionary outside the loop and perform O(1) Contains / TryGetValue lookups inside the loop.",
        Target = "{f.loop_linear_scan_count} linear scan(s) in loop",
        Message = "Method '{f.name}' executes {f.loop_linear_scan_count} linear collection scan(s) inside a loop (effective_loop_depth = {f.effective_loop_depth}). Pre-index into a HashSet/Dictionary outside the loop.",
        Query = """
            MATCH (f:Function)
            WHERE NOT coalesce(f.is_test, false)
              AND f.name <> '<module_init>'
              AND coalesce(f.loop_linear_scan_count, 0) > 0
            RETURN f
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-M-R-003.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate InterProceduralPolynomialCeilingGate = new()
    {
        Id = "SIG-M-R-004",
        Category = GateCategory.Architectural,
        Platform = "agnostic",
        Scope = InvariantScope.Method,
        Dimension = InvariantDimension.Resources,
        Name = "Inter-Procedural Polynomial Complexity Ceiling",
        Severity = GateSeverity.Error,
        Description = "Functions invoked inside caller loops must not iterate directly over unpartitioned lookup dictionaries or workspace-wide collections (has_unpartitioned_loop = true) or execute nested linear collection scans (loop_linear_scan_count > 0).",
        Remediation = "Build workspace/lookup indices once before the outer loop, or partition data by key (Dictionary/TryGetValue) instead of scanning full collections inside helper methods called from loops.",
        Message = "Call '{caller.name}' -> '{callee.name}' amplifies loop complexity across call boundaries (transitive_loop_depth = {callee.transitive_loop_depth}, has_unpartitioned_loop = {callee.has_unpartitioned_loop}, loop_linear_scan_count = {callee.loop_linear_scan_count}).",
        Query = """
            MATCH (caller:Function)-[r:CALLS]->(callee:Function)
            WHERE NOT coalesce(caller.is_test, false)
              AND NOT coalesce(callee.is_test, false)
              AND caller.id <> callee.id
              AND coalesce(callee.caller_loop_depth, 0) >= 1
              AND (coalesce(r.loop_depth, 0) >= 1 OR coalesce(caller.caller_loop_depth, 0) >= 1)
              AND (
                coalesce(callee.has_unpartitioned_loop, false) = true
                OR coalesce(callee.loop_linear_scan_count, 0) > 0
              )
            RETURN caller, r, callee
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-M-R-004.json"
        },
        Source = "BuiltIn"
    };
}
