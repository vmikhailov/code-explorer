import type * as vscodeTypes from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import {
  InstalledEngineCandidate,
  TargetInfo,
  UpdateCheckResult,
} from './types';
import {
  cleanSemver,
  compareSemver,
  getEngineConfigFromExtensionVersion,
  isBuildDifferenceOnly,
  matchesEnginePattern,
} from './semver';
import { ensureExecutablePermissions, extractArchive, resolveTargetAsset } from './platform';
import { GITHUB_REPO, downloadFileWithRedirects, probeEngineVersion } from './downloader';
import { probeCommandVersion } from './discovery';
import { getBestInstalledEngine } from './selector';

function getVsCode(): typeof import('vscode') | undefined {
  try {
    return require('vscode');
  } catch {
    return undefined;
  }
}

/**
 * Manages downloading, caching, and running the CodeExplorer CLI native engine.
 */
export class BinaryManager {
  private context?: vscodeTypes.ExtensionContext;
  private outputChannel: vscodeTypes.OutputChannel;

  constructor(outputChannel: vscodeTypes.OutputChannel, context?: vscodeTypes.ExtensionContext) {
    this.context = context;
    this.outputChannel = outputChannel;
  }

  setContext(context: vscodeTypes.ExtensionContext): void {
    this.context = context;
  }

  /**
   * Gets the extension version dynamically from package.json or ExtensionContext.
   */
  getExtensionVersion(): string {
    if (this.context?.extension?.packageJSON?.version) {
      return this.context.extension.packageJSON.version;
    }
    try {
      if (this.context?.extensionPath) {
        const pkgPath = path.join(this.context.extensionPath, 'package.json');
        if (fs.existsSync(pkgPath)) {
          const pkg = JSON.parse(fs.readFileSync(pkgPath, 'utf8'));
          if (pkg.version) return pkg.version;
        }
      }
      const fallbackPkg = path.resolve(__dirname, '../../package.json');
      if (fs.existsSync(fallbackPkg)) {
        const pkg = JSON.parse(fs.readFileSync(fallbackPkg, 'utf8'));
        if (pkg.version) return pkg.version;
      }
    } catch {
      // Ignore
    }
    return '1.0.0';
  }

  /**
   * Gets the local storage bin directory.
   */
  getBinDir(): string | null {
    if (!this.context) return null;
    return path.join(this.context.globalStorageUri.fsPath, 'bin');
  }

  /**
   * Gets the path to the expected binary in global storage.
   */
  getStoredBinaryPath(): string | null {
    const binDir = this.getBinDir();
    if (!binDir) return null;
    const target = resolveTargetAsset();
    return path.join(binDir, target.binName);
  }

  /**
   * Reads the currently cached engine version, if any.
   */
  getCachedVersion(): string | null {
    const binDir = this.getBinDir();
    if (!binDir) return null;
    try {
      const verFile = path.join(binDir, 'version.json');
      if (fs.existsSync(verFile)) {
        const data = JSON.parse(fs.readFileSync(verFile, 'utf8'));
        return data.version || null;
      }
    } catch {
      // Ignore
    }
    return null;
  }

  /**
   * Probes and returns the currently downloaded engine info, if present on disk.
   */
  getDownloadedEngine(): { command: string; args: string[]; version: string } | null {
    const binPath = this.getStoredBinaryPath();
    if (!binPath || !fs.existsSync(binPath)) return null;

    const cached = this.getCachedVersion();
    const probed = probeCommandVersion(binPath);
    const version = probed || cached;
    if (!version) return null;

    return {
      command: binPath,
      args: [],
      version,
    };
  }

  /**
   * Probes all installed engine candidates across the system (dotnet-tools, PATH, global storage, bundled)
   * and returns the best candidate (highest semver).
   */
  getBestInstalledEngine(workspaceRoot?: string): InstalledEngineCandidate | null {
    const binDir = this.getBinDir();
    const extensionRoot = this.context?.extensionPath;
    return getBestInstalledEngine({
      workspaceRoot,
      globalStorageBinDir: binDir || undefined,
      extensionRoot,
    });
  }

  /**
   * Checks for engine updates against remote releases.
   * Probes all installed engine candidates across the system first.
   * Avoids downloading if an installed version is already the same or higher than the remote release.
   */
  async checkAndUpdate(forceReinstall = false, workspaceRoot?: string): Promise<UpdateCheckResult> {
    const result = await this.resolveOrUpdateEngine({
      forceRemoteProbe: true,
      forceReinstall,
      workspaceRoot,
    });

    const vscode = getVsCode();
    vscode?.window?.showInformationMessage(result.message);

    return {
      updated: result.updated,
      currentVersion: result.currentVersion,
      targetVersion: result.targetVersion,
      command: result.command,
      message: result.message,
    };
  }

  /**
   * Ensures the binary is available and matches the required version.
   * ALWAYS probes all installed engines across the system first before contacting GitHub or downloading.
   * If any installed version satisfies the required pattern, or is newer than or equal to the remote release,
   * it uses the installed engine without downloading.
   */
  async ensureBinary(forceUpdate = false, workspaceRoot?: string): Promise<{ command: string; args: string[] }> {
    const result = await this.resolveOrUpdateEngine({
      forceRemoteProbe: forceUpdate,
      forceReinstall: forceUpdate,
      workspaceRoot,
    });

    return {
      command: result.command,
      args: result.args,
    };
  }

  /**
   * Unified, DRY engine resolution and update pipeline.
   */
  private async resolveOrUpdateEngine(options: {
    forceRemoteProbe: boolean;
    forceReinstall: boolean;
    workspaceRoot?: string;
  }): Promise<{
    command: string;
    args: string[];
    currentVersion: string | null;
    targetVersion: string;
    updated: boolean;
    message: string;
  }> {
    const binDir = this.getBinDir();
    const binPath = this.getStoredBinaryPath();

    if (!binDir || !binPath) {
      throw new Error('Extension storage context is not initialized.');
    }

    const target = resolveTargetAsset();
    const extVersion = this.getExtensionVersion();
    const vscode = getVsCode();
    const config = vscode?.workspace?.getConfiguration('codeExplorer');
    const userSetting = config?.get<string>('engineVersion', '')?.trim() || undefined;
    const { pattern } = getEngineConfigFromExtensionVersion(extVersion, userSetting);

    // 1. Always probe installed engines first across the system
    const bestInstalled = this.getBestInstalledEngine(options.workspaceRoot);
    const currentVersion = bestInstalled?.version || this.getCachedVersion() || null;

    // Fast-path: If local binary exists and satisfies required pattern (or >= extVersion), use it immediately
    if (!options.forceRemoteProbe && !options.forceReinstall && bestInstalled) {
      if (matchesEnginePattern(bestInstalled.version, pattern) || compareSemver(bestInstalled.version, extVersion) >= 0) {
        this.outputChannel.appendLine(
          `[BinaryManager] Installed engine matches pattern '${pattern}' (or >= extension version): v${bestInstalled.version} (${bestInstalled.source}) at ${bestInstalled.command}`
        );
        return {
          command: bestInstalled.command,
          args: bestInstalled.args,
          currentVersion,
          targetVersion: bestInstalled.version,
          updated: false,
          message: `CodeExplorer engine is already up to date (v${bestInstalled.version} via ${bestInstalled.source}).`,
        };
      }
    }

    this.outputChannel.appendLine('[BinaryManager] Probing remote engine releases...');
    if (bestInstalled) {
      this.outputChannel.appendLine(
        `[BinaryManager] Current best installed engine: v${bestInstalled.version} (${bestInstalled.source}) at ${bestInstalled.command}`
      );
    }

    // 2. Query remote releases for the best target
    const resolved = await probeEngineVersion(extVersion, userSetting, this.outputChannel, target.archiveName);
    const targetVersion = resolved.version;

    // 3. Version comparison: avoid redundant download if installed version is >= target version
    if (!options.forceReinstall && currentVersion) {
      const cmp = compareSemver(currentVersion, targetVersion);
      if (cmp >= 0 || isBuildDifferenceOnly(currentVersion, targetVersion)) {
        const msg = cmp === 0 || isBuildDifferenceOnly(currentVersion, targetVersion)
          ? `CodeExplorer engine is already up to date (v${currentVersion}${bestInstalled ? ` via ${bestInstalled.source}` : ''}).`
          : `Installed CodeExplorer engine (v${currentVersion}${bestInstalled ? ` via ${bestInstalled.source}` : ''}) is newer than remote release (v${targetVersion}).`;

        this.outputChannel.appendLine(
          `[BinaryManager] Installed engine (v${currentVersion}) is >= remote release (v${targetVersion}). Skipping download.`
        );

        return {
          command: bestInstalled?.command || binPath,
          args: bestInstalled?.args || [],
          currentVersion,
          targetVersion,
          updated: false,
          message: msg,
        };
      }
    }

    // 4. Remote version is higher (or forced): download and install
    await this.downloadAndExtract(resolved, target, binDir, binPath);

    const msg = currentVersion
      ? `CodeExplorer engine updated from v${currentVersion} to v${targetVersion}.`
      : `CodeExplorer engine v${targetVersion} is ready.`;

    return {
      command: binPath,
      args: [],
      currentVersion,
      targetVersion,
      updated: true,
      message: msg,
    };
  }

  private async downloadAndExtract(
    resolved: { version: string; assetUrl?: string },
    target: TargetInfo,
    binDir: string,
    binPath: string
  ): Promise<void> {
    const targetVersion = resolved.version;
    const downloadUrl = resolved.assetUrl || `https://github.com/${GITHUB_REPO}/releases/download/v${targetVersion}/${target.archiveName}`;
    this.outputChannel.appendLine(`[BinaryManager] Downloading engine v${targetVersion} from: ${downloadUrl}`);

    const runDownload = async (onProgress?: (inc: number, msg: string) => void) => {
      fs.mkdirSync(binDir, { recursive: true });
      const tempArchive = path.join(binDir, `download-${Date.now()}-${target.archiveName}`);

      let lastReportedPct = 0;
      try {
        await downloadFileWithRedirects(downloadUrl, tempArchive, (received, total) => {
          if (total > 0) {
            const pct = Math.floor((received / total) * 100);
            const mbReceived = (received / (1024 * 1024)).toFixed(1);
            const mbTotal = (total / (1024 * 1024)).toFixed(1);
            const inc = pct - lastReportedPct;
            if (inc >= 2) {
              lastReportedPct = pct;
              onProgress?.(inc, `${mbReceived} MB / ${mbTotal} MB (${pct}%)`);
            }
          }
        });

        onProgress?.(0, 'Extracting archive...');
        this.outputChannel.appendLine(`[BinaryManager] Extracting ${target.archiveName} into ${binDir}...`);

        await extractArchive(tempArchive, binDir, target.format);
        ensureExecutablePermissions(binPath, target.platform as NodeJS.Platform);

        // Save version stamp
        const verStamp = path.join(binDir, 'version.json');
        fs.writeFileSync(
          verStamp,
          JSON.stringify({ version: targetVersion, installedAt: new Date().toISOString() }, null, 2),
          'utf8'
        );

        this.outputChannel.appendLine(`[BinaryManager] Successfully installed CodeExplorer v${targetVersion} to ${binPath}`);
      } finally {
        if (fs.existsSync(tempArchive)) {
          fs.unlink(tempArchive, () => {});
        }
      }
    };

    const vscode = getVsCode();
    if (vscode?.window?.withProgress) {
      await vscode.window.withProgress(
        {
          location: vscode.ProgressLocation.Notification,
          title: `CodeExplorer: Downloading engine (v${targetVersion})...`,
          cancellable: false,
        },
        async (progress) => {
          await runDownload((inc, msg) => progress.report({ increment: inc, message: msg }));
        }
      );
    } else {
      await runDownload();
    }
  }
}
