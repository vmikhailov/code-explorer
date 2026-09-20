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

  // Whenever the root target project changes, initialize its direct levels (+1 and -1) as expanded
  const lastRootRef = useRef<string>('');
  useEffect(() => {
    if (rootProjectName && rootProjectName !== lastRootRef.current) {
      lastRootRef.current = rootProjectName;
      setExpandedOutbound(new Set([rootProjectName]));
      setExpandedInbound(new Set([rootProjectName]));
    }
  }, [rootProjectName]);

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
      if (!outMap.has(n.id)) outMap.set(n.id, []);
      if (!inMap.has(n.id)) inMap.set(n.id, []);
    }

    for (const e of dataSource?.edges || []) {
      const srcNode = nMap.get(e.source);
      const tgtNode = nMap.get(e.target);
      if (!srcNode || !tgtNode) continue;

      // Outbound (srcNode is using tgtNode)
      const curOut = outMap.get(srcNode.id) || [];
      if (!curOut.some((x) => x.id === tgtNode.id)) {
        curOut.push(tgtNode);
        outMap.set(srcNode.id, curOut);
        if (srcNode.name !== srcNode.id) outMap.set(srcNode.name, curOut);
      }
      outc[srcNode.id] = (outc[srcNode.id] || 0) + 1;
      outc[srcNode.name] = (outc[srcNode.name] || 0) + 1;

      // Inbound (tgtNode is used by srcNode)
      const curIn = inMap.get(tgtNode.id) || [];
      if (!curIn.some((x) => x.id === srcNode.id)) {
        curIn.push(srcNode);
        inMap.set(tgtNode.id, curIn);
        if (tgtNode.name !== tgtNode.id) inMap.set(tgtNode.name, curIn);
      }
      inc[tgtNode.id] = (inc[tgtNode.id] || 0) + 1;
      inc[tgtNode.name] = (inc[tgtNode.name] || 0) + 1;
    }

    return { nodeMap: nMap, outboundMap: outMap, inboundMap: inMap, inCountMap: inc, outCountMap: outc };
  }, [fullGraph, graph]);

  // Toggle outbound dependencies (using) for a project
  const handleToggleOutbound = useCallback((projectName: string) => {
    setExpandedOutbound((prev) => {
      const next = new Set(prev);
      if (next.has(projectName)) {
        next.delete(projectName);
      } else {
        next.add(projectName);
      }
      return next;
    });
  }, []);

  // Toggle inbound callers (used by) for a project
  const handleToggleInbound = useCallback((projectName: string) => {
    setExpandedInbound((prev) => {
      const next = new Set(prev);
      if (next.has(projectName)) {
        next.delete(projectName);
      } else {
        next.add(projectName);
      }
      return next;
    });
  }, []);

  const handleResetLevels = useCallback(() => {
    if (rootProjectName) {
      setExpandedOutbound(new Set([rootProjectName]));
      setExpandedInbound(new Set([rootProjectName]));
    }
  }, [rootProjectName]);

  // Multi-Level Progressive Layout Construction
  useEffect(() => {
    const rootNode = nodeMap.get(rootProjectName);
    if (!rootNode) {
      setNodes([]);
      setEdges([]);
      return;
    }

    const levelNodesMap = new Map<number, GraphNode[]>();
    levelNodesMap.set(0, [rootNode]);

    const generatedEdges: Edge[] = [];
    const edgeKeySet = new Set<string>();

    const addEdge = (sourceId: string, targetId: string) => {
      const key = `${sourceId}->${targetId}`;
      if (!edgeKeySet.has(key)) {
        edgeKeySet.add(key);
        generatedEdges.push({
          id: `edge-${key}`,
          source: sourceId,
          target: targetId,
          type: 'smoothstep',
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

    // 1. Progressive Outbound Expansion (Levels +1, +2, +3 ...)
    let currentOutLevelNodes = [rootNode];
    let currentOutLevel = 1;
    const visitedOutbound = new Set<string>([rootNode.id, rootNode.name]);

    while (currentOutLevelNodes.length > 0 && currentOutLevel <= 6) {
      const nextOutLevelNodes: GraphNode[] = [];

      for (const parent of currentOutLevelNodes) {
        const isExp = expandedOutbound.has(parent.id) || expandedOutbound.has(parent.name);
        if (isExp) {
          const children = outboundMap.get(parent.id) || outboundMap.get(parent.name) || [];
          for (const child of children) {
            addEdge(parent.id, child.id);
            if (!visitedOutbound.has(child.id)) {
              visitedOutbound.add(child.id);
              visitedOutbound.add(child.name);
              nextOutLevelNodes.push(child);
            }
          }
        }
      }

      if (nextOutLevelNodes.length > 0) {
        levelNodesMap.set(currentOutLevel, nextOutLevelNodes);
        currentOutLevelNodes = nextOutLevelNodes;
        currentOutLevel++;
      } else {
        break;
      }
    }

    // 2. Progressive Inbound Expansion (Levels -1, -2, -3 ...)
    let currentInLevelNodes = [rootNode];
    let currentInLevel = -1;
    const visitedInbound = new Set<string>([rootNode.id, rootNode.name]);

    while (currentInLevelNodes.length > 0 && currentInLevel >= -6) {
      const nextInLevelNodes: GraphNode[] = [];

      for (const child of currentInLevelNodes) {
        const isExp = expandedInbound.has(child.id) || expandedInbound.has(child.name);
        if (isExp) {
          const parents = inboundMap.get(child.id) || inboundMap.get(child.name) || [];
          for (const parent of parents) {
            addEdge(parent.id, child.id);
            if (!visitedInbound.has(parent.id)) {
              visitedInbound.add(parent.id);
              visitedInbound.add(parent.name);
              nextInLevelNodes.push(parent);
            }
          }
        }
      }

      if (nextInLevelNodes.length > 0) {
        levelNodesMap.set(currentInLevel, nextInLevelNodes);
        currentInLevelNodes = nextInLevelNodes;
        currentInLevel--;
      } else {
        break;
      }
    }

    // 3. Compute Coordinates
    const activeLevels = Array.from(levelNodesMap.keys()).sort((a, b) => a - b);
    const minLevel = activeLevels[0] ?? 0;

    const colStep = 280;
    const rowHeight = 90;
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
        const isCenter = lvl === 0;
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
