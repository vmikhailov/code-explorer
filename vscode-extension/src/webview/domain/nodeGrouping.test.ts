import test from 'node:test';
import assert from 'node:assert/strict';
import { EntityKind, CustomNodeGroup, SelectedNodeDetail, ConnectionDetailItem } from './types';

// Helper pure function implementing the visual node grouping & edge bundling logic
export function computeGroupedGraph(options: {
  rawNodes: SelectedNodeDetail[];
  rawEdges: Array<{
    id: string;
    source: string;
    target: string;
    category: 'service_call' | 'database' | 'messaging' | 'external';
    kind?: string;
    label: string;
    count: number;
    isTransitive: boolean;
    isInferred?: boolean;
  }>;
  groupedTypes: Set<EntityKind>;
  customGroups: CustomNodeGroup[];
  hiddenNodeIds?: Set<string>;
}) {
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

    const groupDetail: SelectedNodeDetail = {
      id: groupId,
      name: group.name,
      displayName: `${group.name} (${memberNodes.length})`,
      kind: groupKind,
      isGroup: true,
      groupType: 'custom',
      customGroupId: group.id,
      groupKind: allSameKind ? groupKind : undefined,
      memberNodes,
      internalConnections: [],
      displayTag: allSameKind ? groupKind.slice(0, 3).toUpperCase() : 'GRP',
      bgColor: '#4f46e5',
      borderColor: '#312e81',
      tier: minTier,
      inboundCallsCount: 0,
      outboundCallsCount: 0,
      dbCount: 0,
      messagingCount: 0,
      projects: memberNodes.flatMap((m) => m.projects || []),
    };

    synthesizedGroupNodes.push(groupDetail);
  }

  // 2. Category Groups (for kinds in groupedTypes)
  const CATEGORY_PLURAL_LABELS: Record<string, string> = {
    Service: 'Services',
    Worker: 'Workers',
    TestSuite: 'Tests',
    Database: 'Databases',
    Topic: 'Topics',
    ExternalService: 'External',
    App: 'Apps',
    CliTool: 'CLI Tools',
    Library: 'Libraries',
  };

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

    const groupDetail: SelectedNodeDetail = {
      id: groupId,
      name: pluralLabel,
      displayName: `${pluralLabel} (${memberNodes.length})`,
      kind,
      isGroup: true,
      groupType: 'category',
      groupKind: kind,
      memberNodes,
      internalConnections: [],
      displayTag: kind.slice(0, 3).toUpperCase(),
      bgColor: memberNodes[0]?.bgColor || '#4f46e5',
      borderColor: memberNodes[0]?.borderColor || '#312e81',
      tier: minTier,
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

  // Edge aggregation & internal connection bundling
  const pairInteractionsMap = new Map<string, Array<{
    source: string;
    target: string;
    originalSourceId: string;
    originalSourceName: string;
    originalTargetId: string;
    originalTargetName: string;
    category: 'service_call' | 'database' | 'messaging' | 'external';
    kind: string;
    count: number;
    isTransitive: boolean;
  }>>();

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
          id: `${effU}:int:${e.source}->${e.target}:${e.category}`,
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
        kind: e.label,
        count: e.count,
        isTransitive: e.isTransitive,
      });
    }
  }

  // Synthesize bundled edges
  const bundledEdges: Array<{
    id: string;
    source: string;
    target: string;
    totalCount: number;
    category: string;
    items: Array<{ sourceId: string; sourceName: string; targetId: string; targetName: string; count: number }>;
  }> = [];

  for (const [pairKey, interactions] of pairInteractionsMap.entries()) {
    const hasUtoV = interactions.some((i) => i.source === pairKey.split('--')[0]);
    const edgeSource = hasUtoV ? pairKey.split('--')[0] : pairKey.split('--')[1];
    const edgeTarget = hasUtoV ? pairKey.split('--')[1] : pairKey.split('--')[0];

    const totalCount = interactions.reduce((sum, i) => sum + i.count, 0);
    const hasServiceCall = interactions.some((i) => i.category === 'service_call');
    const hasMessaging = interactions.some((i) => i.category === 'messaging');
    const hasDatabase = interactions.some((i) => i.category === 'database');

    let category = 'external';
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
      items: interactions.map((i) => ({
        sourceId: i.originalSourceId,
        sourceName: i.originalSourceName,
        targetId: i.originalTargetId,
        targetName: i.originalTargetName,
        count: i.count,
      })),
    });

    // Update group metrics
    const sDetail = effectiveDetailMap.get(edgeSource);
    const tDetail = effectiveDetailMap.get(edgeTarget);
    if (sDetail?.isGroup) {
      if (category === 'service_call') sDetail.outboundCallsCount += totalCount;
      if (category === 'database') sDetail.dbCount += totalCount;
      if (category === 'messaging') sDetail.messagingCount += totalCount;
    }
    if (tDetail?.isGroup) {
      if (category === 'service_call') tDetail.inboundCallsCount += totalCount;
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

// ======================= UNIT TESTS =======================

test('nodeGrouping: category grouping collapses >= 2 visible nodes into summary node with min tier', () => {
  const rawNodes: SelectedNodeDetail[] = [
    { id: 'db:users', name: 'UsersDb', displayName: 'Users DB', kind: 'Database', tier: 4, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: ':DB', bgColor: '#7b1fa2', borderColor: '#4a148c', projects: [] },
    { id: 'db:orders', name: 'OrdersDb', displayName: 'Orders DB', kind: 'Database', tier: 3, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: ':DB', bgColor: '#7b1fa2', borderColor: '#4a148c', projects: [] },
    { id: 'db:audit', name: 'AuditDb', displayName: 'Audit DB', kind: 'Database', tier: 4, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: ':DB', bgColor: '#7b1fa2', borderColor: '#4a148c', projects: [] },
  ];

  const result = computeGroupedGraph({
    rawNodes,
    rawEdges: [],
    groupedTypes: new Set<EntityKind>(['Database']),
    customGroups: [],
  });

  assert.equal(result.visibleNodes.length, 1);
  const groupNode = result.visibleNodes[0];
  assert.equal(groupNode.id, 'group:kind:Database');
  assert.equal(groupNode.displayName, 'Databases (3)');
  assert.equal(groupNode.isGroup, true);
  assert.equal(groupNode.groupType, 'category');
  assert.equal(groupNode.groupKind, 'Database');
  assert.equal(groupNode.tier, 3); // Minimum tier among 4, 3, 4 is 3
  assert.equal(groupNode.memberNodes?.length, 3);
});

test('nodeGrouping: category grouping does NOT collapse if fewer than 2 visible nodes exist', () => {
  const rawNodes: SelectedNodeDetail[] = [
    { id: 'db:users', name: 'UsersDb', displayName: 'Users DB', kind: 'Database', tier: 4, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: ':DB', bgColor: '#7b1fa2', borderColor: '#4a148c', projects: [] },
    { id: 'svc:api', name: 'Api', displayName: 'Api Service', kind: 'Service', tier: 1, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: ':Service', bgColor: '#e53935', borderColor: '#7f1d1d', projects: [] },
  ];

  const result = computeGroupedGraph({
    rawNodes,
    rawEdges: [],
    groupedTypes: new Set<EntityKind>(['Database']), // Only 1 database
    customGroups: [],
  });

  assert.equal(result.visibleNodes.length, 2);
  assert.equal(result.visibleNodes.some((n) => n.isGroup), false);
});

test('nodeGrouping: custom groups take precedence over category grouping', () => {
  const rawNodes: SelectedNodeDetail[] = [
    { id: 'db:orders', name: 'OrdersDb', displayName: 'Orders DB', kind: 'Database', tier: 4, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: ':DB', bgColor: '#7b1fa2', borderColor: '#4a148c', projects: [] },
    { id: 'svc:orders', name: 'OrdersService', displayName: 'Orders Service', kind: 'Service', tier: 2, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: ':Service', bgColor: '#e53935', borderColor: '#7f1d1d', projects: [] },
    { id: 'db:users', name: 'UsersDb', displayName: 'Users DB', kind: 'Database', tier: 4, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: ':DB', bgColor: '#7b1fa2', borderColor: '#4a148c', projects: [] },
    { id: 'db:catalog', name: 'CatalogDb', displayName: 'Catalog DB', kind: 'Database', tier: 4, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: ':DB', bgColor: '#7b1fa2', borderColor: '#4a148c', projects: [] },
  ];

  const customGroups: CustomNodeGroup[] = [
    {
      id: 'grp-orders',
      name: 'Order Subsystem',
      nodeIds: ['db:orders', 'svc:orders'],
      collapsed: true,
    },
  ];

  // GroupedTypes has Database enabled as well
  const result = computeGroupedGraph({
    rawNodes,
    rawEdges: [],
    groupedTypes: new Set<EntityKind>(['Database']),
    customGroups,
  });

  // Expected:
  // 1. Custom group "Order Subsystem (2)" contains 'db:orders' and 'svc:orders' with min tier 2
  // 2. Category group "Databases (2)" contains 'db:users' and 'db:catalog' (db:orders was claimed by custom group)
  assert.equal(result.visibleNodes.length, 2);

  const customNode = result.visibleNodes.find((n) => n.id === 'group:custom:grp-orders')!;
  assert.ok(customNode);
  assert.equal(customNode.displayName, 'Order Subsystem (2)');
  assert.equal(customNode.tier, 2); // Math.min(4, 2)
  assert.equal(customNode.memberNodes?.length, 2);

  const catNode = result.visibleNodes.find((n) => n.id === 'group:kind:Database')!;
  assert.ok(catNode);
  assert.equal(catNode.displayName, 'Databases (2)');
  assert.equal(catNode.memberNodes?.length, 2);
  assert.ok(catNode.memberNodes?.some((m) => m.id === 'db:users'));
  assert.ok(catNode.memberNodes?.some((m) => m.id === 'db:catalog'));
});

test('nodeGrouping: external edges bundle with preserved original endpoints, internal edges become internalConnections', () => {
  const rawNodes: SelectedNodeDetail[] = [
    { id: 'svc:api', name: 'Api', displayName: 'Gateway API', kind: 'Service', tier: 0, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: ':Service', bgColor: '#e53935', borderColor: '#7f1d1d', projects: [] },
    { id: 'svc:users', name: 'UsersService', displayName: 'Users Service', kind: 'Service', tier: 2, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: ':Service', bgColor: '#e53935', borderColor: '#7f1d1d', projects: [] },
    { id: 'db:users', name: 'UsersDb', displayName: 'Users DB', kind: 'Database', tier: 4, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: ':DB', bgColor: '#7b1fa2', borderColor: '#4a148c', projects: [] },
  ];

  const rawEdges = [
    // External call: Gateway API -> Users Service (count 5)
    { id: 'e1', source: 'svc:api', target: 'svc:users', category: 'service_call' as const, label: 'CALLS', count: 5, isTransitive: false },
    // Internal call within Users Domain: Users Service -> Users DB (count 3)
    { id: 'e2', source: 'svc:users', target: 'db:users', category: 'database' as const, label: 'USES_DB', count: 3, isTransitive: false },
  ];

  const customGroups: CustomNodeGroup[] = [
    {
      id: 'grp-users-domain',
      name: 'Users Domain',
      nodeIds: ['svc:users', 'db:users'],
      collapsed: true,
    },
  ];

  const result = computeGroupedGraph({
    rawNodes,
    rawEdges,
    groupedTypes: new Set<EntityKind>(),
    customGroups,
  });

  // Visible nodes: Gateway API + Users Domain
  assert.equal(result.visibleNodes.length, 2);
  const groupNode = result.visibleNodes.find((n) => n.id === 'group:custom:grp-users-domain')!;
  assert.ok(groupNode);

  // 1. Internal connection verified
  assert.equal(groupNode.internalConnections?.length, 1);
  const internalConn = groupNode.internalConnections![0];
  assert.equal(internalConn.sourceId, 'svc:users');
  assert.equal(internalConn.sourceName, 'Users Service');
  assert.equal(internalConn.targetId, 'db:users');
  assert.equal(internalConn.targetName, 'Users DB');
  assert.equal(internalConn.category, 'database');
  assert.equal(internalConn.count, 3);

  // 2. Bundled edge between Gateway API and Users Domain verified
  assert.equal(result.bundledEdges.length, 1);
  const edge = result.bundledEdges[0];
  assert.equal(edge.source, 'svc:api');
  assert.equal(edge.target, 'group:custom:grp-users-domain');
  assert.equal(edge.totalCount, 5);
  assert.equal(edge.category, 'service_call');

  // Member names preserved in edge items
  assert.equal(edge.items.length, 1);
  assert.equal(edge.items[0].sourceName, 'Gateway API');
  assert.equal(edge.items[0].targetName, 'Users Service');

  // Group node metrics updated
  assert.equal(groupNode.inboundCallsCount, 5);
});
