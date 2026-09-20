import * as esbuild from 'esbuild';
import * as fs from 'fs';
import * as path from 'path';

const isWatch = process.argv.includes('--watch');
const isProduction = process.argv.includes('--production');

// Ensure dist directory exists
if (!fs.existsSync('dist')) {
  fs.mkdirSync('dist', { recursive: true });
}

// Copy static CSS to dist
function copyStyles() {
  const src = path.resolve('src', 'webview', 'styles.css');
  const dest = path.resolve('dist', 'styles.css');
  if (fs.existsSync(src)) {
    fs.copyFileSync(src, dest);
    console.log('[build] Copied styles.css -> dist/styles.css');
  }
}

copyStyles();

// Build Extension Host (Node.js)
const extensionContext = await esbuild.context({
  entryPoints: ['src/extension.ts'],
  bundle: true,
  outfile: 'dist/extension.js',
  external: ['vscode'],
  format: 'cjs',
  platform: 'node',
  target: 'node18',
  sourcemap: !isProduction,
  minify: isProduction,
});

// Build Webview Script (Browser IIFE)
const webviewContext = await esbuild.context({
  entryPoints: ['src/webview/main.ts'],
  bundle: true,
  outfile: 'dist/webview.js',
  format: 'iife',
  platform: 'browser',
  target: 'es2022',
  sourcemap: !isProduction,
  minify: isProduction,
});

if (isWatch) {
  await Promise.all([extensionContext.watch(), webviewContext.watch()]);
  console.log('[watch] Watching for file changes...');
} else {
  await Promise.all([extensionContext.rebuild(), webviewContext.rebuild()]);
  await Promise.all([extensionContext.dispose(), webviewContext.dispose()]);
  console.log('[build] Extension and webview successfully bundled.');
}
