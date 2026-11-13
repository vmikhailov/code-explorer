import test from 'node:test';
import assert from 'node:assert/strict';
import {
  UniversalGraphMap,
  UniversalGraphMapProps,
  parseUniversalGraph,
  computeUniversalVisibleGraph,
} from './UniversalGraphMap';
import { computeGroupedGraph } from '../domain/nodeGrouping';
import { SelectedNodeDetail, CustomNodeGroup, EntityKind } from '../domain/types';
import { GraphData } from '../../../../proto/types';

const SAMPLE_PROJECT_MAP_GRAPH: GraphData = {
  nodes: [
    {
      id: 'mod:controllers',
      name: 'Controllers',
      displayName: 'Controllers',
      kind: 'Module',
      properties: { tier: '0', file_count: '4' },
    },
    {
      id: 'mod:services',
      name: 'Services',
      displayName: 'Services',
      kind: 'Module',
      properties: { tier: '2', file_count: '6' },
    },
    {
      id: 'mod:repositories',
      name: 'Repositories',
      displayName: 'Repositories',
      kind: 'Module',
      properties: { tier: '3', file_count: '5' },
    },
    {
      id: 'tbl:users',
      name: 'users',
      displayName: 'users',
      kind: 'Table',
      properties: { tier: '5' },
    },
    {
      id: 'tbl:orders',
      name: 'orders',
      displayName: 'orders',
      kind: 'Table',
      properties: { tier: '5' },
    },
  ],
  edges: [
    {
      id: 'e1',
      source: 'mod:controllers',
      target: 'mod:services',
      kind: 'CALLS',
      properties: { call_count: '12' },
    },
    {
      id: 'e2',
      source: 'mod:services',
      target: 'mod:repositories',
      kind: 'CALLS',
      properties: { call_count: '8' },
    },
    {
      id: 'e3',
      source: 'mod:repositories',
      target: 'tbl:users',
      kind: 'READS_FROM',
      properties: { call_count: '3' },
    },
    {
      id: 'e4',
      source: 'mod:repositories',
      target: 'tbl:orders',
      kind: 'WRITES_TO',
      properties: { call_count: '2' },
    },
  ],
};

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

test('UniversalGraphMap: category grouping collapses >= 2 nodes of same kind and bundles external & internal edges', () => {
  const parsedGraph = parseUniversalGraph(SAMPLE_PROJECT_MAP_GRAPH);

  // Group Table nodes (tbl:users, tbl:orders) into group:kind:Table
  const res = computeUniversalVisibleGraph({
    parsedGraph,
    groupedTypes: new Set(['Table']),
  });

  const visibleIds = res.visibleNodes.map((n) => n.data.id);
  assert.ok(visibleIds.includes('group:kind:Table'), 'Should synthesize group:kind:Table node');
  assert.ok(!visibleIds.includes('tbl:users'), 'Individual table node should be collapsed');
  assert.ok(!visibleIds.includes('tbl:orders'), 'Individual table node should be collapsed');
  assert.equal(res.visibleNodes.length, 4); // 3 Module nodes + 1 Table group node

  const tableGroup = res.effectiveDetailMap.get('group:kind:Table');
  assert.ok(tableGroup);
  assert.equal(tableGroup.isGroup, true);
  assert.equal(tableGroup.groupType, 'category');
  assert.equal(tableGroup.displayName, 'Tables (2)');
  assert.equal(tableGroup.memberNodes?.length, 2);
  assert.equal(tableGroup.dbCount, 5); // 3 + 2 database interactions

  // Check bundled edge between mod:repositories and group:kind:Table preserves original targets
  const repoToTablesEdge = res.edgeDetailMap.get('edge:group:kind:Table--mod:repositories');
  assert.ok(repoToTablesEdge, 'Should bundle edges from mod:repositories to group:kind:Table');
  assert.equal(repoToTablesEdge.totalInteractions, 5);
  assert.equal(repoToTablesEdge.items.length, 2);
  assert.deepEqual(
    repoToTablesEdge.items.map((i) => i.targetId).sort(),
    ['tbl:orders', 'tbl:users']
  );
});

test('UniversalGraphMap: custom group collapses selected nodes, records internalConnections, and takes precedence over category grouping', () => {
  const parsedGraph = parseUniversalGraph(SAMPLE_PROJECT_MAP_GRAPH);

  // Create custom group of mod:services + mod:repositories while also grouping Module category
  const res = computeUniversalVisibleGraph({
    parsedGraph,
    groupedTypes: new Set(['Module']),
    customGroups: [
      {
        id: 'domain_core',
        name: 'Domain Core',
        nodeIds: ['mod:services', 'mod:repositories'],
        collapsed: true,
      },
    ],
  });

  const visibleIds = res.visibleNodes.map((n) => n.data.id);
  assert.ok(visibleIds.includes('group:custom:domain_core'), 'Custom group node should be visible');
  // Because only 1 ungrouped Module ('mod:controllers') remains (< 2), category group 'group:kind:Module' should NOT collapse it
  assert.ok(visibleIds.includes('mod:controllers'), 'Remaining single Module should stay ungrouped');
  assert.ok(!visibleIds.includes('group:kind:Module'));

  const customGroup = res.effectiveDetailMap.get('group:custom:domain_core');
  assert.ok(customGroup);
  assert.equal(customGroup.isGroup, true);
  assert.equal(customGroup.groupType, 'custom');
  assert.equal(customGroup.displayName, 'Domain Core (2)');
  assert.equal(customGroup.internalConnections?.length, 1);
  assert.equal(customGroup.internalConnections?.[0].sourceId, 'mod:services');
  assert.equal(customGroup.internalConnections?.[0].targetId, 'mod:repositories');
  assert.equal(customGroup.internalConnections?.[0].count, 8);
  assert.equal(customGroup.inboundCallsCount, 12); // from mod:controllers -> mod:services
  assert.equal(customGroup.dbCount, 5); // from mod:repositories -> tbl:users & tbl:orders
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
