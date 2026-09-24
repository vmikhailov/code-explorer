import React, { useEffect, useRef, useState, useMemo, useCallback } from 'react';
import cytoscape from 'cytoscape';
import dagre from 'cytoscape-dagre';
import { GraphData, GraphNode, GraphEdge } from '../../../../proto/types';

try {
  cytoscape.use(dagre);
} catch {}

export interface DomainArchitectureViewProps {
  graph: GraphData | null;
  onFocusInFlow?: (projectName: string) => void;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
}

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

export interface DomainProjectInfo {
  id: string;
  name: string;
  kind?: string;
  filePath?: string;
  isLibrary?: boolean;
}

export interface SelectedNodeDetail {
  id: string;
  name: string;
  displayName: string;
  kind: 'Service' | 'Ingress' | 'Database' | 'Topic' | 'ExternalService';
  displayTag: string;
  bgColor: string;
  borderColor: string;
  framework?: string;
  language?: string;
  primaryFilePath?: string;
  projects: DomainProjectInfo[];
  inboundCallsCount: number;
  outboundCallsCount: number;
  dbCount: number;
  messagingCount: number;
}

// Cytoscape stylesheets matching the circular Neo4j / graph ontology styling
const CYTOSCAPE_STYLES: cytoscape.StylesheetStyle[] = [
  // Base Node Style (Circular Discs)
  {
    selector: 'node',
    style: {
      'shape': 'ellipse',
      'width': 'data(size)',
      'height': 'data(size)',
      'background-color': 'data(bgColor)',
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
  // Ingress / App
  {
    selector: 'node[kind = "Ingress"]',
    style: {
      'background-color': '#0288d1',
      'border-color': '#01579b',
    },
  },
  // Service
  {
    selector: 'node[kind = "Service"]',
    style: {
      'background-color': '#e53935',
      'border-color': '#7f1d1d',
    },
  },
  // Database
  {
    selector: 'node[kind = "Database"]',
    style: {
      'background-color': '#7b1fa2',
      'border-color': '#4a148c',
    },
  },
  // Topic
  {
    selector: 'node[kind = "Topic"]',
    style: {
      'background-color': '#f59e0b',
      'border-color': '#b45309',
    },
  },
  // External
  {
    selector: 'node[kind = "ExternalService"]',
    style: {
      'background-color': '#26a69a',
      'border-color': '#004d40',
    },
  },
  // Selected Node Highlight
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
  // Hovered Node
  {
    selector: 'node.hovered',
    style: {
      'border-color': '#38bdf8',
      'border-width': 4,
    },
  },
  // Base Edge Style (Bezier with Labeled Directed Arrows)
  {
    selector: 'edge',
    style: {
      'width': 2,
      'line-color': '#64748b',
      'target-arrow-color': '#64748b',
      'target-arrow-shape': 'triangle',
      'arrow-scale': 1.15,
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
      'transition-property': 'line-color, target-arrow-color, width, opacity',
      'transition-duration': 0.2,
    },
  },
  // Edge Categories
  {
    selector: 'edge[category = "service_call"]',
    style: {
      'line-color': '#38bdf8',
      'target-arrow-color': '#38bdf8',
    },
  },
  {
    selector: 'edge[category = "database"]',
    style: {
      'line-color': '#c084fc',
      'target-arrow-color': '#c084fc',
    },
  },
  {
    selector: 'edge[category = "messaging"]',
    style: {
      'line-color': '#fbbf24',
      'target-arrow-color': '#fbbf24',
      'line-style': 'dashed',
    },
  },
  {
    selector: 'edge[category = "external"]',
    style: {
      'line-color': '#34d399',
      'target-arrow-color': '#34d399',
    },
  },
  // Highlighted Edges
  {
    selector: 'edge.highlighted',
    style: {
      'width': 3.5,
      'opacity': 1,
      'z-index': 999,
      'line-color': '#38bdf8',
      'target-arrow-color': '#38bdf8',
    },
  },
  // Dimmed Elements during search or hover
  {
    selector: '.dimmed',
    style: {
      'opacity': 0.12,
    },
  },
  // Search Matches
  {
    selector: 'node.search-match',
    style: {
      'underlay-color': '#f59e0b',
      'underlay-padding': '10px',
      'underlay-opacity': 0.6,
      'border-color': '#f59e0b',
      'border-width': 4,
    },
  },
];

export const DomainArchitectureView: React.FC<DomainArchitectureViewProps> = ({
  graph,
  onFocusInFlow,
  onOpenFile,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const cyRef = useRef<cytoscape.Core | null>(null);

  const [searchQuery, setSearchQuery] = useState('');
  const [layoutName, setLayoutName] = useState<'cose' | 'dagre' | 'concentric'>('cose');
  const [selectedNode, setSelectedNode] = useState<SelectedNodeDetail | null>(null);

  // 1. Synthesize Domain Entities & Infrastructure Nodes from GraphData
  const { elements, stats, nodeDetailMap } = useMemo(() => {
    const cyElements: cytoscape.ElementDefinition[] = [];
    const detailMap = new Map<string, SelectedNodeDetail>();

    const projToDomainMap = new Map<string, string>();
    const domainProjectsMap = new Map<string, DomainProjectInfo[]>();
    const domainPrimaryMap = new Map<string, GraphNode>();
    const domainZoneMap = new Map<string, 'ingress' | 'service'>();
    const domainNameMap = new Map<string, { name: string; displayName: string; framework?: string; language?: string }>();

    // 1a. Categorize Projects into Domains
    for (const node of graph?.nodes || []) {
      if (node.kind !== 'Project') continue;

      const { domainKey, domainDisplayName, isIngressHint } = extractDomainKey(node);
      projToDomainMap.set(node.id, domainKey);
      projToDomainMap.set(node.name, domainKey);
      projToDomainMap.set(node.id.toLowerCase(), domainKey);
      projToDomainMap.set(node.name.toLowerCase(), domainKey);

      let pList = domainProjectsMap.get(domainKey);
      if (!pList) {
        pList = [];
        domainProjectsMap.set(domainKey, pList);

        const layer = (node.properties?.layer || node.properties?.layerId || '').toLowerCase();
        const isIngress = layer === 'layer_ingress' || isIngressHint;
        domainZoneMap.set(domainKey, isIngress ? 'ingress' : 'service');

        domainNameMap.set(domainKey, {
          name: node.name,
          displayName: domainDisplayName,
          framework: node.properties?.framework,
          language: node.properties?.language || node.properties?.project_type,
        });
      }

      pList.push({
        id: node.id,
        name: node.name,
        kind: node.kind,
        filePath: node.filePath,
        isLibrary: node.properties?.is_library === 'true',
      });

      const currPrimary = domainPrimaryMap.get(domainKey);
      if (!currPrimary || (currPrimary.properties?.is_library === 'true' && node.properties?.is_library !== 'true')) {
        domainPrimaryMap.set(domainKey, node);
      }
    }

    // 1b. Collect Infrastructure Entities (Databases, Topics, ExternalServices)
    const dbNodes = new Map<string, { id: string; name: string; dbType: string }>();
    const topicNodes = new Map<string, { id: string; name: string; broker: string }>();
    const extNodes = new Map<string, { id: string; name: string; serviceType: string }>();

    for (const node of graph?.nodes || []) {
      if (node.kind === 'Database' || node.properties?.role === 'database') {
        const name = node.name || node.displayName || 'Database';
        dbNodes.set(node.id, {
          id: node.id,
          name,
          dbType: node.properties?.db_type || 'relational',
        });
        projToDomainMap.set(node.id, node.id);
        projToDomainMap.set(node.id.toLowerCase(), node.id);
      } else if (node.kind === 'Topic' || node.properties?.role === 'topic') {
        const name = node.name || node.displayName || 'Topic';
        topicNodes.set(node.id, {
          id: node.id,
          name,
          broker: node.properties?.broker_type || 'Message Queue',
        });
        projToDomainMap.set(node.id, node.id);
        projToDomainMap.set(node.id.toLowerCase(), node.id);
      } else if (node.kind === 'ExternalService') {
        const name = node.name || node.displayName || 'External Service';
        extNodes.set(node.id, {
          id: node.id,
          name,
          serviceType: node.properties?.service_type || 'API',
        });
        projToDomainMap.set(node.id, node.id);
        projToDomainMap.set(node.id.toLowerCase(), node.id);
      }
    }

    // 2. Synthesize Macro Edges
    interface EdgeAggregator {
      source: string;
      target: string;
      category: 'service_call' | 'database' | 'messaging' | 'external';
      label: string;
      count: number;
    }
    const macroEdges = new Map<string, EdgeAggregator>();

    const inCalls = new Map<string, number>();
    const outCalls = new Map<string, number>();
    const dbUsage = new Map<string, Set<string>>();
    const msgUsage = new Map<string, Set<string>>();

    for (const edge of graph?.edges || []) {
      const srcDomain =
        projToDomainMap.get(edge.source) ||
        projToDomainMap.get(edge.source.toLowerCase()) ||
        (dbNodes.has(edge.source) ? edge.source : null) ||
        (topicNodes.has(edge.source) ? edge.source : null);

      const tgtDomain =
        projToDomainMap.get(edge.target) ||
        projToDomainMap.get(edge.target.toLowerCase()) ||
        (dbNodes.has(edge.target) ? edge.target : null) ||
        (topicNodes.has(edge.target) ? edge.target : null) ||
        (extNodes.has(edge.target) ? edge.target : null);

      if (!srcDomain || !tgtDomain || srcDomain === tgtDomain) continue;

      let cat: 'service_call' | 'database' | 'messaging' | 'external' | null = null;
      let label = 'CALLS';

      if (dbNodes.has(tgtDomain) || edge.category === 'database' || edge.kind === 'USES_DB') {
        cat = 'database';
        label = 'USES_DB';
        if (!dbUsage.has(srcDomain)) dbUsage.set(srcDomain, new Set());
        dbUsage.get(srcDomain)!.add(tgtDomain);
      } else if (topicNodes.has(tgtDomain) || topicNodes.has(srcDomain) || edge.category === 'messaging' || edge.kind === 'TRIGGERS') {
        cat = 'messaging';
        label = topicNodes.has(srcDomain) ? 'SUBSCRIBES' : 'PUBLISHES';
        if (topicNodes.has(tgtDomain)) {
          if (!msgUsage.has(srcDomain)) msgUsage.set(srcDomain, new Set());
          msgUsage.get(srcDomain)!.add(tgtDomain);
        } else if (topicNodes.has(srcDomain)) {
          if (!msgUsage.has(tgtDomain)) msgUsage.set(tgtDomain, new Set());
          msgUsage.get(tgtDomain)!.add(srcDomain);
        }
      } else if (extNodes.has(tgtDomain)) {
        cat = 'external';
        label = 'CALLS';
      } else if (edge.category === 'service_call' || edge.kind === 'SERVICE_CALL' || edge.kind === 'CALLS_ENDPOINT') {
        cat = 'service_call';
        label = 'CALLS';
      } else {
        const isClientLib = edge.target.toLowerCase().includes('.client') || edge.target.toLowerCase().endsWith('client');
        if (isClientLib) {
          cat = 'service_call';
          label = 'CALLS';
        }
      }

      if (!cat) continue;

      if (cat === 'service_call') {
        outCalls.set(srcDomain, (outCalls.get(srcDomain) || 0) + 1);
        inCalls.set(tgtDomain, (inCalls.get(tgtDomain) || 0) + 1);
      }

      const key = `${srcDomain}->${tgtDomain}:${cat}`;
      const existing = macroEdges.get(key);
      if (existing) {
        existing.count += 1;
      } else {
        macroEdges.set(key, {
          source: srcDomain,
          target: tgtDomain,
          category: cat,
          label,
          count: 1,
        });
      }
    }

    // 3. Build Cytoscape Nodes
    let ingressCount = 0;
    let serviceCount = 0;

    // 3a. Service / Ingress Nodes
    for (const [domainId, meta] of domainNameMap.entries()) {
      const zone = domainZoneMap.get(domainId) || 'service';
      const isIngress = zone === 'ingress';
      if (isIngress) ingressCount++;
      else serviceCount++;

      const projects = domainProjectsMap.get(domainId) || [];
      const primaryNode = domainPrimaryMap.get(domainId);
      const tag = isIngress ? ':Ingress' : ':Service';
      const bgColor = isIngress ? '#0288d1' : '#e53935';
      const borderColor = isIngress ? '#01579b' : '#7f1d1d';
      const size = isIngress ? 54 : 50;

      const detail: SelectedNodeDetail = {
        id: domainId,
        name: meta.name,
        displayName: meta.displayName,
        kind: isIngress ? 'Ingress' : 'Service',
        displayTag: tag,
        bgColor,
        borderColor,
        framework: meta.framework,
        language: meta.language,
        primaryFilePath: primaryNode?.filePath || projects[0]?.filePath,
        projects,
        inboundCallsCount: inCalls.get(domainId) || 0,
        outboundCallsCount: outCalls.get(domainId) || 0,
        dbCount: dbUsage.get(domainId)?.size || 0,
        messagingCount: msgUsage.get(domainId)?.size || 0,
      };
      detailMap.set(domainId, detail);

      cyElements.push({
        group: 'nodes',
        data: {
          id: domainId,
          name: meta.name,
          displayName: meta.displayName,
          displayLabel: `${tag}\n${meta.displayName}`,
          kind: isIngress ? 'Ingress' : 'Service',
          bgColor,
          borderColor,
          size,
        },
      });
    }

    // 3b. Database Nodes (Only add databases that are actually used by at least one service, or workspace databases)
    let dbCount = 0;
    for (const [dbId, db] of dbNodes.entries()) {
      const isUsed = Array.from(macroEdges.values()).some((e) => e.target === dbId || e.source === dbId);
      if (!isUsed && dbNodes.size > 20) continue;
      dbCount++;

      const tag = ':DB';
      const bgColor = '#7b1fa2';
      const borderColor = '#4a148c';

      detailMap.set(dbId, {
        id: dbId,
        name: db.name,
        displayName: db.name,
        kind: 'Database',
        displayTag: tag,
        bgColor,
        borderColor,
        framework: db.dbType,
        projects: [],
        inboundCallsCount: 0,
        outboundCallsCount: 0,
        dbCount: 0,
        messagingCount: 0,
      });

      cyElements.push({
        group: 'nodes',
        data: {
          id: dbId,
          name: db.name,
          displayName: db.name,
          displayLabel: `${tag}\n${db.name}`,
          kind: 'Database',
          bgColor,
          borderColor,
          size: 48,
        },
      });
    }

    // 3c. Message Topics / Queues
    let topicCount = 0;
    for (const [tId, t] of topicNodes.entries()) {
      const isUsed = Array.from(macroEdges.values()).some((e) => e.target === tId || e.source === tId);
      if (!isUsed && topicNodes.size > 25) continue;
      topicCount++;

      const tag = ':Topic';
      const bgColor = '#f59e0b';
      const borderColor = '#b45309';

      detailMap.set(tId, {
        id: tId,
        name: t.name,
        displayName: t.name,
        kind: 'Topic',
        displayTag: tag,
        bgColor,
        borderColor,
        framework: t.broker,
        projects: [],
        inboundCallsCount: 0,
        outboundCallsCount: 0,
        dbCount: 0,
        messagingCount: 0,
      });

      cyElements.push({
        group: 'nodes',
        data: {
          id: tId,
          name: t.name,
          displayName: t.name,
          displayLabel: `${tag}\n${t.name}`,
          kind: 'Topic',
          bgColor,
          borderColor,
          size: 46,
        },
      });
    }

    // 3d. External Services
    for (const [extId, ext] of extNodes.entries()) {
      const isUsed = Array.from(macroEdges.values()).some((e) => e.target === extId);
      if (!isUsed) continue;

      const tag = ':External';
      const bgColor = '#26a69a';
      const borderColor = '#004d40';

      detailMap.set(extId, {
        id: extId,
        name: ext.name,
        displayName: ext.name,
        kind: 'ExternalService',
        displayTag: tag,
        bgColor,
        borderColor,
        framework: ext.serviceType,
        projects: [],
        inboundCallsCount: 0,
        outboundCallsCount: 0,
        dbCount: 0,
        messagingCount: 0,
      });

      cyElements.push({
        group: 'nodes',
        data: {
          id: extId,
          name: ext.name,
          displayName: ext.name,
          displayLabel: `${tag}\n${ext.name}`,
          kind: 'ExternalService',
          bgColor,
          borderColor,
          size: 44,
        },
      });
    }

    // 4. Build Cytoscape Edges
    const validNodeIdSet = new Set(cyElements.map((el) => el.data.id));
    let serviceCallsCount = 0;
    let messagesCount = 0;

    for (const [key, e] of macroEdges.entries()) {
      if (!validNodeIdSet.has(e.source) || !validNodeIdSet.has(e.target)) continue;

      if (e.category === 'service_call') serviceCallsCount += e.count;
      else if (e.category === 'messaging') messagesCount += e.count;

      const edgeLabel = e.count > 1 ? `${e.label} (${e.count})` : e.label;

      cyElements.push({
        group: 'edges',
        data: {
          id: key,
          source: e.source,
          target: e.target,
          category: e.category,
          label: edgeLabel,
          count: e.count,
        },
      });
    }

    return {
      elements: cyElements,
      detailMap,
      nodeDetailMap: detailMap,
      stats: {
        total: cyElements.filter((el) => el.group === 'nodes').length,
        ingress: ingressCount,
        services: serviceCount,
        databases: dbCount,
        topics: topicCount,
        serviceCalls: serviceCallsCount,
        messages: messagesCount,
      },
    };
  }, [graph]);

  // Apply layout
  const applyLayout = useCallback(
    (name: 'cose' | 'dagre' | 'concentric', cyInstance?: cytoscape.Core | null) => {
      const cy = cyInstance || cyRef.current;
      if (!cy || cy.nodes().length === 0) return;

      let layoutConfig: any;
      if (name === 'dagre') {
        layoutConfig = {
          name: 'dagre',
          rankDir: 'LR',
          nodeSep: 60,
          rankSep: 140,
          animate: false,
          fit: true,
          padding: 60,
        };
      } else if (name === 'concentric') {
        layoutConfig = {
          name: 'concentric',
          concentric: (node: any) => {
            const kind = node.data('kind');
            if (kind === 'Ingress') return 4;
            if (kind === 'Service') return 3;
            if (kind === 'Topic') return 2;
            return 1;
          },
          levelWidth: () => 1,
          animate: false,
          fit: true,
          padding: 60,
        };
      } else {
        // Organic Force-Directed (COSE)
        layoutConfig = {
          name: 'cose',
          animate: false,
          randomize: false,
          componentSpacing: 80,
          nodeRepulsion: () => 450000,
          nodeOverlap: 25,
          idealEdgeLength: () => 140,
          edgeElasticity: () => 100,
          nestingFactor: 5,
          gravity: 60,
          numIter: 400,
          coolingFactor: 0.95,
          fit: true,
          padding: 60,
        };
      }

      const layout = cy.layout(layoutConfig);
      layout.run();
    },
    []
  );

  // Initialize Cytoscape Instance
  useEffect(() => {
    if (!containerRef.current) return;

    const cy = cytoscape({
      container: containerRef.current,
      elements: [],
      style: CYTOSCAPE_STYLES,
      boxSelectionEnabled: false,
      autoungrabify: false,
      minZoom: 0.15,
      maxZoom: 3.5,
      wheelSensitivity: 0.25,
    });

    // Node Selection & Highlight
    cy.on('tap', 'node', (evt) => {
      const node = evt.target;
      const nodeId = node.id();
      const detail = nodeDetailMap.get(nodeId);
      if (detail) {
        setSelectedNode(detail);
      }

      // Highlight neighborhood
      cy.elements().removeClass('highlighted dimmed');
      const neighborhood = node.neighborhood().add(node);
      cy.elements().not(neighborhood).addClass('dimmed');
      node.connectedEdges().addClass('highlighted');
    });

    // Background click -> Deselect
    cy.on('tap', (evt) => {
      if (evt.target === cy) {
        setSelectedNode(null);
        cy.elements().removeClass('highlighted dimmed');
      }
    });

    // Double-click -> Drill down into Flow
    cy.on('dbltap', 'node', (evt) => {
      const node = evt.target;
      const nodeId = node.id();
      const detail = nodeDetailMap.get(nodeId);
      if (detail && (detail.kind === 'Service' || detail.kind === 'Ingress') && onFocusInFlow) {
        onFocusInFlow(detail.name);
      }
    });

    // Mouseover / Mouseout hover highlights
    cy.on('mouseover', 'node', (evt) => {
      containerRef.current?.classList.add('node-hover');
      evt.target.addClass('hovered');
    });

    cy.on('mouseout', 'node', (evt) => {
      containerRef.current?.classList.remove('node-hover');
      evt.target.removeClass('hovered');
    });

    cyRef.current = cy;

    return () => {
      cy.destroy();
      cyRef.current = null;
    };
  }, [nodeDetailMap, onFocusInFlow]);

  // Load Elements into Cytoscape when data changes
  useEffect(() => {
    const cy = cyRef.current;
    if (!cy) return;

    cy.elements().remove();
    if (elements.length > 0) {
      cy.add(elements);
      applyLayout(layoutName, cy);
    }
  }, [elements, layoutName, applyLayout]);

  // Search Query Filter
  useEffect(() => {
    const cy = cyRef.current;
    if (!cy) return;

    const q = searchQuery.trim().toLowerCase();
    if (!q) {
      cy.elements().removeClass('dimmed search-match');
      return;
    }

    const matches = cy.nodes().filter((n) => {
      const name = (n.data('name') || '').toLowerCase();
      const dName = (n.data('displayName') || '').toLowerCase();
      return name.includes(q) || dName.includes(q);
    });

    if (matches.length > 0) {
      cy.elements().addClass('dimmed');
      matches.removeClass('dimmed').addClass('search-match');
      matches.connectedEdges().removeClass('dimmed');
      matches.neighborhood().removeClass('dimmed');
    } else {
      cy.elements().removeClass('dimmed search-match');
    }
  }, [searchQuery]);

  const handleFitView = useCallback(() => {
    if (cyRef.current) {
      cyRef.current.fit(undefined, 50);
    }
  }, []);

  const handleLayoutChange = useCallback(
    (newLayout: 'cose' | 'dagre' | 'concentric') => {
      setLayoutName(newLayout);
      applyLayout(newLayout);
    },
    [applyLayout]
  );

  return (
    <div className="domain-map-canvas-container">
      {/* Top Floating HUD */}
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
          {stats.databases > 0 && (
            <span className="hud-stat-pill" title="Databases">
              🗄️ {stats.databases} DBs
            </span>
          )}
          {stats.topics > 0 && (
            <span className="hud-stat-pill" title="Message Queues & Event Topics">
              📬 {stats.topics} Topics
            </span>
          )}
          <span className="hud-stat-pill" title="Service Calls (RPC / HTTP)">
            ⚡ {stats.serviceCalls} Calls
          </span>
          <span className="hud-stat-pill" title="Message Flows (Pub / Sub)">
            ✉️ {stats.messages} Msgs
          </span>
        </div>

        {/* Layout Selector */}
        <div className="domain-hud-layout-select">
          <select
            value={layoutName}
            onChange={(e) => handleLayoutChange(e.target.value as any)}
            title="Graph Layout"
            className="domain-layout-dropdown"
          >
            <option value="cose">Force (COSE)</option>
            <option value="dagre">Hierarchical (Dagre)</option>
            <option value="concentric">Concentric</option>
          </select>
        </div>

        <button className="domain-hud-fit-btn" onClick={handleFitView} title="Center and Fit View">
          Fit
        </button>

        {/* Search */}
        <div className="domain-hud-search">
          <input
            type="text"
            className="domain-search-input"
            placeholder="Search domain or service..."
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

      {/* Full-bleed HTML5 Canvas Container for Cytoscape */}
      <div ref={containerRef} className="domain-cytoscape-container" />

      {/* Floating Node Inspector Panel (when node selected) */}
      {selectedNode && (
        <aside className="domain-inspector-panel">
          <div className="domain-inspector-header">
            <div className="inspector-badge" style={{ backgroundColor: selectedNode.bgColor }}>
              {selectedNode.displayTag}
            </div>
            <div className="inspector-title-group">
              <h4 className="inspector-title" title={selectedNode.displayName}>
                {selectedNode.displayName}
              </h4>
              {selectedNode.framework && (
                <span className="inspector-subtitle">{selectedNode.framework}</span>
              )}
            </div>
            <button
              className="inspector-close-btn"
              onClick={() => {
                setSelectedNode(null);
                cyRef.current?.elements().removeClass('highlighted dimmed');
              }}
              title="Close inspector"
            >
              ✕
            </button>
          </div>

          <div className="inspector-body">
            {/* Subprojects breakdown if clustered */}
            {selectedNode.projects && selectedNode.projects.length > 1 && (
              <div className="inspector-section">
                <label className="inspector-section-label">
                  Clustered Projects ({selectedNode.projects.length})
                </label>
                <div className="inspector-subprojects-list">
                  {selectedNode.projects.map((p) => (
                    <div
                      key={p.id}
                      className="inspector-subproject-item"
                      onClick={() => p.filePath && onOpenFile?.(p.filePath, 1)}
                      title={p.filePath || p.name}
                    >
                      <span className="subproject-dot">•</span>
                      <span className="subproject-name">{p.name}</span>
                      {p.isLibrary && <span className="subproject-lib-tag">lib</span>}
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Metrics Grid */}
            <div className="inspector-metrics-grid">
              {(selectedNode.kind === 'Service' || selectedNode.kind === 'Ingress') && (
                <>
                  <div className="inspector-metric-card" title="Inbound calls from other services">
                    <span className="metric-val">{selectedNode.inboundCallsCount}</span>
                    <span className="metric-lbl">Inbound Calls</span>
                  </div>
                  <div className="inspector-metric-card" title="Outbound calls to downstream services">
                    <span className="metric-val">{selectedNode.outboundCallsCount}</span>
                    <span className="metric-lbl">Outbound Calls</span>
                  </div>
                  {selectedNode.dbCount > 0 && (
                    <div className="inspector-metric-card" title="Databases used directly">
                      <span className="metric-val">{selectedNode.dbCount}</span>
                      <span className="metric-lbl">Databases</span>
                    </div>
                  )}
                  {selectedNode.messagingCount > 0 && (
                    <div className="inspector-metric-card" title="Topics published or subscribed">
                      <span className="metric-val">{selectedNode.messagingCount}</span>
                      <span className="metric-lbl">Topics</span>
                    </div>
                  )}
                </>
              )}
            </div>

            {/* Action buttons */}
            <div className="inspector-actions">
              {(selectedNode.kind === 'Service' || selectedNode.kind === 'Ingress') && onFocusInFlow && (
                <button
                  className="inspector-action-btn primary"
                  onClick={() => onFocusInFlow(selectedNode.name)}
                  title="Drill down to Project Flow (C2) view"
                >
                  Explore in Flow (C2) ➔
                </button>
              )}
              {selectedNode.primaryFilePath && onOpenFile && (
                <button
                  className="inspector-action-btn secondary"
                  onClick={() => onOpenFile(selectedNode.primaryFilePath!, 1)}
                  title="Open source file in editor"
                >
                  Open Source
                </button>
              )}
            </div>
          </div>
        </aside>
      )}
    </div>
  );
};
