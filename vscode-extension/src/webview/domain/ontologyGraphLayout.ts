import {
  OntologyEdgeCategoryId,
  OntologyNodeSchemaDto,
  OntologySchemaDocumentDto,
  OntologyWorkspaceStatsDto,
} from '../../../../proto/types';
import {
  getWorkspaceNodeCount,
  getWorkspaceTripleCount,
} from './ontologyModel';

export interface OntologyGraphNodeStyle {
  fill: string;
  glyph: string;
  cluster: number;
}

export interface OntologyGraphNode {
  kind: string;
  pluralLabel: string;
  layerId: number;
  x: number;
  y: number;
  radius: number;
  fill: string;
  glyph: string;
  cluster: number;
  badgeText: string;
  rawCount: number;
  propertyCount: number;
  isAbstract?: boolean;
  extends?: string;
  collapsedCount?: number;
  hasExpandedSubtypes?: boolean;
}

export interface OntologyGraphCapsule {
  kind: string;
  depth: number;
  memberKinds: string[];
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
  headerX: number;
  headerY: number;
  headerW: number;
  headerH: number;
}

export interface OntologyGraphEdge {
  key: string;
  fromKind: string;
  toKind: string;
  rel: string;
  category: OntologyEdgeCategoryId;
  isExtends?: boolean;
  isSelfLoop: boolean;
  selfLoopSlot: number;
  canonicalOffset: number;
  priority: number;
  workspaceCount: number;
}

const COLOR_LOGICAL = '#db2777';
const COLOR_MODULE = '#6d28d9';
const COLOR_WORKLOAD = '#7c3aed';
const COLOR_CONTRACT = '#d97706';
const COLOR_DATA = '#16a34a';
const COLOR_CLOUD = '#0891b2';
const COLOR_SYNTAX = '#dc2626';
const COLOR_SLICING = '#4f46e5';
const COLOR_PROJECT = '#2563eb';
const COLOR_PHYSICAL = '#475569';

const NODE_RADIUS = 25;
const WORLD_WIDTH = 1380;
const WORLD_HEIGHT = 880;

const NODE_STYLE_MAP: Record<string, OntologyGraphNodeStyle> = {
  Domain: { fill: COLOR_LOGICAL, glyph: 'globe', cluster: 8 },
  BoundedContext: { fill: COLOR_LOGICAL, glyph: 'layers', cluster: 8 },

  Module: { fill: COLOR_MODULE, glyph: 'package', cluster: 0 },
  Workload: { fill: COLOR_WORKLOAD, glyph: 'layers', cluster: 0 },
  App: { fill: COLOR_WORKLOAD, glyph: 'browser', cluster: 0 },
  Service: { fill: COLOR_WORKLOAD, glyph: 'server', cluster: 0 },
  Worker: { fill: COLOR_WORKLOAD, glyph: 'gear', cluster: 0 },
  CliTool: { fill: COLOR_WORKLOAD, glyph: 'terminal', cluster: 0 },
  Library: { fill: COLOR_MODULE, glyph: 'library', cluster: 0 },
  TestSuite: { fill: COLOR_MODULE, glyph: 'beaker', cluster: 0 },

  Endpoint: { fill: COLOR_CONTRACT, glyph: 'endpoint', cluster: 1 },
  Topic: { fill: COLOR_CONTRACT, glyph: 'topic', cluster: 1 },
  PeerMesh: { fill: COLOR_CONTRACT, glyph: 'mesh', cluster: 1 },

  Database: { fill: COLOR_DATA, glyph: 'database', cluster: 2 },
  DbSchema: { fill: COLOR_DATA, glyph: 'schema', cluster: 2 },
  Table: { fill: COLOR_DATA, glyph: 'table', cluster: 2 },
  Query: { fill: COLOR_DATA, glyph: 'query', cluster: 2 },
  Procedure: { fill: COLOR_DATA, glyph: 'procedure', cluster: 2 },

  ExternalService: { fill: COLOR_CLOUD, glyph: 'cloud', cluster: 3 },
  Cloud: { fill: COLOR_CLOUD, glyph: 'cloudUp', cluster: 3 },

  Type: { fill: COLOR_SYNTAX, glyph: 'type', cluster: 4 },
  Function: { fill: COLOR_SYNTAX, glyph: 'function', cluster: 4 },
  Member: { fill: COLOR_SYNTAX, glyph: 'member', cluster: 4 },
  CatchClause: { fill: COLOR_SYNTAX, glyph: 'catch', cluster: 4 },

  Parameter: { fill: COLOR_SLICING, glyph: 'param', cluster: 5 },
  CallArgument: { fill: COLOR_SLICING, glyph: 'arg', cluster: 5 },
  CallLiteral: { fill: COLOR_SLICING, glyph: 'literal', cluster: 5 },

  Project: { fill: COLOR_PROJECT, glyph: 'project', cluster: 6 },
  EntryPoint: { fill: COLOR_PROJECT, glyph: 'zap', cluster: 6 },
  Package: { fill: COLOR_PROJECT, glyph: 'package', cluster: 6 },

  File: { fill: COLOR_PHYSICAL, glyph: 'file', cluster: 7 },
  Folder: { fill: COLOR_PHYSICAL, glyph: 'folder', cluster: 7 },
  GitSettings: { fill: COLOR_PHYSICAL, glyph: 'git', cluster: 7 },
};

const CLUSTER_ANCHORS: ReadonlyArray<{ x: number; y: number; rx: number; ry: number }> = [
  { x: 255, y: 235, rx: 145, ry: 105 }, // 0: Modules & Workloads
  { x: 635, y: 255, rx: 145, ry: 85 },  // 1: Contracts & Messaging
  { x: 1125, y: 285, rx: 135, ry: 110 }, // 2: Data, DbSchema & SQL
  { x: 925, y: 155, rx: 105, ry: 60 },  // 3: External & Cloud
  { x: 705, y: 495, rx: 165, ry: 115 }, // 4: Syntactic UAST
  { x: 1065, y: 575, rx: 125, ry: 95 }, // 5: Semantic Slicing
  { x: 305, y: 605, rx: 130, ry: 90 },  // 6: Project Boundaries
  { x: 645, y: 695, rx: 155, ry: 55 },  // 7: Physical Filesystem
  { x: 595, y: 115, rx: 85, ry: 24 },   // 8: Logical Domains & Contexts
];

const HIERARCHY_TREE_ANCHORS: Record<string, { x: number; y: number }> = {
  App: { x: 115, y: 145 },
  CliTool: { x: 115, y: 265 },
  Workload: { x: 265, y: 215 },
  Service: { x: 405, y: 150 },
  Worker: { x: 405, y: 270 },
  Module: { x: 295, y: 385 },
  Library: { x: 135, y: 385 },
  TestSuite: { x: 155, y: 495 },
};

const SECONDARY_WORKLOAD_HUBS = new Set(['App', 'Worker', 'CliTool', 'Library', 'TestSuite']);
const TRAILING_DECIMAL_ZERO = /\.0$/;
const PAIR_SEPARATOR = '::';
const EDGE_KEY_SEPARATOR = '|';

export function formatCompactCount(count: number): string {
  if (!Number.isFinite(count) || count <= 0) return '0';
  if (count < 1000) return String(Math.round(count));
  if (count < 1_000_000) {
    const digits = count >= 100_000 ? 0 : 1;
    return `${(count / 1000).toFixed(digits).replace(TRAILING_DECIMAL_ZERO, '')}k`;
  }
  return `${(count / 1_000_000).toFixed(1).replace(TRAILING_DECIMAL_ZERO, '')}M`;
}

export function getOntologyNodeStyle(kind: string, layerId: number): OntologyGraphNodeStyle {
  const mapped = NODE_STYLE_MAP[kind];
  if (mapped) return mapped;
  if (layerId === 5) return { fill: COLOR_LOGICAL, glyph: 'globe', cluster: 8 };
  if (layerId === 4) return { fill: COLOR_WORKLOAD, glyph: 'server', cluster: 0 };
  if (layerId === 3) return { fill: COLOR_SYNTAX, glyph: 'function', cluster: 4 };
  if (layerId === 2) return { fill: COLOR_PROJECT, glyph: 'project', cluster: 6 };
  return { fill: COLOR_PHYSICAL, glyph: 'file', cluster: 7 };
}

export function buildOntologyGraphEdges(
  schema: OntologySchemaDocumentDto,
  visibleKindSet: ReadonlySet<string>,
  activeCategories: ReadonlySet<string>,
  workspace?: OntologyWorkspaceStatsDto | null,
  onlyActiveInWorkspace?: boolean,
  redirectMap?: ReadonlyMap<string, string>
): OntologyGraphEdge[] {
  const dedupRaw = new Map<
    string,
    {
      fromKind: string;
      toKind: string;
      rel: string;
      category: OntologyEdgeCategoryId;
      isExtends?: boolean;
      priority: number;
      workspaceCount: number;
    }
  >();

  for (const nodeDef of schema.nodes) {
    if (!nodeDef.extends) continue;
    if (!visibleKindSet.has(nodeDef.kind) || !visibleKindSet.has(nodeDef.extends)) continue;
    const extKey = [nodeDef.kind, 'EXTENDS', nodeDef.extends].join(EDGE_KEY_SEPARATOR);
    dedupRaw.set(extKey, {
      fromKind: nodeDef.kind,
      toKind: nodeDef.extends,
      rel: 'EXTENDS',
      category: 'type_dependency',
      isExtends: true,
      priority: 1,
      workspaceCount: getWorkspaceNodeCount(workspace, nodeDef.kind, schema),
    });
  }

  const filterByObserved = Boolean(
    onlyActiveInWorkspace && workspace && workspace.observedTriples && workspace.observedTriples.length > 0
  );

  for (const relDef of schema.relationships) {
    if (!activeCategories.has(relDef.category)) continue;
    for (const pair of relDef.pairs) {
      const effFrom = redirectMap?.get(pair.fromKind) || pair.fromKind;
      const effTo = redirectMap?.get(pair.toKind) || pair.toKind;
      if (!visibleKindSet.has(effFrom) || !visibleKindSet.has(effTo)) continue;
      if (effFrom === effTo && pair.fromKind !== pair.toKind) continue;

      const wsCount = getWorkspaceTripleCount(workspace, pair.fromKind, relDef.rel, pair.toKind, schema);
      if (filterByObserved && wsCount <= 0) continue;

      let priority = 1;
      if (relDef.rel === 'DECLARED_IN' || relDef.rel === 'BELONGS_TO') {
        priority = 4;
      } else if (SECONDARY_WORKLOAD_HUBS.has(effFrom) || SECONDARY_WORKLOAD_HUBS.has(effTo)) {
        priority = 3;
      } else if (
        effFrom === 'Service' ||
        effTo === 'Service' ||
        effFrom === 'Workload' ||
        effTo === 'Workload' ||
        relDef.category === 'structural'
      ) {
        priority = 2;
      }

      const rawKey = [effFrom, relDef.rel, effTo].join(EDGE_KEY_SEPARATOR);
      const prev = dedupRaw.get(rawKey);
      if (prev) {
        prev.workspaceCount += wsCount;
        if (priority < prev.priority) prev.priority = priority;
      } else {
        dedupRaw.set(rawKey, {
          fromKind: effFrom,
          toKind: effTo,
          rel: relDef.rel,
          category: relDef.category,
          priority,
          workspaceCount: wsCount,
        });
      }
    }
  }

  const rawEdges = [...dedupRaw.values()];
  const selfCounts = new Map<string, number>();
  const pairGroups = new Map<string, typeof rawEdges>();

  for (const edge of rawEdges) {
    if (edge.fromKind === edge.toKind) continue;
    const minK = edge.fromKind < edge.toKind ? edge.fromKind : edge.toKind;
    const maxK = edge.fromKind < edge.toKind ? edge.toKind : edge.fromKind;
    const pairKey = `${minK}${PAIR_SEPARATOR}${maxK}`;
    const list = pairGroups.get(pairKey) || [];
    list.push(edge);
    pairGroups.set(pairKey, list);
  }

  const result: OntologyGraphEdge[] = [];

  for (const edge of rawEdges) {
    const key = [edge.fromKind, edge.rel, edge.toKind].join(EDGE_KEY_SEPARATOR);
    if (edge.fromKind === edge.toKind) {
      const slot = selfCounts.get(edge.fromKind) || 0;
      selfCounts.set(edge.fromKind, slot + 1);
      result.push({
        key,
        fromKind: edge.fromKind,
        toKind: edge.toKind,
        rel: edge.rel,
        category: edge.category,
        isSelfLoop: true,
        selfLoopSlot: slot,
        canonicalOffset: 0,
        priority: edge.priority,
        workspaceCount: edge.workspaceCount,
      });
    }
  }

  for (const [, group] of pairGroups) {
    const count = group.length;
    for (let idx = 0; idx < count; idx++) {
      const edge = group[idx];
      const key = [edge.fromKind, edge.rel, edge.toKind].join(EDGE_KEY_SEPARATOR);
      let canonicalOffset = 22;
      if (count > 1) {
        const centered = idx - (count - 1) / 2;
        canonicalOffset = centered * 34;
        if (Math.abs(canonicalOffset) < 6) {
          canonicalOffset = 14;
        }
      }
      result.push({
        key,
        fromKind: edge.fromKind,
        toKind: edge.toKind,
        rel: edge.rel,
        category: edge.category,
        isExtends: edge.isExtends,
        isSelfLoop: false,
        selfLoopSlot: 0,
        canonicalOffset,
        priority: edge.priority,
        workspaceCount: edge.workspaceCount,
      });
    }
  }

  result.sort((a, b) => a.priority - b.priority);
  return result;
}

interface SimBody {
  dto: OntologyNodeSchemaDto;
  style: OntologyGraphNodeStyle;
  x: number;
  y: number;
  ax: number;
  ay: number;
  vx: number;
  vy: number;
  fx: number | null;
  fy: number | null;
  degree: number;
  anchorStrength: number;
}

interface SimSpring {
  source: SimBody;
  target: SimBody;
  distance: number;
  strength: number;
  bias: number;
}

const WORLD_CENTER_X = WORLD_WIDTH / 2;
const WORLD_CENTER_Y = WORLD_HEIGHT / 2;
const DEFAULT_ALPHA_DECAY = 0.0228;
const DEFAULT_ALPHA_MIN = 0.001;
const VELOCITY_RETENTION = 0.6;
const MAX_STEP_SPEED = 28;
const BASE_CHARGE_STRENGTH = -620;
const BASE_COLLIDE_RADIUS = 52;
const CHARGE_MIN_DIST_SQ = 900;
const BASE_MAX_CHARGE_DIST = 540;

function computeSeedAnchors(
  nodesToPlace: readonly OntologyNodeSchemaDto[]
): Map<string, { ax: number; ay: number; style: OntologyGraphNodeStyle; anchorStrength: number }> {
  const clusterBuckets = new Map<number, OntologyNodeSchemaDto[]>();
  for (const n of nodesToPlace) {
    const style = getOntologyNodeStyle(n.kind, n.layerId);
    const arr = clusterBuckets.get(style.cluster) || [];
    arr.push(n);
    clusterBuckets.set(style.cluster, arr);
  }

  const seeds = new Map<
    string,
    { ax: number; ay: number; style: OntologyGraphNodeStyle; anchorStrength: number }
  >();

  for (const [clusterId, bucket] of clusterBuckets) {
    const anchor = CLUSTER_ANCHORS[clusterId] || CLUSTER_ANCHORS[0];
    const count = bucket.length;
    for (let i = 0; i < count; i++) {
      const dto = bucket[i];
      const style = getOntologyNodeStyle(dto.kind, dto.layerId);
      const treePos = clusterId === 0 && count > 1 ? HIERARCHY_TREE_ANCHORS[dto.kind] : undefined;
      const angle = count === 1 ? 0 : (i / count) * Math.PI * 2 - Math.PI / 2;
      const ax = treePos ? treePos.x : anchor.x + (count === 1 ? 0 : Math.cos(angle) * anchor.rx);
      const ay = treePos ? treePos.y : anchor.y + (count === 1 ? 0 : Math.sin(angle) * anchor.ry);
      seeds.set(dto.kind, {
        ax,
        ay,
        style,
        anchorStrength: treePos ? 0.072 : 0.048,
      });
    }
  }
  return seeds;
}

function buildSimSprings(
  schema: OntologySchemaDocumentDto,
  bodyByKind: ReadonlyMap<string, SimBody>,
  activeCategories?: ReadonlySet<string>,
  redirectMap?: ReadonlyMap<string, string>
): SimSpring[] {
  const rawPairs: Array<{ a: SimBody; b: SimBody; isExtends: boolean }> = [];
  const seenPairs = new Set<string>();
  const incidentCounts = new Map<string, number>();

  const addPair = (fromKind: string, toKind: string, isExtends: boolean) => {
    if (fromKind === toKind) return;
    const ba = bodyByKind.get(fromKind);
    const bb = bodyByKind.get(toKind);
    if (!ba || !bb) return;
    const minK = fromKind < toKind ? fromKind : toKind;
    const maxK = fromKind < toKind ? toKind : fromKind;
    const pk = `${minK}${PAIR_SEPARATOR}${maxK}`;
    if (seenPairs.has(pk)) return;
    seenPairs.add(pk);
    rawPairs.push({ a: ba, b: bb, isExtends });
    incidentCounts.set(fromKind, (incidentCounts.get(fromKind) || 0) + 1);
    incidentCounts.set(toKind, (incidentCounts.get(toKind) || 0) + 1);
  };

  for (const nodeDef of schema.nodes) {
    if (nodeDef.extends) {
      addPair(nodeDef.kind, nodeDef.extends, true);
    }
  }

  for (const rel of schema.relationships) {
    if (activeCategories && !activeCategories.has(rel.category)) continue;
    for (const p of rel.pairs) {
      const effFrom = redirectMap?.get(p.fromKind) || p.fromKind;
      const effTo = redirectMap?.get(p.toKind) || p.toKind;
      addPair(effFrom, effTo, false);
    }
  }

  for (const [kind, body] of bodyByKind) {
    body.degree = Math.max(1, incidentCounts.get(kind) || 1);
  }

  return rawPairs.map(({ a, b, isExtends }) => {
    const ca = a.degree;
    const cb = b.degree;
    const rawBias = ca / (ca + cb);
    const bias = Math.max(0.22, Math.min(0.78, rawBias));
    const minDeg = Math.max(1, Math.min(ca, cb));
    const baseStrength = isExtends ? 0.54 : 0.48 / Math.sqrt(minDeg);
    const strength = Math.max(0.16, Math.min(0.56, baseStrength));
    const sameCluster = a.style.cluster === b.style.cluster;
    const distance = isExtends ? 126 : sameCluster ? 144 : 212;
    return { source: a, target: b, distance, strength, bias };
  });
}

function stepForceBodies(
  bodies: readonly SimBody[],
  springs: readonly SimSpring[],
  alpha: number,
  spacingFactor: number,
  hubFactor = 1.0
): void {
  const maxChargeDistSq = Math.pow(BASE_MAX_CHARGE_DIST * spacingFactor * (1 + hubFactor * 0.25), 2);
  const charge = BASE_CHARGE_STRENGTH * spacingFactor * alpha;

  for (let i = 0; i < bodies.length; i++) {
    const bi = bodies[i];
    const wi = 1 + hubFactor * 0.24 * Math.max(0, bi.degree - 2);
    for (let j = i + 1; j < bodies.length; j++) {
      const bj = bodies[j];
      let dx = bj.x - bi.x;
      let dy = bj.y - bi.y;
      let d2 = dx * dx + dy * dy;
      if (d2 > maxChargeDistSq) continue;
      if (d2 < 1) {
        dx = i % 2 === 0 ? 1 : -1;
        dy = j % 2 === 0 ? 0.5 : -0.5;
        d2 = 1.25;
      }
      const wj = 1 + hubFactor * 0.24 * Math.max(0, bj.degree - 2);
      const pairHubMult = 0.5 * (wi + wj) * (1 + hubFactor * 0.08 * Math.min(bi.degree, bj.degree));
      const w = (charge * pairHubMult) / Math.max(d2, CHARGE_MIN_DIST_SQ);
      if (bi.fx === null) {
        bi.vx += dx * w;
        bi.vy += dy * w;
      }
      if (bj.fx === null) {
        bj.vx -= dx * w;
        bj.vy -= dy * w;
      }
    }
  }

  for (const sp of springs) {
    let dx = sp.target.x + sp.target.vx - sp.source.x - sp.source.vx;
    let dy = sp.target.y + sp.target.vy - sp.source.y - sp.source.vy;
    const dist = Math.hypot(dx, dy) || 1;
    const degExcess = Math.max(0, sp.source.degree + sp.target.degree - 4);
    const hubDistMult = 1 + hubFactor * 0.042 * degExcess;
    const hubStrengthMult = 1 / (1 + hubFactor * 0.055 * degExcess);
    const targetDist = sp.distance * spacingFactor * hubDistMult;
    const k = ((dist - targetDist) / dist) * alpha * sp.strength * hubStrengthMult;
    dx *= k;
    dy *= k;
    if (sp.target.fx === null) {
      sp.target.vx -= dx * sp.bias;
      sp.target.vy -= dy * sp.bias;
    }
    if (sp.source.fx === null) {
      sp.source.vx += dx * (1 - sp.bias);
      sp.source.vy += dy * (1 - sp.bias);
    }
  }

  for (const b of bodies) {
    if (b.fx !== null) continue;
    const targetAx = WORLD_CENTER_X + (b.ax - WORLD_CENTER_X) * spacingFactor;
    const targetAy = WORLD_CENTER_Y + (b.ay - WORLD_CENTER_Y) * spacingFactor;
    const effAnchor =
      b.anchorStrength / (1 + hubFactor * 0.045 * Math.max(0, b.degree - 2));
    b.vx += (targetAx - b.x) * effAnchor * alpha;
    b.vy += (targetAy - b.y) * effAnchor * alpha;
  }

  const baseHalfRad = BASE_COLLIDE_RADIUS * 0.5 * Math.min(spacingFactor, 2.2);
  for (let pass = 0; pass < 2; pass++) {
    for (let i = 0; i < bodies.length; i++) {
      const bi = bodies[i];
      const ri = baseHalfRad * (1 + hubFactor * 0.035 * Math.max(0, bi.degree - 2));
      for (let j = i + 1; j < bodies.length; j++) {
        const bj = bodies[j];
        const rj = baseHalfRad * (1 + hubFactor * 0.035 * Math.max(0, bj.degree - 2));
        const minSep = ri + rj;
        let dx = bj.x + bj.vx - (bi.x + bi.vx);
        let dy = bj.y + bj.vy - (bi.y + bi.vy);
        let d2 = dx * dx + dy * dy;
        if (d2 >= minSep * minSep) continue;
        if (d2 < 0.01) {
          dx = 0.5;
          dy = 0.5;
          d2 = 0.5;
        }
        const d = Math.sqrt(d2);
        const overlap = ((minSep - d) / d) * 0.42;
        const ox = dx * overlap;
        const oy = dy * overlap;
        if (bi.fx === null && bj.fx === null) {
          bi.vx -= ox;
          bi.vy -= oy;
          bj.vx += ox;
          bj.vy += oy;
        } else if (bi.fx !== null && bj.fx === null) {
          bj.vx += ox * 2;
          bj.vy += oy * 2;
        } else if (bj.fx !== null && bi.fx === null) {
          bi.vx -= ox * 2;
          bi.vy -= oy * 2;
        }
      }
    }
  }

  for (const b of bodies) {
    if (b.fx !== null && b.fy !== null) {
      b.x = b.fx;
      b.y = b.fy;
      b.vx = 0;
      b.vy = 0;
      continue;
    }
    b.vx *= VELOCITY_RETENTION;
    b.vy *= VELOCITY_RETENTION;
    const speed = Math.hypot(b.vx, b.vy);
    if (speed > MAX_STEP_SPEED) {
      b.vx = (b.vx / speed) * MAX_STEP_SPEED;
      b.vy = (b.vy / speed) * MAX_STEP_SPEED;
    }
    b.x += b.vx;
    b.y += b.vy;
  }
}

function projectGraphNodes(
  bodies: readonly SimBody[],
  bodyByKind: ReadonlyMap<string, SimBody>,
  schema: OntologySchemaDocumentDto,
  workspace?: OntologyWorkspaceStatsDto | null,
  collapsedCounts?: ReadonlyMap<string, number>,
  roundCoords = false
): OntologyGraphNode[] {
  const hasWorkspace = Boolean(workspace && workspace.totalNodes > 0);
  return bodies.map((b) => {
    const rawCount = hasWorkspace
      ? getWorkspaceNodeCount(workspace, b.dto.kind, schema)
      : b.dto.properties.length;
    const hasExpandedSubtypes = Boolean(
      b.dto.subtypes && b.dto.subtypes.some((sub) => bodyByKind.has(sub))
    );
    return {
      kind: b.dto.kind,
      pluralLabel: b.dto.pluralLabel,
      layerId: b.dto.layerId,
      x: roundCoords ? Math.round(b.x * 10) / 10 : b.x,
      y: roundCoords ? Math.round(b.y * 10) / 10 : b.y,
      radius: NODE_RADIUS,
      fill: b.style.fill,
      glyph: b.style.glyph,
      cluster: b.style.cluster,
      badgeText: formatCompactCount(rawCount),
      rawCount,
      propertyCount: b.dto.properties.length,
      isAbstract: b.dto.isAbstract,
      extends: b.dto.extends,
      collapsedCount: collapsedCounts?.get(b.dto.kind),
      hasExpandedSubtypes,
    };
  });
}

export class OntologyForceSimulation {
  private bodyByKind = new Map<string, SimBody>();
  private bodies: SimBody[] = [];
  private springs: SimSpring[] = [];
  private schema: OntologySchemaDocumentDto | null = null;
  private workspace: OntologyWorkspaceStatsDto | null | undefined = null;
  private collapsedCounts: ReadonlyMap<string, number> | undefined = undefined;
  private spacingFactor = 1.0;
  private hubFactor = 1.0;
  private alpha = 0;
  private alphaTarget = 0;
  private frozen = false;
  private lastSignature = '';

  public sync(
    schema: OntologySchemaDocumentDto,
    nodesToPlace: readonly OntologyNodeSchemaDto[],
    activeCategories?: ReadonlySet<string>,
    workspace?: OntologyWorkspaceStatsDto | null,
    redirectMap?: ReadonlyMap<string, string>,
    collapsedCounts?: ReadonlyMap<string, number>
  ): OntologyGraphNode[] {
    this.schema = schema;
    this.workspace = workspace;
    this.collapsedCounts = collapsedCounts;

    const isInitial = this.bodyByKind.size === 0;
    const prevByKind = new Map(this.bodyByKind);
    const seeds = computeSeedAnchors(nodesToPlace);
    const nextBodies: SimBody[] = [];
    const nextMap = new Map<string, SimBody>();

    for (const dto of nodesToPlace) {
      const seed = seeds.get(dto.kind)!;
      const existing = prevByKind.get(dto.kind);
      if (existing) {
        existing.dto = dto;
        existing.style = seed.style;
        existing.ax = seed.ax;
        existing.ay = seed.ay;
        existing.anchorStrength = seed.anchorStrength;
        nextBodies.push(existing);
        nextMap.set(dto.kind, existing);
        continue;
      }

      const scaledAx = WORLD_CENTER_X + (seed.ax - WORLD_CENTER_X) * this.spacingFactor;
      const scaledAy = WORLD_CENTER_Y + (seed.ay - WORLD_CENTER_Y) * this.spacingFactor;
      let startX = scaledAx;
      let startY = scaledAy;
      if (!isInitial && dto.extends) {
        const parentBody = prevByKind.get(dto.extends) || nextMap.get(dto.extends);
        if (parentBody) {
          startX = parentBody.x + (scaledAx - parentBody.x) * 0.32;
          startY = parentBody.y + (scaledAy - parentBody.y) * 0.32;
        }
      }

      const body: SimBody = {
        dto,
        style: seed.style,
        x: startX,
        y: startY,
        ax: seed.ax,
        ay: seed.ay,
        vx: 0,
        vy: 0,
        fx: null,
        fy: null,
        degree: dto.outEdges.length + dto.inEdges.length,
        anchorStrength: seed.anchorStrength,
      };
      nextBodies.push(body);
      nextMap.set(dto.kind, body);
    }

    this.bodies = nextBodies;
    this.bodyByKind = nextMap;
    this.springs = buildSimSprings(schema, nextMap, activeCategories, redirectMap);

    const catKey = activeCategories ? [...activeCategories].sort().join(',') : 'all';
    const sig = `${nodesToPlace.map((n) => n.kind).join(',')}|${catKey}`;

    if (isInitial) {
      let warmAlpha = 1.0;
      for (let step = 0; step < 220; step++) {
        warmAlpha += (0 - warmAlpha) * DEFAULT_ALPHA_DECAY;
        stepForceBodies(this.bodies, this.springs, warmAlpha, this.spacingFactor, this.hubFactor);
      }
      for (const b of this.bodies) {
        b.vx = 0;
        b.vy = 0;
      }
      this.alpha = 0;
    } else if (sig !== this.lastSignature && !this.frozen) {
      this.alpha = Math.max(this.alpha, 0.42);
    }
    this.lastSignature = sig;
    return this.getNodes();
  }

  public isRunning(): boolean {
    return !this.frozen && (this.alpha >= DEFAULT_ALPHA_MIN || this.alphaTarget > 0);
  }

  public getSpacingFactor(): number {
    return this.spacingFactor;
  }

  public setSpacingFactor(factor: number): void {
    const clamped = Math.max(0.1, Math.min(2.5, Number.isFinite(factor) ? factor : 1.0));
    if (Math.abs(clamped - this.spacingFactor) < 0.001) return;
    this.spacingFactor = clamped;
    if (!this.frozen) {
      this.alpha = Math.max(this.alpha, 0.38);
    }
  }

  public getHubFactor(): number {
    return this.hubFactor;
  }

  public setHubFactor(factor: number): void {
    const clamped = Math.max(0.0, Math.min(5.0, Number.isFinite(factor) ? factor : 1.0));
    if (Math.abs(clamped - this.hubFactor) < 0.001) return;
    this.hubFactor = clamped;
    if (!this.frozen) {
      this.alpha = Math.max(this.alpha, 0.42);
    }
  }

  public isFrozen(): boolean {
    return this.frozen;
  }

  public setFrozen(frozen: boolean): void {
    this.frozen = frozen;
    if (frozen) {
      this.alpha = 0;
      this.alphaTarget = 0;
      for (const b of this.bodies) {
        b.vx = 0;
        b.vy = 0;
      }
    } else {
      for (const b of this.bodies) {
        b.fx = null;
        b.fy = null;
      }
      this.alpha = Math.max(this.alpha, 0.28);
    }
  }

  public startDrag(kind: string): void {
    const body = this.bodyByKind.get(kind);
    if (!body) return;
    body.fx = body.x;
    body.fy = body.y;
    if (!this.frozen) {
      this.alphaTarget = 0.3;
      this.alpha = Math.max(this.alpha, 0.3);
    }
  }

  public moveDrag(kind: string, x: number, y: number): void {
    const body = this.bodyByKind.get(kind);
    if (!body) return;
    body.fx = x;
    body.fy = y;
    body.x = x;
    body.y = y;
    if (!this.frozen) {
      this.alpha = Math.max(this.alpha, 0.3);
    }
  }

  public endDrag(kind: string): void {
    const body = this.bodyByKind.get(kind);
    this.alphaTarget = 0;
    if (!body) return;
    if (!this.frozen) {
      body.fx = null;
      body.fy = null;
      this.alpha = Math.max(this.alpha, 0.42);
    }
  }

  public resetLayout(): OntologyGraphNode[] {
    const seeds = computeSeedAnchors(this.bodies.map((b) => b.dto));
    for (const b of this.bodies) {
      const seed = seeds.get(b.dto.kind);
      if (seed) {
        b.x = WORLD_CENTER_X + (seed.ax - WORLD_CENTER_X) * this.spacingFactor;
        b.y = WORLD_CENTER_Y + (seed.ay - WORLD_CENTER_Y) * this.spacingFactor;
      }
      b.vx = 0;
      b.vy = 0;
      b.fx = null;
      b.fy = null;
    }
    let warmAlpha = 1.0;
    for (let step = 0; step < 220; step++) {
      warmAlpha += (0 - warmAlpha) * DEFAULT_ALPHA_DECAY;
      stepForceBodies(this.bodies, this.springs, warmAlpha, this.spacingFactor, this.hubFactor);
    }
    for (const b of this.bodies) {
      b.vx = 0;
      b.vy = 0;
    }
    this.alpha = 0;
    this.alphaTarget = 0;
    return this.getNodes();
  }

  public tick(): boolean {
    if (this.frozen) return false;
    this.alpha += (this.alphaTarget - this.alpha) * DEFAULT_ALPHA_DECAY;
    if (this.alpha < DEFAULT_ALPHA_MIN && this.alphaTarget === 0) {
      this.alpha = 0;
      return false;
    }
    stepForceBodies(this.bodies, this.springs, this.alpha, this.spacingFactor, this.hubFactor);
    return true;
  }

  public getNodes(): OntologyGraphNode[] {
    if (!this.schema) return [];
    return projectGraphNodes(
      this.bodies,
      this.bodyByKind,
      this.schema,
      this.workspace,
      this.collapsedCounts,
      false
    );
  }
}

export function buildOntologyGraphLayout(
  schema: OntologySchemaDocumentDto,
  nodesToPlace: readonly OntologyNodeSchemaDto[],
  workspace?: OntologyWorkspaceStatsDto | null,
  collapsedCounts?: ReadonlyMap<string, number>
): OntologyGraphNode[] {
  const seeds = computeSeedAnchors(nodesToPlace);
  const bodies: SimBody[] = [];
  const bodyByKind = new Map<string, SimBody>();

  for (const dto of nodesToPlace) {
    const seed = seeds.get(dto.kind)!;
    const body: SimBody = {
      dto,
      style: seed.style,
      x: seed.ax,
      y: seed.ay,
      ax: seed.ax,
      ay: seed.ay,
      vx: 0,
      vy: 0,
      fx: null,
      fy: null,
      degree: dto.outEdges.length + dto.inEdges.length,
      anchorStrength: seed.anchorStrength,
    };
    bodies.push(body);
    bodyByKind.set(dto.kind, body);
  }

  const springs = buildSimSprings(schema, bodyByKind);
  let alpha = 1.0;
  for (let step = 0; step < 220; step++) {
    alpha += (0 - alpha) * DEFAULT_ALPHA_DECAY;
    stepForceBodies(bodies, springs, alpha, 1.0);
  }

  return projectGraphNodes(bodies, bodyByKind, schema, workspace, collapsedCounts, true);
}

export function buildOntologyGraphCapsules(
  _schema: OntologySchemaDocumentDto,
  _positionedNodes: readonly OntologyGraphNode[],
  _expandedHierarchies?: ReadonlySet<string>
): OntologyGraphCapsule[] {
  return [];
}
