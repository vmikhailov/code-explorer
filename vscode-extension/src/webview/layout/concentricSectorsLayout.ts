/**
 * Concentric Orbital Layout - Approach 3: Domain / Bounded Context Pie Sectors.
 * 
 * Geometry:
 * - 360° is divided into dynamic, proportional angular pie sectors for each domain.
 * - Each sector is allocated an angular span strictly proportional to its entity count and tier density,
 *   guaranteeing sufficient arc clearance (no node collisions or overlapping labels).
 * - Inside each domain sector, components reside on concentric tiers R_0 .. R_5.
 * - Databases, workers, and topics are placed in the sector of the domain they serve.
 * - Multi-domain / unlinked infrastructure is gathered into a dedicated Shared Infrastructure sector.
 * - Sector guides provide beautiful colored background wedges, radial boundary lines, and header badges.
 */

import {
  ConcentricNodeInput,
  ConcentricEdgeInput,
  ConcentricLayoutResult,
  ConcentricOrbitGuide,
  DomainSectorGuide,
  ORBIT_TITLES,
  cleanEntityToken,
  normalizeAngle,
} from './concentricLayout';

export const SECTOR_PALETTE = [
  '#38bdf8', // Sky Blue
  '#4ade80', // Emerald Green
  '#fb923c', // Orange
  '#c084fc', // Purple
  '#f43f5e', // Rose
  '#eab308', // Amber
  '#06b6d4', // Cyan
  '#a855f7', // Violet
  '#10b981', // Mint
  '#6366f1', // Indigo
  '#ec4899', // Pink
  '#14b8a6', // Teal
];

function extractDomainToken(name: string): string {
  const clean = cleanEntityToken(name);
  const parts = clean.split(/[-_.]/);
  if (parts.length >= 2) {
    if (parts[0].length <= 3 && parts.length > 2) {
      return `${parts[0]}-${parts[1]}`;
    }
    return parts[0];
  }
  return clean || 'core';
}

function formatDomainTitle(token: string): string {
  if (token === '__shared__' || token === '__shared_core__' || token === 'shared') {
    return 'Shared Infrastructure';
  }
  if (token === '__external__' || token === 'external') {
    return 'External Services';
  }
  return token
    .split(/[-_]/)
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ');
}

export function computeConcentricSectorsLayout(
  visibleNodes: ConcentricNodeInput[],
  visibleEdges: ConcentricEdgeInput[],
  spacing = 1.0,
  customOrbitOrder?: number[],
  explicitDomainMap?: Map<string, string>,
  explicitDomainDetails?: Map<string, { name: string; displayName: string }>
): ConcentricLayoutResult {
  const positions = new Map<string, { x: number; y: number }>();
  const nodeAngles = new Map<string, number>();
  const nodeRadii = new Map<string, number>();

  if (visibleNodes.length === 0) {
    return {
      positions,
      nodeAngles,
      nodeRadii,
      guides: [],
      populatedOrbits: [],
      sectors: [],
    };
  }

  // 1. Group nodes by echelon tier
  const orbitBuckets: Record<number, string[]> = { 0: [], 1: [], 2: [], 3: [], 4: [], 5: [] };
  const nodeMap = new Map<string, ConcentricNodeInput>();

  for (const node of visibleNodes) {
    const ech = Math.max(0, Math.min(5, node.echelonTier));
    orbitBuckets[ech].push(node.id);
    nodeMap.set(node.id, node);
  }

  const populatedTiers = [0, 1, 2, 3, 4, 5].filter((idx) => orbitBuckets[idx].length > 0);
  let effectiveOrder = populatedTiers;
  if (customOrbitOrder && customOrbitOrder.length > 0) {
    const validCustom = customOrbitOrder.filter((t) => populatedTiers.includes(t));
    const missing = populatedTiers.filter((t) => !validCustom.includes(t));
    effectiveOrder = [...validCustom, ...missing];
  }

  // 2. Classify each node into its Business Domain
  const nodeDomain = new Map<string, string>();
  const domainDisplayNames = new Map<string, string>();

  // 2a. Determine domains for primary service nodes
  for (const n of visibleNodes) {
    let domain: string | undefined;
    if (explicitDomainMap?.has(n.id)) {
      domain = explicitDomainMap.get(n.id);
    } else if (n.domain) {
      domain = n.domain;
    } else if (n.echelonTier <= 3 || n.kind === 'Service' || n.kind === 'Ingress' || n.kind === 'Worker') {
      domain = extractDomainToken(n.displayName || n.name || n.id);
    }
    if (domain) {
      nodeDomain.set(n.id, domain);
      if (!domainDisplayNames.has(domain)) {
        const explicitName = explicitDomainDetails?.get(domain)?.displayName;
        domainDisplayNames.set(domain, explicitName || formatDomainTitle(domain));
      }
    }
  }

  // 2b. Map infrastructure nodes (Databases, Topics, External) to their connected domain
  const adj = new Map<string, Array<{ neighborId: string; weight: number }>>();
  for (const e of visibleEdges) {
    if (!e.source || !e.target) continue;
    const w = e.count || 1;
    if (!adj.has(e.source)) adj.set(e.source, []);
    adj.get(e.source)!.push({ neighborId: e.target, weight: w });
    if (!adj.has(e.target)) adj.set(e.target, []);
    adj.get(e.target)!.push({ neighborId: e.source, weight: w });
  }

  for (const n of visibleNodes) {
    if (nodeDomain.has(n.id)) continue;

    // Infrastructure node: inspect connected services
    const neighbors = adj.get(n.id) || [];
    const connectedDomains = new Map<string, number>();

    for (const { neighborId, weight } of neighbors) {
      const d = nodeDomain.get(neighborId);
      if (d && d !== '__shared__') {
        connectedDomains.set(d, (connectedDomains.get(d) || 0) + weight);
      }
    }

    if (connectedDomains.size === 1) {
      // Exclusively used by a single domain -> Dedicated component in that domain's sector!
      const dedicatedDomain = connectedDomains.keys().next().value!;
      nodeDomain.set(n.id, dedicatedDomain);
    } else if (connectedDomains.size > 1) {
      // Shared across multiple domains -> Belongs to Shared Core Infrastructure
      nodeDomain.set(n.id, '__shared__');
    } else {
      // Isolated or unmapped infrastructure
      const fallbackDomain = extractDomainToken(n.displayName || n.name || n.id);
      nodeDomain.set(n.id, fallbackDomain || '__shared__');
    }
  }

  // Ensure shared domain label exists if populated
  if (!domainDisplayNames.has('__shared__')) {
    domainDisplayNames.set('__shared__', 'Shared Infrastructure');
  }

  // 3. Group nodes by Domain
  const domainNodeMap = new Map<string, string[]>();
  for (const n of visibleNodes) {
    const d = nodeDomain.get(n.id) || '__shared__';
    if (!domainNodeMap.has(d)) domainNodeMap.set(d, []);
    domainNodeMap.get(d)!.push(n.id);
  }

  // Separate shared domain to place at the end or bottom
  const regularDomains = Array.from(domainNodeMap.entries())
    .filter(([d]) => d !== '__shared__')
    .sort((a, b) => b[1].length - a[1].length);

  const sortedDomainEntries: Array<[string, string[]]> = [...regularDomains];
  if (domainNodeMap.has('__shared__')) {
    sortedDomainEntries.push(['__shared__', domainNodeMap.get('__shared__')!]);
  }

  // 4. Calculate required angular span for each domain to GUARANTEE zero overlaps
  // Arc parameters
  const MIN_ARC_GAP = Math.round(155 * spacing); // clearance between adjacent node centers on the same tier
  const MARGIN_ARC = Math.round(60 * spacing);   // padding from sector boundary
  const RADIAL_STEP = Math.round(220 * spacing);  // distance between concentric echelon orbits
  const BASE_INNER_R = Math.round(170 * spacing);

  // Initial radii for populated orbits
  const orbitRadii = new Map<number, number>();
  effectiveOrder.forEach((tierIdx, rank) => {
    orbitRadii.set(tierIdx, BASE_INNER_R + rank * RADIAL_STEP);
  });

  // Calculate minimum angle required by each domain based on its most crowded tier
  const domainWeights = new Map<string, number>();
  const domainMinAngles = new Map<string, number>();

  for (const [domain, nodeIds] of sortedDomainEntries) {
    // Count nodes per tier in this domain
    const tierCounts = new Map<number, number>();
    for (const id of nodeIds) {
      const ech = nodeMap.get(id)?.echelonTier ?? 2;
      tierCounts.set(ech, (tierCounts.get(ech) || 0) + 1);
    }

    let maxRequiredAngleForDomain = Math.PI / 16; // minimum 11.25 degrees
    let maxTierCount = 1;

    for (const [ech, count] of tierCounts.entries()) {
      if (count > maxTierCount) maxTierCount = count;
      const r = orbitRadii.get(ech) || (BASE_INNER_R + RADIAL_STEP);
      const neededArc = count > 1 ? (count - 1) * MIN_ARC_GAP + 2 * MARGIN_ARC : 2 * MARGIN_ARC;
      const neededAngle = neededArc / r;
      if (neededAngle > maxRequiredAngleForDomain) {
        maxRequiredAngleForDomain = neededAngle;
      }
    }

    domainMinAngles.set(domain, maxRequiredAngleForDomain);
    domainWeights.set(domain, Math.max(nodeIds.length, maxTierCount * 1.4));
  }

  // 5. Angular budget allocation
  const numDomains = sortedDomainEntries.length;
  const SECTOR_GAP = numDomains > 1 ? Math.min(Math.PI / 36, (2 * Math.PI) / (numDomains * 8)) : 0; // ~5° gap between sectors
  const availableBudget = 2 * Math.PI - numDomains * SECTOR_GAP;

  let totalMinAngle = 0;
  for (const minAng of domainMinAngles.values()) {
    totalMinAngle += minAng;
  }

  // If min required angles exceed available budget, expand the concentric radii proportionally!
  let radiusMultiplier = 1.0;
  if (totalMinAngle > availableBudget) {
    radiusMultiplier = (totalMinAngle / availableBudget) * 1.06;
    for (const tierIdx of effectiveOrder) {
      const currentR = orbitRadii.get(tierIdx) || BASE_INNER_R;
      orbitRadii.set(tierIdx, Math.round(currentR * radiusMultiplier));
    }

    // Recalculate min angles with expanded radii
    totalMinAngle = 0;
    for (const [domain, nodeIds] of sortedDomainEntries) {
      const tierCounts = new Map<number, number>();
      for (const id of nodeIds) {
        const ech = nodeMap.get(id)?.echelonTier ?? 2;
        tierCounts.set(ech, (tierCounts.get(ech) || 0) + 1);
      }
      let maxAng = Math.PI / 20;
      for (const [ech, count] of tierCounts.entries()) {
        const r = orbitRadii.get(ech)!;
        const neededArc = count > 1 ? (count - 1) * MIN_ARC_GAP + 2 * MARGIN_ARC : 2 * MARGIN_ARC;
        const ang = neededArc / r;
        if (ang > maxAng) maxAng = ang;
      }
      domainMinAngles.set(domain, maxAng);
      totalMinAngle += maxAng;
    }
  }

  // Distribute any extra angular budget proportionally according to domain weights
  const remainingBudget = Math.max(0, availableBudget - totalMinAngle);
  let totalWeight = 0;
  for (const w of domainWeights.values()) totalWeight += w;
  if (totalWeight === 0) totalWeight = 1;

  const domainAngles = new Map<string, { start: number; end: number; center: number; span: number }>();
  let currentStartAngle = -Math.PI / 2; // start at 12 o'clock

  for (const [domain] of sortedDomainEntries) {
    const minAng = domainMinAngles.get(domain) || (Math.PI / 12);
    const weight = domainWeights.get(domain) || 1;
    const extra = (weight / totalWeight) * remainingBudget;
    const span = minAng + extra;
    const start = currentStartAngle;
    const end = start + span;
    const center = start + span / 2;

    domainAngles.set(domain, { start, end, center, span });
    currentStartAngle = end + SECTOR_GAP;
  }

  // 6. Place nodes inside each domain sector on their respective echelon tiers
  for (const [domain, nodeIds] of sortedDomainEntries) {
    const slice = domainAngles.get(domain)!;

    // Group this domain's nodes by tier
    const nodesByTier = new Map<number, string[]>();
    for (const id of nodeIds) {
      const ech = nodeMap.get(id)?.echelonTier ?? 2;
      if (!nodesByTier.has(ech)) nodesByTier.set(ech, []);
      nodesByTier.get(ech)!.push(id);
    }

    for (const [ech, ids] of nodesByTier.entries()) {
      const r = orbitRadii.get(ech) || BASE_INNER_R;
      const count = ids.length;

      if (count === 1) {
        const ang = slice.center;
        nodeAngles.set(ids[0], ang);
        nodeRadii.set(ids[0], r);
        positions.set(ids[0], {
          x: Math.round(r * Math.cos(ang)),
          y: Math.round(r * Math.sin(ang)),
        });
      } else {
        const marginAngle = Math.min(slice.span * 0.2, MARGIN_ARC / r);
        const usableSpan = Math.max(slice.span - 2 * marginAngle, 0.04);
        const step = usableSpan / (count - 1);

        ids.forEach((id, idx) => {
          const ang = slice.start + marginAngle + idx * step;
          // Apply slight radius stagger for dense tiers (3+ nodes) to enhance visual clarity
          const stagger = count >= 3 ? (idx % 2 === 1 ? 26 * spacing : -26 * spacing) : 0;
          const nodeR = r + stagger;

          nodeAngles.set(id, ang);
          nodeRadii.set(id, nodeR);
          positions.set(id, {
            x: Math.round(nodeR * Math.cos(ang)),
            y: Math.round(nodeR * Math.sin(ang)),
          });
        });
      }
    }
  }

  // 7. Generate concentric orbit guides (rings)
  const populatedOrbits = effectiveOrder.map((tierIdx, rank) => {
    const r = orbitRadii.get(tierIdx) || BASE_INNER_R;
    const title = ORBIT_TITLES[tierIdx] || `Tier ${tierIdx}`;
    const shortLabel = `Orbit ${rank}`;
    return {
      levelIndex: tierIdx,
      label: `${shortLabel}: ${title}`,
      shortLabel,
      title,
      nodeIds: orbitBuckets[tierIdx],
      radius: r,
    };
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

  // 8. Generate Domain Sector Guides (Annular Wedges, Divider Lines, and Badges)
  const maxRadius = Math.max(...Array.from(orbitRadii.values())) + Math.round(75 * spacing);
  const minRadius = Math.max(50, Math.min(...Array.from(orbitRadii.values())) - Math.round(55 * spacing));

  const sectors: DomainSectorGuide[] = sortedDomainEntries.map(([domain, nodeIds], idx) => {
    const slice = domainAngles.get(domain)!;
    const isShared = domain === '__shared__';
    const color = isShared ? '#c084fc' : SECTOR_PALETTE[idx % SECTOR_PALETTE.length];
    const dispName = domainDisplayNames.get(domain) || formatDomainTitle(domain);

    return {
      id: domain,
      domainName: domain,
      displayName: dispName,
      startAngle: slice.start,
      endAngle: slice.end,
      centerAngle: slice.center,
      innerRadius: minRadius,
      outerRadius: maxRadius,
      color,
      count: nodeIds.length,
      nodeIds,
      isShared,
    };
  });

  return {
    positions,
    nodeAngles,
    nodeRadii,
    guides,
    populatedOrbits,
    sectors,
  };
}

export function describeAnnularSector(
  cx: number,
  cy: number,
  rIn: number,
  rOut: number,
  startAngle: number,
  endAngle: number
): string {
  const x1 = cx + rOut * Math.cos(startAngle);
  const y1 = cy + rOut * Math.sin(startAngle);
  const x2 = cx + rOut * Math.cos(endAngle);
  const y2 = cy + rOut * Math.sin(endAngle);

  const x3 = cx + rIn * Math.cos(endAngle);
  const y3 = cy + rIn * Math.sin(endAngle);
  const x4 = cx + rIn * Math.cos(startAngle);
  const y4 = cy + rIn * Math.sin(startAngle);

  const angleSpan = endAngle - startAngle;
  const largeArcFlag = angleSpan > Math.PI ? 1 : 0;

  if (rIn <= 0) {
    return `M ${cx} ${cy} L ${x1} ${y1} A ${rOut} ${rOut} 0 ${largeArcFlag} 1 ${x2} ${y2} Z`;
  }

  return `M ${x1} ${y1} A ${rOut} ${rOut} 0 ${largeArcFlag} 1 ${x2} ${y2} L ${x3} ${y3} A ${rIn} ${rIn} 0 ${largeArcFlag} 0 ${x4} ${y4} Z`;
}

