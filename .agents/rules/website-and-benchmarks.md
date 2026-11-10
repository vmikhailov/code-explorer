---
trigger: always_on
---

# Website (`site/`) & Benchmarks Design, Copy, and Deployment Rules

Whenever creating, editing, or deploying pages in `site/` (`code-explorer.app`) or benchmark reports in `benchmarks/`:

## 1. Design System & Mandatory Headless Chrome Gate (`hallmark`)
- **Single Design Skill**: [`hallmark`](../skills/hallmark/SKILL.md) (`Console` dark graphite theme) is the **sole** website design system. Never install competing generic UI/frontend skills that conflict with Hallmark rules.
- **Zero-`FAIL` Layout & Copy Gate (`sloplint --render`)**: Before deploying any HTML/CSS edits in `site/`, ALWAYS run the 8-viewport (`320px`..`1920px`) Headless Chrome check and clean up temporary `.hallmark` screenshot directories afterward:
  ```powershell
  node .agents/skills/hallmark/scripts/sloplint.mjs site/index.html site/benchmarks.html site/benchmarks/01-*.html site/benchmarks/02-*.html site/benchmarks/03-*.html site/benchmarks/04-*.html site/benchmarks/05-*.html --render
  Remove-Item -Recurse -Force site/.hallmark, site/benchmarks/.hallmark -ErrorAction SilentlyContinue
  ```
- Never deploy while `sloplint` reports any `FAIL` (`0 FAIL` required).

## 2. Self-Hosted Zero-Flicker Fonts & Immutable CSS Caching
- **No External Google Fonts or `display=swap`**: Never link `fonts.googleapis.com` / `fonts.gstatic.com` or use `font-display: swap` (causes visible font flashing / FOUT on `F5` refresh).
- **Synchronous `fonts.css`**: Load `Host Grotesk` and `Atkinson Hyperlegible Mono` exclusively via local `site/fonts/fonts.css?v=1` (`data:font/woff2;base64` variable fonts with `font-display: block`), which decodes glyphs synchronously before First Paint.
- **Version Query Bump Required on CSS Edits**: Nginx on `code-explorer.app` serves `.css` and `.woff2` with `Cache-Control: public, max-age=31536000, immutable`. Whenever modifying `site/benchmarks/benchmarks.css` (or `site/fonts/fonts.css`), **ALWAYS increment the `?v=X.Y.Z` query parameter** across all HTML `<link rel="stylesheet">` tags so browsers fetch the updated stylesheet.

## 3. Readability Rounding vs Exact Table Telemetry
- **Hero Cards, `benchmarks.html` Catalog Outcomes & Prose**: Use clean display rounding so numbers match 1-to-1 between the `benchmarks.html` catalog page and the `.panels--4` hero cards on `01..05.html`:
  - **Values `< 10`**: keep **1 decimal place** (`3.4%`, `~4.4×`, `~5.8×`, `~6.1×`, `~7.2×`, `~1.5×`, `~2.2×`).
  - **Values `>= 10`**: round to **integer** (`~11×`, `~12×`, `~14×`, `~21×`, `&gt;18×`, `~98%`, `99%`, `~82%`).
  - **Tokens**: `~80K`, `~1.4M`, `~1.7M`, `~17.2M`.
- **Per-Case Detailed `<table>` Scorecards (`01..05.html`) & Markdown Tables**: Keep **exact** measured figures (`13.5×`, `20.9×`, `4.44×`, `12.08×`, `97.5%`, `3.4%`, `80,177`, `1,678,913`, `17.60 s`) verified directly against `results/metrics.json`.

## 4. Copy, Punctuation & Table Formatting Invariants
- **`vs` Without Period**: Always write `vs` (never `vs.`).
- **No Em/En Dashes in HTML Copy (`FAIL 19`)**: Never use `—` (`&mdash;`) or `–` (`&ndash;`) in visible HTML copy. Use `to` for ranges (`3 to 5 ms`, `Turns 5 to 8`, `Layer 1 to 3`), colons/commas in prose, and `&minus;` (`−`, U+2212: `<td class="cell-num tone-muted">&minus;</td>`) for empty/completed table cells.
- **Never Abbreviate Cumulative as `cum.` (and Bind Units with `&nbsp;`)**: Avoid `cum.` completely (unintended double meaning in English). Write `Cumulative Input` in column headers and `total` in cells, stacked on two lines with `&nbsp;` between numbers and units (`tokens`, `total`) so numbers never split from their label:
  ```html
  <td class="cell-num">14,644&nbsp;tokens<br><span class="tone-muted">28,341&nbsp;total</span></td>
  ```
- **Gap-Free Turn Progression Tables**: In turn-by-turn tables, account for every turn from `Turn 1` through the final turn using contiguous stages (`Turn 1`, `Turn 2`, `Turn 3`, `Turn 4`, `Turns 5 to 8`, `Turns 9 to 10`, ...) with zero skipped turn numbers.
- **Exact Model & Tool Names**: Keep model names (e.g., `Gemini 3.8 Flash`), agent identifiers, and MCP tool names consistent across headers, captions, prose, and collapsible drawers, matching `benchmarks/*/prompts/` and `benchmarks/*/scripts/` verbatim.

## 5. Responsive Layout Guardrails (`320px` to `1920px`)
- **0px Header Parity Across All Pages**: Keep `--container: 1440px`, `--gutter: 64px`, `--gap-columns: 32px`, `.slab-header { position: sticky; top: 0 }`, `.slab-bar { min-height: 60px; grid-template-columns: auto minmax(0, 1fr) auto }`, `.brand-mark` (`padding: var(--space-3xs) var(--space-xs); font-size: var(--text-xs)`), and `.btn` (`min-height: 44px; line-height: 1.25; background: transparent`) **100% identical** between `site/index.html` and `site/benchmarks/benchmarks.css` so the header never shifts by a single pixel on navigation. Keep `.slab-links` unwrapped (`Overview` + `All Benchmarks` only on benchmark pages).
- **Aligned Metric Panels**: Keep `.reading { white-space: nowrap }`, `.panel { justify-content: flex-start }`, and `.panel-code { margin-top: auto }` so titles, copy, and footers align horizontally across `.panels--3` and `.panels--4`.
- **Buttons & Tables**: Maintain `min-height: 44px` and `white-space: nowrap` on `.btn` without orphan button wraps (`2+1` or `3+1`), prevent mid-word breaks (`CodeExplore` / `r`) in table cells, and preserve `<720px` stacked mobile card layout on `.catalog-table`.

## 6. Single-Source-of-Truth JSON (`benchmarks/benchmarks-data.json`) & Mandatory Sync Script
- **Never Hardcode Benchmark Telemetry by Hand**: All benchmark numbers, tool versions, commit SHAs, pricing, and prompt drawers across `site/*.html`, `site/benchmarks/*.html`, `README.md`, and `benchmarks/README.md` are driven from [`benchmarks/benchmarks-data.json`](../../benchmarks/benchmarks-data.json) (which auto-ingests `benchmarks/*/results/{metrics.json,scores.csv}` and `benchmarks/*/prompts/*.md`).
- **Always Run `sync-benchmarks.mjs` After Re-Running Any Benchmark**:
  ```powershell
  node scripts/sync-benchmarks.mjs
  node scripts/sync-benchmarks.mjs --check
  ```
   HTML slots use `data-bind="..."`, `data-bind-html="..."`, `data-bind-meta="..."`, and `data-bind-file="..."`; Markdown tables use `<!-- BEGIN:md.* --> ... <!-- END:md.* -->`.
- **Single Dedicated "Tested Participants" Block (Zero Version Clutter Elsewhere)**: List exact tool versions (`CodeExplorer vX.Y.Z`, `CBM vX.Y.Z`, `Graphify vX.Y.Z`, `ripgrep vX.Y.Z`), LLM model (`Gemini 3.8 Flash`), and target repo commit SHA **exclusively** in a dedicated numbered block immediately below the result banners (`.panels--4` on case study pages `01..05`, and below the result tables on `benchmarks.html`). Keep `.crumb-bar`, `.hero-lede` prose, and `<th>` table column headers completely free of version numbers and commit hashes so they remain clean and readable.
