import { spawnSync } from 'child_process';
import * as path from 'path';
import * as fs from 'fs';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const extensionRoot = path.resolve(__dirname, '..');
const distVsixDir = path.resolve(extensionRoot, 'dist-vsix');
const binDir = path.resolve(extensionRoot, 'bin');
const binStoreDir = path.resolve(extensionRoot, '.bin-cache');

const pkgJson = JSON.parse(fs.readFileSync(path.resolve(extensionRoot, 'package.json'), 'utf8'));
const version = pkgJson.version || '0.1.0';

export const TARGET_PLATFORMS = [
  { rid: 'win-x64', vsceTarget: 'win32-x64', binName: 'ce.exe' },
  { rid: 'win-arm64', vsceTarget: 'win32-arm64', binName: 'ce.exe' },
  { rid: 'linux-x64', vsceTarget: 'linux-x64', binName: 'ce' },
  { rid: 'linux-arm64', vsceTarget: 'linux-arm64', binName: 'ce' },
  { rid: 'osx-x64', vsceTarget: 'darwin-x64', binName: 'ce' },
  { rid: 'osx-arm64', vsceTarget: 'darwin-arm64', binName: 'ce' },
];

const args = process.argv.slice(2);
const isAll = args.includes('--all');
const isUniversal = args.includes('--universal');
const targetArg = args.find((a) => a.startsWith('--target='));
const rebuildArg = args.includes('--rebuild');

// Create output directories
fs.mkdirSync(distVsixDir, { recursive: true });
fs.mkdirSync(binStoreDir, { recursive: true });

console.log(`[package-extension] Preparing CodeExplorer VS Code extension v${version}`);

// 1. Build extension bundle (esbuild production)
console.log(`[package-extension] Bundling extension and webview via build.mjs...`);
const buildRes = spawnSync('node', ['build.mjs', '--production'], {
  cwd: extensionRoot,
  stdio: 'inherit',
  shell: true,
});
if (buildRes.status !== 0) {
  console.error('[package-extension] Extension bundle build failed.');
  process.exit(1);
}

/**
 * Builds or retrieves the binary for a given RID.
 */
function ensureBinary(target) {
  const cachedBin = path.resolve(binStoreDir, target.rid, target.binName);
  const localBuildBin = path.resolve(binDir, target.rid, target.binName);

  if (!rebuildArg) {
    if (fs.existsSync(cachedBin)) {
      return cachedBin;
    }
    if (fs.existsSync(localBuildBin)) {
      fs.mkdirSync(path.dirname(cachedBin), { recursive: true });
      fs.copyFileSync(localBuildBin, cachedBin);
      return cachedBin;
    }
  }

  console.log(`[package-extension] Compiling binary for ${target.rid}...`);
  const compileRes = spawnSync(
    'node',
    ['scripts/build-extension-binaries.mjs', `--rid=${target.rid}`],
    {
      cwd: extensionRoot,
      stdio: 'inherit',
      shell: true,
    }
  );

  if (compileRes.status !== 0) {
    console.error(`[package-extension] Failed to compile binary for ${target.rid}`);
    process.exit(1);
  }

  const generated = path.resolve(binDir, target.rid, target.binName);
  if (!fs.existsSync(generated)) {
    console.error(`[package-extension] Expected binary not found at ${generated}`);
    process.exit(1);
  }

  fs.mkdirSync(path.dirname(cachedBin), { recursive: true });
  fs.copyFileSync(generated, cachedBin);
  return cachedBin;
}

/**
 * Packages a platform-specific VSIX.
 */
function packagePlatform(target) {
  console.log(`\n======================================================`);
  console.log(`Packaging platform VSIX: ${target.vsceTarget} (${target.rid})`);
  console.log(`======================================================`);

  const binSource = ensureBinary(target);

  // Clean bin/ and place only this target's binary
  if (fs.existsSync(binDir)) {
    fs.rmSync(binDir, { recursive: true, force: true });
  }
  fs.mkdirSync(binDir, { recursive: true });

  const stagedBin = path.resolve(binDir, target.binName);
  fs.copyFileSync(binSource, stagedBin);

  if (target.binName === 'ce') {
    try {
      fs.chmodSync(stagedBin, 0o755);
    } catch {
      // Best effort
    }
  }

  const vsixFileName = `code-explorer-${target.vsceTarget}-${version}.vsix`;
  const vsixOut = path.resolve(distVsixDir, vsixFileName);

  const vsceRes = spawnSync(
    'npx',
    [
      'vsce',
      'package',
      '--target', target.vsceTarget,
      '--no-dependencies',
      '-o', vsixOut,
    ],
    {
      cwd: extensionRoot,
      stdio: 'inherit',
      shell: true,
    }
  );

  if (vsceRes.status !== 0) {
    console.error(`[package-extension] Failed to package VSIX for ${target.vsceTarget}`);
    process.exit(1);
  }

  const stat = fs.statSync(vsixOut);
  const sizeMb = (stat.size / (1024 * 1024)).toFixed(2);
  console.log(`✓ Created: ${vsixFileName} (${sizeMb} MB)`);
}

/**
 * Packages a universal VSIX containing binaries for all platforms.
 */
function packageUniversal() {
  console.log(`\n======================================================`);
  console.log(`Packaging Universal VSIX (All Platforms)`);
  console.log(`======================================================`);

  if (fs.existsSync(binDir)) {
    fs.rmSync(binDir, { recursive: true, force: true });
  }
  fs.mkdirSync(binDir, { recursive: true });

  for (const target of TARGET_PLATFORMS) {
    const binSource = ensureBinary(target);
    const targetFolder = path.resolve(binDir, target.rid);
    fs.mkdirSync(targetFolder, { recursive: true });
    const stagedBin = path.resolve(targetFolder, target.binName);
    fs.copyFileSync(binSource, stagedBin);
    if (target.binName === 'ce') {
      try {
        fs.chmodSync(stagedBin, 0o755);
      } catch {
        // Best effort
      }
    }
  }

  const vsixFileName = `code-explorer-universal-${version}.vsix`;
  const vsixOut = path.resolve(distVsixDir, vsixFileName);

  const vsceRes = spawnSync(
    'npx',
    [
      'vsce',
      'package',
      '--no-dependencies',
      '-o', vsixOut,
    ],
    {
      cwd: extensionRoot,
      stdio: 'inherit',
      shell: true,
    }
  );

  if (vsceRes.status !== 0) {
    console.error(`[package-extension] Failed to package Universal VSIX`);
    process.exit(1);
  }

  const stat = fs.statSync(vsixOut);
  const sizeMb = (stat.size / (1024 * 1024)).toFixed(2);
  console.log(`✓ Created: ${vsixFileName} (${sizeMb} MB)`);
}

// Execution dispatch
if (isUniversal) {
  packageUniversal();
} else if (isAll) {
  for (const target of TARGET_PLATFORMS) {
    packagePlatform(target);
  }
} else if (targetArg) {
  const tName = targetArg.split('=')[1];
  const target = TARGET_PLATFORMS.find((t) => t.vsceTarget === tName || t.rid === tName);
  if (!target) {
    console.error(`Unknown target: ${tName}`);
    process.exit(1);
  }
  packagePlatform(target);
} else {
  // Default: current host platform
  const platform = process.platform;
  const arch = process.arch;
  let target = TARGET_PLATFORMS.find((t) => t.vsceTarget === 'win32-x64');
  if (platform === 'win32') {
    target = TARGET_PLATFORMS.find((t) => t.vsceTarget === (arch === 'arm64' ? 'win32-arm64' : 'win32-x64'));
  } else if (platform === 'darwin') {
    target = TARGET_PLATFORMS.find((t) => t.vsceTarget === (arch === 'arm64' ? 'darwin-arm64' : 'darwin-x64'));
  } else if (platform === 'linux') {
    target = TARGET_PLATFORMS.find((t) => t.vsceTarget === (arch === 'arm64' ? 'linux-arm64' : 'linux-x64'));
  }

  packagePlatform(target);
}

console.log(`\n[package-extension] Packaging complete! Artifacts are in ${distVsixDir}`);
