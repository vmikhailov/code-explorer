import { SelectedNodeDetail, SelectedEdgeDetail, DomainProjectInfo, ConnectionDetailItem } from './types';
import { getEntityBadgeMeta } from './entityBadges';

export interface RawEdgeInfo {
  id: string;
  source: string;
  target: string;
  category: 'service_call' | 'database' | 'messaging' | 'external' | 'library' | 'test';
  label: string;
  count: number;
  kind?: string;
}

export interface InspectorConnectionItem {
  id: string;
  name: string;
  kind: string;
  kindTag?: string;
  bgColor?: string;
  count?: number;
  badgeText?: string;
  directionLabel?: string;
  sourceIds?: string[];
  targetIds?: string[];
  edgeKind?: string;
}

export interface InspectorFileItem {
  id?: string;
  label: string;
  path?: string;
  line?: number;
  badge?: string;
  secondaryRoles?: string[];
  gitBranch?: string;
}

export interface InspectorActionItem<T> {
  id: string;
  label: string;
  variant?: 'primary' | 'secondary';
  icon?: 'tag' | 'external-link' | 'none';
  tooltip?: string;
  when?: (item: T) => boolean;
  onClick: (item: T) => void;
}

export interface InspectorKpiMetric<T> {
  id: string;
  label: string;
  tooltip?: string;
  color?: string;
  value: (item: T) => number | string;
  when?: (item: T) => boolean;
}

export interface InspectorHeaderChip {
  text: string;
  icon?: 'branch' | 'domain' | 'none';
  variant?: 'default' | 'branch' | 'domain';
}

export interface InspectorHeaderSchema<TNode> {
  getId: (n: TNode) => string;
  getTitle: (n: TNode) => string;
  getKind: (n: TNode) => string;
  getSecondaryRoles?: (n: TNode) => string[];
  getTier?: (n: TNode) => number | undefined;
  getTierLabel?: (n: TNode) => string | undefined;
  /** Explainability: Why was this node assigned to its tier/orbit? */
  getTierReason?: (n: TNode) => string | undefined;
  getChips?: (n: TNode) => InspectorHeaderChip[];
  isGroup?: (n: TNode) => boolean;
}

export interface InspectorEdgeHeaderSchema<TEdge> {
  getTitle: (e: TEdge) => string;
  getDirectionBadge: (e: TEdge) => { text: string; isBidirectional: boolean };
  getCollapsedCount: (e: TEdge) => number | string;
  getEndpoints: (e: TEdge) => {
    sourceId: string;
    sourceName: string;
    sourceKind: string;
    targetId: string;
    targetName: string;
    targetKind: string;
    isBidirectional: boolean;
  };
}

export type InspectorSection<TNode, TEdge> =
  | {
      type: 'actions';
      id: string;
      when?: (item: TNode | TEdge) => boolean;
      items: Array<InspectorActionItem<any>>;
    }
  | {
      type: 'kpi-grid';
      id: string;
      when?: (item: TNode | TEdge) => boolean;
      columns?: 2 | 3 | 4;
      metrics: Array<InspectorKpiMetric<any>>;
    }
  | {
      type: 'group-members';
      id: string;
      title: string | ((n: TNode) => string);
      when?: (n: TNode) => boolean;
      getMembers: (n: TNode) => SelectedNodeDetail[];
      canRemoveMember?: (n: TNode) => boolean;
      onRemoveMember?: (n: TNode, memberId: string) => void;
    }
  | {
      type: 'internal-connections';
      id: string;
      title: string | ((n: TNode) => string);
      when?: (n: TNode) => boolean;
      getItems: (n: TNode) => ConnectionDetailItem[];
    }
  | {
      type: 'connection-list';
      id: string;
      title: string | ((n: TNode) => string);
      when?: (n: TNode, items: InspectorConnectionItem[]) => boolean;
      emptyHint?: string | ((n: TNode) => string);
      getItems: (n: TNode) => InspectorConnectionItem[];
      inlineEvidence?: {
        mode: 'edge' | 'database';
        getParams: (
          n: TNode,
          row: InspectorConnectionItem
        ) => {
          sourceIds?: string[];
          targetIds?: string[];
          edgeKind?: string;
          databaseId?: string;
          databaseName?: string;
          serviceId?: string;
        };
      };
    }
  | {
      type: 'file-list';
      id: string;
      title: string | ((n: TNode) => string);
      when?: (n: TNode, files: InspectorFileItem[]) => boolean;
      monospace?: boolean;
      getFiles: (n: TNode) => InspectorFileItem[];
    }
  | {
      type: 'async-evidence';
      id: string;
      title: string;
      when?: (item: TNode | TEdge) => boolean;
      defaultExpanded?: boolean;
      endpoint: 'node-evidence' | 'edge-evidence' | 'database-evidence';
      getParams: (item: any) => {
        nodeIds?: string[];
        nodeKind?: string;
        sourceIds?: string[];
        targetIds?: string[];
        edgeKind?: string;
        databaseId?: string;
        databaseName?: string;
        serviceId?: string;
      };
    }
  | {
      type: 'edge-breakdown';
      id: string;
      title: string | ((e: TEdge) => string);
      when?: (e: TEdge) => boolean;
      getItems: (e: TEdge) => ConnectionDetailItem[];
    };

export interface InspectorSchema<TNode = SelectedNodeDetail, TEdge = SelectedEdgeDetail> {
  nodeHeader: InspectorHeaderSchema<TNode>;
  nodeSections: Array<InspectorSection<TNode, TEdge>>;
  edgeHeader?: InspectorEdgeHeaderSchema<TEdge>;
  edgeSections?: Array<InspectorSection<TNode, TEdge>>;
}

export interface EvaluatedHeaderModel {
  id: string;
  title: string;
  kind: string;
  secondaryRoles: string[];
  tier?: number;
  tierLabel?: string;
  tierReason?: string;
  chips: InspectorHeaderChip[];
  isGroup: boolean;
}

export function evaluateNodeHeader<TNode>(
  schema: InspectorSchema<TNode, any>,
  node: TNode
): EvaluatedHeaderModel {
  const h = schema.nodeHeader;
  return {
    id: h.getId(node),
    title: h.getTitle(node),
    kind: h.getKind(node),
    secondaryRoles: h.getSecondaryRoles?.(node) || [],
    tier: h.getTier?.(node),
    tierLabel: h.getTierLabel?.(node),
    tierReason: h.getTierReason?.(node),
    chips: h.getChips?.(node) || [],
    isGroup: Boolean(h.isGroup?.(node)),
  };
}

export function computeNodeConnections(
  selectedNode: SelectedNodeDetail | null,
  rawEdges: RawEdgeInfo[] | undefined,
  nodeDetailMap: Map<string, SelectedNodeDetail> | undefined
) {
  if (!selectedNode || !rawEdges) {
    return {
      inboundConnections: [] as InspectorConnectionItem[],
      outboundConnections: [] as InspectorConnectionItem[],
      dbConnections: [] as InspectorConnectionItem[],
      topicConnections: [] as InspectorConnectionItem[],
    };
  }

  // 1. Inbound Callers
  const inMap = new Map<string, InspectorConnectionItem>();
  for (const e of rawEdges) {
    if (e.target === selectedNode.id && (e.category === 'service_call' || e.category === 'external' || e.category === 'library' || e.category === 'test')) {
      const sourceDetail = nodeDetailMap?.get(e.source);
      const name = sourceDetail?.displayName || e.source;
      const existing = inMap.get(e.source);
      if (existing) {
        existing.count = (existing.count || 0) + e.count;
      } else {
        const k = sourceDetail?.kind || 'Service';
        inMap.set(e.source, {
          id: e.source,
          name,
          kind: k,
          kindTag: getEntityBadgeMeta(k).code,
          bgColor: sourceDetail?.bgColor || getEntityBadgeMeta(k).bgColor,
          count: e.count,
          directionLabel: e.label,
        });
      }
    }
  }
  const inboundConnections = Array.from(inMap.values()).sort((a, b) => (b.count || 0) - (a.count || 0));

  // 2. Outbound Targets
  const outMap = new Map<string, InspectorConnectionItem>();
  for (const e of rawEdges) {
    if (e.source === selectedNode.id && (e.category === 'service_call' || e.category === 'external' || e.category === 'library' || e.category === 'test')) {
      const targetDetail = nodeDetailMap?.get(e.target);
      const name = targetDetail?.displayName || e.target;
      const existing = outMap.get(e.target);
      if (existing) {
        existing.count = (existing.count || 0) + e.count;
      } else {
        const k = targetDetail?.kind || 'Service';
        outMap.set(e.target, {
          id: e.target,
          name,
          kind: k,
          kindTag: getEntityBadgeMeta(k).code,
          bgColor: targetDetail?.bgColor || getEntityBadgeMeta(k).bgColor,
          count: e.count,
          directionLabel: e.label,
        });
      }
    }
  }
  const outboundConnections = Array.from(outMap.values()).sort((a, b) => (b.count || 0) - (a.count || 0));

  // 3. Database / Table / Procedure Connections
  const dbMap = new Map<string, InspectorConnectionItem>();
  for (const e of rawEdges) {
    if (e.source === selectedNode.id && e.category === 'database') {
      const targetDetail = nodeDetailMap?.get(e.target);
      const name = targetDetail?.displayName || e.target;
      const existing = dbMap.get(e.target);
      if (existing) {
        existing.count = (existing.count || 0) + e.count;
      } else {
        const k = targetDetail?.kind || 'Database';
        dbMap.set(e.target, {
          id: e.target,
          name,
          kind: k,
          kindTag: getEntityBadgeMeta(k).code,
          bgColor: targetDetail?.bgColor || getEntityBadgeMeta(k).bgColor,
          count: e.count,
        });
      }
    } else if (e.target === selectedNode.id && e.category === 'database') {
      const sourceDetail = nodeDetailMap?.get(e.source);
      const name = sourceDetail?.displayName || e.source;
      const existing = dbMap.get(e.source);
      if (existing) {
        existing.count = (existing.count || 0) + e.count;
      } else {
        const k = sourceDetail?.kind || 'Service';
        dbMap.set(e.source, {
          id: e.source,
          name,
          kind: k,
          kindTag: getEntityBadgeMeta(k).code,
          bgColor: sourceDetail?.bgColor || getEntityBadgeMeta(k).bgColor,
          count: e.count,
        });
      }
    }
  }
  const dbConnections = Array.from(dbMap.values()).sort((a, b) => (b.count || 0) - (a.count || 0));

  // 4. Topic / Messaging Connections
  const topicMap = new Map<string, InspectorConnectionItem>();
  for (const e of rawEdges) {
    if (e.category !== 'messaging') continue;
    const isSource = e.source === selectedNode.id;
    const isTarget = e.target === selectedNode.id;
    if (!isSource && !isTarget) continue;

    const otherId = isSource ? e.target : e.source;
    const otherDetail = nodeDetailMap?.get(otherId);
    const name = otherDetail?.displayName || otherId;
    const kUpper = (e.kind || e.label || '').toUpperCase();
    const isSub =
      kUpper === 'SUBSCRIBES_TO' ||
      kUpper === 'SUBSCRIBED_BY' ||
      kUpper === 'SUBSCRIBES' ||
      kUpper === 'CONSUMES' ||
      kUpper === 'CONSUMES_FROM';

    const label =
      selectedNode.kind === 'Topic'
        ? isSub
          ? 'Subscriber'
          : 'Publisher'
        : isSub
        ? 'Consumes'
        : 'Publishes';

    const existing = topicMap.get(otherId);
    if (existing) {
      existing.count = (existing.count || 0) + e.count;
    } else {
      const k = otherDetail?.kind || 'Topic';
      topicMap.set(otherId, {
        id: otherId,
        name,
        kind: k,
        kindTag: getEntityBadgeMeta(k).code,
        bgColor: otherDetail?.bgColor || getEntityBadgeMeta(k).bgColor,
        count: e.count,
        badgeText: label,
      });
    }
  }
  const topicConnections = Array.from(topicMap.values()).sort((a, b) => (b.count || 0) - (a.count || 0));

  return {
    inboundConnections,
    outboundConnections,
    dbConnections,
    topicConnections,
  };
}

export interface ArchitectureInspectorHandlers {
  rawEdges?: RawEdgeInfo[];
  nodeDetailMap?: Map<string, SelectedNodeDetail>;
  onFocusInFlow?: (name: string) => void;
  onOpenProjectMap?: (name: string) => void;
  onOpenFile?: (path: string, line?: number) => void;
  onOpenOverrideModal?: (name: string) => void;
  onRemoveFromGroup?: (groupId: string, nodeId: string) => void;
}

/**
 * Declarative 6-Zone Inspector Schema for the Domain Architecture Map & Project Map.
 */
export function createArchitectureInspectorSchema(
  handlers: ArchitectureInspectorHandlers
): InspectorSchema<SelectedNodeDetail, SelectedEdgeDetail> {
  return {
    nodeHeader: {
      getId: (n) => n.id,
      getTitle: (n) => n.displayName || n.name,
      getKind: (n) => n.kind,
      getSecondaryRoles: (n) => n.secondaryRoles || [],
      getTier: (n) => n.tier,
      getTierLabel: (n) => n.tierLabel,
      getTierReason: (n) => n.tierReason,
      isGroup: (n) => Boolean(n.isGroup),
      getChips: (n) => {
        const chips: InspectorHeaderChip[] = [];
        if (n.framework) chips.push({ text: n.framework });
        if (n.language) chips.push({ text: n.language });
        if (n.gitBranch) chips.push({ text: n.gitBranch, icon: 'branch', variant: 'branch' });
        if (n.domain) chips.push({ text: n.domain, icon: 'domain', variant: 'domain' });
        return chips;
      },
    },
    nodeSections: [
      // Zone 2: Quick Actions Bar
      {
        type: 'actions',
        id: 'quick-actions',
        items: [
          {
            id: 'explore-flow',
            label: 'Explore in Flow ➔',
            variant: 'primary',
            tooltip: 'Drill down to Project Flow view',
            when: (n: SelectedNodeDetail) =>
              Boolean(handlers.onFocusInFlow) && (n.kind === 'Service' || n.kind === 'Ingress'),
            onClick: (n: SelectedNodeDetail) => handlers.onFocusInFlow?.(n.name),
          },
          {
            id: 'open-project-map',
            label: 'Open in Project Map',
            variant: 'secondary',
            tooltip: 'Open the internal module map (C3) of this project',
            when: (n: SelectedNodeDetail) =>
              Boolean(handlers.onOpenProjectMap) &&
              ['Service', 'Ingress', 'App', 'Worker', 'CliTool', 'Library'].includes(n.kind),
            onClick: (n: SelectedNodeDetail) => handlers.onOpenProjectMap?.(n.name),
          },
          {
            id: 'override-domain',
            label: 'Override Domain',
            variant: 'secondary',
            icon: 'tag',
            tooltip: 'Override or reassign service domain and bounded context',
            when: (n: SelectedNodeDetail) =>
              Boolean(handlers.onOpenOverrideModal) && (n.kind === 'Service' || n.kind === 'Ingress'),
            onClick: (n: SelectedNodeDetail) => handlers.onOpenOverrideModal?.(n.name),
          },
          {
            id: 'open-source',
            label: 'Open Source',
            variant: 'secondary',
            icon: 'external-link',
            tooltip: 'Open source file in editor',
            when: (n: SelectedNodeDetail) => Boolean(handlers.onOpenFile && n.primaryFilePath),
            onClick: (n: SelectedNodeDetail) =>
              handlers.onOpenFile?.(n.primaryFilePath!, n.primaryLineStart || 1),
          },
        ],
      },
      // Zone 3: KPI Metrics Grid
      {
        type: 'kpi-grid',
        id: 'metrics-grid',
        when: (n) =>
          Boolean(
            (n as SelectedNodeDetail).isGroup ||
              ['Service', 'Ingress', 'Endpoint', 'EntryPoint', 'Type', 'Worker', 'App', 'Module'].includes(
                (n as SelectedNodeDetail).kind
              )
          ),
        columns: 2,
        metrics: [
          {
            id: 'inbound',
            label: 'Inbound Calls',
            tooltip: 'Inbound calls from other services/modules',
            value: (n: SelectedNodeDetail) => n.inboundCallsCount,
          },
          {
            id: 'outbound',
            label: 'Outbound Calls',
            tooltip: 'Outbound calls to downstream services/modules',
            value: (n: SelectedNodeDetail) => n.outboundCallsCount,
          },
          {
            id: 'databases',
            label: 'Databases',
            tooltip: 'Databases or tables used directly',
            value: (n: SelectedNodeDetail) => n.dbCount,
          },
          {
            id: 'topics',
            label: 'Topics',
            tooltip: 'Topics published or subscribed',
            value: (n: SelectedNodeDetail) => n.messagingCount,
          },
        ],
      },
      // Group Members (Polymorphic Group Inspection)
      {
        type: 'group-members',
        id: 'grouped-entities',
        title: (n) => `Grouped Entities (${n.memberNodes?.length || 0})`,
        when: (n) => Boolean(n.isGroup && n.memberNodes && n.memberNodes.length > 0),
        getMembers: (n) => n.memberNodes || [],
        canRemoveMember: (n) =>
          Boolean(n.groupType === 'custom' && n.customGroupId && handlers.onRemoveFromGroup),
        onRemoveMember: (n, memberId) => {
          if (n.customGroupId && handlers.onRemoveFromGroup) {
            handlers.onRemoveFromGroup(n.customGroupId, memberId);
          }
        },
      },
      // Internal Connections within Group
      {
        type: 'internal-connections',
        id: 'internal-connections',
        title: (n) => `Internal Connections (${n.internalConnections?.length || 0})`,
        when: (n) => Boolean(n.isGroup && n.internalConnections && n.internalConnections.length > 0),
        getItems: (n) => n.internalConnections || [],
      },
      // Zone 4a: Inbound Calls
      {
        type: 'connection-list',
        id: 'inbound',
        title: 'Inbound Calls',
        emptyHint: 'No inbound calls',
        when: (n, items) =>
          n.kind === 'Service' ||
          n.kind === 'Worker' ||
          n.kind === 'Ingress' ||
          n.kind === 'Type' ||
          n.kind === 'Module' ||
          items.length > 0,
        getItems: (n) =>
          computeNodeConnections(n, handlers.rawEdges, handlers.nodeDetailMap).inboundConnections,
        inlineEvidence: {
          mode: 'edge',
          getParams: (n, row) => ({
            sourceIds: [row.id],
            targetIds: n.isGroup && n.memberIds?.length ? n.memberIds : [n.id],
            edgeKind: 'service_call',
          }),
        },
      },
      // Zone 4b: Outbound Calls
      {
        type: 'connection-list',
        id: 'outbound',
        title: 'Outbound Calls',
        emptyHint: 'No outbound calls',
        when: (n, items) =>
          n.kind === 'Service' ||
          n.kind === 'Worker' ||
          n.kind === 'Ingress' ||
          n.kind === 'Endpoint' ||
          n.kind === 'EntryPoint' ||
          n.kind === 'Type' ||
          n.kind === 'App' ||
          n.kind === 'Module' ||
          items.length > 0,
        getItems: (n) =>
          computeNodeConnections(n, handlers.rawEdges, handlers.nodeDetailMap).outboundConnections,
        inlineEvidence: {
          mode: 'edge',
          getParams: (n, row) => ({
            sourceIds: n.isGroup && n.memberIds?.length ? n.memberIds : [n.id],
            targetIds: [row.id],
            edgeKind: row.kind === 'ExternalService' ? 'external' : 'service_call',
          }),
        },
      },
      // Zone 6a: Database Node Evidence
      {
        type: 'async-evidence',
        id: 'database-node-evidence',
        title: 'Database Evidence',
        when: (n) => (n as SelectedNodeDetail).kind === 'Database',
        defaultExpanded: true,
        endpoint: 'database-evidence',
        getParams: (n: SelectedNodeDetail) => ({
          databaseId: n.id,
          databaseName: n.displayName,
        }),
      },
      // Zone 4c: Databases / Connected Services
      {
        type: 'connection-list',
        id: 'databases',
        title: (n) =>
          n.kind === 'Database' || n.kind === 'Table' || n.kind === 'Procedure'
            ? 'Connected Services'
            : 'Databases',
        emptyHint: 'No database connections',
        when: (n, items) => items.length > 0 || n.dbCount > 0,
        getItems: (n) =>
          computeNodeConnections(n, handlers.rawEdges, handlers.nodeDetailMap).dbConnections,
        inlineEvidence: {
          mode: 'database',
          getParams: (n, row) => ({
            databaseId: n.kind === 'Database' ? n.id : row.id,
            databaseName: n.kind === 'Database' ? n.name : row.name,
            serviceId: n.kind === 'Database' ? row.id : n.id,
          }),
        },
      },
      // Zone 4d: Topics & Queues
      {
        type: 'connection-list',
        id: 'topics',
        title: (n) => (n.kind === 'Topic' ? 'Connected Services' : 'Topics & Queues'),
        emptyHint: (n) => (n.kind === 'Topic' ? 'No connected services' : 'No message topics'),
        when: (n, items) => items.length > 0 || n.messagingCount > 0,
        getItems: (n) =>
          computeNodeConnections(n, handlers.rawEdges, handlers.nodeDetailMap).topicConnections,
        inlineEvidence: {
          mode: 'edge',
          getParams: (n, row) => ({
            sourceIds: n.kind === 'Topic' ? [row.id] : [n.id],
            targetIds: n.kind === 'Topic' ? [n.id] : [row.id],
            edgeKind: 'messaging',
          }),
        },
      },
      // Zone 5a: Class Methods (C3 Type nodes)
      {
        type: 'file-list',
        id: 'class-methods',
        title: (n) => {
          try {
            const parsed = n.properties?.methods ? JSON.parse(n.properties.methods) : [];
            return `Methods (${Array.isArray(parsed) ? parsed.length : 0})`;
          } catch {
            return 'Methods';
          }
        },
        monospace: true,
        when: (n, items) => n.kind === 'Type' && items.length > 0,
        getFiles: (n) => {
          const raw = n.properties?.methods;
          if (!raw) return [];
          try {
            const parsed = JSON.parse(raw);
            if (!Array.isArray(parsed)) return [];
            return parsed.map((m: unknown, idx: number) => {
              if (typeof m === 'object' && m !== null) {
                const obj = m as { name?: string; path?: string; file?: string; line?: number };
                return {
                  id: `method-${idx}`,
                  label: `${obj.name || String(m)}()`,
                  path: obj.path || obj.file || n.primaryFilePath,
                  line: obj.line || n.primaryLineStart || 1,
                };
              }
              return {
                id: `method-${idx}`,
                label: `${String(m)}()`,
                path: n.primaryFilePath,
                line: n.primaryLineStart || 1,
              };
            });
          } catch {
            return [];
          }
        },
      },
      // Zone 5b: Clustered Subprojects / Files
      {
        type: 'file-list',
        id: 'projects',
        title: (n) =>
          n.kind === 'Module'
            ? `Files (${n.projects?.length || 0})`
            : `Clustered Projects (${n.projects?.length || 0})`,
        when: (n, files) => !n.isGroup && (n.kind === 'Module' ? files.length > 0 : files.length > 1),
        getFiles: (n) =>
          (n.projects || []).map((p: DomainProjectInfo) => ({
            id: p.id,
            label: p.name,
            path: p.filePath,
            line: p.lineStart || 1,
            badge: p.isLibrary ? 'lib' : undefined,
            secondaryRoles: p.secondaryRoles,
            gitBranch: p.gitBranch,
          })),
      },
      // Zone 5b: Physical Source Declaration
      {
        type: 'file-list',
        id: 'source',
        title: 'Source Declaration',
        monospace: true,
        when: (n, files) => files.length > 0,
        getFiles: (n) =>
          n.primaryFilePath
            ? [
                {
                  id: 'primary-source',
                  label: `${n.primaryFilePath}${n.primaryLineStart ? `:${n.primaryLineStart}` : ''}`,
                  path: n.primaryFilePath,
                  line: n.primaryLineStart || 1,
                },
              ]
            : [],
      },
      // Zone 6b: Universal Code Evidence for Node
      {
        type: 'async-evidence',
        id: 'node-code-evidence',
        title: 'Code Evidence',
        defaultExpanded: true,
        endpoint: 'node-evidence',
        getParams: (n: SelectedNodeDetail) => ({
          nodeIds:
            n.isGroup && n.memberIds && n.memberIds.length > 0
              ? n.memberIds
              : n.projects && n.projects.length > 0
              ? Array.from(new Set([n.id, ...n.projects.map((p: DomainProjectInfo) => p.id)]))
              : [n.id],
          nodeKind: n.kind,
        }),
      },
    ],
    edgeHeader: {
      getTitle: (e) => `${e.sourceName} ➔ ${e.targetName}`,
      getDirectionBadge: (e) => ({
        text: e.isBidirectional ? '⇄ Bi-directional' : '➔ Directional',
        isBidirectional: e.isBidirectional,
      }),
      getCollapsedCount: (e) => e.totalInteractions,
      getEndpoints: (e) => ({
        sourceId: e.sourceId,
        sourceName: e.sourceName,
        sourceKind: e.sourceKind || 'Service',
        targetId: e.targetId,
        targetName: e.targetName,
        targetKind: e.targetKind || 'Service',
        isBidirectional: e.isBidirectional,
      }),
    },
    edgeSections: [
      {
        type: 'kpi-grid',
        id: 'edge-metrics',
        metrics: [
          {
            id: 'total-links',
            label: 'Total Links',
            color: '#f8fafc',
            value: (e: SelectedEdgeDetail) => e.totalInteractions,
          },
          {
            id: 'direct-calls',
            label: 'Direct Calls',
            color: '#34d399',
            value: (e: SelectedEdgeDetail) => e.directCallsCount,
          },
          {
            id: 'events-topics',
            label: 'Events / Topics',
            color: '#fbbf24',
            value: (e: SelectedEdgeDetail) => e.messagingCount,
          },
          {
            id: 'transitive',
            label: 'Transitive',
            color: '#c084fc',
            when: (e: SelectedEdgeDetail) => e.transitiveCount > 0,
            value: (e: SelectedEdgeDetail) => e.transitiveCount,
          },
        ],
      },
      {
        type: 'async-evidence',
        id: 'edge-code-evidence',
        title: 'Evidence',
        defaultExpanded: true,
        endpoint: 'edge-evidence',
        getParams: (e: SelectedEdgeDetail) => ({
          sourceIds:
            e.items.length > 0
              ? Array.from(new Set(e.items.map((it: ConnectionDetailItem) => it.sourceId)))
              : [e.sourceId],
          targetIds:
            e.items.length > 0
              ? Array.from(new Set(e.items.map((it: ConnectionDetailItem) => it.targetId)))
              : [e.targetId],
          edgeKind: e.primaryCategory,
        }),
      },
      {
        type: 'edge-breakdown',
        id: 'edge-connection-details',
        title: (e) => `Connection Details (${e.items.length})`,
        getItems: (e) => e.items,
      },
      {
        type: 'actions',
        id: 'edge-footer-actions',
        when: () => Boolean(handlers.onFocusInFlow),
        items: [
          {
            id: 'focus-source',
            label: 'Focus Source',
            variant: 'secondary',
            onClick: (e: SelectedEdgeDetail) => handlers.onFocusInFlow?.(e.sourceName),
          },
          {
            id: 'focus-target',
            label: 'Focus Target',
            variant: 'secondary',
            onClick: (e: SelectedEdgeDetail) => handlers.onFocusInFlow?.(e.targetName),
          },
        ],
      },
    ],
  };
}
