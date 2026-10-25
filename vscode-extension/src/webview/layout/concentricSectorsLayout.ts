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
  optimizeSlotAssignment,
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
  return token;
}

export function computeConcentricSectorsLayout(
  visibleNodes: ConcentricNodeInput[],
  visibleEdges: ConcentricEdgeInput[],
  spacing = 1.0,
  customOrbitOrder?: number[],
  explicitDomainMap?: Map<string, string>,
  explicitDomainDetails?: Map<string, { name?: string; displayName?: string; color?: string }>,
  orbitDistance?: number,
  minNodeDistance?: number
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
  const outerTiers = populatedTiers.filter((t) => t === 4 || t === 5);
  const innerPopulated = populatedTiers.filter((t) => t !== 4 && t !== 5);
  let effectiveOrder = [...innerPopulated, ...outerTiers];
  if (customOrbitOrder && customOrbitOrder.length > 0) {
    const validCustom = customOrbitOrder.filter((t) => populatedTiers.includes(t));
    const missingInner = innerPopulated.filter((t) => !validCustom.includes(t));
    const missingOuter = outerTiers.filter((t) => !validCustom.includes(t));
    effectiveOrder = [...validCustom, ...missingInner, ...missingOuter];
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
    } else if (
      n.kind !== 'Database' &&
      n.kind !== 'Topic' &&
      n.kind !== 'ExternalService' &&
      (n.echelonTier <= 3 ||
        n.kind === 'Service' ||
        n.kind === 'Ingress' ||
        n.kind === 'App' ||
        n.kind === 'FrontendApp' ||
        n.kind === 'Worker' ||
        n.kind === 'TestSuite' ||
        n.kind === 'CliTool')
    ) {
      domain = extractDomainToken(n.displayName || n.name || n.id);
    }
    if (domain) {
      nodeDomain.set(n.id, domain);
      if (!domainDisplayNames.has(domain)) {
        const explicitName = explicitDomainDetails?.get(domain)?.displayName || (n as any).domainDisplayName;
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
      const fallbackDomain = explicitDomainMap && explicitDomainMap.size > 0
        ? '__shared__'
        : extractDomainToken(n.displayName || n.name || n.id);
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

  // Identify primary UI / Entry Point domain to take the center (if it has Tier 0 / App / Ingress / UI nodes)
  let centerDomain: string | null = null;
  let bestCenterScore = 0;

  for (const [d, ids] of domainNodeMap.entries()) {
    if (d === '__shared__') continue;
    let uiEntryCount = 0;
    let uiEntryDegree = 0;
    for (const id of ids) {
      const n = nodeMap.get(id);
      if (!n) continue;
      const isUiOrEntry =
        n.echelonTier === 0 ||
        n.kind === 'App' ||
        n.kind === 'FrontendApp' ||
        n.kind === 'Ingress';
      if (isUiOrEntry) {
        uiEntryCount++;
        const deg = (adj.get(id) || []).reduce((sum, edge) => sum + edge.weight, 0);
        uiEntryDegree += deg;
      }
    }
    const isPlatformUiName =
      d.includes('platform') ||
      d.includes('ui') ||
      d.includes('frontend') ||
      d.includes('ingress') ||
      d.includes('gateway');
    if (uiEntryCount > 0) {
      const score = uiEntryCount * 100 + uiEntryDegree * 10 + (isPlatformUiName ? 500 : 0);
      if (score > bestCenterScore) {
        bestCenterScore = score;
        centerDomain = d;
      }
    }
  }

  // Separate center domain and shared domain; order remaining domains by topological affinity (TSP / greedy chain)
  const regularEntries = Array.from(domainNodeMap.entries())
    .filter(([d]) => d !== '__shared__' && d !== centerDomain)
    .sort((a, b) => b[1].length - a[1].length);

  // Build inter-domain affinity matrix to place strongly connected sectors adjacent to each other
  const domainAffinity = new Map<string, Map<string, number>>();
  for (const e of visibleEdges) {
    const dS = nodeDomain.get(e.source);
    const dT = nodeDomain.get(e.target);
    if (dS && dT && dS !== dT && dS !== centerDomain && dT !== centerDomain && dS !== '__shared__' && dT !== '__shared__') {
      const w = e.count || 1;
      if (!domainAffinity.has(dS)) domainAffinity.set(dS, new Map());
      if (!domainAffinity.has(dT)) domainAffinity.set(dT, new Map());
      domainAffinity.get(dS)!.set(dT, (domainAffinity.get(dS)!.get(dT) || 0) + w);
      domainAffinity.get(dT)!.set(dS, (domainAffinity.get(dT)!.get(dS) || 0) + w);
    }
  }

  const orderedRegular: Array<[string, string[]]> = [];
  if (regularEntries.length > 0) {
    const remaining = new Map(regularEntries);
    let curr = regularEntries[0][0];
    orderedRegular.push([curr, remaining.get(curr)!]);
    remaining.delete(curr);
    while (remaining.size > 0) {
      let bestNext = '';
      let bestScore = -1;
      const aff = domainAffinity.get(curr);
      for (const cand of remaining.keys()) {
        const s = aff?.get(cand) || 0;
        if (s > bestScore) {
          bestScore = s;
          bestNext = cand;
        }
      }
      const nextDom = bestNext || remaining.keys().next().value!;
      orderedRegular.push([nextDom, remaining.get(nextDom)!]);
      remaining.delete(nextDom);
      curr = nextDom;
    }
  }

  const sortedDomainEntries: Array<[string, string[]]> = [...orderedRegular];
  if (domainNodeMap.has('__shared__')) {
    sortedDomainEntries.push(['__shared__', domainNodeMap.get('__shared__')!]);
  }

  // 4. Calculate required angular span for each domain to GUARANTEE zero overlaps
  // Arc parameters
  const MIN_ARC_GAP = Math.round((minNodeDistance ?? 160) * spacing); // clearance between adjacent node centers on the same tier
  const MARGIN_ARC = Math.round(95 * spacing);   // padding from sector boundary
  const RADIAL_STEP = Math.round((orbitDistance ?? 280) * spacing); // distance between concentric echelon orbits

  // Size center hub with generous sub-ring spacing so center nodes never overlap
  const centerNodeIds = centerDomain ? (domainNodeMap.get(centerDomain) || []) : [];
  const centerByTier = new Map<number, string[]>();
  for (const id of centerNodeIds) {
    const ech = nodeMap.get(id)?.echelonTier ?? 1;
    if (!centerByTier.has(ech)) centerByTier.set(ech, []);
    centerByTier.get(ech)!.push(id);
  }
  const centerTiers = Array.from(centerByTier.keys()).sort((a, b) => a - b);
  const centerSubStep = Math.max(Math.round(125 * spacing), Math.round(RADIAL_STEP * 0.45));
  const centerSubRings: Array<{ tier: number; ids: string[]; radius: number }> = [];
  let prevCenterR = 0;

  if (centerTiers.length > 0) {
    centerTiers.forEach((tier, tIdx) => {
      const ids = centerByTier.get(tier)!;
      if (tIdx === 0 && ids.length === 1) {
        centerSubRings.push({ tier, ids, radius: 0 });
        prevCenterR = 0;
      } else {
        const minCircumR = Math.ceil((ids.length * Math.round(145 * spacing)) / (2 * Math.PI));
        const rRing = Math.max(prevCenterR + centerSubStep, minCircumR, Math.round(125 * spacing));
        centerSubRings.push({ tier, ids, radius: rRing });
        prevCenterR = rRing;
      }
    });
  }

  const centerOuterR = centerNodeIds.length > 0
    ? prevCenterR + Math.round(90 * spacing)
    : 0;
  const BASE_INNER_R = centerNodeIds.length > 0
    ? centerOuterR + Math.max(Math.round(140 * spacing), Math.round(RADIAL_STEP * 0.7))
    : Math.round(240 * spacing);

  // Initial radii for populated outer orbits: only include tiers that actually have nodes in outer sectors!
  const outerPopulatedOrder = effectiveOrder.filter((tierIdx) =>
    sortedDomainEntries.some(([, ids]) => ids.some((id) => (nodeMap.get(id)?.echelonTier ?? 2) === tierIdx))
  );
  const activeOrbitOrder = outerPopulatedOrder.length > 0 ? outerPopulatedOrder : effectiveOrder;

  const orbitRadii = new Map<number, number>();
  activeOrbitOrder.forEach((tierIdx, rank) => {
    orbitRadii.set(tierIdx, BASE_INNER_R + rank * RADIAL_STEP);
  });

  // Calculate minimum angle required by each domain based on its most crowded tier
  // With even (count + 1) slot distribution across the sector (border -> node -> ... -> node -> border),
  // each of the (count + 1) gaps needs at least MIN_ARC_GAP clearance (or 2 * MARGIN_ARC for 1 node).
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
      const r = orbitRadii.get(ech) || BASE_INNER_R;
      const neededArc = count > 1 ? (count + 1) * MIN_ARC_GAP : 2 * MARGIN_ARC;
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

  // Iteratively expand BASE_INNER_R while keeping RADIAL_STEP (Orbit Distance) exact
  // until totalMinAngle <= availableBudget so outer sectors NEVER wrap past 360°!
  let safetyIter = 0;
  while (totalMinAngle > availableBudget && availableBudget > 0 && safetyIter < 8) {
    safetyIter++;
    const ratio = (totalMinAngle / availableBudget) * 1.05;
    const minCurrR = Math.min(...Array.from(orbitRadii.values()));
    const extraR = Math.max(Math.round(60 * spacing), Math.round(minCurrR * (ratio - 1)));
    for (const tierIdx of activeOrbitOrder) {
      const currentR = orbitRadii.get(tierIdx) || BASE_INNER_R;
      orbitRadii.set(tierIdx, currentR + extraR);
    }

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
        const neededArc = count > 1 ? (count + 1) * MIN_ARC_GAP : 2 * MARGIN_ARC;
        const ang = neededArc / r;
        if (ang > maxAng) maxAng = ang;
      }
      domainMinAngles.set(domain, maxAng);
      totalMinAngle += maxAng;
    }
  }

  // Final strict normalization guard so total span + gaps never exceeds 2*PI
  if (totalMinAngle > availableBudget && totalMinAngle > 0 && availableBudget > 0) {
    const scale = availableBudget / totalMinAngle;
    for (const [d, ang] of domainMinAngles.entries()) {
      domainMinAngles.set(d, ang * scale);
    }
    totalMinAngle = availableBudget;
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

  // 5b. Place Center Domain nodes (UI / Entry Points domain) in the center hub
  if (centerDomain && centerSubRings.length > 0) {
    centerSubRings.forEach(({ ids, radius }, rIdx) => {
      if (radius === 0 && ids.length === 1) {
        nodeAngles.set(ids[0], 0);
        nodeRadii.set(ids[0], 0);
      } else {
        const step = (2 * Math.PI) / ids.length;
        const angleOffset = -Math.PI / 2 + (rIdx % 2 === 1 ? step / 2 : 0);
        const slots = ids.map((_, idx) => normalizeAngle(angleOffset + idx * step));
        optimizeSlotAssignment(ids, slots, radius, nodeAngles, nodeRadii, adj, false);
      }
    });
  }

  // 6. Place nodes inside each domain sector strictly lined up on their respective echelon orbits
  // and evenly distributed across all nodes and both sector borders: step = span / (count + 1),
  // optimizing slot assignment across tiers so links are as short as possible!
  const sectorTierSlots: Array<{ ids: string[]; slots: number[]; r: number }> = [];

  for (const [domain, nodeIds] of sortedDomainEntries) {
    const slice = domainAngles.get(domain)!;

    // Group this domain's nodes by tier
    const nodesByTier = new Map<number, string[]>();
    for (const id of nodeIds) {
      const ech = nodeMap.get(id)?.echelonTier ?? 2;
      if (!nodesByTier.has(ech)) nodesByTier.set(ech, []);
      nodesByTier.get(ech)!.push(id);
    }

    for (const ech of activeOrbitOrder) {
      const ids = nodesByTier.get(ech);
      if (!ids || ids.length === 0) continue;
      const r = orbitRadii.get(ech) || BASE_INNER_R;
      const count = ids.length;
      const step = slice.span / (count + 1);
      const slots = Array.from({ length: count }, (_, idx) => slice.start + (idx + 1) * step);

      // Seed initial positions
      ids.forEach((id, idx) => {
        nodeAngles.set(id, slots[idx]);
        nodeRadii.set(id, r);
      });
      sectorTierSlots.push({ ids, slots, r });
    }
  }

  // Multi-pass slot optimization across all sectors and center sub-rings to minimize total link lengths
  for (let pass = 0; pass < 3; pass++) {
    for (const item of sectorTierSlots) {
      optimizeSlotAssignment(item.ids, item.slots, item.r, nodeAngles, nodeRadii, adj, pass > 0);
    }
    if (centerDomain && centerSubRings.length > 0) {
      centerSubRings.forEach(({ ids, radius }, rIdx) => {
        if (radius > 0 && ids.length > 1) {
          const step = (2 * Math.PI) / ids.length;
          const angleOffset = -Math.PI / 2 + (rIdx % 2 === 1 ? step / 2 : 0);
          const slots = ids.map((_, idx) => normalizeAngle(angleOffset + idx * step));
          optimizeSlotAssignment(ids, slots, radius, nodeAngles, nodeRadii, adj, true);
        }
      });
    }
  }

  // Compute Cartesian coordinates from optimized (angle, radius) pairs
  for (const n of visibleNodes) {
    const r = nodeRadii.get(n.id) ?? 0;
    const ang = nodeAngles.get(n.id) ?? 0;
    if (r === 0) {
      positions.set(n.id, { x: 0, y: 0 });
    } else {
      positions.set(n.id, {
        x: Math.round(r * Math.cos(ang)),
        y: Math.round(r * Math.sin(ang)),
      });
    }
  }

  // 7. Generate concentric orbit guides (rings)
  const populatedOrbits = activeOrbitOrder.map((tierIdx, rank) => {
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
  const orbitValues = Array.from(orbitRadii.values());
  const maxRadius = (orbitValues.length > 0 ? Math.max(...orbitValues) : BASE_INNER_R) + Math.round(75 * spacing);
  const minRadius = Math.max(
    centerOuterR > 0 ? centerOuterR + Math.round(28 * spacing) : 50,
    (orbitValues.length > 0 ? Math.min(...orbitValues) : BASE_INNER_R) - Math.round(55 * spacing)
  );

  const sectors: DomainSectorGuide[] = [];

  if (centerDomain && centerNodeIds.length > 0) {
    const dispName = domainDisplayNames.get(centerDomain) || formatDomainTitle(centerDomain);
    const color = explicitDomainDetails?.get(centerDomain)?.color || SECTOR_PALETTE[0];
    sectors.push({
      id: centerDomain,
      domainName: centerDomain,
      displayName: dispName,
      startAngle: 0,
      endAngle: 2 * Math.PI,
      centerAngle: -Math.PI / 2,
      innerRadius: 0,
      outerRadius: centerOuterR,
      color,
      count: centerNodeIds.length,
      nodeIds: centerNodeIds,
      isShared: false,
    });
  }

  sortedDomainEntries.forEach(([domain, nodeIds], idx) => {
    const slice = domainAngles.get(domain)!;
    const isShared = domain === '__shared__';
    const paletteIdx = centerDomain ? idx + 1 : idx;
    const color = isShared ? '#c084fc' : SECTOR_PALETTE[paletteIdx % SECTOR_PALETTE.length];
    const dispName = domainDisplayNames.get(domain) || formatDomainTitle(domain);

    sectors.push({
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
    });
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

