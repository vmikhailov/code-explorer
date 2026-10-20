using CodeExplorer.Core.Gates.Models;

namespace CodeExplorer.Core.Gates.Storage;

/// <summary>
/// Default catalog of standard architectural Semantic Invariant Gates shipped with CodeExplorer.
/// </summary>
public static class BuiltInGateCatalog
{
    public static IReadOnlyList<SemanticGate> GetDefaultGates() =>
    [
        EndpointIdentityUniquenessGate,
        SqlTableSanitizationGate,
        UnmountedExpressRouteGate,
        GenericPipelineLayerIsolationGate,
        GraphMutationCypherPurityGate,
        CoreZeroDomainHardcodesGate,
        OntologyLayerStrictHierarchyGate,
        OntologyModelCohesionGate,
        DisallowStringlyTypedTaxonomiesGate,
        SemanticRuntimeEntityKindIntegrityGate,
        NoAbstractEngineDatabaseNodesGate,
        RustUnsafeCodeIsolationGate,
        RustCleanArchitectureCrateBoundaryGate,
        RustAsyncTokioAntiBlockingGate,
        RustUnparameterizedSqlQueryGate,
        GodClassProhibitionGate
    ];

    public static IReadOnlyList<SemanticGate> GetExampleGates() =>
    [
        EndpointIdentityUniquenessGate,
        SqlTableSanitizationGate,
        UnmountedExpressRouteGate,
        GenericPipelineLayerIsolationGate,
        GraphMutationCypherPurityGate,
        CoreZeroDomainHardcodesGate,
        OntologyLayerStrictHierarchyGate,
        OntologyModelCohesionGate,
        DisallowStringlyTypedTaxonomiesGate,
        SemanticRuntimeEntityKindIntegrityGate,
        NoAbstractEngineDatabaseNodesGate,
        RustUnsafeCodeIsolationGate,
        RustCleanArchitectureCrateBoundaryGate,
        RustAsyncTokioAntiBlockingGate,
        RustUnparameterizedSqlQueryGate,
        GodClassProhibitionGate,
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
                WHERE NOT EXISTS((es)-[:CALLED_BY]->(:Function))
                  AND NOT EXISTS((:Function)-[:CALLS]->(es))
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
            Id = "SIG-A-C-001",
            Category = GateCategory.Architectural,
            Platform = "agnostic",
            Scope = InvariantScope.System,
            Dimension = InvariantDimension.Contract,
            Name = "Dead Internal HTTP Egress Prohibition",
            Severity = GateSeverity.Error,
            Description = "Services must not make HTTP calls to internal system routes that are not exposed by any active service endpoint.",
            Remediation = "Update the HTTP call to a valid endpoint or register the missing endpoint handler in the target service.",
            Query = """
                MATCH (es:ExternalService)-[:CALLED_BY]->(caller), (target:Project)
                WHERE es.is_internal = true
                  AND (target.name = es.name OR target.name = es.domain)
                  AND NOT (es)-[:CALLS_ENDPOINT]->(:Endpoint)
                RETURN 
                  coalesce(caller.file_path, caller.path) AS file,
                  coalesce(caller.start_line, 1) AS line,
                  caller.name AS symbol,
                  es.path AS target,
                  'Internal HTTP call to non-existent endpoint ' + es.path AS message
                """,
            Ratchet = new GateRatchetSettings
            {
                Enabled = true,
                Baseline = "baselines/SIG-A-C-001.json"
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
            Name = "Circular Dependency Shield",
            Severity = GateSeverity.Error,
            Description = "Inter-package and inter-project dependencies must form an acyclic directed graph (DAG).",
            Remediation = "Break the circular dependency cycle using interfaces, events, or moving shared dependencies to a common leaf module.",
            Query = """
                MATCH (p1)-[:DEPENDS_ON*1..3]->(p2)
                WHERE (p1:Package OR p1:Project) AND (p2:Package OR p2:Project)
                  AND (p2)-[:DEPENDS_ON*1..3]->(p1) AND p1.id <> p2.id
                RETURN 
                  p1.name AS file,
                  1 AS line,
                  p1.name AS symbol,
                  p2.name AS target,
                  'Circular dependency detected between packages or projects' AS message
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
                  AND coalesce(lit.role, 'Business') = 'Business'
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
        },

        new SemanticGate
        {
            Id = "SIG-M-I-002",
            Category = GateCategory.Architectural,
            Platform = "agnostic",
            Scope = InvariantScope.Method,
            Dimension = InvariantDimension.Rules,
            Name = "Method Length Limitation",
            Severity = GateSeverity.Error,
            Description = "Methods must not exceed 500 lines of code to prevent monster/god methods and maintain readability and single responsibility.",
            Remediation = "Refactor long methods into smaller focused helper functions, sub-routines, or dedicated pipeline/handler classes.",
            Query = """
                MATCH (f:Function)
                WHERE coalesce(f.loc, f.end_line - f.start_line + 1) > 500
                  AND NOT coalesce(f.is_test, false)
                RETURN 
                  coalesce(f.path, f.file_path) AS file,
                  coalesce(f.start_line, 1) AS line,
                  coalesce(f.start_col, 1) AS column,
                  f.name AS symbol,
                  toString(coalesce(f.loc, f.end_line - f.start_line + 1)) + ' lines' AS target,
                  'Method ' + f.name + ' exceeds maximum allowed length of 500 lines (' + toString(coalesce(f.loc, f.end_line - f.start_line + 1)) + ' lines)' AS message
                """,
            Ratchet = new GateRatchetSettings
            {
                Enabled = true,
                Baseline = "baselines/SIG-M-I-002.json"
            },
            Source = "BuiltIn"
        },

        new SemanticGate
        {
            Id = "SIG-M-R-001",
            Category = GateCategory.Architectural,
            Platform = "agnostic",
            Scope = InvariantScope.Method,
            Dimension = InvariantDimension.Resources,
            Name = "Method Length Ceiling",
            Severity = GateSeverity.Error,
            Description = "Methods and functions must not exceed 500 lines of code to maintain readability and prevent monolithic implementations.",
            Remediation = "Refactor oversized methods into smaller, focused helper functions, sub-routines, or dedicated pipeline/handler classes.",
            Query = """
                MATCH (f:Function)
                WHERE coalesce(f.loc, f.end_line - f.start_line + 1) > 500
                  AND NOT coalesce(f.is_test, false)
                RETURN 
                  coalesce(f.path, f.file_path) AS file,
                  coalesce(f.start_line, 1) AS line,
                  coalesce(f.start_col, 1) AS column,
                  f.name AS symbol,
                  toString(coalesce(f.loc, f.end_line - f.start_line + 1)) + ' lines' AS target,
                  'Method ' + f.name + ' exceeds maximum allowed length of 500 lines (' + toString(coalesce(f.loc, f.end_line - f.start_line + 1)) + ' lines)' AS message
                """,
            Ratchet = new GateRatchetSettings
            {
                Enabled = true,
                Baseline = "baselines/SIG-M-R-001.json"
            },
            Source = "BuiltIn"
        },

        new SemanticGate
        {
            Id = "SIG-M-I-005",
            Category = GateCategory.Architectural,
            Platform = "agnostic",
            Scope = InvariantScope.Method,
            Dimension = InvariantDimension.Rules,
            Name = "No Empty Catch Blocks",
            Severity = GateSeverity.Warning,
            Description = "Catches must not silently suppress exceptions without at least an explanatory comment or handling logic.",
            Remediation = "Log or handle the exception appropriately, rethrow it, or add an explanatory comment (e.g. `// ignored: expected transient condition`) explaining why it is safe to swallow.",
            Query = """
                MATCH (c:CatchClause)
                WHERE c.is_empty = true AND c.has_comment = false AND NOT coalesce(c.is_test, false)
                RETURN 
                  c.file_path AS file,
                  c.line AS line,
                  c.column AS column,
                  coalesce(c.exception_type, 'catch') AS symbol,
                  'catch' AS target,
                  'Empty catch block without an explanatory comment suppresses errors silently' AS message
                """,
            Ratchet = new GateRatchetSettings
            {
                Enabled = true,
                Baseline = "baselines/SIG-M-I-005.json"
            },
            Source = "BuiltIn"
        }
    ];

    public static readonly SemanticGate EndpointIdentityUniquenessGate = new()
    {
        Id = "SIG-M-C-001",
        Category = GateCategory.Architectural,
        Platform = "agnostic",
        Scope = InvariantScope.Module,
        Dimension = InvariantDimension.Rules,
        Name = "Endpoint Identity Uniqueness",
        Severity = GateSeverity.Error,
        Description = "Disallows duplicate API endpoints within the same service/project with identical HTTP method and route template.",
        Remediation = "Ensure each route template in a microservice is registered only once. Check for overlapping router mounts or duplicate route declarations.",
        Query = """
            MATCH (p:Project)-[:CONTAINS]->(e1:Endpoint)
            MATCH (p)-[:CONTAINS]->(e2:Endpoint)
            WHERE e1.id < e2.id
              AND e1.http_method = e2.http_method
              AND e1.route_template = e2.route_template
            RETURN 
              coalesce(e1.path, e1.file_path) AS file,
              1 AS line,
              1 AS column,
              e1.name AS symbol,
              e2.id AS target,
              'Duplicate endpoint route declared in project ' + p.name + ': ' + e1.http_method + ' ' + e1.route_template AS message
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-M-C-001.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate SqlTableSanitizationGate = new()
    {
        Id = "SIG-S-C-001",
        Category = GateCategory.Architectural,
        Platform = "agnostic",
        Scope = InvariantScope.Module,
        Dimension = InvariantDimension.Rules,
        Name = "SQL Table Entity Sanitization",
        Severity = GateSeverity.Error,
        Description = "Graph Table entities must represent valid SQL tables and must not contain SQL keywords (e.g. SKIP, LOCKED, FETCH) or whitespace/syntax fragments.",
        Remediation = "Refactor SQL queries or update SQL lexer exclusions so SQL clauses and keywords are not mistakenly ingested as database tables.",
        Query = """
            MATCH (t:Table)
            WHERE t.name IN ['SKIP', 'LOCKED', 'NOWAIT', 'RETURNING', 'CASCADE', 'RESTRICT', 'CONFLICT', 'DO', 'NOTHING', 'FETCH', 'NEXT', 'ROWS', 'ONLY', 'ROW', 'FIRST', 'LAST', 'FILTER', 'MATERIALIZED', 'COLLATE', 'CHECK', 'CONSTRAINT', 'DEFAULT']
               OR t.name CONTAINS ' '
               OR t.name CONTAINS '`'
               OR t.name CONTAINS '('
               OR t.name CONTAINS ')'
               OR t.name CONTAINS ';'
            RETURN 
              coalesce(t.path, t.file_path, 'schema') AS file,
              1 AS line,
              1 AS column,
              t.name AS symbol,
              t.name AS target,
              'Invalid or keyword SQL Table entity detected in graph: ' + t.name AS message
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-S-C-001.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate UnmountedExpressRouteGate = new()
    {
        Id = "SIG-M-B-001",
        Category = GateCategory.Architectural,
        Platform = "agnostic",
        Scope = InvariantScope.Module,
        Dimension = InvariantDimension.Boundaries,
        Name = "Unmounted Express Parameter Route Shield",
        Severity = GateSeverity.Warning,
        Description = "Parameterized root routes (such as /:id, /:uuid, /:key) must not exist directly at the service root without their resource mount prefix.",
        Remediation = "Mount the route router using app.use('/api/<resource>', router) or prepend the canonical resource path to the route declaration.",
        Query = """
            MATCH (e:Endpoint)
            WHERE e.route_template IN ['/:id', '/:uuid', '/:key']
            RETURN 
              coalesce(e.path, e.file_path) AS file,
              1 AS line,
              1 AS column,
              e.name AS symbol,
              e.id AS target,
              'Unmounted parameter root route detected: ' + e.http_method + ' ' + e.route_template + ' in ' + coalesce(e.path, 'unknown') AS message
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-M-B-001.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate GenericPipelineLayerIsolationGate = new()
    {
        Id = "SIG-P-B-004",
        Category = GateCategory.Architectural,
        Platform = "agnostic",
        Scope = InvariantScope.Module,
        Dimension = InvariantDimension.Boundaries,
        Name = "Generic Pipeline Layer Isolation",
        Severity = GateSeverity.Error,
        Description = "Generic parser pipeline layers (Layer1-Layer5) must not directly reference or couple to library-specific extension parsers (Libraries/). Library support must strictly implement ISemanticExtension.",
        Remediation = "Remove library-specific imports and calls from generic pipeline layers. Implement ISemanticExtension under Libraries/ and register it via dependency injection or parser registry.",
        Query = """
            MATCH (layer:Type)-[:CALLS|USES_TYPE]->(lib:Type)
            WHERE (layer.file_path CONTAINS 'Parser/Layers/' OR layer.symbol CONTAINS 'Parser.Layers')
              AND (lib.file_path CONTAINS 'Libraries/' OR lib.symbol CONTAINS 'Libraries.')
              AND NOT coalesce(layer.is_test, false)
            RETURN 
              layer.file_path AS file,
              coalesce(layer.start_line, 1) AS line,
              coalesce(layer.start_col, 1) AS column,
              layer.name AS symbol,
              lib.name AS target,
              'Generic pipeline layer illegally references library-specific extension parser: ' + lib.name AS message
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-P-B-004.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate GraphMutationCypherPurityGate = new()
    {
        Id = "SIG-S-B-002",
        Category = GateCategory.Platform,
        Platform = "dotnet",
        Scope = InvariantScope.Service,
        Dimension = InvariantDimension.Boundaries,
        Name = "Graph Mutation Cypher Purity",
        Severity = GateSeverity.Error,
        Description = "Graph structure cascades, subtree deletions, and topological traversals must be expressed in OpenCypher (e.g. DETACH DELETE), not multi-statement raw SQL scripts with temporary tables and imperative loops.",
        Remediation = "Replace multi-statement SQL scripts with OpenCypher queries executed via IGraphClient / CypherQueryParser.",
        Query = """
            MATCH (f:Function)
            WHERE (f.name CONTAINS 'DeleteSubpath' OR f.name CONTAINS 'CascadeDeleteSql')
              AND (f.file_path CONTAINS 'Database/' OR f.file_path CONTAINS 'Client')
              AND NOT coalesce(f.is_test, false)
            RETURN 
              f.file_path AS file,
              coalesce(f.start_line, 1) AS line,
              coalesce(f.start_col, 1) AS column,
              f.name AS symbol,
              'Raw SQL temporary table script' AS target,
              'Graph manipulation function ' + f.name + ' uses imperative SQL script instead of declarative Cypher DETACH DELETE' AS message
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-S-B-002.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate CoreZeroDomainHardcodesGate = new()
    {
        Id = "SIG-M-I-003",
        Category = GateCategory.Architectural,
        Platform = "agnostic",
        Scope = InvariantScope.Method,
        Dimension = InvariantDimension.Rules,
        Name = "Core Zero Domain Hardcodes Invariance",
        Severity = GateSeverity.Error,
        Description = "Core analysis engines, classifiers, and convention algorithms must not contain hardcoded company/customer domain literals (e.g. dynadot, rtb, antifraud, google-pub-sub-network).",
        Remediation = "Move customer or workspace-specific tokens into .codeexplorer/conventions.json or resolve them dynamically from graph metadata.",
        Query = """
            MATCH (caller)-[:PASSES_LITERAL]->(lit:CallLiteral)
            WHERE (caller.file_path CONTAINS 'Core/Common/' OR caller.file_path CONTAINS 'Core/Analysis/' OR caller.file_path CONTAINS 'Core/Parser/')
              AND lit.value IN ['dynadot_base_url_v3', 'google-pub-sub-network', 'pub-sub-network', 'antifraud', 'rtb']
              AND NOT coalesce(caller.is_test, false)
            RETURN 
              lit.file_path AS file,
              lit.line AS line,
              lit.column AS column,
              coalesce(caller.name, 'unknown') AS symbol,
              lit.value AS target,
              'Illegal hardcoded customer/domain literal in core algorithm: ' + lit.value AS message
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-M-I-003.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate OntologyLayerStrictHierarchyGate = new()
    {
        Id = "SIG-P-B-005",
        Category = GateCategory.Architectural,
        Platform = "agnostic",
        Scope = InvariantScope.Module,
        Dimension = InvariantDimension.Boundaries,
        Name = "Ontology Layer Strict Hierarchy",
        Severity = GateSeverity.Error,
        Description = "Enforces strict unidirectional layering across the ontology tiers: Layer 1 (Physical) -> Layer 2 (Boundaries) -> Layer 3 (Syntactic) -> Layer 4 (Semantic). Lower-layer models must never reference or depend on higher-order ontology layers (prohibits reverse/backward coupling).",
        Remediation = "Remove higher-layer references from lower-layer ontology models. Use downward relationships (e.g. higher-order models referencing lower-order models via DECLARED_IN, LOCATED_IN, EXPOSED_BY) or abstract contracts.",
        Query = """
            MATCH (src:Type)-[:CALLS|USES_TYPE]->(tgt:Type)
            WHERE NOT coalesce(src.is_test, false)
              AND (
                (src.path CONTAINS 'Common/Nodes/Layer1_' AND (tgt.path CONTAINS 'Common/Nodes/Layer2_' OR tgt.path CONTAINS 'Common/Nodes/Layer3_' OR tgt.path CONTAINS 'Common/Nodes/Layer4_'))
                OR (src.path CONTAINS 'Common/Nodes/Layer2_' AND (tgt.path CONTAINS 'Common/Nodes/Layer3_' OR tgt.path CONTAINS 'Common/Nodes/Layer4_'))
                OR (src.path CONTAINS 'Common/Nodes/Layer3_' AND tgt.path CONTAINS 'Common/Nodes/Layer4_')
              )
            RETURN 
              coalesce(src.path, src.file_path) AS file,
              coalesce(src.start_line, 1) AS line,
              coalesce(src.start_col, 1) AS column,
              src.name AS symbol,
              tgt.name AS target,
              'Ontology layer violation: Lower layer model ' + src.name + ' references higher layer model ' + tgt.name AS message
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-P-B-005.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate OntologyModelCohesionGate = new()
    {
        Id = "SIG-C-B-002",
        Category = GateCategory.Architectural,
        Platform = "agnostic",
        Scope = InvariantScope.Class,
        Dimension = InvariantDimension.Boundaries,
        Name = "Ontology Model Layer Cohesion and Isolation",
        Severity = GateSeverity.Error,
        Description = "All ontology node models implementing IOntologyNode or extending CompositeNode must reside strictly within CodeExplorer.Core/Common/Nodes/ under an approved layer or root abstraction. No ad-hoc ontology node types may leak into Parser, UI, or external packages.",
        Remediation = "Move external ontology node models into the appropriate layer under cli/src/Core/CodeExplorer.Core/Common/Nodes/ or refactor parser-internal DTOs to not implement IOntologyNode.",
        Query = """
            MATCH (src:Type)-[:IMPLEMENTS|USES_TYPE]->(contract:Type)
            WHERE contract.name IN ['IOntologyNode', 'CompositeNode']
              AND NOT coalesce(src.is_test, false)
              AND NOT src.path CONTAINS 'Common/Nodes/'
              AND NOT src.name IN ['IOntologyNode', 'CompositeNode', 'OntologyAttributes', 'OntologyEdgeAttribute', 'OntologyPropertyAttribute', 'OntologyNodeAttribute', 'OntologyRelationshipAttribute', 'OntologyRegistry', 'LayerResults', 'Layer4Result']
            RETURN 
              coalesce(src.path, src.file_path) AS file,
              coalesce(src.start_line, 1) AS line,
              coalesce(src.start_col, 1) AS column,
              src.name AS symbol,
              contract.name AS target,
              'Ontology cohesion violation: Model ' + src.name + ' implements/uses ontology contract outside Common/Nodes' AS message
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-C-B-002.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate DisallowStringlyTypedTaxonomiesGate = new()
    {
        Id = "SIG-C-C-001",
        Category = GateCategory.Architectural,
        Platform = "agnostic",
        Scope = InvariantScope.Class,
        Dimension = InvariantDimension.Contract,
        Name = "Disallow Stringly-Typed Taxonomies and Classifications",
        Severity = GateSeverity.Error,
        Description = "Domain and architectural classifications must be modeled using strongly-typed enums or value objects. Properties or fields ending in 'Type', 'Kind', or 'Category' must not use primitive string types.",
        Remediation = "Replace primitive string taxonomy properties with dedicated enums or strongly-typed value objects.",
        Query = """
            MATCH (m:Member)
            WHERE m.kind IN ['property', 'field']
              AND (m.name = 'Type' OR m.name ENDS WITH 'Type' OR
                   m.name = 'Kind' OR m.name ENDS WITH 'Kind' OR
                   m.name = 'Category' OR m.name ENDS WITH 'Category')
              AND m.data_type IN ['string', 'String', 'string?', 'String?']
              AND NOT (coalesce(m.file_path, '') CONTAINS 'Test' OR coalesce(m.file_path, '') CONTAINS 'test')
            RETURN 
              coalesce(m.file_path, 'unknown') AS file,
              coalesce(m.start_line, 0) AS line,
              coalesce(m.start_col, 0) AS column,
              m.name AS symbol,
              coalesce(m.data_type, 'string') AS target,
              'Property or field ' + m.name + ' uses stringly-typed primitive ' + coalesce(m.data_type, 'string') + ' for taxonomy/classification; use an enum or strongly-typed value object.' AS message
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-C-C-001.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate SemanticRuntimeEntityKindIntegrityGate = new()
    {
        Id = "SIG-O-B-001",
        Category = GateCategory.Architectural,
        Platform = "agnostic",
        Scope = InvariantScope.Module,
        Dimension = InvariantDimension.Boundaries,
        Name = "Semantic Runtime Entity & View Kind Integrity",
        Severity = GateSeverity.Error,
        Description = "Enforces strict parity and integrity between physical project roles, Layer 4 semantic workloads, and architectural view node kinds. Test projects must never be classified as runnable App/Service/Worker workloads, and workload node labels must strictly align with their kind property.",
        Remediation = "Ensure test projects reference test frameworks or match test naming conventions, and verify project classifiers do not overwrite test or workload kinds with conflicting defaults.",
        Query = """
            MATCH (n)
            WHERE (n:App OR n:Service OR n:Worker OR n:CliTool)
              AND (
                coalesce(n.is_test_project, 'false') = 'true'
                OR coalesce(n.role, '') = 'Test'
                OR toLower(n.name) ENDS WITH '.unittests'
                OR toLower(n.name) ENDS WITH '.functionaltests'
                OR toLower(n.name) ENDS WITH '.integrationtests'
                OR toLower(n.name) ENDS WITH '.tests'
                OR toLower(n.name) ENDS WITH '.test'
                OR (n:App AND n.kind IS NOT NULL AND n.kind <> 'App')
                OR (n:Service AND n.kind IS NOT NULL AND n.kind <> 'Service')
                OR (n:Worker AND n.kind IS NOT NULL AND n.kind <> 'Worker')
                OR (n:CliTool AND n.kind IS NOT NULL AND n.kind <> 'CliTool')
              )
            RETURN 
              coalesce(n.path, n.file_path, 'unknown') AS file,
              coalesce(n.start_line, 1) AS line,
              coalesce(n.start_col, 1) AS column,
              n.name AS symbol,
              labels(n)[0] AS target,
              'Workload node ' + n.name + ' has invalid entity type, misclassified test project, or kind mismatch with label ' + labels(n)[0] AS message
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-O-B-001.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate NoAbstractEngineDatabaseNodesGate = new()
    {
        Id = "SIG-O-B-002",
        Category = GateCategory.Architectural,
        Platform = "agnostic",
        Scope = InvariantScope.Module,
        Dimension = InvariantDimension.Boundaries,
        Name = "No Abstract Engine-Only Database Nodes",
        Severity = GateSeverity.Error,
        Description = "Database nodes must represent concrete databases with a valid schema or catalog (e.g. 'PostgreSQL.catalog', 'Redis.basket'). Abstract engine-only or driver-only database nodes (such as 'PostgreSQL', 'MongoDB', 'Redis', 'Database', or generic placeholders) violate domain boundaries and cross-service isolation.",
        Remediation = "Ensure projects declare an explicit database schema in connection strings or code, or avoid creating database nodes for bare driver/ORM library references.",
        Query = """
            MATCH (d:Database)
            WHERE (
                toLower(d.name) IN ['postgresql', 'postgres', 'sql server', 'sqlserver', 'mssql', 'mysql', 'mariadb', 'sqlite', 'mongodb', 'mongo', 'oracle', 'database', 'db', 'default', 'relational', 'redis', 'cache', 'ef-core', 'dapper', 'typeorm', 'prisma', 'hibernate', 'nhibernate', 'sequelize', 'drizzle']
                OR coalesce(d.schema, '') = 'default'
                OR toLower(coalesce(d.schema, '')) IN ['postgresql', 'postgres', 'sql server', 'sqlserver', 'mssql', 'mysql', 'mariadb', 'sqlite', 'mongodb', 'mongo', 'oracle', 'database', 'db', 'default', 'relational', 'redis', 'cache', 'ef-core', 'dapper', 'typeorm', 'prisma', 'hibernate', 'nhibernate', 'sequelize', 'drizzle']
                OR d.id ENDS WITH ':relational:postgresql'
                OR d.id ENDS WITH ':relational:sqlserver'
                OR d.id ENDS WITH ':relational:mysql'
                OR d.id ENDS WITH ':relational:sqlite'
                OR d.id ENDS WITH ':relational:database'
                OR d.id ENDS WITH ':document:mongodb'
                OR d.id ENDS WITH ':cache:redis'
                OR d.id ENDS WITH ':keyvalue:redis'
                OR d.id ENDS WITH ':default:default'
              )
            RETURN 
              coalesce(d.file_path, d.path, 'architecture') AS file,
              coalesce(d.start_line, 1) AS line,
              coalesce(d.start_col, 1) AS column,
              d.name AS symbol,
              coalesce(d.engine, 'unknown') AS target,
              'Abstract engine-only database node ' + d.name + ' (' + d.id + ') is not associated with a concrete schema or domain database' AS message
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-O-B-002.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate RustUnsafeCodeIsolationGate = new()
    {
        Id = "SIG-R-S-001",
        Category = GateCategory.Platform,
        Platform = "rust",
        Scope = InvariantScope.Method,
        Dimension = InvariantDimension.Boundaries,
        Name = "Unsafe Code Isolation Invariant",
        Severity = GateSeverity.Error,
        Description = "Memory safety is Rust's primary value proposition. Unsafe functions and blocks must be isolated to low-level hardware or foreign abstraction crates (sys, native, ffi) and are strictly forbidden in business logic, HTTP handlers, or application services.",
        Remediation = "Move unsafe code into a dedicated low-level crate (e.g. *-sys or *-native) or encapsulate it behind safe abstractions.",
        Query = """
            MATCH (fn:Function)-[:DECLARED_IN]->(f:File)<-[:CONTAINS]-(p:Project)
            WHERE fn.is_unsafe = true
              AND NOT toLower(coalesce(p.path, p.name, '')) CONTAINS "sys"
              AND NOT toLower(coalesce(p.path, p.name, '')) CONTAINS "native"
              AND NOT toLower(coalesce(p.path, p.name, '')) CONTAINS "ffi"
              AND NOT coalesce(fn.is_test, false)
            RETURN 
              coalesce(f.path, fn.file_path, 'unknown') AS file,
              coalesce(fn.start_line, fn.line_number, 1) AS line,
              fn.name AS symbol,
              p.name AS target,
              'Unsafe function declared outside approved FFI/sys boundary: ' + fn.name AS message
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-R-S-001.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate RustCleanArchitectureCrateBoundaryGate = new()
    {
        Id = "SIG-R-A-001",
        Category = GateCategory.Architectural,
        Platform = "rust",
        Scope = InvariantScope.Module,
        Dimension = InvariantDimension.Boundaries,
        Name = "Clean Architecture Crate Boundary Invariant",
        Severity = GateSeverity.Error,
        Description = "Domain crates (core, domain) must remain pure and decoupled from infrastructure. A domain crate must never depend on web frameworks (axum, actix-web, rocket) or database drivers/ORMs (sqlx, diesel, sea-orm).",
        Remediation = "Remove infrastructure dependencies from domain/core crates. Declare interfaces/traits in domain and implement them in infrastructure adapter crates.",
        Query = """
            MATCH (p:Project)-[:DEPENDS_ON]->(pkg:Package)
            WHERE (toLower(p.name) CONTAINS "domain" OR toLower(p.name) CONTAINS "core")
              AND pkg.name IN ["axum", "actix-web", "rocket", "sqlx", "diesel", "sea-orm"]
            RETURN 
              coalesce(p.path, p.file_path, p.name) AS file,
              1 AS line,
              p.name AS symbol,
              pkg.name AS target,
              'Domain project violates Clean Architecture by depending on infrastructure crate: ' + pkg.name AS message
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-R-A-001.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate RustAsyncTokioAntiBlockingGate = new()
    {
        Id = "SIG-R-C-001",
        Category = GateCategory.Platform,
        Platform = "rust",
        Scope = InvariantScope.Method,
        Dimension = InvariantDimension.Rules,
        Name = "Async Tokio Runtime Anti-Blocking Invariant",
        Severity = GateSeverity.Error,
        Description = "Invoking synchronous blocking functions (std::thread::sleep, std::fs::read/write, reqwest::blocking) inside an async function starves the Tokio executor threadpool.",
        Remediation = "Replace blocking calls with their Tokio non-blocking equivalents (e.g. tokio::time::sleep, tokio::fs) or offload via tokio::task::spawn_blocking.",
        Query = """
            MATCH (fn:Function)-[:CALLS]->(target)
            WHERE fn.is_async = true
              AND (
                target.full_name IN [
                  "std::thread::sleep", "std::fs::read", "std::fs::write",
                  "std::fs::read_to_string", "reqwest::blocking"
                ]
                OR target.name IN [
                  "std::thread::sleep", "std::fs::read", "std::fs::write",
                  "std::fs::read_to_string", "reqwest::blocking"
                ]
              )
              AND NOT coalesce(fn.is_test, false)
            RETURN 
              coalesce(fn.file_path, fn.path, 'unknown') AS file,
              coalesce(fn.start_line, 1) AS line,
              fn.name AS symbol,
              coalesce(target.full_name, target.name) AS target,
              'Async function invokes blocking call: ' + coalesce(target.full_name, target.name) AS message
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-R-C-001.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate RustUnparameterizedSqlQueryGate = new()
    {
        Id = "SIG-R-D-001",
        Category = GateCategory.Platform,
        Platform = "rust",
        Scope = InvariantScope.Method,
        Dimension = InvariantDimension.Rules,
        Name = "Unparameterized SQL Query Invariance",
        Severity = GateSeverity.Error,
        Description = "Enforce parameter binding in database queries. String formatting (format!) within a database query represents a critical SQL injection vulnerability.",
        Remediation = "Use query parameter bindings ($1, $2 or ?) instead of format! macro string concatenation.",
        Query = """
            MATCH (fn:Function)-[:QUERIES_DB]->(tbl:Table),
                  (fn)-[:CALLS]->(lit:CallLiteral)
            WHERE (lit.text CONTAINS "format!" OR lit.value CONTAINS "format!")
              AND (
                toUpper(coalesce(lit.text, lit.value)) CONTAINS "SELECT"
                OR toUpper(coalesce(lit.text, lit.value)) CONTAINS "DELETE"
                OR toUpper(coalesce(lit.text, lit.value)) CONTAINS "UPDATE"
                OR toUpper(coalesce(lit.text, lit.value)) CONTAINS "INSERT"
              )
            RETURN 
              coalesce(fn.file_path, fn.path, lit.file_path, 'unknown') AS file,
              coalesce(lit.line, fn.start_line, 1) AS line,
              fn.name AS symbol,
              tbl.name AS target,
              'Dynamic string formatting detected in SQL query: ' + coalesce(lit.text, lit.value) AS message
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-R-D-001.json"
        },
        Source = "BuiltIn"
    };

    public static readonly SemanticGate GodClassProhibitionGate = new()
    {
        Id = "SIG-C-R-001",
        Category = GateCategory.Architectural,
        Platform = "agnostic",
        Scope = InvariantScope.Class,
        Dimension = InvariantDimension.Resources,
        Name = "God Class Prohibition",
        Severity = GateSeverity.Error,
        Description = "Classes and structs must not exceed 1,000 lines of code or declare more than 30 methods in a single type unit. Monolithic 'God Classes' accumulate unrelated responsibilities, violate the Single Responsibility Principle (SRP), and degrade maintainability.",
        Remediation = "Decompose the God Class into smaller, cohesive domain services, handlers, or strategy classes with focused responsibilities.",
        Query = """
            MATCH (t:Type)
            WHERE coalesce(t.kind, 'class') IN ['class', 'struct']
              AND NOT coalesce(t.is_test, false)
              AND NOT (coalesce(t.path, t.file_path, '') CONTAINS 'Test' OR coalesce(t.path, t.file_path, '') CONTAINS 'test')
            OPTIONAL MATCH (t)-[:HAS_METHOD]->(m:Function)
            WITH t, count(m) AS method_count, coalesce(t.loc, t.end_line - t.start_line + 1) AS loc
            WHERE loc > 1000 OR method_count > 30
            RETURN 
              coalesce(t.path, t.file_path) AS file,
              coalesce(t.start_line, 1) AS line,
              coalesce(t.start_col, 1) AS column,
              t.name AS symbol,
              'GodClass' AS target,
              'God class detected: ' + t.name + ' has ' + toString(loc) + ' LOC and ' + toString(method_count) + ' methods (max allowed: 1000 LOC / 30 methods)' AS message
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-C-R-001.json"
        },
        Source = "BuiltIn"
    };
}

