import test from 'node:test';
import assert from 'node:assert/strict';
import { UniversalGraphMap, UniversalGraphMapProps } from './UniversalGraphMap';
import { computeGroupedGraph } from '../domain/nodeGrouping';
import { SelectedNodeDetail, CustomNodeGroup, EntityKind } from '../domain/types';

test('UniversalGraphMap: exports component and valid props interface', () => {
  assert.equal(typeof UniversalGraphMap, 'function');

  const props: UniversalGraphMapProps = {
    graph: { nodes: [], edges: [] },
    hudLabel: 'C3 Internal Structure',
    hudTitle: 'Project Map: Core',
    storagePrefix: 'ce_pm_v3_',
  };

  assert.equal(props.hudLabel, 'C3 Internal Structure');
  assert.equal(props.hudTitle, 'Project Map: Core');
  assert.equal(props.storagePrefix, 'ce_pm_v3_');
});

test('UniversalGraphMap: supports Module and Type category grouping on Project Map', () => {
  const rawNodes: SelectedNodeDetail[] = [
    { id: 'mod:auth', name: 'Auth', displayName: 'Auth Module', kind: 'Module', tier: 1, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: 'MOD', bgColor: '#3b82f6', borderColor: '#1d4ed8', projects: [] },
    { id: 'mod:users', name: 'Users', displayName: 'Users Module', kind: 'Module', tier: 2, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: 'MOD', bgColor: '#3b82f6', borderColor: '#1d4ed8', projects: [] },
    { id: 'mod:billing', name: 'Billing', displayName: 'Billing Module', kind: 'Module', tier: 2, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: 'MOD', bgColor: '#3b82f6', borderColor: '#1d4ed8', projects: [] },
    { id: 'tbl:orders', name: 'OrdersTable', displayName: 'Orders (Table)', kind: 'Table', tier: 5, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: 'TBL', bgColor: '#ec4899', borderColor: '#be185d', projects: [] },
  ];

  const rawEdges = [
    { id: 'e1', source: 'mod:auth', target: 'mod:users', category: 'service_call' as const, label: 'CALLS', count: 4, isTransitive: false },
    { id: 'e2', source: 'mod:users', target: 'tbl:orders', category: 'database' as const, label: 'USES_DB', count: 2, isTransitive: false },
  ];

  const result = computeGroupedGraph({
    rawNodes,
    rawEdges,
    groupedTypes: new Set<EntityKind>(['Module']),
    customGroups: [],
  });

  // 3 Modules collapsed into 1 group node; Table remains ungrouped
  assert.equal(result.visibleNodes.length, 2);
  const groupNode = result.visibleNodes.find((n) => n.id === 'group:kind:Module')!;
  assert.ok(groupNode);
  assert.equal(groupNode.displayName, 'Modules (3)');
  assert.equal(groupNode.isGroup, true);
  assert.equal(groupNode.tier, 1); // min(1, 2, 2)
  assert.equal(groupNode.memberNodes?.length, 3);

  // Internal connection between Auth and Users modules captured
  assert.equal(groupNode.internalConnections?.length, 1);
  assert.equal(groupNode.internalConnections![0].sourceId, 'mod:auth');
  assert.equal(groupNode.internalConnections![0].targetId, 'mod:users');

  // External edge to Orders table bundled
  assert.equal(result.bundledEdges.length, 1);
  assert.equal(result.bundledEdges[0].source, 'group:kind:Module');
  assert.equal(result.bundledEdges[0].target, 'tbl:orders');
  assert.equal(result.bundledEdges[0].totalCount, 2);
});

test('UniversalGraphMap: custom groups collapse modules into custom group with rerouted edges', () => {
  const rawNodes: SelectedNodeDetail[] = [
    { id: 'mod:a', name: 'A', displayName: 'Module A', kind: 'Module', tier: 1, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: 'MOD', bgColor: '#3b82f6', borderColor: '#1d4ed8', projects: [] },
    { id: 'mod:b', name: 'B', displayName: 'Module B', kind: 'Module', tier: 2, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: 'MOD', bgColor: '#3b82f6', borderColor: '#1d4ed8', projects: [] },
    { id: 'mod:c', name: 'C', displayName: 'Module C', kind: 'Module', tier: 3, inboundCallsCount: 0, outboundCallsCount: 0, dbCount: 0, messagingCount: 0, displayTag: 'MOD', bgColor: '#3b82f6', borderColor: '#1d4ed8', projects: [] },
  ];

  const rawEdges = [
    { id: 'e1', source: 'mod:a', target: 'mod:b', category: 'service_call' as const, label: 'CALLS', count: 10, isTransitive: false },
    { id: 'e2', source: 'mod:b', target: 'mod:c', category: 'service_call' as const, label: 'CALLS', count: 5, isTransitive: false },
  ];

  const customGroups: CustomNodeGroup[] = [
    {
      id: 'core-subsystem',
      name: 'Core Subsystem',
      nodeIds: ['mod:a', 'mod:b'],
      collapsed: true,
    },
  ];

  const result = computeGroupedGraph({
    rawNodes,
    rawEdges,
    groupedTypes: new Set<EntityKind>(),
    customGroups,
  });

  assert.equal(result.visibleNodes.length, 2);
  const groupNode = result.visibleNodes.find((n) => n.id === 'group:custom:core-subsystem')!;
  assert.ok(groupNode);
  assert.equal(groupNode.displayName, 'Core Subsystem (2)');
  assert.equal(groupNode.tier, 1);
  assert.equal(groupNode.internalConnections?.length, 1);
  assert.equal(groupNode.internalConnections![0].count, 10);

  // Edge from mod:b (now in group) to mod:c is rerouted from group to mod:c
  assert.equal(result.bundledEdges.length, 1);
  assert.equal(result.bundledEdges[0].source, 'group:custom:core-subsystem');
  assert.equal(result.bundledEdges[0].target, 'mod:c');
  assert.equal(result.bundledEdges[0].totalCount, 5);
});
