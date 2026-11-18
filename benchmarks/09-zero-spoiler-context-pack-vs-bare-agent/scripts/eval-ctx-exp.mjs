#!/usr/bin/env node
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const TWENTY_DIR = 'C:/Work/bench-pilot/repos/twenty-ctx-exp';
const TWENTY_SERVER_DIR = path.join(TWENTY_DIR, 'packages', 'twenty-server');
const TEMPLATE_SPEC = 'C:/Users/viach/.gemini/antigravity/brain/8e28a5de-3371-4584-9b0e-19e7c8977c46/scratch/ctx-exp/acceptance-spec.template.ts';
const MOUNTED_SPEC_REL = 'src/engine/core-modules/__ctx_exp_bench__.spec.ts';
const MOUNTED_SPEC_ABS = path.join(TWENTY_SERVER_DIR, MOUNTED_SPEC_REL);
const CE_DLL = 'c:/Work/Personal/code-explorer/.worktrees/context-pack/cli/.Build/bin_Release_AnyCPU/CodeExplorer/ce.dll';
const SERVER_URL = 'http://127.0.0.1:19580';
const APPDATA_BRAIN_DIR = path.join(os.homedir(), '.gemini', 'antigravity', 'brain');
const RESULTS_DIR = 'C:/Users/viach/.gemini/antigravity/brain/8e28a5de-3371-4584-9b0e-19e7c8977c46/scratch/ctx-exp/results-round2';

function runCmd(command, args, cwd) {
  const res = spawnSync(command, args, {
    cwd,
    encoding: 'utf8',
    shell: false,
    maxBuffer: 32 * 1024 * 1024,
  });
  return {
    status: res.status ?? 1,
    stdout: res.stdout ?? '',
    stderr: res.stderr ?? '',
  };
}

function extractTelemetry(conversationId) {
  if (!conversationId) return null;
  const fullPath = path.join(APPDATA_BRAIN_DIR, conversationId, '.system_generated', 'logs', 'transcript_full.jsonl');
  const compactPath = path.join(APPDATA_BRAIN_DIR, conversationId, '.system_generated', 'logs', 'transcript.jsonl');
  const logPath = fs.existsSync(fullPath) ? fullPath : compactPath;
  if (!fs.existsSync(logPath)) return null;

  const steps = fs.readFileSync(logPath, 'utf8').split(/\r?\n/).filter(Boolean).map((l) => JSON.parse(l));
  let firstTs = null;
  let lastTs = null;
  let llmTurns = 0;
  let totalToolCalls = 0;
  let uncachedIn = 0;
  let cachedIn = 0;
  let outTokens = 0;
  const toolBreakdown = {};
  const purityIssues = [];

  for (const s of steps) {
    if (s.created_at) {
      const t = Date.parse(s.created_at);
      if (!Number.isNaN(t)) {
        if (firstTs === null || t < firstTs) firstTs = t;
        if (lastTs === null || t > lastTs) lastTs = t;
      }
    }
    if (s.type === 'PLANNER_RESPONSE') {
      llmTurns += 1;
      uncachedIn += Number(s.input_tokens ?? 0);
      cachedIn += Number(s.cache_read_tokens ?? 0);
      outTokens += Number(s.output_tokens ?? 0);
      for (const tc of s.tool_calls ?? []) {
        totalToolCalls += 1;
        const name = tc.name ?? 'unknown';
        toolBreakdown[name] = (toolBreakdown[name] ?? 0) + 1;
        if (name === 'read_file' || name === 'edit_file' || name === 'write_file') {
          const p = String(tc.args?.Path ?? '').replace(/\\/g, '/').toLowerCase();
          if (!p.startsWith('c:/work/bench-pilot/repos/twenty-ctx-exp/') || p.includes('/.codeexplorer/')) {
            purityIssues.push(`Step ${s.step_index}: unauthorized ${name} '${tc.args?.Path}'`);
          }
        }
      }
    }
  }

  return {
    conversation_id: conversationId,
    wall_clock_seconds: firstTs !== null && lastTs !== null ? Number(((lastTs - firstTs) / 1000).toFixed(1)) : 0,
    llm_turns: llmTurns,
    total_tool_calls: totalToolCalls,
    tool_breakdown: toolBreakdown,
    api_uncached_input_tokens: uncachedIn,
    api_cached_input_tokens: cachedIn,
    api_total_input_tokens: uncachedIn + cachedIn,
    api_output_tokens: outTokens,
    purity_passed: purityIssues.length === 0,
    purity_issues: purityIssues,
  };
}

async function main() {
  const argv = process.argv.slice(2);
  let featureNum = 1;
  let saveLabel = null;
  let convId = null;
  let resetAfter = false;

  for (let i = 0; i < argv.length; i += 1) {
    if (argv[i] === '--feature') featureNum = Number(argv[++i]);
    else if (argv[i] === '--save') saveLabel = argv[++i];
    else if (argv[i] === '--conv') convId = argv[++i];
    else if (argv[i] === '--reset') resetAfter = true;
  }

  runCmd('git', ['add', '-N', '.'], TWENTY_DIR);
  const gitDiff = runCmd('git', ['diff'], TWENTY_DIR).stdout;
  const tmpJson = path.join(os.tmpdir(), `ctx-exp-jest-${Date.now()}.json`);

  const tsgoBin = path.join(TWENTY_DIR, 'node_modules', '@typescript', 'native-preview-win32-x64', 'lib', 'tsgo.exe');
  const tscRes = runCmd(tsgoBin, ['-p', 'tsconfig.json', '--declaration', 'false', '--noEmit'], TWENTY_SERVER_DIR);

  let jestReport = null;
  try {
    fs.copyFileSync(TEMPLATE_SPEC, MOUNTED_SPEC_ABS);
    const jestArgs = [
      '--max-old-space-size=6144',
      path.join(TWENTY_DIR, 'node_modules', 'jest', 'bin', 'jest.js'),
      '--runTestsByPath',
      MOUNTED_SPEC_REL,
      '--runInBand',
      '-t',
      `Feature ${featureNum}:`,
      '--json',
      `--outputFile=${tmpJson}`,
    ];
    runCmd(process.execPath, jestArgs, TWENTY_SERVER_DIR);
    if (fs.existsSync(tmpJson)) {
      jestReport = JSON.parse(fs.readFileSync(tmpJson, 'utf8'));
    }
  } finally {
    if (fs.existsSync(MOUNTED_SPEC_ABS)) fs.unlinkSync(MOUNTED_SPEC_ABS);
    if (fs.existsSync(tmpJson)) fs.unlinkSync(tmpJson);
  }

  // Wait briefly for FileWatcher debounce, then check gates via warm server
  await new Promise((r) => setTimeout(r, 800));
  const gateText = await fetch(`${SERVER_URL}/api/gates/check`).then((r) => r.text());

  const allAssertions = jestReport?.testResults?.flatMap((tr) => tr.assertionResults ?? []) ?? [];
  const items = allAssertions.filter((a) => a.title.startsWith(`${featureNum}.`) && a.status !== 'pending');
  const passed = items.filter((a) => a.status === 'passed').length;
  const failures = items.filter((a) => a.status !== 'passed').map((a) => ({
    title: a.title,
    msg: (a.failureMessages?.[0] ?? '').split('\n').slice(0, 6).join('\n'),
  }));

  const newViolMatch = gateText.match(/newViolations:\s*(\d+)/);
  const failedGatesMatch = gateText.match(/failed:\s*(\d+)/);
  const newViolations = newViolMatch ? Number(newViolMatch[1]) : -1;
  const failedGates = failedGatesMatch ? Number(failedGatesMatch[1]) : -1;

  const telemetry = extractTelemetry(convId);
  const summary = {
    label: saveLabel,
    feature: featureNum,
    compile_passed: tscRes.status === 0,
    compile_errors: tscRes.status === 0 ? '' : (tscRes.stdout + tscRes.stderr).slice(0, 800),
    behavioral_passed: passed,
    behavioral_total: items.length || 5,
    behavioral_failures: failures,
    gates_passed: 7 - Math.max(0, failedGates),
    gates_failed: failedGates,
    new_sig_violations: newViolations,
    telemetry,
  };

  if (saveLabel) {
    const outDir = path.join(RESULTS_DIR, saveLabel);
    fs.mkdirSync(outDir, { recursive: true });
    fs.writeFileSync(path.join(outDir, 'scorecard.json'), JSON.stringify(summary, null, 2));
    fs.writeFileSync(path.join(outDir, 'git.diff'), gitDiff);
    fs.writeFileSync(path.join(outDir, 'gates.yaml'), gateText);
  }

  console.log(JSON.stringify(summary, null, 2));

  if (resetAfter) {
    runCmd('git', ['reset', '--hard', 'HEAD'], TWENTY_DIR);
    runCmd('git', ['clean', '-fd', '-e', '.codeexplorer', '-e', 'node_modules', '-e', 'packages/*/node_modules', '-e', 'packages/*/dist'], TWENTY_DIR);
    await new Promise((r) => setTimeout(r, 800));
    await fetch(`${SERVER_URL}/api/gates/check`).then((r) => r.text());
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
