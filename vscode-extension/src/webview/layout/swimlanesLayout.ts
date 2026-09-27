/**
 * swimlanesLayout.ts
 *
 * Implements a structured horizontal Architectural Swimlane / Pipeline layout for DomainArchitectureView.
 * Columns/Lanes:
 *   Lane 0: 🚀 Clients & Ingress (Frontends, API Gateways, Public Endpoints)
 *   Lane 1: ⚙️ Primary Services (Gateway-facing Tier 1 Microservices)
 *   Lane 2: 🏛️ Domain Services (Internal Business Domain Microservices)
 *   Lane 3: ⚡ Background Workers (Schedulers, Ad Hubs, Async Workers)
 *   Lane 4: ✉️ Event Bus & Topics (RabbitMQ Exchanges, PubSub Topics)
 *   Lane 5: 🗄️ Databases & Storage (PostgreSQL, ClickHouse, Redis, External APIs)
 *
 * Nodes in each lane are ordered barycentrically (pulled towards connected nodes in adjacent lanes)
 * to strictly minimize edge crossings.
 */

export interface SwimlaneNodeInput {
  id: string;
  echelonTier: number; // 0..5
  kind: string; // 'Ingress' | 'Service' | 'Worker' | 'Topic' | 'Database' | etc.
  name: string;
  displayName?: string;
}

export interface SwimlaneEdgeInput {
  source: string;
  target: string;
  count?: number;
}

export interface SwimlaneGuide {
  id: string;
  title: string;
  icon: string;
  count: number;
  x: number;
  y: number;
  width: number;
  height: number;
  color: string;
}

export interface SwimlanesLayoutResult {
  positions: Map<string, { x: number; y: number }>;
  lanes: SwimlaneGuide[];
  bounds: { minX: number; maxX: number; minY: number; maxY: number };
}

interface LaneDefinition {
  tier: number;
  title: string;
  icon: string;
  color: string;
  accentBg: string;
}

const LANE_DEFINITIONS: LaneDefinition[] = [
  { tier: 0, title: 'Clients & Ingress', icon: '🚀', color: '#38bdf8', accentBg: 'rgba(56, 189, 248, 0.08)' },
  { tier: 1, title: 'Primary Services', icon: '⚙️', color: '#4ade80', accentBg: 'rgba(74, 222, 128, 0.08)' },
  { tier: 2, title: 'Domain Services', icon: '🏛️', color: '#818cf8', accentBg: 'rgba(129, 140, 248, 0.08)' },
  { tier: 3, title: 'Workers & Async', icon: '⚡', color: '#fb923c', accentBg: 'rgba(251, 146, 60, 0.08)' },
  { tier: 4, title: 'Event Bus & Topics', icon: '✉️', color: '#fbbf24', accentBg: 'rgba(251, 191, 36, 0.08)' },
  { tier: 5, title: 'Databases & Storage', icon: '🗄️', color: '#c084fc', accentBg: 'rgba(192, 132, 252, 0.08)' },
];

export function computeSwimlanesLayout(
  nodes: SwimlaneNodeInput[],
  edges: SwimlaneEdgeInput[],
  air: number = 1.2
): SwimlanesLayoutResult {
  const positions = new Map<string, { x: number; y: number }>();
  if (nodes.length === 0) {
    return {
      positions,
      lanes: [],
      bounds: { minX: 0, maxX: 0, minY: 0, maxY: 0 },
    };
  }

  // 1. Group nodes into lanes by echelonTier (0..5)
  const laneBuckets = new Map<number, string[]>();
  for (let t = 0; t <= 5; t++) laneBuckets.set(t, []);

  for (const n of nodes) {
    let t = n.echelonTier;
    if (t < 0 || t > 5 || isNaN(t)) t = 2; // Fallback to Domain Services
    laneBuckets.get(t)!.push(n.id);
  }

  // 2. Identify populated lanes
  const populatedLanes: Array<{ def: LaneDefinition; nodeIds: string[] }> = [];
  for (const def of LANE_DEFINITIONS) {
    const list = laneBuckets.get(def.tier) || [];
    if (list.length > 0) {
      populatedLanes.push({ def, nodeIds: list });
    }
  }

  if (populatedLanes.length === 0) {
    return {
      positions,
      lanes: [],
      bounds: { minX: 0, maxX: 0, minY: 0, maxY: 0 },
    };
  }

  // 3. Build adjacency for barycentric vertical sorting
  const inAdj = new Map<string, Array<{ id: string; weight: number }>>();
  const outAdj = new Map<string, Array<{ id: string; weight: number }>>();
  for (const n of nodes) {
    inAdj.set(n.id, []);
    outAdj.set(n.id, []);
  }
  for (const e of edges) {
    const w = e.count || 1;
    outAdj.get(e.source)?.push({ id: e.target, weight: w });
    inAdj.get(e.target)?.push({ id: e.source, weight: w });
  }

  // 4. Dimensions & Spacing
  const laneColWidth = Math.round(280 * air);
  const laneSpacing = Math.round(140 * air);
  const nodeHeight = Math.round(60 * air);
  const nodeGap = Math.round(45 * air);

  // Compute lane X coordinates (centered horizontally around 0)
  const totalWidth = populatedLanes.length * laneColWidth + (populatedLanes.length - 1) * laneSpacing;
  const startX = -totalWidth / 2 + laneColWidth / 2;

  const laneXCoords: number[] = [];
  populatedLanes.forEach((_, idx) => {
    laneXCoords.push(Math.round(startX + idx * (laneColWidth + laneSpacing)));
  });

  // 5. Initial vertical ordering: sort by degree/name
  const nodeYMap = new Map<string, number>();
  populatedLanes.forEach((lane) => {
    lane.nodeIds.sort((a, b) => {
      const degA = (inAdj.get(a)?.length || 0) + (outAdj.get(a)?.length || 0);
      const degB = (inAdj.get(b)?.length || 0) + (outAdj.get(b)?.length || 0);
      return degB - degA;
    });
    const N = lane.nodeIds.length;
    const laneTotalH = N * nodeHeight + (N - 1) * nodeGap;
    const topY = -laneTotalH / 2 + nodeHeight / 2;
    lane.nodeIds.forEach((id, i) => {
      nodeYMap.set(id, Math.round(topY + i * (nodeHeight + nodeGap)));
    });
  });

  // 6. Barycentric relaxation passes (Left-to-Right then Right-to-Left)
  for (let pass = 0; pass < 3; pass++) {
    // Forward pass (pull right lanes towards left lanes)
    for (let l = 1; l < populatedLanes.length; l++) {
      const lane = populatedLanes[l];
      const ideals = new Map<string, number>();
      for (const id of lane.nodeIds) {
        const predecessors = inAdj.get(id) || [];
        if (predecessors.length > 0) {
          let sumY = 0;
          let sumW = 0;
          for (const pred of predecessors) {
            const py = nodeYMap.get(pred.id);
            if (py !== undefined) {
              sumY += py * pred.weight;
              sumW += pred.weight;
            }
          }
          if (sumW > 0) ideals.set(id, sumY / sumW);
          else ideals.set(id, nodeYMap.get(id) ?? 0);
        } else {
          ideals.set(id, nodeYMap.get(id) ?? 0);
        }
      }

      // Sort lane nodes by ideal Y
      lane.nodeIds.sort((a, b) => (ideals.get(a) ?? 0) - (ideals.get(b) ?? 0));

      // Separate to prevent overlaps
      const N = lane.nodeIds.length;
      const laneTotalH = N * nodeHeight + (N - 1) * nodeGap;
      const avgIdeal = Array.from(ideals.values()).reduce((sum, v) => sum + v, 0) / Math.max(1, N);
      let currY = Math.round(avgIdeal - laneTotalH / 2 + nodeHeight / 2);
      for (const id of lane.nodeIds) {
        nodeYMap.set(id, currY);
        currY += nodeHeight + nodeGap;
      }
    }

    // Backward pass (pull left lanes towards right lanes)
    for (let l = populatedLanes.length - 2; l >= 0; l--) {
      const lane = populatedLanes[l];
      const ideals = new Map<string, number>();
      for (const id of lane.nodeIds) {
        const successors = outAdj.get(id) || [];
        if (successors.length > 0) {
          let sumY = 0;
          let sumW = 0;
          for (const succ of successors) {
            const sy = nodeYMap.get(succ.id);
            if (sy !== undefined) {
              sumY += sy * succ.weight;
              sumW += succ.weight;
            }
          }
          if (sumW > 0) ideals.set(id, sumY / sumW);
          else ideals.set(id, nodeYMap.get(id) ?? 0);
        } else {
          ideals.set(id, nodeYMap.get(id) ?? 0);
        }
      }

      lane.nodeIds.sort((a, b) => (ideals.get(a) ?? 0) - (ideals.get(b) ?? 0));
      const N = lane.nodeIds.length;
      const laneTotalH = N * nodeHeight + (N - 1) * nodeGap;
      const avgIdeal = Array.from(ideals.values()).reduce((sum, v) => sum + v, 0) / Math.max(1, N);
      let currY = Math.round(avgIdeal - laneTotalH / 2 + nodeHeight / 2);
      for (const id of lane.nodeIds) {
        nodeYMap.set(id, currY);
        currY += nodeHeight + nodeGap;
      }
    }
  }

  // 7. Assign final positions
  let globalMinX = Infinity;
  let globalMaxX = -Infinity;
  let globalMinY = Infinity;
  let globalMaxY = -Infinity;

  populatedLanes.forEach((lane, lIdx) => {
    const x = laneXCoords[lIdx];
    lane.nodeIds.forEach((id) => {
      const y = nodeYMap.get(id) ?? 0;
      positions.set(id, { x, y });
      if (x < globalMinX) globalMinX = x;
      if (x > globalMaxX) globalMaxX = x;
      if (y < globalMinY) globalMinY = y;
      if (y > globalMaxY) globalMaxY = y;
    });
  });

  // 8. Generate SVG Lane Guides / Backdrops
  // Compute max height across all lanes to ensure uniform, clean visual swimlane tracks
  const maxLaneSpanY = Math.max(globalMaxY - globalMinY + 160 * air, 600 * air);
  const laneBoxY = Math.round((globalMinY + globalMaxY) / 2 - maxLaneSpanY / 2);

  const guides: SwimlaneGuide[] = populatedLanes.map((lane, lIdx) => {
    const cx = laneXCoords[lIdx];
    const w = laneColWidth + 40 * air;
    const h = maxLaneSpanY;
    return {
      id: `swimlane:${lane.def.tier}`,
      title: lane.def.title,
      icon: lane.def.icon,
      count: lane.nodeIds.length,
      x: Math.round(cx - w / 2),
      y: laneBoxY,
      width: Math.round(w),
      height: Math.round(h),
      color: lane.def.color,
    };
  });

  return {
    positions,
    lanes: guides,
    bounds: {
      minX: globalMinX,
      maxX: globalMaxX,
      minY: globalMinY,
      maxY: globalMaxY,
    },
  };
}
