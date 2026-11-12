import React, { useState, useEffect, useMemo, useRef, useCallback } from 'react';
import {
  OntologyNodeSchemaDto,
  OntologyRelationshipSchemaDto,
  OntologySchemaDocumentDto,
  OntologySchemaResponseDto,
  OntologyWorkspaceStatsDto,
} from '../../../../proto/types';
import {
  BUNDLED_ONTOLOGY_SCHEMA,
  filterOntologyNodes,
  computeEgoNeighborhood,
  getWorkspaceNodeCount,
} from '../domain/ontologyModel';
import { getVsCodeApi } from '../vscodeApi';
import { useServerTransport, webviewTransport } from '../services/webviewTransport';
import { OntologyForceGraph } from './OntologyForceGraph';

export type OntologyDisplayMode = 'graph' | 'layers';

export interface OntologyExplorerViewProps {
  serverHttpUrl?: string;
  onOpenNodeGrid?: (kind: string, layerTitle: string) => void;
  onRunSampleCypher?: (query: string) => void;
}

interface SvgArc {
  key: string;
  d: string;
  category: string;
  direction: 'out' | 'in';
}

const DEFAULT_CATEGORIES = ['control_flow', 'data_flow', 'type_dependency', 'structural'];
const ALL_CATEGORIES = ['control_flow', 'data_flow', 'type_dependency', 'structural'];
const ALL_LAYERS = [4, 3, 2, 1];
const LAYER_SHORT_LABELS: Record<number, string> = {
  4: 'Semantic',
  3: 'Syntax',
  2: 'Boundaries',
  1: 'Physical',
};
const DEFAULT_SELECTED_KIND = 'Endpoint';
const MODE_GRAPH: OntologyDisplayMode = 'graph';
const MODE_LAYERS: OntologyDisplayMode = 'layers';
const ONTOLOGY_API_PATH = '/api/ontology';
const EMPTY_QUERY = '';
const SINGLE_SPACE = ' ';

export const OntologyExplorerView: React.FC<OntologyExplorerViewProps> = ({
  serverHttpUrl,
  onOpenNodeGrid,
  onRunSampleCypher,
}) => {
  const vscodeApi = getVsCodeApi();
  const transportState = useServerTransport();
  const effectiveHttpUrl = serverHttpUrl || transportState.httpUrl || EMPTY_QUERY;

  const [viewMode, setViewMode] = useState<OntologyDisplayMode>(MODE_GRAPH);
  const [schema, setSchema] = useState<OntologySchemaDocumentDto>(BUNDLED_ONTOLOGY_SCHEMA);
  const [workspace, setWorkspace] = useState<OntologyWorkspaceStatsDto | null>(null);
  const [selectedKind, setSelectedKind] = useState<string>(DEFAULT_SELECTED_KIND);
  const [searchQuery, setSearchQuery] = useState<string>(EMPTY_QUERY);
  const [activeCategories, setActiveCategories] = useState<Set<string>>(
    () => new Set(DEFAULT_CATEGORIES)
  );
  const [activeLayers, setActiveLayers] = useState<Set<number>>(
    () => new Set(ALL_LAYERS)
  );
  const [onlyActiveInWorkspace, setOnlyActiveInWorkspace] = useState<boolean>(false);
  const [showCommonProps, setShowCommonProps] = useState<boolean>(false);
  const [copiedCypher, setCopiedCypher] = useState<boolean>(false);
  const [svgArcs, setSvgArcs] = useState<SvgArc[]>([]);

  const stageRef = useRef<HTMLDivElement | null>(null);
  const cardRefs = useRef<Map<string, HTMLButtonElement>>(new Map());

  useEffect(() => {
    if (!effectiveHttpUrl) return;
    let cancelled = false;

    webviewTransport
      .fetchJson<OntologySchemaResponseDto>(ONTOLOGY_API_PATH)
      .then((res) => {
        if (cancelled || !res) return;
        if (res.schema && Array.isArray(res.schema.nodes) && res.schema.nodes.length > 0) {
          setSchema(res.schema);
        }
        if (res.workspace) {
          setWorkspace(res.workspace);
        }
      })
      .catch(() => {
        // Bundled build-time ontology-schema.json remains active when server is offline
      });

    return () => {
      cancelled = true;
    };
  }, [effectiveHttpUrl]);

  const toggleCategory = useCallback((catId: string) => {
    setActiveCategories((prev) => {
      const next = new Set(prev);
      if (next.has(catId)) {
        next.delete(catId);
      } else {
        next.add(catId);
      }
      return next;
    });
  }, []);

  const toggleAllCategories = useCallback(() => {
    setActiveCategories((prev) =>
      prev.size === ALL_CATEGORIES.length ? new Set(['control_flow', 'data_flow', 'type_dependency']) : new Set(ALL_CATEGORIES)
    );
  }, []);

  const toggleLayer = useCallback((layerId: number) => {
    setActiveLayers((prev) => {
      const next = new Set(prev);
      if (next.has(layerId)) {
        next.delete(layerId);
        return next.size === 0 ? new Set(ALL_LAYERS) : next;
      }
      next.add(layerId);
      return next;
    });
  }, []);

  const handleSelectKind = useCallback(
    (kind: string) => {
      const target = schema.nodes.find((n) => n.kind === kind);
      if (target) {
        setActiveLayers((prev) => {
          if (prev.has(target.layerId)) return prev;
          const next = new Set(prev);
          next.add(target.layerId);
          return next;
        });
      }
      setSelectedKind(kind);
    },
    [schema.nodes]
  );

  const visibleNodes = useMemo(
    () =>
      filterOntologyNodes(
        schema,
        { searchQuery, activeCategories, onlyActiveInWorkspace, activeLayers },
        workspace
      ),
    [schema, searchQuery, activeCategories, onlyActiveInWorkspace, activeLayers, workspace]
  );

  const visibleKindSet = useMemo(
    () => new Set(visibleNodes.map((n) => n.kind)),
    [visibleNodes]
  );

  const selectedNode = useMemo(
    () =>
      visibleNodes.find((n) => n.kind === selectedKind) ||
      visibleNodes[0] ||
      schema.nodes.find((n) => n.kind === selectedKind) ||
      schema.nodes[0],
    [visibleNodes, schema.nodes, selectedKind]
  );

  const ego = useMemo(
    () => computeEgoNeighborhood(schema, selectedNode?.kind || selectedKind, activeCategories, workspace),
    [schema, selectedNode, selectedKind, activeCategories, workspace]
  );

  const activeWorkspaceKindsCount = useMemo(() => {
    if (!workspace) return 0;
    return schema.nodes.filter((n) => getWorkspaceNodeCount(workspace, n.kind) > 0).length;
  }, [schema, workspace]);

  const activeSelectedKind = selectedNode?.kind || selectedKind;

  const recomputeArcs = useCallback(() => {
    const stageEl = stageRef.current;
    if (!stageEl || !activeSelectedKind || !visibleKindSet.has(activeSelectedKind)) {
      setSvgArcs([]);
      return;
    }

    const srcEl = cardRefs.current.get(activeSelectedKind);
    if (!srcEl) {
      setSvgArcs([]);
      return;
    }

    const stageRect = stageEl.getBoundingClientRect();
    const scrollLeft = stageEl.scrollLeft;
    const scrollTop = stageEl.scrollTop;
    const sRect = srcEl.getBoundingClientRect();
    const sx = sRect.left - stageRect.left + scrollLeft + sRect.width / 2;
    const sy = sRect.top - stageRect.top + scrollTop + sRect.height / 2;

    const nextArcs: SvgArc[] = [];
    const seenPeers = new Set<string>();

    for (const conn of ego.connections) {
      if (conn.peerKind === activeSelectedKind || !visibleKindSet.has(conn.peerKind)) continue;
      const peerKey = `${conn.direction}:${conn.peerKind}:${conn.category}`;
      if (seenPeers.has(peerKey)) continue;
      seenPeers.add(peerKey);

      const dstEl = cardRefs.current.get(conn.peerKind);
      if (!dstEl) continue;

      const dRect = dstEl.getBoundingClientRect();
      const tx = dRect.left - stageRect.left + scrollLeft + dRect.width / 2;
      const ty = dRect.top - stageRect.top + scrollTop + dRect.height / 2;

      const dy = ty - sy;
      const dx = tx - sx;
      const curvature = Math.max(36, Math.min(120, Math.abs(dy) * 0.45 + Math.abs(dx) * 0.12));
      const c1y = sy + (dy >= 0 ? curvature : -curvature);
      const c2y = ty - (dy >= 0 ? curvature : -curvature);

      const d =
        Math.abs(dy) < 28
          ? `M ${sx} ${sy} Q ${(sx + tx) / 2} ${sy - 42} ${tx} ${ty}`
          : `M ${sx} ${sy} C ${sx} ${c1y}, ${tx} ${c2y}, ${tx} ${ty}`;

      nextArcs.push({
        key: peerKey,
        d,
        category: conn.category,
        direction: conn.direction,
      });
    }

    setSvgArcs(nextArcs);
  }, [activeSelectedKind, ego.connections, visibleKindSet]);

  useEffect(() => {
    const raf = requestAnimationFrame(recomputeArcs);
    const stageEl = stageRef.current;
    if (!stageEl) return () => cancelAnimationFrame(raf);

    const observer = typeof ResizeObserver !== 'undefined' ? new ResizeObserver(recomputeArcs) : null;
    observer?.observe(stageEl);
    window.addEventListener('resize', recomputeArcs);

    return () => {
      cancelAnimationFrame(raf);
      observer?.disconnect();
      window.removeEventListener('resize', recomputeArcs);
    };
  }, [recomputeArcs, visibleNodes, viewMode]);

  const handleCopyCypher = useCallback(() => {
    if (!selectedNode) return;
    const text = selectedNode.sampleCypher;
    if (vscodeApi) {
      vscodeApi.postMessage({ type: 'COPY_TO_CLIPBOARD', text });
    } else if (typeof navigator !== 'undefined' && navigator.clipboard) {
      navigator.clipboard.writeText(text).catch(() => {});
    }
    setCopiedCypher(true);
    setTimeout(() => setCopiedCypher(false), 1600);
  }, [selectedNode, vscodeApi]);

  const selectedLayerMeta = useMemo(
    () => schema.layers.find((l) => l.layerId === selectedNode?.layerId),
    [schema.layers, selectedNode]
  );

  const selectedWorkspaceCount = selectedNode
    ? getWorkspaceNodeCount(workspace, selectedNode.kind)
    : 0;

  const visibleLayerDefs = useMemo(
    () => schema.layers.filter((layer) => activeLayers.has(layer.layerId)),
    [schema.layers, activeLayers]
  );

  return (
    <div className="ontology-explorer-root">
      <OntologyFilterToolbar
        schema={schema}
        viewMode={viewMode}
        searchQuery={searchQuery}
        activeLayers={activeLayers}
        activeCategories={activeCategories}
        workspace={workspace}
        onlyActiveInWorkspace={onlyActiveInWorkspace}
        activeWorkspaceKindsCount={activeWorkspaceKindsCount}
        onChangeViewMode={setViewMode}
        onChangeSearchQuery={setSearchQuery}
        onToggleLayer={toggleLayer}
        onToggleCategory={toggleCategory}
        onToggleAllCategories={toggleAllCategories}
        onToggleWorkspaceActive={() => setOnlyActiveInWorkspace((v) => !v)}
      />

      {/* Main Split Body: Interactive Graph or 4-Layer Swimlanes + Schema Inspector */}
      <div className="ontology-body-split">
        {viewMode === MODE_GRAPH ? (
          <OntologyForceGraph
            schema={schema}
            visibleNodes={visibleNodes}
            activeCategories={activeCategories}
            workspace={workspace}
            onlyActiveInWorkspace={onlyActiveInWorkspace}
            selectedKind={selectedNode?.kind || DEFAULT_SELECTED_KIND}
            onSelectKind={handleSelectKind}
          />
        ) : (
        <div
          ref={stageRef}
          className="ontology-swimlanes-stage"
          onScroll={recomputeArcs}
        >
          <svg className="ontology-arcs-svg" aria-hidden="true">
            {svgArcs.map((arc) => (
              <path
                key={arc.key}
                d={arc.d}
                className={`ontology-arc arc-${arc.category} ${arc.direction === 'in' ? 'arc-inbound' : 'arc-outbound'}`}
              />
            ))}
          </svg>

          {visibleLayerDefs.map((layer) => {
            const layerNodes = visibleNodes.filter((n) => n.layerId === layer.layerId);
            return (
              <section
                key={layer.key}
                className={`ontology-layer-band layer-band-${layer.layerId}`}
              >
                <div className="ontology-layer-header">
                  <div className="ontology-layer-title-group">
                    <span className={`ontology-layer-tag tag-l${layer.layerId}`}>
                      L{layer.layerId}
                    </span>
                    <h2 className="ontology-layer-name">{layer.name}</h2>
                    <span className="ontology-layer-desc">{layer.description}</span>
                  </div>
                  <span className="ontology-layer-count">
                    {layerNodes.length} {layerNodes.length === 1 ? 'entity' : 'entities'}
                  </span>
                </div>

                {layerNodes.length === 0 ? (
                  <div className="ontology-layer-empty">
                    No entities in Layer {layer.layerId} match the active filter.
                  </div>
                ) : (
                  <div className="ontology-cards-grid">
                    {layerNodes.map((node) => {
                      const isSelected = node.kind === selectedNode?.kind;
                      const peerBadges = ego.badgesByKind.get(node.kind) || [];
                      const isNeighbor = peerBadges.length > 0;
                      const isDimmed = Boolean(selectedNode && !isSelected && !isNeighbor);
                      const wsCount = getWorkspaceNodeCount(workspace, node.kind);

                      return (
                        <button
                          key={node.kind}
                          type="button"
                          ref={(el) => {
                            if (el) cardRefs.current.set(node.kind, el);
                            else cardRefs.current.delete(node.kind);
                          }}
                          className={[
                            'ontology-node-card',
                            isSelected ? 'is-selected' : '',
                            isNeighbor ? 'is-neighbor' : '',
                            isDimmed ? 'is-dimmed' : '',
                          ]
                            .filter(Boolean)
                            .join(' ')}
                          onClick={() => handleSelectKind(node.kind)}
                        >
                          <div className="ontology-card-top">
                            <span className="ontology-card-kind">{node.kind}</span>
                            {wsCount > 0 ? (
                              <span className="ontology-card-count is-populated" title={`${wsCount.toLocaleString()} instances in workspace`}>
                                {wsCount.toLocaleString()}
                              </span>
                            ) : (
                              <span className="ontology-card-meta">
                                {node.properties.length}p · {node.outEdges.length + node.inEdges.length}e
                              </span>
                            )}
                          </div>
                          <div className="ontology-card-plural">{node.pluralLabel}</div>

                          {peerBadges.length > 0 && !isSelected && (
                            <div className="ontology-card-rel-badges">
                              {peerBadges.slice(0, 2).map((b, idx) => (
                                <span
                                  key={`${b.direction}-${b.rel}-${idx}`}
                                  className={`ontology-rel-chip chip-${b.category}`}
                                >
                                  {b.direction === 'out' ? '→' : '←'} {b.rel}
                                </span>
                              ))}
                              {peerBadges.length > 2 && (
                                <span className="ontology-rel-more">+{peerBadges.length - 2}</span>
                              )}
                            </div>
                          )}
                        </button>
                      );
                    })}
                  </div>
                )}
              </section>
            );
          })}
        </div>
        )}

        {/* Right-Hand Schema & Cypher Inspector */}
        {selectedNode && (
          <OntologyEntityInspector
            schema={schema}
            selectedNode={selectedNode}
            selectedLayerTitle={selectedLayerMeta?.title}
            selectedWorkspaceCount={selectedWorkspaceCount}
            workspaceHasNodes={Boolean(workspace && workspace.totalNodes > 0)}
            searchQuery={searchQuery}
            showCommonProps={showCommonProps}
            copiedCypher={copiedCypher}
            relationshipMeta={ego.relationshipMeta}
            onToggleCommonProps={() => setShowCommonProps((v) => !v)}
            onSelectKind={handleSelectKind}
            onCopyCypher={handleCopyCypher}
            onOpenNodeGrid={onOpenNodeGrid}
            onRunSampleCypher={onRunSampleCypher}
          />
        )}
      </div>
    </div>
  );
};

interface OntologyFilterToolbarProps {
  schema: OntologySchemaDocumentDto;
  viewMode: OntologyDisplayMode;
  searchQuery: string;
  activeLayers: Set<number>;
  activeCategories: Set<string>;
  workspace: OntologyWorkspaceStatsDto | null;
  onlyActiveInWorkspace: boolean;
  activeWorkspaceKindsCount: number;
  onChangeViewMode: (mode: OntologyDisplayMode) => void;
  onChangeSearchQuery: (q: string) => void;
  onToggleLayer: (layerId: number) => void;
  onToggleCategory: (catId: string) => void;
  onToggleAllCategories: () => void;
  onToggleWorkspaceActive: () => void;
}

const OntologyFilterToolbar: React.FC<OntologyFilterToolbarProps> = ({
  schema,
  viewMode,
  searchQuery,
  activeLayers,
  activeCategories,
  workspace,
  onlyActiveInWorkspace,
  activeWorkspaceKindsCount,
  onChangeViewMode,
  onChangeSearchQuery,
  onToggleLayer,
  onToggleCategory,
  onToggleAllCategories,
  onToggleWorkspaceActive,
}) => (
  <div className="ontology-filter-bar">
    <div className="ontology-mode-switch" role="tablist" aria-label="Ontology visualization mode">
      <button
        type="button"
        role="tab"
        aria-selected={viewMode === MODE_GRAPH}
        className={`ontology-mode-btn ${viewMode === MODE_GRAPH ? 'is-active' : ''}`}
        onClick={() => onChangeViewMode(MODE_GRAPH)}
      >
        Graph
      </button>
      <button
        type="button"
        role="tab"
        aria-selected={viewMode === MODE_LAYERS}
        className={`ontology-mode-btn ${viewMode === MODE_LAYERS ? 'is-active' : ''}`}
        onClick={() => onChangeViewMode(MODE_LAYERS)}
      >
        4 Layers
      </button>
    </div>

    <div className="ontology-search-wrap">
      <input
        type="search"
        className="ontology-search-input"
        placeholder="Filter entities, properties (e.g. loop_depth, route_template), or edges..."
        value={searchQuery}
        onChange={(e) => onChangeSearchQuery(e.target.value)}
      />
      {searchQuery && (
        <button
          type="button"
          className="ontology-clear-btn"
          onClick={() => onChangeSearchQuery(EMPTY_QUERY)}
          title="Clear filter"
        >
          ×
        </button>
      )}
    </div>

    <div className="ontology-layer-pills" role="group" aria-label="Ontology layer filters">
      {schema.layers.map((layer) => {
        const isOn = activeLayers.has(layer.layerId);
        const shortLabel = LAYER_SHORT_LABELS[layer.layerId] || layer.name;
        return (
          <button
            key={layer.layerId}
            type="button"
            aria-pressed={isOn}
            className={`ontology-layer-pill-btn layer-pill-${layer.layerId} ${isOn ? 'is-active' : ''}`}
            onClick={() => onToggleLayer(layer.layerId)}
            title={`${layer.title} — ${layer.description}`}
          >
            <span className="ontology-layer-pill-tag">L{layer.layerId}</span>
            <span>{shortLabel}</span>
          </button>
        );
      })}
    </div>

    <div className="ontology-category-pills" role="group" aria-label="Relationship category filters">
      {schema.edgeCategories.map((cat) => {
        const isOn = activeCategories.has(cat.id);
        return (
          <button
            key={cat.id}
            type="button"
            className={`ontology-cat-pill cat-${cat.id} ${isOn ? 'is-active' : ''}`}
            onClick={() => onToggleCategory(cat.id)}
            title={cat.description}
          >
            <span className={`ontology-cat-dot dot-${cat.id}`} />
            <span>{cat.label}</span>
          </button>
        );
      })}
      <button
        type="button"
        className="ontology-cat-pill ontology-cat-reset"
        onClick={onToggleAllCategories}
        title="Toggle structural containment links"
      >
        {activeCategories.size === ALL_CATEGORIES.length ? 'Hide Containment' : 'All Links'}
      </button>
    </div>

    <div className="ontology-workspace-summary">
      {workspace && workspace.totalNodes > 0 ? (
        <button
          type="button"
          className={`ontology-ws-toggle ${onlyActiveInWorkspace ? 'is-active' : ''}`}
          onClick={onToggleWorkspaceActive}
          title="Filter schema to entity types collected in the current workspace"
        >
          <span>Active in Workspace</span>
          <span className="ontology-ws-badge">
            {activeWorkspaceKindsCount}/{schema.nodes.length}
          </span>
        </button>
      ) : (
        <span className="ontology-schema-summary-tag">
          {schema.nodes.length} entity kinds · {schema.relationships.length} relationship types
        </span>
      )}
    </div>
  </div>
);

interface OntologyEntityInspectorProps {
  schema: OntologySchemaDocumentDto;
  selectedNode: OntologyNodeSchemaDto;
  selectedLayerTitle?: string;
  selectedWorkspaceCount: number;
  workspaceHasNodes: boolean;
  searchQuery: string;
  showCommonProps: boolean;
  copiedCypher: boolean;
  relationshipMeta: Map<string, OntologyRelationshipSchemaDto>;
  onToggleCommonProps: () => void;
  onSelectKind: (kind: string) => void;
  onCopyCypher: () => void;
  onOpenNodeGrid?: (kind: string, layerTitle: string) => void;
  onRunSampleCypher?: (query: string) => void;
}

const OntologyEntityInspector: React.FC<OntologyEntityInspectorProps> = ({
  schema,
  selectedNode,
  selectedLayerTitle,
  selectedWorkspaceCount,
  workspaceHasNodes,
  searchQuery,
  showCommonProps,
  copiedCypher,
  relationshipMeta,
  onToggleCommonProps,
  onSelectKind,
  onCopyCypher,
  onOpenNodeGrid,
  onRunSampleCypher,
}) => (
  <aside className="ontology-inspector" aria-label="Ontology Entity Inspector">
    <div className="ontology-inspector-head">
      <div className="ontology-inspector-badges">
        <span className={`ontology-layer-tag tag-l${selectedNode.layerId}`}>
          Layer {selectedNode.layerId}
        </span>
        <span className="ontology-inspector-plural">{selectedNode.pluralLabel}</span>
      </div>
      <div className="ontology-inspector-title-row">
        <h3 className="ontology-inspector-title">:{selectedNode.kind}</h3>
        {selectedWorkspaceCount > 0 && onOpenNodeGrid && (
          <button
            type="button"
            className="ontology-open-grid-btn"
            onClick={() =>
              onOpenNodeGrid(
                selectedNode.kind,
                selectedLayerTitle || `Layer ${selectedNode.layerId}`
              )
            }
            title={`Inspect ${selectedWorkspaceCount.toLocaleString()} ${selectedNode.kind} nodes in workspace`}
          >
            Browse {selectedWorkspaceCount.toLocaleString()} →
          </button>
        )}
      </div>
      <p className="ontology-inspector-purpose">{selectedNode.purpose}</p>
      {selectedNode.idScheme && (
        <div className="ontology-urn-box">
          <span className="ontology-urn-label">URN</span>
          <code className="ontology-urn-code">{selectedNode.idScheme}</code>
        </div>
      )}
    </div>

    <div className="ontology-inspector-scroll">
      <div className="ontology-section">
        <div className="ontology-section-head">
          <span>Collected Properties ({selectedNode.properties.length})</span>
          <button
            type="button"
            className="ontology-link-btn"
            onClick={onToggleCommonProps}
          >
            {showCommonProps ? 'Hide base fields' : '+4 base fields'}
          </button>
        </div>

        {showCommonProps && (
          <div className="ontology-props-table ontology-common-props">
            {schema.commonProperties.map((cp) => (
              <div key={cp.name} className="ontology-prop-row is-common">
                <div className="ontology-prop-key-row">
                  <code className="ontology-prop-name">{cp.name}</code>
                  <span className="ontology-prop-type">{cp.type}</span>
                </div>
                <div className="ontology-prop-desc">{cp.description}</div>
              </div>
            ))}
          </div>
        )}

        <div className="ontology-props-table">
          {selectedNode.properties.map((prop) => {
            const q = searchQuery.trim().toLowerCase();
            const isMatch =
              Boolean(q) &&
              (prop.name.toLowerCase().includes(q) ||
                prop.description.toLowerCase().includes(q));
            return (
              <div
                key={prop.name}
                className={`ontology-prop-row ${isMatch ? 'is-highlighted' : ''}`}
              >
                <div className="ontology-prop-key-row">
                  <code className="ontology-prop-name">{prop.name}</code>
                  <span className="ontology-prop-type">{prop.type}</span>
                </div>
                <div className="ontology-prop-desc">{prop.description}</div>
              </div>
            );
          })}
        </div>
      </div>

      <div className="ontology-section">
        <div className="ontology-section-head">
          <span>Outbound Relationships ({selectedNode.outEdges.length})</span>
        </div>
        {selectedNode.outEdges.length === 0 ? (
          <div className="ontology-empty-note">No outbound edges declared.</div>
        ) : (
          <div className="ontology-edges-list">
            {selectedNode.outEdges.map((edge) => {
              const relMeta = relationshipMeta.get(edge.rel);
              const edgeProps = relMeta?.properties || [];
              return (
                <div key={`${edge.rel}->${edge.toKind}`} className="ontology-edge-item">
                  <div className="ontology-edge-pattern">
                    <span className={`ontology-rel-chip chip-${edge.category}`}>
                      -[:{edge.rel}]-&gt;
                    </span>
                    <button
                      type="button"
                      className="ontology-peer-btn"
                      onClick={() => onSelectKind(edge.toKind)}
                      title={`Focus :${edge.toKind}`}
                    >
                      :{edge.toKind}
                    </button>
                  </div>
                  {edge.description && (
                    <div className="ontology-edge-desc">{edge.description}</div>
                  )}
                  {edgeProps.length > 1 && (
                    <div className="ontology-edge-props">
                      {edgeProps.map((ep) => (
                        <code key={ep.name} className="ontology-edge-prop-pill" title={ep.description}>
                          r.{ep.name}
                        </code>
                      ))}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>

      <div className="ontology-section">
        <div className="ontology-section-head">
          <span>Inbound Relationships ({selectedNode.inEdges.length})</span>
        </div>
        {selectedNode.inEdges.length === 0 ? (
          <div className="ontology-empty-note">No inbound edges declared.</div>
        ) : (
          <div className="ontology-edges-list">
            {selectedNode.inEdges.map((edge) => (
              <div key={`${edge.fromKind}-${edge.rel}`} className="ontology-edge-item">
                <div className="ontology-edge-pattern">
                  <button
                    type="button"
                    className="ontology-peer-btn"
                    onClick={() => onSelectKind(edge.fromKind)}
                    title={`Focus :${edge.fromKind}`}
                  >
                    :{edge.fromKind}
                  </button>
                  <span className={`ontology-rel-chip chip-${edge.category}`}>
                    -[:{edge.rel}]-&gt;
                  </span>
                </div>
                {edge.description && (
                  <div className="ontology-edge-desc">{edge.description}</div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="ontology-section">
        <div className="ontology-section-head">
          <span>OpenCypher Query</span>
          <div className="ontology-cypher-actions">
            <button
              type="button"
              className="ontology-link-btn"
              onClick={onCopyCypher}
            >
              {copiedCypher ? 'Copied' : 'Copy'}
            </button>
            {onRunSampleCypher && workspaceHasNodes && (
              <button
                type="button"
                className="ontology-link-btn is-accent"
                onClick={() => onRunSampleCypher(selectedNode.sampleCypher.replace(/\n/g, SINGLE_SPACE))}
              >
                Load in Query Bar
              </button>
            )}
          </div>
        </div>
        <pre className="ontology-cypher-pre">
          <code>{selectedNode.sampleCypher}</code>
        </pre>
      </div>
    </div>
  </aside>
);
