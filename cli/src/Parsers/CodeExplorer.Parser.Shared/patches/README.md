# Custom Tree-sitter Grammar Patches

This directory stores the upstream grammar and scanner patches used to build the custom ABI-14 native libraries under [`../runtimes/`](../runtimes/), overriding the stock binaries from the `TreeSitter.DotNet` NuGet package.

---

## 1. `tree-sitter-c-sharp` ([`tree-sitter-c-sharp.patch`](./tree-sitter-c-sharp.patch))

- **Upstream Repository:** `https://github.com/tree-sitter/tree-sitter-c-sharp.git`
- **Base Commit:** `8c0abe0b84a3681d3e6852ce2e2a1eecb4f731c5`
- **Native Binaries:** `runtimes/{win-x64,win-arm64,linux-x64,linux-arm64,osx-x64,osx-arm64}/native/*tree-sitter-c-sharp*`
- **Fixes Included:**
  1. **`with` as contextual keyword identifier:** Adds `'with'` to the contextual identifier rule (`grammar.js`) so variables/parameters named `with` (e.g. `var with = items[0];`, `foreach (var with in items)`) parse cleanly without `ERROR`.
  2. **Pattern combinator keywords (`and`, `or`, `not`) in LINQ `from_clause`:** Allows `alias(choice('and', 'not', 'or'), $.identifier)` as the range variable name in `from_clause` (e.g. `from not in tokens from and in tokens from or in tokens select exp`).

---

## 2. `tree-sitter-typescript` ([`tree-sitter-typescript.patch`](./tree-sitter-typescript.patch))

- **Upstream Repository:** `https://github.com/tree-sitter/tree-sitter-typescript.git`
- **Base Tag / Commit:** `v0.23.2` (`75b3874edb2dc714fb1fd77a32013d0f8699989f`)
- **Native Binaries:**
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

---

## Regenerating & Cross-Compiling

1. Apply the patch to the upstream repository checkout.
2. Generate ABI-14 C parser sources (`TreeSitter.DotNet 1.1.1` requires `LANGUAGE_VERSION 14`):
   ```bash
   tree-sitter generate --abi=14
   ```
3. Compile `src/parser.c` and `src/scanner.c` with `-O2 -Isrc -DNDEBUG` for all 6 RIDs (`win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`) and place the resulting shared libraries in `../runtimes/<rid>/native/`.
