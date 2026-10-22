/**
 * Project Map concentric layout.
 *
 * Rings are the backend-provided tiers (0 = center). Within each ring nodes are equispaced and ordered by the
 * circular mean angle of their already-placed neighbours (barycentric ordering), which keeps connected modules
 * on the same side of the map and reduces edge crossings.
 */

export interface ProjectMapLayoutNode {
  id: string;
  tier: number;
  /** Visual radius in px; used to size rings so nodes never overlap. */
  radius: number;
}

export interface ProjectMapLayoutEdge {
  source: string;
  target: string;
}

export interface ProjectMapRing {
  tier: number;
  radius: number;
  nodeIds: string[];
}

export interface ProjectMapLayoutResult {
  positions: Record<string, { x: number; y: number; angle: number }>;
  rings: ProjectMapRing[];
  /** Half-width of the square that contains the whole layout (including label margin). */
  extent: number;
}

export interface ProjectMapLayoutOptions {
  ringGap?: number;
  labelSpace?: number;
  firstRingMinRadius?: number;
  margin?: number;
}

const TWO_PI = Math.PI * 2;
const START_ANGLE = -Math.PI / 2;

export function normalizeAngle(a: number): number {
  let x = a % TWO_PI;
  if (x < 0) x += TWO_PI;
  return x;
}

export function circularMean(angles: number[]): number | null {
  if (angles.length === 0) return null;
  let sx = 0;
  let sy = 0;
  for (const a of angles) {
    sx += Math.cos(a);
    sy += Math.sin(a);
  }
  if (Math.abs(sx) < 1e-9 && Math.abs(sy) < 1e-9) return null;
  return Math.atan2(sy, sx);
}

export function computeProjectMapLayout(
  nodes: ProjectMapLayoutNode[],
  edges: ProjectMapLayoutEdge[],
  options: ProjectMapLayoutOptions = {}
): ProjectMapLayoutResult {
  const ringGap = options.ringGap ?? 140;
  const labelSpace = options.labelSpace ?? 70;
  const firstRingMinRadius = options.firstRingMinRadius ?? 110;
  const margin = options.margin ?? 90;

  const byTier = new Map<number, ProjectMapLayoutNode[]>();
  for (const n of nodes) {
    const list = byTier.get(n.tier) ?? [];
    list.push(n);
    byTier.set(n.tier, list);
  }
  const tiers = [...byTier.keys()].sort((a, b) => a - b);

  const neighbors = new Map<string, Set<string>>();
  for (const e of edges) {
    if (e.source === e.target) continue;
    if (!neighbors.has(e.source)) neighbors.set(e.source, new Set());
    if (!neighbors.has(e.target)) neighbors.set(e.target, new Set());
    neighbors.get(e.source)!.add(e.target);
    neighbors.get(e.target)!.add(e.source);
  }

  const positions: ProjectMapLayoutResult['positions'] = {};
  const rings: ProjectMapRing[] = [];
  let prevRadius = 0;

  tiers.forEach((tier, ringIndex) => {
    const ringNodes = [...(byTier.get(tier) ?? [])].sort((a, b) => a.id.localeCompare(b.id));
    const maxNodeRadius = Math.max(...ringNodes.map((n) => n.radius), 10);
    const spacing = maxNodeRadius * 2 + labelSpace;
    const needed = (ringNodes.length * spacing) / TWO_PI;

    let radius: number;
    if (ringIndex === 0 && ringNodes.length === 1) {
      radius = 0;
    } else if (ringIndex === 0) {
      radius = Math.max(firstRingMinRadius, needed);
    } else {
      radius = Math.max(prevRadius + ringGap, needed, firstRingMinRadius);
    }

    const ordered = orderRing(ringNodes, neighbors, positions);
    const step = ordered.length > 0 ? TWO_PI / ordered.length : 0;
    const offset = ringOffset(ordered, neighbors, positions, step);

    ordered.forEach((n, k) => {
      const angle = radius === 0 ? 0 : offset + k * step;
      positions[n.id] = { x: Math.cos(angle) * radius, y: Math.sin(angle) * radius, angle };
    });

    rings.push({ tier, radius, nodeIds: ordered.map((n) => n.id) });
    prevRadius = radius;
  });

  return { positions, rings, extent: prevRadius + margin };
}

function desiredAngle(
  id: string,
  neighbors: Map<string, Set<string>>,
  positions: ProjectMapLayoutResult['positions']
): number | null {
  const placed = [...(neighbors.get(id) ?? [])]
    .map((nid) => positions[nid])
    .filter((p): p is { x: number; y: number; angle: number } => !!p && (p.x !== 0 || p.y !== 0))
    .map((p) => Math.atan2(p.y, p.x));
  return circularMean(placed);
}

function orderRing(
  ringNodes: ProjectMapLayoutNode[],
  neighbors: Map<string, Set<string>>,
  positions: ProjectMapLayoutResult['positions']
): ProjectMapLayoutNode[] {
  const anchored: { node: ProjectMapLayoutNode; angle: number }[] = [];
  const floating: ProjectMapLayoutNode[] = [];

  for (const n of ringNodes) {
    const a = desiredAngle(n.id, neighbors, positions);
    if (a === null) floating.push(n);
    else anchored.push({ node: n, angle: normalizeAngle(a - START_ANGLE) });
  }

  anchored.sort((a, b) => a.angle - b.angle || a.node.id.localeCompare(b.node.id));
  return [...anchored.map((a) => a.node), ...floating];
}

/**
 * Rotates the equispaced slots so anchored nodes land as close as possible to their desired angles.
 */
function ringOffset(
  ordered: ProjectMapLayoutNode[],
  neighbors: Map<string, Set<string>>,
  positions: ProjectMapLayoutResult['positions'],
  step: number
): number {
  const residuals: number[] = [];
  ordered.forEach((n, k) => {
    const a = desiredAngle(n.id, neighbors, positions);
    if (a !== null) residuals.push(a - k * step);
  });
  return circularMean(residuals) ?? START_ANGLE;
}
