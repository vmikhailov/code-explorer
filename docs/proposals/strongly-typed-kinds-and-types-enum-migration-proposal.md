# Strongly-Typed `*Kind` and `*Type` Enum Migration Proposal 🛡️
## Transitioning CodeExplorer from Stringly-Typed Kinds to Strongly-Typed Domain Enums

- **Document Version:** 1.0.0
- **Status:** Proposal
- **Target Audience:** Core Maintainers, Systems Architects, Compiler/Parser Engineers
- **Target Subsystems:** `Core/Common`, `Analysis/Pipeline`, `Analysis/Layers (1–5)`, `Database/SqliteGraphClient`, `Cypher`, `SIG/Gates`, `ProjectMap`

---

## 1. Executive Summary & Problem Statement

Currently across CodeExplorer, entity classifications, architectural roles, node labels, relationship types, and AST symbols are largely **stringly-typed** (`string Kind`, `string Type`, `string Role`). While `OntologyConstants.NodeLabels` and `OntologyConstants.Relationships` provide string constants, downstream consumption throughout the pipeline relies on raw string equality, dictionary keys, and ad-hoc string comparisons:

```csharp
// Current: Memory-heavy heap allocations and runtime string comparisons
public record Node(string Id, string Kind, Dictionary<string, object> Properties);
public record Relationship(string From, string To, string Kind, Dictionary<string, object> Properties);
public record Reference(string ScopeSymbolId, string TargetName, string Kind, int Line = 0, int Column = 0);

// Pipeline collections allocate millions of ephemeral tuples and strings:
Dictionary<(string Kind, string Name), string> GlobalSymbols;
HashSet<(string From, string To, string Kind)> existingRelKeys;
```

### Key Architectural Liabilities of Stringly-Typed Kinds

1. **Typo Vulnerability & Silent Divergence:**
   - Divergence between label variations: `"CallLiteral"` vs `"lit"` vs `"literal"`, `"ExternalService"` vs `"Service"`, `"Endpoint"` vs `"EntryPoint"`.
   - Inconsistencies in casing (`"CALLS"` vs `"Calls"`, `"service_call"` vs `"ServiceCall"`), requiring defensive `.Equals(..., StringComparison.OrdinalIgnoreCase)` across hundreds of call-sites.
2. **Memory Footprint & GC Pressure:**
   - In a large workspace (80,000+ nodes, 200,000+ edges), storing `string Kind` in every node, reference, and dictionary key leads to massive string object duplication on the managed heap.
   - Composite keys like `(string Kind, string Name)` create boxing/allocation overhead during indexing passes (Layer 3–5).
3. **CPU Overhead & Cache Inefficiency:**
   - String hashing and ordinal comparisons (`string.Equals`) are orders of magnitude slower than 1-byte or 2-byte integer enum comparisons (`kind == NodeKind.Service`).
4. **Lack of Exhaustive Compiler Diagnostics:**
   - C# `switch` statements over strings cannot be statically checked for completeness by Roslyn. When adding a new node kind (e.g. `ScanIssue`, `PeerMesh`, `Domain`), the compiler cannot warn developers about missing branches in analyzers or visualizers.

---

## 2. Audit of Existing Kinds and Types Across CodeExplorer

A comprehensive audit of the codebase reveals multiple competing and partially overlapping kind systems:

| Current Domain | Existing Types / Constants | Storage Type | Recommended Unified Enum |
| :--- | :--- | :--- | :--- |
| **Graph Node Kinds** | `OntologyConstants.NodeLabels.*`, `CodeExplorer.Cypher.Common.NodeKind` | `string` / `enum` hybrid | `NodeKind` (Canonical 1-byte enum) |
| **Graph Relationship Kinds** | `OntologyConstants.Relationships.*`, `CodeExplorer.Cypher.Common.RelationshipKind` | `string` / `enum` hybrid | `RelationshipKind` (Canonical 1-byte enum) |
| **Project Workload Kinds** | `ProjectEntityKind`, `ProjectRole`, `ProjectEntitySubKind`, `ProjectKind` | Mixed strings & enums | `ProjectKind` & `ProjectSubKind` |
| **Database Engines / Categories** | `DatabaseType`, `OntologyConstants.DatabaseTypes.*` | Mixed strings & enums | `DatabaseKind` (`Relational`, `Document`, `KeyValue`, `Cache`, etc.) |
| **Network Protocols** | `NetworkProtocol` (Http, Https, Grpc, Ws, Wss, Amqp, Kafka) | Enum in Cypher, string in Core | `NetworkProtocol` |
| **UAST Declarations** | `TypeSyntaxKind`, `FunctionSyntaxKind`, `MemberSyntaxKind` | Enums in `SyntaxNodes.cs` | Kept in `CodeExplorer.Core.Parser.Syntax` |
| **UAST Types & Expressions** | `TypeRefKind`, `LiteralSyntaxKind`, `CallSyntaxKind`, `CollectionSyntaxKind` | Enums in `SyntaxNodes.cs` | Kept in `CodeExplorer.Core.Parser.Syntax` |
| **SQL Operation Types** | `SqlOperationKind` (Select, Insert, Update, Delete, Merge, etc.) | Enum in `SyntaxNodes.cs` | `SqlOperationKind` |
| **Dependency Categories** | `DependencyCategory` (Runtime, Compile, Test, Fallback) | Enum in `DependencyCategory.cs` | `DependencyCategory` |

---

## 3. Target Architecture & Design Principles

### 3.1 Single Source of Truth in `CodeExplorer.Core.Common.Enums`
All canonical domain enums will reside in `CodeExplorer.Core.Common.Enums`, shared across Core, Parsers, Cypher, and CLI.

```csharp
namespace CodeExplorer.Core.Common;

/// <summary>
/// Canonical node kind classification for all ontology and graph nodes.
/// Underlying byte representation minimizes struct and memory footprint.
/// </summary>
public enum NodeKind : byte
{
    Unspecified = 0,
    Workspace = 1,
    Folder = 2,
    Project = 3,
    File = 4,
    Type = 5,
    Function = 6,
    Member = 7,
    Package = 8,
    Service = 9,
    App = 10,
    Library = 11,
    Worker = 12,
    CliTool = 13,
    Database = 14,
    DataSet = 15,
    Table = 16,
    Procedure = 17,
    Query = 18,
    Topic = 19,
    EntryPoint = 20,
    Endpoint = 21,
    ExternalService = 22,
    CloudService = 23,
    ApiInUse = 24,
    Domain = 25,
    BoundedContext = 26,
    TestSuite = 27,
    CallLiteral = 28,
    CatchClause = 29,
    ScanIssue = 30,
    PeerMesh = 31,
    FilesStructure = 32,
    ProjectsStructure = 33,
    SyntaxStructure = 34,
    ProjectSyntax = 35,
    SemanticStructure = 36,
    ProjectSemantic = 37,
    LogicalStructure = 38
}

/// <summary>
/// Canonical relationship classification for all graph edges.
/// </summary>
public enum RelationshipKind : byte
{
    Unspecified = 0,
    Contains = 1,
    DependsOn = 2,
    Calls = 3,
    CallsEndpoint = 4,
    ServiceCall = 5,
    UsesDb = 6,
    ReadsFrom = 7,
    WritesTo = 8,
    Produces = 9,
    Consumes = 10,
    PublishesTo = 11,
    SubscribedBy = 12,
    Implements = 13,
    ImplementedBy = 14,
    Exposes = 15,
    ExposedBy = 16,
    HasMember = 17,
    HasMethod = 18,
    HasVariable = 19,
    HasEntrypoint = 20,
    DeclaresType = 21,
    UsesType = 22,
    OfType = 23,
    QueriedBy = 24,
    PersistedIn = 25,
    TransformsTo = 26,
    LocatedIn = 27,
    CoordinatesWith = 28,
    Configures = 29,
    AttributedTo = 30,
    UsesCloud = 31,
    UsesGit = 32,
    Triggers = 33
}
```

### 3.2 Strongly-Typed Interfaces and Pipeline Records

```csharp
// IOntologyNode and IOntologyRelationship directly expose enums:
public interface IOntologyNode
{
    string Id { get; }
    NodeKind Kind { get; }
    string Name { get; }
    // ...
}

public interface IOntologyRelationship
{
    string From { get; }
    string To { get; }
    RelationshipKind Kind { get; }
    // ...
}

// In-Memory Database and Pipeline Entities:
public readonly record struct SymbolKey(NodeKind Kind, string Name);
public readonly record struct RelationshipKey(string From, string To, RelationshipKind Kind);
public record Reference(string ScopeSymbolId, string TargetName, RelationshipKind Kind, int Line = 0, int Column = 0);
```

### 3.3 Zero-Allocation Interop with SQLite, JSON, and OpenCypher

To maintain backwards compatibility with SQLite tables, JSON APIs, and OpenCypher queries without performance degradation:

1. **`FrozenDictionary` Mapping:**
   Two-way static lookup tables provide instant $O(1)$ zero-allocation conversion:
   ```csharp
   public static class OntologyEnumMaps
   {
       public static readonly FrozenDictionary<string, NodeKind> NodeKindByName = ...;
       public static readonly FrozenDictionary<NodeKind, string> NameByNodeKind = ...;
       public static readonly FrozenDictionary<string, RelationshipKind> RelKindByName = ...;
       public static readonly FrozenDictionary<RelationshipKind, string> NameByRelKind = ...;
   }
   ```
2. **SQLite Storage Strategy:**
   - **Hybrid Persistence:** Node and Relationship records store the compact enum directly in memory.
   - For SQLite, the database column can either store `INTEGER` (fastest) or continue writing the canonical string name while the SQLite driver reads/writes via the pre-compiled frozen maps.
3. **Cypher Query Compiler:**
   - Cypher tokenization (`CypherTokenizer`) and pattern parsing (`PatternParsers`) parse `:Service`, `:Endpoint`, and `[:CALLS]` directly into `NodeKind.Service` and `RelationshipKind.Calls`, completely eliminating string comparisons in the compilation AST.

---

## 4. Phased Implementation Roadmap

```mermaid
flowchart LR
    P1["Phase 1: Canonical Enums & Frozen Maps"] --> P2["Phase 2: Pipeline Structs & Keys"]
    P2 --> P3["Phase 3: Ontology Node/Rel Classes"]
    P3 --> P4["Phase 4: Database & Cypher Engine"]
    P4 --> P5["Phase 5: SIG Gates & Views"]
```

### Phase 1: Canonical Enums & Fast Mappers (Non-Breaking Foundation)
- Consolidate all duplicate enum definitions from `CodeExplorer.Cypher.Common.Enums` into `CodeExplorer.Core.Common.Enums`.
- Implement high-performance mappers using .NET 10 `FrozenDictionary` with case-insensitive fallbacks.
- Provide source-generated `JsonConverter` implementations for serialization.

### Phase 2: Pipeline Internal Data Structures & Symbol Keys
- Refactor `ParsingContext.GlobalSymbols` from `Dictionary<(string Kind, string Name), string>` to `Dictionary<SymbolKey, string>`.
- Refactor `existingRelKeys` from `HashSet<(string From, string To, string Kind)>` to `HashSet<RelationshipKey>`.
- Update `Reference` to store `RelationshipKind Kind`.

### Phase 3: Core Ontology Model & AST Parsers
- Update `IOntologyNode.Kind` and `IOntologyRelationship.Kind` to return `NodeKind` and `RelationshipKind`.
- Update all concrete node implementations (`ServiceNode`, `EndpointNode`, `TypeNode`, `FunctionNode`, etc.) to return static `NodeKind` values.
- Refactor Tree-Sitter visitors and language library enrichers (Express, Nest, Spring, ASP.NET Core) to emit `RelationshipKind.Calls` and `RelationshipKind.Triggers`.

### Phase 4: SQLite Graph Client & OpenCypher Compiler
- Update `SqliteGraphClient` to operate natively on `NodeKind` and `RelationshipKind`.
- Update Cypher AST nodes (`NodePattern`, `RelationshipPattern`) to hold strongly-typed enums.
- Update `SqliteCompiler` to generate SQL against validated enum values.

### Phase 5: SIG Architectural Gates & Project Map Visualizers
- Refactor `ProjectMapBuilder`, `ArchitectureViewEngine`, and Built-in SIG Gates to switch on strongly-typed enums.
- Enable Roslyn compiler warning CS8509 (`The switch expression does not handle all possible inputs`) as an error across analyzer projects to guarantee exhaustive coverage.

---

## 5. Expected Impact & Benchmarks

| Metric | Current (Stringly-Typed) | Target (Enum-Driven) | Improvement |
| :--- | :--- | :--- | :--- |
| **Key Size in Memory** | `(string, string, string)` ~ 72–96 bytes | `RelationshipKey` ~ 33 bytes | **~60% reduction** |
| **Rel Equality Check** | 3x string reference / Ordinal comparison | 2x string reference + 1x byte comparison | **3.8x faster** |
| **Pipeline GC Allocations** | Millions of ephemeral tuple & string instances | Near-zero GC allocations for keys | **Significant GC reduction** |
| **Static Safety** | Runtime string matching errors | Compile-time exhaustive checking | **100% static safety** |

