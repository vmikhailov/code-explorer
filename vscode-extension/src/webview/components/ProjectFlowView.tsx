import React, { useState, useEffect, useCallback } from 'react';
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
  onSelectProject: (name: string, direction?: 'all' | 'using' | 'used_by') => void;
  onOpenFile: (filePath: string, lineStart?: number) => void;
  projectInCounts?: Record<string, number>;
  projectOutCounts?: Record<string, number>;
}

const nodeTypes = {
  projectCard: ProjectCardNode as any,
};

const FlowInner: React.FC<ProjectFlowViewProps> = ({
  graph,
  onSelectProject,
  onOpenFile,
  projectInCounts = {},
  projectOutCounts = {},
}) => {
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const { fitView } = useReactFlow();

  const [directionFilter, setDirectionFilter] = useState<'all' | 'using' | 'used_by'>('all');

  const handleNavigate = useCallback(
    (name: string, direction: 'all' | 'using' | 'used_by') => {
      setDirectionFilter(direction);
      onSelectProject(name, direction);
    },
    [onSelectProject]
  );

  useEffect(() => {
    if (!graph || !graph.nodes || graph.nodes.length === 0) {
      setNodes([]);
      setEdges([]);
      return;
    }

    const leftNodes: GraphNode[] = [];
    let centerNode: GraphNode | null = null;
    const rightNodes: GraphNode[] = [];

    for (const node of graph.nodes) {
      const col = node.properties?.column;
      if (col === 'center') {
        centerNode = node;
      } else if (col === 'left') {
        leftNodes.push(node);
      } else if (col === 'right') {
        rightNodes.push(node);
      }
    }

    if (!centerNode && graph.nodes.length > 0) {
      centerNode = graph.nodes[0];
    }

    const showLeft = directionFilter === 'all' || directionFilter === 'used_by';
    const showRight = directionFilter === 'all' || directionFilter === 'using';

    const visibleLeft = showLeft ? leftNodes : [];
    const visibleRight = showRight ? rightNodes : [];

    const maxRows = Math.max(visibleLeft.length, visibleRight.length, 1);
    const rowHeight = 150;
    const totalHeight = maxRows * rowHeight;
    const centerCardY = Math.max(40, totalHeight / 2 - 50);

    const newNodes: Node[] = [];

    // Determine X positions based on active filter
    let leftX = 60;
    let centerX = 440;
    let rightX = 820;

    if (directionFilter === 'using') {
      centerX = 120;
      rightX = 540;
    } else if (directionFilter === 'used_by') {
      leftX = 120;
      centerX = 540;
    }

    // Left Column: Inbound Callers (Used by)
    if (showLeft) {
      visibleLeft.forEach((node, i) => {
        const inCount = projectInCounts[node.id] || projectInCounts[node.name] || 0;
        const outCount = projectOutCounts[node.id] || projectOutCounts[node.name] || 0;
        newNodes.push({
          id: node.id,
          type: 'projectCard',
          position: { x: leftX, y: i * rowHeight + 40 },
          data: {
            graphNode: node,
            column: 'left',
            isCenter: false,
            inCount,
            outCount,
            onNavigate: handleNavigate,
            onOpenFile,
          },
        });
      });
    }

    // Center Column: Hero Target Node
    if (centerNode) {
      const inCount = projectInCounts[centerNode.id] || projectInCounts[centerNode.name] || 0;
      const outCount = projectOutCounts[centerNode.id] || projectOutCounts[centerNode.name] || 0;
      newNodes.push({
        id: centerNode.id,
        type: 'projectCard',
        position: { x: centerX, y: centerCardY },
        data: {
          graphNode: centerNode,
          column: 'center',
          isCenter: true,
          inCount,
          outCount,
          onNavigate: handleNavigate,
          onOpenFile,
        },
      });
    }

    // Right Column: Outbound Dependencies (Using)
    if (showRight) {
      visibleRight.forEach((node, i) => {
        const inCount = projectInCounts[node.id] || projectInCounts[node.name] || 0;
        const outCount = projectOutCounts[node.id] || projectOutCounts[node.name] || 0;
        newNodes.push({
          id: node.id,
          type: 'projectCard',
          position: { x: rightX, y: i * rowHeight + 40 },
          data: {
            graphNode: node,
            column: 'right',
            isCenter: false,
            inCount,
            outCount,
            onNavigate: handleNavigate,
            onOpenFile,
          },
        });
      });
    }

    const activeNodeIds = new Set(newNodes.map((n) => n.id));

    // Edges
    const newEdges: Edge[] = (graph.edges || [])
      .filter((e) => activeNodeIds.has(e.source) && activeNodeIds.has(e.target))
      .map((e, idx) => ({
        id: e.id || `edge-${idx}`,
        source: e.source,
        target: e.target,
        type: 'smoothstep',
        animated: true,
        style: { stroke: '#38bdf8', strokeWidth: 2 },
        markerEnd: {
          type: MarkerType.ArrowClosed,
          color: '#38bdf8',
          width: 14,
          height: 14,
        },
      }));

    setNodes(newNodes);
    setEdges(newEdges);

    // Smooth fit view
    setTimeout(() => {
      fitView({ padding: 0.2, duration: 300 });
    }, 50);
  }, [
    graph,
    directionFilter,
    handleNavigate,
    onOpenFile,
    projectInCounts,
    projectOutCounts,
    fitView,
  ]);

  return (
    <div className="flow-canvas-container">
      {/* Column Titles & Direction Filter Overlay */}
      <div className="column-headers-overlay">
        <div className="flow-direction-bar">
          <button
            className={`direction-btn ${directionFilter === 'all' ? 'active' : ''}`}
            onClick={() => setDirectionFilter('all')}
            title="Show both inbound callers and outbound dependencies"
          >
            All Connections
          </button>
          <button
            className={`direction-btn used-by ${directionFilter === 'used_by' ? 'active' : ''}`}
            onClick={() => setDirectionFilter('used_by')}
            title="Focus on inbound callers (who uses this project)"
          >
            ← Used by
          </button>
          <button
            className={`direction-btn using ${directionFilter === 'using' ? 'active' : ''}`}
            onClick={() => setDirectionFilter('using')}
            title="Focus on outbound dependencies (what this project is using)"
          >
            Using →
          </button>
        </div>

        <div className="columns-titles-row">
          {(directionFilter === 'all' || directionFilter === 'used_by') && (
            <div className="col-header col-left">
              <span className="col-tag">INBOUND</span>
              <span className="col-title">Used by (Callers)</span>
            </div>
          )}
          <div className="col-header col-center">
            <span className="col-tag">FOCUS</span>
            <span className="col-title">Target Project</span>
          </div>
          {(directionFilter === 'all' || directionFilter === 'using') && (
            <div className="col-header col-right">
              <span className="col-tag">OUTBOUND</span>
              <span className="col-title">Using (Dependencies)</span>
            </div>
          )}
        </div>
      </div>

      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        nodeTypes={nodeTypes}
        fitView
        fitViewOptions={{ padding: 0.2 }}
        minZoom={0.2}
        maxZoom={2}
      >
        <Background color="rgba(255,255,255,0.06)" gap={20} />
        <Controls />
        <MiniMap
          nodeColor={(n) => {
            const col = (n.data as any)?.column;
            return col === 'center' ? '#c084fc' : col === 'left' ? '#38bdf8' : '#34d399';
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
