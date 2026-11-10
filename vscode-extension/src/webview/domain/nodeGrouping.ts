import {
  SelectedNodeDetail,
  EntityKind,
  CustomNodeGroup,
  ConnectionDetailItem,
} from './types';
import { RawEdgeInfo } from './inspectorSchema';
import { getEntityBadgeMeta } from './entityBadges';

export const CATEGORY_PLURAL_LABELS: Record<string, string> = {
  Service: 'Services',
  Worker: 'Workers',
  TestSuite: 'Tests',
  Database: 'Databases',
  Table: 'Tables',
  Procedure: 'Procedures',
  Topic: 'Topics',
  ExternalService: 'External',
  CloudService: 'Cloud Services',
  App: 'Apps',
  CliTool: 'CLI Tools',
  Library: 'Libraries',
  Endpoint: 'Endpoints',
  EntryPoint: 'EntryPoints',
  Type: 'Classes',
  Module: 'Modules',
};

export interface BundledGroupEdgeItem {
  id?: string;
  sourceId: string;
  sourceName: string;
  targetId: string;
  targetName: string;
  category: 'service_call' | 'database' | 'messaging' | 'external' | 'library' | 'test';
  kind: string;
  label: string;
  count: number;
  isTransitive?: boolean;
}

export interface BundledGroupEdge {
  id: string;
  source: string;
  target: string;
  totalCount: number;
  category: 'service_call' | 'database' | 'messaging' | 'external' | 'library' | 'test' | 'mixed';
  items: Array<{
    sourceId: string;
    sourceName: string;
    targetId: string;
    targetName: string;
    count: number;
    category?: string;
    kind?: string;
    label?: string;
  }>;
  isBidirectional?: boolean;
  hasForward?: boolean;
  hasReverse?: boolean;
}

export interface ComputeGroupedGraphOptions {
  rawNodes: SelectedNodeDetail[];
  rawEdges: Array<{
    id: string;
    source: string;
    target: string;
    category: 'service_call' | 'database' | 'messaging' | 'external' | 'library' | 'test';
    kind?: string;
    label: string;
    count: number;
    isTransitive?: boolean;
    isInferred?: boolean;
  }>;
  groupedTypes: Set<EntityKind>;
  customGroups: CustomNodeGroup[];
  hiddenNodeIds?: Set<string>;
}

export interface ComputeGroupedGraphResult {
  visibleNodes: SelectedNodeDetail[];
  bundledEdges: BundledGroupEdge[];
  nodeToGroupId: Map<string, string>;
  effectiveDetailMap: Map<string, SelectedNodeDetail>;
}

/**
 * Pure function implementing visual node grouping & edge bundling logic.
 * Collapses nodes by type or custom group, reroutes incident edges,
 * extracts internal connections into group node details, and aggregates metrics.
 */
export function computeGroupedGraph(options: ComputeGroupedGraphOptions): ComputeGroupedGraphResult {
  const { rawNodes, rawEdges, groupedTypes, customGroups, hiddenNodeIds = new Set<string>() } = options;

  const detailMap = new Map<string, SelectedNodeDetail>(rawNodes.map((n) => [n.id, { ...n }]));
  const unhiddenNodes = rawNodes.filter((n) => !hiddenNodeIds.has(n.id));
  const unhiddenNodeIds = new Set(unhiddenNodes.map((n) => n.id));

  const nodeToGroupId = new Map<string, string>();
  const synthesizedGroupNodes: SelectedNodeDetail[] = [];

  // 1. Custom Groups (precedence)
  for (const group of customGroups) {
    if (!group.collapsed) continue;
    const memberIds = group.nodeIds.filter((id) => unhiddenNodeIds.has(id));
    if (memberIds.length < 1) continue;

    const groupId = `group:custom:${group.id}`;
    for (const mId of memberIds) {
      nodeToGroupId.set(mId, groupId);
    }

    const memberNodes = memberIds.map((id) => detailMap.get(id)!).filter(Boolean);
    const memberTiers = memberNodes.map((m) => m.tier ?? 2);
    const minTier = memberTiers.length > 0 ? Math.min(...memberTiers) : 2;

    const allSameKind = memberNodes.length > 0 && memberNodes.every((m) => m.kind === memberNodes[0].kind);
    const groupKind: EntityKind = allSameKind ? memberNodes[0].kind : 'Group';
    const badgeMeta = getEntityBadgeMeta(groupKind);

    const groupDetail: SelectedNodeDetail = {
      id: groupId,
      name: group.name,
      displayName: `${group.name} (${memberNodes.length})`,
      kind: groupKind,
      isGroup: true,
      groupType: 'custom',
      customGroupId: group.id,
      groupKind: allSameKind ? groupKind : undefined,
      memberIds,
      memberNodes,
      internalConnections: [],
      displayTag: allSameKind ? badgeMeta.code : 'GRP',
      bgColor: badgeMeta.bgColor || '#4f46e5',
      borderColor: badgeMeta.borderColor || '#312e81',
      tier: minTier,
      tierLabel: `Tier ${minTier}`,
      inboundCallsCount: 0,
      outboundCallsCount: 0,
      dbCount: 0,
      messagingCount: 0,
      projects: memberNodes.flatMap((m) => m.projects || []),
    };

    synthesizedGroupNodes.push(groupDetail);
  }

  // 2. Category Groups (for kinds in groupedTypes)
  for (const kind of groupedTypes) {
    const memberIds = unhiddenNodes
      .filter((n) => (n.kind === kind || (kind === 'App' && n.kind === 'Ingress')) && !nodeToGroupId.has(n.id))
      .map((n) => n.id);

    if (memberIds.length < 2) continue; // Minimum 2 nodes to collapse category

    const groupId = `group:kind:${kind}`;
    for (const mId of memberIds) {
      nodeToGroupId.set(mId, groupId);
    }

    const memberNodes = memberIds.map((id) => detailMap.get(id)!).filter(Boolean);
    const memberTiers = memberNodes.map((m) => m.tier ?? 2);
    const minTier = memberTiers.length > 0 ? Math.min(...memberTiers) : 2;
    const pluralLabel = CATEGORY_PLURAL_LABELS[kind] || `${kind}s`;
    const badgeMeta = getEntityBadgeMeta(kind);

    const groupDetail: SelectedNodeDetail = {
      id: groupId,
      name: pluralLabel,
      displayName: `${pluralLabel} (${memberNodes.length})`,
      kind,
      isGroup: true,
      groupType: 'category',
      groupKind: kind,
      memberIds,
      memberNodes,
      internalConnections: [],
      displayTag: badgeMeta.code,
      bgColor: memberNodes[0]?.bgColor || badgeMeta.bgColor || '#4f46e5',
      borderColor: memberNodes[0]?.borderColor || badgeMeta.borderColor || '#312e81',
      tier: minTier,
      tierLabel: `Tier ${minTier}`,
      inboundCallsCount: 0,
      outboundCallsCount: 0,
      dbCount: 0,
      messagingCount: 0,
      projects: memberNodes.flatMap((m) => m.projects || []),
    };

    synthesizedGroupNodes.push(groupDetail);
  }

  const effectiveDetailMap = new Map<string, SelectedNodeDetail>(detailMap);
  for (const g of synthesizedGroupNodes) {
    effectiveDetailMap.set(g.id, g);
  }

  const visibleUngrouped = unhiddenNodes.filter((n) => !nodeToGroupId.has(n.id));
  const finalVisibleNodes = [...visibleUngrouped, ...synthesizedGroupNodes];
  const finalVisibleNodeIds = new Set(finalVisibleNodes.map((n) => n.id));

  // 3. Edge aggregation & internal connection bundling
  const pairInteractionsMap = new Map<
    string,
    Array<{
      source: string;
      target: string;
      originalSourceId: string;
      originalSourceName: string;
      originalTargetId: string;
      originalTargetName: string;
      category: 'service_call' | 'database' | 'messaging' | 'external' | 'library' | 'test';
      kind: string;
      label: string;
      count: number;
      isTransitive: boolean;
    }>
  >();

  const getPairKey = (u: string, v: string) => (u < v ? `${u}--${v}` : `${v}--${u}`);

  for (const e of rawEdges) {
    if (!unhiddenNodeIds.has(e.source) || !unhiddenNodeIds.has(e.target)) continue;

    const effU = nodeToGroupId.get(e.source) ?? e.source;
    const effV = nodeToGroupId.get(e.target) ?? e.target;

    if (effU === effV && e.source !== e.target) {
      // Internal Connection within group
      const gDetail = effectiveDetailMap.get(effU);
      if (gDetail && gDetail.internalConnections) {
        gDetail.internalConnections.push({
          id: `${effU}:int:${e.source}->${e.target}:${e.category}:${e.kind || e.label}`,
          sourceId: e.source,
          sourceName: detailMap.get(e.source)?.displayName || e.source,
          targetId: e.target,
          targetName: detailMap.get(e.target)?.displayName || e.target,
          category: e.category,
          kind: e.kind || e.label || 'CALLS',
          label: e.label,
          count: e.count,
          isTransitive: Boolean(e.isTransitive),
        });
      }
    } else if (effU !== effV && finalVisibleNodeIds.has(effU) && finalVisibleNodeIds.has(effV)) {
      const pKey = getPairKey(effU, effV);
      let list = pairInteractionsMap.get(pKey);
      if (!list) {
        list = [];
        pairInteractionsMap.set(pKey, list);
      }
      list.push({
        source: effU,
        target: effV,
        originalSourceId: e.source,
        originalSourceName: detailMap.get(e.source)?.displayName || e.source,
        originalTargetId: e.target,
        originalTargetName: detailMap.get(e.target)?.displayName || e.target,
        category: e.category,
        kind: e.kind || e.label || 'CALLS',
        label: e.label,
        count: e.count,
        isTransitive: Boolean(e.isTransitive),
      });
    }
  }

  // 4. Synthesize bundled edges
  const bundledEdges: BundledGroupEdge[] = [];

  for (const [pairKey, interactions] of pairInteractionsMap.entries()) {
    const [u, v] = pairKey.split('--');
    const hasUtoV = interactions.some((i) => i.source === u && i.target === v);
    const hasVtoU = interactions.some((i) => i.source === v && i.target === u);
    const isBidirectional = hasUtoV && hasVtoU;

    const edgeSource = hasUtoV ? u : v;
    const edgeTarget = hasUtoV ? v : u;

    const totalCount = interactions.reduce((sum, i) => sum + i.count, 0);
    const hasServiceCall = interactions.some((i) => i.category === 'service_call' || i.category === 'library' || i.category === 'test');
    const hasMessaging = interactions.some((i) => i.category === 'messaging');
    const hasDatabase = interactions.some((i) => i.category === 'database');

    let category: BundledGroupEdge['category'] = 'external';
    if (hasServiceCall && hasMessaging) category = 'mixed';
    else if (hasServiceCall) category = 'service_call';
    else if (hasMessaging) category = 'messaging';
    else if (hasDatabase) category = 'database';

    bundledEdges.push({
      id: `edge:${pairKey}`,
      source: edgeSource,
      target: edgeTarget,
      totalCount,
      category,
      isBidirectional,
      hasForward: hasUtoV,
      hasReverse: hasVtoU,
      items: interactions.map((i) => ({
        sourceId: i.originalSourceId,
        sourceName: i.originalSourceName,
        targetId: i.originalTargetId,
        targetName: i.originalTargetName,
        count: i.count,
        category: i.category,
        kind: i.kind,
        label: i.label,
      })),
    });

    // Update group metrics
    const sDetail = effectiveDetailMap.get(edgeSource);
    const tDetail = effectiveDetailMap.get(edgeTarget);
    if (sDetail?.isGroup) {
      if (category === 'service_call' || category === 'mixed') sDetail.outboundCallsCount += totalCount;
      if (category === 'database') sDetail.dbCount += totalCount;
      if (category === 'messaging') sDetail.messagingCount += totalCount;
    }
    if (tDetail?.isGroup) {
      if (category === 'service_call' || category === 'mixed') tDetail.inboundCallsCount += totalCount;
      if (category === 'database') tDetail.dbCount += totalCount;
      if (category === 'messaging') tDetail.messagingCount += totalCount;
    }
  }

  return {
    visibleNodes: finalVisibleNodes,
    bundledEdges,
    nodeToGroupId,
    effectiveDetailMap,
  };
}
