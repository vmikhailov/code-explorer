import type * as vscodeTypes from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import * as https from 'https';
import * as http from 'http';
import * as cp from 'child_process';
import { URL } from 'url';

function getVsCode(): typeof import('vscode') | undefined {
  try {
    return require('vscode');
  } catch {
    return undefined;
  }
}

export interface TargetInfo {
  platform: string;
  arch: string;
  rid: string;
  archiveName: string;
  binName: string;
  format: 'zip' | 'tar.gz';
}

export interface EngineVersionConfig {
  pattern: string;
  fallback: string;
}

export const GITHUB_REPO = 'vmikhailov/code-explorer';

/**
 * Resolves the target platform and asset archive name for the current host.
 */
export function resolveTargetAsset(
  platform: NodeJS.Platform = process.platform,
  arch: string = process.arch
): TargetInfo {
  if (platform === 'win32') {
    if (arch === 'arm64') {
      return {
        platform,
        arch,
        rid: 'win-arm64',
        archiveName: 'ce-win-arm64.zip',
        binName: 'ce.exe',
        format: 'zip',
      };
    }
    return {
      platform,
      arch,
      rid: 'win-x64',
      archiveName: 'ce-win-x64.zip',
      binName: 'ce.exe',
      format: 'zip',
    };
  }

  if (platform === 'darwin') {
    if (arch === 'arm64') {
      return {
        platform,
        arch,
        rid: 'osx-arm64',
        archiveName: 'ce-osx-arm64.tar.gz',
        binName: 'ce',
        format: 'tar.gz',
      };
    }
    return {
      platform,
      arch,
      rid: 'osx-x64',
      archiveName: 'ce-osx-x64.tar.gz',
      binName: 'ce',
      format: 'tar.gz',
    };
  }

  if (platform === 'linux') {
    if (arch === 'arm64') {
      return {
        platform,
        arch,
        rid: 'linux-arm64',
        archiveName: 'ce-linux-arm64.tar.gz',
        binName: 'ce',
        format: 'tar.gz',
      };
    }
    return {
      platform,
      arch,
      rid: 'linux-x64',
      archiveName: 'ce-linux-x64.tar.gz',
      binName: 'ce',
      format: 'tar.gz',
    };
  }

  throw new Error(`Unsupported operating system or architecture: ${platform}-${arch}`);
}

export interface ParsedSemver {
  major: number;
  minor: number;
  patch: number;
  buildNumber?: number;
  prerelease?: string;
  buildMetadata?: string;
  raw: string;
}

/**
 * Parses a version string into a structured SemVer representation.
 * Handles prefixes ("v", "ce ", "ce-"), build metadata ("+..."),
 * prereleases ("-..."), and 4-part .NET assembly versions.
 */
export function parseSemver(version: string): ParsedSemver | null {
  if (!version || typeof version !== 'string') return null;

  const trimmed = version.trim();
  // Strip leading non-digits (e.g. "v1.22.4", "ce 1.22.4", "ce-1.22.4")
  const cleaned = trimmed.replace(/^[^0-9]*/, '');
  if (!cleaned) return null;

  let buildMetadata: string | undefined;
  let withoutBuild = cleaned;
  const plusIdx = cleaned.indexOf('+');
  if (plusIdx !== -1) {
    buildMetadata = cleaned.slice(plusIdx + 1);
    withoutBuild = cleaned.slice(0, plusIdx);
  }

  let prerelease: string | undefined;
  let core = withoutBuild;
  const dashIdx = withoutBuild.indexOf('-');
  if (dashIdx !== -1) {
    prerelease = withoutBuild.slice(dashIdx + 1);
    core = withoutBuild.slice(0, dashIdx);
  }

  const parts = core.split('.').map((p) => parseInt(p, 10));
  if (isNaN(parts[0])) return null;

  const major = parts[0];
  const minor = parts.length > 1 && !isNaN(parts[1]) ? parts[1] : 0;
  const patch = parts.length > 2 && !isNaN(parts[2]) ? parts[2] : 0;
  const buildNumber = parts.length > 3 && !isNaN(parts[3]) ? parts[3] : undefined;

  return {
    major,
    minor,
    patch,
    buildNumber,
    prerelease,
    buildMetadata,
    raw: trimmed,
  };
}

/**
 * Normalizes a version string to clean canonical semver "MAJOR.MINOR.PATCH[-PRERELEASE]".
 * Strips build metadata (+...) and normalizes 4-part assembly versions.
 */
export function cleanSemver(version: string): string {
  const p = parseSemver(version);
  if (!p) return version.replace(/^v/, '').trim();
  const pre = p.prerelease ? `-${p.prerelease}` : '';
  return `${p.major}.${p.minor}.${p.patch}${pre}`;
}

/**
 * Returns true if two versions differ ONLY in build metadata (+...),
 * 4th component build/revision numbers (.x), or prerelease tags,
 * while having identical core major, minor, and patch numbers.
 */
export function isBuildDifferenceOnly(a: string, b: string): boolean {
  const pa = parseSemver(a);
  const pb = parseSemver(b);
  if (!pa || !pb) return false;

  return pa.major === pb.major && pa.minor === pb.minor && pa.patch === pb.patch;
}

/**
 * Compares two semver strings according to SemVer 2.0.0 specification.
 * Build metadata (+...) is completely ignored per SemVer Section 10.
 * Returns > 0 if a > b, < 0 if a < b, 0 if equal.
 */
export function compareSemver(a: string, b: string): number {
  const pa = parseSemver(a);
  const pb = parseSemver(b);

  if (!pa && !pb) return 0;
  if (!pa) return -1;
  if (!pb) return 1;

  if (pa.major !== pb.major) return pa.major - pb.major;
  if (pa.minor !== pb.minor) return pa.minor - pb.minor;
  if (pa.patch !== pb.patch) return pa.patch - pb.patch;

  // If 4th component (build number) is present on either, compare it
  const bna = pa.buildNumber ?? 0;
  const bnb = pb.buildNumber ?? 0;
  if (bna !== bnb) return bna - bnb;

  // Prerelease comparison:
  // If neither has prerelease, they are equal (build metadata is ignored!)
  if (!pa.prerelease && !pb.prerelease) return 0;
  // A version with prerelease has lower precedence than a normal version (Section 9)
  if (!pa.prerelease && pb.prerelease) return 1;
  if (pa.prerelease && !pb.prerelease) return -1;

  // Both have prereleases: compare dot-separated identifiers
  const preA = pa.prerelease!.split('.');
  const preB = pb.prerelease!.split('.');
  for (let i = 0; i < Math.max(preA.length, preB.length); i++) {
    const p1 = preA[i];
    const p2 = preB[i];
    if (p1 === undefined) return -1;
    if (p2 === undefined) return 1;
    if (p1 === p2) continue;

    const num1 = parseInt(p1, 10);
    const num2 = parseInt(p2, 10);
    const isNum1 = !isNaN(num1) && String(num1) === p1;
    const isNum2 = !isNaN(num2) && String(num2) === p2;

    if (isNum1 && isNum2) {
      if (num1 !== num2) return num1 - num2;
    } else if (isNum1) {
      return -1;
    } else if (isNum2) {
      return 1;
    } else {
      return p1.localeCompare(p2);
    }
  }

  return 0;
}

/**
 * Checks if a version satisfies a semver pattern or requirement.
 * Tolerant to build differences (+build metadata, 4th segment build numbers, prerelease tags).
 * Respects SemVer 2.0.0 rules for caret (^), tilde (~), wildcards (*), and comparison ranges.
 */
export function matchesEnginePattern(version: string, pattern: string): boolean {
  if (!pattern || pattern === '*' || pattern === 'latest') {
    return true;
  }

  const vParsed = parseSemver(version);
  if (!vParsed) return false;

  const cleanPattern = pattern.trim().replace(/^v/, '');

  // 1. Wildcard patterns: e.g. "1.22.*", "1.22.x", "1.*", "1.x"
  if (cleanPattern.endsWith('.*') || cleanPattern.endsWith('.x')) {
    const prefix = cleanPattern.slice(0, -2);
    const prefixParts = prefix.split('.').map((p) => parseInt(p, 10));
    if (prefixParts.length === 1 && !isNaN(prefixParts[0])) {
      return vParsed.major === prefixParts[0];
    }
    if (prefixParts.length >= 2 && !isNaN(prefixParts[0]) && !isNaN(prefixParts[1])) {
      return vParsed.major === prefixParts[0] && vParsed.minor === prefixParts[1];
    }
  }

  // 2. Caret pattern: e.g. "^1.22.0"
  // Per SemVer: allows changes that do not modify the leftmost non-zero element.
  // For major >= 1: >= BASE < (MAJOR + 1).0.0
  if (cleanPattern.startsWith('^')) {
    const baseStr = cleanPattern.slice(1);
    const baseParsed = parseSemver(baseStr);
    if (!baseParsed) return false;

    // Major version mismatch is a breaking change
    if (vParsed.major !== baseParsed.major) {
      return false;
    }

    if (baseParsed.major > 0) {
      // Same major version (>= 1):
      // A higher minor version is backwards-compatible in SemVer
      if (vParsed.minor > baseParsed.minor) {
        return true;
      }
      if (vParsed.minor === baseParsed.minor) {
        if (vParsed.patch >= baseParsed.patch) {
          return true;
        }
        return isBuildDifferenceOnly(version, baseStr);
      }
      return false;
    } else {
      // Major is 0: ^0.X.Y
      if (baseParsed.minor > 0) {
        return (
          vParsed.minor === baseParsed.minor &&
          (vParsed.patch >= baseParsed.patch || isBuildDifferenceOnly(version, baseStr))
        );
      } else {
        return vParsed.minor === 0 && vParsed.patch === baseParsed.patch;
      }
    }
  }

  // 3. Tilde pattern: e.g. "~1.22.0"
  // Per SemVer: allows patch-level changes if minor is specified: >= BASE < (MAJOR).(MINOR+1).0
  if (cleanPattern.startsWith('~')) {
    const baseStr = cleanPattern.slice(1);
    const baseParsed = parseSemver(baseStr);
    if (!baseParsed) return false;

    if (vParsed.major !== baseParsed.major || vParsed.minor !== baseParsed.minor) {
      return false;
    }
    return vParsed.patch >= baseParsed.patch || isBuildDifferenceOnly(version, baseStr);
  }

  // 4. Comparison operator ranges: e.g. ">=1.20.0", "<2.0.0"
  if (cleanPattern.startsWith('>=')) {
    return compareSemver(version, cleanPattern.slice(2)) >= 0;
  }
  if (cleanPattern.startsWith('>')) {
    return compareSemver(version, cleanPattern.slice(1)) > 0;
  }
  if (cleanPattern.startsWith('<=')) {
    return compareSemver(version, cleanPattern.slice(2)) <= 0;
  }
  if (cleanPattern.startsWith('<')) {
    return compareSemver(version, cleanPattern.slice(1)) < 0;
  }

  // 5. Exact version pattern: e.g. "1.22.4" or "=1.22.4"
  const targetExact = cleanPattern.startsWith('=') ? cleanPattern.slice(1) : cleanPattern;
  const targetParsed = parseSemver(targetExact);
  if (!targetParsed) return false;

  // Exact semver comparison ignores build metadata per SemVer Section 10
  if (compareSemver(version, targetExact) === 0) {
    return true;
  }

  // Tolerant to build differences (e.g. 1.22.4 vs 1.22.4.0 or 1.22.4+hash or 1.22.4-dev)
  return isBuildDifferenceOnly(version, targetExact);
}

/**
 * Derives the engine probing pattern and fallback version from the extension version.
 * If user explicitly set codeExplorer.engineVersion, that takes precedence.
 * Otherwise, dynamically generates `^${major}.${minor}.0` with fallback to clean `extensionVersion`.
 */
export function getEngineConfigFromExtensionVersion(
  extensionVersion: string,
  userSetting?: string
): EngineVersionConfig {
  const cleanExtVersion = cleanSemver(extensionVersion);
  const parsed = parseSemver(cleanExtVersion);
  const major = parsed ? parsed.major : 1;
  const minor = parsed ? parsed.minor : 0;

  if (userSetting && userSetting.trim().length > 0) {
    return {
      pattern: userSetting.trim(),
      fallback: cleanExtVersion,
    };
  }

  return {
    pattern: `^${major}.${minor}.0`,
    fallback: cleanExtVersion,
  };
}

/**
 * Probes the GitHub releases API to find the best engine version for the given host platform.
 * Verifies that candidate releases actually contain the required binary asset.
 */
export async function probeEngineVersion(
  extensionVersion: string,
  userSetting?: string,
  outputChannel?: vscodeTypes.OutputChannel,
  targetArchiveName?: string
): Promise<{ version: string; assetUrl?: string }> {
  const log = (msg: string) => outputChannel?.appendLine(`[BinaryManager] ${msg}`);
  const { pattern, fallback } = getEngineConfigFromExtensionVersion(extensionVersion, userSetting);
  const cleanExt = extensionVersion.replace(/^v/, '').trim();
  const extMajor = cleanExt.split('.')[0];
  const requiredAsset = targetArchiveName || resolveTargetAsset().archiveName;

  log(`Probing releases for engine: extension v${cleanExt}, pattern '${pattern}', target asset '${requiredAsset}'`);

  try {
    const releases = await fetchGitHubReleases();

    interface ReleaseMatch {
      version: string;
      assetUrl: string;
    }
    const availableWithAsset: ReleaseMatch[] = [];

    for (const rel of releases) {
      if (rel.draft || rel.prerelease) continue;
      const tag = rel.tag_name || '';
      const ver = tag.replace(/^v/, '').trim();
      if (!ver || !/^\d+\.\d+/.test(ver)) continue;

      const matchingAsset = (rel.assets || []).find((a: any) => a.name === requiredAsset);
      if (matchingAsset && matchingAsset.browser_download_url) {
        availableWithAsset.push({
          version: ver,
          assetUrl: matchingAsset.browser_download_url,
        });
      }
    }

    if (availableWithAsset.length > 0) {
      availableWithAsset.sort((a, b) => compareSemver(a.version, b.version));

      // Tier 1: Exact pattern matches (e.g. major.minor.*)
      const patternMatches = availableWithAsset.filter((r) => matchesEnginePattern(r.version, pattern));
      if (patternMatches.length > 0) {
        const best = patternMatches[patternMatches.length - 1];
        log(`Smart probe tier 1: found release v${best.version} matching '${pattern}'`);
        return best;
      }

      // Tier 2: Same major version (e.g. 1.x)
      if (extMajor) {
        const sameMajor = availableWithAsset.filter((r) => r.version.startsWith(`${extMajor}.`));
        if (sameMajor.length > 0) {
          const best = sameMajor[sameMajor.length - 1];
          log(`Smart probe tier 2: found release v${best.version} in major series ${extMajor}.x`);
          return best;
        }
      }

      // Tier 3: Highest available release containing asset
      const latest = availableWithAsset[availableWithAsset.length - 1];
      log(`Smart probe tier 3: using latest available release v${latest.version}`);
      return latest;
    }

    log(`No release found containing asset '${requiredAsset}'. Using fallback: v${fallback}`);
  } catch (err: any) {
    log(`Smart probe failed (${err?.message || err}). Falling back to extension version v${fallback}`);
  }

  const fallbackUrl = `https://github.com/${GITHUB_REPO}/releases/download/v${fallback}/${requiredAsset}`;
  return { version: fallback, assetUrl: fallbackUrl };
}

/**
 * Fetches releases list from GitHub API.
 */
function fetchGitHubReleases(): Promise<any[]> {
  return new Promise((resolve, reject) => {
    const options: https.RequestOptions = {
      hostname: 'api.github.com',
      path: `/repos/${GITHUB_REPO}/releases?per_page=30`,
      method: 'GET',
      headers: {
        'User-Agent': 'CodeExplorer-VSCode-Extension',
        'Accept': 'application/vnd.github.v3+json',
      },
    };

    const req = https.request(options, (res) => {
      if (res.statusCode && (res.statusCode < 200 || res.statusCode >= 300)) {
        return reject(new Error(`GitHub API returned status ${res.statusCode}`));
      }

      let data = '';
      res.on('data', (chunk) => {
        data += chunk;
      });
      res.on('end', () => {
        try {
          const parsed = JSON.parse(data);
          if (Array.isArray(parsed)) {
            resolve(parsed);
          } else {
            reject(new Error('Unexpected GitHub API response structure'));
          }
        } catch (e) {
          reject(e);
        }
      });
    });

    req.on('error', reject);
    req.setTimeout(6000, () => {
      req.destroy();
      reject(new Error('GitHub API request timed out'));
    });
    req.end();
  });
}

/**
 * Downloads a file from a URL, following 301/302 redirects.
 */
function downloadFileWithRedirects(
  downloadUrl: string,
  destPath: string,
  onProgress?: (receivedBytes: number, totalBytes: number) => void
): Promise<void> {
  return new Promise((resolve, reject) => {
    const parsed = new URL(downloadUrl);
    const client = parsed.protocol === 'https:' ? https : http;

    const request = client.get(
      downloadUrl,
      {
        headers: {
          'User-Agent': 'CodeExplorer-VSCode-Extension',
          'Accept': 'application/octet-stream',
        },
      },
      (response) => {
        // Follow redirects
        if (
          response.statusCode &&
          [301, 302, 307, 308].includes(response.statusCode) &&
          response.headers.location
        ) {
          return downloadFileWithRedirects(response.headers.location, destPath, onProgress)
            .then(resolve)
            .catch(reject);
        }

        if (response.statusCode && (response.statusCode < 200 || response.statusCode >= 300)) {
          return reject(new Error(`Download failed with status: ${response.statusCode}`));
        }

        const totalBytes = parseInt(response.headers['content-length'] || '0', 10);
        let receivedBytes = 0;

        const fileStream = fs.createWriteStream(destPath);
        response.on('data', (chunk) => {
          receivedBytes += chunk.length;
          onProgress?.(receivedBytes, totalBytes);
        });

        response.pipe(fileStream);

        fileStream.on('finish', () => {
          fileStream.close();
          resolve();
        });

        fileStream.on('error', (err) => {
          fs.unlink(destPath, () => {});
          reject(err);
        });
      }
    );

    request.on('error', (err) => {
      fs.unlink(destPath, () => {});
      reject(err);
    });

    request.setTimeout(60000, () => {
      request.destroy();
      fs.unlink(destPath, () => {});
      reject(new Error('Download timed out after 60s'));
    });
  });
}

/**
 * Extracts a zip or tar.gz archive to the destination directory.
 */
export function extractArchive(
  archivePath: string,
  destDir: string,
  format: 'zip' | 'tar.gz'
): Promise<void> {
  return new Promise((resolve, reject) => {
    fs.mkdirSync(destDir, { recursive: true });

    if (format === 'zip') {
      // Use PowerShell Expand-Archive (built into all modern Windows releases)
      const command = `powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "Expand-Archive -LiteralPath '${archivePath}' -DestinationPath '${destDir}' -Force"`;
      cp.exec(command, (err, stdout, stderr) => {
        if (err) {
          return reject(new Error(`Failed to extract zip archive: ${stderr || err.message}`));
        }
        resolve();
      });
    } else {
      // Use standard system tar (available on macOS and Linux)
      const command = `tar -xzf "${archivePath}" -C "${destDir}"`;
      cp.exec(command, (err, stdout, stderr) => {
        if (err) {
          return reject(new Error(`Failed to extract tar.gz archive: ${stderr || err.message}`));
        }
        resolve();
      });
    }
  });
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

  setContext(context: vscodeTypes.ExtensionContext) {
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
      const fallbackPkg = path.resolve(__dirname, '../package.json');
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
   * Ensures the binary is available and matches the required version.
   * If missing or outdated, downloads and extracts it with a VS Code progress notification.
   */
  async ensureBinary(forceUpdate = false): Promise<{ command: string; args: string[] }> {
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

    const cachedVersion = this.getCachedVersion();

    // If local binary exists and satisfies the required pattern, use it immediately
    if (!forceUpdate && fs.existsSync(binPath) && cachedVersion && matchesEnginePattern(cachedVersion, pattern)) {
      this.outputChannel.appendLine(
        `[BinaryManager] Stored engine matches pattern '${pattern}': v${cachedVersion} at ${binPath}`
      );
      return { command: binPath, args: [] };
    }

    // Smart probe for the best available engine release containing the platform asset
    const resolved = await probeEngineVersion(extVersion, userSetting, this.outputChannel, target.archiveName);
    const targetVersion = resolved.version;

    if (!forceUpdate && fs.existsSync(binPath) && cachedVersion === targetVersion) {
      this.outputChannel.appendLine(`[BinaryManager] Engine already at target version v${targetVersion}.`);
      return { command: binPath, args: [] };
    }

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

        // Ensure executable permissions on Unix platforms
        if (target.platform !== 'win32' && fs.existsSync(binPath)) {
          try {
            fs.chmodSync(binPath, 0o755);
          } catch (e: any) {
            this.outputChannel.appendLine(`[BinaryManager] Warning: chmod failed: ${e?.message}`);
          }
        }

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
      vscode.window.showInformationMessage(`CodeExplorer engine v${targetVersion} is ready.`);
    } else {
      await runDownload();
    }

    return { command: binPath, args: [] };
  }
}
