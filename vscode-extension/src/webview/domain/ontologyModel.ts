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
}

export interface OntologyFilterState {
  searchQuery: string;
  activeCategories: ReadonlySet<string>;
  onlyActiveInWorkspace: boolean;
  activeLayers?: ReadonlySet<number>;
}

export function getWorkspaceNodeCount(
  workspace: OntologyWorkspaceStatsDto | null | undefined,
  kind: string
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
  return 0;
}

export function getWorkspaceTripleCount(
  workspace: OntologyWorkspaceStatsDto | null | undefined,
  fromKind: string,
  rel: string,
  toKind: string
): number {
  if (!workspace?.observedTriples) return 0;
  const f = fromKind.toLowerCase();
  const r = rel.toLowerCase();
  const t = toKind.toLowerCase();
  let sum = 0;
  for (const item of workspace.observedTriples) {
    if (
      item.fromKind.toLowerCase() === f &&
      item.rel.toLowerCase() === r &&
      item.toKind.toLowerCase() === t
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

    if (hasWorkspaceFilter && getWorkspaceNodeCount(workspace, node.kind) <= 0) {
      return false;
    }

    if (!q) return true;

    if (
      node.kind.toLowerCase().includes(q) ||
      node.pluralLabel.toLowerCase().includes(q) ||
      node.purpose.toLowerCase().includes(q) ||
      node.idScheme.toLowerCase().includes(q)
    ) {
      return true;
    }

    if (node.properties.some((p) => p.name.toLowerCase().includes(q) || p.description.toLowerCase().includes(q))) {
      return true;
    }

    if (
      node.outEdges.some((e) => e.rel.toLowerCase().includes(q) || e.toKind.toLowerCase().includes(q)) ||
      node.inEdges.some((e) => e.rel.toLowerCase().includes(q) || e.fromKind.toLowerCase().includes(q))
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

  for (const edge of node.outEdges) {
    if (activeCategories.size > 0 && !activeCategories.has(edge.category)) {
      continue;
    }
    const workspaceCount = getWorkspaceTripleCount(workspace, node.kind, edge.rel, edge.toKind);
    connections.push({
      peerKind: edge.toKind,
      direction: 'out',
      rel: edge.rel,
      category: edge.category,
      description: edge.description,
      workspaceCount,
    });
    const list = badgesByKind.get(edge.toKind) || [];
    list.push({ direction: 'out', rel: edge.rel, category: edge.category });
    badgesByKind.set(edge.toKind, list);
  }

  for (const edge of node.inEdges) {
    if (activeCategories.size > 0 && !activeCategories.has(edge.category)) {
      continue;
    }
    const workspaceCount = getWorkspaceTripleCount(workspace, edge.fromKind, edge.rel, node.kind);
    connections.push({
      peerKind: edge.fromKind,
      direction: 'in',
      rel: edge.rel,
      category: edge.category,
      description: edge.description,
      workspaceCount,
    });
    const list = badgesByKind.get(edge.fromKind) || [];
    list.push({ direction: 'in', rel: edge.rel, category: edge.category });
    badgesByKind.set(edge.fromKind, list);
  }

  return { connections, badgesByKind, relationshipMeta };
}
