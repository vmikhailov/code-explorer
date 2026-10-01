/**
 * domainIslandsLayout.ts
 *
 * Implements a Bounded Context Islands / Galaxy layout for DomainArchitectureView.
 * Services, their dedicated databases, and their specific workers are clustered into
 * distinct Domain Islands (Bounded Contexts) with visual island boundaries.
 * Shared infrastructure (Shared DBs, Message Brokers) is placed in a central "Shared Core" island.
 */

import { cleanEntityToken } from './concentricLayout';

export interface IslandNodeInput {
  id: string;
  echelonTier: number; // 0..5
  kind: string; // 'Ingress' | 'Service' | 'Worker' | 'Topic' | 'Database' | etc.
  name: string;
  displayName?: string;
}

export interface IslandEdgeInput {
  source: string;
  target: string;
  category?: string;
  count?: number;
}

export interface IslandGuide {
  id: string;
  title: string;
  count: number;
  x: number;
  y: number;
  width: number;
  height: number;
  color: string;
  isSharedCore?: boolean;
}

export interface DomainIslandsLayoutResult {
  positions: Map<string, { x: number; y: number }>;
  islands: IslandGuide[];
  bounds: { minX: number; maxX: number; minY: number; maxY: number };
}

const DOMAIN_PALETTE = [
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
];

function extractDomainToken(name: string): string {
  const clean = cleanEntityToken(name);
  const parts = clean.split('-');
  if (parts.length >= 2) {
    // If first part is a common company/prefix like 'ats', take the first 2 parts e.g. 'ats-ad'
    if (parts[0].length <= 3 && parts.length > 2) {
      return `${parts[0]}-${parts[1]}`;
    }
    return parts[0];
  }
  return clean || 'domain';
}

export function computeDomainIslandsLayout(
  nodes: IslandNodeInput[],
  edges: IslandEdgeInput[],
  air: number = 1.2
): DomainIslandsLayoutResult {
  const positions = new Map<string, { x: number; y: number }>();
  if (nodes.length === 0) {
    return {
      positions,
      islands: [],
      bounds: { minX: 0, maxX: 0, minY: 0, maxY: 0 },
    };
  }

  // 1. Build adjacency
  const adj = new Map<string, Set<string>>();
  for (const n of nodes) adj.set(n.id, new Set());
  for (const e of edges) {
    adj.get(e.source)?.add(e.target);
    adj.get(e.target)?.add(e.source);
  }

  // 2. Classify services, databases, topics, workers
  const nodeMap = new Map(nodes.map((n) => [n.id, n]));
  const serviceNodes = nodes.filter((n) => n.echelonTier <= 2 || n.kind === 'Service' || n.kind === 'Ingress' || n.kind === 'App' || n.kind === 'FrontendApp');
  const infraNodes = nodes.filter((n) => n.echelonTier > 2 && n.kind !== 'Service' && n.kind !== 'Ingress' && n.kind !== 'App' && n.kind !== 'FrontendApp');

  // Assign domains to services
  const nodeDomainMap = new Map<string, string>();
  for (const s of serviceNodes) {
    const domain = extractDomainToken(s.displayName || s.name || s.id);
    nodeDomainMap.set(s.id, domain);
  }

  // Classify infrastructure: dedicated vs shared
  for (const inf of infraNodes) {
    const neighbors = Array.from(adj.get(inf.id) || []);
    const connectedServiceDomains = new Set<string>();
    for (const nbr of neighbors) {
      const d = nodeDomainMap.get(nbr);
      if (d) connectedServiceDomains.add(d);
    }

    if (connectedServiceDomains.size === 1) {
      // 1:1 or 1:N inside a single domain -> Dedicated to that domain!
      nodeDomainMap.set(inf.id, connectedServiceDomains.values().next().value!);
    } else {
      // Called by multiple domains or isolated -> Shared Core Infrastructure
      nodeDomainMap.set(inf.id, '__shared_core__');
    }
  }

  // Any remaining unassigned
  for (const n of nodes) {
    if (!nodeDomainMap.has(n.id)) {
      nodeDomainMap.set(n.id, extractDomainToken(n.displayName || n.name || n.id));
    }
  }

  // 3. Group by domain
  const domainGroups = new Map<string, string[]>();
  for (const [id, d] of nodeDomainMap.entries()) {
    if (!domainGroups.has(d)) domainGroups.set(d, []);
    domainGroups.get(d)!.push(id);
  }

  // Sort domains: regular domains first, shared core in center
  const regularDomains = Array.from(domainGroups.keys()).filter((d) => d !== '__shared_core__');
  regularDomains.sort((a, b) => (domainGroups.get(b)?.length || 0) - (domainGroups.get(a)?.length || 0));

  const hasSharedCore = domainGroups.has('__shared_core__') && (domainGroups.get('__shared_core__')?.length || 0) > 0;
  const numDomains = regularDomains.length;

  // 4. Place Island Centers
  // Shared Core sits at (0, 0). Regular domains form a circle around the core.
  const islandCenters = new Map<string, { cx: number; cy: number; radius: number }>();

  // Estimate radius needed for each domain based on its node count
  const calcDomainSize = (count: number) => {
    const minR = Math.max(130 * air, Math.sqrt(count) * 65 * air);
    return minR;
  };

  const sharedCount = domainGroups.get('__shared_core__')?.length || 0;
  const sharedRadius = calcDomainSize(sharedCount);
  if (hasSharedCore) {
    islandCenters.set('__shared_core__', { cx: 0, cy: 0, radius: sharedRadius });
  }

  // Ring of domain islands
  const maxIslandRadius = regularDomains.reduce((max, d) => Math.max(max, calcDomainSize(domainGroups.get(d)?.length || 0)), 120 * air);
  const ringRadius = Math.max(
    (hasSharedCore ? sharedRadius : 0) + maxIslandRadius + 140 * air,
    ((numDomains * (maxIslandRadius * 2 + 80 * air)) / (2 * Math.PI))
  );

  const angleStep = numDomains > 0 ? (2 * Math.PI) / numDomains : 0;
  regularDomains.forEach((d, idx) => {
    const theta = -Math.PI / 2 + idx * angleStep;
    const cx = Math.round(ringRadius * Math.cos(theta));
    const cy = Math.round(ringRadius * Math.sin(theta));
    const r = calcDomainSize(domainGroups.get(d)?.length || 0);
    islandCenters.set(d, { cx, cy, radius: r });
  });

  // 5. Pack nodes inside each island
  const guides: IslandGuide[] = [];
  let colorIdx = 0;

  for (const [d, memberIds] of domainGroups.entries()) {
    const center = islandCenters.get(d) ?? { cx: 0, cy: 0, radius: 100 * air };
    const N = memberIds.length;
    const isShared = d === '__shared_core__';

    // Separate services vs others inside the island
    const innerNodes = memberIds.filter((id) => {
      const n = nodeMap.get(id);
      return n && (n.echelonTier <= 2 || n.kind === 'Service' || n.kind === 'Ingress' || n.kind === 'App' || n.kind === 'FrontendApp');
    });
    const outerNodes = memberIds.filter((id) => !innerNodes.includes(id));

    let minX = Infinity;
    let maxX = -Infinity;
    let minY = Infinity;
    let maxY = -Infinity;

    if (N === 1) {
      positions.set(memberIds[0], { x: center.cx, y: center.cy });
      minX = center.cx - 50;
      maxX = center.cx + 50;
      minY = center.cy - 30;
      maxY = center.cy + 30;
    } else {
      // Place inner nodes in center/inner ring, outer nodes in outer ring
      if (innerNodes.length === 1) {
        positions.set(innerNodes[0], { x: center.cx, y: center.cy });
      } else if (innerNodes.length > 1) {
        const innerR = Math.max(50 * air, (innerNodes.length * 50 * air) / (2 * Math.PI));
        innerNodes.forEach((id, i) => {
          const a = -Math.PI / 2 + (i * 2 * Math.PI) / innerNodes.length;
          const nx = Math.round(center.cx + innerR * Math.cos(a));
          const ny = Math.round(center.cy + innerR * Math.sin(a));
          positions.set(id, { x: nx, y: ny });
        });
      }

      if (outerNodes.length > 0) {
        const outerR = Math.max(
          (innerNodes.length > 0 ? 80 * air : 0) + 40 * air,
          (outerNodes.length * 60 * air) / (2 * Math.PI)
        );
        outerNodes.forEach((id, i) => {
          const a = -Math.PI / 2 + (i * 2 * Math.PI) / outerNodes.length;
          const nx = Math.round(center.cx + outerR * Math.cos(a));
          const ny = Math.round(center.cy + outerR * Math.sin(a));
          positions.set(id, { x: nx, y: ny });
        });
      }

      // Compute bounding box
      for (const id of memberIds) {
        const p = positions.get(id);
        if (p) {
          if (p.x < minX) minX = p.x;
          if (p.x > maxX) maxX = p.x;
          if (p.y < minY) minY = p.y;
          if (p.y > maxY) maxY = p.y;
        }
      }
    }

    const padding = Math.round(45 * air);
    const boxX = minX - padding;
    const boxY = minY - padding - 24; // Extra headroom for header tag
    const boxW = maxX - minX + padding * 2;
    const boxH = maxY - minY + padding * 2 + 24;

    const color = isShared ? '#c084fc' : DOMAIN_PALETTE[colorIdx % DOMAIN_PALETTE.length];
    if (!isShared) colorIdx++;

    const title = isShared
      ? 'Shared Infrastructure & Core'
      : `${d.toUpperCase()} Context`;

    guides.push({
      id: `island:${d}`,
      title,
      count: N,
      x: boxX,
      y: boxY,
      width: boxW,
      height: boxH,
      color,
      isSharedCore: isShared,
    });
  }

  // Bounds
  let gMinX = Infinity, gMaxX = -Infinity, gMinY = Infinity, gMaxY = -Infinity;
  for (const g of guides) {
    if (g.x < gMinX) gMinX = g.x;
    if (g.x + g.width > gMaxX) gMaxX = g.x + g.width;
    if (g.y < gMinY) gMinY = g.y;
    if (g.y + g.height > gMaxY) gMaxY = g.y + g.height;
  }

  return {
    positions,
    islands: guides,
    bounds: {
      minX: gMinX === Infinity ? 0 : gMinX,
      maxX: gMaxX === -Infinity ? 0 : gMaxX,
      minY: gMinY === Infinity ? 0 : gMinY,
      maxY: gMaxY === -Infinity ? 0 : gMaxY,
    },
  };
}
