import * as esbuild from 'esbuild';
import * as fs from 'fs';
import * as path from 'path';

const isWatch = process.argv.includes('--watch');
const isProduction = process.argv.includes('--production');

// Ensure dist directory exists
if (!fs.existsSync('dist')) {
  fs.mkdirSync('dist', { recursive: true });
}

// Helper to recursively bundle imported CSS files
function resolveImports(cssFilePath) {
  if (!fs.existsSync(cssFilePath)) return '';
  let content = fs.readFileSync(cssFilePath, 'utf8');
  const dir = path.dirname(cssFilePath);
  return content.replace(/@import\s+['"]([^'"]+)['"];?/g, (match, relPath) => {
    const fullImportPath = path.resolve(dir, relPath);
    return resolveImports(fullImportPath);
  });
}

// Combine React Flow CSS + Custom Modular CSS to dist/styles.css
function copyStyles() {
  let combinedCss = '';
  const reactFlowCssPath = path.resolve('node_modules', '@xyflow', 'react', 'dist', 'style.css');
  if (fs.existsSync(reactFlowCssPath)) {
    combinedCss += fs.readFileSync(reactFlowCssPath, 'utf8') + '\n';
  }

  const customCssPath = path.resolve('src', 'webview', 'styles.css');
  if (fs.existsSync(customCssPath)) {
    combinedCss += resolveImports(customCssPath);
  }

  fs.writeFileSync(path.resolve('dist', 'styles.css'), combinedCss);
  console.log('[build] Combined React Flow CSS + modular styles.css -> dist/styles.css');
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

// Build Webview Script (Browser IIFE with React & React Flow)
const webviewContext = await esbuild.context({
  entryPoints: ['src/webview/index.tsx'],
  bundle: true,
  outfile: 'dist/webview.js',
  format: 'iife',
  platform: 'browser',
  target: 'es2022',
  sourcemap: !isProduction,
  minify: isProduction,
  define: {
    'process.env.NODE_ENV': isProduction ? '"production"' : '"development"',
  },
});

if (isWatch) {
  await Promise.all([extensionContext.watch(), webviewContext.watch()]);
  console.log('[watch] Watching for file changes...');
} else {
  await Promise.all([extensionContext.rebuild(), webviewContext.rebuild()]);
  await Promise.all([extensionContext.dispose(), webviewContext.dispose()]);
  console.log('[build] Extension and webview successfully bundled.');
}
