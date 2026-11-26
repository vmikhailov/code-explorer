#!/usr/bin/env node
import { createHash } from 'node:crypto';
import * as cp from 'node:child_process';
import * as fs from 'node:fs';
import * as http from 'node:http';
import * as https from 'node:https';
import * as os from 'node:os';
import * as path from 'node:path';

const GITHUB_REPO = 'vmikhailov/code-explorer';
const EXTENSION_ID = 'vmikhailov.code-explorer-vscode';
const MCP_ARGS = ['mcp', '-q', ...process.argv.slice(2)];
const PROBE_TIMEOUT_MS = 5000;
const DOWNLOAD_TIMEOUT_MS = 60000;

function logStderr(msg) {
  process.stderr.write(`[code-explorer] ${msg}\n`);
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
  } catch {
    return false;
  }
}

function getManagedBinDir() {
  if (process.env.PLUGIN_DATA) {
    return path.join(process.env.PLUGIN_DATA, 'bin');
  }
  const home = os.homedir();
  if (process.platform === 'win32') {
    const localAppData = process.env.LOCALAPPDATA || path.join(home, 'AppData', 'Local');
    return path.join(localAppData, 'CodeExplorer', 'bin');
  }
  return path.join(home, '.local', 'share', 'codeexplorer', 'bin');
}

function getCandidatePaths() {
  const home = os.homedir();
  const bin = getBinName();
  const list = [];

  for (const envVar of ['CE_EXECUTABLE', 'CODEEXPLORER_BIN']) {
    if (process.env[envVar]) {
      list.push(process.env[envVar].trim());
    }
  }

  const managedDir = getManagedBinDir();
  const markerPath = path.join(managedDir, 'current-version.txt');
  if (fs.existsSync(markerPath)) {
    const tag = fs.readFileSync(markerPath, 'utf8').trim();
    if (tag) {
      list.push(path.join(managedDir, tag, bin));
    }
  }
  list.push(path.join(managedDir, bin));
  list.push(path.join(home, '.dotnet', 'tools', bin));
  list.push(path.join(home, '.local', 'bin', bin));
  list.push(path.join(home, '.codeexplorer', 'bin', bin));
  if (process.platform !== 'win32') {
    list.push('/opt/homebrew/bin/ce', '/usr/local/bin/ce');
  }

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

function httpGetBuffer(urlStr, redirectsLeft = 5) {
  return new Promise((resolve, reject) => {
    if (redirectsLeft < 0) {
      reject(new Error(`Too many redirects fetching ${urlStr}`));
      return;
    }
    const client = urlStr.startsWith('https:') ? https : http;
    const req = client.get(
      urlStr,
      {
        headers: {
          'User-Agent': 'CodeExplorer-Plugin-Launcher/1.0',
          Accept: 'application/vnd.github+json, application/octet-stream, */*',
        },
      },
      (res) => {
        if (res.statusCode && [301, 302, 307, 308].includes(res.statusCode) && res.headers.location) {
          res.resume();
          httpGetBuffer(res.headers.location, redirectsLeft - 1).then(resolve, reject);
          return;
        }
        if (res.statusCode !== 200) {
          res.resume();
          reject(new Error(`HTTP ${res.statusCode} fetching ${urlStr}`));
          return;
        }
        const chunks = [];
        res.on('data', (c) => chunks.push(c));
        res.on('end', () => resolve(Buffer.concat(chunks)));
      }
    );
    req.on('error', reject);
    req.setTimeout(DOWNLOAD_TIMEOUT_MS, () => req.destroy(new Error('Request timed out')));
  });
}

function downloadFile(urlStr, destPath, redirectsLeft = 5) {
  return new Promise((resolve, reject) => {
    if (redirectsLeft < 0) {
      reject(new Error(`Too many redirects downloading ${urlStr}`));
      return;
    }
    const client = urlStr.startsWith('https:') ? https : http;
    const req = client.get(
      urlStr,
      { headers: { 'User-Agent': 'CodeExplorer-Plugin-Launcher/1.0' } },
      (res) => {
        if (res.statusCode && [301, 302, 307, 308].includes(res.statusCode) && res.headers.location) {
          res.resume();
          downloadFile(res.headers.location, destPath, redirectsLeft - 1).then(resolve, reject);
          return;
        }
        if (res.statusCode !== 200) {
          res.resume();
          reject(new Error(`HTTP ${res.statusCode} downloading ${urlStr}`));
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
    req.setTimeout(DOWNLOAD_TIMEOUT_MS, () => req.destroy(new Error('Download timed out')));
  });
}

function sha256File(filePath) {
  const hash = createHash('sha256');
  hash.update(fs.readFileSync(filePath));
  return hash.digest('hex').toLowerCase();
}

function extractArchive(archivePath, destDir, format) {
  fs.mkdirSync(destDir, { recursive: true });
  if (format === 'zip' && process.platform === 'win32') {
    const tarRes = cp.spawnSync('tar.exe', ['-xf', archivePath, '-C', destDir], { windowsHide: true });
    if (tarRes.status === 0) return;
    const psRes = cp.spawnSync(
      'powershell.exe',
      [
        '-NoProfile',
        '-NonInteractive',
        '-Command',
        `Expand-Archive -LiteralPath '${archivePath.replace(/'/g, "''")}' -DestinationPath '${destDir.replace(/'/g, "''")}' -Force`,
      ],
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
  const binRoot = getManagedBinDir();
  fs.mkdirSync(binRoot, { recursive: true });

  if (asset) {
    const tmpDir = path.join(binRoot, `.tmp-${process.pid}-${Date.now()}`);
    fs.mkdirSync(tmpDir, { recursive: true });
    const archivePath = path.join(tmpDir, asset.archive);
    try {
      const releaseJson = JSON.parse(
        (await httpGetBuffer(`https://api.github.com/repos/${GITHUB_REPO}/releases/latest`)).toString('utf8')
      );
      const tag = releaseJson.tag_name || 'latest';
      const targetDir = path.join(binRoot, tag);
      const targetBin = path.join(targetDir, getBinName());
      if (fs.existsSync(targetBin) && isRunnable(targetBin)) {
        fs.writeFileSync(path.join(binRoot, 'current-version.txt'), tag, 'utf8');
        return targetBin;
      }

      const downloadUrl =
        (releaseJson.assets || []).find((a) => a.name === asset.archive)?.browser_download_url ||
        `https://github.com/${GITHUB_REPO}/releases/latest/download/${asset.archive}`;
      const checksumUrl = (releaseJson.assets || []).find((a) => a.name === 'checksums.txt')?.browser_download_url;

      logStderr(`Downloading CodeExplorer ${tag} (${asset.archive})...`);
      await downloadFile(downloadUrl, archivePath);

      if (checksumUrl) {
        const checksumsText = (await httpGetBuffer(checksumUrl)).toString('utf8');
        const expectedLine = checksumsText
          .split(/\r?\n/)
          .find((l) => l.trim().endsWith(asset.archive));
        if (expectedLine) {
          const expectedHash = expectedLine.trim().split(/\s+/)[0].toLowerCase();
          const actualHash = sha256File(archivePath);
          if (expectedHash !== actualHash) {
            throw new Error(`SHA-256 mismatch for ${asset.archive}`);
          }
        }
      }

      extractArchive(archivePath, targetDir, asset.format);
      if (process.platform !== 'win32') {
        fs.chmodSync(targetBin, 0o755);
      }
      if (isRunnable(targetBin)) {
        fs.writeFileSync(path.join(binRoot, 'current-version.txt'), tag, 'utf8');
        return targetBin;
      }
    } catch (err) {
      logStderr(`Direct release download fallback: ${err?.message || err}`);
    } finally {
      fs.rmSync(tmpDir, { recursive: true, force: true });
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

async function resolveExecutable() {
  const seen = new Set();
  for (const candidate of getCandidatePaths()) {
    if (!candidate || seen.has(candidate)) continue;
    seen.add(candidate);
    if (fs.existsSync(candidate) && isRunnable(candidate)) {
      return candidate;
    }
  }
  return installManagedBinary();
}

async function main() {
  let exe;
  try {
    exe = await resolveExecutable();
  } catch (err) {
    logStderr(String(err?.message || err));
    process.exit(1);
  }

  const isWinBatch =
    process.platform === 'win32' &&
    (exe.toLowerCase().endsWith('.cmd') || exe.toLowerCase().endsWith('.bat'));

  const child = cp.spawn(exe, MCP_ARGS, {
    stdio: ['inherit', 'inherit', 'inherit'],
    windowsHide: true,
    shell: isWinBatch,
  });

  const forwardSignal = (sig) => {
    if (!child.killed) child.kill(sig);
  };
  process.on('SIGINT', () => forwardSignal('SIGINT'));
  process.on('SIGTERM', () => forwardSignal('SIGTERM'));

  child.on('error', (err) => {
    logStderr(`Failed to launch ${exe}: ${err?.message || err}`);
    process.exit(1);
  });

  child.on('exit', (code, signal) => {
    if (signal) {
      process.kill(process.pid, signal);
      return;
    }
    process.exit(code ?? 0);
  });
}

main();
