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
      'width': 'data(weight)',
      'line-color': 'data(edgeColor)',
      'target-arrow-color': 'data(edgeColor)',
      'target-arrow-shape': 'triangle',
      'arrow-scale': 1.15,
      'curve-style': 'bezier',
      'control-point-step-size': 38,
      'opacity': 0.8,
      'label': 'data(edgeLabel)',
      'font-size': '10px',
      'font-weight': 700,
      'color': '#f8fafc',
      'text-outline-color': '#090d16',
      'text-outline-width': 2.5,
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
      'z-index': 999,
    } as any,
  },
  {
    selector: 'edge.edge-straight',
    style: {
      'curve-style': 'straight',
    } as any,
  },
  {
    selector: 'edge.edge-bezier',
    style: {
      'curve-style': 'bezier',
      'control-point-step-size': 38,
    } as any,
  },
  {
    selector: '.faded',
    style: {
      'opacity': 0.12,
      'text-opacity': 0.1,
    } as any,
  },
  {
    selector: 'node.highlighted',
    style: {
      'border-color': '#38bdf8',
      'border-width': 5,
      'shadow-blur': 25,
      'shadow-color': '#38bdf8',
      'shadow-opacity': 0.9,
      'z-index': 9999,
    } as any,
  },
  {
    selector: 'edge.highlighted',
    style: {
      'opacity': 1,
      'line-color': '#38bdf8',
      'target-arrow-color': '#38bdf8',
      'z-index': 9998,
      'width': 'data(weight)',
    } as any,
  },
];

export interface BundledEdgeDetail {
  id: string;
  source: string;
  target: string;
  sourceDomain: string;
  targetDomain: string;
  totalCalls: number;
  category: string;
  details: string[];
}

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

  const [cytoLayoutName, setCytoLayoutName] = useState<'cose' | 'concentric' | 'circle' | 'grid'>('cose');
  const [lineCurveMode, setLineCurveMode] = useState<'bezier' | 'straight'>('bezier');
  const [bezierCurvature, setBezierCurvature] = useState<number>(40);
  const [searchQuery, setSearchQuery] = useState('');
  const [layerFilter, setLayerFilter] = useState('all');
  const [minCallsFilter, setMinCallsFilter] = useState<number>(2);
  const [selectedContext, setSelectedContext] = useState<BoundedContextDetail | null>(null);
  const [selectedEdge, setSelectedEdge] = useState<BundledEdgeDetail | null>(null);

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
    if (!containerRef.current || contexts.length === 0) {
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
    const nodeDomainMap = new Map<string, string>();
    for (const c of filteredContexts) {
      nodeDomainMap.set(c.id, c.displayName || c.name);
    }

    // Directional edge bundling map: key = `${edge.source}->${edge.target}`
    const bundledMap = new Map<string, BundledEdgeDetail>();

    for (const edge of graph?.edges || []) {
      if (!validNodeIds.has(edge.source) || !validNodeIds.has(edge.target)) continue;
      if (edge.source === edge.target) continue;

      const key = `${edge.source}->${edge.target}`;
      const count = parseInt(edge.properties?.count || '1', 10);
      const cat = edge.category || (edge.kind.toUpperCase().includes('EVENT') ? 'messaging' : 'service_call');
      const label = edge.properties?.label || edge.kind || 'calls';
      const detailStr = edge.properties?.details ? edge.properties.details : `${label} (${count})`;

      const existing = bundledMap.get(key);
      if (existing) {
        existing.totalCalls += count;
        if (!existing.details.includes(detailStr)) {
          existing.details.push(detailStr);
        }
      } else {
        bundledMap.set(key, {
          id: key,
          source: edge.source,
          target: edge.target,
          sourceDomain: nodeDomainMap.get(edge.source) || edge.source,
          targetDomain: nodeDomainMap.get(edge.target) || edge.target,
          totalCalls: count,
          category: cat,
          details: [detailStr],
        });
      }
    }

    const cyEdges: any[] = [];
    for (const bEdge of bundledMap.values()) {
      if (bEdge.totalCalls < minCallsFilter) continue;

      const isEvent = bEdge.category === 'messaging';
      const isDb = bEdge.category === 'database';

      let edgeColor = '#60a5fa'; // Blue for direct calls
      let lineStyle = 'solid';

      if (isEvent) {
        edgeColor = '#f59e0b'; // Amber for events
        lineStyle = 'dashed';
      } else if (isDb) {
        edgeColor = '#06b6d4'; // Cyan for shared database
        lineStyle = 'dotted';
      }

      // Proportional logarithmic highway width from 2px up to 13px
      const weight = Math.min(13, Math.max(2, Math.round(Math.log2(bEdge.totalCalls + 1) * 2.5)));

      cyEdges.push({
        data: {
          id: bEdge.id,
          source: bEdge.source,
          target: bEdge.target,
          weight,
          edgeColor,
          lineStyle,
          edgeLabel: `${bEdge.totalCalls}`,
          edgeData: bEdge,
        },
        classes: lineCurveMode === 'straight' ? 'edge-straight' : 'edge-bezier',
      });
    }

    const cy = cytoscape({
      container: containerRef.current,
      elements: [...cyNodes, ...cyEdges],
      style: CYTO_STYLES,
      layout: {
        name: cytoLayoutName,
        padding: 60,
        animate: false,
        ...(cytoLayoutName === 'cose' ? {
          nodeRepulsion: () => 6000,
          idealEdgeLength: () => 120,
          edgeElasticity: () => 32,
          nestingFactor: 1.2,
          gravity: 0.25,
          numIter: 150,
          initialTemp: 100,
          coolingFactor: 0.95,
          minTemp: 1.0,
          randomize: false,
        } : {}),
      } as any,
      minZoom: 0.25,
      maxZoom: 3,
      wheelSensitivity: 0.25,
    });

    cy.on('tap', 'node', (evt: EventObject) => {
      const node = evt.target;
      const data = node.data('contextData') as BoundedContextDetail;
      setSelectedEdge(null);
      setSelectedContext(data);

      // Ego-graph isolation: fade all other nodes/edges, highlight ego node and direct neighbors
      cy.batch(() => {
        cy.elements().removeClass('highlighted').addClass('faded');
        node.removeClass('faded').addClass('highlighted');
        const connectedEdges = node.connectedEdges();
        connectedEdges.removeClass('faded').addClass('highlighted');
        connectedEdges.connectedNodes().removeClass('faded').addClass('highlighted');
      });
    });

    cy.on('tap', 'edge', (evt: EventObject) => {
      const edge = evt.target;
      const data = edge.data('edgeData') as BundledEdgeDetail;
      setSelectedContext(null);
      setSelectedEdge(data);

      // Highlight edge and its 2 endpoints
      cy.batch(() => {
        cy.elements().removeClass('highlighted').addClass('faded');
        edge.removeClass('faded').addClass('highlighted');
        edge.connectedNodes().removeClass('faded').addClass('highlighted');
      });
    });

    cy.on('tap', (evt: EventObject) => {
      if (evt.target === cy) {
        setSelectedContext(null);
        setSelectedEdge(null);
        cy.batch(() => {
          cy.elements().removeClass('faded').removeClass('highlighted');
        });
      }
    });

    cy.style()
      .selector('edge.edge-bezier')
      .style('control-point-step-size', bezierCurvature)
      .update();

    cyRef.current = cy;

    return () => {
      cy.destroy();
      cyRef.current = null;
    };
  }, [filteredContexts, graph?.edges, cytoLayoutName, minCallsFilter, lineCurveMode]);

  // Dynamic live adjustment of bezier curvature without recreating Cytoscape
  useEffect(() => {
    if (!cyRef.current) return;
    cyRef.current.style()
      .selector('edge.edge-bezier')
      .style('control-point-step-size', bezierCurvature)
      .update();
  }, [bezierCurvature]);

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

          <select
            className="hud-select"
            value={minCallsFilter}
            onChange={(e) => setMinCallsFilter(parseInt(e.target.value, 10))}
            title="Minimum calls threshold to reduce noise"
          >
            <option value="1">All Calls (1+)</option>
            <option value="2">≥ 2 Calls (Filter 1-offs)</option>
            <option value="5">≥ 5 Calls (Core flows)</option>
            <option value="10">≥ 10 Calls (Major highways)</option>
          </select>

          <select
            className="hud-select"
            value={lineCurveMode}
            onChange={(e) => setLineCurveMode(e.target.value as any)}
            title="Line Curve Style"
          >
            <option value="bezier">〰️ Bezier Curves</option>
            <option value="straight">📏 Straight Lines</option>
          </select>

          {lineCurveMode === 'bezier' && (
            <div className="hud-slider-group" title={`Bezier Curvature: ${bezierCurvature}px`}>
              <span className="hud-slider-label">Curve:</span>
              <button
                type="button"
                className="hud-step-btn"
                onClick={() => setBezierCurvature((prev) => Math.max(10, prev - 5))}
                title="Decrease curvature (-)"
              >
                -
              </button>
              <input
                type="range"
                min="10"
                max="100"
                step="5"
                value={bezierCurvature}
                onChange={(e) => setBezierCurvature(parseInt(e.target.value, 10))}
                className="hud-slider"
              />
              <button
                type="button"
                className="hud-step-btn"
                onClick={() => setBezierCurvature((prev) => Math.min(100, prev + 5))}
                title="Increase curvature (+)"
              >
                +
              </button>
              <span
                className="hud-value-badge"
                onClick={() => setBezierCurvature(40)}
                title="Reset curvature to 40px"
              >
                {bezierCurvature}px
              </span>
            </div>
          )}

          <button
            className="hud-btn-toggle"
            onClick={handleFitView}
            title="Fit to screen"
          >
            ⛶ Fit
          </button>
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
        ) : (
          <div className="bounded-context-canvas" ref={containerRef} />
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

        {/* Slide-Out Detail Drawer for Directional Highway Edge */}
        {selectedEdge && (
          <div className="context-detail-drawer">
            <div className="drawer-header">
              <div className="drawer-title-group">
                <span className="drawer-domain-icon">🛣️</span>
                <div>
                  <h3 className="drawer-domain-name">Cross-Domain Interaction</h3>
                  <div style={{ fontSize: '11px', color: '#94a3b8' }}>
                    {selectedEdge.sourceDomain} &nbsp;➔&nbsp; {selectedEdge.targetDomain}
                  </div>
                </div>
              </div>
              <button
                className="drawer-close-btn"
                onClick={() => setSelectedEdge(null)}
                title="Close"
              >
                ✕
              </button>
            </div>

            <div className="drawer-content">
              <div className="drawer-metrics-grid">
                <div className="drawer-metric-box">
                  <div className="metric-val" style={{ color: '#38bdf8' }}>{selectedEdge.totalCalls}</div>
                  <div className="metric-lbl">Total Invocations</div>
                </div>
                <div className="drawer-metric-box">
                  <div className="metric-val" style={{ textTransform: 'capitalize', color: selectedEdge.category === 'messaging' ? '#f59e0b' : '#34d399' }}>
                    {selectedEdge.category === 'messaging' ? 'Event / Queue' : selectedEdge.category === 'database' ? 'Shared DB' : 'Service Call'}
                  </div>
                  <div className="metric-lbl">Flow Type</div>
                </div>
              </div>

              <div>
                <div className="drawer-section-title">📊 Aggregated Call Breakdown</div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  {selectedEdge.details.map((d, idx) => (
                    <div
                      key={idx}
                      style={{
                        padding: '8px 12px',
                        background: 'rgba(255, 255, 255, 0.04)',
                        border: '1px solid rgba(255, 255, 255, 0.08)',
                        borderRadius: '6px',
                        fontSize: '12px',
                        fontFamily: 'monospace',
                        color: '#93c5fd',
                      }}
                    >
                      {d}
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
