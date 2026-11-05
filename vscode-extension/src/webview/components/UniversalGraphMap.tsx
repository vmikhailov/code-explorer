import React, { useEffect, useRef, useState, useMemo, useCallback } from 'react';
import cytoscape, { Core, EventObject } from 'cytoscape';
import dagre from 'cytoscape-dagre';
import { GraphData, GraphNode, GraphEdge } from '../../../../proto/types';
import { attachNormalizedCytoscapeWheel } from '../utils/wheelZoom';
import { computeSwimlanesLayout, SwimlaneGuide, TierDefinition } from '../layout/swimlanesLayout';
import { MermaidDiagramView } from './MermaidDiagramView';
import { getVsCodeApi } from '../vscodeApi';
import { SelectedNodeDetail } from '../domain/types';

try {
  cytoscape.use(dagre);
} catch {
  // Already registered
}

export interface InspectorPresetDefinition {
  id: string;
  label: string;
  description?: string;
  icon?: string;
  isActive: boolean;
  onToggle: () => void;
  badge?: string | number;
}

export interface InspectorOverviewMetric {
  label: string;
  value: string | number;
  highlight?: boolean;
}

export interface InspectorOverviewModel {
  title: string;
  subtitle?: string;
  metrics: InspectorOverviewMetric[];
  actionBanner?: {
    text: string;
    actionLabel: string;
    isActive: boolean;
    onAction: () => void;
  };
}

export interface UniversalGraphMapProps {
  graph: GraphData | null;
  serverHttpUrl?: string;
  loading?: boolean;
  error?: string | null;

  hudLabel: string;
  hudTitle: string;
  headerControlsSlot?: React.ReactNode;

  tierDefinitions?: TierDefinition[];
  allowedLayouts?: Array<{ id: string; label: string }>;
  defaultLayoutName?: string;

  storagePrefix: string;

  inspectorOverviewSlot?: InspectorOverviewModel;
  visibilityPresetsSlot?: InspectorPresetDefinition[];

  onOpenFile?: (filePath: string, lineStart?: number) => void;
  onSelectNode?: (node: GraphNode | null) => void;
  onRefresh?: () => void;
}

const DEFAULT_ALLOWED_LAYOUTS = [
  { id: 'concentric', label: '🎯 Concentric' },
  { id: 'swimlanes', label: '🏊 Swimlanes' },
  { id: 'mermaid', label: '🧜 Mermaid' },
  { id: 'cose', label: '🌐 Force' },
];

export const UniversalGraphMap: React.FC<UniversalGraphMapProps> = ({
  graph,
  serverHttpUrl,
  loading = false,
  error = null,
  hudLabel,
  hudTitle,
  headerControlsSlot,
  tierDefinitions,
  allowedLayouts = DEFAULT_ALLOWED_LAYOUTS,
  defaultLayoutName = 'concentric',
  storagePrefix,
  inspectorOverviewSlot,
  visibilityPresetsSlot,
  onOpenFile,
  onSelectNode,
  onRefresh,
}) => {
  const [layoutType, setLayoutType] = useState<string>(() => {
    if (typeof localStorage !== 'undefined') {
      return localStorage.getItem(`${storagePrefix}layout`) ?? defaultLayoutName;
    }
    return defaultLayoutName;
  });

  const [searchQuery, setSearchQuery] = useState('');
  const [selectedNode, setSelectedNode] = useState<GraphNode | null>(null);

  const containerRef = useRef<HTMLDivElement>(null);
  const cyRef = useRef<Core | null>(null);

  const [cyTransform, setCyTransform] = useState<{ pan: { x: number; y: number }; zoom: number }>({
    pan: { x: 0, y: 0 },
    zoom: 1,
  });

  const [swimlanes, setSwimlanes] = useState<SwimlaneGuide[]>([]);
  const [concentricRings, setConcentricRings] = useState<Array<{ tier: number; radius: number; label: string; count: number }>>([]);

  const handleLayoutChange = useCallback(
    (newLayout: string) => {
      setLayoutType(newLayout);
      if (typeof localStorage !== 'undefined') {
        localStorage.setItem(`${storagePrefix}layout`, newLayout);
      }
    },
    [storagePrefix]
  );

  const [baseIconSize, setBaseIconSize] = useState<number>(() => {
    if (typeof localStorage !== 'undefined') {
      const val = Number(localStorage.getItem(`${storagePrefix}baseIconSize`));
      if (Number.isFinite(val) && val >= 24 && val <= 96) return val;
    }
    return 42;
  });

  const handleBaseIconSizeChange = useCallback(
    (size: number) => {
      setBaseIconSize(size);
      if (typeof localStorage !== 'undefined') {
        localStorage.setItem(`${storagePrefix}baseIconSize`, String(size));
      }
    },
    [storagePrefix]
  );

  // InspectorSidebarView state synchronization
  useEffect(() => {
    const vscode = getVsCodeApi();
    if (!vscode) return;

    const nodeDetailEntries: Array<[string, SelectedNodeDetail]> = (graph?.nodes ?? []).map((n) => [
      n.id,
      {
        id: n.id,
        name: n.name || n.id,
        displayName: n.displayName || n.name || n.id,
        kind: (n.kind as any) || 'Type',
        displayTag: n.properties?.sub_kind || n.kind || 'Type',
        bgColor: n.properties?.color || n.properties?.bgColor || '#3b82f6',
        borderColor: n.properties?.borderColor || '#1e293b',
        primaryFilePath: n.filePath || n.properties?.path,
        properties: n.properties || {},
        tier: Number(n.properties?.tier ?? 2),
        tierLabel: n.properties?.tier_label,
        tierReason: n.properties?.tier_reason,
        inboundCallsCount: Number(n.properties?.fan_in || 0),
        outboundCallsCount: Number(n.properties?.fan_out || 0),
        dbCount: Number(n.properties?.data_file_count || 0),
        messagingCount: 0,
        fileCount: Number(n.properties?.file_count || 1),
        projects: [],
      },
    ]);

    const selectedNodeDetail: SelectedNodeDetail | null = selectedNode
      ? {
          id: selectedNode.id,
          name: selectedNode.name || selectedNode.id,
          displayName: selectedNode.displayName || selectedNode.name || selectedNode.id,
          kind: (selectedNode.kind as any) || 'Type',
          displayTag: selectedNode.properties?.sub_kind || selectedNode.kind || 'Type',
          bgColor: selectedNode.properties?.color || selectedNode.properties?.bgColor || '#3b82f6',
          borderColor: selectedNode.properties?.borderColor || '#1e293b',
          primaryFilePath: selectedNode.filePath || selectedNode.properties?.path,
          properties: selectedNode.properties || {},
          tier: Number(selectedNode.properties?.tier ?? 2),
          tierLabel: selectedNode.properties?.tier_label,
          tierReason: selectedNode.properties?.tier_reason,
          inboundCallsCount: Number(selectedNode.properties?.fan_in || 0),
          outboundCallsCount: Number(selectedNode.properties?.fan_out || 0),
          dbCount: Number(selectedNode.properties?.data_file_count || 0),
          messagingCount: 0,
          fileCount: Number(selectedNode.properties?.file_count || 1),
          projects: [],
        }
      : null;

    const orbitLegendItems = (tierDefinitions ?? []).map((td) => ({
      tier: td.tier,
      title: td.title,
      count: (graph?.nodes ?? []).filter((n) => Number(n.properties?.tier) === td.tier).length,
      color: td.color || '#475569',
    }));

    const projectMapPresets = inspectorOverviewSlot
      ? {
          projectName: inspectorOverviewSlot.title,
          filesCount: Number(inspectorOverviewSlot.subtitle?.replace(/\D/g, '') || 0),
          entryPointCount: Number(inspectorOverviewSlot.metrics.find((m) => m.label === 'EntryPoints')?.value || 0),
          moduleCount: Number(inspectorOverviewSlot.metrics.find((m) => m.label === 'Classes')?.value || 0),
          depCount: Number(inspectorOverviewSlot.metrics.find((m) => m.label === 'Dependencies')?.value || 0),
          resourceCount: Number(inspectorOverviewSlot.metrics.find((m) => m.label === 'Resources')?.value || 0),
          cyclesCount: Number(visibilityPresetsSlot?.find((p) => p.id === 'cyclesOnly')?.badge || 0),
          cyclesOnly: Boolean(visibilityPresetsSlot?.find((p) => p.id === 'cyclesOnly')?.isActive),
          hideUtilities: Boolean(visibilityPresetsSlot?.find((p) => p.id === 'hideUtilities')?.isActive),
          hideResources: Boolean(visibilityPresetsSlot?.find((p) => p.id === 'hideResources')?.isActive),
        }
      : undefined;

    const syncedState = {
      hudLabel,
      hudTitle,
      isMatrixMode: false,
      effectiveSidebarTab: 'info',
      hasOrbitLegend: orbitLegendItems.length > 0,
      orbitLegendItems,
      hiddenCount: 0,
      selectedNode: selectedNodeDetail,
      selectedEdge: null,
      nodeDetailEntries,
      rawEdges: [],
      isInspectorCollapsed: false,
      isEdgeInspectorCollapsed: false,
      availableDomains: [],
      layoutName: layoutType,
      spacingFactor: 1.0,
      orbitDistance: 120,
      minOrbitNodeDistance: 40,
      autoRelayoutOnFilter: false,
      orbitConfig: {} as any,
      hiddenOrbitTiers: [],
      echelonEntries: [],
      hiddenNodeIds: [],
      hiddenTypes: [],
      hideSingleConnectionDbs: false,
      hideIsolatedNodes: false,
      showTransitiveEdges: false,
      counts: {
        totalNodes: graph?.nodes?.length ?? 0,
        totalEdges: graph?.edges?.length ?? 0,
      },
      groupedTypes: [],
      customGroups: [],
      nodeContentMode: 'icon',
      nodeSizingMode: 'fixed',
      edgeCurveMode: 'bezier',
      edgeLabelsOnHover: true,
      baseIconSize,
      fontSize: 11,
      curveFactor: 0.5,
      wheelSensitivity: 0.2,
      projectMapPresets,
    };

    vscode.postMessage({
      type: 'SYNC_INSPECTOR_STATE',
      state: syncedState,
    });
  }, [
    graph,
    selectedNode,
    layoutType,
    hudLabel,
    hudTitle,
    tierDefinitions,
    baseIconSize,
    inspectorOverviewSlot,
    visibilityPresetsSlot,
  ]);

  // Handle actions received from InspectorSidebarView
  useEffect(() => {
    const handleMessage = (event: MessageEvent) => {
      const msg = event.data;
      if (msg?.type === 'INSPECTOR_ACTION' && msg.action) {
        const action = msg.action;
        switch (action.type) {
          case 'SET_LAYOUT':
            if (action.layoutName) handleLayoutChange(action.layoutName);
            break;
          case 'SET_BASE_ICON_SIZE':
            if (action.size) handleBaseIconSizeChange(action.size);
            break;
          case 'TOGGLE_PROJECT_MAP_PRESET':
            if (action.preset) {
              const presetDef = visibilityPresetsSlot?.find((p) => p.id === action.preset);
              presetDef?.onToggle();
            }
            break;
          case 'SELECT_NODE':
            if (action.nodeId) {
              const cy = cyRef.current;
              const cyNode = cy?.getElementById(action.nodeId);
              if (cyNode) {
                cy?.elements().unselect();
                cyNode.select();
                const original = graph?.nodes?.find((n) => n.id === action.nodeId) ?? null;
                setSelectedNode(original);
                onSelectNode?.(original);
              }
            }
            break;
          case 'CLEAR_SELECTION':
            cyRef.current?.elements().unselect();
            setSelectedNode(null);
            onSelectNode?.(null);
            break;
          case 'OPEN_FILE':
            if (action.filePath) {
              onOpenFile?.(action.filePath, action.lineStart);
            }
            break;
        }
      }
    };
    window.addEventListener('message', handleMessage);
    return () => window.removeEventListener('message', handleMessage);
  }, [graph, visibilityPresetsSlot, handleLayoutChange, handleBaseIconSizeChange, onOpenFile, onSelectNode]);

  // Initialize Cytoscape
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
            'font-family': 'system-ui, -apple-system, sans-serif',
            'text-valign': 'bottom',
            'text-halign': 'center',
            'text-margin-y': 5,
            'text-wrap': 'ellipsis',
            'text-max-width': '120px',
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
      ],
    });

    cyRef.current = cy;

    const detachWheel = attachNormalizedCytoscapeWheel(
      containerRef.current,
      () => cyRef.current,
      () => 0.2
    );

    const updateTransform = () => {
      setCyTransform({ pan: cy.pan(), zoom: cy.zoom() });
    };

    cy.on('pan zoom', updateTransform);

    cy.on('select', 'node', (evt: EventObject) => {
      const node = evt.target;
      const originalNode = graph?.nodes?.find((n) => n.id === node.id()) ?? null;
      setSelectedNode(originalNode);
      onSelectNode?.(originalNode);
    });

    cy.on('unselect', 'node', () => {
      setSelectedNode(null);
      onSelectNode?.(null);
    });

    cy.on('dblclick', 'node', (evt: EventObject) => {
      const node = evt.target;
      const originalNode = graph?.nodes?.find((n) => n.id === node.id());
      const filePath = originalNode?.filePath || originalNode?.properties?.path;
      if (filePath) {
        onOpenFile?.(filePath);
        getVsCodeApi()?.postMessage({ type: 'OPEN_FILE', filePath });
      }
    });

    return () => {
      detachWheel();
      cy.destroy();
      cyRef.current = null;
    };
  }, []);

  // Update Cytoscape elements when graph or layout changes
  useEffect(() => {
    const cy = cyRef.current;
    if (!cy || !graph || layoutType === 'mermaid') return;

    cy.elements().remove();

    const rawNodes = graph.nodes ?? [];
    const rawEdges = graph.edges ?? [];

    if (rawNodes.length === 0) return;

    const cyNodes = rawNodes.map((n) => {
      const tier = Number(n.properties?.tier ?? n.properties?.echelon_tier ?? 2);
      return {
        group: 'nodes' as const,
        data: {
          id: n.id,
          label: n.name ?? n.id,
          echelonTier: tier,
          size: Math.round(baseIconSize * (Number(n.properties?.size ?? 42) / 42)),
          bgColor: n.properties?.color ?? n.properties?.bgColor ?? '#3b82f6',
          borderColor: n.properties?.borderColor ?? '#1e293b',
          borderWidth: Number(n.properties?.borderWidth ?? 2),
          shape: n.properties?.shape ?? 'ellipse',
        },
      };
    });

    const cyEdges = rawEdges.map((e) => {
      const edgeId = e.id ?? `${e.source}->${e.target}`;
      return {
        group: 'edges' as const,
        data: {
          id: edgeId,
          source: e.source,
          target: e.target,
          width: Number(e.properties?.width ?? 1.5),
          color: e.properties?.color ?? '#64748b',
        },
      };
    });

    cy.add([...cyNodes, ...cyEdges]);

    // Apply layout
    if (layoutType === 'swimlanes') {
      setConcentricRings([]);
      const swimNodes = rawNodes.map((n) => ({
        id: n.id,
        name: n.name ?? n.id,
        kind: n.kind ?? 'node',
        echelonTier: Number(n.properties?.tier ?? n.properties?.echelon_tier ?? 2),
      }));
      const swimEdges = rawEdges.map((e) => ({
        source: e.source,
        target: e.target,
      }));

      const res = computeSwimlanesLayout(swimNodes, swimEdges, 1.2, tierDefinitions);
      setSwimlanes(res.lanes);

      cy.batch(() => {
        for (const [id, pos] of res.positions) {
          cy.getElementById(id).position(pos);
        }
      });
      cy.fit(undefined, 40);
    } else if (layoutType === 'cose') {
      setSwimlanes([]);
      setConcentricRings([]);
      cy.layout({ name: 'cose', animate: false, padding: 40 }).run();
    } else {
      setSwimlanes([]);
      cy.layout({
        name: 'concentric',
        concentric: (node: any) => 10 - node.data('echelonTier'),
        levelWidth: () => 1,
        padding: 40,
        animate: false,
      }).run();

      const tierNodes = new Map<number, cytoscape.NodeSingular[]>();
      cy.nodes().forEach((n) => {
        const t = Number(n.data('echelonTier'));
        if (!tierNodes.has(t)) tierNodes.set(t, []);
        tierNodes.get(t)!.push(n);
      });
      const rings: Array<{ tier: number; radius: number; label: string; count: number }> = [];
      for (const [t, nodes] of tierNodes.entries()) {
        if (nodes.length === 0) continue;
        const first = nodes[0].position();
        const r = Math.round(Math.sqrt(first.x * first.x + first.y * first.y));
        const tierDef = tierDefinitions?.find((td) => td.tier === t);
        rings.push({
          tier: t,
          radius: r,
          label: tierDef?.title || `Tier ${t}`,
          count: nodes.length,
        });
      }
      setConcentricRings(rings);
    }
  }, [graph, layoutType, tierDefinitions]);

  // Search highlighting
  useEffect(() => {
    const cy = cyRef.current;
    if (!cy) return;

    if (!searchQuery.trim()) {
      cy.elements().removeClass('is-dimmed');
      return;
    }

    const q = searchQuery.toLowerCase();
    cy.batch(() => {
      cy.elements().addClass('is-dimmed');
      cy.nodes().forEach((n) => {
        const label = (n.data('label') ?? '').toLowerCase();
        if (label.includes(q)) {
          n.removeClass('is-dimmed');
          n.connectedEdges().removeClass('is-dimmed');
        }
      });
    });
  }, [searchQuery]);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', width: '100%', height: '100%', position: 'relative', overflow: 'hidden' }}>
      {/* Top HUD Control Bar */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '8px 16px',
          background: 'var(--vscode-editor-background, #0f172a)',
          borderBottom: '1px solid var(--vscode-widget-border, #1e293b)',
          zIndex: 10,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <div>
            <div style={{ fontSize: '10px', textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--vscode-descriptionForeground, #94a3b8)' }}>
              {hudLabel}
            </div>
            <div style={{ fontSize: '14px', fontWeight: 600, color: 'var(--vscode-editor-foreground, #f8fafc)' }}>
              {hudTitle}
            </div>
          </div>
          {headerControlsSlot}
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          {/* Layout Selector */}
          <select
            value={layoutType}
            onChange={(e) => handleLayoutChange(e.target.value)}
            style={{
              background: 'var(--vscode-dropdown-background, #1e293b)',
              color: 'var(--vscode-dropdown-foreground, #f8fafc)',
              border: '1px solid var(--vscode-dropdown-border, #334155)',
              borderRadius: '4px',
              padding: '4px 8px',
              fontSize: '12px',
            }}
          >
            {allowedLayouts.map((opt) => (
              <option key={opt.id} value={opt.id}>
                {opt.label}
              </option>
            ))}
          </select>

          {/* Search Input */}
          <input
            type="text"
            placeholder="Search nodes..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            style={{
              background: 'var(--vscode-input-background, #1e293b)',
              color: 'var(--vscode-input-foreground, #f8fafc)',
              border: '1px solid var(--vscode-input-border, #334155)',
              borderRadius: '4px',
              padding: '4px 8px',
              fontSize: '12px',
              width: '140px',
            }}
          />

          {/* Zoom controls */}
          <button
            onClick={() => cyRef.current?.zoom(cyRef.current.zoom() * 1.2)}
            title="Zoom In"
            style={{ background: 'transparent', border: '1px solid #334155', color: '#f8fafc', borderRadius: '4px', padding: '4px 8px', cursor: 'pointer' }}
          >
            +
          </button>
          <button
            onClick={() => cyRef.current?.zoom(cyRef.current.zoom() / 1.2)}
            title="Zoom Out"
            style={{ background: 'transparent', border: '1px solid #334155', color: '#f8fafc', borderRadius: '4px', padding: '4px 8px', cursor: 'pointer' }}
          >
            -
          </button>
          <button
            onClick={() => cyRef.current?.fit(undefined, 40)}
            title="Fit to view"
            style={{ background: 'transparent', border: '1px solid #334155', color: '#f8fafc', borderRadius: '4px', padding: '4px 8px', cursor: 'pointer' }}
          >
            Fit
          </button>

          {onRefresh && (
            <button
              onClick={onRefresh}
              title="Refresh"
              style={{ background: 'transparent', border: '1px solid #334155', color: '#f8fafc', borderRadius: '4px', padding: '4px 8px', cursor: 'pointer' }}
            >
              🔄
            </button>
          )}

          <button
            onClick={() => {
              getVsCodeApi()?.postMessage({ type: 'FOCUS_INSPECTOR_VIEW', preserveFocus: false });
            }}
            title="Open Inspector in Sidebar"
            style={{
              background: 'var(--vscode-button-secondaryBackground, #1e293b)',
              border: '1px solid var(--vscode-widget-border, #334155)',
              color: 'var(--vscode-button-secondaryForeground, #f8fafc)',
              borderRadius: '4px',
              padding: '4px 8px',
              fontSize: '11px',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: '4px'
            }}
          >
            🔍 Inspector
          </button>
        </div>
      </div>

      {/* Main Canvas Area */}
      <div style={{ flex: 1, position: 'relative', width: '100%', height: '100%' }}>
        {layoutType === 'mermaid' ? (
          <MermaidDiagramView
            graph={graph}
            serverHttpUrl={serverHttpUrl}
            onOpenFile={onOpenFile ?? (() => {})}
          />
        ) : (
          <>
            {/* SVG Guides Overlay */}
            {layoutType === 'concentric' && concentricRings.length > 0 && (
              <svg
                style={{
                  position: 'absolute',
                  top: 0,
                  left: 0,
                  width: '100%',
                  height: '100%',
                  pointerEvents: 'none',
                  zIndex: 1,
                  transform: `translate(${cyTransform.pan.x}px, ${cyTransform.pan.y}px) scale(${cyTransform.zoom})`,
                  transformOrigin: '0 0',
                }}
              >
                {concentricRings.map((ring) => (
                  <g key={`ring-${ring.tier}`}>
                    <circle
                      cx={0}
                      cy={0}
                      r={ring.radius}
                      fill="none"
                      stroke="#475569"
                      strokeWidth={1}
                      strokeDasharray="4 4"
                      opacity={0.35}
                    />
                    <text
                      x={0}
                      y={-ring.radius - 6}
                      fill="#94a3b8"
                      fontSize="11px"
                      fontWeight={600}
                      textAnchor="middle"
                      opacity={0.75}
                    >
                      {ring.label} ({ring.count})
                    </text>
                  </g>
                ))}
              </svg>
            )}

            {layoutType === 'swimlanes' && swimlanes.length > 0 && (
              <svg
                style={{
                  position: 'absolute',
                  top: 0,
                  left: 0,
                  width: '100%',
                  height: '100%',
                  pointerEvents: 'none',
                  zIndex: 1,
                  transform: `translate(${cyTransform.pan.x}px, ${cyTransform.pan.y}px) scale(${cyTransform.zoom})`,
                  transformOrigin: '0 0',
                }}
              >
                {swimlanes.map((lane) => (
                  <g key={lane.id}>
                    <rect
                      x={lane.x}
                      y={lane.y}
                      width={lane.width}
                      height={lane.height}
                      fill="rgba(30, 41, 59, 0.4)"
                      stroke={lane.color}
                      strokeWidth={1}
                      strokeDasharray="4 4"
                      rx={8}
                    />
                    <text
                      x={lane.x + 12}
                      y={lane.y + 24}
                      fill={lane.color}
                      fontSize="12px"
                      fontWeight="bold"
                      fontFamily="system-ui, sans-serif"
                    >
                      {lane.icon} {lane.title} ({lane.count})
                    </text>
                  </g>
                ))}
              </svg>
            )}

            <div ref={containerRef} style={{ width: '100%', height: '100%', background: 'transparent' }} />

            {/* Selected Node Inspector Drawer */}
            {selectedNode && (
              <div
                style={{
                  position: 'absolute',
                  bottom: 16,
                  right: 16,
                  width: 280,
                  maxHeight: '60%',
                  overflowY: 'auto',
                  background: 'var(--vscode-editor-background, #0f172a)',
                  border: '1px solid var(--vscode-widget-border, #334155)',
                  borderRadius: 6,
                  boxShadow: '0 8px 24px rgba(0, 0, 0, 0.4)',
                  padding: 12,
                  zIndex: 50,
                  fontSize: 12,
                  color: '#f8fafc',
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
                  <span style={{ fontWeight: 600, fontSize: 13, textOverflow: 'ellipsis', overflow: 'hidden' }}>{selectedNode.name || selectedNode.id}</span>
                  <button
                    onClick={() => { setSelectedNode(null); onSelectNode?.(null); }}
                    style={{ background: 'transparent', border: 'none', color: '#94a3b8', cursor: 'pointer', fontSize: 14 }}
                  >
                    ✕
                  </button>
                </div>
                <div style={{ color: '#94a3b8', fontSize: 11, marginBottom: 4 }}>
                  Kind: <span style={{ color: '#38bdf8' }}>{selectedNode.kind || 'Unknown'}</span>
                </div>
                {selectedNode.properties?.tier !== undefined && (
                  <div style={{ color: '#94a3b8', fontSize: 11, marginBottom: 4 }}>
                    Tier: <span style={{ color: '#f8fafc' }}>{String(selectedNode.properties.tier)}</span>
                  </div>
                )}
                {selectedNode.properties?.path && (
                  <div style={{ marginTop: 6 }}>
                    <div style={{ color: '#94a3b8', fontSize: 10, textTransform: 'uppercase' }}>File Path</div>
                    <div
                      onClick={() => {
                        const filePath = selectedNode.properties?.path;
                        if (filePath) onOpenFile?.(String(filePath));
                      }}
                      style={{ color: '#38bdf8', fontSize: 11, textDecoration: 'underline', cursor: 'pointer', wordBreak: 'break-all' }}
                      title="Click to open file"
                    >
                      {String(selectedNode.properties.path)}
                    </div>
                  </div>
                )}
              </div>
            )}
          </>
        )}
      </div>

      {/* Loading Overlay */}
      {loading && (
        <div style={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'rgba(15, 23, 42, 0.7)', zIndex: 100 }}>
          <div style={{ color: '#f8fafc', fontSize: 13, display: 'flex', alignItems: 'center', gap: 8 }}>
            <span>⏳ Loading graph data…</span>
          </div>
        </div>
      )}

      {/* Error Banner */}
      {error && !loading && (
        <div style={{ position: 'absolute', top: 50, left: 16, right: 16, padding: '8px 12px', background: 'rgba(239, 68, 68, 0.85)', color: '#fff', borderRadius: 4, zIndex: 90, fontSize: 12 }}>
          {error}
        </div>
      )}
    </div>
  );
};
