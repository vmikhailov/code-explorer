import React, { useMemo, useState, useCallback, useEffect } from 'react';
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
import { GraphData, GraphNode, GraphEdge, isProjectKind } from '../../../../proto/types';
import {
  C1EndpointCardNode,
  C1ProjectCardNode,
  C1EgressCardNode,
  C1BoundaryCardNode,
} from './C1CardNodes';

export interface C1SystemContextViewProps {
  graph: GraphData | null;
  onDrillDownToC2?: (projectName: string) => void;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
}

const nodeTypes = {
  c1Endpoint: C1EndpointCardNode as any,
  c1Project: C1ProjectCardNode as any,
  c1Egress: C1EgressCardNode as any,
  c1Boundary: C1BoundaryCardNode as any,
};

function C1Canvas({
  graph,
  onDrillDownToC2,
  onOpenFile,
}: C1SystemContextViewProps) {
  const { fitView } = useReactFlow();
  const [searchQuery, setSearchQuery] = useState('');
  const [showEndpoints, setShowEndpoints] = useState(true);
  const [showExternal, setShowExternal] = useState(true);
  const [showDatabases, setShowDatabases] = useState(true);

  // Parse and categorize graph nodes
  const { ingressNodes, internalProjects, egressNodes, edgesList, systemName } = useMemo(() => {
    if (!graph || !graph.nodes) {
      return {
        ingressNodes: [] as GraphNode[],
        internalProjects: [] as GraphNode[],
        egressNodes: [] as { node: GraphNode; category: 'database' | 'external' | 'messaging' }[],
        edgesList: [] as GraphEdge[],
        systemName: 'System',
      };
    }

    const allNodes = graph.nodes;
    const allEdges = graph.edges || [];

    // System name from workspace or first project
    let sysName = 'CodeExplorer';
    const wsNode = allNodes.find((n) => n.kind === 'Workspace');
    if (wsNode?.name) {
      sysName = wsNode.name;
    } else if (allNodes.find((n) => isProjectKind(n.kind))) {
      const p = allNodes.find((n) => isProjectKind(n.kind))!;
      sysName = p.name.split('.')[0] || p.name;
    }

    // 1. Ingress Nodes (Endpoints & public pages)
    const ingress: GraphNode[] = [];
    // 2. Internal Projects
    const projects: GraphNode[] = [];
    // 3. Egress Nodes (Databases, External Services, Message Topics)
    const egress: { node: GraphNode; category: 'database' | 'external' | 'messaging' }[] = [];

    const dbSeen = new Set<string>();

    for (const node of allNodes) {
      const kind = (node.kind || '').toLowerCase();
      const name = node.name || '';
      const filePath = (node.filePath || '').toLowerCase();

      if (kind === 'endpoint' || node.id.includes('endpoint:') || filePath.endsWith('.cfm')) {
        ingress.push(node);
      } else if (isProjectKind(node.kind)) {
        projects.push(node);
      } else if (kind === 'database') {
        if (!dbSeen.has(name.toLowerCase())) {
          dbSeen.add(name.toLowerCase());
          egress.push({ node, category: 'database' });
        }
      } else if (kind === 'externalservice') {
        egress.push({ node, category: 'external' });
      } else if (kind === 'topic' || kind === 'queue') {
        egress.push({ node, category: 'messaging' });
      }
    }

    // If no explicit database nodes but there are tables, synthesize a database
    if (egress.filter((e) => e.category === 'database').length === 0) {
      const tableNodes = allNodes.filter((n) => n.kind === 'Table');
      if (tableNodes.length > 0) {
        egress.push({
          node: {
            id: 'workspace:synthetic-database',
            name: `${tableNodes.length} Database Tables`,
            kind: 'Database',
            properties: { db_type: 'Relational DB', table_count: String(tableNodes.length) },
          },
          category: 'database',
        });
      }
    }

    return {
      ingressNodes: ingress,
      internalProjects: projects,
      egressNodes: egress,
      edgesList: allEdges,
      systemName: sysName,
    };
  }, [graph]);

  // Layout calculations
  const { flowNodes, flowEdges } = useMemo(() => {
    const nodes: Node[] = [];
    const edges: Edge[] = [];

    const isMatch = (text?: string) => {
      if (!searchQuery) return true;
      return (text || '').toLowerCase().includes(searchQuery.toLowerCase());
    };

    // Filtered lists
    const activeIngress = showEndpoints ? ingressNodes.filter((n) => isMatch(n.name) || isMatch(n.filePath)) : [];
    const activeProjects = internalProjects.filter((n) => isMatch(n.name));
    const activeEgress = egressNodes.filter(
      (e) =>
        (e.category === 'database' ? showDatabases : showExternal) &&
        (isMatch(e.node.name) || isMatch(e.node.kind))
    );

    // Compute Inbound and Outbound counts for each project
    const projInMap = new Map<string, number>();
    const projOutMap = new Map<string, number>();
    const projIdSet = new Set(internalProjects.map((p) => p.id));
    const projNameSet = new Set(internalProjects.map((p) => p.name.toLowerCase()));

    for (const edge of edgesList) {
      if (projIdSet.has(edge.target)) {
        projInMap.set(edge.target, (projInMap.get(edge.target) || 0) + 1);
      }
      if (projIdSet.has(edge.source)) {
        projOutMap.set(edge.source, (projOutMap.get(edge.source) || 0) + 1);
      }
    }

    // Determine layout geometry
    const twoProjectCols = activeProjects.length > 6;
    const projCol1X = 460;
    const projCol2X = twoProjectCols ? 800 : 460;
    const egressX = twoProjectCols ? 1180 : 860;
    const ingressX = 60;

    const ingressItemHeight = 110;
    const projectItemHeight = 145;
    const egressItemHeight = 140;

    // 1. Position Ingress Nodes
    const ingressStartY = 60;
    activeIngress.forEach((node, idx) => {
      nodes.push({
        id: node.id,
        type: 'c1Endpoint',
        position: { x: ingressX, y: ingressStartY + idx * ingressItemHeight },
        data: {
          id: node.id,
          name: node.name,
          method: node.properties?.method,
          route: node.properties?.route,
          filePath: node.filePath,
          lineStart: node.lineStart,
          onOpenFile,
        },
      });
    });

    // 2. Position Internal Projects & System Boundary Frame
    const projectStartY = 80;
    const col1Projects = twoProjectCols
      ? activeProjects.slice(0, Math.ceil(activeProjects.length / 2))
      : activeProjects;
    const col2Projects = twoProjectCols
      ? activeProjects.slice(Math.ceil(activeProjects.length / 2))
      : [];

    col1Projects.forEach((proj, idx) => {
      nodes.push({
        id: proj.id,
        type: 'c1Project',
        position: { x: projCol1X, y: projectStartY + idx * projectItemHeight },
        data: {
          id: proj.id,
          name: proj.name,
          framework: proj.properties?.framework,
          projectType: proj.properties?.project_type,
          filePath: proj.filePath,
          inboundCalls: projInMap.get(proj.id) || 0,
          outboundCalls: projOutMap.get(proj.id) || 0,
          packageCount: proj.properties?.package_count ? parseInt(proj.properties.package_count, 10) : undefined,
          onDrillDown: onDrillDownToC2,
          onOpenFile,
        },
      });
    });

    col2Projects.forEach((proj, idx) => {
      nodes.push({
        id: proj.id,
        type: 'c1Project',
        position: { x: projCol2X, y: projectStartY + idx * projectItemHeight },
        data: {
          id: proj.id,
          name: proj.name,
          framework: proj.properties?.framework,
          projectType: proj.properties?.project_type,
          filePath: proj.filePath,
          inboundCalls: projInMap.get(proj.id) || 0,
          outboundCalls: projOutMap.get(proj.id) || 0,
          packageCount: proj.properties?.package_count ? parseInt(proj.properties.package_count, 10) : undefined,
          onDrillDown: onDrillDownToC2,
          onOpenFile,
        },
      });
    });

    // Add Boundary Frame Node behind projects
    if (activeProjects.length > 0) {
      const boundaryHeight = Math.max(
        col1Projects.length * projectItemHeight + 60,
        col2Projects.length * projectItemHeight + 60,
        220
      );
      const boundaryWidth = twoProjectCols ? 660 : 340;
      nodes.unshift({
        id: 'c1-system-boundary',
        type: 'c1Boundary',
        position: { x: projCol1X - 25, y: projectStartY - 45 },
        style: { width: boundaryWidth, height: boundaryHeight, zIndex: -1 },
        data: {
          systemName,
          projectCount: activeProjects.length,
        },
        draggable: false,
        selectable: false,
      });
    }

    // 3. Position Egress Nodes
    const egressStartY = 60;
    activeEgress.forEach(({ node, category }, idx) => {
      nodes.push({
        id: node.id,
        type: 'c1Egress',
        position: { x: egressX, y: egressStartY + idx * egressItemHeight },
        data: {
          id: node.id,
          name: node.name,
          category,
          subType: node.properties?.db_type || (category === 'external' ? 'REST API' : undefined),
          details: node.properties?.table_count ? `${node.properties.table_count} tables` : undefined,
          filePath: node.filePath,
          onOpenFile,
        },
      });
    });

    // 4. Edges Construction (Ingress -> System Projects -> Egress)
    const validNodeIds = new Set(nodes.map((n) => n.id));

    // A. Connect Endpoints to System Projects
    // If endpoint has EXPOSED_BY or DECLARED_IN, connect directly.
    // Otherwise, connect first endpoint to first web/api project.
    const apiProject = activeProjects.find(
      (p) =>
        p.name.toLowerCase().includes('api') ||
        p.name.toLowerCase().includes('ui') ||
        p.name.toLowerCase().includes('web') ||
        p.name.toLowerCase().includes('server')
    ) || activeProjects[0];

    activeIngress.forEach((ep) => {
      // Find edge in edgesList if exists
      const directEdge = edgesList.find(
        (e) => (e.source === ep.id || e.target === ep.id) && validNodeIds.has(e.source === ep.id ? e.target : e.source)
      );

      const targetId = directEdge
        ? directEdge.source === ep.id
          ? directEdge.target
          : directEdge.source
        : apiProject?.id;

      if (targetId && validNodeIds.has(targetId)) {
        edges.push({
          id: `c1-edge-${ep.id}-${targetId}`,
          source: ep.id,
          target: targetId,
          sourceHandle: 'egress',
          targetHandle: 'ingress',
          animated: true,
          style: { stroke: '#38bdf8', strokeWidth: 2 },
          markerEnd: {
            type: MarkerType.ArrowClosed,
            color: '#38bdf8',
            width: 16,
            height: 16,
          },
        });
      }
    });

    // B. Connect Projects to Egress (Databases & External Services)
    activeEgress.forEach(({ node, category }) => {
      // Find direct links from projects to this database/service
      const directLinks = edgesList.filter(
        (e) => (e.target === node.id || e.source === node.id) && validNodeIds.has(e.source === node.id ? e.target : e.source)
      );

      const color = category === 'database' ? '#f59e0b' : category === 'external' ? '#10b981' : '#a855f7';

      if (directLinks.length > 0) {
        directLinks.forEach((link) => {
          const sourceProjId = link.source === node.id ? link.target : link.source;
          edges.push({
            id: `c1-edge-${sourceProjId}-${node.id}`,
            source: sourceProjId,
            target: node.id,
            sourceHandle: 'egress',
            targetHandle: 'ingress',
            style: { stroke: color, strokeWidth: 2 },
            markerEnd: {
              type: MarkerType.ArrowClosed,
              color,
              width: 16,
              height: 16,
            },
          });
        });
      } else {
        // Fallback: connect from core project
        const coreProj = activeProjects.find((p) => p.name.toLowerCase().includes('core')) || activeProjects[0];
        if (coreProj && validNodeIds.has(coreProj.id)) {
          edges.push({
            id: `c1-edge-${coreProj.id}-${node.id}`,
            source: coreProj.id,
            target: node.id,
            sourceHandle: 'egress',
            targetHandle: 'ingress',
            style: { stroke: color, strokeWidth: 1.5, strokeDasharray: '4 4' },
            markerEnd: {
              type: MarkerType.ArrowClosed,
              color,
              width: 14,
              height: 14,
            },
          });
        }
      }
    });

    // C. Internal Project-to-Project Dependencies
    for (const edge of edgesList) {
      if (validNodeIds.has(edge.source) && validNodeIds.has(edge.target) && edge.source !== edge.target) {
        // Only if both are projects
        if (projIdSet.has(edge.source) && projIdSet.has(edge.target)) {
          edges.push({
            id: `c1-edge-${edge.source}-${edge.target}`,
            source: edge.source,
            target: edge.target,
            sourceHandle: 'egress',
            targetHandle: 'ingress',
            style: { stroke: '#818cf8', strokeWidth: 1.5, opacity: 0.8 },
            markerEnd: {
              type: MarkerType.ArrowClosed,
              color: '#818cf8',
              width: 12,
              height: 12,
            },
          });
        }
      }
    }

    return { flowNodes: nodes, flowEdges: edges };
  }, [
    ingressNodes,
    internalProjects,
    egressNodes,
    edgesList,
    systemName,
    searchQuery,
    showEndpoints,
    showExternal,
    showDatabases,
    onDrillDownToC2,
    onOpenFile,
  ]);

  const [nodes, setNodes, onNodesChange] = useNodesState(flowNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(flowEdges);

  useEffect(() => {
    setNodes(flowNodes);
    setEdges(flowEdges);
    setTimeout(() => {
      fitView({ padding: 0.2, duration: 400 });
    }, 50);
  }, [flowNodes, flowEdges, fitView, setNodes, setEdges]);

  return (
    <div className="c1-system-context-wrapper">
      {/* Compact Floating Controls */}
      <div className="c1-floating-controls">
        <div className="c1-controls-pill">
          <div className="c1-hud-filters">
            <button
              className={`c1-filter-btn ${showEndpoints ? 'active' : ''}`}
              onClick={() => setShowEndpoints((prev) => !prev)}
              title="Toggle Endpoints"
            >
              Endpoints ({ingressNodes.length})
            </button>
            <button
              className={`c1-filter-btn ${showDatabases ? 'active' : ''}`}
              onClick={() => setShowDatabases((prev) => !prev)}
              title="Toggle Databases"
            >
              Databases
            </button>
            <button
              className={`c1-filter-btn ${showExternal ? 'active' : ''}`}
              onClick={() => setShowExternal((prev) => !prev)}
              title="Toggle External Services"
            >
              External APIs
            </button>
          </div>

          <div className="c1-controls-divider" />

          <div className="c1-hud-search-wrap">
            <input
              type="text"
              className="c1-hud-search-input"
              placeholder="Filter components..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
            {searchQuery && (
              <button className="c1-search-clear" onClick={() => setSearchQuery('')}>
                ✕
              </button>
            )}
          </div>

          <button
            className="c1-fit-btn"
            onClick={() => fitView({ padding: 0.2, duration: 400 })}
            title="Fit Diagram to View"
          >
            ⤢ Fit
          </button>
        </div>
      </div>

      {/* Main Flow Canvas */}
      <div className="c1-flow-viewport">
        <ReactFlow
          nodes={nodes}
          edges={edges}
          nodeTypes={nodeTypes}
          onNodesChange={onNodesChange}
          onEdgesChange={onEdgesChange}
          minZoom={0.2}
          maxZoom={2.5}
          proOptions={{ hideAttribution: true }}
        >
          <Background color="var(--vscode-panel-border, rgba(128, 128, 128, 0.15))" gap={24} size={1} />
          <Controls position="bottom-right" showInteractive={false} />
          <MiniMap
            position="bottom-left"
            zoomable
            pannable
            nodeColor={(n) => {
              if (n.type === 'c1Endpoint') return '#38bdf8';
              if (n.type === 'c1Project') return '#6366f1';
              if (n.type === 'c1Egress') return '#f59e0b';
              return '#334155';
            }}
          />
        </ReactFlow>
      </div>
    </div>
  );
}

export function C1SystemContextView(props: C1SystemContextViewProps) {
  return (
    <ReactFlowProvider>
      <C1Canvas {...props} />
    </ReactFlowProvider>
  );
}
