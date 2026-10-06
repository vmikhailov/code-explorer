import * as fs from 'fs';
import * as path from 'path';
import * as cp from 'child_process';
import { ArchiveFormat, TargetInfo } from './types';

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

  // Fallback default
  return {
    platform,
    arch,
    rid: `${platform}-${arch}`,
    archiveName: `ce-${platform}-${arch}.tar.gz`,
    binName: 'ce',
    format: 'tar.gz',
  };
}
/**
 * Returns the binary executable name for the specified platform (e.g. 'ce.exe' on win32, 'ce' on POSIX).
 */
export function getExecutableName(
  baseName: string = 'ce',
  platform: NodeJS.Platform = process.platform
): string {
  return platform === 'win32' ? `${baseName}.exe` : baseName;
}

/**
 * Executes a file with the given arguments array, returning a promise.
 * Avoids shell injection vulnerabilities and handles paths with spaces cleanly.
 */
function execFileAsync(file: string, args: string[]): Promise<void> {
  return new Promise((resolve, reject) => {
    cp.execFile(file, args, { windowsHide: true }, (err, _stdout, stderr) => {
      if (err) {
        reject(new Error(stderr?.trim() || err.message));
      } else {
        resolve();
      }
    });
  });
}

/**
 * Extracts a zip or tar.gz archive into the destination directory.
 * Cross-platform support:
 * - On Windows (.zip): attempts built-in tar.exe first, then powershell Expand-Archive with safe argument passing.
 * - On Unix (.zip): attempts unzip, then tar (bsdtar), then python3 -m zipfile.
 * - On any platform (.tar.gz): uses system tar (or tar.exe on Windows).
 */
export async function extractArchive(
  archivePath: string,
  destDir: string,
  format: ArchiveFormat,
  platform: NodeJS.Platform = process.platform
): Promise<void> {
  fs.mkdirSync(destDir, { recursive: true });

  if (format === 'zip') {
    if (platform === 'win32') {
      // 1. Try Windows built-in tar.exe (fastest; available on Win 10 build 17063+ & Win 11)
      try {
        await execFileAsync('tar.exe', ['-xf', archivePath, '-C', destDir]);
        return;
      } catch {
        // Fall back to PowerShell
      }

      // 2. PowerShell Expand-Archive (built into all modern Windows releases)
      // Arguments passed safely via $args array without shell string interpolation
      try {
        await execFileAsync('powershell.exe', [
          '-NoProfile',
          '-NonInteractive',
          '-ExecutionPolicy',
          'Bypass',
          '-Command',
          'Expand-Archive -LiteralPath $args[0] -DestinationPath $args[1] -Force',
          archivePath,
          destDir,
        ]);
        return;
      } catch (err: any) {
        throw new Error(`Failed to extract zip archive on Windows: ${err.message}`);
      }
    } else {
      // Non-Windows (macOS, Linux):
      // 1. Try standard unzip
      try {
        await execFileAsync('unzip', ['-q', '-o', archivePath, '-d', destDir]);
        return;
      } catch {
        // Fall back to tar
      }

      // 2. Try tar (bsdtar on macOS and modern Linux tar can extract zip)
      try {
        await execFileAsync('tar', ['-xf', archivePath, '-C', destDir]);
        return;
      } catch {
        // Fall back to python3 zipfile module
      }

      // 3. Try python3 -m zipfile -e
      try {
        await execFileAsync('python3', ['-m', 'zipfile', '-e', archivePath, destDir]);
        return;
      } catch {
        // Fall back to python
      }

      try {
        await execFileAsync('python', ['-m', 'zipfile', '-e', archivePath, destDir]);
        return;
      } catch (err: any) {
        throw new Error(`Failed to extract zip archive on ${platform}: neither unzip, tar, nor python3 were able to extract it.`);
      }
    }
  } else {
    // format === 'tar.gz'
    const tarCmd = platform === 'win32' ? 'tar.exe' : 'tar';
    try {
      await execFileAsync(tarCmd, ['-xzf', archivePath, '-C', destDir]);
      return;
    } catch {
      // Fallback: some tar implementations auto-detect gzip with -xf
      try {
        await execFileAsync(tarCmd, ['-xf', archivePath, '-C', destDir]);
        return;
      } catch (err: any) {
        throw new Error(`Failed to extract tar.gz archive: ${err.message}`);
      }
    }
  }
}

/**
 * Sets executable permissions (0o755) on Unix binaries.
 */
export function ensureExecutablePermissions(
  binPath: string,
  platform: NodeJS.Platform = process.platform
): void {
  if (platform !== 'win32' && fs.existsSync(binPath)) {
    try {
      fs.chmodSync(binPath, 0o755);
    } catch {
      // Ignore if chmod fails (e.g. read-only volume)
    }
  }
}

/**
 * Resolves all candidate paths for an executable command by searching directories in PATH.
 * Cross-platform: works without external utilities (which/where.exe), but falls back to them if needed.
 */
export function findInPath(
  commandName: string,
  envPath?: string,
  platform: NodeJS.Platform = process.platform
): string[] {
  const results: string[] = [];
  const rawPath = envPath !== undefined ? envPath : (process.env.PATH || process.env.Path || '');
  if (!rawPath) return results;

  const delimiter = platform === 'win32' ? ';' : ':';
  const dirs = rawPath
    .split(delimiter)
    .map((d) => d.trim().replace(/^"(.*)"$/, '$1'))
    .filter(Boolean);

  const extensions = platform === 'win32'
    ? (process.env.PATHEXT ? process.env.PATHEXT.split(';') : ['.EXE', '.CMD', '.BAT', '.COM'])
    : [''];

  for (const dir of dirs) {
    try {
      const hasExt = platform === 'win32' && extensions.some((ext) => commandName.toUpperCase().endsWith(ext.toUpperCase()));
      if (hasExt || platform !== 'win32') {
        const fullPath = path.join(dir, commandName);
        if (fs.existsSync(fullPath)) {
          if (platform === 'win32') {
            results.push(fullPath);
          } else {
            try {
              fs.accessSync(fullPath, fs.constants.X_OK);
              results.push(fullPath);
            } catch {
              // Not executable
            }
          }
        }
      } else {
        // Windows without extension: try appending extensions
        for (const ext of extensions) {
          const candidate = path.join(dir, `${commandName}${ext.toLowerCase()}`);
          if (fs.existsSync(candidate)) {
            results.push(candidate);
          }
          const upperCandidate = path.join(dir, `${commandName}${ext.toUpperCase()}`);
          if (upperCandidate !== candidate && fs.existsSync(upperCandidate)) {
            results.push(upperCandidate);
          }
        }
      }
    } catch {
      // Ignore directory access errors
    }
  }

  // Fallback to system command (where.exe / which) if direct PATH search found nothing
  if (results.length === 0) {
    try {
      const tool = platform === 'win32' ? 'where.exe' : 'which';
      const res = cp.spawnSync(tool, [commandName], {
        encoding: 'utf8',
        timeout: 4000,
        windowsHide: true,
      });
      if (res.status === 0 && res.stdout?.trim()) {
        const lines = res.stdout.trim().split(/\r?\n/);
        for (const line of lines) {
          const p = line.trim();
          if (p && fs.existsSync(p)) {
            results.push(p);
          }
        }
      }
    } catch {
      // Ignore
    }
  }

  // Return unique results
  return Array.from(new Set(results));
}
