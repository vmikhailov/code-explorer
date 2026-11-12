import type * as vscodeTypes from 'vscode';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import type { ProcessManager } from './processManager';
import { findInPath } from './binaryManager';

export const MCP_PROVIDER_ID = 'codeExplorer.mcpProvider';
export const MCP_SERVER_KEY = 'codeexplorer';
export const MCP_SERVER_LABEL = 'CodeExplorer';
export const CMD_REGISTER_MCP = 'codeExplorer.registerMcp';
export const STATE_MCP_CONSENT = 'codeExplorer.mcpRegistrationConsent';
const CONFIG_NAMESPACE = 'codeExplorer';
const SETTING_MCP_PROMPT_TO_REGISTER = 'mcp.promptToRegister';

const SECTION_MCP_SERVERS = 'mcpServers';
const SECTION_VSCODE_SERVERS = 'servers';
const ARG_MCP = 'mcp';
const ARG_QUIET = '-q';
const ENV_WORKSPACE_ROOT = 'WORKSPACE_ROOT';
const VSCODE_MODULE_ID = 'vscode';
const UTF8_ENCODING = 'utf8';
const NATIVE_VSCODE_ID = 'vscode-native';
const NATIVE_VSCODE_DISPLAY_NAME = 'VS Code (Native MCP)';
const NATIVE_VSCODE_DETAIL = 'Built-in VS Code MCP provider API (GitHub Copilot Agent Mode)';
const ACTION_REGISTER_MCP = 'Register MCP';
const ACTION_LATER = 'Later';
const ACTION_DONT_ASK_AGAIN = "Don't Ask Again";
const CONSENT_APPROVED = 'approved';
const CONSENT_DISMISSED = 'dismissed';
const PROMPT_REGISTER_MCP =
  'CodeExplorer: Register the MCP server so AI coding agents can query the architecture graph?';
const QUICK_PICK_PLACEHOLDER =
  'Select AI coding agents / IDEs where CodeExplorer MCP should be registered';
const JSON_INDENT = 2;

export type McpConsentState = 'approved' | 'dismissed' | undefined;

export type HostIdeKind =
  | 'cursor'
  | 'antigravity'
  | 'windsurf'
  | 'vscode-insiders'
  | 'vscode'
  | 'unknown';

export type McpMergeStatus =
  | 'added'
  | 'updated'
  | 'healed'
  | 'unchanged'
  | 'skipped-missing'
  | 'skipped-invalid-json'
  | 'error';

export interface McpExecutableSpec {
  command: string;
  args: string[];
}

export interface McpConfigTarget {
  id: string;
  displayName: string;
  filePath: string;
  sectionKey: string;
  format: 'standard' | 'vscode';
  createIfMissing: boolean;
}

export interface McpMergeOutcome {
  target: McpConfigTarget;
  status: McpMergeStatus;
  details?: string;
}

function getVsCode(): typeof vscodeTypes | undefined {
  try {
    return require(VSCODE_MODULE_ID);
  } catch (err) {
    return err ? undefined : undefined;
  }
}

export function detectHostIdeKind(appName = '', uriScheme = ''): HostIdeKind {
  const haystack = `${appName} ${uriScheme}`.toLowerCase();
  if (haystack.includes('cursor')) return 'cursor';
  if (haystack.includes('antigravity')) return 'antigravity';
  if (haystack.includes('windsurf')) return 'windsurf';
  if (haystack.includes('insiders')) return 'vscode-insiders';
  if (haystack.includes('visual studio code') || haystack.includes('vscode')) return 'vscode';
  return 'unknown';
}

export function getVsCodeUserMcpPath(
  isInsiders = false,
  platform: NodeJS.Platform = process.platform,
  homeDir: string = os.homedir(),
  appDataDir: string | undefined = process.env.APPDATA
): string {
  const codeDir = isInsiders ? 'Code - Insiders' : 'Code';
  if (platform === 'win32') {
    const base = appDataDir || path.join(homeDir, 'AppData', 'Roaming');
    return path.join(base, codeDir, 'User', 'mcp.json');
  }
  if (platform === 'darwin') {
    return path.join(homeDir, 'Library', 'Application Support', codeDir, 'User', 'mcp.json');
  }
  return path.join(homeDir, '.config', codeDir, 'User', 'mcp.json');
}

function resolveAntigravityConfigPath(homeDir: string): string {
  const agyDir = path.join(homeDir, '.gemini', 'antigravity');
  if (fs.existsSync(agyDir)) {
    return path.join(agyDir, 'mcp_config.json');
  }
  const geminiConfigDir = path.join(homeDir, '.gemini', 'config');
  if (fs.existsSync(geminiConfigDir)) {
    return path.join(geminiConfigDir, 'mcp_config.json');
  }
  return path.join(agyDir, 'mcp_config.json');
}

export function resolveMcpTargets(options: {
  hostKind: HostIdeKind;
  nativeProviderSupported: boolean;
  includeAllKnownTargets?: boolean;
  homeDir?: string;
  platform?: NodeJS.Platform;
  appDataDir?: string;
}): McpConfigTarget[] {
  const home = options.homeDir || os.homedir();
  const platform = options.platform || process.platform;
  const all = Boolean(options.includeAllKnownTargets);
  const targets: McpConfigTarget[] = [];

  if (options.hostKind === 'cursor' || all) {
    targets.push({
      id: 'cursor',
      displayName: 'Cursor',
      filePath: path.join(home, '.cursor', 'mcp.json'),
      sectionKey: SECTION_MCP_SERVERS,
      format: 'standard',
      createIfMissing: options.hostKind === 'cursor' || (all && fs.existsSync(path.join(home, '.cursor'))),
    });
  }

  if (options.hostKind === 'antigravity' || all) {
    const agyPath = resolveAntigravityConfigPath(home);
    targets.push({
      id: 'antigravity',
      displayName: 'Google Antigravity',
      filePath: agyPath,
      sectionKey: SECTION_MCP_SERVERS,
      format: 'standard',
      createIfMissing: options.hostKind === 'antigravity' || (all && fs.existsSync(path.join(home, '.gemini'))),
    });
  }

  if (options.hostKind === 'windsurf' || all) {
    const windsurfDir = path.join(home, '.codeium', 'windsurf');
    targets.push({
      id: 'windsurf',
      displayName: 'Windsurf',
      filePath: path.join(windsurfDir, 'mcp_config.json'),
      sectionKey: SECTION_MCP_SERVERS,
      format: 'standard',
      createIfMissing: options.hostKind === 'windsurf' || (all && fs.existsSync(windsurfDir)),
    });
  }

  const isVsCodeHost = options.hostKind === 'vscode' || options.hostKind === 'vscode-insiders';
  if ((isVsCodeHost && !options.nativeProviderSupported) || (all && isVsCodeHost)) {
    const vsPath = getVsCodeUserMcpPath(
      options.hostKind === 'vscode-insiders',
      platform,
      home,
      options.appDataDir
    );
    targets.push({
      id: 'vscode',
      displayName: 'VS Code User MCP',
      filePath: vsPath,
      sectionKey: SECTION_VSCODE_SERVERS,
      format: 'vscode',
      createIfMissing: true,
    });
  }

  const claudeCodePath = path.join(home, '.claude.json');
  targets.push({
    id: 'claude-code',
    displayName: 'Claude Code',
    filePath: claudeCodePath,
    sectionKey: SECTION_MCP_SERVERS,
    format: 'standard',
    createIfMissing: false,
  });

  return targets;
}

export function isCommandReachable(
  command: string | undefined,
  platform: NodeJS.Platform = process.platform,
  envPath: string | undefined = process.env.PATH
): boolean {
  if (!command || typeof command !== 'string') return false;
  const trimmed = command.trim();
  if (!trimmed) return false;

  if (path.isAbsolute(trimmed) || trimmed.includes('/') || trimmed.includes('\\')) {
    return fs.existsSync(trimmed);
  }

  return findInPath(trimmed, envPath, platform).length > 0;
}

export function buildMcpEntryPayload(
  executable: McpExecutableSpec,
  format: 'standard' | 'vscode'
): Record<string, unknown> {
  const args = [...executable.args, ARG_MCP, ARG_QUIET];
  if (format === 'vscode') {
    return {
      type: 'stdio',
      command: executable.command,
      args,
    };
  }
  return {
    command: executable.command,
    args,
  };
}

export function mergeMcpServerConfigFile(
  target: McpConfigTarget,
  executable: McpExecutableSpec,
  options: {
    force?: boolean;
    platform?: NodeJS.Platform;
    envPath?: string;
  } = {}
): McpMergeOutcome {
  const fileExists = fs.existsSync(target.filePath);
  if (!fileExists && !target.createIfMissing) {
    return { target, status: 'skipped-missing' };
  }

  let root: Record<string, any> = {};
  if (fileExists) {
    try {
      const raw = fs.readFileSync(target.filePath, UTF8_ENCODING);
      if (raw.trim().length > 0) {
        const parsed = JSON.parse(raw);
        if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
          return { target, status: 'skipped-invalid-json', details: 'Root JSON value is not an object.' };
        }
        root = parsed;
      }
    } catch (err: any) {
      return {
        target,
        status: 'skipped-invalid-json',
        details: err?.message || 'Failed to parse existing JSON config.',
      };
    }
  }

  const existingSection = root[target.sectionKey];
  const section: Record<string, any> =
    existingSection && typeof existingSection === 'object' && !Array.isArray(existingSection)
      ? existingSection
      : {};
  root[target.sectionKey] = section;

  const currentEntry = section[MCP_SERVER_KEY];
  const desiredEntry = buildMcpEntryPayload(executable, target.format);

  let status: McpMergeStatus = 'added';
  if (currentEntry && typeof currentEntry === 'object') {
    if (!options.force) {
      const currentCmd = typeof currentEntry.command === 'string' ? currentEntry.command : undefined;
      if (isCommandReachable(currentCmd, options.platform, options.envPath)) {
        return { target, status: 'unchanged' };
      }
      status = 'healed';
    } else {
      const sameJson = JSON.stringify(currentEntry) === JSON.stringify(desiredEntry);
      if (sameJson) {
        return { target, status: 'unchanged' };
      }
      status = 'updated';
    }
  }

  section[MCP_SERVER_KEY] = desiredEntry;

  try {
    const dir = path.dirname(target.filePath);
    if (!fs.existsSync(dir)) {
      fs.mkdirSync(dir, { recursive: true });
    }
    const serialized = JSON.stringify(root, null, JSON_INDENT) + '\n';
    const tmpPath = `${target.filePath}.ce-tmp-${process.pid}`;
    fs.writeFileSync(tmpPath, serialized, UTF8_ENCODING);
    fs.renameSync(tmpPath, target.filePath);
    return { target, status };
  } catch (err: any) {
    return { target, status: 'error', details: err?.message || String(err) };
  }
}

export function isMcpAlreadyConfigured(
  filePath: string,
  sectionKey: string,
  platform: NodeJS.Platform = process.platform,
  envPath: string | undefined = process.env.PATH
): boolean {
  if (!fs.existsSync(filePath)) return false;
  try {
    const raw = fs.readFileSync(filePath, UTF8_ENCODING);
    if (!raw.trim()) return false;
    const parsed = JSON.parse(raw);
    const entry = parsed?.[sectionKey]?.[MCP_SERVER_KEY];
    if (!entry || typeof entry !== 'object') return false;
    const cmd = typeof entry.command === 'string' ? entry.command : undefined;
    return isCommandReachable(cmd, platform, envPath);
  } catch (err) {
    return err ? false : false;
  }
}

export function isHostIdeMcpConfigured(options: {
  hostKind: HostIdeKind;
  workspaceRoot?: string;
  homeDir?: string;
  platform?: NodeJS.Platform;
  appDataDir?: string;
  envPath?: string;
}): boolean {
  const home = options.homeDir || os.homedir();
  const platform = options.platform || process.platform;
  const checks: Array<{ filePath: string; sectionKey: string }> = [];

  if (options.hostKind === 'cursor') {
    checks.push({ filePath: path.join(home, '.cursor', 'mcp.json'), sectionKey: SECTION_MCP_SERVERS });
    if (options.workspaceRoot) {
      checks.push({ filePath: path.join(options.workspaceRoot, '.cursor', 'mcp.json'), sectionKey: SECTION_MCP_SERVERS });
    }
  } else if (options.hostKind === 'antigravity') {
    checks.push({ filePath: resolveAntigravityConfigPath(home), sectionKey: SECTION_MCP_SERVERS });
    if (options.workspaceRoot) {
      checks.push({ filePath: path.join(options.workspaceRoot, '.agents', 'mcp_config.json'), sectionKey: SECTION_MCP_SERVERS });
    }
  } else if (options.hostKind === 'windsurf') {
    checks.push({ filePath: path.join(home, '.codeium', 'windsurf', 'mcp_config.json'), sectionKey: SECTION_MCP_SERVERS });
    if (options.workspaceRoot) {
      checks.push({ filePath: path.join(options.workspaceRoot, '.windsurf', 'mcp.json'), sectionKey: SECTION_MCP_SERVERS });
    }
  } else if (options.hostKind === 'vscode' || options.hostKind === 'vscode-insiders') {
    checks.push({
      filePath: getVsCodeUserMcpPath(options.hostKind === 'vscode-insiders', platform, home, options.appDataDir),
      sectionKey: SECTION_VSCODE_SERVERS,
    });
    if (options.workspaceRoot) {
      checks.push({ filePath: path.join(options.workspaceRoot, '.vscode', 'mcp.json'), sectionKey: SECTION_VSCODE_SERVERS });
    }
  }

  return checks.some((c) => isMcpAlreadyConfigured(c.filePath, c.sectionKey, platform, options.envPath));
}

export function shouldPromptForMcpRegistration(options: {
  promptSettingEnabled: boolean;
  consent: McpConsentState;
  hostAlreadyConfigured: boolean;
}): boolean {
  return options.promptSettingEnabled && options.consent === undefined && !options.hostAlreadyConfigured;
}

export function registerNativeVsCodeMcpProvider(
  processManager: Pick<ProcessManager, 'resolveInstalledExecutableSync' | 'resolveExecutable'>,
  getWorkspaceRoot: () => string | undefined,
  extensionVersion: string,
  outputChannel: Pick<vscodeTypes.OutputChannel, 'appendLine'>,
  vscodeApi: any = getVsCode(),
  isEnabled: () => boolean = () => true
): { supported: boolean; notifyChanged: () => void; disposable?: vscodeTypes.Disposable } {
  const registerFn = vscodeApi?.lm?.registerMcpServerDefinitionProvider;
  const StdioDef = vscodeApi?.McpStdioServerDefinition;
  if (typeof registerFn !== 'function' || typeof StdioDef !== 'function') {
    return { supported: false, notifyChanged: () => {} };
  }

  const emitter = new vscodeApi.EventEmitter();
  const buildDefinition = (spec: McpExecutableSpec, wsRoot?: string) => {
    const args = [...spec.args, ARG_MCP, ARG_QUIET];
    const env: Record<string, string> = wsRoot ? { [ENV_WORKSPACE_ROOT]: wsRoot } : {};
    const def = new StdioDef(MCP_SERVER_LABEL, spec.command, args, env, extensionVersion);
    if (wsRoot && vscodeApi.Uri?.file) {
      def.cwd = vscodeApi.Uri.file(wsRoot);
    }
    return def;
  };

  const provider = {
    onDidChangeMcpServerDefinitions: emitter.event,
    provideMcpServerDefinitions: async () => {
      if (!isEnabled()) {
        return [];
      }
      const wsRoot = getWorkspaceRoot();
      const installed = processManager.resolveInstalledExecutableSync(wsRoot) || {
        command: 'ce',
        args: [],
      };
      return [buildDefinition(installed, wsRoot)];
    },
    resolveMcpServerDefinition: async (server: any) => {
      const wsRoot = getWorkspaceRoot();
      const resolved = await processManager.resolveExecutable(wsRoot);
      if (resolved) {
        server.command = resolved.command;
        server.args = [...resolved.args, ARG_MCP, ARG_QUIET];
        if (wsRoot) {
          server.env = { ...(server.env || {}), [ENV_WORKSPACE_ROOT]: wsRoot };
          if (vscodeApi.Uri?.file) {
            server.cwd = vscodeApi.Uri.file(wsRoot);
          }
        }
      }
      return server;
    },
  };

  const registration = vscodeApi.lm.registerMcpServerDefinitionProvider(MCP_PROVIDER_ID, provider);
  outputChannel.appendLine(`[MCP] Registered native VS Code MCP provider '${MCP_PROVIDER_ID}'.`);

  return {
    supported: true,
    notifyChanged: () => emitter.fire(undefined),
    disposable: {
      dispose: () => {
        registration.dispose();
        emitter.dispose();
      },
    },
  };
}

export async function applyMcpTargetsRegistration(options: {
  processManager: ProcessManager;
  getWorkspaceRoot: () => string | undefined;
  outputChannel: vscodeTypes.OutputChannel;
  targets: McpConfigTarget[];
  notifyNativeProviderChanged: () => void;
  force: boolean;
}): Promise<McpMergeOutcome[]> {
  const wsRoot = options.getWorkspaceRoot();
  const resolved = await options.processManager.resolveExecutable(wsRoot);
  if (!resolved) {
    options.outputChannel.appendLine('[MCP] Could not resolve CodeExplorer executable for MCP registration.');
    return [];
  }

  options.notifyNativeProviderChanged();

  const outcomes: McpMergeOutcome[] = [];
  for (const target of options.targets) {
    const outcome = mergeMcpServerConfigFile(target, resolved, { force: options.force });
    outcomes.push(outcome);
    if (outcome.status === 'added' || outcome.status === 'updated' || outcome.status === 'healed') {
      options.outputChannel.appendLine(
        `[MCP] ${outcome.status.toUpperCase()} '${MCP_SERVER_KEY}' in ${target.displayName} (${target.filePath}) -> ${resolved.command}`
      );
    } else if (outcome.status === 'skipped-invalid-json' || outcome.status === 'error') {
      options.outputChannel.appendLine(
        `[MCP] Skipped ${target.displayName} (${target.filePath}): ${outcome.details || outcome.status}`
      );
    }
  }
  return outcomes;
}

interface TargetQuickPickItem extends vscodeTypes.QuickPickItem {
  targetId: string;
  target?: McpConfigTarget;
}

function buildInteractiveQuickPickItems(
  hostKind: HostIdeKind,
  nativeSupported: boolean
): TargetQuickPickItem[] {
  const allTargets = resolveMcpTargets({
    hostKind,
    nativeProviderSupported: nativeSupported,
    includeAllKnownTargets: true,
  });

  const items: TargetQuickPickItem[] = [];
  if (nativeSupported) {
    items.push({
      label: NATIVE_VSCODE_DISPLAY_NAME,
      detail: NATIVE_VSCODE_DETAIL,
      picked: true,
      targetId: NATIVE_VSCODE_ID,
    });
  }
  for (const target of allTargets) {
    const isCurrentHost = target.id === hostKind;
    const alreadySet = isMcpAlreadyConfigured(target.filePath, target.sectionKey);
    items.push({
      label: target.displayName,
      detail: target.filePath,
      picked: (isCurrentHost && !nativeSupported) || alreadySet,
      targetId: target.id,
      target,
    });
  }
  return items;
}

async function runInteractiveMcpRegistration(
  vscode: typeof vscodeTypes,
  context: vscodeTypes.ExtensionContext,
  processManager: ProcessManager,
  getWorkspaceRoot: () => string | undefined,
  outputChannel: vscodeTypes.OutputChannel,
  nativeProvider: { supported: boolean; notifyChanged: () => void }
): Promise<void> {
  const hostKind = detectHostIdeKind(vscode.env?.appName, vscode.env?.uriScheme);
  const items = buildInteractiveQuickPickItems(hostKind, nativeProvider.supported);
  const selected = await vscode.window.showQuickPick(items, {
    canPickMany: true,
    placeHolder: QUICK_PICK_PLACEHOLDER,
  });
  if (!selected || selected.length === 0) {
    return;
  }

  const choseNative = selected.some((s) => s.targetId === NATIVE_VSCODE_ID);
  const chosenFileTargets = selected
    .filter((s): s is TargetQuickPickItem & { target: McpConfigTarget } => Boolean(s.target))
    .map((s) => ({ ...s.target, createIfMissing: true }));

  await context.globalState.update(STATE_MCP_CONSENT, CONSENT_APPROVED);
  const outcomes = await applyMcpTargetsRegistration({
    processManager,
    getWorkspaceRoot,
    outputChannel,
    targets: chosenFileTargets,
    notifyNativeProviderChanged: nativeProvider.notifyChanged,
    force: true,
  });

  const registeredNames = outcomes
    .filter((o) => o.status === 'added' || o.status === 'updated' || o.status === 'healed' || o.status === 'unchanged')
    .map((o) => o.target.displayName);
  if (choseNative) {
    registeredNames.unshift(NATIVE_VSCODE_DISPLAY_NAME);
  }
  if (registeredNames.length > 0) {
    vscode.window.showInformationMessage(`CodeExplorer MCP server registered for: ${registeredNames.join(', ')}.`);
  }
}

function promptUserForMcpConsent(
  vscode: typeof vscodeTypes,
  context: vscodeTypes.ExtensionContext,
  processManager: ProcessManager,
  getWorkspaceRoot: () => string | undefined,
  outputChannel: vscodeTypes.OutputChannel,
  hostPrimaryTargets: McpConfigTarget[],
  nativeProvider: { supported: boolean; notifyChanged: () => void }
): void {
  void vscode.window
    .showInformationMessage(PROMPT_REGISTER_MCP, ACTION_REGISTER_MCP, ACTION_LATER, ACTION_DONT_ASK_AGAIN)
    .then(async (choice) => {
      if (choice === ACTION_REGISTER_MCP) {
        await context.globalState.update(STATE_MCP_CONSENT, CONSENT_APPROVED);
        const outcomes = await applyMcpTargetsRegistration({
          processManager,
          getWorkspaceRoot,
          outputChannel,
          targets: hostPrimaryTargets,
          notifyNativeProviderChanged: nativeProvider.notifyChanged,
          force: false,
        });
        const names = outcomes
          .filter((o) => o.status === 'added' || o.status === 'updated' || o.status === 'healed' || o.status === 'unchanged')
          .map((o) => o.target.displayName);
        if (nativeProvider.supported && !names.includes(NATIVE_VSCODE_DISPLAY_NAME)) {
          names.unshift(NATIVE_VSCODE_DISPLAY_NAME);
        }
        vscode.window.showInformationMessage(
          `CodeExplorer MCP server registered for: ${names.join(', ') || 'current IDE'}.`
        );
      } else if (choice === ACTION_DONT_ASK_AGAIN) {
        await context.globalState.update(STATE_MCP_CONSENT, CONSENT_DISMISSED);
      }
    });
}

export function initializeMcpAutoRegistration(
  context: vscodeTypes.ExtensionContext,
  processManager: ProcessManager,
  getWorkspaceRoot: () => string | undefined,
  outputChannel: vscodeTypes.OutputChannel
): void {
  const vscode = getVsCode();
  if (!vscode) return;

  const getConsent = () => context.globalState.get<McpConsentState>(STATE_MCP_CONSENT);
  const extensionVersion = context.extension?.packageJSON?.version || '1.0.0';
  const nativeProvider = registerNativeVsCodeMcpProvider(
    processManager,
    getWorkspaceRoot,
    extensionVersion,
    outputChannel,
    vscode,
    () => getConsent() === CONSENT_APPROVED
  );
  if (nativeProvider.disposable) {
    context.subscriptions.push(nativeProvider.disposable);
  }

  const registerCmd = vscode.commands.registerCommand(CMD_REGISTER_MCP, async () => {
    try {
      await runInteractiveMcpRegistration(
        vscode,
        context,
        processManager,
        getWorkspaceRoot,
        outputChannel,
        nativeProvider
      );
    } catch (err: any) {
      outputChannel.appendLine(`[MCP] Manual registration failed: ${err?.message || err}`);
      vscode.window.showErrorMessage(`CodeExplorer MCP registration failed: ${err?.message || err}`);
    }
  });
  context.subscriptions.push(registerCmd);

  const hostKind = detectHostIdeKind(vscode.env?.appName, vscode.env?.uriScheme);
  const hostPrimaryTargets = resolveMcpTargets({
    hostKind,
    nativeProviderSupported: nativeProvider.supported,
    includeAllKnownTargets: false,
  }).filter((t) => t.createIfMissing);

  const consent = getConsent();
  if (consent === CONSENT_APPROVED) {
    void applyMcpTargetsRegistration({
      processManager,
      getWorkspaceRoot,
      outputChannel,
      targets: hostPrimaryTargets,
      notifyNativeProviderChanged: nativeProvider.notifyChanged,
      force: false,
    }).catch((err: any) => {
      outputChannel.appendLine(`[MCP] Self-healing check notice: ${err?.message || err}`);
    });
    return;
  }

  const promptEnabled = vscode.workspace
    .getConfiguration(CONFIG_NAMESPACE)
    .get<boolean>(SETTING_MCP_PROMPT_TO_REGISTER, true);
  const hostAlreadyConfigured = isHostIdeMcpConfigured({
    hostKind,
    workspaceRoot: getWorkspaceRoot(),
  });

  if (shouldPromptForMcpRegistration({ promptSettingEnabled: promptEnabled, consent, hostAlreadyConfigured })) {
    promptUserForMcpConsent(
      vscode,
      context,
      processManager,
      getWorkspaceRoot,
      outputChannel,
      hostPrimaryTargets,
      nativeProvider
    );
  }
}
