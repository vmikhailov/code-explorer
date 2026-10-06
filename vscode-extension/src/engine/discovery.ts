import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import * as cp from 'child_process';
import { DotnetToolInfo, InstalledEngineCandidate, ProbeInstalledOptions } from './types';
import { cleanSemver, compareSemver } from './semver';
import { getExecutableName, findInPath } from './platform';

/**
 * Runs a command with '--version' and returns the normalized SemVer string, or null on failure.
 */
export function probeCommandVersion(cmd: string, args: string[] = []): string | null {
  try {
    const isWin = process.platform === 'win32';
    const isBatch = isWin && (cmd.toLowerCase().endsWith('.cmd') || cmd.toLowerCase().endsWith('.bat'));
    const res = cp.spawnSync(cmd, [...args, '--version'], {
      encoding: 'utf8',
      timeout: 5000,
      windowsHide: true,
      shell: isBatch,
    });
    const output = (res.stdout && res.stdout.trim().length > 0 ? res.stdout : res.stderr)?.trim();
    if (res.status === 0 && output) {
      const firstLine = output.split(/\r?\n/)[0];
      return cleanSemver(firstLine);
    }
  } catch {
    // Ignore execution failure
  }
  return null;
}

/**
 * Probes for an installed CodeExplorer dotnet tool.
 * Checks:
 * 1. Local workspace tool manifest (.config/dotnet-tools.json)
 * 2. Standard global dotnet tools directory (~/.dotnet/tools/ce[.exe])
 * 3. System PATH 'ce'
 */
export function probeDotnetTool(
  workspaceRoot?: string,
  customHomelessDir?: string
): DotnetToolInfo | null {
  // 1. Check workspace local tool manifest: .config/dotnet-tools.json
  if (workspaceRoot) {
    try {
      const localManifest = path.join(workspaceRoot, '.config', 'dotnet-tools.json');
      if (fs.existsSync(localManifest)) {
        const ver = probeCommandVersion('dotnet', ['tool', 'run', 'ce']);
        if (ver) {
          return { command: 'dotnet', args: ['tool', 'run', 'ce'], version: ver };
        }
      }
    } catch {
      // Ignore
    }
  }

  // 2. Check standard dotnet tools directory (~/.dotnet/tools)
  try {
    const home = customHomelessDir || os.homedir();
    const globalToolPath = path.join(home, '.dotnet', 'tools', getExecutableName('ce'));
    if (fs.existsSync(globalToolPath)) {
      let ver = probeCommandVersion(globalToolPath);
      if (!ver) {
        const storeDir = path.join(home, '.dotnet', 'tools', '.store', 'codeexplorer.cli');
        if (fs.existsSync(storeDir)) {
          const entries = fs.readdirSync(storeDir).filter((e) => /^\d+\.\d+/.test(e));
          if (entries.length > 0) {
            entries.sort(compareSemver);
            ver = cleanSemver(entries[entries.length - 1]);
          }
        }
      }
      if (ver) {
        return { command: globalToolPath, args: [], version: ver };
      }
    }
  } catch {
    // Ignore
  }

  // 3. Check system PATH 'ce'
  try {
    const sysPaths = findInPath('ce');
    for (const sysPath of sysPaths) {
      const ver = probeCommandVersion(sysPath);
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
 * Probes all available installed engine candidates on the machine.
 * Sources inspected:
 * 1. Explicitly configured path (settings / ENV)
 * 2. Workspace dotnet tool manifest (.config/dotnet-tools.json)
 * 3. Global dotnet tools directory (~/.dotnet/tools/ce[.exe])
 * 4. Downloaded engine in extension global storage (<globalStorage>/bin/ce[.exe])
 * 5. System PATH 'ce'
 * 6. Bundled binary in extension (<extensionRoot>/bin/ce[.exe])
 */
export function probeAllInstalledEngines(options?: ProbeInstalledOptions): InstalledEngineCandidate[] {
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
  const envPath = process.env.CE_EXECUTABLE;
  const customTarget = options?.customExecutablePath?.trim() || envPath?.trim();
  if (customTarget) {
    try {
      const resolved = path.isAbsolute(customTarget)
        ? customTarget
        : (options?.workspaceRoot ? path.resolve(options.workspaceRoot, customTarget) : path.resolve(customTarget));
      if (fs.existsSync(resolved)) {
        const isDll = resolved.endsWith('.dll');
        const ver = isDll ? probeCommandVersion('dotnet', [resolved]) : probeCommandVersion(resolved);
        if (ver) {
          addCandidate({
            command: isDll ? 'dotnet' : resolved,
            args: isDll ? [resolved] : [],
            version: ver,
            source: 'custom',
          });
        }
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
        const ver = probeCommandVersion('dotnet', ['tool', 'run', 'ce']);
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
    const globalToolPath = path.join(home, '.dotnet', 'tools', getExecutableName('ce'));
    if (fs.existsSync(globalToolPath)) {
      let ver = probeCommandVersion(globalToolPath);
      if (!ver) {
        const storeDir = path.join(home, '.dotnet', 'tools', '.store', 'codeexplorer.cli');
        if (fs.existsSync(storeDir)) {
          const entries = fs.readdirSync(storeDir).filter((e) => /^\d+\.\d+/.test(e));
          if (entries.length > 0) {
            entries.sort(compareSemver);
            ver = cleanSemver(entries[entries.length - 1]);
          }
        }
      }
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
      const storedBin = path.join(options.globalStorageBinDir, getExecutableName('ce'));
      if (fs.existsSync(storedBin)) {
        let ver = probeCommandVersion(storedBin);
        if (!ver) {
          const verFile = path.join(options.globalStorageBinDir, 'version.json');
          if (fs.existsSync(verFile)) {
            const data = JSON.parse(fs.readFileSync(verFile, 'utf8'));
            if (data.version) ver = cleanSemver(data.version);
          }
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
    const sysPaths = findInPath('ce');
    for (const sysPath of sysPaths) {
      const ver = probeCommandVersion(sysPath);
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
      const bundledBin = path.join(options.extensionRoot, 'bin', getExecutableName('ce'));
      if (fs.existsSync(bundledBin)) {
        const ver = probeCommandVersion(bundledBin);
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

  return candidates;
}
