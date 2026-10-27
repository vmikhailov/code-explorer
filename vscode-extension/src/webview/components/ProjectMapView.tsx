import React, { useEffect, useMemo, useRef, useState, useCallback } from 'react';
import cytoscape, { Core, EventObject } from 'cytoscape';
import { GraphData } from '../../../../proto/types';
import {
  filterProjectMap,
  parseProjectMapGraph,
  ProjectMapEdgeModel,
  ProjectMapModel,
  ProjectMapNodeModel,
  TIER_COLORS,
  FOUNDATION_TIER,
  RESOURCE_TIER,
} from '../projectMap/projectMapModel';
import { computeProjectMapLayout, ProjectMapRing } from '../layout/projectMapLayout';
import { computeSwimlanesLayout, SwimlaneGuide, PROJECT_MAP_SWIMLANE_TITLES } from '../layout/swimlanesLayout';
import { attachNormalizedCytoscapeWheel } from '../utils/wheelZoom';
import { useServerTransport } from '../services/webviewTransport';
import { MermaidDiagramView } from './MermaidDiagramView';

export interface ProjectMapViewProps {
  serverHttpUrl?: string;
  project?: string;
  onProjectChange?: (projectName: string) => void;
  onOpenFile: (filePath: string, lineStart?: number) => void;
}

type ProjectMapLayoutType = 'concentric' | 'swimlanes' | 'mermaid' | 'cose';

const LAYOUT_OPTIONS: Array<{ id: ProjectMapLayoutType; label: string }> = [
  { id: 'concentric', label: '🎯 Default' },
  { id: 'swimlanes', label: '🏊 Swimlanes' },
  { id: 'mermaid', label: '🧜 Mermaid' },
  { id: 'cose', label: '🌐 Force' },
];

function resolveBaseUrl(serverHttpUrl?: string): string | undefined {
  if (serverHttpUrl) return serverHttpUrl;
  const wsUrl = typeof window !== 'undefined' ? (window as any).__CE_CONFIG__?.wsUrl : undefined;
  return wsUrl?.replace(/^ws:\/\//, 'http://').replace(/^wss:\/\//, 'https://').replace(/\/ws$/, '');
}

export const ProjectMapView: React.FC<ProjectMapViewProps> = ({
  serverHttpUrl,
  project,
  onProjectChange,
  onOpenFile,
}) => {
  const { httpUrl: transportHttpUrl, connectionState } = useServerTransport();
  const [graph, setGraph] = useState<GraphData | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  // Filters & layout state
  const [layoutType, setLayoutType] = useState<ProjectMapLayoutType>('concentric');
  const [hideUtilities, setHideUtilities] = useState(false);
  const [hideResources, setHideResources] = useState(false);
  const [cyclesOnly, setCyclesOnly] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');

  // Selection state
  const [selectedNode, setSelectedNode] = useState<ProjectMapNodeModel | null>(null);
  const [selectedEdge, setSelectedEdge] = useState<ProjectMapEdgeModel | null>(null);

  // Cytoscape and SVG transform state
  const containerRef = useRef<HTMLDivElement>(null);
  const cyRef = useRef<Core | null>(null);
  const [cyTransform, setCyTransform] = useState<{ pan: { x: number; y: number }; zoom: number }>({
    pan: { x: 0, y: 0 },
    zoom: 1,
  });
  const [rings, setRings] = useState<ProjectMapRing[]>([]);
  const [swimlanes, setSwimlanes] = useState<SwimlaneGuide[]>([]);

  // Fetch project map data
  useEffect(() => {
    const baseUrl = serverHttpUrl || transportHttpUrl || resolveBaseUrl(serverHttpUrl);
    if (!baseUrl) {
      setLoading(true);
      setError(null);
      return;
    }
    let cancelled = false;
    let retryTimer: ReturnType<typeof setTimeout> | null = null;
    setLoading(true);
    setError(null);

    const loadWithRetry = (attempt: number) => {
      const currentBase = serverHttpUrl || transportHttpUrl || resolveBaseUrl(serverHttpUrl) || baseUrl;
      fetch(`${currentBase}/api/view?view=project-map&scope=${encodeURIComponent(project ?? '')}`)
        .then(async (res) => {
          if (!res.ok) throw new Error(`HTTP ${res.status}: ${await res.text()}`);
          return (await res.json()) as GraphData;
        })
        .then((data) => {
          if (cancelled) return;
          setGraph(data);
          setError(null);
          setLoading(false);
        })
        .catch((err: unknown) => {
          if (cancelled) return;
          if (attempt < 5) {
            retryTimer = setTimeout(() => loadWithRetry(attempt + 1), 500);
            return;
          }
          setError(err instanceof Error ? err.message : String(err));
          setLoading(false);
        });
    };

    loadWithRetry(0);
    return () => {
      cancelled = true;
      if (retryTimer) clearTimeout(retryTimer);
    };
  }, [serverHttpUrl, transportHttpUrl, connectionState === 'connected', project, reloadToken]);

  const model: ProjectMapModel = useMemo(() => parseProjectMapGraph(graph), [graph]);
  const visible = useMemo(
    () => filterProjectMap(model, { hideUtilities, hideResources, cyclesOnly }),
    [model, hideUtilities, hideResources, cyclesOnly]
  );

  // Filtered graph for Mermaid view
  const filteredGraph = useMemo<GraphData | null>(() => {
    if (!graph) return null;
    if (!hideUtilities && !hideResources && !cyclesOnly) return graph;
    const visibleNodeIds = new Set(visible.nodes.map((n) => n.id));
    const visibleEdgeIds = new Set(visible.edges.map((e) => e.id));
    return {
      ...graph,
      nodes: (graph.nodes ?? []).filter((n) => visibleNodeIds.has(n.id)),
      edges: (graph.edges ?? []).filter((e) => {
        const edgeId = e.id ?? `${e.source}->${e.target}`;
        return visibleEdgeIds.has(edgeId);
      }),
    };
  }, [graph, visible, hideUtilities, hideResources, cyclesOnly]);

  // Initialize Cytoscape core instance
  useEffect(() => {
    if (!containerRef.current) return;

    const cy = cytoscape({
      container: containerRef.current,
      boxSelectionEnabled: false,
      autounselectify: false,
      wheelSensitivity: 0.2,
      style: [
        {
          selector: 'node',
          style: {
            'shape': 'ellipse',
            'width': 'data(size)',
            'height': 'data(size)',
            'background-color': 'data(bgColor)',
            'border-width': 'data(borderWidth)',
            'border-color': 'data(borderColor)',
            'border-opacity': 0.95,
            'label': 'data(label)',
            'color': '#f8fafc',
            'font-size': '11px',
            'font-weight': 600,
            'font-family': 'system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
            'text-valign': 'bottom',
            'text-halign': 'center',
            'text-margin-y': 5,
            'text-wrap': 'ellipsis',
            'text-max-width': '110px',
            'text-background-color': 'rgba(15, 23, 42, 0.75)',
            'text-background-opacity': 0.9,
            'text-background-padding': '2px',
            'text-background-shape': 'roundrectangle',
          },
        },
        {
          selector: 'node[shape = "round-rectangle"]',
          style: {
            'shape': 'round-rectangle',
          },
        },
        {
          selector: 'node[shape = "diamond"]',
          style: {
            'shape': 'diamond',
          },
        },
        {
          selector: 'node:selected',
          style: {
            'border-color': '#38bdf8',
            'border-width': 4,
          },
        },
        {
          selector: 'edge',
          style: {
            'width': 'data(width)',
            'line-color': 'data(color)',
            'target-arrow-color': 'data(color)',
            'target-arrow-shape': 'triangle',
            'arrow-scale': 0.85,
            'curve-style': 'bezier',
            'opacity': 0.7,
          },
        },
        {
          selector: 'edge:selected',
          style: {
            'line-color': '#38bdf8',
            'target-arrow-color': '#38bdf8',
            'width': 3.5,
            'opacity': 1,
          },
        },
        {
          selector: '.is-dimmed',
          style: {
            'opacity': 0.15,
          },
        },
        {
          selector: '.is-highlighted',
          style: {
            'border-color': '#38bdf8',
            'border-width': 3,
            'opacity': 1,
          },
        },
      ],
    });

    const updateTransform = () => {
      setCyTransform({ pan: { ...cy.pan() }, zoom: cy.zoom() });
    };

    cy.on('pan zoom', updateTransform);

    // Node selection
    cy.on('tap', 'node', (evt: EventObject) => {
      const nodeData = evt.target.data('nodeModel') as ProjectMapNodeModel;
      setSelectedNode(nodeData || null);
      setSelectedEdge(null);
    });

    // Double-click node to open file
    cy.on('dblclick', 'node', (evt: EventObject) => {
      const nodeData = evt.target.data('nodeModel') as ProjectMapNodeModel;
      if (nodeData?.filePath || nodeData?.path) {
        onOpenFile(nodeData.filePath || nodeData.path);
      }
    });

    // Edge selection
    cy.on('tap', 'edge', (evt: EventObject) => {
      const edgeData = evt.target.data('edgeModel') as ProjectMapEdgeModel;
      setSelectedEdge(edgeData || null);
      setSelectedNode(null);
    });

    // Canvas click to deselect
    cy.on('tap', (evt: EventObject) => {
      if (evt.target === cy) {
        setSelectedNode(null);
        setSelectedEdge(null);
      }
    });

    const cleanupWheel = attachNormalizedCytoscapeWheel(
      containerRef.current,
      () => cyRef.current,
      () => 0.2
    );

    let resizeObserver: ResizeObserver | null = null;
    if (typeof ResizeObserver !== 'undefined' && containerRef.current) {
      resizeObserver = new ResizeObserver(() => {
        if (cyRef.current) {
          cyRef.current.resize();
        }
      });
      resizeObserver.observe(containerRef.current);
    }

    cyRef.current = cy;

    return () => {
      if (resizeObserver) {
        resizeObserver.disconnect();
      }
      cleanupWheel();
      cy.destroy();
      cyRef.current = null;
    };
  }, [onOpenFile]);

  // Layout runner and elements updater
  useEffect(() => {
    const cy = cyRef.current;
    if (!cy || layoutType === 'mermaid') return;

    let cancelled = false;

    const runLayout = async () => {
      // Ensure container has valid DOM dimensions and Cytoscape is resized (especially returning from Mermaid)
      if (containerRef.current) {
        if (containerRef.current.clientWidth === 0 || containerRef.current.clientHeight === 0) {
          await new Promise<void>((resolve) => {
            let attempts = 0;
            const check = () => {
              if (cancelled || attempts++ > 30) return resolve();
              if (containerRef.current && containerRef.current.clientWidth > 0 && containerRef.current.clientHeight > 0) {
                resolve();
              } else {
                requestAnimationFrame(check);
              }
            };
            requestAnimationFrame(check);
          });
          if (cancelled) return;
        }
        cy.resize();
      }

      // Build elements
      const elements: cytoscape.ElementDefinition[] = [];

      for (const n of visible.nodes) {
        const isEntryPoint = n.pmType === 'entrypoint' || n.kind === 'EntryPoint' || n.kind === 'Endpoint';
        const shape = isEntryPoint ? 'diamond' : n.isResource ? 'round-rectangle' : 'ellipse';
        const size = Math.max(38, Math.min(68, 38 + Math.round(Math.sqrt(Math.max(1, n.fileCount || n.dataFileCount || 1)) * 5)));
        const bgColor = TIER_COLORS[n.tier] || '#64748b';
        const borderColor = n.cycleGroup ? '#f59e0b' : '#334155';
        const borderWidth = n.cycleGroup ? 3 : 1.5;

        elements.push({
          group: 'nodes',
          data: {
            id: n.id,
            label: n.name,
            size,
            shape,
            bgColor,
            borderColor,
            borderWidth,
            nodeModel: n,
          },
        });
      }

      for (const e of visible.edges) {
        const edgeWidth = Math.min(5, Math.max(1.5, Math.log2(e.count + 1)));
        const edgeColor = e.isCircular
          ? '#f59e0b'
          : e.isResourceLink
          ? '#a855f7'
          : e.kind === 'INSTANTIATES'
          ? '#22c55e'
          : '#38bdf8';

        elements.push({
          group: 'edges',
          data: {
            id: e.id,
            source: e.source,
            target: e.target,
            width: edgeWidth,
            color: edgeColor,
            edgeModel: e,
          },
        });
      }

      cy.batch(() => {
        cy.elements().remove();
        cy.add(elements);
      });

      if (layoutType === 'concentric') {
        const layoutNodes = visible.nodes.map((n) => ({
          id: n.id,
          tier: n.tier,
          radius: 28,
        }));
        const layoutEdges = visible.edges.map((e) => ({
          source: e.source,
          target: e.target,
        }));

        const result = computeProjectMapLayout(layoutNodes, layoutEdges);
        setRings(result.rings);
        setSwimlanes([]);

        const layout = cy.layout({
          name: 'preset',
          positions: (node: any) => {
            const p = result.positions[node.id()];
            return p ? { x: p.x, y: p.y } : { x: 0, y: 0 };
          },
          fit: true,
          padding: 60,
          animate: false,
          stop: () => {
            if (!cancelled) {
              cy.resize();
              const z = cy.zoom();
              if (!Number.isFinite(z) || z <= 0) {
                cy.fit(undefined, 60);
              }
              setCyTransform({ pan: { ...cy.pan() }, zoom: cy.zoom() });
            }
          },
        });
        layout.run();
      } else if (layoutType === 'swimlanes') {
        setRings([]);
        const swimNodes = visible.nodes.map((n) => ({
          id: n.id,
          echelonTier: n.tier,
          kind: n.kind,
          name: n.name,
        }));
        const swimEdges = visible.edges.map((e) => ({
          source: e.source,
          target: e.target,
          count: e.count,
        }));

        const result = computeSwimlanesLayout(swimNodes, swimEdges, 1.2, true);
        setSwimlanes(result.lanes);

        const layout = cy.layout({
          name: 'preset',
          positions: (node: any) => {
            const p = result.positions.get(node.id());
            return p ? { x: p.x, y: p.y } : { x: 0, y: 0 };
          },
          fit: true,
          padding: 60,
          animate: false,
          stop: () => {
            if (!cancelled) {
              cy.resize();
              const z = cy.zoom();
              if (!Number.isFinite(z) || z <= 0) {
                cy.fit(undefined, 60);
              }
              setCyTransform({ pan: { ...cy.pan() }, zoom: cy.zoom() });
            }
          },
        });
        layout.run();
      } else if (layoutType === 'cose') {
        setRings([]);
        setSwimlanes([]);
        const layout = cy.layout({
          name: 'cose',
          animate: false,
          componentSpacing: 80,
          nodeOverlap: 25,
          gravity: 40,
          numIter: 300,
          fit: true,
          padding: 60,
          stop: () => {
            if (!cancelled) {
              cy.resize();
              setCyTransform({ pan: { ...cy.pan() }, zoom: cy.zoom() });
            }
          },
        });
        layout.run();
      }
    };

    runLayout();

    return () => {
      cancelled = true;
    };
  }, [visible, layoutType]);

  // Search filter highlighting
  useEffect(() => {
    const cy = cyRef.current;
    if (!cy) return;
    const query = searchQuery.trim().toLowerCase();

    cy.batch(() => {
      if (!query) {
        cy.elements().removeClass('is-dimmed is-highlighted');
        return;
      }

      cy.nodes().forEach((n) => {
        const label = (n.data('label') || '').toLowerCase();
        const id = n.id().toLowerCase();
        const matches = label.includes(query) || id.includes(query);
        if (matches) {
          n.removeClass('is-dimmed').addClass('is-highlighted');
        } else {
          n.removeClass('is-highlighted').addClass('is-dimmed');
        }
      });

      cy.edges().forEach((e) => {
        const src = e.source();
        const tgt = e.target();
        if (src.hasClass('is-highlighted') || tgt.hasClass('is-highlighted')) {
          e.removeClass('is-dimmed');
        } else {
          e.addClass('is-dimmed');
        }
      });
    });
  }, [searchQuery]);

  const handleFit = useCallback(() => {
    const cy = cyRef.current;
    if (!cy) return;
    cy.resize();
    cy.fit(undefined, 60);
    setCyTransform({ pan: { ...cy.pan() }, zoom: cy.zoom() });
  }, []);

  const currentProjectFiles = model.projects.find((p) => p.name === (model.projectName || project))?.files;

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        width: '100%',
        height: '100%',
        position: 'relative',
        background: '#0b0f19',
        color: '#f8fafc',
        overflow: 'hidden',
      }}
    >
      {/* Top Header Controls Bar */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '6px 12px',
          background: 'rgba(15, 23, 42, 0.95)',
          borderBottom: '1px solid rgba(51, 65, 85, 0.5)',
          zIndex: 10,
          gap: 12,
          rowGap: 8,
          flexWrap: 'wrap',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', minWidth: 0 }}>
          <span style={{ fontSize: '12px', fontWeight: 600, color: '#38bdf8', whiteSpace: 'nowrap', flexShrink: 0 }}>
            📦 Project Map
          </span>

          <select
            className="domain-layout-dropdown"
            style={{
              minWidth: 150,
              maxWidth: 240,
              height: 26,
              boxSizing: 'border-box',
              fontSize: 11,
              background: '#1e293b',
              color: '#f8fafc',
              border: '1px solid #334155',
              borderRadius: 4,
              padding: '0 6px',
              textOverflow: 'ellipsis',
              overflow: 'hidden',
              flexShrink: 0,
            }}
            value={model.projectName || project || ''}
            onChange={(e) => onProjectChange?.(e.target.value)}
            title="Select project to inspect"
          >
            {model.projects.length === 0 && (
              <option value={project ?? ''}>{project || '(current workspace)'}</option>
            )}
            {model.projects.map((p) => (
              <option key={p.id} value={p.name} title={p.path}>
                {p.name} ({p.files} files)
              </option>
            ))}
          </select>

          <select
            className="domain-layout-dropdown"
            style={{
              height: 26,
              boxSizing: 'border-box',
              fontSize: 11,
              background: '#1e293b',
              color: '#f8fafc',
              border: '1px solid #334155',
              borderRadius: 4,
              padding: '0 6px',
              flexShrink: 0,
            }}
            value={layoutType}
            onChange={(e) => setLayoutType(e.target.value as ProjectMapLayoutType)}
            title="Switch graph layout algorithm"
          >
            {LAYOUT_OPTIONS.map((opt) => (
              <option key={opt.id} value={opt.id}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        {/* Filters and Search */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap', minWidth: 0 }}>
          {model.cycles.length > 0 && (
            <button
              type="button"
              onClick={() => setCyclesOnly((v) => !v)}
              style={{
                height: 26,
                boxSizing: 'border-box',
                padding: '0 8px',
                fontSize: 11,
                borderRadius: 4,
                border: '1px solid #f59e0b',
                background: cyclesOnly ? '#f59e0b' : 'transparent',
                color: cyclesOnly ? '#0f172a' : '#f59e0b',
                cursor: 'pointer',
                fontWeight: 600,
                whiteSpace: 'nowrap',
                display: 'inline-flex',
                alignItems: 'center',
                flexShrink: 0,
              }}
              title="Isolate only modules in dependency cycles"
            >
              ⚠️ Cycles ({model.cycles.length})
            </button>
          )}

          <button
            type="button"
            onClick={() => setHideUtilities((v) => !v)}
            style={{
              height: 26,
              boxSizing: 'border-box',
              padding: '0 8px',
              fontSize: 11,
              borderRadius: 4,
              border: '1px solid #475569',
              background: hideUtilities ? '#334155' : 'transparent',
              color: hideUtilities ? '#94a3b8' : '#e2e8f0',
              cursor: 'pointer',
              whiteSpace: 'nowrap',
              display: 'inline-flex',
              alignItems: 'center',
              flexShrink: 0,
            }}
            title="Hide utility and foundation classes (Tier 4+)"
          >
            🧰 {hideUtilities ? 'Show Utilities' : 'Hide Utilities'}
          </button>

          <button
            type="button"
            onClick={() => setHideResources((v) => !v)}
            style={{
              height: 26,
              boxSizing: 'border-box',
              padding: '0 8px',
              fontSize: 11,
              borderRadius: 4,
              border: '1px solid #475569',
              background: hideResources ? '#334155' : 'transparent',
              color: hideResources ? '#94a3b8' : '#e2e8f0',
              cursor: 'pointer',
              whiteSpace: 'nowrap',
              display: 'inline-flex',
              alignItems: 'center',
              flexShrink: 0,
            }}
            title="Hide databases, tables, and external resources"
          >
            🗄️ {hideResources ? 'Show Resources' : 'Hide Resources'}
          </button>

          <input
            type="text"
            placeholder="Search modules…"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            style={{
              height: 26,
              boxSizing: 'border-box',
              width: 130,
              fontSize: 11,
              background: '#0f172a',
              border: '1px solid #334155',
              borderRadius: 4,
              padding: '0 6px',
              color: '#f8fafc',
              flexShrink: 0,
            }}
          />

          <button
            type="button"
            onClick={handleFit}
            style={{
              height: 26,
              boxSizing: 'border-box',
              padding: '0 8px',
              fontSize: 11,
              borderRadius: 4,
              border: '1px solid #334155',
              background: '#1e293b',
              color: '#f8fafc',
              cursor: 'pointer',
              whiteSpace: 'nowrap',
              display: 'inline-flex',
              alignItems: 'center',
              flexShrink: 0,
            }}
            title="Fit view to graph"
          >
            🔍 Fit
          </button>

          <button
            type="button"
            onClick={() => setReloadToken((t) => t + 1)}
            style={{
              height: 26,
              boxSizing: 'border-box',
              padding: '0 8px',
              fontSize: 11,
              borderRadius: 4,
              border: '1px solid #334155',
              background: '#1e293b',
              color: '#f8fafc',
              cursor: 'pointer',
              whiteSpace: 'nowrap',
              display: 'inline-flex',
              alignItems: 'center',
              flexShrink: 0,
            }}
            title="Refresh graph"
          >
            🔄
          </button>
        </div>
      </div>

      {/* Loading Overlay */}
      {loading && (
        <div className="view-loading-overlay" style={{ zIndex: 1000 }}>
          <div className="view-loading-card">
            <div className="view-loading-spinner" />
            <div className="view-loading-content">
              <span className="view-loading-title">Project Map</span>
              <span className="view-loading-subtitle">Loading project modules and resources…</span>
            </div>
          </div>
        </div>
      )}

      {/* Error Overlay */}
      {!loading && (error || model.error) && (
        <div className="pm-overlay pm-error" style={{ zIndex: 900 }}>
          {error || model.error}
        </div>
      )}

      {/* Main Graph Content */}
      <div style={{ flex: 1, position: 'relative', width: '100%', height: '100%', overflow: 'hidden' }}>
        {layoutType === 'mermaid' ? (
          <MermaidDiagramView
            graph={filteredGraph}
            serverHttpUrl={serverHttpUrl}
            onOpenFile={onOpenFile}
          />
        ) : (
          <>
            {/* SVG Guides Overlay */}
            <svg
              style={{
                position: 'absolute',
                inset: 0,
                width: '100%',
                height: '100%',
                pointerEvents: 'none',
                zIndex: 0,
              }}
            >
              <g transform={`translate(${cyTransform.pan.x}, ${cyTransform.pan.y}) scale(${cyTransform.zoom})`}>
                {/* Concentric Tier Rings */}
                {layoutType === 'concentric' &&
                  rings.map((ring) => {
                    const tierLabel =
                      model.tiers.find((t) => t.tier === ring.tier)?.label || `Tier ${ring.tier}`;
                    return (
                      <g key={`ring-${ring.tier}`}>
                        <circle
                          cx={0}
                          cy={0}
                          r={ring.radius}
                          fill="none"
                          stroke={TIER_COLORS[ring.tier] || '#475569'}
                          strokeWidth={1.5 / cyTransform.zoom}
                          strokeDasharray={`${6 / cyTransform.zoom} ${4 / cyTransform.zoom}`}
                          opacity={0.35}
                        />
                        <text
                          x={0}
                          y={-ring.radius - 8 / cyTransform.zoom}
                          fill={TIER_COLORS[ring.tier] || '#94a3b8'}
                          fontSize={11 / cyTransform.zoom}
                          fontWeight={600}
                          textAnchor="middle"
                          opacity={0.75}
                        >
                          {tierLabel} ({ring.nodeIds.length})
                        </text>
                      </g>
                    );
                  })}

                {/* Swimlane Column Cards */}
                {layoutType === 'swimlanes' &&
                  swimlanes.map((lane) => (
                    <g key={lane.id}>
                      <rect
                        x={lane.x}
                        y={lane.y}
                        width={lane.width}
                        height={lane.height}
                        rx={8 / cyTransform.zoom}
                        fill="rgba(30, 41, 59, 0.25)"
                        stroke={lane.color || '#334155'}
                        strokeWidth={1.5 / cyTransform.zoom}
                        opacity={0.6}
                      />
                      <text
                        x={lane.x + lane.width / 2}
                        y={lane.y + 20 / cyTransform.zoom}
                        fill={lane.color || '#38bdf8'}
                        fontSize={12 / cyTransform.zoom}
                        fontWeight={700}
                        textAnchor="middle"
                      >
                        {lane.icon} {lane.title} ({lane.count})
                      </text>
                    </g>
                  ))}
              </g>
            </svg>

            {/* Cytoscape Container */}
            <div
              ref={containerRef}
              style={{
                position: 'absolute',
                inset: 0,
                width: '100%',
                height: '100%',
                zIndex: 1,
              }}
            />
          </>
        )}

        {/* Right-docked Inspector Drawer for Node / Edge */}
        {(selectedNode || selectedEdge) && (
          <div
            style={{
              position: 'absolute',
              top: 12,
              right: 12,
              bottom: 12,
              width: 320,
              background: 'rgba(15, 23, 42, 0.94)',
              border: '1px solid rgba(56, 189, 248, 0.3)',
              borderRadius: 8,
              boxShadow: '0 8px 32px rgba(0, 0, 0, 0.6)',
              backdropFilter: 'blur(10px)',
              zIndex: 20,
              display: 'flex',
              flexDirection: 'column',
              overflow: 'hidden',
              fontSize: '12px',
            }}
          >
            {/* Header */}
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                padding: '10px 14px',
                borderBottom: '1px solid rgba(51, 65, 85, 0.5)',
                background: 'rgba(30, 41, 59, 0.6)',
              }}
            >
              <span style={{ fontWeight: 700, color: '#38bdf8', fontSize: '13px' }}>
                {selectedNode ? `Node: ${selectedNode.name}` : `Edge: ${selectedEdge?.kind}`}
              </span>
              <button
                type="button"
                onClick={() => {
                  setSelectedNode(null);
                  setSelectedEdge(null);
                }}
                style={{
                  background: 'none',
                  border: 'none',
                  color: '#94a3b8',
                  fontSize: '16px',
                  cursor: 'pointer',
                  padding: '0 4px',
                }}
              >
                ✕
              </button>
            </div>

            {/* Drawer Body */}
            <div style={{ flex: 1, overflowY: 'auto', padding: '12px 14px' }}>
              {selectedNode && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                  <div>
                    <div style={{ color: '#94a3b8', fontSize: '10px', textTransform: 'uppercase' }}>Kind</div>
                    <div style={{ fontWeight: 600, color: '#f8fafc' }}>{selectedNode.kind}</div>
                  </div>

                  <div>
                    <div style={{ color: '#94a3b8', fontSize: '10px', textTransform: 'uppercase' }}>Tier</div>
                    <div style={{ color: TIER_COLORS[selectedNode.tier] || '#38bdf8', fontWeight: 600 }}>
                      {selectedNode.tierLabel || `Tier ${selectedNode.tier}`}
                    </div>
                    {selectedNode.tierReason && (
                      <div style={{ color: '#94a3b8', fontSize: '11px', marginTop: 2 }}>
                        {selectedNode.tierReason}
                      </div>
                    )}
                  </div>

                  <div style={{ display: 'flex', gap: 16 }}>
                    <div>
                      <div style={{ color: '#94a3b8', fontSize: '10px', textTransform: 'uppercase' }}>Fan-In</div>
                      <div style={{ fontWeight: 600 }}>{selectedNode.fanIn}</div>
                    </div>
                    <div>
                      <div style={{ color: '#94a3b8', fontSize: '10px', textTransform: 'uppercase' }}>Fan-Out</div>
                      <div style={{ fontWeight: 600 }}>{selectedNode.fanOut}</div>
                    </div>
                    <div>
                      <div style={{ color: '#94a3b8', fontSize: '10px', textTransform: 'uppercase' }}>Instability</div>
                      <div style={{ fontWeight: 600 }}>{selectedNode.instability.toFixed(2)}</div>
                    </div>
                  </div>

                  {selectedNode.path && (
                    <div>
                      <div style={{ color: '#94a3b8', fontSize: '10px', textTransform: 'uppercase' }}>Path</div>
                      <div
                        style={{
                          fontSize: '11px',
                          color: '#38bdf8',
                          wordBreak: 'break-all',
                          cursor: 'pointer',
                          textDecoration: 'underline',
                          marginTop: 2,
                        }}
                        onClick={() => onOpenFile(selectedNode.filePath || selectedNode.path)}
                        title="Click to open file"
                      >
                        {selectedNode.path}
                      </div>
                    </div>
                  )}

                  {selectedNode.files && selectedNode.files.length > 0 && (
                    <div>
                      <div style={{ color: '#94a3b8', fontSize: '10px', textTransform: 'uppercase' }}>
                        Source Files ({selectedNode.files.length})
                      </div>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 4, marginTop: 4 }}>
                        {selectedNode.files.map((file) => (
                          <div
                            key={file}
                            onClick={() => onOpenFile(file)}
                            style={{
                              fontSize: '11px',
                              color: '#93c5fd',
                              cursor: 'pointer',
                              textDecoration: 'underline',
                              wordBreak: 'break-all',
                            }}
                            title="Open in editor"
                          >
                            {file}
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              )}

              {selectedEdge && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                  <div>
                    <div style={{ color: '#94a3b8', fontSize: '10px', textTransform: 'uppercase' }}>Relationship</div>
                    <div style={{ fontWeight: 600, color: '#f8fafc' }}>
                      {selectedEdge.source} ➔ {selectedEdge.target}
                    </div>
                  </div>

                  <div style={{ display: 'flex', gap: 16 }}>
                    <div>
                      <div style={{ color: '#94a3b8', fontSize: '10px', textTransform: 'uppercase' }}>Calls</div>
                      <div style={{ fontWeight: 600 }}>{selectedEdge.count}</div>
                    </div>
                    <div>
                      <div style={{ color: '#94a3b8', fontSize: '10px', textTransform: 'uppercase' }}>Files</div>
                      <div style={{ fontWeight: 600 }}>{selectedEdge.distinctFiles || 1}</div>
                    </div>
                    {selectedEdge.isCircular && (
                      <div style={{ color: '#f59e0b', fontWeight: 600 }}>⚠️ Cycle</div>
                    )}
                  </div>

                  {selectedEdge.kinds && Object.keys(selectedEdge.kinds).length > 0 && (
                    <div>
                      <div style={{ color: '#94a3b8', fontSize: '10px', textTransform: 'uppercase' }}>Kinds</div>
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4, marginTop: 4 }}>
                        {Object.entries(selectedEdge.kinds).map(([k, cnt]) => (
                          <span
                            key={k}
                            style={{
                              background: '#334155',
                              padding: '2px 6px',
                              borderRadius: 4,
                              fontSize: '10px',
                            }}
                          >
                            {k} ({cnt})
                          </span>
                        ))}
                      </div>
                    </div>
                  )}

                  {selectedEdge.samples && selectedEdge.samples.length > 0 && (
                    <div>
                      <div style={{ color: '#94a3b8', fontSize: '10px', textTransform: 'uppercase' }}>
                        Sample Occurrences ({selectedEdge.samples.length})
                      </div>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 4, marginTop: 4 }}>
                        {selectedEdge.samples.map((s, idx) => (
                          <div
                            key={idx}
                            onClick={() => onOpenFile(s.file, s.line ?? undefined)}
                            style={{
                              fontSize: '11px',
                              color: '#93c5fd',
                              cursor: 'pointer',
                              textDecoration: 'underline',
                              wordBreak: 'break-all',
                            }}
                            title="Jump to code line"
                          >
                            {s.file}{s.line ? `:${s.line}` : ''} ({s.kind})
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
