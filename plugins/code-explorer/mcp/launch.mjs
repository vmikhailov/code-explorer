#!/usr/bin/env node
import * as cp from 'node:child_process';
import * as fs from 'node:fs';
import * as https from 'node:https';
import * as http from 'node:http';
import * as os from 'node:os';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';

const GITHUB_RELEASE_BASE = 'https://github.com/vmikhailov/code-explorer/releases/latest/download';
const EXTENSION_ID = 'vmikhailov.code-explorer-vscode';
const MCP_ARGS = ['mcp', '-q'];
const PROBE_TIMEOUT_MS = 5000;
const DOWNLOAD_TIMEOUT_MS = 60000;

function logStderr(msg) {
  process.stderr.write(`[code-explorer-mcp] ${msg}\n`);
}

function getBinName() {
  return process.platform === 'win32' ? 'ce.exe' : 'ce';
}

function resolveReleaseArchive() {
  const { platform, arch } = process;
  if (platform === 'win32') {
    return arch === 'arm64'
      ? { archive: 'ce-win-arm64.zip', format: 'zip' }
      : { archive: 'ce-win-x64.zip', format: 'zip' };
  }
  if (platform === 'darwin') {
    return arch === 'arm64'
      ? { archive: 'ce-osx-arm64.tar.gz', format: 'tar.gz' }
      : { archive: 'ce-osx-x64.tar.gz', format: 'tar.gz' };
  }
  if (platform === 'linux') {
    return arch === 'arm64'
      ? { archive: 'ce-linux-arm64.tar.gz', format: 'tar.gz' }
      : { archive: 'ce-linux-x64.tar.gz', format: 'tar.gz' };
  }
  return null;
}

function isRunnable(binPath) {
  try {
    const isWinBatch =
      process.platform === 'win32' &&
      (binPath.toLowerCase().endsWith('.cmd') || binPath.toLowerCase().endsWith('.bat'));
    const res = cp.spawnSync(binPath, ['--version'], {
      stdio: 'pipe',
      timeout: PROBE_TIMEOUT_MS,
      windowsHide: true,
      shell: isWinBatch,
    });
    return res.status === 0;
  } catch (err) {
    // Candidate binary is missing or cannot execute on this host
    return false;
  }
}

function getCandidatePaths() {
  const home = os.homedir();
  const bin = getBinName();
  const list = [];

  if (process.env.CE_EXECUTABLE) {
    list.push(process.env.CE_EXECUTABLE.trim());
  }
  list.push(path.join(home, '.dotnet', 'tools', bin));
  list.push(path.join(home, '.codeexplorer', 'bin', bin));

  const pathDirs = (process.env.PATH || '').split(path.delimiter).filter(Boolean);
  const exts = process.platform === 'win32' ? ['.exe', '.cmd', ''] : [''];
  for (const dir of pathDirs) {
    for (const ext of exts) {
      list.push(path.join(dir, `ce${ext}`));
    }
  }

  const storageRoots =
    process.platform === 'win32'
      ? [process.env.APPDATA || path.join(home, 'AppData', 'Roaming')]
      : process.platform === 'darwin'
        ? [path.join(home, 'Library', 'Application Support')]
        : [path.join(home, '.config')];
  const ideNames = ['Code', 'Cursor', 'Antigravity', 'Windsurf', 'Code - Insiders'];
  for (const root of storageRoots) {
    for (const ide of ideNames) {
      list.push(path.join(root, ide, 'User', 'globalStorage', EXTENSION_ID, 'bin', bin));
    }
  }

  return list;
}

function downloadFile(urlStr, destPath) {
  return new Promise((resolve, reject) => {
    const client = urlStr.startsWith('https:') ? https : http;
    const req = client.get(
      urlStr,
      { headers: { 'User-Agent': 'CodeExplorer-Plugin-Launcher' } },
      (res) => {
        if (res.statusCode && [301, 302, 307, 308].includes(res.statusCode) && res.headers.location) {
          res.resume();
          downloadFile(res.headers.location, destPath).then(resolve, reject);
          return;
        }
        if (res.statusCode !== 200) {
          res.resume();
          reject(new Error(`HTTP ${res.statusCode} while downloading ${urlStr}`));
          return;
        }
        const out = fs.createWriteStream(destPath);
        res.pipe(out);
        out.on('finish', () => out.close(() => resolve()));
        out.on('error', (err) => {
          fs.rmSync(destPath, { force: true });
          reject(err);
        });
      }
    );
    req.on('error', reject);
    req.setTimeout(DOWNLOAD_TIMEOUT_MS, () => {
      req.destroy(new Error('Download timed out'));
    });
  });
}

function extractArchive(archivePath, destDir, format) {
  if (format === 'zip' && process.platform === 'win32') {
    const tarRes = cp.spawnSync('tar.exe', ['-xf', archivePath, '-C', destDir], { windowsHide: true });
    if (tarRes.status === 0) return;
    const psRes = cp.spawnSync(
      'powershell.exe',
      ['-NoProfile', '-NonInteractive', '-Command', 'Expand-Archive -LiteralPath $args[0] -DestinationPath $args[1] -Force', archivePath, destDir],
      { windowsHide: true }
    );
    if (psRes.status !== 0) {
      throw new Error('Failed to extract zip archive');
    }
    return;
  }
  const res = cp.spawnSync('tar', ['-xzf', archivePath, '-C', destDir], { windowsHide: true });
  if (res.status !== 0) {
    throw new Error('Failed to extract tar.gz archive');
  }
}

async function installManagedBinary() {
  const asset = resolveReleaseArchive();
  const destDir = path.join(os.homedir(), '.codeexplorer', 'bin');
  const targetBin = path.join(destDir, getBinName());
  fs.mkdirSync(destDir, { recursive: true });

  if (asset) {
    const archivePath = path.join(destDir, asset.archive);
    try {
      logStderr(`Downloading CodeExplorer engine (${asset.archive})...`);
      await downloadFile(`${GITHUB_RELEASE_BASE}/${asset.archive}`, archivePath);
      extractArchive(archivePath, destDir, asset.format);
      fs.rmSync(archivePath, { force: true });
      if (process.platform !== 'win32') {
        fs.chmodSync(targetBin, 0o755);
      }
      if (isRunnable(targetBin)) {
        return targetBin;
      }
    } catch (err) {
      fs.rmSync(archivePath, { force: true });
      logStderr(`Direct release download fallback: ${err?.message || err}`);
    }
  }

  logStderr('Attempting fallback install via dotnet tool (codeexplorer.cli)...');
  const dotnetRes = cp.spawnSync('dotnet', ['tool', 'install', '-g', 'codeexplorer.cli'], {
    stdio: ['ignore', 'ignore', 'pipe'],
    windowsHide: true,
  });
  const dotnetBin = path.join(os.homedir(), '.dotnet', 'tools', getBinName());
  if ((dotnetRes.status === 0 || fs.existsSync(dotnetBin)) && isRunnable(dotnetBin)) {
    return dotnetBin;
  }

  throw new Error(
    'Unable to locate or download CodeExplorer (ce). Install via `brew tap vmikhailov/tap && brew install ce` or `dotnet tool install -g codeexplorer.cli`.'
  );
}

function resolveWorkingDirectory() {
  const pluginDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
  const cwd = process.cwd();
  if (path.resolve(cwd).startsWith(pluginDir)) {
    const envRoot = process.env.WORKSPACE_ROOT || process.env.PWD || process.env.INIT_CWD;
    if (envRoot && !path.resolve(envRoot).startsWith(pluginDir) && fs.existsSync(envRoot)) {
      return envRoot;
    }
  }
  return cwd;
}

async function main() {
  let binary = getCandidatePaths().find((candidate) => fs.existsSync(candidate) && isRunnable(candidate));
  if (!binary) {
    binary = await installManagedBinary();
  }

  const workDir = resolveWorkingDirectory();
  const isWinBatch =
    process.platform === 'win32' &&
    (binary.toLowerCase().endsWith('.cmd') || binary.toLowerCase().endsWith('.bat'));
  const child = cp.spawn(binary, MCP_ARGS, {
    cwd: workDir,
    stdio: 'inherit',
    windowsHide: true,
    shell: isWinBatch,
  });

  const forwardSignal = (sig) => {
    if (!child.killed) {
      child.kill(sig);
    }
  };
  process.on('SIGINT', () => forwardSignal('SIGINT'));
  process.on('SIGTERM', () => forwardSignal('SIGTERM'));
  child.on('exit', (code) => process.exit(code ?? 0));
  child.on('error', (err) => {
    logStderr(`Failed to launch ${binary}: ${err.message}`);
    process.exit(1);
  });
}

main().catch((err) => {
  logStderr(err?.message || String(err));
  process.exit(1);
});
