# CodeExplorer Decoupled Graph Ontology Specification

This document defines the taxonomy, nodes, properties, and relationships collected by CodeExplorer. The graph is organized into **four decoupled buckets** linked by reference pointers, rather than a single deeply nested physical hierarchy.

```mermaid
graph TD
    subgraph FilesStructure [1. FilesStructure (Physical)]
        Folder[Folder] -->|CONTAINS_FILE| File[File]
    end

    subgraph ClassStructure [2. ClassStructure (Syntactic)]
        Project[Project] -->|DECLARES_TYPE| Type[Type]
        Type -->|HAS_METHOD| Function[Function]
        Type -->|HAS_MEMBER| Member[Member]
        Function -->|HAS_VARIABLE| Member
    end

    subgraph SemanticStructure [3. SemanticStructure (Runtime Interfaces)]
        Endpoint[Endpoint]
        Database[Database]
        Topic[Topic]
        EntryPoint[EntryPoint]
    end

    %% Cross-Bucket Links (SystemBindings)
    File -.->|Reference Pointer| Project
    Type -.->|DECLARED_IN| File
    Function -.->|DECLARED_IN| File
    
    %% Semantic Bindings
    Function -->|EXPOSES_ENDPOINT| Endpoint
    Function -->|QUERIES_DB| Database
    Function -->|PUBLISHES_TO| Topic
    Function -->|SUBSCRIBES_TO| Topic
    Function -.->|CALLS_ENDPOINT| Endpoint
```

---

## 1. FilesStructure (Physical Topology)

The physical structure tracks the exact directory layout of the workspace on disk. It is decoupled from the syntactic structures, meaning it is only updated when folders or files are created, renamed, or deleted.

### Nodes
*   **`Folder`**
    *   *Description:* A physical directory in the workspace.
    *   *Properties:*
        *   `id` (string): Absolute directory path.
        *   `name` (string): The folder name.
        *   `path` (string): Absolute filesystem path.
*   **`File`**
    *   *Description:* A source code document or data query script.
    *   *Properties:*
        *   `id` (string): Absolute file path.
        *   `name` (string): Filename basename with extension.
        *   `path` (string): Absolute filesystem path.
        *   `language` (string): Code language (`csharp`, `go`, `python`, `typescript`, `sql`).
        *   `hash` (string): MD5/SHA hash of the file contents to verify edit status.

### Relationships
*   `Folder -[CONTAINS_FILE]-> File`
*   `Folder -[CONTAINS_FOLDER]-> Folder`

---

## 2. ClassStructure (Syntactic Code Models)

The syntactic structure stores declarations found within the AST. By isolating this bucket, we can surgically drop and rebuild AST nodes for a single modified file without touching the rest of the database.

### Nodes
*   **`Project`**
    *   *Description:* A compilation unit or package boundary (e.g. C# `.csproj`, Go module, TypeScript `package.json`).
    *   *Properties:*
        *   `id` (string): Unique project path/name.
        *   `name` (string): Project name.
        *   `language` (string): Core language type.
        *   `path` (string): Absolute path to the project file/directory.
*   **`Type`**
    *   *Description:* Unified class, interface, struct, record, or enum declarations.
    *   *Properties:*
        *   `id` (string): Fully qualified type symbol.
        *   `name` (string): Unqualified name of the type.
        *   `kind` (string): Specifier (`class`, `interface`, `struct`, `record`, `enum`, `union`).
        *   `file_path` (string): Absolute path to the declaring file (cached for instant lookup).
        *   `start_line` / `end_line` (int): Source code bounds.
*   **`Function`**
    *   *Description:* Methods, constructors, or free-floating functions.
    *   *Properties:*
        *   `id` (string): Fully qualified method/function symbol.
        *   `name` (string): Unqualified name of the function.
        *   `signature` (string): Method parameter and return type signature.
        *   `return_type` (string): Declared return type.
        *   `file_path` (string): Absolute path to the declaring file (cached).
        *   `start_line` / `end_line` (int): Source code bounds.
*   **`Member`**
    *   *Description:* Fields, properties, method parameters, or local variables.
    *   *Properties:*
        *   `id` (string): Fully qualified member symbol.
        *   `name` (string): Member name.
        *   `type_name` (string): Declared type name.
        *   `kind` (string): Specifier (`field`, `property`, `parameter`, `variable`).
        *   `start_line` / `end_line` (int): Source code bounds.

### Relationships
*   `Project -[DECLARES_TYPE]-> Type`
*   `Type -[HAS_METHOD]-> Function`
*   `Type -[HAS_MEMBER]-> Member`
*   `Function -[HAS_VARIABLE]-> Member` (For local variables or parameters inside a function scope)
*   `Type -[DECLARED_IN]-> File` (Link pointing back to FilesStructure)
*   `Function -[DECLARED_IN]-> File` (Link pointing back to FilesStructure)

---

## 3. SemanticStructure (Runtime System Map)

The semantic model represents the entry points and external targets of the system, allowing visual mapping of services and microservice architecture.

### Nodes
*   **`Endpoint`**
    *   *Description:* An exposed API route.
    *   *Properties:*
        *   `id` (string): HTTP method + route template.
        *   `http_method` (string): HTTP verb (`GET`, `POST`, `PUT`, `DELETE`).
        *   `route_template` (string): API route path.
*   **`Database`**
    *   *Description:* A physical data store instance or schema.
    *   *Properties:*
        *   `id` (string): Unique database/schema name.
        *   `name` (string): Database name.
        *   `db_type` (string): Database system type (`sqlserver`, `postgres`, `sqlite`, `mongodb`).
*   **`Topic`**
    *   *Description:* Message broker pub/sub queues and exchanges.
    *   *Properties:*
        *   `id` (string): Topic/Queue name.
        *   `name` (string): Topic name.
        *   `broker_type` (string): Broker engine (`rabbitmq`, `kafka`, `sqs`, `in-memory`).
*   **`EntryPoint`**
    *   *Description:* General runtime entry triggers (e.g. gRPC stubs, CLI commands, background processes).
    *   *Properties:*
        *   `id` (string): Unique identifier.
        *   `entry_type` (string): Specifier (`grpc`, `cli`, `cron`).

---

## 4. SystemBindings (Cross-Project Integration & References)

System bindings represent cross-cutting connections that link the physical, syntactic, and semantic models together.

### Intra-Project Relationships (Layer 3 Semantics)
*   `Type -[INHERITS_FROM]-> Type` (Inheritance / Interface implementation)
*   `Member -[OF_TYPE]-> Type` (Links a property/field to its concrete type node)
*   `Function -[CALLS]-> Function` (Direct compiler-resolved function call)
*   `Function -[USES_TYPE]-> Type` (Type references, instances, generics)

### Inter-Project Relationships (Layer 4 System Integration)
*   `Function -[EXPOSES_ENDPOINT]-> Endpoint` (API Ingress)
*   `Function -[CALLS_ENDPOINT]-> Endpoint` (API Egress)
*   `Function -[QUERIES_DB]-> Database` (Data Access Lineage)
*   `Function -[PUBLISHES_TO]-> Topic` (Asynchronous Publishing)
*   `Function -[SUBSCRIBES_TO]-> Topic` (Asynchronous Subscription)

---

## 5. Node URN / ID Schemes

To guarantee uniqueness across multi-project workspaces and multiple database instances, CodeExplorer uses structured Uniform Resource Names (URNs) for node IDs, prefixed by an auto-incremented workspace identifier (`{workspaceId}`). 

| Node Label | ID / URN Scheme | Uniqueness Scope | Example |
| :--- | :--- | :--- | :--- |
| **`Folder`** | `{workspaceId}:folder:{absoluteFolderPath}` | Workspace | `1:folder:/Work/Personal/code-explorer/Core` |
| **`File`** | `{workspaceId}:file:{absoluteFilePath}` | Workspace | `1:file:/Work/Personal/code-explorer/Core/Registry.cs` |
| **`Project`** | `{workspaceId}:project:{absoluteProjectPath}` | Workspace | `1:project:/Work/Personal/code-explorer/UI/UI.csproj` |
| **`Type`** | `{workspaceId}:symbol:{file_path}:Type:{name}:{line}` | File | `1:symbol:/Core/Registry.cs:Type:OntologyRegistry:12` |
| **`Function`** | `{workspaceId}:symbol:{file_path}:Function:{name}:{line}` | File | `1:symbol:/Core/Registry.cs:Function:Register:24` |
| **`Member`** | `{workspaceId}:symbol:{file_path}:Member:{name}:{line}` | File | `1:symbol:/Core/Registry.cs:Member:KindMapping:15` |
| **`Endpoint`** | `{workspaceId}:endpoint:{http_method}:{route}` | Workspace | `1:endpoint:POST:/api/v1/users` |
| **`Database`** | `{workspaceId}:db:{db_type}:{name}` | Workspace | `1:db:sqlserver:orders_db` |
| **`Topic`** | `{workspaceId}:topic:{broker_type}:{name}` | Workspace | `1:topic:kafka:order-created` |
