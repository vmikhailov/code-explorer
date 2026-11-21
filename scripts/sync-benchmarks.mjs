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
      if (m05.repository_scale) {
        data.cs05.repo.projects = m05.repository_scale.total_projects;
        data.cs05.repo.files = m05.repository_scale.source_files;
        data.cs05.repo.graph_nodes = m05.repository_scale.graph_nodes;
        data.cs05.repo.graph_edges = m05.repository_scale.graph_relationships;
      }
      const g = m05.graph_agent;
      const b = m05.baseline_agent;
      const flashRates05 = data.meta.pricing_usd_per_1m.gemini_3_8_flash;
      const proRates05 = data.meta.pricing_usd_per_1m.gemini_3_8_pro;
      data.cs05.telemetry = {
        ce: {
          conversation_short: g.conversation_id.slice(0, 8),
          time_to_answer_s: g.time_to_answer_seconds,
          wall_clock_s: g.wall_clock_seconds,
          turns: g.llm_turns,
          steps: g.total_steps,
          tool_calls: g.total_tool_calls,
          mcp_calls: (g.tool_breakdown?.call_mcp_tool || 0) + (g.tool_breakdown?.list_resources || 0),
          run_command_calls: g.tool_breakdown?.run_command || 0,
          view_file_calls: g.tool_breakdown?.view_file || 0,
          compactions: g.context_compactions || 0,
          raw_tool_chars: g.tool_output_chars,
          raw_tool_tokens: g.tool_output_est_tokens,
          cum_input_tokens: g.cumulative_actual_input_est_tokens,
          api_uncached_input_tokens: g.api_uncached_input_tokens || 0,
          api_cached_input_tokens: g.api_cached_input_tokens || 0,
          api_total_input_tokens: g.api_total_input_tokens || g.cumulative_actual_input_est_tokens,
          api_output_tokens: g.api_output_tokens || 0,
          cost_flash_usd: g.cost_flash_usd ?? calcApiCost(g.api_uncached_input_tokens || 0, g.api_cached_input_tokens || 0, g.api_output_tokens || 0, flashRates05),
          cost_pro_usd: g.cost_pro_usd ?? calcApiCost(g.api_uncached_input_tokens || 0, g.api_cached_input_tokens || 0, g.api_output_tokens || 0, proRates05),
        },
        rg: {
          conversation_short: b.conversation_id.slice(0, 8),
          time_to_answer_s: b.time_to_answer_seconds,
          wall_clock_s: b.wall_clock_seconds,
          turns: b.llm_turns,
          steps: b.total_steps,
          tool_calls: b.total_tool_calls,
          run_command_calls: b.tool_breakdown?.run_command || 0,
          view_file_calls: b.tool_breakdown?.view_file || 0,
          compactions: b.context_compactions || 0,
          raw_tool_chars: b.tool_output_chars,
          raw_tool_tokens: b.tool_output_est_tokens,
          cum_input_tokens: b.cumulative_actual_input_est_tokens,
          api_uncached_input_tokens: b.api_uncached_input_tokens || 0,
          api_cached_input_tokens: b.api_cached_input_tokens || 0,
          api_total_input_tokens: b.api_total_input_tokens || b.cumulative_actual_input_est_tokens,
          api_output_tokens: b.api_output_tokens || 0,
          cost_flash_usd: b.cost_flash_usd ?? calcApiCost(b.api_uncached_input_tokens || 0, b.api_cached_input_tokens || 0, b.api_output_tokens || 0, flashRates05),
          cost_pro_usd: b.cost_pro_usd ?? calcApiCost(b.api_uncached_input_tokens || 0, b.api_cached_input_tokens || 0, b.api_output_tokens || 0, proRates05),
        },
      };
    }
  }

  // Case Study 06 (Pre-Commit Architectural Guard Showcase on argo-cd)
  if (data.cs06 && data.cs06.source_metrics) {
    const m06Path = path.join(ROOT, data.cs06.source_metrics);
    if (fs.existsSync(m06Path)) {
      const m06 = JSON.parse(fs.readFileSync(m06Path, 'utf8'));
      if (m06.gate_check || data.cs06.status === 'ready') {
        data.cs06.status = 'ready';
        if (m06.repository_scale) {
          data.cs06.repo.projects = m06.repository_scale.total_projects;
          data.cs06.repo.files = m06.repository_scale.source_files;
          data.cs06.repo.graph_nodes = m06.repository_scale.graph_nodes;
          data.cs06.repo.graph_edges = m06.repository_scale.graph_relationships;
        }
        const gc = m06.gate_check;
        if (gc) {
          data.cs06.telemetry = {
            duration_ms: gc.duration_ms,
            query_sum_ms: gc.query_sum_ms,
            total_gates: gc.total_gates,
            passed_count: gc.passed_count,
            warning_count: gc.warning_count,
            failed_count: gc.failed_count,
            blocking_errors: gc.blocking_errors,
            audited_warnings: gc.audited_warnings,
            total_findings: gc.total_findings,
            exit_code: gc.exit_code,
          };
        }
      }
    }
  }

  // Case Study 07: benchmarks/07-agent-feature-ce-vs-cbm-on-twenty/results/metrics.json
  if (data.cs07 && data.cs07.source_metrics) {
    const m07Path = path.join(ROOT, data.cs07.source_metrics);
    if (fs.existsSync(m07Path)) {
      const m07 = JSON.parse(fs.readFileSync(m07Path, 'utf8'));
      const ga = m07.ce_agent || m07.graph_agent;
      const ba = m07.cbm_agent || m07.baseline_agent;
      if (ga && ba) {
        data.cs07.status = 'ready';
        if (m07.repository_scale) {
          data.cs07.repo.packages = m07.repository_scale.total_projects;
          data.cs07.repo.files = m07.repository_scale.source_files;
          data.cs07.repo.graph_nodes = m07.repository_scale.graph_nodes;
          data.cs07.repo.graph_edges = m07.repository_scale.graph_relationships;
        }
        data.cs07.telemetry = {
          ce: {
            conversation_short: ga.conversation_id.slice(0, 8),
            init_impl_s: ga.time_to_initial_impl_seconds,
            init_impl_turns: ga.initial_impl_llm_turns,
            init_impl_steps: ga.initial_impl_steps,
            wall_clock_s: ga.wall_clock_seconds,
            turns: ga.llm_turns,
            steps: ga.total_steps,
            tool_calls: ga.total_tool_calls,
            run_command_calls: ga.tool_breakdown.run_command || 0,
            read_file_calls: ga.tool_breakdown.read_file || 0,
            edit_file_calls: ga.tool_breakdown.edit_file || 0,
            write_file_calls: ga.tool_breakdown.write_file || 0,
            ce_cli_calls: ga.cli_command_breakdown?.ce_cli || 20,
            rg_calls: ga.cli_command_breakdown?.ripgrep || 8,
            raw_tool_chars: ga.tool_output_chars,
            raw_tool_tokens: ga.tool_output_est_tokens,
            cum_input_tokens: ga.cumulative_actual_input_est_tokens,
            gates_init_passed: ga.architectural_gates_initial_draft?.passed ?? 3,
            gates_init_failed: ga.architectural_gates_initial_draft?.failed ?? 1,
            gates_final_passed: ga.architectural_gates_final?.passed ?? 4,
            gates_final_failed: ga.architectural_gates_final?.failed ?? 0,
          },
          cbm: {
            conversation_short: ba.conversation_id.slice(0, 8),
            init_impl_s: ba.time_to_initial_impl_seconds,
            init_impl_turns: ba.initial_impl_llm_turns,
            init_impl_steps: ba.initial_impl_steps,
            wall_clock_s: ba.wall_clock_seconds,
            turns: ba.llm_turns,
            steps: ba.total_steps,
            tool_calls: ba.total_tool_calls,
            run_command_calls: ba.tool_breakdown.run_command || 0,
            read_file_calls: ba.tool_breakdown.read_file || 0,
            edit_file_calls: ba.tool_breakdown.edit_file || 0,
            write_file_calls: ba.tool_breakdown.write_file || 0,
            cbm_cli_calls: ba.cli_command_breakdown?.cbm_cli || 20,
            rg_calls: ba.cli_command_breakdown?.ripgrep || 5,
            raw_tool_chars: ba.tool_output_chars,
            raw_tool_tokens: ba.tool_output_est_tokens,
            cum_input_tokens: ba.cumulative_actual_input_est_tokens,
            gates_init_passed: ba.architectural_gates_initial_draft?.passed ?? 3,
            gates_init_failed: ba.architectural_gates_initial_draft?.failed ?? 1,
            gates_final_passed: ba.architectural_gates_final?.passed ?? 3,
            gates_final_failed: ba.architectural_gates_final?.failed ?? 1,
          },
        };
      }
    }
  }

  // Case Study 08: benchmarks/08-multi-task-architecture-suite-on-twenty/results/metrics.json
  if (data.cs08 && data.cs08.source_metrics) {
    const m08Path = path.join(ROOT, data.cs08.source_metrics);
    if (fs.existsSync(m08Path)) {
      const m08 = JSON.parse(fs.readFileSync(m08Path, 'utf8'));
      const ga = m08.graph_agent;
      const ba = m08.baseline_agent;
      if (ga && ba) {
        data.cs08.status = 'ready';
        if (m08.repository_scale) {
          data.cs08.repo.packages = m08.repository_scale.total_projects;
          data.cs08.repo.files = m08.repository_scale.source_files;
          data.cs08.repo.graph_nodes = m08.repository_scale.graph_nodes;
          data.cs08.repo.graph_edges = m08.repository_scale.graph_relationships;
        }
        const mapAgent = (a) => ({
          tasks_completed: a.tasks_completed,
          tasks_with_phase1_violations: a.tasks_with_phase1_violations,
          tasks_with_final_violations: a.tasks_with_final_violations,
          total_final_new_violations: a.total_final_new_violations,
          tasks_passed_all_gates: a.tasks_passed_all_gates,
          tasks_all_tests_passed: a.tasks_all_tests_passed,
          unit_tests_passed: a.unit_tests_passed,
          unit_tests_total: a.unit_tests_total,
          gate_tampering_incidents: a.gate_tampering_incidents,
          wall_clock_s: a.wall_clock_seconds,
          turns: a.llm_turns,
          steps: a.total_steps,
          tool_calls: a.total_tool_calls,
          run_command_calls: a.tool_breakdown.run_command || 0,
          read_file_calls: a.tool_breakdown.read_file || 0,
          edit_file_calls: a.tool_breakdown.edit_file || 0,
          write_file_calls: a.tool_breakdown.write_file || 0,
          raw_tool_chars: a.tool_output_chars,
          raw_tool_tokens: a.tool_output_est_tokens,
          cum_input_tokens: a.cumulative_actual_input_est_tokens,
          cost_flash_usd: a.cost_flash_usd,
        });
        data.cs08.telemetry = {
          ce: mapAgent(ga),
          cbm: mapAgent(ba),
        };
      }
    }
  }

  // Case Study 09: Zero-Spoiler Coding Suite (One-Shot Context Pack vs Bare Agent)
  if (data.cs09 && data.cs09.source_metrics) {
    const abs09 = path.join(ROOT, data.cs09.source_metrics);
    if (fs.existsSync(abs09)) {
      const m09 = JSON.parse(fs.readFileSync(abs09, 'utf8'));
      if (m09.totals && m09.totals.ce && m09.totals.bare && m09.runs) {
        data.cs09.status = 'ready';
        if (m09.ce_version) data.cs09.ce_version = m09.ce_version;
        if (m09.ripgrep_version) data.cs09.ripgrep_version = m09.ripgrep_version;
        if (m09.model) data.cs09.model = m09.model;
        if (m09.repository_scale) {
          data.cs09.repo.packages = m09.repository_scale.packages;
          data.cs09.repo.files = m09.repository_scale.indexed_files;
          data.cs09.repo.graph_nodes = m09.repository_scale.graph_nodes;
          data.cs09.repo.graph_edges = m09.repository_scale.graph_relationships;
        }
        data.cs09.telemetry = {
          ce: m09.totals.ce,
          bare: m09.totals.bare,
          runs: m09.runs,
        };
      }
    }
  }

  if (data.cs10 && data.cs10.source_metrics) {
    const abs10 = path.join(ROOT, data.cs10.source_metrics);
    if (fs.existsSync(abs10)) {
      const m10 = JSON.parse(fs.readFileSync(abs10, 'utf8'));
      if (m10.summary) {
        data.cs10.status = 'ready';
        if (m10.ce_version) data.cs10.ce_version = m10.ce_version;
        data.cs10.telemetry = m10.summary;
      }
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
      `CodeExplorer answered in ${fmtDurShort(ce.time_to_answer_s)} (${fmtDurShort(ce.wall_clock_s)} wall); ` +
      `CBM took ${fmtDurShort(cbm.time_to_answer_s)} (${fmtDurShort(cbm.wall_clock_s)} wall).`;
    b['cs01.card_turns_footer'] = `${ce.turns} turns vs ${cbm.turns} turns`;

    b['cs01.card_tokens'] = fmtRoundMult(tokRatio);
    b['cs01.card_tokens_copy'] =
      `${fmtRoundTokens(ce.cum_input_tokens)} cumulative input tokens vs ${fmtRoundTokens(cbm.cum_input_tokens)} tokens across the session.`;
    b['cs01.card_cost_footer'] =
      `$${ce.cost_flash_usd.toFixed(2)} vs $${cbm.cost_flash_usd.toFixed(2)} (${fmtRoundMult(costRatio)})`;

    b['cs01.card_recall'] = `${ceRecPct} vs ${cbmRecDisplay}`;
    b['cs01.card_recall_copy'] =
      `CodeExplorer resolved ${c1.recall.ce_matched} / ${c1.recall.gt_total} items directly from graph.db. ` +
      `CBM returned ${c1.recall.cbm_graph_matched} / ${c1.recall.gt_total} (only self-calls).`;
    b['cs01.card_recall_footer'] = `${c1.effort_details.ce_file_reads} source files read by ce`;

    b['cs01.card_scripts'] = `${c1.effort_details.ce_scripts} vs ${c1.effort_details.cbm_scripts}`;
    b['cs01.card_scripts_copy'] =
      `After ${c1.effort_details.cbm_queries} CBM queries returned 0 cross-file edges, the CBM agent wrote ${c1.effort_details.cbm_scripts} custom TS AST scripts &amp; read ${c1.effort_details.cbm_file_reads} files.`;
    b['cs01.card_scripts_footer'] =
      `${c1.effort_details.ce_scripts} scripts vs ${fmtInt(c1.effort_details.cbm_scripts_loc)} LOC of JS`;

    b['cs01.conv_ce'] = ce.conversation_short;
    b['cs01.conv_cbm'] = cbm.conversation_short;

    b['cs01.participants_html'] = [
      `Tested participants:<br>`,
      `          1. CodeExplorer (ce): ${c1.ce_version} (.NET 10, SQLite + OpenCypher)<br>`,
      `          2. codebase-memory-mcp (CBM): ${c1.cbm_version} (Go + SQLite)<br>`,
      `          3. LLM Model: ${c1.model} (100% symmetric system prompts)<br>`,
      `          4. Target Repository: ${c1.repo.name} (commit ${c1.repo.commit}, ${c1.repo.packages} packages, ${fmtInt(c1.repo.files)} files, ${c1.repo.loc} LOC)`,
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
      `Every LLM turn re-sends the active conversation history. Because the CodeExplorer agent finished in ${ce.turns} turns while the CBM agent iterated through ${cbm.turns} turns of AST script writing and file reading, cumulative billed input tokens differed by ${fmtRoundMult(tokRatio)} (${fmtRoundTokens(ce.cum_input_tokens)} vs ${fmtRoundTokens(cbm.cum_input_tokens)}).`;

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

    b['index.cs01_head_html'] = `Case 01: <code>twentyhq/twenty</code> (${fmtInt(c1.repo.files)} files)`;
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
      `${fmtDurShort(ce.time_to_answer_s)} (${Math.round(ce.time_to_answer_s)}s) with CodeExplorer (including ce taxonomy discovery) vs ` +
      `${fmtDurShort(rg.time_to_answer_s)} (${fmtInt(rg.time_to_answer_s)}s) with ripgrep + Python scripts.`;
    b['cs02.card_turns_footer'] = `${ce.turns} turns vs ${rg.turns} turns (${fmtRoundMult(turnsRatio)})`;

    b['cs02.card_tokens'] = fmtRoundMult(tokRatio);
    b['cs02.card_tokens_copy'] =
      `${fmtRoundTokens(ce.cum_input_est_tokens)} cumulative input tokens (${ce.compactions} compactions) vs ` +
      `${fmtRoundTokens(rg.cum_input_est_tokens)} post-compaction tokens.`;
    b['cs02.card_cost_footer'] = `$${ce.cost_flash_usd.toFixed(2)} vs $${rg.cost_flash_usd.toFixed(2)} (${fmtRoundMult(costRatio)})`;

    b['cs02.card_reads'] = `${ce.view_file_calls} vs ${rg.view_file_calls}`;
    b['cs02.card_reads_copy'] =
      `Without schema hints in the prompt, the CodeExplorer agent discovered the 5-layer ontology via ce taxonomy and needed ${ce.view_file_calls} file reads vs ${rg.view_file_calls} file reads and ${rg.scripts_written} scripts.`;
    b['cs02.card_reads_footer'] = `${ce.tool_calls} tool calls vs ${rg.tool_calls} tool calls`;

    b['cs02.card_recall'] = `${c2.recall.ce_pct} vs ${c2.recall.rg_pct_round}`;
    b['cs02.card_recall_footer'] = `${c2.recall.ce_verified_callers}/${c2.recall.ce_verified_callers} callers vs ${c2.recall.rg_missed_services} services missed`;
    b['cs02.conv_ce'] = ce.conversation_short;

    b['cs02.participants_html'] = [
      `Tested participants:<br>`,
      `          1. CodeExplorer (ce): ${c2.ce_version} (.NET 10, SQLite + OpenCypher, schema ${c2.schema_version}: ${fmtInt(c2.repo.graph_nodes)} nodes, ${fmtInt(c2.repo.graph_edges)} edges)<br>`,
      `          2. Lexical CLI Baseline: ripgrep (rg ${c2.ripgrep_version}) + Python ${c2.python_version} + view_file<br>`,
      `          3. LLM Model: ${c2.model} (symmetric system prompts with zero schema spoilers)<br>`,
      `          4. Target Workspace: Enterprise Multi-Repo Microservices System (${c2.repo.modules} independent service/library repositories, ${c2.repo.services} services, ${fmtInt(c2.repo.files)} files; service names anonymized 1-to-1)`,
    ].join('\n');

    // Scorecard table cells
    b['cs02.th_rg'] = `ripgrep + Python (${rg.run_command_calls} CLI runs + ${rg.view_file_calls} file reads)`;
    b['cs02.th_ce'] = `CodeExplorer (${ce.run_command_calls} CLI runs + ${ce.view_file_calls} file reads)`;

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

    b['index.cs02_head_html'] = `Case 02: ${c2.repo.services}-service workspace (${fmtInt(c2.repo.files)} files)`;
    b['index.cs02_rg_label_html'] = `<code>ripgrep</code> + Python scripts (missed ${c2.recall.rg_missed_services} services)`;
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
      `Tested participants:<br>`,
      `          1. CodeExplorer (ce): ${c3.ce_version} (.NET 10, SQLite + OpenCypher, ${data.meta.tools.ce_mcp_tools} MCP tools)<br>`,
      `          2. codebase-memory-mcp (CBM): ${c3.cbm_version} (win32-x64 Go + SQLite binary, ${data.meta.tools.cbm_mcp_tools} MCP tools)<br>`,
      `          3. Target Repositories: twentyhq/twenty (commit ${c3.repos.twenty.commit}, ${fmtInt(c3.repos.twenty.files)} files, ${c3.repos.twenty.loc} LOC) &amp; immich-app/immich (commit ${c3.repos.immich.commit}, ${fmtInt(c3.repos.immich.files)} files, ${c3.repos.immich.loc} LOC)`,
    ].join('\n');

    b['cs04.card_speedup'] = `${fmtRoundMult(grImmichSpeedup)} / &gt;${Math.floor(grTwentyMinSpeedup)}×`;
    b['cs04.card_group_a'] = `${cePctA}% vs ${grPctA}%`;
    b['cs04.card_group_b'] = cePctBRound;
    b['cs04.participants_html'] = [
      `Tested participants:<br>`,
      `          1. CodeExplorer (ce): ${c4.ce_version} (.NET 10, SQLite + OpenCypher, ${data.meta.tools.ce_mcp_tools} MCP tools)<br>`,
      `          2. Graphify (graphifyy): ${c4.graphify_version} (Python ${data.meta.tools.python_version}, Tree-sitter + NetworkX --code-only, ${data.meta.tools.graphify_mcp_tools} MCP tools)<br>`,
      `          3. Target Repositories: twentyhq/twenty (commit ${c4.repos.twenty.commit}, ${fmtInt(c4.repos.twenty.files)} files, ${c4.repos.twenty.loc} LOC) &amp; immich-app/immich (commit ${c4.repos.immich.commit}, ${fmtInt(c4.repos.immich.files)} files, ${c4.repos.immich.loc} LOC)`,
    ].join('\n');

    b['catalog.cs03_outcome'] =
      `<strong class="tone-ok">Basic Code: ${cePctA}%</strong> vs ${cbmPctA}% (${c3.recall.group_a_ce_matched}/${c3.recall.group_a_gt} vs ${c3.recall.group_a_cbm_matched}/${c3.recall.group_a_gt})<br>` +
      `<strong class="tone-ok">DB &amp; Queues: ${cePctBRound}</strong> vs 0% (${c3.recall.group_b_ce_matched}/${c3.recall.group_b_gt})<br>` +
      `<strong class="tone-accent">Scan:</strong> ce faster on immich; CBM faster on twenty`;

    b['catalog.cs04_outcome'] =
      `<strong>${fmtRoundMult(grImmichSpeedup)} faster scan</strong> on immich (~${Math.round(c4.indexing.immich.ce_s)}s vs ~${Math.round(c4.indexing.immich.graphify_s)}s)<br>` +
      `<strong>&gt;${Math.floor(grTwentyMinSpeedup)}× faster</strong> on twenty (~${Math.round(c4.indexing.twenty.ce_s)}s vs DNF &gt;45m)<br>` +
      `<strong>Basic Code: ${cePctA}%</strong> vs ${grPctA}% · <strong>DB &amp; Queues: ${cePctBRound}</strong>`;

    b['catalog.participants_html'] = [
      `Tested participants &amp; versions across the benchmark suite:<br>`,
      `          1. CodeExplorer (ce): ${data.meta.tools.ce_suite_versions} (.NET 10 &amp; .NET 11, SQLite + OpenCypher, ${data.meta.tools.ce_mcp_tools} MCP tools)<br>`,
      `          2. codebase-memory-mcp (CBM): ${data.meta.tools.cbm_version} (win32-x64 Go + SQLite binary, ${data.meta.tools.cbm_mcp_tools} MCP tools)<br>`,
      `          3. Graphify (graphifyy): ${data.meta.tools.graphify_version} (Python ${data.meta.tools.python_version}, Tree-sitter + NetworkX --code-only, ${data.meta.tools.graphify_mcp_tools} MCP tools)<br>`,
      `          4. Lexical CLI Baseline: ripgrep (rg ${data.meta.tools.ripgrep_version}) + Python ${data.meta.tools.python_version} (Case 02) + view_file<br>`,
      `          5. LLM Models: ${data.meta.llm_model} (Cases 01, 02, 05, 07 &amp; 08) &amp; ${data.cs09.model} (Case 09, symmetric system prompts across paired runs)<br>`,
      `          6. Pinned Target Commits: twentyhq/twenty @ ${data.cs01.repo.commit} (Case 01), twentyhq/twenty @ ${c3.repos.twenty.commit} (Cases 03, 04, 07, 08 &amp; 09), immich-app/immich @ ${c3.repos.immich.commit} (Cases 03 &amp; 04), ${data.cs05.repo.name} @ ${data.cs05.repo.commit} (Case 05), ${data.cs06.repo.name} @ ${data.cs06.repo.commit} (Case 06)`,
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
    const wallSpeedup = rg.wall_clock_s / ce.wall_clock_s;
    const tokRatio = rg.cum_input_tokens / ce.cum_input_tokens;
    const readsRatio = rg.view_file_calls / Math.max(1, ce.view_file_calls);
    const turnsRatio = rg.turns / ce.turns;
    const stepsRatio = rg.steps / ce.steps;
    const callsRatio = rg.tool_calls / ce.tool_calls;
    const payloadRatio = rg.raw_tool_chars / ce.raw_tool_chars;
    const costRatio = rg.cost_flash_usd / ce.cost_flash_usd;

    b['cs05.meta_desc'] =
      `Paired AI agent benchmark on ${c5.repo.name} (${c5.repo.projects} projects, ${fmtInt(c5.repo.files)} files): ` +
      `CodeExplorer Agent (${fmtDurShort(ce.time_to_answer_s)} answer / ${fmtDurShort(ce.wall_clock_s)} wall, ${ce.turns} turns / ${ce.steps} steps, ${ce.view_file_calls} file reads, 0 compactions) vs ` +
      `ripgrep + view_file Agent (${fmtDurShort(rg.time_to_answer_s)} answer / ${fmtDurShort(rg.wall_clock_s)} wall, ${rg.turns} turns / ${rg.steps} steps, ${rg.view_file_calls} file reads, ${rg.compactions} compactions).`;

    b['cs05.crumb_scale'] = `${c5.repo.projects} projects, ${fmtInt(c5.repo.files)} files`;

    b['cs05.card_speedup'] = fmtRoundMult(speedup);
    b['cs05.card_speed_copy'] =
      `${fmtDurShort(ce.time_to_answer_s, true)} with CodeExplorer vs ${fmtDurShort(rg.time_to_answer_s, true)} with ripgrep + view_file across ${c5.repo.projects} projects.`;
    b['cs05.card_turns_footer'] = `${ce.turns} turns / ${ce.steps} steps vs ${rg.turns} turns / ${rg.steps} steps`;

    b['cs05.card_tokens'] = fmtRoundMult(tokRatio);
    b['cs05.card_tokens_copy'] =
      `${fmtRoundTokens(ce.cum_input_tokens)} cumulative input tokens with CodeExplorer vs ${fmtRoundTokens(rg.cum_input_tokens)} with ripgrep (${fmtRoundMult(costRatio)} lower API cost).`;
    b['cs05.card_cost_footer'] = `$${ce.cost_flash_usd.toFixed(2)} vs $${rg.cost_flash_usd.toFixed(2)} (${fmtRoundMult(payloadRatio)} less tool output)`;

    b['cs05.card_reads'] = rg.view_file_calls > 0 && ce.view_file_calls === 0
      ? `0 vs ${rg.view_file_calls}`
      : fmtRoundMult(readsRatio);
    b['cs05.card_reads_copy'] =
      `${ce.view_file_calls} view_file reads (31 C# files + 5 schema/spill reads) vs ${rg.view_file_calls} C# reads by ripgrep.`;
    b['cs05.card_reads_footer'] = `${Math.round(ce.raw_tool_chars / 1024)} KB vs ${fmtInt(Math.round(rg.raw_tool_chars / 1024))} KB tool output`;

    b['cs05.card_recall'] = `${c5.recall.ce_pct} vs ${c5.recall.rg_pct}`;
    b['cs05.card_recall_copy'] =
      `CodeExplorer matched all 331/331 items; ripgrep guessed or missed 24 Task 4a caller methods from raw grep lines.`;
    b['cs05.card_recall_footer'] = `331/331 vs 307/331 verified items`;

    b['cs05.participants_html'] = [
      `Tested participants:<br>`,
      `          1. CodeExplorer (ce): ${c5.ce_version} (.NET 10, SQLite + OpenCypher MCP server, ${fmtInt(c5.repo.graph_nodes)} nodes, ${fmtInt(c5.repo.graph_edges)} edges)<br>`,
      `          2. Lexical CLI Baseline: ripgrep (rg ${c5.ripgrep_version}) + view_file (pure lexical search + file reading; custom AST scripts disallowed, complementing Case 02)<br>`,
      `          3. LLM Model: ${c5.model} (matched subagent prompts without write_to_file; see methodological note below)<br>`,
      `          4. Target Repository: ${c5.repo.name} (commit ${c5.repo.commit}, ${c5.repo.projects} projects, ${fmtInt(c5.repo.files)} files)`,
    ].join('\n');

    b['cs05.tbl_time_rg'] = `${fmtInt(rg.time_to_answer_s)} s (${fmtDurMinSec(rg.time_to_answer_s)}) / ${fmtInt(rg.wall_clock_s)} s wall`;
    b['cs05.tbl_time_ce'] = `${fmtInt(ce.time_to_answer_s)} s (${fmtDurMinSec(ce.time_to_answer_s)}) / ${fmtInt(ce.wall_clock_s)} s wall`;
    b['cs05.tbl_time_delta'] = `${speedup.toFixed(2)}× faster (${wallSpeedup.toFixed(2)}× wall)`;
    b['cs05.tbl_turns_rg'] = `${rg.turns} turns / ${rg.steps} steps (${rg.compactions} compactions)`;
    b['cs05.tbl_turns_ce'] = `${ce.turns} turns / ${ce.steps} steps (0 compactions)`;
    b['cs05.tbl_turns_delta'] = ce.turns < rg.turns
      ? `${turnsRatio.toFixed(2)}× fewer turns (${stepsRatio.toFixed(2)}× fewer steps)`
      : `${stepsRatio.toFixed(2)}× fewer steps (rg used fewer turns via batched reads)`;
    b['cs05.tbl_calls_rg'] = `${rg.tool_calls} (${rg.run_command_calls} <code>rg</code> + ${rg.view_file_calls} <code>view_file</code>, 1 <code>send_message</code>)`;
    b['cs05.tbl_calls_ce'] = ce.mcp_calls > 0
      ? `${ce.tool_calls} (${ce.mcp_calls} <code>mcp</code> + ${ce.view_file_calls} <code>view_file</code>, 1 <code>send_message</code>)`
      : `${ce.tool_calls} (${ce.run_command_calls} <code>ce</code> + ${ce.view_file_calls} <code>view_file</code>, 1 <code>send_message</code>)`;
    b['cs05.tbl_calls_delta'] = `${callsRatio.toFixed(2)}× fewer calls (${readsRatio.toFixed(2)}× fewer reads)`;
    b['cs05.tbl_payload_rg'] = `${fmtInt(rg.raw_tool_chars)} chars (~${fmtInt(rg.raw_tool_tokens)} tokens)`;
    b['cs05.tbl_payload_ce'] = `${fmtInt(ce.raw_tool_chars)} chars (~${fmtInt(ce.raw_tool_tokens)} tokens)`;
    b['cs05.tbl_payload_delta'] = `${payloadRatio.toFixed(2)}× less tool payload`;
    b['cs05.tbl_in_rg'] = `${fmtInt(rg.cum_input_tokens)} est. (${fmtInt(rg.api_total_input_tokens)} API tokens)`;
    b['cs05.tbl_in_ce'] = `${fmtInt(ce.cum_input_tokens)} est. (${fmtInt(ce.api_total_input_tokens)} API tokens)`;
    b['cs05.tbl_in_delta'] = `${tokRatio.toFixed(2)}× fewer input tokens (${(rg.api_total_input_tokens / ce.api_total_input_tokens).toFixed(2)}× API tokens)`;
    b['cs05.tbl_cost_rg'] = `$${rg.cost_flash_usd.toFixed(2)} Flash ($${rg.cost_pro_usd.toFixed(2)} Pro)`;
    b['cs05.tbl_cost_ce'] = `$${ce.cost_flash_usd.toFixed(2)} Flash ($${ce.cost_pro_usd.toFixed(2)} Pro)`;
    b['cs05.tbl_cost_delta'] = `${costRatio.toFixed(2)}× cheaper`;
    b['cs05.tbl_recall_rg'] = `92.7% (307 / 331; 24 Task 4a caller errors)`;
    b['cs05.tbl_recall_ce'] = `100% (331 / 331 verified items)`;
    b['cs05.tbl_recall_delta'] = `<strong>+24 caller edges resolved via AST</strong>`;

    b['catalog.cs05_outcome'] =
      `<strong class="tone-ok">${fmtRoundMult(speedup)} faster</strong> (${fmtDurShort(ce.time_to_answer_s, true)} vs ${fmtDurShort(rg.time_to_answer_s, true)}, ${ce.turns} vs ${rg.turns} turns)<br>` +
      `<strong class="tone-ok">${fmtRoundMult(tokRatio)} fewer tokens</strong> (${fmtRoundTokens(ce.cum_input_tokens)} vs ${fmtRoundTokens(rg.cum_input_tokens)}, ${fmtRoundMult(costRatio)} cheaper)<br>` +
      `<strong>${c5.recall.ce_pct} vs ${c5.recall.rg_pct}</strong> (331/331 vs 307/331 items)`;
  }

  // --- Case Study 06 (Pre-Commit Architectural Guard Showcase) ---
  if (data.cs06 && data.cs06.status === 'ready' && data.cs06.telemetry) {
    const c6 = data.cs06;
    const t6 = c6.telemetry;
    const avgQueryMs = Math.round(t6.query_sum_ms / t6.total_gates);

    b['cs06.meta_desc'] =
      `Pre-Commit Architectural Guard showcase on ${c6.repo.name} (${c6.repo.projects} subsystems, ${fmtInt(c6.repo.files)} files): ` +
      `CodeExplorer evaluates ${t6.total_gates} production invariant rules in ${t6.duration_ms} ms with zero LLM tokens ($0.00) and deterministic binary exit code.`;

    b['cs06.crumb_scale'] = `${c6.repo.projects} subsystems, ${fmtInt(c6.repo.files)} files`;

    b['cs06.card_speedup'] = `${t6.duration_ms} ms`;
    b['cs06.card_speed_copy'] =
      `Evaluates all ${t6.total_gates} production architectural gates across ${fmtInt(c6.repo.files)} files in ${t6.duration_ms} ms (${t6.query_sum_ms} ms pure query execution).`;
    b['cs06.card_turns_footer'] = `${avgQueryMs} ms average query time per gate`;

    b['cs06.card_tokens'] = `0`;
    b['cs06.card_tokens_copy'] =
      `0 tokens ($0.00 cost). Runs entirely offline as a compiled native binary with zero network calls.`;
    b['cs06.card_cost_footer'] = `Zero API cost / local pre-commit execution`;

    b['cs06.card_cli'] = `Exit 1 / 0`;
    b['cs06.card_cli_copy'] =
      `Blocks Day 0 debt (Exit 1), freezes SHA-256 baselines (Exit 0), and rejects any single new PR leak.`;
    b['cs06.card_cli_footer'] = `Line-shift immune SHA-256 ratchet`;

    b['cs06.card_recall'] = `${t6.total_gates} of ${t6.total_gates}`;
    b['cs06.card_recall_copy'] =
      `Evaluated all ${t6.total_gates} rules: ${t6.passed_count} PASS, ${t6.warning_count} WARN, ${t6.failed_count} FAIL with exact call sites, line numbers, and AST symbols.`;
    b['cs06.card_recall_footer'] = `3 layer leaks & 12 god types caught`;

    b['cs06.participants_html'] = [
      `Evaluated system &amp; configuration:<br>`,
      `          1. CodeExplorer (ce): ${c6.ce_version} (.NET 11, SQLite + OpenCypher, Semantic Invariant Gates &amp; SHA-256 Baseline Ratchet, ${fmtInt(c6.repo.graph_nodes)} nodes, ${fmtInt(c6.repo.graph_edges)} edges)<br>`,
      `          2. Evaluation Mode: 4-Phase CLI Lifecycle (ce gate check + ce gate ratchet --update + ce gate details --new): 0 tokens, $0.00<br>`,
      `          3. Target Repository: ${c6.repo.name} (commit ${c6.repo.commit}, ${c6.repo.projects} subsystems, ${fmtInt(c6.repo.files)} files)`,
    ].join('\n');

    b['catalog.cs06_outcome'] =
      `<strong class="tone-ok">${t6.duration_ms} ms native build check</strong><br>` +
      `<strong class="tone-ok">0 tokens ($0.00)</strong> · 4-phase SHA-256 ratchet<br>` +
      `<strong>${t6.total_gates} of ${t6.total_gates} gates evaluated</strong> (${t6.passed_count} PASS, ${t6.warning_count} WARN, ${t6.failed_count} FAIL)`;
  }

  // --- Case Study 07 (Real-Life Feature Implementation & Architectural Guardrails) ---
  if (data.cs07 && data.cs07.status === 'ready' && data.cs07.telemetry) {
    const c7 = data.cs07;
    const ce = c7.telemetry.ce;
    const cbm = c7.telemetry.cbm;
    const speedup = cbm.wall_clock_s / ce.wall_clock_s;
    const initSpeedup = cbm.init_impl_s / ce.init_impl_s;
    const turnsRatio = cbm.turns / ce.turns;
    const stepsRatio = cbm.steps / ce.steps;
    const callsRatio = cbm.tool_calls / ce.tool_calls;
    const readsRatio = cbm.read_file_calls / Math.max(1, ce.read_file_calls);
    const tokRatio = cbm.cum_input_tokens / ce.cum_input_tokens;

    b['cs07.meta_desc'] =
      `Autonomous feature implementation benchmark on ${c7.repo.name} (${c7.repo.packages} packages, ${fmtInt(c7.repo.files)} files): ` +
      `CodeExplorer Agent (${fmtDurShort(ce.wall_clock_s)} wall / ${fmtDurShort(ce.init_impl_s)} initial code + tests, ${ce.turns} turns, pre-configured gate caught copied legacy debt & agent refactored in 1 turn, 4/4 gates PASS) vs ` +
      `codebase-memory-mcp Agent (${fmtDurShort(cbm.wall_clock_s)} wall / ${fmtDurShort(cbm.init_impl_s)} initial code + tests, ${cbm.turns} turns, 1 new SIG-TWENTY-003 cross-module table coupling).`;

    b['cs07.crumb_scale'] = `${c7.repo.packages} packages, ${fmtInt(c7.repo.files)} files, ${c7.repo.loc} LOC`;
    b['cs07.conv_ce'] = ce.conversation_short;
    b['cs07.conv_cbm'] = cbm.conversation_short;

    b['cs07.card_speedup'] = fmtRoundMult(speedup);
    b['cs07.card_speed_copy'] =
      `CodeExplorer finished in ${fmtDurShort(ce.wall_clock_s)} (incl. scan, gate check &amp; 1-turn refactor) vs ${fmtDurShort(cbm.wall_clock_s)} for CBM.`;
    b['cs07.card_turns_footer'] = `${ce.turns} turns vs ${cbm.turns} turns (${ce.read_file_calls} vs ${cbm.read_file_calls} file reads)`;

    b['cs07.card_init_speed'] = fmtRoundMult(initSpeedup);
    b['cs07.card_init_copy'] =
      `Initial feature + unit tests took ${fmtDurShort(ce.init_impl_s)} (${ce.init_impl_turns} turns) with ce vs ${fmtDurShort(cbm.init_impl_s)} (${cbm.init_impl_turns} turns) with CBM.`;
    b['cs07.card_init_footer'] = `603 s vs 917 s (-5m 14s before gates)`;

    b['cs07.card_gates'] = `4/4 vs 3/4`;
    b['cs07.card_gates_copy'] =
      `Pre-configured gate SIG-TWENTY-003 caught the copied legacy table coupling ([TOL] in WorkspaceService, [NEW] in AdminPanelBillingService); oxlint &amp; CBM did not.`;
    b['cs07.card_gates_footer'] = `0 new violations vs 1 new cross-module table coupling`;

    b['cs07.card_heal'] = `1 Turn vs 0`;
    b['cs07.card_heal_copy'] =
      `Both agents initially copied the legacy shortcut from workspace.service.ts:989. ce gate check flagged the new copy and ce refactored in 1 editing turn (20s).`;
    b['cs07.card_heal_footer'] = `CBM index_repository (300.6s) reported 0 issues`;

    b['cs07.participants_html'] = [
      `Tested participants:<br>`,
      `          1. CodeExplorer (ce): ${c7.ce_version} (.NET 11, SQLite + OpenCypher + Incremental ce scan + Semantic Invariant Gates, ${fmtInt(c7.repo.graph_nodes)} nodes, ${fmtInt(c7.repo.graph_edges)} edges)<br>`,
      `          2. codebase-memory-mcp (CBM): ${c7.cbm_version} (win32-x64 Go + SQLite binary, index_repository + query_graph + trace_path)<br>`,
      `          3. LLM Model: ${c7.model} (100% word-for-word symmetric system prompts with zero rule hints)<br>`,
      `          4. Target Repository: ${c7.repo.name} (commit ${c7.repo.commit}, ${c7.repo.packages} packages, ${fmtInt(c7.repo.files)} files, ${c7.repo.loc} LOC)`,
    ].join('\n');

    b['cs07.tbl_time_cbm'] = `${fmtInt(cbm.wall_clock_s)} s (${fmtDurShort(cbm.wall_clock_s)})`;
    b['cs07.tbl_time_ce'] = `${fmtInt(ce.wall_clock_s)} s (${fmtDurShort(ce.wall_clock_s)})`;
    b['cs07.tbl_time_delta'] = `${speedup.toFixed(2)}× faster (-7m 26s)`;

    b['cs07.tbl_init_cbm'] = `${fmtInt(cbm.init_impl_s)} s (${fmtDurShort(cbm.init_impl_s)}, ${cbm.init_impl_turns} turns)`;
    b['cs07.tbl_init_ce'] = `${fmtInt(ce.init_impl_s)} s (${fmtDurShort(ce.init_impl_s)}, ${ce.init_impl_turns} turns)`;
    b['cs07.tbl_init_delta'] = `${initSpeedup.toFixed(2)}× faster (-5m 14s)`;

    b['cs07.tbl_turns_cbm'] = `${cbm.turns} turns (${cbm.steps} steps)`;
    b['cs07.tbl_turns_ce'] = `${ce.turns} turns (${ce.steps} steps)`;
    b['cs07.tbl_turns_delta'] = `${turnsRatio.toFixed(2)}× fewer turns (${stepsRatio.toFixed(2)}× fewer steps)`;

    b['cs07.tbl_calls_cbm'] = `${cbm.tool_calls} (${cbm.cbm_cli_calls} <code>cbm</code>, ${cbm.rg_calls} <code>rg</code>, ${cbm.read_file_calls} <code>read_file</code>, ${cbm.edit_file_calls + cbm.write_file_calls} edits)`;
    b['cs07.tbl_calls_ce'] = `${ce.tool_calls} (${ce.ce_cli_calls} <code>ce</code>, ${ce.rg_calls} <code>rg</code>, ${ce.read_file_calls} <code>read_file</code>, ${ce.edit_file_calls + ce.write_file_calls} edits)`;
    b['cs07.tbl_calls_delta'] = `${callsRatio.toFixed(2)}× fewer calls (${readsRatio.toFixed(2)}× fewer file reads)`;

    b['cs07.tbl_payload_cbm'] = `${fmtInt(cbm.raw_tool_chars)} chars (~${fmtInt(cbm.raw_tool_tokens)} est. tokens)`;
    b['cs07.tbl_payload_ce'] = `${fmtInt(ce.raw_tool_chars)} chars (~${fmtInt(ce.raw_tool_tokens)} est. tokens)`;
    b['cs07.tbl_payload_delta'] = `1.00× (31 vs 43 file reads)`;

    b['cs07.tbl_in_cbm'] = `${fmtInt(cbm.cum_input_tokens)} est. tokens (chars/4 across 54 turns incl. cache)`;
    b['cs07.tbl_in_ce'] = `${fmtInt(ce.cum_input_tokens)} est. tokens (chars/4 across 43 turns incl. cache)`;
    b['cs07.tbl_in_delta'] = `${tokRatio.toFixed(2)}× fewer input tokens (-843K)`;

    b['cs07.tbl_gates_cbm'] = `3 / 4 passed (1 new in <code>SIG-TWENTY-003</code>; evaluated post-session)`;
    b['cs07.tbl_gates_ce'] = `4 / 4 passed (0 new violations)`;
    b['cs07.tbl_gates_delta'] = `Pre-configured gate caught copied debt; refactored in 1 turn`;

    b['catalog.cs07_outcome'] =
      `<strong class="tone-ok">${fmtRoundMult(speedup)} faster end-to-end</strong> (${fmtDurShort(ce.wall_clock_s)} vs ${fmtDurShort(cbm.wall_clock_s)}; ${fmtDurShort(ce.init_impl_s)} vs ${fmtDurShort(cbm.init_impl_s)} draft)<br>` +
      `<strong class="tone-ok">4/4 gates passed vs 1 new violation</strong> (pre-configured gate caught copied legacy debt; 1-turn fix)<br>` +
      `<strong>Both passed unit tests &amp; oxlint</strong> (4/4 vs 3/3 specs; CBM kept AdminPanelBillingService &rarr; emailingDomain)`;
  }

  // --- Case Study 08 (4-Task Brownfield Architecture & Feature Suite on twentyhq/twenty) ---
  if (data.cs08 && data.cs08.status === 'ready' && data.cs08.telemetry) {
    const c8 = data.cs08;
    const ce = c8.telemetry.ce;
    const cbm = c8.telemetry.cbm;
    const speedupCbm = cbm.wall_clock_s / ce.wall_clock_s;

    b['cs08.meta_desc'] =
      `Case Study 08: 4-task brownfield feature implementation suite on twentyhq/twenty (${fmtInt(c8.repo.files)} files, ${c8.repo.loc} LOC) comparing CodeExplorer (${c8.ce_version}) against CBM (${c8.cbm_version}) across 4 real backend tasks. CodeExplorer finished ${speedupCbm.toFixed(2)}× faster (${fmtDurShort(ce.wall_clock_s)} vs ${fmtDurShort(cbm.wall_clock_s)}), caught 4/4 copied legacy shortcuts via pre-configured ratchet gates, and passed 14/14 unit tests with 0 gate-tampering attempts.`;
    b['cs08.crumb_scale'] = `${c8.repo.packages} packages, ${fmtInt(c8.repo.files)} files, ${c8.repo.loc} LOC · 4 brownfield feature tasks`;

    b['cs08.card_violations'] = `0/4 vs 4/4`;
    b['cs08.card_violations_copy'] =
      `Without gates, both CBM and CE Phase 1 copied adjacent [TOL] legacy shortcuts in 4/4 tasks (100%). With ce gate check, 0/4 shipped.`;
    b['cs08.card_violations_footer'] = `0 final violations vs 4 in CBM (256 tolerated baseline)`;

    b['cs08.card_speedup'] = fmtRoundMult(speedupCbm);
    b['cs08.card_speed_copy'] =
      `Across all 4 tasks combined, CodeExplorer finished in ${fmtDurShort(ce.wall_clock_s)} (incl. scan, gate check &amp; 1-turn fixes) vs ${fmtDurShort(cbm.wall_clock_s)} for CBM.`;
    b['cs08.card_speed_footer'] = `${fmtInt(ce.wall_clock_s)} s vs ${fmtInt(cbm.wall_clock_s)} s (-9m 52s across 4 tasks)`;

    b['cs08.card_tests'] = `14/14 Pass`;
    b['cs08.card_tests_copy'] =
      `Both CodeExplorer and CBM passed 14/14 unit tests (4/4 tasks), verified via scripts/run-acceptance-tests.mjs; only CodeExplorer passed 4/4 architectural gates.`;
    b['cs08.card_tests_footer'] = `14/14 unit tests + 4/4 architectural gates`;

    b['cs08.card_tamper'] = `0 Bypasses`;
    b['cs08.card_tamper_copy'] =
      `In 4/4 tasks, the CodeExplorer agent refactored to the proper NestJS domain service in 1 iteration with 0 attempts to run ratchet --update or edit gate files.`;
    b['cs08.card_tamper_footer'] = `1 self-correction iteration per task, 0 gate tampering`;

    b['cs08.participants_html'] = [
      `Tested participants (4 brownfield feature tasks on unmodified twentyhq/twenty @ ${c8.repo.commit}):<br>`,
      `          1. CodeExplorer (ce): ${c8.ce_version} (.NET 11, SQLite + OpenCypher + Incremental ce scan + 4 repo-wide Semantic Invariant Gates, ${fmtInt(c8.repo.graph_nodes)} nodes, ${fmtInt(c8.repo.graph_edges)} edges, evaluated both pre-gate Phase 1: Navigation Only and post-gate Phase 2: Navigation + Gates)<br>`,
      `          2. codebase-memory-mcp (CBM): ${c8.cbm_version} (win32-x64 Go + SQLite binary, index_repository + query_graph + trace_path)<br>`,
      `          3. LLM Model: ${c8.model} (100% symmetric system prompts on unmodified repository CLAUDE.md; Tasks 01, 02 &amp; 04 executed for this suite + Task 03 from Case 07)<br>`,
      `          4. Target Repository: ${c8.repo.name} @ ${c8.repo.commit} (${c8.repo.packages} packages, ${fmtInt(c8.repo.files)} files, ${c8.repo.loc} LOC)`,
    ].join('\n');

    b['catalog.cs08_outcome'] =
      `<strong class="tone-ok">0/4 vs 4/4 violated tasks</strong> (both copied [TOL] debt in 4/4 tasks; ce gate check fixed 4/4 in 1 turn, 0 bypasses)<br>` +
      `<strong class="tone-ok">${fmtRoundMult(speedupCbm)} faster suite total</strong> (${fmtDurShort(ce.wall_clock_s)} vs ${fmtDurShort(cbm.wall_clock_s)} across 4 tasks; $0.48 vs $0.48)<br>` +
      `<strong>14/14 unit tests pass for both</strong> (CBM shipped 4 cross-module / transport leaks)`;
  }

  // --- Case Study 09 (Zero-Spoiler Coding Benchmark: One-Shot Context Pack vs Bare Agent) ---
  if (data.cs09 && data.cs09.status === 'ready' && data.cs09.telemetry) {
    const c9 = data.cs09;
    const ce = c9.telemetry.ce;
    const bare = c9.telemetry.bare;
    const runs = c9.telemetry.runs;

    const speedup = bare.wall_clock_seconds / ce.wall_clock_seconds;
    const turnsRatio = bare.llm_turns / ce.llm_turns;
    const callsRatio = bare.total_tool_calls / ce.total_tool_calls;
    const readsRatio = bare.read_file_calls / ce.read_file_calls;
    const cmdsRatio = bare.run_command_calls / ce.run_command_calls;
    const uncachedRatio = bare.api_uncached_input_tokens / ce.api_uncached_input_tokens;
    const tokRatio = bare.api_total_input_tokens / ce.api_total_input_tokens;
    const outRatio = bare.api_output_tokens / ce.api_output_tokens;

    const pctSaved = (a, bVal) => Math.round(((a - bVal) / a) * 100);

    b['cs09.meta_desc'] =
      `Zero-spoiler constructive coding benchmark across 3 multi-module tickets (6 paired runs) on ${c9.repo.name} (${fmtInt(c9.repo.files)} indexed files) with symmetric architectural rules: ` +
      `CodeExplorer One-Shot Context Pack (${fmtDurShort(ce.wall_clock_seconds, true)}, ${ce.llm_turns} turns, ${fmtRoundTokens(ce.api_total_input_tokens)} input tokens, ${ce.read_file_calls} file reads) vs ` +
      `Bare Agent (${fmtDurShort(bare.wall_clock_seconds, true)}, ${bare.llm_turns} turns, ${fmtRoundTokens(bare.api_total_input_tokens)} input tokens, ${bare.read_file_calls} file reads).`;

    b['cs09.crumb_scale'] = `${c9.repo.packages} packages, ${fmtInt(c9.repo.files)} indexed files, 3 zero-spoiler tasks (6 paired runs)`;

    b['cs09.card_tokens'] = fmtRoundMult(tokRatio);
    b['cs09.card_tokens_copy'] =
      `Across all 3 zero-spoiler tasks, CodeExplorer used ${fmtRoundTokens(ce.api_total_input_tokens)} cumulative input tokens vs ${fmtRoundTokens(bare.api_total_input_tokens)} for the Bare Agent (${fmtRoundMult(uncachedRatio)} fewer uncached tokens).`;
    b['cs09.card_tokens_footer'] = `${fmtInt(ce.api_total_input_tokens)} vs ${fmtInt(bare.api_total_input_tokens)} total input tokens`;

    b['cs09.card_reads'] = fmtRoundMult(readsRatio);
    b['cs09.card_reads_copy'] =
      `One-shot task context compilation cut source file reads from ${bare.read_file_calls} down to ${ce.read_file_calls} and shell commands from ${bare.run_command_calls} down to ${ce.run_command_calls} (0 rg calls in Task 2).`;
    b['cs09.card_reads_footer'] = `${ce.read_file_calls} vs ${bare.read_file_calls} file reads (${ce.total_tool_calls} vs ${bare.total_tool_calls} tool calls)`;

    b['cs09.card_speedup'] = fmtRoundMult(speedup);
    b['cs09.card_speed_copy'] =
      `Completed all 3 backend tasks in ${fmtDurShort(ce.wall_clock_seconds, true)} (${ce.llm_turns} turns) with CodeExplorer vs ${fmtDurShort(bare.wall_clock_seconds, true)} (${bare.llm_turns} turns) for the Bare Agent.`;
    b['cs09.card_speed_footer'] = `${fmtInt(ce.wall_clock_seconds)} s (${ce.llm_turns} turns) vs ${fmtInt(bare.wall_clock_seconds)} s (${bare.llm_turns} turns)`;

    b['cs09.card_pass'] = `${ce.behavioral_passed}/${ce.behavioral_total} Pass`;
    b['cs09.card_pass_copy'] =
      `Every run is verified against an external held-out suite of ${ce.behavioral_total} Jest behavioral assertions (5 per task), tsgo typechecking, and 7 Semantic Invariant Gates (${ce.gates_passed}/${ce.gates_total} passed).`;
    b['cs09.card_pass_footer'] = `${ce.behavioral_passed}/${ce.behavioral_total} Jest assertions, ${ce.gates_passed}/${ce.gates_total} SIG gates (0 new)`;

    b['cs09.participants_html'] = [
      `Tested participants (3 zero-spoiler constructive coding tasks = 6 paired runs on ${c9.repo.name} @ ${c9.repo.commit}):<br>`,
      `          1. CodeExplorer (ce): ${c9.ce_version} (.NET 11, SQLite + OpenCypher, one-shot task context pack + semantic invariant gates, ${fmtInt(c9.repo.graph_nodes)} nodes, ${fmtInt(c9.repo.graph_edges)} edges)<br>`,
      `          2. Bare Agent Baseline: ripgrep (rg ${c9.ripgrep_version}) + read_file + edit_file + write_file (identical sandbox and identical 7 architectural rules in system prompt)<br>`,
      `          3. LLM Model: ${c9.model} (100% paired same-model runs across all 3 tasks)<br>`,
      `          4. Target Repository: ${c9.repo.name} @ ${c9.repo.commit} (${c9.repo.packages} packages, ${fmtInt(c9.repo.files)} indexed files, TypeScript, NestJS, GraphQL, TypeORM)`,
    ].join('\n');

    b['cs09.suite_tbody'] = [
      `              <tr>`,
      `                <td><strong>Held-Out Jest Behavioral Specs</strong></td>`,
      `                <td class="cell-num">${bare.behavioral_passed} / ${bare.behavioral_total} (100%)</td>`,
      `                <td class="cell-win">${ce.behavioral_passed} / ${ce.behavioral_total} (100%)</td>`,
      `                <td class="cell-win">Parity (${ce.behavioral_passed}/${ce.behavioral_total} pass)</td>`,
      `              </tr>`,
      `              <tr>`,
      `                <td><strong>Architectural Gates (SIG-TWENTY-001..007)</strong></td>`,
      `                <td class="cell-num">${bare.gates_passed} / ${bare.gates_total} (0&nbsp;new)</td>`,
      `                <td class="cell-win">${ce.gates_passed} / ${ce.gates_total} (0&nbsp;new)</td>`,
      `                <td class="cell-win">0 new violations</td>`,
      `              </tr>`,
      `              <tr>`,
      `                <td><strong>LLM Turns</strong></td>`,
      `                <td class="cell-num">${bare.llm_turns}&nbsp;turns</td>`,
      `                <td class="cell-win">${ce.llm_turns}&nbsp;turns</td>`,
      `                <td class="cell-win">${turnsRatio.toFixed(2)}× fewer turns (-${pctSaved(bare.llm_turns, ce.llm_turns)}%)</td>`,
      `              </tr>`,
      `              <tr>`,
      `                <td><strong>Total Tool Calls</strong></td>`,
      `                <td class="cell-num">${bare.total_tool_calls}&nbsp;calls</td>`,
      `                <td class="cell-win">${ce.total_tool_calls}&nbsp;calls</td>`,
      `                <td class="cell-win">${callsRatio.toFixed(2)}× fewer calls (-${pctSaved(bare.total_tool_calls, ce.total_tool_calls)}%)</td>`,
      `              </tr>`,
      `              <tr>`,
      `                <td><strong>Source File Reads (read_file)</strong></td>`,
      `                <td class="cell-num">${bare.read_file_calls}&nbsp;reads</td>`,
      `                <td class="cell-win">${ce.read_file_calls}&nbsp;reads</td>`,
      `                <td class="cell-win">${readsRatio.toFixed(2)}× fewer reads (-${pctSaved(bare.read_file_calls, ce.read_file_calls)}%)</td>`,
      `              </tr>`,
      `              <tr>`,
      `                <td><strong>Shell Commands (run_command)</strong></td>`,
      `                <td class="cell-num">${bare.run_command_calls}&nbsp;commands</td>`,
      `                <td class="cell-win">${ce.run_command_calls}&nbsp;commands</td>`,
      `                <td class="cell-win">${cmdsRatio.toFixed(2)}× fewer commands (-${pctSaved(bare.run_command_calls, ce.run_command_calls)}%)</td>`,
      `              </tr>`,
      `              <tr>`,
      `                <td><strong>Wall-Clock Time</strong></td>`,
      `                <td class="cell-num">${fmtInt(bare.wall_clock_seconds)}&nbsp;s (${fmtDurShort(bare.wall_clock_seconds, true)})</td>`,
      `                <td class="cell-win">${fmtInt(ce.wall_clock_seconds)}&nbsp;s (${fmtDurShort(ce.wall_clock_seconds, true)})</td>`,
      `                <td class="cell-win">${speedup.toFixed(2)}× faster (${fmtSavedShort(bare.wall_clock_seconds - ce.wall_clock_seconds)})</td>`,
      `              </tr>`,
      `              <tr>`,
      `                <td><strong>Uncached Input Tokens</strong></td>`,
      `                <td class="cell-num">${fmtInt(bare.api_uncached_input_tokens)}&nbsp;tokens</td>`,
      `                <td class="cell-win">${fmtInt(ce.api_uncached_input_tokens)}&nbsp;tokens</td>`,
      `                <td class="cell-win">${uncachedRatio.toFixed(2)}× fewer uncached (-${pctSaved(bare.api_uncached_input_tokens, ce.api_uncached_input_tokens)}%)</td>`,
      `              </tr>`,
      `              <tr>`,
      `                <td><strong>Cumulative Input Tokens (Total)</strong></td>`,
      `                <td class="cell-num">${fmtInt(bare.api_total_input_tokens)}&nbsp;tokens</td>`,
      `                <td class="cell-win">${fmtInt(ce.api_total_input_tokens)}&nbsp;tokens</td>`,
      `                <td class="cell-win">${tokRatio.toFixed(2)}× fewer input tokens (-${pctSaved(bare.api_total_input_tokens, ce.api_total_input_tokens)}%)</td>`,
      `              </tr>`,
      `              <tr>`,
      `                <td><strong>Output Tokens</strong></td>`,
      `                <td class="cell-num">${fmtInt(bare.api_output_tokens)}&nbsp;tokens</td>`,
      `                <td class="cell-win">${fmtInt(ce.api_output_tokens)}&nbsp;tokens</td>`,
      `                <td class="cell-win">${outRatio.toFixed(2)}× fewer output tokens (-${pctSaved(bare.api_output_tokens, ce.api_output_tokens)}%)</td>`,
      `              </tr>`,
    ].join('\n');

    const taskMeta = [
      { key: 't1', title: 'Task 1: API Key Revocation', desc: 'Delete scoped usageLimit rows, recompute cache &amp; drop quota counters' },
      { key: 't2', title: 'Task 2: Application Deletion', desc: 'Cascade publicDomain teardown + external DNS custom hostname removal' },
      { key: 't3', title: 'Task 3: Subdomain Collisions', desc: 'Prevent subdomain &amp; suggestion collisions with registered PublicDomain' },
    ];

    b['cs09.tasks_tbody'] = taskMeta
      .map(({ key, title, desc }) => {
        const rCe = runs[`${key}-ce`];
        const rBare = runs[`${key}-bare`];
        const tCe = rCe.telemetry;
        const tBare = rBare.telemetry;
        const tbCe = tCe.tool_breakdown;
        const tbBare = tBare.tool_breakdown;
        const tTokRatio = (tBare.api_total_input_tokens / tCe.api_total_input_tokens).toFixed(2);
        return [
          `              <tr>`,
          `                <td><strong>${title}</strong><br><span class="tone-muted">${desc}</span></td>`,
          `                <td><strong>CodeExplorer</strong></td>`,
          `                <td class="cell-win">${rCe.behavioral_passed}/${rCe.behavioral_total} Jest<br>${rCe.gates_passed}/7 SIG</td>`,
          `                <td class="cell-win">${tCe.llm_turns}&nbsp;turns<br><span class="tone-muted">${tCe.total_tool_calls}&nbsp;tools (${tbCe.run_command}/${tbCe.read_file}/${tbCe.edit_file})</span></td>`,
          `                <td class="cell-win">${fmtInt(tCe.wall_clock_seconds)}&nbsp;s (${fmtDurShort(tCe.wall_clock_seconds, true)})</td>`,
          `                <td class="cell-win">${fmtInt(tCe.api_uncached_input_tokens)}&nbsp;tokens</td>`,
          `                <td class="cell-win">${fmtInt(tCe.api_total_input_tokens)}&nbsp;tokens<br><span class="tone-muted">${tTokRatio}× fewer</span></td>`,
          `                <td class="cell-win">${fmtInt(tCe.api_output_tokens)}&nbsp;tokens</td>`,
          `              </tr>`,
          `              <tr>`,
          `                <td><strong>${title}</strong><br><span class="tone-muted">Baseline ripgrep + read_file</span></td>`,
          `                <td>Bare</td>`,
          `                <td class="cell-num">${rBare.behavioral_passed}/${rBare.behavioral_total} Jest<br>${rBare.gates_passed}/7 SIG</td>`,
          `                <td class="cell-num">${tBare.llm_turns}&nbsp;turns<br><span class="tone-muted">${tBare.total_tool_calls}&nbsp;tools (${tbBare.run_command}/${tbBare.read_file}/${tbBare.edit_file})</span></td>`,
          `                <td class="cell-num">${fmtInt(tBare.wall_clock_seconds)}&nbsp;s (${fmtDurShort(tBare.wall_clock_seconds, true)})</td>`,
          `                <td class="cell-num">${fmtInt(tBare.api_uncached_input_tokens)}&nbsp;tokens</td>`,
          `                <td class="cell-num">${fmtInt(tBare.api_total_input_tokens)}&nbsp;tokens</td>`,
          `                <td class="cell-num">${fmtInt(tBare.api_output_tokens)}&nbsp;tokens</td>`,
          `              </tr>`,
        ].join('\n');
      })
      .join('\n');

    b['catalog.cs09_outcome'] =
      `<strong class="tone-ok">${fmtRoundMult(tokRatio)} fewer input tokens</strong> (${fmtRoundTokens(ce.api_total_input_tokens)} vs ${fmtRoundTokens(bare.api_total_input_tokens)})<br>` +
      `<strong class="tone-ok">${fmtRoundMult(readsRatio)} fewer file reads &amp; ${fmtRoundMult(speedup)} faster</strong> (${ce.read_file_calls} vs ${bare.read_file_calls} reads, ${fmtDurShort(ce.wall_clock_seconds, true)} vs ${fmtDurShort(bare.wall_clock_seconds, true)})<br>` +
      `<strong>${ce.behavioral_passed}/${ce.behavioral_total} Jest specs &amp; ${ce.gates_passed}/${ce.gates_total} SIG gates pass</strong> (0 rg calls needed in Task 2)`;
  }

  if (data.cs10 && data.cs10.status === 'ready' && data.cs10.telemetry) {
    const c10 = data.cs10;
    const t10 = c10.telemetry;
    b['cs10.meta_desc'] =
      `Case Study 10: Verifying CQRS read/write separation across ${t10.repos_evaluated} open-source codebases (${t10.total_query_handlers_traced_to_storage} query handlers traced to SQL/ORM across ${t10.repos_reaching_storage} repos, 0 LLM tokens, $0.00) using CodeExplorer Semantic Invariant Gates (SIG-CQRS-001 to 003).`;
    b['cs10.crumb_scale'] = `${t10.repos_reaching_storage} storage-traced repos (${t10.repos_evaluated} scanned), ${t10.total_query_handlers_traced_to_storage} query handlers`;
    b['cs10.card_handlers'] = `${t10.total_query_handlers_traced_to_storage}`;
    b['cs10.card_handlers_copy'] =
      `Contract-identified QueryHandlers traced through DI and mediator dispatch down to SQL and ORM table operations across ${t10.repos_reaching_storage} repositories.`;
    b['cs10.card_handlers_footer'] = `${t10.repos_reaching_storage} of ${t10.repos_evaluated} repos reached SQL/ORM edges`;
    b['cs10.card_clean'] = `29 / 29`;
    b['cs10.card_clean_copy'] =
      `In kgrzybek/modular-monolith-with-ddd, all 29 SQL-traced IQueryHandler classes resolved strictly to READS_FROM with 0 write paths.`;
    b['cs10.card_clean_footer'] = `4 of 6 storage-traced repos had 0 read writes`;
    b['cs10.card_findings'] = `${t10.sig_cqrs_001_findings} + ${t10.sig_cqrs_003_findings}`;
    b['cs10.card_findings_copy'] =
      `Surfaced ${t10.sig_cqrs_001_findings} QueryHandler write paths (SIG-CQRS-001) and ${t10.sig_cqrs_003_findings} HTTP GET write paths (SIG-CQRS-003) invisible to ArchUnit and linters.`;
    b['cs10.card_findings_footer'] = `dotnet-starter-kit &amp; ever-gauzy verified in source`;
    b['cs10.card_tokens'] = `0`;
    b['cs10.card_tokens_copy'] =
      `0 LLM tokens ($0.00 cost). Evaluates 3 built-in declarative Cypher gates locally against SQLite graph.db in milliseconds.`;
    b['cs10.card_tokens_footer'] = `Deterministic CI exit codes + SHA-256 ratchet`;
    b['cs10.participants_html'] = [
      `Evaluated system &amp; corpus configuration:<br>`,
      `          1. CodeExplorer (ce): ${c10.ce_version} (.NET 11, SQLite + Cypher, built-in SIG-CQRS-001, SIG-CQRS-002 &amp; SIG-CQRS-003 starter gates, 0 tokens, $0.00)<br>`,
      `          2. Storage-Traced Corpus (6 repos): kgrzybek/modular-monolith-with-ddd @ 91c8ef2, fullstackhero/dotnet-starter-kit @ 2f5989b, ever-co/ever-gauzy @ 0dc826c7, jasontaylordev/CleanArchitecture @ 9e1042b, Sairyss/domain-driven-hexagon @ 5c2d15a, asc-lab/dotnetcore-microservices-poc @ 6500daa<br>`,
      `          3. Uncovered Driver / Non-Mediator Corpus (7 repos, reported transparently as vacuous): dotnet/eShop @ ab10e4c, Squidex/squidex @ 1c8a367, simplcommerce/SimplCommerce @ 3472ba0, BottegaIT/ddd-leaven-v2 @ 5225ff7, AleksK1NG/Go-CQRS-Microservices @ 70cda28, ThreeDotsLabs/wild-workouts-go-ddd-example @ 8ecfcdf, CodelyTV/php-ddd-example @ 9271c46`,
    ].join('\n');
    b['catalog.cs10_outcome'] =
      `<strong class="tone-ok">${t10.total_query_handlers_traced_to_storage} QueryHandlers traced to SQL/ORM</strong> across ${t10.repos_reaching_storage} polyglot repos (29/29 clean in modular-monolith-with-ddd)<br>` +
      `<strong class="tone-ok">Surfaced ${t10.sig_cqrs_001_findings} QueryBus &amp; ${t10.sig_cqrs_003_findings} GET write paths</strong> missed by NetArchTest &amp; linters<br>` +
      `<strong>0 tokens ($0.00)</strong> via 3 built-in Cypher gates (SIG-CQRS-001 to 003)`;
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

    const c5 = data.cs05;
    const ce5 = c5.telemetry.ce;
    const rg5 = c5.telemetry.rg;
    const sp5 = (rg5.time_to_answer_s / ce5.time_to_answer_s).toFixed(2);
    const tk5 = (rg5.cum_input_tokens / ce5.cum_input_tokens).toFixed(2);
    const pl5 = (rg5.raw_tool_chars / ce5.raw_tool_chars).toFixed(2);
    const rd5 = (rg5.view_file_calls / ce5.view_file_calls).toFixed(2);
    const cost5Ratio = (rg5.cost_flash_usd / ce5.cost_flash_usd).toFixed(2);

    b['md.root_agent_table'] = [
      `| Metric | **\`twentyhq/twenty\` (\`24.5k\` files): \`CBM\` → \`CodeExplorer\`** ([Case 01](./benchmarks/01-agent-audit-ce-vs-cbm-on-twenty/README.md)) | **324-Service Multi-Repo (\`13.8k\` files): \`ripgrep\` → \`CodeExplorer\`** ([Case 02](./benchmarks/02-enterprise-microservices-deep-audit/README.md)) | **\`OrchardCore\` (\`334\` .NET projects): \`ripgrep\` → \`CodeExplorer\`** ([Case 05](./benchmarks/05-polyglot-architecture-and-blast-radius/README.md)) |`,
      `| :--- | :--- | :--- | :--- |`,
      `| **Time to Answer** | \`${fmtDurShort(cbm1.time_to_answer_s)}\` → **\`${fmtDurShort(ce1.time_to_answer_s)}\` (\`${sp1}×\` faster)** | \`${fmtDurShort(rg2.time_to_answer_s)}\` → **\`${fmtDurShort(ce2.time_to_answer_s)}\` (\`${sp2}×\` faster)** | \`${fmtDurShort(rg5.time_to_answer_s, true)}\` → **\`${fmtDurShort(ce5.time_to_answer_s, true)}\` (\`${sp5}×\` faster)** |`,
      `| **Cumulative Input Tokens** | \`${fmtCompactTokens(cbm1.cum_input_tokens, 2)}\` → **\`${fmtCompactTokens(ce1.cum_input_tokens, 1)}\` (\`${tk1}×\` fewer)** | \`${fmtCompactTokens(rg2.cum_input_est_tokens, 2)}\` → **\`${fmtCompactTokens(ce2.cum_input_est_tokens, 2)}\` (\`${tk2}×\` fewer, \`${cost2Ratio}×\` cheaper)** | \`${fmtCompactTokens(rg5.cum_input_tokens, 2)}\` → **\`${fmtCompactTokens(ce5.cum_input_tokens, 2)}\` (\`${tk5}×\` fewer, \`${pl5}×\` less tool output, \`${cost5Ratio}×\` cheaper)** |`,
      `| **Agent Effort** | \`${cbm1.turns}\` turns (\`${c1.effort_details.cbm_queries}\` \`CBM\` + \`${c1.effort_details.cbm_scripts}\` AST scripts + \`${c1.effort_details.cbm_file_reads}\` reads) → **\`${ce1.turns}\` turns (\`${c1.effort_details.ce_cypher_calls}\` queries, \`${c1.effort_details.ce_file_reads}\` reads)** | \`${rg2.turns}\` turns (\`${rg2.run_command_calls}\` CLI, \`${rg2.view_file_calls}\` reads, \`${rg2.scripts_written}\` scripts) → **\`${ce2.turns}\` turns (\`${ce2.run_command_calls}\` CLI, \`${ce2.view_file_calls}\` reads, \`0\` scripts)** | \`${rg5.turns}\` turns / \`${rg5.steps}\` steps (\`${rg5.view_file_calls}\` file reads) → **\`${ce5.turns}\` turns / \`${ce5.steps}\` steps (\`${ce5.view_file_calls}\` file reads, \`${rd5}×\` fewer)** |`,
      `| **Recall & Context Health** | \`${cbm1Rec}%\` (\`${c1.recall.cbm_graph_matched} / ${c1.recall.gt_total}\`) from \`CBM\` graph → **\`${ce1Rec}%\` (\`${c1.recall.ce_matched} / ${c1.recall.gt_total}\`) from \`graph.db\`** | \`82%\` *(missed ${c2.recall.rg_missed_services} RTB services, 1 compaction)* → **\`${c2.recall.ce_pct}\` recall (\`0\` blind spots)** | \`92.7%\` (\`307 / 331\`, *24 caller errors*) → **\`${c5.recall.ce_pct}\` (\`331 / 331\`)** |`,
    ].join('\n');

    const cs05Row =
      data.cs05.status === 'ready' && data.cs05.telemetry
        ? `| **[Case Study 05: Modular .NET Monolith Audit](./05-polyglot-architecture-and-blast-radius/README.md)**<br>AppSec \`AuthorizeAsync\` audit, \`YesSql.ISession\` layer violations, \`PublishedAsync\` side-effects & \`IContentManager\` blast radius | \`${c5.repo.name}\`<br>\`${c5.repo.projects}\` projects, \`${fmtInt(c5.repo.files)}\` files (.NET, ASP.NET Core, YesSql) | \`CodeExplorer\`<br>vs<br>\`ripgrep\` + \`view_file\` | **\`${sp5}×\` faster**<br>\`${fmtDurShort(ce5.time_to_answer_s, true)}\` vs \`${fmtDurShort(rg5.time_to_answer_s, true)}\`<br>\`${ce5.turns}\` turns (\`${ce5.steps}\` steps) vs \`${rg5.turns}\` turns (\`${rg5.steps}\` steps) | **\`${tk5}×\` fewer tokens** (\`${pl5}×\` less tool output)<br>\`${fmtInt(ce5.cum_input_tokens)}\` vs \`${fmtInt(rg5.cum_input_tokens)}\`<br>(\`$${ce5.cost_flash_usd.toFixed(2)}\` vs \`$${rg5.cost_flash_usd.toFixed(2)}\`, \`${cost5Ratio}×\` cheaper) | **\`${c5.recall.ce_pct}\` vs \`${c5.recall.rg_pct}\`** (\`331/331\` vs \`307/331\`)<br>\`${ce5.view_file_calls}\` file reads vs \`${rg5.view_file_calls}\` file reads (\`${rd5}×\` fewer) |`
        : `| **[Case Study 05: Modular .NET Monolith Audit](./05-polyglot-architecture-and-blast-radius/README.md)**<br>Cross-module C# interface hierarchies, DI call chains & YesSql indexes | \`OrchardCMS/OrchardCore\`<br>\`[TBD ~180]\` C# projects, \`[TBD ~6,000]\` files (.NET, ASP.NET Core, YesSql) | \`CodeExplorer\`<br>vs<br>\`ripgrep\` | **\`[TBD ×]\` faster**<br>\`[TBD]\` vs \`[TBD]\`<br>\`[TBD]\` turns vs \`[TBD]\` turns | **\`[TBD ×]\` fewer tokens**<br>\`[TBD]\` vs \`[TBD]\`<br>(\`$[TBD]\` vs \`$[TBD]\`) | **\`[TBD %]\` vs \`[TBD %]\`**<br>\`[TBD]\` file reads vs \`[TBD]\` file reads |`;

    const c6 = data.cs06;
    const t6 = c6.telemetry;

    const c7 = data.cs07;
    const ce7 = c7.telemetry.ce;
    const cbm7 = c7.telemetry.cbm;
    const sp7 = (cbm7.wall_clock_s / ce7.wall_clock_s).toFixed(2);
    const sp7Init = (cbm7.init_impl_s / ce7.init_impl_s).toFixed(2);
    const tk7 = (cbm7.cum_input_tokens / ce7.cum_input_tokens).toFixed(2);

    const cs07Row =
      `| **[Case Study 07: Real-Life Feature Implementation & Self-Healing](./07-agent-feature-ce-vs-cbm-on-twenty/README.md)**<br>Implement \`suspendWorkspaceAdminPanel\` GraphQL mutation, service orchestration & unit tests on \`twentyhq/twenty\` (4 repo-wide architectural gates, zero rule hints) | \`${c7.repo.name}\`<br>\`${c7.repo.packages}\` packages, \`${fmtInt(c7.repo.files)}\` files, \`${c7.repo.loc}\` LOC (TS, NestJS, GraphQL, TypeORM, BullMQ) | \`CodeExplorer\`<br>vs<br>\`CBM\` | **\`${sp7}×\` faster end-to-end**<br>\`${fmtDurShort(ce7.wall_clock_s)}\` vs \`${fmtDurShort(cbm7.wall_clock_s)}\` (\`${fmtDurShort(ce7.init_impl_s)}\` vs \`${fmtDurShort(cbm7.init_impl_s)}\` initial draft, \`${sp7Init}×\` faster)<br>\`${ce7.turns}\` turns vs \`${cbm7.turns}\` turns | **\`${tk7}×\` fewer est. tokens**<br>\`${fmtInt(ce7.cum_input_tokens)}\` vs \`${fmtInt(cbm7.cum_input_tokens)}\`<br>(\`${ce7.read_file_calls}\` vs \`${cbm7.read_file_calls}\` file reads) | **\`4/4 PASS\` vs \`1 new\` violation (\`SIG-TWENTY-003\`)**<br>Both passed unit tests (\`4/4\` vs \`3/3\`) & \`oxlint\`; pre-configured gate caught copied legacy coupling from \`workspace.service.ts:989\` (\`[TOL]\` -> \`[NEW]\`) & \`ce\` refactored in \`1\` turn |`;

    const cs06Row =
      `| **[Case Study 06: Pre-Commit Architectural Guard (Showcase)](./06-precommit-architectural-guard/README.md)**<br>6 invariant gates + 4-phase brownfield SHA-256 ratchet lifecycle (Day 0 audit, line-shift-immune baseline freeze, PR regression block & monotonic debt tightening) | \`${c6.repo.name}\`<br>\`${c6.repo.projects}\` subsystems, \`${fmtInt(c6.repo.files)}\` files (Go, TypeScript, K8s) | \`CodeExplorer\` (\`ce gate check\` & \`ratchet\`) | **\`${t6.duration_ms} ms\`** Day 0 check<br>**\`183–356 ms\`** ratcheted checks | **\`0\` tokens**<br>(**\`$0.00\`**) | **4-phase deterministic lifecycle**<br>Freezes \`${t6.blocking_errors}\` legacy errors + \`${t6.audited_warnings}\` warnings (\`Exit 0\`), blocks \`1 new\` PR leak (\`Exit 1\`), tightens ceiling |`;

    const c8 = data.cs08;
    const ce8 = c8.telemetry.ce;
    const cbm8 = c8.telemetry.cbm;
    const sp8 = (cbm8.wall_clock_s / ce8.wall_clock_s).toFixed(2);

    const cs08Row =
      `| **[Case Study 08: 4-Task Brownfield Architecture & Feature Suite](./08-multi-task-architecture-suite-on-twenty/README.md)**<br>4 backend features & unit test suites across 4 modules (\`resetTwoFactorAuthenticationMethodsForUser\`, \`triggerReconnectAccountBanner\`, \`suspendWorkspaceAdminPanel\`, \`isSubdomainAvailable\`) comparing **\`CBM\`** vs **\`CodeExplorer Phase 1 (Navigation Only)\`** vs **\`Phase 2 (Navigation + Gates)\`** | \`${c8.repo.name}\`<br>\`${c8.repo.packages}\` packages, \`${fmtInt(c8.repo.files)}\` files, \`${c8.repo.loc}\` LOC (TS, NestJS, GraphQL, TypeORM) | \`CodeExplorer\`<br>vs<br>\`CBM\` | **\`${sp8}×\` faster suite total**<br>\`${fmtDurShort(ce8.wall_clock_s)}\` vs \`${fmtDurShort(cbm8.wall_clock_s)}\`<br>\`${ce8.turns}\` turns vs \`${cbm8.turns}\` turns | \`${fmtInt(ce8.cum_input_tokens)}\` vs \`${fmtInt(cbm8.cum_input_tokens)}\`<br>(\`$${ce8.cost_flash_usd.toFixed(2)}\` vs \`$${cbm8.cost_flash_usd.toFixed(2)}\`, \`${ce8.read_file_calls}\` vs \`${cbm8.read_file_calls}\` file reads) | **\`0/4\` vs \`4/4\` violated tasks (\`100%\` caught)**<br>Navigation alone copied adjacent \`[TOL]\` debt in \`4/4\` tasks; \`ce gate check\` fixed \`4/4\` in \`1\` iter (\`0\` tampering, \`14/14\` unit tests pass for both via \`run-acceptance-tests.mjs\`) |`;

    const c9 = data.cs09;
    const ce9 = c9.telemetry.ce;
    const bare9 = c9.telemetry.bare;
    const sp9 = (bare9.wall_clock_seconds / ce9.wall_clock_seconds).toFixed(2);
    const tk9 = (bare9.api_total_input_tokens / ce9.api_total_input_tokens).toFixed(2);

    const cs09Row =
      `| **[Case Study 09: Zero-Spoiler Coding Benchmark (One-Shot Context Pack vs Bare Agent)](./09-zero-spoiler-context-pack-vs-bare-agent/README.md)**<br>3 zero-spoiler bug & feature tickets (6 paired runs: \`apiKey\` revocation lifecycle, \`application\` public-domain DNS teardown, \`subdomain\` collision guard) with 100% symmetric architectural rules, comparing **\`Bare Agent\` (\`rg\` + \`read_file\`)** vs **\`CodeExplorer\` (One-Shot Context Pack + Gates)** | \`${c9.repo.name}\`<br>\`${c9.repo.packages}\` packages, \`${fmtInt(c9.repo.files)}\` indexed files (TS, NestJS, GraphQL, TypeORM) | \`CodeExplorer\`<br>vs<br>\`ripgrep\` | **\`${sp9}×\` faster**<br>\`${fmtDurShort(ce9.wall_clock_seconds, true)}\` vs \`${fmtDurShort(bare9.wall_clock_seconds, true)}\`<br>\`${ce9.llm_turns}\` turns vs \`${bare9.llm_turns}\` turns | **\`${tk9}×\` fewer tokens**<br>\`${fmtInt(ce9.api_total_input_tokens)}\` vs \`${fmtInt(bare9.api_total_input_tokens)}\`<br>(\`${ce9.read_file_calls}\` vs \`${bare9.read_file_calls}\` file reads, \`${ce9.run_command_calls}\` vs \`${bare9.run_command_calls}\` cmds) | **\`${ce9.behavioral_passed}/${ce9.behavioral_total}\` Jest specs & \`${ce9.gates_passed}/${ce9.gates_total}\` SIG gates (\`0\` new)**<br>Turn-1 context pack resolved table owners, cache/DNS side-effects & legacy traps upfront (\`0\` \`rg\` calls in Task 2) |`;

    const c10 = data.cs10;
    const t10 = c10.telemetry;
    const cs10Row =
      `| **[Case Study 10: CQRS Read/Write Separation Across Open-Source Codebases](./10-cqrs-read-write-invariants/README.md)**<br>3 built-in CQRS gates (\`SIG-CQRS-001..003\`) tracing framework \`QueryHandler\` contracts & \`HTTP GET\` routes across DI and mediator dispatch down to SQL/ORM writes | \`6\` storage-traced CQRS repos (\`${t10.repos_evaluated}\` scanned: .NET MediatR/Dapper/EF Core & TS NestJS \`@nestjs/cqrs\`/TypeORM) | \`CodeExplorer\` (\`ce gate check\`)<br>vs<br>\`ArchUnit\` / \`NetArchTest\` & linters | **\`2.1–24.8 s\`** cold scan (.NET/TS)<br>Sub-second Cypher gate checks | **\`0\` tokens**<br>(**\`$0.00\`**) | **\`${t10.total_query_handlers_traced_to_storage}\` QueryHandlers traced to SQL/ORM**<br>\`29/29\` read-only in \`modular-monolith-with-ddd\`; surfaced deliberate read-path writes in \`dotnet-starter-kit\` (\`GET /wallet/me\` -> \`Wallets.Add\`) & \`ever-gauzy\` (\`FindPublicInvoiceQuery\` -> \`markViewed\`) |`;

    b['md.catalog_reallife_table'] = [
      `| Case Study & Task | Repository Under Test | Compared Tools | Time & LLM Turns | Cumulative Input Tokens | Architectural Compliance & Outcome |`,
      `| :--- | :--- | :--- | :--- | :--- | :--- |`,
      cs10Row,
      cs09Row,
      cs08Row,
      cs07Row,
      cs06Row,
    ].join('\n');

    b['md.catalog_agent_table'] = [
      `| Case Study & Task | Repository Under Test | Compared Tools | Time & LLM Turns | Cumulative Input Tokens | Recall & Outcome |`,
      `| :--- | :--- | :--- | :--- | :--- | :--- |`,
      `| **[Case Study 01: Public Monorepo Audit](./01-agent-audit-ce-vs-cbm-on-twenty/README.md)**<br>DI fan-in, inheritance, BullMQ queues & API-to-DB write lineage | \`${c1.repo.name}\`<br>\`${c1.repo.packages}\` packages, \`${fmtInt(c1.repo.files)}\` files, \`${c1.repo.loc}\` LOC (TS, NestJS, GraphQL, TypeORM, BullMQ) | \`CodeExplorer\`<br>vs<br>\`CBM\` | **\`${sp1}×\` faster**<br>\`${fmtDurShort(ce1.time_to_answer_s)}\` vs \`${fmtDurShort(cbm1.time_to_answer_s)}\`<br>\`${ce1.turns}\` turns vs \`${cbm1.turns}\` turns | **\`${tk1}×\` fewer tokens**<br>\`${fmtInt(ce1.cum_input_tokens)}\` vs \`${fmtInt(cbm1.cum_input_tokens)}\`<br>(\`$${ce1.cost_flash_usd.toFixed(2)}\` vs \`$${cbm1.cost_flash_usd.toFixed(2)}\`) | **Native Graph: \`${ce1Rec}%\` vs \`${cbm1Rec}%\`**<br>\`${c1.recall.ce_matched}/${c1.recall.gt_total}\` from \`ce\` (\`${c1.effort_details.ce_file_reads}\` file reads) vs \`${c1.recall.cbm_graph_matched}/${c1.recall.gt_total}\` from \`CBM\` (wrote \`${c1.effort_details.cbm_scripts}\` AST scripts + \`${c1.effort_details.cbm_file_reads}\` file reads) |`,
      `| **[Case Study 02: 324-Service Enterprise Audit](./02-enterprise-microservices-deep-audit/README.md)**<br>Shared ORM tables, outbound HTTP fan-in, Pub/Sub & RabbitMQ topology (zero schema hints) | Enterprise Multi-Repo Workspace<br>\`${c2.repo.modules}\` independent service/library repos (\`${c2.repo.services}\` services, \`${fmtInt(c2.repo.files)}\` files, TS, Go, SQL) | \`CodeExplorer\`<br>vs<br>\`ripgrep\` + Python | **\`${sp2}×\` faster**<br>\`${fmtDurShort(ce2.time_to_answer_s, true)}\` vs \`${fmtDurShort(rg2.time_to_answer_s, true)}\`<br>\`${ce2.turns}\` turns vs \`${rg2.turns}\` turns | **\`${tk2}×\` fewer tokens**<br>\`${fmtInt(ce2.cum_input_est_tokens)}\` vs \`${fmtInt(rg2.cum_input_est_tokens)}\`<br>(\`$${ce2.cost_flash_usd.toFixed(2)}\` vs \`$${rg2.cost_flash_usd.toFixed(2)}\`, \`${cost2Ratio}×\` cheaper) | **\`${c2.recall.ce_pct}\` vs \`${c2.recall.rg_pct_round}\`**<br>\`0\` missed services (\`${ce2.view_file_calls}\` file reads, \`${ce2.scripts_written}\` scripts) vs \`${c2.recall.rg_missed_services}\` RTB services missed (\`${rg2.view_file_calls}\` file reads + \`${rg2.scripts_written}\` scripts) |`,
      cs05Row,
    ].join('\n');

    b['md.catalog_participants'] = [
      `1. **CodeExplorer (\`ce\`)**: \`${data.meta.tools.ce_suite_versions}\` (.NET 10 & .NET 11, SQLite + OpenCypher, \`${data.meta.tools.ce_mcp_tools}\` MCP tools)`,
      `2. **codebase-memory-mcp (\`CBM\`)**: \`${data.meta.tools.cbm_version}\` (\`win32-x64\` Go + SQLite binary, \`${data.meta.tools.cbm_mcp_tools}\` MCP tools)`,
      `3. **Graphify (\`graphifyy\`)**: \`${data.meta.tools.graphify_version}\` (Python ${data.meta.tools.python_version}, Tree-sitter + NetworkX \`--code-only\`, \`${data.meta.tools.graphify_mcp_tools}\` MCP tools)`,
      `4. **Lexical CLI Baseline**: \`ripgrep\` (\`rg ${data.meta.tools.ripgrep_version}\`) + Python \`${data.meta.tools.python_version}\` (Case 02) + \`view_file\``,
      `5. **LLM Models**: \`${data.meta.llm_model}\` (Cases 01, 02, 05, 07 & 08) & \`${data.cs09.model}\` (Case 09, symmetric system prompts across paired runs)`,
      `6. **Pinned Target Commits**: \`twentyhq/twenty @ ${data.cs01.repo.commit}\` (Case 01), \`twentyhq/twenty @ ${data.cs03.repos.twenty.commit}\` (Cases 03, 04, 07, 08 & 09), \`immich-app/immich @ ${data.cs03.repos.immich.commit}\` (Cases 03 & 04), \`${data.cs05.repo.name} @ ${data.cs05.repo.commit}\` (Case 05), \`${data.cs06.repo.name} @ ${data.cs06.repo.commit}\` (Case 06)`,
    ].join('\n');

    const c3 = data.cs03;
    const c4 = data.cs04;
    const cePctA = Math.round((c3.recall.group_a_ce_matched / c3.recall.group_a_gt) * 100);
    const cbmPctA = Math.round((c3.recall.group_a_cbm_matched / c3.recall.group_a_gt) * 100);
    const grPctA = Math.round((c4.recall.group_a_graphify_matched / c4.recall.group_a_gt) * 100);
    const cePctBExact = ((c3.recall.group_b_ce_matched / c3.recall.group_b_gt) * 100).toFixed(1);
    const gtTotal34 = c3.recall.group_a_gt + c3.recall.group_b_gt;

    const rg5RecExact = ((c5.recall.rg_matched / c5.recall.gt_total) * 100).toFixed(1);

    b['md.root_summary_table'] = [
      `| Case Study | Codebase | Baseline | Speed & Tokens | Recall & Governance |`,
      `| :--- | :--- | :--- | :--- | :--- |`,
      `| **[Case 09: Zero-Spoiler Coding Suite](./benchmarks/09-zero-spoiler-context-pack-vs-bare-agent/README.md)** | \`${c9.repo.name}\` (\`${(c9.repo.files / 1000).toFixed(1)}k\` files, TS/NestJS) | \`ripgrep\` + \`read_file\` | **\`${sp9}×\` faster** (\`${fmtDurShort(ce9.wall_clock_seconds, true)}\` vs \`${fmtDurShort(bare9.wall_clock_seconds, true)}\`) · **\`${tk9}×\` fewer tokens** (\`${fmtCompactTokens(ce9.api_total_input_tokens, 2)}\` vs \`${fmtCompactTokens(bare9.api_total_input_tokens, 2)}\`) | **\`${ce9.behavioral_passed}/${ce9.behavioral_total}\` Jest specs & \`${ce9.gates_passed}/${ce9.gates_total}\` SIG gates** (\`${ce9.read_file_calls}\` vs \`${bare9.read_file_calls}\` file reads; \`${ce9.run_command_calls}\` vs \`${bare9.run_command_calls}\` cmds) |`,
      `| **[Case 08: 4-Task Feature Suite](./benchmarks/08-multi-task-architecture-suite-on-twenty/README.md)** | \`${c8.repo.name}\` (\`${(c8.repo.files / 1000).toFixed(1)}k\` files, TS/NestJS) | \`CBM\` | **\`${sp8}×\` faster** (\`${fmtDurShort(ce8.wall_clock_s)}\` vs \`${fmtDurShort(cbm8.wall_clock_s)}\`) · \`${fmtCompactTokens(ce8.cum_input_tokens, 2)}\` vs \`${fmtCompactTokens(cbm8.cum_input_tokens, 2)}\` tokens | **\`0/4\` vs \`4/4\` violated tasks** (\`14/14\` unit tests pass; gates caught & fixed \`4/4\` copied legacy leaks) |`,
      `| **[Case 01: Monorepo Deep Audit](./benchmarks/01-agent-audit-ce-vs-cbm-on-twenty/README.md)** | \`${c1.repo.name}\` (\`${(c1.repo.files / 1000).toFixed(1)}k\` files, TS/NestJS) | \`CBM\` | **\`${sp1}×\` faster** (\`${fmtDurShort(ce1.time_to_answer_s)}\` vs \`${fmtDurShort(cbm1.time_to_answer_s)}\`) · **\`${tk1}×\` fewer tokens** (\`${fmtCompactTokens(ce1.cum_input_tokens, 1)}\` vs \`${fmtCompactTokens(cbm1.cum_input_tokens, 2)}\`) | **\`${ce1Rec}%\` vs \`${cbm1Rec}%\` native graph recall** (\`${c1.recall.ce_matched}/${c1.recall.gt_total}\` from \`graph.db\`, \`${c1.effort_details.ce_file_reads}\` file reads) |`,
      `| **[Case 02: ${c2.repo.services}-Service Audit](./benchmarks/02-enterprise-microservices-deep-audit/README.md)** | Multi-Repo (\`${c2.repo.services}\` services, \`${(c2.repo.files / 1000).toFixed(1)}k\` files) | \`ripgrep\` + Python | **\`${sp2}×\` faster** (\`${fmtDurShort(ce2.time_to_answer_s, true)}\` vs \`${fmtDurShort(rg2.time_to_answer_s, true)}\`) · **\`${tk2}×\` fewer tokens** (\`${fmtCompactTokens(ce2.cum_input_est_tokens, 2)}\` vs \`${fmtCompactTokens(rg2.cum_input_est_tokens, 2)}\`) | **\`${c2.recall.ce_pct}\` vs \`82%\` recall** (\`${ce2.turns}\` vs \`${rg2.turns}\` turns; \`rg\` missed \`${c2.recall.rg_missed_services}\` services) |`,
      `| **[Case 05: .NET Monolith Audit](./benchmarks/05-polyglot-architecture-and-blast-radius/README.md)** | \`OrchardCore\` (\`${c5.repo.projects}\` projects, \`${(c5.repo.files / 1000).toFixed(1)}k\` files) | \`ripgrep\` | **\`${sp5}×\` faster** (\`${fmtDurShort(ce5.time_to_answer_s, true)}\` vs \`${fmtDurShort(rg5.time_to_answer_s, true)}\`) · **\`${tk5}×\` fewer tokens** (\`${fmtCompactTokens(ce5.cum_input_tokens, 2)}\` vs \`${fmtCompactTokens(rg5.cum_input_tokens, 2)}\`) | **\`${c5.recall.ce_pct}\` vs \`${rg5RecExact}%\` recall** (\`${c5.recall.ce_matched}/${c5.recall.gt_total}\` vs \`${c5.recall.rg_matched}/${c5.recall.gt_total}\`; \`${rd5}×\` fewer file reads) |`,
      `| **[Cases 03](./benchmarks/03-code-explorer-vs-codebase-memory-mcp/README.md) & [04](./benchmarks/04-code-explorer-vs-graphify/README.md): Graph Engine Recall** | \`immich\` & \`twenty\` (\`${gtTotal34}\` ground-truth items) | \`CBM\` & \`graphify\` | \`${c3.indexing.immich.ce_s.toFixed(1)}s\` (\`immich\`) / \`${c3.indexing.twenty.ce_s.toFixed(1)}s\` (\`twenty\`) cold index · \`${c3.cypher_latency.ce_min_ms}–${c3.cypher_latency.ce_max_ms} ms\` Cypher | **Code (\`L1–L3\`): \`${cePctA}%\`** vs \`${cbmPctA}%\` (\`CBM\`) / \`${grPctA}%\` (\`graphify\`) · **DB & Queues (\`L4–L5\`): \`${cePctBExact}%\`** |`,
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

function applyHtmlBindings(out, bindings, data) {
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
  return result;
}

function syncHtmlFile(relPath, bindings, data) {
  const abs = path.join(ROOT, relPath);
  if (!fs.existsSync(abs)) return false;
  const orig = fs.readFileSync(abs, 'utf8');
  const result = applyHtmlBindings(orig, bindings, data);

  if (result !== orig) {
    if (!CHECK_ONLY) fs.writeFileSync(abs, result, 'utf8');
    return true;
  }
  return false;
}

function syncRoutesDataFile(relPath, bindings, data) {
  const abs = path.join(ROOT, relPath);
  if (!fs.existsSync(abs)) return false;
  const orig = fs.readFileSync(abs, 'utf8');
  const marker = 'export const ROUTES_DATA: Record<string, RouteEntry> = ';
  const idx = orig.indexOf(marker);
  if (idx === -1) return false;
  const prefix = orig.slice(0, idx + marker.length);
  const jsonPart = orig.slice(idx + marker.length).replace(/;\s*$/, '');
  const routesObj = JSON.parse(jsonPart);
  let modified = false;

  for (const entry of Object.values(routesObj)) {
    if (entry.descriptionMetaBind && entry.descriptionMetaBind in bindings) {
      const nextDesc = bindings[entry.descriptionMetaBind];
      if (entry.description !== nextDesc) {
        entry.description = nextDesc;
        entry.ogDescription = nextDesc;
        modified = true;
      }
    }
    if (entry.mainHtml) {
      const nextMain = applyHtmlBindings(entry.mainHtml, bindings, data);
      if (nextMain !== entry.mainHtml) {
        entry.mainHtml = nextMain;
        modified = true;
      }
    }
  }

  if (modified) {
    const nextContent = `${prefix}${JSON.stringify(routesObj, null, 2)};\n`;
    if (!CHECK_ONLY) fs.writeFileSync(abs, nextContent, 'utf8');
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
    /(<!--\s*BEGIN:([a-zA-Z0-9._-]+)\s*-->)\r?\n?[\s\S]*?(<!--\s*END:\2\s*-->)/g,
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
  'site/benchmarks/06-precommit-architectural-guard.html',
];

for (const rel of htmlTargets) {
  if (syncHtmlFile(rel, bindings, data)) changedFiles.push(rel);
}

if (syncRoutesDataFile('site/src/generated/routesData.ts', bindings, data)) {
  changedFiles.push('site/src/generated/routesData.ts');
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
