import { GraphData, GraphEdge, GraphNode } from '../../../../proto/types';

export interface ProjectMapProjectOption {
  id: string;
  name: string;
  path: string;
  files: number;
}

export interface ProjectMapTierInfo {
  tier: number;
  label: string;
  description: string;
}

export interface ProjectMapSample {
  from?: string | null;
  to?: string | null;
  file: string;
  line?: number | null;
  kind: string;
}

export interface ProjectMapNodeModel {
  id: string;
  name: string;
  isResource: boolean;
  resourceKind?: string;
  tier: number;
  tierLabel: string;
  tierReason: string;
  path: string;
  filePath?: string;
  fileCount: number;
  lineCount: number;
  fanIn: number;
  fanOut: number;
  instability: number;
  entryPoints: number;
  dataFileCount: number;
  cycleGroup?: number;
  isRootFiles: boolean;
  isMerged: boolean;
  files: string[];
}

export interface ProjectMapEdgeModel {
  id: string;
  source: string;
  target: string;
  kind: string;
  count: number;
  distinctFiles: number;
  kinds: Record<string, number>;
  samples: ProjectMapSample[];
  isCircular: boolean;
  isResourceLink: boolean;
}

export interface ProjectMapModel {
  projectId: string;
  projectName: string;
  projectPath: string;
  sourceRoot: string;
  granularity: string;
  error?: string;
  warnings: string[];
  projects: ProjectMapProjectOption[];
  tiers: ProjectMapTierInfo[];
  cycles: string[][];
  testFilesExcluded: number;
  nodes: ProjectMapNodeModel[];
  edges: ProjectMapEdgeModel[];
}

export const FOUNDATION_TIER = 4;
export const RESOURCE_TIER = 5;

export const TIER_COLORS: Record<number, string> = {
  0: '#4fc3f7',
  1: '#ba68c8',
  2: '#81c784',
  3: '#ffb74d',
  4: '#90a4ae',
  5: '#e57373',
};

function parseJson<T>(raw: string | undefined, fallback: T): T {
  if (!raw) return fallback;
  try {
    return JSON.parse(raw) as T;
  } catch {
    return fallback;
  }
}

function num(raw: string | undefined, fallback = 0): number {
  const v = raw === undefined ? NaN : Number(raw);
  return Number.isFinite(v) ? v : fallback;
}

function toNode(n: GraphNode): ProjectMapNodeModel {
  const p = n.properties ?? {};
  const isResource = p.pm_type === 'resource';
  return {
    id: n.id,
    name: n.name,
    isResource,
    resourceKind: isResource ? p.resource_kind || n.kind : undefined,
    tier: num(p.tier, isResource ? RESOURCE_TIER : 2),
    tierLabel: p.tier_label ?? '',
    tierReason: p.tier_reason ?? '',
    path: p.path ?? '',
    filePath: n.filePath,
    fileCount: num(p.file_count),
    lineCount: num(p.line_count),
    fanIn: num(p.fan_in),
    fanOut: num(p.fan_out),
    instability: num(p.instability, 0.5),
    entryPoints: num(p.entry_points),
    dataFileCount: num(p.data_file_count),
    cycleGroup: p.cycle_group ? num(p.cycle_group) : undefined,
    isRootFiles: p.is_root_files === 'true',
    isMerged: p.is_merged === 'true',
    files: parseJson<string[]>(p.files, []),
  };
}

function toEdge(e: GraphEdge): ProjectMapEdgeModel {
  const p = e.properties ?? {};
  return {
    id: e.id ?? `${e.source}->${e.target}`,
    source: e.source,
    target: e.target,
    kind: e.kind,
    count: num(p.call_count, 1),
    distinctFiles: num(p.distinct_files),
    kinds: parseJson<Record<string, number>>(p.kinds, {}),
    samples: parseJson<ProjectMapSample[]>(p.samples, []),
    isCircular: p.is_circular === 'true',
    isResourceLink: p.is_resource_link === 'true',
  };
}

export function parseProjectMapGraph(graph: GraphData | null | undefined): ProjectMapModel {
  const m = graph?.metadata ?? {};
  return {
    projectId: m.project_id ?? '',
    projectName: m.project_name ?? '',
    projectPath: m.project_path ?? '',
    sourceRoot: m.source_root ?? '',
    granularity: m.granularity ?? 'module',
    error: m.error || undefined,
    warnings: parseJson<string[]>(m.warnings, []),
    projects: parseJson<ProjectMapProjectOption[]>(m.projects, []),
    tiers: parseJson<ProjectMapTierInfo[]>(m.tiers, []),
    cycles: parseJson<string[][]>(m.cycles, []),
    testFilesExcluded: num(m.test_files_excluded),
    nodes: (graph?.nodes ?? []).map(toNode),
    edges: (graph?.edges ?? []).map(toEdge),
  };
}

export interface ProjectMapFilters {
  hideUtilities: boolean;
  hideResources: boolean;
  cyclesOnly: boolean;
}

/**
 * Applies HUD filters. Edges are kept only when both endpoints remain visible.
 */
export function filterProjectMap(
  model: ProjectMapModel,
  filters: ProjectMapFilters
): { nodes: ProjectMapNodeModel[]; edges: ProjectMapEdgeModel[] } {
  const nodes = model.nodes.filter((n) => {
    if (filters.hideResources && n.isResource) return false;
    if (filters.hideUtilities && !n.isResource && n.tier === FOUNDATION_TIER) return false;
    if (filters.cyclesOnly && (n.isResource || n.cycleGroup === undefined)) return false;
    return true;
  });
  const visible = new Set(nodes.map((n) => n.id));
  const edges = model.edges.filter((e) => {
    if (!visible.has(e.source) || !visible.has(e.target)) return false;
    if (filters.cyclesOnly && !e.isCircular) return false;
    return true;
  });
  return { nodes, edges };
}

export function nodeVisualRadius(n: ProjectMapNodeModel): number {
  if (n.isResource) return 14;
  return Math.max(14, Math.min(34, 10 + Math.sqrt(Math.max(1, n.fileCount)) * 3));
}

export function resourceBadge(kind: string | undefined): string {
  switch ((kind ?? '').toLowerCase()) {
    case 'table':
      return 'TBL';
    case 'database':
      return 'DB';
    case 'topic':
      return 'MSG';
    case 'externalservice':
      return 'EXT';
    case 'cloudservice':
      return 'CLD';
    case 'procedure':
      return 'SP';
    default:
      return 'RES';
  }
}
