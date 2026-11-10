# Custom Tree-sitter Grammar Patches

This directory stores the upstream grammar and scanner patches used to build the custom ABI-14 native libraries on the fly into `cli/.Build/runtimes/<rid>/native/` via [`scripts/build-tree-sitter-natives.ps1`](../../../../../scripts/build-tree-sitter-natives.ps1), overriding the stock binaries from the `TreeSitter.DotNet` NuGet package during `dotnet build` and `dotnet publish`.

---

## 1. `tree-sitter-c-sharp` ([`tree-sitter-c-sharp.patch`](./tree-sitter-c-sharp.patch))

- **Fork Repository:** `https://github.com/vmikhailov/tree-sitter-c-sharp.git` (branch: `fix/contextual-keywords-with-linq`, commit: `3e84193374546752812dec17a06ac00eef21703d`)
- **Upstream Repository:** `https://github.com/tree-sitter/tree-sitter-c-sharp.git`
- **Base Commit:** `8c0abe0b84a3681d3e6852ce2e2a1eecb4f731c5`
- **Output Binaries (in `cli/.Build/runtimes/`):** `runtimes/{win-x64,win-arm64,linux-x64,linux-arm64,osx-x64,osx-arm64}/native/*tree-sitter-c-sharp*`
- **Fixes Included:**
  1. **`with` as contextual keyword identifier:** Adds `'with'` to the contextual identifier rule (`grammar.js`) so variables/parameters named `with` (e.g. `var with = items[0];`, `foreach (var with in items)`) parse cleanly without `ERROR`.
  2. **Pattern combinator keywords (`and`, `or`, `not`) in LINQ `from_clause`:** Allows `alias(choice('and', 'not', 'or'), $.identifier)` as the range variable name in `from_clause` (e.g. `from not in tokens from and in tokens from or in tokens select exp`).

---

## 2. `tree-sitter-typescript` ([`tree-sitter-typescript.patch`](./tree-sitter-typescript.patch))

- **Fork Repository:** `https://github.com/vmikhailov/tree-sitter-typescript.git` (branch: `fix-typescript-tsx-grammar-and-scanner`, commit: `e660d046fcbc92427ed5fba098fed002a58f642a`)
- **Upstream Repository:** `https://github.com/tree-sitter/tree-sitter-typescript.git`
- **Base Tag / Commit:** `v0.23.2` (`75b3874edb2dc714fb1fd77a32013d0f8699989f`)
- **Output Binaries (in `cli/.Build/runtimes/`):**
  - `runtimes/{win-x64,win-arm64,linux-x64,linux-arm64,osx-x64,osx-arm64}/native/*tree-sitter-typescript*`
  - `runtimes/{win-x64,win-arm64,linux-x64,linux-arm64,osx-x64,osx-arm64}/native/*tree-sitter-tsx*`
- **Fixes Included:**
  1. **`<typeof import('...')>` in generic `type_arguments` (`common/define-grammar.js`):**
     Replaces static precedence `[$.call_expression, $._type_query_call_expression]` with `prec('call', ...)` on `_type_query_call_expression` and adds `[$.call_expression, $._type_query_call_expression]` to `conflicts`, enabling GLR splitting on `jest.requireActual<typeof import('...')>(...)` and `importOriginal<typeof import('...')>()`.
  2. **`abstract override readonly` field modifier combinations (`common/define-grammar.js`):**
     Adds `optional($.override_modifier)` alongside `'abstract'` and `'readonly'` in `public_field_definition`.
  3. **Generic tagged template expressions (`common/define-grammar.js`):**
     Adds `prec('call', seq(field('function', $.expression), field('type_arguments', $.type_arguments), field('arguments', $.template_string)))` to `call_expression` so `styled.div<{ $isDimmed?: boolean }>` `` `...` `` parses without syntax errors or hallucinated `MISSING "!"` (`non_null_expression`) nodes.
  4. **Bare `&` in JSX text (`common/scanner.h`):**
     Updates `scan_jsx_text` to check whether `&` is followed by a valid HTML character reference (`&name;`, `&#123;`, `&#x1F;`). If not, `&` is consumed as part of `JSX_TEXT` instead of causing a syntax error.
  5. **`unique`, `unknown`, and `never` contextual keywords in `_reserved_identifier` (`common/define-grammar.js`):**
     Adds `'unique'`, `'unknown'`, and `'never'` (all contextual keywords from `predefined_type`) to `_reserved_identifier` so expressions like `i < unique.length`, `i < unknown.length`, and `i < never.length` parse as identifiers instead of failing when the context-aware lexer prioritizes `predefined_type` tokens after `<`.
  6. **Embedded NUL byte (`\0`) inside template strings (`common/scanner.h`):**
     Updates `scan_template_chars` to check `lexer->eof(lexer)` on `case '\0':` instead of unconditionally returning `false`, allowing template literals containing literal NUL bytes (e.g. `` `${a}\0${b}` ``) to scan cleanly until real EOF.
  7. **Bare `&` and URL query parameters in JSX string attributes (`common/define-grammar.js`):**
     Overrides `unescaped_double_jsx_string_fragment` and `unescaped_single_jsx_string_fragment` to allow arbitrary query strings and unescaped ampersands (`/[^"]+/` and `/[^']+/`) in JSX attributes (e.g. `href="https://example.com?foo=1&bar=2"`, `src="./iframe.html?id=desktop&viewMode=story"`) without spurious syntax errors.

---

## On-the-Fly Build & Cross-Compilation (`cli/.Build/runtimes`)

Native binaries are **not** stored in git. Instead, [`cli/Directory.Build.targets`](../../../../Directory.Build.targets) automatically invokes [`scripts/build-tree-sitter-natives.ps1`](../../../../../scripts/build-tree-sitter-natives.ps1) before building `CodeExplorer.Parser.Shared`, which:
1. Downloads a portable Zig toolchain (`0.13.0`) into `cli/.Build/tools/zig/` (if `zig` is not already on `PATH`).
2. Fetches the pinned ABI-14 C parser sources into `cli/.Build/tree-sitter-native/src/`.
3. Cross-compiles `src/parser.c` and `src/scanner.c` with `zig cc -shared -O2 -DNDEBUG` into `cli/.Build/runtimes/{win-x64,win-arm64,linux-x64,linux-arm64,osx-x64,osx-arm64}/native/`.
