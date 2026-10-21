import test from 'node:test';
import assert from 'node:assert/strict';
import {
  DEFAULT_ORBIT_CONFIG,
  effectiveOrbitTier,
  mergeOrbits,
  moveKindToOrbit,
  moveOrbit,
  orbitOrder,
  orbitRepresentative,
  orbitTitle,
  sanitizeOrbitConfig,
  splitOrbit,
} from './orbitConfig';
import { computeConcentricLayout } from './concentricLayout';

test('default order is App, First, Messaging, Second, DB+External as one orbit', () => {
  assert.deepEqual(DEFAULT_ORBIT_CONFIG.orbits, [[0], [1], [2], [3], [4, 5]]);
  assert.deepEqual(orbitOrder(DEFAULT_ORBIT_CONFIG), [0, 1, 2, 3, 4]);
  assert.equal(orbitRepresentative(DEFAULT_ORBIT_CONFIG, 5), 4);
});

test('merging adjacent orbits produces one entry and one title', () => {
  const merged = mergeOrbits(DEFAULT_ORBIT_CONFIG, 1, 3);
  assert.deepEqual(merged.orbits, [[0], [1, 2, 3], [4, 5]]);
  assert.equal(effectiveOrbitTier(merged, 'Topic', 2), 1);
  assert.equal(orbitTitle([1, 2]).includes(' + '), true);
  assert.equal(orbitTitle([4, 5]), 'Databases & External Infrastructure');
});

test('split restores single orbits and merge does not mutate its input', () => {
  const merged = mergeOrbits(DEFAULT_ORBIT_CONFIG, 1, 2);
  assert.deepEqual(DEFAULT_ORBIT_CONFIG.orbits, [[0], [1], [2], [3], [4, 5]]);
  assert.deepEqual(splitOrbit(merged, 1).orbits, [[0], [1], [2], [3], [4, 5]]);
});

test('manual reordering moves a merged orbit as a unit', () => {
  const merged = mergeOrbits(DEFAULT_ORBIT_CONFIG, 1, 2);
  const moved = moveOrbit(merged, 1, 3);
  assert.deepEqual(moved.orbits, [[0], [3], [4, 5], [1, 2]]);
  assert.deepEqual(orbitOrder(moved), [0, 3, 4, 1]);
});

test('moving an entity kind overrides its tier and can be removed', () => {
  const moved = moveKindToOrbit(DEFAULT_ORBIT_CONFIG, 'Database', 3);
  assert.equal(effectiveOrbitTier(moved, 'Database', 4), 3);
  assert.equal(effectiveOrbitTier(moved, 'Service', 1), 1);
  assert.equal(effectiveOrbitTier(moveKindToOrbit(moved, 'Database', null), 'Database', 4), 4);
});

test('sanitize repairs invalid, duplicated and missing data', () => {
  assert.deepEqual(sanitizeOrbitConfig(null), DEFAULT_ORBIT_CONFIG);
  const repaired = sanitizeOrbitConfig({ orbits: [[2, 2, 9], 'x', [0]], kindTiers: { Topic: 'a', Queue: 1 } });
  assert.deepEqual(repaired.orbits, [[2], [0], [1], [3], [4], [5]]);
  assert.deepEqual(repaired.kindTiers, { Queue: 1 });
});

test('layout places merged tiers on a single shared ring', () => {
  const cfg = mergeOrbits(DEFAULT_ORBIT_CONFIG, 1, 2);
  const nodes = [
    { id: 'app', kind: 'App', echelonTier: 0 },
    { id: 'svc', kind: 'Service', echelonTier: 1 },
    { id: 'topic', kind: 'Topic', echelonTier: 2 },
    { id: 'db', kind: 'Database', echelonTier: 5 },
  ].map((n) => ({ ...n, echelonTier: effectiveOrbitTier(cfg, n.kind, n.echelonTier) }));
  const edges = [
    { source: 'app', target: 'svc', category: 'service_call' },
    { source: 'svc', target: 'topic', category: 'messaging' },
    { source: 'svc', target: 'db', category: 'database' },
  ];

  const result = computeConcentricLayout(nodes, edges, 1, orbitOrder(cfg));

  assert.equal(result.nodeRadii.get('svc'), result.nodeRadii.get('topic'));
  assert.deepEqual(
    result.populatedOrbits.map((o) => o.levelIndex),
    [0, 1, 4]
  );
});

test('without a custom order the layout keeps the fixed default order', () => {
  const nodes = [
    { id: 'app', kind: 'App', echelonTier: 0 },
    { id: 'svc', kind: 'Service', echelonTier: 1 },
    { id: 'topic', kind: 'Topic', echelonTier: 2 },
    { id: 'svc2', kind: 'Service', echelonTier: 3 },
    { id: 'db', kind: 'Database', echelonTier: 4 },
  ];
  const edges = [
    { source: 'app', target: 'svc2', category: 'service_call' },
    { source: 'svc2', target: 'db', category: 'database' },
    { source: 'svc', target: 'topic', category: 'messaging' },
  ];

  const result = computeConcentricLayout(nodes, edges, 1, orbitOrder(DEFAULT_ORBIT_CONFIG));

  assert.deepEqual(
    result.populatedOrbits.map((o) => o.levelIndex),
    [0, 1, 2, 3, 4]
  );
});
