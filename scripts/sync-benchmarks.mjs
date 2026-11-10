#!/usr/bin/env node
/**
 * sync-benchmarks.mjs
 *
 * Single-source-of-truth benchmark synchronization pipeline:
 *   1. Ingests raw telemetry from `benchmarks/<id>/results/{metrics.json,scores.csv}`
 *      into `benchmarks/benchmarks-data.json`.
 *   2. Computes derived ratios, costs, rounded hero strings (`~14×`, `~21×`, `~98%`),
 *      and exact table telemetry (`13.5×`, `20.9×`, `97.5%`) per `.agents/rules/website-and-benchmarks.md`.
 *   3. Updates bound slots (`data-bind`, `data-bind-html`, `data-bind-meta`, `data-bind-file`)
 *      across `site/*.html` and `site/benchmarks/*.html`, plus `<!-- BEGIN:... -->` blocks
 *      in `README.md` and `benchmarks/README.md`.
 *
 * Usage:
 *   node scripts/sync-benchmarks.mjs           # Sync all files in-place
 *   node scripts/sync-benchmarks.mjs --check   # Verify zero drift (CI mode)
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');
const DATA_PATH = path.join(ROOT, 'benchmarks', 'benchmarks-data.json');
const CHECK_ONLY = process.argv.includes('--check');

// ---------------------------------------------------------------------------
// Formatting Helpers (enforcing .agents/rules/website-and-benchmarks.md)
// ---------------------------------------------------------------------------

const fmtInt = (n) => Math.round(n).toLocaleString('en-US');

const escapeHtml = (s) =>
  String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');

/** Hero/card/catalog rounding: integers for >= 10 (`~14×`, `~21×`), 1 decimal for < 10 (`~4.4×`, `~6.1×`). */
function fmtRoundMult(x) {
  if (x >= 10) return `~${Math.round(x)}×`;
  return `~${x.toFixed(1)}×`;
}

/** Percentage rounding: 1 decimal for < 10 (`3.4%`), integer for >= 10 (`99%`, `~98%`). */
function fmtDisplayPct(pct, withTilde = false) {
  if (pct === 0 || pct === 100) return `${pct}%`;
  const prefix = withTilde ? '~' : '';
  if (pct < 10) return `${prefix}${pct.toFixed(1)}%`;
  return `${prefix}${Math.round(pct)}%`;
}

/** Readable token count with tilde for cards/prose (`~80K`, `~1.7M`, `~5.7M`, `~17.2M`). */
function fmtRoundTokens(n) {
  if (n >= 1_000_000) return `~${(n / 1_000_000).toFixed(1)}M`;
  return `~${Math.round(n / 1_000)}K`;
}

/** Compact token count without tilde for table summaries (`80K`, `1.68M`, `5.72M`, `17.19M`). */
function fmtCompactTokens(n, decimalsM = 2) {
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(decimalsM)}M`;
  if (n >= 100_000) return `${Math.round(n / 1_000)}K`;
  return `${(n / 1_000).toFixed(1).replace(/\.0$/, '')}K`;
}

/** Short duration (`41s`, `1m 49s`, `9m 15s`, `14m 06s`). */
function fmtDurShort(sec, padSec = false) {
  const s = Math.round(sec);
  if (s < 60) return `${s}s`;
  const m = Math.floor(s / 60);
  const rem = s % 60;
  return `${m}m ${padSec ? String(rem).padStart(2, '0') : rem}s`;
}

/** Long duration for tables (`0m 41s` or `14 min 06 sec`). */
function fmtDurMinSec(sec) {
  const s = Math.round(sec);
  const m = Math.floor(s / 60);
  const rem = String(s % 60).padStart(2, '0');
  return `${m} min ${rem} sec`;
}

function fmtSavedShort(savedSec) {
  const s = Math.round(savedSec);
  const m = Math.floor(s / 60);
  const rem = s % 60;
  return `-${m}m ${String(rem).padStart(2, '0')}s`;
}

function calcApiCostRaw(uncached, cached, output, rates) {
  return (
    (uncached * rates.input + cached * rates.cached_input + output * rates.output) /
    1_000_000
  );
}

function calcApiCost(uncached, cached, output, rates) {
  return Math.round(calcApiCostRaw(uncached, cached, output, rates) * 100) / 100;
}

function parseScoresCsv(relPath) {
  const abs = path.join(ROOT, relPath);
  if (!fs.existsSync(abs)) return null;
  const lines = fs.readFileSync(abs, 'utf8').trim().split(/\r?\n/);
  const headers = lines[0].split(',');
  return lines.slice(1).map((line) => {
    const cols = line.split(',');
    const row = {};
    headers.forEach((h, idx) => {
      row[h] = cols[idx];
    });
    row.gt_count = Number(row.gt_count);
    row.matched = Number(row.matched);
    row.recall = Number(row.recall);
    return row;
  });
}

// ---------------------------------------------------------------------------
// Step 1: Ingest Raw Result Artifacts -> benchmarks-data.json
// ---------------------------------------------------------------------------

function ingestRawSources(data) {
  let rawMilestones01 = null;

  // Case Study 01
  const m01Path = path.join(ROOT, data.cs01.source_metrics);
  if (fs.existsSync(m01Path)) {
    const m01 = JSON.parse(fs.readFileSync(m01Path, 'utf8'));
    rawMilestones01 = { ce: m01.ce.milestones, cbm: m01.cbm.milestones };
    const flashRates = data.meta.pricing_usd_per_1m.gemini_3_8_flash;
    const proRates = data.meta.pricing_usd_per_1m.gemini_3_8_pro;

    for (const key of ['ce', 'cbm']) {
      const src = m01[key];
      const dst = data.cs01.telemetry[key];
      dst.conversation_short = src.conversation_id.slice(0, 8);
      dst.time_to_answer_s = src.time_to_answer_s;
      dst.wall_clock_s = src.total_wall_s;
      dst.turns = src.llm_turns;
      dst.steps = src.total_steps;
      dst.tool_calls = src.tool_calls_total;
      dst.raw_tool_chars = src.raw_tool_chars;
      dst.raw_tool_tokens = src.raw_tool_tokens;
      dst.input_tokens_uncached = src.input_tokens;
      dst.cache_read_tokens = src.cache_read_tokens;
      dst.cum_input_tokens = src.cum_input_tokens;
      dst.output_tokens = src.output_tokens;
      dst.cost_flash_usd = calcApiCost(src.input_tokens, src.cache_read_tokens, src.output_tokens, flashRates);
      dst.cost_pro_usd = calcApiCost(src.input_tokens, src.cache_read_tokens, src.output_tokens, proRates);
    }
  }

  // Case Study 02
  const m02Path = path.join(ROOT, data.cs02.source_metrics);
  if (fs.existsSync(m02Path)) {
    const m02 = JSON.parse(fs.readFileSync(m02Path, 'utf8'));
    if (m02.repository_scale) {
      data.cs02.repo.modules = m02.repository_scale.total_projects;
      data.cs02.repo.services = m02.repository_scale.microservices;
      data.cs02.repo.shared_libraries = m02.repository_scale.shared_libraries;
      data.cs02.repo.workers = m02.repository_scale.workers;
      data.cs02.repo.files = m02.repository_scale.source_files;
      data.cs02.repo.graph_nodes = m02.repository_scale.graph_nodes;
      data.cs02.repo.graph_edges = m02.repository_scale.graph_relationships;
    }
    const g = m02.graph_agent;
    const b = m02.baseline_agent;
    const dstCe = data.cs02.telemetry.ce;
    dstCe.conversation_short = g.conversation_id.slice(0, 8);
    dstCe.time_to_answer_s = g.time_to_answer_seconds;
    dstCe.wall_clock_s = g.wall_clock_seconds;
    dstCe.turns = g.llm_turns;
    dstCe.steps = g.total_steps;
    dstCe.tool_calls = g.total_tool_calls;
    dstCe.run_command_calls = g.tool_breakdown.run_command || 0;
    dstCe.view_file_calls = g.tool_breakdown.view_file || 0;
    dstCe.scripts_written = g.tool_breakdown.write_to_file || g.tool_breakdown.write_file || 0;
    dstCe.compactions = g.context_compactions;
    dstCe.raw_tool_chars = g.tool_output_chars;
    dstCe.raw_tool_tokens = g.tool_output_est_tokens;
    dstCe.cum_input_chars = g.cumulative_actual_input_chars;
    dstCe.cum_input_est_tokens = g.cumulative_actual_input_est_tokens;
    const flashRates = data.meta.pricing_usd_per_1m.gemini_3_8_flash;
    const proRates = data.meta.pricing_usd_per_1m.gemini_3_8_pro;
    if (Array.isArray(g.turn_by_turn_curve) && g.turn_by_turn_curve.length > 0) {
      const lastTurn = g.turn_by_turn_curve[g.turn_by_turn_curve.length - 1];
      const uncachedEst = lastTurn.active_context_est_tokens || 0;
      const cachedEst = Math.max(0, g.cumulative_actual_input_est_tokens - uncachedEst);
      const outEst = g.output_and_thinking_est_tokens || 0;
      dstCe.cost_flash_usd = calcApiCost(uncachedEst, cachedEst, outEst, flashRates);
      dstCe.cost_pro_usd = calcApiCost(uncachedEst, cachedEst, outEst, proRates);
    }
    if (g.api_total_input_tokens) {
      dstCe.api_total_input_tokens = g.api_total_input_tokens;
      dstCe.api_cost_flash_usd = calcApiCost(
        g.api_uncached_input_tokens,
        g.api_cached_input_tokens,
        g.api_output_tokens,
        flashRates
      );
      dstCe.api_cost_pro_usd = calcApiCost(
        g.api_uncached_input_tokens,
        g.api_cached_input_tokens,
        g.api_output_tokens,
        proRates
      );
    }

    const dstRg = data.cs02.telemetry.rg;
    dstRg.conversation_short = b.conversation_id.slice(0, 8);
    dstRg.time_to_answer_s = b.time_to_answer_seconds;
    dstRg.wall_clock_s = b.wall_clock_seconds;
    dstRg.turns = b.llm_turns;
    dstRg.steps = b.total_steps;
    dstRg.tool_calls = b.total_tool_calls;
    dstRg.run_command_calls = b.tool_breakdown.run_command || 0;
    dstRg.view_file_calls = b.tool_breakdown.view_file || 0;
    dstRg.scripts_written = b.tool_breakdown.write_to_file || b.tool_breakdown.write_file || 0;
    dstRg.manage_task_calls = b.tool_breakdown.manage_task || 0;
    dstRg.compactions = b.context_compactions;
    dstRg.raw_tool_chars = b.tool_output_chars;
    dstRg.raw_tool_tokens = b.tool_output_est_tokens;
    dstRg.cum_input_chars = b.cumulative_actual_input_chars;
    dstRg.cum_input_est_tokens = b.cumulative_actual_input_est_tokens;
  }

  // Case Studies 03 & 04 CSV scores
  for (const [csKey, compTool] of [
    ['cs03', 'cbm'],
    ['cs04', 'graphify'],
  ]) {
    const rows = parseScoresCsv(data[csKey].source_scores_csv);
    if (!rows) continue;
    const p1Ce = rows.filter((r) => r.part === 'Part 1' && r.tool === 'ce');
    const p1Comp = rows.filter((r) => r.part === 'Part 1' && r.tool === compTool);
    const p2Ce = rows.filter((r) => r.part === 'Part 2' && r.tool === 'ce');

    const rec = data[csKey].recall;
    rec.group_a_gt = p1Ce.reduce((s, r) => s + r.gt_count, 0);
    rec.group_a_ce_matched = p1Ce.reduce((s, r) => s + r.matched, 0);
    rec[`group_a_${compTool}_matched`] = p1Comp.reduce((s, r) => s + r.matched, 0);

    let wins = 0;
    let ties = 0;
    let losses = 0;
    for (const ceRow of p1Ce) {
      const compRow = p1Comp.find((r) => r.repo === ceRow.repo && r.category === ceRow.category);
      if (!compRow) continue;
      if (ceRow.matched > compRow.matched) wins++;
      else if (ceRow.matched === compRow.matched) ties++;
      else losses++;
    }
    rec.group_a_wins = wins;
    rec.group_a_ties = ties;
    rec.group_a_losses = losses;

    const immCe = p1Ce.filter((r) => r.repo === 'immich');
    const immComp = p1Comp.filter((r) => r.repo === 'immich');
    rec.immich_gt = immCe.reduce((s, r) => s + r.gt_count, 0);
    rec.immich_ce_matched = immCe.reduce((s, r) => s + r.matched, 0);
    rec[`immich_${compTool}_matched`] = immComp.reduce((s, r) => s + r.matched, 0);

    rec.group_b_gt = p2Ce.reduce((s, r) => s + r.gt_count, 0);
    rec.group_b_ce_matched = p2Ce.reduce((s, r) => s + r.matched, 0);
  }

  // Case Study 05 (auto-activates when OrchardCore metrics.json lands)
  const m05Path = path.join(ROOT, data.cs05.source_metrics);
  if (fs.existsSync(m05Path)) {
    const m05 = JSON.parse(fs.readFileSync(m05Path, 'utf8'));
    const repoMatches = (m05.repository || '')
      .toLowerCase()
      .includes((data.cs05.expected_repo_match || '').toLowerCase());
    if ((repoMatches && m05.status !== 'pending' && m05.graph_agent) || data.cs05.status === 'ready') {
      data.cs05.status = 'ready';
      const g = m05.graph_agent;
      const b = m05.baseline_agent;
      data.cs05.telemetry = {
        ce: {
          time_to_answer_s: g.time_to_answer_seconds,
          wall_clock_s: g.wall_clock_seconds,
          turns: g.llm_turns,
          tool_calls: g.total_tool_calls,
          view_file_calls: g.tool_breakdown?.view_file || 0,
          raw_tool_chars: g.tool_output_chars,
          raw_tool_tokens: g.tool_output_est_tokens,
          cum_input_tokens: g.cumulative_actual_input_est_tokens,
          cost_flash_usd: g.cost_flash_usd || 0.08,
        },
        rg: {
          time_to_answer_s: b.time_to_answer_seconds,
          wall_clock_s: b.wall_clock_seconds,
          turns: b.llm_turns,
          tool_calls: b.total_tool_calls,
          view_file_calls: b.tool_breakdown?.view_file || 0,
          raw_tool_chars: b.tool_output_chars,
          raw_tool_tokens: b.tool_output_est_tokens,
          cum_input_tokens: b.cumulative_actual_input_est_tokens,
          cost_flash_usd: b.cost_flash_usd || 0.14,
        },
      };
    }
  }

  return rawMilestones01;
}

// ---------------------------------------------------------------------------
// Step 2: Build Derived Bindings Dictionary
// ---------------------------------------------------------------------------

function buildBindings(data, rawMilestones01) {
  const b = {};

  // --- Case Study 01 ---
  {
    const c1 = data.cs01;
    const ce = c1.telemetry.ce;
    const cbm = c1.telemetry.cbm;
    const speedup = cbm.time_to_answer_s / ce.time_to_answer_s;
    const wallSpeedup = cbm.wall_clock_s / ce.wall_clock_s;
    const turnsRatio = cbm.turns / ce.turns;
    const callsRatio = cbm.tool_calls / ce.tool_calls;
    const payloadRatio = cbm.raw_tool_chars / ce.raw_tool_chars;
    const tokRatio = cbm.cum_input_tokens / ce.cum_input_tokens;
    const outTokRatio = cbm.output_tokens / ce.output_tokens;
    const flashRates = data.meta.pricing_usd_per_1m.gemini_3_8_flash;
    const ceCostExact = calcApiCostRaw(ce.input_tokens_uncached, ce.cache_read_tokens, ce.output_tokens, flashRates);
    const cbmCostExact = calcApiCostRaw(cbm.input_tokens_uncached, cbm.cache_read_tokens, cbm.output_tokens, flashRates);
    const costRatio = cbmCostExact / ceCostExact;

    const ceRecPct = fmtDisplayPct((c1.recall.ce_matched / c1.recall.gt_total) * 100);
    const cbmRecExact = ((c1.recall.cbm_graph_matched / c1.recall.gt_total) * 100).toFixed(1);
    const cbmRecDisplay = fmtDisplayPct((c1.recall.cbm_graph_matched / c1.recall.gt_total) * 100);
    const recMult = Math.round(c1.recall.ce_matched / c1.recall.cbm_graph_matched);

    b['cs01.meta_desc'] =
      `Paired AI agent audit on ${c1.repo.name} (${fmtInt(c1.repo.files)} files, ${c1.repo.loc} LOC) with 100% symmetric system prompts: ` +
      `CodeExplorer Agent (${fmtDurShort(ce.time_to_answer_s)} answer / ${fmtDurShort(ce.wall_clock_s)} wall, ${ce.turns} turns, ${fmtRoundTokens(ce.cum_input_tokens)} tokens, ${c1.effort_details.ce_file_reads} files read) vs ` +
      `codebase-memory-mcp Agent (${fmtDurShort(cbm.time_to_answer_s)} answer / ${fmtDurShort(cbm.wall_clock_s)} wall, ${cbm.turns} turns, ${fmtRoundTokens(cbm.cum_input_tokens)} tokens, ${c1.effort_details.cbm_scripts} TS AST scripts).`;

    b['cs01.crumb_scale'] = `${c1.repo.packages} packages, ${fmtInt(c1.repo.files)} files, ${c1.repo.loc} LOC`;
    b['cs01.card_speedup'] = fmtRoundMult(speedup);
    b['cs01.card_speed_copy'] =
      `<code>CodeExplorer</code> answered in <strong>${fmtDurShort(ce.time_to_answer_s)}</strong> (<strong>${fmtDurShort(ce.wall_clock_s)}</strong> wall); ` +
      `<code>CBM</code> took <strong>${fmtDurShort(cbm.time_to_answer_s)}</strong> (<strong>${fmtDurShort(cbm.wall_clock_s)}</strong> wall).`;
    b['cs01.card_turns_footer'] = `${ce.turns} turns vs ${cbm.turns} turns`;

    b['cs01.card_tokens'] = fmtRoundMult(tokRatio);
    b['cs01.card_tokens_copy'] =
      `<code>${fmtRoundTokens(ce.cum_input_tokens)}</code> cumulative input tokens vs <code>${fmtRoundTokens(cbm.cum_input_tokens)}</code> tokens across the session.`;
    b['cs01.card_cost_footer'] =
      `$${ce.cost_flash_usd.toFixed(2)} vs $${cbm.cost_flash_usd.toFixed(2)} (${fmtRoundMult(costRatio)})`;

    b['cs01.card_recall'] = `${ceRecPct} vs ${cbmRecDisplay}`;
    b['cs01.card_recall_copy'] =
      `<code>ce</code> resolved <strong>${c1.recall.ce_matched} / ${c1.recall.gt_total}</strong> items directly from <code>graph.db</code>. ` +
      `<code>CBM</code> returned <strong>${c1.recall.cbm_graph_matched} / ${c1.recall.gt_total}</strong> (only self-calls).`;
    b['cs01.card_recall_footer'] = `${c1.effort_details.ce_file_reads} source files read by ce`;

    b['cs01.card_scripts'] = `${c1.effort_details.ce_scripts} vs ${c1.effort_details.cbm_scripts}`;
    b['cs01.card_scripts_copy'] =
      `After ${c1.effort_details.cbm_queries} <code>CBM</code> queries returned 0 cross-file edges, the <code>CBM</code> agent wrote ${c1.effort_details.cbm_scripts} custom TS AST scripts &amp; read ${c1.effort_details.cbm_file_reads} files.`;
    b['cs01.card_scripts_footer'] =
      `${c1.effort_details.ce_scripts} scripts vs ${fmtInt(c1.effort_details.cbm_scripts_loc)} LOC of JS`;

    b['cs01.participants_html'] = [
      `<strong>Tested participants:</strong><br>`,
      `          1. <strong>CodeExplorer (<code>ce</code>):</strong> <code>${c1.ce_version}</code> (.NET 10, SQLite + OpenCypher)<br>`,
      `          2. <strong>codebase-memory-mcp (<code>CBM</code>):</strong> <code>${c1.cbm_version}</code> (Go + SQLite)<br>`,
      `          3. <strong>LLM Model:</strong> <code>${c1.model}</code> (100% symmetric system prompts)<br>`,
      `          4. <strong>Target Repository:</strong> <code>${c1.repo.name}</code> (commit <code>${c1.repo.commit}</code>, ${c1.repo.packages} packages, ${fmtInt(c1.repo.files)} files, ${c1.repo.loc} LOC)`,
    ].join('\n');

    // Scorecard table cells
    b['cs01.tbl_time_cbm'] = `${Math.round(cbm.time_to_answer_s)} s (${fmtDurShort(cbm.time_to_answer_s)})`;
    b['cs01.tbl_time_ce'] = `${Math.round(ce.time_to_answer_s)} s (${fmtDurShort(ce.time_to_answer_s)})`;
    b['cs01.tbl_time_delta'] = `${speedup.toFixed(1)}× faster (${fmtSavedShort(cbm.time_to_answer_s - ce.time_to_answer_s)})`;

    b['cs01.tbl_wall_cbm'] = `${Math.round(cbm.wall_clock_s)} s (${fmtDurShort(cbm.wall_clock_s)})`;
    b['cs01.tbl_wall_ce'] = `${Math.round(ce.wall_clock_s)} s (${fmtDurShort(ce.wall_clock_s)})`;
    b['cs01.tbl_wall_delta'] = `${wallSpeedup.toFixed(1)}× faster (${fmtSavedShort(cbm.wall_clock_s - ce.wall_clock_s)})`;

    b['cs01.tbl_turns_cbm'] = `${cbm.turns} turns (${cbm.steps} steps)`;
    b['cs01.tbl_turns_ce'] = `${ce.turns} turns (${ce.steps} steps)`;
    b['cs01.tbl_turns_delta'] = `${turnsRatio.toFixed(1)}× fewer turns`;

    b['cs01.tbl_calls_cbm'] =
      `${cbm.tool_calls} (${c1.effort_details.cbm_queries} <code>cbm</code>, ${c1.effort_details.cbm_rg_node_runs} <code>rg</code>/Node, ${c1.effort_details.cbm_file_reads} <code>view_file</code>, ${c1.effort_details.cbm_scripts} scripts, 1 <code>send_message</code>)`;
    b['cs01.tbl_calls_ce'] =
      `${ce.tool_calls} (${c1.effort_details.ce_ontology_calls} <code>ce taxonomy</code>, ${c1.effort_details.ce_cypher_calls} <code>ce query</code>, ${c1.effort_details.ce_file_reads} file reads, 1 <code>send_message</code>)`;
    b['cs01.tbl_calls_delta'] = `${callsRatio.toFixed(1)}× fewer calls (${c1.effort_details.ce_file_reads} file reads)`;

    b['cs01.tbl_payload_cbm'] = `${Math.round(cbm.raw_tool_chars / 1000)} KB (~${(cbm.raw_tool_tokens / 1000).toFixed(1)}K tokens)`;
    b['cs01.tbl_payload_ce'] = `${(ce.raw_tool_chars / 1000).toFixed(1)} KB (~${(ce.raw_tool_tokens / 1000).toFixed(1)}K tokens)`;
    b['cs01.tbl_payload_delta'] = `${payloadRatio.toFixed(1)}× smaller payload`;

    b['cs01.tbl_in_cbm'] =
      `${fmtInt(cbm.cum_input_tokens)} tokens (${Math.round(cbm.input_tokens_uncached / 1000)}K uncached + ${(cbm.cache_read_tokens / 1_000_000).toFixed(2)}M cached)`;
    b['cs01.tbl_in_ce'] =
      `${fmtInt(ce.cum_input_tokens)} tokens (${(ce.input_tokens_uncached / 1000).toFixed(1)}K uncached + ${(ce.cache_read_tokens / 1000).toFixed(1)}K cached)`;
    b['cs01.tbl_in_delta'] = `${tokRatio.toFixed(1)}× fewer input tokens`;

    b['cs01.tbl_out_cbm'] = `${fmtInt(cbm.output_tokens)} tokens`;
    b['cs01.tbl_out_ce'] = `${fmtInt(ce.output_tokens)} tokens`;
    b['cs01.tbl_out_delta'] = `${outTokRatio.toFixed(1)}× fewer output tokens`;

    b['cs01.tbl_cost_cbm'] = `$${cbm.cost_flash_usd.toFixed(2)} / $${cbm.cost_pro_usd.toFixed(2)}`;
    b['cs01.tbl_cost_ce'] = `$${ce.cost_flash_usd.toFixed(2)} / $${ce.cost_pro_usd.toFixed(2)}`;
    b['cs01.tbl_cost_delta'] = `${costRatio.toFixed(1)}× cheaper`;

    b['cs01.tbl_rec_cbm'] = `${cbmRecExact}% (${c1.recall.cbm_graph_matched} / ${c1.recall.gt_total} items)`;
    b['cs01.tbl_rec_ce'] = `${ceRecPct} (${c1.recall.ce_matched} / ${c1.recall.gt_total} items)`;
    b['cs01.tbl_rec_delta'] = `${recMult}× more items from graph`;

    b['cs01.sec3_lede'] =
      `Every LLM turn re-sends the active conversation history. Because the <code>CodeExplorer</code> agent finished in ${ce.turns} turns while the <code>CBM</code> agent iterated through ${cbm.turns} turns of AST script writing and file reading, cumulative billed input tokens differed by <strong>${fmtRoundMult(tokRatio)}</strong> (<code>${fmtRoundTokens(ce.cum_input_tokens)}</code> vs <code>${fmtRoundTokens(cbm.cum_input_tokens)}</code>).`;

    if (rawMilestones01) {
      const rowsHtml = c1.turn_stages.map((st) => {
        const cbmM = rawMilestones01.cbm[`turn_${st.end_turn}`];
        const cbmCell = st.cbm_loss
          ? `<td class="cell-loss">${fmtInt(cbmM.active_tokens)}&nbsp;tokens<br>${fmtInt(cbmM.cum_input_tokens)}&nbsp;total</td>`
          : `<td class="cell-num">${fmtInt(cbmM.active_tokens)}&nbsp;tokens<br><span class="tone-muted">${fmtInt(cbmM.cum_input_tokens)}&nbsp;total</span></td>`;

        let ceCell = `<td class="cell-num tone-muted">&minus;</td>`;
        if (st.ce_turn != null) {
          const ceM = rawMilestones01.ce[`turn_${st.ce_turn}`];
          ceCell = st.ce_win
            ? `<td class="cell-win">${fmtInt(ceM.active_tokens)}&nbsp;tokens<br>${fmtInt(ceM.cum_input_tokens)}&nbsp;total</td>`
            : `<td class="cell-num">${fmtInt(ceM.active_tokens)}&nbsp;tokens<br><span class="tone-muted">${fmtInt(ceM.cum_input_tokens)}&nbsp;total</span></td>`;
        }

        return [
          `              <tr>`,
          `                <td>${st.label}</td>`,
          `                ${cbmCell}`,
          `                ${ceCell}`,
          `                <td>${st.activity}</td>`,
          `              </tr>`,
        ].join('\n');
      });
      b['cs01.turns_tbody'] = '\n' + rowsHtml.join('\n') + '\n            ';
    }

    // Catalog & landing bindings for CS01
    b['catalog.cs01_outcome'] =
      `<strong>${fmtRoundMult(speedup)} faster</strong> (${fmtDurShort(ce.time_to_answer_s)} vs ${fmtDurShort(cbm.time_to_answer_s)})<br>` +
      `<strong>${fmtRoundMult(tokRatio)} fewer tokens</strong> (${fmtRoundTokens(ce.cum_input_tokens)} vs ${fmtRoundTokens(cbm.cum_input_tokens)})<br>` +
      `<strong>${ceRecPct} vs ${cbmRecDisplay}</strong> native graph recall`;

    b['index.cs01_cbm_val'] = `${fmtDurShort(cbm.time_to_answer_s)}, ${cbm.turns} turns, ${fmtRoundTokens(cbm.cum_input_tokens)} tokens`;
    b['index.cs01_ce_val'] = `${fmtDurShort(ce.time_to_answer_s)} (${fmtRoundMult(speedup)} faster), ${ce.turns} turns, ${fmtRoundTokens(ce.cum_input_tokens)} tokens`;
    b['index.cs01_cbm_rec'] = `${cbmRecDisplay} (${c1.recall.cbm_graph_matched}/${c1.recall.gt_total}) from CBM graph`;
    b['index.cs01_ce_rec'] = `${ceRecPct} (${c1.recall.ce_matched}/${c1.recall.gt_total}) directly from graph.db`;
    b['index.card_token_comp'] = fmtRoundMult(tokRatio);
  }

  // --- Case Study 02 ---
  {
    const c2 = data.cs02;
    const ce = c2.telemetry.ce;
    const rg = c2.telemetry.rg;
    const speedup = rg.time_to_answer_s / ce.time_to_answer_s;
    const wallSpeedup = rg.wall_clock_s / ce.wall_clock_s;
    const turnsRatio = rg.turns / ce.turns;
    const callsRatio = rg.tool_calls / ce.tool_calls;
    const readsRatio = rg.view_file_calls / Math.max(1, ce.view_file_calls);
    const payloadRatio = rg.raw_tool_chars / ce.raw_tool_chars;
    const tokRatio = rg.cum_input_est_tokens / ce.cum_input_est_tokens;
    const costRatio = rg.cost_flash_usd / ce.cost_flash_usd;
    const turnsSavedPct = (((rg.turns - ce.turns) / rg.turns) * 100).toFixed(1);

    b['cs02.meta_desc'] =
      `Paired AI agent audit across a ${c2.repo.services}-service multi-repo workspace (${fmtInt(c2.repo.files)} files): ` +
      `CodeExplorer (${fmtDurShort(ce.time_to_answer_s)}, ${ce.turns} turns, ${fmtRoundTokens(ce.cum_input_est_tokens)} tokens, ${c2.recall.ce_pct} recall) vs ` +
      `ripgrep (${fmtDurShort(rg.time_to_answer_s)}, ${rg.turns} turns, ${fmtRoundTokens(rg.cum_input_est_tokens)} tokens, ${c2.recall.rg_pct_round} recall).`;

    b['cs02.card_speedup'] = fmtRoundMult(speedup);
    b['cs02.card_speed_copy'] =
      `<code>${fmtDurShort(ce.time_to_answer_s)}</code> (<code>${Math.round(ce.time_to_answer_s)}s</code>) with CodeExplorer (including <code>ce taxonomy</code> discovery) vs ` +
      `<code>${fmtDurShort(rg.time_to_answer_s)}</code> (<code>${fmtInt(rg.time_to_answer_s)}s</code>) with <code>ripgrep</code> + Python scripts.`;
    b['cs02.card_turns_footer'] = `${ce.turns} turns vs ${rg.turns} turns (${fmtRoundMult(turnsRatio)})`;

    b['cs02.card_tokens'] = fmtRoundMult(tokRatio);
    b['cs02.card_tokens_copy'] =
      `<code>${fmtRoundTokens(ce.cum_input_est_tokens)}</code> cumulative input tokens (<code>${ce.compactions}</code> compactions) vs ` +
      `<code>${fmtRoundTokens(rg.cum_input_est_tokens)}</code> post-compaction tokens.`;
    b['cs02.card_cost_footer'] = `$${ce.cost_flash_usd.toFixed(2)} vs $${rg.cost_flash_usd.toFixed(2)} (${fmtRoundMult(costRatio)})`;

    b['cs02.card_reads'] = `${ce.view_file_calls} vs ${rg.view_file_calls}`;
    b['cs02.card_reads_copy'] =
      `Without schema hints in the prompt, the <code>ce</code> agent discovered the 5-layer ontology via <code>ce taxonomy</code> and needed <code>${ce.view_file_calls}</code> file reads vs <code>${rg.view_file_calls}</code> file reads and <code>${rg.scripts_written}</code> scripts.`;
    b['cs02.card_reads_footer'] = `${ce.tool_calls} tool calls vs ${rg.tool_calls} tool calls`;

    b['cs02.card_recall'] = `${c2.recall.ce_pct} vs ${c2.recall.rg_pct_round}`;
    b['cs02.card_recall_footer'] = `${c2.recall.ce_verified_callers}/${c2.recall.ce_verified_callers} callers vs ${c2.recall.rg_missed_services} services missed`;
    b['cs02.conv_ce'] = ce.conversation_short;

    b['cs02.participants_html'] = [
      `<strong>Tested participants:</strong><br>`,
      `          1. <strong>CodeExplorer (<code>ce</code>):</strong> <code>${c2.ce_version}</code> (.NET 10, SQLite + OpenCypher, schema <code>${c2.schema_version}</code>: ${fmtInt(c2.repo.graph_nodes)} nodes, ${fmtInt(c2.repo.graph_edges)} edges)<br>`,
      `          2. <strong>Lexical CLI Baseline:</strong> <code>ripgrep</code> (<code>rg ${c2.ripgrep_version}</code>) + Python <code>${c2.python_version}</code> + <code>view_file</code><br>`,
      `          3. <strong>LLM Model:</strong> <code>${c2.model}</code> (symmetric system prompts with zero schema spoilers)<br>`,
      `          4. <strong>Target Workspace:</strong> Enterprise Multi-Repo Microservices System (${c2.repo.modules} independent service/library repositories, ${c2.repo.services} services, ${fmtInt(c2.repo.files)} files; service names anonymized 1-to-1)`,
    ].join('\n');

    // Scorecard table cells
    b['cs02.th_rg'] = `<code>ripgrep</code> + Python (${rg.run_command_calls} CLI runs + ${rg.view_file_calls} file reads)`;
    b['cs02.th_ce'] = `<code>CodeExplorer</code> (${ce.run_command_calls} CLI runs + ${ce.view_file_calls} file reads)`;

    b['cs02.tbl_time_rg'] = `${fmtInt(rg.time_to_answer_s)}.0 s (${fmtDurMinSec(rg.time_to_answer_s)})`;
    b['cs02.tbl_time_ce'] = `${fmtInt(ce.time_to_answer_s)}.0 s (${fmtDurMinSec(ce.time_to_answer_s)})`;
    b['cs02.tbl_time_delta'] = `${speedup.toFixed(2)}× faster (${fmtSavedShort(rg.time_to_answer_s - ce.time_to_answer_s)})`;

    b['cs02.tbl_wall_rg'] = `${fmtInt(rg.wall_clock_s)}.0 s (${fmtDurMinSec(rg.wall_clock_s)})`;
    b['cs02.tbl_wall_ce'] = `${fmtInt(ce.wall_clock_s)}.0 s (${fmtDurMinSec(ce.wall_clock_s)})`;
    b['cs02.tbl_wall_delta'] = `${wallSpeedup.toFixed(2)}× faster (${fmtSavedShort(rg.wall_clock_s - ce.wall_clock_s)})`;

    b['cs02.tbl_turns_rg'] = `${rg.turns} turns (${rg.steps} steps)`;
    b['cs02.tbl_turns_ce'] = `${ce.turns} turns (${ce.steps} steps)`;
    b['cs02.tbl_turns_delta'] = `${turnsRatio.toFixed(2)}× fewer turns (-${turnsSavedPct}%)`;

    b['cs02.tbl_calls_rg'] =
      `${rg.tool_calls} (${rg.run_command_calls} <code>run_command</code>, ${rg.view_file_calls} <code>view_file</code>, ${rg.scripts_written} <code>write_to_file</code>, ${rg.manage_task_calls} <code>manage_task</code>, 1 <code>send_message</code>)`;
    b['cs02.tbl_calls_ce'] =
      `${ce.tool_calls} (${ce.run_command_calls} <code>run_command</code>, ${ce.view_file_calls} <code>view_file</code>, ${ce.scripts_written} scripts written, 1 <code>send_message</code>)`;
    b['cs02.tbl_calls_delta'] = `${callsRatio.toFixed(2)}× fewer tool calls (${readsRatio.toFixed(2)}× fewer file reads)`;

    b['cs02.tbl_payload_rg'] = `${fmtInt(rg.raw_tool_chars)} chars (~${fmtInt(rg.raw_tool_tokens)} tokens)`;
    b['cs02.tbl_payload_ce'] = `${fmtInt(ce.raw_tool_chars)} chars (~${fmtInt(ce.raw_tool_tokens)} tokens)`;
    b['cs02.tbl_payload_delta'] = `${payloadRatio.toFixed(2)}× smaller tool payload`;

    const apiTokRatio = (rg.cum_input_est_tokens / ce.api_total_input_tokens).toFixed(2);
    b['cs02.tbl_in_rg'] = `${fmtInt(rg.cum_input_chars)} chars (~${fmtInt(rg.cum_input_est_tokens)} tokens)`;
    b['cs02.tbl_in_ce'] = `${fmtInt(ce.cum_input_chars)} chars (~${fmtInt(ce.cum_input_est_tokens)} est. / ${fmtInt(ce.api_total_input_tokens)} API tokens)`;
    b['cs02.tbl_in_delta'] = `${tokRatio.toFixed(2)}× fewer input tokens (${apiTokRatio}× vs API)`;

    b['cs02.tbl_cost_rg'] = `$${rg.cost_flash_usd.toFixed(2)} / $${rg.cost_pro_usd.toFixed(2)}`;
    b['cs02.tbl_cost_ce'] = ce.api_cost_flash_usd
      ? `$${ce.cost_flash_usd.toFixed(2)} / $${ce.cost_pro_usd.toFixed(2)} ($${ce.api_cost_flash_usd.toFixed(2)} / $${ce.api_cost_pro_usd.toFixed(2)} API)`
      : `$${ce.cost_flash_usd.toFixed(2)} / $${ce.cost_pro_usd.toFixed(2)}`;
    b['cs02.tbl_cost_delta'] = `${costRatio.toFixed(2)}× cheaper (Flash)`;

    b['cs02.graph_scale'] = `${fmtInt(c2.repo.graph_nodes)} Nodes, ${fmtInt(c2.repo.graph_edges)} Edges in .codeexplorer/graph.db`;

    b['catalog.cs02_outcome'] =
      `<strong>${fmtRoundMult(speedup)} faster</strong> (${fmtDurShort(ce.time_to_answer_s)} vs ${fmtDurShort(rg.time_to_answer_s)})<br>` +
      `<strong>${fmtRoundMult(tokRatio)} fewer tokens</strong> (${fmtRoundTokens(ce.cum_input_est_tokens)} vs ${fmtRoundTokens(rg.cum_input_est_tokens)})<br>` +
      `<strong>${c2.recall.ce_pct} vs ${c2.recall.rg_pct_round}</strong> (rg missed ${c2.recall.rg_missed_services} services)`;

    b['index.cs02_ce_label'] =
      `<code>CodeExplorer</code> (${ce.run_command_calls} CLI commands + ${ce.view_file_calls} file reads, ${ce.scripts_written} scripts, ${ce.compactions} compactions)`;
    b['index.cs02_rg_val'] =
      `${fmtDurShort(rg.time_to_answer_s)}, ${rg.turns} turns, ${fmtRoundTokens(rg.cum_input_est_tokens)} tokens ($${rg.cost_flash_usd.toFixed(2)})`;
    b['index.cs02_ce_val'] =
      `${fmtDurShort(ce.time_to_answer_s)} (${fmtRoundMult(speedup)} faster), ${ce.turns} turns, ${fmtRoundTokens(ce.cum_input_est_tokens)} tokens ($${ce.cost_flash_usd.toFixed(2)})`;
    b['index.card_token_copy'] =
      `Reduced cumulative input tokens from ~${fmtCompactTokens(data.cs01.telemetry.cbm.cum_input_tokens, 2)} to ${fmtRoundTokens(data.cs01.telemetry.ce.cum_input_tokens)} on twentyhq/twenty (and ${fmtRoundMult(tokRatio)} on ${c2.repo.services} services).`;
  }

  // --- Case Studies 03 & 04 ---
  {
    const c3 = data.cs03;
    const c4 = data.cs04;
    const cePctA = Math.round((c3.recall.group_a_ce_matched / c3.recall.group_a_gt) * 100);
    const cbmPctA = Math.round((c3.recall.group_a_cbm_matched / c3.recall.group_a_gt) * 100);
    const grPctA = Math.round((c4.recall.group_a_graphify_matched / c4.recall.group_a_gt) * 100);
    const cePctBExact = ((c3.recall.group_b_ce_matched / c3.recall.group_b_gt) * 100).toFixed(1);
    const cePctBRound = `~${Math.round((c3.recall.group_b_ce_matched / c3.recall.group_b_gt) * 100)}%`;

    const grImmichSpeedup = c4.indexing.immich.graphify_s / c4.indexing.immich.ce_s;
    const grTwentyMinSpeedup = c4.indexing.twenty.graphify_timeout_s / c4.indexing.twenty.ce_s;

    b['cs03.card_group_a'] = `${cePctA}% vs ${cbmPctA}%`;
    b['cs03.card_group_b'] = cePctBRound;
    b['cs03.card_scan'] = `${Math.round(c3.indexing.twenty.cbm_s)}s vs ${Math.round(c3.indexing.twenty.ce_s)}s`;
    b['cs03.participants_html'] = [
      `<strong>Tested participants:</strong><br>`,
      `          1. <strong>CodeExplorer (<code>ce</code>):</strong> <code>${c3.ce_version}</code> (.NET 10, SQLite + OpenCypher, ${data.meta.tools.ce_mcp_tools} MCP tools)<br>`,
      `          2. <strong>codebase-memory-mcp (<code>CBM</code>):</strong> <code>${c3.cbm_version}</code> (<code>win32-x64</code> Go + SQLite binary, ${data.meta.tools.cbm_mcp_tools} MCP tools)<br>`,
      `          3. <strong>Target Repositories:</strong> <code>twentyhq/twenty</code> (commit <code>${c3.repos.twenty.commit}</code>, ${fmtInt(c3.repos.twenty.files)} files, ${c3.repos.twenty.loc} LOC) &amp; <code>immich-app/immich</code> (commit <code>${c3.repos.immich.commit}</code>, ${fmtInt(c3.repos.immich.files)} files, ${c3.repos.immich.loc} LOC)`,
    ].join('\n');

    b['cs04.card_speedup'] = `${fmtRoundMult(grImmichSpeedup)} / &gt;${Math.floor(grTwentyMinSpeedup)}×`;
    b['cs04.card_group_a'] = `${cePctA}% vs ${grPctA}%`;
    b['cs04.card_group_b'] = cePctBRound;
    b['cs04.participants_html'] = [
      `<strong>Tested participants:</strong><br>`,
      `          1. <strong>CodeExplorer (<code>ce</code>):</strong> <code>${c4.ce_version}</code> (.NET 10, SQLite + OpenCypher, ${data.meta.tools.ce_mcp_tools} MCP tools)<br>`,
      `          2. <strong>Graphify (<code>graphifyy</code>):</strong> <code>${c4.graphify_version}</code> (Python ${data.meta.tools.python_version}, Tree-sitter + NetworkX <code>--code-only</code>, ${data.meta.tools.graphify_mcp_tools} MCP tools)<br>`,
      `          3. <strong>Target Repositories:</strong> <code>twentyhq/twenty</code> (commit <code>${c4.repos.twenty.commit}</code>, ${fmtInt(c4.repos.twenty.files)} files, ${c4.repos.twenty.loc} LOC) &amp; <code>immich-app/immich</code> (commit <code>${c4.repos.immich.commit}</code>, ${fmtInt(c4.repos.immich.files)} files, ${c4.repos.immich.loc} LOC)`,
    ].join('\n');

    b['catalog.cs03_outcome'] =
      `<strong class="tone-ok">Basic Code: ${cePctA}%</strong> vs ${cbmPctA}% (${c3.recall.group_a_ce_matched}/${c3.recall.group_a_gt} vs ${c3.recall.group_a_cbm_matched}/${c3.recall.group_a_gt})<br>` +
      `<strong class="tone-ok">DB &amp; Queues: ${cePctBRound}</strong> vs 0% (${c3.recall.group_b_ce_matched}/${c3.recall.group_b_gt})<br>` +
      `<strong class="tone-accent">Scan:</strong> <code>ce</code> faster on <code>immich</code>; <code>CBM</code> faster on <code>twenty</code>`;

    b['catalog.cs04_outcome'] =
      `<strong>${fmtRoundMult(grImmichSpeedup)} faster scan</strong> on <code>immich</code> (~${Math.round(c4.indexing.immich.ce_s)}s vs ~${Math.round(c4.indexing.immich.graphify_s)}s)<br>` +
      `<strong>&gt;${Math.floor(grTwentyMinSpeedup)}× faster</strong> on <code>twenty</code> (~${Math.round(c4.indexing.twenty.ce_s)}s vs DNF &gt;45m)<br>` +
      `<strong>Basic Code: ${cePctA}%</strong> vs ${grPctA}% · <strong>DB &amp; Queues: ${cePctBRound}</strong>`;

    b['catalog.participants_html'] = [
      `<strong>Tested participants &amp; versions across the benchmark suite:</strong><br>`,
      `          1. <strong>CodeExplorer (<code>ce</code>):</strong> <code>${data.meta.tools.ce_suite_versions}</code> (.NET 10, SQLite + OpenCypher, ${data.meta.tools.ce_mcp_tools} MCP tools)<br>`,
      `          2. <strong>codebase-memory-mcp (<code>CBM</code>):</strong> <code>${data.meta.tools.cbm_version}</code> (<code>win32-x64</code> Go + SQLite binary, ${data.meta.tools.cbm_mcp_tools} MCP tools)<br>`,
      `          3. <strong>Graphify (<code>graphifyy</code>):</strong> <code>${data.meta.tools.graphify_version}</code> (Python ${data.meta.tools.python_version}, Tree-sitter + NetworkX <code>--code-only</code>, ${data.meta.tools.graphify_mcp_tools} MCP tools)<br>`,
      `          4. <strong>Lexical CLI Baseline:</strong> <code>ripgrep</code> (<code>rg ${data.meta.tools.ripgrep_version}</code>) + Python <code>${data.meta.tools.python_version}</code> + <code>view_file</code><br>`,
      `          5. <strong>LLM Model (Track 1):</strong> <code>${data.meta.llm_model}</code> (100% symmetric system prompts)<br>`,
      `          6. <strong>Pinned Target Commits:</strong> <code>twentyhq/twenty @ ${data.cs01.repo.commit}</code> (Case 01), <code>twentyhq/twenty @ ${c3.repos.twenty.commit}</code> &amp; <code>immich-app/immich @ ${c3.repos.immich.commit}</code> (Cases 03 &amp; 04), <code>${data.cs05.repo.name} @ ${data.cs05.repo.commit}</code> (Case 05)`,
    ].join('\n');

    b['index.cs0304_p1_comp'] = `CBM: ${cbmPctA}% (${c3.recall.group_a_cbm_matched}/${c3.recall.group_a_gt}) | graphify: ${grPctA}% (${c4.recall.group_a_graphify_matched}/${c4.recall.group_a_gt})`;
    b['index.cs0304_p1_ce'] = `${cePctA}% recall (${c4.recall.group_a_ce_matched}/${c4.recall.group_a_gt}, ${c4.recall.group_a_wins} wins, ${c4.recall.group_a_ties} ties, ${c4.recall.group_a_losses} losses)`;
    b['index.cs0304_p2_ce'] = `${cePctBRound} micro-recall (${c3.recall.group_b_ce_matched}/${c3.recall.group_b_gt} verified items)`;
    b['index.card_arch_recall'] = cePctBRound;
  }

  // --- Case Study 05 (populated automatically once status === 'ready') ---
  if (data.cs05.status === 'ready' && data.cs05.telemetry) {
    const c5 = data.cs05;
    const ce = c5.telemetry.ce;
    const rg = c5.telemetry.rg;
    const speedup = rg.time_to_answer_s / ce.time_to_answer_s;
    const tokRatio = rg.cum_input_tokens / ce.cum_input_tokens;
    const readsRatio = rg.view_file_calls / Math.max(1, ce.view_file_calls);
    const turnsRatio = rg.turns / ce.turns;
    const callsRatio = rg.tool_calls / ce.tool_calls;
    const payloadRatio = rg.raw_tool_chars / ce.raw_tool_chars;
    const costRatio = rg.cost_flash_usd / ce.cost_flash_usd;

    b['cs05.card_speedup'] = fmtRoundMult(speedup);
    b['cs05.card_speed_copy'] =
      `<code>${fmtDurShort(ce.time_to_answer_s, true)}</code> with <code>CodeExplorer</code> vs <code>${fmtDurShort(rg.time_to_answer_s, true)}</code> with <code>ripgrep</code> + <code>view_file</code> across <code>OrchardCore</code> modules.`;
    b['cs05.card_turns_footer'] = `${ce.turns} turns vs ${rg.turns} turns`;

    b['cs05.card_tokens'] = fmtRoundMult(tokRatio);
    b['cs05.card_tokens_copy'] =
      `<code>${fmtRoundTokens(ce.cum_input_tokens)}</code> cumulative input tokens with <code>CodeExplorer</code> vs <code>${fmtRoundTokens(rg.cum_input_tokens)}</code> tokens with <code>ripgrep</code>.`;
    b['cs05.card_cost_footer'] = `$${ce.cost_flash_usd.toFixed(2)} vs $${rg.cost_flash_usd.toFixed(2)} API cost`;

    b['cs05.card_reads'] = rg.view_file_calls > 0 && ce.view_file_calls === 0
      ? `0 vs ${rg.view_file_calls}`
      : fmtRoundMult(readsRatio);

    b['cs05.tbl_time_rg'] = `${fmtInt(rg.time_to_answer_s)} s (${fmtDurMinSec(rg.time_to_answer_s)})`;
    b['cs05.tbl_time_ce'] = `${fmtInt(ce.time_to_answer_s)} s (${fmtDurMinSec(ce.time_to_answer_s)})`;
    b['cs05.tbl_time_delta'] = `${speedup.toFixed(2)}× faster`;
    b['cs05.tbl_turns_rg'] = `${rg.turns} turns`;
    b['cs05.tbl_turns_ce'] = `${ce.turns} turns`;
    b['cs05.tbl_turns_delta'] = `${turnsRatio.toFixed(2)}× fewer turns`;
    b['cs05.tbl_calls_rg'] = `${rg.tool_calls} (<code>rg</code> + <code>view_file</code>)`;
    b['cs05.tbl_calls_ce'] = `${ce.tool_calls} (<code>ce</code> + <code>view_file</code>)`;
    b['cs05.tbl_calls_delta'] = `${callsRatio.toFixed(2)}× fewer tool calls`;
    b['cs05.tbl_payload_rg'] = `${fmtInt(rg.raw_tool_chars)} chars (~${fmtInt(rg.raw_tool_tokens)} tokens)`;
    b['cs05.tbl_payload_ce'] = `${fmtInt(ce.raw_tool_chars)} chars (~${fmtInt(ce.raw_tool_tokens)} tokens)`;
    b['cs05.tbl_payload_delta'] = `${payloadRatio.toFixed(2)}× less tool payload`;
    b['cs05.tbl_in_rg'] = `${fmtInt(rg.cum_input_tokens)} tokens`;
    b['cs05.tbl_in_ce'] = `${fmtInt(ce.cum_input_tokens)} tokens`;
    b['cs05.tbl_in_delta'] = `${tokRatio.toFixed(2)}× fewer input tokens`;
    b['cs05.tbl_cost_rg'] = `$${rg.cost_flash_usd.toFixed(2)}`;
    b['cs05.tbl_cost_ce'] = `$${ce.cost_flash_usd.toFixed(2)}`;
    b['cs05.tbl_cost_delta'] = `${costRatio.toFixed(2)}× cheaper`;

    b['catalog.cs05_outcome'] =
      `<strong class="tone-ok">${fmtRoundMult(speedup)} faster</strong> (${fmtDurShort(ce.time_to_answer_s, true)} vs ${fmtDurShort(rg.time_to_answer_s, true)})<br>` +
      `<strong class="tone-ok">${fmtRoundMult(tokRatio)} fewer tokens</strong> (${fmtRoundTokens(ce.cum_input_tokens)} vs ${fmtRoundTokens(rg.cum_input_tokens)})<br>` +
      `<strong>${c5.recall.ce_pct} vs ${c5.recall.rg_pct}</strong> module recall`;
  }

  // --- GitHub Markdown Blocks (README.md & benchmarks/README.md) ---
  {
    const c1 = data.cs01;
    const ce1 = c1.telemetry.ce;
    const cbm1 = c1.telemetry.cbm;
    const sp1 = (cbm1.time_to_answer_s / ce1.time_to_answer_s).toFixed(1);
    const tk1 = (cbm1.cum_input_tokens / ce1.cum_input_tokens).toFixed(1);
    const ce1Rec = Math.round((c1.recall.ce_matched / c1.recall.gt_total) * 100);
    const cbm1Rec = ((c1.recall.cbm_graph_matched / c1.recall.gt_total) * 100).toFixed(1);

    const c2 = data.cs02;
    const ce2 = c2.telemetry.ce;
    const rg2 = c2.telemetry.rg;
    const sp2 = (rg2.time_to_answer_s / ce2.time_to_answer_s).toFixed(2);
    const tk2 = (rg2.cum_input_est_tokens / ce2.cum_input_est_tokens).toFixed(2);
    const cost2Ratio = (rg2.cost_flash_usd / ce2.cost_flash_usd).toFixed(2);

    b['md.root_agent_table'] = [
      `| Metric | **\`twentyhq/twenty\` (\`24.5k\` files): \`CBM\` → \`CodeExplorer\`** ([Case Study 01](./benchmarks/01-agent-audit-ce-vs-cbm-on-twenty/README.md)) | **324-Service Multi-Repo (\`13.8k\` files): \`ripgrep\` → \`CodeExplorer\`** ([Case Study 02](./benchmarks/02-enterprise-microservices-deep-audit/README.md)) |`,
      `| :--- | :--- | :--- |`,
      `| **Time to Answer** | \`${fmtDurShort(cbm1.time_to_answer_s)}\` → **\`${fmtDurShort(ce1.time_to_answer_s)}\` (\`${sp1}×\` faster)** | \`${fmtDurShort(rg2.time_to_answer_s)}\` → **\`${fmtDurShort(ce2.time_to_answer_s)}\` (\`${sp2}×\` faster, symmetric prompts)** |`,
      `| **Cumulative Input Tokens** | \`${fmtCompactTokens(cbm1.cum_input_tokens, 2)}\` → **\`${fmtCompactTokens(ce1.cum_input_tokens, 1)}\` tokens (\`${tk1}×\` fewer tokens)** | \`${fmtCompactTokens(rg2.cum_input_est_tokens, 2)}\` → **\`${fmtCompactTokens(ce2.cum_input_est_tokens, 2)}\` tokens (\`${tk2}×\` fewer tokens, \`${cost2Ratio}×\` cheaper)** |`,
      `| **Agent Effort** | \`${cbm1.turns}\` turns (\`${c1.effort_details.cbm_queries}\` \`CBM\` queries + \`${c1.effort_details.cbm_scripts}\` AST scripts + \`${c1.effort_details.cbm_file_reads}\` file reads) → **\`${ce1.turns}\` turns (\`${c1.effort_details.ce_cypher_calls}\` graph queries, \`${c1.effort_details.ce_file_reads}\` source reads)** | \`${rg2.turns}\` turns (\`${rg2.run_command_calls}\` shell commands, \`${rg2.view_file_calls}\` file reads, \`${rg2.scripts_written}\` scripts) → **\`${ce2.turns}\` turns (\`${ce2.run_command_calls}\` CLI runs, \`${ce2.view_file_calls}\` file reads, \`${ce2.scripts_written}\` scripts)** |`,
      `| **Blast-Radius Recall** | \`${cbm1Rec}%\` (\`${c1.recall.cbm_graph_matched} / ${c1.recall.gt_total}\`) from \`CBM\` graph → **\`${ce1Rec}%\` (\`${c1.recall.ce_matched} / ${c1.recall.gt_total}\`) directly from \`graph.db\`** | \`82%\` *(missed ${c2.recall.rg_missed_services} RTB services behind env constants)* → **\`${c2.recall.ce_pct}\` recall (\`0\` blind spots)** |`,
    ].join('\n');

    const cs05Row =
      data.cs05.status === 'ready' && data.cs05.telemetry
        ? `| **[Case Study 05: Modular .NET Monolith Audit](./05-polyglot-architecture-and-blast-radius/README.md)**<br>Cross-module C# interface hierarchies, DI call chains & YesSql indexes | \`${data.cs05.repo.name}\`<br>\`${data.cs05.repo.projects}\` C# projects, \`${data.cs05.repo.files}\` files (.NET, ASP.NET Core, YesSql) | \`CodeExplorer\`<br>vs<br>\`ripgrep\` | **\`${(data.cs05.telemetry.rg.time_to_answer_s / data.cs05.telemetry.ce.time_to_answer_s).toFixed(2)}×\` faster**<br>\`${fmtDurShort(data.cs05.telemetry.ce.time_to_answer_s, true)}\` vs \`${fmtDurShort(data.cs05.telemetry.rg.time_to_answer_s, true)}\`<br>\`${data.cs05.telemetry.ce.turns}\` turns vs \`${data.cs05.telemetry.rg.turns}\` turns | **\`${(data.cs05.telemetry.rg.cum_input_tokens / data.cs05.telemetry.ce.cum_input_tokens).toFixed(2)}×\` fewer tokens**<br>\`${fmtInt(data.cs05.telemetry.ce.cum_input_tokens)}\` vs \`${fmtInt(data.cs05.telemetry.rg.cum_input_tokens)}\`<br>(\`$${data.cs05.telemetry.ce.cost_flash_usd.toFixed(2)}\` vs \`$${data.cs05.telemetry.rg.cost_flash_usd.toFixed(2)}\`) | **\`${data.cs05.recall.ce_pct}\` vs \`${data.cs05.recall.rg_pct}\`**<br>\`${data.cs05.telemetry.ce.view_file_calls}\` file reads vs \`${data.cs05.telemetry.rg.view_file_calls}\` file reads |`
        : `| **[Case Study 05: Modular .NET Monolith Audit](./05-polyglot-architecture-and-blast-radius/README.md)**<br>Cross-module C# interface hierarchies, DI call chains & YesSql indexes | \`OrchardCMS/OrchardCore\`<br>\`[TBD ~180]\` C# projects, \`[TBD ~6,000]\` files (.NET, ASP.NET Core, YesSql) | \`CodeExplorer\`<br>vs<br>\`ripgrep\` | **\`[TBD ×]\` faster**<br>\`[TBD]\` vs \`[TBD]\`<br>\`[TBD]\` turns vs \`[TBD]\` turns | **\`[TBD ×]\` fewer tokens**<br>\`[TBD]\` vs \`[TBD]\`<br>(\`$[TBD]\` vs \`$[TBD]\`) | **\`[TBD %]\` vs \`[TBD %]\`**<br>\`[TBD]\` file reads vs \`[TBD]\` file reads |`;

    b['md.catalog_agent_table'] = [
      `| Case Study & Task | Repository Under Test | Compared Tools | Time & LLM Turns | Cumulative Input Tokens | Recall & Outcome |`,
      `| :--- | :--- | :--- | :--- | :--- | :--- |`,
      `| **[Case Study 01: Public Monorepo Audit](./01-agent-audit-ce-vs-cbm-on-twenty/README.md)**<br>DI fan-in, inheritance, BullMQ queues & API-to-DB write lineage | \`${c1.repo.name}\`<br>\`${c1.repo.packages}\` packages, \`${fmtInt(c1.repo.files)}\` files, \`${c1.repo.loc}\` LOC (TS, NestJS, GraphQL, TypeORM, BullMQ) | \`CodeExplorer\`<br>vs<br>\`CBM\` | **\`${sp1}×\` faster**<br>\`${fmtDurShort(ce1.time_to_answer_s)}\` vs \`${fmtDurShort(cbm1.time_to_answer_s)}\`<br>\`${ce1.turns}\` turns vs \`${cbm1.turns}\` turns | **\`${tk1}×\` fewer tokens**<br>\`${fmtInt(ce1.cum_input_tokens)}\` vs \`${fmtInt(cbm1.cum_input_tokens)}\`<br>(\`$${ce1.cost_flash_usd.toFixed(2)}\` vs \`$${cbm1.cost_flash_usd.toFixed(2)}\`) | **Native Graph: \`${ce1Rec}%\` vs \`${cbm1Rec}%\`**<br>\`${c1.recall.ce_matched}/${c1.recall.gt_total}\` from \`ce\` (\`${c1.effort_details.ce_file_reads}\` file reads) vs \`${c1.recall.cbm_graph_matched}/${c1.recall.gt_total}\` from \`CBM\` (wrote \`${c1.effort_details.cbm_scripts}\` AST scripts + \`${c1.effort_details.cbm_file_reads}\` file reads) |`,
      `| **[Case Study 02: 324-Service Enterprise Audit](./02-enterprise-microservices-deep-audit/README.md)**<br>Shared ORM tables, outbound HTTP fan-in, Pub/Sub & RabbitMQ topology (zero schema hints) | Enterprise Multi-Repo Workspace<br>\`${c2.repo.modules}\` independent service/library repos (\`${c2.repo.services}\` services, \`${fmtInt(c2.repo.files)}\` files, TS, Go, SQL) | \`CodeExplorer\`<br>vs<br>\`ripgrep\` + Python | **\`${sp2}×\` faster**<br>\`${fmtDurShort(ce2.time_to_answer_s, true)}\` vs \`${fmtDurShort(rg2.time_to_answer_s, true)}\`<br>\`${ce2.turns}\` turns vs \`${rg2.turns}\` turns | **\`${tk2}×\` fewer tokens**<br>\`${fmtInt(ce2.cum_input_est_tokens)}\` vs \`${fmtInt(rg2.cum_input_est_tokens)}\`<br>(\`$${ce2.cost_flash_usd.toFixed(2)}\` vs \`$${rg2.cost_flash_usd.toFixed(2)}\`, \`${cost2Ratio}×\` cheaper) | **\`${c2.recall.ce_pct}\` vs \`${c2.recall.rg_pct_round}\`**<br>\`0\` missed services (\`${ce2.view_file_calls}\` file reads, \`${ce2.scripts_written}\` scripts) vs \`${c2.recall.rg_missed_services}\` RTB services missed (\`${rg2.view_file_calls}\` file reads + \`${rg2.scripts_written}\` scripts) |`,
      cs05Row,
    ].join('\n');

    b['md.catalog_participants'] = [
      `1. **CodeExplorer (\`ce\`)**: \`${data.meta.tools.ce_suite_versions}\` (.NET 10, SQLite + OpenCypher, \`${data.meta.tools.ce_mcp_tools}\` MCP tools)`,
      `2. **codebase-memory-mcp (\`CBM\`)**: \`${data.meta.tools.cbm_version}\` (\`win32-x64\` Go + SQLite binary, \`${data.meta.tools.cbm_mcp_tools}\` MCP tools)`,
      `3. **Graphify (\`graphifyy\`)**: \`${data.meta.tools.graphify_version}\` (Python ${data.meta.tools.python_version}, Tree-sitter + NetworkX \`--code-only\`, \`${data.meta.tools.graphify_mcp_tools}\` MCP tools)`,
      `4. **Lexical CLI Baseline**: \`ripgrep\` (\`rg ${data.meta.tools.ripgrep_version}\`) + Python \`${data.meta.tools.python_version}\` + \`view_file\``,
      `5. **LLM Model (Track 1)**: \`${data.meta.llm_model}\` (\`100%\` symmetric system prompts)`,
      `6. **Pinned Target Commits**: \`twentyhq/twenty @ ${data.cs01.repo.commit}\` (Case 01), \`twentyhq/twenty @ ${data.cs03.repos.twenty.commit}\` & \`immich-app/immich @ ${data.cs03.repos.immich.commit}\` (Cases 03 & 04), \`${data.cs05.repo.name} @ ${data.cs05.repo.commit}\` (Case 05)`,
    ].join('\n');
  }

  return b;
}

// ---------------------------------------------------------------------------
// Step 3: Balanced HTML Tag & Markdown Marker Updaters
// ---------------------------------------------------------------------------

const VOID_TAGS = new Set(['area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input', 'link', 'meta', 'param', 'source', 'track', 'wbr']);

function findMatchingCloseTag(html, tagName, startPos) {
  const lowerTag = tagName.toLowerCase();
  const tokenRe = new RegExp(`<(\\/)?${lowerTag}\\b([^>]*)>`, 'gi');
  tokenRe.lastIndex = startPos;
  let depth = 1;
  let m;
  while ((m = tokenRe.exec(html)) !== null) {
    const isClose = Boolean(m[1]);
    const selfClosing = !isClose && (VOID_TAGS.has(lowerTag) || /\/\s*$/.test(m[2]));
    if (selfClosing) continue;
    depth += isClose ? -1 : 1;
    if (depth === 0) {
      return m.index;
    }
  }
  throw new Error(`Unclosed <${tagName}> starting near index ${startPos}`);
}

function syncHtmlFile(relPath, bindings, data) {
  const abs = path.join(ROOT, relPath);
  if (!fs.existsSync(abs)) return false;
  const orig = fs.readFileSync(abs, 'utf8');
  let out = orig;

  // 1. <meta ... data-bind-meta="key" ...>
  out = out.replace(/<meta\b[^>]*\bdata-bind-meta="([^"]+)"[^>]*>/gi, (fullTag, key) => {
    if (!(key in bindings)) return fullTag;
    const escaped = escapeHtml(bindings[key]).replace(/"/g, '&quot;');
    return fullTag.replace(/\bcontent="[^"]*"/i, `content="${escaped}"`);
  });

  // 2. Balanced element bindings: data-bind="...", data-bind-html="...", data-bind-file="..."
  const openTagRe = /<([a-zA-Z][a-zA-Z0-9-]*)\b([^>]*\bdata-bind(?:-html|-file)?="[^"]+"[^>]*)>/g;
  let result = '';
  let cursor = 0;
  let m;

  while ((m = openTagRe.exec(out)) !== null) {
    const fullOpen = m[0];
    const tagName = m[1];
    const attrs = m[2];
    if (VOID_TAGS.has(tagName.toLowerCase())) continue;

    const innerStart = m.index + fullOpen.length;
    const innerEnd = findMatchingCloseTag(out, tagName, innerStart);

    // Optional guard: data-bind-when="cs05.ready"
    const whenMatch = /\bdata-bind-when="([^"]+)"/.exec(attrs);
    if (whenMatch && whenMatch[1] === 'cs05.ready' && data.cs05.status !== 'ready') {
      result += out.slice(cursor, innerEnd);
      cursor = innerEnd;
      openTagRe.lastIndex = innerEnd;
      continue;
    }

    let replacement = null;
    const bindText = /\bdata-bind="([^"]+)"/.exec(attrs);
    const bindHtml = /\bdata-bind-html="([^"]+)"/.exec(attrs);
    const bindFile = /\bdata-bind-file="([^"]+)"/.exec(attrs);

    if (bindText && bindText[1] in bindings) {
      replacement = escapeHtml(bindings[bindText[1]]);
    } else if (bindHtml && bindHtml[1] in bindings) {
      replacement = bindings[bindHtml[1]];
    } else if (bindFile) {
      const fileAbs = path.join(ROOT, bindFile[1]);
      if (fs.existsSync(fileAbs)) {
        const rawText = fs
          .readFileSync(fileAbs, 'utf8')
          .replace(/\r\n/g, '\n')
          .replace(/^---[\s\S]*?---\n+/, '')
          .replace(/\n$/, '');
        replacement = escapeHtml(rawText);
      }
    }

    if (replacement !== null) {
      result += out.slice(cursor, innerStart) + replacement;
      cursor = innerEnd;
    } else {
      result += out.slice(cursor, innerEnd);
      cursor = innerEnd;
    }
    openTagRe.lastIndex = innerEnd;
  }
  result += out.slice(cursor);

  if (result !== orig) {
    if (!CHECK_ONLY) fs.writeFileSync(abs, result, 'utf8');
    return true;
  }
  return false;
}

function syncMarkdownFile(relPath, bindings) {
  const abs = path.join(ROOT, relPath);
  if (!fs.existsSync(abs)) return false;
  const orig = fs.readFileSync(abs, 'utf8');
  let out = orig;

  // Block markers: <!-- BEGIN:key --> ... <!-- END:key -->
  out = out.replace(
    /(<!--\s*BEGIN:([a-zA-Z0-9._-]+)\s*-->)\r?\n[\s\S]*?\r?\n(<!--\s*END:\2\s*-->)/g,
    (full, startTag, key, endTag) => {
      if (!(key in bindings)) return full;
      const eol = orig.includes('\r\n') ? '\r\n' : '\n';
      return `${startTag}${eol}${bindings[key].replace(/\n/g, eol)}${eol}${endTag}`;
    }
  );

  // Inline markers: <!-- bind:key -->...<!-- /bind -->
  out = out.replace(
    /(<!--\s*bind:([a-zA-Z0-9._-]+)\s*-->)[\s\S]*?(<!--\s*\/bind\s*-->)/g,
    (full, startTag, key, endTag) => {
      if (!(key in bindings)) return full;
      return `${startTag}${bindings[key]}${endTag}`;
    }
  );

  if (out !== orig) {
    if (!CHECK_ONLY) fs.writeFileSync(abs, out, 'utf8');
    return true;
  }
  return false;
}

// ---------------------------------------------------------------------------
// Main Execution
// ---------------------------------------------------------------------------

const origDataRaw = fs.readFileSync(DATA_PATH, 'utf8');
const data = JSON.parse(origDataRaw);
const rawMilestones01 = ingestRawSources(data);
const updatedDataRaw = JSON.stringify(data, null, 2) + '\n';

const changedFiles = [];
if (updatedDataRaw !== origDataRaw) {
  if (!CHECK_ONLY) fs.writeFileSync(DATA_PATH, updatedDataRaw, 'utf8');
  changedFiles.push('benchmarks/benchmarks-data.json');
}

const bindings = buildBindings(data, rawMilestones01);
for (const [key, val] of Object.entries(bindings)) {
  if (typeof val !== 'string' || /\b(?:undefined|NaN)\b/.test(val)) {
    throw new Error(`Invalid binding "${key}": ${val}`);
  }
}

const htmlTargets = [
  'site/index.html',
  'site/benchmarks.html',
  'site/benchmarks/01-agent-audit-ce-vs-cbm-on-twenty.html',
  'site/benchmarks/02-enterprise-microservices-deep-audit.html',
  'site/benchmarks/03-code-explorer-vs-codebase-memory-mcp.html',
  'site/benchmarks/04-code-explorer-vs-graphify.html',
  'site/benchmarks/05-polyglot-architecture-and-blast-radius.html',
];

for (const rel of htmlTargets) {
  if (syncHtmlFile(rel, bindings, data)) changedFiles.push(rel);
}

const mdTargets = [
  'README.md',
  'benchmarks/README.md',
];

for (const rel of mdTargets) {
  if (syncMarkdownFile(rel, bindings)) changedFiles.push(rel);
}

if (CHECK_ONLY) {
  if (changedFiles.length > 0) {
    console.error(`sync-benchmarks --check FAILED: ${changedFiles.length} file(s) out of sync:`);
    for (const f of changedFiles) console.error(`  - ${f}`);
    process.exit(1);
  }
  console.log('sync-benchmarks --check OK: 0 drift across JSON, HTML, and Markdown.');
} else {
  if (changedFiles.length === 0) {
    console.log('sync-benchmarks: all files already up to date (0 changes).');
  } else {
    console.log(`sync-benchmarks: updated ${changedFiles.length} file(s):`);
    for (const f of changedFiles) console.log(`  - ${f}`);
  }
}
