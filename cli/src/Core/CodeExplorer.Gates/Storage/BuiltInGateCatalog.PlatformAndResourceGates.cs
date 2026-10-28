using CodeExplorer.Core.Gates.Models;

namespace CodeExplorer.Core.Gates.Storage;

public static partial class BuiltInGateCatalog
{
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
        Target = "Raw SQL temporary table script",
        Message = "Graph manipulation function {f.name} uses imperative SQL script instead of declarative Cypher DETACH DELETE",
        Query = """
            MATCH (f:Function)
            WHERE (f.name CONTAINS 'DeleteSubpath' OR f.name CONTAINS 'CascadeDeleteSql')
              AND (f.file_path CONTAINS 'Database/' OR f.file_path CONTAINS 'Client')
              AND NOT coalesce(f.is_test, false)
            RETURN f
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
        Target = "{lit.value}",
        Message = "Illegal hardcoded customer/domain literal in core algorithm: {lit.value}",
        Query = """
            MATCH (caller)-[:PASSES_LITERAL]->(lit:CallLiteral)
            WHERE (caller.file_path CONTAINS 'Core/Common/' OR caller.file_path CONTAINS 'Core/Analysis/' OR caller.file_path CONTAINS 'Core/Parser/')
              AND lit.value IN ['dynadot_base_url_v3', 'google-pub-sub-network', 'pub-sub-network', 'antifraud', 'rtb']
              AND NOT coalesce(caller.is_test, false)
            RETURN caller, lit
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
        Description = "Enforces strict unidirectional layering across the ontology tiers: Layer 1 (File Structure) -> Layer 2 (Project Structure) -> Layer 3 (Syntax Structure) -> Layer 4 (Semantic Structure) -> Layer 5 (Logical Structure). Lower-layer models must never reference or depend on higher-order ontology layers (prohibits reverse/backward coupling).",
        Remediation = "Remove higher-layer references from lower-layer ontology models. Use downward relationships (e.g. higher-order models referencing lower-order models via DECLARED_IN, LOCATED_IN, EXPOSED_BY) or abstract contracts.",
        Message = "Ontology layer violation: Lower layer model {src.name} references higher layer model {tgt.name}",
        Query = """
            MATCH (src:Type)-[r:CALLS|USES_TYPE]->(tgt:Type)
            WHERE NOT coalesce(src.is_test, false)
              AND (
                (src.path CONTAINS 'Common/Nodes/Layer1_' AND (tgt.path CONTAINS 'Common/Nodes/Layer2_' OR tgt.path CONTAINS 'Common/Nodes/Layer3_' OR tgt.path CONTAINS 'Common/Nodes/Layer4_' OR tgt.path CONTAINS 'Common/Nodes/Layer5_'))
                OR (src.path CONTAINS 'Common/Nodes/Layer2_' AND (tgt.path CONTAINS 'Common/Nodes/Layer3_' OR tgt.path CONTAINS 'Common/Nodes/Layer4_' OR tgt.path CONTAINS 'Common/Nodes/Layer5_'))
                OR (src.path CONTAINS 'Common/Nodes/Layer3_' AND (tgt.path CONTAINS 'Common/Nodes/Layer4_' OR tgt.path CONTAINS 'Common/Nodes/Layer5_'))
                OR (src.path CONTAINS 'Common/Nodes/Layer4_' AND tgt.path CONTAINS 'Common/Nodes/Layer5_')
              )
            RETURN src, r, tgt
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
        Message = "Ontology cohesion violation: Model {src.name} implements/uses ontology contract outside Common/Nodes",
        Query = """
            MATCH (src:Type)-[r:IMPLEMENTS|USES_TYPE]->(contract:Type)
            WHERE contract.name IN ['IOntologyNode', 'CompositeNode']
              AND NOT coalesce(src.is_test, false)
              AND NOT src.path CONTAINS 'Common/Nodes/'
              AND NOT src.name IN ['IOntologyNode', 'CompositeNode', 'OntologyAttributes', 'OntologyEdgeAttribute', 'OntologyPropertyAttribute', 'OntologyNodeAttribute', 'OntologyRelationshipAttribute', 'OntologyRegistry', 'LayerResults', 'Layer4Result']
            RETURN src, r, contract
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
        Target = "{m.data_type|string}",
        Message = "Property or field {m.name} uses stringly-typed primitive {m.data_type|string} for taxonomy/classification; use an enum or strongly-typed value object.",
        Query = """
            MATCH (m:Member)
            WHERE m.kind IN ['property', 'field']
              AND (m.name = 'Type' OR m.name ENDS WITH 'Type' OR
                   m.name = 'Kind' OR m.name ENDS WITH 'Kind' OR
                   m.name = 'Category' OR m.name ENDS WITH 'Category')
              AND m.data_type IN ['string', 'String', 'string?', 'String?']
              AND NOT (coalesce(m.file_path, '') CONTAINS 'Test' OR coalesce(m.file_path, '') CONTAINS 'test')
            RETURN m
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
        Target = "{n.label}",
        Message = "Workload node {n.name} has invalid entity type, misclassified test project, or kind mismatch with label {n.label}",
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
            RETURN n
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
        Target = "{d.engine|unknown}",
        Message = "Abstract engine-only database node {d.name} ({d.id}) is not associated with a concrete schema or domain database",
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
            RETURN d
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
        Message = "Unsafe function declared outside approved FFI/sys boundary: {fn.name}",
        Query = """
            MATCH (fn:Function)-[:DECLARED_IN]->(f:File)<-[:CONTAINS]-(p:Project)
            WHERE fn.is_unsafe = true
              AND NOT toLower(coalesce(p.path, p.name, '')) CONTAINS "sys"
              AND NOT toLower(coalesce(p.path, p.name, '')) CONTAINS "native"
              AND NOT toLower(coalesce(p.path, p.name, '')) CONTAINS "ffi"
              AND NOT coalesce(fn.is_test, false)
            RETURN fn, f, p
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
        Message = "Domain project violates Clean Architecture by depending on infrastructure crate: {pkg.name}",
        Query = """
            MATCH (p:Project)-[r:DEPENDS_ON]->(pkg:Package)
            WHERE (toLower(p.name) CONTAINS "domain" OR toLower(p.name) CONTAINS "core")
              AND pkg.name IN ["axum", "actix-web", "rocket", "sqlx", "diesel", "sea-orm"]
            RETURN p, r, pkg
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
        Message = "Async function '{fn.name}' invokes blocking call: '{target.name}'",
        Query = """
            MATCH (fn:Function)-[r:CALLS]->(target)
            WHERE (fn.is_async = 'true' OR fn.is_async = true)
              AND (
                target.full_name IN [
                  "std::thread::sleep", "std::fs::read", "std::fs::write",
                  "std::fs::read_to_string", "reqwest::blocking"
                ]
                OR target.name IN [
                  "std::thread::sleep", "thread::sleep", "std::fs::read", "fs::read",
                  "std::fs::write", "fs::write", "std::fs::read_to_string",
                  "fs::read_to_string", "read_to_string"
                ]
                OR (target.name CONTAINS 'blocking' AND NOT target.name CONTAINS 'non_blocking' AND NOT target.name CONTAINS 'spawn_blocking')
              )
              AND NOT coalesce(fn.is_test, false)
            RETURN fn, r, target
            
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
        Message = "Dynamic string formatting detected in SQL query: {lit.text|format!}",
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
            RETURN fn, lit, tbl
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
        Target = "GodClass",
        Message = "God class detected: {t.name} has {loc} LOC and {method_count} methods (max allowed: 1000 LOC / 30 methods)",
        Query = """
            MATCH (t:Type)
            WHERE coalesce(t.kind, 'class') IN ['class', 'struct']
              AND NOT coalesce(t.is_test, false)
              AND NOT (coalesce(t.path, t.file_path, '') CONTAINS 'Test' OR coalesce(t.path, t.file_path, '') CONTAINS 'test')
            OPTIONAL MATCH (t)-[:HAS_METHOD]->(m:Function)
            WITH t, count(m) AS method_count, coalesce(t.loc, t.end_line - t.start_line + 1) AS loc
            WHERE loc > 1000 OR method_count > 30
            RETURN t, loc, method_count
            """,
        Ratchet = new GateRatchetSettings
        {
            Enabled = true,
            Baseline = "baselines/SIG-C-R-001.json"
        },
        Source = "BuiltIn"
    };
}
