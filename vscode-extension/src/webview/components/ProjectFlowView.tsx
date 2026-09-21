import React, { useState, useEffect, useMemo, useCallback, useRef } from 'react';
import dagre from 'dagre';
import {
  ReactFlow,
  Background,
  Controls,
  MiniMap,
  useNodesState,
  useEdgesState,
  useReactFlow,
  ReactFlowProvider,
  Node,
  Edge,
  MarkerType,
} from '@xyflow/react';
import { GraphData, GraphNode, GraphEdge } from '../../../../proto/types';
import { ProjectCardNode, NodeCommsSummary } from './ProjectCardNode';

export interface ProjectFlowViewProps {
  graph: GraphData | null;
  fullGraph?: GraphData | null;
  onSelectProject: (name: string) => void;
  onOpenFile: (filePath: string, lineStart?: number) => void;
}

interface EdgeVisuals {
  stroke: string;
  strokeDasharray?: string;
  strokeWidth: number;
  animated: boolean;
  markerColor: string;
  markerType: MarkerType;
  markerWidth: number;
  markerHeight: number;
  markerStrokeWidth?: number;
  label?: string;
  className?: string;
}

export type EdgeCategory = 'library' | 'service_call' | 'database' | 'messaging';

export const getEdgeCategory = (edge?: GraphEdge, targetNode?: GraphNode): EdgeCategory => {
  // 1. Authoritative Backend Protocol Category
  if (edge?.category) {
    const cat = edge.category.toLowerCase();
    if (cat === 'service_call' || cat === 'database' || cat === 'messaging' || cat === 'library') {
      return cat as EdgeCategory;
    }
  }

  const depType = (edge?.properties?.dependency_type || '').toLowerCase();
  const kind = (edge?.kind || '').toUpperCase();
  const targetRole = targetNode?.properties?.role;
  const targetKind = targetNode?.kind;
  const targetLayer = (targetNode?.properties?.layer || targetNode?.properties?.layerId || '').toLowerCase();
  const targetProjType = (targetNode?.properties?.project_type || '').toLowerCase();

  // 2. Explicit dependency_type or kind
  if (depType === 'database' || kind === 'USES_DB' || targetKind === 'Database' || targetRole === 'database') {
    return 'database';
  }

  if (depType === 'messaging' || kind === 'TRIGGERS' || targetKind === 'Topic' || targetRole === 'topic') {
    return 'messaging';
  }

  if (depType === 'service_call' || kind === 'SERVICE_CALL' || kind === 'CALLS_ENDPOINT') {
    return 'service_call';
  }

  if (depType === 'library' || kind === 'LIBRARY') {
    return 'library';
  }

  // 3. Inferred Fallbacks
  const isLibraryTarget =
    targetNode?.properties?.is_library === 'true' ||
    targetLayer === 'layer_foundation' ||
    targetProjType === 'library';

  if (isLibraryTarget) {
    return 'library';
  }

  const isServiceTarget = targetLayer === 'layer_ingress' || targetLayer === 'layer_components' || targetLayer === 'layer_egress';
  if (isServiceTarget && targetProjType !== 'library') {
    return 'service_call';
  }

  return 'library';
};

const getEdgeVisuals = (edge?: GraphEdge, targetNode?: GraphNode): EdgeVisuals => {
  const category = getEdgeCategory(edge, targetNode);

  switch (category) {
    case 'database':
      return {
        stroke: '#c084fc',
        strokeDasharray: '3,4',
        strokeWidth: 2,
        animated: false,
        markerColor: '#c084fc',
        markerType: MarkerType.ArrowClosed,
        markerWidth: 12,
        markerHeight: 12,
        label: 'Database',
        className: 'edge-database',
      };
    case 'messaging':
      return {
        stroke: '#fbbf24',
        strokeDasharray: '8,3,2,3',
        strokeWidth: 2,
        animated: true,
        markerColor: '#fbbf24',
        markerType: MarkerType.Arrow,
        markerWidth: 16,
        markerHeight: 16,
        markerStrokeWidth: 2.2,
        label: 'Event / Queue',
        className: 'edge-messaging',
      };
    case 'service_call':
      return {
        stroke: '#38bdf8',
        strokeDasharray: '6,4',
        strokeWidth: 2.2,
        animated: false,
        markerColor: '#38bdf8',
        markerType: MarkerType.ArrowClosed,
        markerWidth: 16,
        markerHeight: 16,
        label: 'Service Call',
        className: 'edge-service-call',
      };
    case 'library':
    default:
      return {
        stroke: '#34d399',
        strokeDasharray: undefined,
        strokeWidth: 1,
        animated: false,
        markerColor: '#34d399',
        markerType: MarkerType.Arrow,
        markerWidth: 12,
        markerHeight: 12,
        markerStrokeWidth: 1.4,
        label: 'Library',
        className: 'edge-library',
      };
  }
};

const nodeTypes = {
  projectCard: ProjectCardNode as any,
};

const FlowInner: React.FC<ProjectFlowViewProps> = ({
  graph,
  fullGraph,
  onSelectProject,
  onOpenFile,
}) => {
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const { fitView } = useReactFlow();

  // Root project under inspection (Level 0)
  const rootProjectName = useMemo(() => {
    return (
      graph?.metadata?.selectedProject ||
      graph?.nodes?.find((n) => n.properties?.column === 'center')?.name ||
      ''
    );
  }, [graph]);

  // Track granular expanded categories for each project:
  // Key: project id or name. Value: Set of category strings:
  // 'callsOut' | 'acceptsIn' | 'dbOut' | 'messagesOut' | 'messagesIn' | 'libsOut'
  const [expandedCategories, setExpandedCategories] = useState<Map<string, Set<string>>>(new Map());

  // Connection types visibility toggle (Library, Service Call, Database, Event/Queue)
  const [visibleEdgeTypes, setVisibleEdgeTypes] = useState<Record<EdgeCategory, boolean>>({
    library: true,
    service_call: true,
    database: true,
    messaging: true,
  });

  const handleToggleEdgeType = useCallback((cat: EdgeCategory) => {
    setVisibleEdgeTypes((prev) => ({
      ...prev,
      [cat]: !prev[cat],
    }));
  }, []);

  // Build comprehensive graph lookup and communications breakdown from fullGraph (fallback to graph)
  const { nodeMap, outboundMap, inboundMap, inCountMap, outCountMap, edgeLookup, commsMap } = useMemo(() => {
    const dataSource = fullGraph && fullGraph.nodes && fullGraph.nodes.length > 0 ? fullGraph : graph;

    const nMap = new Map<string, GraphNode>();
    const outMap = new Map<string, GraphNode[]>();
    const inMap = new Map<string, GraphNode[]>();
    const inc: Record<string, number> = {};
    const outc: Record<string, number> = {};
    const cMap = new Map<string, NodeCommsSummary>();

    const getOrCreateComms = (id: string): NodeCommsSummary => {
      let c = cMap.get(id);
      if (!c) {
        c = { callsOut: [], acceptsIn: [], dbOut: [], messagesOut: [], messagesIn: [], libsOut: [] };
        cMap.set(id, c);
      }
      return c;
    };

    for (const n of dataSource?.nodes || []) {
      nMap.set(n.id, n);
      if (!nMap.has(n.name)) nMap.set(n.name, n);
      outMap.set(n.id, []);
      inMap.set(n.id, []);
    }

    // Also include any nodes from graph (if neighborhood had extra info)
    for (const n of graph?.nodes || []) {
      if (!nMap.has(n.id)) nMap.set(n.id, n);
      if (!nMap.has(n.name)) nMap.set(n.name, n);
      if (!outMap.has(n.id)) outMap.set(n.id, []);
      if (!inMap.has(n.id)) inMap.set(n.id, []);
    }

    // Combine all edges from dataSource and graph
    const allEdges = [...(dataSource?.edges || [])];
    if (graph?.edges && graph !== dataSource) {
      for (const ge of graph.edges) {
        if (!allEdges.some((e) => e.source === ge.source && e.target === ge.target)) {
          allEdges.push(ge);
        }
      }
    }

    const edgeMap = new Map<string, GraphEdge>();

    for (const e of allEdges) {
      const srcNode = nMap.get(e.source);
      const tgtNode = nMap.get(e.target);
      if (!srcNode || !tgtNode) continue;

      edgeMap.set(`${e.source}->${e.target}`, e);
      edgeMap.set(`${srcNode.id}->${tgtNode.id}`, e);

      // Outbound (srcNode is using tgtNode)
      const curOut = outMap.get(srcNode.id) || [];
      if (!curOut.some((x) => x.id === tgtNode.id)) {
        curOut.push(tgtNode);
        outMap.set(srcNode.id, curOut);
      }

      // Inbound (tgtNode is used by srcNode)
      const curIn = inMap.get(tgtNode.id) || [];
      if (!curIn.some((x) => x.id === srcNode.id)) {
        curIn.push(srcNode);
        inMap.set(tgtNode.id, curIn);
      }

      // Communications breakdown
      const srcComms = getOrCreateComms(srcNode.id);
      const tgtComms = getOrCreateComms(tgtNode.id);
      const category = getEdgeCategory(e, tgtNode);

      if (category === 'service_call') {
        if (!srcComms.callsOut.some((x) => x.id.toLowerCase() === tgtNode.id.toLowerCase())) {
          srcComms.callsOut.push({ id: tgtNode.id, name: tgtNode.name, type: tgtNode.properties?.service_type, filePath: tgtNode.filePath });
        }
        if (!tgtComms.acceptsIn.some((x) => x.id.toLowerCase() === srcNode.id.toLowerCase())) {
          tgtComms.acceptsIn.push({ id: srcNode.id, name: srcNode.name, type: srcNode.properties?.service_type, filePath: srcNode.filePath });
        }
      } else if (category === 'database') {
        if (!srcComms.dbOut.some((x) => x.id.toLowerCase() === tgtNode.id.toLowerCase() || x.name.toLowerCase() === tgtNode.name.toLowerCase())) {
          srcComms.dbOut.push({ id: tgtNode.id, name: tgtNode.name, dbType: tgtNode.properties?.db_type });
        }
      } else if (category === 'messaging') {
        if (!srcComms.messagesOut.some((x) => x.id.toLowerCase() === tgtNode.id.toLowerCase())) {
          srcComms.messagesOut.push({ id: tgtNode.id, name: tgtNode.name });
        }
        if (!tgtComms.messagesIn.some((x) => x.id.toLowerCase() === srcNode.id.toLowerCase())) {
          tgtComms.messagesIn.push({ id: srcNode.id, name: srcNode.name });
        }
      } else if (category === 'library') {
        if (!srcComms.libsOut.some((x) => x.id.toLowerCase() === tgtNode.id.toLowerCase() || x.name.toLowerCase() === tgtNode.name.toLowerCase())) {
          srcComms.libsOut.push({ id: tgtNode.id, name: tgtNode.name });
        }
        if (!tgtComms.acceptsIn.some((x) => x.id.toLowerCase() === srcNode.id.toLowerCase())) {
          tgtComms.acceptsIn.push({ id: srcNode.id, name: srcNode.name, filePath: srcNode.filePath });
        }
      }
    }

    for (const n of nMap.values()) {
      const outList = outMap.get(n.id) || [];
      const inList = inMap.get(n.id) || [];
      outc[n.id] = outList.length;
      inc[n.id] = inList.length;
    }

    return {
      nodeMap: nMap,
      outboundMap: outMap,
      inboundMap: inMap,
      inCountMap: inc,
      outCountMap: outc,
      edgeLookup: edgeMap,
      commsMap: cMap,
    };
  }, [fullGraph, graph]);

  // Whenever the root target project changes, initialize its direct categories as expanded
  const lastRootRef = useRef<string>('');
  useEffect(() => {
    if (rootProjectName && rootProjectName !== lastRootRef.current) {
      lastRootRef.current = rootProjectName;
      const rootNode = nodeMap.get(rootProjectName);
      const allCats = new Set<string>([
        'callsOut',
        'acceptsIn',
        'dbOut',
        'messagesOut',
        'messagesIn',
        'libsOut',
      ]);
      const nextMap = new Map<string, Set<string>>();
      nextMap.set(rootProjectName, allCats);
      if (rootNode) {
        nextMap.set(rootNode.id, allCats);
      }
      setExpandedCategories(nextMap);
    }
  }, [rootProjectName, nodeMap]);

  const DEFAULT_CATEGORIES = useMemo(
    () => new Set<string>(['callsOut', 'acceptsIn', 'dbOut', 'messagesOut', 'messagesIn', 'libsOut']),
    []
  );

  const getActiveCategories = useCallback(
    (id: string, name?: string): Set<string> => {
      if (expandedCategories.has(id)) return expandedCategories.get(id)!;
      if (name && expandedCategories.has(name)) return expandedCategories.get(name)!;
      return DEFAULT_CATEGORIES;
    },
    [expandedCategories, DEFAULT_CATEGORIES]
  );

  // Toggle specific communication category for a project
  const handleToggleCategory = useCallback(
    (projectName: string, category: string, projectId?: string) => {
      setExpandedCategories((prev) => {
        const next = new Map(prev);
        const current =
          (projectId && prev.get(projectId)) ||
          prev.get(projectName) ||
          new Set(['callsOut', 'acceptsIn', 'dbOut', 'messagesOut', 'messagesIn', 'libsOut']);

        const updated = new Set(current);
        if (updated.has(category)) {
          updated.delete(category);
        } else {
          updated.add(category);
        }

        next.set(projectName, updated);
        if (projectId) next.set(projectId, updated);
        return next;
      });
    },
    []
  );

  // Set of project IDs/names whose cards are in expanded mode (showing typed connector dots)
  const [expandedCards, setExpandedCards] = useState<Set<string>>(new Set());

  // Initialize root project card as expanded
  useEffect(() => {
    if (rootProjectName) {
      const rootNode = nodeMap.get(rootProjectName);
      const set = new Set<string>([rootProjectName]);
      if (rootNode) set.add(rootNode.id);
      setExpandedCards(set);
    }
  }, [rootProjectName, nodeMap]);

  const handleToggleCardExpand = useCallback((projectName: string, projectId?: string) => {
    setExpandedCards((prev) => {
      const next = new Set(prev);
      const isExp = next.has(projectName) || (projectId && next.has(projectId));
      if (isExp) {
        next.delete(projectName);
        if (projectId) next.delete(projectId);
      } else {
        next.add(projectName);
        if (projectId) next.add(projectId);
      }
      return next;
    });
  }, []);

  const handleResetLevels = useCallback(() => {
    if (rootProjectName) {
      const rootNode = nodeMap.get(rootProjectName);
      const allCats = new Set<string>([
        'callsOut',
        'acceptsIn',
        'dbOut',
        'messagesOut',
        'messagesIn',
        'libsOut',
      ]);
      const nextMap = new Map<string, Set<string>>();
      nextMap.set(rootProjectName, allCats);
      if (rootNode) {
        nextMap.set(rootNode.id, allCats);
      }
      setExpandedCategories(nextMap);
      const set = new Set<string>([rootProjectName]);
      if (rootNode) set.add(rootNode.id);
      setExpandedCards(set);
    }
  }, [rootProjectName, nodeMap]);

  // Multi-Level Progressive Layout Construction (Bidirectional)
  useEffect(() => {
    let rootNode = nodeMap.get(rootProjectName);
    if (!rootNode && rootProjectName) {
      const lower = rootProjectName.toLowerCase();
      for (const [key, n] of nodeMap.entries()) {
        if (key.toLowerCase() === lower || n.name.toLowerCase() === lower) {
          rootNode = n;
          break;
        }
      }
    }

    if (!rootNode) {
      setNodes([]);
      setEdges([]);
      return;
    }

    // ----------------------------------------------------
    // Phase 1: Progressive Expansion of Visible Nodes by Category
    // ----------------------------------------------------
    const visibleNodesMap = new Map<string, GraphNode>();
    visibleNodesMap.set(rootNode.id, rootNode);

    // Iterative expansion for any visible node whose categories are toggled on
    const processedNodes = new Set<string>();

    const isAlreadyVisible = (n: GraphNode) =>
      visibleNodesMap.has(n.id) ||
      Array.from(visibleNodesMap.values()).some((v) => v.id.toLowerCase() === n.id.toLowerCase());

    for (let round = 0; round < 10; round++) {
      let newlyAdded = false;
      const currentNodes = Array.from(visibleNodesMap.values());

      for (const curr of currentNodes) {
        const activeCats =
          expandedCategories.get(curr.id) ||
          expandedCategories.get(curr.name) ||
          new Set<string>();

        const cacheKey = `${curr.id}:${Array.from(activeCats).sort().join(',')}`;
        if (processedNodes.has(cacheKey)) continue;
        processedNodes.add(cacheKey);

        const comms = commsMap.get(curr.id) || commsMap.get(curr.name);

        // Outbound calls
        if (activeCats.has('callsOut') && comms?.callsOut) {
          for (const item of comms.callsOut) {
            const targetNode = nodeMap.get(item.id) || nodeMap.get(item.name);
            if (targetNode && !isAlreadyVisible(targetNode)) {
              visibleNodesMap.set(targetNode.id, targetNode);
              newlyAdded = true;
            }
          }
        }

        // Databases out
        if (activeCats.has('dbOut') && comms?.dbOut) {
          for (const item of comms.dbOut) {
            const targetNode = nodeMap.get(item.id) || nodeMap.get(item.name);
            if (targetNode && !isAlreadyVisible(targetNode)) {
              visibleNodesMap.set(targetNode.id, targetNode);
              newlyAdded = true;
            }
          }
        }

        // Messages out
        if (activeCats.has('messagesOut') && comms?.messagesOut) {
          for (const item of comms.messagesOut) {
            const targetNode = nodeMap.get(item.id) || nodeMap.get(item.name);
            if (targetNode && !isAlreadyVisible(targetNode)) {
              visibleNodesMap.set(targetNode.id, targetNode);
              newlyAdded = true;
            }
          }
        }

        // Libraries out
        if (activeCats.has('libsOut') && comms?.libsOut) {
          for (const item of comms.libsOut) {
            const targetNode = nodeMap.get(item.id) || nodeMap.get(item.name);
            if (targetNode && !isAlreadyVisible(targetNode)) {
              visibleNodesMap.set(targetNode.id, targetNode);
              newlyAdded = true;
            }
          }
        }

        // Inbound accepts calls
        if (activeCats.has('acceptsIn')) {
          if (comms?.acceptsIn && comms.acceptsIn.length > 0) {
            for (const item of comms.acceptsIn) {
              const srcNode = nodeMap.get(item.id) || nodeMap.get(item.name);
              if (srcNode && !isAlreadyVisible(srcNode)) {
                visibleNodesMap.set(srcNode.id, srcNode);
                newlyAdded = true;
              }
            }
          } else {
            const parents = inboundMap.get(curr.id) || inboundMap.get(curr.name) || [];
            for (const p of parents) {
              if (!isAlreadyVisible(p)) {
                visibleNodesMap.set(p.id, p);
                newlyAdded = true;
              }
            }
          }
        }

        // Inbound messages
        if (activeCats.has('messagesIn') && comms?.messagesIn) {
          for (const item of comms.messagesIn) {
            const srcNode = nodeMap.get(item.id) || nodeMap.get(item.name);
            if (srcNode && !isAlreadyVisible(srcNode)) {
              visibleNodesMap.set(srcNode.id, srcNode);
              newlyAdded = true;
            }
          }
        }
      }

      if (!newlyAdded) break;
    }

    const uniqueVisible = Array.from(visibleNodesMap.values());
    const visibleIdSet = new Set<string>();
    for (const n of uniqueVisible) {
      visibleIdSet.add(n.id);
    }

    // ----------------------------------------------------
    // Phase 2: Collect All Directed Edges between Visible Nodes
    // ----------------------------------------------------
    const generatedEdges: Edge[] = [];
    const edgeKeySet = new Set<string>();

    const addEdge = (sourceId: string, targetId: string) => {
      const key = `${sourceId}->${targetId}`;
      if (!edgeKeySet.has(key)) {
        edgeKeySet.add(key);

        const srcNode = nodeMap.get(sourceId);
        const tgtNode = nodeMap.get(targetId);

        const edgeObj =
          edgeLookup.get(key) ||
          (srcNode && tgtNode
            ? edgeLookup.get(`${srcNode.id}->${tgtNode.id}`) ||
              edgeLookup.get(`${srcNode.name}->${tgtNode.name}`) ||
              edgeLookup.get(`${srcNode.id}->${tgtNode.name}`) ||
              edgeLookup.get(`${srcNode.name}->${tgtNode.id}`)
            : undefined);

        const category = getEdgeCategory(edgeObj, tgtNode);
        if (!visibleEdgeTypes[category]) {
          return;
        }

        // Check if communication category is active on source/target cards
        const srcCats = getActiveCategories(sourceId, srcNode?.name);
        const tgtCats = getActiveCategories(targetId, tgtNode?.name);

        let outCat = 'callsOut';
        if (category === 'database') outCat = 'dbOut';
        else if (category === 'messaging') outCat = 'messagesOut';
        else if (category === 'library') outCat = 'libsOut';

        if (!srcCats.has(outCat)) {
          return;
        }

        if (category === 'service_call' && !tgtCats.has('acceptsIn')) {
          return;
        }
        if (category === 'messaging' && !tgtCats.has('messagesIn')) {
          return;
        }

        const visuals = getEdgeVisuals(edgeObj, tgtNode);

        const isSourceExpanded =
          expandedCards.has(sourceId) || (srcNode && (expandedCards.has(srcNode.id) || expandedCards.has(srcNode.name)));
        const isTargetExpanded =
          expandedCards.has(targetId) || (tgtNode && (expandedCards.has(tgtNode.id) || expandedCards.has(tgtNode.name)));

        let sourceHandle = 'source-default';
        let targetHandle = 'target-default';

        if (isSourceExpanded) {
          if (category === 'service_call') sourceHandle = 'source-calls';
          else if (category === 'database') sourceHandle = 'source-db';
          else if (category === 'messaging') sourceHandle = 'source-events';
          else if (category === 'library') sourceHandle = 'source-libs';
        }

        if (isTargetExpanded) {
          if (category === 'service_call' || category === 'database' || category === 'library') targetHandle = 'target-calls';
          else if (category === 'messaging') targetHandle = 'target-events';
        }

        generatedEdges.push({
          id: `edge-${key}`,
          source: sourceId,
          target: targetId,
          sourceHandle,
          targetHandle,
          type: 'bezier',
          animated: visuals.animated,
          className: visuals.className,
          style: {
            stroke: visuals.stroke,
            strokeWidth: visuals.strokeWidth,
            strokeDasharray: visuals.strokeDasharray,
          },
          markerEnd: {
            type: visuals.markerType,
            color: visuals.markerColor,
            width: visuals.markerWidth,
            height: visuals.markerHeight,
            strokeWidth: visuals.markerStrokeWidth,
          },
        });
      }
    };

    // Any edge between two visible nodes in the solution graph is an active visible edge
    for (const u of uniqueVisible) {
      const children = outboundMap.get(u.id) || outboundMap.get(u.name) || [];
      for (const child of children) {
        if (visibleIdSet.has(child.id) || visibleIdSet.has(child.name)) {
          addEdge(u.id, child.id);
        }
      }
    }

    // ----------------------------------------------------
    // Phase 3: DAG Topological Rank & Longest-Path Layering
    // Pushes dependencies to the right (level(target) >= level(source) + 1)
    // so no connections ever exist on the same level!
    // ----------------------------------------------------
    const activeNodeIds = new Set<string>();
    activeNodeIds.add(rootNode.id);
    activeNodeIds.add(rootNode.name);
    for (const edge of generatedEdges) {
      activeNodeIds.add(edge.source);
      activeNodeIds.add(edge.target);
    }

    const finalVisibleNodes = uniqueVisible.filter(
      (n) => activeNodeIds.has(n.id) || activeNodeIds.has(n.name)
    );

    // Build local adjacency for visible nodes and generated edges
    const adjOut = new Map<string, string[]>();
    const adjIn = new Map<string, string[]>();
    for (const n of finalVisibleNodes) {
      adjOut.set(n.id, []);
      adjOut.set(n.name, []);
      adjIn.set(n.id, []);
      adjIn.set(n.name, []);
    }

    for (const edge of generatedEdges) {
      const sOut = adjOut.get(edge.source);
      if (sOut && !sOut.includes(edge.target)) {
        sOut.push(edge.target);
      }
      const tIn = adjIn.get(edge.target);
      if (tIn && !tIn.includes(edge.source)) {
        tIn.push(edge.source);
      }
    }

    // Step 1: Cycle Detection via DFS (Feedback Arc Set)
    // Any edge targeting an ancestor in the active DFS stack is marked as a back-edge.
    const backEdges = new Set<string>();
    const dfsState = new Map<string, number>(); // 0: unvisited, 1: visiting, 2: visited

    const dfs = (uId: string) => {
      dfsState.set(uId, 1);
      const targets = adjOut.get(uId) || [];
      for (const vId of targets) {
        const st = dfsState.get(vId) || 0;
        if (st === 1) {
          backEdges.add(`${uId}->${vId}`);
        } else if (st === 0) {
          dfs(vId);
        }
      }
      dfsState.set(uId, 2);
    };

    // Prioritize DFS starting points:
    // 1. In-degree 0 nodes in visible graph (top-level callers / entry points)
    for (const n of finalVisibleNodes) {
      const inDeg = (adjIn.get(n.id) || []).length;
      if (inDeg === 0 && (dfsState.get(n.id) || 0) === 0) {
        dfs(n.id);
      }
    }
    // 2. rootNode (if not already visited)
    if ((dfsState.get(rootNode.id) || 0) === 0) {
      dfs(rootNode.id);
    }
    // 3. Any remaining nodes
    for (const n of finalVisibleNodes) {
      if ((dfsState.get(n.id) || 0) === 0) {
        dfs(n.id);
      }
    }

    // Step 2: Longest-Path Layering on the forward DAG
    // For every forward edge u -> v: rank(v) >= rank(u) + 1
    // Number of columns naturally equals the topological depth of the visible graph!
    const nodeRankMap = new Map<string, number>();
    for (const n of finalVisibleNodes) {
      nodeRankMap.set(n.id, 0);
      nodeRankMap.set(n.name, 0);
    }

    const maxPasses = Math.max(1, finalVisibleNodes.length);
    for (let pass = 0; pass < maxPasses; pass++) {
      let changed = false;
      for (const edge of generatedEdges) {
        if (backEdges.has(`${edge.source}->${edge.target}`)) continue;

        const uRank = nodeRankMap.get(edge.source) ?? 0;
        const vRank = nodeRankMap.get(edge.target) ?? 0;
        if (vRank < uRank + 1) {
          const nextRank = uRank + 1;
          nodeRankMap.set(edge.target, nextRank);
          const tgtNode = nodeMap.get(edge.target);
          if (tgtNode) {
            nodeRankMap.set(tgtNode.name, nextRank);
          }
          changed = true;
        }
      }
      if (!changed) break;
    }

    // Step 3: Dense Rank Normalization
    // Eliminate any empty column gaps so columns form a contiguous 0 .. (K - 1) sequence
    const uniqueRanks = Array.from(
      new Set(finalVisibleNodes.map((n) => nodeRankMap.get(n.id) ?? 0))
    ).sort((a, b) => a - b);

    const denseMap = new Map<number, number>();
    uniqueRanks.forEach((r, idx) => {
      denseMap.set(r, idx);
    });

    for (const n of finalVisibleNodes) {
      const oldRank = nodeRankMap.get(n.id) ?? 0;
      const dense = denseMap.get(oldRank) ?? 0;
      nodeRankMap.set(n.id, dense);
      nodeRankMap.set(n.name, dense);
    }

    // Step 4: Group unique nodes by column rank
    const levelNodesMap = new Map<number, GraphNode[]>();
    for (const node of finalVisibleNodes) {
      const lvl = nodeRankMap.get(node.id) ?? 0;
      const list = levelNodesMap.get(lvl) || [];
      list.push(node);
      levelNodesMap.set(lvl, list);
    }

    // ----------------------------------------------------
    // Phase 4: Barycentric Crossing Minimization & Centering
    // ----------------------------------------------------
    const sortedLevels = Array.from(levelNodesMap.keys()).sort((a, b) => a - b);

    // Forward sweep: order nodes in each column by average position of their incoming sources
    for (let c = 1; c < sortedLevels.length; c++) {
      const lvl = sortedLevels[c];
      const list = levelNodesMap.get(lvl) || [];
      if (list.length <= 1) continue;

      const prevLvl = sortedLevels[c - 1];
      const prevList = levelNodesMap.get(prevLvl) || [];
      const prevPos = new Map<string, number>();
      prevList.forEach((n, idx) => {
        prevPos.set(n.id, idx);
        prevPos.set(n.name, idx);
      });

      const bary = new Map<string, number>();
      for (const node of list) {
        const inNeighbors = adjIn.get(node.id) || [];
        let sum = 0;
        let count = 0;
        for (const inId of inNeighbors) {
          if (prevPos.has(inId)) {
            sum += prevPos.get(inId)!;
            count++;
          }
        }
        bary.set(node.id, count > 0 ? sum / count : 999);
      }

      list.sort((a, b) => {
        const bA = bary.get(a.id) ?? 999;
        const bB = bary.get(b.id) ?? 999;
        if (bA !== bB) return bA - bB;
        return (a?.name || '').localeCompare(b?.name || '');
      });
      levelNodesMap.set(lvl, list);
    }

    // Backward sweep: order nodes in each column by average position of their outgoing targets
    for (let c = sortedLevels.length - 2; c >= 0; c--) {
      const lvl = sortedLevels[c];
      const list = levelNodesMap.get(lvl) || [];
      if (list.length <= 1) continue;

      const nextLvl = sortedLevels[c + 1];
      const nextList = levelNodesMap.get(nextLvl) || [];
      const nextPos = new Map<string, number>();
      nextList.forEach((n, idx) => {
        nextPos.set(n.id, idx);
        nextPos.set(n.name, idx);
      });

      const bary = new Map<string, number>();
      for (const node of list) {
        const outNeighbors = adjOut.get(node.id) || [];
        let sum = 0;
        let count = 0;
        for (const outId of outNeighbors) {
          if (nextPos.has(outId)) {
            sum += nextPos.get(outId)!;
            count++;
          }
        }
        bary.set(node.id, count > 0 ? sum / count : 999);
      }

      list.sort((a, b) => {
        const bA = bary.get(a.id) ?? 999;
        const bB = bary.get(b.id) ?? 999;
        if (bA !== bB) return bA - bB;
        return (a?.name || '').localeCompare(b?.name || '');
      });
      levelNodesMap.set(lvl, list);
    }

    // If rootNode shares a column with other nodes, place it in the center of that column
    for (const [lvl, list] of levelNodesMap.entries()) {
      const rootIndex = list.findIndex((n) => n.id === rootNode!.id || n.name === rootNode!.name);
      if (rootIndex >= 0 && list.length > 1) {
        const others = list.filter((n) => n.id !== rootNode!.id && n.name !== rootNode!.name);
        const mid = Math.floor(others.length / 2);
        levelNodesMap.set(lvl, [
          ...others.slice(0, mid),
          rootNode!,
          ...others.slice(mid),
        ]);
      }
    }

    // ----------------------------------------------------
    // Phase 5: Coordinates Calculation & Dynamic Columns Placement
    // ----------------------------------------------------
    const activeLevels = Array.from(levelNodesMap.keys()).sort((a, b) => a - b);
    const colStep = 340;
    const colGap = 24;

    const getNodeHeight = (node: GraphNode) => {
      const isExp = expandedCards.has(node.id) || expandedCards.has(node.name);
      return isExp ? 225 : 62;
    };

    let maxColHeight = 60;
    for (const list of levelNodesMap.values()) {
      const h = list.reduce((sum, n) => sum + getNodeHeight(n), 0) + Math.max(0, list.length - 1) * colGap;
      if (h > maxColHeight) maxColHeight = h;
    }
    const maxCenterY = Math.max(60, maxColHeight / 2);

    const generatedNodes: Node[] = [];

    for (let colIdx = 0; colIdx < activeLevels.length; colIdx++) {
      const lvl = activeLevels[colIdx];
      const list = levelNodesMap.get(lvl) || [];
      const colX = 60 + colIdx * colStep;

      const nodeHeights = list.map((n) => getNodeHeight(n));
      const colTotalHeight = nodeHeights.reduce((sum, h) => sum + h, 0) + Math.max(0, list.length - 1) * colGap;
      const startY = Math.max(60, maxCenterY - colTotalHeight / 2);

      let currentY = startY;

      list.forEach((node, i) => {
        const isCenter = node.id === rootNode!.id || node.name === rootNode!.name;
        const inCount = inCountMap[node.id] || inCountMap[node.name] || 0;
        const outCount = outCountMap[node.id] || outCountMap[node.name] || 0;
        const h = nodeHeights[i];

        generatedNodes.push({
          id: node.id,
          type: 'projectCard',
          position: { x: colX, y: currentY },
          data: {
            graphNode: node,
            level: lvl,
            isCenter,
            inCount,
            outCount,
            isExpanded: expandedCards.has(node.id) || expandedCards.has(node.name),
            onToggleExpand: handleToggleCardExpand,
            comms: commsMap.get(node.id) || commsMap.get(node.name),
            activeCategories: Array.from(getActiveCategories(node.id, node.name)),
            onToggleCategory: handleToggleCategory,
            onFocusProject: onSelectProject,
            onOpenFile,
          },
        });

        currentY += h + colGap;
      });
    }

    setNodes(generatedNodes);
    setEdges(generatedEdges);

    // Smooth re-fit after layout calculation
    setTimeout(() => {
      fitView({ padding: 0.25, duration: 300 });
    }, 40);
  }, [
    rootProjectName,
    expandedCards,
    expandedCategories,
    visibleEdgeTypes,
    nodeMap,
    outboundMap,
    inboundMap,
    inCountMap,
    outCountMap,
    edgeLookup,
    commsMap,
    getActiveCategories,
    handleToggleCategory,
    handleToggleCardExpand,
    onSelectProject,
    onOpenFile,
    fitView,
  ]);

  return (
    <div className="flow-canvas-container">
      {!rootProjectName ? (
        <div className="flow-empty-state">
          <div className="empty-state-icon">🔀</div>
          <h3 className="empty-state-title">Select a Project to Inspect</h3>
          <p className="empty-state-desc">
            Choose a project from the top toolbar selector or click <strong>Flow</strong> on any service card in System Layers to explore its topological dependency graph.
          </p>
        </div>
      ) : (
        <>
          {/* Floating Progressive Levels Controls Overlay */}
          <div className="flow-levels-hud">
            <div className="hud-title-badge">
              <span className="hud-label">Project Dependency Flow</span>
              <span className="hud-target-name">{rootProjectName}</span>
            </div>
            <div className="hud-actions">
              <button
                className="hud-action-btn"
                onClick={handleResetLevels}
                title="Reset to direct 1-hop callers and dependencies"
              >
                Reset 1-Hop
              </button>
              <button
                className="hud-action-btn"
                onClick={() => fitView({ padding: 0.25, duration: 300 })}
                title="Center and fit diagram to view"
              >
                Fit
              </button>
            </div>
          </div>

          {/* Floating Connection Types Legend */}
          <div className="flow-edge-legend">
        <div className="edge-legend-title">Connection Types</div>
        <div className="edge-legend-items">
          <label className={`edge-legend-item ${!visibleEdgeTypes.library ? 'is-dimmed' : ''}`} title="Toggle Library connections">
            <input
              type="checkbox"
              className="edge-legend-checkbox"
              checked={visibleEdgeTypes.library}
              onChange={() => handleToggleEdgeType('library')}
            />
            <svg width="34" height="12" viewBox="0 0 34 12" style={{ flexShrink: 0 }}>
              <line x1="0" y1="6" x2="24" y2="6" stroke="#34d399" strokeWidth="1" />
              <polyline points="22 3, 29 6, 22 9" fill="none" stroke="#34d399" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
            <span className="edge-legend-label">Library (Open Chevron)</span>
          </label>
          <label className={`edge-legend-item ${!visibleEdgeTypes.service_call ? 'is-dimmed' : ''}`} title="Toggle Service Call connections">
            <input
              type="checkbox"
              className="edge-legend-checkbox"
              checked={visibleEdgeTypes.service_call}
              onChange={() => handleToggleEdgeType('service_call')}
            />
            <svg width="34" height="12" viewBox="0 0 34 12" style={{ flexShrink: 0 }}>
              <line x1="0" y1="6" x2="22" y2="6" stroke="#38bdf8" strokeWidth="2" strokeDasharray="4,3" />
              <polygon points="21 2, 32 6, 21 10" fill="#38bdf8" />
            </svg>
            <span className="edge-legend-label">Service Call (Solid Arrow)</span>
          </label>
          <label className={`edge-legend-item ${!visibleEdgeTypes.database ? 'is-dimmed' : ''}`} title="Toggle Database connections">
            <input
              type="checkbox"
              className="edge-legend-checkbox"
              checked={visibleEdgeTypes.database}
              onChange={() => handleToggleEdgeType('database')}
            />
            <svg width="34" height="12" viewBox="0 0 34 12" style={{ flexShrink: 0 }}>
              <line x1="0" y1="6" x2="24" y2="6" stroke="#c084fc" strokeWidth="2" strokeDasharray="2,3" />
              <polygon points="23 6, 27 2, 31 6, 27 10" fill="#c084fc" />
            </svg>
            <span className="edge-legend-label">Database</span>
          </label>
          <label className={`edge-legend-item ${!visibleEdgeTypes.messaging ? 'is-dimmed' : ''}`} title="Toggle Event/Queue connections">
            <input
              type="checkbox"
              className="edge-legend-checkbox"
              checked={visibleEdgeTypes.messaging}
              onChange={() => handleToggleEdgeType('messaging')}
            />
            <svg width="34" height="12" viewBox="0 0 34 12" style={{ flexShrink: 0 }}>
              <line x1="0" y1="6" x2="24" y2="6" stroke="#fbbf24" strokeWidth="2" strokeDasharray="6,2,2,2" />
              <polyline points="22 2, 30 6, 22 10" fill="none" stroke="#fbbf24" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
            <span className="edge-legend-label">Event / Queue</span>
          </label>
        </div>
      </div>

      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        nodeTypes={nodeTypes}
        nodesConnectable={false}
        autoPanOnConnect={false}
        connectOnClick={false}
        fitView
        fitViewOptions={{ padding: 0.25 }}
        minZoom={0.2}
        maxZoom={2}
      >
        <Background color="rgba(255,255,255,0.06)" gap={20} />
        <Controls />
        <MiniMap
          nodeColor={(n) => {
            const isCenter = (n.data as any)?.isCenter;
            const lvl = (n.data as any)?.level;
            return isCenter ? '#c084fc' : lvl < 0 ? '#38bdf8' : '#34d399';
          }}
          maskColor="rgba(0, 0, 0, 0.6)"
          style={{ background: '#1e1e1e', border: '1px solid #333' }}
        />
      </ReactFlow>
      </>
    )}
  </div>
);
};

export const ProjectFlowView: React.FC<ProjectFlowViewProps> = (props) => {
  return (
    <ReactFlowProvider>
      <FlowInner {...props} />
    </ReactFlowProvider>
  );
};
