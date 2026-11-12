<!-- AUTO-GENERATED — do not edit manually. Re-generated on every build by OntologyGen. -->

# CodeExplorer Ontology

> This document is generated from source annotations during the build.
> Edit the `[OntologyNode]`, `[OntologyEdge<>]`, `[OntologyProperty]`, and `[OntologyRelationship]` attributes in the source files to update it.

---

## 📊 Architectural Overview (Mermaid Diagram)

```mermaid
graph TD
    Workspace["Workspace"]

    subgraph Layer1 ["Layer 1: File Structure"]
        Counter["Counter"]
        File["File"]
        FilesStructure["FilesStructure"]
        Folder["Folder"]
        GitSettings["GitSettings"]
        ScanIssue["ScanIssue"]
    end

    subgraph Layer2 ["Layer 2: Project Structure"]
        EntryPoint["EntryPoint"]
        Package["Package"]
        Project["Project"]
        ProjectsStructure["ProjectsStructure"]
    end

    subgraph Layer3 ["Layer 3: Syntax Structure"]
        CallArgument["CallArgument"]
        CallLiteral["CallLiteral"]
        CatchClause["CatchClause"]
        Function["Function"]
        Member["Member"]
        Parameter["Parameter"]
        ProjectSyntax["ProjectSyntax"]
        SyntaxStructure["SyntaxStructure"]
        Type["Type"]
    end

    subgraph Layer4 ["Layer 4: Semantic Structure"]
        ApiInUse["ApiInUse"]
        App["App"]
        CliTool["CliTool"]
        CloudService["CloudService"]
        Database["Database"]
        DataSet["DataSet"]
        Endpoint["Endpoint"]
        ExternalService["ExternalService"]
        Library["Library"]
        PeerMesh["PeerMesh"]
        Procedure["Procedure"]
        Query["Query"]
        SemanticStructure["SemanticStructure"]
        Service["Service"]
        Table["Table"]
        TestSuite["TestSuite"]
        Topic["Topic"]
        Worker["Worker"]
    end

    subgraph Layer5 ["Layer 5: Logical Structure"]
        BoundedContext["BoundedContext"]
        Domain["Domain"]
        LogicalStructure["LogicalStructure"]
    end

    ApiInUse -->|DECLARED_IN| File
    ApiInUse -->|ACCESSED_BY| Function
    ApiInUse -->|ACCESSED_BY| Type
    App -->|IMPLEMENTED_BY| Project
    App -->|DEPENDS_ON| Library
    App -->|DEPENDS_ON| Package
    App -->|SERVICE_CALL| Service
    App -->|SERVICE_CALL| ExternalService
    App -->|USES_DB| Database
    App -->|PUBLISHES_TO| Topic
    App -->|SUBSCRIBES_TO| Topic
    App -->|USES_CLOUD| CloudService
    App -->|READS_FROM| CloudService
    App -->|WRITES_TO| CloudService
    App -->|COORDINATES_WITH| PeerMesh
    App -->|EXPOSES| Endpoint
    App -->|CONTAINS| Endpoint
    App -->|CONTAINS| ApiInUse
    App -->|USES_API| ApiInUse
    App -->|CALLED_BY| Function
    BoundedContext -->|DEPENDS_ON| BoundedContext
    BoundedContext -->|CONTAINS| Service
    BoundedContext -->|CONTAINS| App
    BoundedContext -->|CONTAINS| Worker
    BoundedContext -->|CONTAINS| Library
    BoundedContext -->|CONTAINS| CliTool
    BoundedContext -->|CONTAINS| Database
    BoundedContext -->|CONTAINS| Topic
    BoundedContext -->|DEFINES| Type
    CallArgument -->|DECLARED_IN| File
    CallArgument -->|FLOWS_TO| Parameter
    CallLiteral -->|DECLARED_IN| File
    CatchClause -->|DECLARED_IN| File
    CliTool -->|IMPLEMENTED_BY| Project
    CliTool -->|DEPENDS_ON| Library
    CliTool -->|DEPENDS_ON| Package
    CliTool -->|SERVICE_CALL| Service
    CliTool -->|SERVICE_CALL| ExternalService
    CliTool -->|PUBLISHES_TO| Topic
    CliTool -->|SUBSCRIBES_TO| Topic
    CliTool -->|USES_CLOUD| CloudService
    CliTool -->|READS_FROM| CloudService
    CliTool -->|WRITES_TO| CloudService
    CliTool -->|COORDINATES_WITH| PeerMesh
    CliTool -->|USES_DB| Database
    CliTool -->|EXPOSES| Endpoint
    CliTool -->|CONTAINS| Endpoint
    CliTool -->|USES_API| ApiInUse
    CliTool -->|CALLED_BY| Function
    CloudService -->|DECLARED_IN| File
    CloudService -->|ACCESSED_BY| Function
    CloudService -->|ACCESSED_BY| Type
    CloudService -->|TRANSITIVELY_CALLS| Function
    Database -->|DECLARED_IN| File
    Database -->|ACCESSED_BY| Function
    Database -->|ACCESSED_BY| Type
    Database -->|ACCESSED_BY| Query
    Database -->|TRANSITIVELY_CALLS| Function
    Database -->|CONTAINS| Table
    Database -->|CONTAINS| Procedure
    Database -->|CONTAINS| Query
    Database -->|TARGETED_BY| CallArgument
    DataSet -->|CONTAINS| Table
    Domain -->|CONTAINS| BoundedContext
    Endpoint -->|TRIGGERS| Function
    Endpoint -->|EXPOSED_BY| Type
    Endpoint -->|EXPOSED_BY| Function
    Endpoint -->|SUBSCRIBES_TO| Topic
    Endpoint -->|TARGETED_BY| CallArgument
    Endpoint -->|DECLARED_IN| File
    Endpoint -->|DECLARED_IN| Project
    Endpoint -->|CALLS| Function
    Endpoint -->|PASSES_LITERAL| CallLiteral
    Endpoint -->|DECLARES| CatchClause
    Endpoint -->|ATTRIBUTED_TO| Query
    Endpoint -->|ATTRIBUTED_TO| Database
    Endpoint -->|ATTRIBUTED_TO| ExternalService
    Endpoint -->|ATTRIBUTED_TO| CloudService
    EntryPoint -->|DECLARED_IN| File
    EntryPoint -->|TRIGGERS| File
    EntryPoint -->|BELONGS_TO| Project
    ExternalService -->|DECLARED_IN| File
    ExternalService -->|DECLARED_IN| Project
    ExternalService -->|CALLS_ENDPOINT| Endpoint
    ExternalService -->|CALLED_BY| Function
    ExternalService -->|TRANSITIVELY_CALLS| Function
    ExternalService -->|TARGETED_BY| CallArgument
    File -->|CONTAINS| ScanIssue
    FilesStructure -->|CONTAINS| Folder
    FilesStructure -->|CONTAINS| File
    FilesStructure -->|CONTAINS| ScanIssue
    Folder -->|CONTAINS| Folder
    Folder -->|CONTAINS| File
    Folder -->|CONTAINS| GitSettings
    Folder -->|CONTAINS| ScanIssue
    Function -->|DECLARED_IN| File
    Function -->|CALLS| Function
    Function -->|CALLS| Type
    Function -->|USES_TYPE| Type
    Function -->|INSTANTIATES| Type
    Function -->|INSTANTIATES| Function
    Function -->|INSTANTIATES| EntryPoint
    Function -->|IMPLEMENTS| EntryPoint
    Function -->|HAS_PARAMETER| Parameter
    Function -->|PASSES_ARGUMENT| CallArgument
    Function -->|PASSES_LITERAL| CallLiteral
    Function -->|DECLARES| CatchClause
    Function -->|DECLARES| Function
    Function -->|DECLARES| Member
    Function -->|DECLARES| Type
    Library -->|IMPLEMENTED_BY| Project
    Library -->|DEPENDS_ON| Library
    Library -->|DEPENDS_ON| Package
    Library -->|SERVICE_CALL| Service
    Library -->|SERVICE_CALL| ExternalService
    Library -->|USES_DB| Database
    Library -->|PUBLISHES_TO| Topic
    Library -->|SUBSCRIBES_TO| Topic
    Library -->|USES_CLOUD| CloudService
    Library -->|READS_FROM| CloudService
    Library -->|WRITES_TO| CloudService
    Library -->|COORDINATES_WITH| PeerMesh
    Library -->|EXPOSES| Endpoint
    Library -->|USES_API| ApiInUse
    Library -->|CALLED_BY| Function
    LogicalStructure -->|CONTAINS| Domain
    Member -->|DECLARED_IN| File
    Member -->|OF_TYPE| Type
    Member -->|ACCESSED_BY| Function
    Package -->|PRODUCES| Project
    Package -->|IMPLEMENTED_BY| Project
    Parameter -->|DECLARED_IN| File
    Parameter -->|FLOWS_TO| CallArgument
    Procedure -->|CONTAINS| Query
    Procedure -->|ACCESSED_BY| Function
    Procedure -->|TARGETED_BY| CallArgument
    Project -->|LOCATED_IN| Folder
    Project -->|LOCATED_IN| Workspace
    Project -->|CONTAINS| Project
    Project -->|DEPENDS_ON| Project
    Project -->|DEPENDS_ON| Package
    Project -->|PRODUCES| Package
    Project -->|CONTAINS| File
    Project -->|CONTAINS| EntryPoint
    Project -->|HAS_ENTRYPOINT| EntryPoint
    Project -->|USES_GIT| GitSettings
    ProjectsStructure -->|CONTAINS| Project
    ProjectsStructure -->|CONTAINS| EntryPoint
    ProjectSyntax -->|CONTAINS| Type
    ProjectSyntax -->|CONTAINS| Function
    ProjectSyntax -->|BELONGS_TO| Project
    Query -->|DECLARED_IN| File
    Query -->|READS_FROM| Table
    Query -->|WRITES_TO| Table
    Query -->|DEPENDS_ON| Table
    Query -->|ACCESSED_BY| Function
    Query -->|ACCESSED_BY| Type
    Query -->|TRANSITIVELY_CALLS| Function
    ScanIssue -->|DECLARED_IN| File
    SemanticStructure -->|CONTAINS| App
    SemanticStructure -->|CONTAINS| Service
    SemanticStructure -->|CONTAINS| Worker
    SemanticStructure -->|CONTAINS| Library
    SemanticStructure -->|CONTAINS| CliTool
    SemanticStructure -->|CONTAINS| TestSuite
    SemanticStructure -->|CONTAINS| Database
    SemanticStructure -->|CONTAINS| Table
    SemanticStructure -->|CONTAINS| Procedure
    SemanticStructure -->|CONTAINS| Query
    SemanticStructure -->|CONTAINS| Topic
    SemanticStructure -->|CONTAINS| ExternalService
    SemanticStructure -->|CONTAINS| CloudService
    SemanticStructure -->|CONTAINS| ApiInUse
    SemanticStructure -->|CONTAINS| Endpoint
    SemanticStructure -->|CONTAINS| PeerMesh
    Service -->|IMPLEMENTED_BY| Project
    Service -->|DEPENDS_ON| Library
    Service -->|DEPENDS_ON| Package
    Service -->|SERVICE_CALL| Service
    Service -->|SERVICE_CALL| ExternalService
    Service -->|USES_DB| Database
    Service -->|PUBLISHES_TO| Topic
    Service -->|SUBSCRIBES_TO| Topic
    Service -->|EXPOSES| Endpoint
    Service -->|CONTAINS| Endpoint
    Service -->|CONTAINS| CloudService
    Service -->|USES_CLOUD| CloudService
    Service -->|READS_FROM| CloudService
    Service -->|WRITES_TO| CloudService
    Service -->|COORDINATES_WITH| PeerMesh
    Service -->|CONTAINS| ApiInUse
    Service -->|USES_API| ApiInUse
    Service -->|CALLED_BY| Function
    SyntaxStructure -->|CONTAINS| ProjectSyntax
    Table -->|ACCESSED_BY| Function
    Table -->|ACCESSED_BY| Type
    Table -->|ACCESSED_BY| Query
    Table -->|TARGETED_BY| CallArgument
    TestSuite -->|IMPLEMENTED_BY| Project
    TestSuite -->|DEPENDS_ON| Library
    TestSuite -->|DEPENDS_ON| Package
    TestSuite -->|USES_API| ApiInUse
    TestSuite -->|TESTS| Service
    TestSuite -->|TESTS| Library
    TestSuite -->|TESTS| App
    TestSuite -->|TESTS| Worker
    TestSuite -->|TESTS| CliTool
    Topic -->|DECLARED_IN| File
    Topic -->|PUBLISHED_BY| Function
    Topic -->|SUBSCRIBED_BY| Function
    Topic -->|TARGETED_BY| CallArgument
    Type -->|DECLARED_IN| File
    Type -->|USES_TYPE| Type
    Type -->|IMPLEMENTS| Type
    Type -->|INHERITS_FROM| Type
    Type -->|POTENTIAL_TYPE| Type
    Type -->|HAS_METHOD| Function
    Type -->|HAS_MEMBER| Member
    Type -->|CALLS| Function
    Type -->|PASSES_LITERAL| CallLiteral
    Type -->|DECLARES| Type
    Worker -->|IMPLEMENTED_BY| Project
    Worker -->|DEPENDS_ON| Library
    Worker -->|DEPENDS_ON| Package
    Worker -->|SERVICE_CALL| Service
    Worker -->|SERVICE_CALL| ExternalService
    Worker -->|USES_DB| Database
    Worker -->|PUBLISHES_TO| Topic
    Worker -->|SUBSCRIBES_TO| Topic
    Worker -->|USES_CLOUD| CloudService
    Worker -->|READS_FROM| CloudService
    Worker -->|WRITES_TO| CloudService
    Worker -->|COORDINATES_WITH| PeerMesh
    Worker -->|EXPOSES| Endpoint
    Worker -->|CONTAINS| Endpoint
    Worker -->|USES_API| ApiInUse
    Worker -->|CALLED_BY| Function
    Workspace -->|CONTAINS| FilesStructure
    Workspace -->|CONTAINS| ProjectsStructure
    Workspace -->|CONTAINS| SyntaxStructure
    Workspace -->|CONTAINS| SemanticStructure
    Workspace -->|CONTAINS| LogicalStructure
```

---

## 📂 Layered Definitions

### 🌐 Root System Umbrella

#### `Workspace`

> Represents the absolute root of the workspace directory hierarchy.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `CONTAINS` | `FilesStructure` |
| `CONTAINS` | `ProjectsStructure` |
| `CONTAINS` | `SyntaxStructure` |
| `CONTAINS` | `SemanticStructure` |
| `CONTAINS` | `LogicalStructure` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `Project` | `LOCATED_IN` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the entity. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |
| `IndexedAt` | `string?` | Timestamp when the workspace was indexed (ISO 8601 UTC). |
| `PackageName` | `string?` | Primary package name declared in the workspace root manifest. |
| `PackageVersion` | `string?` | Primary package version declared in the workspace root manifest. |

---

### 📂 Layer 1: File Structure

#### `Counter`

> Represents an internal database counter used for auto-incrementing identifiers.

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the counter. |
| `Value` | `int` | The current counter value. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |

---

#### `File`

> Represents a source code file containing parsable content.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `CONTAINS` | `ScanIssue` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `ApiInUse` | `DECLARED_IN` |
| `CallArgument` | `DECLARED_IN` |
| `CallLiteral` | `DECLARED_IN` |
| `CatchClause` | `DECLARED_IN` |
| `CloudService` | `DECLARED_IN` |
| `Database` | `DECLARED_IN` |
| `Endpoint` | `DECLARED_IN` |
| `EntryPoint` | `DECLARED_IN` |
| `EntryPoint` | `TRIGGERS` |
| `ExternalService` | `DECLARED_IN` |
| `FilesStructure` | `CONTAINS` |
| `Folder` | `CONTAINS` |
| `Function` | `DECLARED_IN` |
| `Member` | `DECLARED_IN` |
| `Parameter` | `DECLARED_IN` |
| `Project` | `CONTAINS` |
| `Query` | `DECLARED_IN` |
| `ScanIssue` | `DECLARED_IN` |
| `Topic` | `DECLARED_IN` |
| `Type` | `DECLARED_IN` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the entity. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |
| `IsTest` | `bool` | Indicates whether the file is part of a test suite or test project. |
| `ProjectKind` | `ProjectEntityKind?` | The architectural kind of the enclosing project. |
| `ProjectName` | `string?` | The name of the enclosing project. |
| `LineCount` | `int?` | Total lines of code in this file. |
| `SizeBytes` | `long?` | Size of the file in bytes. |
| `ArtifactRole` | `string?` | The physical artifact role of the file (source, dist, minified, declaration, script). |

---

#### `FilesStructure`

> Represents an intermediate node grouping the physical folder and file tree of the entire workspace.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `CONTAINS` | `Folder` |
| `CONTAINS` | `File` |
| `CONTAINS` | `ScanIssue` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `Workspace` | `CONTAINS` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the entity. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |

---

#### `Folder`

> Represents a directory within the indexed workspace.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `CONTAINS` | `Folder` |
| `CONTAINS` | `File` |
| `CONTAINS` | `GitSettings` |
| `CONTAINS` | `ScanIssue` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `FilesStructure` | `CONTAINS` |
| `Folder` | `CONTAINS` |
| `Project` | `LOCATED_IN` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the folder. |
| `Path` | `string` | The path to the folder. |

---

#### `GitSettings`

> Represents the Git repository configuration settings for the workspace.

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `Folder` | `CONTAINS` |
| `Project` | `USES_GIT` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the entity. |
| `Branch` | `string` | The currently checked-out branch name. |
| `OriginUrl` | `string` | The remote origin repository URL. |
| `UserName` | `string` | The git user name. |
| `UserEmail` | `string` | The git user email address. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |
| `CommitHash` | `string?` | Current Git HEAD commit hash. |
| `HeadCommit` | `string?` | Current Git HEAD commit hash alias. |
| `RepoName` | `string?` | Git repository name. |

---

#### `ScanIssue`

> Represents a file scanning, manifest, or AST parsing issue encountered during indexing so affected files can be inspected or reparsed.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `DECLARED_IN` | `File` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `File` | `CONTAINS` |
| `FilesStructure` | `CONTAINS` |
| `Folder` | `CONTAINS` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The short summary or title of the scanning/parsing issue. |
| `Path` | `string` | The relative path of the file or folder where the issue occurred. |
| `Stage` | `string` | The indexing pipeline stage where the issue occurred (e.g. Layer1_Physical, Layer2_Boundaries, Layer3_Syntactic, Layer3_Visitor, Layer4_Semantic, Layer5_Global). |
| `Category` | `ScanIssueCategory` | The issue category (e.g. syntax_error, parse_exception, visitor_exception, manifest_error, io_error, reference_error). |
| `Severity` | `string` | The severity of the issue (error or warning). |
| `Message` | `string` | The detailed error message or syntax snippet. |
| `StartLine` | `int?` | The 1-based starting line number of the issue, if known. |
| `ExceptionType` | `string?` | The CLR exception type name if thrown. |
| `Language` | `string?` | The parser or language associated with the file. |

---

### 📂 Layer 2: Project Structure

#### `EntryPoint`

> Represents the binary, process, or bundle bootstrap entry point of an executable project (e.g. Program.Main, func main, __main__, index.tsx root mount).

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `DECLARED_IN` | `File` |
| `TRIGGERS` | `File` |
| `BELONGS_TO` | `Project` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `Function` | `INSTANTIATES` |
| `Function` | `IMPLEMENTS` |
| `Project` | `CONTAINS` |
| `Project` | `HAS_ENTRYPOINT` |
| `ProjectsStructure` | `CONTAINS` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the binary or process entry point. |
| `Path` | `string` | The path of the entry point file relative to its parent container. |
| `EntryType` | `EntryPointType` | The kind of binary/process entry point (e.g. main-method, top-level, script-main, bundle-entry). |
| `FilePath` | `string?` | Relative path of the source file declaring the entry point. |
| `StartLine` | `int?` | 1-based starting line number of the entry point. |
| `Command` | `string?` | Shell command executed by a lifecycle or task entry point. |
| `HookKind` | `string?` | Lifecycle hook classification (e.g. install, build). |
| `TaskLabel` | `string?` | Task label for VS Code folderOpen tasks. |

---

#### `Package`

> Represents an external dependency package or workspace package referenced or produced by projects.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `PRODUCES` | `Project` |
| `IMPLEMENTED_BY` | `Project` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `App` | `DEPENDS_ON` |
| `CliTool` | `DEPENDS_ON` |
| `Library` | `DEPENDS_ON` |
| `Project` | `DEPENDS_ON` |
| `Project` | `PRODUCES` |
| `Service` | `DEPENDS_ON` |
| `TestSuite` | `DEPENDS_ON` |
| `Worker` | `DEPENDS_ON` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the entity. |
| `Version` | `string` | The package version. |
| `IsInternal` | `bool` | Whether this package is produced internally by a workspace project. |
| `Ecosystem` | `string` | The package ecosystem (e.g. nuget, npm, maven, pip, go). |
| `Path` | `string` | The path of the folder or file relative to its parent container. |
| `Archetype` | `ProjectArchetype` | The architectural archetype of the package (e.g. PureUtility, ClientUi, CliTool, Framework, BackendService, DatabaseDriver, NativeAddon). |
| `DependencyScope` | `string?` | Dependency scope (e.g. runtime, dev, test, build). |
| `IsExternal` | `bool` | Whether this package is an external third-party dependency. |

---

#### `Project`

> Represents a buildable/compilable module or package directory (e.g. C# project, Go module, TS library, Python package).

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `LOCATED_IN` | `Folder` |
| `LOCATED_IN` | `Workspace` |
| `CONTAINS` | `Project` |
| `DEPENDS_ON` | `Project` |
| `DEPENDS_ON` | `Package` |
| `PRODUCES` | `Package` |
| `CONTAINS` | `File` |
| `CONTAINS` | `EntryPoint` |
| `HAS_ENTRYPOINT` | `EntryPoint` |
| `USES_GIT` | `GitSettings` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `App` | `IMPLEMENTED_BY` |
| `CliTool` | `IMPLEMENTED_BY` |
| `Endpoint` | `DECLARED_IN` |
| `EntryPoint` | `BELONGS_TO` |
| `ExternalService` | `DECLARED_IN` |
| `Library` | `IMPLEMENTED_BY` |
| `Package` | `PRODUCES` |
| `Package` | `IMPLEMENTED_BY` |
| `Project` | `CONTAINS` |
| `Project` | `DEPENDS_ON` |
| `ProjectsStructure` | `CONTAINS` |
| `ProjectSyntax` | `BELONGS_TO` |
| `Service` | `IMPLEMENTED_BY` |
| `TestSuite` | `IMPLEMENTED_BY` |
| `Worker` | `IMPLEMENTED_BY` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the entity. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |
| `ProjectType` | `ProjectLanguage` | The language/signature identifier (e.g. 'csharp', 'go', 'python', 'typescript'). |
| `Language` | `string` | The programming language (e.g. 'csharp', 'go', 'python', 'typescript'). |
| `KindValue` | `string` | The physical entity classification kind (e.g. Service, App, Worker, FunctionApp, Library, DatabaseMigration, Test). |
| `Role` | `string` | Architectural role of the project (e.g. Service, SharedLibrary, FrontendApp, Worker, CliTool, Test). |
| `Archetype` | `ProjectArchetype` | Architectural archetype of the project (e.g. PureUtility, ClientUi, CliTool, Framework, BackendService, DatabaseDriver, NativeAddon). |
| `PrimaryRole` | `string?` | Primary architectural role of the project (e.g. WebService, Scheduler, WorkerService, SharedLibrary, FrontendFramework, CliFramework, TestFramework). |
| `Framework` | `string?` | Detected primary framework of the project. |
| `EntityType` | `string?` | Normalized semantic entity type (app, service, worker, cli, test, library). |
| `ExternalApis` | `string?` | Comma-separated external APIs or SDKs used by this project. |
| `PackageName` | `string?` | Package name declared in the project manifest. |
| `PackageVersion` | `string?` | Package version declared in the project manifest. |
| `MainFile` | `string?` | Main entry file declared in the project manifest. |
| `ModuleFile` | `string?` | ESM module entry file declared in the project manifest. |
| `Description` | `string?` | Project description from manifest. |
| `IsLibrary` | `bool` | Indicates whether the project is a shared library rather than an executable application. |
| `EntityKind` | `ProjectEntityKind` | The physical entity classification (e.g. Service, App, Worker, FunctionApp, Library, DatabaseMigration, Test). |
| `SubKind` | `ProjectEntitySubKind` | Sub-classification for apps (e.g. None, Web, Mobile, Desktop, Cli). |
| `IsTest` | `bool` | Indicates whether this project is a test project. |
| `ProjectKind` | `ProjectEntityKind` | The physical entity classification kind. |

---

#### `ProjectsStructure`

> Represents an intermediate node grouping all logical projects in the workspace.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `CONTAINS` | `Project` |
| `CONTAINS` | `EntryPoint` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `Workspace` | `CONTAINS` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the entity. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |

---

### 📂 Layer 3: Syntax Structure

#### `CallArgument`

> Represents a semantically colored invocation argument sliced backward to its provenance origin.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `DECLARED_IN` | `File` |
| `FLOWS_TO` | `Parameter` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `Database` | `TARGETED_BY` |
| `Endpoint` | `TARGETED_BY` |
| `ExternalService` | `TARGETED_BY` |
| `Function` | `PASSES_ARGUMENT` |
| `Parameter` | `FLOWS_TO` |
| `Procedure` | `TARGETED_BY` |
| `Table` | `TARGETED_BY` |
| `Topic` | `TARGETED_BY` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | Display label or slot representation of the argument. |
| `Callee` | `string` | The method, function, or constructor being invoked. |
| `ArgIndex` | `int` | The 0-indexed positional slot of the argument. |
| `PropertyKey` | `string?` | The object property key when passed via an options object (e.g. sql, text, url, topic). |
| `RawExpression` | `string` | The syntactic expression passed at the call site. |
| `ResolvedValue` | `string?` | The concrete value resolved via backward constant/template slicing, if determinable. |
| `Nature` | `ParameterNature` | The semantic nature of the argument slot. |
| `Provenance` | `ArgumentProvenance` | Where the argument's value originates (InternalConstant, CallerParameter, ConfigOrEnv, MemberOrField, etc.). |
| `Role` | `ExpressionRole` | How the expression participates in constructing the argument value. |
| `ValueKind` | `ConstantValueKind` | Fine-grained classification of the resolved constant or template value. |
| `FilePath` | `string` | The relative file path where the call occurs. |
| `Line` | `int` | The 1-indexed line number of the call argument. |
| `OriginFile` | `string?` | The relative file path where the value was originally defined, if distinct. |
| `OriginLine` | `int?` | The 1-indexed line number where the value was originally defined. |
| `OriginSymbol` | `string?` | The variable, constant, field, parameter, or config key from which the value originated. |
| `IsTest` | `bool` | Indicates whether the call occurs within test code. |
| `ProjectKind` | `ProjectEntityKind?` | The architectural kind of the enclosing project. |
| `ProjectName` | `string?` | The name of the enclosing project. |

---

#### `CallLiteral`

> Represents a raw literal argument passed directly into a method or constructor invocation.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `DECLARED_IN` | `File` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `Endpoint` | `PASSES_LITERAL` |
| `Function` | `PASSES_LITERAL` |
| `Type` | `PASSES_LITERAL` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name or representation of the literal argument. |
| `Value` | `string` | The raw string value of the literal. |
| `Callee` | `string` | The method or callee being invoked. |
| `ArgIndex` | `int` | The index of the argument (0-indexed). |
| `FilePath` | `string` | The relative file path where the call occurs. |
| `Line` | `int` | The 1-indexed line number of the argument. |
| `IsTest` | `bool` | Indicates whether the call occurs within test code. |
| `ProjectKind` | `ProjectEntityKind?` | The architectural kind of the enclosing project. |
| `ProjectName` | `string?` | The name of the enclosing project. |
| `Role` | `string` | The semantic role of the literal argument (e.g. Business, Routing, ParamBinding, Config, Logging, QueryBuilder, DI). |
| `IsDecorator` | `bool` | Indicates whether the literal argument occurs within a decorator or attribute. |
| `Nature` | `ParameterNature` | The semantic nature of the parameter/argument (e.g. SqlQuery, UrlOrRoute, ConnectionString, FilePath). |

---

#### `CatchClause`

> Represents a catch or except block in structured exception handling.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `DECLARED_IN` | `File` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `Endpoint` | `DECLARES` |
| `Function` | `DECLARES` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The identifier or descriptor of the catch clause. |
| `ExceptionType` | `string` | The declared exception type caught by this block, if specified. |
| `FilePath` | `string` | The relative file path where the catch clause occurs. |
| `Line` | `int` | The 1-indexed line number where the catch clause starts. |
| `IsEmpty` | `bool` | Indicates whether the catch body contains no executable statements. |
| `HasComment` | `bool` | Indicates whether an empty catch block contains an explanatory comment. |
| `StatementCount` | `int` | The number of executable statements inside the catch body. |
| `IsTest` | `bool` | Indicates whether the catch clause occurs within test code. |
| `ProjectKind` | `ProjectEntityKind?` | The architectural kind of the enclosing project. |
| `ProjectName` | `string?` | The name of the enclosing project. |

---

#### `Function`

> Represents a parsed method, function, subroutine, or procedure.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `DECLARED_IN` | `File` |
| `CALLS` | `Function` |
| `CALLS` | `Type` |
| `USES_TYPE` | `Type` |
| `INSTANTIATES` | `Type` |
| `INSTANTIATES` | `Function` |
| `INSTANTIATES` | `EntryPoint` |
| `IMPLEMENTS` | `EntryPoint` |
| `HAS_PARAMETER` | `Parameter` |
| `PASSES_ARGUMENT` | `CallArgument` |
| `PASSES_LITERAL` | `CallLiteral` |
| `DECLARES` | `CatchClause` |
| `DECLARES` | `Function` |
| `DECLARES` | `Member` |
| `DECLARES` | `Type` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `ApiInUse` | `ACCESSED_BY` |
| `App` | `CALLED_BY` |
| `CliTool` | `CALLED_BY` |
| `CloudService` | `ACCESSED_BY` |
| `CloudService` | `TRANSITIVELY_CALLS` |
| `Database` | `ACCESSED_BY` |
| `Database` | `TRANSITIVELY_CALLS` |
| `Endpoint` | `TRIGGERS` |
| `Endpoint` | `EXPOSED_BY` |
| `Endpoint` | `CALLS` |
| `ExternalService` | `CALLED_BY` |
| `ExternalService` | `TRANSITIVELY_CALLS` |
| `Function` | `CALLS` |
| `Function` | `INSTANTIATES` |
| `Function` | `DECLARES` |
| `Library` | `CALLED_BY` |
| `Member` | `ACCESSED_BY` |
| `Procedure` | `ACCESSED_BY` |
| `ProjectSyntax` | `CONTAINS` |
| `Query` | `ACCESSED_BY` |
| `Query` | `TRANSITIVELY_CALLS` |
| `Service` | `CALLED_BY` |
| `Table` | `ACCESSED_BY` |
| `Topic` | `PUBLISHED_BY` |
| `Topic` | `SUBSCRIBED_BY` |
| `Type` | `HAS_METHOD` |
| `Type` | `CALLS` |
| `Worker` | `CALLED_BY` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the entity. |
| `Symbol` | `string` | A globally unique ID for this symbol scope. |
| `FilePath` | `string` | The relative path of the declaring file. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |
| `StartLine` | `int` | The starting line number (1-indexed) of the declaration. |
| `IsTest` | `bool` | Indicates whether the function is declared within test code. |
| `TestFramework` | `string?` | Detected test runner or framework (e.g. xunit, nunit, mstest, jest, pytest, go-test, junit). |
| `ProjectKind` | `ProjectEntityKind?` | The architectural kind of the enclosing project. |
| `ProjectName` | `string?` | The name of the enclosing project. |
| `Loc` | `int?` | Lines of code span for this function. |
| `IsExported` | `bool?` | Indicates whether the function is publicly exported from its module. |
| `IsModuleInit` | `bool?` | Indicates whether this node represents top-level module initialization code. |
| `IsAsync` | `bool?` | Indicates whether the function is asynchronous. |
| `IsUnsafe` | `bool?` | Indicates whether the function uses unsafe code blocks or modifiers. |
| `Visibility` | `string?` | Declared visibility or access modifier of the function (e.g. public, private, remote). |
| `GraphqlOperation` | `string?` | GraphQL client operation name or flag. |
| `OperationType` | `string?` | Operation type for GraphQL or RPC operations. |
| `GraphqlFields` | `string?` | Comma-separated GraphQL root selection fields. |
| `Attributes` | `string?` | Comma-separated annotations, attributes, or decorators applied to this function. |

---

#### `Member`

> Represents a declared field, property, parameter, or local variable.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `DECLARED_IN` | `File` |
| `OF_TYPE` | `Type` |
| `ACCESSED_BY` | `Function` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `Function` | `DECLARES` |
| `Type` | `HAS_MEMBER` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the entity. |
| `Symbol` | `string` | A globally unique ID for this symbol scope. |
| `FilePath` | `string` | The relative path of the declaring file. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |
| `StartLine` | `int` | The starting line number (1-indexed) of the declaration. |
| `MemberKind` | `MemberKind` | The specific member kind (field, property, parameter, variable). |
| `DataType` | `string?` | The declared data type of the member. |
| `ParameterIndex` | `int?` | 0-based formal parameter index when the member is a function parameter. |
| `VariableClassification` | `string?` | Variable classification tags (e.g. parameter, config, etl, constant, global, property, field). |
| `InitializerExpression` | `string?` | Bounded initializer expression text. |
| `IsConstant` | `bool?` | Indicates whether the member is a compile-time or immutable constant. |
| `Attributes` | `string?` | Comma-separated annotations, attributes, or decorators applied to this member. |

---

#### `Parameter`

> Represents a semantically colored function or method parameter that flows into an architectural sink.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `DECLARED_IN` | `File` |
| `FLOWS_TO` | `CallArgument` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `CallArgument` | `FLOWS_TO` |
| `Function` | `HAS_PARAMETER` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The parameter identifier name. |
| `FunctionSymbol` | `string` | The symbol ID of the declaring function or method. |
| `ParamIndex` | `int` | The 0-indexed position of the parameter in the function signature. |
| `DataType` | `string?` | The declared parameter type annotation, if present. |
| `FilePath` | `string` | The relative file path of the declaring function. |
| `Line` | `int` | The 1-indexed declaration line number. |
| `Nature` | `ParameterNature` | The semantic nature inferred from intra- or inter-procedural sink usage. |
| `SinkCallee` | `string?` | The primary sink callee that determined the parameter's semantic nature. |
| `IsTest` | `bool` | Indicates whether the parameter belongs to test code. |
| `ProjectKind` | `ProjectEntityKind?` | The architectural kind of the enclosing project. |
| `ProjectName` | `string?` | The name of the enclosing project. |

---

#### `ProjectSyntax`

> Represents an intermediate node grouping AST/syntactic declarations of a specific project.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `CONTAINS` | `Type` |
| `CONTAINS` | `Function` |
| `BELONGS_TO` | `Project` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `SyntaxStructure` | `CONTAINS` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the entity. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |

---

#### `SyntaxStructure`

> Represents an intermediate node grouping all AST/syntactic declarations of the entire workspace.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `CONTAINS` | `ProjectSyntax` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `Workspace` | `CONTAINS` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the entity. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |

---

#### `Type`

> Represents a type declaration (Class, Interface, Struct, Record, Enum, or Union type).

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `DECLARED_IN` | `File` |
| `USES_TYPE` | `Type` |
| `IMPLEMENTS` | `Type` |
| `INHERITS_FROM` | `Type` |
| `POTENTIAL_TYPE` | `Type` |
| `HAS_METHOD` | `Function` |
| `HAS_MEMBER` | `Member` |
| `CALLS` | `Function` |
| `PASSES_LITERAL` | `CallLiteral` |
| `DECLARES` | `Type` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `ApiInUse` | `ACCESSED_BY` |
| `BoundedContext` | `DEFINES` |
| `CloudService` | `ACCESSED_BY` |
| `Database` | `ACCESSED_BY` |
| `Endpoint` | `EXPOSED_BY` |
| `Function` | `CALLS` |
| `Function` | `USES_TYPE` |
| `Function` | `INSTANTIATES` |
| `Function` | `DECLARES` |
| `Member` | `OF_TYPE` |
| `ProjectSyntax` | `CONTAINS` |
| `Query` | `ACCESSED_BY` |
| `Table` | `ACCESSED_BY` |
| `Type` | `USES_TYPE` |
| `Type` | `IMPLEMENTS` |
| `Type` | `INHERITS_FROM` |
| `Type` | `POTENTIAL_TYPE` |
| `Type` | `DECLARES` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the entity. |
| `Symbol` | `string` | A globally unique ID for this symbol scope. |
| `FilePath` | `string` | The relative path of the declaring file. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |
| `StartLine` | `int` | The starting line number (1-indexed) of the declaration. |
| `TypeKind` | `TypeKind` | The specific type kind (class, interface, struct, record, enum). |
| `IsTest` | `bool` | Indicates whether the type is declared within test code. |
| `ProjectKind` | `ProjectEntityKind?` | The architectural kind of the enclosing project. |
| `ProjectName` | `string?` | The name of the enclosing project. |
| `Loc` | `int?` | Lines of code span for this type declaration. |
| `IsExported` | `bool?` | Indicates whether the type is publicly exported from its module. |
| `Extends` | `string?` | Declared base type or component inherited by this type. |
| `Implements` | `string?` | Declared interfaces implemented by this type. |
| `Attributes` | `string?` | Comma-separated annotations, attributes, or decorators applied to this type. |

---

### 📂 Layer 4: Semantic Structure

#### `ApiInUse`

> Represents an external API library or client service used by the project (e.g. NestJS, Axios, HttpClient).

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `DECLARED_IN` | `File` |
| `ACCESSED_BY` | `Function` |
| `ACCESSED_BY` | `Type` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `App` | `CONTAINS` |
| `App` | `USES_API` |
| `CliTool` | `USES_API` |
| `Library` | `USES_API` |
| `SemanticStructure` | `CONTAINS` |
| `Service` | `CONTAINS` |
| `Service` | `USES_API` |
| `TestSuite` | `USES_API` |
| `Worker` | `USES_API` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the entity. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |
| `Capability` | `string?` | The canonical capability identifier (e.g. fs:read, net:http-client, proc:exec). |
| `Dimension` | `string?` | The capability dimension (e.g. Filesystem, Network, Process, DynamicCode, Environment, Native, Crypto, Concurrency). |
| `SemanticRole` | `string?` | The semantic security role of the API call (Sink, SensitiveSource, Transform, MonkeyPatch, Benign). |

---

#### `App`

> Represents a single-page application, web frontend, mobile client, or desktop app (e.g. React, Next.js, Vue, Angular, Electron).

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `IMPLEMENTED_BY` | `Project` |
| `DEPENDS_ON` | `Library` |
| `DEPENDS_ON` | `Package` |
| `SERVICE_CALL` | `Service` |
| `SERVICE_CALL` | `ExternalService` |
| `USES_DB` | `Database` |
| `PUBLISHES_TO` | `Topic` |
| `SUBSCRIBES_TO` | `Topic` |
| `USES_CLOUD` | `CloudService` |
| `READS_FROM` | `CloudService` |
| `WRITES_TO` | `CloudService` |
| `COORDINATES_WITH` | `PeerMesh` |
| `EXPOSES` | `Endpoint` |
| `CONTAINS` | `Endpoint` |
| `CONTAINS` | `ApiInUse` |
| `USES_API` | `ApiInUse` |
| `CALLED_BY` | `Function` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `BoundedContext` | `CONTAINS` |
| `SemanticStructure` | `CONTAINS` |
| `TestSuite` | `TESTS` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The application name. |
| `Path` | `string` | The path of the application directory relative to the workspace. |
| `ProjectType` | `ProjectLanguage` | The project type or language. |
| `Role` | `string?` | The primary semantic role of the application. |
| `IsLibrary` | `bool?` | Indicates whether the project is a library. |
| `PrimaryRole` | `string?` | The primary architectural role. |
| `Framework` | `string?` | The detected application framework. |

---

#### `CliTool`

> Represents a command-line tool, developer script, or administrative CLI utility.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `IMPLEMENTED_BY` | `Project` |
| `DEPENDS_ON` | `Library` |
| `DEPENDS_ON` | `Package` |
| `SERVICE_CALL` | `Service` |
| `SERVICE_CALL` | `ExternalService` |
| `PUBLISHES_TO` | `Topic` |
| `SUBSCRIBES_TO` | `Topic` |
| `USES_CLOUD` | `CloudService` |
| `READS_FROM` | `CloudService` |
| `WRITES_TO` | `CloudService` |
| `COORDINATES_WITH` | `PeerMesh` |
| `USES_DB` | `Database` |
| `EXPOSES` | `Endpoint` |
| `CONTAINS` | `Endpoint` |
| `USES_API` | `ApiInUse` |
| `CALLED_BY` | `Function` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `BoundedContext` | `CONTAINS` |
| `SemanticStructure` | `CONTAINS` |
| `TestSuite` | `TESTS` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The CLI tool name. |
| `Path` | `string` | The path of the tool directory relative to the workspace. |
| `ProjectType` | `ProjectLanguage` | The project type or language. |
| `Role` | `string?` | The primary semantic role of the CLI tool. |
| `IsLibrary` | `bool?` | Indicates whether the project is a library. |
| `PrimaryRole` | `string?` | The primary architectural role. |
| `Framework` | `string?` | The detected CLI framework. |

---

#### `CloudService`

> Represents a cloud provider service used by the project (e.g. AWS S3, Stripe, Firebase).

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `DECLARED_IN` | `File` |
| `ACCESSED_BY` | `Function` |
| `ACCESSED_BY` | `Type` |
| `TRANSITIVELY_CALLS` | `Function` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `App` | `USES_CLOUD` |
| `App` | `READS_FROM` |
| `App` | `WRITES_TO` |
| `CliTool` | `USES_CLOUD` |
| `CliTool` | `READS_FROM` |
| `CliTool` | `WRITES_TO` |
| `Endpoint` | `ATTRIBUTED_TO` |
| `Library` | `USES_CLOUD` |
| `Library` | `READS_FROM` |
| `Library` | `WRITES_TO` |
| `SemanticStructure` | `CONTAINS` |
| `Service` | `CONTAINS` |
| `Service` | `USES_CLOUD` |
| `Service` | `READS_FROM` |
| `Service` | `WRITES_TO` |
| `Worker` | `USES_CLOUD` |
| `Worker` | `READS_FROM` |
| `Worker` | `WRITES_TO` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the entity. |
| `ResourceType` | `CloudResourceType` | The cloud service resource type (e.g. S3Bucket, DynamoDb, CloudService). |
| `Path` | `string` | The path of the folder or file relative to its parent container. |
| `Provider` | `string?` | Cloud or SaaS vendor (aws, gcp, azure, cloudflare, stripe, firebase, supabase). |
| `ResourceRole` | `string?` | Operational role of the cloud resource (storage, secrets, auth, compute, cdn, ai). |

---

#### `Database`

> Represents a database instance, catalog, or physical schema.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `DECLARED_IN` | `File` |
| `ACCESSED_BY` | `Function` |
| `ACCESSED_BY` | `Type` |
| `ACCESSED_BY` | `Query` |
| `TRANSITIVELY_CALLS` | `Function` |
| `CONTAINS` | `Table` |
| `CONTAINS` | `Procedure` |
| `CONTAINS` | `Query` |
| `TARGETED_BY` | `CallArgument` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `App` | `USES_DB` |
| `BoundedContext` | `CONTAINS` |
| `CliTool` | `USES_DB` |
| `Endpoint` | `ATTRIBUTED_TO` |
| `Library` | `USES_DB` |
| `SemanticStructure` | `CONTAINS` |
| `Service` | `USES_DB` |
| `Worker` | `USES_DB` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The database engine or catalog name. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |
| `DbType` | `DatabaseType` | The database system type (sqlserver, postgres, sqlite, mongodb, neo4j). |
| `DisplayName` | `string?` | Human-readable database or catalog display name. |
| `Engine` | `string?` | Detected database engine (e.g. postgresql, sqlserver, sqlite, mysql, mongodb, redis). |
| `Technology` | `string?` | ORM, driver, or client technology accessing the database (e.g. EF Core, Dapper, Prisma, TypeORM). |
| `Schema` | `string?` | Physical database schema name when declared (e.g. public, dbo, billing). |
| `Provider` | `string?` | Database driver or provider package identifier. |
| `Role` | `string?` | Storage role classification (primary, cache, analytics, read-replica). |
| `ConfigKey` | `string?` | Configuration key or connection string name bound to this database. |
| `FromConfig` | `bool?` | Indicates whether the database connection was discovered from configuration files. |
| `IsTest` | `bool?` | Indicates whether this database instance is used exclusively in tests. |
| `IsOrm` | `bool?` | Indicates whether access was inferred via an ORM entity/DbContext mapping. |

---

#### `DataSet`

> Represents a collection of data tables or datasets.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `CONTAINS` | `Table` |

---

#### `Endpoint`

> Represents an exposed external API or message contract endpoint (REST, gRPC, GraphQL, WebSocket, WCF, or Broker Consumer).

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `TRIGGERS` | `Function` |
| `EXPOSED_BY` | `Type` |
| `EXPOSED_BY` | `Function` |
| `SUBSCRIBES_TO` | `Topic` |
| `TARGETED_BY` | `CallArgument` |
| `DECLARED_IN` | `File` |
| `DECLARED_IN` | `Project` |
| `CALLS` | `Function` |
| `PASSES_LITERAL` | `CallLiteral` |
| `DECLARES` | `CatchClause` |
| `ATTRIBUTED_TO` | `Query` |
| `ATTRIBUTED_TO` | `Database` |
| `ATTRIBUTED_TO` | `ExternalService` |
| `ATTRIBUTED_TO` | `CloudService` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `App` | `EXPOSES` |
| `App` | `CONTAINS` |
| `CliTool` | `EXPOSES` |
| `CliTool` | `CONTAINS` |
| `ExternalService` | `CALLS_ENDPOINT` |
| `Library` | `EXPOSES` |
| `SemanticStructure` | `CONTAINS` |
| `Service` | `EXPOSES` |
| `Service` | `CONTAINS` |
| `Worker` | `EXPOSES` |
| `Worker` | `CONTAINS` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The endpoint name (e.g. GET /api/orders, MSG:SubmitOrder). |
| `Path` | `string` | The path of the folder or file relative to its parent container. |
| `HttpMethod` | `string` | The HTTP Verb or contract verb (GET, POST, PUT, DELETE, ALL, MSG, WS, RPC). |
| `RouteTemplate` | `string` | The declared route template or message contract name. |
| `Protocol` | `string` | The contract protocol (REST, gRPC, GraphQL, WS, WCF, MSG, RPC). |
| `IsAnonymous` | `bool` | Whether endpoint allows unauthenticated anonymous access. |
| `RequiredRoles` | `string?` | Comma-separated required roles or permissions. |
| `Policies` | `string?` | Authorization policies guarding the endpoint. |
| `RequestType` | `string?` | The request payload type (e.g. StartRideRequest, CreateOrderDto). |
| `ResponseType` | `string?` | The response payload type (e.g. RideDto, OrderResponse). |
| `OperationType` | `OperationType?` | The operation type for GraphQL (Query, Mutation, Subscription) or gRPC (Unary, Streaming). |
| `FilePath` | `string?` | Workspace-relative source file declaring the endpoint. |
| `StartLine` | `int?` | 1-indexed starting line number of the endpoint declaration. |
| `Line` | `int?` | 1-indexed source line number of the route or contract mapping. |
| `Operation` | `string?` | Operation or handler action name associated with this endpoint. |
| `EvidenceKind` | `string?` | Extraction provenance kind (endpoint, grpc, graphql, ws, consumer). |
| `GraphqlField` | `string?` | Exposed GraphQL root field name when protocol is GraphQL. |
| `IsGraphqlSdl` | `bool?` | Indicates whether the endpoint was extracted from a GraphQL SDL schema. |
| `StatusCode` | `string?` | Declared HTTP response status codes (e.g. 200, 201, 404). |
| `Summary` | `string?` | OpenAPI / Swagger or docstring endpoint summary. |

---

#### `ExternalService`

> Represents a physical/logical external host dependency.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `DECLARED_IN` | `File` |
| `DECLARED_IN` | `Project` |
| `CALLS_ENDPOINT` | `Endpoint` |
| `CALLED_BY` | `Function` |
| `TRANSITIVELY_CALLS` | `Function` |
| `TARGETED_BY` | `CallArgument` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `App` | `SERVICE_CALL` |
| `CliTool` | `SERVICE_CALL` |
| `Endpoint` | `ATTRIBUTED_TO` |
| `Library` | `SERVICE_CALL` |
| `SemanticStructure` | `CONTAINS` |
| `Service` | `SERVICE_CALL` |
| `Worker` | `SERVICE_CALL` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | Logical host or external client dependency name. |
| `Protocol` | `string` | Outbound transport protocol (HTTP, HTTPS, gRPC, GraphQL, WS, WCF). |
| `DomainOrService` | `string` | Target domain hostname or logical service identifier. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |
| `FilePath` | `string?` | Workspace-relative source file issuing the outbound call. |
| `StartLine` | `int?` | 1-indexed line number of the outbound client call. |
| `BaseUrl` | `string?` | Configured base URL or service address. |
| `Url` | `string?` | Resolved outbound request URL or path template. |
| `ConfigKey` | `string?` | Configuration setting or environment variable supplying the host URL. |
| `GraphqlField` | `string?` | Queried GraphQL field name on the remote service. |
| `OperationType` | `string?` | Remote operation kind (Query, Mutation, Unary, Streaming). |
| `Method` | `string?` | Outbound HTTP verb or RPC method name. |
| `IsExternal` | `bool?` | True when the target resolves outside workspace services. |

---

#### `Library`

> Represents a shared library, utility module, DTO package, or domain contract reused across services.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `IMPLEMENTED_BY` | `Project` |
| `DEPENDS_ON` | `Library` |
| `DEPENDS_ON` | `Package` |
| `SERVICE_CALL` | `Service` |
| `SERVICE_CALL` | `ExternalService` |
| `USES_DB` | `Database` |
| `PUBLISHES_TO` | `Topic` |
| `SUBSCRIBES_TO` | `Topic` |
| `USES_CLOUD` | `CloudService` |
| `READS_FROM` | `CloudService` |
| `WRITES_TO` | `CloudService` |
| `COORDINATES_WITH` | `PeerMesh` |
| `EXPOSES` | `Endpoint` |
| `USES_API` | `ApiInUse` |
| `CALLED_BY` | `Function` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `App` | `DEPENDS_ON` |
| `BoundedContext` | `CONTAINS` |
| `CliTool` | `DEPENDS_ON` |
| `Library` | `DEPENDS_ON` |
| `SemanticStructure` | `CONTAINS` |
| `Service` | `DEPENDS_ON` |
| `TestSuite` | `DEPENDS_ON` |
| `TestSuite` | `TESTS` |
| `Worker` | `DEPENDS_ON` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The library name. |
| `Path` | `string` | The path of the library directory relative to the workspace. |
| `ProjectType` | `ProjectLanguage` | The project type or language. |
| `Role` | `string?` | The primary semantic role of the library. |
| `IsLibrary` | `bool?` | Indicates whether the project is a library. |
| `PrimaryRole` | `string?` | The primary architectural role. |
| `Framework` | `string?` | The detected framework. |

---

#### `PeerMesh`

> Represents a symmetric peer-to-peer cluster or collective topology.

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `App` | `COORDINATES_WITH` |
| `CliTool` | `COORDINATES_WITH` |
| `Library` | `COORDINATES_WITH` |
| `SemanticStructure` | `CONTAINS` |
| `Service` | `COORDINATES_WITH` |
| `Worker` | `COORDINATES_WITH` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the peer mesh. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |
| `MemberCount` | `int?` | The number of configured peer members, when known. |
| `Protocols` | `string` | The detected communication protocols. |

---

#### `Procedure`

> Represents a stored procedure in a database.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `CONTAINS` | `Query` |
| `ACCESSED_BY` | `Function` |
| `TARGETED_BY` | `CallArgument` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `Database` | `CONTAINS` |
| `SemanticStructure` | `CONTAINS` |

---

#### `Query`

> Represents a SQL query.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `DECLARED_IN` | `File` |
| `READS_FROM` | `Table` |
| `WRITES_TO` | `Table` |
| `DEPENDS_ON` | `Table` |
| `ACCESSED_BY` | `Function` |
| `ACCESSED_BY` | `Type` |
| `TRANSITIVELY_CALLS` | `Function` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `Database` | `ACCESSED_BY` |
| `Database` | `CONTAINS` |
| `Endpoint` | `ATTRIBUTED_TO` |
| `Procedure` | `CONTAINS` |
| `SemanticStructure` | `CONTAINS` |
| `Table` | `ACCESSED_BY` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | Short query identifier or operation summary. |
| `QueryText` | `string` | Extracted SQL or query statement text. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |
| `StartLine` | `int` | 1-indexed starting line number of the query in source. |
| `Verb` | `string?` | SQL DML/DDL verb (SELECT, INSERT, UPDATE, DELETE, MERGE, EXEC). |
| `Operation` | `string?` | Normalized data access operation (read, write, schema). |
| `EvidenceKind` | `string?` | Extraction provenance kind (query, orm, migration, procedure). |
| `FilePath` | `string?` | Workspace-relative source file path declaring the query. |
| `Line` | `int?` | 1-indexed source line of the query call site. |

---

#### `SemanticStructure`

> Represents an intermediate node grouping all runtime workloads, databases, endpoints, cloud services, and APIs used in the entire workspace.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `CONTAINS` | `App` |
| `CONTAINS` | `Service` |
| `CONTAINS` | `Worker` |
| `CONTAINS` | `Library` |
| `CONTAINS` | `CliTool` |
| `CONTAINS` | `TestSuite` |
| `CONTAINS` | `Database` |
| `CONTAINS` | `Table` |
| `CONTAINS` | `Procedure` |
| `CONTAINS` | `Query` |
| `CONTAINS` | `Topic` |
| `CONTAINS` | `ExternalService` |
| `CONTAINS` | `CloudService` |
| `CONTAINS` | `ApiInUse` |
| `CONTAINS` | `Endpoint` |
| `CONTAINS` | `PeerMesh` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `Workspace` | `CONTAINS` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the entity. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |

---

#### `Service`

> Represents an executable backend service, microservice, or API daemon (e.g. ASP.NET Core Web API, NestJS, Express, Go HTTP server).

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `IMPLEMENTED_BY` | `Project` |
| `DEPENDS_ON` | `Library` |
| `DEPENDS_ON` | `Package` |
| `SERVICE_CALL` | `Service` |
| `SERVICE_CALL` | `ExternalService` |
| `USES_DB` | `Database` |
| `PUBLISHES_TO` | `Topic` |
| `SUBSCRIBES_TO` | `Topic` |
| `EXPOSES` | `Endpoint` |
| `CONTAINS` | `Endpoint` |
| `CONTAINS` | `CloudService` |
| `USES_CLOUD` | `CloudService` |
| `READS_FROM` | `CloudService` |
| `WRITES_TO` | `CloudService` |
| `COORDINATES_WITH` | `PeerMesh` |
| `CONTAINS` | `ApiInUse` |
| `USES_API` | `ApiInUse` |
| `CALLED_BY` | `Function` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `App` | `SERVICE_CALL` |
| `BoundedContext` | `CONTAINS` |
| `CliTool` | `SERVICE_CALL` |
| `Library` | `SERVICE_CALL` |
| `SemanticStructure` | `CONTAINS` |
| `Service` | `SERVICE_CALL` |
| `TestSuite` | `TESTS` |
| `Worker` | `SERVICE_CALL` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The service name. |
| `Path` | `string` | The path of the service directory relative to the workspace. |
| `ProjectType` | `ProjectLanguage` | The project type or language. |
| `Role` | `string?` | The primary semantic role of the service. |
| `IsLibrary` | `bool?` | Indicates whether the project is a library. |
| `PrimaryRole` | `string?` | The primary architectural role. |
| `Framework` | `string?` | The detected service framework. |

---

#### `Table`

> Represents a physical database table.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `ACCESSED_BY` | `Function` |
| `ACCESSED_BY` | `Type` |
| `ACCESSED_BY` | `Query` |
| `TARGETED_BY` | `CallArgument` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `Database` | `CONTAINS` |
| `DataSet` | `CONTAINS` |
| `Query` | `READS_FROM` |
| `Query` | `WRITES_TO` |
| `Query` | `DEPENDS_ON` |
| `SemanticStructure` | `CONTAINS` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | Physical database table or view name. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |
| `Engine` | `string?` | Database engine hosting this table (e.g. postgresql, sqlserver, sqlite). |
| `Schema` | `string?` | Database schema qualifying this table (e.g. dbo, public, core). |
| `EntityName` | `string?` | Mapped ORM entity or domain model type name, if applicable. |

---

#### `TestSuite`

> Represents an automated test project, runner suite, or test module (e.g. NUnit/xUnit test project, Jest test suite, pytest suite).

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `IMPLEMENTED_BY` | `Project` |
| `DEPENDS_ON` | `Library` |
| `DEPENDS_ON` | `Package` |
| `USES_API` | `ApiInUse` |
| `TESTS` | `Service` |
| `TESTS` | `Library` |
| `TESTS` | `App` |
| `TESTS` | `Worker` |
| `TESTS` | `CliTool` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `SemanticStructure` | `CONTAINS` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The test suite name. |
| `Path` | `string` | The path of the test suite directory relative to the workspace. |
| `ProjectType` | `ProjectLanguage` | The project type or language. |
| `Role` | `string?` | The primary semantic role of the test suite. |
| `IsLibrary` | `bool?` | Indicates whether the project is a library. |
| `PrimaryRole` | `string?` | The primary architectural role. |
| `Framework` | `string?` | The detected test framework. |

---

#### `Topic`

> Represents a message queue, event exchange, or topic boundary.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `DECLARED_IN` | `File` |
| `PUBLISHED_BY` | `Function` |
| `SUBSCRIBED_BY` | `Function` |
| `TARGETED_BY` | `CallArgument` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `App` | `PUBLISHES_TO` |
| `App` | `SUBSCRIBES_TO` |
| `BoundedContext` | `CONTAINS` |
| `CliTool` | `PUBLISHES_TO` |
| `CliTool` | `SUBSCRIBES_TO` |
| `Endpoint` | `SUBSCRIBES_TO` |
| `Library` | `PUBLISHES_TO` |
| `Library` | `SUBSCRIBES_TO` |
| `SemanticStructure` | `CONTAINS` |
| `Service` | `PUBLISHES_TO` |
| `Service` | `SUBSCRIBES_TO` |
| `Worker` | `PUBLISHES_TO` |
| `Worker` | `SUBSCRIBES_TO` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the topic or exchange. |
| `Path` | `string` | The path of the folder or file relative to its parent container. |
| `BrokerType` | `BrokerType` | The broker system type (rabbitmq, kafka, sqs, in-memory). |
| `FilePath` | `string?` | Workspace-relative source file declaring or referencing the topic. |
| `Line` | `int?` | 1-indexed source line of the publish or subscribe site. |
| `MessagingDirection` | `string?` | Direction of broker interaction (publish, subscribe, both). |
| `Scope` | `string?` | Broker scope or virtual host / namespace. |
| `MessageType` | `string?` | Payload or event contract type published/consumed on this topic. |

---

#### `Worker`

> Represents a background job processor, queue consumer, or scheduled task (e.g. Hangfire, Celery, Worker Service).

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `IMPLEMENTED_BY` | `Project` |
| `DEPENDS_ON` | `Library` |
| `DEPENDS_ON` | `Package` |
| `SERVICE_CALL` | `Service` |
| `SERVICE_CALL` | `ExternalService` |
| `USES_DB` | `Database` |
| `PUBLISHES_TO` | `Topic` |
| `SUBSCRIBES_TO` | `Topic` |
| `USES_CLOUD` | `CloudService` |
| `READS_FROM` | `CloudService` |
| `WRITES_TO` | `CloudService` |
| `COORDINATES_WITH` | `PeerMesh` |
| `EXPOSES` | `Endpoint` |
| `CONTAINS` | `Endpoint` |
| `USES_API` | `ApiInUse` |
| `CALLED_BY` | `Function` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `BoundedContext` | `CONTAINS` |
| `SemanticStructure` | `CONTAINS` |
| `TestSuite` | `TESTS` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The worker name. |
| `Path` | `string` | The path of the worker directory relative to the workspace. |
| `ProjectType` | `ProjectLanguage` | The project type or language. |
| `Role` | `string?` | The primary semantic role of the worker. |
| `IsLibrary` | `bool?` | Indicates whether the project is a library. |
| `PrimaryRole` | `string?` | The primary architectural role. |
| `Framework` | `string?` | The detected worker framework. |

---

### 📂 Layer 5: Logical Structure

#### `BoundedContext`

> Represents a DDD Bounded Context in Level 5 (Logical Structure) that encapsulates semantic workloads, data stores, topics, and domain types.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `DEPENDS_ON` | `BoundedContext` |
| `CONTAINS` | `Service` |
| `CONTAINS` | `App` |
| `CONTAINS` | `Worker` |
| `CONTAINS` | `Library` |
| `CONTAINS` | `CliTool` |
| `CONTAINS` | `Database` |
| `CONTAINS` | `Topic` |
| `DEFINES` | `Type` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `BoundedContext` | `DEPENDS_ON` |
| `Domain` | `CONTAINS` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The canonical bounded context name. |
| `DisplayName` | `string` | The human-readable display name of the bounded context. |
| `DomainId` | `string` | The parent domain ID that owns this bounded context. |
| `Summary` | `string?` | A concise summary of the bounded context's business responsibility. |
| `PurityPercentage` | `double` | Domain purity percentage (0-100). |
| `Path` | `string` | Primary path associated with the bounded context. |

---

#### `Domain`

> Represents a high-level business domain (e.g. Core, Supporting, Generic, Shared) containing one or more bounded contexts in Level 5 (Logical Structure).

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `CONTAINS` | `BoundedContext` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `LogicalStructure` | `CONTAINS` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The canonical domain name. |
| `DisplayName` | `string` | The human-readable display name of the domain. |
| `DomainType` | `DomainType` | The DDD classification of the domain (Core, Supporting, Generic, Shared). |
| `Summary` | `string?` | A concise summary of the domain's business responsibility. |
| `Path` | `string` | Primary path associated with the domain. |

---

#### `LogicalStructure`

> Represents an intermediate root node grouping all high-level business domains, bounded contexts, and logical architectures in the workspace.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `CONTAINS` | `Domain` |

**Incoming edges** *(derived from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `Workspace` | `CONTAINS` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Name` | `string` | The name of the entity. |
| `Path` | `string` | The path of the workspace root. |

---

## 📂 Layer 5: SystemBindings (Integration Links)

> This layer contains the relationship edges that connect nodes across layers into a unified semantic map.

| Relationship Label | Description |
| :--- | :--- |
| `ACCESSED_BY` | Links a Layer 4 resource (Database, Table, Query, CloudService, ApiInUse) downward to the Layer 3 function or type that accesses it. |
| `ATTRIBUTED_TO` | Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain. |
| `BELONGS_TO` | Links an entity to its parent project or container. |
| `CALLED_BY` | Links a service, external service, or database to the function that invokes or queries it. |
| `CALLS` | Links a calling function to the function it directly invokes. |
| `CALLS_ENDPOINT` | Links an external API call to the target HTTP endpoint it invokes. |
| `CONFIGURES` | Links a configuration or infrastructure file to the semantic node (database, message broker, cloud or external service) it configures. |
| `CONTAINS` | Represents directory structure containment or syntactic scoping of elements. |
| `COORDINATES_WITH` | Links a workload to the symmetric peer mesh it coordinates with. |
| `DECLARED_IN` | Links a syntactic type, function, or member declaration to its physical declaring source file. |
| `DECLARES` | Indicates that a container entity declares a sub-entity. |
| `DECLARES_TYPE` | Indicates that a project declares a specific type. |
| `DEFINES` | Indicates that an entity defines a particular property or configuration. |
| `DEPENDS_ON` | Represents a dependency relationship between projects, packages, or other entities. |
| `EXPOSED_BY` | Links an entrypoint or endpoint to the type or function that exposes it. |
| `EXPOSES` | Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes. |
| `FLOWS_TO` | Traces semantic dataflow between call arguments and parameters across intra- and inter-procedural hops. |
| `HAS_ENTRYPOINT` | Links an executable ProjectNode to its binary or process EntryPointNode. |
| `HAS_MEMBER` | Links a type declaration to its declared member variables or fields. |
| `HAS_METHOD` | Links a type declaration to its declared methods or functions. |
| `HAS_PARAMETER` | Links a function or method to one of its semantically colored parameters. |
| `HAS_VARIABLE` | Links a function scope to its declared local variables or parameters. |
| `IMPLEMENTED_BY` | Links a library package to the project that implements or encapsulates it. |
| `IMPLEMENTS` | Links a class/struct declaration to the interface it implements. |
| `INHERITS_FROM` | Links a class/interface to its base class or inherited interface. |
| `INSTANTIATES` | Links a host component to a runtime UI or subsystem component it instantiates. |
| `LOCATED_IN` | Links a project to the physical folder or files structure where it is located. |
| `OF_TYPE` | Links a member variable or field to its declared type. |
| `PASSES_ARGUMENT` | Links a calling function or scope to a semantically colored call argument. |
| `PASSES_LITERAL` | Links a calling function or scope to a literal argument passed directly into an invocation. |
| `PERSISTED_IN` | Links an ORM entity or model class to its physical database table. |
| `POTENTIAL_TYPE` | Links a variable or parameter to concrete classes that implement its declared interface type. |
| `PRODUCES` | Indicates that a library project builds and produces a package. |
| `PUBLISHED_BY` | Links a topic to the function that publishes to it. |
| `PUBLISHES_TO` | Links a service or function to a message queue or topic it publishes messages or events to. |
| `QUERIED_BY` | Links a database or table to the function or query that accesses it. |
| `READS_FROM` | Links a query or workload to the table or cloud resource it reads from. |
| `SERVICE_CALL` | Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API. |
| `SUBSCRIBED_BY` | Links a topic to the function that subscribes to it. |
| `SUBSCRIBES_TO` | Links a service or handler function to a message queue or topic it listens to or consumes events from. |
| `TARGETED_BY` | Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it. |
| `TESTS` | Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies. |
| `TRANSFORMS_TO` | Links a data model or dataset representing a transformation step to its destination structure. |
| `TRANSITIVELY_CALLS` | Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph. |
| `TRIGGERS` | Links an entry point or API endpoint to the handler function it triggers. |
| `USES_API` | Links a project, file, or class to an external API library or client model. |
| `USES_CLOUD` | Links a project, file, or class to a cloud service resource. |
| `USES_DB` | Links a project to a database catalog or instance it utilizes. |
| `USES_GIT` | Links the workspace to its repository configuration. |
| `USES_TYPE` | Links a calling function or type reference to the type it instantiates or references. |
| `WRITES_TO` | Links a query or workload to the table or cloud resource it writes to. |

---

## 🏷️ Uniform Resource Name (URN) & ID Schemes

> Every node in the CodeExplorer graph has a structured ID (URN) that guarantees uniqueness across projects and workspaces.

| Layer | Node Label | ID / URN Scheme |
| :--- | :--- | :--- |
| Root / Umbrella | `Workspace` | `{workspaceId}` |
| Layer 1: File Structure | `Counter` | `workspace_id` |
| Layer 1: File Structure | `File` | `{workspaceId}:f:{relativeFilePath}` |
| Layer 1: File Structure | `FilesStructure` | `{workspaceId}:fs` |
| Layer 1: File Structure | `Folder` | `{workspaceId}:dir:{relativeDirectoryPath}` |
| Layer 1: File Structure | `GitSettings` | `{workspaceId}:git` |
| Layer 1: File Structure | `ScanIssue` | `{workspaceId}:issue:{relativeFilePath}:{stage}:{line}:{col}` |
| Layer 2: Project Structure | `EntryPoint` | `{workspaceId}:entry:{entryType}:{name}` |
| Layer 2: Project Structure | `Package` | `{workspaceId}:pkg:{ecosystem}:{packageName}` |
| Layer 2: Project Structure | `Project` | `{workspaceId}:p:{relativeProjectDir}:` |
| Layer 2: Project Structure | `ProjectsStructure` | `{workspaceId}:ps` |
| Layer 3: Syntax Structure | `CallArgument` | `{workspaceId}:arg:{filePath}:{line}:{col}:{slot}` |
| Layer 3: Syntax Structure | `CallLiteral` | `{workspaceId}:lit:{filePath}:{line}:{col}` |
| Layer 3: Syntax Structure | `CatchClause` | `{workspaceId}:catch:{filePath}:{line}:{col}` |
| Layer 3: Syntax Structure | `Function` | `{workspaceId}:sym:{filePath}:Function:{name}:{line}` |
| Layer 3: Syntax Structure | `Member` | `{workspaceId}:sym:{filePath}:Member:{name}:{line}` |
| Layer 3: Syntax Structure | `Parameter` | `{workspaceId}:param:{functionSymbol}:{index}:{name}` |
| Layer 3: Syntax Structure | `ProjectSyntax` | `{workspaceId}:p:{relativeProjectDir}:syntax` |
| Layer 3: Syntax Structure | `SyntaxStructure` | `{workspaceId}:ss` |
| Layer 3: Syntax Structure | `Type` | `{workspaceId}:sym:{filePath}:Type:{name}:{line}` |
| Layer 4: Semantic Structure | `ApiInUse` | `{workspaceId}:p:{relativeProjectDir}:api:{apiName}` |
| Layer 4: Semantic Structure | `App` | `{workspaceId}:app:{appName}` |
| Layer 4: Semantic Structure | `CliTool` | `{workspaceId}:cli:{toolName}` |
| Layer 4: Semantic Structure | `CloudService` | `{workspaceId}:p:{relativeProjectDir}:cloud:{serviceName}` |
| Layer 4: Semantic Structure | `Database` | `{workspaceId}:db:{dbType}:{dbName}` |
| Layer 4: Semantic Structure | `DataSet` | `{workspaceId}:ds:{datasetName}` |
| Layer 4: Semantic Structure | `Endpoint` | `{workspaceId}:ep:{httpMethod}:{routeTemplate}` |
| Layer 4: Semantic Structure | `ExternalService` | `{workspaceId}:es:{protocol}:{host}` |
| Layer 4: Semantic Structure | `Library` | `{workspaceId}:lib:{libraryName}` |
| Layer 4: Semantic Structure | `PeerMesh` | `{workspaceId}:mesh:peer-network` |
| Layer 4: Semantic Structure | `Procedure` | `{workspaceId}:proc:{procedureName}` |
| Layer 4: Semantic Structure | `Query` | `{workspaceId}:q:{queryHash}` |
| Layer 4: Semantic Structure | `SemanticStructure` | `{workspaceId}:sem` |
| Layer 4: Semantic Structure | `Service` | `{workspaceId}:s:{serviceName}` |
| Layer 4: Semantic Structure | `Table` | `{workspaceId}:tbl:{tableName}` |
| Layer 4: Semantic Structure | `TestSuite` | `{workspaceId}:suite:{suiteName}` |
| Layer 4: Semantic Structure | `Topic` | `{workspaceId}:top:{brokerType}:{topicName}` |
| Layer 4: Semantic Structure | `Worker` | `{workspaceId}:w:{workerName}` |
| Layer 5: Logical Structure | `BoundedContext` | `{workspaceId}:bc:{contextKey}` |
| Layer 5: Logical Structure | `Domain` | `{workspaceId}:dom:{domainKey}` |
| Layer 5: Logical Structure | `LogicalStructure` | `{workspaceId}:ls` |

