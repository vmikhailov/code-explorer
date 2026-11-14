import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import * as cp from 'child_process';
import { DotnetToolInfo, InstalledEngineCandidate, ProbeInstalledOptions } from './types';
import { cleanSemver, compareSemver } from './semver';
import { getExecutableName, findInPath } from './platform';

const EVT_DATA = 'data';
const EVT_ERROR = 'error';
const EVT_CLOSE = 'close';
const ENCODING_UTF8: BufferEncoding = 'utf8';
const CMD_DOTNET = 'dotnet';
const BIN_CE = 'ce';

const sessionVersionCache = new Map<string, string>();
let sessionCandidatesCache: {
  cacheKey: string;
  candidates: InstalledEngineCandidate[];
} | null = null;

/**
 * Clears the in-memory session discovery cache (e.g. when executablePath settings change or after engine update).
 */
export function clearEngineDiscoveryCache(): void {
  sessionVersionCache.clear();
  sessionCandidatesCache = null;
}

/**
 * Extracts a normalized SemVer string from command output, ignoring warning/log lines.
 * Checks stdout first, iterating from bottom to top, and falls back to stderr.
 */
export function extractVersionFromOutput(stdout?: string | null, stderr?: string | null): string | null {
  const parseLines = (text: string | null | undefined): string | null => {
    if (!text) return null;
    const lines = text.split(/\r?\n/).map((l) => l.trim()).filter(Boolean);
    for (let i = lines.length - 1; i >= 0; i--) {
      const line = lines[i];
      if (/^\s*(?:ce\s+|ce-)?v?\d+\.\d+(?:\.\d+)?/i.test(line)) {
        const cleaned = cleanSemver(line);
        if (cleaned) return cleaned;
      }
    }
    for (let i = lines.length - 1; i >= 0; i--) {
      const cleaned = cleanSemver(lines[i]);
      if (cleaned) return cleaned;
    }
    return null;
  };

  return parseLines(stdout) || parseLines(stderr);
}

function makeCommandCacheKey(cmd: string, args: string[]): string {
  return `${path.normalize(cmd).toLowerCase()}|${args.join(' ')}`;
}

/**
 * Non-blocking async version probe using `cp.execFile` (or `cp.spawn` for Windows .cmd/.bat shims).
 * Caches resolved versions in memory for the duration of the extension session.
 */
export function probeCommandVersionAsync(cmd: string, args: string[] = []): Promise<string | null> {
  const key = makeCommandCacheKey(cmd, args);
  const cached = sessionVersionCache.get(key);
  if (cached) {
    return Promise.resolve(cached);
  }

  return new Promise<string | null>((resolve) => {
    try {
      const isWin = process.platform === 'win32';
      const isBatch = isWin && (cmd.toLowerCase().endsWith('.cmd') || cmd.toLowerCase().endsWith('.bat'));
      const fullArgs = [...args, '--version'];

      if (isBatch) {
        const child = cp.spawn(cmd, fullArgs, {
          windowsHide: true,
          shell: true,
        });
        let stdout = '';
        let stderr = '';
        const timer = setTimeout(() => {
          try {
            child.kill();
          } catch {
            // ignored: process may have already exited
          }
          resolve(null);
        }, 5000);

        child.stdout?.on(EVT_DATA, (d) => (stdout += String(d)));
        child.stderr?.on(EVT_DATA, (d) => (stderr += String(d)));
        child.on(EVT_ERROR, () => {
          clearTimeout(timer);
          resolve(null);
        });
        child.on(EVT_CLOSE, (code) => {
          clearTimeout(timer);
          if (code === 0) {
            const ver = extractVersionFromOutput(stdout, stderr);
            if (ver) {
              sessionVersionCache.set(key, ver);
            }
            resolve(ver);
          } else {
            resolve(null);
          }
        });
        return;
      }

      cp.execFile(
        cmd,
        fullArgs,
        { encoding: 'utf8', timeout: 5000, windowsHide: true },
        (err, stdout, stderr) => {
          if (!err) {
            const ver = extractVersionFromOutput(stdout, stderr);
            if (ver) {
              sessionVersionCache.set(key, ver);
            }
            resolve(ver);
          } else {
            resolve(null);
          }
        }
      );
    } catch {
      resolve(null);
    }
  });
}

function readLocalManifestToolVersion(manifestPath: string): string | null {
  try {
    if (!fs.existsSync(manifestPath)) return null;
    const raw = JSON.parse(fs.readFileSync(manifestPath, ENCODING_UTF8));
    const toolEntry = raw?.tools?.['codeexplorer.cli'] || raw?.tools?.['CodeExplorer.Cli'];
    if (toolEntry?.version) {
      return cleanSemver(String(toolEntry.version));
    }
  } catch {
    // Ignore
  }
  return null;
}

function readGlobalStoreToolVersion(home: string): string | null {
  try {
    const storeDir = path.join(home, '.dotnet', 'tools', '.store', 'codeexplorer.cli');
    if (fs.existsSync(storeDir)) {
      const entries = fs.readdirSync(storeDir).filter((e) => /^\d+\.\d+/.test(e));
      if (entries.length > 0) {
        entries.sort(compareSemver);
        return cleanSemver(entries[entries.length - 1]);
      }
    }
  } catch {
    // Ignore
  }
  return null;
}

/**
 * Synchronous non-blocking version lookup from session cache or adjacent version metadata files.
 * Never calls `cp.spawnSync`.
 */
export function probeCommandVersion(cmd: string, args: string[] = []): string | null {
  const key = makeCommandCacheKey(cmd, args);
  const cached = sessionVersionCache.get(key);
  if (cached) {
    return cached;
  }

  try {
    const dir = path.dirname(cmd);
    const verFile = path.join(dir, 'version.json');
    if (fs.existsSync(verFile)) {
      const verData = JSON.parse(fs.readFileSync(verFile, ENCODING_UTF8));
      if (verData?.version) {
        const cleaned = cleanSemver(String(verData.version));
        if (cleaned) {
          sessionVersionCache.set(key, cleaned);
          return cleaned;
        }
      }
    }
  } catch {
    // Ignore
  }

  return null;
}

/**
 * Asynchronously probes for an installed CodeExplorer dotnet tool without blocking the Extension Host.
 */
export async function probeDotnetToolAsync(
  workspaceRoot?: string,
  customHomeDir?: string
): Promise<DotnetToolInfo | null> {
  if (workspaceRoot) {
    try {
      const localManifest = path.join(workspaceRoot, '.config', 'dotnet-tools.json');
      if (fs.existsSync(localManifest)) {
        const ver =
          readLocalManifestToolVersion(localManifest) ||
          (await probeCommandVersionAsync(CMD_DOTNET, ['tool', 'run', 'ce']));
        if (ver) {
          return { command: 'dotnet', args: ['tool', 'run', 'ce'], version: ver };
        }
      }
    } catch {
      // Ignore
    }
  }

  try {
    const home = customHomeDir || os.homedir();
    const globalToolPath = path.join(home, '.dotnet', 'tools', getExecutableName(BIN_CE));
    if (fs.existsSync(globalToolPath)) {
      const ver =
        readGlobalStoreToolVersion(home) || (await probeCommandVersionAsync(globalToolPath));
      if (ver) {
        return { command: globalToolPath, args: [], version: ver };
      }
    }
  } catch {
    // Ignore
  }

  try {
    const sysPaths = findInPath(BIN_CE);
    for (const sysPath of sysPaths) {
      const ver = await probeCommandVersionAsync(sysPath);
      if (ver) {
        return { command: sysPath, args: [], version: ver };
      }
    }
  } catch {
    // Ignore
  }

  return null;
}

/**
 * Synchronous non-blocking dotnet tool check using manifest/store metadata and session cache.
 */
export function probeDotnetTool(
  workspaceRoot?: string,
  customHomeDir?: string
): DotnetToolInfo | null {
  if (workspaceRoot) {
    const localManifest = path.join(workspaceRoot, '.config', 'dotnet-tools.json');
    const ver =
      readLocalManifestToolVersion(localManifest) ||
      probeCommandVersion('dotnet', ['tool', 'run', 'ce']);
    if (ver) {
      return { command: 'dotnet', args: ['tool', 'run', 'ce'], version: ver };
    }
  }

  const home = customHomeDir || os.homedir();
  const globalToolPath = path.join(home, '.dotnet', 'tools', getExecutableName('ce'));
  if (fs.existsSync(globalToolPath)) {
    const ver = readGlobalStoreToolVersion(home) || probeCommandVersion(globalToolPath);
    if (ver) {
      return { command: globalToolPath, args: [], version: ver };
    }
  }

  const sysPaths = findInPath('ce');
  for (const sysPath of sysPaths) {
    const ver = probeCommandVersion(sysPath);
    if (ver) {
      return { command: sysPath, args: [], version: ver };
    }
  }

  return null;
}

/**
 * Asynchronously probes all installed engine candidates across the machine and caches the result for the session.
 */
export async function probeAllInstalledEnginesAsync(
  options?: ProbeInstalledOptions
): Promise<InstalledEngineCandidate[]> {
  const envPath = process.env.CE_EXECUTABLE || '';
  const cacheKey = JSON.stringify({
    ws: options?.workspaceRoot || '',
    bin: options?.globalStorageBinDir || '',
    ext: options?.extensionRoot || '',
    custom: options?.customExecutablePath || envPath,
    home: options?.homedir || '',
  });

  if (sessionCandidatesCache && sessionCandidatesCache.cacheKey === cacheKey) {
    return sessionCandidatesCache.candidates;
  }

  const candidates: InstalledEngineCandidate[] = [];
  const seenCommands = new Set<string>();

  const addCandidate = (cand: InstalledEngineCandidate) => {
    const norm = path.normalize(cand.command).toLowerCase();
    const key = `${norm}|${cand.args.join(' ')}`;
    if (!seenCommands.has(key)) {
      seenCommands.add(key);
      candidates.push(cand);
    }
  };

  // 1. Explicit custom path or ENV if provided
  const customTarget = options?.customExecutablePath?.trim() || envPath.trim();
  if (customTarget) {
    try {
      const resolved = path.isAbsolute(customTarget)
        ? customTarget
        : options?.workspaceRoot
          ? path.resolve(options.workspaceRoot, customTarget)
          : path.resolve(customTarget);
      if (fs.existsSync(resolved)) {
        const isDll = resolved.endsWith('.dll');
        const ver =
          (isDll
            ? await probeCommandVersionAsync(CMD_DOTNET, [resolved])
            : await probeCommandVersionAsync(resolved)) || '0.0.0-custom';
        addCandidate({
          command: isDll ? 'dotnet' : resolved,
          args: isDll ? [resolved] : [],
          version: ver,
          source: 'custom',
        });
      }
    } catch {
      // Ignore
    }
  }

  // 2. Local workspace dotnet tool (.config/dotnet-tools.json)
  if (options?.workspaceRoot) {
    try {
      const localManifest = path.join(options.workspaceRoot, '.config', 'dotnet-tools.json');
      if (fs.existsSync(localManifest)) {
        const ver =
          readLocalManifestToolVersion(localManifest) ||
          (await probeCommandVersionAsync(CMD_DOTNET, ['tool', 'run', 'ce']));
        if (ver) {
          addCandidate({
            command: 'dotnet',
            args: ['tool', 'run', 'ce'],
            version: ver,
            source: 'dotnet-tool',
          });
        }
      }
    } catch {
      // Ignore
    }
  }

  // 3. Global dotnet tools directory (~/.dotnet/tools/ce[.exe])
  try {
    const home = options?.homedir || os.homedir();
    const globalToolPath = path.join(home, '.dotnet', 'tools', getExecutableName(BIN_CE));
    if (fs.existsSync(globalToolPath)) {
      const ver =
        readGlobalStoreToolVersion(home) || (await probeCommandVersionAsync(globalToolPath));
      if (ver) {
        addCandidate({
          command: globalToolPath,
          args: [],
          version: ver,
          source: 'dotnet-tool',
        });
      }
    }
  } catch {
    // Ignore
  }

  // 4. Downloaded engine in extension global storage (<globalStorage>/bin/ce[.exe])
  if (options?.globalStorageBinDir) {
    try {
      const storedBin = path.join(options.globalStorageBinDir, getExecutableName(BIN_CE));
      if (fs.existsSync(storedBin)) {
        let ver = probeCommandVersion(storedBin);
        if (!ver) {
          ver = await probeCommandVersionAsync(storedBin);
        }
        if (ver) {
          addCandidate({
            command: storedBin,
            args: [],
            version: ver,
            source: 'downloaded',
          });
        }
      }
    } catch {
      // Ignore
    }
  }

  // 5. System PATH 'ce'
  try {
    const sysPaths = findInPath(BIN_CE);
    for (const sysPath of sysPaths) {
      const ver = await probeCommandVersionAsync(sysPath);
      if (ver) {
        addCandidate({
          command: sysPath,
          args: [],
          version: ver,
          source: 'path',
        });
      }
    }
  } catch {
    // Ignore
  }

  // 6. Bundled binary in extension bin/
  if (options?.extensionRoot) {
    try {
      const bundledBin = path.join(options.extensionRoot, 'bin', getExecutableName(BIN_CE));
      if (fs.existsSync(bundledBin)) {
        const ver =
          probeCommandVersion(bundledBin) || (await probeCommandVersionAsync(bundledBin));
        if (ver) {
          addCandidate({
            command: bundledBin,
            args: [],
            version: ver,
            source: 'bundled',
          });
        }
      }
    } catch {
      // Ignore
    }
  }

  sessionCandidatesCache = { cacheKey, candidates };
  return candidates;
}

/**
 * Synchronous non-blocking candidate lookup using session cache and filesystem metadata only (0 `spawnSync`).
 */
export function probeAllInstalledEngines(options?: ProbeInstalledOptions): InstalledEngineCandidate[] {
  if (sessionCandidatesCache) {
    return sessionCandidatesCache.candidates;
  }

  const candidates: InstalledEngineCandidate[] = [];
  const envPath = process.env.CE_EXECUTABLE || '';
  const customTarget = options?.customExecutablePath?.trim() || envPath.trim();
  if (customTarget) {
    const resolved = path.isAbsolute(customTarget)
      ? customTarget
      : options?.workspaceRoot
        ? path.resolve(options.workspaceRoot, customTarget)
        : path.resolve(customTarget);
    if (fs.existsSync(resolved)) {
      const isDll = resolved.endsWith('.dll');
      candidates.push({
        command: isDll ? 'dotnet' : resolved,
        args: isDll ? [resolved] : [],
        version: probeCommandVersion(resolved) || '0.0.0-custom',
        source: 'custom',
      });
    }
  }

  const tool = probeDotnetTool(options?.workspaceRoot, options?.homedir);
  if (tool) {
    candidates.push({
      command: tool.command,
      args: tool.args,
      version: tool.version,
      source: 'dotnet-tool',
    });
  }

  if (options?.globalStorageBinDir) {
    const storedBin = path.join(options.globalStorageBinDir, getExecutableName('ce'));
    if (fs.existsSync(storedBin)) {
      const ver = probeCommandVersion(storedBin);
      if (ver) {
        candidates.push({
          command: storedBin,
          args: [],
          version: ver,
          source: 'downloaded',
        });
      }
    }
  }

  return candidates;
}
