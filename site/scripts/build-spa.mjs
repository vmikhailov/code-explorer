import fs from 'node:fs';
import path from 'node:path';
import { execSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const siteDir = path.resolve(__dirname, '..');
const repoRoot = path.resolve(siteDir, '..');

function escapeAttr(str) {
  return String(str || '')
    .replace(/&/g, '&amp;')
    .replace(/"/g, '&quot;');
}

function escapeHtml(str) {
  return String(str || '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

console.log('0. Syncing Ontology SPA data from proto/ontology-schema.json...');
execSync('node scripts/sync-ontology-site.mjs', { cwd: repoRoot, stdio: 'inherit' });

console.log('1. Building Vite client bundle...');
execSync('npx vite build', { cwd: siteDir, stdio: 'inherit' });

console.log('2. Building Vite SSR bundle...');
execSync('npx vite build --ssr src/entry-server.tsx --outDir .prerender', {
  cwd: siteDir,
  stdio: 'inherit',
});

const distAssetsDir = path.join(siteDir, 'dist', 'assets');
const assetFiles = fs.readdirSync(distAssetsDir);
const cssFile = assetFiles.find((f) => f.endsWith('.css'));
const jsFile = assetFiles.find((f) => f.endsWith('.js'));

if (!cssFile || !jsFile) {
  throw new Error(`Missing built assets in ${distAssetsDir}: ${assetFiles.join(', ')}`);
}

const outAssetsDir = path.join(siteDir, 'assets');
fs.rmSync(outAssetsDir, { recursive: true, force: true });
fs.mkdirSync(outAssetsDir, { recursive: true });

const hallmarkStamp = [
  '/* Hallmark · theme: Console · modern-minimal · dark · restrained · host-grotesk + atkinson-hyperlegible-mono · hero: workbench · nav: slab · footer: mast · redesign: full */',
  '/* Pre-emit critique: Console (graphite) theme. Two fonts: Host Grotesk for display, body, labels, and controls; Atkinson Hyperlegible Mono for code, timings, tabular figures, and diffs. Warm graphite surfaces oklch(17%/21%/13.5% 0.008 250) with 1px rules oklch(31% 0.01 250) and amber signal oklch(82% 0.15 80). */',
  '/* Hallmark · waive 38iii · one accented phrase in the hero h1 · highlights the dual human and AI agent audience once in the hero */',
].join('\n');

const rawCss = fs.readFileSync(path.join(distAssetsDir, cssFile), 'utf8');
fs.writeFileSync(
  path.join(outAssetsDir, cssFile),
  `${hallmarkStamp}\n${rawCss}`,
  'utf8',
);
fs.copyFileSync(
  path.join(distAssetsDir, jsFile),
  path.join(outAssetsDir, jsFile),
);

console.log(`3. Writing SPA entry index.html with ${cssFile} and ${jsFile}...`);
const ssrModuleUrl = pathToFileURL(
  path.join(siteDir, '.prerender', 'entry-server.js'),
).href;
const { renderRoute, ROUTES_DATA } = await import(ssrModuleUrl);

const meta = ROUTES_DATA['/'];
const appHtml = renderRoute('/');

const html = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>${escapeHtml(meta.title)}</title>
  <meta name="description" content="${escapeAttr(meta.description)}">
  <meta name="author" content="CodeExplorer">
  <meta name="robots" content="index, follow">
  <meta name="theme-color" content="#121417">
  <link rel="canonical" href="${escapeAttr(meta.canonical)}">
  <link rel="icon" type="image/svg+xml" href="/favicon.svg">
  <link rel="icon" type="image/png" sizes="512x512" href="/icon-512.png">
  <link rel="apple-touch-icon" href="/icon-512.png">

  <meta property="og:type" content="website">
  <meta property="og:site_name" content="CodeExplorer">
  <meta property="og:title" content="${escapeAttr(meta.ogTitle || meta.title)}">
  <meta property="og:description" content="${escapeAttr(meta.ogDescription || meta.description)}">
  <meta property="og:url" content="${escapeAttr(meta.canonical)}">
  <meta property="og:image" content="https://code-explorer.app/all_services.png">

  <meta name="twitter:card" content="summary_large_image">
  <meta name="twitter:title" content="${escapeAttr(meta.twitterTitle || meta.ogTitle || meta.title)}">
  <meta name="twitter:description" content="${escapeAttr(meta.twitterDescription || meta.ogDescription || meta.description)}">
  <meta name="twitter:image" content="https://code-explorer.app/all_services.png">

  <link rel="stylesheet" href="fonts/fonts.css?v=1">
  <link rel="stylesheet" href="assets/${cssFile}">
</head>
<body>
  <div id="root">${appHtml}</div>
  <script type="module" src="assets/${jsFile}"></script>
</body>
</html>
`;

fs.writeFileSync(path.join(siteDir, 'index.html'), html, 'utf8');
fs.rmSync(path.join(siteDir, 'dist'), { recursive: true, force: true });
fs.rmSync(path.join(siteDir, '.prerender'), { recursive: true, force: true });
console.log('4. Pure SPA build complete (site/index.html + site/assets/*).');
