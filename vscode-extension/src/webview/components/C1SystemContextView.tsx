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
  C1GroupCardNode,
} from './C1CardNodes';

export interface C1SystemContextViewProps {
  graph: GraphData | null;
  onDrillDownToC2?: (projectName: string) => void;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
  onSelectNode?: (node: GraphNode | null) => void;
  selectedNodeId?: string;
}

const nodeTypes = {
  c1Endpoint: C1EndpointCardNode as any,
  c1Project: C1ProjectCardNode as any,
  c1Egress: C1EgressCardNode as any,
  c1Boundary: C1BoundaryCardNode as any,
  c1Group: C1GroupCardNode as any,
};

function C1Canvas({
  graph,
  onDrillDownToC2,
  onOpenFile,
  onSelectNode,
  selectedNodeId,
}: C1SystemContextViewProps) {
  const { fitView } = useReactFlow();
  const [searchQuery, setSearchQuery] = useState('');
  const [showEndpoints, setShowEndpoints] = useState(true);
  const [showExternal, setShowExternal] = useState(true);
  const [showDatabases, setShowDatabases] = useState(true);
  const [expandedGroupIds, setExpandedGroupIds] = useState<Set<string>>(new Set());
  const [groupAllProjects, setGroupAllProjects] = useState(false);

  const toggleGroup = useCallback((groupId: string) => {
    setExpandedGroupIds((prev) => {
      const next = new Set(prev);
      if (next.has(groupId)) {
        next.delete(groupId);
      } else {
        next.add(groupId);
      }
      return next;
    });
  }, []);

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

    for (const edge of edgesList) {
      if (projIdSet.has(edge.target)) {
        projInMap.set(edge.target, (projInMap.get(edge.target) || 0) + 1);
      }
      if (projIdSet.has(edge.source)) {
        projOutMap.set(edge.source, (projOutMap.get(edge.source) || 0) + 1);
      }
    }

    const ingressItemHeight = 110;
    const projectItemHeight = 145;
    const egressItemHeight = 140;
    const ingressX = 60;

    const THRESHOLD = 10;
    const nodeToGroupMap = new Map<string, string>();

    // 1. Ingress Nodes
    const shouldGroupIngress = activeIngress.length > THRESHOLD;
    const isIngressExpanded = expandedGroupIds.has('ingress:endpoints');

    if (shouldGroupIngress && !isIngressExpanded) {
      const ingressGroupId = 'group:ingress:endpoints';
      const methodCounts: Record<string, number> = {};
      for (const ep of activeIngress) {
        const rawMethod = (
          ep.properties?.method ||
          (ep.name.startsWith('GET:') ? 'GET' : ep.name.startsWith('POST:') ? 'POST' : ep.name.startsWith('DELETE:') ? 'DELETE' : ep.name.startsWith('PUT:') ? 'PUT' : 'HTTP')
        ).toUpperCase();
        methodCounts[rawMethod] = (methodCounts[rawMethod] || 0) + 1;
      }
      const methodSummary = Object.entries(methodCounts)
        .map(([m, c]) => `${c} ${m}`)
        .join(' · ');

      nodes.push({
        id: ingressGroupId,
        type: 'c1Group',
        position: { x: ingressX, y: 120 }, // will be adjusted below
        data: {
          id: ingressGroupId,
          kind: 'Endpoint',
          category: 'ingress',
          title: `${activeIngress.length} API Endpoints`,
          count: activeIngress.length,
          items: activeIngress,
          subSummary: methodSummary,
          methodCounts,
          previewItems: activeIngress.slice(0, 4).map((n) => n.name.replace(/^[A-Z]+:/, '')),
          isExpanded: false,
          onToggleExpand: () => toggleGroup('ingress:endpoints'),
          onSelectNode: () =>
            onSelectNode?.({
              id: ingressGroupId,
              name: `${activeIngress.length} API Endpoints`,
              kind: 'Group',
              displayName: `Group: ${activeIngress.length} API Endpoints`,
              properties: {
                group_kind: 'Endpoint',
                total_count: String(activeIngress.length),
                items: JSON.stringify(
                  activeIngress.map((i) => ({
                    id: i.id,
                    name: i.name,
                    filePath: i.filePath,
                    lineStart: i.lineStart,
                    properties: i.properties,
                  }))
                ),
              },
            }),
          isSelected: selectedNodeId === ingressGroupId,
        },
      });

      activeIngress.forEach((ep) => nodeToGroupMap.set(ep.id, ingressGroupId));
    } else {
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
            onSelectNode: () => onSelectNode?.(node),
            isSelected: selectedNodeId ? node.id === selectedNodeId || node.name === selectedNodeId : false,
          },
        });
      });
    }

    // 2. Internal Projects
    const projectStartY = 80;
    const visibleProjectCards: Node[] = [];

    if (groupAllProjects && activeProjects.length > THRESHOLD) {
      const projAllId = 'group:project:all';
      const inCalls = activeProjects.reduce((acc, p) => acc + (projInMap.get(p.id) || 0), 0);
      const outCalls = activeProjects.reduce((acc, p) => acc + (projOutMap.get(p.id) || 0), 0);
      visibleProjectCards.push({
        id: projAllId,
        type: 'c1Group',
        position: { x: 460, y: projectStartY },
        data: {
          id: projAllId,
          kind: 'Project',
          category: 'project',
          title: `${activeProjects.length} Internal Projects`,
          count: activeProjects.length,
          items: activeProjects,
          subSummary: `${inCalls} in · ${outCalls} out`,
          previewItems: activeProjects.slice(0, 4).map((p) => p.name),
          isExpanded: false,
          onToggleExpand: () => setGroupAllProjects(false),
          onSelectNode: () =>
            onSelectNode?.({
              id: projAllId,
              name: `${activeProjects.length} Internal Projects`,
              kind: 'Group',
              displayName: `Group: ${activeProjects.length} Internal Projects`,
              properties: {
                group_kind: 'Project',
                total_count: String(activeProjects.length),
                items: JSON.stringify(
                  activeProjects.map((p) => ({
                    id: p.id,
                    name: p.name,
                    filePath: p.filePath,
                    properties: p.properties,
                  }))
                ),
              },
            }),
          isSelected: selectedNodeId === projAllId,
        },
      });
      activeProjects.forEach((p) => nodeToGroupMap.set(p.id, projAllId));
    } else {
      // Partition projects by kind/role
      const roleBuckets = new Map<string, GraphNode[]>();
      for (const proj of activeProjects) {
        const rawRole = proj.properties?.role || proj.kind || 'Project';
        let role = 'Project';
        const lower = rawRole.toLowerCase();
        if (lower.includes('service')) role = 'Service';
        else if (lower.includes('lib')) role = 'Library';
        else if (lower.includes('worker')) role = 'Worker';
        else if (lower.includes('app') || lower.includes('ui') || lower.includes('frontend')) role = 'App';
        else if (lower.includes('cli') || lower.includes('tool')) role = 'CliTool';
        else role = rawRole;

        if (!roleBuckets.has(role)) roleBuckets.set(role, []);
        roleBuckets.get(role)!.push(proj);
      }

      for (const [role, projList] of roleBuckets.entries()) {
        const shouldGroupRole = projList.length > THRESHOLD;
        const groupKey = `project:${role.toLowerCase()}`;
        const isRoleExpanded = expandedGroupIds.has(groupKey);

        if (shouldGroupRole && !isRoleExpanded) {
          const groupId = `group:${groupKey}`;
          const inCalls = projList.reduce((acc, p) => acc + (projInMap.get(p.id) || 0), 0);
          const outCalls = projList.reduce((acc, p) => acc + (projOutMap.get(p.id) || 0), 0);
          const roleLabel =
            role === 'Library'
              ? 'Shared Libraries'
              : role === 'Service'
              ? 'Services'
              : role === 'Worker'
              ? 'Workers'
              : role === 'App'
              ? 'Applications'
              : `${role}s`;

          visibleProjectCards.push({
            id: groupId,
            type: 'c1Group',
            position: { x: 0, y: 0 },
            data: {
              id: groupId,
              kind: role,
              category: 'project',
              title: `${projList.length} ${roleLabel}`,
              count: projList.length,
              items: projList,
              subSummary: `${inCalls} in · ${outCalls} out`,
              previewItems: projList.slice(0, 4).map((p) => p.name),
              isExpanded: false,
              onToggleExpand: () => toggleGroup(groupKey),
              onSelectNode: () =>
                onSelectNode?.({
                  id: groupId,
                  name: `${projList.length} ${roleLabel}`,
                  kind: 'Group',
                  displayName: `Group: ${projList.length} ${roleLabel}`,
                  properties: {
                    group_kind: role,
                    total_count: String(projList.length),
                    items: JSON.stringify(
                      projList.map((p) => ({
                        id: p.id,
                        name: p.name,
                        filePath: p.filePath,
                        properties: p.properties,
                      }))
                    ),
                  },
                }),
              isSelected: selectedNodeId === groupId,
            },
          });
          projList.forEach((p) => nodeToGroupMap.set(p.id, groupId));
        } else {
          for (const proj of projList) {
            visibleProjectCards.push({
              id: proj.id,
              type: 'c1Project',
              position: { x: 0, y: 0 },
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
                onSelectNode: () => onSelectNode?.(proj),
                isSelected: selectedNodeId ? proj.id === selectedNodeId || proj.name === selectedNodeId : false,
              },
            });
          }
        }
      }
    }

    // Determine layout geometry based on visible project cards
    const twoProjectCols = visibleProjectCards.length > 6;
    const projCol1X = 460;
    const projCol2X = twoProjectCols ? 800 : 460;
    const egressX = twoProjectCols ? 1180 : 860;

    const col1Cards = twoProjectCols
      ? visibleProjectCards.slice(0, Math.ceil(visibleProjectCards.length / 2))
      : visibleProjectCards;
    const col2Cards = twoProjectCols
      ? visibleProjectCards.slice(Math.ceil(visibleProjectCards.length / 2))
      : [];

    col1Cards.forEach((c, idx) => {
      c.position = { x: projCol1X, y: projectStartY + idx * projectItemHeight };
      nodes.push(c);
    });

    col2Cards.forEach((c, idx) => {
      c.position = { x: projCol2X, y: projectStartY + idx * projectItemHeight };
      nodes.push(c);
    });

    const boundaryHeight = Math.max(
      col1Cards.length * projectItemHeight + 60,
      col2Cards.length * projectItemHeight + 60,
      220
    );
    const boundaryWidth = twoProjectCols ? 660 : 340;

    if (activeProjects.length > 0) {
      nodes.unshift({
        id: 'c1-system-boundary',
        type: 'c1Boundary',
        position: { x: projCol1X - 25, y: projectStartY - 45 },
        style: { width: boundaryWidth, height: boundaryHeight, zIndex: -1 },
        data: {
          systemName,
          projectCount: activeProjects.length,
          onToggleGroupAll: activeProjects.length > THRESHOLD ? () => setGroupAllProjects((prev) => !prev) : undefined,
          isGroupedAll: groupAllProjects,
        },
        draggable: false,
        selectable: false,
      });
    }

    // Vertically center ingress group if grouped
    if (shouldGroupIngress && !isIngressExpanded) {
      const ingGroup = nodes.find((n) => n.id === 'group:ingress:endpoints');
      if (ingGroup) {
        ingGroup.position.y = Math.max(60, projectStartY + (boundaryHeight - 160) / 2);
      }
    }

    // 3. Egress Nodes
    const visibleEgressCards: Node[] = [];
    const dbs = activeEgress.filter((e) => e.category === 'database');
    const exts = activeEgress.filter((e) => e.category === 'external');
    const msgs = activeEgress.filter((e) => e.category === 'messaging');

    // Databases
    const shouldGroupDbs = dbs.length > THRESHOLD;
    const isDbsExpanded = expandedGroupIds.has('egress:database');
    if (shouldGroupDbs && !isDbsExpanded) {
      const dbGroupId = 'group:egress:database';
      const dbTypes = Array.from(new Set(dbs.map((d) => d.node.properties?.db_type || 'DB'))).join(', ');
      visibleEgressCards.push({
        id: dbGroupId,
        type: 'c1Group',
        position: { x: egressX, y: 0 },
        data: {
          id: dbGroupId,
          kind: 'Database',
          category: 'egress',
          title: `${dbs.length} Databases`,
          count: dbs.length,
          items: dbs.map((d) => d.node),
          subSummary: dbTypes,
          previewItems: dbs.slice(0, 4).map((d) => d.node.name),
          isExpanded: false,
          onToggleExpand: () => toggleGroup('egress:database'),
          onSelectNode: () =>
            onSelectNode?.({
              id: dbGroupId,
              name: `${dbs.length} Databases`,
              kind: 'Group',
              displayName: `Group: ${dbs.length} Databases`,
              properties: {
                group_kind: 'Database',
                total_count: String(dbs.length),
                items: JSON.stringify(dbs.map((d) => ({ id: d.node.id, name: d.node.name, properties: d.node.properties }))),
              },
            }),
          isSelected: selectedNodeId === dbGroupId,
        },
      });
      dbs.forEach((d) => nodeToGroupMap.set(d.node.id, dbGroupId));
    } else {
      for (const d of dbs) {
        visibleEgressCards.push({
          id: d.node.id,
          type: 'c1Egress',
          position: { x: egressX, y: 0 },
          data: {
            id: d.node.id,
            name: d.node.name,
            category: 'database',
            subType: d.node.properties?.db_type,
            details: d.node.properties?.table_count ? `${d.node.properties.table_count} tables` : undefined,
            filePath: d.node.filePath,
            onOpenFile,
            onSelectNode: () => onSelectNode?.(d.node),
            isSelected: selectedNodeId ? d.node.id === selectedNodeId || d.node.name === selectedNodeId : false,
          },
        });
      }
    }

    // External Services
    const shouldGroupExt = exts.length > THRESHOLD;
    const isExtExpanded = expandedGroupIds.has('egress:external');
    if (shouldGroupExt && !isExtExpanded) {
      const extGroupId = 'group:egress:external';
      visibleEgressCards.push({
        id: extGroupId,
        type: 'c1Group',
        position: { x: egressX, y: 0 },
        data: {
          id: extGroupId,
          kind: 'ExternalService',
          category: 'egress',
          title: `${exts.length} External APIs`,
          count: exts.length,
          items: exts.map((e) => e.node),
          subSummary: 'External REST APIs & Cloud Services',
          previewItems: exts.slice(0, 4).map((e) => e.node.name),
          isExpanded: false,
          onToggleExpand: () => toggleGroup('egress:external'),
          onSelectNode: () =>
            onSelectNode?.({
              id: extGroupId,
              name: `${exts.length} External APIs`,
              kind: 'Group',
              displayName: `Group: ${exts.length} External APIs`,
              properties: {
                group_kind: 'ExternalService',
                total_count: String(exts.length),
                items: JSON.stringify(exts.map((e) => ({ id: e.node.id, name: e.node.name, properties: e.node.properties }))),
              },
            }),
          isSelected: selectedNodeId === extGroupId,
        },
      });
      exts.forEach((e) => nodeToGroupMap.set(e.node.id, extGroupId));
    } else {
      for (const e of exts) {
        visibleEgressCards.push({
          id: e.node.id,
          type: 'c1Egress',
          position: { x: egressX, y: 0 },
          data: {
            id: e.node.id,
            name: e.node.name,
            category: 'external',
            subType: 'REST API',
            filePath: e.node.filePath,
            onOpenFile,
            onSelectNode: () => onSelectNode?.(e.node),
            isSelected: selectedNodeId ? e.node.id === selectedNodeId || e.node.name === selectedNodeId : false,
          },
        });
      }
    }

    // Messaging (Topics / Queues)
    const shouldGroupMsg = msgs.length > THRESHOLD;
    const isMsgExpanded = expandedGroupIds.has('egress:messaging');
    if (shouldGroupMsg && !isMsgExpanded) {
      const msgGroupId = 'group:egress:messaging';
      visibleEgressCards.push({
        id: msgGroupId,
        type: 'c1Group',
        position: { x: egressX, y: 0 },
        data: {
          id: msgGroupId,
          kind: 'Topic',
          category: 'egress',
          title: `${msgs.length} Message Topics`,
          count: msgs.length,
          items: msgs.map((m) => m.node),
          subSummary: 'Event streams & message queues',
          previewItems: msgs.slice(0, 4).map((m) => m.node.name),
          isExpanded: false,
          onToggleExpand: () => toggleGroup('egress:messaging'),
          onSelectNode: () =>
            onSelectNode?.({
              id: msgGroupId,
              name: `${msgs.length} Message Topics`,
              kind: 'Group',
              displayName: `Group: ${msgs.length} Message Topics`,
              properties: {
                group_kind: 'Topic',
                total_count: String(msgs.length),
                items: JSON.stringify(msgs.map((m) => ({ id: m.node.id, name: m.node.name, properties: m.node.properties }))),
              },
            }),
          isSelected: selectedNodeId === msgGroupId,
        },
      });
      msgs.forEach((m) => nodeToGroupMap.set(m.node.id, msgGroupId));
    } else {
      for (const m of msgs) {
        visibleEgressCards.push({
          id: m.node.id,
          type: 'c1Egress',
          position: { x: egressX, y: 0 },
          data: {
            id: m.node.id,
            name: m.node.name,
            category: 'messaging',
            subType: m.node.properties?.broker_type || 'Message Queue',
            filePath: m.node.filePath,
            onOpenFile,
            onSelectNode: () => onSelectNode?.(m.node),
            isSelected: selectedNodeId ? m.node.id === selectedNodeId || m.node.name === selectedNodeId : false,
          },
        });
      }
    }

    // Position visible egress cards
    const egressStartY = 60;
    visibleEgressCards.forEach((c, idx) => {
      c.position = { x: egressX, y: egressStartY + idx * egressItemHeight };
      nodes.push(c);
    });

    // 4. Edges Construction (Ingress -> System Projects -> Egress)
    const validNodeIds = new Set(nodes.map((n) => n.id));
    const getEffectiveId = (id: string) => nodeToGroupMap.get(id) || id;

    interface AggEdge {
      source: string;
      target: string;
      count: number;
      color: string;
      animated?: boolean;
      dash?: string;
    }
    const edgeAggregates = new Map<string, AggEdge>();

    const addAggEdge = (rawSrc: string, rawTgt: string, color: string, animated = false, dash?: string) => {
      const src = getEffectiveId(rawSrc);
      const tgt = getEffectiveId(rawTgt);
      if (!validNodeIds.has(src) || !validNodeIds.has(tgt) || src === tgt) return;

      const key = `${src}-->${tgt}`;
      const existing = edgeAggregates.get(key);
      if (existing) {
        existing.count += 1;
      } else {
        edgeAggregates.set(key, {
          source: src,
          target: tgt,
          count: 1,
          color,
          animated,
          dash,
        });
      }
    };

    // A. Connect Endpoints to System Projects
    const apiProject =
      activeProjects.find(
        (p) =>
          p.name.toLowerCase().includes('api') ||
          p.name.toLowerCase().includes('ui') ||
          p.name.toLowerCase().includes('web') ||
          p.name.toLowerCase().includes('server')
      ) || activeProjects[0];

    activeIngress.forEach((ep) => {
      const directEdge = edgesList.find(
        (e) => (e.source === ep.id || e.target === ep.id) && validNodeIds.has(getEffectiveId(e.source === ep.id ? e.target : e.source))
      );
      const targetId = directEdge
        ? directEdge.source === ep.id
          ? directEdge.target
          : directEdge.source
        : apiProject?.id;

      if (targetId) {
        addAggEdge(ep.id, targetId, '#38bdf8', true);
      }
    });

    // B. Connect Projects to Egress
    activeEgress.forEach(({ node, category }) => {
      const directLinks = edgesList.filter(
        (e) => e.target === node.id || e.source === node.id
      );
      const color = category === 'database' ? '#f59e0b' : category === 'external' ? '#10b981' : '#a855f7';

      if (directLinks.length > 0) {
        directLinks.forEach((link) => {
          const sourceProjId = link.source === node.id ? link.target : link.source;
          addAggEdge(sourceProjId, node.id, color, false);
        });
      } else {
        const coreProj = activeProjects.find((p) => p.name.toLowerCase().includes('core')) || activeProjects[0];
        if (coreProj) {
          addAggEdge(coreProj.id, node.id, color, false, '4 4');
        }
      }
    });

    // C. Internal Project-to-Project Dependencies
    for (const edge of edgesList) {
      if (projIdSet.has(edge.source) && projIdSet.has(edge.target)) {
        addAggEdge(edge.source, edge.target, '#818cf8', false);
      }
    }

    // Convert Aggregated Edges to React Flow Edges
    for (const [key, agg] of edgeAggregates.entries()) {
      edges.push({
        id: `c1-edge-${agg.source}-${agg.target}`,
        source: agg.source,
        target: agg.target,
        sourceHandle: 'egress',
        targetHandle: 'ingress',
        animated: agg.animated || false,
        label: agg.count > 1 ? `${agg.count}` : undefined,
        labelStyle: { fill: '#cbd5e1', fontSize: 10, fontWeight: 700 },
        labelBgStyle: {
          fill: 'rgba(24, 25, 38, 0.92)',
          fillOpacity: 0.95,
          stroke: agg.color,
          strokeWidth: 1,
          rx: 4,
          ry: 4,
        },
        labelBgPadding: [4, 6],
        style: {
          stroke: agg.color,
          strokeWidth: Math.min(3.5, 1.5 + Math.log2(agg.count) * 0.35),
          strokeDasharray: agg.dash,
        },
        markerEnd: {
          type: MarkerType.ArrowClosed,
          color: agg.color,
          width: 14,
          height: 14,
        },
      });
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
    expandedGroupIds,
    groupAllProjects,
    selectedNodeId,
    onDrillDownToC2,
    onOpenFile,
    onSelectNode,
    toggleGroup,
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
            <button
              className={`c1-filter-btn ${expandedGroupIds.size === 0 ? 'active' : ''}`}
              onClick={() => {
                if (expandedGroupIds.size > 0) {
                  setExpandedGroupIds(new Set());
                } else {
                  const allGroups = new Set<string>();
                  allGroups.add('ingress:endpoints');
                  allGroups.add('egress:database');
                  allGroups.add('egress:external');
                  allGroups.add('egress:messaging');
                  setExpandedGroupIds(allGroups);
                }
              }}
              title={
                expandedGroupIds.size === 0
                  ? 'All groups with > 10 items are collapsed. Click to expand all.'
                  : 'Groups are expanded. Click to collapse all.'
              }
            >
              {expandedGroupIds.size === 0 ? '⊞ Grouped (>10)' : '⊟ All Expanded'}
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
          onNodeClick={(_, rfNode) => {
            const raw = (rfNode.data as any)?.graphNode || (rfNode.data as any);
            if (raw) {
              const gNode: GraphNode = raw.kind ? raw : {
                id: raw.id,
                name: raw.name || raw.id,
                kind: rfNode.type === 'c1Endpoint' ? 'Endpoint' : rfNode.type === 'c1Project' ? 'Project' : (raw.category === 'database' ? 'Database' : 'ExternalService'),
                filePath: raw.filePath,
                lineStart: raw.lineStart,
                properties: raw,
              };
              onSelectNode?.(gNode);
            }
          }}
          onPaneClick={() => onSelectNode?.(null)}
        >
          <Background color="var(--vscode-panel-border, rgba(128, 128, 128, 0.15))" gap={24} size={1} />
          <Controls position="bottom-right" showInteractive={false} />
          <MiniMap
            position="bottom-left"
            zoomable
            pannable
            nodeColor={(n) => {
              if (n.type === 'c1Group') return '#8b5cf6';
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
