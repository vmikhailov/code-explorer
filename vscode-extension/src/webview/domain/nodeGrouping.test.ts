import test from 'node:test';
import assert from 'node:assert/strict';
import { EntityKind, CustomNodeGroup, SelectedNodeDetail } from './types';
import { computeGroupedGraph } from './nodeGrouping';

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

test( ' nodeGrouping: downstream traversal finds all transitive downstream nodes ' , () => {
  const outAdj = new Map<string, Array<{ target: string }>>([
    [ ' svc:api ' , [{ target:  ' svc:order '  }, { target:  ' svc:payment '  }]],
    [ ' svc:order ' , [{ target:  ' db:order '  }]],
    [ ' svc:payment ' , []],
    [ ' db:order ' , []],
  ]);

  function getDownstreamNodeIds(rootNodeId: string): string[] {
    const downstreamIds: string[] = [];
    const queue = [rootNodeId];
    const visited = new Set<string>([rootNodeId]);

    while (queue.length > 0) {
      const curr = queue.shift()!;
      const outgoing = outAdj.get(curr) || [];
      for (const edge of outgoing) {
        if (!visited.has(edge.target)) {
          visited.add(edge.target);
          downstreamIds.push(edge.target);
          queue.push(edge.target);
        }
      }
    }
    return downstreamIds;
  }

  const downstreamOfApi = getDownstreamNodeIds( ' svc:api ' );
  assert.deepEqual(downstreamOfApi, [ ' svc:order ' ,  ' svc:payment ' ,  ' db:order ' ]);

  const downstreamOfOrder = getDownstreamNodeIds( ' svc:order ' );
  assert.deepEqual(downstreamOfOrder, [ ' db:order ' ]);

  const downstreamOfDb = getDownstreamNodeIds( ' db:order ' );
  assert.deepEqual(downstreamOfDb, []);
});