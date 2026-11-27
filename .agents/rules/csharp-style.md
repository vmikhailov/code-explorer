---
trigger: always_on
---

# C# Coding Style Rules

## Mandatory Implicit Namespaces (`ImplicitUsings`) & File-Scoped Namespaces
- **ALWAYS enable and use implicit namespaces (`<ImplicitUsings>enable</ImplicitUsings>`)** in every C# project (`.csproj` / `Directory.Build.props`) and rely on SDK implicit usings and project-wide `<Using Include="..." />` / `global using` directives.
- **NEVER add redundant `using` directives** at the top of `.cs` files for namespaces already imported implicitly or globally (e.g. `System`, `System.Collections.Generic`, `System.IO`, `System.Linq`, `System.Net.Http`, `System.Threading`, `System.Threading.Tasks`, or global `CodeExplorer.*` `<Using>` entries in `Directory.Build.props` / `.csproj`).
- **ALWAYS use file-scoped namespaces** (`namespace Foo.Bar;`) instead of block-scoped `namespace Foo.Bar { ... }`.

## Strict Ban on Fully Qualified Names
- **NEVER** use fully qualified type or namespace names inline in C# code (e.g. `System.Diagnostics.Stopwatch`, `Microsoft.Extensions.Logging.ILogger`, `Common.OntologyConstants.NodeLabels.ScanIssue`, `CodeExplorer.Core.Common.Nodes.FileNode`).
- **Always** import the required namespace via a `using` directive at the top of the file (when not already covered by implicit/global usings) and use the unqualified short type name.
- **Sole Exception — Genuine Name Conflicts**: Inline qualification (or a `using Alias = Namespace.Type;` directive) is allowed **ONLY** when importing both namespaces causes a real compiler ambiguity/name conflict in that file. If the conflicting type is referenced more than once in the file, prefer a file-level `using` alias over repeating the full qualification.

## Strict One-Entity-Per-File Rule
- **EVERY** top-level C# entity (`class`, `struct`, `record`, `interface`, `enum`, `delegate`) **MUST** reside in its own dedicated `.cs` file whose filename matches the entity name (e.g. `WorkspaceScope` in `WorkspaceScope.cs`, `ScanMode` in `ScanMode.cs`).
- **NEVER** place multiple top-level types, records, interfaces, enums, or static extension classes in a single `.cs` file — even small DTOs, options records, result types, or helper enums must each have their own file.
- `partial` declarations of the **same** single entity (`Foo.cs`, `Foo.Scanning.cs`) are permitted, provided no second top-level entity is declared in any of those files.
