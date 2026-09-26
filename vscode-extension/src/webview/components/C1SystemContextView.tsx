import React, { useState, useEffect, useMemo, useCallback, useRef } from 'react';
import dagre from 'dagre';
import cytoscape from 'cytoscape';
import cytoscapeDagre from 'cytoscape-dagre';
import mermaid from 'mermaid';
import {
  ReactFlow,
  ReactFlowProvider,
  Background,
  Controls,
  MiniMap,
  useNodesState,
  useEdgesState,
  useReactFlow,
  Node,
  Edge,
  MarkerType,
} from '@xyflow/react';
import { GraphData, GraphNode, isProjectKind } from '../../../../proto/types';
import {
  C1LaconicCardNode,
  C1AppCardNode,
  C1ServiceCardNode,
  C1ExternalCardNode,
  C1SwimlaneCardNode,
  C1NodeData,
  C1SwimlaneData,
} from './C1CardNodes';
import { C1FocusEgoView } from './C1FocusEgoView';
import { C1ChordWheelView } from './C1ChordWheelView';
import { C1MatrixView } from './C1MatrixView';

try {
  cytoscape.use(cytoscapeDagre);
} catch {}

export interface C1SystemContextViewProps {
  graph: GraphData | null;
  serverHttpUrl?: string;
  onDrillDownToC2?: (projectName: string) => void;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
  onSelectNode?: (node: GraphNode | null) => void;
  selectedNodeId?: string;
}

export type C1Variant =
  | 'playground' // 1. Laconic Minimalist Shapes + Live Tuner
  | 'swimlanes' // 2. 3-Column Swimlanes (Apps | Services | External)
  | 'focus' // 3. Focus / Ego-Network (1 Service + Neighbors)
  | 'chord' // 4. Chord Wheel (Interactive Circular Ribbons)
  | 'matrix' // 5. Dependency Structure Matrix (DSM)
  | 'concentric' // 6. Radial Concentric Rings (Ingress -> Core -> Ext)
  | 'clusters' // 7. Clustered Bounded Contexts
  | 'dagre-lr' // 8. Dagre Hierarchical (L -> R)
  | 'dagre-tb' // 9. Dagre Hierarchical (Top -> Down)
  | 'cytoscape' // 10. Force-Directed Physics (Cytoscape COSE)
  | 'mermaid'; // 11. Mermaid Flowchart

export type C1Category = 'app' | 'service' | 'external';

export interface ClassifiedC1Node {
  id: string;
  name: string;
  displayName: string;
  category: C1Category;
  kind: string;
  framework?: string;
  language?: string;
  filePath?: string;
  lineStart?: number;
  rawNode: GraphNode;
}

export interface ClassifiedC1Edge {
  source: string;
  target: string;
  count: number;
  label: string;
}

export interface C1PlaygroundSettings {
  rankSep: number; // Horizontal / Rank gap (40 - 320px)
  nodeSep: number; // Vertical / Node gap (15 - 150px)
  nodeWidth: number; // Card width (180 - 360px)
  fontSize: number; // Title font size (12 - 22px)
  curveType: 'smoothstep' | 'bezier' | 'straight' | 'step';
  edgeStyle: 'solid' | 'dashed' | 'animated';
  strokeWidth: number; // Line thickness (1 - 5px)
  showLabels: boolean; // Show "CALLS (N)" labels
  showArrows: boolean; // Show directional arrowheads
  cytoscapeAir: number; // Node repulsion / spacing factor (0.5 - 3.0x)
}

const DEFAULT_SETTINGS: C1PlaygroundSettings = {
  rankSep: 110,
  nodeSep: 35,
  nodeWidth: 240,
  fontSize: 15,
  curveType: 'smoothstep',
  edgeStyle: 'solid',
  strokeWidth: 2,
  showLabels: true,
  showArrows: true,
  cytoscapeAir: 1.2,
};

const nodeTypes = {
  c1Laconic: C1LaconicCardNode as any,
  c1App: C1AppCardNode as any,
  c1Service: C1ServiceCardNode as any,
  c1External: C1ExternalCardNode as any,
  c1Swimlane: C1SwimlaneCardNode as any,
};

// ============================================================================
// Node Classifier: Strictly Apps, Services, External (No Libs, Not DBs)
// ============================================================================
function classifyNode(node: GraphNode): C1Category | null {
  const kind = (node.kind || '').toLowerCase();
  const role = (node.properties?.role || '').toLowerCase();
  const isLib =
    node.properties?.is_library === 'true' ||
    (node.properties as any)?.is_library === true ||
    role === 'sharedlibrary';

  // 1. Strictly exclude: Libraries, Databases, Topics/Queues, Endpoints, AST Types
  if (kind === 'library' || kind === 'sharedlibrary' || isLib) return null;
  if (kind === 'database' || kind === 'table' || kind === 'column' || kind === 'query') return null;
  if (kind === 'topic' || kind === 'queue' || kind === 'broker') return null;
  if (kind === 'endpoint' || kind === 'entrypoint' || kind === 'type' || kind === 'function') return null;
  if (kind === 'workspace' || kind === 'package' || role === 'test' || kind === 'test') return null;

  // 2. External Services
  if (kind === 'externalservice' || kind === 'cloudservice' || role === 'externalservice') {
    return 'external';
  }

  // 3. Apps & Frontends (Ingress tier)
  if (
    kind === 'app' ||
    kind === 'frontendapp' ||
    kind === 'ingress' ||
    role === 'app' ||
    role === 'frontendapp' ||
    role === 'ingress'
  ) {
    return 'app';
  }

  const lowerName = (node.name || '').toLowerCase();
  if (
    lowerName.endsWith('-front') ||
    lowerName.endsWith('-fe') ||
    lowerName.includes('frontend') ||
    lowerName.includes('landing') ||
    lowerName.endsWith('-bff') ||
    lowerName.endsWith('gateway')
  ) {
    return 'app';
  }

  // 4. Services & Workers (Core domain microservices)
  if (
    kind === 'service' ||
    kind === 'worker' ||
    kind === 'clitool' ||
    role === 'service' ||
    role === 'worker' ||
    role === 'clitool'
  ) {
    return 'service';
  }

  if (isProjectKind(node.kind)) {
    return 'service';
  }

  return null;
}

// ============================================================================
// Inner ReactFlow Canvas
// ============================================================================
function C1ReactFlowCanvas({
  nodes,
  edges,
  onNodesChange,
  onEdgesChange,
  onFitViewRef,
  onZoomInRef,
  onZoomOutRef,
  currentZoom,
  setCurrentZoom,
}: {
  nodes: Node[];
  edges: Edge[];
  onNodesChange: any;
  onEdgesChange: any;
  onFitViewRef: React.MutableRefObject<(() => void) | null>;
  onZoomInRef: React.MutableRefObject<(() => void) | null>;
  onZoomOutRef: React.MutableRefObject<(() => void) | null>;
  currentZoom: number;
  setCurrentZoom: (z: number) => void;
}) {
  const { fitView, zoomIn, zoomOut, getZoom } = useReactFlow();

  useEffect(() => {
    onFitViewRef.current = () => fitView({ padding: 0.15, duration: 300 });
    onZoomInRef.current = () => zoomIn({ duration: 200 });
    onZoomOutRef.current = () => zoomOut({ duration: 200 });
  }, [fitView, zoomIn, zoomOut, onFitViewRef, onZoomInRef, onZoomOutRef]);

  useEffect(() => {
    if (nodes.length > 0) {
      const timer = setTimeout(() => {
        fitView({ padding: 0.15, duration: 300 });
        setCurrentZoom(getZoom());
      }, 80);
      return () => clearTimeout(timer);
    }
    return undefined;
  }, [nodes.length, fitView, getZoom, setCurrentZoom]);

  const handleMoveEnd = useCallback(
    (_: any, viewport: { zoom: number }) => {
      setCurrentZoom(viewport.zoom);
    },
    [setCurrentZoom]
  );

  return (
    <ReactFlow
      nodes={nodes}
      edges={edges}
      onNodesChange={onNodesChange}
      onEdgesChange={onEdgesChange}
      onMoveEnd={handleMoveEnd}
      nodeTypes={nodeTypes}
      fitView
      minZoom={0.15}
      maxZoom={2.8}
    >
      <Background color="rgba(255, 255, 255, 0.04)" gap={24} size={1.5} />
      <Controls showInteractive={false} position="bottom-right" />
      <MiniMap
        nodeStrokeColor="#38bdf8"
        nodeColor={(n) => {
          if (n.type === 'c1Swimlane') return 'rgba(255, 255, 255, 0.03)';
          const cat = (n.data as any)?.category;
          if (cat === 'app') return '#0288d1';
          if (cat === 'service') return '#ef4444';
          return '#10b981';
        }}
        maskColor="rgba(8, 12, 20, 0.75)"
        style={{
          background: 'rgba(20, 20, 26, 0.9)',
          border: '1px solid rgba(255, 255, 255, 0.12)',
          borderRadius: 6,
        }}
        position="bottom-left"
      />
    </ReactFlow>
  );
}

// ============================================================================
// Main C1 System Context Component
// ============================================================================
export const C1SystemContextView: React.FC<C1SystemContextViewProps> = ({
  graph,
  serverHttpUrl,
  onDrillDownToC2,
  onOpenFile,
  onSelectNode,
  selectedNodeId,
}) => {
  // Active Layout Variant
  const [variant, setVariant] = useState<C1Variant>('playground');

  // Filter toggles
  const [showApps, setShowApps] = useState(true);
  const [showServices, setShowServices] = useState(true);
  const [showExternal, setShowExternal] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');

  // Live Playground Tuning Settings
  const [settings, setSettings] = useState<C1PlaygroundSettings>(DEFAULT_SETTINGS);
  const [isTunerOpen, setIsTunerOpen] = useState(true);

  // Zoom and pan
  const [zoomLevel, setZoomLevel] = useState(1);
  const [mermaidZoom, setMermaidZoom] = useState(1);
  const [mermaidPan, setMermaidPan] = useState({ x: 0, y: 0 });
  const [showMermaidMarkdown, setShowMermaidMarkdown] = useState(false);
  const [copied, setCopied] = useState(false);

  // Mermaid render state
  const [mermaidSvgHtml, setMermaidSvgHtml] = useState<string>('');
  const [mermaidError, setMermaidError] = useState<string | null>(null);

  // Canvas refs
  const fitFlowRef = useRef<(() => void) | null>(null);
  const zoomInFlowRef = useRef<(() => void) | null>(null);
  const zoomOutFlowRef = useRef<(() => void) | null>(null);

  // Cytoscape ref
  const cyContainerRef = useRef<HTMLDivElement>(null);
  const cyInstanceRef = useRef<cytoscape.Core | null>(null);

  const isDraggingMermaidRef = useRef(false);
  const mermaidDragStartRef = useRef({ x: 0, y: 0 });

  // Initialize mermaid on mount
  useEffect(() => {
    mermaid.initialize({
      startOnLoad: false,
      theme: 'dark',
      securityLevel: 'loose',
      fontFamily: 'var(--vscode-font-family, sans-serif)',
      flowchart: {
        useMaxWidth: false,
        htmlLabels: true,
        curve: 'basis',
      },
    });
  }, []);

  // --------------------------------------------------------------------------
  // Step 1: Filter and classify nodes strictly according to C1 rules
  // --------------------------------------------------------------------------
  const { classifiedNodes, idToNodeMap, counts } = useMemo(() => {
    const nodesList: ClassifiedC1Node[] = [];
    const nMap = new Map<string, ClassifiedC1Node>();

    let appCount = 0;
    let svcCount = 0;
    let extCount = 0;

    for (const node of graph?.nodes || []) {
      const cat = classifyNode(node);
      if (!cat) continue;

      if (cat === 'app') appCount++;
      else if (cat === 'service') svcCount++;
      else if (cat === 'external') extCount++;

      const cNode: ClassifiedC1Node = {
        id: node.id,
        name: node.name || node.id,
        displayName: node.displayName || node.name || node.id,
        category: cat,
        kind: node.kind || (cat === 'app' ? 'App' : cat === 'service' ? 'Service' : 'ExternalService'),
        framework: node.properties?.framework,
        language: node.properties?.language || node.properties?.project_type,
        filePath: node.filePath,
        lineStart: node.lineStart,
        rawNode: node,
      };

      nodesList.push(cNode);
      nMap.set(cNode.id, cNode);
      nMap.set(cNode.id.toLowerCase(), cNode);
      if (cNode.name) {
        nMap.set(cNode.name, cNode);
        nMap.set(cNode.name.toLowerCase(), cNode);
      }
    }

    return {
      classifiedNodes: nodesList,
      idToNodeMap: nMap,
      counts: { apps: appCount, services: svcCount, external: extCount },
    };
  }, [graph]);

  // --------------------------------------------------------------------------
  // Step 2: Build C1 Edges & In/Out Call Stats
  // --------------------------------------------------------------------------
  const { c1Edges, inboundCallsMap, outboundCallsMap } = useMemo(() => {
    const inMap = new Map<string, number>();
    const outMap = new Map<string, number>();
    const edgeAgg = new Map<string, ClassifiedC1Edge>();

    for (const edge of graph?.edges || []) {
      const srcNode = idToNodeMap.get(edge.source) || idToNodeMap.get(edge.source.toLowerCase());
      const tgtNode = idToNodeMap.get(edge.target) || idToNodeMap.get(edge.target.toLowerCase());

      if (!srcNode || !tgtNode || srcNode.id === tgtNode.id) continue;

      outMap.set(srcNode.id, (outMap.get(srcNode.id) || 0) + 1);
      inMap.set(tgtNode.id, (inMap.get(tgtNode.id) || 0) + 1);

      const edgeKey = `${srcNode.id}->${tgtNode.id}`;
      const existing = edgeAgg.get(edgeKey);
      if (existing) {
        existing.count += 1;
      } else {
        edgeAgg.set(edgeKey, {
          source: srcNode.id,
          target: tgtNode.id,
          count: 1,
          label: 'CALLS',
        });
      }
    }

    return {
      c1Edges: Array.from(edgeAgg.values()),
      inboundCallsMap: inMap,
      outboundCallsMap: outMap,
    };
  }, [graph, idToNodeMap]);

  // --------------------------------------------------------------------------
  // Step 3: Filter Nodes by Active Toggles & Search
  // --------------------------------------------------------------------------
  const filteredNodes = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();

    return classifiedNodes.filter((n) => {
      if (n.category === 'app' && !showApps) return false;
      if (n.category === 'service' && !showServices) return false;
      if (n.category === 'external' && !showExternal) return false;

      if (q) {
        const matchesName = n.name.toLowerCase().includes(q);
        const matchesDisplay = n.displayName.toLowerCase().includes(q);
        const matchesTech = (n.framework || n.language || '').toLowerCase().includes(q);
        if (!matchesName && !matchesDisplay && !matchesTech) return false;
      }

      return true;
    });
  }, [classifiedNodes, showApps, showServices, showExternal, searchQuery]);

  const visibleNodeIdSet = useMemo(() => {
    return new Set(filteredNodes.map((n) => n.id));
  }, [filteredNodes]);

  const filteredEdges = useMemo(() => {
    return c1Edges.filter(
      (e) => visibleNodeIdSet.has(e.source) && visibleNodeIdSet.has(e.target)
    );
  }, [c1Edges, visibleNodeIdSet]);

  // --------------------------------------------------------------------------
  // Step 4: Compute ReactFlow Nodes & Edges based on Active Variant
  // --------------------------------------------------------------------------
  const { rfNodesData, rfEdgesData } = useMemo(() => {
    if (
      variant === 'mermaid' ||
      variant === 'cytoscape' ||
      variant === 'concentric' ||
      variant === 'focus' ||
      variant === 'chord' ||
      variant === 'matrix'
    ) {
      return { rfNodesData: [] as Node[], rfEdgesData: [] as Edge[] };
    }

    const {
      rankSep,
      nodeSep,
      nodeWidth,
      fontSize,
      curveType,
      edgeStyle,
      strokeWidth,
      showLabels,
      showArrows,
    } = settings;

    let computedNodes: Node[] = [];

    // Helper for Edge building
    const buildEdges = (orientation: 'LR' | 'TB'): Edge[] => {
      const strokeDash =
        edgeStyle === 'dashed' ? '5 5' : edgeStyle === 'animated' ? '6 4' : undefined;

      return filteredEdges.map((e, idx) => ({
        id: `c1-edge-${e.source}->${e.target}-${idx}`,
        source: e.source,
        target: e.target,
        sourceHandle: 'out',
        targetHandle: 'in',
        type: curveType,
        animated: edgeStyle === 'animated',
        label: showLabels ? (e.count > 1 ? `CALLS (${e.count})` : 'CALLS') : undefined,
        labelStyle: { fill: '#38bdf8', fontSize: 9.5, fontWeight: 600 },
        labelBgStyle: { fill: '#0b1120', fillOpacity: 0.88, rx: 3, ry: 3 },
        labelBgPadding: [4, 6],
        style: {
          stroke: '#38bdf8',
          strokeWidth,
          strokeDasharray: strokeDash,
          opacity: 0.85,
        },
        markerEnd: showArrows
          ? {
              type: MarkerType.ArrowClosed,
              color: '#38bdf8',
              width: 14 + strokeWidth,
              height: 14 + strokeWidth,
            }
          : undefined,
      }));
    };

    // ------------------------------------------------------------------------
    // Variant 2: 3-Column Swimlanes Layout (Apps | Services | External)
    // ------------------------------------------------------------------------
    if (variant === 'swimlanes') {
      const colApps = filteredNodes.filter((n) => n.category === 'app');
      const colServices = filteredNodes.filter((n) => n.category === 'service');
      const colExternal = filteredNodes.filter((n) => n.category === 'external');

      // Sort by call activity descending
      const sortByCalls = (a: ClassifiedC1Node, b: ClassifiedC1Node) => {
        const aCalls = (inboundCallsMap.get(a.id) || 0) + (outboundCallsMap.get(a.id) || 0);
        const bCalls = (inboundCallsMap.get(b.id) || 0) + (outboundCallsMap.get(b.id) || 0);
        return bCalls - aCalls || a.name.localeCompare(b.name);
      };

      colApps.sort(sortByCalls);
      colServices.sort(sortByCalls);
      colExternal.sort(sortByCalls);

      const colGap = rankSep + 40;
      const cardHeight = 84;
      const startY = 80;
      const startX = 40;

      const swimlaneWidth = nodeWidth + 40;

      const maxItems = Math.max(colApps.length, colServices.length, colExternal.length, 1);
      const totalSwimlaneHeight = Math.max(380, startY + maxItems * (cardHeight + nodeSep) + 40);

      const swimlaneBackgrounds: Node[] = [
        {
          id: 'swimlane:apps',
          type: 'c1Swimlane',
          position: { x: startX, y: 20 },
          selectable: false,
          draggable: false,
          zIndex: -1,
          data: {
            id: 'swimlane:apps',
            title: '🌐 Apps & Ingress',
            count: colApps.length,
            category: 'app',
            width: swimlaneWidth,
            height: totalSwimlaneHeight,
          } as C1SwimlaneData,
        },
        {
          id: 'swimlane:services',
          type: 'c1Swimlane',
          position: { x: startX + swimlaneWidth + colGap, y: 20 },
          selectable: false,
          draggable: false,
          zIndex: -1,
          data: {
            id: 'swimlane:services',
            title: '⚙️ Domain Services',
            count: colServices.length,
            category: 'service',
            width: swimlaneWidth,
            height: totalSwimlaneHeight,
          } as C1SwimlaneData,
        },
        {
          id: 'swimlane:external',
          type: 'c1Swimlane',
          position: { x: startX + (swimlaneWidth + colGap) * 2, y: 20 },
          selectable: false,
          draggable: false,
          zIndex: -1,
          data: {
            id: 'swimlane:external',
            title: '🔌 External APIs',
            count: colExternal.length,
            category: 'external',
            width: swimlaneWidth,
            height: totalSwimlaneHeight,
          } as C1SwimlaneData,
        },
      ];

      const makeColumnNodes = (list: ClassifiedC1Node[], colX: number) => {
        return list.map((n, idx) => {
          const isSelected = selectedNodeId === n.id;
          const nodeData: C1NodeData = {
            id: n.id,
            name: n.name,
            displayName: n.displayName,
            category: n.category,
            kind: n.kind,
            framework: n.framework,
            language: n.language,
            filePath: n.filePath,
            lineStart: n.lineStart,
            orientation: 'LR',
            fontSize,
            width: nodeWidth,
            inboundCalls: inboundCallsMap.get(n.id) || 0,
            outboundCalls: outboundCallsMap.get(n.id) || 0,
            onOpenFile,
            onDrillDownToC2,
            onSelectNode: () => onSelectNode?.(n.rawNode),
            isSelected,
          };

          return {
            id: n.id,
            type: 'c1Laconic',
            position: {
              x: colX + 20,
              y: startY + idx * (cardHeight + nodeSep),
            },
            data: nodeData,
          };
        });
      };

      const nodesApps = makeColumnNodes(colApps, startX);
      const nodesServices = makeColumnNodes(colServices, startX + swimlaneWidth + colGap);
      const nodesExternal = makeColumnNodes(colExternal, startX + (swimlaneWidth + colGap) * 2);

      computedNodes = [...swimlaneBackgrounds, ...nodesApps, ...nodesServices, ...nodesExternal];
      return { rfNodesData: computedNodes, rfEdgesData: buildEdges('LR') };
    }

    // ------------------------------------------------------------------------
    // Variant 7: Clustered Bounded Contexts (Subdomain Domains)
    // ------------------------------------------------------------------------
    if (variant === 'clusters') {
      const getContextGroup = (n: ClassifiedC1Node): { id: string; title: string; category: C1Category } => {
        if (n.category === 'external') {
          return { id: 'ctx_external', title: '🔌 External APIs & SaaS', category: 'external' };
        }
        const lower = n.name.toLowerCase();
        if (
          n.category === 'app' ||
          lower.includes('front') ||
          lower.includes('fe') ||
          lower.includes('landing') ||
          lower.includes('bff') ||
          lower.includes('gateway')
        ) {
          return { id: 'ctx_ingress', title: '🌐 Ingress & Gateways', category: 'app' };
        }
        if (
          lower.includes('route') ||
          lower.includes('calculation') ||
          lower.includes('rate') ||
          lower.includes('redis')
        ) {
          return { id: 'ctx_routing', title: '⚡ Traffic & Route Calculation', category: 'service' };
        }
        if (
          lower.includes('analytic') ||
          lower.includes('stat') ||
          lower.includes('tracker') ||
          lower.includes('network')
        ) {
          return { id: 'ctx_analytics', title: '📊 Analytics & Telemetry', category: 'service' };
        }
        if (
          lower.includes('partner') ||
          lower.includes('lander') ||
          lower.includes('approval') ||
          lower.includes('template')
        ) {
          return { id: 'ctx_partners', title: '🤝 Partners & Landing Ops', category: 'service' };
        }
        return { id: 'ctx_platform', title: '⚙️ Core Platform Services', category: 'service' };
      };

      const groupsMap = new Map<string, { title: string; category: C1Category; nodes: ClassifiedC1Node[] }>();
      const groupOrder = ['ctx_ingress', 'ctx_routing', 'ctx_partners', 'ctx_analytics', 'ctx_platform', 'ctx_external'];

      for (const gid of groupOrder) {
        groupsMap.set(gid, { title: '', category: 'service', nodes: [] });
      }

      for (const n of filteredNodes) {
        const ctx = getContextGroup(n);
        if (!groupsMap.has(ctx.id)) {
          groupsMap.set(ctx.id, { title: ctx.title, category: ctx.category, nodes: [] });
        }
        const grp = groupsMap.get(ctx.id)!;
        grp.title = ctx.title;
        grp.category = ctx.category;
        grp.nodes.push(n);
      }

      const gridCols = 3;
      const boxWidth = Math.max(300, nodeWidth + 40);
      const cardW = nodeWidth;
      const cardH = 78;
      const gapX = 70;
      const startX = 40;
      const startY = 40;

      const clusterBgNodes: Node[] = [];
      const clusterItemNodes: Node[] = [];

      const activeGroups = groupOrder
        .map((gid) => ({ id: gid, ...groupsMap.get(gid)! }))
        .filter((g) => g.nodes.length > 0);

      activeGroups.forEach((g, gIdx) => {
        const c = gIdx % gridCols;
        const r = Math.floor(gIdx / gridCols);

        const groupHeight = Math.max(220, 60 + g.nodes.length * (cardH + 16));
        const posX = startX + c * (boxWidth + gapX);
        const posY = startY + r * 620;

        clusterBgNodes.push({
          id: `cluster_bg_${g.id}`,
          type: 'c1Swimlane',
          position: { x: posX, y: posY },
          selectable: false,
          draggable: false,
          zIndex: -1,
          data: {
            id: `cluster_bg_${g.id}`,
            title: g.title,
            count: g.nodes.length,
            category: g.category,
            width: boxWidth,
            height: groupHeight,
          } as C1SwimlaneData,
        });

        g.nodes.forEach((n, nIdx) => {
          const isSelected = selectedNodeId === n.id;
          const nodeData: C1NodeData = {
            id: n.id,
            name: n.name,
            displayName: n.displayName,
            category: n.category,
            kind: n.kind,
            framework: n.framework,
            language: n.language,
            filePath: n.filePath,
            lineStart: n.lineStart,
            orientation: 'LR',
            fontSize,
            width: cardW,
            inboundCalls: inboundCallsMap.get(n.id) || 0,
            outboundCalls: outboundCallsMap.get(n.id) || 0,
            onOpenFile,
            onDrillDownToC2,
            onSelectNode: () => onSelectNode?.(n.rawNode),
            isSelected,
          };

          clusterItemNodes.push({
            id: n.id,
            type: 'c1Laconic',
            position: {
              x: posX + 20,
              y: posY + 50 + nIdx * (cardH + 16),
            },
            data: nodeData,
          });
        });
      });

      return {
        rfNodesData: [...clusterBgNodes, ...clusterItemNodes],
        rfEdgesData: buildEdges('LR'),
      };
    }

    // ------------------------------------------------------------------------
    // Dagre Graph Layout (Playground, LR, TB)
    // ------------------------------------------------------------------------
    const isTB = variant === 'dagre-tb';
    const isLaconic = variant === 'playground';
    const orientation = isTB ? 'TB' : 'LR';

    const cardHeight = isLaconic ? 82 : 98;

    const g = new dagre.graphlib.Graph();
    g.setGraph({
      rankdir: orientation,
      align: 'UL',
      nodesep: nodeSep,
      ranksep: rankSep,
      marginx: 50,
      marginy: 50,
    });
    g.setDefaultEdgeLabel(() => ({}));

    for (const n of filteredNodes) {
      g.setNode(n.id, { width: nodeWidth, height: cardHeight });
    }

    for (const e of filteredEdges) {
      g.setEdge(e.source, e.target);
    }

    dagre.layout(g);

    computedNodes = filteredNodes.map((n) => {
      const pos = g.node(n.id);
      const isSelected = selectedNodeId === n.id;
      const type = isLaconic
        ? 'c1Laconic'
        : n.category === 'app'
        ? 'c1App'
        : n.category === 'external'
        ? 'c1External'
        : 'c1Service';

      const nodeData: C1NodeData = {
        id: n.id,
        name: n.name,
        displayName: n.displayName,
        category: n.category,
        kind: n.kind,
        framework: n.framework,
        language: n.language,
        filePath: n.filePath,
        lineStart: n.lineStart,
        orientation,
        fontSize,
        width: nodeWidth,
        inboundCalls: inboundCallsMap.get(n.id) || 0,
        outboundCalls: outboundCallsMap.get(n.id) || 0,
        onOpenFile,
        onDrillDownToC2,
        onSelectNode: () => onSelectNode?.(n.rawNode),
        isSelected,
      };

      return {
        id: n.id,
        type,
        position: {
          x: (pos?.x || 0) - nodeWidth / 2,
          y: (pos?.y || 0) - cardHeight / 2,
        },
        data: nodeData,
      };
    });

    return { rfNodesData: computedNodes, rfEdgesData: buildEdges(orientation) };
  }, [
    variant,
    filteredNodes,
    filteredEdges,
    settings,
    selectedNodeId,
    inboundCallsMap,
    outboundCallsMap,
    onOpenFile,
    onDrillDownToC2,
    onSelectNode,
  ]);

  const [rfNodes, setRfNodes, onNodesChange] = useNodesState(rfNodesData);
  const [rfEdges, setRfEdges, onEdgesChange] = useEdgesState(rfEdgesData);

  useEffect(() => {
    setRfNodes(rfNodesData);
  }, [rfNodesData, setRfNodes]);

  useEffect(() => {
    setRfEdges(rfEdgesData);
  }, [rfEdgesData, setRfEdges]);

  // --------------------------------------------------------------------------
  // Step 5: Cytoscape Force-Directed (Variant 5)
  // --------------------------------------------------------------------------
  useEffect(() => {
    if ((variant !== 'cytoscape' && variant !== 'concentric') || !cyContainerRef.current) return;

    if (cyInstanceRef.current) {
      cyInstanceRef.current.destroy();
      cyInstanceRef.current = null;
    }

    const elements: cytoscape.ElementDefinition[] = [];

    for (const n of filteredNodes) {
      const bgColor = n.category === 'app' ? '#0288d1' : n.category === 'external' ? '#10b981' : '#e53935';
      const borderColor = n.category === 'app' ? '#38bdf8' : n.category === 'external' ? '#34d399' : '#f87171';
      const tag = n.category === 'app' ? 'APP' : n.category === 'external' ? 'EXT' : 'SVC';

      elements.push({
        group: 'nodes',
        data: {
          id: n.id,
          name: n.name,
          label: `${tag}\n${n.displayName}`,
          bgColor,
          borderColor,
          category: n.category,
        },
      });
    }

    for (const e of filteredEdges) {
      elements.push({
        group: 'edges',
        data: {
          id: `cy-edge-${e.source}->${e.target}`,
          source: e.source,
          target: e.target,
          label: e.count > 1 ? String(e.count) : '',
        },
      });
    }

    const cy = cytoscape({
      container: cyContainerRef.current,
      elements,
      boxSelectionEnabled: false,
      style: [
        {
          selector: 'node',
          style: {
            'shape': 'round-rectangle',
            'width': 180,
            'height': 60,
            'background-color': 'data(bgColor)',
            'border-width': 2,
            'border-color': 'data(borderColor)',
            'border-opacity': 0.9,
            'label': 'data(label)',
            'color': '#ffffff',
            'font-size': '11px',
            'font-weight': 700,
            'text-valign': 'center',
            'text-halign': 'center',
            'text-wrap': 'wrap',
            'text-max-width': '160px',
            'transition-property': 'background-color, border-color, width, height, opacity',
            'transition-duration': 0.15,
          },
        },
        {
          selector: 'edge',
          style: {
            'width': 2,
            'line-color': '#38bdf8',
            'target-arrow-color': '#38bdf8',
            'target-arrow-shape': 'triangle',
            'arrow-scale': 1.1,
            'curve-style': 'bezier',
            'label': 'data(label)',
            'font-size': '10px',
            'font-weight': 600,
            'color': '#38bdf8',
            'text-background-color': '#080c14',
            'text-background-opacity': 0.85,
            'text-background-padding': '2px',
            'text-background-shape': 'roundrectangle',
            'opacity': 0.8,
          },
        },
        {
          selector: 'node:selected',
          style: {
            'border-color': '#ffffff',
            'border-width': 3,
            'underlay-color': '#38bdf8',
            'underlay-padding': '6px',
            'underlay-opacity': 0.4,
          },
        },
        {
          selector: '.dimmed',
          style: {
            'opacity': 0.25,
          },
        },
      ],
    });

    cy.on('tap', 'node', (evt) => {
      const node = evt.target;
      const nId = node.id();
      const cNode = idToNodeMap.get(nId);
      if (cNode) {
        onSelectNode?.(cNode.rawNode);
      }
      cy.elements().removeClass('dimmed');
      const neighborhood = node.neighborhood().add(node);
      cy.elements().not(neighborhood).addClass('dimmed');
    });

    cy.on('tap', (evt) => {
      if (evt.target === cy) {
        onSelectNode?.(null);
        cy.elements().removeClass('dimmed');
      }
    });

    // Run layout based on active variant
    const layout =
      variant === 'concentric'
        ? cy.layout({
            name: 'concentric',
            concentric: (node: any) => {
              const cat = node.data('category');
              return cat === 'app' ? 3 : cat === 'service' ? 2 : 1;
            },
            levelWidth: () => 1,
            minNodeSpacing: 55 * settings.cytoscapeAir,
            padding: 40,
            animate: true,
            animationDuration: 400,
          } as any)
        : cy.layout({
            name: 'cose',
            animate: true,
            animationDuration: 500,
            nodeRepulsion: () => 45000 * settings.cytoscapeAir,
            idealEdgeLength: () => 120 * settings.cytoscapeAir,
            edgeElasticity: () => 32,
            gravity: 0.25,
            nodeOverlap: 20,
          } as any);

    layout.run();
    cyInstanceRef.current = cy;

    return () => {
      cy.destroy();
      cyInstanceRef.current = null;
    };
  }, [variant, filteredNodes, filteredEdges, settings.cytoscapeAir, idToNodeMap, onSelectNode]);

  // --------------------------------------------------------------------------
  // Step 6: Mermaid Flowchart (Variant 6)
  // --------------------------------------------------------------------------
  const mermaidSource = useMemo(() => {
    if (filteredNodes.length === 0) {
      return 'flowchart LR\n  empty["No C1 System Context entities loaded"]';
    }

    const sanitize = (id: string) => id.replace(/[^a-zA-Z0-9_]/g, '_');
    const esc = (text: string) => (text || '').replace(/"/g, "'");

    const lines: string[] = ['flowchart LR'];

    const apps = filteredNodes.filter((n) => n.category === 'app');
    if (apps.length > 0) {
      lines.push('  subgraph Apps ["Apps & Ingress"]');
      for (const a of apps) {
        lines.push(`    app_${sanitize(a.id)}["🌐 ${esc(a.displayName)}"]`);
      }
      lines.push('  end');
    }

    const services = filteredNodes.filter((n) => n.category === 'service');
    if (services.length > 0) {
      lines.push('  subgraph Services ["Core Domain Services"]');
      for (const s of services) {
        const icon = s.kind.toLowerCase() === 'worker' ? '⚡' : '⚙️';
        lines.push(`    svc_${sanitize(s.id)}["${icon} ${esc(s.displayName)}"]`);
      }
      lines.push('  end');
    }

    const external = filteredNodes.filter((n) => n.category === 'external');
    if (external.length > 0) {
      lines.push('  subgraph External ["External Services"]');
      for (const x of external) {
        lines.push(`    ext_${sanitize(x.id)}["🔌 ${esc(x.displayName)}"]`);
      }
      lines.push('  end');
    }

    const getPrefix = (id: string) => {
      const node = idToNodeMap.get(id);
      if (!node) return 'n_';
      return node.category === 'app' ? 'app_' : node.category === 'external' ? 'ext_' : 'svc_';
    };

    for (const edge of filteredEdges) {
      const srcPrefix = getPrefix(edge.source);
      const tgtPrefix = getPrefix(edge.target);
      const label = edge.count > 1 ? `CALLS (${edge.count})` : 'CALLS';
      lines.push(
        `  ${srcPrefix}${sanitize(edge.source)} -->|"${label}"| ${tgtPrefix}${sanitize(edge.target)}`
      );
    }

    return lines.join('\n');
  }, [filteredNodes, filteredEdges, idToNodeMap]);

  useEffect(() => {
    if (variant !== 'mermaid' || !mermaidSource) return;

    let isMounted = true;
    setMermaidError(null);

    const renderId = `c1-mermaid-${Date.now()}`;
    mermaid
      .render(renderId, mermaidSource)
      .then(({ svg }) => {
        if (isMounted) {
          setMermaidSvgHtml(svg);
          setMermaidError(null);
        }
      })
      .catch((err) => {
        if (isMounted) {
          console.error('[C1 Mermaid render error]', err);
          setMermaidError(err.message || String(err));
        }
      });

    return () => {
      isMounted = false;
    };
  }, [variant, mermaidSource]);

  // Mermaid Pan & Zoom Handlers
  const handleMermaidWheel = (e: React.WheelEvent) => {
    e.preventDefault();
    const zoomFactor = e.deltaY < 0 ? 1.15 : 0.88;
    setMermaidZoom((prev) => Math.min(3.0, Math.max(0.2, prev * zoomFactor)));
  };

  const handleMermaidMouseDown = (e: React.MouseEvent) => {
    if (e.button !== 0) return;
    isDraggingMermaidRef.current = true;
    mermaidDragStartRef.current = {
      x: e.clientX - mermaidPan.x,
      y: e.clientY - mermaidPan.y,
    };
  };

  const handleMermaidMouseMove = (e: React.MouseEvent) => {
    if (!isDraggingMermaidRef.current) return;
    setMermaidPan({
      x: e.clientX - mermaidDragStartRef.current.x,
      y: e.clientY - mermaidDragStartRef.current.y,
    });
  };

  const handleMermaidMouseUp = () => {
    isDraggingMermaidRef.current = false;
  };

  const handleResetMermaid = () => {
    setMermaidZoom(1);
    setMermaidPan({ x: 0, y: 0 });
  };

  const handleCopyMermaid = async () => {
    try {
      await navigator.clipboard.writeText(mermaidSource);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (err) {
      console.error('Failed to copy Mermaid:', err);
    }
  };

  return (
    <div className="c1-system-context-wrapper">
      {/* Top Floating HUD Toolbar */}
      <header className="c1-hud-toolbar">
        {/* Left: View Title & Dropdown of 6 Variants */}
        <div className="c1-hud-left">
          <div className="c1-hud-title-badge">
            <span className="c1-hud-label">Macro Architecture</span>
            <span className="c1-hud-title">C1: System Context</span>
          </div>

          <div className="c1-hud-layout-select">
            <select
              className="c1-layout-dropdown"
              value={variant}
              onChange={(e) => setVariant(e.target.value as C1Variant)}
              title="Select C1 Architectural Layout Variant"
            >
              <option value="playground">🎛️ 1. Laconic Playground (Live Tuner)</option>
              <option value="swimlanes">🏛️ 2. 3-Column Swimlanes (Apps | Services | External)</option>
              <option value="focus">🎯 3. Focus / Ego-Network (1 Service + Neighbors)</option>
              <option value="chord">⭕ 4. Chord Wheel (Interactive Circular Ribbons)</option>
              <option value="matrix">▦ 5. Dependency Structure Matrix (DSM)</option>
              <option value="concentric">🔘 6. Radial Concentric Rings (Ingress → Core → Ext)</option>
              <option value="clusters">📦 7. Clustered Bounded Contexts</option>
              <option value="dagre-lr">📊 8. Dagre Hierarchical (L → R)</option>
              <option value="dagre-tb">⬇️ 9. Dagre Hierarchical (Top → Down)</option>
              <option value="cytoscape">🌐 10. Force-Directed Physics (Cytoscape COSE)</option>
              <option value="mermaid">🧜 11. Mermaid Flowchart</option>
            </select>
          </div>

          {/* Toggle Tuner Drawer */}
          {variant !== 'mermaid' && variant !== 'focus' && variant !== 'chord' && variant !== 'matrix' && (
            <button
              className={`c1-tuner-toggle-btn ${isTunerOpen ? 'active' : ''}`}
              onClick={() => setIsTunerOpen(!isTunerOpen)}
              title="Toggle Live Playground Tuning Controls"
            >
              ⚙️ Tuner
            </button>
          )}
        </div>

        {/* Center: Category Filters (Apps, Services, External) */}
        <div className="c1-hud-filters">
          <button
            className={`c1-filter-chip ${showApps ? 'is-active' : 'is-hidden'}`}
            onClick={() => setShowApps(!showApps)}
            title={showApps ? 'Hide Apps & Ingress' : 'Show Apps & Ingress'}
          >
            {!showApps && <span className="filter-cross">✕</span>}
            🌐 Apps ({counts.apps})
          </button>

          <button
            className={`c1-filter-chip ${showServices ? 'is-active' : 'is-hidden'}`}
            onClick={() => setShowServices(!showServices)}
            title={showServices ? 'Hide Core Services' : 'Show Core Services'}
          >
            {!showServices && <span className="filter-cross">✕</span>}
            ⚙️ Services ({counts.services})
          </button>

          <button
            className={`c1-filter-chip ${showExternal ? 'is-active' : 'is-hidden'}`}
            onClick={() => setShowExternal(!showExternal)}
            title={showExternal ? 'Hide External Services' : 'Show External Services'}
          >
            {!showExternal && <span className="filter-cross">✕</span>}
            🔌 External ({counts.external})
          </button>

          <span className="c1-calls-pill" title="Total inter-service call relationships">
            ⚡ {filteredEdges.length} Links
          </span>
        </div>

        {/* Right: Search & Zoom Controls */}
        <div className="c1-hud-right">
          <div className="c1-search-box">
            <span className="c1-search-icon">🔍</span>
            <input
              type="text"
              className="c1-search-input"
              placeholder="Filter..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
            {searchQuery && (
              <button
                className="c1-search-clear-btn"
                onClick={() => setSearchQuery('')}
                title="Clear filter"
              >
                ✕
              </button>
            )}
          </div>

          <div className="c1-toolbar-separator" />

          {/* Variant-specific zoom / action buttons */}
          {variant === 'mermaid' ? (
            <div className="c1-mermaid-actions">
              <div className="c1-zoom-controls">
                <button
                  className="ctrl-btn"
                  onClick={() => setMermaidZoom((z) => Math.max(0.2, z / 1.2))}
                  title="Zoom Out (−)"
                >
                  −
                </button>
                <span
                  className="zoom-pct"
                  onClick={handleResetMermaid}
                  title="Reset Zoom to 100%"
                >
                  {Math.round(mermaidZoom * 100)}%
                </span>
                <button
                  className="ctrl-btn"
                  onClick={() => setMermaidZoom((z) => Math.min(3.0, z * 1.2))}
                  title="Zoom In (+)"
                >
                  +
                </button>
                <button
                  className="ctrl-btn reset-btn"
                  onClick={handleResetMermaid}
                  title="Reset View"
                >
                  ↺ Reset
                </button>
              </div>

              <div className="c1-toolbar-separator" />

              <button
                className={`action-btn ${showMermaidMarkdown ? 'primary' : 'secondary'}`}
                onClick={() => setShowMermaidMarkdown(!showMermaidMarkdown)}
                title="Toggle between SVG diagram and Mermaid Markdown"
              >
                {showMermaidMarkdown ? '📊 Diagram' : '📝 Markdown'}
              </button>

              <button
                className={`action-btn ${copied ? 'copied' : 'secondary'}`}
                onClick={handleCopyMermaid}
                title="Copy Mermaid source to clipboard"
              >
                {copied ? '✓ Copied!' : '📋 Copy'}
              </button>
            </div>
          ) : variant === 'cytoscape' || variant === 'concentric' ? (
            <div className="c1-zoom-controls">
              <button
                className="ctrl-btn"
                onClick={() => cyInstanceRef.current?.zoom(cyInstanceRef.current.zoom() / 1.2)}
                title="Zoom Out (−)"
              >
                −
              </button>
              <button
                className="ctrl-btn reset-btn"
                onClick={() => cyInstanceRef.current?.fit(undefined, 40)}
                title="Fit to Screen"
              >
                ↺ Fit
              </button>
              <button
                className="ctrl-btn"
                onClick={() => cyInstanceRef.current?.zoom(cyInstanceRef.current.zoom() * 1.2)}
                title="Zoom In (+)"
              >
                +
              </button>
            </div>
          ) : variant === 'focus' || variant === 'matrix' || variant === 'chord' ? null : (
            <div className="c1-zoom-controls">
              <button
                className="ctrl-btn"
                onClick={() => zoomOutFlowRef.current?.()}
                title="Zoom Out (−)"
              >
                −
              </button>
              <span
                className="zoom-pct"
                onClick={() => fitFlowRef.current?.()}
                title="Fit View"
              >
                {Math.round(zoomLevel * 100)}%
              </span>
              <button
                className="ctrl-btn"
                onClick={() => zoomInFlowRef.current?.()}
                title="Zoom In (+)"
              >
                +
              </button>
              <button
                className="ctrl-btn reset-btn"
                onClick={() => fitFlowRef.current?.()}
                title="Fit to Screen"
              >
                ↺ Fit
              </button>
            </div>
          )}
        </div>
      </header>

      {/* Floating Live Playground Tuning Bar (Expandable) */}
      {isTunerOpen && variant !== 'mermaid' && (
        <div className="c1-tuner-panel">
          <div className="c1-tuner-row">
            {variant === 'cytoscape' ? (
              <div className="c1-tuner-item">
                <span className="c1-tuner-label">🌬️ Air / Spacing:</span>
                <input
                  type="range"
                  min="0.5"
                  max="3.0"
                  step="0.1"
                  value={settings.cytoscapeAir}
                  onChange={(e) =>
                    setSettings((s) => ({ ...s, cytoscapeAir: parseFloat(e.target.value) }))
                  }
                  className="c1-tuner-slider"
                />
                <span className="c1-tuner-val">{settings.cytoscapeAir.toFixed(1)}x</span>
              </div>
            ) : (
              <>
                <div className="c1-tuner-item" title="Horizontal spacing between hierarchical tiers">
                  <span className="c1-tuner-label">↔️ Gap X:</span>
                  <input
                    type="range"
                    min="40"
                    max="300"
                    step="5"
                    value={settings.rankSep}
                    onChange={(e) =>
                      setSettings((s) => ({ ...s, rankSep: parseInt(e.target.value, 10) }))
                    }
                    className="c1-tuner-slider"
                  />
                  <span className="c1-tuner-val">{settings.rankSep}px</span>
                </div>

                <div className="c1-tuner-item" title="Vertical spacing between sibling cards">
                  <span className="c1-tuner-label">↕️ Gap Y:</span>
                  <input
                    type="range"
                    min="15"
                    max="150"
                    step="5"
                    value={settings.nodeSep}
                    onChange={(e) =>
                      setSettings((s) => ({ ...s, nodeSep: parseInt(e.target.value, 10) }))
                    }
                    className="c1-tuner-slider"
                  />
                  <span className="c1-tuner-val">{settings.nodeSep}px</span>
                </div>

                <div className="c1-tuner-item" title="Card Width">
                  <span className="c1-tuner-label">🔲 Width:</span>
                  <input
                    type="range"
                    min="180"
                    max="360"
                    step="10"
                    value={settings.nodeWidth}
                    onChange={(e) =>
                      setSettings((s) => ({ ...s, nodeWidth: parseInt(e.target.value, 10) }))
                    }
                    className="c1-tuner-slider"
                  />
                  <span className="c1-tuner-val">{settings.nodeWidth}px</span>
                </div>

                <div className="c1-tuner-item" title="Service Title Font Size">
                  <span className="c1-tuner-label">🔤 Font:</span>
                  <input
                    type="range"
                    min="12"
                    max="22"
                    step="1"
                    value={settings.fontSize}
                    onChange={(e) =>
                      setSettings((s) => ({ ...s, fontSize: parseInt(e.target.value, 10) }))
                    }
                    className="c1-tuner-slider"
                  />
                  <span className="c1-tuner-val">{settings.fontSize}px</span>
                </div>

                <div className="c1-tuner-item" title="Edge Connection Type">
                  <span className="c1-tuner-label">〰️ Curve:</span>
                  <select
                    className="c1-tuner-dropdown"
                    value={settings.curveType}
                    onChange={(e) =>
                      setSettings((s) => ({ ...s, curveType: e.target.value as any }))
                    }
                  >
                    <option value="smoothstep">Smoothstep (Orthogonal Rounded)</option>
                    <option value="bezier">Bezier (Curved)</option>
                    <option value="straight">Straight Line</option>
                    <option value="step">Step (Sharp 90°)</option>
                  </select>
                </div>

                <div className="c1-tuner-item" title="Edge Line Style">
                  <span className="c1-tuner-label">➖ Style:</span>
                  <select
                    className="c1-tuner-dropdown"
                    value={settings.edgeStyle}
                    onChange={(e) =>
                      setSettings((s) => ({ ...s, edgeStyle: e.target.value as any }))
                    }
                  >
                    <option value="solid">Solid Line</option>
                    <option value="dashed">Dashed Line</option>
                    <option value="animated">Animated Marching</option>
                  </select>
                </div>

                <div className="c1-tuner-item" title="Edge Thickness">
                  <span className="c1-tuner-label">📏 Line:</span>
                  <input
                    type="range"
                    min="1"
                    max="5"
                    step="0.5"
                    value={settings.strokeWidth}
                    onChange={(e) =>
                      setSettings((s) => ({ ...s, strokeWidth: parseFloat(e.target.value) }))
                    }
                    className="c1-tuner-slider width-slider"
                  />
                  <span className="c1-tuner-val">{settings.strokeWidth}px</span>
                </div>

                <div className="c1-tuner-item">
                  <button
                    className={`c1-tuner-chip ${settings.showLabels ? 'active' : ''}`}
                    onClick={() =>
                      setSettings((s) => ({ ...s, showLabels: !s.showLabels }))
                    }
                    title="Toggle edge call count labels"
                  >
                    🏷️ Labels
                  </button>
                  <button
                    className={`c1-tuner-chip ${settings.showArrows ? 'active' : ''}`}
                    onClick={() =>
                      setSettings((s) => ({ ...s, showArrows: !s.showArrows }))
                    }
                    title="Toggle arrowheads"
                  >
                    ➔ Arrows
                  </button>
                </div>

                <button
                  className="c1-tuner-reset-btn"
                  onClick={() => setSettings(DEFAULT_SETTINGS)}
                  title="Reset to default tuning values"
                >
                  ↺ Reset
                </button>
              </>
            )}
          </div>
        </div>
      )}

      {/* Main Viewport Content */}
      <main className="c1-main-viewport">
        {variant === 'mermaid' ? (
          <div
            className="c1-mermaid-container"
            onWheel={handleMermaidWheel}
            onMouseDown={handleMermaidMouseDown}
            onMouseMove={handleMermaidMouseMove}
            onMouseUp={handleMermaidMouseUp}
            onMouseLeave={handleMermaidMouseUp}
            style={{
              cursor: isDraggingMermaidRef.current ? 'grabbing' : 'grab',
            }}
          >
            {mermaidError && (
              <div className="mermaid-error-banner">
                <span className="error-icon">⚠️</span>
                <div className="error-details">
                  <strong>Diagram Notice:</strong>
                  <p>{mermaidError}</p>
                </div>
                <button
                  className="action-btn primary"
                  onClick={() => setShowMermaidMarkdown(true)}
                >
                  Inspect Source
                </button>
              </div>
            )}

            {showMermaidMarkdown ? (
              <div className="mermaid-code-viewer">
                <div className="code-viewer-header">
                  <span>C1 System Context (Mermaid Definition)</span>
                  <button
                    className={`mini-btn ${copied ? 'copied' : ''}`}
                    onClick={handleCopyMermaid}
                  >
                    {copied ? '✓ Copied' : '📋 Copy Source'}
                  </button>
                </div>
                <pre className="mermaid-code-pre">
                  <code>{mermaidSource}</code>
                </pre>
              </div>
            ) : (
              <div
                className="c1-mermaid-svg-layer"
                style={{
                  transform: `translate(${mermaidPan.x}px, ${mermaidPan.y}px) scale(${mermaidZoom})`,
                  transformOrigin: '0 0',
                }}
                dangerouslySetInnerHTML={{ __html: mermaidSvgHtml }}
              />
            )}
          </div>
        ) : variant === 'focus' ? (
          <C1FocusEgoView
            nodes={filteredNodes}
            edges={filteredEdges}
            selectedNodeId={selectedNodeId}
            onSelectNode={onSelectNode}
            onDrillDownToC2={onDrillDownToC2}
            onOpenFile={onOpenFile}
          />
        ) : variant === 'chord' ? (
          <C1ChordWheelView
            nodes={filteredNodes}
            edges={filteredEdges}
            selectedNodeId={selectedNodeId}
            onSelectNode={onSelectNode}
            onDrillDownToC2={onDrillDownToC2}
          />
        ) : variant === 'matrix' ? (
          <C1MatrixView
            nodes={filteredNodes}
            edges={filteredEdges}
            selectedNodeId={selectedNodeId}
            onSelectNode={onSelectNode}
            onDrillDownToC2={onDrillDownToC2}
          />
        ) : variant === 'cytoscape' || variant === 'concentric' ? (
          <div className="c1-cytoscape-container" ref={cyContainerRef} />
        ) : (
          <ReactFlowProvider>
            <C1ReactFlowCanvas
              nodes={rfNodes}
              edges={rfEdges}
              onNodesChange={onNodesChange}
              onEdgesChange={onEdgesChange}
              onFitViewRef={fitFlowRef}
              onZoomInRef={zoomInFlowRef}
              onZoomOutRef={zoomOutFlowRef}
              currentZoom={zoomLevel}
              setCurrentZoom={setZoomLevel}
            />
          </ReactFlowProvider>
        )}
      </main>
    </div>
  );
};
