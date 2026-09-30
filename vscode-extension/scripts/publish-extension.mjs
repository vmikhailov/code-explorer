import { spawnSync } from 'child_process';
import * as path from 'path';
import * as fs from 'fs';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const extensionRoot = path.resolve(__dirname, '..');
const distVsixDir = path.resolve(extensionRoot, 'dist-vsix');

const args = process.argv.slice(2);
const toMarketplace = args.includes('--marketplace') || (!args.includes('--ovsx'));
const toOvsx = args.includes('--ovsx') || (!args.includes('--marketplace'));

const vscePat = process.env.VSCE_PAT;
const ovsxPat = process.env.OVSX_PAT;

if (!fs.existsSync(distVsixDir)) {
  console.error(`No dist-vsix directory found at ${distVsixDir}. Run 'npm run package:all' first.`);
  process.exit(1);
}

const vsixFiles = fs.readdirSync(distVsixDir).filter((f) => f.endsWith('.vsix'));
if (vsixFiles.length === 0) {
  console.error(`No .vsix files found in ${distVsixDir}. Run 'npm run package:all' first.`);
  process.exit(1);
}

console.log(`[publish-extension] Found ${vsixFiles.length} VSIX packages in ${distVsixDir}:`);
for (const file of vsixFiles) {
  console.log(`  - ${file}`);
}

const platformVsixFiles = vsixFiles.filter((f) => !f.includes('universal'));
const filesToPublish = platformVsixFiles.length > 0 ? platformVsixFiles : vsixFiles;
const vsixPathsToPublish = filesToPublish.map((f) => path.resolve(distVsixDir, f));

let hasErrors = false;

// 1. Publish to VS Code Marketplace
if (toMarketplace) {
  console.log('\n========================================');
  console.log('Publishing to Visual Studio Marketplace');
  console.log('========================================');

  if (!vscePat) {
    console.log('::warning::VSCE_PAT secret is not set. Skipping Visual Studio Marketplace publishing.');
  } else {
    console.log(`Publishing ${vsixPathsToPublish.length} package(s) to VS Code Marketplace in single batch...`);
    const res = spawnSync('npx', ['vsce', 'publish', '-p', vscePat, '--skip-duplicate', '--packagePath', ...vsixPathsToPublish], {
      cwd: extensionRoot,
      stdio: 'inherit',
      shell: true,
    });
    if (res.status !== 0) {
      console.error(`Failed to publish packages to VS Code Marketplace (exit code ${res.status}).`);
      hasErrors = true;
    } else {
      console.log(`✓ Published packages to VS Code Marketplace.`);
    }
  }
}

// 2. Publish to Open VSX Registry
if (toOvsx) {
  console.log('\n========================================');
  console.log('Publishing to Open VSX Registry');
  console.log('========================================');

  if (!ovsxPat) {
    console.log('::warning::OVSX_PAT secret is not set. Skipping Open VSX Registry publishing.');
  } else {
    // Ensure publisher namespace exists on Open VSX
    try {
      const pkgJson = JSON.parse(fs.readFileSync(path.resolve(extensionRoot, 'package.json'), 'utf8'));
      const publisher = pkgJson.publisher || 'vmikhailov';
      console.log(`Ensuring namespace '${publisher}' exists on Open VSX...`);
      spawnSync('npx', ['ovsx', 'create-namespace', publisher, '-p', ovsxPat], {
        cwd: extensionRoot,
        stdio: 'inherit',
        shell: true,
      });
    } catch (e) {
      console.warn('Note: create-namespace check:', e?.message || e);
    }

    for (const vsix of filesToPublish) {
      const vsixPath = path.resolve(distVsixDir, vsix);
      console.log(`Publishing ${vsix} to Open VSX Registry...`);
      const res = spawnSync('npx', ['ovsx', 'publish', vsixPath, '-p', ovsxPat, '--skip-duplicate'], {
        cwd: extensionRoot,
        stdio: 'inherit',
        shell: true,
      });
      if (res.status !== 0) {
        console.error(`Failed to publish ${vsix} to Open VSX Registry.`);
        hasErrors = true;
      } else {
        console.log(`✓ Published ${vsix} to Open VSX Registry.`);
      }
    }
  }
}

if (hasErrors) {
  process.exit(1);
}

console.log('\n[publish-extension] Done.');
