import { spawnSync } from 'child_process';
import * as path from 'path';
import * as fs from 'fs';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const extensionRoot = path.resolve(__dirname, '..');
const repoRoot = path.resolve(extensionRoot, '..');
const csprojPath = path.resolve(repoRoot, 'cli', 'src', 'UI', 'CodeExplorer', 'CodeExplorer.csproj');

/**
 * Mapping of supported platforms and architectures.
 */
export const TARGET_PLATFORMS = [
  { rid: 'win-x64', vsceTarget: 'win32-x64', binName: 'ce.exe' },
  { rid: 'win-arm64', vsceTarget: 'win32-arm64', binName: 'ce.exe' },
  { rid: 'linux-x64', vsceTarget: 'linux-x64', binName: 'ce' },
  { rid: 'linux-arm64', vsceTarget: 'linux-arm64', binName: 'ce' },
  { rid: 'osx-x64', vsceTarget: 'darwin-x64', binName: 'ce' },
  { rid: 'osx-arm64', vsceTarget: 'darwin-arm64', binName: 'ce' },
];

/**
 * Parses CLI arguments:
 *   --rid=<rid>        Build only the specified RID
 *   --target=<target>  Build only the specified VS Code target (e.g. win32-x64)
 *   --current          Build only for current host platform/arch
 *   --all              Build all supported platforms (default)
 *   --flat             Output binary directly to bin/<binName> instead of bin/<rid>/<binName>
 */
const args = process.argv.slice(2);
let selectedTargets = TARGET_PLATFORMS;

const ridArg = args.find((a) => a.startsWith('--rid='));
const targetArg = args.find((a) => a.startsWith('--target='));
const currentArg = args.includes('--current');
const flatArg = args.includes('--flat');

if (ridArg) {
  const rid = ridArg.split('=')[1];
  selectedTargets = TARGET_PLATFORMS.filter((t) => t.rid === rid);
  if (selectedTargets.length === 0) {
    console.error(`Unknown RID: ${rid}. Available RIDs: ${TARGET_PLATFORMS.map((t) => t.rid).join(', ')}`);
    process.exit(1);
  }
} else if (targetArg) {
  const target = targetArg.split('=')[1];
  selectedTargets = TARGET_PLATFORMS.filter((t) => t.vsceTarget === target);
  if (selectedTargets.length === 0) {
    console.error(`Unknown target: ${target}. Available targets: ${TARGET_PLATFORMS.map((t) => t.vsceTarget).join(', ')}`);
    process.exit(1);
  }
} else if (currentArg) {
  const platform = process.platform;
  const arch = process.arch;
  let matchingRid = 'win-x64';
  if (platform === 'win32') {
    matchingRid = arch === 'arm64' ? 'win-arm64' : 'win-x64';
  } else if (platform === 'darwin') {
    matchingRid = arch === 'arm64' ? 'osx-arm64' : 'osx-x64';
  } else if (platform === 'linux') {
    matchingRid = arch === 'arm64' ? 'linux-arm64' : 'linux-x64';
  }
  selectedTargets = TARGET_PLATFORMS.filter((t) => t.rid === matchingRid);
}

console.log(`[build-binaries] Building self-contained CE binaries for: ${selectedTargets.map((t) => t.rid).join(', ')}`);

for (const target of selectedTargets) {
  const outDir = flatArg
    ? path.resolve(extensionRoot, 'bin')
    : path.resolve(extensionRoot, 'bin', target.rid);

  fs.mkdirSync(outDir, { recursive: true });

  const publishArgs = [
    'publish',
    csprojPath,
    '-c', 'Release',
    '-r', target.rid,
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:IncludeAllContentForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:PublishTrimmed=false',
    '-p:DebugType=none',
    '-o', outDir,
  ];

  console.log(`\n========================================`);
  console.log(`Building ${target.rid} (${target.vsceTarget}) -> ${outDir}`);
  console.log(`dotnet ${publishArgs.join(' ')}`);
  console.log(`========================================`);

  const result = spawnSync('dotnet', publishArgs, {
    cwd: repoRoot,
    stdio: 'inherit',
    shell: true,
  });

  if (result.status !== 0) {
    console.error(`[build-binaries] Failed to build ${target.rid} (exit code ${result.status})`);
    process.exit(result.status || 1);
  }

  // Ensure file permissions on Unix binaries if on POSIX
  const binFile = path.resolve(outDir, target.binName);
  if (fs.existsSync(binFile) && target.binName === 'ce') {
    try {
      fs.chmodSync(binFile, 0o755);
    } catch {
      // Best effort on Windows hosts
    }
  }

  console.log(`✓ Successfully built ${target.binName} for ${target.rid}`);
}

console.log(`\n[build-binaries] All target binaries built successfully.`);
