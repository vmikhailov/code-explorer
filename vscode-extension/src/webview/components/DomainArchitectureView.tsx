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
import {
  forceSimulation,
  forceLink,
  forceManyBody,
  forceCenter,
  forceCollide,
  forceX,
  forceY,
  SimulationNodeDatum,
  SimulationLinkDatum,
} from 'd3-force';
import { GraphData, GraphNode, GraphEdge } from '../../../../proto/types';
import { DomainCardNode, DomainCardData, DomainProjectInfo } from './DomainCardNode';

export interface DomainArchitectureViewProps {
  graph: GraphData | null;
  onFocusInFlow?: (projectName: string) => void;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
}

const nodeTypes = {
  domainCard: DomainCardNode as any,
};

// Common sub-project naming suffixes that belong to a parent domain
const SUB_PROJECT_SUFFIX_REGEX =
  /\.(Logic|Client|Contracts|Data|Core|Domain|Infrastructure|Api|Service|Services|Web|Worker|Test|Tests|Shared|Models|Dto|SDK|UnitTests|IntegrationTests)$/i;

// Ingress / Frontend indicators
const INGRESS_KEYWORDS = ['admin', 'app', 'ui', 'fe', 'gateway', 'bff', 'portal', 'web', 'client-app', 'landing'];
const INGRESS_FRAMEWORKS = ['angular', 'react', 'vue', 'svelte', 'next', 'vite', 'blazor'];

/**
 * Normalizes project names and directory paths into a cohesive Domain / Bounded Context key.
 */
function extractDomainKey(node: GraphNode): { domainKey: string; domainDisplayName: string; isIngressHint: boolean } {
  const name = node.name || '';
  const path = (node.filePath || '').toLowerCase().replace(/\\/g, '/');
  const lowerName = name.toLowerCase();

  // 1. Check if name ends with standard architectural suffix (e.g. Lidoma.Services.Player.Logic)
  const suffixMatch = name.match(SUB_PROJECT_SUFFIX_REGEX);
  if (suffixMatch) {
    const parentName = name.substring(0, suffixMatch.index);
    // Extract short domain name (e.g., from Lidoma.Services.Player -> Player)
    const dotParts = parentName.split('.');
    const shortName = dotParts[dotParts.length - 1];
    return {
      domainKey: `domain:${parentName.toLowerCase()}`,
      domainDisplayName: `${shortName} Service`,
      isIngressHint: false,
    };
  }

  // 2. Directory-based grouping (e.g. services/player/... or adhub/...)
  if (path) {
    const pathParts = path.split('/').filter(Boolean);
    const servicesIdx = pathParts.findIndex((p) => p === 'services' || p === 'microservices');
    if (servicesIdx !== -1 && servicesIdx + 1 < pathParts.length) {
      const folderDomain = pathParts[servicesIdx + 1];
      const cleanName = folderDomain.charAt(0).toUpperCase() + folderDomain.slice(1);
      return {
        domainKey: `domain:${folderDomain.toLowerCase()}`,
        domainDisplayName: `${cleanName} Service`,
        isIngressHint: false,
      };
    }

    // Monorepo sub-directory pattern (e.g. adhub/adhub-cli -> adhub)
    if (pathParts.length >= 2 && !['src', 'packages', 'libs', 'projects'].includes(pathParts[0])) {
      const folderDomain = pathParts[0];
      const cleanName = folderDomain.charAt(0).toUpperCase() + folderDomain.slice(1);
      return {
        domainKey: `domain:${folderDomain.toLowerCase()}`,
        domainDisplayName: `${cleanName}`,
        isIngressHint: INGRESS_KEYWORDS.some((kw) => folderDomain.toLowerCase().includes(kw)),
      };
    }
  }

  // 3. Standalone project
  const isIngress =
    INGRESS_KEYWORDS.some((kw) => lowerName.includes(kw)) ||
    INGRESS_FRAMEWORKS.some((fw) => (node.properties?.framework || '').toLowerCase().includes(fw));

  return {
    domainKey: `domain:${lowerName}`,
    domainDisplayName: name,
    isIngressHint: isIngress,
  };
}

interface InternalDomainRecord {
  domainId: string;
  name: string;
  displayName: string;
  zone: 'ingress' | 'service' | 'data' | 'topic' | 'external';
  primaryNode?: GraphNode;
  projects: DomainProjectInfo[];
  framework?: string;
  language?: string;
}

interface LayoutSimNode extends SimulationNodeDatum {
  id: string;
  domain: DomainCardData;
  width: number;
  height: number;
  radius: number;
  targetX: number;
}

interface LayoutSimLink extends SimulationLinkDatum<LayoutSimNode> {
  source: string | LayoutSimNode;
  target: string | LayoutSimNode;
  category: 'service_call' | 'messaging';
  count: number;
}

const DomainMapInner: React.FC<DomainArchitectureViewProps> = ({
  graph,
  onFocusInFlow,
  onOpenFile,
}) => {
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const { fitView } = useReactFlow();

  // 1. Synthesize Domain Entities from Graph Data
  const { domains, projectToDomain, macroEdges, stats } = useMemo(() => {
    const domainMap = new Map<string, InternalDomainRecord>();
    const projToDomainMap = new Map<string, string>();

    // 1a. Process Message Topics, Queues, and External Services (Databases are mapped for metric counting, not as domain cards)
    for (const node of graph?.nodes || []) {
      if (node.kind === 'Database' || node.properties?.role === 'database') {
        // Map database IDs so Project -> Database edges can be resolved for metric counts on service cards,
        // but do not add Database nodes to domainMap (keeps Domain Service Map focused on services and messaging).
        projToDomainMap.set(node.id, node.id);
        projToDomainMap.set(node.name, node.id);
      } else if (node.kind === 'Topic' || node.properties?.role === 'topic') {
        const domainId = node.id;
        projToDomainMap.set(node.id, domainId);
        projToDomainMap.set(node.name, domainId);
        domainMap.set(domainId, {
          domainId,
          name: node.name,
          displayName: node.displayName || node.name,
          zone: 'topic',
          primaryNode: node,
          projects: [],
          framework: node.properties?.broker_type || 'Message Queue',
        });
      } else if (node.kind === 'ExternalService') {
        const isMsg = (node.properties?.service_type || '').toLowerCase() === 'messagebroker';
        const domainId = node.id;
        projToDomainMap.set(node.id, domainId);
        projToDomainMap.set(node.name, domainId);
        domainMap.set(domainId, {
          domainId,
          name: node.name,
          displayName: node.displayName || node.name,
          zone: isMsg ? 'topic' : 'external',
          primaryNode: node,
          projects: [],
          framework: node.properties?.service_type || 'External',
        });
      }
    }

    // 1b. Process Projects and Cluster into Domains
    for (const node of graph?.nodes || []) {
      if (node.kind !== 'Project') continue;

      const { domainKey, domainDisplayName, isIngressHint } = extractDomainKey(node);
      projToDomainMap.set(node.id, domainKey);
      projToDomainMap.set(node.name, domainKey);

      let rec = domainMap.get(domainKey);
      if (!rec) {
        const layer = (node.properties?.layer || node.properties?.layerId || '').toLowerCase();
        const isIngress = layer === 'layer_ingress' || isIngressHint;

        rec = {
          domainId: domainKey,
          name: node.name,
          displayName: domainDisplayName,
          zone: isIngress ? 'ingress' : 'service',
          primaryNode: node,
          projects: [],
          framework: node.properties?.framework,
          language: node.properties?.language || node.properties?.project_type,
        };
        domainMap.set(domainKey, rec);
      }

      rec.projects.push({
        id: node.id,
        name: node.name,
        kind: node.kind,
        filePath: node.filePath,
        isLibrary: node.properties?.is_library === 'true',
      });

      // Update primary node if current is an entrypoint / API rather than sub-library
      if (
        rec.primaryNode &&
        (rec.primaryNode.properties?.is_library === 'true' ||
          rec.primaryNode.name.toLowerCase().includes('client')) &&
        node.properties?.is_library !== 'true'
      ) {
        rec.primaryNode = node;
      }
    }

    // 2. Synthesize Macro Edges (Only Service Calls and Messaging flows)
    interface EdgeAggregator {
      source: string;
      target: string;
      category: 'service_call' | 'messaging';
      count: number;
    }
    const macroEdgeMap = new Map<string, EdgeAggregator>();
    const dbsUsed = new Map<string, Set<string>>();

    for (const edge of graph?.edges || []) {
      const srcDomain = projToDomainMap.get(edge.source) || projToDomainMap.get(edge.source.toLowerCase());
      const tgtDomain = projToDomainMap.get(edge.target) || projToDomainMap.get(edge.target.toLowerCase());

      if (!srcDomain || !tgtDomain || srcDomain === tgtDomain) {
        continue;
      }

      // Check for Database usage
      if (edge.category === 'database' || edge.kind === 'USES_DB') {
        if (!dbsUsed.has(srcDomain)) dbsUsed.set(srcDomain, new Set());
        dbsUsed.get(srcDomain)!.add(tgtDomain);
        continue;
      }

      // Only domain entities that exist in domainMap can participate in macro edges
      if (!domainMap.has(srcDomain) || !domainMap.has(tgtDomain)) {
        continue;
      }

      const tgtRecord = domainMap.get(tgtDomain);
      let cat: 'service_call' | 'messaging' | 'library' | null = null;

      if (tgtRecord?.zone === 'topic') {
        cat = 'messaging';
      } else if (edge.category === 'messaging' || edge.kind === 'TRIGGERS') {
        cat = 'messaging';
      } else if (edge.category === 'service_call' || edge.kind === 'SERVICE_CALL') {
        cat = 'service_call';
      } else {
        // If target project is a .Client library, referencing it means calling the service!
        const isClientLib = edge.target.toLowerCase().includes('.client') || edge.target.toLowerCase().endsWith('client');
        cat = isClientLib ? 'service_call' : 'library';
      }

      // On Domain Service Map, only show service calls and messages!
      if (cat !== 'service_call' && cat !== 'messaging') {
        continue;
      }

      const key = `${srcDomain}->${tgtDomain}:${cat}`;
      const existing = macroEdgeMap.get(key);

      if (existing) {
        existing.count += 1;
      } else {
        macroEdgeMap.set(key, {
          source: srcDomain,
          target: tgtDomain,
          category: cat,
          count: 1,
        });
      }
    }

    // 3. Compute Metrics for each Domain
    const inCalls = new Map<string, number>();
    const outCalls = new Map<string, number>();
    const msgsUsed = new Map<string, Set<string>>();

    for (const edge of macroEdgeMap.values()) {
      if (edge.category === 'service_call') {
        outCalls.set(edge.source, (outCalls.get(edge.source) || 0) + 1);
        inCalls.set(edge.target, (inCalls.get(edge.target) || 0) + 1);
      } else if (edge.category === 'messaging') {
        if (!msgsUsed.has(edge.source)) msgsUsed.set(edge.source, new Set());
        msgsUsed.get(edge.source)!.add(edge.target);
      }
    }

    const domainList: DomainCardData[] = [];
    let ingressCount = 0;
    let serviceCount = 0;
    let topicCount = 0;

    for (const d of domainMap.values()) {
      if (d.zone === 'ingress') ingressCount++;
      else if (d.zone === 'service') serviceCount++;
      else if (d.zone === 'topic') topicCount++;

      domainList.push({
        domainId: d.domainId,
        name: d.name,
        displayName: d.displayName,
        zone: d.zone,
        primaryNode: d.primaryNode,
        projects: d.projects,
        framework: d.framework,
        language: d.language,
        inboundCallsCount: inCalls.get(d.domainId) || 0,
        outboundCallsCount: outCalls.get(d.domainId) || 0,
        dbCount: dbsUsed.get(d.domainId)?.size || 0,
        messagingCount: msgsUsed.get(d.domainId)?.size || 0,
        onFocusInFlow,
        onOpenFile,
      });
    }

    let serviceCallsCount = 0;
    let messagesCount = 0;
    for (const edge of macroEdgeMap.values()) {
      if (edge.category === 'service_call') serviceCallsCount += edge.count;
      else if (edge.category === 'messaging') messagesCount += edge.count;
    }

    return {
      domains: domainList,
      projectToDomain: projToDomainMap,
      macroEdges: Array.from(macroEdgeMap.values()),
      stats: {
        total: domainList.length,
        ingress: ingressCount,
        services: serviceCount,
        topics: topicCount,
        serviceCalls: serviceCallsCount,
        messages: messagesCount,
      },
    };
  }, [graph, onFocusInFlow, onOpenFile]);

  // 2. Perform Force-Directed Layout & Build ReactFlow Graph
  useEffect(() => {
    if (!domains || domains.length === 0) {
      setNodes([]);
      setEdges([]);
      return;
    }

    // Filter based on search query
    const query = searchQuery.trim().toLowerCase();
    const visibleDomainIds = new Set<string>();

    for (const d of domains) {
      if (!query) {
        visibleDomainIds.add(d.domainId);
        continue;
      }

      const matchName = d.displayName.toLowerCase().includes(query) || d.name.toLowerCase().includes(query);
      const matchSub = d.projects.some((p) => p.name.toLowerCase().includes(query));
      if (matchName || matchSub) {
        visibleDomainIds.add(d.domainId);
      }
    }

    const visibleDomains = domains.filter((d) => visibleDomainIds.has(d.domainId));
    const totalVisible = visibleDomains.length;

    // Simulation nodes with collision radius and target horizontal bias
    const simNodes: LayoutSimNode[] = visibleDomains.map((d, index) => {
      const isTopic = d.zone === 'topic';
      const width = isTopic ? 190 : 250;
      const height = isTopic ? 80 : (d.projects.length > 1 ? 165 : 135);
      const radius = isTopic ? 120 : 160;

      // Target X bias: Ingress frontends on left, Services in middle, Topics on right
      let targetX = 0;
      if (d.zone === 'ingress') {
        targetX = -450;
      } else if (d.zone === 'topic') {
        targetX = 400;
      }

      // Initial circular / banded distribution to prevent starting from exact (0,0)
      const angle = (2 * Math.PI * index) / (totalVisible || 1);
      const ringRadius = 260 + totalVisible * 16;
      const initX = targetX !== 0 ? targetX + Math.cos(angle) * 120 : Math.cos(angle) * ringRadius;
      const initY = Math.sin(angle) * (ringRadius * 0.7);

      return {
        id: d.domainId,
        domain: d,
        width,
        height,
        radius,
        targetX,
        x: initX,
        y: initY,
      };
    });

    const simNodeMap = new Map<string, LayoutSimNode>(simNodes.map((n) => [n.id, n]));

    // Simulation links
    const simLinks: LayoutSimLink[] = [];
    for (const e of macroEdges) {
      if (visibleDomainIds.has(e.source) && visibleDomainIds.has(e.target)) {
        simLinks.push({
          source: e.source,
          target: e.target,
          category: e.category,
          count: e.count,
        });
      }
    }

    // Force simulation
    const simulation = forceSimulation<LayoutSimNode>(simNodes)
      .force(
        'link',
        forceLink<LayoutSimNode, LayoutSimLink>(simLinks)
          .id((d) => d.id)
          .distance((l) => (l.category === 'messaging' ? 260 : 320))
          .strength(0.45)
      )
      .force('charge', forceManyBody<LayoutSimNode>().strength(-1800).distanceMax(2200))
      .force('collide', forceCollide<LayoutSimNode>().radius((d) => d.radius).iterations(4))
      .force(
        'x',
        forceX<LayoutSimNode>((d) => d.targetX).strength((d) => (d.domain.zone === 'ingress' ? 0.15 : 0.08))
      )
      .force('y', forceY<LayoutSimNode>(0).strength(0.06))
      .force('center', forceCenter(0, 0))
      .stop();

    // Execute 300 iterations synchronously for instant, stable layout
    for (let i = 0; i < 300; ++i) {
      simulation.tick();
    }

    // Build ReactFlow Nodes
    const flowNodes: Node[] = simNodes.map((sNode) => ({
      id: sNode.id,
      type: 'domainCard',
      position: {
        x: Math.round((sNode.x || 0) - sNode.width / 2),
        y: Math.round((sNode.y || 0) - sNode.height / 2),
      },
      data: sNode.domain,
    }));

    // Build ReactFlow Edges with dynamic nearest-face handles
    const flowEdges: Edge[] = [];
    for (const link of simLinks) {
      const sNode = (typeof link.source === 'object' ? link.source : simNodeMap.get(link.source)) as LayoutSimNode | undefined;
      const tNode = (typeof link.target === 'object' ? link.target : simNodeMap.get(link.target)) as LayoutSimNode | undefined;

      if (!sNode || !tNode) continue;

      const sx = sNode.x || 0;
      const sy = sNode.y || 0;
      const tx = tNode.x || 0;
      const ty = tNode.y || 0;

      const dx = tx - sx;
      const dy = ty - sy;

      let sourceHandle = 'source-right';
      let targetHandle = 'target-left';

      if (Math.abs(dx) >= Math.abs(dy)) {
        if (dx >= 0) {
          sourceHandle = 'source-right';
          targetHandle = 'target-left';
        } else {
          sourceHandle = 'source-left';
          targetHandle = 'target-right';
        }
      } else {
        if (dy >= 0) {
          sourceHandle = 'source-bottom';
          targetHandle = 'target-top';
        } else {
          sourceHandle = 'source-top';
          targetHandle = 'target-bottom';
        }
      }

      const isMsg = link.category === 'messaging';
      const stroke = isMsg ? '#fbbf24' : '#38bdf8';
      const strokeDasharray = isMsg ? '6,4' : undefined;
      const label = link.count > 1 ? (isMsg ? `${link.count} msgs` : `${link.count} calls`) : undefined;

      flowEdges.push({
        id: `${sNode.id}->${tNode.id}:${link.category}`,
        source: sNode.id,
        target: tNode.id,
        sourceHandle,
        targetHandle,
        animated: false,
        style: {
          stroke,
          strokeWidth: 2,
          strokeDasharray,
        },
        label,
        labelStyle: { fill: '#94a3b8', fontSize: 10, fontWeight: 600 },
        labelBgStyle: { fill: 'rgba(15, 23, 42, 0.85)', rx: 4, ry: 4 },
        markerEnd: {
          type: MarkerType.ArrowClosed,
          color: stroke,
          width: 14,
          height: 14,
        },
      });
    }

    setNodes(flowNodes);
    setEdges(flowEdges);

    setTimeout(() => {
      fitView({ padding: 0.2, duration: 400 });
    }, 50);
  }, [domains, macroEdges, searchQuery, fitView, setNodes, setEdges]);

  return (
    <div className="domain-map-canvas-container">
      {/* Top-Left Floating HUD */}
      <div className="domain-map-hud">
        <div className="domain-hud-title-badge">
          <span className="domain-hud-label">Macro Architecture</span>
          <span className="domain-hud-title">Domain Microservice Map</span>
        </div>

        <div className="domain-hud-stats">
          <span className="hud-stat-pill" title="Ingress & Frontends">
            🌐 {stats.ingress} Apps
          </span>
          <span className="hud-stat-pill" title="Domain Services">
            ⚙️ {stats.services} Services
          </span>
          {stats.topics > 0 && (
            <span className="hud-stat-pill" title="Message Queues & Event Topics">
              📬 {stats.topics} Queues
            </span>
          )}
          <span className="hud-stat-pill" title="Service Calls (RPC / HTTP)">
            ⚡ {stats.serviceCalls} Calls
          </span>
          <span className="hud-stat-pill" title="Message Flows (Pub / Sub)">
            ✉️ {stats.messages} Msgs
          </span>
        </div>

        <div className="domain-hud-search">
          <input
            type="text"
            className="domain-search-input"
            placeholder="Filter domains or projects..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
          {searchQuery && (
            <button
              className="domain-search-clear"
              onClick={() => setSearchQuery('')}
              title="Clear search"
            >
              ✕
            </button>
          )}
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
        minZoom={0.15}
        maxZoom={2}
      >
        <Background color="rgba(255,255,255,0.06)" gap={24} />
        <Controls />
        <MiniMap
          nodeColor={(n) => {
            const zone = (n.data as any)?.zone;
            if (zone === 'ingress') return '#38bdf8';
            if (zone === 'service') return '#a855f7';
            if (zone === 'topic') return '#fbbf24';
            return '#34d399';
          }}
          maskColor="rgba(0, 0, 0, 0.6)"
          style={{ background: '#181822', border: '1px solid #333' }}
        />
      </ReactFlow>
    </div>
  );
};

export const DomainArchitectureView: React.FC<DomainArchitectureViewProps> = (props) => {
  return (
    <ReactFlowProvider>
      <DomainMapInner {...props} />
    </ReactFlowProvider>
  );
};
