import React, { useEffect, useRef, useState, useMemo, useCallback } from 'react';
import cytoscape, { Core, EventObject } from 'cytoscape';
import dagre from 'cytoscape-dagre';
import { GraphData, GraphNode } from '../../../../proto/types';
import { attachNormalizedCytoscapeWheel } from '../utils/wheelZoom';
import {
  computeConcentricLayout,
  ConcentricOrbitGuide,
  ConcentricLayoutResult,
} from '../layout/concentricLayout';
import {
  OrbitConfig,
  sanitizeOrbitConfig,
  effectiveOrbitTier,
  orbitOrder,
  orbitTitle,
} from '../layout/orbitConfig';
import { computeConcentricEquispacedLayout } from '../layout/concentricEquispacedLayout';
import { computeConcentricBarycentricLayout } from '../layout/projectMapLayout';
import { computeConcentricPolarForceLayout } from '../layout/concentricPolarForceLayout';
import { computeSwimlanesLayout, SwimlaneGuide, TierDefinition } from '../layout/swimlanesLayout';
import { DomainMermaidView } from '../domain/components/DomainMermaidView';
import { getVsCodeApi } from '../vscodeApi';
import {
  SelectedNodeDetail,
  SelectedEdgeDetail,
  ConnectionDetailItem,
  EntityKind,
  CustomNodeGroup,
  NodeContentMode,
  NodeSizingMode,
  EdgeCurveMode,
  DomainLayoutName,
} from '../domain/types';
import { getNodeBadgeSvgDataUri, getEntityBadgeMeta } from '../domain/entityBadges';
import { computeGroupedGraph } from '../domain/nodeGrouping';
import { matchesSearchTokens } from '../domain/searchUtils';
import {
  OrbitLegendItem,
  buildAllOrbitLegendItems,
  applyEdgeCurveMode,
  applyEdgeLabelVisibility,
} from './DomainArchitectureView';
import { RawEdgeInfo } from '../domain/inspectorSchema';

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
  color?: string;
  highlight?: boolean;
}

export interface InspectorOverviewModel {
  title: string;
  subtitle?: string;
  icon?: string;
  metrics: InspectorOverviewMetric[];
  actionBanner?: {
    text: string;
    actionLabel: string;
    isActive: boolean;
    presetId?: string;
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
  allowedLayouts?: Array<{ id: DomainLayoutName; label: string }>;
  defaultLayoutName?: DomainLayoutName;
  defaultOrbitConfig?: OrbitConfig;
  defaultNodeSizingMode?: NodeSizingMode;
  defaultNodeContentMode?: NodeContentMode;

  storagePrefix: string;

  inspectorOverviewSlot?: InspectorOverviewModel;
  visibilityPresetsSlot?: InspectorPresetDefinition[];

  onOpenFile?: (filePath: string, lineStart?: number) => void;
  onSelectNode?: (node: GraphNode | null) => void;
  onRefresh?: () => void;
}

const DEFAULT_ALLOWED_LAYOUTS: Array<{ id: DomainLayoutName; label: string }> = [
  { id: 'concentric', label: '🎯 Concentric (Chord Min)' },
  { id: 'concentric-equispaced', label: '⭕ Concentric (Equispaced)' },
  { id: 'concentric-barycentric', label: '🧭 Concentric (Barycentric)' },
  { id: 'concentric-polar-force', label: '🧲 Concentric (Polar Force)' },
  { id: 'swimlanes', label: '🏊 Architectural Swimlanes' },
  { id: 'mermaid', label: '🧜‍♀️ Mermaid Flowchart' },
  { id: 'cose', label: '🌐 Force-Directed (CoSE)' },
  { id: 'dagre', label: '📐 Hierarchical (Dagre)' },
];

const DEFAULT_SEVEN_TIER_ORBIT_CONFIG: OrbitConfig = {
  version: 1,
  orbits: [[0], [1], [2], [3], [4], [5], [6]],
  kindTiers: {},
};

const CYTOSCAPE_STYLES: cytoscape.StylesheetStyle[] = [
  {
    selector: 'node',
    style: {
      'shape': 'data(shape)' as any,
      'width': 'data(size)',
      'height': 'data(size)',
      'background-color': 'data(bgColor)',
      'background-image': 'data(badgeSvg)',
      'background-fit': 'contain',
      'background-clip': 'node',
      'background-width': '100%',
      'background-height': '100%',
      'border-width': 3,
      'border-color': 'data(borderColor)',
      'border-opacity': 0.9,
      'label': 'data(displayLabel)',
      'color': '#f8fafc',
      'font-size': '10.5px',
      'font-weight': 600,
      'font-family': 'system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
      'text-valign': 'bottom',
      'text-halign': 'center',
      'text-margin-y': 7,
      'text-wrap': 'wrap',
      'text-max-width': '130px',
      'text-background-color': '#090d16',
      'text-background-opacity': 0.92,
      'text-background-padding': '3px',
      'text-background-shape': 'roundrectangle',
      'text-border-color': 'rgba(255, 255, 255, 0.14)',
      'text-border-width': 1,
      'text-border-opacity': 0.6,
      'transition-property': 'background-color, border-color, width, height, opacity',
      'transition-duration': 0.2,
    },
  },
  {
    selector: 'node:selected',
    style: {
      'border-color': '#ffffff',
      'border-width': 4,
      'underlay-color': '#38bdf8',
      'underlay-padding': '8px',
      'underlay-opacity': 0.5,
    },
  },
  {
    selector: 'node.group-node',
    style: {
      'width': 'data(size)',
      'height': 'data(size)',
      'border-width': 4.5,
      'border-style': 'double',
    },
  },
  {
    selector: 'node.multi-selected',
    style: {
      'border-color': '#38bdf8',
      'border-width': 4.5,
      'underlay-color': '#38bdf8',
      'underlay-padding': '8px',
      'underlay-opacity': 0.65,
    },
  },
  {
    selector: 'node.hovered',
    style: {
      'border-color': '#38bdf8',
      'border-width': 4,
    },
  },
  {
    selector: 'edge',
    style: {
      'width': 2,
      'line-color': '#64748b',
      'target-arrow-color': '#64748b',
      'target-arrow-shape': 'triangle',
      'arrow-scale': 2.0,
      'curve-style': 'bezier',
      'label': 'data(label)',
      'font-size': '8.5px',
      'font-weight': 'bold',
      'color': '#e2e8f0',
      'text-rotation': 'autorotate',
      'text-background-color': '#090d16',
      'text-background-opacity': 0.94,
      'text-background-padding': '3px',
      'text-background-shape': 'roundrectangle',
      'text-border-color': 'rgba(255, 255, 255, 0.1)',
      'text-border-width': 1,
      'text-border-opacity': 0.7,
      'opacity': 0.8,
    },
  },
  {
    selector: 'edge[category = "service_call"]',
    style: {
      'line-style': 'solid',
      'line-color': '#34d399',
      'target-arrow-color': '#34d399',
      'source-arrow-color': '#34d399',
    },
  },
  {
    selector: 'edge[category = "database"]',
    style: {
      'line-style': 'solid',
      'line-color': '#c084fc',
      'target-arrow-color': '#c084fc',
      'source-arrow-color': '#c084fc',
    },
  },
  {
    selector: 'edge[category = "messaging"]',
    style: {
      'line-style': 'dashed',
      'line-dash-pattern': [6, 4],
      'line-color': '#fbbf24',
      'target-arrow-color': '#fbbf24',
      'source-arrow-color': '#fbbf24',
    },
  },
  {
    selector: 'edge[category = "external"]',
    style: {
      'line-style': 'solid',
      'line-color': '#26a69a',
      'target-arrow-color': '#26a69a',
      'source-arrow-color': '#26a69a',
    },
  },
  {
    selector: 'edge[category = "library"]',
    style: {
      'line-style': 'solid',
      'line-color': '#34d399',
      'target-arrow-color': '#34d399',
      'source-arrow-color': '#34d399',
    },
  },
  {
    selector: 'edge[isBidirectional = "true"]',
    style: {
      'source-arrow-shape': 'triangle',
      'target-arrow-shape': 'triangle',
      'source-arrow-fill': 'filled',
      'target-arrow-fill': 'filled',
    },
  },
  {
    selector: 'edge.highlighted, edge:selected, edge.hovered',
    style: {
      'width': 3.5,
      'opacity': 1,
      'z-index': 999,
      'arrow-scale': 2.3,
    },
  },
  {
    selector: 'edge.label-hover-only',
    style: {
      'label': '',
    },
  },
  {
    selector: 'edge.label-hover-only.hovered, edge.label-hover-only:selected, edge.label-hover-only.highlighted',
    style: {
      'label': 'data(label)',
    },
  },
  {
    selector: '.dimmed',
    style: {
      'opacity': 0.12,
    },
  },
  {
    selector: 'node.search-match',
    style: {
      'underlay-color': '#f59e0b',
      'underlay-padding': '12px',
      'underlay-opacity': 0.7,
      'border-color': '#fbbf24',
      'border-width': 4,
      'opacity': 1,
      'z-index': 999,
    },
  },
];

function classifyEdgeCategory(
  kind: string | undefined,
  targetKind: string | undefined
): 'service_call' | 'database' | 'messaging' | 'external' | 'library' | 'test' {
  const k = (kind || '').toLowerCase();
  const tk = (targetKind || '').toLowerCase();
  if (tk === 'database' || tk === 'table' || tk === 'procedure' || k.includes('db') || k.includes('sql') || k.includes('table')) {
    return 'database';
  }
  if (tk === 'topic' || k.includes('publish') || k.includes('subscribe') || k.includes('consume') || k.includes('topic') || k.includes('event')) {
    return 'messaging';
  }
  if (tk === 'externalservice' || tk === 'cloudservice') {
    return 'external';
  }
  if (tk === 'library') {
    return 'library';
  }
  return 'service_call';
}

export const UniversalGraphMap: React.FC<UniversalGraphMapProps> = ({
  graph,
  loading = false,
  error = null,
  hudLabel,
  hudTitle,
  headerControlsSlot,
  tierDefinitions,
  allowedLayouts = DEFAULT_ALLOWED_LAYOUTS,
  defaultLayoutName = 'concentric',
  defaultOrbitConfig = DEFAULT_SEVEN_TIER_ORBIT_CONFIG,
  defaultNodeSizingMode = 'sqrt-weight',
  defaultNodeContentMode = 'icon',
  storagePrefix,
  inspectorOverviewSlot,
  visibilityPresetsSlot,
  onOpenFile,
  onSelectNode,
  onRefresh,
}) => {
  const readStored = useCallback(
    (suffix: string, fallback: string): string => {
      try {
        if (typeof localStorage !== 'undefined') {
          return localStorage.getItem(`${storagePrefix}${suffix}`) ?? fallback;
        }
      } catch {
        // ignored: localStorage unavailable in restricted webview context
      }
      return fallback;
    },
    [storagePrefix]
  );

  const writeStored = useCallback(
    (suffix: string, val: string): void => {
      try {
        if (typeof localStorage !== 'undefined') {
          localStorage.setItem(`${storagePrefix}${suffix}`, val);
        }
      } catch {
        // ignored: localStorage quota or security restriction
      }
    },
    [storagePrefix]
  );

  const [layoutName, setLayoutName] = useState<DomainLayoutName>(() => {
    const saved = readStored('layout', defaultLayoutName) as DomainLayoutName;
    return allowedLayouts.some((l) => l.id === saved) ? saved : defaultLayoutName;
  });

  const [activeSidebarTab, setActiveSidebarTab] = useState<'info' | 'orbits' | 'visibility' | 'settings'>('info');
  const [isInspectorCollapsed, setIsInspectorCollapsed] = useState(false);
  const [isEdgeInspectorCollapsed, setIsEdgeInspectorCollapsed] = useState(false);

  const [searchQuery, setSearchQuery] = useState('');
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [selectedEdgeId, setSelectedEdgeId] = useState<string | null>(null);

  const [spacingFactor, setSpacingFactor] = useState<number>(() => {
    const v = parseFloat(readStored('spacing', '1.0'));
    return Number.isFinite(v) && v >= 0.4 && v <= 3.5 ? v : 1.0;
  });

  const [orbitDistance, setOrbitDistance] = useState<number>(() => {
    const v = parseInt(readStored('orbitDistance', '280'), 10);
    return Number.isFinite(v) && v >= 120 && v <= 800 ? v : 280;
  });

  const [minOrbitNodeDistance, setMinOrbitNodeDistance] = useState<number>(() => {
    const v = parseInt(readStored('minOrbitNodeDistance', '115'), 10);
    return Number.isFinite(v) && v >= 50 && v <= 400 ? v : 115;
  });

  const [autoRelayoutOnFilter, setAutoRelayoutOnFilter] = useState<boolean>(() => {
    return readStored('autoRelayout', 'true') !== 'false';
  });

  const [orbitConfig, setOrbitConfig] = useState<OrbitConfig>(() => {
    const raw = readStored('orbitConfig', '');
    if (raw) {
      try {
        return sanitizeOrbitConfig(JSON.parse(raw));
      } catch {
        // ignored: malformed persisted orbit config JSON
      }
    }
    return defaultOrbitConfig;
  });

  const [hiddenOrbitTiers, setHiddenOrbitTiers] = useState<Set<number>>(new Set());
  const [hiddenNodeIds, setHiddenNodeIds] = useState<Set<string>>(new Set());
  const [hiddenTypes, setHiddenTypes] = useState<Set<EntityKind>>(new Set());
  const [hideIsolatedNodes, setHideIsolatedNodes] = useState<boolean>(false);

  const [groupedTypes, setGroupedTypes] = useState<Set<EntityKind>>(() => {
    try {
      const saved = readStored('grouped_types', '');
      if (saved) {
        const parsed = JSON.parse(saved);
        if (Array.isArray(parsed)) return new Set<EntityKind>(parsed);
      }
    } catch {}
    return new Set<EntityKind>();
  });

  const [customGroups, setCustomGroups] = useState<CustomNodeGroup[]>(() => {
    try {
      const saved = readStored('custom_groups', '');
      if (saved) {
        const parsed = JSON.parse(saved);
        if (Array.isArray(parsed)) return parsed;
      }
    } catch {}
    return [];
  });

  const [multiSelectedNodeIds, setMultiSelectedNodeIds] = useState<Set<string>>(new Set());
  const [floatingGroupName, setFloatingGroupName] = useState('');
  const multiSelectedNodeIdsRef = useRef(multiSelectedNodeIds);
  multiSelectedNodeIdsRef.current = multiSelectedNodeIds;

  useEffect(() => {
    writeStored('grouped_types', JSON.stringify([...groupedTypes]));
  }, [groupedTypes, writeStored]);

  useEffect(() => {
    writeStored('custom_groups', JSON.stringify(customGroups));
  }, [customGroups, writeStored]);

  const toggleTypeGrouping = useCallback((kind: EntityKind) => {
    setGroupedTypes((prev) => {
      const next = new Set(prev);
      if (next.has(kind)) next.delete(kind);
      else next.add(kind);
      return next;
    });
  }, []);

  const createCustomGroup = useCallback((name: string, nodeIds: string[]) => {
    const newGroup: CustomNodeGroup = {
      id: 'cg_' + Date.now().toString(36) + '_' + Math.random().toString(36).substring(2, 6),
      name: name.trim() || `Group (${nodeIds.length})`,
      nodeIds: Array.from(new Set(nodeIds)),
      collapsed: true,
    };
    setCustomGroups((prev) => [...prev, newGroup]);
    setMultiSelectedNodeIds(new Set());
  }, []);

  const toggleCustomGroupCollapse = useCallback((groupId: string) => {
    setCustomGroups((prev) =>
      prev.map((g) => (g.id === groupId ? { ...g, collapsed: !g.collapsed } : g))
    );
  }, []);

  const deleteCustomGroup = useCallback((groupId: string) => {
    setCustomGroups((prev) => prev.filter((g) => g.id !== groupId));
  }, []);

  const removeNodeFromCustomGroup = useCallback((groupId: string, nodeId: string) => {
    setCustomGroups((prev) =>
      prev
        .map((g) => {
          if (g.id !== groupId) return g;
          return { ...g, nodeIds: g.nodeIds.filter((id) => id !== nodeId) };
        })
        .filter((g) => g.nodeIds.length > 0)
    );
  }, []);

  const toggleMultiSelectedNode = useCallback((id: string) => {
    setMultiSelectedNodeIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  const clearMultiSelection = useCallback(() => {
    setMultiSelectedNodeIds(new Set());
  }, []);

  const addNodesToCustomGroup = useCallback((groupId: string, nodeIds: string[]) => {
    setCustomGroups((prev) =>
      prev.map((g) => {
        if (g.id !== groupId) return g;
        const set = new Set(g.nodeIds);
        for (const id of nodeIds) set.add(id);
        return { ...g, nodeIds: Array.from(set) };
      })
    );
    setMultiSelectedNodeIds(new Set());
  }, []);

  // Context Menu State for Right-Click on Nodes
  const [contextMenu, setContextMenu] = useState<{
    x: number;
    y: number;
    nodeId: string;
    nodeName: string;
    nodeKind: EntityKind;
  } | null>(null);
  const [isAddGroupSubmenuOpen, setIsAddGroupSubmenuOpen] = useState(false);

  useEffect(() => {
    if (!contextMenu) {
      setIsAddGroupSubmenuOpen(false);
    }
  }, [contextMenu]);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setContextMenu(null);
        clearMultiSelection();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [clearMultiSelection]);

  const [nodeContentMode, setNodeContentMode] = useState<NodeContentMode>(() => {
    const v = readStored('nodeContentMode', defaultNodeContentMode);
    return v === 'icon' || v === 'icon-code' || v === 'metric' ? v : defaultNodeContentMode;
  });

  const [nodeSizingMode, setNodeSizingMode] = useState<NodeSizingMode>(() => {
    const v = readStored('nodeSizingMode', defaultNodeSizingMode);
    return v === 'uniform' || v === 'sqrt-weight' || v === 'degree' ? v : defaultNodeSizingMode;
  });

  const [edgeCurveMode, setEdgeCurveMode] = useState<EdgeCurveMode>(() => {
    const v = readStored('edgeCurveMode', 'bezier');
    return v === 'straight' || v === 'bezier' || v === 'avoid-inner' ? v : 'bezier';
  });

  const [edgeLabelsOnHover, setEdgeLabelsOnHover] = useState<boolean>(() => {
    return readStored('edgeLabelsOnHover', 'true') !== 'false';
  });

  const [baseIconSize, setBaseIconSize] = useState<number>(() => {
    const v = parseInt(readStored('baseIconSize', '48'), 10);
    return Number.isFinite(v) && v >= 28 && v <= 84 ? v : 48;
  });

  const [fontSize, setFontSize] = useState<number>(() => {
    const v = parseInt(readStored('fontSize', '10'), 10);
    return Number.isFinite(v) && v >= 7 && v <= 24 ? v : 10;
  });

  const [curveFactor, setCurveFactor] = useState<number>(() => {
    const v = parseInt(readStored('curveFactor', '35'), 10);
    return Number.isFinite(v) && v >= 0 && v <= 100 ? v : 35;
  });

  const [wheelSensitivity, setWheelSensitivity] = useState<number>(() => {
    const v = parseFloat(readStored('wheelSensitivity', '1.0'));
    return Number.isFinite(v) && v >= 0.5 && v <= 3.0 ? v : 1.0;
  });

  const [relayoutCounter, setRelayoutCounter] = useState(0);

  const containerRef = useRef<HTMLDivElement>(null);
  const cyRef = useRef<Core | null>(null);
  const wheelSensitivityRef = useRef(wheelSensitivity);
  wheelSensitivityRef.current = wheelSensitivity;

  const [cyTransform, setCyTransform] = useState<{ pan: { x: number; y: number }; zoom: number }>({
    pan: { x: 0, y: 0 },
    zoom: 1,
  });

  const [concentricGuides, setConcentricGuides] = useState<ConcentricOrbitGuide[]>([]);
  const [swimlaneGuides, setSwimlaneGuides] = useState<SwimlaneGuide[]>([]);
  const [orbitLegendItems, setOrbitLegendItems] = useState<OrbitLegendItem[]>([]);

  const customTitlesByTier = useMemo<Record<number, string>>(() => {
    const map: Record<number, string> = {};
    for (const td of tierDefinitions ?? []) {
      map[td.tier] = td.title;
    }
    return map;
  }, [tierDefinitions]);

  // Parse raw graph into rich SelectedNodeDetail & RawEdgeInfo models
  const parsedGraph = useMemo(() => {
    const detailMap = new Map<string, SelectedNodeDetail>();
    const echelonMap = new Map<string, number>();
    const rawEdges: RawEdgeInfo[] = [];
    const allCyNodes: cytoscape.NodeDefinition[] = [];

    const nodes = graph?.nodes ?? [];
    const edges = graph?.edges ?? [];

    for (const n of nodes) {
      const p = n.properties ?? {};
      const rawKind = String(n.kind || 'Type');
      const kind: EntityKind =
        rawKind === 'Folder' || rawKind === 'File'
          ? 'Module'
          : ((rawKind as EntityKind) || 'Type');
      const badgeMeta = getEntityBadgeMeta(kind);
      const tier = Number.isFinite(Number(p.tier)) ? Number(p.tier) : Number.isFinite(Number(p.echelon_tier)) ? Number(p.echelon_tier) : 2;
      echelonMap.set(n.id, tier);

      let filesList: Array<{ id: string; name: string; filePath: string; lineStart: number }> = [];
      if (p.files) {
        try {
          const parsed = JSON.parse(p.files);
          if (Array.isArray(parsed)) {
            filesList = parsed.map((f: string, idx: number) => ({
              id: `${n.id}:file:${idx}`,
              name: String(f).split(/[/\\]/).pop() || String(f),
              filePath: String(f),
              lineStart: 1,
            }));
          }
        } catch {
          // ignored: files property is not a JSON array
        }
      }

      const primaryFilePath = n.filePath || p.file_path || p.path || (filesList.length > 0 ? filesList[0].filePath : undefined);
      const primaryLineStart = n.lineStart || (p.start_line ? parseInt(p.start_line, 10) : undefined);

      const detail: SelectedNodeDetail = {
        id: n.id,
        name: n.name || n.id,
        displayName: n.displayName || n.name || n.id,
        kind,
        displayTag: p.sub_kind || badgeMeta.code,
        bgColor: p.color || p.bgColor || badgeMeta.bgColor,
        borderColor: p.borderColor || badgeMeta.borderColor,
        primaryFilePath,
        primaryLineStart,
        properties: p,
        tier,
        tierLabel: p.tier_label || customTitlesByTier[tier] || `Tier ${tier}`,
        tierReason: p.tier_reason,
        inboundCallsCount: Number(p.fan_in || 0),
        outboundCallsCount: Number(p.fan_out || 0),
        dbCount: Number(p.data_file_count || 0),
        messagingCount: 0,
        fileCount: Number(p.file_count || filesList.length || 1),
        projects: filesList,
      };
      detailMap.set(n.id, detail);

      allCyNodes.push({
        group: 'nodes',
        data: {
          id: n.id,
          kind,
          echelonTier: tier,
        },
      });
    }

    // Compute accurate inbound/outbound/db/messaging counts from edges
    const inCounts = new Map<string, number>();
    const outCounts = new Map<string, number>();
    const dbCounts = new Map<string, number>();
    const msgCounts = new Map<string, number>();

    for (const e of edges) {
      const sDetail = detailMap.get(e.source);
      const tDetail = detailMap.get(e.target);
      if (!sDetail || !tDetail) continue;

      const count = Number(e.properties?.call_count ?? e.properties?.count ?? 1) || 1;
      const category = classifyEdgeCategory(e.kind, tDetail.kind);
      const edgeId = e.id ?? `${e.source}->${e.target}`;

      rawEdges.push({
        id: edgeId,
        source: e.source,
        target: e.target,
        category,
        label: e.kind || 'CALLS',
        count,
        kind: e.kind,
      });

      if (category === 'database') {
        dbCounts.set(e.source, (dbCounts.get(e.source) ?? 0) + 1);
        dbCounts.set(e.target, (dbCounts.get(e.target) ?? 0) + 1);
      } else if (category === 'messaging') {
        msgCounts.set(e.source, (msgCounts.get(e.source) ?? 0) + 1);
        msgCounts.set(e.target, (msgCounts.get(e.target) ?? 0) + 1);
      } else {
        outCounts.set(e.source, (outCounts.get(e.source) ?? 0) + count);
        inCounts.set(e.target, (inCounts.get(e.target) ?? 0) + count);
      }
    }

    for (const [id, detail] of detailMap.entries()) {
      if (inCounts.has(id)) detail.inboundCallsCount = inCounts.get(id)!;
      if (outCounts.has(id)) detail.outboundCallsCount = outCounts.get(id)!;
      if (dbCounts.has(id)) detail.dbCount = Math.max(detail.dbCount, dbCounts.get(id)!);
      if (msgCounts.has(id)) detail.messagingCount = msgCounts.get(id)!;
    }

    return { detailMap, echelonMap, rawEdges, allCyNodes };
  }, [graph, customTitlesByTier]);

  // Filter visible nodes & bundle edges
  const { visibleNodes, visibleEdges, edgeDetailMap, effectiveRawEdges, effectiveDetailMap } = useMemo(() => {
    const candidateNodeIds = new Set<string>();
    for (const [id, detail] of parsedGraph.detailMap.entries()) {
      if (hiddenNodeIds.has(id)) continue;
      if (hiddenTypes.has(detail.kind)) continue;
      const effTier = effectiveOrbitTier(orbitConfig, detail.kind, detail.tier ?? 2);
      if (hiddenOrbitTiers.has(effTier)) continue;
      candidateNodeIds.add(id);
    }

    const activeRawEdges = parsedGraph.rawEdges.filter(
      (e) => candidateNodeIds.has(e.source) && candidateNodeIds.has(e.target) && e.source !== e.target
    );

    if (hideIsolatedNodes) {
      const connected = new Set<string>();
      for (const e of activeRawEdges) {
        connected.add(e.source);
        connected.add(e.target);
      }
      for (const id of Array.from(candidateNodeIds)) {
        if (!connected.has(id)) {
          candidateNodeIds.delete(id);
        }
      }
    }

    const unhiddenRawNodes = Array.from(candidateNodeIds)
      .map((id) => parsedGraph.detailMap.get(id)!)
      .filter(Boolean);

    const groupedResult = computeGroupedGraph({
      rawNodes: unhiddenRawNodes,
      rawEdges: activeRawEdges,
      groupedTypes,
      customGroups,
      hiddenNodeIds,
    });

    const vNodes: cytoscape.ElementDefinition[] = [];
    for (const detail of groupedResult.visibleNodes) {
      const id = detail.id;
      const badgeMeta = getEntityBadgeMeta(detail.kind);
      const effTier = effectiveOrbitTier(orbitConfig, detail.kind, detail.tier ?? 2);

      if (detail.isGroup) {
        const groupSize = Math.round(baseIconSize * 1.15);
        const badgeSvg = getNodeBadgeSvgDataUri(detail.kind, {
          isGroup: true,
          count: detail.memberNodes?.length || 0,
        });

        vNodes.push({
          group: 'nodes',
          classes: 'group-node',
          data: {
            id,
            displayLabel: detail.displayName,
            name: detail.name,
            kind: detail.kind,
            isGroup: 'true',
            groupType: detail.groupType,
            echelonTier: effTier,
            shape: badgeMeta.shape,
            size: groupSize,
            bgColor: detail.bgColor,
            borderColor: detail.borderColor,
            badgeSvg,
          },
        });
      } else {
        const totalConnections =
          detail.inboundCallsCount + detail.outboundCallsCount + detail.dbCount + detail.messagingCount;
        const weightVal = Math.max(1, detail.fileCount || totalConnections || 1);

        let computedSize = baseIconSize;
        if (nodeSizingMode === 'sqrt-weight') {
          const base = Math.round(baseIconSize * (34 / 52));
          const factor = (baseIconSize / 52) * 8.5;
          const minSize = Math.round(baseIconSize * (36 / 52));
          const maxSize = Math.round(baseIconSize * (88 / 52));
          computedSize = Math.round(Math.max(minSize, Math.min(maxSize, base + factor * Math.sqrt(weightVal))));
        } else if (nodeSizingMode === 'degree') {
          const base = Math.round(baseIconSize * (36 / 52));
          const factor = (baseIconSize / 52) * 6.5;
          const minSize = Math.round(baseIconSize * (38 / 52));
          const maxSize = Math.round(baseIconSize * (88 / 52));
          computedSize = Math.round(
            Math.max(minSize, Math.min(maxSize, base + factor * Math.sqrt(Math.max(1, totalConnections))))
          );
        }

        const metricText = detail.fileCount && detail.fileCount > 1 ? `${detail.fileCount}f` : `${totalConnections}`;
        const badgeSvg = getNodeBadgeSvgDataUri(detail.kind, {
          isGroup: false,
          count: 0,
          contentMode: nodeContentMode,
          metricText,
        });

        vNodes.push({
          group: 'nodes',
          data: {
            id,
            displayLabel: detail.displayName,
            name: detail.name,
            kind: detail.kind,
            echelonTier: effTier,
            shape: badgeMeta.shape,
            size: computedSize,
            bgColor: detail.bgColor,
            borderColor: detail.borderColor,
            badgeSvg,
          },
        });
      }
    }

    // Bundle parallel edges per visible pair {u, v}
    const pairMap = new Map<string, Array<RawEdgeInfo & { originalSource?: string; originalTarget?: string }>>();
    const visibleIdsSet = new Set(groupedResult.visibleNodes.map((n) => n.id));

    for (const e of activeRawEdges) {
      const effU = groupedResult.nodeToGroupId.get(e.source) ?? e.source;
      const effV = groupedResult.nodeToGroupId.get(e.target) ?? e.target;
      if (effU === effV || !visibleIdsSet.has(effU) || !visibleIdsSet.has(effV)) continue;

      const key = effU < effV ? `${effU}--${effV}` : `${effV}--${effU}`;
      let list = pairMap.get(key);
      if (!list) {
        list = [];
        pairMap.set(key, list);
      }
      list.push({ ...e, source: effU, target: effV, originalSource: e.source, originalTarget: e.target });
    }

    const vEdges: cytoscape.ElementDefinition[] = [];
    const eDetailMap = new Map<string, SelectedEdgeDetail>();

    for (const [pairKey, items] of pairMap.entries()) {
      const [nodeU, nodeV] = pairKey.split('--');
      const hasUtoV = items.some((i) => i.source === nodeU && i.target === nodeV);
      const hasVtoU = items.some((i) => i.source === nodeV && i.target === nodeU);
      const isBidirectional = hasUtoV && hasVtoU;

      const edgeSource = hasUtoV ? nodeU : nodeV;
      const edgeTarget = hasUtoV ? nodeV : nodeU;
      const primaryCategory = items[0].category === 'library' || items[0].category === 'test' ? 'service_call' : items[0].category;
      const totalCount = items.reduce((sum, i) => sum + i.count, 0);

      const edgeLabel = isBidirectional
        ? `⇄ ${totalCount}`
        : primaryCategory === 'database'
        ? 'USES_DB'
        : totalCount > 1
        ? `${totalCount} calls`
        : items[0].label || 'CALLS';

      const edgeId = `edge:${pairKey}`;
      vEdges.push({
        group: 'edges',
        data: {
          id: edgeId,
          source: edgeSource,
          target: edgeTarget,
          category: items[0].category,
          label: edgeLabel,
          count: totalCount,
          isBidirectional: isBidirectional ? 'true' : 'false',
        },
      });

      const sDetail = groupedResult.effectiveDetailMap.get(edgeSource);
      const tDetail = groupedResult.effectiveDetailMap.get(edgeTarget);
      const detailItems: ConnectionDetailItem[] = items.map((it) => ({
        id: it.id,
        sourceId: it.originalSource || it.source,
        sourceName: parsedGraph.detailMap.get(it.originalSource || it.source)?.displayName || it.originalSource || it.source,
        targetId: it.originalTarget || it.target,
        targetName: parsedGraph.detailMap.get(it.originalTarget || it.target)?.displayName || it.originalTarget || it.target,
        category: it.category,
        kind: it.kind || it.label,
        label: it.label,
        count: it.count,
        isTransitive: false,
      }));

      eDetailMap.set(edgeId, {
        id: edgeId,
        sourceId: edgeSource,
        sourceName: sDetail?.displayName || edgeSource,
        sourceKind: sDetail?.kind || 'Type',
        sourceTag: sDetail?.displayTag || 'TYP',
        sourceBgColor: sDetail?.bgColor || '#3b82f6',
        targetId: edgeTarget,
        targetName: tDetail?.displayName || edgeTarget,
        targetKind: tDetail?.kind || 'Type',
        targetTag: tDetail?.displayTag || 'TYP',
        targetBgColor: tDetail?.bgColor || '#3b82f6',
        isBidirectional,
        hasForward: hasUtoV,
        hasReverse: hasVtoU,
        totalInteractions: totalCount,
        directCallsCount: items.filter((i) => i.category === 'service_call' || i.category === 'library').reduce((s, i) => s + i.count, 0),
        messagingCount: items.filter((i) => i.category === 'messaging').reduce((s, i) => s + i.count, 0),
        transitiveCount: 0,
        dbCount: items.filter((i) => i.category === 'database').reduce((s, i) => s + i.count, 0),
        primaryCategory,
        items: detailItems,
      });
    }

    return {
      visibleNodes: vNodes,
      visibleEdges: vEdges,
      edgeDetailMap: eDetailMap,
      effectiveRawEdges: activeRawEdges,
      effectiveDetailMap: groupedResult.effectiveDetailMap,
    };
  }, [
    parsedGraph,
    hiddenNodeIds,
    hiddenTypes,
    hiddenOrbitTiers,
    hideIsolatedNodes,
    orbitConfig,
    baseIconSize,
    nodeSizingMode,
    nodeContentMode,
    groupedTypes,
    customGroups,
  ]);

  const selectedNode = useMemo(
    () => (selectedNodeId ? effectiveDetailMap.get(selectedNodeId) ?? null : null),
    [selectedNodeId, effectiveDetailMap]
  );

  const selectedEdge = useMemo(
    () => (selectedEdgeId ? edgeDetailMap.get(selectedEdgeId) ?? null : null),
    [selectedEdgeId, edgeDetailMap]
  );

  const hiddenCount = useMemo(() => {
    let count = 0;
    for (const [id, detail] of parsedGraph.detailMap.entries()) {
      const effTier = effectiveOrbitTier(orbitConfig, detail.kind, detail.tier ?? 2);
      if (
        hiddenNodeIds.has(id) ||
        hiddenTypes.has(detail.kind) ||
        hiddenOrbitTiers.has(effTier)
      ) {
        count++;
      }
    }
    return count;
  }, [parsedGraph.detailMap, orbitConfig, hiddenNodeIds, hiddenTypes, hiddenOrbitTiers]);

  const hideNode = useCallback((nodeId: string) => {
    setHiddenNodeIds((prev) => new Set(prev).add(nodeId));
    setSelectedNodeId((curr) => (curr === nodeId ? null : curr));
    setContextMenu(null);
    if (cyRef.current) {
      cyRef.current.elements().removeClass('highlighted dimmed');
    }
  }, []);

  const getDownstreamNodeIds = useCallback((rootNodeId: string): string[] => {
    const downstreamIds: string[] = [];
    const queue = [rootNodeId];
    const visited = new Set<string>([rootNodeId]);

    const outEdgesMap = new Map<string, string[]>();
    for (const e of parsedGraph.rawEdges) {
      const arr = outEdgesMap.get(e.source) || [];
      arr.push(e.target);
      outEdgesMap.set(e.source, arr);
    }

    while (queue.length > 0) {
      const curr = queue.shift()!;
      const outgoing = outEdgesMap.get(curr) || [];
      for (const target of outgoing) {
        if (!visited.has(target)) {
          visited.add(target);
          downstreamIds.push(target);
          queue.push(target);
        }
      }
    }
    return downstreamIds;
  }, [parsedGraph.rawEdges]);

  const hideDownstreamNodes = useCallback((rootNodeId: string) => {
    const downstreamIds = getDownstreamNodeIds(rootNodeId);
    if (downstreamIds.length > 0) {
      setHiddenNodeIds((prev) => {
        const next = new Set(prev);
        for (const id of downstreamIds) next.add(id);
        return next;
      });
      const downstreamSet = new Set(downstreamIds);
      setSelectedNodeId((curr) => (curr && downstreamSet.has(curr) ? null : curr));
    }
    setContextMenu(null);
    if (cyRef.current) {
      cyRef.current.elements().removeClass('highlighted dimmed');
    }
  }, [getDownstreamNodeIds]);

  const hideAllOfKind = useCallback((kind: EntityKind) => {
    setHiddenTypes((prev) => new Set(prev).add(kind));
    setSelectedNodeId((curr) => {
      if (!curr) return null;
      const detail = effectiveDetailMap.get(curr);
      return detail?.kind === kind ? null : curr;
    });
    setContextMenu(null);
    if (cyRef.current) {
      cyRef.current.elements().removeClass('highlighted dimmed');
    }
  }, [effectiveDetailMap]);

  // Handlers for settings and layout
  const handleLayoutChange = useCallback(
    (next: DomainLayoutName) => {
      setLayoutName(next);
      writeStored('layout', next);
    },
    [writeStored]
  );

  const handleSpacingChange = useCallback(
    (factor: number) => {
      const clamped = Math.max(0.4, Math.min(3.5, +factor.toFixed(2)));
      setSpacingFactor(clamped);
      writeStored('spacing', String(clamped));
    },
    [writeStored]
  );

  const handleOrbitDistanceChange = useCallback(
    (dist: number) => {
      const clamped = Math.max(120, Math.min(800, Math.round(dist)));
      setOrbitDistance(clamped);
      writeStored('orbitDistance', String(clamped));
    },
    [writeStored]
  );

  const handleMinOrbitNodeDistanceChange = useCallback(
    (dist: number) => {
      const clamped = Math.max(50, Math.min(400, Math.round(dist)));
      setMinOrbitNodeDistance(clamped);
      writeStored('minOrbitNodeDistance', String(clamped));
    },
    [writeStored]
  );

  const handleNodeContentModeChange = useCallback(
    (mode: NodeContentMode) => {
      setNodeContentMode(mode);
      writeStored('nodeContentMode', mode);
    },
    [writeStored]
  );

  const handleNodeSizingModeChange = useCallback(
    (mode: NodeSizingMode) => {
      setNodeSizingMode(mode);
      writeStored('nodeSizingMode', mode);
    },
    [writeStored]
  );

  const handleEdgeCurveModeChange = useCallback(
    (mode: EdgeCurveMode) => {
      setEdgeCurveMode(mode);
      writeStored('edgeCurveMode', mode);
      applyEdgeCurveMode(cyRef.current, mode, { x: 0, y: 0 }, curveFactor);
    },
    [writeStored, curveFactor]
  );

  const handleEdgeLabelsOnHoverChange = useCallback(
    (onHover: boolean) => {
      setEdgeLabelsOnHover(onHover);
      writeStored('edgeLabelsOnHover', String(onHover));
      applyEdgeLabelVisibility(cyRef.current, onHover);
    },
    [writeStored]
  );

  const handleBaseIconSizeChange = useCallback(
    (size: number) => {
      const clamped = Math.max(28, Math.min(84, Math.round(size)));
      setBaseIconSize(clamped);
      writeStored('baseIconSize', String(clamped));
    },
    [writeStored]
  );

  const handleFontSizeChange = useCallback(
    (size: number) => {
      const clamped = Math.max(7, Math.min(24, Math.round(size)));
      setFontSize(clamped);
      writeStored('fontSize', String(clamped));
      cyRef.current?.nodes().style('font-size', `${clamped}px`);
    },
    [writeStored]
  );

  const handleCurveFactorChange = useCallback(
    (factor: number) => {
      const clamped = Math.max(0, Math.min(100, Math.round(factor)));
      setCurveFactor(clamped);
      writeStored('curveFactor', String(clamped));
      applyEdgeCurveMode(cyRef.current, edgeCurveMode, { x: 0, y: 0 }, clamped);
    },
    [writeStored, edgeCurveMode]
  );

  const handleWheelSensitivityChange = useCallback(
    (sens: number) => {
      const clamped = Math.max(0.5, Math.min(3.0, +sens.toFixed(1)));
      setWheelSensitivity(clamped);
      writeStored('wheelSensitivity', String(clamped));
    },
    [writeStored]
  );

  // Sync state with InspectorSidebarView
  const buildInspectorSyncedState = useCallback(() => {
    const inspectorOverview = inspectorOverviewSlot
      ? {
          title: inspectorOverviewSlot.title,
          subtitle: inspectorOverviewSlot.subtitle,
          icon: inspectorOverviewSlot.icon,
          metrics: inspectorOverviewSlot.metrics.map((m) => ({
            label: m.label,
            value: m.value,
            color: m.color,
          })),
          actionBanner: inspectorOverviewSlot.actionBanner
            ? {
                text: inspectorOverviewSlot.actionBanner.text,
                actionLabel: inspectorOverviewSlot.actionBanner.actionLabel,
                isActive: inspectorOverviewSlot.actionBanner.isActive,
                presetId: inspectorOverviewSlot.actionBanner.presetId,
              }
            : undefined,
        }
      : undefined;

    const customVisibilityPresets = visibilityPresetsSlot?.map((p) => ({
      id: p.id,
      label: p.label,
      description: p.description,
      icon: p.icon,
      isActive: p.isActive,
      badge: p.badge,
    }));

    return {
      hudLabel,
      hudTitle,
      isMatrixMode: false,
      effectiveSidebarTab: activeSidebarTab,
      hasOrbitLegend: orbitLegendItems.length > 0,
      orbitLegendItems,
      hiddenCount,
      selectedNode,
      selectedEdge,
      nodeDetailEntries: Array.from(effectiveDetailMap.entries()),
      rawEdges: effectiveRawEdges,
      isInspectorCollapsed,
      isEdgeInspectorCollapsed,
      availableDomains: [],
      layoutName,
      spacingFactor,
      orbitDistance,
      minOrbitNodeDistance,
      autoRelayoutOnFilter,
      orbitConfig,
      defaultOrbitConfig,
      hiddenOrbitTiers: Array.from(hiddenOrbitTiers),
      echelonEntries: Array.from(parsedGraph.echelonMap.entries()),
      hiddenNodeIds: Array.from(hiddenNodeIds),
      hiddenTypes: Array.from(hiddenTypes),
      hideSingleConnectionDbs: false,
      hideIsolatedNodes,
      showTransitiveEdges: false,
      counts: {
        singleConnDbs: 0,
        sharedDbs: 0,
        transitiveCalls: 0,
      },
      groupedTypes: Array.from(groupedTypes),
      customGroups,
      nodeContentMode,
      nodeSizingMode,
      edgeCurveMode,
      edgeLabelsOnHover,
      baseIconSize,
      fontSize,
      curveFactor,
      wheelSensitivity,
      inspectorOverview,
      customVisibilityPresets,
    };
  }, [
    hudLabel,
    hudTitle,
    activeSidebarTab,
    orbitLegendItems,
    hiddenCount,
    selectedNode,
    selectedEdge,
    parsedGraph,
    effectiveDetailMap,
    effectiveRawEdges,
    isInspectorCollapsed,
    isEdgeInspectorCollapsed,
    layoutName,
    spacingFactor,
    orbitDistance,
    minOrbitNodeDistance,
    autoRelayoutOnFilter,
    orbitConfig,
    defaultOrbitConfig,
    hiddenOrbitTiers,
    hiddenNodeIds,
    hiddenTypes,
    groupedTypes,
    customGroups,
    hideIsolatedNodes,
    nodeContentMode,
    nodeSizingMode,
    edgeCurveMode,
    edgeLabelsOnHover,
    baseIconSize,
    fontSize,
    curveFactor,
    wheelSensitivity,
    inspectorOverviewSlot,
    visibilityPresetsSlot,
  ]);

  const buildInspectorSyncedStateRef = useRef(buildInspectorSyncedState);
  buildInspectorSyncedStateRef.current = buildInspectorSyncedState;

  useEffect(() => {
    getVsCodeApi()?.postMessage({
      type: 'SYNC_INSPECTOR_STATE',
      state: buildInspectorSyncedState(),
    });
  }, [buildInspectorSyncedState]);

  // Listen to InspectorSidebarView actions
  const actionContextRef = useRef({
    parsedGraph,
    effectiveDetailMap,
    visibilityPresetsSlot,
    inspectorOverviewSlot,
    defaultOrbitConfig,
    onOpenFile,
    onSelectNode,
    handleLayoutChange,
    handleSpacingChange,
    handleOrbitDistanceChange,
    handleMinOrbitNodeDistanceChange,
    handleNodeContentModeChange,
    handleNodeSizingModeChange,
    handleEdgeCurveModeChange,
    handleEdgeLabelsOnHoverChange,
    handleBaseIconSizeChange,
    handleFontSizeChange,
    handleCurveFactorChange,
    handleWheelSensitivityChange,
    toggleTypeGrouping,
    createCustomGroup,
    toggleCustomGroupCollapse,
    deleteCustomGroup,
    removeNodeFromCustomGroup,
    toggleMultiSelectedNode,
    clearMultiSelection,
    addNodesToCustomGroup,
    setContextMenu,
    hideNode,
    hideDownstreamNodes,
    hideAllOfKind,
    writeStored,
  });
  actionContextRef.current = {
    parsedGraph,
    effectiveDetailMap,
    visibilityPresetsSlot,
    inspectorOverviewSlot,
    defaultOrbitConfig,
    onOpenFile,
    onSelectNode,
    handleLayoutChange,
    handleSpacingChange,
    handleOrbitDistanceChange,
    handleMinOrbitNodeDistanceChange,
    handleNodeContentModeChange,
    handleNodeSizingModeChange,
    handleEdgeCurveModeChange,
    handleEdgeLabelsOnHoverChange,
    handleBaseIconSizeChange,
    handleFontSizeChange,
    handleCurveFactorChange,
    handleWheelSensitivityChange,
    toggleTypeGrouping,
    createCustomGroup,
    toggleCustomGroupCollapse,
    deleteCustomGroup,
    removeNodeFromCustomGroup,
    toggleMultiSelectedNode,
    clearMultiSelection,
    addNodesToCustomGroup,
    setContextMenu,
    hideNode,
    hideDownstreamNodes,
    hideAllOfKind,
    writeStored,
  };

  useEffect(() => {
    const onMessage = (event: MessageEvent) => {
      const msg = event.data;
      if (msg?.type === 'REQUEST_INSPECTOR_SYNC') {
        getVsCodeApi()?.postMessage({
          type: 'SYNC_INSPECTOR_STATE',
          state: buildInspectorSyncedStateRef.current(),
        });
        return;
      }
      if (msg?.type !== 'INSPECTOR_ACTION' || !msg.action) return;
      const act = msg.action;
      const ctx = actionContextRef.current;

      switch (act.type) {
        case 'SET_SIDEBAR_TAB':
          setActiveSidebarTab(act.tab);
          break;
        case 'SET_INSPECTOR_COLLAPSED':
          setIsInspectorCollapsed(Boolean(act.collapsed));
          break;
        case 'SET_EDGE_INSPECTOR_COLLAPSED':
          setIsEdgeInspectorCollapsed(Boolean(act.collapsed));
          break;
        case 'SELECT_NODE': {
          const nid = act.node?.id ?? act.nodeId ?? null;
          setSelectedNodeId(nid);
          setSelectedEdgeId(null);
          if (nid && cyRef.current) {
            const cyNode = cyRef.current.getElementById(nid);
            cyRef.current.elements().unselect();
            cyNode.select();
          }
          break;
        }
        case 'SELECT_NODE_BY_ID': {
          const nid = act.id;
          setSelectedNodeId(nid);
          setSelectedEdgeId(null);
          if (nid && cyRef.current) {
            const cyNode = cyRef.current.getElementById(nid);
            cyRef.current.elements().unselect();
            cyNode.select();
            if (cyNode.nonempty()) {
              cyRef.current.animate({ center: { eles: cyNode }, duration: 250 });
            }
          }
          break;
        }
        case 'SELECT_EDGE': {
          setSelectedEdgeId(act.edge?.id ?? null);
          setSelectedNodeId(null);
          break;
        }
        case 'CLEAR_SELECTION':
          cyRef.current?.elements().unselect();
          cyRef.current?.elements().removeClass('highlighted dimmed');
          setSelectedNodeId(null);
          setSelectedEdgeId(null);
          ctx.onSelectNode?.(null);
          break;
        case 'CLEAR_GRAPH_HIGHLIGHT':
          cyRef.current?.elements().removeClass('highlighted dimmed');
          break;
        case 'HIDE_NODE':
          if (act.id) {
            setHiddenNodeIds((prev) => new Set(prev).add(act.id));
            if (selectedNodeId === act.id) setSelectedNodeId(null);
          }
          break;
        case 'UNHIDE_NODE':
          if (act.id) {
            setHiddenNodeIds((prev) => {
              const next = new Set(prev);
              next.delete(act.id);
              return next;
            });
          }
          break;
        case 'RESTORE_ALL_HIDDEN':
          setHiddenNodeIds(new Set());
          setHiddenTypes(new Set());
          setHiddenOrbitTiers(new Set());
          setHideIsolatedNodes(false);
          break;
        case 'TOGGLE_TYPE_VISIBILITY':
          if (act.kind) {
            setHiddenTypes((prev) => {
              const next = new Set(prev);
              if (next.has(act.kind)) next.delete(act.kind);
              else next.add(act.kind);
              return next;
            });
          }
          break;
        case 'TOGGLE_ISOLATED_NODES':
          setHideIsolatedNodes((prev) => !prev);
          break;
        case 'TOGGLE_TYPE_GROUPING':
          if (act.kind) ctx.toggleTypeGrouping(act.kind);
          break;
        case 'CREATE_CUSTOM_GROUP':
          if (act.name && Array.isArray(act.nodeIds)) ctx.createCustomGroup(act.name, act.nodeIds);
          break;
        case 'TOGGLE_CUSTOM_GROUP_COLLAPSE':
          if (act.groupId) ctx.toggleCustomGroupCollapse(act.groupId);
          break;
        case 'DELETE_CUSTOM_GROUP':
          if (act.groupId) ctx.deleteCustomGroup(act.groupId);
          break;
        case 'UNGROUP_NODE': {
          const node = act.node;
          if (!node) break;
          if (node.id.startsWith('group:custom:')) {
            const cId = node.customGroupId || node.id.replace('group:custom:', '');
            ctx.deleteCustomGroup(cId);
          } else if (node.id.startsWith('group:kind:') || node.groupKind) {
            const kind = (node.groupKind || node.id.replace('group:kind:', '')) as EntityKind;
            ctx.toggleTypeGrouping(kind);
          }
          break;
        }
        case 'REMOVE_FROM_GROUP':
          if (act.groupId && act.memberId) {
            ctx.removeNodeFromCustomGroup(act.groupId, act.memberId);
          }
          break;
        case 'APPLY_ORBIT_CONFIG':
          if (act.resetHidden) {
            setOrbitConfig(ctx.defaultOrbitConfig);
            setHiddenOrbitTiers(new Set());
            ctx.writeStored('orbitConfig', '');
          } else if (act.next) {
            setOrbitConfig(act.next);
            ctx.writeStored('orbitConfig', JSON.stringify(act.next));
            if (Array.isArray(act.clearHiddenFor) && act.clearHiddenFor.length > 0) {
              setHiddenOrbitTiers((prev) => {
                const copy = new Set(prev);
                for (const t of act.clearHiddenFor) copy.delete(Number(t));
                return copy;
              });
            }
          }
          break;
        case 'TOGGLE_ORBIT_VISIBILITY':
          if (typeof act.levelIndex === 'number') {
            setHiddenOrbitTiers((prev) => {
              const next = new Set(prev);
              if (next.has(act.levelIndex)) next.delete(act.levelIndex);
              else next.add(act.levelIndex);
              return next;
            });
          }
          break;
        case 'HIGHLIGHT_ORBIT_NODES':
          if (Array.isArray(act.nodeIds) && cyRef.current) {
            const cy = cyRef.current;
            const idSet = new Set<string>(act.nodeIds);
            cy.batch(() => {
              cy.elements().removeClass('highlighted').addClass('dimmed');
              cy.nodes().forEach((n) => {
                if (idSet.has(n.id())) {
                  n.removeClass('dimmed').addClass('highlighted');
                }
              });
            });
          }
          break;
        case 'CLEAR_ORBIT_HIGHLIGHT':
          cyRef.current?.elements().removeClass('highlighted dimmed');
          break;
        case 'SET_LAYOUT':
          if (act.layoutName) ctx.handleLayoutChange(act.layoutName);
          break;
        case 'SET_SPACING':
          if (typeof act.spacingFactor === 'number') ctx.handleSpacingChange(act.spacingFactor);
          break;
        case 'SET_ORBIT_DISTANCE':
          if (typeof act.orbitDistance === 'number') ctx.handleOrbitDistanceChange(act.orbitDistance);
          break;
        case 'SET_MIN_ORBIT_NODE_DISTANCE':
          if (typeof act.minOrbitNodeDistance === 'number')
            ctx.handleMinOrbitNodeDistanceChange(act.minOrbitNodeDistance);
          break;
        case 'SET_AUTO_RELAYOUT':
          setAutoRelayoutOnFilter(Boolean(act.enabled));
          ctx.writeStored('autoRelayout', String(Boolean(act.enabled)));
          break;
        case 'FORCE_RELAYOUT':
          setRelayoutCounter((c) => c + 1);
          break;
        case 'SET_NODE_CONTENT_MODE':
          if (act.mode) ctx.handleNodeContentModeChange(act.mode);
          break;
        case 'SET_NODE_SIZING_MODE':
          if (act.mode) ctx.handleNodeSizingModeChange(act.mode);
          break;
        case 'SET_EDGE_CURVE_MODE':
          if (act.mode) ctx.handleEdgeCurveModeChange(act.mode);
          break;
        case 'SET_EDGE_LABELS_ON_HOVER':
          ctx.handleEdgeLabelsOnHoverChange(Boolean(act.onHover));
          break;
        case 'SET_BASE_ICON_SIZE':
          if (typeof act.size === 'number') ctx.handleBaseIconSizeChange(act.size);
          break;
        case 'SET_FONT_SIZE':
          if (typeof act.fontSize === 'number') ctx.handleFontSizeChange(act.fontSize);
          break;
        case 'SET_CURVE_FACTOR':
          if (typeof act.curveFactor === 'number') ctx.handleCurveFactorChange(act.curveFactor);
          break;
        case 'SET_WHEEL_SENSITIVITY':
          if (typeof act.wheelSensitivity === 'number') ctx.handleWheelSensitivityChange(act.wheelSensitivity);
          break;
        case 'TOGGLE_CUSTOM_PRESET': {
          const preset = ctx.visibilityPresetsSlot?.find((p) => p.id === act.presetId);
          preset?.onToggle();
          break;
        }
        case 'TRIGGER_OVERVIEW_BANNER': {
          if (ctx.inspectorOverviewSlot?.actionBanner) {
            ctx.inspectorOverviewSlot.actionBanner.onAction();
          } else if (act.presetId) {
            ctx.visibilityPresetsSlot?.find((p) => p.id === act.presetId)?.onToggle();
          }
          break;
        }
        case 'OPEN_FILE':
          if (act.filePath) {
            ctx.onOpenFile?.(act.filePath, act.lineStart);
          }
          break;
      }
    };
    window.addEventListener('message', onMessage);
    return () => window.removeEventListener('message', onMessage);
  }, [selectedNodeId]);

  // Initialize Cytoscape canvas
  useEffect(() => {
    if (!containerRef.current) return;

    const cy = cytoscape({
      container: containerRef.current,
      boxSelectionEnabled: false,
      autounselectify: false,
      minZoom: 0.08,
      maxZoom: 4.0,
      style: CYTOSCAPE_STYLES,
    });

    cyRef.current = cy;

    const detachWheel = attachNormalizedCytoscapeWheel(
      containerRef.current,
      () => cyRef.current,
      () => wheelSensitivityRef.current
    );

    const updateTransform = () => {
      setCyTransform({ pan: { ...cy.pan() }, zoom: cy.zoom() });
    };

    cy.on('pan zoom', () => {
      updateTransform();
      actionContextRef.current.setContextMenu(null);
    });

    cy.on('mouseover', 'node', (evt: EventObject) => {
      evt.target.addClass('hovered');
    });
    cy.on('mouseout', 'node', (evt: EventObject) => {
      evt.target.removeClass('hovered');
    });
    cy.on('mouseover', 'edge', (evt: EventObject) => {
      evt.target.addClass('hovered');
    });
    cy.on('mouseout', 'edge', (evt: EventObject) => {
      evt.target.removeClass('hovered');
    });

    cy.on('tap', 'node', (evt: EventObject) => {
      actionContextRef.current.setContextMenu(null);
      const originalEvent = evt.originalEvent as MouseEvent | TouchEvent | undefined;
      const isShift = Boolean(originalEvent && 'shiftKey' in originalEvent && originalEvent.shiftKey);
      const node = evt.target;
      const nid = node.id();

      if (isShift) {
        actionContextRef.current.toggleMultiSelectedNode(nid);
        return;
      }

      if (multiSelectedNodeIdsRef.current.size > 0) {
        actionContextRef.current.clearMultiSelection();
      }

      setSelectedNodeId(nid);
      setSelectedEdgeId(null);
      setActiveSidebarTab('info');
      cy.batch(() => {
        cy.elements().removeClass('highlighted').addClass('dimmed');
        node.removeClass('dimmed').addClass('highlighted');
        node.connectedEdges().removeClass('dimmed').addClass('highlighted');
        node.neighborhood('node').removeClass('dimmed');
      });
      const rawNode = graph?.nodes?.find((n) => n.id === nid) ?? null;
      onSelectNode?.(rawNode);
    });

    cy.on('tap', 'edge', (evt: EventObject) => {
      actionContextRef.current.setContextMenu(null);
      const edge = evt.target;
      const eid = edge.id();
      setSelectedEdgeId(eid);
      setSelectedNodeId(null);
      setActiveSidebarTab('info');
      cy.batch(() => {
        cy.elements().removeClass('highlighted').addClass('dimmed');
        edge.removeClass('dimmed').addClass('highlighted');
        edge.source().removeClass('dimmed').addClass('highlighted');
        edge.target().removeClass('dimmed').addClass('highlighted');
      });
    });

    cy.on('tap', (evt: EventObject) => {
      if (evt.target === cy) {
        actionContextRef.current.setContextMenu(null);
        cy.elements().removeClass('highlighted dimmed');
        setSelectedNodeId(null);
        setSelectedEdgeId(null);
        actionContextRef.current.clearMultiSelection();
        onSelectNode?.(null);
      }
    });

    // Right-click / Context-tap -> Open context menu
    cy.on('cxttap', 'node', (evt: EventObject) => {
      const node = evt.target;
      const nid = node.id();
      const detail =
        actionContextRef.current.effectiveDetailMap?.get(nid) ||
        actionContextRef.current.parsedGraph.detailMap.get(nid);
      if (!detail) return;

      const renderedPos = evt.renderedPosition;
      actionContextRef.current.setContextMenu({
        x: renderedPos.x,
        y: renderedPos.y,
        nodeId: detail.id,
        nodeName: detail.displayName,
        nodeKind: detail.kind,
      });
    });

    cy.on('cxttap', (evt: EventObject) => {
      if (evt.target === cy) {
        actionContextRef.current.setContextMenu(null);
      }
    });

    cy.on('dbltap', 'node', (evt: EventObject) => {
      const nid = evt.target.id();
      const detail = actionContextRef.current.effectiveDetailMap?.get(nid) || actionContextRef.current.parsedGraph.detailMap.get(nid);
      if (detail?.primaryFilePath) {
        actionContextRef.current.onOpenFile?.(detail.primaryFilePath, detail.primaryLineStart);
        getVsCodeApi()?.postMessage({
          type: 'OPEN_FILE',
          filePath: detail.primaryFilePath,
          lineStart: detail.primaryLineStart,
        });
      }
    });

    return () => {
      detachWheel();
      cy.destroy();
      cyRef.current = null;
    };
  }, []);

  // Populate Cytoscape elements and compute layout
  useEffect(() => {
    const cy = cyRef.current;
    if (!cy || layoutName === 'mermaid') return;

    cy.batch(() => {
      cy.elements().remove();
      if (visibleNodes.length > 0) {
        cy.add([...visibleNodes, ...visibleEdges]);
      }
      cy.nodes().style('font-size', `${fontSize}px`);
    });

    applyEdgeLabelVisibility(cy, edgeLabelsOnHover);

    if (visibleNodes.length === 0) {
      setConcentricGuides([]);
      setSwimlaneGuides([]);
      setOrbitLegendItems([]);
      return;
    }

    const visibleNodesInput = visibleNodes.map((n) => ({
      id: n.data.id as string,
      echelonTier: Number((n.data as any).echelonTier ?? 2),
      kind: (n.data as any).kind as string,
      name: (n.data as any).name as string,
      displayName: (n.data as any).displayLabel as string,
    }));

    const visibleEdgesInput = visibleEdges.map((e) => ({
      source: (e.data as any).source as string,
      target: (e.data as any).target as string,
      category: (e.data as any).category as string,
      count: Number((e.data as any).count ?? 1),
    }));

    const effectiveOrder = orbitOrder(orbitConfig);

    if (layoutName.startsWith('concentric')) {
      setSwimlaneGuides([]);
      let layoutResult: ConcentricLayoutResult;
      if (layoutName === 'concentric-equispaced') {
        layoutResult = computeConcentricEquispacedLayout(
          visibleNodesInput,
          visibleEdgesInput,
          spacingFactor,
          effectiveOrder,
          orbitDistance,
          minOrbitNodeDistance,
          customTitlesByTier
        );
      } else if (layoutName === 'concentric-barycentric') {
        layoutResult = computeConcentricBarycentricLayout(
          visibleNodesInput,
          visibleEdgesInput,
          spacingFactor,
          effectiveOrder,
          orbitDistance,
          minOrbitNodeDistance,
          customTitlesByTier
        );
      } else if (layoutName === 'concentric-polar-force') {
        layoutResult = computeConcentricPolarForceLayout(
          visibleNodesInput,
          visibleEdgesInput,
          spacingFactor,
          effectiveOrder,
          orbitDistance,
          minOrbitNodeDistance,
          customTitlesByTier
        );
      } else {
        layoutResult = computeConcentricLayout(
          visibleNodesInput,
          visibleEdgesInput,
          spacingFactor,
          effectiveOrder,
          orbitDistance,
          minOrbitNodeDistance,
          customTitlesByTier
        );
      }

      // Relabel merged orbits
      for (const orbit of layoutResult.populatedOrbits) {
        const entry = orbitConfig.orbits.find((o) => o[0] === orbit.levelIndex);
        if (entry && entry.length > 1) {
          const mergedTitle = orbitTitle(entry, customTitlesByTier);
          orbit.title = mergedTitle;
          orbit.label = `${orbit.shortLabel}: ${mergedTitle}`;
        }
      }
      for (const guide of layoutResult.guides) {
        const entry = orbitConfig.orbits.find((o) => o[0] === guide.levelIndex);
        if (entry && entry.length > 1) {
          const mergedTitle = orbitTitle(entry, customTitlesByTier);
          guide.title = mergedTitle;
          guide.label = `${guide.shortLabel}: ${mergedTitle}`;
        }
      }

      setConcentricGuides(layoutResult.guides);
      setOrbitLegendItems(
        buildAllOrbitLegendItems(
          parsedGraph.allCyNodes,
          parsedGraph.echelonMap,
          layoutResult.populatedOrbits,
          orbitConfig,
          customTitlesByTier,
          'Orbit',
          customTitlesByTier
        )
      );

      cy.layout({
        name: 'preset',
        positions: (node: any) => layoutResult.positions.get(node.id()) || { x: 0, y: 0 },
        fit: true,
        padding: 60,
        animate: false,
      }).run();
    } else if (layoutName === 'swimlanes') {
      setConcentricGuides([]);
      const res = computeSwimlanesLayout(
        visibleNodesInput,
        visibleEdgesInput,
        spacingFactor,
        tierDefinitions,
        effectiveOrder,
        baseIconSize
      );
      setSwimlaneGuides(res.lanes);
      setOrbitLegendItems(
        buildAllOrbitLegendItems(
          parsedGraph.allCyNodes,
          parsedGraph.echelonMap,
          res.populatedLanes.map((l) => ({
            levelIndex: l.levelIndex,
            radius: 0,
            shortLabel: `Lane ${l.levelIndex}`,
            title: l.title,
            nodeIds: l.nodeIds,
          })),
          orbitConfig,
          customTitlesByTier,
          'Lane',
          customTitlesByTier
        )
      );

      cy.layout({
        name: 'preset',
        positions: (node: any) => res.positions.get(node.id()) || { x: 0, y: 0 },
        fit: true,
        padding: 60,
        animate: false,
      }).run();
    } else if (layoutName === 'dagre') {
      setConcentricGuides([]);
      setSwimlaneGuides([]);
      cy.layout({
        name: 'dagre',
        rankDir: 'LR',
        nodeSep: Math.round(60 * spacingFactor),
        rankSep: Math.round(120 * spacingFactor),
        padding: 50,
        animate: false,
      } as any).run();
    } else {
      setConcentricGuides([]);
      setSwimlaneGuides([]);
      cy.layout({
        name: 'cose',
        animate: false,
        padding: 50,
      }).run();
    }

    applyEdgeCurveMode(cy, edgeCurveMode, { x: 0, y: 0 }, curveFactor);
    setCyTransform({ pan: { ...cy.pan() }, zoom: cy.zoom() });
  }, [
    visibleNodes,
    visibleEdges,
    layoutName,
    spacingFactor,
    orbitDistance,
    minOrbitNodeDistance,
    orbitConfig,
    tierDefinitions,
    customTitlesByTier,
    baseIconSize,
    fontSize,
    edgeCurveMode,
    curveFactor,
    edgeLabelsOnHover,
    parsedGraph,
    relayoutCounter,
  ]);

  // Search highlighting
  const searchMatchCount = useMemo(() => {
    const q = searchQuery.trim();
    if (!q) return null;
    let count = 0;
    for (const n of visibleNodes) {
      const id = n.data.id as string;
      const detail = parsedGraph.detailMap.get(id);
      if (
        matchesSearchTokens(
          [detail?.displayName, detail?.name, id, detail?.kind, detail?.primaryFilePath],
          q
        )
      ) {
        count++;
      }
    }
    return count;
  }, [searchQuery, visibleNodes, parsedGraph.detailMap]);

  useEffect(() => {
    const cy = cyRef.current;
    if (!cy) return;

    const q = searchQuery.trim();
    if (!q) {
      cy.elements().removeClass('search-match dimmed');
      return;
    }

    cy.batch(() => {
      cy.elements().removeClass('search-match').addClass('dimmed');
      cy.nodes().forEach((n) => {
        const id = n.id();
        const detail = parsedGraph.detailMap.get(id);
        if (
          matchesSearchTokens(
            [detail?.displayName, detail?.name, id, detail?.kind, detail?.primaryFilePath],
            q
          )
        ) {
          n.removeClass('dimmed').addClass('search-match');
          n.connectedEdges().removeClass('dimmed');
        }
      });
    });
  }, [searchQuery, parsedGraph.detailMap]);

  const handleZoomIn = () => {
    const cy = cyRef.current;
    if (!cy) return;
    cy.zoom({
      level: Math.min(cy.maxZoom(), cy.zoom() * 1.25),
      renderedPosition: { x: cy.width() / 2, y: cy.height() / 2 },
    });
  };

  const handleZoomOut = () => {
    const cy = cyRef.current;
    if (!cy) return;
    cy.zoom({
      level: Math.max(cy.minZoom(), cy.zoom() / 1.25),
      renderedPosition: { x: cy.width() / 2, y: cy.height() / 2 },
    });
  };

  const handleResetZoom = () => {
    const cy = cyRef.current;
    if (!cy) return;
    cy.zoom({
      level: 1.0,
      renderedPosition: { x: cy.width() / 2, y: cy.height() / 2 },
    });
  };

  const handleFitView = () => {
    cyRef.current?.fit(undefined, 50);
  };

  return (
    <div
      className="domain-architecture-container"
      style={{ display: 'flex', flexDirection: 'column', width: '100%', height: '100%', position: 'relative', overflow: 'hidden' }}
    >
      {/* Loading Overlay */}
      {loading && (
        <div className="view-loading-overlay">
          <div className="view-loading-card">
            <div className="view-loading-spinner" />
            <div className="view-loading-content">
              <span className="view-loading-title">{hudTitle}</span>
              <span className="view-loading-subtitle">Loading graph data…</span>
            </div>
          </div>
        </div>
      )}

      {/* Docked Top Studio Bar (Identical to Architecture Map) */}
      <header className="domain-map-hud">
        <div className="domain-hud-brand">
          <span className="domain-hud-label">{hudLabel}</span>
          <span className="domain-hud-title">{hudTitle}</span>
        </div>

        <div className="domain-hud-content">
          <div className="domain-hud-row domain-hud-primary-row">
            <div className="domain-hud-controls-left">
              {headerControlsSlot}

              <div className="domain-hud-layout-select" title="Graph Layout Algorithm">
                <select
                  value={layoutName}
                  onChange={(e) => handleLayoutChange(e.target.value as DomainLayoutName)}
                  className="domain-layout-dropdown"
                  title="Graph Layout"
                >
                  {allowedLayouts.map((opt) => (
                    <option key={opt.id} value={opt.id}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              </div>

              <div className="domain-hud-control-group" title="Viewport Controls">
                <button type="button" className="domain-hud-btn" onClick={handleZoomOut} title="Zoom Out">
                  −
                </button>
                <button
                  type="button"
                  className="domain-hud-btn zoom-level-btn"
                  onClick={handleResetZoom}
                  title="Reset Zoom to 100%"
                >
                  {Math.round(cyTransform.zoom * 100)}%
                </button>
                <button type="button" className="domain-hud-btn" onClick={handleZoomIn} title="Zoom In">
                  +
                </button>
                <button type="button" className="domain-hud-btn fit-btn" onClick={handleFitView} title="Fit to Screen">
                  {'⛶\u00A0Fit'}
                </button>
                {onRefresh && (
                  <button
                    type="button"
                    className="domain-hud-btn refresh-btn"
                    onClick={onRefresh}
                    title="Reload Graph"
                  >
                    {'🔄\u00A0Refresh'}
                  </button>
                )}
              </div>
            </div>

            <div className="domain-hud-controls-right">
              <div className="domain-hud-search">
                <input
                  type="text"
                  className="domain-search-input"
                  placeholder="Search nodes..."
                  value={searchQuery}
                  style={{
                    paddingRight:
                      searchMatchCount !== null && searchQuery
                        ? 54
                        : searchMatchCount !== null || searchQuery
                        ? 28
                        : 8,
                  }}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') {
                      const matches = cyRef.current?.nodes('.search-match');
                      if (matches && matches.length > 0) {
                        cyRef.current?.animate({ fit: { eles: matches, padding: 80 }, duration: 350 });
                      }
                    }
                  }}
                />
                {searchMatchCount !== null && (
                  <span
                    className={`domain-search-count ${searchMatchCount === 0 ? 'is-zero' : ''}`}
                    title={
                      searchMatchCount === 0
                        ? 'No matches found'
                        : `${searchMatchCount} matching entities (Press Enter to focus)`
                    }
                    onClick={() => {
                      const matches = cyRef.current?.nodes('.search-match');
                      if (matches && matches.length > 0) {
                        cyRef.current?.animate({ fit: { eles: matches, padding: 80 }, duration: 350 });
                      }
                    }}
                  >
                    {searchMatchCount}
                  </span>
                )}
                {searchQuery && (
                  <button
                    type="button"
                    className="domain-search-clear"
                    onClick={() => setSearchQuery('')}
                    title="Clear search"
                  >
                    ✕
                  </button>
                )}
              </div>

              <button
                type="button"
                className="domain-hud-btn sidebar-toggle-btn"
                onClick={() => {
                  getVsCodeApi()?.postMessage({ type: 'FOCUS_INSPECTOR_VIEW', preserveFocus: false });
                  try {
                    getVsCodeApi()?.postMessage({
                      type: 'SYNC_INSPECTOR_STATE',
                      state: buildInspectorSyncedState(),
                    });
                  } catch {
                    // ignored: transient postMessage error
                  }
                }}
                title="Open Graph Inspector & Controls in Secondary Side Bar (Info, Orbits, Visibility, Settings)"
              >
                <span className="btn-icon">🗂️</span>
                <span className="btn-text">Inspector</span>
                {hiddenCount > 0 && (
                  <span
                    style={{
                      fontSize: '9.5px',
                      marginLeft: 4,
                      background: 'rgba(239, 68, 68, 0.35)',
                      padding: '1px 5px',
                      borderRadius: '10px',
                    }}
                  >
                    {hiddenCount}
                  </span>
                )}
              </button>
            </div>
          </div>
        </div>
      </header>

      {/* Main Canvas Area */}
      <div
        className="domain-cytoscape-wrapper"
        style={{
          display: 'flex',
          flexDirection: 'column',
          width: '100%',
          height: '100%',
          flex: 1,
          position: 'relative',
          overflow: 'hidden',
        }}
      >
        {layoutName === 'mermaid' ? (
          <DomainMermaidView
            nodes={visibleNodes.map((n) => effectiveDetailMap.get(n.data.id as string)!).filter(Boolean)}
            edges={visibleEdges.map((e) => ({
              source: (e.data as any).source,
              target: (e.data as any).target,
              category: (e.data as any).category,
              count: (e.data as any).count || 1,
              label: (e.data as any).label,
            }))}
            selectedNodeId={selectedNodeId ?? undefined}
            onSelectNode={(nodeId) => {
              setSelectedNodeId(nodeId);
              setSelectedEdgeId(null);
            }}
            title={hudTitle}
          />
        ) : (
          <>
            {((layoutName.startsWith('concentric') && concentricGuides.length > 0) ||
              (layoutName === 'swimlanes' && swimlaneGuides.length > 0)) && (
              <svg
                className="domain-layout-overlay"
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
                  {layoutName.startsWith('concentric') &&
                    concentricGuides.map((g, idx) => {
                      if (hiddenOrbitTiers.has(g.levelIndex)) return null;
                      const zoomFactor = Math.max(0.75, Math.min(1.35, 1 / cyTransform.zoom));
                      const strokeW = Math.max(1, Math.min(3, 1.5 / cyTransform.zoom));
                      const dashPattern = `${Math.round(8 / cyTransform.zoom)} ${Math.round(6 / cyTransform.zoom)}`;
                      const guideFontSize = Math.min(14, Math.max(9, 11 * zoomFactor));
                      const shortLabel = g.shortLabel || (g.label.includes(':') ? g.label.split(':')[0] : g.label);
                      const labelText = `${shortLabel}: ${g.title} (${g.count})`;
                      const textWidth = labelText.length * guideFontSize * 0.58;
                      const hPad = Math.max(10, Math.min(18, 14 * zoomFactor));
                      const badgeWidth = textWidth + hPad * 2;
                      const badgeHeight = Math.max(18, Math.min(28, 22 * zoomFactor));
                      const badgeY = -g.radius - badgeHeight - Math.min(10, 6 * zoomFactor);

                      return (
                        <g key={idx}>
                          <circle
                            cx={0}
                            cy={0}
                            r={g.radius}
                            fill="none"
                            stroke="rgba(56, 189, 248, 0.22)"
                            strokeWidth={strokeW}
                            strokeDasharray={dashPattern}
                          />
                          <rect
                            x={-badgeWidth / 2}
                            y={badgeY}
                            width={badgeWidth}
                            height={badgeHeight}
                            rx={Math.min(6, 4 * zoomFactor)}
                            fill="rgba(15, 23, 42, 0.88)"
                            stroke="rgba(56, 189, 248, 0.45)"
                            strokeWidth={strokeW}
                          />
                          <text
                            x={0}
                            y={badgeY + badgeHeight / 2}
                            fill="#38bdf8"
                            fontSize={`${guideFontSize.toFixed(1)}px`}
                            fontWeight="700"
                            textAnchor="middle"
                            dominantBaseline="central"
                            textLength={badgeWidth - 8}
                            lengthAdjust="spacingAndGlyphs"
                          >
                            <title>{labelText}</title>
                            {labelText}
                          </text>
                        </g>
                      );
                    })}

                  {layoutName === 'swimlanes' &&
                    swimlaneGuides.map((g) => {
                      const strokeW = Math.max(1, Math.min(3, 1.5 / cyTransform.zoom));
                      const dashPattern = `${Math.round(6 / cyTransform.zoom)} ${Math.round(4 / cyTransform.zoom)}`;
                      const zoomFactor = Math.max(0.75, Math.min(1.35, 1 / cyTransform.zoom));
                      const headerHeight = Math.max(24, Math.min(36, 28 * zoomFactor));
                      const hPad = Math.max(6, Math.min(12, 8 * zoomFactor));
                      const boxWidth = Math.max(40, g.width - hPad * 2);
                      const maxTextWidth = Math.max(20, boxWidth - 14);

                      const fullLabel = `${g.icon ? g.icon + ' ' : ''}${g.title} (${g.count})`;
                      const estCharWidth = 0.56;
                      const preferredFontSize = 11.5 * zoomFactor;
                      const maxFittingFontSize = maxTextWidth / (fullLabel.length * estCharWidth);
                      const finalFontSize = Math.min(preferredFontSize, Math.max(8, maxFittingFontSize));

                      let displayLabel = fullLabel;
                      if (fullLabel.length * 8 * estCharWidth > maxTextWidth) {
                        const iconPart = g.icon ? g.icon + ' ' : '';
                        const countPart = ` (${g.count})`;
                        const fixedLen = iconPart.length + countPart.length + 3;
                        const availableTitleChars = Math.max(3, Math.floor(maxTextWidth / (8 * estCharWidth) - fixedLen));
                        const truncatedTitle =
                          g.title.length > availableTitleChars
                            ? g.title.substring(0, availableTitleChars).trim() + '...'
                            : g.title;
                        displayLabel = `${iconPart}${truncatedTitle}${countPart}`;
                      }

                      const isSqueezed = fullLabel.length * finalFontSize * estCharWidth > maxTextWidth;

                      return (
                        <g key={g.id}>
                          <rect
                            x={g.x}
                            y={g.y}
                            width={g.width}
                            height={g.height}
                            rx={Math.min(12, 8 * zoomFactor)}
                            fill="rgba(15, 23, 42, 0.45)"
                            stroke={`${g.color}35`}
                            strokeWidth={strokeW}
                            strokeDasharray={dashPattern}
                          />
                          <rect
                            x={g.x + hPad}
                            y={g.y + hPad}
                            width={boxWidth}
                            height={headerHeight}
                            rx={Math.min(6, 4 * zoomFactor)}
                            fill="rgba(15, 23, 42, 0.92)"
                            stroke={`${g.color}70`}
                            strokeWidth={strokeW}
                          />
                          <text
                            x={g.x + g.width / 2}
                            y={g.y + hPad + headerHeight / 2}
                            fill={g.color}
                            fontSize={`${finalFontSize.toFixed(1)}px`}
                            fontWeight="700"
                            textAnchor="middle"
                            dominantBaseline="central"
                            textLength={isSqueezed ? maxTextWidth : undefined}
                            lengthAdjust={isSqueezed ? 'spacingAndGlyphs' : undefined}
                          >
                            <title>{fullLabel}</title>
                            {displayLabel}
                          </text>
                        </g>
                      );
                    })}
                </g>
              </svg>
            )}
          </>
        )}
        <div
          ref={containerRef}
          className="domain-cytoscape-container"
          style={{ display: layoutName === 'mermaid' ? 'none' : 'block' }}
        />

        {/* Floating Multi-Select Grouping Bar */}
        {multiSelectedNodeIds.size > 1 && (
          <div
            className="floating-multi-select-bar"
            style={{
              position: 'absolute',
              bottom: 24,
              left: '50%',
              transform: 'translateX(-50%)',
              background: 'rgba(15, 23, 42, 0.95)',
              border: '1px solid #0284c7',
              boxShadow: '0 8px 24px rgba(0,0,0,0.6)',
              borderRadius: 8,
              padding: '8px 14px',
              display: 'flex',
              alignItems: 'center',
              gap: 10,
              zIndex: 1000,
              backdropFilter: 'blur(8px)',
            }}
          >
            <span style={{ fontSize: 12, fontWeight: 600, color: '#38bdf8' }}>
              {multiSelectedNodeIds.size} Selected
            </span>
            <input
              type="text"
              placeholder={`Group (${multiSelectedNodeIds.size})`}
              value={floatingGroupName}
              onChange={(e) => setFloatingGroupName(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  createCustomGroup(
                    floatingGroupName.trim() || `Group (${multiSelectedNodeIds.size})`,
                    Array.from(multiSelectedNodeIds)
                  );
                  setFloatingGroupName('');
                }
              }}
              style={{
                height: 24,
                background: '#1e293b',
                border: '1px solid #334155',
                borderRadius: 4,
                padding: '0 8px',
                fontSize: 11,
                color: '#f8fafc',
                width: 140,
              }}
            />
            <button
              type="button"
              onClick={() => {
                createCustomGroup(
                  floatingGroupName.trim() || `Group (${multiSelectedNodeIds.size})`,
                  Array.from(multiSelectedNodeIds)
                );
                setFloatingGroupName('');
              }}
              style={{
                background: '#0284c7',
                color: '#fff',
                border: 'none',
                borderRadius: 4,
                padding: '4px 10px',
                fontSize: 11,
                fontWeight: 600,
                cursor: 'pointer',
              }}
            >
              Group
            </button>
            <button
              type="button"
              onClick={clearMultiSelection}
              style={{
                background: 'transparent',
                color: '#94a3b8',
                border: '1px solid #334155',
                borderRadius: 4,
                padding: '4px 8px',
                fontSize: 11,
                cursor: 'pointer',
              }}
            >
              Cancel
            </button>
          </div>
        )}

        {/* Context Menu for Nodes */}
        {contextMenu && (() => {
          const isGroupNode = contextMenu.nodeId.startsWith('group:');
          const targetNodeIds =
            multiSelectedNodeIds.has(contextMenu.nodeId) && multiSelectedNodeIds.size > 1
              ? Array.from(multiSelectedNodeIds)
              : [contextMenu.nodeId];
          const downstreamNodeIds = isGroupNode ? [] : getDownstreamNodeIds(contextMenu.nodeId);
          const isSubmenuLeft = typeof window !== 'undefined' && contextMenu.x + 360 > window.innerWidth;

          return (
            <div
              className="domain-node-context-menu"
              style={{
                position: 'absolute',
                left: `${contextMenu.x}px`,
                top: `${contextMenu.y}px`,
                zIndex: 1000,
              }}
              onClick={(e) => e.stopPropagation()}
            >
              <div className="context-menu-header">
                <span className="context-menu-title" title={contextMenu.nodeName}>
                  {contextMenu.nodeName}
                </span>
                <span className="context-menu-kind">{contextMenu.nodeKind}</span>
              </div>
              <div className="context-menu-divider" />
              {multiSelectedNodeIds.size > 1 && (
                <button
                  type="button"
                  className="context-menu-item"
                  onClick={() => {
                    createCustomGroup(`Group (${multiSelectedNodeIds.size})`, Array.from(multiSelectedNodeIds));
                    setContextMenu(null);
                  }}
                >
                  <span>Group {multiSelectedNodeIds.size} Selected</span>
                </button>
              )}
              {!isGroupNode && (
                <>
                  <div
                    className="context-menu-submenu-container"
                    onMouseEnter={() => setIsAddGroupSubmenuOpen(true)}
                    onMouseLeave={() => setIsAddGroupSubmenuOpen(false)}
                  >
                    <button
                      type="button"
                      className="context-menu-item context-menu-submenu-trigger"
                      onClick={(e) => {
                        e.stopPropagation();
                        setIsAddGroupSubmenuOpen((prev) => !prev);
                      }}
                    >
                      <span>Add to group</span>
                      <span className="submenu-arrow">▶</span>
                    </button>
                    {isAddGroupSubmenuOpen && (
                      <div className={`context-menu-submenu ${isSubmenuLeft ? 'submenu-left' : ''}`}>
                        {customGroups.map((group) => {
                          const isMember = targetNodeIds.every((id) => group.nodeIds.includes(id));
                          return (
                            <button
                              key={group.id}
                              type="button"
                              className="context-menu-item"
                              onClick={() => {
                                addNodesToCustomGroup(group.id, targetNodeIds);
                                setContextMenu(null);
                              }}
                            >
                              <span className="context-menu-submenu-item-name" title={group.name}>
                                {isMember ? '✓ ' : ''}{group.name}
                              </span>
                              <span className="context-menu-badge">{group.nodeIds.length}</span>
                            </button>
                          );
                        })}
                        {customGroups.length > 0 && <div className="context-menu-divider" />}
                        <button
                          type="button"
                          className="context-menu-item"
                          onClick={() => {
                            const defaultName =
                              targetNodeIds.length > 1
                                ? `Group (${targetNodeIds.length})`
                                : `Group of ${contextMenu.nodeName}`;
                            createCustomGroup(defaultName, targetNodeIds);
                            setContextMenu(null);
                          }}
                        >
                          <span>+ New Group...</span>
                        </button>
                      </div>
                    )}
                  </div>
                  <button
                    type="button"
                    className="context-menu-item"
                    disabled={downstreamNodeIds.length === 0}
                    onClick={() => {
                      createCustomGroup(`Downstream of ${contextMenu.nodeName}`, downstreamNodeIds);
                      setContextMenu(null);
                    }}
                  >
                    <span>
                      Group all downstream {downstreamNodeIds.length > 0 ? `(${downstreamNodeIds.length})` : ''}
                    </span>
                  </button>
                </>
              )}
              {isGroupNode && (
                <button
                  type="button"
                  className="context-menu-item"
                  onClick={() => {
                    const gId = contextMenu.nodeId;
                    if (gId.startsWith('group:custom:')) {
                      deleteCustomGroup(gId.replace('group:custom:', ''));
                    } else if (gId.startsWith('group:kind:')) {
                      toggleTypeGrouping(gId.replace('group:kind:', '') as EntityKind);
                    }
                    setContextMenu(null);
                    setSelectedNodeId(null);
                  }}
                >
                  <span>Ungroup / Expand</span>
                </button>
              )}
              <div className="context-menu-divider" />
              <button
                type="button"
                className="context-menu-item"
                onClick={() => hideNode(contextMenu.nodeId)}
              >
                <span>Hide Node</span>
              </button>
              <button
                type="button"
                className="context-menu-item"
                disabled={downstreamNodeIds.length === 0}
                onClick={() => hideDownstreamNodes(contextMenu.nodeId)}
              >
                <span>Hide Downstream</span>
              </button>
              <button
                type="button"
                className="context-menu-item"
                onClick={() => hideAllOfKind(contextMenu.nodeKind)}
              >
                <span>Hide all {contextMenu.nodeKind}s</span>
              </button>
            </div>
          );
        })()}
      </div>

      {/* Error Banner */}
      {error && !loading && (
        <div
          style={{
            position: 'absolute',
            top: 56,
            left: 16,
            right: 16,
            padding: '8px 12px',
            background: 'rgba(239, 68, 68, 0.85)',
            color: '#fff',
            borderRadius: 4,
            zIndex: 90,
            fontSize: 12,
          }}
        >
          {error}
        </div>
      )}
    </div>
  );
};
