/**
 * hivePlotLayout.ts
 *
 * Implements a Hive Plot (Multi-Axis Semantic Radial Layout) for DomainArchitectureView.
 * Maps nodes onto 5 radial axes according to their functional echelon:
 *   Axis 0 (-90° / North): 🚀 Ingress & Applications
 *   Axis 1 (-18° / North-East): ⚙️ Domain Services
 *   Axis 2 (54° / South-East): ⚡ Background Workers
 *   Axis 3 (126° / South-West): ✉️ Event Topics & Messaging
 *   Axis 4 (198° / North-West): 🗄️ Databases & Storage
 *
 * Along each axis, nodes are sorted by connectivity/degree.
 */

export interface HiveNodeInput {
  id: string;
  echelonTier: number; // 0..5
  kind: string;
  name: string;
  displayName?: string;
}

export interface HiveEdgeInput {
  source: string;
  target: string;
  count?: number;
}

export interface HiveAxisGuide {
  id: string;
  title: string;
  icon: string;
  angle: number; // in radians
  length: number;
  count: number;
  color: string;
}

export interface HivePlotLayoutResult {
  positions: Map<string, { x: number; y: number }>;
  axes: HiveAxisGuide[];
  bounds: { minX: number; maxX: number; minY: number; maxY: number };
}

interface AxisDefinition {
  tier: number;
  title: string;
  icon: string;
  angleDeg: number;
  color: string;
}

const AXIS_DEFINITIONS: AxisDefinition[] = [
  { tier: 0, title: 'Clients & Ingress', icon: '🚀', angleDeg: -90, color: '#38bdf8' },
  { tier: 1, title: 'Domain Services', icon: '⚙️', angleDeg: -18, color: '#4ade80' },
  { tier: 3, title: 'Background Workers', icon: '⚡', angleDeg: 54, color: '#c026d3' },
  { tier: 4, title: 'Event Topics', icon: '✉️', angleDeg: 126, color: '#fbbf24' },
  { tier: 5, title: 'Databases & Storage', icon: '🗄️', color: '#c084fc', angleDeg: 198 },
];

export function computeHivePlotLayout(
  nodes: HiveNodeInput[],
  edges: HiveEdgeInput[],
  air: number = 1.2
): HivePlotLayoutResult {
  const positions = new Map<string, { x: number; y: number }>();
  if (nodes.length === 0) {
    return {
      positions,
      axes: [],
      bounds: { minX: 0, maxX: 0, minY: 0, maxY: 0 },
    };
  }

  // 1. Calculate degrees for sorting along axes
  const degrees = new Map<string, number>();
  for (const n of nodes) degrees.set(n.id, 0);
  for (const e of edges) {
    degrees.set(e.source, (degrees.get(e.source) || 0) + (e.count || 1));
    degrees.set(e.target, (degrees.get(e.target) || 0) + (e.count || 1));
  }

  // 2. Map nodes to axes (Tier 2 services merge with Tier 1 on Services Axis)
  const axisBuckets = new Map<number, string[]>();
  AXIS_DEFINITIONS.forEach((def) => axisBuckets.set(def.tier, []));

  for (const n of nodes) {
    let t = n.echelonTier;
    if (t === 2) t = 1; // Merge tier 2 domain services with tier 1 onto Services axis
    if (!axisBuckets.has(t)) {
      t = n.kind === 'Topic' ? 4 : n.kind === 'Database' ? 5 : n.kind === 'Worker' ? 3 : 1;
    }
    axisBuckets.get(t)!.push(n.id);
  }

  const populatedAxes: Array<{ def: AxisDefinition; nodeIds: string[] }> = [];
  AXIS_DEFINITIONS.forEach((def) => {
    const list = axisBuckets.get(def.tier) || [];
    if (list.length > 0) {
      populatedAxes.push({ def, nodeIds: list });
    }
  });

  // 3. Spacing and dimensions
  const minRadius = Math.round(180 * air);
  const nodeStep = Math.round(55 * air);

  let maxAxisLen = minRadius;
  populatedAxes.forEach((axis) => {
    const len = minRadius + axis.nodeIds.length * nodeStep;
    if (len > maxAxisLen) maxAxisLen = len;
  });

  // 4. Place nodes along each axis
  let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;

  const guides: HiveAxisGuide[] = [];

  populatedAxes.forEach((axis) => {
    // Sort nodes by degree ascending (hub nodes towards outside or inside)
    axis.nodeIds.sort((a, b) => (degrees.get(b) || 0) - (degrees.get(a) || 0));

    const angleRad = (axis.def.angleDeg * Math.PI) / 180;
    const cosA = Math.cos(angleRad);
    const sinA = Math.sin(angleRad);

    axis.nodeIds.forEach((id, idx) => {
      const r = minRadius + idx * nodeStep;
      const x = Math.round(r * cosA);
      const y = Math.round(r * sinA);
      positions.set(id, { x, y });

      if (x < minX) minX = x;
      if (x > maxX) maxX = x;
      if (y < minY) minY = y;
      if (y > maxY) maxY = y;
    });

    const axisLength = minRadius + axis.nodeIds.length * nodeStep + 40;
    guides.push({
      id: `axis:${axis.def.tier}`,
      title: axis.def.title,
      icon: axis.def.icon,
      angle: angleRad,
      length: axisLength,
      count: axis.nodeIds.length,
      color: axis.def.color,
    });
  });

  return {
    positions,
    axes: guides,
    bounds: {
      minX: minX === Infinity ? 0 : minX,
      maxX: maxX === -Infinity ? 0 : maxX,
      minY: minY === Infinity ? 0 : minY,
      maxY: maxY === -Infinity ? 0 : maxY,
    },
  };
}
