/**
 * Multi-Tier Concentric Orbital Layout with Edge-Length Minimization,
 * Topological Affinity Ordering, and 12 O'Clock Badge Collision Avoidance.
 */

export interface ConcentricNodeInput {
  id: string;
  echelonTier: number;
  kind?: string;
  name?: string;
  displayName?: string;
  domain?: string;
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
  shortLabel: string;
  title: string;
  levelIndex: number;
  count: number;
}

export interface DomainSectorGuide {
  id: string;
  domainName: string;
  displayName: string;
  startAngle: number;
  endAngle: number;
  centerAngle: number;
  innerRadius: number;
  outerRadius: number;
  color: string;
  count: number;
  nodeIds: string[];
  isShared?: boolean;
}

export interface ConcentricLayoutResult {
  positions: Map<string, { x: number; y: number }>;
  nodeAngles: Map<string, number>;
  nodeRadii: Map<string, number>;
  guides: ConcentricOrbitGuide[];
  populatedOrbits: Array<{
    levelIndex: number;
    label: string;
    shortLabel: string;
    title: string;
    nodeIds: string[];
    radius: number;
  }>;
  sectors?: DomainSectorGuide[];
}

export const ORBIT_TITLES: Record<number, string> = {
  0: 'Applications & Clients',
  1: 'First Echelon (Gateway Facing Services)',
  2: 'Message Topics & Queues (Pub/Sub & RabbitMQ)',
  3: 'Second Echelon (Internal Domain Services & Workers)',
  4: 'Databases & External Infrastructure',
  5: 'Databases & External Infrastructure',
  6: 'Databases & Storage',
};

export const ORBIT_NAMES: Record<number, string> = {
  0: 'Orbit 0: Applications & Clients',
  1: 'Orbit 1: First Echelon (Gateway Facing Services)',
  2: 'Orbit 2: Message Topics & Queues (Pub/Sub & RabbitMQ)',
  3: 'Orbit 3: Second Echelon (Internal Domain Services & Workers)',
  4: 'Orbit 4: Databases & External Infrastructure',
  5: 'Orbit 5: Databases & External Infrastructure',
  6: 'Orbit 6: Databases & Storage',
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

  // 1. Applications and Ingress nodes (Tier 0)
  for (const n of allNodes) {
    if (n.kind === 'App' || n.kind === 'FrontendApp' || n.kind === 'Ingress') {
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
    if (n.kind === 'App' || n.kind === 'FrontendApp' || n.kind === 'Ingress') {
      echelonMap.set(n.id, 0);
    } else if (n.kind === 'Topic' || n.kind === 'Queue' || n.kind === 'Broker' || n.kind === 'EventBus') {
      // Message Topics & Queues placed between First and Second Echelon of services
      echelonMap.set(n.id, 2);
    } else if (n.kind === 'Service') {
      const t = serviceTiers.get(n.id) ?? 2;
      // First echelon services (Gateway-facing): Orbit 1
      // Second echelon services (Internal Domain): Orbit 3 (after Topics)
      echelonMap.set(n.id, t <= 1 ? 1 : 3);
    } else if (n.kind === 'Worker' || n.kind === 'CliTool' || n.kind === 'TestSuite') {
      // Workers, CLI, TestSuites merged into Second Echelon (Orbit 3) alongside internal domain services
      echelonMap.set(n.id, 3);
    } else if (n.kind === 'Database' || n.kind === 'ExternalService') {
      echelonMap.set(n.id, 4);
    } else if (n.kind === 'Library' || n.kind === 'SharedLibrary') {
      echelonMap.set(n.id, 4);
    } else {
      echelonMap.set(n.id, 4);
    }
  }

  return echelonMap;
}

/**
 * Computes human-readable explainability reasons ("why is this node in this tier?")
 * for each node in the architecture graph.
 */
export function computeEchelonTierReasons(
  allNodes: Array<{ id: string; kind: string; displayName?: string }>,
  rawEdges: Array<{ source: string; target: string; category?: string }>
): Map<string, string> {
  const nameMap = new Map<string, string>();
  const kindMap = new Map<string, string>();
  for (const n of allNodes) {
    nameMap.set(n.id, n.displayName || n.id);
    kindMap.set(n.id, n.kind);
  }

  const serviceAdj = new Map<string, Set<string>>();
  const inboundCallers = new Map<string, Set<string>>();
  for (const e of rawEdges) {
    if (e.category === 'service_call') {
      if (!serviceAdj.has(e.source)) serviceAdj.set(e.source, new Set());
      serviceAdj.get(e.source)!.add(e.target);
      if (!inboundCallers.has(e.target)) inboundCallers.set(e.target, new Set());
      inboundCallers.get(e.target)!.add(e.source);
    }
  }

  const serviceTiers = new Map<string, number>();
  const viaCaller = new Map<string, string>();
  const queue: Array<{ id: string; tier: number }> = [];

  for (const n of allNodes) {
    if (n.kind === 'App' || n.kind === 'FrontendApp' || n.kind === 'Ingress') {
      queue.push({ id: n.id, tier: 0 });
    }
  }
  for (const n of allNodes) {
    if (n.kind === 'Service') {
      const inCount = inboundCallers.get(n.id)?.size || 0;
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
          viaCaller.set(nextId, id);
          queue.push({ id: nextId, tier: nextTier });
        }
      }
    }
  }

  const reasons = new Map<string, string>();
  for (const n of allNodes) {
    if (n.kind === 'App' || n.kind === 'FrontendApp' || n.kind === 'Ingress') {
      reasons.set(n.id, 'Client application or API ingress entrypoint (Orbit 0)');
    } else if (n.kind === 'Topic' || n.kind === 'Queue' || n.kind === 'Broker' || n.kind === 'EventBus') {
      reasons.set(n.id, 'Asynchronous message broker topic/queue bridging echelons (Orbit 2)');
    } else if (n.kind === 'Service') {
      const t = serviceTiers.get(n.id) ?? 2;
      const callerId = viaCaller.get(n.id);
      const callerName = callerId ? nameMap.get(callerId) || callerId : undefined;
      if (t <= 1) {
        if (callerName) {
          reasons.set(n.id, `Gateway-facing service called directly by ${callerName}`);
        } else {
          const outCount = serviceAdj.get(n.id)?.size || 0;
          reasons.set(n.id, `Top-level orchestrator service (0 inbound calls, ${outCount} downstream calls)`);
        }
      } else {
        if (callerName) {
          reasons.set(n.id, `Internal domain service (hop depth ${t} via ${callerName})`);
        } else {
          reasons.set(n.id, 'Internal domain service with no direct ingress path');
        }
      }
    } else if (n.kind === 'Worker') {
      reasons.set(n.id, 'Background worker / consumer placed in internal domain echelon (Orbit 3)');
    } else if (n.kind === 'CliTool' || n.kind === 'TestSuite') {
      reasons.set(n.id, `${n.kind === 'CliTool' ? 'CLI utility' : 'Test suite'} placed in internal execution echelon (Orbit 3)`);
    } else if (n.kind === 'Database' || n.kind === 'Table' || n.kind === 'Procedure') {
      reasons.set(n.id, 'Persistence data store / schema resource on outer infrastructure ring (Orbit 4)');
    } else if (n.kind === 'ExternalService' || n.kind === 'CloudService') {
      reasons.set(n.id, 'External third-party or cloud infrastructure dependency (Orbit 4)');
    } else if (n.kind === 'Library' || n.kind === 'SharedLibrary') {
      reasons.set(n.id, 'Shared code library referenced across services (Orbit 4)');
    } else {
      reasons.set(n.id, `Classified as ${n.kind} in outer orbit`);
    }
  }

  return reasons;
}

/**
 * Optimizes the assignment of `nodeIds` onto a fixed set of `slotAngles` on an orbit of radius `orbitRadius`
 * to strictly minimize total weighted Euclidean link length (both inter-orbit links to already-placed nodes
 * and intra-orbit links between nodes on the same ring), while preserving the exact slot angles (equispacing).
 */
export function optimizeSlotAssignment(
  nodeIds: string[],
  slotAngles: number[],
  orbitRadius: number,
  nodeAngles: Map<string, number>,
  nodeRadii: Map<string, number>,
  graphAdj: Map<string, Array<{ neighborId: string; weight: number }>>,
  preserveInitialOrder = false
): void {
  const N = nodeIds.length;
  if (N === 0 || slotAngles.length === 0) return;
  if (N === 1) {
    nodeAngles.set(nodeIds[0], slotAngles[0]);
    nodeRadii.set(nodeIds[0], orbitRadius);
    return;
  }

  const slotSet = new Set(nodeIds);
  const posOfSlot = slotAngles.map((ang) => ({
    x: orbitRadius * Math.cos(ang),
    y: orbitRadius * Math.sin(ang),
  }));

  const extNeighbors = new Map<string, Array<{ x: number; y: number; weight: number }>>();
  const intEdges: Array<{ u: string; v: string; weight: number }> = [];

  for (let i = 0; i < N; i++) {
    const u = nodeIds[i];
    const ext: Array<{ x: number; y: number; weight: number }> = [];
    for (const { neighborId: v, weight } of graphAdj.get(u) || []) {
      if (slotSet.has(v)) {
        if (u < v) intEdges.push({ u, v, weight });
      } else if (nodeAngles.has(v)) {
        const rV = nodeRadii.get(v) ?? orbitRadius;
        const aV = nodeAngles.get(v)!;
        if (rV > 0) {
          ext.push({ x: rV * Math.cos(aV), y: rV * Math.sin(aV), weight });
        }
      }
    }
    extNeighbors.set(u, ext);
  }

  const extCost = (u: string, s: number): number => {
    const p = posOfSlot[s];
    let cost = 0;
    for (const nbr of extNeighbors.get(u) || []) {
      cost += nbr.weight * Math.hypot(p.x - nbr.x, p.y - nbr.y);
    }
    return cost;
  };

  let assignment: string[];
  if (preserveInitialOrder) {
    assignment = [...nodeIds];
  } else {
    const connected: string[] = [];
    const disconnected: string[] = [];
    const ideal = new Map<string, number>();

    for (const u of nodeIds) {
      const nbrs = (graphAdj.get(u) || []).filter(
        (n) => !slotSet.has(n.neighborId) && nodeAngles.has(n.neighborId) && (nodeRadii.get(n.neighborId) ?? 1) > 0
      );
      if (nbrs.length > 0) {
        ideal.set(
          u,
          circularMean(nbrs.map((n) => ({ angle: nodeAngles.get(n.neighborId)!, weight: n.weight })))
        );
        connected.push(u);
      } else {
        disconnected.push(u);
      }
    }

    if (connected.length > 0 && disconnected.length > 0) {
      const meanConn = circularMean(connected.map((u) => ({ angle: ideal.get(u)!, weight: 1 })));
      const opp = normalizeAngle(meanConn + Math.PI);
      disconnected.forEach((u, i) => {
        ideal.set(u, normalizeAngle(opp + (i - (disconnected.length - 1) / 2) * 0.08));
      });
    } else {
      disconnected.forEach((u, i) => {
        ideal.set(u, slotAngles[i % slotAngles.length]);
      });
    }

    const startAng = slotAngles[0];
    const toUnrolled = (ang: number): number => {
      let rel = (ang - startAng) % (2 * Math.PI);
      if (rel < 0) rel += 2 * Math.PI;
      return rel;
    };

    assignment = [...nodeIds].sort((a, b) => toUnrolled(ideal.get(a) ?? 0) - toUnrolled(ideal.get(b) ?? 0));
  }

  const slotOfNode = new Map<string, number>();
  assignment.forEach((u, s) => slotOfNode.set(u, s));

  const totalCost = (): number => {
    let c = 0;
    for (let s = 0; s < N; s++) {
      c += extCost(assignment[s], s);
    }
    for (const e of intEdges) {
      const pU = posOfSlot[slotOfNode.get(e.u)!];
      const pV = posOfSlot[slotOfNode.get(e.v)!];
      c += e.weight * Math.hypot(pU.x - pV.x, pU.y - pV.y);
    }
    return c;
  };

  // Iterative 2-opt pairwise slot swap until local minimum of total link length is reached
  let improved = true;
  let curCost = totalCost();
  let iter = 0;
  while (improved && iter < 40) {
    improved = false;
    iter++;
    for (let i = 0; i < N; i++) {
      for (let j = i + 1; j < N; j++) {
        const u = assignment[i];
        const v = assignment[j];
        assignment[i] = v;
        assignment[j] = u;
        slotOfNode.set(v, i);
        slotOfNode.set(u, j);

        const newCost = totalCost();
        if (newCost < curCost - 1e-4) {
          curCost = newCost;
          improved = true;
        } else {
          assignment[i] = u;
          assignment[j] = v;
          slotOfNode.set(u, i);
          slotOfNode.set(v, j);
        }
      }
    }
  }

  for (let s = 0; s < N; s++) {
    nodeAngles.set(assignment[s], slotAngles[s]);
    nodeRadii.set(assignment[s], orbitRadius);
  }
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

  // Enforce strict circular wrap-around separation between the last node and the first node!
  // Without this, angles[N - 1] and angles[0] + 2*PI can sit right on top of each other when the badge gap is small.
  const wrapDelta = Math.max(0, delta - 2 * badgeHalfAngle);
  const maxSpan = availableSpan - wrapDelta;

  // Monotonic forward separation
  for (let i = 1; i < N; i++) {
    if (angles[i] < angles[i - 1] + delta) {
      angles[i] = angles[i - 1] + delta;
    }
  }

  const span = angles[N - 1] - angles[0];
  if (span > maxSpan || angles[N - 1] > availableSpan) {
    // If we have enough room in maxSpan, compress backward from the end while keeping >= delta between all adjacent pairs
    if ((N - 1) * delta <= maxSpan) {
      angles[N - 1] = Math.min(angles[N - 1], angles[0] + maxSpan, availableSpan);
      for (let i = N - 2; i >= 0; i--) {
        if (angles[i] > angles[i + 1] - delta) {
          angles[i] = angles[i + 1] - delta;
        }
      }
      if (angles[0] < 0) {
        const shiftRight = -angles[0];
        for (let i = 0; i < N; i++) angles[i] += shiftRight;
      }
    } else {
      const step = availableSpan / N;
      const start = (availableSpan - (N - 1) * step) / 2;
      for (let i = 0; i < N; i++) {
        angles[i] = start + i * step;
      }
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
 * Optimizes the permutation of concentric orbits to minimize total radial edge lengths
 * while anchoring Ingress at Orbit 0 (center) if present.
 * Uses exact exhaustive evaluation across intermediate tier permutations (k! <= 120, completes in < 0.1ms).
 */
export function optimizeOrbitPermutation(
  populatedTiers: number[],
  orbitBuckets: Record<number, string[]>,
  weightedEdges: Array<{ sourceTier: number; targetTier: number; weight: number }>,
  spacing = 1.0,
  orbitDistance?: number,
  minNodeDistance?: number
): number[] {
  if (populatedTiers.length <= 2) {
    return populatedTiers;
  }

  // Anchor Ingress (Tier 0) at center (Orbit 0), and Databases & External Services (Tier 4 / 5) at outer periphery
  const hasIngress = populatedTiers.includes(0);
  const fixedPrefix: number[] = hasIngress ? [0] : [];
  const outerTiers = populatedTiers.filter((t) => t === 4 || t === 5);
  const fixedSuffix: number[] = outerTiers.length > 0 ? outerTiers : [];
  const permutableTiers = populatedTiers.filter((t) => t !== 0 && t !== 4 && t !== 5);

  if (permutableTiers.length <= 1) {
    return [...fixedPrefix, ...permutableTiers, ...fixedSuffix];
  }

  function getPermutations<T>(arr: T[]): T[][] {
    if (arr.length <= 1) return [arr];
    const res: T[][] = [];
    for (let i = 0; i < arr.length; i++) {
      const current = arr[i];
      const remaining = arr.slice(0, i).concat(arr.slice(i + 1));
      for (const p of getPermutations(remaining)) {
        res.push([current, ...p]);
      }
    }
    return res;
  }

  const allPerms = getPermutations(permutableTiers);

  const minArcSpacing = Math.round((minNodeDistance ?? 125) * spacing);
  const radialStep = Math.round((orbitDistance ?? 320) * spacing);

  let bestPerm = [...fixedPrefix, ...permutableTiers, ...fixedSuffix];
  let bestCost = Infinity;

  for (const perm of allPerms) {
    const candidateOrder = [...fixedPrefix, ...perm, ...fixedSuffix];

    const tierRadii = new Map<number, number>();
    let prevRadius = 0;
    candidateOrder.forEach((tier, idx) => {
      const count = orbitBuckets[tier]?.length || 0;
      let r: number;
      if (idx === 0) {
        r = count <= 1 ? 0 : Math.max(Math.round(100 * spacing), (count * minArcSpacing) / (2 * Math.PI));
      } else {
        const minCircumRadius = (count * minArcSpacing) / (2 * Math.PI);
        r = Math.max(prevRadius + radialStep, minCircumRadius);
      }
      tierRadii.set(tier, r);
      prevRadius = r;
    });

    let cost = 0;
    for (const edge of weightedEdges) {
      const rS = tierRadii.get(edge.sourceTier) ?? 0;
      const rT = tierRadii.get(edge.targetTier) ?? 0;
      cost += edge.weight * Math.abs(rS - rT);
    }

    if (cost < bestCost) {
      bestCost = cost;
      bestPerm = candidateOrder;
    }
  }

  return bestPerm;
}

/**
 * Complete concentric layout pipeline:
 * - Multi-tier orbital echelon bucketing
 * - Optimal orbit permutation to minimize radial edge spans (anchoring Ingress at center)
 * - Collision-free radius calculation
 * - Multi-pass circular barycentric alignment to minimize chord lengths
 * - 12 o'clock badge collision avoidance
 */
export function computeConcentricLayout(
  visibleNodes: ConcentricNodeInput[],
  visibleEdges: ConcentricEdgeInput[],
  spacing = 1.0,
  customOrbitOrder?: number[],
  orbitDistance?: number,
  minNodeDistance?: number,
  customOrbitTitles?: Record<number, string>
): ConcentricLayoutResult {
  const orbitBuckets: Record<number, string[]> = {
    0: [],
    1: [],
    2: [],
    3: [],
    4: [],
    5: [],
    6: [],
  };

  const nodeToTier = new Map<string, number>();
  for (const node of visibleNodes) {
    const ech = Math.max(0, Math.min(6, node.echelonTier));
    nodeToTier.set(node.id, ech);
    orbitBuckets[ech].push(node.id);
  }

  const weightedEdges: Array<{ sourceTier: number; targetTier: number; weight: number }> = [];
  for (const e of visibleEdges) {
    const sTier = nodeToTier.get(e.source);
    const tTier = nodeToTier.get(e.target);
    if (sTier !== undefined && tTier !== undefined) {
      const cat = e.category || '';
      const count = e.count || 1;
      const weight = cat === 'service_call' ? 3 * count : cat === 'database' || cat === 'messaging' ? 2 * count : count;
      weightedEdges.push({ sourceTier: sTier, targetTier: tTier, weight });
    }
  }

  const populatedTiers = [0, 1, 2, 3, 4, 5, 6].filter((idx) => orbitBuckets[idx].length > 0);
  const outerTiers = populatedTiers.filter((t) => t === 4 || t === 5 || t === 6);
  const innerPopulated = populatedTiers.filter((t) => t !== 4 && t !== 5 && t !== 6);
  let optimalOrder: number[];
  if (customOrbitOrder && customOrbitOrder.length > 0) {
    const validCustom = customOrbitOrder.filter((t) => populatedTiers.includes(t));
    const missingInner = innerPopulated.filter((t) => !validCustom.includes(t));
    const missingOuter = outerTiers.filter((t) => !validCustom.includes(t));
    optimalOrder = [...validCustom, ...missingInner, ...missingOuter];
  } else {
    optimalOrder = optimizeOrbitPermutation(
      populatedTiers,
      orbitBuckets,
      weightedEdges,
      spacing,
      orbitDistance,
      minNodeDistance
    );
  }

  const populatedOrbits: Array<{
    levelIndex: number;
    label: string;
    shortLabel: string;
    title: string;
    nodeIds: string[];
    radius: number;
  }> = [];

  let displayIdx = 0;
  for (const idx of optimalOrder) {
    const title = customOrbitTitles?.[idx] || ORBIT_TITLES[idx] || `Tier ${idx}`;
    const shortLabel = `Orbit ${displayIdx}`;
    populatedOrbits.push({
      levelIndex: idx,
      label: `${shortLabel}: ${title}`,
      shortLabel,
      title,
      nodeIds: orbitBuckets[idx],
      radius: 0,
    });
    displayIdx++;
  }

  const minArcSpacing = Math.round((minNodeDistance ?? 125) * spacing);
  const radialStep = Math.round((orbitDistance ?? 320) * spacing);

  let prevRadius = 0;
  populatedOrbits.forEach((orbit, idx) => {
    const count = orbit.nodeIds.length;
    let r: number;
    if (idx === 0) {
      r = count <= 1 ? 0 : Math.max(Math.round(100 * spacing), (count * minArcSpacing) / (2 * Math.PI));
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
  const nodeRadii = new Map<string, number>();
  populatedOrbits.forEach((orbit) => {
    orbit.nodeIds.forEach((id) => nodeRadii.set(id, orbit.radius));
  });

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
    const slots = orderedFirst.map((_, i) => normalizeAngle(startAngle + i * step));

    optimizeSlotAssignment(orderedFirst, slots, firstOrbit.radius, nodeAngles, nodeRadii, graphAdj, true);
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
        const nbrs = (graphAdj.get(id) || []).filter(
          (n) => nodeAngles.has(n.neighborId) && (nodeRadii.get(n.neighborId) ?? 1) > 0
        );
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
      const badgeMax = -Math.PI / 2 + badgeHalf;
      const toUnrolled = (ang: number): number => {
        let rel = (ang - badgeMax) % (2 * Math.PI);
        if (rel < 0) rel += 2 * Math.PI;
        return rel;
      };
      const sortedIds = [...orbit.nodeIds].sort(
        (a, b) => toUnrolled(relaxed.get(a) ?? 0) - toUnrolled(relaxed.get(b) ?? 0)
      );
      const slotAngles = sortedIds.map((id) => relaxed.get(id) ?? 0);
      optimizeSlotAssignment(sortedIds, slotAngles, orbit.radius, nodeAngles, nodeRadii, graphAdj, true);
    }

    // Multi-pass iterative barycentric + 2-opt relaxation to globally minimize chord lengths
    for (let pass = 0; pass < 3; pass++) {
      // Inward sweep
      for (let idx = populatedOrbits.length - 2; idx >= firstNonZeroRingIdx; idx--) {
        const orbit = populatedOrbits[idx];
        const ideal = new Map<string, number>();
        orbit.nodeIds.forEach((id) => {
          const nbrs = (graphAdj.get(id) || []).filter(
            (n) => nodeAngles.has(n.neighborId) && (nodeRadii.get(n.neighborId) ?? 1) > 0
          );
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
        const badgeHalf = Math.max(Math.PI / 15, 70 / Math.max(1, orbit.radius));
        const badgeMax = -Math.PI / 2 + badgeHalf;
        const toUnrolled = (ang: number): number => {
          let rel = (ang - badgeMax) % (2 * Math.PI);
          if (rel < 0) rel += 2 * Math.PI;
          return rel;
        };
        const sortedIds = [...orbit.nodeIds].sort(
          (a, b) => toUnrolled(relaxed.get(a) ?? 0) - toUnrolled(relaxed.get(b) ?? 0)
        );
        const slotAngles = sortedIds.map((id) => relaxed.get(id) ?? 0);
        optimizeSlotAssignment(sortedIds, slotAngles, orbit.radius, nodeAngles, nodeRadii, graphAdj, true);
      }

      // Outward sweep
      for (let idx = firstNonZeroRingIdx + 1; idx < populatedOrbits.length; idx++) {
        const orbit = populatedOrbits[idx];
        const ideal = new Map<string, number>();
        orbit.nodeIds.forEach((id) => {
          const nbrs = (graphAdj.get(id) || []).filter(
            (n) => nodeAngles.has(n.neighborId) && (nodeRadii.get(n.neighborId) ?? 1) > 0
          );
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
        const badgeHalf = Math.max(Math.PI / 15, 70 / Math.max(1, orbit.radius));
        const badgeMax = -Math.PI / 2 + badgeHalf;
        const toUnrolled = (ang: number): number => {
          let rel = (ang - badgeMax) % (2 * Math.PI);
          if (rel < 0) rel += 2 * Math.PI;
          return rel;
        };
        const sortedIds = [...orbit.nodeIds].sort(
          (a, b) => toUnrolled(relaxed.get(a) ?? 0) - toUnrolled(relaxed.get(b) ?? 0)
        );
        const slotAngles = sortedIds.map((id) => relaxed.get(id) ?? 0);
        optimizeSlotAssignment(sortedIds, slotAngles, orbit.radius, nodeAngles, nodeRadii, graphAdj, true);
      }
    }
  }

  const positions = new Map<string, { x: number; y: number }>();

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
      shortLabel: o.shortLabel,
      title: o.title,
      levelIndex: o.levelIndex,
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
