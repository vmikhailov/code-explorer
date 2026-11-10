---
trigger: always_on
---

# Recording & Publishing Benchmark Results (`benchmarks/` -> `site/` & `README.md`)

Whenever running, re-running, or adding a benchmark in `benchmarks/`:

> **CRITICAL RULE**: **NEVER manually type or edit benchmark numbers inside `site/*.html`, `site/benchmarks/*.html`, `README.md`, or `benchmarks/README.md`**. All telemetry, ratios, costs, rounded hero strings (`~14×`, `~21×`), exact table cells (`13.5×`, `20.9×`), turn progression tables, and prompt drawers are generated deterministically from JSON and prompt files by [`scripts/sync-benchmarks.mjs`](../../scripts/sync-benchmarks.mjs).

---

## Step 1: Save Raw Run Artifacts in `benchmarks/<case-dir>/`

Every benchmark run MUST persist its verbatim inputs and outputs before updating any summary:

1. **Prompts (`benchmarks/<case-dir>/prompts/*.md`)**:
   - Save the exact shared task prompt (`task-prompt.md`) and 100% symmetric agent system prompts (`ce-agent.md` / `graph-agent.md` and `cbm-agent.md` / `baseline-agent.md`).
   - YAML subagent frontmatter (`--- ... ---`) at the top of `prompts/*.md` is stripped automatically when `sync-benchmarks.mjs` injects `data-bind-file="..."` into HTML `<details class="prompt-drawer">` blocks.

2. **Verbatim Agent Reports (`benchmarks/<case-dir>/results/*-agent-output.md`)**:
   - Save the exact unedited final markdown answer delivered via `send_message` by each agent (`ce-agent-output.md` / `graph-agent-output.md` and `cbm-agent-output.md` / `baseline-agent-output.md`).

3. **Machine-Readable Telemetry (`results/metrics.json` or `results/scores.csv`)**:
   - **For Agent Benchmarks (`cs02`, `cs05`, and new agent cases)** — write `benchmarks/<case-dir>/results/metrics.json` extracted directly from `<appDataDir>/brain/<conversation-id>/.system_generated/logs/transcript_full.jsonl` using this schema:
     ```json
     {
       "benchmark_id": "05-polyglot-architecture-and-blast-radius",
       "repository": "OrchardCMS/OrchardCore",
       "model": "Gemini 3.8 Flash",
       "repository_scale": {
         "total_projects": 180,
         "source_files": 6000,
         "graph_nodes": 120000,
         "graph_relationships": 280000
       },
       "graph_agent": {
         "conversation_id": "<uuid>",
         "time_to_answer_seconds": 120.0,
         "wall_clock_seconds": 145.0,
         "total_steps": 30,
         "llm_turns": 12,
         "context_compactions": 0,
         "total_tool_calls": 18,
         "tool_breakdown": {
           "run_command": 10,
           "view_file": 7,
           "write_to_file": 0,
           "send_message": 1
         },
         "tool_output_chars": 85000,
         "tool_output_est_tokens": 21250,
         "cumulative_actual_input_chars": 1800000,
         "cumulative_actual_input_est_tokens": 450000,
         "api_uncached_input_tokens": 65000,
         "api_cached_input_tokens": 385000,
         "api_total_input_tokens": 450000,
         "api_output_tokens": 9500,
         "cost_flash_usd": 0.08
       },
       "baseline_agent": {
         "conversation_id": "<uuid>",
         "time_to_answer_seconds": 420.0,
         "wall_clock_seconds": 460.0,
         "total_steps": 95,
         "llm_turns": 42,
         "context_compactions": 0,
         "total_tool_calls": 64,
         "tool_breakdown": {
           "run_command": 35,
           "view_file": 28,
           "write_to_file": 0,
           "send_message": 1
         },
         "tool_output_chars": 310000,
         "tool_output_est_tokens": 77500,
         "cumulative_actual_input_chars": 6400000,
         "cumulative_actual_input_est_tokens": 1600000,
         "cost_flash_usd": 0.24
       }
     }
     ```
   - **For `cs01` (`01-agent-audit-ce-vs-cbm-on-twenty/results/metrics.json`)** — preserve the `{ "ce": { ..., "milestones": { "turn_1": { "active_tokens", "cum_input_tokens" }, ... } }, "cbm": { ..., "milestones": { ... } } }` schema so the 28-turn progression table (`cs01.turns_tbody`) regenerates automatically.
   - **For Deterministic Engine Benchmarks (`cs03`, `cs04`)** — write `results/scores.csv` (`repo,part,category,tool,gt_count,matched,recall,total_extracted,notes`) and `results/mcp-benchmark.json`.

---

## Step 2: Update Metadata in `benchmarks/benchmarks-data.json`

Open [`benchmarks/benchmarks-data.json`](../../benchmarks/benchmarks-data.json) and update only the non-telemetry metadata that changed for that case:
- `ce_version` (e.g., `"v1.76.0"`) and `meta.tools.ce_suite_versions` (e.g., `"v1.75.0 to v1.76.0"`).
- `repo.commit` (pinned 8-char git SHA), `repo.projects` / `repo.files` / `repo.loc` if target repo changed.
- `recall` counts/percentages (`gt_total`, `ce_matched`, `ce_pct`, `rg_pct`, missed items).
- For `cs01`: update `effort_details` and `turn_stages` (contiguous gap-free stages from `Turn 1` to final turn) if the turn count changed.
- For pending benchmarks (like `cs05` on `OrchardCore`): once `results/metrics.json` has `"repository": "OrchardCMS/OrchardCore"`, `sync-benchmarks.mjs` automatically flips `cs05.status` from `"pending"` to `"ready"`; also wire `data-bind-file` on `05-*.html`'s task-prompt drawer once `prompts/task-prompt.md` is finalized.

---

## Step 3: Run `sync-benchmarks.mjs` & Verify Zero Drift

Run the sync generator from the repository root:

```powershell
node scripts/sync-benchmarks.mjs
node scripts/sync-benchmarks.mjs --check
```

What this updates automatically:
- `benchmarks/benchmarks-data.json` (ingests exact numbers & API costs from `results/{metrics.json,scores.csv}`).
- `site/index.html` (`#benchmark` diff rows & summary panels).
- `site/benchmarks.html` (Track 1 & Track 2 catalog outcomes + Tested Participants block).
- `site/benchmarks/01-*.html` .. `05-*.html` (`<meta name="description">`, `.panels--4` hero cards, Tested Participants block, Scorecard `<table>` cells, `cs01` Turn-by-turn `<tbody>`, and `<pre data-bind-file="...">` prompt drawers).
- `README.md` & `benchmarks/README.md` (`<!-- BEGIN:md.* --> ... <!-- END:md.* -->` tables and participant versions).

Also update the narrative per-case `benchmarks/<case-dir>/README.md` prose/tables if qualitative findings changed.

---

## Step 4: Run Layout & Copy Gate (`sloplint --render`) Before Deploying

Whenever `site/` HTML files were updated by `sync-benchmarks.mjs`, run the Headless Chrome gate (`0 FAIL` required) and clean temporary `.hallmark` screenshot directories:

```powershell
node .agents/skills/hallmark/scripts/sloplint.mjs site/index.html site/benchmarks.html site/benchmarks/01-*.html site/benchmarks/02-*.html site/benchmarks/03-*.html site/benchmarks/04-*.html site/benchmarks/05-*.html --render
Remove-Item -Recurse -Force site/.hallmark, site/benchmarks/.hallmark -ErrorAction SilentlyContinue
```

---

## Step 5: Adding New Bound Slots (When Extending Pages)

If you add a new metric card, table cell, or Markdown summary table:
1. Add the raw/metadata field to `benchmarks/benchmarks-data.json` and compute its formatted string in `buildBindings()` inside `scripts/sync-benchmarks.mjs` (using `fmtRoundMult` / `fmtRoundTokens` for cards & prose, and exact decimals / `fmtInt` for `<table>` cells).
2. Bind it in HTML with `data-bind="key"` (escaped text), `data-bind-html="key"` (rich HTML), `data-bind-meta="key"` (`<meta content>`), or `data-bind-file="benchmarks/.../prompt.md"`, and in Markdown with `<!-- BEGIN:key --> ... <!-- END:key -->` or `<!-- bind:key -->...<!-- /bind -->`.
3. Re-run `node scripts/sync-benchmarks.mjs --check` to confirm `0` drift.
