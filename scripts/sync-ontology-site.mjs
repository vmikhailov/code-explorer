#!/usr/bin/env node
/**
 * Deterministically generates site/ontology.html from proto/ontology-schema.json.
 * Usage:
 *   node scripts/sync-ontology-site.mjs
 *   node scripts/sync-ontology-site.mjs --check
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { renderOntologyClientScript } from './ontology-graph-client.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');
const SCHEMA_PATH = path.join(ROOT, 'proto', 'ontology-schema.json');
const OUT_HTML_PATH = path.join(ROOT, 'site', 'ontology.html');

const isCheck = process.argv.includes('--check');

function esc(str) {
  return String(str ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/[\u2013\u2014]/g, ' to ');
}

function renderStaticInspector(schema, defaultKind = 'Endpoint') {
  const node = schema.nodes.find((n) => n.kind === defaultKind) || schema.nodes[0];
  const relMap = new Map(schema.relationships.map((r) => [r.rel, r]));

  const propsHtml = node.properties
    .map(
      (p) => `              <div class="ont-prop-item">
                <div class="ont-prop-top">
                  <code class="ont-prop-name">${esc(p.name)}</code>
                  <code class="ont-prop-type">${esc(p.type)}</code>
                </div>
                <span class="ont-prop-desc">${esc(p.description)}</span>
              </div>`
    )
    .join('\n');

  const outHtml = node.outEdges
    .map((e) => {
      const rMeta = relMap.get(e.rel);
      const eProps =
        rMeta && rMeta.properties && rMeta.properties.length > 1
          ? `\n                <div class="ont-edge-props-row">${rMeta.properties
              .map((ep) => `<code class="ont-edge-prop" title="${esc(ep.description)}">r.${esc(ep.name)}</code>`)
              .join('')}</div>`
          : '';
      return `              <div class="ont-rel-row">
                <div class="ont-rel-line">
                  <code class="ont-edge-tag ont-edge-tag--${esc(e.category)}">-[:${esc(e.rel)}]-&gt;</code>
                  <button type="button" class="ont-jump-btn" data-select-kind="${esc(e.toKind)}">:${esc(e.toKind)}</button>
                </div>
                <span class="ont-prop-desc">${esc(e.description)}</span>${eProps}
              </div>`;
    })
    .join('\n');

  const inHtml = node.inEdges
    .map(
      (e) => `              <div class="ont-rel-row">
                <div class="ont-rel-line">
                  <button type="button" class="ont-jump-btn" data-select-kind="${esc(e.fromKind)}">:${esc(e.fromKind)}</button>
                  <code class="ont-edge-tag ont-edge-tag--${esc(e.category)}">-[:${esc(e.rel)}]-&gt;</code>
                </div>
                <span class="ont-prop-desc">${esc(e.description)}</span>
              </div>`
    )
    .join('\n');

  const compactPropsHtml = node.properties
    .slice(0, 6)
    .map((p) => `<code class="ont-edge-prop">${esc(p.name)}: ${esc(p.type)}</code>`)
    .join('');
  const morePropsHtml =
    node.properties.length > 6
      ? `<code class="ont-edge-prop">+${node.properties.length - 6} more</code>`
      : '';

  const peerSet = new Set();
  for (const e of node.outEdges) peerSet.add(e.toKind);
  for (const e of node.inEdges) peerSet.add(e.fromKind);
  const peersList = Array.from(peerSet);
  const compactRelsHtml = peersList
    .slice(0, 6)
    .map((pk) => `<button type="button" class="ont-jump-btn" data-select-kind="${esc(pk)}">:${esc(pk)}</button>`)
    .join('');
  const morePeersHtml =
    peersList.length > 6
      ? `<code class="ont-edge-prop">+${peersList.length - 6} more</code>`
      : '';

  return `              <aside id="entity-inspector" class="ont-inspector is-compact" aria-live="polite" hidden>
                <div class="ont-insp-head">
                  <div class="ont-insp-top-bar">
                    <div class="ont-insp-title-row">
                      <h3 id="insp-title" class="ont-insp-title">:${esc(node.kind)}</h3>
                      <code id="insp-layer-pill" class="ont-layer-pill ont-layer-pill--l${node.layerId}">Layer ${node.layerId}</code>
                      <code id="insp-abstract-pill" class="ont-edge-prop"${node.isAbstract ? '' : ' hidden'}>abstract</code>
                    </div>
                    <div class="ont-insp-actions">
                      <button type="button" id="insp-toggle-mode-btn" class="ont-insp-action-btn" title="Toggle between compact summary and full node details">Full</button>
                      <button type="button" id="insp-close-btn" class="ont-insp-action-btn ont-insp-close-btn" aria-label="Close entity inspector" title="Close inspector">&times;</button>
                    </div>
                  </div>
                  <div class="ont-insp-meta">
                    <span id="insp-plural" class="ont-layer-sub">${esc(node.pluralLabel)}</span>
                  </div>
                  <div id="insp-lineage" class="ont-lineage-wrap" hidden></div>
                  <p id="insp-purpose" class="panel-copy ont-insp-purpose">${esc(node.purpose)}</p>
                  <div class="ont-urn-strip">
                    <code class="ont-urn-key">urn</code>
                    <code id="insp-urn" class="ont-urn-val">${esc(node.idScheme)}</code>
                  </div>
                </div>

                <div id="insp-compact-body" class="ont-insp-compact-body">
                  <div class="ont-compact-group">
                    <span id="insp-compact-props-label" class="ont-block-label">Properties (${node.properties.length} + ${schema.commonProperties.length} base)</span>
                    <div id="insp-compact-props" class="ont-compact-pills">${compactPropsHtml}${morePropsHtml}</div>
                  </div>
                  <div class="ont-compact-group">
                    <span id="insp-compact-rels-label" class="ont-block-label">Connected entities (${node.outEdges.length} out, ${node.inEdges.length} in)</span>
                    <div id="insp-compact-rels" class="ont-compact-pills">${compactRelsHtml}${morePeersHtml}</div>
                  </div>
                </div>

                <div id="insp-full-body" class="ont-insp-full-body">
                  <div class="ont-block">
                    <div class="ont-block-bar">
                      <span id="insp-props-heading" class="ont-block-label">Collected properties (${node.properties.length} + ${schema.commonProperties.length} base)</span>
                    </div>
                    <div id="insp-props-list" class="ont-props-list">
${propsHtml}
                    </div>
                  </div>

                  <div class="ont-block">
                    <div class="ont-block-bar">
                      <span id="insp-rels-heading" class="ont-block-label">Typed relationships (${node.outEdges.length} out, ${node.inEdges.length} in)</span>
                    </div>
                    <div id="insp-rels-list" class="ont-rels-list">
${outHtml}
${inHtml}
                    </div>
                  </div>

                  <div class="ont-block">
                    <div class="ont-block-bar">
                      <span class="ont-block-label">OpenCypher query</span>
                      <button type="button" id="copy-cypher-btn" class="ont-copy-btn">Copy Query</button>
                    </div>
                    <pre id="insp-cypher" class="ont-cypher-box">${esc(node.sampleCypher)}</pre>
                  </div>
                </div>
              </aside>`;
}

function generateHtml(schema) {
  const defaultKind = 'Endpoint';
  const defaultNode = schema.nodes.find((n) => n.kind === defaultKind) || schema.nodes[0];
  const defaultActiveCats = new Set(['control_flow', 'data_flow', 'type_dependency', 'structural']);

  const initialBadges = new Map();
  const initialFocus = [];
  for (const e of defaultNode.outEdges) {
    if (!defaultActiveCats.has(e.category)) continue;
    const arr = initialBadges.get(e.toKind) || [];
    arr.push({ dir: 'out', rel: e.rel, cat: e.category });
    initialBadges.set(e.toKind, arr);
    initialFocus.push({ dir: 'out', rel: e.rel, peer: e.toKind, cat: e.category });
  }
  for (const e of defaultNode.inEdges) {
    if (!defaultActiveCats.has(e.category)) continue;
    const arr = initialBadges.get(e.fromKind) || [];
    arr.push({ dir: 'in', rel: e.rel, cat: e.category });
    initialBadges.set(e.fromKind, arr);
    initialFocus.push({ dir: 'in', rel: e.rel, peer: e.fromKind, cat: e.category });
  }

  const totalProps = schema.nodes.reduce(
    (acc, n) => acc + n.properties.filter((p) => !p.inheritedFrom).length,
    0
  );

  const focusLinksHtml = initialFocus
    .slice(0, 10)
    .map((f) => {
      const label =
        f.dir === 'out'
          ? `out: ${esc(f.rel)} :${esc(f.peer)}`
          : `in: :${esc(f.peer)} ${esc(f.rel)}`;
      return `                <button type="button" class="ont-focus-chip ont-focus-chip--${esc(f.cat)}" data-select-kind="${esc(f.peer)}"><code>${label}</code></button>`;
    })
    .join('\n');

  const layersHtml = schema.layers
    .map((layer) => {
      const layerNodes = schema.nodes.filter((n) => n.layerId === layer.layerId);
      const cardsHtml = layerNodes
        .map((n) => {
          const isSelected = n.kind === defaultNode.kind;
          const badges = initialBadges.get(n.kind) || [];
          const isNeighbor = badges.length > 0;
          const isDimmed = !isSelected && !isNeighbor;
          const primaryCat = badges.length > 0 ? badges[0].cat : '';
          const cls = [
            'ont-node-btn',
            isNeighbor ? 'is-neighbor' : '',
            isNeighbor && primaryCat ? `is-neighbor--${primaryCat}` : '',
            isDimmed ? 'is-dimmed' : '',
          ]
            .filter(Boolean)
            .join(' ');

          const edgeTotal =
            n.outEdges.length +
            n.inEdges.length +
            (n.inheritedOutEdges ? n.inheritedOutEdges.length : 0) +
            (n.inheritedInEdges ? n.inheritedInEdges.length : 0);

          return `                  <button type="button" class="${cls}" data-kind="${esc(n.kind)}" title="${esc(n.pluralLabel)}: ${esc(n.purpose)}" aria-selected="${isSelected ? 'true' : 'false'}">
                    <code class="ont-node-kind">:${esc(n.kind)}</code>
                    <span class="ont-node-stats">${n.properties.length}p / ${edgeTotal}e</span>
                  </button>`;
        })
        .join('\n');

      return `              <div class="ont-layer" data-layer-id="${layer.layerId}">
                <div class="ont-layer-bar">
                  <h3 class="ont-layer-title">Layer ${layer.layerId}: ${esc(layer.name)}</h3>
                  <code class="ont-layer-pill ont-layer-pill--l${layer.layerId}">${layerNodes.length} kinds</code>
                </div>
                <p class="ont-layer-sub">${esc(layer.description)}</p>
                <div class="ont-node-grid">
${cardsHtml}
                </div>
              </div>`;
    })
    .join('\n\n');

  const shortLayerNames = { 5: 'Logical', 4: 'Semantic', 3: 'Syntax', 2: 'Boundaries', 1: 'Physical' };
  const layerButtonsHtml = schema.layers
    .map((layer) => {
      const shortName = shortLayerNames[layer.layerId] || layer.name;
      return `              <button type="button" class="ont-layer-btn" data-layer="${layer.layerId}" aria-pressed="true" title="${esc(layer.title)}: ${esc(layer.description)}">
                <code class="ont-layer-btn-tag">L${layer.layerId}</code>
                <span>${esc(shortName)}</span>
              </button>`;
    })
    .join('\n');

  const hierarchyNodes = schema.nodes
    .filter((n) => Array.isArray(n.subtypes) && n.subtypes.length > 0)
    .sort((a, b) => (a.ancestors ? a.ancestors.length : 0) - (b.ancestors ? b.ancestors.length : 0));
  const capsuleButtonsHtml = hierarchyNodes
    .map(
      (h) => `                  <button type="button" class="ont-capsule-btn" data-hierarchy="${esc(h.kind)}" aria-pressed="true" title="Toggle :${esc(h.kind)} inheritance hierarchy (${h.subtypes.length} direct subtypes)">
                    <code class="ont-layer-btn-tag">Extends</code>
                    <span>:${esc(h.kind)}</span>
                  </button>`
    )
    .join('\n');

  const shortCatNames = {
    control_flow: 'Control',
    data_flow: 'Data & SQL',
    type_dependency: 'Types & Cloud',
    structural: 'Containment',
  };
  const catButtonsHtml = schema.edgeCategories
    .map((cat) => {
      const pressed = defaultActiveCats.has(cat.id);
      const shortLabel = shortCatNames[cat.id] || cat.label;
      return `                  <button type="button" class="ont-cat-btn" data-cat="${esc(cat.id)}" aria-pressed="${pressed ? 'true' : 'false'}" title="${esc(cat.label)}: ${esc(cat.description)}">
                    <span class="ont-dot ont-dot--${esc(cat.id)}"></span>
                    <span>${esc(shortLabel)}</span>
                  </button>`;
    })
    .join('\n');

  const relCatalogHtml = schema.edgeCategories
    .map((cat) => {
      const catRels = schema.relationships.filter((r) => r.category === cat.id);
      const rowsHtml = catRels
        .map((r) => {
          const pairsHtml = r.pairs
            .slice(0, 6)
            .map((p) => `<code class="ont-pair-pill">${esc(p.fromKind)} -&gt; ${esc(p.toKind)}</code>`)
            .join('');
          const moreCount = r.pairs.length > 6 ? ` <code class="ont-pair-pill">+${r.pairs.length - 6} more</code>` : '';
          const edgePropsHtml = r.properties
            .map((ep) => `<code>${esc(ep.name)}</code>`)
            .join(', ');
          return `                  <tr>
                    <td><code class="ont-edge-tag ont-edge-tag--${esc(r.category)}">${esc(r.rel)}</code></td>
                    <td>${esc(r.description)}</td>
                    <td>${edgePropsHtml}</td>
                    <td><div class="ont-pairs-wrap">${pairsHtml}${moreCount}</div></td>
                  </tr>`;
        })
        .join('\n');

      return `          <div class="ont-rel-cat-block">
            <div class="ont-rel-cat-head">
              <h3>${esc(cat.label)}</h3>
              <span class="ont-layer-count">${catRels.length} relationship types</span>
            </div>
            <p class="panel-copy">${esc(cat.description)}</p>
            <div class="table-surface">
              <table class="bench-table catalog-table">
                <thead>
                  <tr>
                    <th scope="col">Relationship</th>
                    <th scope="col">Semantics</th>
                    <th scope="col">Edge Properties</th>
                    <th scope="col">Connected Entities</th>
                  </tr>
                </thead>
                <tbody>
${rowsHtml}
                </tbody>
              </table>
            </div>
          </div>`;
    })
    .join('\n\n');

  return `<section id="explorer" class="ont-hero-stage">
      <div class="wrap">
        <div class="stage-intro">
          <div>
            <h1 class="hero-title">Every fact CodeExplorer extracts.</h1>
          </div>
          <div class="hero-aside">
            <p class="hero-lede">
              Inspect ${schema.nodes.length} entity kinds, ${totalProps} properties, and ${schema.relationships.length} relationships across 5 layers.
            </p>
            <div class="hero-controls">
              <a href="#swimlanes" class="btn btn--primary">Explore Ontology</a>
              <a href="#relationships" class="btn">Relationship Catalog</a>
            </div>
          </div>
        </div>

        <div id="swimlanes" class="ont-workbench">
          <div id="ont-graph-panel" class="ont-graph-panel" data-slot="ontology-workbench">
            <div class="ont-graph-bar">
              <div class="ont-bar-left">
                <div class="ont-mode-group" role="group" aria-label="Ontology visualization mode">
                  <button type="button" class="ont-mode-btn" data-mode="graph" aria-pressed="true">Graph</button>
                  <button type="button" class="ont-mode-btn" data-mode="layers" aria-pressed="false">5 Layers</button>
                </div>
                <input
                  type="search"
                  id="ont-search"
                  class="ont-search-input"
                  aria-label="Filter ontology entities, properties, or relationships"
                  placeholder="Filter entities, properties, or edges..."
                >
              </div>
              <div class="ont-graph-actions">
                <div id="ont-graph-zoom-group" class="ont-graph-zoom-group">
                  <button type="button" id="graph-show-all" class="ont-graph-btn ont-graph-btn--accent" hidden>Show All</button>
                  <label class="ont-air-control" title="Regulate elastic spacing (air) between nodes">
                    <span class="ont-air-label">Air</span>
                    <input type="range" id="graph-air-slider" min="0.1" max="2.4" step="0.1" value="1.0" aria-label="Node spacing factor">
                    <code id="graph-air-val" class="ont-air-val">1.0x</code>
                  </label>
                  <label class="ont-air-control" title="Scale repulsion and spring distance by node edge degree (ForceAtlas2 hub factor)">
                    <span class="ont-air-label">Hub</span>
                    <input type="range" id="graph-hub-slider" min="0.0" max="5.0" step="0.1" value="1.0" aria-label="Degree repulsion factor">
                    <code id="graph-hub-val" class="ont-air-val">1.0x</code>
                  </label>
                  <button type="button" id="graph-freeze" class="ont-graph-btn" aria-pressed="false" title="Freeze or resume elastic physics">Freeze</button>
                  <button type="button" id="graph-zoom-out" class="ont-graph-btn" aria-label="Zoom out">&minus;</button>
                  <button type="button" id="graph-zoom-in" class="ont-graph-btn" aria-label="Zoom in">+</button>
                  <button type="button" id="graph-zoom-fit" class="ont-graph-btn">Fit</button>
                </div>
                <button type="button" id="graph-fullscreen" class="ont-graph-btn" aria-pressed="false" title="Expand within window (Esc)">Expand</button>
              </div>
            </div>

            <div class="ont-stage-body">
              <div id="ont-filter-overlay" class="ont-filter-overlay" aria-label="Layer, inheritance, and relationship filters">
                <div class="ont-filter-row">
                  <div class="ont-layer-group" role="group" aria-label="Ontology layer filters">
${layerButtonsHtml}
                  </div>
                  <div class="ont-cat-group" role="group" aria-label="Relationship category filters">
${catButtonsHtml}
                  </div>
                </div>
                <div id="ont-capsule-group" class="ont-capsule-group" role="group" aria-label="Inheritance hierarchy toggles">
${capsuleButtonsHtml}
                </div>
              </div>

              <div id="ont-canvas-frame" class="ont-canvas-frame">
                <canvas id="ont-graph-canvas" class="ont-graph-canvas" role="img" aria-label="Interactive ontology entity relationship graph"></canvas>
              </div>

              <div id="ont-swimlanes" class="ont-swimlanes" hidden>
                <div id="ont-focus-strip" class="ont-focus-strip">
                  <div class="ont-focus-bar">
                    <h2 id="ont-focus-label" class="ont-focus-label">Active connections for :${esc(defaultNode.kind)}</h2>
                    <span id="ont-focus-count" class="ont-layer-count">${initialFocus.length} active links</span>
                  </div>
                  <div id="ont-focus-links" class="ont-focus-links">
${focusLinksHtml}
                  </div>
                </div>

${layersHtml}
              </div>

${renderStaticInspector(schema, defaultKind)}
            </div>

            <div id="ont-graph-foot" class="ont-graph-foot">
              <div class="ont-cluster-legend">
                <span class="ont-cluster-item"><span class="ont-cluster-swatch ont-swatch--logical"></span>Domains</span>
                <span class="ont-cluster-item"><span class="ont-cluster-swatch ont-swatch--workload"></span>Workloads</span>
                <span class="ont-cluster-item"><span class="ont-cluster-swatch ont-swatch--contract"></span>Contracts</span>
                <span class="ont-cluster-item"><span class="ont-cluster-swatch ont-swatch--data"></span>Data &amp; SQL</span>
                <span class="ont-cluster-item"><span class="ont-cluster-swatch ont-swatch--cloud"></span>Cloud</span>
                <span class="ont-cluster-item"><span class="ont-cluster-swatch ont-swatch--syntax"></span>Syntax</span>
                <span class="ont-cluster-item"><span class="ont-cluster-swatch ont-swatch--slicing"></span>Slicing</span>
                <span class="ont-cluster-item"><span class="ont-cluster-swatch ont-swatch--project"></span>Projects</span>
                <span class="ont-cluster-item"><span class="ont-cluster-swatch ont-swatch--physical"></span>Files</span>
              </div>
              <span class="ont-layer-sub">Drag circles to rearrange, scroll to zoom, click entities or inheritance toggles to inspect.</span>
            </div>
          </div>
        </div>
      </div>
    </section>

    <section id="relationships" class="ont-rel-section">
      <div class="wrap">
        <div class="section-head">
          <h2>Typed Relationship Catalog</h2>
          <p class="panel-copy">
            Auto-generated at build time from C# ontology annotations. Every edge carries source line provenance or transitive call-chain evidence.
          </p>
        </div>

        <div class="ont-rel-cats">
${relCatalogHtml}
        </div>
      </div>
    </section>`;
}

const schema = JSON.parse(fs.readFileSync(SCHEMA_PATH, 'utf8'));
const nextMainHtml = generateHtml(schema).trim();

const routesDataPath = path.join(ROOT, 'site', 'src', 'generated', 'routesData.ts');
const ontCtrlPath = path.join(ROOT, 'site', 'src', 'generated', 'ontologyController.ts');

const safeJsonInline = JSON.stringify(schema).replace(/<\//g, '<\\/');
const rawOntScript = renderOntologyClientScript(safeJsonInline)
  .replace(/^\s*<script>\s*\(function\s*\(\)\s*\{/, '')
  .replace(/\}\)\(\);\s*<\/script>\s*$/, '');

const nextControllerTs = `// Auto-generated Ontology Page interactive controller for React SPA
// @ts-nocheck
export function initOntologyController(): () => void {
  if (typeof window === 'undefined' || typeof document === 'undefined') {
    return () => {};
  }
${rawOntScript}
  return () => {
    document.body.classList.remove("has-fullscreen-graph");
    if (typeof simRafId !== "undefined" && simRafId !== null) {
      cancelAnimationFrame(simRafId);
    }
  };
}
`;

let nextRoutesContent = '';
if (fs.existsSync(routesDataPath)) {
  const origRoutes = fs.readFileSync(routesDataPath, 'utf8');
  const marker = 'export const ROUTES_DATA: Record<string, RouteEntry> = ';
  const idx = origRoutes.indexOf(marker);
  if (idx !== -1) {
    const prefix = origRoutes.slice(0, idx + marker.length);
    const routesObj = JSON.parse(origRoutes.slice(idx + marker.length).replace(/;\s*$/, ''));
    if (routesObj['/ontology']) {
      routesObj['/ontology'].mainHtml = nextMainHtml;
    }
    nextRoutesContent = `${prefix}${JSON.stringify(routesObj, null, 2)};\n`;
  }
}

const curCtrl = fs.existsSync(ontCtrlPath) ? fs.readFileSync(ontCtrlPath, 'utf8') : '';
const curRoutes = fs.existsSync(routesDataPath) ? fs.readFileSync(routesDataPath, 'utf8') : '';
const hasDrift = curCtrl !== nextControllerTs || (Boolean(nextRoutesContent) && curRoutes !== nextRoutesContent);

if (isCheck) {
  if (hasDrift) {
    console.error('Drift detected in Ontology SPA data! Run: node scripts/sync-ontology-site.mjs');
    process.exit(1);
  }
  console.log('Zero drift: Ontology SPA controller and route match proto/ontology-schema.json.');
  process.exit(0);
}

if (curCtrl !== nextControllerTs) {
  fs.writeFileSync(ontCtrlPath, nextControllerTs, 'utf8');
}
if (nextRoutesContent && curRoutes !== nextRoutesContent) {
  fs.writeFileSync(routesDataPath, nextRoutesContent, 'utf8');
}
console.log(
  `Synced Ontology SPA route & controller (${schema.nodes.length} entity kinds, ${schema.relationships.length} relationship types).`
);
