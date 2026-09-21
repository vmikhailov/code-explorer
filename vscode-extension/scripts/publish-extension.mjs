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

// 1. Publish to VS Code Marketplace
if (toMarketplace) {
  console.log('\n========================================');
  console.log('Publishing to Visual Studio Marketplace');
  console.log('========================================');

  if (!vscePat) {
    console.log('::warning::VSCE_PAT secret is not set. Skipping Visual Studio Marketplace publishing.');
  } else {
    for (const vsix of vsixFiles) {
      const vsixPath = path.resolve(distVsixDir, vsix);
      console.log(`Publishing ${vsix} to VS Code Marketplace...`);
      const res = spawnSync('npx', ['vsce', 'publish', '--packagePath', vsixPath, '-p', vscePat], {
        cwd: extensionRoot,
        stdio: 'inherit',
        shell: true,
      });
      if (res.status !== 0) {
        console.error(`Failed to publish ${vsix} to VS Code Marketplace.`);
        process.exit(1);
      } else {
        console.log(`✓ Published ${vsix} to VS Code Marketplace.`);
      }
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
    for (const vsix of vsixFiles) {
      const vsixPath = path.resolve(distVsixDir, vsix);
      console.log(`Publishing ${vsix} to Open VSX Registry...`);
      const res = spawnSync('npx', ['ovsx', 'publish', vsixPath, '-p', ovsxPat], {
        cwd: extensionRoot,
        stdio: 'inherit',
        shell: true,
      });
      if (res.status !== 0) {
        console.error(`Failed to publish ${vsix} to Open VSX Registry.`);
        process.exit(1);
      } else {
        console.log(`✓ Published ${vsix} to Open VSX Registry.`);
      }
    }
  }
}

console.log('\n[publish-extension] Done.');
