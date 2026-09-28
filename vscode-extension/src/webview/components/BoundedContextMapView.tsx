import React, { useEffect, useRef, useState, useMemo, useCallback } from 'react';
import cytoscape, { Core, EventObject } from 'cytoscape';
import { GraphData, GraphNode, GraphEdge } from '../../../../proto/types';

export interface BoundedContextFile {
  filePath: string;
  layer?: string;
  pattern?: string;
  operationType?: string;
  capabilityTag?: string;
  summary?: string;
  isPureDomain?: boolean;
}

export interface BoundedContextDetail {
  id: string;
  name: string;
  displayName: string;
  summary?: string;
  fileCount: number;
  pureDomainCount: number;
  purityPercentage: number;
  layers: Record<string, number>;
  patterns: Record<string, number>;
  operations: Record<string, number>;
  targetEntities: string[];
  capabilities: string[];
  emittedEvents: string[];
  handledEvents: string[];
  projects: string[];
  files: BoundedContextFile[];
  bgColor: string;
  borderColor: string;
}

export interface BoundedContextMapViewProps {
  graph: GraphData | null;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
  onTriggerScan?: () => void;
  onTriggerIntent?: () => void;
  onManageModel?: () => void;
  onRefresh?: () => void;
}

const CYTO_STYLES: cytoscape.StylesheetStyle[] = [
  {
    selector: 'node',
    style: {
      'shape': 'ellipse',
      'width': 'data(size)',
      'height': 'data(size)',
      'background-color': 'data(bgColor)',
      'border-width': 3,
      'border-color': 'data(borderColor)',
      'border-opacity': 0.9,
      'label': 'data(displayLabel)',
      'color': '#f8fafc',
      'font-size': '11px',
      'font-weight': 600,
      'font-family': 'system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
      'text-valign': 'center',
      'text-halign': 'center',
      'text-wrap': 'wrap',
      'text-max-width': '100px',
      'text-outline-color': '#0f172a',
      'text-outline-width': 2,
      'text-outline-opacity': 0.8,
      'transition-property': 'background-color, border-color, width, height, border-width',
      'transition-duration': 0.2,
      'cursor': 'pointer',
    } as any,
  },
  {
    selector: 'node:selected',
    style: {
      'border-color': '#38bdf8',
      'border-width': 5,
      'shadow-blur': 25,
      'shadow-color': '#38bdf8',
      'shadow-opacity': 0.9,
    } as any,
  },
  {
    selector: 'edge',
    style: {
      'width': 'mapData(weight, 1, 15, 2, 7)',
      'line-color': 'data(edgeColor)',
      'target-arrow-color': 'data(edgeColor)',
      'target-arrow-shape': 'triangle',
      'curve-style': 'bezier',
      'opacity': 0.75,
      'label': 'data(edgeLabel)',
      'font-size': '9.5px',
      'font-weight': 600,
      'color': '#cbd5e1',
      'text-outline-color': '#0b0f19',
      'text-outline-width': 2,
      'text-rotation': 'autorotate',
      'line-style': 'data(lineStyle)' as any,
    } as any,
  },
  {
    selector: 'edge:selected',
    style: {
      'width': 6,
      'line-color': '#38bdf8',
      'target-arrow-color': '#38bdf8',
      'opacity': 1,
    } as any,
  },
];

export const BoundedContextMapView: React.FC<BoundedContextMapViewProps> = ({
  graph,
  onOpenFile,
  onTriggerScan,
  onTriggerIntent,
  onManageModel,
  onRefresh,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const cyRef = useRef<Core | null>(null);

  const [viewLayout, setViewLayout] = useState<'graph' | 'cards'>('graph');
  const [cytoLayoutName, setCytoLayoutName] = useState<'cose' | 'concentric' | 'circle' | 'grid'>('cose');
  const [searchQuery, setSearchQuery] = useState('');
  const [layerFilter, setLayerFilter] = useState('all');
  const [selectedContext, setSelectedContext] = useState<BoundedContextDetail | null>(null);

  // Parse Bounded Context records from GraphData
  const { contexts, hasIntents, totalIntents, totalPureDomains } = useMemo(() => {
    if (!graph || !graph.nodes || graph.nodes.length === 0) {
      return { contexts: [], hasIntents: false, totalIntents: 0, totalPureDomains: 0 };
    }

    const hasIntentsMeta = graph.metadata?.hasIntents !== 'false';
    const parsedContexts: BoundedContextDetail[] = [];

    for (const node of graph.nodes) {
      if (node.kind !== 'BoundedContext' && !node.properties?.domain) continue;

      const p = node.properties || {};
      const name = p.domain || node.name || 'Domain';
      const displayName = p.displayName || name.replace(/([a-z])([A-Z])/g, '$1 $2');
      const fileCount = parseInt(p.fileCount || '1', 10);
      const pureCount = parseInt(p.pureCount || '0', 10);
      const purityPct = parseFloat(p.purity || (fileCount > 0 ? ((pureCount / fileCount) * 100).toFixed(1) : '0'));

      const safeParseJson = <T,>(jsonStr?: string, fallback: T = [] as any): T => {
        if (!jsonStr) return fallback;
        try {
          return JSON.parse(jsonStr) as T;
        } catch {
          return fallback;
        }
      };

      const targetEntities = safeParseJson<string[]>(p.entities, []);
      const capabilities = safeParseJson<string[]>(p.capabilities, []);
      const emittedEvents = safeParseJson<string[]>(p.events, []);
      const handledEvents = safeParseJson<string[]>(p.handledEvents, []);
      const layers = safeParseJson<Record<string, number>>(p.layers, {});
      const patterns = safeParseJson<Record<string, number>>(p.patterns, {});
      const operations = safeParseJson<Record<string, number>>(p.operations, {});
      const projects = safeParseJson<string[]>(p.projects, []);
      const files = safeParseJson<BoundedContextFile[]>(p.files, []);

      parsedContexts.push({
        id: node.id,
        name,
        displayName,
        summary: p.summary,
        fileCount,
        pureDomainCount: pureCount,
        purityPercentage: purityPct,
        layers,
        patterns,
        operations,
        targetEntities,
        capabilities,
        emittedEvents,
        handledEvents,
        projects,
        files,
        bgColor: p.bgColor || '#3b82f6',
        borderColor: p.borderColor || '#1d4ed8',
      });
    }

    const tIntents = parseInt(graph.metadata?.totalIntents || `${parsedContexts.reduce((acc, c) => acc + c.fileCount, 0)}`, 10);
    const tPure = parseInt(graph.metadata?.totalPureDomains || `${parsedContexts.filter((c) => c.purityPercentage > 50).length}`, 10);

    return {
      contexts: parsedContexts,
      hasIntents: hasIntentsMeta && parsedContexts.length > 0,
      totalIntents: tIntents,
      totalPureDomains: tPure,
    };
  }, [graph]);

  // Filtered contexts based on search & layer filter
  const filteredContexts = useMemo(() => {
    return contexts.filter((c) => {
      if (layerFilter !== 'all') {
        if (!c.layers[layerFilter] || c.layers[layerFilter] === 0) return false;
      }
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase();
        const matchName = c.name.toLowerCase().includes(q) || c.displayName.toLowerCase().includes(q);
        const matchEntity = c.targetEntities.some((e) => e.toLowerCase().includes(q));
        const matchCap = c.capabilities.some((cap) => cap.toLowerCase().includes(q));
        const matchSummary = (c.summary || '').toLowerCase().includes(q);
        return matchName || matchEntity || matchCap || matchSummary;
      }
      return true;
    });
  }, [contexts, layerFilter, searchQuery]);

  // Cytoscape initialization and graph updates
  useEffect(() => {
    if (viewLayout !== 'graph' || !containerRef.current || contexts.length === 0) {
      if (cyRef.current) {
        cyRef.current.destroy();
        cyRef.current = null;
      }
      return;
    }

    const cyNodes = filteredContexts.map((c) => {
      const size = Math.min(120, Math.max(52, 48 + c.fileCount * 3 + c.targetEntities.length * 4));
      return {
        data: {
          id: c.id,
          name: c.name,
          displayLabel: `${c.displayName}\n(${c.fileCount} files)`,
          bgColor: c.bgColor,
          borderColor: c.borderColor,
          size,
          contextData: c,
        },
      };
    });

    const validNodeIds = new Set(filteredContexts.map((c) => c.id));
    const cyEdges: any[] = [];

    for (const edge of graph?.edges || []) {
      if (!validNodeIds.has(edge.source) || !validNodeIds.has(edge.target)) continue;

      const isEvent = edge.category === 'messaging' || edge.kind.toUpperCase().includes('EVENT') || edge.kind.toUpperCase().includes('TOPIC');
      const isDb = edge.category === 'database' || edge.kind.toUpperCase().includes('DB');

      let edgeColor = '#60a5fa'; // Blue for direct calls
      let lineStyle = 'solid';

      if (isEvent) {
        edgeColor = '#f59e0b'; // Amber for events
        lineStyle = 'dashed';
      } else if (isDb) {
        edgeColor = '#06b6d4'; // Cyan for shared database
        lineStyle = 'dotted';
      }

      const count = parseInt(edge.properties?.count || '1', 10);
      cyEdges.push({
        data: {
          id: edge.id,
          source: edge.source,
          target: edge.target,
          weight: Math.min(15, Math.max(1, count)),
          edgeColor,
          lineStyle,
          edgeLabel: count > 1 ? `${edge.kind} (${count})` : edge.kind,
        },
      });
    }

    const cy = cytoscape({
      container: containerRef.current,
      elements: [...cyNodes, ...cyEdges],
      style: CYTO_STYLES,
      layout: {
        name: cytoLayoutName,
        padding: 50,
        animate: true,
        animationDuration: 400,
      } as any,
      minZoom: 0.3,
      maxZoom: 3,
      wheelSensitivity: 0.25,
    });

    cy.on('tap', 'node', (evt: EventObject) => {
      const node = evt.target;
      const data = node.data('contextData') as BoundedContextDetail;
      setSelectedContext(data);
    });

    cy.on('tap', (evt: EventObject) => {
      if (evt.target === cy) {
        setSelectedContext(null);
      }
    });

    cyRef.current = cy;

    return () => {
      cy.destroy();
      cyRef.current = null;
    };
  }, [viewLayout, filteredContexts, graph?.edges, cytoLayoutName]);

  const handleFitView = useCallback(() => {
    if (cyRef.current) {
      cyRef.current.fit(undefined, 40);
    }
  }, []);

  return (
    <div className="bounded-context-view-container">
      {/* Top Floating HUD */}
      <div className="bounded-context-hud">
        <div className="hud-title-section">
          <div className="hud-badge-icon">🧩</div>
          <div className="hud-title-group">
            <h2>Bounded Context Map</h2>
            <div className="hud-subtitle">
              <span>{contexts.length} Domain Contexts</span>
              <span>•</span>
              <span>{totalIntents} Files Analyzed</span>
              {totalPureDomains > 0 && (
                <span className="hud-metrics-pill">
                  🛡️ {totalPureDomains} Pure Domains
                </span>
              )}
            </div>
          </div>
        </div>

        <div className="hud-controls-section">
          <input
            type="text"
            className="hud-search-input"
            placeholder="Search domain, entity, capability..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />

          <select
            className="hud-select"
            value={layerFilter}
            onChange={(e) => setLayerFilter(e.target.value)}
          >
            <option value="all">All Layers</option>
            <option value="Domain">Has Domain Layer</option>
            <option value="Application">Has Application Layer</option>
            <option value="Infrastructure">Has Infrastructure</option>
            <option value="Presentation">Has Presentation</option>
          </select>

          {viewLayout === 'graph' && (
            <select
              className="hud-select"
              value={cytoLayoutName}
              onChange={(e) => setCytoLayoutName(e.target.value as any)}
            >
              <option value="cose">Force Layout</option>
              <option value="concentric">Concentric</option>
              <option value="circle">Circle</option>
              <option value="grid">Grid</option>
            </select>
          )}

          <button
            className={`hud-btn-toggle ${viewLayout === 'graph' ? 'active' : ''}`}
            onClick={() => setViewLayout('graph')}
            title="Graph View"
          >
            🕸️ Graph
          </button>

          <button
            className={`hud-btn-toggle ${viewLayout === 'cards' ? 'active' : ''}`}
            onClick={() => setViewLayout('cards')}
            title="Cards Matrix View"
          >
            🗂️ Cards
          </button>

          {viewLayout === 'graph' && (
            <button
              className="hud-btn-toggle"
              onClick={handleFitView}
              title="Fit to screen"
            >
              ⛶ Fit
            </button>
          )}

          {onRefresh && (
            <button
              className="hud-btn-toggle"
              onClick={onRefresh}
              title="Reload Bounded Contexts"
            >
              🔄 Refresh
            </button>
          )}

          {onTriggerIntent && (
            <button
              className="hud-btn-toggle"
              onClick={onTriggerIntent}
              title="Run local SLM architectural intent distillation"
              style={{ color: '#a78bfa', borderColor: 'rgba(167, 139, 250, 0.4)' }}
            >
              ✨ Distill
            </button>
          )}

          {onManageModel && (
            <button
              className="hud-btn-toggle"
              onClick={onManageModel}
              title="Manage AI Model"
            >
              🧠 Model
            </button>
          )}
        </div>
      </div>

      {/* Main Content Area */}
      <div className="bounded-context-main">
        {!hasIntents || contexts.length === 0 ? (
          <div className="bounded-context-empty">
            <div className="empty-icon">🧠</div>
            <div className="empty-title">No AI Bounded Contexts Discovered Yet</div>
            <div className="empty-desc">
              Run architectural intent distillation using the embedded SLM to automatically classify files
              into business domains, extract ubiquitous entities, and map cross-context dependencies.
            </div>
            <div className="empty-command-box">
              ce intent &nbsp; or &nbsp; ce scan --intent
            </div>
            <div style={{ display: 'flex', gap: '10px', marginTop: '14px', justifyContent: 'center' }}>
              {onRefresh && (
                <button
                  className="empty-action-btn"
                  onClick={onRefresh}
                  style={{ background: '#334155', color: '#e2e8f0', borderColor: '#475569' }}
                >
                  🔄 Check / Reload
                </button>
              )}
              {onTriggerIntent && (
                <button className="empty-action-btn" onClick={onTriggerIntent}>
                  ✨ Distill Architectural Intents
                </button>
              )}
            </div>
          </div>
        ) : viewLayout === 'graph' ? (
          <div className="bounded-context-canvas" ref={containerRef} />
        ) : (
          <div className="bounded-context-cards-container">
            <div className="bounded-context-cards-grid">
              {filteredContexts.map((ctx) => {
                const totalFiles = ctx.fileCount || 1;
                const dPct = ((ctx.layers['Domain'] || 0) / totalFiles) * 100;
                const aPct = ((ctx.layers['Application'] || 0) / totalFiles) * 100;
                const iPct = ((ctx.layers['Infrastructure'] || 0) / totalFiles) * 100;
                const pPct = ((ctx.layers['Presentation'] || 0) / totalFiles) * 100;
                const sPct = ((ctx.layers['Shared'] || 0) / totalFiles) * 100;

                return (
                  <div
                    key={ctx.id}
                    className={`context-card ${selectedContext?.id === ctx.id ? 'selected' : ''}`}
                    style={{ '--card-accent': ctx.bgColor } as React.CSSProperties}
                    onClick={() => setSelectedContext(ctx)}
                  >
                    <div className="card-header">
                      <div className="card-title-group">
                        <div className="card-domain-name">{ctx.displayName}</div>
                        <div className="card-canonical-name">{ctx.name}</div>
                      </div>
                      <div className={`card-purity-badge ${ctx.purityPercentage < 40 ? 'impure' : ''}`}>
                        {ctx.purityPercentage}% Pure
                      </div>
                    </div>

                    {ctx.summary && <div className="card-summary">{ctx.summary}</div>}

                    {/* Stacked Layers Progress Bar */}
                    <div className="card-layers-bar" title="Layer Distribution: Domain (Blue), Application (Green), Infrastructure (Amber), Presentation (Pink)">
                      {dPct > 0 && <div className="layer-bar-segment Domain" style={{ width: `${dPct}%` }} />}
                      {aPct > 0 && <div className="layer-bar-segment Application" style={{ width: `${aPct}%` }} />}
                      {iPct > 0 && <div className="layer-bar-segment Infrastructure" style={{ width: `${iPct}%` }} />}
                      {pPct > 0 && <div className="layer-bar-segment Presentation" style={{ width: `${pPct}%` }} />}
                      {sPct > 0 && <div className="layer-bar-segment Shared" style={{ width: `${sPct}%` }} />}
                    </div>

                    {/* Target Entities Chips */}
                    {ctx.targetEntities.length > 0 && (
                      <div className="card-entities-section">
                        {ctx.targetEntities.slice(0, 4).map((ent, idx) => (
                          <span key={idx} className="entity-chip">
                            🏷️ {ent}
                          </span>
                        ))}
                        {ctx.targetEntities.length > 4 && (
                          <span className="entity-chip">+{ctx.targetEntities.length - 4} more</span>
                        )}
                      </div>
                    )}

                    <div className="card-footer">
                      <div className="card-stat-pill">
                        📄 <strong>{ctx.fileCount}</strong> files
                      </div>
                      <div className="card-stat-pill">
                        ⚡ <strong>{ctx.capabilities.length}</strong> capabilities
                      </div>
                      {ctx.emittedEvents.length > 0 && (
                        <div className="card-stat-pill">
                          📡 <strong>{ctx.emittedEvents.length}</strong> events
                        </div>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* Slide-Out Detail Drawer */}
        {selectedContext && (
          <div className="context-detail-drawer">
            <div className="drawer-header">
              <div className="drawer-title-group">
                <span className="drawer-domain-icon">🏛️</span>
                <div>
                  <h3 className="drawer-domain-name">{selectedContext.displayName}</h3>
                  <div style={{ fontSize: '11px', color: '#64748b' }}>{selectedContext.name}</div>
                </div>
              </div>
              <button
                className="drawer-close-btn"
                onClick={() => setSelectedContext(null)}
                title="Close"
              >
                ✕
              </button>
            </div>

            <div className="drawer-content">
              {/* Summary */}
              {selectedContext.summary && (
                <div className="drawer-summary-card">
                  {selectedContext.summary}
                </div>
              )}

              {/* Metrics Grid */}
              <div className="drawer-metrics-grid">
                <div className="drawer-metric-box">
                  <div className="metric-val">{selectedContext.fileCount}</div>
                  <div className="metric-lbl">Files</div>
                </div>
                <div className="drawer-metric-box">
                  <div className="metric-val" style={{ color: '#34d399' }}>
                    {selectedContext.purityPercentage}%
                  </div>
                  <div className="metric-lbl">Purity</div>
                </div>
                <div className="drawer-metric-box">
                  <div className="metric-val">{selectedContext.targetEntities.length}</div>
                  <div className="metric-lbl">Entities</div>
                </div>
              </div>

              {/* Ubiquitous Language (Entities) */}
              {selectedContext.targetEntities.length > 0 && (
                <div>
                  <div className="drawer-section-title">🏷️ Ubiquitous Language (Entities)</div>
                  <div className="drawer-chips-wrap">
                    {selectedContext.targetEntities.map((ent, idx) => (
                      <span key={idx} className="entity-chip">
                        {ent}
                      </span>
                    ))}
                  </div>
                </div>
              )}

              {/* Capabilities & CQRS Breakdown */}
              {selectedContext.capabilities.length > 0 && (
                <div>
                  <div className="drawer-section-title">⚡ Capabilities & CQRS</div>
                  <div className="drawer-chips-wrap">
                    {selectedContext.capabilities.map((cap, idx) => {
                      const matchedFile = selectedContext.files.find((f) => f.capabilityTag === cap);
                      const op = matchedFile?.operationType || 'Command';
                      return (
                        <div key={idx} className="capability-badge">
                          <span className={`op-badge ${op}`}>{op}</span>
                          <span>{cap}</span>
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}

              {/* Emitted Events */}
              {selectedContext.emittedEvents.length > 0 && (
                <div>
                  <div className="drawer-section-title">📡 Emitted Domain Events</div>
                  <div className="drawer-chips-wrap">
                    {selectedContext.emittedEvents.map((ev, idx) => (
                      <span key={idx} className="capability-badge" style={{ borderColor: 'rgba(245, 158, 11, 0.4)' }}>
                        ⚡ {ev}
                      </span>
                    ))}
                  </div>
                </div>
              )}

              {/* Constituent Files */}
              {selectedContext.files.length > 0 && (
                <div>
                  <div className="drawer-section-title">
                    📂 Files in Context ({selectedContext.files.length})
                  </div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
                    {selectedContext.files.map((f, idx) => (
                      <div
                        key={idx}
                        className="drawer-file-item"
                        onClick={() => onOpenFile?.(f.filePath, 1)}
                        title={`Click to open ${f.filePath}`}
                      >
                        <span className="drawer-file-path">{f.filePath}</span>
                        {f.layer && (
                          <span className={`drawer-file-layer-tag ${f.layer}`}>
                            {f.layer}
                          </span>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
