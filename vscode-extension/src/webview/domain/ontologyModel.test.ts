import test from 'node:test';
import assert from 'node:assert/strict';
import {
  BUNDLED_ONTOLOGY_SCHEMA,
  filterOntologyNodes,
  computeEgoNeighborhood,
  getWorkspaceNodeCount,
  getWorkspaceTripleCount,
  getOntologyHierarchies,
  getDefaultExpandedHierarchies,
  resolveHierarchyVisibility,
} from './ontologyModel';
import {
  formatCompactCount,
  buildOntologyGraphLayout,
  buildOntologyGraphEdges,
  buildOntologyGraphCapsules,
  OntologyForceSimulation,
} from './ontologyGraphLayout';
import { OntologyWorkspaceStatsDto } from '../../../../proto/types';

test('BUNDLED_ONTOLOGY_SCHEMA: exposes 5-layer v2.0 inheritance hierarchies, Domain, BoundedContext, Cloud, DbSchema, and retires CloudService', () => {
  assert.equal(BUNDLED_ONTOLOGY_SCHEMA.schemaVersion, '2.0');
  assert.equal(BUNDLED_ONTOLOGY_SCHEMA.layers.length, 5);
  assert.ok(BUNDLED_ONTOLOGY_SCHEMA.nodes.length >= 26);

  const kinds = new Set(BUNDLED_ONTOLOGY_SCHEMA.nodes.map((n) => n.kind));
  assert.ok(kinds.has('Domain'));
  assert.ok(kinds.has('BoundedContext'));
  assert.ok(kinds.has('Module'));
  assert.ok(kinds.has('Workload'));
  assert.ok(kinds.has('Service'));
  assert.ok(kinds.has('Cloud'));
  assert.ok(kinds.has('DbSchema'));
  assert.ok(kinds.has('Endpoint'));
  assert.ok(kinds.has('Function'));
  assert.ok(kinds.has('CallArgument'));
  assert.ok(kinds.has('Database'));
  assert.ok(!kinds.has('CloudService'));
  assert.ok(!kinds.has('ApiInUse'));
  assert.ok(!kinds.has('LogicalStructure'));
  assert.ok(!kinds.has('SyntaxStructure'));

  const hierarchies = getOntologyHierarchies(BUNDLED_ONTOLOGY_SCHEMA);
  assert.deepEqual(
    hierarchies.map((h) => h.kind),
    ['Module', 'Workload']
  );
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

  // Zero disconnected standalone nodes (every node either has a direct edge or sits inside an inheritance capsule)
  const connectedKinds = new Set<string>();
  for (const e of edges) {
    connectedKinds.add(e.fromKind);
    connectedKinds.add(e.toKind);
  }
  for (const node of BUNDLED_ONTOLOGY_SCHEMA.nodes) {
    assert.ok(
      connectedKinds.has(node.kind) || Boolean(node.extends),
      `Ontology node ${node.kind} must have at least one edge or extend a supertype`
    );
  }

  // Verify Ontology v2.0 relationships and inheritance capsules
  assert.ok(edges.some((e) => e.fromKind === 'Function' && e.rel === 'PASSES_LITERAL' && e.toKind === 'CallLiteral'));
  assert.ok(edges.some((e) => e.fromKind === 'Function' && e.rel === 'DECLARES' && e.toKind === 'CatchClause'));
  assert.ok(edges.some((e) => e.fromKind === 'Project' && e.rel === 'USES_GIT' && e.toKind === 'GitSettings'));
  assert.ok(edges.some((e) => e.fromKind === 'TestSuite' && e.rel === 'TESTS' && e.toKind === 'Module'));
  assert.ok(edges.some((e) => e.fromKind === 'Database' && e.rel === 'CONTAINS' && e.toKind === 'DbSchema'));
  assert.ok(edges.some((e) => e.fromKind === 'DbSchema' && e.rel === 'CONTAINS' && e.toKind === 'Table'));
  assert.ok(edges.some((e) => e.fromKind === 'ExternalService' && e.rel === 'HOSTED_IN' && e.toKind === 'Cloud'));
  assert.ok(edges.some((e) => e.fromKind === 'Workload' && e.rel === 'HOSTED_IN' && e.toKind === 'Cloud'));
  assert.ok(edges.some((e) => e.fromKind === 'Workload' && e.rel === 'SERVICE_CALL' && e.toKind === 'Service'));
  assert.ok(edges.some((e) => e.fromKind === 'Service' && e.rel === 'EXPOSES' && e.toKind === 'Endpoint'));
  assert.ok(edges.some((e) => e.fromKind === 'Worker' && e.rel === 'SUBSCRIBES_TO' && e.toKind === 'Topic'));

  // Inheritance is rendered via explicit EXTENDS edges (zero contour capsules)
  assert.ok(edges.some((e) => e.fromKind === 'Workload' && e.rel === 'EXTENDS' && e.toKind === 'Module' && e.isExtends));
  assert.ok(edges.some((e) => e.fromKind === 'Service' && e.rel === 'EXTENDS' && e.toKind === 'Workload' && e.isExtends));
  assert.ok(edges.some((e) => e.fromKind === 'Library' && e.rel === 'EXTENDS' && e.toKind === 'Module' && e.isExtends));
  const defaultExpanded = getDefaultExpandedHierarchies(BUNDLED_ONTOLOGY_SCHEMA);
  const capsules = buildOntologyGraphCapsules(BUNDLED_ONTOLOGY_SCHEMA, placed, defaultExpanded);
  assert.equal(capsules.length, 0);

  // Collapsing Workload hides App/Service/Worker/CliTool (+4), collapsing Module hides all 7 descendants (+7) and reroutes edges
  const wlCollapsed = resolveHierarchyVisibility(
    BUNDLED_ONTOLOGY_SCHEMA,
    BUNDLED_ONTOLOGY_SCHEMA.nodes,
    new Set(['Module'])
  );
  assert.equal(wlCollapsed.collapsedCounts.get('Workload'), 4);
  assert.ok(!wlCollapsed.visibleNodes.some((n) => n.kind === 'Service'));
  assert.ok(wlCollapsed.visibleNodes.some((n) => n.kind === 'Workload'));

  const modCollapsed = resolveHierarchyVisibility(
    BUNDLED_ONTOLOGY_SCHEMA,
    BUNDLED_ONTOLOGY_SCHEMA.nodes,
    new Set()
  );
  assert.equal(modCollapsed.collapsedCounts.get('Module'), 7);
  const modVisibleSet = new Set(modCollapsed.visibleNodes.map((n) => n.kind));
  const reroutedEdges = buildOntologyGraphEdges(
    BUNDLED_ONTOLOGY_SCHEMA,
    modVisibleSet,
    allCats,
    undefined,
    false,
    modCollapsed.redirectMap
  );
  assert.ok(reroutedEdges.some((e) => e.fromKind === 'Module' && e.rel === 'USES_DB' && e.toKind === 'Database'));
  assert.ok(reroutedEdges.some((e) => e.fromKind === 'Module' && e.rel === 'EXPOSES' && e.toKind === 'Endpoint'));

  // Subtype ego neighborhood includes own Service edges + inherited edges from Workload and Module
  const svcEgo = computeEgoNeighborhood(BUNDLED_ONTOLOGY_SCHEMA, 'Service', allCats);
  assert.ok(svcEgo.connections.some((c) => c.rel === 'EXPOSES' && c.peerKind === 'Endpoint' && !c.inheritedFrom));
  assert.ok(svcEgo.connections.some((c) => c.rel === 'USES_DB' && c.peerKind === 'Database' && c.inheritedFrom === 'Workload'));
  assert.ok(svcEgo.connections.some((c) => c.rel === 'IMPLEMENTED_BY' && c.peerKind === 'Project' && c.inheritedFrom === 'Module'));
});

test('OntologyForceSimulation: provides elastic neighbor pull during drag, smooth relaxation on release, freeze pinning, and Air spacing', () => {
  const sim = new OntologyForceSimulation();
  const allCats = new Set(['control_flow', 'data_flow', 'type_dependency', 'structural']);
  const initial = sim.sync(BUNDLED_ONTOLOGY_SCHEMA, BUNDLED_ONTOLOGY_SCHEMA.nodes, allCats);

  const byKind = (list: typeof initial) => new Map(list.map((n) => [n.kind, n]));
  const initMap = byKind(initial);
  const initService = initMap.get('Service')!;
  const initWorkload = initMap.get('Workload')!;
  const initEndpoint = initMap.get('Endpoint')!;
  assert.equal(sim.isRunning(), false);

  // Elastic drag: pulling Service right+down elastically pulls connected Workload & Endpoint along
  sim.startDrag('Service');
  sim.moveDrag('Service', initService.x + 220, initService.y + 160);
  assert.equal(sim.isRunning(), true);
  for (let i = 0; i < 25; i++) {
    sim.tick();
  }
  const draggedMap = byKind(sim.getNodes());
  assert.equal(draggedMap.get('Service')!.x, initService.x + 220);
  assert.ok(
    draggedMap.get('Workload')!.x > initWorkload.x + 20,
    'Expected connected parent Workload to stretch elastically toward dragged Service'
  );
  assert.ok(
    draggedMap.get('Endpoint')!.y > initEndpoint.y + 8 || draggedMap.get('Endpoint')!.x !== initEndpoint.x,
    'Expected connected Endpoint to respond elastically to dragged Service'
  );

  // Release: unpins Service and relaxes smoothly back toward equilibrium until simulation stops
  sim.endDrag('Service');
  let ticks = 0;
  while (sim.tick() && ticks < 400) {
    ticks++;
  }
  assert.equal(sim.isRunning(), false);
  const relaxedService = byKind(sim.getNodes()).get('Service')!;
  assert.ok(
    Math.hypot(relaxedService.x - initService.x, relaxedService.y - initService.y) < 35,
    'Expected released node to relax back near equilibrium'
  );

  // Air spacing factor expands inter-node distances
  const spanAtOne =
    Math.max(...sim.getNodes().map((n) => n.x)) - Math.min(...sim.getNodes().map((n) => n.x));
  sim.setSpacingFactor(1.7);
  for (let i = 0; i < 120; i++) {
    sim.tick();
  }
  const spanExpanded =
    Math.max(...sim.getNodes().map((n) => n.x)) - Math.min(...sim.getNodes().map((n) => n.x));
  assert.ok(spanExpanded > spanAtOne * 1.25, 'Expected Air spacing > 1.0x to expand graph span');

  // Hub factor scales degree-dependent repulsion for high-degree hubs
  sim.setSpacingFactor(1.0);
  sim.setHubFactor(0.0);
  for (let i = 0; i < 120; i++) {
    sim.tick();
  }
  const zeroHubMap = byKind(sim.getNodes());
  const fnFileDistAtZero = Math.hypot(
    zeroHubMap.get('Function')!.x - zeroHubMap.get('File')!.x,
    zeroHubMap.get('Function')!.y - zeroHubMap.get('File')!.y
  );
  sim.setHubFactor(2.0);
  assert.equal(sim.getHubFactor(), 2.0);
  for (let i = 0; i < 120; i++) {
    sim.tick();
  }
  const highHubMap = byKind(sim.getNodes());
  const fnFileDistAtTwo = Math.hypot(
    highHubMap.get('Function')!.x - highHubMap.get('File')!.x,
    highHubMap.get('Function')!.y - highHubMap.get('File')!.y
  );
  assert.ok(
    fnFileDistAtTwo > fnFileDistAtZero * 1.15,
    `Expected Hub factor 2.0x to separate high-degree hubs Function and File (${fnFileDistAtTwo.toFixed(1)} vs ${fnFileDistAtZero.toFixed(1)})`
  );

  // Freeze mode pins dragged nodes in place
  sim.setFrozen(true);
  const preFreezeSvc = byKind(sim.getNodes()).get('Service')!;
  sim.startDrag('Service');
  sim.moveDrag('Service', preFreezeSvc.x + 140, preFreezeSvc.y - 90);
  sim.endDrag('Service');
  assert.equal(sim.tick(), false);
  assert.equal(byKind(sim.getNodes()).get('Service')!.x, preFreezeSvc.x + 140);
});
