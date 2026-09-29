/**
 * Concentric Orbital Layout - Approach 1: Polar Constrained Force Simulation.
 * 
 * Physics:
 * - Rigid radius: r = R_k is strictly fixed for every orbit k.
 * - Angular springs: Edges exert rotational torque pulling connected nodes towards each other.
 * - 1D Angular Repulsion: Nodes on the same ring repel each other along the circumference (Coulomb force).
 * - Badge repulsion barrier: Strong repulsive potential around 12 o'clock (-90°).
 * - Damped velocity integration over 80 iterations.
 */

import {
  ConcentricNodeInput,
  ConcentricEdgeInput,
  ConcentricLayoutResult,
  ConcentricOrbitGuide,
  ORBIT_TITLES,
  ORBIT_NAMES,
  cleanEntityToken,
  normalizeAngle,
} from './concentricLayout';

export function computeConcentricPolarForceLayout(
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

  const nodeOrbit = new Map<string, number>();
  const nodeAngles = new Map<string, number>();
  const nodeVelocities = new Map<string, number>();

  // Initial angle placement: uniformly around each circle, avoiding badge
  populatedOrbits.forEach((orbit) => {
    const N = orbit.nodeIds.length;
    const r = orbit.radius;
    if (N === 0) return;
    if (r === 0) {
      orbit.nodeIds.forEach((id) => {
        nodeOrbit.set(id, 0);
        nodeAngles.set(id, 0);
        nodeVelocities.set(id, 0);
      });
      return;
    }

    const badgeHalf = Math.max(Math.PI / 15, 70 / Math.max(1, r));
    const available = 2 * Math.PI - 2 * badgeHalf;
    const step = available / N;
    const startAngle = -Math.PI / 2 + badgeHalf + step / 2;

    orbit.nodeIds.forEach((id, i) => {
      nodeOrbit.set(id, orbit.levelIndex);
      nodeAngles.set(id, normalizeAngle(startAngle + i * step));
      nodeVelocities.set(id, 0);
    });
  });

  // Graph adjacency with weights
  const weightedEdges: Array<{ source: string; target: string; weight: number }> = [];
  for (const e of visibleEdges) {
    if (!nodeAngles.has(e.source) || !nodeAngles.has(e.target)) continue;
    const cat = e.category || '';
    const count = e.count || 1;
    const sTok = cleanEntityToken(e.source);
    const tTok = cleanEntityToken(e.target);
    let bonus = 0;
    if (sTok && tTok && (sTok === tTok || sTok.includes(tTok) || tTok.includes(sTok))) {
      bonus = 25;
    }
    const weight =
      (cat === 'service_call' ? 3 * count : cat === 'database' || cat === 'messaging' ? 2 * count : count) + bonus;

    weightedEdges.push({ source: e.source, target: e.target, weight: Math.min(100, weight) });
  }

  // --- POLAR FORCE SIMULATION ---
  const iterations = 90;
  const dt = 0.08;
  const damping = 0.82;
  const kSpring = 0.015;

  for (let iter = 0; iter < iterations; iter++) {
    const torques = new Map<string, number>();
    for (const id of nodeAngles.keys()) {
      torques.set(id, 0);
    }

    // 1. Angular Springs along edges
    for (const edge of weightedEdges) {
      const a1 = nodeAngles.get(edge.source)!;
      const a2 = nodeAngles.get(edge.target)!;
      const diff = normalizeAngle(a2 - a1);
      const torque = kSpring * edge.weight * diff;

      torques.set(edge.source, torques.get(edge.source)! + torque);
      torques.set(edge.target, torques.get(edge.target)! - torque);
    }

    // 2. Angular 1D Repulsion between nodes on the SAME ring
    populatedOrbits.forEach((orbit) => {
      if (orbit.radius === 0 || orbit.nodeIds.length < 2) return;
      const ids = orbit.nodeIds;
      const N = ids.length;
      // Desired angular separation
      const idealSep = (2 * Math.PI) / N;
      const kRep = 0.08 * (idealSep * idealSep);

      for (let i = 0; i < N; i++) {
        const idA = ids[i];
        const angA = nodeAngles.get(idA)!;

        for (let j = i + 1; j < N; j++) {
          const idB = ids[j];
          const angB = nodeAngles.get(idB)!;
          const diff = normalizeAngle(angA - angB);
          const absDiff = Math.abs(diff);

          if (absDiff < idealSep * 1.8 && absDiff > 0.001) {
            // Repulsive force inversely proportional to angular distance squared
            const repForce = Math.min(1.5, kRep / (diff * diff + 0.01));
            const signedForce = diff > 0 ? repForce : -repForce;

            torques.set(idA, torques.get(idA)! + signedForce);
            torques.set(idB, torques.get(idB)! - signedForce);
          }
        }
      }
    });

    // 3. 12 O'Clock Badge Repulsion Barrier
    populatedOrbits.forEach((orbit) => {
      if (orbit.radius === 0) return;
      const badgeCenter = -Math.PI / 2;
      const badgeHalf = Math.max(Math.PI / 15, 70 / Math.max(1, orbit.radius));

      orbit.nodeIds.forEach((id) => {
        const ang = nodeAngles.get(id)!;
        const diffFromBadge = normalizeAngle(ang - badgeCenter);
        if (Math.abs(diffFromBadge) < badgeHalf * 1.5) {
          const push = diffFromBadge >= 0 ? 0.35 : -0.35;
          torques.set(id, torques.get(id)! + push);
        }
      });
    });

    // 4. Euler Integration with Damping
    for (const [id, torque] of torques.entries()) {
      if (nodeOrbit.get(id) === 0 && populatedOrbits[0]?.radius === 0) continue;
      let vel = (nodeVelocities.get(id)! + torque * dt) * damping;
      // Cap maximum angular velocity per tick to prevent instability
      vel = Math.max(-0.25, Math.min(0.25, vel));
      nodeVelocities.set(id, vel);

      let ang = normalizeAngle(nodeAngles.get(id)! + vel * dt);
      nodeAngles.set(id, ang);
    }
  }

  // Final Cartesian projection onto exact rigid concentric circles
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
