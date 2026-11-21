import { DiagramDefinition, DiagramPresetKey } from './diagramRegistry';

export interface SessionRawNode {
  id: string;
  kind: string;
  name: string;
  displayName?: string;
  filePath?: string;
  lineStart?: number;
  lineEnd?: number;
  parentId?: string;
  properties?: Record<string, string>;
}

export interface SessionRawEdge {
  id: string;
  source: string;
  target: string;
  kind: string;
  category?: string;
  isTransitive?: boolean;
  isInferred?: boolean;
  properties?: Record<string, string>;
}

export interface SessionGraphData {
  nodes: SessionRawNode[];
  edges: SessionRawEdge[];
  metadata?: Record<string, string>;
}

export interface SessionGateViolationOverlay {
  gateId: string;
  severity: 'error' | 'warning' | 'info';
  sourceNodeId?: string;
  targetNodeId?: string;
  filePath?: string;
  message: string;
}

export interface SessionGroupSpec {
  id: string;
  name: string;
  nodeIds: string[];
  collapsed?: boolean;
}

export interface FocusedNeighborhoodState {
  anchorNodeId: string;
  hops: number;
}

export interface SessionSelectionState {
  nodeId?: string;
  edgeId?: string;
  groupId?: string;
}

export interface SessionPersistedState {
  presets: Partial<Record<DiagramPresetKey, boolean>>;
  hiddenEntityTypes: string[];
  hiddenNodeIds: string[];
  groups: SessionGroupSpec[];
  focusedNeighborhood?: FocusedNeighborhoodState | null;
}

export interface ProjectedGroupMetrics {
  memberCount: number;
  inboundCount: number;
  outboundCount: number;
  cycleCount: number;
  dbCount: number;
}

export interface ProjectedGroup {
  id: string;
  name: string;
  nodeIds: string[];
  collapsed: boolean;
  metrics: ProjectedGroupMetrics;
}

export interface BundledProjectionEdge {
  id: string;
  source: string;
  target: string;
  totalCount: number;
  category: string;
  isBidirectional: boolean;
  inCycle: boolean;
  violatedGateIds: string[];
  items: SessionRawEdge[];
}

export interface RenderProjection {
  nodes: SessionRawNode[];
  groups: ProjectedGroup[];
  edges: BundledProjectionEdge[];
  violatedGateIdsByNode: Record<string, string[]>;
}

export interface EntityTypeSummary {
  kind: string;
  totalCount: number;
  visibleCount: number;
  hiddenCount: number;
  isHiddenByType: boolean;
}

export interface InspectorSnapshot {
  diagramId: string;
  totalNodesInCatalog: number;
  visibleNodeCount: number;
  hiddenNodeCount: number;
  entityTypeSummaries: EntityTypeSummary[];
  groups: ProjectedGroup[];
  selection: SessionSelectionState;
  selectedNode?: SessionRawNode;
  selectedEdge?: BundledProjectionEdge;
  selectedGroup?: ProjectedGroup;
  inboundEdges: BundledProjectionEdge[];
  outboundEdges: BundledProjectionEdge[];
  activeGateViolations: SessionGateViolationOverlay[];
}

export interface GraphSessionSnapshot {
  projection: RenderProjection;
  inspector: InspectorSnapshot;
}

const DB_NODE_KINDS = new Set(['Database', 'Table', 'DataSet', 'DbSchema', 'Topic']);

/**
 * Headless 5-stage deterministic diagram pipeline:
 * Stage 1: FullCatalogIndex (immutable raw catalog; never shrunk by filters)
 * Stage 2: PresetFilter (cyclesOnly, sharedDatabasesOnly, includeLibraries, includeTests)
 * Stage 3: VisibilityFilter (hiddenEntityTypes, hiddenNodeIds, focusedNeighborhood)
 * Stage 4: GroupingAndBundling (groups, aggregated metrics, parallel edge bundling, gate overlays)
 * Stage 5: RenderProjection + InspectorSnapshot
 */
export class GraphSessionStore {
  private readonly definition: DiagramDefinition;
  private allNodesById = new Map<string, SessionRawNode>();
  private allEdgesById = new Map<string, SessionRawEdge>();
  private entityTypeCounts = new Map<string, number>();

  private presets: Partial<Record<DiagramPresetKey, boolean>>;
  private hiddenEntityTypes = new Set<string>();
  private hiddenNodeIds = new Set<string>();
  private groups: SessionGroupSpec[] = [];
  private focusedNeighborhood: FocusedNeighborhoodState | null = null;
  private selection: SessionSelectionState = {};
  private gateViolations: SessionGateViolationOverlay[] = [];

  constructor(definition: DiagramDefinition, initialData?: SessionGraphData) {
    this.definition = definition;
    this.presets = { ...definition.defaultPresets };
    if (initialData) {
      this.setGraphData(initialData);
    }
  }

  public setGraphData(data: SessionGraphData): GraphSessionSnapshot {
    this.allNodesById = new Map();
    this.allEdgesById = new Map();
    this.entityTypeCounts = new Map();

    for (const node of data.nodes) {
      this.allNodesById.set(node.id, node);
      this.entityTypeCounts.set(node.kind, (this.entityTypeCounts.get(node.kind) ?? 0) + 1);
    }

    for (const edge of data.edges) {
      if (this.allNodesById.has(edge.source) && this.allNodesById.has(edge.target)) {
        const edgeId = edge.id || `${edge.source}->${edge.target}:${edge.kind}`;
        this.allEdgesById.set(edgeId, { ...edge, id: edgeId });
      }
    }

    return this.computeSnapshot();
  }

  public setPreset(key: DiagramPresetKey, enabled: boolean): GraphSessionSnapshot {
    this.presets = { ...this.presets, [key]: enabled };
    return this.computeSnapshot();
  }

  public toggleEntityTypeVisibility(kind: string): GraphSessionSnapshot {
    if (this.hiddenEntityTypes.has(kind)) {
      this.hiddenEntityTypes.delete(kind);
    } else {
      this.hiddenEntityTypes.add(kind);
    }
    return this.computeSnapshot();
  }

  public setNodeHidden(nodeId: string, hidden: boolean): GraphSessionSnapshot {
    if (hidden) {
      this.hiddenNodeIds.add(nodeId);
    } else {
      this.hiddenNodeIds.delete(nodeId);
    }
    return this.computeSnapshot();
  }

  public resetVisibility(): GraphSessionSnapshot {
    this.hiddenEntityTypes.clear();
    this.hiddenNodeIds.clear();
    this.focusedNeighborhood = null;
    return this.computeSnapshot();
  }

  public setFocusedNeighborhood(focus: FocusedNeighborhoodState | null): GraphSessionSnapshot {
    this.focusedNeighborhood = focus;
    return this.computeSnapshot();
  }

  public upsertGroup(group: SessionGroupSpec): GraphSessionSnapshot {
    const idx = this.groups.findIndex((g) => g.id === group.id);
    if (idx >= 0) {
      this.groups[idx] = { ...group, nodeIds: [...new Set(group.nodeIds)] };
    } else {
      this.groups.push({ ...group, nodeIds: [...new Set(group.nodeIds)] });
    }
    return this.computeSnapshot();
  }

  public removeGroup(groupId: string): GraphSessionSnapshot {
    this.groups = this.groups.filter((g) => g.id !== groupId);
    if (this.selection.groupId === groupId) {
      this.selection = {};
    }
    return this.computeSnapshot();
  }

  public setSelection(selection: SessionSelectionState): GraphSessionSnapshot {
    this.selection = { ...selection };
    return this.computeSnapshot();
  }

  public setGateViolations(violations: SessionGateViolationOverlay[]): GraphSessionSnapshot {
    this.gateViolations = [...violations];
    return this.computeSnapshot();
  }

  public exportPersistedState(): SessionPersistedState {
    return {
      presets: { ...this.presets },
      hiddenEntityTypes: [...this.hiddenEntityTypes].sort(),
      hiddenNodeIds: [...this.hiddenNodeIds].sort(),
      groups: this.groups.map((g) => ({ ...g, nodeIds: [...g.nodeIds] })),
      focusedNeighborhood: this.focusedNeighborhood ? { ...this.focusedNeighborhood } : null,
    };
  }

  public applyPersistedState(state: SessionPersistedState): GraphSessionSnapshot {
    this.presets = { ...this.definition.defaultPresets, ...state.presets };
    this.hiddenEntityTypes = new Set(state.hiddenEntityTypes ?? []);
    this.hiddenNodeIds = new Set(state.hiddenNodeIds ?? []);
    this.groups = (state.groups ?? []).map((g) => ({ ...g, nodeIds: [...g.nodeIds] }));
    this.focusedNeighborhood = state.focusedNeighborhood ? { ...state.focusedNeighborhood } : null;
    return this.computeSnapshot();
  }

  public computeSnapshot(): GraphSessionSnapshot {
    const allNodes = [...this.allNodesById.values()];
    const allEdges = [...this.allEdgesById.values()];

    // Stage 2: PresetFilter
    const presetVisibleIds = new Set<string>();
    for (const node of allNodes) {
      if (this.passesPresetFilter(node)) {
        presetVisibleIds.add(node.id);
      }
    }

    if (this.presets.sharedDatabasesOnly) {
      const sharedIds = new Set(
        allNodes
          .filter((n) => n.properties?.is_shared === 'true')
          .map((n) => n.id)
      );
      const keep = new Set(sharedIds);
      for (const edge of allEdges) {
        if (sharedIds.has(edge.target)) keep.add(edge.source);
        if (sharedIds.has(edge.source)) keep.add(edge.target);
      }
      for (const id of [...presetVisibleIds]) {
        if (!keep.has(id)) presetVisibleIds.delete(id);
      }
    }

    // Stage 3: VisibilityFilter
    let neighborhoodIds: Set<string> | null = null;
    if (this.focusedNeighborhood && this.allNodesById.has(this.focusedNeighborhood.anchorNodeId)) {
      neighborhoodIds = this.collectNeighborhood(
        this.focusedNeighborhood.anchorNodeId,
        Math.max(1, this.focusedNeighborhood.hops),
        allEdges
      );
    }

    const visibleNodes: SessionRawNode[] = [];
    const visibleNodeIds = new Set<string>();
    for (const node of allNodes) {
      if (!presetVisibleIds.has(node.id)) continue;
      if (this.hiddenEntityTypes.has(node.kind)) continue;
      if (this.hiddenNodeIds.has(node.id)) continue;
      if (neighborhoodIds && !neighborhoodIds.has(node.id)) continue;

      visibleNodes.push(node);
      visibleNodeIds.add(node.id);
    }

    const visibleRawEdges = allEdges.filter(
      (e) => visibleNodeIds.has(e.source) && visibleNodeIds.has(e.target)
    );

    // Stage 4: GroupingAndBundling + Gate Overlays
    const nodeToCollapsedGroup = new Map<string, string>();
    for (const group of this.groups) {
      if (!group.collapsed) continue;
      for (const nid of group.nodeIds) {
        if (visibleNodeIds.has(nid)) {
          nodeToCollapsedGroup.set(nid, group.id);
        }
      }
    }

    const violatedGateIdsByNode: Record<string, string[]> = {};
    const violatedGateIdsByPair = new Map<string, Set<string>>();
    for (const v of this.gateViolations) {
      if (v.sourceNodeId) {
        (violatedGateIdsByNode[v.sourceNodeId] ??= []).push(v.gateId);
      }
      if (v.targetNodeId) {
        (violatedGateIdsByNode[v.targetNodeId] ??= []).push(v.gateId);
      }
      if (v.sourceNodeId && v.targetNodeId) {
        const key = `${v.sourceNodeId}->${v.targetNodeId}`;
        if (!violatedGateIdsByPair.has(key)) violatedGateIdsByPair.set(key, new Set());
        violatedGateIdsByPair.get(key)!.add(v.gateId);
      }
    }

    const bundledMap = new Map<string, BundledProjectionEdge>();
    for (const edge of visibleRawEdges) {
      const effSrc = nodeToCollapsedGroup.get(edge.source) ?? edge.source;
      const effTgt = nodeToCollapsedGroup.get(edge.target) ?? edge.target;
      if (effSrc === effTgt) continue;

      const pairKey = `${effSrc}->${effTgt}`;
      const gateSet = violatedGateIdsByPair.get(`${edge.source}->${edge.target}`);
      let bundle = bundledMap.get(pairKey);
      if (!bundle) {
        bundle = {
          id: `bundle:${pairKey}`,
          source: effSrc,
          target: effTgt,
          totalCount: 0,
          category: edge.category ?? edge.kind,
          isBidirectional: false,
          inCycle: false,
          violatedGateIds: [],
          items: [],
        };
        bundledMap.set(pairKey, bundle);
      }

      bundle.totalCount += 1;
      bundle.items.push(edge);
      if (bundle.category !== (edge.category ?? edge.kind)) {
        bundle.category = 'mixed';
      }
      if (edge.properties?.in_cycle === 'true') {
        bundle.inCycle = true;
      }
      if (gateSet) {
        for (const gid of gateSet) {
          if (!bundle.violatedGateIds.includes(gid)) {
            bundle.violatedGateIds.push(gid);
          }
        }
      }
    }

    for (const bundle of bundledMap.values()) {
      const reverseKey = `${bundle.target}->${bundle.source}`;
      if (bundledMap.has(reverseKey)) {
        bundle.isBidirectional = true;
      }
    }

    const bundledEdges = [...bundledMap.values()];

    const projectedGroups: ProjectedGroup[] = this.groups.map((group) => {
      const memberSet = new Set(group.nodeIds.filter((id) => visibleNodeIds.has(id)));
      let inboundCount = 0;
      let outboundCount = 0;
      let cycleCount = 0;
      let dbCount = 0;

      for (const nid of memberSet) {
        const node = this.allNodesById.get(nid);
        if (node?.properties?.in_cycle === 'true') cycleCount += 1;
        if (node && DB_NODE_KINDS.has(node.kind)) dbCount += 1;
      }

      for (const edge of visibleRawEdges) {
        const srcIn = memberSet.has(edge.source);
        const tgtIn = memberSet.has(edge.target);
        if (!srcIn && tgtIn) inboundCount += 1;
        if (srcIn && !tgtIn) outboundCount += 1;
      }

      return {
        id: group.id,
        name: group.name,
        nodeIds: [...memberSet],
        collapsed: Boolean(group.collapsed),
        metrics: {
          memberCount: memberSet.size,
          inboundCount,
          outboundCount,
          cycleCount,
          dbCount,
        },
      };
    });

    // Stage 5: RenderProjection & InspectorSnapshot
    const visibleByKind = new Map<string, number>();
    for (const node of visibleNodes) {
      visibleByKind.set(node.kind, (visibleByKind.get(node.kind) ?? 0) + 1);
    }

    // FullCatalogIndex invariant: entityTypeSummaries ALWAYS includes every kind from Stage 1
    const entityTypeSummaries: EntityTypeSummary[] = [...this.entityTypeCounts.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([kind, totalCount]) => {
        const visibleCount = visibleByKind.get(kind) ?? 0;
        return {
          kind,
          totalCount,
          visibleCount,
          hiddenCount: Math.max(0, totalCount - visibleCount),
          isHiddenByType: this.hiddenEntityTypes.has(kind),
        };
      });

    const selectedNode = this.selection.nodeId
      ? this.allNodesById.get(this.selection.nodeId)
      : undefined;
    const selectedEdge = this.selection.edgeId
      ? bundledEdges.find((e) => e.id === this.selection.edgeId)
      : undefined;
    const selectedGroup = this.selection.groupId
      ? projectedGroups.find((g) => g.id === this.selection.groupId)
      : undefined;

    const focusId = this.selection.nodeId ?? this.selection.groupId;
    const inboundEdges = focusId ? bundledEdges.filter((e) => e.target === focusId) : [];
    const outboundEdges = focusId ? bundledEdges.filter((e) => e.source === focusId) : [];

    const projection: RenderProjection = {
      nodes: visibleNodes.filter((n) => !nodeToCollapsedGroup.has(n.id)),
      groups: projectedGroups,
      edges: bundledEdges,
      violatedGateIdsByNode,
    };

    const inspector: InspectorSnapshot = {
      diagramId: this.definition.id,
      totalNodesInCatalog: allNodes.length,
      visibleNodeCount: visibleNodes.length,
      hiddenNodeCount: Math.max(0, allNodes.length - visibleNodes.length),
      entityTypeSummaries,
      groups: projectedGroups,
      selection: { ...this.selection },
      selectedNode,
      selectedEdge,
      selectedGroup,
      inboundEdges,
      outboundEdges,
      activeGateViolations: this.gateViolations,
    };

    return { projection, inspector };
  }

  private passesPresetFilter(node: SessionRawNode): boolean {
    if (this.presets.cyclesOnly && node.properties?.in_cycle !== 'true') {
      return false;
    }
    if (this.presets.includeLibraries === false &&
      (node.kind === 'Library' || node.properties?.is_library === 'true')) {
      return false;
    }
    if (this.presets.includeTests === false &&
      (node.kind === 'TestSuite' || node.properties?.is_test === 'true')) {
      return false;
    }
    return true;
  }

  private collectNeighborhood(anchorId: string, maxHops: number, edges: SessionRawEdge[]): Set<string> {
    const visited = new Set<string>([anchorId]);
    let frontier = [anchorId];

    for (let hop = 0; hop < maxHops && frontier.length > 0; hop++) {
      const next: string[] = [];
      const frontierSet = new Set(frontier);
      for (const edge of edges) {
        if (frontierSet.has(edge.source) && !visited.has(edge.target)) {
          visited.add(edge.target);
          next.push(edge.target);
        }
        if (frontierSet.has(edge.target) && !visited.has(edge.source)) {
          visited.add(edge.source);
          next.push(edge.source);
        }
      }
      frontier = next;
    }

    return visited;
  }
}
