import test from 'node:test';
import assert from 'node:assert/strict';
import {
  UniversalGraphMap,
  UniversalGraphMapProps,
  parseUniversalGraph,
  computeUniversalVisibleGraph,
} from './UniversalGraphMap';
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
