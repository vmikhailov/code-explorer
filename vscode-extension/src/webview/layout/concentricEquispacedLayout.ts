/**
 * Concentric Orbital Layout - Approach 2: Spectral / Barycentric Ordering with Strict Equispacing.
 * 
 * Guarantees:
 * - Perfectly even angular spacing around each orbit (no clumps, no empty half-circles).
 * - Maintains topological barycentric ordering relative to connected services.
 * - Respects the 12 o'clock badge exclusion zone.
 */

import {
  ConcentricNodeInput,
  ConcentricEdgeInput,
  ConcentricLayoutResult,
  ConcentricOrbitGuide,
  ORBIT_TITLES,
  ORBIT_NAMES,
  cleanEntityToken,
  circularMean,
  normalizeAngle,
} from './concentricLayout';

export function computeConcentricEquispacedLayout(
  visibleNodes: ConcentricNodeInput[],
  visibleEdges: ConcentricEdgeInput[],
  spacing = 1.0
): ConcentricLayoutResult {
  const orbitBuckets: Record<number, string[]> = { 0: [], 1: [], 2: [], 3: [], 4: [], 5: [] };

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

  let displayIdx = 0;
  for (const idx of [0, 1, 2, 3, 4, 5]) {
    if (orbitBuckets[idx].length > 0) {
      const title = ORBIT_TITLES[idx] || `Tier ${idx}`;
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

  // Graph adjacency with token match bonus
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

  let firstNonZeroRingIdx = -1;
  for (let idx = 0; idx < populatedOrbits.length; idx++) {
    if (populatedOrbits[idx].radius > 0) {
      firstNonZeroRingIdx = idx;
      break;
    }
  }

  // Helper to place N nodes with strictly equal spacing avoiding 12 o'clock badge
  const placeEquispaced = (nodeIdsSorted: string[], radius: number) => {
    const N = nodeIdsSorted.length;
    if (N === 0) return;
    if (N === 1) {
      nodeAngles.set(nodeIdsSorted[0], Math.PI / 2); // 6 o'clock
      return;
    }

    const badgeHalf = Math.max(Math.PI / 15, 70 / Math.max(1, radius));
    const available = 2 * Math.PI - 2 * badgeHalf;
    const step = available / N;
    const startAngle = -Math.PI / 2 + badgeHalf + step / 2;

    nodeIdsSorted.forEach((id, i) => {
      nodeAngles.set(id, normalizeAngle(startAngle + i * step));
    });
  };

  // 1. Seed innermost non-zero ring
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

    placeEquispaced(orderedFirst, firstOrbit.radius);
  }

  // Center ring (R = 0)
  if (populatedOrbits.length > 0 && populatedOrbits[0].radius === 0) {
    for (const id of populatedOrbits[0].nodeIds) {
      nodeAngles.set(id, 0.0);
    }
  }

  // 2. Outward sweep: Compute target barycenter angles, sort, and apply strict equispacing
  if (firstNonZeroRingIdx !== -1) {
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
          // Disconnected node: place at neutral angle
          ideal.set(id, 0.0);
        }
      });

      const badgeHalf = Math.max(Math.PI / 15, 70 / Math.max(1, orbit.radius));
      const badgeMax = -Math.PI / 2 + badgeHalf;

      // Unroll angles clockwise starting right after the badge
      const toUnrolled = (ang: number): number => {
        let rel = (ang - badgeMax) % (2 * Math.PI);
        if (rel < 0) rel += 2 * Math.PI;
        return rel;
      };

      const sorted = [...orbit.nodeIds].sort((a, b) => {
        return toUnrolled(ideal.get(a) ?? 0) - toUnrolled(ideal.get(b) ?? 0);
      });

      placeEquispaced(sorted, orbit.radius);
    }
  }

  // Calculate final Cartesian positions
  const positions = new Map<string, { x: number; y: number }>();
  const nodeRadii = new Map<string, number>();

  populatedOrbits.forEach((orbit) => {
    orbit.nodeIds.forEach((id) => {
      const ang = nodeAngles.get(id) ?? 0.0;
      const r = orbit.radius;
      nodeRadii.set(id, r);
      positions.set(id, {
        x: Math.round(r * Math.cos(ang)),
        y: Math.round(r * Math.sin(ang)),
      });
    });
  });

  const guides: ConcentricOrbitGuide[] = populatedOrbits
    .filter((orbit) => orbit.radius > 0)
    .map((orbit) => ({
      radius: orbit.radius,
      label: orbit.label,
      shortLabel: orbit.shortLabel,
      title: orbit.title,
      levelIndex: orbit.levelIndex,
      count: orbit.nodeIds.length,
    }));

  return {
    positions,
    nodeAngles,
    nodeRadii,
    guides,
    populatedOrbits,
  };
}
