# CodeExplorer.Parser.Terraform (SCAFFOLD — v2, post owner review)

Terraform / OpenTofu / Terragrunt (HCL) parser for Code Explorer.
**Pure C#** (`UsesTreeSitter => false`) — no tree-sitter, no native grammar.

**Status:** scaffold — plumbing + stubs; logic TODO (throws `NotImplementedException`).
**Spec:** `docs/proposals/terraform-terragrunt-parser-spec.md` (v2). Read it first.
**Templates:** `../CodeExplorer.Parser.Kotlin/`, `../CodeExplorer.Parser.SQL/` (NOT Go).

## Files
| File | Role | Status |
|------|------|--------|
| `CodeExplorer.Parser.Terraform.csproj` | Core ref only (no TreeSitter.DotNet) | scaffolded |
| `TerraformParser.cs` | `IProjectParser`+`IFileParser`, `UsesTreeSitter=>false`, detection+classification | scaffolded; `ParseAsync`/deps TODO |
| `Hcl/HclLexer.cs` | tokenizer | STUB — spec §3 |
| `Hcl/HclParser.cs` | recursive-descent parser | STUB — spec §3 |
| `Hcl/HclAst.cs` | AST records | done (shape may evolve) |
| `TerraformDeclarationMapper.cs` | HCL block → TypeNode/MemberNode + URN IDs | STUB — spec §4.1/§4.3 |
| `TerraformSyntaxEnricher.cs` | Layer 4: cross-file/unit resolution + cloud-resource mapping | STUB — spec §5.6/§5.7 |
| `CloudResourceCatalog.cs` | resource_type → Database/Topic/CloudService | starter table — spec §4.2 |

## Engineer start-here
1. Implement `Hcl/` lexer+parser (spec §3), then `TerraformDeclarationMapper` (§4.1/§4.3) → `ParseAsync` emits TypeNode/MemberNode with **URN IDs**.
2. Implement project **classification** so `.tf` folders aren't microservices (§4.4); add `.terraform`/`.terragrunt-cache`/`.serverless` to core `WorkspaceFileFilter.ExcludedDirectoryNames`, and `terragrunt.hcl`/`.terraform.lock.hcl` to `ManifestFileNames`.
3. Implement `TerraformSyntaxEnricher.EnrichAsync` (Layer 4): resolve cross-file/unit refs → `DEPENDS_ON`/`CONFIGURES` (`via`/`ref` props), and map cloud resources → `Database`/`Topic`/`CloudService` for App↔IaC (§4.2/§5.6).
4. Surface local module/unit paths via `ParseDependenciesAsync` → Layer 2 `DEPENDS_ON` (§5.3/§5.7).
5. **Register:** add to `cli/CodeExplorer.slnx` AND add a `ProjectReference` from `cli/src/UI/CodeExplorer/CodeExplorer.csproj` (single-file publish).
6. Tests + fixtures (spec §7), incl. an app↔infra fixture proving §5.6, and an AILA-style multi-stack.

## Hard constraints (owner-verified — do not violate)
- `BaseParserVisitor` drops `Variable`/`Member`-kinded nodes → use `TypeNode`/`MemberNode` with `kind` props.
- `Layer3SyntacticParser` throws on unknown labels → no custom node labels.
- `USAGE`/`IMPORTS`/`OVERRIDE` edges don't exist → use `DEPENDS_ON`/`CONFIGURES`+`via`/`ref`.
- Node IDs MUST be workspace-scoped URNs → no bare `resource.T.N`.
- `ISemanticExtension` is per-file + disposed → cross-file/unit resolution goes in Layer 4 enricher.

## Reference (read-only, do not copy code)
cbm `src/pipeline/pass_terraform.c` + `pass_configlink.c` (MIT, C) — DOMAIN semantics only (which constructs map to which edges). ce ontology/lifecycle in the spec overrides cbm's flat vocabulary.
