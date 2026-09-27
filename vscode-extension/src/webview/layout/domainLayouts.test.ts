/**
 * domainLayouts.test.ts
 * Unit tests verifying Swimlanes, Domain Islands, and Hive Plot layouts.
 */

import test from 'node:test';
import assert from 'node:assert/strict';
import { computeSwimlanesLayout, SwimlaneNodeInput, SwimlaneEdgeInput } from './swimlanesLayout';
import { computeDomainIslandsLayout, IslandNodeInput, IslandEdgeInput } from './domainIslandsLayout';
import { computeHivePlotLayout, HiveNodeInput, HiveEdgeInput } from './hivePlotLayout';

const sampleNodes = [
  { id: 'ats-front', name: 'ats-front', kind: 'Ingress', echelonTier: 0 },
  { id: 'cf-worker-service', name: 'cf-worker-service', kind: 'Worker', echelonTier: 1 },
  { id: 'player-service', name: 'player-service', kind: 'Service', echelonTier: 2 },
  { id: 'ad-hub-service', name: 'ad-hub-service', kind: 'Service', echelonTier: 2 },
  { id: 'action-scheduler', name: 'action-scheduler', kind: 'Worker', echelonTier: 3 },
  { id: 'topic-billing', name: 'topic-billing', kind: 'Topic', echelonTier: 4 },
  { id: 'db-player', name: 'db-player', kind: 'Database', echelonTier: 5 },
  { id: 'db-shared', name: 'db-shared', kind: 'Database', echelonTier: 5 },
];

const sampleEdges = [
  { source: 'ats-front', target: 'cf-worker-service', category: 'service_call' },
  { source: 'cf-worker-service', target: 'player-service', category: 'service_call' },
  { source: 'player-service', target: 'ad-hub-service', category: 'service_call' },
  { source: 'player-service', target: 'db-player', category: 'database' },
  { source: 'ad-hub-service', target: 'db-shared', category: 'database' },
  { source: 'player-service', target: 'db-shared', category: 'database' },
  { source: 'ad-hub-service', target: 'topic-billing', category: 'messaging' },
  { source: 'action-scheduler', target: 'db-shared', category: 'database' },
];

test('computeSwimlanesLayout: computes non-overlapping vertical columns for all populated tiers', () => {
  const result = computeSwimlanesLayout(sampleNodes as SwimlaneNodeInput[], sampleEdges as SwimlaneEdgeInput[]);

  assert.equal(result.positions.size, sampleNodes.length, 'All nodes must have positions');
  assert.ok(result.lanes.length >= 4, 'Must have at least 4 swimlane guides');

  // Verify each node is inside its lane's x bounds
  for (const node of sampleNodes) {
    const pos = result.positions.get(node.id);
    assert.ok(pos, `Node ${node.id} must have position`);
    const lane = result.lanes.find((g) => pos.x >= g.x - 10 && pos.x <= g.x + g.width + 10);
    assert.ok(lane, `Node ${node.id} at (${pos.x}, ${pos.y}) must fall within a lane`);
  }

  // Verify lane headers do not overlap horizontally
  for (let i = 0; i < result.lanes.length - 1; i++) {
    const current = result.lanes[i];
    const next = result.lanes[i + 1];
    assert.ok(current.x + current.width <= next.x + 1, `Lane ${current.id} must not overlap lane ${next.id}`);
  }
});

test('computeDomainIslandsLayout: groups services into islands and identifies shared core', () => {
  const result = computeDomainIslandsLayout(sampleNodes as IslandNodeInput[], sampleEdges as IslandEdgeInput[]);

  assert.equal(result.positions.size, sampleNodes.length, 'All nodes must have positions');
  assert.ok(result.islands.length >= 2, 'Must produce multiple domain islands');

  // db-shared is used by player-service, ad-hub-service, and action-scheduler -> must be in shared core
  const sharedGuide = result.islands.find((g) => g.isSharedCore);
  assert.ok(sharedGuide, 'Must identify a shared infrastructure core');

  // Verify all positions are finite numbers
  for (const [id, pos] of result.positions.entries()) {
    assert.ok(!Number.isNaN(pos.x) && Number.isFinite(pos.x), `Node ${id} x must be finite`);
    assert.ok(!Number.isNaN(pos.y) && Number.isFinite(pos.y), `Node ${id} y must be finite`);
  }
});

test('computeHivePlotLayout: distributes nodes along canonical axes without NaN or overlaps', () => {
  const result = computeHivePlotLayout(sampleNodes as HiveNodeInput[], sampleEdges as HiveEdgeInput[]);

  assert.equal(result.positions.size, sampleNodes.length, 'All nodes must have positions');
  assert.equal(result.axes.length, 5, 'Must have 5 populated hive axes');

  for (const [id, pos] of result.positions.entries()) {
    assert.ok(!Number.isNaN(pos.x) && Number.isFinite(pos.x), `Node ${id} x must be finite`);
    assert.ok(!Number.isNaN(pos.y) && Number.isFinite(pos.y), `Node ${id} y must be finite`);
  }

  // Verify axis angles are separated by 72 deg (2*PI/5)
  const diff1 = Math.abs(result.axes[1].angle - result.axes[0].angle);
  assert.ok(Math.abs(diff1 - (2 * Math.PI) / 5) < 0.05, 'Axes must be 72 deg apart');
});
