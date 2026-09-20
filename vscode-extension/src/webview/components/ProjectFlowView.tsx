import React, { useMemo, useEffect } from 'react';
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
  onSelectProject: (name: string) => void;
  onOpenFile: (filePath: string, lineStart?: number) => void;
}

const nodeTypes = {
  projectCard: ProjectCardNode as any,
};

const FlowInner: React.FC<ProjectFlowViewProps> = ({ graph, onSelectProject, onOpenFile }) => {
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const { fitView } = useReactFlow();

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

    // Fallback if center was not explicitly tagged
    if (!centerNode && graph.nodes.length > 0) {
      centerNode = graph.nodes[0];
    }

    const maxRows = Math.max(leftNodes.length, rightNodes.length, 1);
    const rowHeight = 130;
    const totalHeight = maxRows * rowHeight;
    const centerCardY = Math.max(40, totalHeight / 2 - 50);

    const newNodes: Node[] = [];

    // Left Column: Inbound Callers
    leftNodes.forEach((node, i) => {
      newNodes.push({
        id: node.id,
        type: 'projectCard',
        position: { x: 60, y: i * rowHeight + 40 },
        data: {
          graphNode: node,
          column: 'left',
          isCenter: false,
          onSelectProject,
          onOpenFile,
        },
      });
    });

    // Center Column: Hero Node
    if (centerNode) {
      newNodes.push({
        id: centerNode.id,
        type: 'projectCard',
        position: { x: 420, y: centerCardY },
        data: {
          graphNode: centerNode,
          column: 'center',
          isCenter: true,
          onSelectProject,
          onOpenFile,
        },
      });
    }

    // Right Column: Outbound Dependencies
    rightNodes.forEach((node, i) => {
      newNodes.push({
        id: node.id,
        type: 'projectCard',
        position: { x: 780, y: i * rowHeight + 40 },
        data: {
          graphNode: node,
          column: 'right',
          isCenter: false,
          onSelectProject,
          onOpenFile,
        },
      });
    });

    // Edges
    const newEdges: Edge[] = (graph.edges || []).map((e, idx) => ({
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
  }, [graph, onSelectProject, onOpenFile, fitView]);

  return (
    <div className="flow-canvas-container">
      {/* Column Titles Overlay */}
      <div className="column-headers-overlay">
        <div className="col-header col-left">
          <span className="col-tag">INBOUND</span>
          <span className="col-title">Who Calls This Project</span>
        </div>
        <div className="col-header col-center">
          <span className="col-tag">FOCUS</span>
          <span className="col-title">Selected Project</span>
        </div>
        <div className="col-header col-right">
          <span className="col-tag">OUTBOUND</span>
          <span className="col-title">Dependencies &amp; Resources</span>
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
