import {
  OntologyEdgeCategoryId,
  OntologyNodeSchemaDto,
  OntologySchemaDocumentDto,
  OntologyWorkspaceStatsDto,
} from '../../../../proto/types';
import { getWorkspaceNodeCount, getWorkspaceTripleCount } from './ontologyModel';

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
}

export interface OntologyGraphEdge {
  key: string;
  fromKind: string;
  toKind: string;
  rel: string;
  category: OntologyEdgeCategoryId;
  isSelfLoop: boolean;
  selfLoopSlot: number;
  canonicalOffset: number;
  priority: number;
  workspaceCount: number;
}

const COLOR_WORKLOAD = '#7c3aed';
const COLOR_CONTRACT = '#d97706';
const COLOR_DATA = '#16a34a';
const COLOR_CLOUD = '#0891b2';
const COLOR_SYNTAX = '#dc2626';
const COLOR_SLICING = '#4f46e5';
const COLOR_PROJECT = '#2563eb';
const COLOR_PHYSICAL = '#475569';

const NODE_RADIUS = 25;
const WORLD_WIDTH = 1280;
const WORLD_HEIGHT = 860;

const NODE_STYLE_MAP: Record<string, OntologyGraphNodeStyle> = {
  App: { fill: COLOR_WORKLOAD, glyph: 'browser', cluster: 0 },
  Service: { fill: COLOR_WORKLOAD, glyph: 'server', cluster: 0 },
  Worker: { fill: COLOR_WORKLOAD, glyph: 'gear', cluster: 0 },
  CliTool: { fill: COLOR_WORKLOAD, glyph: 'terminal', cluster: 0 },
  Library: { fill: COLOR_WORKLOAD, glyph: 'library', cluster: 0 },
  TestSuite: { fill: COLOR_WORKLOAD, glyph: 'beaker', cluster: 0 },

  Endpoint: { fill: COLOR_CONTRACT, glyph: 'endpoint', cluster: 1 },
  Topic: { fill: COLOR_CONTRACT, glyph: 'topic', cluster: 1 },
  ApiInUse: { fill: COLOR_CONTRACT, glyph: 'plug', cluster: 1 },
  PeerMesh: { fill: COLOR_CONTRACT, glyph: 'mesh', cluster: 1 },

  Database: { fill: COLOR_DATA, glyph: 'database', cluster: 2 },
  Table: { fill: COLOR_DATA, glyph: 'table', cluster: 2 },
  Query: { fill: COLOR_DATA, glyph: 'query', cluster: 2 },
  Procedure: { fill: COLOR_DATA, glyph: 'procedure', cluster: 2 },

  ExternalService: { fill: COLOR_CLOUD, glyph: 'cloud', cluster: 3 },
  CloudService: { fill: COLOR_CLOUD, glyph: 'cloudUp', cluster: 3 },

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
  { x: 265, y: 205, rx: 145, ry: 95 },  // 0: Workloads
  { x: 615, y: 175, rx: 135, ry: 75 },  // 1: Contracts & Messaging
  { x: 1030, y: 285, rx: 120, ry: 95 }, // 2: Data & SQL
  { x: 865, y: 140, rx: 95, ry: 55 },   // 3: External & Cloud
  { x: 615, y: 465, rx: 135, ry: 95 },  // 4: Syntactic UAST
  { x: 945, y: 535, rx: 115, ry: 85 },  // 5: Semantic Slicing
  { x: 295, y: 560, rx: 120, ry: 85 },  // 6: Project Boundaries
  { x: 635, y: 685, rx: 145, ry: 50 },  // 7: Physical Filesystem
];

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
  onlyActiveInWorkspace?: boolean
): OntologyGraphEdge[] {
  const rawEdges: Array<{
    fromKind: string;
    toKind: string;
    rel: string;
    category: OntologyEdgeCategoryId;
    priority: number;
    workspaceCount: number;
  }> = [];

  const filterByObserved = Boolean(
    onlyActiveInWorkspace && workspace && workspace.observedTriples && workspace.observedTriples.length > 0
  );

  for (const relDef of schema.relationships) {
    if (!activeCategories.has(relDef.category)) continue;
    for (const pair of relDef.pairs) {
      if (!visibleKindSet.has(pair.fromKind) || !visibleKindSet.has(pair.toKind)) continue;
      const wsCount = getWorkspaceTripleCount(workspace, pair.fromKind, relDef.rel, pair.toKind);
      if (filterByObserved && wsCount <= 0) continue;

      let priority = 1;
      if (relDef.rel === 'DECLARED_IN' || relDef.rel === 'BELONGS_TO') {
        priority = 4;
      } else if (SECONDARY_WORKLOAD_HUBS.has(pair.fromKind) || SECONDARY_WORKLOAD_HUBS.has(pair.toKind)) {
        priority = 3;
      } else if (pair.fromKind === 'Service' || pair.toKind === 'Service' || relDef.category === 'structural') {
        priority = 2;
      }

      rawEdges.push({
        fromKind: pair.fromKind,
        toKind: pair.toKind,
        rel: relDef.rel,
        category: relDef.category,
        priority,
        workspaceCount: wsCount,
      });
    }
  }

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

export function buildOntologyGraphLayout(
  schema: OntologySchemaDocumentDto,
  nodesToPlace: readonly OntologyNodeSchemaDto[],
  workspace?: OntologyWorkspaceStatsDto | null
): OntologyGraphNode[] {
  const hasWorkspace = Boolean(workspace && workspace.totalNodes > 0);
  const clusterBuckets = new Map<number, OntologyNodeSchemaDto[]>();

  for (const n of nodesToPlace) {
    const style = getOntologyNodeStyle(n.kind, n.layerId);
    const arr = clusterBuckets.get(style.cluster) || [];
    arr.push(n);
    clusterBuckets.set(style.cluster, arr);
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
    degree: number;
  }

  const bodies: SimBody[] = [];
  const bodyByKind = new Map<string, SimBody>();

  for (const [clusterId, bucket] of clusterBuckets) {
    const anchor = CLUSTER_ANCHORS[clusterId] || CLUSTER_ANCHORS[0];
    const count = bucket.length;
    for (let i = 0; i < count; i++) {
      const dto = bucket[i];
      const style = getOntologyNodeStyle(dto.kind, dto.layerId);
      const angle = count === 1 ? 0 : (i / count) * Math.PI * 2 - Math.PI / 2;
      const ax = anchor.x + (count === 1 ? 0 : Math.cos(angle) * anchor.rx);
      const ay = anchor.y + (count === 1 ? 0 : Math.sin(angle) * anchor.ry);
      const body: SimBody = {
        dto,
        style,
        x: ax,
        y: ay,
        ax,
        ay,
        vx: 0,
        vy: 0,
        degree: dto.outEdges.length + dto.inEdges.length,
      };
      bodies.push(body);
      bodyByKind.set(dto.kind, body);
    }
  }

  const uniqueLinks: Array<{ a: SimBody; b: SimBody; weight: number }> = [];
  const seenPairs = new Set<string>();
  for (const rel of schema.relationships) {
    for (const p of rel.pairs) {
      if (p.fromKind === p.toKind) continue;
      const ba = bodyByKind.get(p.fromKind);
      const bb = bodyByKind.get(p.toKind);
      if (!ba || !bb) continue;
      const minK = p.fromKind < p.toKind ? p.fromKind : p.toKind;
      const maxK = p.fromKind < p.toKind ? p.toKind : p.fromKind;
      const pk = `${minK}${PAIR_SEPARATOR}${maxK}`;
      if (seenPairs.has(pk)) continue;
      seenPairs.add(pk);
      const maxDeg = Math.max(ba.degree, bb.degree);
      const weight = maxDeg > 14 ? 0.16 : 0.55;
      uniqueLinks.push({ a: ba, b: bb, weight });
    }
  }

  const minSeparation = 128;
  const iterations = 180;

  for (let step = 0; step < iterations; step++) {
    const temp = 1 - step / iterations;

    for (let i = 0; i < bodies.length; i++) {
      const bi = bodies[i];
      for (let j = i + 1; j < bodies.length; j++) {
        const bj = bodies[j];
        let dx = bj.x - bi.x;
        let dy = bj.y - bi.y;
        let distSq = dx * dx + dy * dy;
        if (distSq < 1) {
          dx = 1;
          dy = 0.5;
          distSq = 1.25;
        }
        const dist = Math.sqrt(distSq);
        const repulse = 24000 / distSq;
        let force = repulse;
        if (dist < minSeparation) {
          force += (minSeparation - dist) * 0.65;
        }
        const fx = (dx / dist) * force;
        const fy = (dy / dist) * force;
        bi.vx -= fx;
        bi.vy -= fy;
        bj.vx += fx;
        bj.vy += fy;
      }
    }

    for (const link of uniqueLinks) {
      const dx = link.b.x - link.a.x;
      const dy = link.b.y - link.a.y;
      const dist = Math.sqrt(dx * dx + dy * dy) || 1;
      const ideal = link.a.style.cluster === link.b.style.cluster ? 148 : 240;
      const delta = (dist - ideal) * 0.018 * link.weight;
      const fx = (dx / dist) * delta;
      const fy = (dy / dist) * delta;
      link.a.vx += fx;
      link.a.vy += fy;
      link.b.vx -= fx;
      link.b.vy -= fy;
    }

    for (const b of bodies) {
      b.vx += (b.ax - b.x) * 0.055;
      b.vy += (b.ay - b.y) * 0.055;

      const speed = Math.sqrt(b.vx * b.vx + b.vy * b.vy);
      const maxStep = 18 * temp + 1.5;
      if (speed > maxStep) {
        b.vx = (b.vx / speed) * maxStep;
        b.vy = (b.vy / speed) * maxStep;
      }
      b.x = Math.max(75, Math.min(WORLD_WIDTH - 75, b.x + b.vx));
      b.y = Math.max(70, Math.min(WORLD_HEIGHT - 70, b.y + b.vy));
      b.vx *= 0.55;
      b.vy *= 0.55;
    }
  }

  return bodies.map((b) => {
    const rawCount = hasWorkspace
      ? getWorkspaceNodeCount(workspace, b.dto.kind)
      : b.dto.properties.length;
    return {
      kind: b.dto.kind,
      pluralLabel: b.dto.pluralLabel,
      layerId: b.dto.layerId,
      x: Math.round(b.x * 10) / 10,
      y: Math.round(b.y * 10) / 10,
      radius: NODE_RADIUS,
      fill: b.style.fill,
      glyph: b.style.glyph,
      cluster: b.style.cluster,
      badgeText: formatCompactCount(rawCount),
      rawCount,
      propertyCount: b.dto.properties.length,
    };
  });
}
