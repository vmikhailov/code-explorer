import assert from 'node:assert/strict';
import { execSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import esbuild from 'file:///C:/Work/Personal/code-explorer/vscode-extension/node_modules/esbuild/lib/main.js';

const repoRoot = 'C:/Work/bench-pilot/repos/twenty';
const repoPkg = 'C:/Work/bench-pilot/repos/twenty/packages/twenty-server';
const resultsRoot = 'C:/Work/Personal/code-explorer/benchmarks/08-multi-task-architecture-suite-on-twenty/results';

const realFilePatterns = [
  'two-factor-authentication.resolver',
  'two-factor-authentication.service',
  'two-factor-authentication.exception',
  'timeline-messaging.resolver',
  'accounts-to-reconnect.service',
  'accounts-to-reconnect-key-value.type',
  'admin-panel-billing.service',
  'billing.exception',
  'billing-credit-grant-type.enum',
  'to-display-credits.util',
  'custom-exception',
  'get-billing-exception-status-code.util',
  'subdomain-manager.service',
  'get-workspace-scoped-repository-token.util',
  'inject-workspace-scoped-repository.decorator',
  'convert-class-name-to-object-metadata-name.util',
  'get-queue-token.util',
  'message-queue.decorator',
  'message-queue.constants',
  'cache-storage-namespace.enum',
  'access-policy-guard-config-key.constant',
  'access-policy.guard',
  'require-access-policy.decorator',
];

function injectConstructorTokens(tsCode, filePath) {
  if (filePath.includes('exception')) return tsCode;
  return tsCode.replace(/constructor\s*\(([\s\S]*?)\)\s*\{/g, (_full, paramsStr) => {
    const params = [];
    let depth = 0;
    let cur = '';
    for (const ch of paramsStr) {
      if (ch === '<' || ch === '(' || ch === '{' || ch === '[') depth++;
      else if (ch === '>' || ch === ')' || ch === '}' || ch === ']') depth--;
      if (ch === ',' && depth === 0) {
        params.push(cur);
        cur = '';
      } else {
        cur += ch;
      }
    }
    if (cur.trim()) params.push(cur);

    const newParams = params.map((p) => {
      if (!p.trim()) return p;
      if (/@Inject/.test(p)) return p;
      const m = p.match(/:\s*([A-Za-z0-9_]+)\s*(?:<[^>]*>)?\s*$/);
      if (m) {
        return `@__InjectType(${m[1]}) ${p}`;
      }
      return p;
    });
    return `constructor(${newParams.join(',')}) {\n`;
  }) + `\nfunction __InjectType(t: any) { return (target: any, _k: any, idx: number) => { target.__paramTokens = target.__paramTokens || []; target.__paramTokens[idx] = t; }; }\n`;
}

async function runSpecFile(specFile) {
  const stubPlugin = {
    name: 'stub-external',
    setup(build) {
      build.onResolve({ filter: /.*/ }, (args) => {
        if (args.kind === 'entry-point') return null;
        if (args.path === '@nestjs/testing') return { path: '@nestjs/testing', namespace: 'nestjs-testing' };
        if (args.path === '@nestjs/typeorm') return { path: '@nestjs/typeorm', namespace: 'nestjs-typeorm' };
        if (args.path === '@nestjs/common' || args.path === '@nestjs/common/constants') return { path: args.path, namespace: 'nestjs-common' };
        if (args.path === 'twenty-shared/utils') return { path: 'twenty-shared/utils', namespace: 'twenty-utils' };
        if (args.path === 'twenty-shared/constants') return { path: 'twenty-shared/constants', namespace: 'twenty-constants' };

        if (args.path.startsWith('src/') && realFilePatterns.some((f) => args.path.includes(f))) {
          const base = path.join(repoPkg, args.path);
          for (const ext of ['.ts', '/index.ts', '']) {
            if (fs.existsSync(base + ext) && fs.statSync(base + ext).isFile()) {
              return { path: base + ext };
            }
          }
        }
        if (args.path.startsWith('.')) {
          const base = path.resolve(args.resolveDir, args.path);
          for (const ext of ['.ts', '/index.ts', '']) {
            if (fs.existsSync(base + ext) && fs.statSync(base + ext).isFile() && realFilePatterns.some((f) => (base + ext).includes(f))) {
              return { path: base + ext };
            }
          }
        }
        return { path: args.path, namespace: 'stub' };
      });

      build.onLoad({ filter: /\.ts$/ }, (args) => {
        let code = fs.readFileSync(args.path, 'utf8');
        if (!args.path.endsWith('.spec.ts')) {
          code = injectConstructorTokens(code, args.path);
        }
        return { contents: code, loader: 'ts' };
      });

      build.onLoad({ filter: /.*/, namespace: 'nestjs-testing' }, () => ({
        contents: `
          export const Test = {
            createTestingModule: ({ providers = [] }) => ({
              compile: async () => {
                const map = new Map();
                const classes = [];
                for (const p of providers) {
                  if (typeof p === 'function') {
                    classes.push(p);
                  } else if (p && typeof p === 'object' && 'provide' in p) {
                    if ('useValue' in p) map.set(p.provide, p.useValue);
                    else if ('useClass' in p) classes.push(p.useClass);
                  }
                }
                const instantiate = (Cls) => {
                  if (map.has(Cls)) return map.get(Cls);
                  const tokens = Cls.__paramTokens || [];
                  const args = [];
                  const len = Math.max(Cls.length, tokens.length);
                  for (let i = 0; i < len; i++) {
                    const tok = tokens[i];
                    if (tok && map.has(tok)) {
                      args.push(map.get(tok));
                    } else if (typeof tok === 'function' && classes.includes(tok)) {
                      args.push(instantiate(tok));
                    } else {
                      args.push(undefined);
                    }
                  }
                  const inst = new Cls(...args);
                  map.set(Cls, inst);
                  return inst;
                };
                for (const Cls of classes) instantiate(Cls);
                return {
                  get: (tok) => map.get(tok),
                };
              }
            })
          };
        `,
        loader: 'js',
      }));

      build.onLoad({ filter: /.*/, namespace: 'nestjs-typeorm' }, () => ({
        contents: `
          export const getRepositoryToken = (entity) => 'REPO_' + (entity?.name || String(entity));
          export const InjectRepository = (entity) => (target, _k, idx) => {
            target.__paramTokens = target.__paramTokens || [];
            target.__paramTokens[idx] = 'REPO_' + (entity?.name || String(entity));
          };
        `,
        loader: 'js',
      }));

      build.onLoad({ filter: /.*/, namespace: 'nestjs-common' }, () => ({
        contents: `
          export const GUARDS_METADATA = '__guards__';
          export const SetMetadata = (k, v) => (target, key, desc) => {
            const t = desc ? desc.value : target;
            if (t) {
              globalThis.__metadataMap = globalThis.__metadataMap || new WeakMap();
              let m = globalThis.__metadataMap.get(t);
              if (!m) { m = new Map(); globalThis.__metadataMap.set(t, m); }
              m.set(k, v);
            }
          };
          export const UseGuards = (...guards) => SetMetadata(GUARDS_METADATA, guards);
          export const UsePipes = () => () => {};
          export const UseFilters = () => () => {};
          export const Injectable = () => () => {};
          export const mixin = (cls) => cls;
          export const Inject = (tok) => (target, _k, idx) => {
            target.__paramTokens = target.__paramTokens || [];
            target.__paramTokens[idx] = tok;
          };
          export const Logger = class { log() {} error() {} warn() {} };
        `,
        loader: 'js',
      }));

      build.onLoad({ filter: /.*/, namespace: 'twenty-utils' }, () => ({
        contents: `
          export const isDefined = (v) => v !== undefined && v !== null;
          export const assertIsDefinedOrThrow = (v, err) => {
            if (v === undefined || v === null) {
              throw err || new Error('Value is not defined');
            }
          };
          export const capitalize = (s) => (s ? s[0].toUpperCase() + s.slice(1) : '');
          export const getSubdomainSlugFromDisplayName = (s) => (s || '').toLowerCase().replace(/[^a-z0-9]/g, '');
          export const appendCommonExceptionCode = (e) => ({ ...e, INTERNAL_SERVER_ERROR: 'INTERNAL_SERVER_ERROR' });
          export const assertUnreachable = (x) => { throw new Error('Unreachable: ' + x); };
          export class CustomError extends Error { constructor(msg, code) { super(msg); this.code = code; this.name = new.target.name; } }
        `,
        loader: 'js',
      }));

      build.onLoad({ filter: /.*/, namespace: 'twenty-constants' }, () => ({
        contents: `
          export const INTERNAL_CREDITS_PER_DISPLAY_CREDIT = 1000;
          export const PermissionFlagType = { WORKSPACE_MEMBERS: 'WORKSPACE_MEMBERS', SECURITY: 'SECURITY' };
        `,
        loader: 'js',
      }));

      build.onLoad({ filter: /.*/, namespace: 'stub' }, (args) => {
        const baseName = path.basename(args.path).replace(/\.[a-z]+$/, '');
        return {
          contents: `
            globalThis.__stubCache = globalThis.__stubCache || new Map();
            function makeStub(name) {
              if (globalThis.__stubCache.has(name)) return globalThis.__stubCache.get(name);
              const fn = function() {};
              Object.defineProperty(fn, 'name', { value: name || 'Stub' });
              const p = new Proxy(fn, {
                get: (t, prop) => {
                  if (prop === '__esModule') return false;
                  if (prop === 'name') return name || 'Stub';
                  if (prop === 'length') return 0;
                  if (prop === 'IsNull') return () => 'IS_NULL';
                  if (prop === 'BillingPlanKey') return { PRO: 'PRO', ENTERPRISE: 'ENTERPRISE' };
                  if (prop === 'AppTokenType') return { RefreshToken: 'RefreshToken' };
                  if (prop === 'MessageQueue') return { deleteCascadeQueue: 'delete-cascade-queue' };
                  if (prop === 'EmailingDomainWorkspaceCleanupJob') return { name: 'EmailingDomainWorkspaceCleanupJob' };
                  if (typeof prop === 'string') return makeStub(prop);
                  return undefined;
                },
                apply: (_t, _this, callArgs) => {
                  return (target, _k, idx) => {
                    if (typeof idx === 'number' && target) {
                      target.__paramTokens = target.__paramTokens || [];
                      if (name === 'InjectWorkspaceScopedRepository' && callArgs[0]) {
                        target.__paramTokens[idx] = (callArgs[0]?.name || String(callArgs[0])) + 'WorkspaceScopedRepository';
                      } else if (name === 'InjectCacheStorage') {
                        target.__paramTokens[idx] = 'CacheStorageNamespace:' + String(callArgs[0] || '');
                      } else if (name === 'InjectMessageQueue') {
                        target.__paramTokens[idx] = 'MessageQueue:' + String(callArgs[0] || '');
                      }
                    }
                  };
                },
                construct: (_t, cArgs) => ({ message: cArgs[0], code: cArgs[1] }),
              });
              globalThis.__stubCache.set(name, p);
              return p;
            }
            const root = makeStub(${JSON.stringify(baseName)});
            const exported = Object.create(root);
            module.exports = exported;
          `,
          loader: 'js',
        };
      });
    },
  };

  const result = await esbuild.build({
    entryPoints: [specFile],
    bundle: true,
    write: false,
    treeShaking: false,
    format: 'cjs',
    platform: 'node',
    plugins: [stubPlugin],
    tsconfigRaw: JSON.stringify({ compilerOptions: { experimentalDecorators: true } }),
  });

  if (typeof Reflect.getMetadata !== 'function') {
    Reflect.getMetadata = (k, target) => {
      const m = globalThis.__metadataMap?.get(target);
      return m ? m.get(k) : undefined;
    };
  }

  const suites = [{ beforeEach: [], afterEach: [], tests: [] }];
  globalThis.describe = (_name, fn) => {
    const parent = suites[suites.length - 1];
    const suite = { beforeEach: [...parent.beforeEach], afterEach: [...parent.afterEach], tests: [] };
    suites.push(suite);
    fn();
    suites.pop();
    parent.tests.push(...suite.tests);
  };
  globalThis.beforeEach = (fn) => suites[suites.length - 1].beforeEach.push(fn);
  globalThis.afterEach = (fn) => suites[suites.length - 1].afterEach.push(fn);
  globalThis.it = (name, fn) => {
    const cur = suites[suites.length - 1];
    cur.tests.push({ name, fn, beforeEach: [...cur.beforeEach], afterEach: [...cur.afterEach] });
  };

  const allMocks = [];
  globalThis.jest = {
    fn: (impl) => {
      const calls = [];
      let currentImpl = impl;
      let onceQueue = [];
      const mockFn = (...args) => {
        calls.push(args);
        if (onceQueue.length > 0) return onceQueue.shift()(...args);
        return currentImpl ? currentImpl(...args) : undefined;
      };
      mockFn.mock = { calls };
      mockFn.mockImplementation = (fn) => { currentImpl = fn; return mockFn; };
      mockFn.mockReturnValue = (val) => { currentImpl = () => val; return mockFn; };
      mockFn.mockReturnValueOnce = (val) => { onceQueue.push(() => val); return mockFn; };
      mockFn.mockResolvedValue = (val) => { currentImpl = () => Promise.resolve(val); return mockFn; };
      mockFn.mockResolvedValueOnce = (val) => { onceQueue.push(() => Promise.resolve(val)); return mockFn; };
      mockFn.mockRejectedValue = (err) => { currentImpl = () => Promise.reject(err); return mockFn; };
      mockFn.mockRejectedValueOnce = (err) => { onceQueue.push(() => Promise.reject(err)); return mockFn; };
      mockFn.mockClear = () => { calls.length = 0; };
      allMocks.push(mockFn);
      return mockFn;
    },
    clearAllMocks: () => {
      for (const m of allMocks) m.mockClear();
    },
    restoreAllMocks: () => {
      for (const m of allMocks) m.mockClear();
    },
    spyOn: (obj, prop) => {
      const orig = obj[prop];
      const m = globalThis.jest.fn(orig ? orig.bind(obj) : undefined);
      obj[prop] = m;
      return m;
    },
  };

  function deepMatch(a, b) {
    if (b && typeof b === 'object' && b.__anyType) {
      return a instanceof b.__anyType || a?.constructor === b.__anyType || typeof a === b.__anyType.name?.toLowerCase();
    }
    if (Object.is(a, b)) return true;
    if (a && b && typeof a === 'object' && typeof b === 'object') {
      if (Array.isArray(a) !== Array.isArray(b)) return false;
      const keys = Object.keys(b);
      if (Array.isArray(b) && a.length !== b.length) return false;
      return keys.every((k) => deepMatch(a[k], b[k]));
    }
    return false;
  }

  function makeExpect(actual, isNot = false) {
    const check = (cond, msg) => {
      assert.ok(isNot ? !cond : cond, msg);
    };
    return {
      toBe: (expected) => check(Object.is(actual, expected), `Expected ${actual} ${isNot ? 'not ' : ''}toBe ${expected}`),
      toEqual: (expected) => {
        check(deepMatch(actual, expected), `Expected ${JSON.stringify(actual)} toEqual ${JSON.stringify(expected)}`);
      },
      toHaveLength: (n) => check(actual?.length === n, `Expected length ${n}, got ${actual?.length}`),
      toBeDefined: () => check(actual !== undefined, `Expected value to be defined`),
      toContain: (item) => check(Array.isArray(actual) ? actual.includes(item) : String(actual).includes(String(item)), `Expected ${actual} toContain ${item}`),
      toHaveBeenCalled: () => check(actual.mock.calls.length > 0, `Expected mock ${isNot ? 'not ' : ''}to have been called`),
      toHaveBeenCalledTimes: (n) => check(actual.mock.calls.length === n, `Expected ${n} calls, got ${actual.mock.calls.length}`),
      toHaveBeenCalledWith: (...expected) => {
        const match = actual.mock.calls.some((call) => deepMatch(call, expected));
        check(match, `Expected call with ${JSON.stringify(expected)}, got ${JSON.stringify(actual.mock.calls)}`);
      },
      get not() {
        return makeExpect(actual, !isNot);
      },
      resolves: {
        toBe: async (expected) => {
          const val = await actual;
          makeExpect(val, isNot).toBe(expected);
        },
        toEqual: async (expected) => {
          const val = await actual;
          makeExpect(val, isNot).toEqual(expected);
        },
      },
      rejects: {
        toThrow: async (ErrClass) => {
          let threw = false;
          try {
            await actual;
          } catch (e) {
            threw = true;
            if (ErrClass && typeof ErrClass === 'function') {
              assert.ok(e instanceof ErrClass || e?.constructor?.name === ErrClass.name || e?.name === ErrClass.name, `Unexpected error type: ${e}`);
            } else if (typeof ErrClass === 'string') {
              assert.ok(String(e?.message || e).includes(ErrClass), `Unexpected error message: ${e?.message}`);
            }
          }
          assert.ok(threw, 'Expected promise to reject');
        },
        toMatchObject: async (sub) => {
          let threw = false;
          try {
            await actual;
          } catch (e) {
            threw = true;
            for (const [k, v] of Object.entries(sub)) assert.deepStrictEqual(e[k], v);
          }
          assert.ok(threw, 'Expected promise to reject');
        },
      },
    };
  }
  globalThis.expect = Object.assign((actual) => makeExpect(actual, false), {
    any: (cls) => ({ __anyType: cls }),
  });

  const fn = new Function('require', 'module', 'exports', result.outputFiles[0].text);
  const mod = { exports: {} };
  fn(() => ({}), mod, mod.exports);

  const rootTests = suites[0].tests;
  let passed = 0;
  const failures = [];
  for (const t of rootTests) {
    let unhandledErr = null;
    const onUnhandled = (err) => { unhandledErr = err; };
    process.on('unhandledRejection', onUnhandled);
    try {
      for (const b of t.beforeEach) await b();
      await t.fn();
      await new Promise((r) => setTimeout(r, 10));
      if (unhandledErr) throw unhandledErr;
      for (const a of t.afterEach) await a();
      passed++;
    } catch (err) {
      failures.push(`${t.name}: ${err.message}`);
    } finally {
      process.off('unhandledRejection', onUnhandled);
    }
  }
  return { passed, total: rootTests.length, failures };
}

const matrix = [
  { task: 'task-01', cond: 'cbm', patch: 'cbm-agent.patch', agentSpec: 'cbm-two-factor-authentication.resolver.spec.ts', sharedSpec: 'ce-two-factor-authentication.resolver.spec.ts' },
  { task: 'task-01', cond: 'ce', patch: 'ce-agent.patch', agentSpec: 'ce-two-factor-authentication.resolver.spec.ts', sharedSpec: 'ce-two-factor-authentication.resolver.spec.ts' },
  { task: 'task-02', cond: 'cbm', patch: 'cbm-agent.patch', agentSpec: 'cbm-timeline-messaging.resolver.spec.ts', sharedSpec: 'cbm-timeline-messaging.resolver.spec.ts' },
  { task: 'task-02', cond: 'ce', patch: 'ce-agent.patch', agentSpec: 'ce-timeline-messaging.resolver.spec.ts', sharedSpec: 'cbm-timeline-messaging.resolver.spec.ts' },
  { task: 'task-03', cond: 'cbm', patch: 'cbm-agent.patch', agentSpec: 'cbm-admin-panel-billing.service.spec.ts', sharedSpec: 'ce-admin-panel-billing.service.spec.ts' },
  { task: 'task-03', cond: 'ce', patch: 'ce-agent.patch', agentSpec: 'ce-admin-panel-billing.service.spec.ts', sharedSpec: 'ce-admin-panel-billing.service.spec.ts' },
  { task: 'task-04', cond: 'cbm', patch: 'cbm-agent.patch', agentSpec: 'cbm-subdomain-manager.service.spec.ts', sharedSpec: 'ce-subdomain-manager.service.spec.ts' },
  { task: 'task-04', cond: 'ce', patch: 'ce-agent.patch', agentSpec: 'ce-subdomain-manager.service.spec.ts', sharedSpec: 'ce-subdomain-manager.service.spec.ts' },
];

for (const item of matrix) {
  execSync('git checkout -- . && git clean -fd -e .codeexplorer', { cwd: repoRoot, stdio: 'ignore' });
  const patchPath = path.join(resultsRoot, item.task, item.patch);
  execSync(`git apply "${patchPath}"`, { cwd: repoRoot });
  const agentSpecPath = path.join(resultsRoot, item.task, item.agentSpec);
  const res = await runSpecFile(agentSpecPath);
  console.log(`[${res.passed === res.total ? 'PASS' : 'FAIL'}] ${item.task} / ${item.cond}: ${res.passed}/${res.total} unit tests passed`);
}
execSync('git checkout -- . && git clean -fd -e .codeexplorer', { cwd: repoRoot, stdio: 'ignore' });
