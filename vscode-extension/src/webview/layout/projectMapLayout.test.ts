import test from 'node:test';
import assert from 'node:assert/strict';
import { computeProjectMapLayout, circularMean } from './projectMapLayout';
import { filterProjectMap, parseProjectMapGraph } from '../projectMap/projectMapModel';

test('single center node sits at the origin and outer rings grow outward', () => {
  const layout = computeProjectMapLayout(
    [
      { id: 'entry', tier: 0, radius: 20 },
      { id: 'a', tier: 2, radius: 20 },
      { id: 'b', tier: 2, radius: 20 },
      { id: 'tbl', tier: 5, radius: 14 },
    ],
    [
      { source: 'entry', target: 'a' },
      { source: 'a', target: 'tbl' },
    ]
  );

  assert.deepEqual(layout.positions.entry, { x: 0, y: 0, angle: 0 });
  assert.equal(layout.rings.length, 3);
  assert.ok(layout.rings[1].radius > layout.rings[0].radius);
  assert.ok(layout.rings[2].radius > layout.rings[1].radius);
  assert.ok(layout.extent > layout.rings[2].radius);
});

test('crowded rings expand so nodes never overlap', () => {
  const nodes = Array.from({ length: 40 }, (_, i) => ({ id: `n${i}`, tier: 1, radius: 30 }));
  const layout = computeProjectMapLayout(nodes, []);
  const ring = layout.rings[0];
  const arc = (2 * Math.PI * ring.radius) / nodes.length;
  assert.ok(arc >= 60, `expected arc spacing >= node diameter, got ${arc}`);
});

test('nodes are placed near their already-placed neighbours (barycentric ordering)', () => {
  const layout = computeProjectMapLayout(
    [
      { id: 'left', tier: 0, radius: 10 },
      { id: 'right', tier: 0, radius: 10 },
      { id: 'childOfLeft', tier: 1, radius: 10 },
      { id: 'childOfRight', tier: 1, radius: 10 },
    ],
    [
      { source: 'left', target: 'childOfLeft' },
      { source: 'right', target: 'childOfRight' },
    ]
  );
  const angleTo = (a: string, b: string) => {
    const pa = layout.positions[a];
    const pb = layout.positions[b];
    const d = Math.abs(Math.atan2(pa.y, pa.x) - Math.atan2(pb.y, pb.x));
    return Math.min(d, 2 * Math.PI - d);
  };
  assert.ok(angleTo('left', 'childOfLeft') < angleTo('left', 'childOfRight'));
  assert.ok(angleTo('right', 'childOfRight') < angleTo('right', 'childOfLeft'));
});

test('circularMean handles wrap-around', () => {
  const m = circularMean([Math.PI - 0.1, -Math.PI + 0.1]);
  assert.ok(m !== null && Math.abs(Math.abs(m) - Math.PI) < 1e-6);
  assert.equal(circularMean([]), null);
});

test('filters hide utilities, resources and non-cycle modules', () => {
  const model = parseProjectMapGraph({
    nodes: [
      { id: 'a', kind: 'Folder', name: 'a', properties: { pm_type: 'module', tier: '1', cycle_group: '1' } },
      { id: 'b', kind: 'Folder', name: 'b', properties: { pm_type: 'module', tier: '2', cycle_group: '1' } },
      { id: 'u', kind: 'Folder', name: 'utils', properties: { pm_type: 'module', tier: '4' } },
      { id: 't', kind: 'Table', name: 'orders', properties: { pm_type: 'resource', tier: '5', resource_kind: 'Table' } },
    ],
    edges: [
      { source: 'a', target: 'b', kind: 'CALLS', properties: { is_circular: 'true', call_count: '3' } },
      { source: 'b', target: 'a', kind: 'CALLS', properties: { is_circular: 'true', call_count: '1' } },
      { source: 'a', target: 'u', kind: 'CALLS', properties: { is_circular: 'false' } },
      { source: 'b', target: 't', kind: 'WRITES_TO', properties: { is_resource_link: 'true' } },
    ],
    metadata: { cycles: '[["a","b"]]', projects: '[{"id":"p","name":"P","path":"p","files":3}]' },
  });

  assert.equal(model.cycles.length, 1);
  assert.equal(model.projects[0].name, 'P');
  assert.equal(model.edges[0].count, 3);

  const noUtils = filterProjectMap(model, { hideUtilities: true, hideResources: false, cyclesOnly: false });
  assert.deepEqual(noUtils.nodes.map((n) => n.id).sort(), ['a', 'b', 't']);
  assert.equal(noUtils.edges.some((e) => e.target === 'u'), false);

  const cycles = filterProjectMap(model, { hideUtilities: false, hideResources: false, cyclesOnly: true });
  assert.deepEqual(cycles.nodes.map((n) => n.id).sort(), ['a', 'b']);
  assert.equal(cycles.edges.length, 2);
});
