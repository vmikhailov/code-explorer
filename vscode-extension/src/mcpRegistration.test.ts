import test from 'node:test';
import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as os from 'node:os';
import * as path from 'node:path';
import {
  MCP_PROVIDER_ID,
  MCP_SERVER_KEY,
  detectHostIdeKind,
  getVsCodeUserMcpPath,
  isHostIdeMcpConfigured,
  mergeMcpServerConfigFile,
  registerNativeVsCodeMcpProvider,
  resolveMcpTargets,
  shouldPromptForMcpRegistration,
  McpConfigTarget,
} from './mcpRegistration';

test('detectHostIdeKind: identifies Cursor, Antigravity, Windsurf, VS Code, and Insiders', () => {
  assert.equal(detectHostIdeKind('Cursor', 'cursor'), 'cursor');
  assert.equal(detectHostIdeKind('Google Antigravity', 'antigravity'), 'antigravity');
  assert.equal(detectHostIdeKind('Windsurf', 'windsurf'), 'windsurf');
  assert.equal(detectHostIdeKind('Visual Studio Code - Insiders', 'vscode-insiders'), 'vscode-insiders');
  assert.equal(detectHostIdeKind('Visual Studio Code', 'vscode'), 'vscode');
  assert.equal(detectHostIdeKind('Custom Editor', 'custom'), 'unknown');
});

test('getVsCodeUserMcpPath: resolves OS-specific user mcp.json paths', () => {
  assert.equal(
    getVsCodeUserMcpPath(false, 'win32', 'C:\\Users\\dev', 'C:\\Users\\dev\\AppData\\Roaming'),
    path.join('C:\\Users\\dev\\AppData\\Roaming', 'Code', 'User', 'mcp.json')
  );
  assert.equal(
    getVsCodeUserMcpPath(true, 'darwin', '/Users/dev'),
    path.join('/Users/dev', 'Library', 'Application Support', 'Code - Insiders', 'User', 'mcp.json')
  );
  assert.equal(
    getVsCodeUserMcpPath(false, 'linux', '/home/dev'),
    path.join('/home/dev', '.config', 'Code', 'User', 'mcp.json')
  );
});

test('resolveMcpTargets: skips VS Code file config when native MCP provider is supported', () => {
  const nativeTargets = resolveMcpTargets({
    hostKind: 'vscode',
    nativeProviderSupported: true,
    homeDir: '/Users/dev',
    platform: 'darwin',
  });
  assert.deepEqual(
    nativeTargets.map((t) => t.id),
    ['claude-code']
  );

  const fallbackTargets = resolveMcpTargets({
    hostKind: 'vscode',
    nativeProviderSupported: false,
    homeDir: '/Users/dev',
    platform: 'darwin',
  });
  assert.deepEqual(
    fallbackTargets.map((t) => t.id),
    ['vscode', 'claude-code']
  );

  const cursorTargets = resolveMcpTargets({
    hostKind: 'cursor',
    nativeProviderSupported: false,
    homeDir: '/Users/dev',
    platform: 'darwin',
  });
  assert.equal(cursorTargets[0].id, 'cursor');
  assert.equal(cursorTargets[0].createIfMissing, true);
});

test('mergeMcpServerConfigFile: adds, preserves reachable commands, self-heals stale paths, and protects invalid JSON', () => {
  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'ce-mcp-reg-test-'));
  try {
    const validBin = path.join(tmpDir, 'ce-bin');
    fs.writeFileSync(validBin, '#!/bin/sh\n', 'utf8');

    // 1. Skips missing file when createIfMissing is false
    const claudeTarget: McpConfigTarget = {
      id: 'claude-code',
      displayName: 'Claude Code',
      filePath: path.join(tmpDir, '.claude.json'),
      sectionKey: 'mcpServers',
      format: 'standard',
      createIfMissing: false,
    };
    const skipped = mergeMcpServerConfigFile(claudeTarget, { command: validBin, args: [] });
    assert.equal(skipped.status, 'skipped-missing');
    assert.equal(fs.existsSync(claudeTarget.filePath), false);

    // 2. Creates config file and adds server when createIfMissing is true
    const cursorTarget: McpConfigTarget = {
      id: 'cursor',
      displayName: 'Cursor',
      filePath: path.join(tmpDir, '.cursor', 'mcp.json'),
      sectionKey: 'mcpServers',
      format: 'standard',
      createIfMissing: true,
    };
    const added = mergeMcpServerConfigFile(cursorTarget, { command: validBin, args: [] });
    assert.equal(added.status, 'added');
    const cursorJson = JSON.parse(fs.readFileSync(cursorTarget.filePath, 'utf8'));
    assert.deepEqual(cursorJson.mcpServers[MCP_SERVER_KEY], {
      command: validBin,
      args: ['mcp', '-q'],
    });

    // 3. Preserves custom user entry when existing command is still reachable
    cursorJson.mcpServers[MCP_SERVER_KEY] = {
      command: validBin,
      args: ['mcp', '--custom-flag'],
    };
    fs.writeFileSync(cursorTarget.filePath, JSON.stringify(cursorJson, null, 2), 'utf8');
    const unchanged = mergeMcpServerConfigFile(cursorTarget, { command: validBin, args: [] });
    assert.equal(unchanged.status, 'unchanged');
    const preservedJson = JSON.parse(fs.readFileSync(cursorTarget.filePath, 'utf8'));
    assert.deepEqual(preservedJson.mcpServers[MCP_SERVER_KEY].args, ['mcp', '--custom-flag']);

    // 4. Self-heals when existing command points to a deleted binary
    cursorJson.mcpServers[MCP_SERVER_KEY] = {
      command: path.join(tmpDir, 'deleted-ce-binary'),
      args: ['mcp'],
    };
    fs.writeFileSync(cursorTarget.filePath, JSON.stringify(cursorJson, null, 2), 'utf8');
    const healed = mergeMcpServerConfigFile(cursorTarget, { command: validBin, args: [] });
    assert.equal(healed.status, 'healed');
    const healedJson = JSON.parse(fs.readFileSync(cursorTarget.filePath, 'utf8'));
    assert.equal(healedJson.mcpServers[MCP_SERVER_KEY].command, validBin);

    // 5. Never overwrites malformed JSON files
    const brokenPath = path.join(tmpDir, 'broken-mcp.json');
    const brokenContent = '{ "mcpServers": { trailing-syntax-error ';
    fs.writeFileSync(brokenPath, brokenContent, 'utf8');
    const brokenOutcome = mergeMcpServerConfigFile(
      { ...cursorTarget, filePath: brokenPath },
      { command: validBin, args: [] }
    );
    assert.equal(brokenOutcome.status, 'skipped-invalid-json');
    assert.equal(fs.readFileSync(brokenPath, 'utf8'), brokenContent);
  } finally {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  }
});

test('registerNativeVsCodeMcpProvider: registers provider and resolves managed binary lazily', async () => {
  let registeredId = '';
  let registeredProvider: any = null;

  class MockEventEmitter {
    public event = () => ({ dispose: () => {} });
    public fire() {}
    public dispose() {}
  }

  class MockStdioDef {
    public cwd?: any;
    constructor(
      public label: string,
      public command: string,
      public args: string[],
      public env: Record<string, string>,
      public version: string
    ) {}
  }

  const mockVsCode = {
    EventEmitter: MockEventEmitter,
    McpStdioServerDefinition: MockStdioDef,
    Uri: { file: (fsPath: string) => ({ fsPath }) },
    lm: {
      registerMcpServerDefinitionProvider: (id: string, provider: any) => {
        registeredId = id;
        registeredProvider = provider;
        return { dispose: () => {} };
      },
    },
  };

  const logs: string[] = [];
  let userApproved = false;
  const res = registerNativeVsCodeMcpProvider(
    {
      resolveInstalledExecutableSync: () => null,
      resolveExecutable: async () => ({ command: '/managed/bin/ce', args: [] }),
    },
    () => '/workspace/project',
    '1.81.0',
    { appendLine: (m: string) => logs.push(m) },
    mockVsCode,
    () => userApproved
  );

  assert.equal(res.supported, true);
  assert.equal(registeredId, MCP_PROVIDER_ID);

  // Before user consent, zero MCP servers are exposed to the IDE
  const beforeConsent = await registeredProvider.provideMcpServerDefinitions();
  assert.equal(beforeConsent.length, 0);

  // Once user clicks "Register MCP", definition is exposed and resolved lazily
  userApproved = true;
  const defs = await registeredProvider.provideMcpServerDefinitions();
  assert.equal(defs.length, 1);
  assert.equal(defs[0].command, 'ce');
  assert.deepEqual(defs[0].args, ['mcp', '-q']);

  const resolvedDef = await registeredProvider.resolveMcpServerDefinition(defs[0]);
  assert.equal(resolvedDef.command, '/managed/bin/ce');
  assert.deepEqual(resolvedDef.args, ['mcp', '-q']);
  assert.equal(resolvedDef.env.WORKSPACE_ROOT, '/workspace/project');
  assert.equal(resolvedDef.cwd.fsPath, '/workspace/project');
});

test('shouldPromptForMcpRegistration & isHostIdeMcpConfigured: respects user consent and existing configs', () => {
  assert.equal(
    shouldPromptForMcpRegistration({
      promptSettingEnabled: true,
      consent: undefined,
      hostAlreadyConfigured: false,
    }),
    true
  );
  assert.equal(
    shouldPromptForMcpRegistration({
      promptSettingEnabled: true,
      consent: 'dismissed',
      hostAlreadyConfigured: false,
    }),
    false
  );
  assert.equal(
    shouldPromptForMcpRegistration({
      promptSettingEnabled: false,
      consent: undefined,
      hostAlreadyConfigured: false,
    }),
    false
  );
  assert.equal(
    shouldPromptForMcpRegistration({
      promptSettingEnabled: true,
      consent: undefined,
      hostAlreadyConfigured: true,
    }),
    false
  );

  const tmpHome = fs.mkdtempSync(path.join(os.tmpdir(), 'ce-mcp-consent-'));
  try {
    const validBin = path.join(tmpHome, 'ce');
    fs.writeFileSync(validBin, '#!/bin/sh\n', 'utf8');
    assert.equal(isHostIdeMcpConfigured({ hostKind: 'cursor', homeDir: tmpHome }), false);

    const cursorCfg = path.join(tmpHome, '.cursor', 'mcp.json');
    fs.mkdirSync(path.dirname(cursorCfg), { recursive: true });
    fs.writeFileSync(
      cursorCfg,
      JSON.stringify({ mcpServers: { [MCP_SERVER_KEY]: { command: validBin, args: ['mcp', '-q'] } } }),
      'utf8'
    );
    assert.equal(isHostIdeMcpConfigured({ hostKind: 'cursor', homeDir: tmpHome }), true);
  } finally {
    fs.rmSync(tmpHome, { recursive: true, force: true });
  }
});
