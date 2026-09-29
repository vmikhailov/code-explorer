import test from 'node:test';
import assert from 'node:assert/strict';
import {
  computeEchelonTiers,
  computeConcentricLayout,
  optimizeOrbitPermutation,
  relaxOrbitWithBadgeExclusion,
  normalizeAngle,
  cleanEntityToken,
  ConcentricNodeInput,
  ConcentricEdgeInput,
} from './concentricLayout';

test('cleanEntityToken: extracts clean tokens for infrastructure matching', () => {
  assert.equal(cleanEntityToken('domain:internal-service-action-scheduler'), 'action-scheduler');
  assert.equal(cleanEntityToken('ws:db:relational:postgresql:action-scheduler'), 'action-scheduler');
  assert.equal(cleanEntityToken('ws:top:rabbitmq:events-topic'), 'events');
  assert.equal(cleanEntityToken('domain:cf-pages'), 'cf-pages');
});

test('computeEchelonTiers: invariant echelon tiers across service graph', () => {
  const nodes = [
    { id: 'app:frontend', kind: 'Ingress' },
    { id: 'svc:gateway', kind: 'Service' },
    { id: 'svc:core', kind: 'Service' },
    { id: 'worker:cron', kind: 'Worker' },
    { id: 'topic:events', kind: 'Topic' },
    { id: 'db:postgres', kind: 'Database' },
  ];

  const edges = [
    { source: 'app:frontend', target: 'svc:gateway', category: 'service_call' },
    { source: 'svc:gateway', target: 'svc:core', category: 'service_call' },
    { source: 'svc:core', target: 'db:postgres', category: 'database' },
  ];

  const tiers = computeEchelonTiers(nodes, edges);

  assert.equal(tiers.get('app:frontend'), 0, 'Frontend should be Orbit 0');
  assert.equal(tiers.get('svc:gateway'), 1, 'Gateway-facing service should be Orbit 1');
  assert.equal(tiers.get('topic:events'), 2, 'Topic should be Orbit 2 (between 1st and 2nd echelon)');
  assert.equal(tiers.get('svc:core'), 3, 'Core internal service should be Orbit 3');
  assert.equal(tiers.get('worker:cron'), 3, 'Worker should be Orbit 3 (Second Echelon)');
  assert.equal(tiers.get('db:postgres'), 4, 'Database should be Orbit 4');
});

test('relaxOrbitWithBadgeExclusion: strictly prevents node collisions at 12 o clock badge', () => {
  const nodeIds = ['n1', 'n2', 'n3', 'n4', 'n5', 'n6'];
  // All nodes have ideal angle pointing to 12 o'clock (-90 deg)
  const ideal = new Map<string, number>();
  nodeIds.forEach((id) => ideal.set(id, -Math.PI / 2));

  const radius = 260;
  const minArcSpacing = 110;
  const result = relaxOrbitWithBadgeExclusion(nodeIds, ideal, radius, minArcSpacing);

  const badgeHalf = Math.max(Math.PI / 15, 70 / radius);
  const badgeCenter = -Math.PI / 2;

  // Verify none are in the badge zone
  for (const id of nodeIds) {
    const angle = result.get(id)!;
    const diff = Math.abs(normalizeAngle(angle - badgeCenter));
    assert.ok(
      diff >= badgeHalf - 0.001,
      `Node ${id} at ${angle} is inside badge zone (margin ${badgeHalf})`
    );
  }

  // Verify non-zero separation
  const sortedAngles = nodeIds.map((id) => result.get(id)!).sort((a, b) => a - b);
  for (let i = 1; i < sortedAngles.length; i++) {
    const gap = sortedAngles[i] - sortedAngles[i - 1];
    assert.ok(gap > 0.01, `Nodes ${i} and ${i - 1} overlap (gap: ${gap})`);
  }
});

test('computeConcentricLayout: Scenario 1 - Filtered State (Services & Topics Hidden)', () => {
  // Represents the exact screenshot state:
  // 1 Ingress (echelon 0)
  // 7 Workers (echelon 3)
  // 20 Databases (echelon 5)
  // Services (echelons 1 & 2) and Topics (4) are hidden
  const visibleNodes: ConcentricNodeInput[] = [
    { id: 'ats-front', echelonTier: 0 },
    { id: 'cf-pages', echelonTier: 3 },
    { id: 'cf-landing', echelonTier: 3 },
    { id: 'cf-worker-bindings', echelonTier: 3 },
    { id: 'action-scheduler', echelonTier: 3 },
    { id: 'ad-hub-worker', echelonTier: 3 },
    { id: 'bundle-scheduler', echelonTier: 3 },
    { id: 'workers-site', echelonTier: 3 },
  ];

  for (let i = 0; i < 20; i++) {
    visibleNodes.push({ id: `db-${i}`, echelonTier: 5 });
  }

  const visibleEdges: ConcentricEdgeInput[] = [
    { source: 'ats-front', target: 'action-scheduler', category: 'service_call' },
    { source: 'ats-front', target: 'bundle-scheduler', category: 'service_call' },
    { source: 'action-scheduler', target: 'db-0', category: 'database' },
    { source: 'bundle-scheduler', target: 'db-1', category: 'database' },
    { source: 'bundle-scheduler', target: 'db-2', category: 'database' },
  ];

  const result = computeConcentricLayout(visibleNodes, visibleEdges, 1.0);

  // Invariant 1: Only 3 populated rings (0, 3, 5)
  assert.equal(result.populatedOrbits.length, 3);
  assert.equal(result.populatedOrbits[0].levelIndex, 0);
  assert.equal(result.populatedOrbits[1].levelIndex, 3);
  assert.equal(result.populatedOrbits[2].levelIndex, 5);

  // Invariant 2: Guides strictly match populated orbits with R > 0
  assert.equal(result.guides.length, 2);
  assert.equal(result.guides[0].radius, result.populatedOrbits[1].radius);
  assert.equal(result.guides[1].radius, result.populatedOrbits[2].radius);

  // Invariant 3: Radii are strictly distinct
  assert.ok(result.guides[0].radius < result.guides[1].radius);

  // Invariant 4: Every node lies strictly on its orbit radius
  for (const orbit of result.populatedOrbits) {
    for (const nid of orbit.nodeIds) {
      assert.equal(result.nodeRadii.get(nid), orbit.radius);
      const pos = result.positions.get(nid)!;
      const actualR = Math.round(Math.hypot(pos.x, pos.y));
      assert.ok(
        Math.abs(actualR - orbit.radius) <= 1,
        `Node ${nid} placed at R=${actualR} != orbit radius ${orbit.radius}`
      );
    }
  }

  // Invariant 5: Zero badge collisions at 12 o'clock
  for (const orbit of result.populatedOrbits) {
    if (orbit.radius === 0) continue;
    const badgeHalf = Math.max(Math.PI / 15, 70 / orbit.radius);
    for (const nid of orbit.nodeIds) {
      const angle = result.nodeAngles.get(nid)!;
      const diff = Math.abs(normalizeAngle(angle - (-Math.PI / 2)));
      assert.ok(
        diff >= badgeHalf - 0.001,
        `Node ${nid} on orbit ${orbit.levelIndex} collides with badge at -90 deg`
      );
    }
  }
});

test('computeConcentricLayout: Scenario 2 - Full Graph with all 5 echelons', () => {
  const visibleNodes: ConcentricNodeInput[] = [
    { id: 'fe-app', echelonTier: 0 },
    { id: 'svc-t1-a', echelonTier: 1 },
    { id: 'svc-t1-b', echelonTier: 1 },
    { id: 'topic-1', echelonTier: 2 },
    { id: 'svc-t2-a', echelonTier: 3 },
    { id: 'svc-t2-b', echelonTier: 3 },
    { id: 'worker-1', echelonTier: 3 },
    { id: 'db-1', echelonTier: 4 },
    { id: 'db-2', echelonTier: 4 },
  ];

  const visibleEdges: ConcentricEdgeInput[] = [
    { source: 'fe-app', target: 'svc-t1-a', category: 'service_call' },
    { source: 'svc-t1-a', target: 'topic-1', category: 'messaging' },
    { source: 'topic-1', target: 'svc-t2-a', category: 'messaging' },
    { source: 'svc-t2-a', target: 'worker-1', category: 'service_call' },
    { source: 'worker-1', target: 'db-1', category: 'database' },
  ];

  const result = computeConcentricLayout(visibleNodes, visibleEdges, 1.0);

  // All 5 orbits populated (0, 1, 2, 3, 4)
  assert.equal(result.populatedOrbits.length, 5);
  assert.equal(result.guides.length, 4); // Orbits 1..4

  // Strictly increasing radii
  for (let i = 1; i < result.guides.length; i++) {
    assert.ok(result.guides[i].radius > result.guides[i - 1].radius);
  }

  // 1:1 check on positions
  for (const orbit of result.populatedOrbits) {
    for (const nid of orbit.nodeIds) {
      const pos = result.positions.get(nid)!;
      const actualR = Math.round(Math.hypot(pos.x, pos.y));
      assert.ok(Math.abs(actualR - orbit.radius) <= 1);
    }
  }
});

test('computeConcentricLayout: Optional Messaging Orbit - cleanly skipped when no topics exist', () => {
  const visibleNodes: ConcentricNodeInput[] = [
    { id: 'fe-app', echelonTier: 0 },
    { id: 'svc-t1', echelonTier: 1 },
    // Echelon 2 (Topic) is completely omitted!
    { id: 'svc-t2', echelonTier: 3 },
    { id: 'worker-1', echelonTier: 3 },
    { id: 'db-1', echelonTier: 4 },
  ];

  const result = computeConcentricLayout(visibleNodes, [], 1.0);

  // Exactly 4 populated orbits (0, 1, 3, 4)
  assert.equal(result.populatedOrbits.length, 4);
  // Labels are dynamically renumbered 0, 1, 2, 3 without gap!
  assert.ok(result.populatedOrbits[0].label.startsWith('Orbit 0:'));
  assert.ok(result.populatedOrbits[1].label.startsWith('Orbit 1:'));
  assert.ok(result.populatedOrbits[2].label.startsWith('Orbit 2: Second Echelon'));
  assert.ok(result.populatedOrbits[3].label.startsWith('Orbit 3: Databases'));
});

test('computeConcentricEquispacedLayout: guarantees strictly equal spacing and valid positions', async () => {
  const { computeConcentricEquispacedLayout } = await import('./concentricEquispacedLayout');
  const visibleNodes: ConcentricNodeInput[] = [
    { id: 'svc-1', echelonTier: 1 },
    { id: 'svc-2', echelonTier: 1 },
    { id: 'db-1', echelonTier: 5 },
    { id: 'db-2', echelonTier: 5 },
    { id: 'db-3', echelonTier: 5 },
    { id: 'db-4', echelonTier: 5 },
  ];
  const visibleEdges: ConcentricEdgeInput[] = [
    { source: 'svc-1', target: 'db-1', category: 'database' },
    { source: 'svc-2', target: 'db-2', category: 'database' },
  ];

  const result = computeConcentricEquispacedLayout(visibleNodes, visibleEdges, 1.0);
  assert.equal(result.positions.size, 6);
  for (const [id, pos] of result.positions.entries()) {
    assert.ok(!isNaN(pos.x) && !isNaN(pos.y), `Node ${id} has NaN position`);
  }
});

test('computeConcentricPolarForceLayout: converges to valid positions without NaN', async () => {
  const { computeConcentricPolarForceLayout } = await import('./concentricPolarForceLayout');
  const visibleNodes: ConcentricNodeInput[] = [
    { id: 'svc-1', echelonTier: 1 },
    { id: 'svc-2', echelonTier: 1 },
    { id: 'db-1', echelonTier: 5 },
    { id: 'db-2', echelonTier: 5 },
  ];
  const visibleEdges: ConcentricEdgeInput[] = [
    { source: 'svc-1', target: 'db-1', category: 'database' },
  ];

  const result = computeConcentricPolarForceLayout(visibleNodes, visibleEdges, 1.0);
  assert.equal(result.positions.size, 4);
  for (const [id, pos] of result.positions.entries()) {
    assert.ok(!isNaN(pos.x) && !isNaN(pos.y), `Node ${id} has NaN position`);
  }
});

test('computeConcentricSectorsLayout: groups domain nodes in pie slices without NaN', async () => {
  const { computeConcentricSectorsLayout } = await import('./concentricSectorsLayout');
  const visibleNodes: ConcentricNodeInput[] = [
    { id: 'billing-svc', echelonTier: 1 },
    { id: 'billing-db', echelonTier: 5 },
    { id: 'traffic-svc', echelonTier: 1 },
    { id: 'traffic-db', echelonTier: 5 },
  ];
  const visibleEdges: ConcentricEdgeInput[] = [
    { source: 'billing-svc', target: 'billing-db', category: 'database' },
    { source: 'traffic-svc', target: 'traffic-db', category: 'database' },
  ];

  const result = computeConcentricSectorsLayout(visibleNodes, visibleEdges, 1.0);
  assert.equal(result.positions.size, 4);
  for (const [id, pos] of result.positions.entries()) {
    assert.ok(!isNaN(pos.x) && !isNaN(pos.y), `Node ${id} has NaN position`);
  }
});

test('optimizeOrbitPermutation: anchors Ingress at center and minimizes radial edge lengths', () => {
  const buckets: Record<number, string[]> = {
    0: ['ingress-1'],
    1: ['gateway-1'],
    2: ['heavy-topic-1'],
    3: ['worker-1', 'worker-2'],
    4: ['db-analytics'],
  };

  // Scenario: Ingress (0) -> Gateway (1) -> Worker (3) -> Topic (2) -> DB (4)
  // Topic (2) interacts heavily with Worker (3) and DB (4), with ZERO traffic to Gateway (1).
  // Placing Topic (2) at slot 3 between Worker (3) and DB (4) saves hundreds of pixels of radial span!
  const edges = [
    { sourceTier: 0, targetTier: 1, weight: 10 },
    { sourceTier: 1, targetTier: 3, weight: 10 },
    { sourceTier: 3, targetTier: 2, weight: 30 }, // High traffic Worker -> Topic
    { sourceTier: 2, targetTier: 4, weight: 30 }, // High traffic Topic -> DB
  ];

  const optimalOrder = optimizeOrbitPermutation([0, 1, 2, 3, 4], buckets, edges, 1.0);

  // Invariant 1: Ingress (0) MUST remain at slot 0
  assert.equal(optimalOrder[0], 0, 'Ingress must be anchored at center (Orbit 0)');

  // Invariant 2: Gateway (1) must be next, followed by Worker (3), Topic (2), DB (4)
  // because Topic (2) sits between Worker (3) and DB (4)
  assert.deepEqual(optimalOrder, [0, 1, 3, 2, 4], 'Orbits should be permuted [0, 1, 3, 2, 4] to minimize edge length');
});

test('computeConcentricLayout: respects customOrbitOrder when provided', () => {
  const visibleNodes: ConcentricNodeInput[] = [
    { id: 'app', echelonTier: 0 },
    { id: 'svc-1', echelonTier: 1 },
    { id: 'topic-1', echelonTier: 2 },
    { id: 'db-1', echelonTier: 4 },
  ];
  const visibleEdges: ConcentricEdgeInput[] = [
    { source: 'app', target: 'svc-1' },
    { source: 'svc-1', target: 'topic-1' },
    { source: 'svc-1', target: 'db-1' },
  ];

  // User specifies custom order: DB (4) inside, then Topics (2), Services (1), Apps (0)
  const customOrder = [4, 2, 1, 0];
  const result = computeConcentricLayout(visibleNodes, visibleEdges, 1.0, customOrder);

  const resultingTiers = result.populatedOrbits.map((o) => o.levelIndex);
  assert.deepEqual(resultingTiers, [4, 2, 1, 0], 'Populated orbits must follow custom order exactly');
  assert.equal(result.populatedOrbits[0].shortLabel, 'Orbit 0');
  assert.equal(result.populatedOrbits[0].levelIndex, 4);
});

