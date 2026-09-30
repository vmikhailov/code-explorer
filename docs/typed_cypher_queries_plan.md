# Feature Plan: Strongly-Typed Cypher Queries (`QueryAsync<T>`)

## 1. Problem Statement
Currently, `IGraphClient.ExecuteQueryAsync` returns a serialized JSON `string`. Callers across the codebase (e.g. `PostIndexAnalyzer`, `ArchitectureViewEngine`, `DiagramExporter`, MCP handlers) repeatedly:
1. Box values into `Dictionary<string, object?>`.
2. Serialize them into a JSON text string (`JsonSerializer.Serialize`).
3. Parse the JSON string back into a `JsonDocument` (`JsonDocument.Parse`).
4. Manually extract properties with repetitive boilerplate (`row.TryGetProperty("name", out var pt) ? pt.GetString() : null`).

This causes severe GC pressure, memory allocations on large graphs, slower query execution, and lack of compile-time type safety.

---

## 2. Proposed Architecture & Design

### A. Core Interfaces (`IGraphClient`)
Add strongly-typed query overloads:
```csharp
public interface IGraphClient : IAsyncDisposable
{
    // Existing:
    Task<string> ExecuteQueryAsync(string query, object? parameters = null, CancellationToken cancellationToken = default);

    // New strongly-typed overloads:
    Task<List<T>> QueryAsync<T>(string cypher, object? parameters = null, CancellationToken cancellationToken = default);
    Task<T?> QuerySingleOrDefaultAsync<T>(string cypher, object? parameters = null, CancellationToken cancellationToken = default);
    Task<T> QueryScalarAsync<T>(string cypher, object? parameters = null, CancellationToken cancellationToken = default);
    
    // LINQ-style row selector overload:
    Task<List<T>> QueryAsync<T>(string cypher, Func<IDataRecord, T> selector, object? parameters = null, CancellationToken cancellationToken = default);
}
```

### B. High-Performance Reader Materializer (`SqliteEntityMapper<T>`)
- Implement a fast mapper from `SqliteDataReader` to `T`:
  - **Primitives / Scalars**: If `typeof(T).IsPrimitive || typeof(T) == typeof(string) || typeof(T) == typeof(DateTime)`: read column 0 directly via `reader.GetFieldValue<T>(0)`.
  - **Records / Classes**: Inspect public constructor parameters or writable properties once and compile an expression delegate `(SqliteDataReader r) => new T(...)` or use cached property setters.
  - **Graph Entities (`Node`, `Relationship`)**: Directly hydrate entity records from columns without unneeded roundtrips.
- Cache compiled mappers in `ConcurrentDictionary<Type, Delegate>` to achieve Dapper-level performance.

### C. Refactoring Target Areas
1. **`PostIndexAnalyzer.cs`**:
   Replace manual `JsonDocument.Parse` blocks (e.g. lines 2004–2035, 2252–2275) with:
   ```csharp
   var projects = await db.QueryAsync<ProjectInfo>("MATCH (p:Project) RETURN p.id AS Id, p.name AS Name, p.path AS Path", ct: cancellationToken);
   var edges = await db.QueryAsync<EdgeRow>("MATCH (src)-[r]->(tgt) WHERE r.kind IN ['USES_DB', 'CONFIGURES', 'DEPENDS_ON'] RETURN src.id AS Source, tgt.id AS Target, r.kind AS Kind", ct: cancellationToken);
   ```
2. **`ArchitectureViewEngine.cs`**:
   Eliminate JSON parsing in cross-project dependency resolution and component grouping.
3. **`CodeExplorerRepository.cs` (MCP Layer)**:
   Convert symbol lookups, project listings, and lineage inspections to direct typed record returns.

---

## 3. Milestones
- [ ] **M1: Mapper Engine**: Implement `SqliteEntityMapper` with support for records, classes, and scalars with cached delegate compilation.
- [ ] **M2: Client Methods**: Add `QueryAsync<T>`, `QuerySingleOrDefaultAsync<T>`, and `QueryScalarAsync<T>` to `IGraphClient` and `SqliteGraphClient`.
- [ ] **M3: Unit Tests**: Add test suite verifying mapping of records, nullable fields, case-insensitive column matching, and primitives.
- [ ] **M4: Migration**: Refactor `PostIndexAnalyzer.cs` and `ArchitectureViewEngine.cs` to use typed queries.
