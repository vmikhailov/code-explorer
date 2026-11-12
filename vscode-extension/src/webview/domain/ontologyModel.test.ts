import test from 'node:test';
import assert from 'node:assert/strict';
import {
  BUNDLED_ONTOLOGY_SCHEMA,
  filterOntologyNodes,
  computeEgoNeighborhood,
  getWorkspaceNodeCount,
  getWorkspaceTripleCount,
} from './ontologyModel';
import {
  formatCompactCount,
  buildOntologyGraphLayout,
  buildOntologyGraphEdges,
} from './ontologyGraphLayout';
import { OntologyWorkspaceStatsDto } from '../../../../proto/types';

test('BUNDLED_ONTOLOGY_SCHEMA: exposes 4 active layers and excludes hidden Layer 5 Domain entities', () => {
  assert.equal(BUNDLED_ONTOLOGY_SCHEMA.schemaVersion, '1.0');
  assert.equal(BUNDLED_ONTOLOGY_SCHEMA.layers.length, 4);
  assert.ok(BUNDLED_ONTOLOGY_SCHEMA.nodes.length >= 20);

  const kinds = new Set(BUNDLED_ONTOLOGY_SCHEMA.nodes.map((n) => n.kind));
  assert.ok(kinds.has('Service'));
  assert.ok(kinds.has('Endpoint'));
  assert.ok(kinds.has('Function'));
  assert.ok(kinds.has('CallArgument'));
  assert.ok(kinds.has('Database'));
  assert.ok(!kinds.has('Domain'));
  assert.ok(!kinds.has('BoundedContext'));
  assert.ok(!kinds.has('SyntaxStructure'));
});

test('filterOntologyNodes: matches by property name and respects onlyActiveInWorkspace', () => {
  const allCats = new Set(['control_flow', 'data_flow', 'type_dependency', 'structural']);
  const routeMatches = filterOntologyNodes(BUNDLED_ONTOLOGY_SCHEMA, {
    searchQuery: 'route_template',
    activeCategories: allCats,
    onlyActiveInWorkspace: false,
  });
  assert.ok(routeMatches.some((n) => n.kind === 'Endpoint'));

  const mockWorkspace: OntologyWorkspaceStatsDto = {
    totalNodes: 150,
    totalEdges: 320,
    nodeCounts: { Function: 120, Endpoint: 30 },
    relationshipCounts: { TRIGGERS: 30 },
    observedTriples: [{ fromKind: 'Endpoint', rel: 'TRIGGERS', toKind: 'Function', count: 30 }],
  };

  const activeOnly = filterOntologyNodes(
    BUNDLED_ONTOLOGY_SCHEMA,
    {
      searchQuery: '',
      activeCategories: allCats,
      onlyActiveInWorkspace: true,
    },
    mockWorkspace
  );
  assert.deepEqual(
    activeOnly.map((n) => n.kind).sort(),
    ['Endpoint', 'Function']
  );
  assert.equal(getWorkspaceNodeCount(mockWorkspace, 'function'), 120);
  assert.equal(getWorkspaceTripleCount(mockWorkspace, 'Endpoint', 'TRIGGERS', 'Function'), 30);

  const semanticAndSyntaxOnly = filterOntologyNodes(BUNDLED_ONTOLOGY_SCHEMA, {
    searchQuery: '',
    activeCategories: allCats,
    onlyActiveInWorkspace: false,
    activeLayers: new Set([4, 3]),
  });
  assert.ok(semanticAndSyntaxOnly.length > 0);
  assert.ok(semanticAndSyntaxOnly.every((n) => n.layerId === 4 || n.layerId === 3));
  assert.ok(!semanticAndSyntaxOnly.some((n) => n.kind === 'Project' || n.kind === 'File'));
});

test('computeEgoNeighborhood: resolves outbound and inbound peer badges filtered by category', () => {
  const controlOnly = new Set(['control_flow']);
  const ego = computeEgoNeighborhood(BUNDLED_ONTOLOGY_SCHEMA, 'Endpoint', controlOnly);
  assert.ok(ego.connections.some((c) => c.direction === 'out' && c.rel === 'TRIGGERS' && c.peerKind === 'Function'));
  assert.ok(ego.badgesByKind.has('Function'));
  assert.ok(!ego.connections.some((c) => c.category === 'data_flow'));
});

test('ontologyGraphLayout: formats compact counts and places non-overlapping circular nodes with curved multi-edges and self-loops', () => {
  assert.equal(formatCompactCount(0), '0');
  assert.equal(formatCompactCount(30), '30');
  assert.equal(formatCompactCount(1776), '1.8k');
  assert.equal(formatCompactCount(13189), '13.2k');

  const mockWorkspace: OntologyWorkspaceStatsDto = {
    totalNodes: 20500,
    totalEdges: 45000,
    nodeCounts: { Function: 7339, Type: 1776, Endpoint: 30 },
    relationshipCounts: {},
    observedTriples: [],
  };

  const placed = buildOntologyGraphLayout(
    BUNDLED_ONTOLOGY_SCHEMA,
    BUNDLED_ONTOLOGY_SCHEMA.nodes,
    mockWorkspace
  );
  assert.equal(placed.length, BUNDLED_ONTOLOGY_SCHEMA.nodes.length);

  const fnNode = placed.find((n) => n.kind === 'Function');
  const serviceNode = placed.find((n) => n.kind === 'Service');
  assert.ok(fnNode);
  assert.ok(serviceNode);
  assert.equal(fnNode.badgeText, '7.3k');
  assert.equal(serviceNode.badgeText, '0');

  // Verify deterministic separation (no overlapping node centers)
  for (let i = 0; i < placed.length; i++) {
    for (let j = i + 1; j < placed.length; j++) {
      const dx = placed[i].x - placed[j].x;
      const dy = placed[i].y - placed[j].y;
      assert.ok(Math.hypot(dx, dy) > 48, `Nodes ${placed[i].kind} and ${placed[j].kind} are too close`);
    }
  }

  const allKinds = new Set(BUNDLED_ONTOLOGY_SCHEMA.nodes.map((n) => n.kind));
  const allCats = new Set(['control_flow', 'data_flow', 'type_dependency', 'structural']);
  const edges = buildOntologyGraphEdges(BUNDLED_ONTOLOGY_SCHEMA, allKinds, allCats);

  // Self-loop present for Function -[:CALLS]-> Function
  const fnSelf = edges.find((e) => e.fromKind === 'Function' && e.toKind === 'Function' && e.rel === 'CALLS');
  assert.ok(fnSelf && fnSelf.isSelfLoop);

  // Parallel multi-edges between Query and Table (READS_FROM, WRITES_TO) fan out with distinct offsets
  const qtEdges = edges.filter(
    (e) =>
      (e.fromKind === 'Query' && e.toKind === 'Table') ||
      (e.fromKind === 'Table' && e.toKind === 'Query')
  );
  assert.ok(qtEdges.length >= 2);
  const offsets = new Set(qtEdges.map((e) => e.canonicalOffset));
  assert.equal(offsets.size, qtEdges.length);

  // Zero disconnected nodes when all 4 categories are active
  const connectedKinds = new Set<string>();
  for (const e of edges) {
    connectedKinds.add(e.fromKind);
    connectedKinds.add(e.toKind);
  }
  for (const kind of allKinds) {
    assert.ok(connectedKinds.has(kind), `Ontology node ${kind} must have at least one edge`);
  }

  // Verify previously missing edges exist
  assert.ok(edges.some((e) => e.fromKind === 'Function' && e.rel === 'PASSES_LITERAL' && e.toKind === 'CallLiteral'));
  assert.ok(edges.some((e) => e.fromKind === 'Function' && e.rel === 'DECLARES' && e.toKind === 'CatchClause'));
  assert.ok(edges.some((e) => e.fromKind === 'Project' && e.rel === 'USES_GIT' && e.toKind === 'GitSettings'));
  assert.ok(edges.some((e) => e.fromKind === 'TestSuite' && e.rel === 'TESTS' && e.toKind === 'Library'));
});
