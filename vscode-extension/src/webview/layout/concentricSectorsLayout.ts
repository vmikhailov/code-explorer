/**
 * Concentric Orbital Layout - Approach 3: Domain / Bounded Context Pie Sectors.
 * 
 * Geometry:
 * - 360° is divided into angular pie sectors by business domain / subsystem.
 * - Each sector is allocated an angular span proportional to the size of that domain.
 * - Inside each domain sector, components reside on concentric tiers R_0 .. R_5.
 * - Databases, workers, and topics are placed in the sector of the domain they serve.
 * - Intra-domain flows run cleanly RADIALLY from center to perimeter!
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

export function computeConcentricSectorsLayout(
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

  // Extract domain key for each node
  const nodeDomain = new Map<string, string>();
  for (const n of visibleNodes) {
    const tok = cleanEntityToken(n.id);
    // Take the primary prefix or root domain name
    const parts = tok.split(/[-_.]/);
    const domain = parts[0] || 'core';
    nodeDomain.set(n.id, domain);
  }

  // Refine infrastructure nodes (Topics, DBs) to match their connected service's domain
  const serviceAdj = new Map<string, Array<{ serviceId: string; weight: number }>>();
  for (const e of visibleEdges) {
    if (!e.source || !e.target) continue;
    const sEch = visibleNodes.find((n) => n.id === e.source)?.echelonTier ?? 2;
    const tEch = visibleNodes.find((n) => n.id === e.target)?.echelonTier ?? 2;

    if (sEch <= 3 && tEch >= 4) {
      // Service -> Infra
      if (!serviceAdj.has(e.target)) serviceAdj.set(e.target, []);
      serviceAdj.get(e.target)!.push({ serviceId: e.source, weight: e.count || 1 });
    } else if (tEch <= 3 && sEch >= 4) {
      // Infra -> Service
      if (!serviceAdj.has(e.source)) serviceAdj.set(e.source, []);
      serviceAdj.get(e.source)!.push({ serviceId: e.target, weight: e.count || 1 });
    }
  }

  for (const [infraId, svcs] of serviceAdj.entries()) {
    if (svcs.length > 0) {
      // Pick domain of the most strongly connected service
      const topSvc = svcs.sort((a, b) => b.weight - a.weight)[0];
      const svcDomain = nodeDomain.get(topSvc.serviceId);
      if (svcDomain) {
        nodeDomain.set(infraId, svcDomain);
      }
    }
  }

  // Group nodes by domain
  const domains = new Map<string, string[]>();
  for (const n of visibleNodes) {
    const d = nodeDomain.get(n.id) || 'shared';
    if (!domains.has(d)) domains.set(d, []);
    domains.get(d)!.push(n.id);
  }

  // Sort domains by size
  const sortedDomains = Array.from(domains.entries()).sort((a, b) => b[1].length - a[1].length);
  const totalNodes = visibleNodes.length || 1;

  // Allocate angular slice for each domain
  const badgeHalf = Math.max(Math.PI / 15, 70 / Math.max(1, populatedOrbits[1]?.radius || 300));
  const availableSpan = 2 * Math.PI - 2 * badgeHalf;
  let currAngle = -Math.PI / 2 + badgeHalf;

  const domainSlices = new Map<string, { start: number; end: number; center: number }>();
  for (const [domain, dNodes] of sortedDomains) {
    const fraction = dNodes.length / totalNodes;
    // Guarantee minimum slice angle of 15°
    const span = Math.max(Math.PI / 12, fraction * availableSpan);
    const start = currAngle;
    const end = currAngle + span;
    domainSlices.set(domain, { start, end, center: start + span / 2 });
    currAngle += span;
  }

  // Place nodes on their orbit within their domain slice
  const nodeAngles = new Map<string, number>();

  for (const orbit of populatedOrbits) {
    const r = orbit.radius;
    if (r === 0) {
      orbit.nodeIds.forEach((id) => nodeAngles.set(id, 0));
      continue;
    }

    // Group orbit nodes by domain
    const orbitByDomain = new Map<string, string[]>();
    for (const id of orbit.nodeIds) {
      const d = nodeDomain.get(id) || 'shared';
      if (!orbitByDomain.has(d)) orbitByDomain.set(d, []);
      orbitByDomain.get(d)!.push(id);
    }

    for (const [domain, ids] of orbitByDomain.entries()) {
      const slice = domainSlices.get(domain) || {
        start: -Math.PI / 4,
        end: Math.PI / 4,
        center: 0,
      };
      const N = ids.length;
      if (N === 1) {
        nodeAngles.set(ids[0], normalizeAngle(slice.center));
      } else {
        const step = (slice.end - slice.start) / (N + 1);
        ids.forEach((id, i) => {
          nodeAngles.set(id, normalizeAngle(slice.start + (i + 1) * step));
        });
      }
    }
  }

  // Cartesian positions
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
