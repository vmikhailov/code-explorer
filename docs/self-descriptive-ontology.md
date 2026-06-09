# Plan: Auto-Generated `ontology.md` from Build

## Current State Assessment

- **Nodes** already have `[OntologyNode(description, ...relationships)]` and `[OntologyProperty(description)]` attributes — rich metadata exists, but edges are encoded as raw Cypher-pattern strings, making them refactor-blind (renaming a node class does not update the strings).
- **Relationships** have zero description metadata — just `Kind`, `From`, `To`.
- **Label/ID strings** live in `OntologyConstants.NodeLabels` / `OntologyConstants.Relationships` as separate constants, disconnected from the class definitions.
- `docs/ontology.md` is manually written and already drifted.

---

## Part 1 — Attribute Enrichment (source changes)

### 1a. Extend `OntologyNodeAttribute`

Add `label` and `idScheme` parameters. Edges are moved out of `OntologyNodeAttribute` entirely and into a separate repeatable `[OntologyEdge]` attribute — typed via `typeof()` so IDE renames propagate automatically.

**Before** (`OntologyAttributes.cs`):

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class OntologyNodeAttribute(string purpose, params string[] relationships) : Attribute
{
    public string Purpose { get; } = purpose;
    public string[] Relationships { get; } = relationships;
}
```

**After**:

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class OntologyNodeAttribute(
    string label,
    string idScheme,
    string purpose) : Attribute
{
    public string Label { get; } = label;
    public string IdScheme { get; } = idScheme;
    public string Purpose { get; } = purpose;
}

// Repeatable — one attribute per outbound edge.
// Generic type parameter is the target node; `rel` is the relationship label constant.
// Inbound edges are NOT declared here; they are derived by the generator
// by scanning all other node classes' outbound declarations.
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class OntologyEdgeAttribute<TTo>(string rel) : Attribute
    where TTo : IOntologyNode
{
    public string Rel { get; } = rel;
    public Type   To  { get; } = typeof(TTo);
}
```

**Node class after**:

```csharp
// Outbound edges only — FileNode declares the CONTAINS edge pointing here,
// so ClassNode only declares what it sends out.
[OntologyNode(
    label:    OntologyConstants.NodeLabels.Class,
    idScheme: "{workspaceId}:symbol:{filePath}:Class:{name}:{line}",
    purpose:  "Represents a parsed OOP class, struct, or concrete type definition.")]
[OntologyEdge<ClassNode>(OntologyConstants.Relationships.UsesType)]
[OntologyEdge<InterfaceNode>(OntologyConstants.Relationships.UsesType)]
[OntologyEdge<InterfaceNode>(OntologyConstants.Relationships.Implements)]
[OntologyEdge<ClassNode>(OntologyConstants.Relationships.InheritsFrom)]
public record ClassNode(string Id, ...) : CompositeNode(Id, Extensions)
```

A container node that only initiates edges (`ProjectNode.cs`, partial):

```csharp
[OntologyNode(
    label:    OntologyConstants.NodeLabels.Project,
    idScheme: "{workspaceId}:project:{relativeProjectDir}:",
    purpose:  "Represents a buildable/compilable module or package directory.")]
[OntologyEdge<ApisInUseNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<FilesNode>(OntologyConstants.Relationships.Contains)]
[OntologyEdge<ProjectNode>(OntologyConstants.Relationships.DependsOn)]
[OntologyEdge<PackageNode>(OntologyConstants.Relationships.DependsOn)]
public record ProjectNode(string Id, ...) : CompositeNode(Id, Extensions)
```

---

### 1b. Add `OntologyRelationshipAttribute`

**New attribute** (added to `OntologyAttributes.cs`):

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class OntologyRelationshipAttribute(string label, string description) : Attribute
{
    public string Label { get; } = label;
    public string Description { get; } = description;
}
```

**Relationship class after**:

```csharp
[OntologyRelationship(
    label:       OntologyConstants.Relationships.Calls,
    description: "Links a calling function to the function it directly invokes. Populated during Layer 1 AST traversal.")]
public record CallsRelationship(
    [property: JsonIgnore] string From,
    [property: JsonIgnore] string To,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyRelationship
{
    [JsonIgnore]
    public string Kind => OntologyConstants.Relationships.Calls;
}
```

---

## Part 2 — Generator Tool

### 2a. project: `Tools/CodeExplorer.OntologyGen/OntologyGen.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.*" />
  </ItemGroup>
</Project>
```

### 2b. Roslyn syntax-only extraction (`Program.cs` sketch)

The tool parses `.cs` files as raw syntax trees. Attribute arguments are read directly from the AST as string literals.

```csharp
// Usage: OntologyGen <commonDir> <outputMdPath>
var commonDir  = args[0]; // e.g. Core/CodeExplorer.Core/Common
var outputPath = args[1]; // e.g. docs/ontology.md
```

### 2c. Sample generated output

Given `ClassNode.cs` and `CallsRelationship.cs` with the updated attributes, the generator produces:

```markdown
## Nodes

### `Class`

> Represents a parsed OOP class, struct, or concrete type definition.

**Outbound edges:**

| Relationship | To |
| :--- | :--- |
| `USES_TYPE` | `Class` |
| `USES_TYPE` | `Interface` |
| `IMPLEMENTS` | `Interface` |
| `INHERITS_FROM` | `Class` |

**Incoming edges** *(derived by generator from other nodes' declarations)*:

| From | Relationship |
| :--- | :--- |
| `File` | `CONTAINS` |

**Properties:**

| Property | Type | Description |
| :--- | :--- | :--- |
| `Id` | `string` | A unique identifier for the node. |
```

---

## Part 3 — MSBuild Integration

### 3a. Add `OntologyGen.csproj` to `CodeExplorer.sln`

```bash
dotnet sln add Tools/CodeExplorer.OntologyGen/OntologyGen.csproj
```

### 3b. Add `AfterBuild` target to `CodeExplorer.Core.csproj`

```xml
<Target Name="GenerateOntologyDoc" AfterTargets="Build"
        Condition="'$(GenerateOntologyDoc)' != 'false'">
  <Message Importance="high" Text="[OntologyGen] Regenerating docs/ontology.md..." />
  <Exec Command="dotnet run --project &quot;$(SolutionDir)Tools/CodeExplorer.OntologyGen&quot; -- &quot;$(MSBuildThisFileDirectory)Common&quot; &quot;$(SolutionDir)docs/ontology.md&quot;"
        WorkingDirectory="$(SolutionDir)" />
</Target>
```

---

## Summary of Files Changed / Created

| Action | File |
| :--- | :--- |
| Modify | `Core/CodeExplorer.Core/Common/Nodes/OntologyAttributes.cs` |
| Modify | all 26 `*Node.cs` files |
| Modify | all 18 `*Relationship.cs` files |
| Create | `Tools/CodeExplorer.OntologyGen/OntologyGen.csproj` |
| Create | `Tools/CodeExplorer.OntologyGen/Program.cs` |
| Modify | `Core/CodeExplorer.Core/CodeExplorer.Core.csproj` — `AfterBuild` target |
| Modify | `CodeExplorer.sln` — add new project |
| Regenerated | `docs/ontology.md` — on every build |
