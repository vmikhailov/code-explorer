import bundledSchemaJson from '../../../../proto/ontology-schema.json';
import {
  OntologySchemaDocumentDto,
  OntologyNodeSchemaDto,
  OntologyRelationshipSchemaDto,
  OntologyWorkspaceStatsDto,
  OntologyEdgeCategoryId,
} from '../../../../proto/types';

export const BUNDLED_ONTOLOGY_SCHEMA: OntologySchemaDocumentDto =
  bundledSchemaJson as OntologySchemaDocumentDto;

export interface OntologyNeighborConnection {
  peerKind: string;
  direction: 'out' | 'in';
  rel: string;
  category: OntologyEdgeCategoryId;
  description: string;
  workspaceCount: number;
  inheritedFrom?: string;
}

export interface OntologyHierarchyDescriptor {
  kind: string;
  extends?: string;
  directSubtypes: string[];
  allDescendants: string[];
  depth: number;
}

export interface OntologyHierarchyResolution {
  visibleNodes: OntologyNodeSchemaDto[];
  redirectMap: Map<string, string>;
  collapsedCounts: Map<string, number>;
  hierarchies: OntologyHierarchyDescriptor[];
}

export interface OntologyFilterState {
  searchQuery: string;
  activeCategories: ReadonlySet<string>;
  onlyActiveInWorkspace: boolean;
  activeLayers?: ReadonlySet<number>;
  expandedHierarchies?: ReadonlySet<string>;
}

export function getOntologyHierarchies(
  schema: OntologySchemaDocumentDto
): OntologyHierarchyDescriptor[] {
  const byKind = new Map<string, OntologyNodeSchemaDto>();
  for (const n of schema.nodes) {
    byKind.set(n.kind, n);
  }

  const collectDescendants = (kind: string, visited = new Set<string>()): string[] => {
    const node = byKind.get(kind);
    if (!node?.subtypes || node.subtypes.length === 0) return [];
    const acc: string[] = [];
    for (const sub of node.subtypes) {
      if (visited.has(sub)) continue;
      visited.add(sub);
      acc.push(sub);
      acc.push(...collectDescendants(sub, visited));
    }
    return acc;
  };

  const computeDepth = (kind: string): number => {
    let d = 0;
    let cur = byKind.get(kind);
    const guard = new Set<string>();
    while (cur?.extends && !guard.has(cur.extends)) {
      guard.add(cur.extends);
      d++;
      cur = byKind.get(cur.extends);
    }
    return d;
  };

  const result: OntologyHierarchyDescriptor[] = [];
  for (const node of schema.nodes) {
    if (node.subtypes && node.subtypes.length > 0) {
      result.push({
        kind: node.kind,
        extends: node.extends,
        directSubtypes: [...node.subtypes],
        allDescendants: collectDescendants(node.kind),
        depth: computeDepth(node.kind),
      });
    }
  }

  result.sort((a, b) => a.depth - b.depth || a.kind.localeCompare(b.kind));
  return result;
}

export function getDefaultExpandedHierarchies(
  schema: OntologySchemaDocumentDto
): Set<string> {
  return new Set(getOntologyHierarchies(schema).map((h) => h.kind));
}

export function resolveHierarchyVisibility(
  schema: OntologySchemaDocumentDto,
  filteredNodes: readonly OntologyNodeSchemaDto[],
  expandedHierarchies?: ReadonlySet<string>
): OntologyHierarchyResolution {
  const hierarchies = getOntologyHierarchies(schema);
  const redirectMap = new Map<string, string>();
  const collapsedCounts = new Map<string, number>();

  if (!expandedHierarchies) {
    return {
      visibleNodes: [...filteredNodes],
      redirectMap,
      collapsedCounts,
      hierarchies,
    };
  }

  const filteredSet = new Set(filteredNodes.map((n) => n.kind));
  const hiddenKinds = new Set<string>();

  // Process outer-to-inner (depth ascending) so outer collapsed supertype claims all nested descendants
  for (const h of hierarchies) {
    if (expandedHierarchies.has(h.kind)) continue;
    if (!filteredSet.has(h.kind) || hiddenKinds.has(h.kind)) continue;

    let count = 0;
    for (const desc of h.allDescendants) {
      if (filteredSet.has(desc) && !hiddenKinds.has(desc)) {
        hiddenKinds.add(desc);
        count++;
      }
      if (!redirectMap.has(desc)) {
        redirectMap.set(desc, h.kind);
      }
    }
    if (count > 0) {
      collapsedCounts.set(h.kind, count);
    }
  }

  const visibleNodes = filteredNodes.filter((n) => !hiddenKinds.has(n.kind));
  return {
    visibleNodes,
    redirectMap,
    collapsedCounts,
    hierarchies,
  };
}

export function getWorkspaceNodeCount(
  workspace: OntologyWorkspaceStatsDto | null | undefined,
  kind: string,
  schema?: OntologySchemaDocumentDto
): number {
  if (!workspace?.nodeCounts) return 0;
  if (typeof workspace.nodeCounts[kind] === 'number') {
    return workspace.nodeCounts[kind];
  }
  const lower = kind.toLowerCase();
  for (const [k, val] of Object.entries(workspace.nodeCounts)) {
    if (k.toLowerCase() === lower) {
      return val;
    }
  }
  if (schema) {
    const node = schema.nodes.find((n) => n.kind.toLowerCase() === lower);
    if (node?.subtypes && node.subtypes.length > 0) {
      let sum = 0;
      for (const sub of node.subtypes) {
        sum += getWorkspaceNodeCount(workspace, sub, schema);
      }
      return sum;
    }
  }
  return 0;
}

function expandKindToConcreteLowerSet(
  kind: string,
  schema?: OntologySchemaDocumentDto
): Set<string> {
  const result = new Set<string>([kind.toLowerCase()]);
  if (!schema) return result;
  const byLower = new Map<string, OntologyNodeSchemaDto>();
  for (const n of schema.nodes) {
    byLower.set(n.kind.toLowerCase(), n);
  }
  const visit = (k: string) => {
    const node = byLower.get(k);
    if (!node?.subtypes) return;
    for (const sub of node.subtypes) {
      const low = sub.toLowerCase();
      if (!result.has(low)) {
        result.add(low);
        visit(low);
      }
    }
  };
  visit(kind.toLowerCase());
  return result;
}

export function getWorkspaceTripleCount(
  workspace: OntologyWorkspaceStatsDto | null | undefined,
  fromKind: string,
  rel: string,
  toKind: string,
  schema?: OntologySchemaDocumentDto
): number {
  if (!workspace?.observedTriples) return 0;
  const fromSet = expandKindToConcreteLowerSet(fromKind, schema);
  const r = rel.toLowerCase();
  const toSet = expandKindToConcreteLowerSet(toKind, schema);
  let sum = 0;
  for (const item of workspace.observedTriples) {
    if (
      fromSet.has(item.fromKind.toLowerCase()) &&
      item.rel.toLowerCase() === r &&
      toSet.has(item.toKind.toLowerCase())
    ) {
      sum += item.count;
    }
  }
  return sum;
}

export function filterOntologyNodes(
  schema: OntologySchemaDocumentDto,
  filter: OntologyFilterState,
  workspace?: OntologyWorkspaceStatsDto | null
): OntologyNodeSchemaDto[] {
  const q = filter.searchQuery.trim().toLowerCase();
  const hasWorkspaceFilter = Boolean(filter.onlyActiveInWorkspace && workspace && workspace.totalNodes > 0);

  return schema.nodes.filter((node) => {
    if (filter.activeLayers && !filter.activeLayers.has(node.layerId)) {
      return false;
    }

    if (hasWorkspaceFilter && getWorkspaceNodeCount(workspace, node.kind, schema) <= 0) {
      return false;
    }

    if (!q) return true;

    if (
      node.kind.toLowerCase().includes(q) ||
      node.pluralLabel.toLowerCase().includes(q) ||
      node.purpose.toLowerCase().includes(q) ||
      node.idScheme.toLowerCase().includes(q) ||
      (node.extends && node.extends.toLowerCase().includes(q))
    ) {
      return true;
    }

    if (node.properties.some((p) => p.name.toLowerCase().includes(q) || p.description.toLowerCase().includes(q))) {
      return true;
    }

    const allOut = [...node.outEdges, ...(node.inheritedOutEdges || [])];
    const allIn = [...node.inEdges, ...(node.inheritedInEdges || [])];
    if (
      allOut.some((e) => e.rel.toLowerCase().includes(q) || e.toKind.toLowerCase().includes(q)) ||
      allIn.some((e) => e.rel.toLowerCase().includes(q) || e.fromKind.toLowerCase().includes(q))
    ) {
      return true;
    }

    return false;
  });
}

export function computeEgoNeighborhood(
  schema: OntologySchemaDocumentDto,
  selectedKind: string | null,
  activeCategories: ReadonlySet<string>,
  workspace?: OntologyWorkspaceStatsDto | null
): {
  connections: OntologyNeighborConnection[];
  badgesByKind: Map<string, Array<{ direction: 'out' | 'in'; rel: string; category: string }>>;
  relationshipMeta: Map<string, OntologyRelationshipSchemaDto>;
} {
  const badgesByKind = new Map<string, Array<{ direction: 'out' | 'in'; rel: string; category: string }>>();
  const relationshipMeta = new Map<string, OntologyRelationshipSchemaDto>();
  for (const r of schema.relationships) {
    relationshipMeta.set(r.rel, r);
  }

  if (!selectedKind) {
    return { connections: [], badgesByKind, relationshipMeta };
  }

  const node = schema.nodes.find((n) => n.kind === selectedKind);
  if (!node) {
    return { connections: [], badgesByKind, relationshipMeta };
  }

  const connections: OntologyNeighborConnection[] = [];
  const seenOut = new Set<string>();
  const seenIn = new Set<string>();

  for (const edge of [...node.outEdges, ...(node.inheritedOutEdges || [])]) {
    if (activeCategories.size > 0 && !activeCategories.has(edge.category)) {
      continue;
    }
    const dedup = `${edge.rel}->${edge.toKind}`;
    if (seenOut.has(dedup)) continue;
    seenOut.add(dedup);

    const workspaceCount = getWorkspaceTripleCount(workspace, node.kind, edge.rel, edge.toKind, schema);
    connections.push({
      peerKind: edge.toKind,
      direction: 'out',
      rel: edge.rel,
      category: edge.category,
      description: edge.description,
      workspaceCount,
      inheritedFrom: edge.inheritedFrom,
    });
    const list = badgesByKind.get(edge.toKind) || [];
    list.push({ direction: 'out', rel: edge.rel, category: edge.category });
    badgesByKind.set(edge.toKind, list);
  }

  for (const edge of [...node.inEdges, ...(node.inheritedInEdges || [])]) {
    if (activeCategories.size > 0 && !activeCategories.has(edge.category)) {
      continue;
    }
    const dedup = `${edge.fromKind}->${edge.rel}`;
    if (seenIn.has(dedup)) continue;
    seenIn.add(dedup);

    const workspaceCount = getWorkspaceTripleCount(workspace, edge.fromKind, edge.rel, node.kind, schema);
    connections.push({
      peerKind: edge.fromKind,
      direction: 'in',
      rel: edge.rel,
      category: edge.category,
      description: edge.description,
      workspaceCount,
      inheritedFrom: edge.inheritedFrom,
    });
    const list = badgesByKind.get(edge.fromKind) || [];
    list.push({ direction: 'in', rel: edge.rel, category: edge.category });
    badgesByKind.set(edge.fromKind, list);
  }

  return { connections, badgesByKind, relationshipMeta };
}
