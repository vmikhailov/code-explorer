using CodeExplorer.Core.Gates.Models;

namespace CodeExplorer.Core.Gates.Storage;

/// <summary>
/// Default catalog of standard architectural Semantic Invariant Gates shipped with CodeExplorer.
/// </summary>
public static class BuiltInGateCatalog
{
    public static IReadOnlyList<SemanticGate> GetDefaultGates() =>
    [
        new SemanticGate
        {
            Id = "SIG-P-B-001",
            Category = GateCategory.Architectural,
            Platform = "agnostic",
            Scope = InvariantScope.Module,
            Dimension = InvariantDimension.Boundaries,
            Name = "Pipeline Layer Strict Hierarchy",
            Severity = GateSeverity.Error,
            Description = "Enforces unidirectional pipeline flow: parser stage N must not depend on or call higher-order parser stages.",
            Remediation = "Remove references to higher pipeline layers. Use decoupled events, abstractions, or move shared data structures into Core/Common.",
            Query = """
                MATCH (src)-[:CALLS|USES_TYPE]->(tgt)
                WHERE NOT coalesce(src.is_test, false)
                  AND src.stage_order IS NOT NULL
                  AND tgt.stage_order IS NOT NULL
                  AND src.stage_order < tgt.stage_order
                RETURN 
                  coalesce(src.symbol, src.file_path) AS file,
                  coalesce(src.start_line, 0) AS line,
                  src.name AS symbol,
                  tgt.name AS target,
                  'Pipeline stage violation: component depends on higher-order pipeline layer' AS message
                """,
            Ratchet = new GateRatchetSettings
            {
                Enabled = true,
                Baseline = "baselines/SIG-P-B-001.json"
            },
            Source = "BuiltIn"
        },

        new SemanticGate
        {
            Id = "SIG-P-B-002",
            Category = GateCategory.Architectural,
            Platform = "agnostic",
            Scope = InvariantScope.Module,
            Dimension = InvariantDimension.Boundaries,
            Name = "Database Driver Isolation",
            Severity = GateSeverity.Error,
            Description = "Storage infrastructure drivers are leaf components and must not reference domain logic, parsers, or analyzers.",
            Remediation = "Remove calls to domain parsers from database drivers. Drivers must only execute graph commands passed to them.",
            Query = """
                MATCH (driver)-[:CALLS|USES_TYPE]->(domain:Type)
                WHERE (driver.symbol CONTAINS 'Database/' OR driver.file_path CONTAINS 'Database/')
                  AND (domain.symbol CONTAINS 'CodeExplorer.Core/Parser/' OR domain.name IN ['PostIndexAnalyzer', 'WorkspaceIndexer'])
                  AND NOT coalesce(driver.is_test, false)
                RETURN 
                  coalesce(driver.symbol, driver.file_path) AS file,
                  coalesce(driver.start_line, 0) AS line,
                  driver.name AS symbol,
                  domain.name AS target,
                  'Storage infrastructure driver illegally references high-level domain parser/analyzer' AS message
                """,
            Ratchet = new GateRatchetSettings
            {
                Enabled = true,
                Baseline = "baselines/SIG-P-B-002.json"
            },
            Source = "BuiltIn"
        },

        new SemanticGate
        {
            Id = "SIG-S-B-001",
            Category = GateCategory.Platform,
            Platform = "dotnet",
            Scope = InvariantScope.Service,
            Dimension = InvariantDimension.Boundaries,
            Name = "Storage Interface Abstraction Invariance",
            Severity = GateSeverity.Error,
            Description = "Domain logic must interact with the graph database solely via abstract IGraphClient without concrete downcasting.",
            Remediation = "Remove casts to SqliteGraphClient and raw SQLite DDL/DML. Use Cypher queries or declare maintenance abstractions.",
            Query = """
                MATCH (parser)-[:CALLS|USES_TYPE]->(concreteDb:Type)
                WHERE (parser.symbol CONTAINS 'Parser/' OR parser.file_path CONTAINS 'Parser/')
                  AND concreteDb.name IN ['SqliteGraphClient', 'SqliteConnection', 'SqliteCommand']
                  AND NOT coalesce(parser.is_test, false)
                RETURN 
                  coalesce(parser.symbol, parser.file_path) AS file,
                  coalesce(parser.start_line, 0) AS line,
                  coalesce(parser.start_col, 0) AS column,
                  parser.name AS symbol,
                  concreteDb.name AS target,
                  'High-level parser directly couples to concrete SQLite storage implementation instead of IGraphClient' AS message
                """,
            Ratchet = new GateRatchetSettings
            {
                Enabled = true,
                Baseline = "baselines/SIG-S-B-001.json"
            },
            Source = "BuiltIn"
        },

        new SemanticGate
        {
            Id = "SIG-C-I-001",
            Category = GateCategory.Architectural,
            Platform = "agnostic",
            Scope = InvariantScope.Class,
            Dimension = InvariantDimension.Rules,
            Name = "In-Memory AST Visitor IO Purity",
            Severity = GateSeverity.Error,
            Description = "Syntax visitors and enrichers must operate purely on in-memory ASTs and avoid ambient disk I/O across any language.",
            Remediation = "Eliminate direct filesystem read/write calls (e.g. File/Directory in .NET, fs in Node, os/ioutil in Go, pathlib in Python, Files in Java) inside AST traversal. Pass file text or AST nodes from the pipeline context.",
            Query = """
                MATCH (visitor:Type)-[:CALLS]->(io:Type)
                WHERE (visitor.symbol CONTAINS 'SyntaxEnricher' OR visitor.symbol CONTAINS 'Visitor' OR visitor.file_path CONTAINS 'SyntaxEnricher')
                  AND io.name IN [
                    'File', 'Directory', 'FileStream', 'StreamReader', 'StreamWriter',
                    'fs', 'promises', 'fs-extra',
                    'os', 'ioutil',
                    'pathlib', 'shutil',
                    'Files', 'Paths', 'FileInputStream', 'FileOutputStream'
                  ]
                RETURN 
                  coalesce(visitor.symbol, visitor.file_path) AS file,
                  coalesce(visitor.start_line, 0) AS line,
                  coalesce(visitor.start_col, 0) AS column,
                  visitor.name AS symbol,
                  io.name AS target,
                  'AST enricher/visitor performs ambient disk I/O instead of operating on memory AST' AS message
                """,
            Ratchet = new GateRatchetSettings
            {
                Enabled = true,
                Baseline = "baselines/SIG-C-I-001.json"
            },
            Source = "BuiltIn"
        },

        new SemanticGate
        {
            Id = "SIG-P-C-001",
            Category = GateCategory.Architectural,
            Platform = "agnostic",
            Scope = InvariantScope.Module,
            Dimension = InvariantDimension.Contract,
            Name = "Project Identity Bijection",
            Severity = GateSeverity.Error,
            Description = "Within a workspace, each logical project entity must have a unique name and 1-to-1 bijection with a directory.",
            Remediation = "Rename duplicated project names or resolve naming collisions with the directory name.",
            Query = """
                MATCH (p1:Project), (p2:Project)
                WHERE p1.name = p2.name AND p1.path <> p2.path
                RETURN 
                  p1.path AS file,
                  1 AS line,
                  p1.name AS symbol,
                  p2.path AS target,
                  'Ambiguous project identity: multiple workspace projects claim identical name' AS message
                """,
            Ratchet = new GateRatchetSettings
            {
                Enabled = true,
                Baseline = "baselines/SIG-P-C-001.json"
            },
            Source = "BuiltIn"
        },

        new SemanticGate
        {
            Id = "SIG-A-B-001",
            Category = GateCategory.Architectural,
            Platform = "agnostic",
            Scope = InvariantScope.System,
            Dimension = InvariantDimension.Boundaries,
            Name = "Call Lineage Provenance",
            Severity = GateSeverity.Warning,
            Description = "Every external interaction must be provably rooted in an atomic calling function or endpoint handler.",
            Remediation = "Propagate caller_symbol_id when synthesizing external service calls so that callers link directly to endpoints.",
            Query = """
                MATCH (es:ExternalService)-[:CALLS_ENDPOINT]->(ep:Endpoint)
                WHERE NOT EXISTS((:Function)-[:CALLS]->(es))
                  AND NOT EXISTS((:Function)-[:CALLS_ENDPOINT]->(ep))
                RETURN 
                  es.file_path AS file,
                  coalesce(es.start_line, 0) AS line,
                  es.name AS symbol,
                  ep.name AS target,
                  'Dangling cross-service interaction: external call cannot be attributed to an atomic calling function' AS message
                """,
            Ratchet = new GateRatchetSettings
            {
                Enabled = true,
                Baseline = "baselines/SIG-A-B-001.json"
            },
            Source = "BuiltIn"
        },

        new SemanticGate
        {
            Id = "SIG-P-B-003",
            Category = GateCategory.Architectural,
            Platform = "agnostic",
            Scope = InvariantScope.Module,
            Dimension = InvariantDimension.Boundaries,
            Name = "Clean Architecture Transport Purity",
            Severity = GateSeverity.Error,
            Description = "Controllers and HTTP transport endpoints must not directly execute database queries or mutate ORM tables.",
            Remediation = "Delegate data persistence to a service or repository layer; controllers must not call database contexts directly.",
            Query = """
                MATCH (ep:Endpoint)-[:CALLS]->(q:Query)
                RETURN 
                  ep.file_path AS file,
                  coalesce(ep.start_line, 0) AS line,
                  ep.name AS symbol,
                  q.name AS target,
                  'Transport endpoint directly calls raw database query bypassing domain services' AS message
                """,
            Ratchet = new GateRatchetSettings
            {
                Enabled = true,
                Baseline = "baselines/SIG-P-B-003.json"
            },
            Source = "BuiltIn"
        },

        new SemanticGate
        {
            Id = "SIG-P-I-001",
            Category = GateCategory.Architectural,
            Platform = "agnostic",
            Scope = InvariantScope.Module,
            Dimension = InvariantDimension.Rules,
            Name = "Circular Package Dependency Shield",
            Severity = GateSeverity.Error,
            Description = "Inter-package dependencies must form an acyclic directed graph (DAG).",
            Remediation = "Break the circular dependency cycle using interfaces, events, or moving shared dependencies to a common leaf module.",
            Query = """
                MATCH (p1:Package)-[:DEPENDS_ON*1..3]->(p2:Package)
                WHERE (p2)-[:DEPENDS_ON*1..3]->(p1) AND p1.id <> p2.id
                RETURN 
                  p1.name AS file,
                  1 AS line,
                  p1.name AS symbol,
                  p2.name AS target,
                  'Circular package dependency detected between packages' AS message
                """,
            Ratchet = new GateRatchetSettings
            {
                Enabled = true,
                Baseline = "baselines/SIG-P-I-001.json"
            },
            Source = "BuiltIn"
        },

        new SemanticGate
        {
            Id = "SIG-M-I-001",
            Category = GateCategory.Architectural,
            Platform = "agnostic",
            Scope = InvariantScope.Method,
            Dimension = InvariantDimension.Rules,
            Name = "Disallow Hardcoded Call Arguments",
            Severity = GateSeverity.Error,
            Description = "Methods must not receive raw, hardcoded string literals as arguments. Use typed enums, strongly-typed domain identifiers, named constants, or external configuration.",
            Remediation = "Replace raw string literals with domain enums, strongly-typed IDs, named constants, or external configuration.",
            Query = """
                MATCH (caller)-[:PASSES_LITERAL]->(lit:CallLiteral)
                WHERE NOT coalesce(caller.is_test, false) AND NOT coalesce(lit.is_test, false)
                RETURN 
                  lit.file_path AS file,
                  lit.line AS line,
                  lit.column AS column,
                  coalesce(caller.name, 'unknown') AS symbol,
                  lit.callee AS target,
                  'Hardcoded string literal "' + (CASE WHEN size(lit.value) > 40 THEN substring(lit.value, 0, 37) + '...' ELSE lit.value END) + '" passed to ' + lit.callee AS message
                """,
            Ratchet = new GateRatchetSettings
            {
                Enabled = true,
                Baseline = "baselines/SIG-M-I-001.json"
            },
            Source = "BuiltIn"
        }
    ];
}
