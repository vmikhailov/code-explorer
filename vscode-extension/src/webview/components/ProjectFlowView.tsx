import React, { useState, useEffect, useMemo, useCallback, useRef } from 'react';
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
import { GraphData, GraphNode } from '../../../../proto/types';
import { ProjectCardNode } from './ProjectCardNode';

export interface ProjectFlowViewProps {
  graph: GraphData | null;
  fullGraph?: GraphData | null;
  onSelectProject: (name: string) => void;
  onOpenFile: (filePath: string, lineStart?: number) => void;
}

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

  // Track which projects have their outbound (using) or inbound (used by) levels expanded
  const [expandedOutbound, setExpandedOutbound] = useState<Set<string>>(new Set());
  const [expandedInbound, setExpandedInbound] = useState<Set<string>>(new Set());

  // Build comprehensive graph lookup from fullGraph (fallback to graph)
  const { nodeMap, outboundMap, inboundMap, inCountMap, outCountMap } = useMemo(() => {
    const dataSource = fullGraph && fullGraph.nodes && fullGraph.nodes.length > 0 ? fullGraph : graph;

    const nMap = new Map<string, GraphNode>();
    const outMap = new Map<string, GraphNode[]>();
    const inMap = new Map<string, GraphNode[]>();
    const inc: Record<string, number> = {};
    const outc: Record<string, number> = {};

    for (const n of dataSource?.nodes || []) {
      nMap.set(n.id, n);
      nMap.set(n.name, n);
      outMap.set(n.id, []);
      outMap.set(n.name, []);
      inMap.set(n.id, []);
      inMap.set(n.name, []);
    }

    // Also include any nodes from graph (if neighborhood had extra info)
    for (const n of graph?.nodes || []) {
      if (!nMap.has(n.id)) nMap.set(n.id, n);
      if (!nMap.has(n.name)) nMap.set(n.name, n);
      if (!outMap.has(n.id)) {
        outMap.set(n.id, []);
        outMap.set(n.name, []);
      }
      if (!inMap.has(n.id)) {
        inMap.set(n.id, []);
        inMap.set(n.name, []);
      }
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

    for (const e of allEdges) {
      const srcNode = nMap.get(e.source);
      const tgtNode = nMap.get(e.target);
      if (!srcNode || !tgtNode) continue;

      // Outbound (srcNode is using tgtNode)
      const curOut = outMap.get(srcNode.id) || [];
      if (!curOut.some((x) => x.id === tgtNode.id || x.name === tgtNode.name)) {
        curOut.push(tgtNode);
        outMap.set(srcNode.id, curOut);
        if (srcNode.name !== srcNode.id) outMap.set(srcNode.name, curOut);
      }

      // Inbound (tgtNode is used by srcNode)
      const curIn = inMap.get(tgtNode.id) || [];
      if (!curIn.some((x) => x.id === srcNode.id || x.name === srcNode.name)) {
        curIn.push(srcNode);
        inMap.set(tgtNode.id, curIn);
        if (tgtNode.name !== tgtNode.id) inMap.set(tgtNode.name, curIn);
      }
    }

    for (const n of nMap.values()) {
      const outList = outMap.get(n.id) || [];
      const inList = inMap.get(n.id) || [];
      outc[n.id] = outList.length;
      outc[n.name] = outList.length;
      inc[n.id] = inList.length;
      inc[n.name] = inList.length;
    }

    return { nodeMap: nMap, outboundMap: outMap, inboundMap: inMap, inCountMap: inc, outCountMap: outc };
  }, [fullGraph, graph]);

  // Whenever the root target project changes, initialize its direct levels (+1 and -1) as expanded
  const lastRootRef = useRef<string>('');
  useEffect(() => {
    if (rootProjectName && rootProjectName !== lastRootRef.current) {
      lastRootRef.current = rootProjectName;
      const rootNode = nodeMap.get(rootProjectName);
      const setOut = new Set<string>([rootProjectName]);
      const setIn = new Set<string>([rootProjectName]);
      if (rootNode) {
        setOut.add(rootNode.id);
        setIn.add(rootNode.id);
      }
      setExpandedOutbound(setOut);
      setExpandedInbound(setIn);
    }
  }, [rootProjectName, nodeMap]);

  // Toggle outbound dependencies (using) for a project
  const handleToggleOutbound = useCallback((projectName: string, projectId?: string) => {
    setExpandedOutbound((prev) => {
      const next = new Set(prev);
      const isPresent = next.has(projectName) || (projectId && next.has(projectId));
      if (isPresent) {
        next.delete(projectName);
        if (projectId) next.delete(projectId);
      } else {
        next.add(projectName);
        if (projectId) next.add(projectId);
      }
      return next;
    });
  }, []);

  // Toggle inbound callers (used by) for a project
  const handleToggleInbound = useCallback((projectName: string, projectId?: string) => {
    setExpandedInbound((prev) => {
      const next = new Set(prev);
      const isPresent = next.has(projectName) || (projectId && next.has(projectId));
      if (isPresent) {
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
      const setOut = new Set<string>([rootProjectName]);
      const setIn = new Set<string>([rootProjectName]);
      if (rootNode) {
        setOut.add(rootNode.id);
        setIn.add(rootNode.id);
      }
      setExpandedOutbound(setOut);
      setExpandedInbound(setIn);
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
    // Phase 1: Progressive Expansion of Visible Nodes
    // ----------------------------------------------------
    const visibleNodesMap = new Map<string, GraphNode>();
    visibleNodesMap.set(rootNode.id, rootNode);
    if (rootNode.name !== rootNode.id) {
      visibleNodesMap.set(rootNode.name, rootNode);
    }

    // Iterative expansion for any visible node whose outbound or inbound is toggled on
    const processedOut = new Set<string>();
    const processedIn = new Set<string>();

    for (let round = 0; round < 10; round++) {
      let newlyAdded = false;
      const currentNodes = Array.from(new Set(visibleNodesMap.values()));

      for (const curr of currentNodes) {
        // Outbound expansion (curr uses children)
        const isExpOut = expandedOutbound.has(curr.id) || expandedOutbound.has(curr.name);
        if (isExpOut && !processedOut.has(curr.id)) {
          processedOut.add(curr.id);
          processedOut.add(curr.name);
          const children = outboundMap.get(curr.id) || outboundMap.get(curr.name) || [];
          for (const child of children) {
            if (!visibleNodesMap.has(child.id) && !visibleNodesMap.has(child.name)) {
              visibleNodesMap.set(child.id, child);
              if (child.name !== child.id) visibleNodesMap.set(child.name, child);
              newlyAdded = true;
            }
          }
        }

        // Inbound expansion (parents use curr)
        const isExpIn = expandedInbound.has(curr.id) || expandedInbound.has(curr.name);
        if (isExpIn && !processedIn.has(curr.id)) {
          processedIn.add(curr.id);
          processedIn.add(curr.name);
          const parents = inboundMap.get(curr.id) || inboundMap.get(curr.name) || [];
          for (const parent of parents) {
            if (!visibleNodesMap.has(parent.id) && !visibleNodesMap.has(parent.name)) {
              visibleNodesMap.set(parent.id, parent);
              if (parent.name !== parent.id) visibleNodesMap.set(parent.name, parent);
              newlyAdded = true;
            }
          }
        }
      }

      if (!newlyAdded) break;
    }

    const uniqueVisible = Array.from(new Set(visibleNodesMap.values()));
    const visibleIdSet = new Set<string>();
    for (const n of uniqueVisible) {
      visibleIdSet.add(n.id);
      visibleIdSet.add(n.name);
    }

    // ----------------------------------------------------
    // Phase 2: Collect All Directed Edges between Visible Nodes
    // ----------------------------------------------------
    const generatedEdges: Edge[] = [];
    const edgeKeySet = new Set<string>();
    const visibleEdgePairs: Array<{ sourceNode: GraphNode; targetNode: GraphNode }> = [];

    const addEdge = (sourceId: string, targetId: string) => {
      const key = `${sourceId}->${targetId}`;
      if (!edgeKeySet.has(key)) {
        edgeKeySet.add(key);
        generatedEdges.push({
          id: `edge-${key}`,
          source: sourceId,
          target: targetId,
          type: 'bezier',
          animated: false,
          style: { stroke: '#64748b', strokeWidth: 1.2 },
          markerEnd: {
            type: MarkerType.ArrowClosed,
            color: '#64748b',
            width: 9,
            height: 9,
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
          visibleEdgePairs.push({
            sourceNode: u,
            targetNode: child,
          });
        }
      }
    }

    // ----------------------------------------------------
    // Phase 3: DAG Topological Rank & Longest-Path Layering
    // Pushes dependencies to the right (level(target) >= level(source) + 1)
    // so no connections ever exist on the same level!
    // ----------------------------------------------------
    const nodeLevelMap = new Map<string, number>();
    const setNodeLevel = (node: GraphNode, lvl: number) => {
      nodeLevelMap.set(node.id, lvl);
      nodeLevelMap.set(node.name, lvl);
    };
    const getNodeLevel = (node: GraphNode): number => {
      return nodeLevelMap.get(node.id) ?? nodeLevelMap.get(node.name) ?? 0;
    };

    // Root is permanently pinned at Level 0
    setNodeLevel(rootNode, 0);

    // Initial base levels relative to root
    for (const n of uniqueVisible) {
      if (n.id === rootNode.id || n.name === rootNode.name) continue;
      setNodeLevel(n, 1);
    }

    // Upstream BFS to mark callers of root as negative
    const upstreamQueue: GraphNode[] = [rootNode];
    const visitedUpstream = new Set<string>([rootNode.id, rootNode.name]);
    while (upstreamQueue.length > 0) {
      const curr = upstreamQueue.shift()!;
      const currLvl = getNodeLevel(curr);
      const parents = inboundMap.get(curr.id) || inboundMap.get(curr.name) || [];
      for (const p of parents) {
        if (visibleIdSet.has(p.id) || visibleIdSet.has(p.name)) {
          if (!visitedUpstream.has(p.id)) {
            visitedUpstream.add(p.id);
            visitedUpstream.add(p.name);
            setNodeLevel(p, currLvl - 1);
            upstreamQueue.push(p);
          }
        }
      }
    }

    // Forward BFS for downstream nodes starting from root (Level 0)
    const downstreamQueue: GraphNode[] = [rootNode];
    const visitedDownstream = new Set<string>([rootNode.id, rootNode.name]);
    while (downstreamQueue.length > 0) {
      const curr = downstreamQueue.shift()!;
      const currLvl = getNodeLevel(curr);
      const children = outboundMap.get(curr.id) || outboundMap.get(curr.name) || [];
      for (const c of children) {
        if (visibleIdSet.has(c.id) || visibleIdSet.has(c.name)) {
          if (!visitedDownstream.has(c.id)) {
            visitedDownstream.add(c.id);
            visitedDownstream.add(c.name);
            setNodeLevel(c, currLvl + 1);
            downstreamQueue.push(c);
          }
        }
      }
    }

    // Relaxation passes:
    // For every edge u -> v: enforce level(v) >= level(u) + 1!
    // Never allow u and v to be on the same level or backwards.
    const maxIterations = Math.max(12, uniqueVisible.length);
    for (let iter = 0; iter < maxIterations; iter++) {
      let changed = false;

      for (const pair of visibleEdgePairs) {
        const srcLvl = getNodeLevel(pair.sourceNode);
        const tgtLvl = getNodeLevel(pair.targetNode);

        // If target is not strictly to the right of source:
        if (tgtLvl <= srcLvl) {
          // If source is at or to the right of root (>= 0), push target to the right
          if (srcLvl >= 0) {
            const newTgtLvl = Math.min(6, srcLvl + 1);
            if (newTgtLvl !== tgtLvl) {
              setNodeLevel(pair.targetNode, newTgtLvl);
              changed = true;
            }
          } else if (tgtLvl <= 0) {
            // Both are to the left of root (<= 0), push source to the left
            const newSrcLvl = Math.max(-6, tgtLvl - 1);
            if (newSrcLvl !== srcLvl && pair.sourceNode.id !== rootNode.id && pair.sourceNode.name !== rootNode.name) {
              setNodeLevel(pair.sourceNode, newSrcLvl);
              changed = true;
            }
          }
        }
      }

      // Root is always pinned at level 0
      setNodeLevel(rootNode, 0);

      if (!changed) break;
    }

    // Group unique nodes by level
    const levelNodesMap = new Map<number, GraphNode[]>();
    for (const node of uniqueVisible) {
      const lvl = getNodeLevel(node);
      const list = levelNodesMap.get(lvl) || [];
      list.push(node);
      levelNodesMap.set(lvl, list);
    }

    // In Level 0, if there are multiple nodes (e.g. callers of a Level 1 dependency),
    // ensure rootNode is vertically centered in the column so it lines up with its dependencies.
    const listAtZero = levelNodesMap.get(0) || [];
    if (listAtZero.length > 1) {
      const otherNodes = listAtZero.filter(
        (n) => n.id !== rootNode!.id && n.name !== rootNode!.name
      );
      otherNodes.sort((a, b) => a.name.localeCompare(b.name));
      const mid = Math.floor(otherNodes.length / 2);
      const reordered = [
        ...otherNodes.slice(0, mid),
        rootNode,
        ...otherNodes.slice(mid),
      ];
      levelNodesMap.set(0, reordered);
    }

    // ----------------------------------------------------
    // Phase 4: Barycentric Crossing Minimization
    // Reorders nodes in each column according to average connected neighbor positions
    // to minimize edge crossings.
    // ----------------------------------------------------
    const sortedLevels = Array.from(levelNodesMap.keys()).sort((a, b) => a - b);

    // Forward sweep (from Level 1 to max level):
    // Align downstream targets with the vertical order of their upstream sources.
    for (const lvl of sortedLevels) {
      if (lvl <= 0) continue;
      const list = levelNodesMap.get(lvl) || [];
      if (list.length <= 1) continue;

      const barycenterMap = new Map<string, number>();
      for (const node of list) {
        const sources = inboundMap.get(node.id) || inboundMap.get(node.name) || [];
        const visibleSources = sources.filter(
          (s) => visibleIdSet.has(s.id) && getNodeLevel(s) < lvl
        );

        if (visibleSources.length > 0) {
          let sumRank = 0;
          for (const s of visibleSources) {
            const sLvl = getNodeLevel(s);
            const sList = levelNodesMap.get(sLvl) || [];
            const sIndex = sList.findIndex((n) => n.id === s.id || n.name === s.name);
            sumRank += sIndex >= 0 ? sIndex : 0;
          }
          barycenterMap.set(node.id, sumRank / visibleSources.length);
        } else {
          barycenterMap.set(node.id, 999);
        }
      }

      list.sort((a, b) => {
        const bA = barycenterMap.get(a.id) ?? 999;
        const bB = barycenterMap.get(b.id) ?? 999;
        if (bA !== bB) return bA - bB;
        return a.name.localeCompare(b.name);
      });
      levelNodesMap.set(lvl, list);
    }

    // Backward sweep (from Level -1 down to min level):
    // Align upstream callers with the vertical order of their downstream targets.
    for (const lvl of [...sortedLevels].reverse()) {
      if (lvl >= 0) continue;
      const list = levelNodesMap.get(lvl) || [];
      if (list.length <= 1) continue;

      const barycenterMap = new Map<string, number>();
      for (const node of list) {
        const targets = outboundMap.get(node.id) || outboundMap.get(node.name) || [];
        const visibleTargets = targets.filter(
          (t) => visibleIdSet.has(t.id) && getNodeLevel(t) > lvl
        );

        if (visibleTargets.length > 0) {
          let sumRank = 0;
          for (const t of visibleTargets) {
            const tLvl = getNodeLevel(t);
            const tList = levelNodesMap.get(tLvl) || [];
            const tIndex = tList.findIndex((n) => n.id === t.id || n.name === t.name);
            sumRank += tIndex >= 0 ? tIndex : 0;
          }
          barycenterMap.set(node.id, sumRank / visibleTargets.length);
        } else {
          barycenterMap.set(node.id, 999);
        }
      }

      list.sort((a, b) => {
        const bA = barycenterMap.get(a.id) ?? 999;
        const bB = barycenterMap.get(b.id) ?? 999;
        if (bA !== bB) return bA - bB;
        return a.name.localeCompare(b.name);
      });
      levelNodesMap.set(lvl, list);
    }

    // Compute Coordinates with increased breathing room
    const activeLevels = Array.from(levelNodesMap.keys()).sort((a, b) => a - b);
    const minLevel = activeLevels[0] ?? 0;

    const colStep = 340;
    const rowHeight = 120;
    const xBaseOffset = Math.abs(Math.min(0, minLevel)) * colStep + 60;

    // Find maximum level height to vertically balance columns
    let maxRowCount = 1;
    for (const list of levelNodesMap.values()) {
      if (list.length > maxRowCount) maxRowCount = list.length;
    }
    const maxCenterY = Math.max(60, (maxRowCount * rowHeight) / 2);

    const generatedNodes: Node[] = [];

    for (const lvl of activeLevels) {
      const list = levelNodesMap.get(lvl) || [];
      const colX = xBaseOffset + lvl * colStep;
      const colHeight = list.length * rowHeight;
      const startY = Math.max(60, maxCenterY - colHeight / 2);

      list.forEach((node, i) => {
        const isCenter = node.id === rootNode!.id || node.name === rootNode!.name;
        const inCount = inCountMap[node.id] || inCountMap[node.name] || 0;
        const outCount = outCountMap[node.id] || outCountMap[node.name] || 0;
        const isOutboundExp = expandedOutbound.has(node.id) || expandedOutbound.has(node.name);
        const isInboundExp = expandedInbound.has(node.id) || expandedInbound.has(node.name);

        generatedNodes.push({
          id: node.id,
          type: 'projectCard',
          position: { x: colX, y: startY + i * rowHeight },
          data: {
            graphNode: node,
            level: lvl,
            isCenter,
            inCount,
            outCount,
            isInboundExpanded: isInboundExp,
            isOutboundExpanded: isOutboundExp,
            onToggleInbound: handleToggleInbound,
            onToggleOutbound: handleToggleOutbound,
            onFocusProject: onSelectProject,
            onOpenFile,
          },
        });
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
    expandedOutbound,
    expandedInbound,
    nodeMap,
    outboundMap,
    inboundMap,
    inCountMap,
    outCountMap,
    handleToggleInbound,
    handleToggleOutbound,
    onSelectProject,
    onOpenFile,
    fitView,
  ]);

  return (
    <div className="flow-canvas-container">
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
