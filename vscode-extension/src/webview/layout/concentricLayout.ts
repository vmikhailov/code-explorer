/**
 * Multi-Tier Concentric Orbital Layout with Edge-Length Minimization,
 * Topological Affinity Ordering, and 12 O'Clock Badge Collision Avoidance.
 */

export interface ConcentricNodeInput {
  id: string;
  echelonTier: number;
}

export interface ConcentricEdgeInput {
  source: string;
  target: string;
  category?: string;
  count?: number;
}

export interface ConcentricOrbitGuide {
  radius: number;
  label: string;
  count: number;
}

export interface ConcentricLayoutResult {
  positions: Map<string, { x: number; y: number }>;
  nodeAngles: Map<string, number>;
  nodeRadii: Map<string, number>;
  guides: ConcentricOrbitGuide[];
  populatedOrbits: Array<{
    levelIndex: number;
    label: string;
    nodeIds: string[];
    radius: number;
  }>;
}

export const ORBIT_NAMES: Record<number, string> = {
  0: 'Orbit 0: Ingress & Gateways',
  1: 'Orbit 1: First Echelon (Gateway Facing)',
  2: 'Orbit 2: Second Echelon (Internal Domain)',
  3: 'Orbit 3: Downstream Services & Workers',
  4: 'Orbit 4: Message Topics (Pub/Sub & RabbitMQ)',
  5: 'Orbit 5: Databases & External Services',
};

export function normRad(a: number): number {
  return ((a % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);
}

export function normalizeAngle(a: number): number {
  let ang = a % (2 * Math.PI);
  if (ang <= -Math.PI) ang += 2 * Math.PI;
  if (ang > Math.PI) ang -= 2 * Math.PI;
  return ang;
}

export function circularMean(items: Array<{ angle: number; weight: number }>): number {
  if (items.length === 0) return 0;
  let s = 0;
  let c = 0;
  for (const item of items) {
    s += Math.sin(item.angle) * item.weight;
    c += Math.cos(item.angle) * item.weight;
  }
  if (Math.hypot(s, c) < 1e-6) return 0;
  return Math.atan2(s, c);
}

export function cleanEntityToken(id: string): string {
  let t = id.toLowerCase();
  for (const prefix of [
    'domain:',
    'ws:db:relational:postgresql:',
    'ws:db:analytics:bigquery:',
    'ws:db:cache:',
    'ws:db:relational:mysql:',
    'ws:db:analytics:clickhouse:',
    'ws:top:rabbitmq:',
    'ws:top:gcp:',
  ]) {
    if (t.startsWith(prefix)) {
      t = t.slice(prefix.length);
    }
  }
  for (const prefix of ['internal-service-', 'integration-service-', 'internal-', 'service-', 'cli-']) {
    if (t.startsWith(prefix)) {
      t = t.slice(prefix.length);
    }
  }
  for (const suffix of ['-service', '-worker', '-topic']) {
    if (t.endsWith(suffix)) {
      t = t.slice(0, -suffix.length);
    }
  }
  return t;
}

/**
 * Computes deterministic, invariant architectural echelon tiers (0..5) on the complete architecture graph.
 */
export function computeEchelonTiers(
  allNodes: Array<{ id: string; kind: string }>,
  rawEdges: Array<{ source: string; target: string; category?: string }>
): Map<string, number> {
  const serviceAdj = new Map<string, Set<string>>();
  const inboundServiceCalls = new Map<string, number>();

  for (const e of rawEdges) {
    if (e.category === 'service_call') {
      if (!serviceAdj.has(e.source)) serviceAdj.set(e.source, new Set());
      serviceAdj.get(e.source)!.add(e.target);
      inboundServiceCalls.set(e.target, (inboundServiceCalls.get(e.target) || 0) + 1);
    }
  }

  const serviceTiers = new Map<string, number>();
  const queue: Array<{ id: string; tier: number }> = [];

  // 1. Ingress nodes (Tier 0)
  for (const n of allNodes) {
    if (n.kind === 'Ingress') {
      queue.push({ id: n.id, tier: 0 });
    }
  }

  // 2. Top-level service orchestrators (0 in, >0 out)
  for (const n of allNodes) {
    if (n.kind === 'Service') {
      const inCount = inboundServiceCalls.get(n.id) || 0;
      const outCount = serviceAdj.get(n.id)?.size || 0;
      if (inCount === 0 && outCount > 0) {
        serviceTiers.set(n.id, 1);
        queue.push({ id: n.id, tier: 1 });
      }
    }
  }

  while (queue.length > 0) {
    const { id, tier } = queue.shift()!;
    const neighbors = serviceAdj.get(id);
    if (neighbors) {
      for (const nextId of neighbors) {
        const nextTier = tier + 1;
        const existing = serviceTiers.get(nextId);
        if (existing === undefined || nextTier < existing) {
          serviceTiers.set(nextId, nextTier);
          queue.push({ id: nextId, tier: nextTier });
        }
      }
    }
  }

  const echelonMap = new Map<string, number>();
  for (const n of allNodes) {
    if (n.kind === 'Ingress') {
      echelonMap.set(n.id, 0);
    } else if (n.kind === 'Worker') {
      // Background workers & schedulers are strictly Orbit 3
      echelonMap.set(n.id, 3);
    } else if (n.kind === 'Service') {
      const t = serviceTiers.get(n.id) ?? 2;
      echelonMap.set(n.id, t <= 1 ? 1 : t === 2 ? 2 : 3);
    } else if (n.kind === 'Topic') {
      echelonMap.set(n.id, 4);
    } else if (n.kind === 'Database' || n.kind === 'ExternalService') {
      echelonMap.set(n.id, 5);
    } else {
      echelonMap.set(n.id, 5);
    }
  }

  return echelonMap;
}

/**
 * 1D Circular relaxation with strict monotonic separation delta and
 * 12 o'clock (-90°) badge exclusion zone avoidance.
 */
export function relaxOrbitWithBadgeExclusion(
  nodeIds: string[],
  idealAngles: Map<string, number>,
  radius: number,
  minArcSpacing: number
): Map<string, number> {
  const N = nodeIds.length;
  const result = new Map<string, number>();
  if (N === 0) return result;

  const badgeHalfAngle = Math.max(Math.PI / 15, 70 / Math.max(1, radius));
  const badgeCenter = -Math.PI / 2;
  const badgeMin = badgeCenter - badgeHalfAngle;
  const badgeMax = badgeCenter + badgeHalfAngle;

  if (N === 1) {
    const ideal = idealAngles.get(nodeIds[0]) ?? 0.0;
    let normIdeal = normalizeAngle(ideal);
    if (normIdeal >= badgeMin && normIdeal <= badgeMax) {
      normIdeal = Math.PI / 2; // Snap to 6 o'clock (opposite to badge)
    }
    result.set(nodeIds[0], normIdeal);
    return result;
  }

  const availableSpan = 2 * Math.PI - 2 * badgeHalfAngle;
  const delta = Math.min(availableSpan / N, minArcSpacing / Math.max(1, radius));

  const toUnrolled = (ang: number): number => {
    let rel = (ang - badgeMax) % (2 * Math.PI);
    if (rel < 0) rel += 2 * Math.PI;
    if (rel > availableSpan) {
      const distToStart = 2 * Math.PI - rel;
      const distToEnd = rel - availableSpan;
      rel = distToStart < distToEnd ? 0 : availableSpan;
    }
    return rel;
  };

  const sortedNodes = [...nodeIds].sort((a, b) => {
    const uA = toUnrolled(idealAngles.get(a) ?? 0.0);
    const uB = toUnrolled(idealAngles.get(b) ?? 0.0);
    return uA - uB;
  });

  const ideals = sortedNodes.map((nid) => toUnrolled(idealAngles.get(nid) ?? 0.0));
  const angles = [...ideals];

  // Monotonic forward separation
  for (let i = 1; i < N; i++) {
    if (angles[i] < angles[i - 1] + delta) {
      angles[i] = angles[i - 1] + delta;
    }
  }

  const span = angles[N - 1] - angles[0];
  if (span > availableSpan || angles[N - 1] > availableSpan) {
    const step = availableSpan / N;
    const start = (availableSpan - (N - 1) * step) / 2;
    for (let i = 0; i < N; i++) {
      angles[i] = start + i * step;
    }
  } else {
    const idealAvg = ideals.reduce((s, v) => s + v, 0) / N;
    const actualAvg = angles.reduce((s, v) => s + v, 0) / N;
    let shift = idealAvg - actualAvg;
    const minShift = -angles[0];
    const maxShift = availableSpan - angles[N - 1];
    shift = Math.max(minShift, Math.min(maxShift, shift));
    for (let i = 0; i < N; i++) {
      angles[i] += shift;
    }
  }

  for (let i = 0; i < N; i++) {
    result.set(sortedNodes[i], normalizeAngle(badgeMax + angles[i]));
  }
  return result;
}

/**
 * Complete concentric layout pipeline:
 * - Multi-tier orbital echelon bucketing
 * - Collision-free radius calculation
 * - Circular barycentric alignment
 * - Inward / outward force sweeps
 */
export function computeConcentricLayout(
  visibleNodes: ConcentricNodeInput[],
  visibleEdges: ConcentricEdgeInput[],
  spacing = 1.0
): ConcentricLayoutResult {
  const orbitBuckets: Record<number, string[]> = {
    0: [],
    1: [],
    2: [],
    3: [],
    4: [],
    5: [],
  };

  for (const node of visibleNodes) {
    const ech = Math.max(0, Math.min(5, node.echelonTier));
    orbitBuckets[ech].push(node.id);
  }

  const populatedOrbits: Array<{
    levelIndex: number;
    label: string;
    nodeIds: string[];
    radius: number;
  }> = [];

  for (const idx of [0, 1, 2, 3, 4, 5]) {
    if (orbitBuckets[idx].length > 0) {
      populatedOrbits.push({
        levelIndex: idx,
        label: ORBIT_NAMES[idx] || `Orbit ${idx}`,
        nodeIds: orbitBuckets[idx],
        radius: 0,
      });
    }
  }

  const minArcSpacing = Math.round(110 * spacing);
  const radialStep = Math.round(260 * spacing);

  let prevRadius = 0;
  populatedOrbits.forEach((orbit, idx) => {
    const count = orbit.nodeIds.length;
    let r: number;
    if (idx === 0) {
      r = count <= 1 ? 0 : Math.max(Math.round(90 * spacing), (count * minArcSpacing) / (2 * Math.PI));
    } else {
      const minCircumRadius = (count * minArcSpacing) / (2 * Math.PI);
      r = Math.max(prevRadius + radialStep, minCircumRadius);
    }
    orbit.radius = r;
    prevRadius = r;
  });

  // Graph adjacency with dedicated token match bonus
  const graphAdj = new Map<string, Array<{ neighborId: string; weight: number }>>();
  for (const e of visibleEdges) {
    const s = e.source;
    const t = e.target;
    if (!s || !t) continue;
    const cat = e.category || '';
    const count = e.count || 1;
    const sTok = cleanEntityToken(s);
    const tTok = cleanEntityToken(t);
    let bonus = 0;
    if (sTok && tTok && (sTok === tTok || sTok.includes(tTok) || tTok.includes(sTok))) {
      bonus = 30;
    }
    const weight =
      (cat === 'service_call' ? 3 * count : cat === 'database' || cat === 'messaging' ? 2 * count : count) + bonus;

    if (!graphAdj.has(s)) graphAdj.set(s, []);
    if (!graphAdj.has(t)) graphAdj.set(t, []);
    graphAdj.get(s)!.push({ neighborId: t, weight });
    graphAdj.get(t)!.push({ neighborId: s, weight });
  }

  const nodeAngles = new Map<string, number>();

  // Find first ring with R > 0
  let firstNonZeroRingIdx = -1;
  for (let idx = 0; idx < populatedOrbits.length; idx++) {
    if (populatedOrbits[idx].radius > 0) {
      firstNonZeroRingIdx = idx;
      break;
    }
  }

  // Seed innermost non-zero radius ring
  if (firstNonZeroRingIdx !== -1) {
    const firstOrbit = populatedOrbits[firstNonZeroRingIdx];
    const oNodes = firstOrbit.nodeIds;
    const unvisited = new Set(oNodes);

    let startNode = oNodes[0];
    let maxDeg = -1;
    for (const s of oNodes) {
      const deg = (graphAdj.get(s) || []).reduce((sum, n) => sum + n.weight, 0);
      if (deg > maxDeg) {
        maxDeg = deg;
        startNode = s;
      }
    }

    const orderedFirst: string[] = [startNode];
    unvisited.delete(startNode);
    while (unvisited.size > 0) {
      const curr = orderedFirst[orderedFirst.length - 1];
      let bestCand = '';
      let bestScore = -1;
      for (const cand of unvisited) {
        const score = (graphAdj.get(curr) || []).find((n) => n.neighborId === cand)?.weight || 0;
        if (score > bestScore) {
          bestScore = score;
          bestCand = cand;
        }
      }
      const nextNode = bestCand || unvisited.values().next().value!;
      orderedFirst.push(nextNode);
      unvisited.delete(nextNode);
    }

    const N = orderedFirst.length;
    const badgeHalf = Math.max(Math.PI / 15, 70 / Math.max(1, firstOrbit.radius));
    const available = 2 * Math.PI - 2 * badgeHalf;
    const step = available / N;
    const startAngle = -Math.PI / 2 + badgeHalf + step / 2;

    orderedFirst.forEach((id, i) => {
      nodeAngles.set(id, normalizeAngle(startAngle + i * step));
    });
  }

  // Center ring (R = 0)
  if (populatedOrbits.length > 0 && populatedOrbits[0].radius === 0) {
    for (const id of populatedOrbits[0].nodeIds) {
      nodeAngles.set(id, 0.0);
    }
  }

  // Outward sweep for subsequent rings
  if (firstNonZeroRingIdx !== -1) {
    for (let idx = firstNonZeroRingIdx + 1; idx < populatedOrbits.length; idx++) {
      const orbit = populatedOrbits[idx];
      const connected: string[] = [];
      const disconnected: string[] = [];
      const ideal = new Map<string, number>();

      orbit.nodeIds.forEach((id) => {
        const nbrs = (graphAdj.get(id) || []).filter((n) => nodeAngles.has(n.neighborId));
        if (nbrs.length > 0) {
          ideal.set(
            id,
            circularMean(nbrs.map((n) => ({ angle: nodeAngles.get(n.neighborId)!, weight: n.weight })))
          );
          connected.push(id);
        } else {
          disconnected.push(id);
        }
      });

      const badgeHalf = Math.max(Math.PI / 15, 70 / Math.max(1, orbit.radius));
      const available = 2 * Math.PI - 2 * badgeHalf;

      if (connected.length === 0) {
        const N = disconnected.length;
        const step = available / N;
        const startAngle = -Math.PI / 2 + badgeHalf + step / 2;
        disconnected.forEach((id, i) => {
          ideal.set(id, normalizeAngle(startAngle + i * step));
        });
      } else {
        const meanConnected = circularMean(
          connected.map((id) => ({ angle: ideal.get(id)!, weight: 1 }))
        );
        const opposite = normalizeAngle(meanConnected + Math.PI);
        const M = disconnected.length;
        const discStep = Math.min(Math.PI / 4, available / Math.max(1, M * 2));
        const discStart = normalizeAngle(opposite - ((M - 1) * discStep) / 2);
        disconnected.forEach((id, i) => {
          ideal.set(id, normalizeAngle(discStart + i * discStep));
        });
      }

      const relaxed = relaxOrbitWithBadgeExclusion(orbit.nodeIds, ideal, orbit.radius, minArcSpacing);
      relaxed.forEach((angle, id) => nodeAngles.set(id, angle));
    }

    // Inward sweep
    for (let idx = populatedOrbits.length - 2; idx >= firstNonZeroRingIdx; idx--) {
      const orbit = populatedOrbits[idx];
      const ideal = new Map<string, number>();
      orbit.nodeIds.forEach((id) => {
        const nbrs = (graphAdj.get(id) || []).filter((n) => nodeAngles.has(n.neighborId));
        if (nbrs.length > 0) {
          ideal.set(
            id,
            circularMean(nbrs.map((n) => ({ angle: nodeAngles.get(n.neighborId)!, weight: n.weight })))
          );
        } else {
          ideal.set(id, nodeAngles.get(id) ?? 0.0);
        }
      });
      const relaxed = relaxOrbitWithBadgeExclusion(orbit.nodeIds, ideal, orbit.radius, minArcSpacing);
      relaxed.forEach((angle, id) => nodeAngles.set(id, angle));
    }

    // Final outward snap
    for (let idx = firstNonZeroRingIdx + 1; idx < populatedOrbits.length; idx++) {
      const orbit = populatedOrbits[idx];
      const ideal = new Map<string, number>();
      orbit.nodeIds.forEach((id) => {
        const nbrs = (graphAdj.get(id) || []).filter((n) => nodeAngles.has(n.neighborId));
        if (nbrs.length > 0) {
          ideal.set(
            id,
            circularMean(nbrs.map((n) => ({ angle: nodeAngles.get(n.neighborId)!, weight: n.weight })))
          );
        } else {
          ideal.set(id, nodeAngles.get(id) ?? 0.0);
        }
      });
      const relaxed = relaxOrbitWithBadgeExclusion(orbit.nodeIds, ideal, orbit.radius, minArcSpacing);
      relaxed.forEach((angle, id) => nodeAngles.set(id, angle));
    }
  }

  const positions = new Map<string, { x: number; y: number }>();
  const nodeRadii = new Map<string, number>();

  for (const orbit of populatedOrbits) {
    const R = orbit.radius;
    for (const id of orbit.nodeIds) {
      nodeRadii.set(id, R);
      if (R === 0) {
        positions.set(id, { x: 0, y: 0 });
      } else {
        const theta = nodeAngles.get(id) ?? 0;
        positions.set(id, {
          x: Math.round(R * Math.cos(theta)),
          y: Math.round(R * Math.sin(theta)),
        });
      }
    }
  }

  const guides: ConcentricOrbitGuide[] = populatedOrbits
    .filter((o) => o.radius > 0)
    .map((o) => ({
      radius: o.radius,
      label: o.label,
      count: o.nodeIds.length,
    }));

  return {
    positions,
    nodeAngles,
    nodeRadii,
    guides,
    populatedOrbits,
  };
}
