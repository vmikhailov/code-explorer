/**
 * domainLayouts.test.ts
 * Unit tests verifying Swimlanes, Domain Islands, and Hive Plot layouts.
 */

import test from 'node:test';
import assert from 'node:assert/strict';
import { computeSwimlanesLayout, SwimlaneNodeInput, SwimlaneEdgeInput } from './swimlanesLayout';
import { computeDomainIslandsLayout, IslandNodeInput, IslandEdgeInput } from './domainIslandsLayout';
import { computeHivePlotLayout, HiveNodeInput, HiveEdgeInput } from './hivePlotLayout';
import { computeConcentricSectorsLayout, describeAnnularSector } from './concentricSectorsLayout';

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

test('computeConcentricSectorsLayout: allocates proportional sector angles and guides without overlap', () => {
  const nodes = [
    { id: 'p1', name: 'player-svc', kind: 'Service', echelonTier: 2, domain: 'domain-player' },
    { id: 'p2', name: 'player-db', kind: 'Database', echelonTier: 5, domain: 'domain-player' },
    { id: 'p3', name: 'player-worker', kind: 'Worker', echelonTier: 3, domain: 'domain-player' },
    { id: 'p4', name: 'player-cache', kind: 'Database', echelonTier: 5, domain: 'domain-player' },
    { id: 'b1', name: 'billing-svc', kind: 'Service', echelonTier: 2, domain: 'domain-billing' },
  ];
  const edges = [
    { source: 'p1', target: 'p2', category: 'database' },
    { source: 'p1', target: 'p3', category: 'service_call' },
    { source: 'p1', target: 'p4', category: 'database' },
  ];
  const nodeDomainMap = new Map([
    ['p1', 'domain-player'],
    ['p2', 'domain-player'],
    ['p3', 'domain-player'],
    ['p4', 'domain-player'],
    ['b1', 'domain-billing'],
  ]);
  const domainDetailsMap = new Map([
    ['domain-player', { displayName: 'Player Domain', color: '#38bdf8' }],
    ['domain-billing', { displayName: 'Billing Domain', color: '#f59e0b' }],
  ]);

  const result = computeConcentricSectorsLayout(nodes, edges, 1.0, undefined, nodeDomainMap, domainDetailsMap);

  assert.equal(result.positions.size, 5, 'All 5 nodes must have positions');
  assert.ok(result.sectors && result.sectors.length >= 2, 'Must produce at least 2 sector guides');

  const playerSector = result.sectors!.find((s) => s.id === 'domain-player');
  const billingSector = result.sectors!.find((s) => s.id === 'domain-billing');

  assert.ok(playerSector, 'Player sector must exist');
  assert.ok(billingSector, 'Billing sector must exist');

  // Player domain has 4 nodes vs Billing domain 1 node -> Player sector must have a wider angle span!
  const playerSpan = playerSector!.endAngle - playerSector!.startAngle;
  const billingSpan = billingSector!.endAngle - billingSector!.startAngle;
  assert.ok(playerSpan > billingSpan, 'Domain with 4 nodes must receive larger angular sector than domain with 1 node');

  // Verify node angles are inside their sector boundaries
  for (const n of nodes) {
    const pos = result.positions.get(n.id)!;
    assert.ok(Number.isFinite(pos.x) && Number.isFinite(pos.y), `Node ${n.id} position must be finite`);
    const angle = result.nodeAngles?.get(n.id);
    assert.ok(angle !== undefined, `Node ${n.id} must have assigned angle`);
    const sector = n.domain === 'domain-player' ? playerSector! : billingSector!;
    assert.ok(
      angle! >= sector.startAngle - 0.05 && angle! <= sector.endAngle + 0.05,
      `Node ${n.id} angle ${angle} must fall within sector [${sector.startAngle}, ${sector.endAngle}]`
    );
  }

  // Verify describeAnnularSector produces valid SVG path
  const svgPath = describeAnnularSector(0, 0, 100, 300, 0, Math.PI / 2);
  assert.ok(svgPath.startsWith('M '), 'SVG path must start with M');
  assert.ok(svgPath.endsWith('Z'), 'SVG path must end with Z');
  assert.ok(svgPath.includes('A 300 300'), 'SVG path must contain outer arc');
  assert.ok(svgPath.includes('A 100 100'), 'SVG path must contain inner arc');
});
