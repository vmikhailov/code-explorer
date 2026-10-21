# UniversalParser Proposal 🌐
## Profile-Driven Tree-Sitter Visitor — Proof of Concept

- **Document Version:** 2.0.0
- **Status:** Proposal — PoC scoped
- **Target Audience:** Core Maintainers, Parser Engineers
- **Target Subsystems:** `CodeExplorer.Core/Parser` (`TreeSitter/BaseParserVisitor`, `Profiles/LanguageSyntaxProfile`), `CodeExplorer.Parser.Java`, new `CodeExplorer.Parser.Swift`

---

## 1. Goal

Prove, with measurable results, that a **single profile-driven `UniversalAstVisitor` plus small per-language hooks** can produce the same Layer 3 output (`SyntacticSymbol` tree, `RawImports`, `RawVariables`, `RawTypeBindings`, calls, call literals) as a hand-written language visitor, and can onboard a new language cheaply.

The PoC has two tracks:

| Track | Language | Question it answers |
| :--- | :--- | :--- |
| **A — Alternative engine** | Java (existing) | Can the universal visitor reach **parity** with a mature hand-written visitor, verified against real output? |
| **B — New language** | Swift (net-new) | How much code does a **new language** really cost on the universal visitor? |

### Non-goals

- Deleting or replacing any existing visitor. That is decided **after** the PoC, per language, based on §6.
- Changing Layer 2 (project and manifest detection). `*Parser.cs` files are mostly Layer 2 logic and stay as they are.
- Kotlin. `TreeSitter.DotNet` 1.3.0 ships no Kotlin grammar (see §2.3).
- Layer 4 semantic detection (HTTP clients, messaging, ORM, routes). Library parsers (`ISemanticExtension`) keep working unchanged through the existing hooks.

---

## 2. Current State

### 2.1 Parser layout

12 assemblies live under `cli/src/Parsers`: `CSharp`, `TypeScript`, `Java`, `Python`, `Go`, `Rust`, `Kotlin`, `SQL`, `ColdFusion`, `Android`, `Script`, `Shared`. Each Tree-sitter language has:

- `*Parser.cs`, which implements both `IProjectParser` (Layer 2) and `IFileParser` (Layer 3). Most of its code is Layer 2. For example, around 1,000 of the 1,211 lines in `CSharpParser.cs` handle directory exclusion, project detection, manifest properties, and dependency/package resolution.
- `*FileVisitor.cs`, which derives from [BaseParserVisitor](../../cli/src/Core/CodeExplorer.Core/Parser/TreeSitter/BaseParserVisitor.cs). This is the **only part this proposal targets**.
- `*DeclarationExtractor.cs`, which handles early constant/declaration discovery.
- `*SyntaxProfile.cs`, which provides node-type sets for [LanguageSyntaxProfile](../../cli/src/Core/CodeExplorer.Core/Parser/Profiles/LanguageSyntaxProfile.cs).

Code that is realistically in scope for unification (visitor + declaration extractor):

| Language | Visitor LOC | Declaration extractor LOC |
| :--- | :---: | :---: |
| Java | 411 | 90 |
| Go | 481 | 73 |
| Python | 512 | 60 |
| Rust | 527 | 223 |
| C# | 615 | 135 |
| TypeScript | 809 | 124 |
| **Total** | **3,355** | **705** |

Kotlin (`KotlinFileParser.cs`, 262 LOC) is regex-based (`UsesTreeSitter => false`) and has no visitor.

### 2.2 What is duplicated vs. genuinely language-specific

`BaseParserVisitor` already owns traversal, dispatch (`Visit*` methods), reference collection, call-literal collection, and symbol stacking. Subclasses override `MapNodeType`, `ExtractIdentifier`, `FindCallName` (abstract), `CollectCustomReferencesForSymbol`, `VisitImportStatement`, `VisitInheritanceClause`, and `VisitVariableDeclaration`.

**Duplicated across visitors (candidates for the universal visitor):**
- The library-parser dispatch loop at the top of `MapNodeType` / `ExtractIdentifier` (copied verbatim in Java, Go and others).
- Name extraction via the `name` field or the first identifier child.
- Direct, member, and constructor call-name extraction.
- Superclass / interface reference emission.
- Annotation, attribute, and decorator name + argument collection.
- Field/variable `(name, type, initializer)` collection into `RawVariables` / `RawTypeBindings`.

**Language-specific (must stay as hooks, not profile data):**
- Import shapes: Rust `use a::{b, c}` trees, Python relative imports, TypeScript `import {a as b}` and re-exports, C# `global using` / `using static` / aliases, Go import aliases.
- Kind discrimination by structure rather than node type:
  - Go: an interface is a `type_spec` containing an `interface_type`.
  - Swift: `class_declaration` covers class, struct, enum, actor and extension.
  - Kotlin: an interface is a `class_declaration` with an `interface` keyword.
- Binding semantics: Go method receivers, Rust `impl Trait for X`, TypeScript/Kotlin constructor-parameter properties, Lombok-generated members.
- Visitor-embedded Layer 4 detection (e.g. Go HTTP client calls, entry-point routes, SQL in string literals). The PoC keeps this behind hooks. Moving it to declarative projections belongs to the [Semantic Archetypes & YAML Projections proposal](./semantic-archetypes-and-yaml-projections-proposal.md).

### 2.3 Grammar availability (`TreeSitter.DotNet` 1.3.0)

| Available | Not available |
| :--- | :--- |
| c, c-sharp, cpp, go, java, javascript, typescript, tsx, python, rust, **swift**, scala, php, ruby, bash, css, html, json, toml, haskell, julia, ocaml, … | **kotlin**, dart, sql |

Swift needs no native build work. Kotlin would require building and shipping `tree-sitter-kotlin` binaries for every supported runtime identifier (RID), which is a separate decision.

### 2.4 Parser selection constraint

`WorkspaceIndexer.GetParserForFile` returns the **first** registered `IFileParser` whose `CanParseFile` matches. Registering a second parser for `.java` would be order-dependent. The alternative engine is therefore selected **inside** `JavaParser.CreateVisitor`, not by registering a competing parser.

---

## 3. Design

### 3.1 `UniversalAstVisitor`

```
BaseParserVisitor (unchanged)
└── UniversalAstVisitor          (new, in CodeExplorer.Core/Parser/TreeSitter)
      uses LanguageSyntaxProfile  (data: node-type sets + field selectors)
      uses ILanguageHooks?        (code: residual language-specific logic)
```

`UniversalAstVisitor` implements every override listed in §2.2 generically:

1. **Library-parser dispatch first.** Run the shared loop over `LibraryParsers` for `MapNodeType` / `ExtractIdentifier` before any generic logic. This removes the per-visitor copies.
2. **Generic logic driven by the profile.** Use node-type sets and ordered field selectors.
3. **Hook overrides.** Each step first asks `ILanguageHooks`. A non-null answer wins.

```csharp
public interface ILanguageHooks
{
    string? MapNodeType(Node node, LanguageSyntaxProfile profile) => null;
    string? ExtractIdentifier(Node node, LanguageSyntaxProfile profile) => null;
    string? FindCallName(Node callNode, LanguageSyntaxProfile profile) => null;
    IEnumerable<RawImport>? ExtractImports(Node importNode, string filePath) => null;
    void CollectCustomReferences(Node node, SyntacticSymbol symbol, SyntacticSymbol parent) { }
}
```

The visitor stays isolated from ontology and I/O, as required by the architecture guidelines: it builds `SyntacticSymbol` trees only, and uses `NodeExtensions` (`IsValid()`, `GetChildFieldText()`, …) for node access.

### 3.2 `LanguageSyntaxProfile` additions (additive only)

All existing members stay: `ConstantDeclarations`, `CatchClauses`, `StringInterpolations`, `BinaryExpressions`, `MemberAccessExpressions`, `ElementAccessExpressions`, `ExcludedStringInterpolations`, `Inheritance`, etc. The PoC adds:

```csharp
// Declarations
public FrozenSet<string> NamespaceDeclarations { get; init; } = FrozenSet<string>.Empty;
public FrozenSet<string> StructDeclarations { get; init; } = FrozenSet<string>.Empty;
public FrozenSet<string> ConstructorDeclarations { get; init; } = FrozenSet<string>.Empty;
public FrozenSet<string> PropertyParameters { get; init; } = FrozenSet<string>.Empty;

// Annotations
public FrozenSet<string> AnnotationNodes { get; init; } = FrozenSet<string>.Empty;
public FrozenSet<string> ModifierContainers { get; init; } = FrozenSet<string>.Empty;

// Inheritance
public FrozenSet<string> SuperclassNodes { get; init; } = FrozenSet<string>.Empty;
public FrozenSet<string> SuperInterfaceNodes { get; init; } = FrozenSet<string>.Empty;

// Calls
public FrozenSet<string> ObjectCreations { get; init; } = FrozenSet<string>.Empty;

// Field selectors (ordered; first valid wins)
public FieldSelectorConfig Fields { get; init; } = FieldSelectorConfig.Default;
```

`FieldSelectorConfig` holds ordered field-name lists (`NameFields`, `TypeFields`, `ValueFields`, `CallObjectFields`, `CallNameFields`, `CallArgumentsFields`). When no selector matches, the visitor **records a diagnostic** (node type + file + line) instead of silently returning `null`. The parity harness (§4.1) reports these diagnostics.

Profiles remain **C# classes** for the PoC. A YAML profile format is deferred until the PoC shows the profile is sufficient.

---

## 4. PoC Track A — Alternative Engine for Java

### 4.1 Mechanism

- `JavaParser.CreateVisitor` returns `UniversalAstVisitor(JavaSyntaxProfile.Instance, JavaHooks?)` when the language is listed in the environment variable `UNIVERSAL_PARSER_LANGUAGES` (comma-separated, e.g. `java`). Otherwise it returns `JavaFileVisitor`. The default is unchanged.
- `JavaHooks` starts **empty**. Logic is moved into it only when the parity harness shows a gap the profile cannot express. Its final size is a primary PoC metric.

### 4.2 Parity harness (`UniversalParserParityTests`)

For every Java fixture already used by the test suite (`JavaParserTests`, `JavaMessagingTests`, `OrmDataLineageTests`, `MultiLanguageHttpResolutionTests`, `ParserValidationTests`, …):

1. Parse once, then run **both** visitors on the same root node.
2. Normalise and diff:
   - The `SyntacticSymbol` tree: kind, name, line span, and references (sorted).
   - `RawImports`, `RawVariables`, `RawTypeBindings`.
   - Call names and call literals.
3. Fail with a structured diff that lists missing, extra, and changed items per category.

In addition, index one real Java sample workspace with both engines and compare graph node/edge counts per label/kind.

### 4.3 Tests under both engines

Run the existing Java-related test classes twice, once per engine (parameterised via the environment variable or an injected option), so behaviour coverage is reused rather than re-written.

---

## 5. PoC Track B — Swift as a New Language

### 5.1 Scope

New assembly `CodeExplorer.Parser.Swift` (`[assembly: ParserAssembly]`):

| Component | Responsibility |
| :--- | :--- |
| `SwiftParser : IFileParser` | `LanguageName => "swift"`, `.swift` files, `CreateVisitor` → `UniversalAstVisitor`. |
| `SwiftSyntaxProfile` | Node-type sets and field selectors for `tree-sitter-swift`. |
| `SwiftHooks` | Only what the profile cannot express. Expected: class/struct/enum/actor/extension discrimination, and attaching extension members to the extended type. |
| `SwiftPackageProjectParser : IProjectParser` | Minimal Layer 2: detect `Package.swift`, project name = package directory. Xcode projects (`.xcodeproj`) are out of scope. |

No `ISemanticExtension` library parsers in the PoC. URLSession, Alamofire and similar come later via the projection proposal.

### 5.2 Step 0 — Validate the grammar

Before writing the profile, add a test utility that dumps node types and field names from `tree-sitter-swift` for a fixture covering classes, structs, enums, protocols, extensions, actors, initialisers, properties, attributes, imports, calls and string literals. The profile is written from this dump, not from assumed node names.

### 5.3 Expected output

On a small SwiftPM fixture under `cli/tests`:
- `Type` symbols for class/struct/enum/actor/protocol, with correct kind.
- `Function` symbols for methods, free functions, and initialisers.
- Imports as `RawImports` (module names; `Foundation`, `UIKit`, `SwiftUI` classified as external).
- Inheritance and protocol conformance references.
- Attributes (`@MainActor`, `@objc`, property wrappers) collected on the symbol.
- Calls (`receiver.method()`, `Type()` initialisers) and string call literals.
- Properties with declared types as `RawVariables` / `RawTypeBindings`.

---

## 6. Success Criteria & Decision Gate

| Metric | Target |
| :--- | :--- |
| Java symbol-tree parity (normalised diff) | ≥ 98% of items identical; every remaining diff explained and accepted, or fixed |
| Java existing tests under universal engine | 100% pass |
| Java graph node/edge counts on sample workspace | within ±1% per label/kind |
| `JavaHooks` size | ≤ 150 LOC (vs. 411 LOC `JavaFileVisitor`) |
| Swift language-specific code (`SwiftSyntaxProfile` + `SwiftHooks`, excluding Layer 2) | ≤ 250 LOC |
| Unmatched field-selector diagnostics on fixtures | 0 unexplained |
| Parse throughput (files/s) vs. current Java visitor | within −10% |

**Go:** migrate the next language (Python or Go) behind the same switch, then make `universal` the default per language once parity holds.
**No-go:** keep `UniversalAstVisitor` only for new languages, and pull the shared library-dispatch loop up into `BaseParserVisitor` as a standalone cleanup.

---

## 7. Plan

| Step | Work | Estimate |
| :--- | :--- | :---: |
| 1 | `UniversalAstVisitor`, `ILanguageHooks`, additive profile fields, selector diagnostics | 2 days |
| 2 | Parity harness + `UNIVERSAL_PARSER_LANGUAGES` switch in `JavaParser.CreateVisitor` | 1 day |
| 3 | Iterate on `JavaSyntaxProfile` / `JavaHooks` until §6 Java criteria pass | 2–3 days |
| 4 | Swift grammar dump (Step 0), `SwiftSyntaxProfile`, `SwiftHooks`, `SwiftParser`, `SwiftPackageProjectParser` | 2 days |
| 5 | Swift fixture tests, throughput measurement, PoC report against §6 | 1 day |

Steps 1–3 (Track A) come first, so Track B builds on an engine already validated against known-good output.

---

## 8. Risks

| Risk | Mitigation |
| :--- | :--- |
| Generic field-selector fallbacks pick the wrong child | Ordered selectors per profile, plus diagnostics on no-match; parity diff surfaces wrong picks. |
| Hooks grow until they are the old visitor again | `JavaHooks` LOC is a gating metric (§6). |
| Library parsers depend on visitor-specific behaviour | Track A runs every Java library-parser test under both engines. |
| `tree-sitter-swift` node shapes differ from assumptions | Step 0 grammar dump before writing the profile. |
| Kotlin remains regex-based | Explicitly out of scope; requires a separate decision on shipping a native grammar. |

---

## 9. Relationship to Other Proposals

- **[Semantic Archetypes & YAML Projections](./semantic-archetypes-and-yaml-projections-proposal.md):** consumes the uniform Layer 3 output (annotations with arguments, calls, type bindings). It is the intended home for the Layer 4 detection currently embedded in visitors.
- **[Universal Edge & Node Evidence](./universal-edge-and-node-evidence-proposal.md):** needs exact line spans on Layer 3 symbols. `UniversalAstVisitor` must preserve `start_line` / `end_line` for every symbol it emits. Provenance itself is recorded by Layer 4/5 analyzers, not by this visitor.
- **[Semantic Invariant Gates](./semantic-invariant-gates-proposal.md):** unaffected; gates operate on the graph regardless of which visitor produced it.
