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
        Query = $"""
            MATCH (src:Function)-[:CALLS]->(tgt:Function)
            WHERE NOT coalesce(src.is_test, false)
              AND NOT tgt.name IN {AmbiguousCalleeNames}
              AND (
                (src.path CONTAINS 'Layer1_Physical' AND (tgt.path CONTAINS 'Layer2_Boundaries' OR tgt.path CONTAINS 'Layer3_Syntactic' OR tgt.path CONTAINS 'Layer4_Semantic' OR tgt.path CONTAINS 'Layer5_Global'))
                OR (src.path CONTAINS 'Layer2_Boundaries' AND (tgt.path CONTAINS 'Layer3_Syntactic' OR tgt.path CONTAINS 'Layer4_Semantic' OR tgt.path CONTAINS 'Layer5_Global'))
                OR (src.path CONTAINS 'Layer3_Syntactic' AND (tgt.path CONTAINS 'Layer4_Semantic' OR tgt.path CONTAINS 'Layer5_Global'))
                OR (src.path CONTAINS 'Layer4_Semantic' AND tgt.path CONTAINS 'Layer5_Global')
              )
            RETURN
              coalesce(src.path, src.file_path) AS file,
              coalesce(src.start_line, 0) AS line,
              src.name AS symbol,
              tgt.name AS target,
              'Pipeline stage violation: ' + src.name + ' calls ' + tgt.name + ' declared in a higher-order layer (' + coalesce(tgt.path, tgt.file_path) + ')' AS message
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
        Description = "Language parsers are independent plug-ins: they may depend on Core and Parser.Shared only. Core, Cypher and Parser.Shared must not depend on any parser or on the host application.",
        Remediation = "Move the shared code into CodeExplorer.Parser.Shared (or Core) and reference it from both parsers; never reference a sibling parser or the host.",
        Query = """
            MATCH (p:Project)-[:DEPENDS_ON]->(d:Project)
            WHERE d.name <> p.name
              AND (
                (p.name STARTS WITH 'CodeExplorer.Parser.' AND p.name <> 'CodeExplorer.Parser.Shared'
                  AND d.name STARTS WITH 'CodeExplorer.Parser.' AND d.name <> 'CodeExplorer.Parser.Shared')
                OR (p.name IN ['CodeExplorer.Core', 'CodeExplorer.Cypher', 'CodeExplorer.Parser.Shared']
                  AND (d.name STARTS WITH 'CodeExplorer.Parser.' OR d.name = 'CodeExplorer'))
                OR (p.name = 'CodeExplorer.Cypher' AND d.name STARTS WITH 'CodeExplorer')
              )
            RETURN
              coalesce(p.path, p.file_path, p.name) AS file,
              1 AS line,
              p.name AS symbol,
              d.name AS target,
              'Project dependency ' + p.name + ' -> ' + d.name + ' breaks the parser plug-in / Core-isolation contract' AS message
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
        Query = $"""
            MATCH (src:Function)-[:CALLS]->(tgt:Function)
            WHERE src.path CONTAINS 'CodeExplorer.Core/Analysis/Services/'
              AND (tgt.path CONTAINS 'CodeExplorer.Core/Analysis/Layers/' OR tgt.path CONTAINS 'CodeExplorer.Core/Analysis/Pipeline/')
              AND NOT tgt.name IN {AmbiguousCalleeNames}
              AND NOT coalesce(src.is_test, false)
            RETURN
              coalesce(src.path, src.file_path) AS file,
              coalesce(src.start_line, 0) AS line,
              src.name AS symbol,
              tgt.name AS target,
              'Query-side service ' + src.name + ' depends on indexing-pipeline code ' + tgt.name + ' (' + coalesce(tgt.path, tgt.file_path) + ')' AS message
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
        Query = """
            MATCH (caller:Function)-[:CALLS]->(parse:Function)
            WHERE parse.name IN ['Parse', 'ParseFile']
              AND parse.path CONTAINS 'Parser/Shared/SharedFileParserRegistry'
              AND NOT caller.path CONTAINS 'SharedFileEnricher'
              AND NOT caller.path CONTAINS 'Parser/Shared/'
              AND NOT coalesce(caller.is_test, false)
            RETURN
              coalesce(caller.path, caller.file_path) AS file,
              coalesce(caller.start_line, 0) AS line,
              caller.name AS symbol,
              parse.name AS target,
              caller.name + ' re-parses a file through the shared parser registry instead of consuming project enrichments' AS message
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = false
        },
        Source = "BuiltIn"
    };
}
