import React, { useEffect, useRef, useState, useMemo, useCallback } from 'react';
import cytoscape from 'cytoscape';
import dagre from 'cytoscape-dagre';

cytoscape.use(dagre);
import { GraphData, GraphNode, GraphEdge, isProjectKind } from '../../../../proto/types';
import {
  computeEchelonTiers,
  computeEchelonTierReasons,
  computeConcentricLayout,
  ConcentricNodeInput,
  ConcentricEdgeInput,
  ConcentricOrbitGuide,
  ConcentricLayoutResult,
  DomainSectorGuide,
  ORBIT_TITLES,
} from '../layout/concentricLayout';
import {
  OrbitConfig,
  DEFAULT_ORBIT_CONFIG,
  DEFAULT_PROJECT_MAP_ORBIT_CONFIG,
  sanitizeOrbitConfig,
  isDefaultOrbitConfig,
  effectiveOrbitTier,
  orbitOrder,
  orbitRepresentative,
  orbitTitle,
  mergeOrbits,
  splitOrbit,
  moveOrbit,
  moveKindToOrbit,
} from '../layout/orbitConfig';
import { computeConcentricEquispacedLayout } from '../layout/concentricEquispacedLayout';
import { computeConcentricBarycentricLayout } from '../layout/projectMapLayout';
import { computeConcentricPolarForceLayout } from '../layout/concentricPolarForceLayout';
import { computeConcentricSectorsLayout, describeAnnularSector } from '../layout/concentricSectorsLayout';
import {
  computeSwimlanesLayout,
  SwimlaneGuide,
  SWIMLANE_TITLES,
  PROJECT_MAP_SWIMLANE_TITLES,
} from '../layout/swimlanesLayout';
import { computeDomainIslandsLayout, IslandGuide } from '../layout/domainIslandsLayout';
import { computeHivePlotLayout, HiveAxisGuide } from '../layout/hivePlotLayout';
import { DomainMatrixView } from './DomainMatrixView';
import { DomainMermaidView } from '../domain/components/DomainMermaidView';
import { DomainOverrideModal } from './DomainOverrideModal';
import { attachNormalizedCytoscapeWheel } from '../utils/wheelZoom';
import { DomainInspector } from '../domain/components/DomainInspector';
import { DomainVisibilityPanel } from '../domain/components/DomainVisibilityPanel';
import { matchesSearchTokens } from '../domain/searchUtils';
import { Group, Panel, Separator } from 'react-resizable-panels';
import { FloatingPanel } from './FloatingPanel';
import {
  LayersIcon,
  RotateCcwIcon,
  ChevronUpIcon,
  ChevronDownIcon,
  EyeIcon,
  EyeOffIcon,
  XIcon,
  InfoIcon,
} from './Icons';
import { DomainGroupSummary, CustomNodeGroup, NodeContentMode, NodeSizingMode } from '../domain/types';
import { useCommand } from '../commands/CommandContext';
import {
  HideNodeCommand,
  HideDownstreamCommand,
  HideAllOfKindCommand,
  UnhideNodeCommand,
  RestoreAllHiddenNodesCommand,
} from '../commands/commands';
import { getVsCodeApi } from '../vscodeApi';

const domainSessionMemory = new Map<string, string>();

function readDomainSessionValue(key: string): string | null {
  if (domainSessionMemory.has(key)) {
    return domainSessionMemory.get(key) ?? null;
  }
  try {
    const api = getVsCodeApi();
    const state = api?.getState();
    if (state?.domainSession && typeof state.domainSession[key] === 'string') {
      domainSessionMemory.set(key, state.domainSession[key]);
      return state.domainSession[key];
    }
  } catch { }
  return null;
}

function writeDomainSessionValue(key: string, value: string): void {
  domainSessionMemory.set(key, value);
  try {
    const api = getVsCodeApi();
    if (api) {
      const current = api.getState() || {};
      const domainSession = { ...(current.domainSession || {}), [key]: value };
      api.setState({ ...current, domainSession });
    }
  } catch { }
}

export type DomainLayoutName =
  | 'concentric'
  | 'concentric-equispaced'
  | 'concentric-barycentric'
  | 'concentric-polar-force'
  | 'concentric-sectors'
  | 'swimlanes'
  | 'clusters'
  | 'hive'
  | 'matrix'
  | 'cose'
  | 'dagre'
  | 'mermaid';

export type EdgeCurveMode = 'bezier' | 'straight' | 'avoid-inner';

export interface DomainArchitectureViewProps {
  mode?: 'map' | 'matrix';
  graph: GraphData | null;
  serverHttpUrl?: string;
  onFocusInFlow?: (projectName: string) => void;
  /** Drill down into the Project Map (C3 internal modules) of the given project. */
  onOpenProjectMap?: (projectName: string) => void;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
  onSelectNode?: (node: GraphNode | null) => void;
  onSwitchToContexts?: () => void;
  onSetDomainOverride?: (serviceName: string, domain: string, context?: string, reset?: boolean) => void;
  availableDomains?: string[];
  onRefresh?: () => void;
  gateEditorRequest?: { gateId?: string; timestamp: number } | null;
  /** Optional custom HUD brand label (e.g., 'C3 Internal Structure'). */
  /** Whether this view is in Project Map mode. */
  isProjectMap?: boolean;
  /** Allowed layout options. */
  allowedLayouts?: Array<{ id: DomainLayoutName; label: string }>;
  hudLabel?: string;
  /** Optional custom HUD title (e.g., 'Project Map'). */
  hudTitle?: string;
  /** Optional custom controls injected into the HUD primary row (e.g., Project selector, quick filters). */
  headerSlot?: React.ReactNode;
  /** Optional custom architectural preset toggles injected into the Visibility sidebar tab. */
  customPresetsSlot?: React.ReactNode;
  /** Serializable Project Map preset state synced to the native VS Code Inspector sidebar. */
  projectMapPresets?: {
    cyclesCount: number;
    cyclesOnly: boolean;
    hideUtilities: boolean;
    hideResources: boolean;
    projectName?: string;
    filesCount?: number;
    entryPointCount?: number;
    moduleCount?: number;
    granularity?: 'class' | 'file' | 'module';
    depCount?: number;
    resourceCount?: number;
  };
  /** Handler for toggling Project Map presets from the native VS Code Inspector sidebar. */
  onToggleProjectMapPreset?: (preset: 'cyclesOnly' | 'hideUtilities' | 'hideResources') => void;
  /** Default layout algorithm when none is saved under storagePrefix. */
  defaultLayoutName?: DomainLayoutName;
  /** Default node sizing mode when none is saved under storagePrefix. */
  defaultNodeSizingMode?: NodeSizingMode;
  /** Default node content mode when none is saved under storagePrefix. */
  defaultNodeContentMode?: NodeContentMode;
  /** Default hidden entity kinds when none is saved under storagePrefix. */
  defaultHiddenTypes?: EntityKind[];
  /** Storage key prefix to isolate persisted settings per view. */
  storagePrefix?: string;
}

export interface DomainProjectInfo {
  id: string;
  name: string;
  kind?: string;
  role?: string;
  primaryRole?: string;
  secondaryRoles?: string[];
  filePath?: string;
  lineStart?: number;
  isLibrary?: boolean;
  gitBranch?: string;
}

export type EntityKind =
  | 'App'
  | 'Service'
  | 'Ingress'
  | 'Endpoint'
  | 'EntryPoint'
  | 'Type'
  | 'Worker'
  | 'CliTool'
  | 'Library'
  | 'TestSuite'
  | 'Database'
  | 'Table'
  | 'Procedure'
  | 'Module'
  | 'Topic'
  | 'CloudService'
  | 'PeerMesh'
  | 'ExternalService'
  | 'Group';

export interface SelectedNodeDetail {
  id: string;
  name: string;
  displayName: string;
  kind: EntityKind;
  primaryRole?: string;
  secondaryRoles?: string[];
  properties?: Record<string, string>;
  domain?: string;
  displayTag: string;
  bgColor: string;
  borderColor: string;
  framework?: string;
  language?: string;
  gitBranch?: string;
  primaryFilePath?: string;
  primaryLineStart?: number;
  projects: DomainProjectInfo[];
  inboundCallsCount: number;
  outboundCallsCount: number;
  dbCount: number;
  messagingCount: number;
  fileCount?: number;
  tier?: number;
  tierLabel?: string;
  tierReason?: string;
  isGroup?: boolean;
  groupType?: 'category' | 'custom';
  groupKind?: EntityKind;
  customGroupId?: string;
  memberIds?: string[];
  memberNodes?: SelectedNodeDetail[];
  internalConnections?: ConnectionDetailItem[];
}

export interface ConnectionDetailItem {
  id: string;
  sourceId: string;
  sourceName: string;
  targetId: string;
  targetName: string;
  category: 'service_call' | 'database' | 'messaging' | 'external' | 'library' | 'test';
  kind: string;
  label: string;
  count: number;
  isTransitive: boolean;
  isInferred?: boolean;
  viaNames?: string[];
}

export interface SelectedEdgeDetail {
  id: string;
  sourceId: string;
  sourceName: string;
  sourceKind: EntityKind;
  sourceTag: string;
  sourceBgColor: string;
  targetId: string;
  targetName: string;
  targetKind: EntityKind;
  targetTag: string;
  targetBgColor: string;
  isBidirectional: boolean;
  hasForward: boolean;
  hasReverse: boolean;
  totalInteractions: number;
  directCallsCount: number;
  messagingCount: number;
  transitiveCount: number;
  inferredCount?: number;
  dbCount: number;
  primaryCategory: 'service_call' | 'database' | 'messaging' | 'external' | 'mixed';
  items: ConnectionDetailItem[];
}

export type DomainNodeDetail = SelectedNodeDetail;

/**
 * In-Circle Vector SVG Badge (mini vector icon + 3-letter uppercase code inside node circle)
 * Re-exported from shared entityBadges module.
 */
import { getNodeBadgeSvgDataUri, getEntityBadgeMeta } from '../domain/entityBadges';
export { getNodeBadgeSvgDataUri };

export const GROUP_NODE_SIZE = 68;

// Cytoscape stylesheets matching the semantic graph ontology styling (circles for code/services, rounded rectangles for resources)
const CYTOSCAPE_STYLES: cytoscape.StylesheetStyle[] = [
  // Base Node Style
  {
    selector: 'node',
    style: {
      'shape': 'data(shape)' as any,
      'width': 'data(size)',
      'height': 'data(size)',
      'background-color': 'data(bgColor)',
      'background-image': 'data(badgeSvg)',
      'background-fit': 'contain',
      'background-clip': 'node',
      'background-width': '100%',
      'background-height': '100%',
      'border-width': 3,
      'border-color': 'data(borderColor)',
      'border-opacity': 0.9,
      'label': 'data(displayLabel)',
      'color': '#f8fafc',
      'font-size': '10.5px',
      'font-weight': 600,
      'font-family': 'system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
      'text-valign': 'bottom',
      'text-halign': 'center',
      'text-margin-y': 7,
      'text-wrap': 'wrap',
      'text-max-width': '130px',
      'text-background-color': '#090d16',
      'text-background-opacity': 0.92,
      'text-background-padding': '3px',
      'text-background-shape': 'roundrectangle',
      'text-border-color': 'rgba(255, 255, 255, 0.14)',
      'text-border-width': 1,
      'text-border-opacity': 0.6,
      'transition-property': 'background-color, border-color, width, height, opacity',
      'transition-duration': 0.2,
    },
  },
  {
    selector: 'node.group-node',
    style: {
      'width': 'data(size)',
      'height': 'data(size)',
      'border-width': 4.5,
      'border-style': 'double',
    },
  },
  // Multi-Selected Node Ring (Shift+Click canvas selection)
  {
    selector: 'node.multi-selected',
    style: {
      'border-color': '#38bdf8',
      'border-width': 4.5,
      'underlay-color': '#38bdf8',
      'underlay-padding': '8px',
      'underlay-opacity': 0.65,
    },
  },
  // Selected Node Highlight
  {
    selector: 'node:selected',
    style: {
      'border-color': '#ffffff',
      'border-width': 4,
      'underlay-color': '#38bdf8',
      'underlay-padding': '8px',
      'underlay-opacity': 0.5,
    },
  },
  // Hovered Node
  {
    selector: 'node.hovered',
    style: {
      'border-color': '#38bdf8',
      'border-width': 4,
    },
  },
  // Base Edge Style (Bezier with Labeled Directed Arrows)
  {
    selector: 'edge',
    style: {
      'width': 2,
      'line-color': '#64748b',
      'target-arrow-color': '#64748b',
      'target-arrow-shape': 'triangle',
      'arrow-scale': 2.3,
      'curve-style': 'bezier',
      'label': 'data(label)',
      'font-size': '8.5px',
      'font-weight': 'bold',
      'color': '#e2e8f0',
      'text-rotation': 'autorotate',
      'text-background-color': '#090d16',
      'text-background-opacity': 0.94,
      'text-background-padding': '3px',
      'text-background-shape': 'roundrectangle',
      'text-border-color': 'rgba(255, 255, 255, 0.1)',
      'text-border-width': 1,
      'text-border-opacity': 0.7,
      'opacity': 0.8,
      'transition-property': 'line-color, target-arrow-color, width, opacity',
      'transition-duration': 0.2,
    },
  },
  // Edge Categories (Direct connections - Solid lines)
  {
    selector: 'edge[category = "service_call"]',
    style: {
      'line-style': 'solid',
      'line-color': '#34d399',
      'target-arrow-color': '#34d399',
      'source-arrow-color': '#34d399',
    },
  },
  {
    selector: 'edge[category = "database"]',
    style: {
      'line-style': 'solid',
      'line-color': '#c084fc',
      'target-arrow-color': '#c084fc',
      'source-arrow-color': '#c084fc',
    },
  },
  {
    selector: 'edge[category = "messaging"]',
    style: {
      'line-style': 'dashed',
      'line-dash-pattern': [6, 4],
      'line-color': '#fbbf24',
      'target-arrow-color': '#fbbf24',
      'source-arrow-color': '#fbbf24',
    },
  },
  {
    selector: 'edge[category = "external"]',
    style: {
      'line-style': 'solid',
      'line-color': '#26a69a',
      'target-arrow-color': '#26a69a',
      'source-arrow-color': '#26a69a',
    },
  },
  {
    selector: 'edge[category = "mixed"]',
    style: {
      'line-style': 'solid',
      'line-color': '#34d399',
      'target-arrow-color': '#34d399',
      'source-arrow-color': '#34d399',
    },
  },
  {
    selector: 'edge[category = "library"]',
    style: {
      'line-style': 'solid',
      'line-color': '#34d399',
      'target-arrow-color': '#34d399',
      'source-arrow-color': '#34d399',
    },
  },
  {
    selector: 'edge[category = "test"]',
    style: {
      'line-style': 'dashed',
      'line-dash-pattern': [6, 4],
      'line-color': '#10b981',
      'target-arrow-color': '#10b981',
      'source-arrow-color': '#10b981',
    },
  },
  // Bidirectional & Unidirectional Edge Arrow Shapes
  {
    selector: 'edge[isBidirectional = "true"]',
    style: {
      'source-arrow-shape': 'triangle',
      'target-arrow-shape': 'triangle',
      'source-arrow-fill': 'filled',
      'target-arrow-fill': 'filled',
      'arrow-scale': 1.6,
    },
  },
  {
    selector: 'edge[isBidirectional = "false"]',
    style: {
      'source-arrow-shape': 'none',
      'target-arrow-shape': 'triangle',
      'arrow-scale': 1.6,
    },
  },
  // Explicit Direct Edges
  {
    selector: 'edge[isTransitive = "false"][category != "messaging"]',
    style: {
      'line-style': 'solid',
    },
  },
  {
    selector: 'edge[isTransitive = "false"][category = "messaging"]',
    style: {
      'line-style': 'dashed',
      'line-dash-pattern': [6, 4],
    },
  },
  // Transitive / Indirect Edges (connecting through hidden entities - Dashed lines)
  {
    selector: 'edge[isTransitive = "true"], edge.transitive-edge',
    style: {
      'line-style': 'dashed',
      'line-dash-pattern': [8, 5],
      'opacity': 0.88,
    },
  },
  // Base Highlighted & Selected Edges
  {
    selector: 'edge.highlighted, edge:selected',
    style: {
      'width': 3.5,
      'opacity': 1,
      'z-index': 999,
      'arrow-scale': 2.3,
    },
  },
  {
    selector: 'edge.highlighted[isBidirectional = "true"], edge:selected[isBidirectional = "true"]',
    style: {
      'source-arrow-shape': 'triangle',
      'target-arrow-shape': 'triangle',
    },
  },
  // Hovered Edge
  {
    selector: 'edge.hovered',
    style: {
      'width': 3.5,
      'opacity': 1,
      'z-index': 998,
      'arrow-scale': 2.3,
    },
  },
  {
    selector: 'edge.hovered[isBidirectional = "true"]',
    style: {
      'source-arrow-shape': 'triangle',
      'target-arrow-shape': 'triangle',
    },
  },
  // Edge Label Hover-Only Mode: Hide label by default
  {
    selector: 'edge.label-hover-only',
    style: {
      'label': '',
    },
  },
  // In hover-only mode, reveal label on hover, selection, or highlight
  {
    selector: 'edge.label-hover-only.hovered, edge.label-hover-only:selected, edge.label-hover-only.highlighted',
    style: {
      'label': 'data(label)',
    },
  },
  // Preserve Category Colors on Highlight & Selection
  {
    selector: 'edge[category = "service_call"].highlighted, edge[category = "service_call"]:selected',
    style: {
      'line-color': '#34d399',
      'target-arrow-color': '#34d399',
      'source-arrow-color': '#34d399',
    },
  },
  {
    selector: 'edge[category = "database"].highlighted, edge[category = "database"]:selected',
    style: {
      'line-color': '#c084fc',
      'target-arrow-color': '#c084fc',
      'source-arrow-color': '#c084fc',
    },
  },
  {
    selector: 'edge[category = "messaging"].highlighted, edge[category = "messaging"]:selected',
    style: {
      'line-style': 'dashed',
      'line-dash-pattern': [8, 5],
      'line-color': '#fbbf24',
      'target-arrow-color': '#fbbf24',
      'source-arrow-color': '#fbbf24',
    },
  },
  {
    selector: 'edge[category = "external"].highlighted, edge[category = "external"]:selected',
    style: {
      'line-color': '#26a69a',
      'target-arrow-color': '#26a69a',
      'source-arrow-color': '#26a69a',
    },
  },
  {
    selector: 'edge[category = "mixed"].highlighted, edge[category = "mixed"]:selected',
    style: {
      'line-color': '#34d399',
      'target-arrow-color': '#34d399',
      'source-arrow-color': '#34d399',
    },
  },
  {
    selector: 'edge[category = "library"].highlighted, edge[category = "library"]:selected',
    style: {
      'line-color': '#34d399',
      'target-arrow-color': '#34d399',
      'source-arrow-color': '#34d399',
    },
  },
  // Transitive Edges MUST retain dashed style and dash pattern in Highlighted & Selected states
  {
    selector: 'edge[isTransitive = "true"].highlighted, edge.transitive-edge.highlighted, edge[isTransitive = "true"]:selected, edge.transitive-edge:selected',
    style: {
      'line-style': 'dashed',
      'line-dash-pattern': [10, 6],
      'width': 3.5,
      'opacity': 1,
      'z-index': 999,
      'target-arrow-shape': 'triangle',
      'arrow-scale': 2.3,
    },
  },
  // Specificity rules for Transitive by category to guarantee 100% preservation of colors and dashed style
  {
    selector: 'edge[isTransitive = "true"][category = "messaging"].highlighted, edge.transitive-edge[category = "messaging"].highlighted, edge[isTransitive = "true"][category = "messaging"]:selected, edge.transitive-edge[category = "messaging"]:selected',
    style: {
      'line-style': 'dashed',
      'line-dash-pattern': [10, 6],
      'line-color': '#fbbf24',
      'target-arrow-color': '#fbbf24',
      'width': 3.5,
      'opacity': 1,
      'z-index': 999,
    },
  },
  {
    selector: 'edge[isTransitive = "true"][category = "service_call"].highlighted, edge.transitive-edge[category = "service_call"].highlighted, edge[isTransitive = "true"][category = "service_call"]:selected, edge.transitive-edge[category = "service_call"]:selected',
    style: {
      'line-style': 'dashed',
      'line-dash-pattern': [10, 6],
      'line-color': '#38bdf8',
      'target-arrow-color': '#38bdf8',
      'width': 3.5,
      'opacity': 1,
      'z-index': 999,
    },
  },
  {
    selector: 'edge[isTransitive = "true"][category = "database"].highlighted, edge.transitive-edge[category = "database"].highlighted, edge[isTransitive = "true"][category = "database"]:selected, edge.transitive-edge[category = "database"]:selected',
    style: {
      'line-style': 'dashed',
      'line-dash-pattern': [10, 6],
      'line-color': '#c084fc',
      'target-arrow-color': '#c084fc',
      'width': 3.5,
      'opacity': 1,
      'z-index': 999,
    },
  },
  {
    selector: 'edge[isTransitive = "true"][category = "external"].highlighted, edge.transitive-edge[category = "external"].highlighted, edge[isTransitive = "true"][category = "external"]:selected, edge.transitive-edge[category = "external"]:selected',
    style: {
      'line-style': 'dashed',
      'line-dash-pattern': [10, 6],
      'line-color': '#34d399',
      'target-arrow-color': '#34d399',
      'width': 3.5,
      'opacity': 1,
      'z-index': 999,
    },
  },
  // Dimmed Elements during search or hover
  {
    selector: '.dimmed',
    style: {
      'opacity': 0.12,
    },
  },
  // Search Matches
  {
    selector: 'node.search-match',
    style: {
      'underlay-color': '#f59e0b',
      'underlay-padding': '12px',
      'underlay-opacity': 0.7,
      'border-color': '#fbbf24',
      'border-width': 4,
      'opacity': 1,
      'z-index': 999,
    },
  },
  // Dynamic Curve Styles (Straight, Bezier, Unbundled Core Bypass)
  {
    selector: 'edge.edge-straight',
    style: {
      'curve-style': 'straight',
    },
  },
  {
    selector: 'edge.edge-bezier',
    style: {
      'curve-style': 'bezier',
    },
  },
  {
    selector: 'edge.edge-unbundled',
    style: {
      'curve-style': 'unbundled-bezier',
      'control-point-distances': 'data(ctrlDist)' as any,
      'control-point-weights': 'data(ctrlWeight)' as any,
    },
  },
];

/**
 * Toggles edge label visibility: always visible or visible only on hover / selection / highlight.
 */
export function applyEdgeLabelVisibility(cy: cytoscape.Core | null, onHoverOnly: boolean) {
  if (!cy) return;
  cy.batch(() => {
    if (onHoverOnly) {
      cy.edges().addClass('label-hover-only');
    } else {
      cy.edges().removeClass('label-hover-only');
    }
  });
}

export interface OrbitLegendItem {
  /** Orbit identifier (first tier of the orbit); this is what hiding is keyed on. */
  levelIndex: number;
  /** Tier this row represents; several rows share an orbit when orbits are merged. */
  slotTier: number;
  orbitIndex: number;
  shortLabel: string;
  title: string;
  count: number;
  radius: number;
  nodeIds: string[];
  isFirstInOrbit: boolean;
  isLastInOrbit: boolean;
  /** Number of legend rows that belong to the same (merged) orbit. */
  orbitRowCount: number;
  kinds: Array<{ kind: string; count: number; overridden: boolean }>;
}

/**
 * Builds one legend row per populated tier, following the configured orbit order. Rows of a
 * merged orbit are adjacent and share one radius; all populated tiers stay listed even when hidden.
 */
export function buildAllOrbitLegendItems(
  allNodes: cytoscape.NodeDefinition[],
  echelonMap: Map<string, number>,
  layoutPopulatedOrbits: Array<{ levelIndex: number; radius: number; shortLabel?: string; title?: string; nodeIds: string[] }>,
  config: OrbitConfig,
  customTitles?: Record<number, string>,
  unitPrefix: string = 'Orbit',
  titlesByTier?: Record<number, string>
): OrbitLegendItem[] {
  const slotBuckets = new Map<number, Array<{ id: string; kind: string }>>();
  for (const n of allNodes) {
    const nid = n.data.id as string;
    const kind = ((n.data as any).kind as string) ?? '';
    const base = (n.data as any).echelonTier ?? echelonMap.get(nid) ?? 2;
    const slot = config.kindTiers[kind] ?? base;
    if (!slotBuckets.has(slot)) slotBuckets.set(slot, []);
    slotBuckets.get(slot)!.push({ id: nid, kind });
  }

  const radiusMap = new Map<number, number>();
  for (const o of layoutPopulatedOrbits) {
    radiusMap.set(o.levelIndex, o.radius);
  }

  const items: OrbitLegendItem[] = [];
  let orbitNumber = 0;
  config.orbits.forEach((tiers, orbitIndex) => {
    const populated = tiers.filter((t) => (slotBuckets.get(t)?.length ?? 0) > 0);
    if (populated.length === 0) return;
    const shortLabel = `${unitPrefix} ${orbitNumber++}`;
    populated.forEach((tier, i) => {
      const members = slotBuckets.get(tier)!;
      const kindCounts = new Map<string, number>();
      for (const m of members) kindCounts.set(m.kind, (kindCounts.get(m.kind) ?? 0) + 1);
      items.push({
        levelIndex: tiers[0],
        slotTier: tier,
        orbitIndex,
        shortLabel,
        title: customTitles?.[tier] || titlesByTier?.[tier] || ORBIT_TITLES[tier] || `Tier ${tier}`,
        count: members.length,
        radius: radiusMap.get(tiers[0]) || 0,
        nodeIds: members.map((m) => m.id),
        isFirstInOrbit: i === 0,
        isLastInOrbit: i === populated.length - 1,
        orbitRowCount: populated.length,
        kinds: Array.from(kindCounts.entries())
          .sort((a, b) => b[1] - a[1])
          .map(([kind, count]) => ({ kind, count, overridden: kind in config.kindTiers })),
      });
    });
  });
  return items;
}

/** Gives merged orbits a combined title on the canvas guides and in the layout result. */
function relabelMergedOrbits(result: ConcentricLayoutResult, config: OrbitConfig, customTitles?: Record<number, string>): void {
  const titleFor = (levelIndex: number) => {
    const entry = config.orbits.find((o) => o[0] === levelIndex);
    return entry && entry.length > 1 ? orbitTitle(entry, customTitles) : undefined;
  };
  for (const orbit of result.populatedOrbits) {
    const title = titleFor(orbit.levelIndex);
    if (title) {
      orbit.title = title;
      orbit.label = `${orbit.shortLabel}: ${title}`;
    }
  }
  for (const guide of result.guides) {
    const title = titleFor(guide.levelIndex);
    if (title) {
      guide.title = title;
      guide.label = `${guide.shortLabel}: ${title}`;
    }
  }
}

/**
 * Dynamically toggles line rendering: Straight, Standard Bezier, or Orbit Shield (bypasses inner orbits).
 */
export function applyEdgeCurveMode(
  cy: cytoscape.Core | null,
  mode: EdgeCurveMode,
  center = { x: 0, y: 0 },
  curveFactor = 35
) {
  if (!cy) return;
  cy.batch(() => {
    if (mode === 'straight') {
      cy.edges().forEach((edge) => {
        edge.style({
          'curve-style': 'straight',
        });
      });
      return;
    }

    // Group edges by connected node pair to fan out parallel/multi edges
    const pairMap = new Map<string, cytoscape.EdgeSingular[]>();
    cy.edges().forEach((edge) => {
      const s = edge.source().id();
      const t = edge.target().id();
      if (!s || !t) return;
      const key = s < t ? `${s}--${t}` : `${t}--${s}`;
      let list = pairMap.get(key);
      if (!list) {
        list = [];
        pairMap.set(key, list);
      }
      list.push(edge);
    });

    const scale = curveFactor / 35;

    cy.edges().forEach((edge) => {
      const srcNode = edge.source();
      const tgtNode = edge.target();
      if (!srcNode || !tgtNode || srcNode.empty() || tgtNode.empty()) {
        return;
      }

      const src = srcNode.position();
      const tgt = tgtNode.position();

      const edx = tgt.x - src.x;
      const edy = tgt.y - src.y;
      const chordLen = Math.hypot(edx, edy);

      if (chordLen < 1) return;

      const r1 = Math.hypot(src.x - center.x, src.y - center.y);
      const r2 = Math.hypot(tgt.x - center.x, tgt.y - center.y);
      const minNodeRadius = Math.min(r1, r2);
      const maxNodeRadius = Math.max(r1, r2);

      // Midpoint of the straight chord
      const mx = (src.x + tgt.x) / 2;
      const my = (src.y + tgt.y) / 2;
      const vx = mx - center.x;
      const vy = my - center.y;

      // In Cytoscape unbundled-bezier, vectorNormInverse is (-edy / chordLen, edx / chordLen).
      // Dot product with outward vector from center to chord midpoint determines if positive distance points outward:
      let dotOutward = (-edy * vx + edx * vy) / chordLen;
      if (Math.abs(dotOutward) < 1e-4) {
        dotOutward = -edy * (src.x - center.x) + edx * (src.y - center.y);
      }
      const sign = dotOutward >= 0 ? 1 : -1;

      // Angular span between endpoints around center [0..PI]
      const angle1 = Math.atan2(src.y - center.y, src.x - center.x);
      const angle2 = Math.atan2(tgt.y - center.y, tgt.x - center.x);
      let dTheta = Math.abs(angle1 - angle2);
      if (dTheta > Math.PI) {
        dTheta = 2 * Math.PI - dTheta;
      }
      const spanFactor = Math.sin(dTheta / 2); // 0 at adjacent, 0.707 at 90 deg, 1.0 at 180 deg

      // Deterministic hash based on edge ID for subtle organic variation (+/- 10%)
      let hash = 0;
      const idStr = edge.id();
      for (let i = 0; i < idStr.length; i++) {
        hash = (hash * 31 + idStr.charCodeAt(i)) & 0xffffffff;
      }
      const subtleVariation = 1.0 + ((hash % 17) - 8) * 0.015;

      // Multi-edge parallel spread
      const sId = srcNode.id();
      const tId = tgtNode.id();
      const pairKey = sId < tId ? `${sId}--${tId}` : `${tId}--${sId}`;
      const pairEdges = pairMap.get(pairKey) || [edge];
      const edgeIdx = pairEdges.indexOf(edge);
      const pairCount = pairEdges.length;
      const multiEdgeSpread = pairCount > 1
        ? (edgeIdx - (pairCount - 1) / 2) * 22 * Math.max(0.6, Math.abs(scale))
        : 0;

      if (mode === 'bezier') {
        // Expressive fluid Bezier curves scaling gracefully with chord length and angular span
        const baseDeflection = chordLen * (0.05 + 0.07 * spanFactor);
        const pushDistance = (baseDeflection * subtleVariation + multiEdgeSpread) * scale;
        edge.style({
          'curve-style': 'unbundled-bezier',
          'control-point-distances': [sign * pushDistance],
          'control-point-weights': [0.5],
        });
        return;
      }

      if (mode === 'avoid-inner') {
        // Projection of (center - src) onto (tgt - src) to find closest approach of chord to center:
        const tProj = Math.max(0, Math.min(1, -((src.x - center.x) * edx + (src.y - center.y) * edy) / (chordLen * chordLen)));
        const closestX = src.x + tProj * edx - center.x;
        const closestY = src.y + tProj * edy - center.y;
        const dMin = Math.hypot(closestX, closestY);

        // Penetration depth: how far closer to the center the segment reaches below the inner-most endpoint
        const penetration = Math.max(0, minNodeRadius - dMin);

        // Dynamic base curvature tailored to angular span and chord
        const baseNatural = chordLen * (0.06 + 0.10 * spanFactor);

        let pushDistance: number;
        if (penetration > 12 && minNodeRadius > 80) {
          // Edge cuts into inner orbits: calculate proportional clearance based on penetration,
          // angular span, and outer orbit radius so that outer edges curve wider than inner edges!
          const radiusBoost = 0.5 + 0.35 * Math.min(1.5, maxNodeRadius / 500);
          const bypassClearance = penetration * (0.6 + 0.3 * spanFactor) * radiusBoost;
          pushDistance = (baseNatural + bypassClearance) * scale * subtleVariation + multiEdgeSpread;
        } else {
          // Edge does not penetrate inner orbits: render with clean natural curvature
          pushDistance = baseNatural * scale * subtleVariation + multiEdgeSpread;
        }

        edge.style({
          'curve-style': 'unbundled-bezier',
          'control-point-distances': [sign * pushDistance],
          'control-point-weights': [0.5],
        });
      }
    });
  });
}

export const DomainArchitectureView: React.FC<DomainArchitectureViewProps> = ({
  mode = 'map',
  graph,
  serverHttpUrl,
  onFocusInFlow,
  onOpenProjectMap,
  onOpenFile,
  onSelectNode,
  onSwitchToContexts,
  onSetDomainOverride,
  availableDomains = [],
  onRefresh,
  hudLabel,
  hudTitle,
  headerSlot,
  customPresetsSlot,
  projectMapPresets,
  onToggleProjectMapPreset,
  defaultLayoutName = 'concentric',
  defaultNodeSizingMode = 'uniform',
  defaultNodeContentMode = 'icon',
  defaultHiddenTypes = ['ExternalService', 'Library'],
  storagePrefix = 'ce_domain_',
  isProjectMap = false,
  allowedLayouts,
}) => {
  const isMatrixMode = mode === 'matrix';
  const containerRef = useRef<HTMLDivElement>(null);
  const cyRef = useRef<cytoscape.Core | null>(null);
  const [isCyReady, setIsCyReady] = useState(false);

  const onFocusInFlowRef = useRef(onFocusInFlow);
  onFocusInFlowRef.current = onFocusInFlow;

  const onSelectNodeRef = useRef(onSelectNode);
  onSelectNodeRef.current = onSelectNode;

  let executeCommand: ((cmd: any) => void) | null = null;
  try {
    const cmdCtx = useCommand();
    executeCommand = cmdCtx.executeCommand;
  } catch {}

  const [overrideModalOpen, setOverrideModalOpen] = useState(false);
  const [overrideTargetService, setOverrideTargetService] = useState('');

  const [searchQuery, setSearchQuery] = useState('');
  const searchQueryRef = useRef<string>(searchQuery);
  searchQueryRef.current = searchQuery;
  const [layoutName, setLayoutName] = useState<DomainLayoutName>(() => {
    try {
      const saved = localStorage.getItem(`${storagePrefix}layout`);
      if (
        saved === 'cose' ||
        saved === 'concentric' ||
        saved === 'concentric-equispaced' ||
        saved === 'concentric-barycentric' ||
        saved === 'concentric-polar-force' ||
        saved === 'concentric-sectors' ||
        saved === 'swimlanes' ||
        saved === 'clusters' ||
        saved === 'hive' ||
        saved === 'matrix' ||
        saved === 'dagre'
      ) {
        if (allowedLayouts && allowedLayouts.length > 0 && !allowedLayouts.some((l: { id: DomainLayoutName }) => l.id === saved)) {
          return allowedLayouts[0].id;
        }
        return saved as DomainLayoutName;
      }
    } catch { }
    return defaultLayoutName;
  });

  const [nodeContentMode, setNodeContentMode] = useState<NodeContentMode>(() => {
    try {
      const saved = localStorage.getItem(`${storagePrefix}node_content_mode`);
      if (saved === 'icon' || saved === 'icon-code' || saved === 'metric') {
        return saved as NodeContentMode;
      }
    } catch { }
    return defaultNodeContentMode;
  });

  const handleNodeContentModeChange = useCallback((mode: NodeContentMode) => {
    setNodeContentMode(mode);
    try {
      localStorage.setItem(`${storagePrefix}node_content_mode`, mode);
    } catch { }
  }, [storagePrefix]);

  const [nodeSizingMode, setNodeSizingMode] = useState<NodeSizingMode>(() => {
    try {
      const saved = localStorage.getItem(`${storagePrefix}node_sizing_mode`);
      if (saved === 'uniform' || saved === 'sqrt-weight' || saved === 'degree') {
        return saved as NodeSizingMode;
      }
    } catch { }
    return defaultNodeSizingMode;
  });

  const handleNodeSizingModeChange = useCallback((mode: NodeSizingMode) => {
    setNodeSizingMode(mode);
    try {
      localStorage.setItem(`${storagePrefix}node_sizing_mode`, mode);
    } catch { }
  }, [storagePrefix]);
  const [edgeCurveMode, setEdgeCurveMode] = useState<EdgeCurveMode>(() => {
    try {
      const saved = localStorage.getItem('ce_edge_curve_mode');
      if (saved === 'straight' || saved === 'bezier' || saved === 'avoid-inner') {
        return saved as EdgeCurveMode;
      }
    } catch { }
    return 'bezier';
  });
  const edgeCurveModeRef = useRef<EdgeCurveMode>(edgeCurveMode);
  edgeCurveModeRef.current = edgeCurveMode;

  const [curveFactor, setCurveFactor] = useState<number>(() => {
    try {
      const saved = localStorage.getItem('ce_curve_factor');
      if (saved !== null) {
        const parsed = parseInt(saved, 10);
        if (!isNaN(parsed)) {
          return Math.max(-200, Math.min(200, parsed));
        }
      }
    } catch { }
    return 35;
  });
  const curveFactorRef = useRef<number>(curveFactor);
  curveFactorRef.current = curveFactor;

  const handleCurveFactorChange = useCallback((val: number) => {
    const clamped = Math.max(-200, Math.min(200, val));
    setCurveFactor(clamped);
    try {
      localStorage.setItem('ce_curve_factor', String(clamped));
    } catch { }
    if (cyRef.current) {
      applyEdgeCurveMode(cyRef.current, edgeCurveModeRef.current, { x: 0, y: 0 }, clamped);
    }
  }, []);

  const handleEdgeCurveModeChange = useCallback((mode: EdgeCurveMode) => {
    setEdgeCurveMode(mode);
    try {
      localStorage.setItem('ce_edge_curve_mode', mode);
    } catch { }
    if (cyRef.current) {
      applyEdgeCurveMode(cyRef.current, mode, { x: 0, y: 0 }, curveFactorRef.current);
    }
  }, []);

  const [edgeLabelsOnHover, setEdgeLabelsOnHover] = useState<boolean>(() => {
    try {
      const saved = localStorage.getItem('ce_edge_labels_on_hover');
      return saved !== null ? saved === 'true' : false;
    } catch { }
    return false;
  });
  const edgeLabelsOnHoverRef = useRef<boolean>(edgeLabelsOnHover);
  edgeLabelsOnHoverRef.current = edgeLabelsOnHover;

  const handleEdgeLabelsOnHoverChange = useCallback((onHoverOnly: boolean) => {
    setEdgeLabelsOnHover(onHoverOnly);
    edgeLabelsOnHoverRef.current = onHoverOnly;
    try {
      localStorage.setItem('ce_edge_labels_on_hover', String(onHoverOnly));
    } catch { }
    if (cyRef.current) {
      applyEdgeLabelVisibility(cyRef.current, onHoverOnly);
    }
  }, []);

  const isProjectMapGraph = graph?.metadata?.graphType === 'projectmap' || storagePrefix.startsWith('ce_pm_');
  const customOrbitTitles = useMemo<Record<number, string> | undefined>(() => {
    const rawTiers = graph?.metadata?.tiers;
    if (!rawTiers) return undefined;
    try {
      const parsed = JSON.parse(rawTiers);
      if (Array.isArray(parsed)) {
        const map: Record<number, string> = {};
        for (const item of parsed) {
          if (typeof item?.tier === 'number' && typeof item?.label === 'string') {
            map[item.tier] = item.label;
          }
        }
        return Object.keys(map).length > 0 ? map : undefined;
      }
    } catch { }
    return undefined;
  }, [graph?.metadata?.tiers]);
  const customOrbitTitlesRef = useRef<Record<number, string> | undefined>(customOrbitTitles);
  customOrbitTitlesRef.current = customOrbitTitles;

  const [concentricGuides, setConcentricGuides] = useState<ConcentricOrbitGuide[]>([]);
  const [orbitLegendItems, setOrbitLegendItems] = useState<OrbitLegendItem[]>([]);
  const [isOrbitLegendOpen, setIsOrbitLegendOpen] = useState(true);
  const [orbitConfig, setOrbitConfigState] = useState<OrbitConfig>(() =>
    isProjectMapGraph ? DEFAULT_PROJECT_MAP_ORBIT_CONFIG : DEFAULT_ORBIT_CONFIG
  );
  const orbitConfigRef = useRef<OrbitConfig>(orbitConfig);
  orbitConfigRef.current = orbitConfig;
  const [selectedOrbitRows, setSelectedOrbitRows] = useState<number[]>([]);
  const lastSelectedOrbitRowRef = useRef<number | null>(null);

  useEffect(() => {
    if (isProjectMapGraph) {
      orbitConfigRef.current = DEFAULT_PROJECT_MAP_ORBIT_CONFIG;
      setOrbitConfigState(DEFAULT_PROJECT_MAP_ORBIT_CONFIG);
      return;
    }
    const onMessage = (event: MessageEvent) => {
      const msg = event.data;
      if (msg?.type === 'ORBIT_CONFIG') {
        const loaded = sanitizeOrbitConfig(msg.config);
        orbitConfigRef.current = loaded;
        setOrbitConfigState(loaded);
        forceRelayoutRef.current = true;
      }
    };
    window.addEventListener('message', onMessage);
    getVsCodeApi()?.postMessage({ type: 'GET_ORBIT_CONFIG' });
    return () => window.removeEventListener('message', onMessage);
  }, [isProjectMapGraph]);

  const [draggedOrbitIndex, setDraggedOrbitIndex] = useState<number | null>(null);
  const draggedKindRef = useRef<string | null>(null);
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);
  const [swimlaneGuides, setSwimlaneGuides] = useState<SwimlaneGuide[]>([]);
  const [islandGuides, setIslandGuides] = useState<IslandGuide[]>([]);
  const [hiveGuides, setHiveGuides] = useState<HiveAxisGuide[]>([]);
  const [sectorGuides, setSectorGuides] = useState<DomainSectorGuide[]>([]);
  const baseConcentricGuidesRef = useRef<ConcentricOrbitGuide[]>([]);
  const baseSwimlaneGuidesRef = useRef<SwimlaneGuide[]>([]);
  const baseIslandGuidesRef = useRef<IslandGuide[]>([]);
  const baseHiveGuidesRef = useRef<HiveAxisGuide[]>([]);
  const baseSectorGuidesRef = useRef<DomainSectorGuide[]>([]);
  const [cyTransform, setCyTransform] = useState<{ pan: { x: number; y: number }; zoom: number }>({ pan: { x: 0, y: 0 }, zoom: 1 });
  const [selectedNode, setSelectedNode] = useState<SelectedNodeDetail | null>(null);
  const selectedNodeRef = useRef<SelectedNodeDetail | null>(null);
  selectedNodeRef.current = selectedNode;

  const [selectedEdge, setSelectedEdge] = useState<SelectedEdgeDetail | null>(null);
  const selectedEdgeRef = useRef<SelectedEdgeDetail | null>(null);
  selectedEdgeRef.current = selectedEdge;
  const edgeDetailMapRef = useRef<Map<string, SelectedEdgeDetail>>(new Map());

  const applySearchHighlight = useCallback((cy: cytoscape.Core, query: string) => {
    const trimmed = query.trim();
    if (!trimmed) {
      cy.batch(() => {
        cy.elements().removeClass('search-match');
        if (selectedNodeRef.current) {
          const node = cy.getElementById(selectedNodeRef.current.id);
          if (node.length > 0) {
            cy.elements().removeClass('highlighted dimmed');
            const neighborhood = node.neighborhood().add(node);
            cy.elements().not(neighborhood).addClass('dimmed');
            node.connectedEdges().addClass('highlighted');
          } else {
            cy.elements().removeClass('highlighted dimmed');
          }
        } else if (selectedEdgeRef.current) {
          const edge = cy.getElementById(selectedEdgeRef.current.id);
          if (edge.length > 0) {
            cy.elements().removeClass('highlighted dimmed');
            const connectedNodes = edge.connectedNodes();
            const activeGroup = connectedNodes.add(edge);
            cy.elements().not(activeGroup).addClass('dimmed');
            edge.addClass('highlighted');
            connectedNodes.addClass('highlighted');
          } else {
            cy.elements().removeClass('highlighted dimmed');
          }
        } else {
          cy.elements().removeClass('highlighted dimmed');
        }
      });
      return;
    }

    cy.batch(() => {
      const matchedNodeIds = new Set<string>();

      cy.nodes().forEach((n) => {
        const detail = rawGraphRef.current?.detailMap?.get(n.id());
        const data = n.data() || {};
        const isMatch = matchesSearchTokens([
          detail?.name || data.name,
          detail?.displayName || data.displayName || data.displayLabel,
          detail?.domain || data.domain,
          detail?.kind || data.kind,
          detail?.displayTag,
          detail?.framework || data.framework,
          detail?.language || data.language,
          detail?.primaryFilePath,
          ...(detail?.projects || []).map((p: DomainProjectInfo) => p.name),
        ], trimmed);

        if (isMatch) {
          matchedNodeIds.add(n.id());
          n.addClass('search-match').removeClass('dimmed');
        } else {
          n.removeClass('search-match').addClass('dimmed');
        }
      });

      // Highlight edges connecting matching nodes, dim all other edges
      cy.edges().forEach((e) => {
        if (matchedNodeIds.has(e.source().id()) && matchedNodeIds.has(e.target().id())) {
          e.addClass('highlighted').removeClass('dimmed');
        } else {
          e.removeClass('highlighted').addClass('dimmed');
        }
      });
    });
  }, []);

  const highlightOrbitNodes = useCallback((nodeIds: string[]) => {
    const cy = cyRef.current;
    if (!cy || nodeIds.length === 0) return;
    const nodeSet = new Set(nodeIds);
    cy.batch(() => {
      cy.nodes().forEach((n) => {
        if (nodeSet.has(n.id())) {
          n.addClass('highlighted').removeClass('dimmed');
        } else {
          n.addClass('dimmed').removeClass('highlighted');
        }
      });
      cy.edges().addClass('dimmed');
    });
  }, []);

  const clearOrbitHighlight = useCallback(() => {
    const cy = cyRef.current;
    if (!cy || selectedNodeRef.current) return;
    if (searchQueryRef.current.trim()) {
      applySearchHighlight(cy, searchQueryRef.current);
    } else {
      cy.batch(() => {
        cy.elements().removeClass('highlighted dimmed');
      });
    }
  }, [applySearchHighlight]);
  const [hiddenTypes, setHiddenTypes] = useState<Set<EntityKind>>(() => {
    try {
      const saved = readDomainSessionValue(`${storagePrefix}hidden_types_v2`);
      if (saved) {
        const parsed = JSON.parse(saved);
        if (Array.isArray(parsed)) return new Set<EntityKind>(parsed);
      }
    } catch { }
    return new Set<EntityKind>(defaultHiddenTypes);
  });
  const [hiddenNodeIds, setHiddenNodeIds] = useState<Set<string>>(() => {
    try {
      const saved = readDomainSessionValue(`${storagePrefix}hidden_node_ids`);
      if (saved) {
        const parsed = JSON.parse(saved);
        if (Array.isArray(parsed)) return new Set<string>(parsed);
      }
    } catch { }
    return new Set<string>();
  });
  const [hiddenOrbitTiers, setHiddenOrbitTiers] = useState<Set<number>>(() => {
    try {
      const saved = readDomainSessionValue(`${storagePrefix}hidden_orbit_tiers`);
      if (saved) {
        const parsed = JSON.parse(saved);
        if (Array.isArray(parsed)) return new Set<number>(parsed);
      }
    } catch { }
    return new Set<number>();
  });
  const [forcedVisibleNodeIds, setForcedVisibleNodeIds] = useState<Set<string>>(() => {
    try {
      const saved = readDomainSessionValue(`${storagePrefix}forced_visible_nodes`);
      if (saved) {
        const parsed = JSON.parse(saved);
        if (Array.isArray(parsed)) return new Set<string>(parsed);
      }
    } catch { }
    return new Set<string>();
  });
  const [hideSingleConnectionDbs, setHideSingleConnectionDbs] = useState<boolean>(() => {
    try {
      const saved = readDomainSessionValue(`${storagePrefix}hide_single_connection_dbs`);
      return saved !== null ? saved === 'true' : false;
    } catch { }
    return false;
  });
  const [hideIsolatedNodes, setHideIsolatedNodes] = useState<boolean>(() => {
    try {
      const saved = readDomainSessionValue(`${storagePrefix}hide_isolated_nodes`);
      return saved !== null ? saved === 'true' : false;
    } catch { }
    return false;
  });

  // Sync hidden and filter states to in-memory webview session state
  useEffect(() => {
    writeDomainSessionValue(`${storagePrefix}hidden_types_v2`, JSON.stringify([...hiddenTypes]));
  }, [hiddenTypes, storagePrefix]);

  useEffect(() => {
    writeDomainSessionValue(`${storagePrefix}hidden_node_ids`, JSON.stringify([...hiddenNodeIds]));
  }, [hiddenNodeIds, storagePrefix]);

  useEffect(() => {
    writeDomainSessionValue(`${storagePrefix}hidden_orbit_tiers`, JSON.stringify([...hiddenOrbitTiers]));
  }, [hiddenOrbitTiers, storagePrefix]);

  useEffect(() => {
    writeDomainSessionValue(`${storagePrefix}forced_visible_nodes`, JSON.stringify([...forcedVisibleNodeIds]));
  }, [forcedVisibleNodeIds, storagePrefix]);

  useEffect(() => {
    writeDomainSessionValue(`${storagePrefix}hide_single_connection_dbs`, String(hideSingleConnectionDbs));
  }, [hideSingleConnectionDbs, storagePrefix]);

  useEffect(() => {
    writeDomainSessionValue(`${storagePrefix}hide_isolated_nodes`, String(hideIsolatedNodes));
  }, [hideIsolatedNodes, storagePrefix]);

  // Grouped Types & Custom Groups (held in in-memory webview session state)
  const [groupedTypes, setGroupedTypes] = useState<Set<EntityKind>>(() => {
    try {
      const saved = readDomainSessionValue(`${storagePrefix}grouped_types`);
      if (saved) {
        const parsed = JSON.parse(saved);
        if (Array.isArray(parsed)) return new Set<EntityKind>(parsed);
      }
    } catch { }
    return new Set<EntityKind>();
  });

  const [customGroups, setCustomGroups] = useState<CustomNodeGroup[]>(() => {
    try {
      const saved = readDomainSessionValue(`${storagePrefix}custom_groups`);
      if (saved) {
        const parsed = JSON.parse(saved);
        if (Array.isArray(parsed)) return parsed;
      }
    } catch { }
    return [];
  });

  const [multiSelectedNodeIds, setMultiSelectedNodeIds] = useState<Set<string>>(new Set());
  const [floatingGroupName, setFloatingGroupName] = useState('');
  const multiSelectedNodeIdsRef = useRef(multiSelectedNodeIds);
  multiSelectedNodeIdsRef.current = multiSelectedNodeIds;
  const effectiveDetailMapRef = useRef<Map<string, SelectedNodeDetail>>(new Map());

  // Sync grouped types and custom groups to in-memory webview session state
  useEffect(() => {
    writeDomainSessionValue(`${storagePrefix}grouped_types`, JSON.stringify([...groupedTypes]));
  }, [groupedTypes, storagePrefix]);

  useEffect(() => {
    writeDomainSessionValue(`${storagePrefix}custom_groups`, JSON.stringify(customGroups));
  }, [customGroups, storagePrefix]);

  // Sync multi-selected classes to Cytoscape
  useEffect(() => {
    if (!cyRef.current) return;
    const cy = cyRef.current;
    cy.batch(() => {
      cy.nodes().each((node) => {
        if (multiSelectedNodeIds.has(node.id())) {
          node.addClass('multi-selected');
        } else {
          node.removeClass('multi-selected');
        }
      });
    });
  }, [multiSelectedNodeIds]);

  const toggleTypeGrouping = useCallback((kind: EntityKind) => {
    forceRelayoutRef.current = true;
    setGroupedTypes((prev) => {
      const next = new Set(prev);
      if (next.has(kind)) next.delete(kind);
      else next.add(kind);
      return next;
    });
  }, []);

  const createCustomGroup = useCallback((name: string, nodeIds: string[]) => {
    forceRelayoutRef.current = true;
    const newGroup: CustomNodeGroup = {
      id: 'cg_' + Date.now().toString(36) + '_' + Math.random().toString(36).substring(2, 6),
      name: name.trim() || `Group (${nodeIds.length})`,
      nodeIds: Array.from(new Set(nodeIds)),
      collapsed: true,
    };
    setCustomGroups((prev) => [...prev, newGroup]);
    setMultiSelectedNodeIds(new Set());
  }, []);

  const toggleCustomGroupCollapse = useCallback((groupId: string) => {
    forceRelayoutRef.current = true;
    setCustomGroups((prev) =>
      prev.map((g) => (g.id === groupId ? { ...g, collapsed: !g.collapsed } : g))
    );
  }, []);

  const deleteCustomGroup = useCallback((groupId: string) => {
    forceRelayoutRef.current = true;
    setCustomGroups((prev) => prev.filter((g) => g.id !== groupId));
  }, []);

  const removeNodeFromCustomGroup = useCallback((groupId: string, nodeId: string) => {
    forceRelayoutRef.current = true;
    setCustomGroups((prev) =>
      prev
        .map((g) => {
          if (g.id !== groupId) return g;
          return { ...g, nodeIds: g.nodeIds.filter((id) => id !== nodeId) };
        })
        .filter((g) => g.nodeIds.length > 0)
    );
  }, []);

  const addNodesToCustomGroup = useCallback((groupId: string, nodeIds: string[]) => {
    forceRelayoutRef.current = true;
    setCustomGroups((prev) =>
      prev.map((g) => {
        if (g.id !== groupId) return g;
        const set = new Set(g.nodeIds);
        for (const id of nodeIds) set.add(id);
        return { ...g, nodeIds: Array.from(set) };
      })
    );
    setMultiSelectedNodeIds(new Set());
  }, []);

  const toggleMultiSelectedNode = useCallback((id: string) => {
    setMultiSelectedNodeIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  const clearMultiSelection = useCallback(() => {
    setMultiSelectedNodeIds(new Set());
  }, []);

  const [showTransitiveEdges, setShowTransitiveEdges] = useState<boolean>(() => {
    try {
      const saved = localStorage.getItem('ce_show_transitive_edges');
      return saved !== null ? saved === 'true' : true;
    } catch { }
    return true;
  });

  const handleToggleTransitiveEdges = useCallback(() => {
    forceRelayoutRef.current = true;
    setShowTransitiveEdges((prev) => {
      const next = !prev;
      try {
        localStorage.setItem('ce_show_transitive_edges', String(next));
      } catch { }
      return next;
    });
  }, []);

  // Font Size State (7px to 24px, default 10px)
  const [fontSize, setFontSize] = useState<number>(() => {
    try {
      const saved = localStorage.getItem('ce_domain_font_size');
      if (saved) {
        const val = parseInt(saved, 10);
        if (!isNaN(val) && val >= 7 && val <= 24) return val;
      }
    } catch { }
    return 10;
  });
  const fontSizeRef = useRef<number>(fontSize);
  fontSizeRef.current = fontSize;

  const handleFontSizeChange = useCallback((newSize: number) => {
    const clamped = Math.max(7, Math.min(24, newSize));
    setFontSize(clamped);
    try {
      localStorage.setItem('ce_domain_font_size', String(clamped));
    } catch { }
    if (cyRef.current) {
      cyRef.current.style()
        .selector('node')
        .style('font-size', `${clamped}px`)
        .selector('edge')
        .style('font-size', `${Math.max(6, Math.round(clamped * 0.82))}px`)
        .update();
    }
  }, []);

  // Unified Right Side Panel State
  const [rightSidebarTab, setRightSidebarTab] = useState<'info' | 'orbits' | 'visibility' | 'settings'>(() => {
    if (typeof window !== 'undefined' && window.localStorage) {
      try {
        const saved = localStorage.getItem('ce_domain_sidebar_tab');
        if (saved === 'info' || saved === 'orbits' || saved === 'visibility' || saved === 'settings') return saved;
        if (saved === 'hidden') return 'visibility';
      } catch {}
    }
    return 'info';
  });
  const [isRightSidebarOpen, setIsRightSidebarOpen] = useState<boolean>(() => {
    if (typeof window !== 'undefined' && window.localStorage) {
      try {
        const saved = localStorage.getItem('ce_domain_sidebar_open');
        if (saved !== null) return saved === 'true';
      } catch {}
    }
    return true;
  });

  const handleSidebarTabChange = useCallback((tab: 'info' | 'orbits' | 'visibility' | 'settings') => {
    setRightSidebarTab(tab);
    if (typeof window !== 'undefined' && window.localStorage) {
      try {
        localStorage.setItem('ce_domain_sidebar_tab', tab);
      } catch {}
    }
  }, []);

  const handleToggleSidebar = useCallback((open?: boolean) => {
    setIsRightSidebarOpen((prev) => {
      const next = open !== undefined ? open : !prev;
      if (typeof window !== 'undefined' && window.localStorage) {
        try {
          localStorage.setItem('ce_domain_sidebar_open', String(next));
        } catch {}
      }
      return next;
    });
  }, []);

  const [isInspectorCollapsed, setIsInspectorCollapsed] = useState(false);
  const [isEdgeInspectorCollapsed, setIsEdgeInspectorCollapsed] = useState(false);

  // Auto-switch to Info tab and reveal native VS Code Inspector sidebar when a node or edge is selected
  useEffect(() => {
    if (selectedNode !== null || selectedEdge !== null) {
      setRightSidebarTab('info');
      setIsRightSidebarOpen(true);
      if (typeof window !== 'undefined' && window.localStorage) {
        try {
          localStorage.setItem('ce_domain_sidebar_tab', 'info');
          localStorage.setItem('ce_domain_sidebar_open', 'true');
        } catch {}
      }
      getVsCodeApi()?.postMessage({
        type: 'FOCUS_INSPECTOR_VIEW',
        preserveFocus: true,
      });
    }
  }, [selectedNode, selectedEdge]);

  // Restore all hidden entities across individual hides, category filters, and orbits
  const restoreAllHiddenNodes = useCallback(() => {
    const cmd = new RestoreAllHiddenNodesCommand(
      new Set(hiddenNodeIds),
      new Set(hiddenTypes),
      new Set(hiddenOrbitTiers),
      new Set(forcedVisibleNodeIds),
      hideSingleConnectionDbs,
      hideIsolatedNodes,
      (nodes) => setHiddenNodeIds(nodes),
      (types) => setHiddenTypes(types),
      (tiers) => setHiddenOrbitTiers(tiers),
      (nodes) => setForcedVisibleNodeIds(nodes),
      (val) => setHideSingleConnectionDbs(val),
      (val) => setHideIsolatedNodes(val),
      () => {
        forceRelayoutRef.current = true;
        setRelayoutTrigger((n) => n + 1);
      }
    );
    if (executeCommand) {
      executeCommand(cmd);
    } else {
      cmd.execute();
    }
  }, [hiddenNodeIds, hiddenTypes, hiddenOrbitTiers, forcedVisibleNodeIds, hideSingleConnectionDbs, hideIsolatedNodes, executeCommand]);

  // Auto-relayout on filter toggle (persisted)
  const [autoRelayoutOnFilter, setAutoRelayoutOnFilter] = useState<boolean>(() => {
    try {
      const saved = localStorage.getItem('ce_auto_relayout_on_filter');
      if (saved !== null) return saved === 'true';
    } catch { }
    return true;
  });
  const [relayoutTrigger, setRelayoutTrigger] = useState(0);
  const forceRelayoutRef = useRef(false);

  const handleAutoRelayoutChange = useCallback((enabled: boolean) => {
    setAutoRelayoutOnFilter(enabled);
    try {
      localStorage.setItem('ce_auto_relayout_on_filter', String(enabled));
    } catch { }
  }, []);

  const handleForceRelayout = useCallback(() => {
    forceRelayoutRef.current = true;
    setRelayoutTrigger((prev) => prev + 1);
  }, []);

  const prevLayoutRef = useRef(layoutName);
  const [isPreparing, setIsPreparing] = useState(true);
  const [preparingStatus, setPreparingStatus] = useState('Analyzing domain microservices...');
  const activeLayoutRef = useRef<cytoscape.Layouts | null>(null);

  // Zoom & Node Spacing (Air) State
  const [currentZoom, setCurrentZoom] = useState<number>(1.0);

  const loadSpacingForLayout = useCallback((prefix: string, layout: DomainLayoutName): number => {
    try {
      const saved = localStorage.getItem(`${prefix}spacing_${layout}`);
      if (saved) {
        const val = parseFloat(saved);
        if (!isNaN(val) && val >= 0.4 && val <= 3.5) return val;
      }
    } catch { }
    return 1.0;
  }, []);

  const loadOrbitDistanceForLayout = useCallback((prefix: string, layout: DomainLayoutName): number => {
    try {
      const saved = localStorage.getItem(`${prefix}orbit_distance_${layout}`);
      if (saved) {
        const val = parseInt(saved, 10);
        if (!isNaN(val) && val >= 100 && val <= 900) return val;
      }
      if (layout.startsWith('concentric')) {
        const legacy = localStorage.getItem('ce_orbit_distance');
        if (legacy) {
          const val = parseInt(legacy, 10);
          if (!isNaN(val) && val >= 100 && val <= 900) return val;
        }
      }
    } catch { }
    return 280;
  }, []);

  const loadMinOrbitDistForLayout = useCallback((prefix: string, layout: DomainLayoutName): number => {
    try {
      const saved = localStorage.getItem(`${prefix}min_orbit_dist_${layout}`);
      if (saved) {
        const val = parseInt(saved, 10);
        if (!isNaN(val) && val >= 50 && val <= 400) return val;
      }
      if (layout.startsWith('concentric')) {
        const legacy = localStorage.getItem('ce_min_orbit_node_distance');
        if (legacy) {
          const val = parseInt(legacy, 10);
          if (!isNaN(val) && val >= 50 && val <= 400) return val;
        }
      }
    } catch { }
    return 125;
  }, []);
  const [spacingFactor, setSpacingFactor] = useState<number>(() => {
    return loadSpacingForLayout(storagePrefix, layoutName);
  });
  const spacingFactorRef = useRef<number>(1.0);
  spacingFactorRef.current = spacingFactor;

  // Distance between concentric orbits (px)
  const [orbitDistance, setOrbitDistance] = useState<number>(() => {
    return loadOrbitDistanceForLayout(storagePrefix, layoutName);
  });
  const orbitDistanceRef = useRef<number>(orbitDistance);
  orbitDistanceRef.current = orbitDistance;

  const handleOrbitDistanceChange = useCallback((newDist: number) => {
    const clamped = Math.max(100, Math.min(900, Math.round(newDist / 10) * 10));
    setOrbitDistance(clamped);
    orbitDistanceRef.current = clamped;
    try {
      localStorage.setItem(`${storagePrefix}orbit_distance_${layoutName}`, String(clamped));
    } catch { }
    forceRelayoutRef.current = true;
    setRelayoutTrigger((prev) => prev + 1);
  }, []);

  // Minimum distance between nodes on the same orbit (px)
  const [minOrbitNodeDistance, setMinOrbitNodeDistance] = useState<number>(() => {
    return loadMinOrbitDistForLayout(storagePrefix, layoutName);
  });
  const minOrbitNodeDistanceRef = useRef<number>(minOrbitNodeDistance);
  minOrbitNodeDistanceRef.current = minOrbitNodeDistance;

  const handleMinOrbitNodeDistanceChange = useCallback((newDist: number) => {
    const clamped = Math.max(50, Math.min(400, Math.round(newDist / 5) * 5));
    setMinOrbitNodeDistance(clamped);
    minOrbitNodeDistanceRef.current = clamped;
    try {
      localStorage.setItem(`${storagePrefix}min_orbit_dist_${layoutName}`, String(clamped));
    } catch { }
    forceRelayoutRef.current = true;
    setRelayoutTrigger((prev) => prev + 1);
  }, []);

  const loadBaseIconSizeForLayout = useCallback((prefix: string, layout: DomainLayoutName): number => {
    try {
      const saved = localStorage.getItem(`${prefix}base_icon_size_${layout}`);
      if (saved) {
        const val = parseInt(saved, 10);
        if (!isNaN(val) && val >= 28 && val <= 84) return val;
      }
      const globalSaved = localStorage.getItem(`${prefix}base_icon_size`);
      if (globalSaved) {
        const val = parseInt(globalSaved, 10);
        if (!isNaN(val) && val >= 28 && val <= 84) return val;
      }
    } catch { }
    return 52;
  }, []);

  // Base Icon Size (28px - 84px, default 52px baseline for real icon size computations)
  const [baseIconSize, setBaseIconSize] = useState<number>(() => {
    return loadBaseIconSizeForLayout(storagePrefix, layoutName);
  });
  const baseIconSizeRef = useRef<number>(baseIconSize);
  baseIconSizeRef.current = baseIconSize;

  const handleBaseIconSizeChange = useCallback((newSize: number) => {
    const clamped = Math.max(28, Math.min(84, Math.round(newSize)));
    setBaseIconSize(clamped);
    baseIconSizeRef.current = clamped;
    try {
      localStorage.setItem(`${storagePrefix}base_icon_size_${layoutName}`, String(clamped));
      localStorage.setItem(`${storagePrefix}base_icon_size`, String(clamped));
    } catch { }
    forceRelayoutRef.current = true;
    setRelayoutTrigger((prev) => prev + 1);
  }, [storagePrefix, layoutName]);

  // Mouse wheel zoom sensitivity (default 1.0x with universal normalized wheel)
  const [wheelSensitivity, setWheelSensitivity] = useState<number>(() => {
    try {
      const saved = localStorage.getItem('ce_wheel_sensitivity_v2');
      if (saved) {
        const val = parseFloat(saved);
        if (!isNaN(val) && val >= 0.2 && val <= 3.0) return val;
      }
    } catch { }
    return 1.0;
  });
  const wheelSensitivityRef = useRef<number>(1.0);
  wheelSensitivityRef.current = wheelSensitivity;

  const basePositionsRef = useRef<Map<string, cytoscape.Position>>(new Map());
  const centroidRef = useRef<{ cx: number; cy: number }>({ cx: 0, cy: 0 });
  const rawGraphRef = useRef<any>(null);

  // Preserve hidden filters and entities when updating/refreshing graph
  const prevGraphRef = useRef<any>(graph);
  useEffect(() => {
    if (prevGraphRef.current === graph) return;
    prevGraphRef.current = graph;

    // Do NOT clear hiddenNodeIds, hiddenTypes, hiddenOrbitTiers, or forcedVisibleNodeIds on graph refresh!
    // Update or clear selectedNode if it no longer exists in the updated graph
    setSelectedNode((prev) => {
      if (!prev) return null;
      const updated = rawGraphRef.current?.detailMap?.get(prev.id);
      return updated || null;
    });
  }, [graph]);

  const toggleTypeVisibility = useCallback((kind: EntityKind) => {
    forceRelayoutRef.current = true;
    setHiddenTypes((prev) => {
      const next = new Set(prev);
      if (next.has(kind)) {
        next.delete(kind);
      } else {
        next.add(kind);
      }
      return next;
    });
    setForcedVisibleNodeIds((prev) => {
      const next = new Set(prev);
      for (const id of prev) {
        if (rawGraphRef.current?.detailMap.get(id)?.kind === kind) {
          next.delete(id);
        }
      }
      return next;
    });
    setSelectedNode((curr) => (curr?.kind === kind ? null : curr));
    if (cyRef.current) {
      cyRef.current.elements().removeClass('highlighted dimmed');
    }
  }, []);

  const toggleOrbitVisibility = useCallback((levelIndex: number) => {
    forceRelayoutRef.current = true;
    setHiddenOrbitTiers((prev) => {
      const next = new Set(prev);
      if (next.has(levelIndex)) {
        next.delete(levelIndex);
      } else {
        next.add(levelIndex);
      }
      return next;
    });
    setForcedVisibleNodeIds((prev) => {
      const next = new Set(prev);
      for (const id of prev) {
        const raw = rawGraphRef.current;
        const node = raw?.detailMap.get(id);
        const base = raw?.echelonMap.get(id);
        if (base === undefined) continue;
        if (effectiveOrbitTier(orbitConfigRef.current, node?.kind, base) === levelIndex) {
          next.delete(id);
        }
      }
      return next;
    });
    if (cyRef.current) {
      cyRef.current.elements().removeClass('highlighted dimmed');
    }
  }, []);

  // Context Menu State for Right-Click on Nodes
  const [contextMenu, setContextMenu] = useState<{
    x: number;
    y: number;
    nodeId: string;
    nodeName: string;
    nodeKind: EntityKind;
  } | null>(null);
  const [isAddGroupSubmenuOpen, setIsAddGroupSubmenuOpen] = useState(false);

  useEffect(() => {
    if (!contextMenu) {
      setIsAddGroupSubmenuOpen(false);
    }
  }, [contextMenu]);

  const hideNode = useCallback((nodeId: string) => {
    const nodeName = rawGraphRef.current?.detailMap?.get(nodeId)?.displayName || nodeId;
    const prevHidden = new Set(hiddenNodeIds);
    const nextHidden = new Set(prevHidden);
    nextHidden.add(nodeId);

    const prevForced = new Set(forcedVisibleNodeIds);
    const nextForced = new Set(prevForced);
    nextForced.delete(nodeId);

    const cmd = new HideNodeCommand(
      nodeName,
      prevHidden,
      nextHidden,
      prevForced,
      nextForced,
      (nodes) => setHiddenNodeIds(nodes),
      (nodes) => setForcedVisibleNodeIds(nodes),
      () => {
        forceRelayoutRef.current = true;
        setRelayoutTrigger((n) => n + 1);
      }
    );

    if (executeCommand) {
      executeCommand(cmd);
    } else {
      cmd.execute();
    }

    setSelectedNode((curr) => (curr?.id === nodeId ? null : curr));
    setContextMenu(null);
    if (cyRef.current) {
      cyRef.current.elements().removeClass('highlighted dimmed');
    }
  }, [hiddenNodeIds, forcedVisibleNodeIds, executeCommand]);

  const getDownstreamNodeIds = useCallback((rootNodeId: string): string[] => {
    const downstreamIds: string[] = [];
    const queue = [rootNodeId];
    const visited = new Set<string>([rootNodeId]);

    while (queue.length > 0) {
      const curr = queue.shift()!;
      const outgoing = rawGraphRef.current?.outAdj?.get(curr) || [];
      for (const edge of outgoing) {
        if (!visited.has(edge.target)) {
          visited.add(edge.target);
          downstreamIds.push(edge.target);
          queue.push(edge.target);
        }
      }
    }
    return downstreamIds;
  }, []);

  const hideDownstreamNodes = useCallback((rootNodeId: string) => {
    const downstreamIds = getDownstreamNodeIds(rootNodeId);

    if (downstreamIds.length > 0) {
      const downstreamSet = new Set(downstreamIds);
      const rootNodeName = rawGraphRef.current?.detailMap?.get(rootNodeId)?.displayName || rootNodeId;
      const prevHidden = new Set(hiddenNodeIds);
      const nextHidden = new Set(prevHidden);
      for (const id of downstreamIds) nextHidden.add(id);

      const prevForced = new Set(forcedVisibleNodeIds);
      const nextForced = new Set(prevForced);
      for (const id of downstreamIds) nextForced.delete(id);

      const cmd = new HideDownstreamCommand(
        rootNodeName,
        downstreamIds.length,
        prevHidden,
        nextHidden,
        prevForced,
        nextForced,
        (nodes) => setHiddenNodeIds(nodes),
        (nodes) => setForcedVisibleNodeIds(nodes),
        () => {
          forceRelayoutRef.current = true;
          setRelayoutTrigger((n) => n + 1);
        }
      );

      if (executeCommand) {
        executeCommand(cmd);
      } else {
        cmd.execute();
      }

      setSelectedNode((curr) => (curr && downstreamSet.has(curr.id) ? null : curr));
    }
    setContextMenu(null);
    if (cyRef.current) {
      cyRef.current.elements().removeClass('highlighted dimmed');
    }
  }, [hiddenNodeIds, forcedVisibleNodeIds, executeCommand, getDownstreamNodeIds]);

  const hideAllOfKind = useCallback((kind: EntityKind) => {
    const prevHiddenTypes = new Set(hiddenTypes);
    const nextHiddenTypes = new Set(prevHiddenTypes);
    nextHiddenTypes.add(kind);

    const cmd = new HideAllOfKindCommand(
      kind,
      prevHiddenTypes,
      nextHiddenTypes,
      (types) => setHiddenTypes(types),
      () => {
        forceRelayoutRef.current = true;
        setRelayoutTrigger((n) => n + 1);
      }
    );

    if (executeCommand) {
      executeCommand(cmd);
    } else {
      cmd.execute();
    }

    setSelectedNode((curr) => (curr?.kind === kind ? null : curr));
    setContextMenu(null);
    if (cyRef.current) {
      cyRef.current.elements().removeClass('highlighted dimmed');
    }
  }, [hiddenTypes, executeCommand]);

  const unhideNode = useCallback((nodeId: string) => {
    const nodeName = rawGraphRef.current?.detailMap?.get(nodeId)?.displayName || nodeId;
    const prevHidden = new Set(hiddenNodeIds);
    const nextHidden = new Set(prevHidden);
    nextHidden.delete(nodeId);

    const prevForced = new Set(forcedVisibleNodeIds);
    const nextForced = new Set(prevForced);
    nextForced.add(nodeId);

    const cmd = new UnhideNodeCommand(
      nodeName,
      prevHidden,
      nextHidden,
      prevForced,
      nextForced,
      (nodes) => setHiddenNodeIds(nodes),
      (nodes) => setForcedVisibleNodeIds(nodes),
      () => {
        forceRelayoutRef.current = true;
        setRelayoutTrigger((n) => n + 1);
      }
    );

    if (executeCommand) {
      executeCommand(cmd);
    } else {
      cmd.execute();
    }
  }, [hiddenNodeIds, forcedVisibleNodeIds, executeCommand]);

  const handleSelectNodeById = useCallback((id: string) => {
    const detail = effectiveDetailMapRef.current?.get(id) || rawGraphRef.current?.detailMap?.get(id);
    if (detail) {
      setSelectedEdge(null);
      setSelectedNode(detail);
    }
    const cy = cyRef.current;
    if (cy) {
      const ele = cy.$id(id);
      if (ele && ele.length > 0) {
        cy.center(ele);
        cy.elements().removeClass('highlighted dimmed');
        ele.addClass('highlighted');
      }
    }
  }, []);

  const unhideAll = restoreAllHiddenNodes;

  // 1. Synthesize Domain Entities & Infrastructure Nodes from GraphData
  const rawGraph = useMemo(() => {
    const cyNodes: cytoscape.NodeDefinition[] = [];
    const detailMap = new Map<string, SelectedNodeDetail>();
    const domainNameMap = new Map<string, { name: string; displayName: string; framework?: string; language?: string; color?: string }>();
    const domainEntitiesMap = new Map<string, SelectedNodeDetail[]>();

    let appCount = 0;
    let serviceCount = 0;
    let workerCount = 0;
    let cliCount = 0;
    let testSuiteCount = 0;
    let dbCount = 0;
    let topicCount = 0;
    let extCount = 0;
    let libCount = 0;

    for (const node of graph?.nodes || []) {
      // Ignore NuGet / npm package dependencies
      if (node.kind === 'Package') continue;

      const nodeKind = node.kind as EntityKind;
      const badgeMeta = getEntityBadgeMeta(nodeKind);
      let tag = `:${badgeMeta.label}`;
      let bgColor = badgeMeta.bgColor;
      let borderColor = badgeMeta.borderColor;
      let size = 52;

      if (nodeKind === 'Type') {
        serviceCount++;
      } else if (nodeKind === 'App' || nodeKind === 'Endpoint' || nodeKind === 'EntryPoint') {
        appCount++;
        size = 56;
      } else if (nodeKind === 'Worker') {
        workerCount++;
      } else if (nodeKind === 'CliTool') {
        cliCount++;
      } else if (nodeKind === 'TestSuite') {
        testSuiteCount++;
      } else if (nodeKind === 'Database' || nodeKind === 'Table' || nodeKind === 'Procedure') {
        dbCount++;
        size = 50;
      } else if (nodeKind === 'Topic') {
        topicCount++;
        size = 50;
      } else if (nodeKind === 'ExternalService' || nodeKind === 'CloudService') {
        extCount++;
        size = 50;
      } else if (nodeKind === 'Library') {
        libCount++;
        size = 46;
      } else {
        serviceCount++;
      }

      const isDb = nodeKind === 'Database';
      const isTable = nodeKind === 'Table';
      const isProc = nodeKind === 'Procedure';
      const isTopic = nodeKind === 'Topic';
      const isExt = nodeKind === 'ExternalService' || nodeKind === 'CloudService';
      const isLib = nodeKind === 'Library';

      const rawDomain =
        node.properties?.domain ||
        node.properties?.domainDisplayName ||
        node.properties?.bounded_context ||
        (isDb || isTable || isProc ? 'Databases' : isTopic ? 'Messaging' : isExt ? 'External Services' : 'Core Platform');

      const domain = rawDomain.trim();
      const domainDisplayName = node.properties?.domainDisplayName || domain;

      if (!domainNameMap.has(domain)) {
        domainNameMap.set(domain, {
          name: domain,
          displayName: domainDisplayName,
          framework: node.properties?.framework,
          language: node.properties?.language || node.properties?.project_type,
        });
      }

      const displayName = node.displayName || node.name || node.id;
      let secondaryRoles: string[] = [];
      const rawSecRoles = node.properties?.secondary_roles;
      if (rawSecRoles) {
        if (rawSecRoles.trim().startsWith('[')) {
          try {
            const parsed = JSON.parse(rawSecRoles);
            if (Array.isArray(parsed)) {
              secondaryRoles = parsed.map(String);
            }
          } catch {
            // fallback
          }
        }
        if (secondaryRoles.length === 0) {
          secondaryRoles = rawSecRoles.split(',').map((s) => s.trim()).filter(Boolean);
        }
      }
      const primaryRole = node.properties?.primary_role || node.properties?.role;

      const parsedFileCount = node.properties?.file_count ? parseInt(node.properties.file_count, 10) : undefined;
      let moduleFiles: string[] = [];
      if (node.properties?.files) {
        try {
          const parsed = JSON.parse(node.properties.files);
          if (Array.isArray(parsed)) moduleFiles = parsed.map(String);
        } catch { }
      }

      const projectsList: DomainProjectInfo[] =
        moduleFiles.length > 0
          ? moduleFiles.map((fp, idx) => ({
              id: `${node.id}:file:${idx}`,
              name: fp.split(/[/\\]/).slice(-2).join('/'),
              kind: 'File',
              filePath: fp,
              lineStart: 1,
            }))
          : [
              {
                id: node.id,
                name: node.name || displayName,
                kind: node.kind,
                primaryRole,
                secondaryRoles: secondaryRoles.length > 0 ? secondaryRoles : undefined,
                filePath: node.filePath,
                lineStart: node.lineStart ?? (node.properties?.start_line ? parseInt(node.properties.start_line, 10) : undefined),
                isLibrary: isLib,
                gitBranch: node.properties?.git_branch,
              },
            ];

      const explicitTier =
        node.properties?.tier !== undefined && !isNaN(Number(node.properties.tier))
          ? Math.min(6, Math.max(0, Number(node.properties.tier)))
          : undefined;

      const detail: SelectedNodeDetail = {
        id: node.id,
        name: node.name || displayName,
        displayName,
        domain,
        kind: nodeKind,
        primaryRole,
        secondaryRoles: secondaryRoles.length > 0 ? secondaryRoles : undefined,
        properties: node.properties,
        displayTag: tag,
        bgColor,
        borderColor,
        framework: node.properties?.framework || node.properties?.db_type || node.properties?.broker_type || node.properties?.service_type,
        language: node.properties?.language || node.properties?.project_type,
        gitBranch: node.properties?.git_branch,
        primaryFilePath: node.filePath || moduleFiles[0],
        primaryLineStart: node.lineStart ?? (node.properties?.start_line ? parseInt(node.properties.start_line, 10) : undefined),
        projects: projectsList,
        fileCount: Number.isFinite(parsedFileCount) ? parsedFileCount : undefined,
        tier: explicitTier,
        tierLabel: node.properties?.tier_label || undefined,
        tierReason: node.properties?.tier_reason || undefined,
        inboundCallsCount: 0,
        outboundCallsCount: 0,
        dbCount: 0,
        messagingCount: 0,
      };
      detailMap.set(node.id, detail);

      let dList = domainEntitiesMap.get(domain);
      if (!dList) {
        dList = [];
        domainEntitiesMap.set(domain, dList);
      }
      dList.push(detail);

      const clampedName = displayName.length > 32 ? displayName.slice(0, 30).trimEnd() + '…' : displayName;

      cyNodes.push({
        group: 'nodes',
        data: {
          id: node.id,
          name: node.name || displayName,
          displayName,
          displayLabel: clampedName,
          badgeSvg: getNodeBadgeSvgDataUri(nodeKind),
          rawLabel: displayName,
          kind: nodeKind,
          bgColor,
          borderColor,
          size,
        },
      });
    }

    // 2. Synthesize Macro Edges
    interface EdgeAggregator {
      source: string;
      target: string;
      category: 'service_call' | 'database' | 'messaging' | 'external' | 'library' | 'test';
      label: string;
      count: number;
    }
    const macroEdges = new Map<string, EdgeAggregator>();

    const inCalls = new Map<string, number>();
    const outCalls = new Map<string, number>();
    const dbUsage = new Map<string, Set<string>>();
    const msgUsage = new Map<string, Set<string>>();
    const topicPublishers = new Map<string, Set<string>>();
    const topicSubscribers = new Map<string, Set<string>>();
    const directPubSubChords = new Map<string, { source: string; target: string; label: string; count: number }>();

    const validNodeIdSet = new Set(cyNodes.map((n) => n.data.id as string));

    for (const edge of graph?.edges || []) {
      if (!validNodeIdSet.has(edge.source) || !validNodeIdSet.has(edge.target)) continue;
      if (edge.source === edge.target) continue;

      const srcDetail = detailMap.get(edge.source);
      const tgtDetail = detailMap.get(edge.target);
      if (!srcDetail || !tgtDetail) continue;

      let cat: 'service_call' | 'database' | 'messaging' | 'external' | 'library' | 'test' | null = null;
      let label = 'CALLS';
      const edgeWeight = edge.properties?.call_count
        ? Math.max(1, parseInt(edge.properties.call_count, 10) || 1)
        : 1;

      const isDb =
        tgtDetail.kind === 'Database' ||
        tgtDetail.kind === 'Table' ||
        tgtDetail.kind === 'Procedure' ||
        srcDetail.kind === 'Database' ||
        srcDetail.kind === 'Table' ||
        srcDetail.kind === 'Procedure' ||
        edge.category === 'database' ||
        edge.kind === 'USES_DB' ||
        edge.kind === 'READS_TABLE' ||
        edge.kind === 'WRITES_TABLE' ||
        edge.kind === 'CALLS_PROCEDURE';
      const isTopic = tgtDetail.kind === 'Topic' || srcDetail.kind === 'Topic' || edge.category === 'messaging';
      const isExt = tgtDetail.kind === 'ExternalService' || edge.category === 'external';

      if (isDb) {
        cat = 'database';
        label = tgtDetail.kind === 'Table' || tgtDetail.kind === 'Procedure' ? (edge.kind || 'USES') : 'USES_DB';
        if (!dbUsage.has(edge.source)) dbUsage.set(edge.source, new Set());
        dbUsage.get(edge.source)!.add(edge.target);
      } else if (isTopic) {
        cat = 'messaging';
        const isPub = edge.kind === 'PUBLISHES_TO' || edge.kind === 'PUBLISHED_BY' || edge.kind === 'PUBLISHES';
        const isSub =
          !isPub &&
          (edge.kind === 'SUBSCRIBES_TO' ||
            edge.kind === 'SUBSCRIBED_BY' ||
            edge.kind === 'SUBSCRIBES' ||
            (tgtDetail.kind !== 'Topic' && srcDetail.kind === 'Topic'));
        label = isSub ? 'SUBSCRIBES' : 'PUBLISHES';

        const tNode = tgtDetail.kind === 'Topic' ? edge.target : edge.source;
        const sNode = tgtDetail.kind === 'Topic' ? edge.source : edge.target;

        if (isSub) {
          if (!topicSubscribers.has(tNode)) topicSubscribers.set(tNode, new Set());
          topicSubscribers.get(tNode)!.add(sNode);
        } else {
          if (!topicPublishers.has(tNode)) topicPublishers.set(tNode, new Set());
          topicPublishers.get(tNode)!.add(sNode);
        }

        if (tgtDetail.kind === 'Topic') {
          if (!msgUsage.has(edge.source)) msgUsage.set(edge.source, new Set());
          msgUsage.get(edge.source)!.add(edge.target);
        } else if (srcDetail.kind === 'Topic') {
          if (!msgUsage.has(edge.target)) msgUsage.set(edge.target, new Set());
          msgUsage.get(edge.target)!.add(edge.source);
        }
      } else if (isExt) {
        cat = 'external';
        label = 'CALLS';
      } else if (
        tgtDetail.kind === 'Library' &&
        (edge.properties?.is_resource_link === 'true' ||
          edge.category === 'library' ||
          edge.kind === 'LIBRARY' ||
          edge.kind === 'DEPENDS_ON')
      ) {
        cat = 'library';
        label = (edge as any).label || (edge as any).properties?.label || edge.kind || 'CALLS';
      } else if (
        edge.category === 'service_call' ||
        edge.category === 'dependency' ||
        edge.kind === 'SERVICE_CALL' ||
        edge.kind === 'CALLS' ||
        edge.kind === 'USES_TYPE' ||
        edge.kind === 'IMPLEMENTS' ||
        edge.kind === 'INHERITS_FROM' ||
        edge.kind === 'EXPOSED_BY' ||
        edge.kind === 'IMPLEMENTED_BY' ||
        edge.kind === 'CALLS_ENDPOINT' ||
        edge.kind === 'TRIGGERS' ||
        edge.kind === 'MODULE_DEPENDS_ON' ||
        (srcDetail.kind === 'Module' && tgtDetail.kind === 'Module')
      ) {
        cat = 'service_call';
        label = edge.kind === 'MODULE_DEPENDS_ON' ? 'DEPENDS_ON' : edge.kind || 'CALLS';
      } else if (
        edge.kind === 'SUBSCRIBES_TO' ||
        edge.kind === 'SUBSCRIBED_BY' ||
        edge.kind === 'PUBLISHES_TO' ||
        edge.kind === 'PUBLISHED_BY'
      ) {
        const chordLabel =
          edge.kind === 'SUBSCRIBES_TO' || edge.kind === 'SUBSCRIBED_BY' ? 'SUBSCRIBES' : 'PUBLISHES';
        const chordKey = `${edge.source}->${edge.target}`;
        const existingChord = directPubSubChords.get(chordKey);
        if (existingChord) {
          existingChord.count += edgeWeight;
        } else {
          directPubSubChords.set(chordKey, {
            source: edge.source,
            target: edge.target,
            label: chordLabel,
            count: edgeWeight,
          });
        }
        continue;
      } else if (
        edge.category === 'library' ||
        edge.kind === 'LIBRARY' ||
        edge.kind === 'DEPENDS_ON'
      ) {
        cat = 'library';
        label = (edge as any).label || (edge as any).properties?.label || edge.kind || 'DEPENDS_ON';
      } else if (
        edge.category === 'test' ||
        edge.kind === 'TESTS'
      ) {
        cat = 'test';
        label = 'TESTS';
      } else if (edge.properties?.is_resource_link === 'true') {
        cat = 'external';
        label = edge.kind || 'USES';
      }

      if (!cat) continue;

      if (cat === 'service_call' || cat === 'library') {
        outCalls.set(edge.source, (outCalls.get(edge.source) || 0) + edgeWeight);
        inCalls.set(edge.target, (inCalls.get(edge.target) || 0) + edgeWeight);
      }

      const isTrans = Boolean(
        edge.isTransitive ||
        edge.properties?.is_transitive === 'true' ||
        edge.properties?.semantic_lifted === 'true'
      );
      const isInferred = Boolean(
        isTrans ||
        edge.isInferred ||
        edge.properties?.inferred === 'true' ||
        edge.properties?.is_late_bound === 'true' ||
        edge.properties?.is_semantic === 'true'
      );

      const key = `${edge.source}->${edge.target}:${cat}`;
      const existing = macroEdges.get(key);
      if (existing) {
        existing.count += edgeWeight;
        (existing as any).isTransitive = (existing as any).isTransitive || isTrans;
        (existing as any).isInferred = (existing as any).isInferred || isInferred;
      } else {
        macroEdges.set(key, {
          source: edge.source,
          target: edge.target,
          category: cat,
          kind: edge.kind,
          label,
          count: edgeWeight,
          isTransitive: isTrans,
          isInferred: isInferred,
        } as any);
      }
    }

    // Update node details with traffic stats
    for (const [id, detail] of detailMap.entries()) {
      detail.inboundCallsCount = inCalls.get(id) || 0;
      detail.outboundCallsCount = outCalls.get(id) || 0;
      detail.dbCount = dbUsage.get(id)?.size || 0;
      detail.messagingCount = msgUsage.get(id)?.size || 0;
    }

    // 3. Raw Macro Edges & Outgoing Adjacency
    const rawEdges: Array<{
      id: string;
      source: string;
      target: string;
      category: 'service_call' | 'database' | 'messaging' | 'external' | 'library' | 'test';
      kind?: string;
      label: string;
      count: number;
      isTransitive: boolean;
      isInferred: boolean;
    }> = [];
    const outAdj = new Map<string, Array<{ target: string; category: 'service_call' | 'database' | 'messaging' | 'external' | 'library' | 'test'; label: string; count: number }>>();

    let serviceCallsCount = 0;
    let messagesCount = 0;

    for (const [key, e] of macroEdges.entries()) {
      if (!validNodeIdSet.has(e.source) || !validNodeIdSet.has(e.target)) continue;

      if (e.category === 'service_call') serviceCallsCount += e.count;
      else if (e.category === 'messaging') messagesCount += e.count;

      rawEdges.push({
        id: key,
        source: e.source,
        target: e.target,
        category: e.category,
        kind: (e as any).kind || (e as any).label,
        label: e.label,
        count: e.count,
        isTransitive: Boolean((e as any).isTransitive),
        isInferred: Boolean((e as any).isInferred),
      });

      let list = outAdj.get(e.source);
      if (!list) {
        list = [];
        outAdj.set(e.source, list);
      }
      list.push({
        target: e.target,
        category: e.category,
        label: e.label,
        count: e.count,
      });
    }

    // 4. Compute connected source services for each database node
    const dbSourceServicesMap = new Map<string, Set<string>>();
    for (const e of rawEdges) {
      if (e.category === 'database' || e.label === 'USES_DB') {
        let set = dbSourceServicesMap.get(e.target);
        if (!set) {
          set = new Set<string>();
          dbSourceServicesMap.set(e.target, set);
        }
        set.add(e.source);
      }
    }

    let singleConnDbCount = 0;
    let sharedDbCount = 0;
    for (const node of cyNodes) {
      if ((node.data as any).kind === 'Database') {
        const id = node.data.id as string;
        const inboundCount = dbSourceServicesMap.get(id)?.size || 0;
        if (inboundCount <= 1) {
          singleConnDbCount++;
        } else {
          sharedDbCount++;
        }
      }
    }

    // 5. Compute Stable Architectural Echelon Tiers (0..5) across Full Graph
    const echelonNodesInput = cyNodes.map((n) => ({
      id: n.data.id as string,
      kind: (n.data as any).kind as string,
      displayName: (n.data as any).displayName as string | undefined,
    }));
    const echelonMap = computeEchelonTiers(echelonNodesInput, rawEdges);
    const tierReasonMap = computeEchelonTierReasons(echelonNodesInput, rawEdges);

    for (const node of cyNodes) {
      const id = node.data.id as string;
      const detail = detailMap.get(id);
      const explicitTier = detail?.tier;
      const ech = explicitTier !== undefined ? explicitTier : (echelonMap.get(id) ?? 2);
      echelonMap.set(id, ech);
      (node.data as any).echelonTier = ech;
      if (detail) {
        detail.tier = ech;
        detail.tierLabel =
          detail.tierLabel ||
          (ech === 0
            ? 'Tier 0 (Ingress / Gateway & Frontends)'
            : ech === 1
            ? 'Tier 1 (First Echelon / Gateway-facing)'
            : ech === 2
            ? 'Tier 2 (Message Topics & Queues)'
            : ech === 3
            ? 'Tier 3 (Second Echelon / Internal Domain Services & Workers)'
            : 'Tier 4 (Databases & External Infrastructure)');
        detail.tierReason = detail.tierReason || tierReasonMap.get(id);
      }
    }

    // 6. Compute Domain Groups for Grid View
    const domainGroups: DomainGroupSummary[] = [];
    for (const [domKey, entities] of domainEntitiesMap.entries()) {
      const dMeta = domainNameMap.get(domKey);
      const displayName = dMeta?.displayName || domKey;

      const workloadNodes = entities.filter(
        (e) => e.kind === 'Service' || e.kind === 'App' || e.kind === 'Worker' || e.kind === 'CliTool' || e.kind === 'TestSuite'
      );
      if (workloadNodes.length === 0) continue;

      const domainNodeIds = new Set(workloadNodes.map((n) => n.id));
      const dDbs = new Map<string, SelectedNodeDetail>();
      const dTopics = new Map<string, SelectedNodeDetail>();
      const dExt = new Map<string, SelectedNodeDetail>();

      for (const edge of rawEdges) {
        if (domainNodeIds.has(edge.source)) {
          const targetDetail = detailMap.get(edge.target);
          if (targetDetail) {
            if (targetDetail.kind === 'Database') dDbs.set(targetDetail.id, targetDetail);
            else if (targetDetail.kind === 'Topic') dTopics.set(targetDetail.id, targetDetail);
            else if (targetDetail.kind === 'ExternalService') dExt.set(targetDetail.id, targetDetail);
          }
        } else if (domainNodeIds.has(edge.target)) {
          const sourceDetail = detailMap.get(edge.source);
          if (sourceDetail) {
            if (sourceDetail.kind === 'Database') dDbs.set(sourceDetail.id, sourceDetail);
            else if (sourceDetail.kind === 'Topic') dTopics.set(sourceDetail.id, sourceDetail);
            else if (sourceDetail.kind === 'ExternalService') dExt.set(sourceDetail.id, sourceDetail);
          }
        }
      }

      domainGroups.push({
        domainKey: domKey.toLowerCase(),
        displayName,
        nodes: workloadNodes,
        databases: Array.from(dDbs.values()),
        topics: Array.from(dTopics.values()),
        externalServices: Array.from(dExt.values()),
      });
    }

    domainGroups.sort((a, b) => a.displayName.localeCompare(b.displayName));

    return {
      allNodes: cyNodes,
      rawEdges,
      detailMap,
      echelonMap,
      domainGroups,
      outAdj,
      dbSourceServicesMap,
      topicPublishers,
      topicSubscribers,
      directPubSubChords,
      domainMap: domainNameMap,
      counts: {
        ingress: appCount,
        services: serviceCount,
        workers: workerCount,
        cli: cliCount,
        testSuites: testSuiteCount,
        libraries: libCount,
        databases: dbCount,
        singleConnDbs: singleConnDbCount,
        sharedDbs: sharedDbCount,
        topics: topicCount,
        external: extCount,
        serviceCalls: serviceCallsCount,
        messages: messagesCount,
      },
    };
  }, [graph]);
  rawGraphRef.current = rawGraph;

  // 2. Visible Graph Memo with Directional Transitive Contraction
  const { elements, visibleNodes, visibleEdges, hiddenNodeIdSet, stats, hiddenCount, effectiveDetailMap, effectiveRawEdges } = useMemo(() => {
    const hiddenNodeIdSet = new Set<string>();
    for (const node of rawGraph.allNodes) {
      const id = node.data.id as string;
      if (forcedVisibleNodeIds.has(id)) {
        continue;
      }
      const kind = (node.data as any).kind as EntityKind;
      const ech = effectiveOrbitTier(
        orbitConfigRef.current,
        kind,
        (node.data as any).echelonTier ?? rawGraph.echelonMap.get(id) ?? 2
      );
      let isHidden = hiddenNodeIds.has(id) || hiddenTypes.has(kind) || hiddenOrbitTiers.has(ech);
      if (!isHidden && hideSingleConnectionDbs && kind === 'Database') {
        const inboundCount = rawGraph.dbSourceServicesMap.get(id)?.size || 0;
        if (inboundCount <= 1) {
          isHidden = true;
        }
      }
      if (isHidden) {
        hiddenNodeIdSet.add(id);
      }
    }

    const unhiddenRawNodes = rawGraph.allNodes.filter((n) => !hiddenNodeIdSet.has(n.data.id as string));
    const unhiddenNodeIds = new Set<string>(unhiddenRawNodes.map((n) => n.data.id as string));

    const nodeToGroupId = new Map<string, string>();
    const synthesizedGroupNodes: SelectedNodeDetail[] = [];
    const synthesizedGroupCyNodes: cytoscape.ElementDefinition[] = [];

    // 1. Custom Groups (precedence over category collapse)
    for (const group of customGroups) {
      if (!group.collapsed) continue;
      const memberIds = group.nodeIds.filter((id) => unhiddenNodeIds.has(id));
      if (memberIds.length < 1) continue;

      const groupId = `group:custom:${group.id}`;
      for (const mId of memberIds) {
        nodeToGroupId.set(mId, groupId);
      }

      const memberNodes = memberIds
        .map((id) => rawGraph.detailMap.get(id)!)
        .filter(Boolean);
      const memberTiers = memberNodes.map((m) => m.tier ?? rawGraph.echelonMap.get(m.id) ?? 2);
      const minTier = memberTiers.length > 0 ? Math.min(...memberTiers) : 2;

      const allSameKind = memberNodes.length > 0 && memberNodes.every((m) => m.kind === memberNodes[0].kind);
      const groupKind: EntityKind = allSameKind ? memberNodes[0].kind : 'Group';
      const badgeMeta = getEntityBadgeMeta(groupKind);
      const displayName = `${group.name} (${memberNodes.length})`;
      const badgeSvg = getNodeBadgeSvgDataUri(groupKind, { isGroup: true, count: memberNodes.length });

      const groupDetail: SelectedNodeDetail = {
        id: groupId,
        name: group.name,
        displayName,
        kind: groupKind,
        isGroup: true,
        groupType: 'custom',
        customGroupId: group.id,
        groupKind: allSameKind ? groupKind : undefined,
        memberIds,
        memberNodes,
        internalConnections: [],
        displayTag: allSameKind ? badgeMeta.code : 'GRP',
        bgColor: badgeMeta.bgColor,
        borderColor: badgeMeta.borderColor,
        tier: minTier,
        tierLabel: ORBIT_TITLES[minTier] || `Tier ${minTier + 1}`,
        inboundCallsCount: 0,
        outboundCallsCount: 0,
        dbCount: 0,
        messagingCount: 0,
        projects: memberNodes.flatMap((m) => m.projects || []),
      };

      synthesizedGroupNodes.push(groupDetail);
      synthesizedGroupCyNodes.push({
        group: 'nodes',
        classes: 'group-node',
        data: {
          id: groupId,
          name: group.name,
          displayLabel: displayName,
          kind: groupKind,
          isGroup: 'true',
          groupType: 'custom',
          size: GROUP_NODE_SIZE,
          bgColor: badgeMeta.bgColor,
          borderColor: badgeMeta.borderColor,
          badgeSvg,
          echelonTier: minTier,
          inboundCalls: 0,
          outboundCalls: 0,
          dbCount: 0,
          messagingCount: 0,
        },
      });
    }

    // 2. Category Groups (for kinds in groupedTypes)
    const CATEGORY_PLURAL_LABELS: Record<string, string> = {
      Service: 'Services',
      Worker: 'Workers',
      TestSuite: 'Tests',
      Database: 'Databases',
      Table: 'Tables',
      Procedure: 'Procedures',
      Topic: 'Topics',
      ExternalService: 'External',
      CloudService: 'Cloud Services',
      App: 'Apps',
      CliTool: 'CLI Tools',
      Library: 'Libraries',
      Endpoint: 'Endpoints',
      EntryPoint: 'EntryPoints',
      Type: 'Classes',
      Module: 'Modules',
    };

    for (const kind of groupedTypes) {
      if (hiddenTypes.has(kind)) continue;
      const memberIds = unhiddenRawNodes
        .filter((n) => {
          const k = (n.data as any).kind as EntityKind;
          const id = n.data.id as string;
          return k === kind && !nodeToGroupId.has(id);
        })
        .map((n) => n.data.id as string);

      if (memberIds.length < 2) continue; // Only collapse if >= 2 visible nodes

      const groupId = `group:kind:${kind}`;
      for (const mId of memberIds) {
        nodeToGroupId.set(mId, groupId);
      }

      const memberNodes = memberIds
        .map((id) => rawGraph.detailMap.get(id)!)
        .filter(Boolean);
      const memberTiers = memberNodes.map((m) => m.tier ?? rawGraph.echelonMap.get(m.id) ?? 2);
      const minTier = memberTiers.length > 0 ? Math.min(...memberTiers) : 2;

      const badgeMeta = getEntityBadgeMeta(kind);
      const pluralLabel = CATEGORY_PLURAL_LABELS[kind] || `${kind}s`;
      const displayName = `${pluralLabel} (${memberNodes.length})`;
      const badgeSvg = getNodeBadgeSvgDataUri(kind, { isGroup: true, count: memberNodes.length });

      const groupDetail: SelectedNodeDetail = {
        id: groupId,
        name: pluralLabel,
        displayName,
        kind,
        isGroup: true,
        groupType: 'category',
        groupKind: kind,
        memberIds,
        memberNodes,
        internalConnections: [],
        displayTag: badgeMeta.code,
        bgColor: badgeMeta.bgColor,
        borderColor: badgeMeta.borderColor,
        tier: minTier,
        tierLabel: ORBIT_TITLES[minTier] || `Tier ${minTier + 1}`,
        inboundCallsCount: 0,
        outboundCallsCount: 0,
        dbCount: 0,
        messagingCount: 0,
        projects: memberNodes.flatMap((m) => m.projects || []),
      };

      synthesizedGroupNodes.push(groupDetail);
      synthesizedGroupCyNodes.push({
        group: 'nodes',
        classes: 'group-node',
        data: {
          id: groupId,
          name: pluralLabel,
          displayLabel: displayName,
          kind,
          isGroup: 'true',
          groupType: 'category',
          size: GROUP_NODE_SIZE,
          bgColor: badgeMeta.bgColor,
          borderColor: badgeMeta.borderColor,
          badgeSvg,
          echelonTier: minTier,
          inboundCalls: 0,
          outboundCalls: 0,
          dbCount: 0,
          messagingCount: 0,
        },
      });
    }

    const effectiveDetailMap = new Map<string, SelectedNodeDetail>(rawGraph.detailMap);
    for (const g of synthesizedGroupNodes) {
      effectiveDetailMap.set(g.id, g);
    }
    effectiveDetailMapRef.current = effectiveDetailMap;

    const ungroupedNodes = unhiddenRawNodes.filter((n) => !nodeToGroupId.has(n.data.id as string));
    const initialVisibleNodes = [...ungroupedNodes, ...synthesizedGroupCyNodes];
    const visibleNodeIds = new Set<string>(initialVisibleNodes.map((n) => n.data.id as string));

    // Collect all interactions between connected node pairs (deduplicating parallel lines)
    interface RawInteraction {
      source: string;
      target: string;
      originalSourceId?: string;
      originalSourceName?: string;
      originalTargetId?: string;
      originalTargetName?: string;
      category: 'service_call' | 'database' | 'messaging' | 'external' | 'library' | 'test';
      kind: string;
      label: string;
      count: number;
      isTransitive: boolean;
      isInferred?: boolean;
      viaNames?: string[];
    }

    const pairInteractionsMap = new Map<string, RawInteraction[]>();
    const getPairKey = (u: string, v: string) => (u < v ? `${u}--${v}` : `${v}--${u}`);

    const recordInteraction = (item: RawInteraction) => {
      const pKey = getPairKey(item.source, item.target);
      let list = pairInteractionsMap.get(pKey);
      if (!list) {
        list = [];
        pairInteractionsMap.set(pKey, list);
      }
      const viaKey = (item.viaNames || []).join('>');
      const origSrc = item.originalSourceId || item.source;
      const origTgt = item.originalTargetId || item.target;

      const existing = list.find(
        (x) =>
          x.source === item.source &&
          x.target === item.target &&
          (x.originalSourceId || x.source) === origSrc &&
          (x.originalTargetId || x.target) === origTgt &&
          x.category === item.category &&
          x.kind === item.kind &&
          (x.viaNames || []).join('>') === viaKey
      );
      if (existing) {
        existing.count += item.count;
        existing.isTransitive = existing.isTransitive || item.isTransitive;
        existing.isInferred = existing.isInferred || Boolean(item.isInferred);
      } else {
        list.push(item);
      }
    };

    // 1. Direct edges between raw nodes (mapping through nodeToGroupId)
    const directVisibleEdgeKeys = new Set<string>();
    for (const e of rawGraph.rawEdges) {
      if (unhiddenNodeIds.has(e.source) && unhiddenNodeIds.has(e.target)) {
        const effU = nodeToGroupId.get(e.source) ?? e.source;
        const effV = nodeToGroupId.get(e.target) ?? e.target;

        if (effU === effV && e.source !== e.target) {
          // Internal connection within group
          const gDetail = effectiveDetailMap.get(effU);
          if (gDetail && gDetail.internalConnections) {
            gDetail.internalConnections.push({
              id: `${effU}:int:${e.source}->${e.target}:${e.category}:${e.kind || e.label}`,
              sourceId: e.source,
              sourceName: rawGraph.detailMap.get(e.source)?.displayName || e.source,
              targetId: e.target,
              targetName: rawGraph.detailMap.get(e.target)?.displayName || e.target,
              category: e.category,
              kind: e.kind || e.label || 'CALLS',
              label: e.label,
              count: e.count,
              isTransitive: Boolean(e.isTransitive),
              isInferred: Boolean(e.isInferred),
            });
          }
        } else if (effU !== effV && visibleNodeIds.has(effU) && visibleNodeIds.has(effV)) {
          recordInteraction({
            source: effU,
            target: effV,
            originalSourceId: e.source,
            originalSourceName: rawGraph.detailMap.get(e.source)?.displayName || e.source,
            originalTargetId: e.target,
            originalTargetName: rawGraph.detailMap.get(e.target)?.displayName || e.target,
            category: e.category,
            kind: e.label || 'CALLS',
            label: e.label,
            count: e.count,
            isTransitive: Boolean(e.isTransitive),
            isInferred: Boolean(e.isInferred),
          });
          directVisibleEdgeKeys.add(`${effU}->${effV}`);
        }
      }
    }

    // 2. Contract hidden Topic nodes: connecting publishers to subscribers
    for (const topicId of hiddenNodeIdSet) {
      if (!rawGraph.topicPublishers.has(topicId) && !rawGraph.topicSubscribers.has(topicId)) continue;
      const pubs = rawGraph.topicPublishers.get(topicId) || new Set<string>();
      const subs = rawGraph.topicSubscribers.get(topicId) || new Set<string>();
      const tDetail = rawGraph.detailMap.get(topicId);
      const tName = tDetail?.displayName || topicId;

      for (const p of pubs) {
        for (const s of subs) {
          if (p === s) continue;
          if (unhiddenNodeIds.has(s) && unhiddenNodeIds.has(p)) {
            const effP = nodeToGroupId.get(p) ?? p;
            const effS = nodeToGroupId.get(s) ?? s;

            if (effP === effS && p !== s) {
              const gDetail = effectiveDetailMap.get(effP);
              if (gDetail && gDetail.internalConnections) {
                gDetail.internalConnections.push({
                  id: `${effP}:int:${p}->${s}:messaging:PUBLISHES_TO`,
                  sourceId: p,
                  sourceName: rawGraph.detailMap.get(p)?.displayName || p,
                  targetId: s,
                  targetName: rawGraph.detailMap.get(s)?.displayName || s,
                  category: 'messaging',
                  kind: 'PUBLISHES_TO',
                  label: `Topic: ${tName}`,
                  count: 1,
                  isTransitive: true,
                  isInferred: true,
                  viaNames: [tName],
                });
              }
            } else if (effP !== effS && visibleNodeIds.has(effP) && visibleNodeIds.has(effS)) {
              if (directVisibleEdgeKeys.has(`${effP}->${effS}`)) continue;

              recordInteraction({
                source: effP,
                target: effS,
                originalSourceId: p,
                originalSourceName: rawGraph.detailMap.get(p)?.displayName || p,
                originalTargetId: s,
                originalTargetName: rawGraph.detailMap.get(s)?.displayName || s,
                category: 'messaging',
                kind: 'PUBLISHES_TO',
                label: `Topic: ${tName}`,
                count: 1,
                isTransitive: true,
                isInferred: true,
                viaNames: [tName],
              });
            }
          }
        }
      }
    }

    // 3. Direct service-to-service pub/sub chords (when no visible topic connects them)
    for (const chord of rawGraph.directPubSubChords.values()) {
      if (unhiddenNodeIds.has(chord.source) && unhiddenNodeIds.has(chord.target)) {
        const effSrc = nodeToGroupId.get(chord.source) ?? chord.source;
        const effTgt = nodeToGroupId.get(chord.target) ?? chord.target;

        const hasVisibleTopic = Array.from(rawGraph.topicPublishers.keys()).some(
          (tId) =>
            unhiddenNodeIds.has(tId) &&
            ((rawGraph.topicPublishers.get(tId)?.has(chord.target) &&
              rawGraph.topicSubscribers.get(tId)?.has(chord.source)) ||
              (rawGraph.topicPublishers.get(tId)?.has(chord.source) &&
                rawGraph.topicSubscribers.get(tId)?.has(chord.target)))
        );

        if (!hasVisibleTopic) {
          if (effSrc === effTgt && chord.source !== chord.target) {
            const gDetail = effectiveDetailMap.get(effSrc);
            if (gDetail && gDetail.internalConnections) {
              gDetail.internalConnections.push({
                id: `${effSrc}:int:${chord.source}->${chord.target}:messaging:${chord.label}`,
                sourceId: chord.source,
                sourceName: rawGraph.detailMap.get(chord.source)?.displayName || chord.source,
                targetId: chord.target,
                targetName: rawGraph.detailMap.get(chord.target)?.displayName || chord.target,
                category: 'messaging',
                kind: chord.label,
                label: `${chord.label} (event-bus)`,
                count: chord.count,
                isTransitive: true,
                isInferred: true,
                viaNames: ['event-bus'],
              });
            }
          } else if (effSrc !== effTgt && visibleNodeIds.has(effSrc) && visibleNodeIds.has(effTgt)) {
            recordInteraction({
              source: effSrc,
              target: effTgt,
              originalSourceId: chord.source,
              originalSourceName: rawGraph.detailMap.get(chord.source)?.displayName || chord.source,
              originalTargetId: chord.target,
              originalTargetName: rawGraph.detailMap.get(chord.target)?.displayName || chord.target,
              category: 'messaging',
              kind: chord.label,
              label: `${chord.label} (event-bus)`,
              count: chord.count,
              isTransitive: true,
              isInferred: true,
              viaNames: ['event-bus'],
            });
          }
        }
      }
    }

    // 4. Invariant: Service calls (RPC/HTTP) are strictly point-to-point and never transitively chained through intermediate hidden services.

    // 5. Synthesize Exactly One Bundled Edge Per Connected Pair {u, v}
    const visibleEdges: cytoscape.ElementDefinition[] = [];
    const edgeDetailMap = new Map<string, SelectedEdgeDetail>();

    for (const [pairKey, rawInteractions] of pairInteractionsMap.entries()) {
      const interactions = showTransitiveEdges
        ? rawInteractions
        : rawInteractions.filter((i) => !i.isTransitive);

      if (interactions.length === 0) continue;

      const parts = pairKey.split('--');
      const nodeU = parts[0];
      const nodeV = parts[1];

      const hasUtoV = interactions.some((i) => i.source === nodeU && i.target === nodeV);
      const hasVtoU = interactions.some((i) => i.source === nodeV && i.target === nodeU);
      const isBidirectional = hasUtoV && hasVtoU;

      const edgeSource = hasUtoV ? nodeU : nodeV;
      const edgeTarget = hasUtoV ? nodeV : nodeU;

      const hasServiceCall = interactions.some((i) => i.category === 'service_call' || i.category === 'library');
      const hasMessaging = interactions.some((i) => i.category === 'messaging');
      const hasDatabase = interactions.some((i) => i.category === 'database');
      const allTransitive = interactions.every((i) => i.isTransitive);

      let edgeCategory: 'service_call' | 'database' | 'messaging' | 'external' | 'mixed';
      if (hasServiceCall && hasMessaging) {
        edgeCategory = 'mixed';
      } else if (hasServiceCall) {
        edgeCategory = 'service_call';
      } else if (hasMessaging) {
        edgeCategory = 'messaging';
      } else if (hasDatabase) {
        edgeCategory = 'database';
      } else {
        edgeCategory = 'external';
      }

      const totalCount = interactions.reduce((sum, i) => sum + i.count, 0);
      const serviceCallsCount = interactions.filter((i) => i.category === 'service_call' || i.category === 'library').reduce((s, i) => s + i.count, 0);
      const messagingCount = interactions.filter((i) => i.category === 'messaging').reduce((s, i) => s + i.count, 0);
      const dbCount = interactions.filter((i) => i.category === 'database').reduce((s, i) => s + i.count, 0);
      const transitiveCount = interactions.filter((i) => i.isTransitive).reduce((s, i) => s + i.count, 0);
      const inferredCount = interactions.filter((i) => i.isInferred).reduce((s, i) => s + i.count, 0);

      let edgeLabel: string;
      if (isBidirectional) {
        if (edgeCategory === 'mixed') {
          edgeLabel = `⇄ ${serviceCallsCount} calls, ${messagingCount} msgs`;
        } else if (edgeCategory === 'service_call') {
          edgeLabel = `⇄ ${serviceCallsCount} calls`;
        } else if (edgeCategory === 'messaging') {
          edgeLabel = `⇄ ${messagingCount} msgs`;
        } else {
          edgeLabel = `⇄ ${totalCount}`;
        }
      } else {
        if (edgeCategory === 'mixed') {
          edgeLabel = `${serviceCallsCount} calls, ${messagingCount} msgs`;
        } else if (edgeCategory === 'service_call') {
          edgeLabel = serviceCallsCount > 1 ? `${serviceCallsCount} calls` : 'CALLS';
        } else if (edgeCategory === 'messaging') {
          edgeLabel = messagingCount > 1 ? `${messagingCount} msgs` : 'MSGS';
        } else if (edgeCategory === 'database') {
          edgeLabel = 'USES_DB';
        } else {
          edgeLabel = totalCount > 1 ? `${totalCount}` : 'CALLS';
        }
      }

      const edgeId = `edge:${pairKey}`;

      visibleEdges.push({
        group: 'edges',
        classes: allTransitive ? 'transitive-edge' : '',
        data: {
          id: edgeId,
          source: edgeSource,
          target: edgeTarget,
          category: edgeCategory,
          label: edgeLabel,
          count: totalCount,
          isTransitive: allTransitive ? 'true' : 'false',
          isBidirectional: isBidirectional ? 'true' : 'false',
        },
      });

      const sDetail = effectiveDetailMap.get(edgeSource);
      const tDetail = effectiveDetailMap.get(edgeTarget);

      edgeDetailMap.set(edgeId, {
        id: edgeId,
        sourceId: edgeSource,
        sourceName: sDetail?.displayName || edgeSource,
        sourceKind: sDetail?.kind || 'Service',
        sourceTag: sDetail?.displayTag || ':Service',
        sourceBgColor: sDetail?.bgColor || '#e53935',
        targetId: edgeTarget,
        targetName: tDetail?.displayName || edgeTarget,
        targetKind: tDetail?.kind || 'Service',
        targetTag: tDetail?.displayTag || ':Service',
        targetBgColor: tDetail?.bgColor || '#e53935',
        isBidirectional,
        hasForward: interactions.some((i) => i.source === edgeSource && i.target === edgeTarget),
        hasReverse: interactions.some((i) => i.source === edgeTarget && i.target === edgeSource),
        totalInteractions: totalCount,
        directCallsCount: serviceCallsCount,
        messagingCount,
        transitiveCount,
        inferredCount,
        dbCount,
        primaryCategory: edgeCategory,
        items: interactions.map((i, idx) => ({
          id: `${edgeId}:item:${idx}`,
          sourceId: i.originalSourceId || i.source,
          sourceName: i.originalSourceName || effectiveDetailMap.get(i.source)?.displayName || i.source,
          targetId: i.originalTargetId || i.target,
          targetName: i.originalTargetName || effectiveDetailMap.get(i.target)?.displayName || i.target,
          category: i.category,
          kind: i.kind,
          label: i.label,
          count: i.count,
          isTransitive: i.isTransitive,
          isInferred: Boolean(i.isInferred),
          viaNames: i.viaNames,
        })),
      });

      // Update group node metrics
      if (sDetail?.isGroup) {
        sDetail.outboundCallsCount += (serviceCallsCount + interactions.filter(i => i.category === 'external').reduce((s, i) => s + i.count, 0));
        sDetail.dbCount += dbCount;
        sDetail.messagingCount += messagingCount;
      }
      if (tDetail?.isGroup) {
        tDetail.inboundCallsCount += (serviceCallsCount + interactions.filter(i => i.category === 'external').reduce((s, i) => s + i.count, 0));
        tDetail.dbCount += dbCount;
        tDetail.messagingCount += messagingCount;
      }
    }

    // Reflect updated metrics onto synthesized Cytoscape group nodes
    for (const gn of synthesizedGroupCyNodes) {
      const d = effectiveDetailMap.get(gn.data.id as string);
      if (d) {
        (gn.data as any).inboundCalls = d.inboundCallsCount;
        (gn.data as any).outboundCalls = d.outboundCallsCount;
        (gn.data as any).dbCount = d.dbCount;
        (gn.data as any).messagingCount = d.messagingCount;
      }
    }

    // Build effectiveRawEdges for DomainInspector
    const effectiveRawEdges: Array<{
      id: string;
      source: string;
      target: string;
      category: 'service_call' | 'database' | 'messaging' | 'external' | 'library' | 'test';
      label: string;
      count: number;
      kind?: string;
    }> = [];

    for (const e of rawGraph.rawEdges) {
      if (unhiddenNodeIds.has(e.source) && unhiddenNodeIds.has(e.target)) {
        const effS = nodeToGroupId.get(e.source) ?? e.source;
        const effT = nodeToGroupId.get(e.target) ?? e.target;
        if (effS !== effT) {
          effectiveRawEdges.push({
            id: e.id,
            source: effS,
            target: effT,
            category: e.category,
            label: e.label,
            count: e.count,
            kind: e.kind,
          });
        }
      }
    }

    edgeDetailMapRef.current = edgeDetailMap;

    let finalVisibleNodes = initialVisibleNodes;
    if (hideIsolatedNodes) {
      const connectedNodeIds = new Set<string>();
      for (const e of visibleEdges) {
        const s = (e.data as any)?.source as string;
        const t = (e.data as any)?.target as string;
        if (s && t) {
          connectedNodeIds.add(s);
          connectedNodeIds.add(t);
        }
      }
      for (const n of initialVisibleNodes) {
        const nid = n.data.id as string;
        if (!connectedNodeIds.has(nid) && !forcedVisibleNodeIds.has(nid)) {
          hiddenNodeIdSet.add(nid);
        }
      }
      finalVisibleNodes = initialVisibleNodes.filter((n) => !hiddenNodeIdSet.has(n.data.id as string));
    }

    const visibleNodeIdSet = new Set(finalVisibleNodes.map((n) => n.data.id as string));
    const safeVisibleEdges = visibleEdges.filter((e) => {
      const s = (e.data as any)?.source as string;
      const t = (e.data as any)?.target as string;
      return s && t && visibleNodeIdSet.has(s) && visibleNodeIdSet.has(t);
    });

    // Apply semantic shape, dynamic sizing (uniform | sqrt-weight | degree), and badge content mode (icon | icon-code | metric)
    const styledVisibleNodes: cytoscape.ElementDefinition[] = finalVisibleNodes.map((n) => {
      const nid = n.data.id as string;
      const detail = effectiveDetailMap.get(nid);
      const kind = ((n.data as any).kind || detail?.kind || 'Service') as EntityKind;
      const badgeMeta = getEntityBadgeMeta(kind);
      const isGroup = Boolean(detail?.isGroup || (n.data as any).isGroup === 'true');
      const memberCount = detail?.memberNodes?.length || detail?.memberIds?.length || 0;

      const totalConnections =
        (detail?.inboundCallsCount || 0) +
        (detail?.outboundCallsCount || 0) +
        (detail?.dbCount || 0) +
        (detail?.messagingCount || 0);
      const fileCount =
        detail?.fileCount ??
        (detail?.properties?.file_count ? parseInt(detail.properties.file_count, 10) : undefined);
      const projectCount = detail?.projects?.length || 1;
      const weightVal =
        fileCount && !isNaN(fileCount) && fileCount > 0
          ? fileCount
          : Math.max(1, projectCount * 3 + totalConnections);

      // Base icon size is the baseline for all real icon/node size computations
      const baseNodeScale = (n.data as any).size ? (n.data as any).size / 52 : 1.0;
      const groupSize = Math.round(baseIconSize * (68 / 52));
      let computedSize: number = isGroup ? groupSize : Math.round(baseIconSize * baseNodeScale);

      if (nodeSizingMode === 'sqrt-weight') {
        const base = isGroup ? Math.round(baseIconSize * (44 / 52)) : Math.round(baseIconSize * (30 / 52));
        const factor = (baseIconSize / 52) * 9.5;
        const minSize = Math.round(baseIconSize * (36 / 52));
        const maxSize = Math.round(baseIconSize * (92 / 52));
        computedSize = Math.round(Math.max(minSize, Math.min(maxSize, base + factor * Math.sqrt(weightVal))));
      } else if (nodeSizingMode === 'degree') {
        const base = isGroup ? Math.round(baseIconSize * (44 / 52)) : Math.round(baseIconSize * (36 / 52));
        const factor = (baseIconSize / 52) * 6.5;
        const minSize = Math.round(baseIconSize * (38 / 52));
        const maxSize = Math.round(baseIconSize * (90 / 52));
        computedSize = Math.round(Math.max(minSize, Math.min(maxSize, base + factor * Math.sqrt(Math.max(1, totalConnections)))));
      }

      const metricText =
        fileCount && !isNaN(fileCount) && fileCount > 0
          ? `${fileCount}f`
          : projectCount > 1
          ? `${projectCount}p`
          : `${totalConnections}`;

      const badgeSvg = getNodeBadgeSvgDataUri(kind, {
        isGroup,
        count: memberCount,
        contentMode: nodeContentMode,
        metricText,
      });

      return {
        ...n,
        data: {
          ...n.data,
          shape: badgeMeta.shape,
          size: computedSize,
          badgeSvg,
        },
      };
    });

    let visibleCalls = 0;
    let visibleMsgs = 0;
    let directCalls = 0;
    let transitiveCalls = 0;
    for (const e of safeVisibleEdges) {
      const cat = (e.data as any).category;
      const count = (e.data as any).count || 1;
      const isTrans = (e.data as any).isTransitive === 'true';
      if (isTrans) {
        transitiveCalls += count;
      } else {
        directCalls += count;
      }
      if (cat === 'service_call') visibleCalls += count;
      else if (cat === 'messaging') visibleMsgs += count;
    }

    return {
      elements: [...styledVisibleNodes, ...safeVisibleEdges],
      visibleNodes: styledVisibleNodes,
      visibleEdges: safeVisibleEdges,
      hiddenNodeIdSet,
      hiddenCount: rawGraph.allNodes.length - styledVisibleNodes.length,
      effectiveDetailMap,
      effectiveRawEdges,
      stats: {
        total: styledVisibleNodes.length,
        ingress: rawGraph.counts.ingress,
        services: rawGraph.counts.services,
        databases: rawGraph.counts.databases,
        topics: rawGraph.counts.topics,
        serviceCalls: visibleCalls,
        messages: visibleMsgs,
        directCalls,
        transitiveCalls,
      },
    };
  }, [rawGraph, hiddenTypes, hiddenNodeIds, hiddenOrbitTiers, orbitConfig, forcedVisibleNodeIds, hideSingleConnectionDbs, hideIsolatedNodes, showTransitiveEdges, groupedTypes, customGroups, nodeContentMode, nodeSizingMode, baseIconSize]);

  const nodeDetailMap = effectiveDetailMap;

  // Derived docked panels visibility and active tab selection
  const hasHiddenEntities = hiddenCount > 0;
  const hasOrbitLegend = !isMatrixMode;
  const effectiveSidebarTab: 'info' | 'orbits' | 'visibility' | 'settings' = rightSidebarTab;


  // Initialize Cytoscape Instance
  useEffect(() => {
    if (!containerRef.current) return;

    const cy = cytoscape({
      container: containerRef.current,
      elements: [],
      style: CYTOSCAPE_STYLES,
      boxSelectionEnabled: false,
      userZoomingEnabled: false,
      autoungrabify: false,
      minZoom: 0.15,
      maxZoom: 3.5,
    });

    // Node Selection & Highlight
    cy.on('tap', 'node', (evt) => {
      const node = evt.target;
      const nodeId = node.id();
      const origEvent = evt.originalEvent as MouseEvent | undefined;
      const isMultiKey = Boolean(origEvent && (origEvent.shiftKey || origEvent.ctrlKey || origEvent.metaKey));

      if (isMultiKey) {
        toggleMultiSelectedNode(nodeId);
        return;
      }

      // Normal single click - clear multi-selection if any
      if (multiSelectedNodeIdsRef.current.size > 0) {
        clearMultiSelection();
      }

      const detail = effectiveDetailMapRef.current?.get(nodeId) || rawGraphRef.current?.detailMap?.get(nodeId);
      if (detail) {
        setSelectedEdge(null);
        setSelectedNode(detail);
        const gNode: GraphNode = {
          id: detail.id,
          name: detail.name,
          displayName: detail.displayName,
          kind: detail.kind as any,
          filePath: detail.primaryFilePath,
          properties: {
            kind: detail.kind,
            framework: detail.framework || '',
            language: detail.language || '',
            inboundCalls: String(detail.inboundCallsCount),
            outboundCalls: String(detail.outboundCallsCount),
            dbCount: String(detail.dbCount),
            messagingCount: String(detail.messagingCount),
          },
        };
        onSelectNodeRef.current?.(gNode);
      }

      // Highlight neighborhood
      cy.elements().removeClass('highlighted dimmed');
      const neighborhood = node.neighborhood().add(node);
      cy.elements().not(neighborhood).addClass('dimmed');
      node.connectedEdges().addClass('highlighted');
    });

    // Edge Selection -> Open Connection Inspector Panel
    cy.on('tap', 'edge', (evt) => {
      const edge = evt.target;
      const edgeId = edge.id();
      const detail = edgeDetailMapRef.current.get(edgeId);
      if (detail) {
        setSelectedNode(null);
        setSelectedEdge(detail);
        onSelectNodeRef.current?.(null);

        // Highlight this edge and its two endpoints, dim everything else
        cy.elements().removeClass('highlighted dimmed');
        const connectedNodes = edge.connectedNodes();
        const activeGroup = connectedNodes.add(edge);
        cy.elements().not(activeGroup).addClass('dimmed');
        edge.addClass('highlighted');
        connectedNodes.addClass('highlighted');
      }
    });

    // Background click -> Deselect & close context menu
    cy.on('tap', (evt) => {
      setContextMenu(null);
      if (evt.target === cy) {
        if (multiSelectedNodeIdsRef.current.size > 0) {
          clearMultiSelection();
        }
        setSelectedNode(null);
        setSelectedEdge(null);
        onSelectNodeRef.current?.(null);
        if (searchQueryRef.current.trim()) {
          applySearchHighlight(cy, searchQueryRef.current);
        } else {
          cy.elements().removeClass('highlighted dimmed');
        }
      }
    });

    // Double-click -> Drill down into Flow
    cy.on('dbltap', 'node', (evt) => {
      setContextMenu(null);
      const node = evt.target;
      const nodeId = node.id();
      const detail = effectiveDetailMapRef.current?.get(nodeId) || rawGraphRef.current?.detailMap?.get(nodeId);
      if (detail && (detail.kind === 'Service' || detail.kind === 'Ingress') && onFocusInFlowRef.current) {
        onFocusInFlowRef.current(detail.name);
      }
    });

    // Right-click / Context-tap -> Open context menu
    cy.on('cxttap', 'node', (evt) => {
      const node = evt.target;
      const nodeId = node.id();
      const detail = effectiveDetailMapRef.current?.get(nodeId) || rawGraphRef.current?.detailMap?.get(nodeId);
      if (!detail) return;

      const renderedPos = evt.renderedPosition;
      setContextMenu({
        x: renderedPos.x,
        y: renderedPos.y,
        nodeId: detail.id,
        nodeName: detail.displayName || detail.name,
        nodeKind: detail.kind,
      });
    });

    // Also close context menu on pan or zoom
    cy.on('pan zoom', () => {
      setContextMenu(null);
    });

    // Mouseover / Mouseout hover highlights
    cy.on('mouseover', 'node', (evt) => {
      containerRef.current?.classList.add('node-hover');
      evt.target.addClass('hovered');
    });

    cy.on('mouseout', 'node', (evt) => {
      containerRef.current?.classList.remove('node-hover');
      evt.target.removeClass('hovered');
    });

    cy.on('mouseover', 'edge', (evt) => {
      containerRef.current?.classList.add('edge-hover');
      evt.target.addClass('hovered');
    });

    cy.on('mouseout', 'edge', (evt) => {
      containerRef.current?.classList.remove('edge-hover');
      evt.target.removeClass('hovered');
    });

    // Viewport transform listener to keep HUD percentage and SVG guides synchronized
    let syncRafId: number | null = null;
    const handleViewportSync = () => {
      if (syncRafId !== null) return;
      syncRafId = requestAnimationFrame(() => {
        syncRafId = null;
        if (!cyRef.current) return;
        const z = cyRef.current.zoom();
        const p = cyRef.current.pan();
        setCurrentZoom(z);
        setCyTransform({ pan: { x: p.x, y: p.y }, zoom: z });
      });
    };
    cy.on('pan zoom resize', handleViewportSync);
    cy.on('layoutstop', () => {
      applyEdgeCurveMode(cy, edgeCurveModeRef.current, { x: 0, y: 0 }, curveFactorRef.current);
    });

    // Track manually dragged node positions relative to centroid
    cy.on('dragfree', 'node', (evt) => {
      const node = evt.target;
      const p = node.position();
      const { cx, cy: cyPos } = centroidRef.current;
      const currentFactor = spacingFactorRef.current || 1.0;
      const unscaledX = cx + (p.x - cx) / currentFactor;
      const unscaledY = cyPos + (p.y - cyPos) / currentFactor;
      basePositionsRef.current.set(node.id(), { x: unscaledX, y: unscaledY });
    });

    cyRef.current = cy;
    setIsCyReady(true);
    cy.style()
      .selector('node')
      .style('font-size', `${fontSizeRef.current}px`)
      .selector('edge')
      .style('font-size', `${Math.max(6, Math.round(fontSizeRef.current * 0.82))}px`)
      .update();
    applyEdgeLabelVisibility(cy, edgeLabelsOnHoverRef.current);

    const cleanupWheel = containerRef.current
      ? attachNormalizedCytoscapeWheel(containerRef.current, () => cyRef.current, () => wheelSensitivityRef.current)
      : () => {};

    let resizeObserver: ResizeObserver | null = null;
    if (typeof ResizeObserver !== 'undefined' && containerRef.current) {
      resizeObserver = new ResizeObserver(() => {
        if (cyRef.current) {
          cyRef.current.resize();
        }
      });
      resizeObserver.observe(containerRef.current);
    }

    return () => {
      if (syncRafId !== null) {
        cancelAnimationFrame(syncRafId);
        syncRafId = null;
      }
      if (resizeObserver) {
        resizeObserver.disconnect();
        resizeObserver = null;
      }
      cleanupWheel();
      cy.destroy();
      cyRef.current = null;
      setIsCyReady(false);
    };
  }, []);

  // Keep Cytoscape node and edge font sizes synchronized with fontSize state
  useEffect(() => {
    if (cyRef.current) {
      cyRef.current.style()
        .selector('node')
        .style('font-size', `${fontSize}px`)
        .selector('edge')
        .style('font-size', `${Math.max(6, Math.round(fontSize * 0.82))}px`)
        .update();
    }
  }, [fontSize]);

  // Keep Cytoscape edge label visibility synchronized with edgeLabelsOnHover state
  useEffect(() => {
    if (cyRef.current) {
      applyEdgeLabelVisibility(cyRef.current, edgeLabelsOnHover);
    }
  }, [edgeLabelsOnHover]);

  // Keyboard shortcut to hide selected node (Delete, Backspace, 'h') or dismiss inspector (Escape)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (!selectedNode) return;
      const tag = (e.target as HTMLElement)?.tagName?.toUpperCase();
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') {
        return;
      }
      if (e.key === 'Delete' || e.key === 'Backspace' || e.key === 'h' || e.key === 'H') {
        e.preventDefault();
        hideNode(selectedNode.id);
      } else if (e.key === 'Escape') {
        setSelectedNode(null);
        if (cyRef.current) {
          if (searchQueryRef.current.trim()) {
            applySearchHighlight(cyRef.current, searchQueryRef.current);
          } else {
            cyRef.current.elements().removeClass('highlighted dimmed');
          }
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [selectedNode, hideNode, applySearchHighlight]);

  // Synchronize Cytoscape element highlights/dimming with searchQuery
  useEffect(() => {
    if (cyRef.current && isCyReady) {
      applySearchHighlight(cyRef.current, searchQuery);
    }
  }, [searchQuery, isCyReady, elements, applySearchHighlight]);

  // Asynchronously load elements and execute layout without freezing the UI
  useEffect(() => {
    const cy = cyRef.current;
    if (!isCyReady || !cy) return;

    if (elements.length === 0) {
      cy.elements().remove();
      setIsPreparing(false);
      return;
    }

    let cancelled = false;

    const runAsyncLayout = async () => {
      const isForced = forceRelayoutRef.current;
      forceRelayoutRef.current = false;

      const layoutChanged = prevLayoutRef.current !== layoutName;
      prevLayoutRef.current = layoutName;

      // Capture existing positions of visible nodes
      const savedPositions = new Map<string, cytoscape.Position>();
      cy.nodes().forEach((n) => {
        savedPositions.set(n.id(), { ...n.position() });
      });
      const hadExisting = savedPositions.size > 0;
      const isInitial = !hadExisting;

      if (isInitial) {
        setIsPreparing(true);
        setPreparingStatus('Synthesizing domain clusters...');
        // Yield execution to the browser event loop so React renders the loading overlay on initial empty load
        await new Promise((resolve) => setTimeout(resolve, 16));
        if (cancelled) return;
      }

      cy.batch(() => {
        cy.elements().remove();
        cy.add(elements);
        applyEdgeLabelVisibility(cy, edgeLabelsOnHoverRef.current);
        if (searchQueryRef.current.trim()) {
          applySearchHighlight(cy, searchQueryRef.current);
        }

        if (hadExisting && !layoutChanged && layoutName === 'cose') {
          cy.nodes().forEach((n) => {
            const pos = savedPositions.get(n.id());
            if (pos) {
              n.position(pos);
            }
          });
        }
      });

      if (hadExisting && !layoutChanged && layoutName === 'cose') {
        let hasNewNodes = false;
        cy.nodes().forEach((n) => {
          if (!savedPositions.has(n.id())) {
            hasNewNodes = true;
          }
        });

        // If auto-relayout is disabled and no new nodes appeared (and not manually forced), keep current positions and return
        if (!autoRelayoutOnFilter && !hasNewNodes && !isForced) {
          setIsPreparing(false);
          return;
        }
      }

      if (cancelled) return;

      if (activeLayoutRef.current) {
        try {
          activeLayoutRef.current.stop();
        } catch {}
        activeLayoutRef.current = null;
      }

      if (layoutName === 'matrix' || layoutName === 'mermaid') {
        setConcentricGuides([]);
        setOrbitLegendItems([]);
        setSwimlaneGuides([]);
        setIslandGuides([]);
        setHiveGuides([]);
        baseConcentricGuidesRef.current = [];
        baseSwimlaneGuidesRef.current = [];
        baseIslandGuidesRef.current = [];
        baseHiveGuidesRef.current = [];
        setIsPreparing(false);
        return;
      }

      let layoutConfig: any;
      const spacing = spacingFactorRef.current || 1.0;

      const orbitCfg = orbitConfigRef.current;
      const visibleNodesInput = cy.nodes().map((n) => {
        const id = n.id();
        const kind = n.data('kind') as string;
        const ech = effectiveOrbitTier(
          orbitCfg,
          kind,
          (n.data('echelonTier') as number) ?? rawGraph.echelonMap.get(id) ?? 2
        );
        const detail = rawGraph.detailMap.get(id);
        const isInfra = detail?.kind === 'Database' || detail?.kind === 'Topic' || detail?.kind === 'ExternalService';
        const rawDomain = isInfra ? undefined : (detail?.properties?.domainId || detail?.domain || detail?.properties?.intent_domain)?.replace(/^domain:/i, '').toLowerCase();
        const domainDisplayName = rawDomain ? (detail?.properties?.domainDisplayName || rawGraph.domainMap?.get(detail?.domain || '')?.displayName) : undefined;
        return {
          id,
          echelonTier: ech,
          kind,
          name: detail?.name || id,
          displayName: detail?.displayName || id,
          domain: rawDomain,
          domainDisplayName,
        };
      });

      const visibleEdgesInput = cy.edges().map((e) => ({
        source: e.data('source') as string,
        target: e.data('target') as string,
        category: e.data('category') as string,
        count: (e.data('count') as number) || 1,
      }));

      if (
        layoutName === 'concentric' ||
        layoutName === 'concentric-equispaced' ||
        layoutName === 'concentric-barycentric' ||
        layoutName === 'concentric-polar-force' ||
        layoutName === 'concentric-sectors'
      ) {
        setSwimlaneGuides([]);
        setIslandGuides([]);
        setHiveGuides([]);
        baseSwimlaneGuidesRef.current = [];
        baseIslandGuidesRef.current = [];
        baseHiveGuidesRef.current = [];

        const effectiveOrder = orbitOrder(orbitCfg);
        let layoutResult: ConcentricLayoutResult;
        if (layoutName === 'concentric-sectors') {
          const nodeDomainMap = new Map<string, string>();
          const domainDetailsMap = new Map<string, { name: string; displayName: string }>();

          for (const [id, detail] of rawGraph.detailMap.entries()) {
            if (detail.kind === 'Database' || detail.kind === 'Topic' || detail.kind === 'ExternalService') {
              continue;
            }
            const raw = detail.properties?.domainId || detail.domain || detail.properties?.intent_domain;
            if (!raw || raw === 'Databases' || raw === 'Messaging' || raw === 'External Services') {
              continue;
            }
            const domainKey = raw.replace(/^domain:/i, '').toLowerCase();
            const displayName =
              detail.properties?.domainDisplayName ||
              rawGraph.domainMap.get(detail.domain || '')?.displayName ||
              raw;
            nodeDomainMap.set(id, domainKey);
            if (!domainDetailsMap.has(domainKey)) {
              domainDetailsMap.set(domainKey, { name: domainKey, displayName });
            }
          }

          layoutResult = computeConcentricSectorsLayout(
            visibleNodesInput,
            visibleEdgesInput,
            spacing,
            effectiveOrder,
            nodeDomainMap,
            domainDetailsMap,
            orbitDistanceRef.current,
            minOrbitNodeDistanceRef.current,
            customOrbitTitlesRef.current
          );
          const effSpacing = spacing || 1.0;
          setSectorGuides(layoutResult.sectors || []);
          baseSectorGuidesRef.current = (layoutResult.sectors || []).map((s) => ({
            ...s,
            innerRadius: s.innerRadius / effSpacing,
            outerRadius: s.outerRadius / effSpacing,
          }));
        } else {
          setSectorGuides([]);
          baseSectorGuidesRef.current = [];
          if (layoutName === 'concentric-equispaced') {
            layoutResult = computeConcentricEquispacedLayout(visibleNodesInput, visibleEdgesInput, spacing, effectiveOrder, orbitDistanceRef.current, minOrbitNodeDistanceRef.current, customOrbitTitlesRef.current);
          } else if (layoutName === 'concentric-barycentric') {
            layoutResult = computeConcentricBarycentricLayout(visibleNodesInput, visibleEdgesInput, spacing, effectiveOrder, orbitDistanceRef.current, minOrbitNodeDistanceRef.current, customOrbitTitlesRef.current);
          } else if (layoutName === 'concentric-polar-force') {
            layoutResult = computeConcentricPolarForceLayout(visibleNodesInput, visibleEdgesInput, spacing, effectiveOrder, orbitDistanceRef.current, minOrbitNodeDistanceRef.current, customOrbitTitlesRef.current);
          } else {
            layoutResult = computeConcentricLayout(visibleNodesInput, visibleEdgesInput, spacing, effectiveOrder, orbitDistanceRef.current, minOrbitNodeDistanceRef.current, customOrbitTitlesRef.current);
          }
        }

        const shouldFit = isInitial || layoutChanged;
        layoutConfig = {
          name: 'preset',
          positions: (node: any) => layoutResult.positions.get(node.id()) || { x: 0, y: 0 },
          fit: shouldFit,
          padding: 60,
          animate: false,
        };
        const effSpacing = spacing || 1.0;
        baseConcentricGuidesRef.current = layoutResult.guides.map((g) => ({
          ...g,
          radius: g.radius / effSpacing,
        }));
        relabelMergedOrbits(layoutResult, orbitCfg, customOrbitTitlesRef.current);
        setConcentricGuides(layoutResult.guides);
        setOrbitLegendItems(
          buildAllOrbitLegendItems(
            rawGraph.allNodes,
            rawGraph.echelonMap,
            layoutResult.populatedOrbits,
            orbitCfg,
            customOrbitTitlesRef.current
          )
        );
      } else if (layoutName === 'swimlanes') {
        setConcentricGuides([]);
        setIslandGuides([]);
        setHiveGuides([]);
        setSectorGuides([]);
        baseConcentricGuidesRef.current = [];
        baseIslandGuidesRef.current = [];
        baseHiveGuidesRef.current = [];
        baseSectorGuidesRef.current = [];
        const effectiveOrder = orbitOrder(orbitCfg);
        const layoutResult = computeSwimlanesLayout(
          visibleNodesInput,
          visibleEdgesInput,
          spacing,
          isProjectMap,
          effectiveOrder,
          baseIconSizeRef.current
        );
        layoutConfig = {
          name: 'preset',
          positions: (node: any) => layoutResult.positions.get(node.id()) || { x: 0, y: 0 },
          fit: true,
          padding: 60,
          animate: false,
        };
        const effSpacing = spacing || 1.0;
        baseSwimlaneGuidesRef.current = layoutResult.lanes.map((g) => ({
          ...g,
          x: g.x / effSpacing,
          y: g.y / effSpacing,
          width: g.width / effSpacing,
          height: g.height / effSpacing,
        }));
        setSwimlaneGuides(layoutResult.lanes);
        setOrbitLegendItems(
          buildAllOrbitLegendItems(
            rawGraph.allNodes,
            rawGraph.echelonMap,
            layoutResult.populatedLanes.map((l) => ({
              levelIndex: l.levelIndex,
              radius: 0,
              shortLabel: `Lane ${l.levelIndex}`,
              title: l.title,
              nodeIds: l.nodeIds,
            })),
            orbitCfg,
            customOrbitTitlesRef.current,
            'Lane',
            isProjectMap ? PROJECT_MAP_SWIMLANE_TITLES : SWIMLANE_TITLES
          )
        );
      } else if (layoutName === 'clusters') {
        setConcentricGuides([]);
        setOrbitLegendItems([]);
        setSwimlaneGuides([]);
        setHiveGuides([]);
        setSectorGuides([]);
        baseConcentricGuidesRef.current = [];
        baseSwimlaneGuidesRef.current = [];
        baseHiveGuidesRef.current = [];
        baseSectorGuidesRef.current = [];
        const layoutResult = computeDomainIslandsLayout(visibleNodesInput, visibleEdgesInput, spacing);
        layoutConfig = {
          name: 'preset',
          positions: (node: any) => layoutResult.positions.get(node.id()) || { x: 0, y: 0 },
          fit: true,
          padding: 60,
          animate: false,
        };
        const effSpacing = spacing || 1.0;
        baseIslandGuidesRef.current = layoutResult.islands.map((g) => ({
          ...g,
          x: g.x / effSpacing,
          y: g.y / effSpacing,
          width: g.width / effSpacing,
          height: g.height / effSpacing,
        }));
        setIslandGuides(layoutResult.islands);
      } else if (layoutName === 'hive') {
        setConcentricGuides([]);
        setOrbitLegendItems([]);
        setSwimlaneGuides([]);
        setIslandGuides([]);
        setSectorGuides([]);
        baseConcentricGuidesRef.current = [];
        baseSwimlaneGuidesRef.current = [];
        baseIslandGuidesRef.current = [];
        baseSectorGuidesRef.current = [];
        const layoutResult = computeHivePlotLayout(visibleNodesInput, visibleEdgesInput, spacing);
        layoutConfig = {
          name: 'preset',
          positions: (node: any) => layoutResult.positions.get(node.id()) || { x: 0, y: 0 },
          fit: true,
          padding: 60,
          animate: false,
        };
        const effSpacing = spacing || 1.0;
        baseHiveGuidesRef.current = layoutResult.axes.map((g) => ({
          ...g,
          length: g.length / effSpacing,
        }));
        setHiveGuides(layoutResult.axes);
      } else if (layoutName === 'dagre') {
        setConcentricGuides([]);
        setOrbitLegendItems([]);
        setSwimlaneGuides([]);
        setIslandGuides([]);
        setHiveGuides([]);
        setSectorGuides([]);
        baseConcentricGuidesRef.current = [];
        baseSwimlaneGuidesRef.current = [];
        baseIslandGuidesRef.current = [];
        baseHiveGuidesRef.current = [];
        baseSectorGuidesRef.current = [];
        // Hierarchical DAG (Mermaid Flow - Top-Down)
        layoutConfig = {
          name: 'dagre',
          rankDir: 'TB',
          nodeSep: Math.round(50 * spacing),
          rankSep: Math.round(80 * spacing),
          edgeSep: Math.round(25 * spacing),
          fit: true,
          padding: 60,
          animate: false,
        };
      } else {
        setConcentricGuides([]);
        setOrbitLegendItems([]);
        setSwimlaneGuides([]);
        setIslandGuides([]);
        setHiveGuides([]);
        setSectorGuides([]);
        baseConcentricGuidesRef.current = [];
        baseSwimlaneGuidesRef.current = [];
        baseIslandGuidesRef.current = [];
        baseHiveGuidesRef.current = [];
        baseSectorGuidesRef.current = [];
        // Organic Force-Directed (COSE)
        layoutConfig = {
          name: 'cose',
          animate: false,
          randomize: false,
          componentSpacing: Math.round(80 * spacing),
          nodeRepulsion: () => Math.round(450000 * spacing),
          nodeOverlap: 25,
          idealEdgeLength: () => Math.round(140 * spacing),
          edgeElasticity: () => 100,
          nestingFactor: 5,
          gravity: Math.max(10, Math.round(60 / spacing)),
          numIter: 300,
          coolingFactor: 0.95,
          fit: true,
          padding: 60,
        };
      }

      try {
        const layout = cy.layout({
          ...layoutConfig,
          stop: () => {
            if (!cancelled) {
              recordBasePositions(cy);
              const factor = spacingFactorRef.current;
              if (factor !== 1.0) {
                const { cx, cy: cyPos } = centroidRef.current;
                cy.batch(() => {
                  cy.nodes().forEach((node) => {
                    const base = basePositionsRef.current.get(node.id());
                    if (base) {
                      node.position({
                        x: cx + (base.x - cx) * factor,
                        y: cyPos + (base.y - cyPos) * factor,
                      });
                    }
                  });
                });
              }
              setCurrentZoom(cy.zoom());
              applyEdgeCurveMode(cy, edgeCurveModeRef.current, { x: 0, y: 0 }, curveFactorRef.current);
              setIsPreparing(false);
            }
          },
        });
        activeLayoutRef.current = layout;
        layout.run();
        setTimeout(() => {
          if (!cancelled) {
            applyEdgeCurveMode(cy, edgeCurveModeRef.current, { x: 0, y: 0 }, curveFactorRef.current);
            setIsPreparing(false);
          }
        }, 1200);
      } catch {
        if (!cancelled) {
          setIsPreparing(false);
        }
      }
    };

    runAsyncLayout();

    return () => {
      cancelled = true;
      if (activeLayoutRef.current) {
        try {
          activeLayoutRef.current.stop();
        } catch {}
        activeLayoutRef.current = null;
      }
    };
  }, [isCyReady, elements, layoutName, autoRelayoutOnFilter, relayoutTrigger]);

  // Record unscaled node positions relative to centroid
  const recordBasePositions = useCallback((cy: cytoscape.Core) => {
    let sumX = 0;
    let sumY = 0;
    let count = 0;
    const currentFactor = spacingFactorRef.current || 1.0;

    cy.nodes().forEach((n) => {
      const p = n.position();
      sumX += p.x;
      sumY += p.y;
      count++;
    });

    let cx = count > 0 ? sumX / count : 0;
    let cyPos = count > 0 ? sumY / count : 0;
    if (layoutName.startsWith('concentric') || layoutName === 'hive' || layoutName === 'swimlanes' || layoutName === 'clusters') {
      cx = 0;
      cyPos = 0;
    }
    centroidRef.current = { cx, cy: cyPos };

    const baseMap = new Map<string, cytoscape.Position>();
    cy.nodes().forEach((n) => {
      const p = n.position();
      const unscaledX = cx + (p.x - cx) / currentFactor;
      const unscaledY = cyPos + (p.y - cyPos) / currentFactor;
      baseMap.set(n.id(), { x: unscaledX, y: unscaledY });
    });
    basePositionsRef.current = baseMap;
  }, [layoutName]);

  // Real-time radial node spacing (air) adjustment
  const handleSpacingChange = useCallback(
    (newFactor: number) => {
      const clamped = Math.max(0.4, Math.min(3.5, Math.round(newFactor * 10) / 10));
      setSpacingFactor(clamped);
      spacingFactorRef.current = clamped;
      try {
        localStorage.setItem(`${storagePrefix}spacing_${layoutName}`, String(clamped));
      } catch { }

      const cy = cyRef.current;
      if (!cy) return;

      if (basePositionsRef.current.size === 0) {
        recordBasePositions(cy);
      }

      const { cx, cy: cyPos } = centroidRef.current;
      cy.batch(() => {
        cy.nodes().forEach((node) => {
          const base = basePositionsRef.current.get(node.id());
          if (base) {
            const dx = base.x - cx;
            const dy = base.y - cyPos;
            node.position({
              x: cx + dx * clamped,
              y: cyPos + dy * clamped,
            });
          }
        });
      });

      // Update layout guides in real time to match new spacing without full relayout
      if (layoutName.startsWith('concentric')) {
        if (baseConcentricGuidesRef.current.length === 0 && concentricGuides.length > 0) {
          const cur = spacingFactorRef.current || 1.0;
          baseConcentricGuidesRef.current = concentricGuides.map((g) => ({
            ...g,
            radius: g.radius / cur,
          }));
        }
        if (baseConcentricGuidesRef.current.length > 0) {
          setConcentricGuides(
            baseConcentricGuidesRef.current.map((g) => ({
              ...g,
              radius: Math.round(g.radius * clamped),
            }))
          );
        }
        if (layoutName === 'concentric-sectors') {
          if (baseSectorGuidesRef.current.length === 0 && sectorGuides.length > 0) {
            const cur = spacingFactorRef.current || 1.0;
            baseSectorGuidesRef.current = sectorGuides.map((g) => ({
              ...g,
              innerRadius: g.innerRadius / cur,
              outerRadius: g.outerRadius / cur,
            }));
          }
          if (baseSectorGuidesRef.current.length > 0) {
            setSectorGuides(
              baseSectorGuidesRef.current.map((g) => ({
                ...g,
                innerRadius: Math.round(g.innerRadius * clamped),
                outerRadius: Math.round(g.outerRadius * clamped),
              }))
            );
          }
        }
      } else if (layoutName === 'swimlanes') {
        if (baseSwimlaneGuidesRef.current.length === 0 && swimlaneGuides.length > 0) {
          const cur = spacingFactorRef.current || 1.0;
          baseSwimlaneGuidesRef.current = swimlaneGuides.map((g) => ({
            ...g,
            x: g.x / cur,
            y: g.y / cur,
            width: g.width / cur,
            height: g.height / cur,
          }));
        }
        if (baseSwimlaneGuidesRef.current.length > 0) {
          setSwimlaneGuides(
            baseSwimlaneGuidesRef.current.map((g) => ({
              ...g,
              x: Math.round(g.x * clamped),
              y: Math.round(g.y * clamped),
              width: Math.round(g.width * clamped),
              height: Math.round(g.height * clamped),
            }))
          );
        }
      } else if (layoutName === 'clusters') {
        if (baseIslandGuidesRef.current.length === 0 && islandGuides.length > 0) {
          const cur = spacingFactorRef.current || 1.0;
          baseIslandGuidesRef.current = islandGuides.map((g) => ({
            ...g,
            x: g.x / cur,
            y: g.y / cur,
            width: g.width / cur,
            height: g.height / cur,
          }));
        }
        if (baseIslandGuidesRef.current.length > 0) {
          setIslandGuides(
            baseIslandGuidesRef.current.map((g) => ({
              ...g,
              x: Math.round(g.x * clamped),
              y: Math.round(g.y * clamped),
              width: Math.round(g.width * clamped),
              height: Math.round(g.height * clamped),
            }))
          );
        }
      } else if (layoutName === 'hive') {
        if (baseHiveGuidesRef.current.length === 0 && hiveGuides.length > 0) {
          const cur = spacingFactorRef.current || 1.0;
          baseHiveGuidesRef.current = hiveGuides.map((g) => ({
            ...g,
            length: g.length / cur,
          }));
        }
        if (baseHiveGuidesRef.current.length > 0) {
          setHiveGuides(
            baseHiveGuidesRef.current.map((g) => ({
              ...g,
              length: Math.round(g.length * clamped),
            }))
          );
        }
      }

      applyEdgeCurveMode(cy, edgeCurveModeRef.current, { x: 0, y: 0 }, curveFactorRef.current);
    },
    [recordBasePositions, layoutName, concentricGuides, swimlaneGuides, islandGuides, hiveGuides, sectorGuides]
  );


  // Orbit configuration: persisted per workspace, applied through the layout effect.
  const applyOrbitConfig = useCallback(
    (next: OrbitConfig, clearHiddenFor: number[] = []) => {
      orbitConfigRef.current = next;
      setOrbitConfigState(next);
      setSelectedOrbitRows([]);
      lastSelectedOrbitRowRef.current = null;
      if (clearHiddenFor.length > 0) {
        setHiddenOrbitTiers((prev) => {
          const copy = new Set(prev);
          for (const t of clearHiddenFor) copy.delete(t);
          return copy;
        });
        setForcedVisibleNodeIds(new Set());
      }
      getVsCodeApi()?.postMessage({
        type: 'SAVE_ORBIT_CONFIG',
        config: isDefaultOrbitConfig(next) ? null : next,
      });
      forceRelayoutRef.current = true;
      setRelayoutTrigger((n) => n + 1);
    },
    []
  );

  const orbitEntryTiers = useCallback(
    (orbitIndex: number) => orbitConfigRef.current.orbits[orbitIndex] ?? [],
    []
  );

  const handleMoveOrbit = useCallback(
    (item: OrbitLegendItem, direction: -1 | 1) => {
      const cfg = orbitConfigRef.current;
      const populated = Array.from(new Set(orbitLegendItems.map((it) => it.orbitIndex)));
      const pos = populated.indexOf(item.orbitIndex);
      const target = populated[pos + direction];
      if (pos < 0 || target === undefined) return;
      applyOrbitConfig(moveOrbit(cfg, item.orbitIndex, target));
    },
    [orbitLegendItems, applyOrbitConfig]
  );

  const handleDragStart = useCallback((e: React.DragEvent, idx: number) => {
    setDraggedOrbitIndex(idx);
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', String(idx));
  }, []);

  const handleDragOver = useCallback((e: React.DragEvent, idx: number) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';
    setDragOverIndex((prev) => (prev !== idx ? idx : prev));
  }, []);

  const handleDrop = useCallback(
    (e: React.DragEvent, targetIdx: number) => {
      e.preventDefault();
      const dragged = draggedOrbitIndex;
      const kind = draggedKindRef.current;
      setDraggedOrbitIndex(null);
      setDragOverIndex(null);
      draggedKindRef.current = null;
      const target = orbitLegendItems[targetIdx];
      if (!target) return;
      if (kind) {
        applyOrbitConfig(moveKindToOrbit(orbitConfigRef.current, kind, target.levelIndex), [target.levelIndex]);
        return;
      }
      const source = dragged === null ? undefined : orbitLegendItems[dragged];
      if (!source || source.orbitIndex === target.orbitIndex) return;
      applyOrbitConfig(moveOrbit(orbitConfigRef.current, source.orbitIndex, target.orbitIndex));
    },
    [draggedOrbitIndex, orbitLegendItems, applyOrbitConfig]
  );

  const handleDragEnd = useCallback(() => {
    setDraggedOrbitIndex(null);
    setDragOverIndex(null);
    draggedKindRef.current = null;
  }, []);

  const handleOrbitRowClick = useCallback(
    (e: React.MouseEvent, idx: number) => {
      if (!e.shiftKey && !e.ctrlKey && !e.metaKey) return false;
      e.preventDefault();
      const anchor = lastSelectedOrbitRowRef.current;
      if (e.shiftKey && anchor !== null) {
        const [a, b] = anchor < idx ? [anchor, idx] : [idx, anchor];
        setSelectedOrbitRows(Array.from({ length: b - a + 1 }, (_, i) => a + i));
      } else {
        setSelectedOrbitRows((prev) => (prev.includes(idx) ? prev.filter((i) => i !== idx) : [...prev, idx]));
        lastSelectedOrbitRowRef.current = idx;
      }
      return true;
    },
    []
  );

  // Selected rows must belong to adjacent orbit entries to be merged.
  const mergeSelection = useMemo(() => {
    const entries = Array.from(new Set(selectedOrbitRows.map((i) => orbitLegendItems[i]?.orbitIndex))).filter(
      (v): v is number => v !== undefined
    );
    if (entries.length < 2) return null;
    entries.sort((a, b) => a - b);
    const populated = Array.from(new Set(orbitLegendItems.map((it) => it.orbitIndex)));
    const adjacent = entries.every((e, i) => i === 0 || populated.indexOf(e) === populated.indexOf(entries[i - 1]) + 1);
    return adjacent ? entries : null;
  }, [selectedOrbitRows, orbitLegendItems]);

  // Legend rows grouped per rendered orbit; merged orbits hold several tier rows.
  const orbitLegendGroups = useMemo(() => {
    const groups: Array<{
      orbitIndex: number;
      levelIndex: number;
      shortLabel: string;
      rows: Array<{ item: OrbitLegendItem; idx: number }>;
    }> = [];
    orbitLegendItems.forEach((item, idx) => {
      const last = groups[groups.length - 1];
      if (last && last.orbitIndex === item.orbitIndex) {
        last.rows.push({ item, idx });
      } else {
        groups.push({ orbitIndex: item.orbitIndex, levelIndex: item.levelIndex, shortLabel: item.shortLabel, rows: [{ item, idx }] });
      }
    });
    return groups;
  }, [orbitLegendItems]);

  const handleMergeSelected = useCallback(() => {
    if (!mergeSelection) return;
    const cfg = orbitConfigRef.current;
    const first = mergeSelection[0];
    const last = mergeSelection[mergeSelection.length - 1];
    const affected = cfg.orbits.slice(first, last + 1).flat();
    applyOrbitConfig(mergeOrbits(cfg, first, last), affected);
  }, [mergeSelection, applyOrbitConfig]);

  const handleSplitOrbit = useCallback(
    (orbitIndex: number) => {
      const cfg = orbitConfigRef.current;
      applyOrbitConfig(splitOrbit(cfg, orbitIndex), cfg.orbits[orbitIndex] ?? []);
    },
    [applyOrbitConfig]
  );

  const handleResetOrbitOrder = useCallback(() => {
    setHiddenOrbitTiers(new Set());
    setForcedVisibleNodeIds(new Set());
    applyOrbitConfig(Boolean(customPresetsSlot) ? DEFAULT_PROJECT_MAP_ORBIT_CONFIG : DEFAULT_ORBIT_CONFIG);
  }, [applyOrbitConfig, customPresetsSlot]);

  // Wheel sensitivity change handler
  const handleWheelSensitivityChange = useCallback((val: number) => {
    const clamped = Math.max(0.2, Math.min(3.0, +val.toFixed(1)));
    setWheelSensitivity(clamped);
    try {
      localStorage.setItem('ce_wheel_sensitivity_v2', clamped.toString());
    } catch { }
  }, []);

  // Zoom control handlers
  const handleZoomIn = useCallback(() => {
    const cy = cyRef.current;
    if (!cy) return;
    const newZoom = Math.min(3.5, cy.zoom() * 1.25);
    cy.zoom({
      level: newZoom,
      renderedPosition: { x: cy.width() / 2, y: cy.height() / 2 },
    });
    setCurrentZoom(newZoom);
  }, []);

  const handleZoomOut = useCallback(() => {
    const cy = cyRef.current;
    if (!cy) return;
    const newZoom = Math.max(0.15, cy.zoom() * 0.8);
    cy.zoom({
      level: newZoom,
      renderedPosition: { x: cy.width() / 2, y: cy.height() / 2 },
    });
    setCurrentZoom(newZoom);
  }, []);

  const handleResetZoom = useCallback(() => {
    const cy = cyRef.current;
    if (!cy) return;
    cy.zoom({
      level: 1.0,
      renderedPosition: { x: cy.width() / 2, y: cy.height() / 2 },
    });
    cy.center();
    setCurrentZoom(1.0);
  }, []);

  const handleFitView = useCallback(() => {
    const cy = cyRef.current;
    if (!cy) return;
    cy.fit(undefined, 50);
    setCurrentZoom(cy.zoom());
  }, []);

  const handleLayoutChange = useCallback((newLayout: DomainLayoutName) => {
    setLayoutName(newLayout);
    try {
      localStorage.setItem(`${storagePrefix}layout`, newLayout);
    } catch { }

    const nextSpacing = loadSpacingForLayout(storagePrefix, newLayout);
    setSpacingFactor(nextSpacing);
    spacingFactorRef.current = nextSpacing;

    const nextOrbitDist = loadOrbitDistanceForLayout(storagePrefix, newLayout);
    setOrbitDistance(nextOrbitDist);
    orbitDistanceRef.current = nextOrbitDist;

    const nextMinOrbitDist = loadMinOrbitDistForLayout(storagePrefix, newLayout);
    setMinOrbitNodeDistance(nextMinOrbitDist);
    minOrbitNodeDistanceRef.current = nextMinOrbitDist;

    const nextBaseIconSize = loadBaseIconSizeForLayout(storagePrefix, newLayout);
    setBaseIconSize(nextBaseIconSize);
    baseIconSizeRef.current = nextBaseIconSize;

    forceRelayoutRef.current = true;
    setRelayoutTrigger((prev) => prev + 1);
  }, [storagePrefix, loadSpacingForLayout, loadOrbitDistanceForLayout, loadMinOrbitDistForLayout, loadBaseIconSizeForLayout]);

  const DEFAULT_LAYOUT_OPTIONS: Array<{ id: DomainLayoutName; label: string }> = [
    { id: 'concentric', label: '🎯 Concentric (Original)' },
    { id: 'concentric-equispaced', label: '🪐 Concentric: Equispaced' },
    { id: 'concentric-barycentric', label: '🪐 Concentric: Barycentric' },
    { id: 'concentric-polar-force', label: '🪐 Concentric: Polar Force' },
    { id: 'concentric-sectors', label: '🪐 Concentric: Domain Sectors' },
    { id: 'swimlanes', label: '🏊 Swimlanes (Pipeline)' },
    { id: 'clusters', label: '🏝️ Domain Islands (Bounded Contexts)' },
    { id: 'hive', label: '🕸️ Hive Plot (Multi-Axis)' },
    { id: 'mermaid', label: '🧜 Mermaid' },
    { id: 'dagre', label: '📊 Dagre Flow' },
    { id: 'cose', label: '⚡ Force (COSE)' },
  ];

  const layoutOptions: Array<{ id: DomainLayoutName; label: string }> = useMemo(() => {
    if (allowedLayouts && allowedLayouts.length > 0) {
      return allowedLayouts;
    }
    return DEFAULT_LAYOUT_OPTIONS;
  }, [allowedLayouts]);

  const searchMatchCount = useMemo(() => {
    if (!searchQuery.trim()) return null;
    let count = 0;
    for (const n of visibleNodes) {
      const detail = rawGraph.detailMap.get(n.data.id as string);
      const data = n.data || {};
      if (
        matchesSearchTokens([
          detail?.name || data.name,
          detail?.displayName || data.displayName || data.displayLabel,
          detail?.domain || data.domain,
          detail?.kind || data.kind,
          detail?.displayTag,
          detail?.framework || data.framework,
          detail?.language || data.language,
          detail?.primaryFilePath,
          ...(detail?.projects || []).map((p: DomainProjectInfo) => p.name),
        ], searchQuery)
      ) {
        count++;
      }
    }
    return count;
  }, [searchQuery, visibleNodes, rawGraph.detailMap]);

  const renderOrbitLegend = (docked: boolean) => {
    const isSwimlanes = layoutName === 'swimlanes';
    const unitName = isSwimlanes ? 'Lane' : 'Orbit';
    const unitsName = isSwimlanes ? 'Lanes' : 'Orbits';
    const isTierActive = layoutName.startsWith('concentric') || isSwimlanes;

    return (
    <FloatingPanel
      dock="top-right"
      docked={docked}
      isCollapsed={false}
      title={`${unitName} Legend`}
      icon={<LayersIcon size={14} />}
      badge={
        <span
          className="floating-dock-badge"
          style={{
            backgroundColor: 'rgba(56, 189, 248, 0.18)',
            color: '#38bdf8',
            border: '1px solid rgba(56, 189, 248, 0.35)',
            fontSize: '10px',
            padding: '1px 6px',
          }}
        >
          {orbitLegendItems.length}
        </span>
      }
      headerActions={
        <>
          {mergeSelection ? (
            <button
              type="button"
              className="floating-dock-btn action-btn"
              onClick={(e) => {
                e.stopPropagation();
                handleMergeSelected();
              }}
              title={`Merge the selected adjacent ${isSwimlanes ? 'lanes into one column' : 'orbits into one ring'}`}
            >
              <LayersIcon size={11} />
              <span>Merge {mergeSelection.length}</span>
            </button>
          ) : null}
          {!isDefaultOrbitConfig(orbitConfig) || hiddenOrbitTiers.size > 0 ? (
            <button
              type="button"
              className="floating-dock-btn action-btn"
              onClick={(e) => {
                e.stopPropagation();
                handleResetOrbitOrder();
              }}
              title={`Reset to the default ${isSwimlanes ? 'lane order and show all lanes' : 'orbit order and show all orbits'}`}
            >
              <RotateCcwIcon size={11} />
              <span>Reset</span>
            </button>
          ) : null}
        </>
      }
      collapsedTitle={`${unitsName} (${orbitLegendItems.length})`}
      collapsedIcon={<LayersIcon size={13} />}
      collapseTooltip={`Collapse ${unitName} Legend`}
      expandTooltip={`Expand ${unitName} Legend`}
      width={360}
      minWidth={280}
      maxWidth={700}
      persistKey="ce_orbit_legend"
      className="domain-orbit-legend-floating no-body-padding"
    >
      <div className="domain-orbit-legend-body" style={{ flex: 1, minHeight: 0, overflowY: 'auto' }}>
        {/* 1. Layout & Geometry Controls */}
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            gap: '8px',
            padding: '10px 12px',
            borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
            background: 'rgba(15, 23, 42, 0.35)',
          }}
        >
          <div className="display-setting-row" title="Select graph layout algorithm">
            <div className="setting-label-row">
              <span className="setting-icon">📐</span>
              <span className="setting-name">Graph Layout</span>
            </div>
            <select
              value={layoutName}
              onChange={(e) => handleLayoutChange(e.target.value as any)}
              title="Graph Layout"
              className="domain-layout-dropdown"
              style={{ width: '100%', marginTop: '4px' }}
            >
              {layoutOptions.map((opt) => (
                <option key={opt.id} value={opt.id}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>

          {/* Node Spacing / Air Control */}
          <div className="display-setting-row" title="Adjust node spacing / distance between orbits">
            <div className="setting-label-row">
              <span className="setting-icon">💨</span>
              <span className="setting-name">Node Air (Spacing)</span>
              <span
                className="domain-hud-value-badge"
                onClick={() => handleSpacingChange(1.0)}
                title="Click to reset air to 1.0x"
              >
                {spacingFactor.toFixed(1)}x
              </span>
            </div>
            <div className="setting-slider-row">
              <button
                type="button"
                className="domain-hud-step-btn"
                onClick={() => handleSpacingChange(Math.max(0.4, +(spacingFactor - 0.2).toFixed(1)))}
                title="Decrease spacing"
              >
                −
              </button>
              <input
                type="range"
                min="0.5"
                max="3.0"
                step="0.1"
                value={spacingFactor}
                onChange={(e) => handleSpacingChange(parseFloat(e.target.value))}
                className="domain-hud-slider"
              />
              <button
                type="button"
                className="domain-hud-step-btn"
                onClick={() => handleSpacingChange(Math.min(3.5, +(spacingFactor + 0.2).toFixed(1)))}
                title="Increase spacing"
              >
                +
              </button>
            </div>
          </div>

          {/* Orbit Distance Control */}
          {layoutName.startsWith('concentric') && (
            <div className="display-setting-row" title="Adjust distance between concentric orbits (100px - 900px)">
              <div className="setting-label-row">
                <span className="setting-icon">🪐</span>
                <span className="setting-name">Orbit Distance</span>
                <span
                  className="domain-hud-value-badge"
                  onClick={() => handleOrbitDistanceChange(280)}
                  title="Click to reset orbit distance to 280px"
                >
                  {orbitDistance}px
                </span>
              </div>
              <div className="setting-slider-row">
                <button
                  type="button"
                  className="domain-hud-step-btn"
                  onClick={() => handleOrbitDistanceChange(orbitDistance - 20)}
                  title="Decrease orbit distance (-20px)"
                >
                  −
                </button>
                <input
                  type="range"
                  min="100"
                  max="900"
                  step="10"
                  value={orbitDistance}
                  onChange={(e) => handleOrbitDistanceChange(parseInt(e.target.value, 10))}
                  className="domain-hud-slider"
                />
                <button
                  type="button"
                  className="domain-hud-step-btn"
                  onClick={() => handleOrbitDistanceChange(orbitDistance + 20)}
                  title="Increase orbit distance (+20px)"
                >
                  +
                </button>
              </div>
            </div>
          )}

          {/* Min Distance on Orbit Control */}
          {layoutName.startsWith('concentric') && (
            <div className="display-setting-row" title="Adjust minimum distance between nodes on the same orbit (50px - 400px)">
              <div className="setting-label-row">
                <span className="setting-icon">↔️</span>
                <span className="setting-name">Min Distance on Orbit</span>
                <span
                  className="domain-hud-value-badge"
                  onClick={() => handleMinOrbitNodeDistanceChange(125)}
                  title="Click to reset min orbit node distance to 125px"
                >
                  {minOrbitNodeDistance}px
                </span>
              </div>
              <div className="setting-slider-row">
                <button
                  type="button"
                  className="domain-hud-step-btn"
                  onClick={() => handleMinOrbitNodeDistanceChange(minOrbitNodeDistance - 15)}
                  title="Decrease min distance on orbit (-15px)"
                >
                  −
                </button>
                <input
                  type="range"
                  min="50"
                  max="400"
                  step="5"
                  value={minOrbitNodeDistance}
                  onChange={(e) => handleMinOrbitNodeDistanceChange(parseInt(e.target.value, 10))}
                  className="domain-hud-slider"
                />
                <button
                  type="button"
                  className="domain-hud-step-btn"
                  onClick={() => handleMinOrbitNodeDistanceChange(minOrbitNodeDistance + 15)}
                  title="Increase min distance on orbit (+15px)"
                >
                  +
                </button>
              </div>
            </div>
          )}

          {/* Auto-update graph & Relayout */}
          <div className="display-popover-actions" style={{ paddingTop: '2px' }}>
            <label
              className={`domain-hud-checkbox-label ${autoRelayoutOnFilter ? 'is-active' : ''}`}
              title={
                autoRelayoutOnFilter
                  ? 'Auto-relayout enabled: layout automatically refits when entities are hidden. Uncheck to keep node positions unchanged.'
                  : 'Auto-relayout disabled: nodes are hidden in place without moving remaining nodes.'
              }
            >
              <input
                type="checkbox"
                className="domain-hud-checkbox"
                checked={autoRelayoutOnFilter}
                onChange={(e) => handleAutoRelayoutChange(e.target.checked)}
              />
              <span>🔄 Auto-recalc</span>
            </label>
            <button
              type="button"
              className="domain-hud-btn"
              onClick={handleForceRelayout}
              title="Recalculate graph layout now (Re-layout)"
              style={{ padding: '2px 8px', fontSize: '11px', lineHeight: 1 }}
            >
              ⟳ Re-layout
            </button>
          </div>
        </div>

        {/* 2. Concentric Orbits / Swimlanes Hierarchy */}
        {isTierActive ? (
          <>
            <div
              className="orbit-axis-label"
              title={
                isSwimlanes
                  ? 'Lanes run from ingress (left) to storage (right). Drag cards or use ▲/▼ to reorder; shift-click adjacent cards, then Merge, to share one column; drag a kind chip onto another lane to move it.'
                  : 'Orbits run from the center of the map (top) to its periphery (bottom). Drag cards or use ▲/▼ to reorder; shift-click adjacent cards, then Merge, to share one ring; drag a kind chip onto another orbit to move it.'
              }
            >
              <span className="orbit-axis-dot" />
              <span>{isSwimlanes ? 'Ingress (Left)' : 'Center'}</span>
            </div>
        {orbitLegendGroups.map((group, gIdx) => {
          const anchorIdx = group.rows[0].idx;
          const anchorItem = group.rows[0].item;
          const isOrbitHidden = hiddenOrbitTiers.has(group.levelIndex);
          const isMerged = group.rows.length > 1;
          const isSelected = group.rows.some((r) => selectedOrbitRows.includes(r.idx));
          const total = group.rows.reduce((sum, r) => sum + r.item.count, 0);
          const allNodeIds = group.rows.flatMap((r) => r.item.nodeIds);
          const depth = orbitLegendGroups.length > 1 ? gIdx / (orbitLegendGroups.length - 1) : 0;
          const groupTitle = group.rows.map((r) => r.item.title).join(' + ');

          return (
            <section
              key={`${group.orbitIndex}-${group.levelIndex}`}
              className={`orbit-card ${isOrbitHidden ? 'is-orbit-hidden' : ''} ${isSelected ? 'is-selected' : ''} ${isMerged ? 'is-merged' : ''} ${draggedOrbitIndex === anchorIdx ? 'is-dragging' : ''} ${dragOverIndex === anchorIdx ? 'is-drag-over' : ''}`}
              style={{ '--orbit-depth': depth } as React.CSSProperties}
              draggable
              onClick={(e) => handleOrbitRowClick(e, anchorIdx)}
              onDragStart={(e) => handleDragStart(e, anchorIdx)}
              onDragOver={(e) => handleDragOver(e, anchorIdx)}
              onDrop={(e) => handleDrop(e, anchorIdx)}
              onDragEnd={handleDragEnd}
              onMouseEnter={() => !isOrbitHidden && highlightOrbitNodes(allNodeIds)}
              onMouseLeave={clearOrbitHighlight}
              title={`${group.shortLabel}: ${groupTitle} (${total} nodes). Drag or use ▲/▼ to reorder; shift-click to select for merging.`}
            >
              <header className="orbit-card-header">
                <span className="orbit-drag-handle" title={`Drag to reorder ${unitName.toLowerCase()}`}>⠿</span>
                <span className="orbit-legend-pill">{group.shortLabel}</span>
                {isMerged ? <span className="orbit-merged-tag">Merged</span> : null}
                <span className="orbit-card-spacer" />
                <span className="orbit-legend-count" title={`${total} nodes in this ${unitName.toLowerCase()}`}>{total}</span>
                <div className="orbit-card-actions">
                  {isMerged ? (
                    <button
                      type="button"
                      className="orbit-split-btn"
                      onClick={(e) => {
                        e.stopPropagation();
                        handleSplitOrbit(group.orbitIndex);
                      }}
                      title={`Split this merged ${unitName.toLowerCase()} back into separate ${unitsName.toLowerCase()}`}
                    >
                      Split
                    </button>
                  ) : null}
                  <button
                    type="button"
                    className="orbit-move-btn"
                    disabled={gIdx === 0}
                    onClick={(e) => {
                      e.stopPropagation();
                      handleMoveOrbit(anchorItem, -1);
                    }}
                    title={isSwimlanes ? 'Move toward ingress (left lane)' : 'Move toward center (inner orbit)'}
                  >
                    <ChevronUpIcon size={10} />
                  </button>
                  <button
                    type="button"
                    className="orbit-move-btn"
                    disabled={gIdx === orbitLegendGroups.length - 1}
                    onClick={(e) => {
                      e.stopPropagation();
                      handleMoveOrbit(anchorItem, 1);
                    }}
                    title={isSwimlanes ? 'Move toward storage (right lane)' : 'Move toward periphery (outer orbit)'}
                  >
                    <ChevronDownIcon size={10} />
                  </button>
                  <button
                    type="button"
                    className={`orbit-visibility-btn ${isOrbitHidden ? 'is-hidden' : ''}`}
                    onClick={(e) => {
                      e.stopPropagation();
                      toggleOrbitVisibility(group.levelIndex);
                    }}
                    title={isOrbitHidden ? `Show ${group.shortLabel}` : `Hide ${group.shortLabel}`}
                  >
                    {isOrbitHidden ? <EyeOffIcon size={13} /> : <EyeIcon size={13} />}
                  </button>
                </div>
              </header>
              {group.rows.map(({ item }) => (
                <div
                  key={item.slotTier}
                  className="orbit-tier"
                  onMouseEnter={() => isMerged && !isOrbitHidden && highlightOrbitNodes(item.nodeIds)}
                  onMouseLeave={() => isMerged && !isOrbitHidden && highlightOrbitNodes(allNodeIds)}
                >
                  <div className="orbit-tier-title">
                    <span>{item.title}</span>
                    {isMerged ? <span className="orbit-tier-count">{item.count}</span> : null}
                  </div>
                  <div className="orbit-kind-chips">
                    {item.kinds.map((k) => (
                      <span
                        key={k.kind}
                        className={`orbit-kind-chip ${k.overridden ? 'is-overridden' : ''}`}
                        draggable
                        onDragStart={(e) => {
                          e.stopPropagation();
                          draggedKindRef.current = k.kind;
                          e.dataTransfer.effectAllowed = 'move';
                          e.dataTransfer.setData('text/plain', k.kind);
                        }}
                        onDragEnd={handleDragEnd}
                        title={`Drag ${k.kind} onto another ${unitName.toLowerCase()} to move all ${k.kind} entities there`}
                      >
                        <span className="orbit-kind-chip-name">{getEntityBadgeMeta(k.kind as EntityKind)?.label || k.kind}</span>
                        <span className="orbit-kind-chip-count">{k.count}</span>
                        {k.overridden ? (
                          <button
                            type="button"
                            className="orbit-kind-chip-clear"
                            onClick={(e) => {
                              e.stopPropagation();
                              applyOrbitConfig(moveKindToOrbit(orbitConfigRef.current, k.kind, null), [item.levelIndex]);
                            }}
                            title={`Restore the default ${unitName.toLowerCase()} for this kind`}
                          >
                            ✕
                          </button>
                        ) : null}
                      </span>
                    ))}
                  </div>
                </div>
              ))}
            </section>
          );
        })}
            <div className="orbit-axis-label is-outer">
              <span>{isSwimlanes ? 'Databases (Right)' : 'Periphery'}</span>
            </div>
          </>
        ) : (
          <div style={{ padding: '14px 12px', fontSize: '11px', color: '#94a3b8', lineHeight: 1.45 }}>
            Tier hierarchy and reordering are active when a <strong>Concentric</strong> or <strong>Swimlanes</strong> layout algorithm is selected above.
          </div>
        )}
      </div>
    </FloatingPanel>
  );
};

  const inspectorRawEdges = useMemo(() => {
    if (
      selectedNode &&
      !selectedNode.isGroup &&
      Array.from(nodeDetailMap.values()).some(
        (g) =>
          g.isGroup &&
          (g.memberIds?.includes(selectedNode.id) || g.memberNodes?.some((m) => m.id === selectedNode.id))
      )
    ) {
      return rawGraph.rawEdges;
    }
    return effectiveRawEdges;
  }, [selectedNode, nodeDetailMap, rawGraph.rawEdges, effectiveRawEdges]);

  const buildInspectorSyncedState = useCallback(() => {
    return {
      hudLabel,
      hudTitle: hudTitle || (isMatrixMode ? 'Architecture Matrix' : 'Architecture Map'),
      isMatrixMode,
      effectiveSidebarTab,
      hasOrbitLegend,
      orbitLegendItems,
      hiddenCount,
      selectedNode,
      selectedEdge,
      nodeDetailEntries: Array.from(nodeDetailMap.entries()),
      rawEdges: inspectorRawEdges,
      isInspectorCollapsed,
      isEdgeInspectorCollapsed,
      availableDomains,
      layoutName,
      spacingFactor,
      orbitDistance,
      minOrbitNodeDistance,
      autoRelayoutOnFilter,
      orbitConfig,
      hiddenOrbitTiers: Array.from(hiddenOrbitTiers),
      echelonEntries: Array.from(rawGraph.echelonMap.entries()),
      hiddenNodeIds: Array.from(hiddenNodeIds),
      hiddenTypes: Array.from(hiddenTypes),
      hideSingleConnectionDbs,
      hideIsolatedNodes,
      showTransitiveEdges,
      counts: {
        ...rawGraph.counts,
        transitiveCalls: stats.transitiveCalls,
      },
      groupedTypes: Array.from(groupedTypes),
      customGroups,
      nodeContentMode,
      nodeSizingMode,
      edgeCurveMode,
      edgeLabelsOnHover,
      baseIconSize,
      fontSize,
      curveFactor,
      wheelSensitivity,
      projectMapPresets,
    };
  }, [
    hudLabel,
    hudTitle,
    isMatrixMode,
    effectiveSidebarTab,
    hasOrbitLegend,
    orbitLegendItems,
    hiddenCount,
    selectedNode,
    selectedEdge,
    nodeDetailMap,
    inspectorRawEdges,
    isInspectorCollapsed,
    isEdgeInspectorCollapsed,
    availableDomains,
    layoutName,
    spacingFactor,
    orbitDistance,
    minOrbitNodeDistance,
    autoRelayoutOnFilter,
    orbitConfig,
    hiddenOrbitTiers,
    rawGraph.echelonMap,
    rawGraph.counts,
    stats.transitiveCalls,
    hiddenNodeIds,
    hiddenTypes,
    hideSingleConnectionDbs,
    hideIsolatedNodes,
    showTransitiveEdges,
    groupedTypes,
    customGroups,
    nodeContentMode,
    nodeSizingMode,
    edgeCurveMode,
    edgeLabelsOnHover,
    baseIconSize,
    fontSize,
    curveFactor,
    wheelSensitivity,
    projectMapPresets,
  ]);

  const buildInspectorSyncedStateRef = useRef(buildInspectorSyncedState);
  buildInspectorSyncedStateRef.current = buildInspectorSyncedState;

  useEffect(() => {
    getVsCodeApi()?.postMessage({
      type: 'SYNC_INSPECTOR_STATE',
      state: buildInspectorSyncedState(),
    });
  }, [buildInspectorSyncedState]);

  const inspectorActionsRef = useRef({
    handleSidebarTabChange,
    setIsInspectorCollapsed,
    setIsEdgeInspectorCollapsed,
    setSelectedNode,
    handleSelectNodeById,
    setSelectedEdge,
    hideNode,
    deleteCustomGroup,
    toggleTypeGrouping,
    removeNodeFromCustomGroup,
    onFocusInFlow,
    onOpenProjectMap,
    onOpenFile,
    onSetDomainOverride,
    handleLayoutChange,
    handleSpacingChange,
    handleOrbitDistanceChange,
    handleMinOrbitNodeDistanceChange,
    handleAutoRelayoutChange,
    handleForceRelayout,
    applyOrbitConfig,
    handleResetOrbitOrder,
    toggleOrbitVisibility,
    highlightOrbitNodes,
    clearOrbitHighlight,
    unhideNode,
    restoreAllHiddenNodes,
    toggleTypeVisibility,
    handleToggleTransitiveEdges,
    createCustomGroup,
    toggleCustomGroupCollapse,
    handleNodeContentModeChange,
    handleNodeSizingModeChange,
    handleEdgeCurveModeChange,
    handleEdgeLabelsOnHoverChange,
    handleBaseIconSizeChange,
    handleFontSizeChange,
    handleCurveFactorChange,
    handleWheelSensitivityChange,
    onToggleProjectMapPreset,
  });
  inspectorActionsRef.current = {
    handleSidebarTabChange,
    setIsInspectorCollapsed,
    setIsEdgeInspectorCollapsed,
    setSelectedNode,
    handleSelectNodeById,
    setSelectedEdge,
    hideNode,
    deleteCustomGroup,
    toggleTypeGrouping,
    removeNodeFromCustomGroup,
    onFocusInFlow,
    onOpenProjectMap,
    onOpenFile,
    onSetDomainOverride,
    handleLayoutChange,
    handleSpacingChange,
    handleOrbitDistanceChange,
    handleMinOrbitNodeDistanceChange,
    handleAutoRelayoutChange,
    handleForceRelayout,
    applyOrbitConfig,
    handleResetOrbitOrder,
    toggleOrbitVisibility,
    highlightOrbitNodes,
    clearOrbitHighlight,
    unhideNode,
    restoreAllHiddenNodes,
    toggleTypeVisibility,
    handleToggleTransitiveEdges,
    createCustomGroup,
    toggleCustomGroupCollapse,
    handleNodeContentModeChange,
    handleNodeSizingModeChange,
    handleEdgeCurveModeChange,
    handleEdgeLabelsOnHoverChange,
    handleBaseIconSizeChange,
    handleFontSizeChange,
    handleCurveFactorChange,
    handleWheelSensitivityChange,
    onToggleProjectMapPreset,
  };

  useEffect(() => {
    const onMessage = (event: MessageEvent) => {
      const msg = event.data;
      if (msg?.type === 'REQUEST_INSPECTOR_SYNC') {
        getVsCodeApi()?.postMessage({
          type: 'SYNC_INSPECTOR_STATE',
          state: buildInspectorSyncedStateRef.current(),
        });
        return;
      }
      if (msg?.type === 'INSPECTOR_ACTION' && msg.action) {
        const act = msg.action;
        const h = inspectorActionsRef.current;
        switch (act.type) {
          case 'SET_SIDEBAR_TAB':
            h.handleSidebarTabChange(act.tab);
            break;
          case 'SET_INSPECTOR_COLLAPSED':
            h.setIsInspectorCollapsed(act.collapsed);
            break;
          case 'SET_EDGE_INSPECTOR_COLLAPSED':
            h.setIsEdgeInspectorCollapsed(act.collapsed);
            break;
          case 'SELECT_NODE':
            h.setSelectedNode(act.node);
            break;
          case 'SELECT_NODE_BY_ID':
            h.handleSelectNodeById(act.id);
            break;
          case 'SELECT_EDGE':
            h.setSelectedEdge(act.edge);
            break;
          case 'CLEAR_SELECTION':
            h.setSelectedNode(null);
            h.setSelectedEdge(null);
            break;
          case 'CLEAR_GRAPH_HIGHLIGHT':
            cyRef.current?.elements().removeClass('highlighted dimmed');
            break;
          case 'HIDE_NODE':
            h.hideNode(act.id);
            break;
          case 'UNGROUP_NODE': {
            const node = act.node;
            if (node.id.startsWith('group:custom:')) {
              const cId = node.customGroupId || node.id.replace('group:custom:', '');
              h.deleteCustomGroup(cId);
            } else if (node.id.startsWith('group:kind:') || node.groupKind) {
              const kind = (node.groupKind || node.id.replace('group:kind:', '')) as EntityKind;
              h.toggleTypeGrouping(kind);
            }
            h.setSelectedNode(null);
            break;
          }
          case 'REMOVE_FROM_GROUP': {
            const cId = act.groupId.startsWith('group:custom:')
              ? act.groupId.replace('group:custom:', '')
              : act.groupId;
            h.removeNodeFromCustomGroup(cId, act.memberId);
            break;
          }
          case 'FOCUS_IN_FLOW':
            h.onFocusInFlow?.(act.projectName);
            break;
          case 'OPEN_PROJECT_MAP':
            h.onOpenProjectMap?.(act.projectName);
            break;
          case 'OPEN_FILE':
            h.onOpenFile?.(act.filePath, act.lineStart);
            break;
          case 'SET_DOMAIN_OVERRIDE':
            h.onSetDomainOverride?.(act.serviceName, act.domain, act.context, act.reset);
            break;
          case 'SET_LAYOUT':
            h.handleLayoutChange(act.layoutName);
            break;
          case 'SET_SPACING':
            h.handleSpacingChange(act.spacingFactor);
            break;
          case 'SET_ORBIT_DISTANCE':
            h.handleOrbitDistanceChange(act.orbitDistance);
            break;
          case 'SET_MIN_ORBIT_NODE_DISTANCE':
            h.handleMinOrbitNodeDistanceChange(act.minOrbitNodeDistance);
            break;
          case 'SET_AUTO_RELAYOUT':
            h.handleAutoRelayoutChange(act.enabled);
            break;
          case 'FORCE_RELAYOUT':
            h.handleForceRelayout();
            break;
          case 'APPLY_ORBIT_CONFIG':
            if (act.resetHidden) {
              h.handleResetOrbitOrder();
            } else {
              h.applyOrbitConfig(act.next, act.clearHiddenFor || []);
            }
            break;
          case 'TOGGLE_ORBIT_VISIBILITY':
            h.toggleOrbitVisibility(act.levelIndex);
            break;
          case 'HIGHLIGHT_ORBIT_NODES':
            h.highlightOrbitNodes(act.nodeIds);
            break;
          case 'CLEAR_ORBIT_HIGHLIGHT':
            h.clearOrbitHighlight();
            break;
          case 'UNHIDE_NODE':
            h.unhideNode(act.id);
            break;
          case 'RESTORE_ALL_HIDDEN':
            h.restoreAllHiddenNodes();
            break;
          case 'TOGGLE_TYPE_VISIBILITY':
            h.toggleTypeVisibility(act.kind);
            break;
          case 'TOGGLE_SINGLE_CONN_DBS':
            forceRelayoutRef.current = true;
            setHideSingleConnectionDbs((prev) => !prev);
            break;
          case 'TOGGLE_ISOLATED_NODES':
            forceRelayoutRef.current = true;
            setHideIsolatedNodes((prev) => !prev);
            break;
          case 'TOGGLE_TRANSITIVE_EDGES':
            h.handleToggleTransitiveEdges();
            break;
          case 'TOGGLE_TYPE_GROUPING':
            h.toggleTypeGrouping(act.kind);
            break;
          case 'CREATE_CUSTOM_GROUP':
            h.createCustomGroup(act.name, act.nodeIds);
            break;
          case 'TOGGLE_CUSTOM_GROUP_COLLAPSE':
            h.toggleCustomGroupCollapse(act.groupId);
            break;
          case 'DELETE_CUSTOM_GROUP':
            h.deleteCustomGroup(act.groupId);
            break;
          case 'SET_NODE_CONTENT_MODE':
            h.handleNodeContentModeChange(act.mode);
            break;
          case 'SET_NODE_SIZING_MODE':
            h.handleNodeSizingModeChange(act.mode);
            break;
          case 'SET_EDGE_CURVE_MODE':
            h.handleEdgeCurveModeChange(act.mode);
            break;
          case 'SET_EDGE_LABELS_ON_HOVER':
            h.handleEdgeLabelsOnHoverChange(act.onHover);
            break;
          case 'SET_BASE_ICON_SIZE':
            h.handleBaseIconSizeChange(act.size);
            break;
          case 'SET_FONT_SIZE':
            h.handleFontSizeChange(act.fontSize);
            break;
          case 'SET_CURVE_FACTOR':
            h.handleCurveFactorChange(act.curveFactor);
            break;
          case 'SET_WHEEL_SENSITIVITY':
            h.handleWheelSensitivityChange(act.wheelSensitivity);
            break;
          case 'TOGGLE_PM_PRESET':
            h.onToggleProjectMapPreset?.(act.preset);
            break;
        }
      }
    };
    window.addEventListener('message', onMessage);
    return () => window.removeEventListener('message', onMessage);
  }, []);

  return (
    <div className="domain-map-canvas-container">
      {/* Non-blocking loading overlay during heavy synthesis and layout */}
      {isPreparing && !isMatrixMode && (
        <div className="view-loading-overlay">
          <div className="view-loading-card">
            <div className="view-loading-spinner" />
            <div className="view-loading-content">
              <span className="view-loading-title">{hudTitle || 'Architecture Map'}</span>
              <span className="view-loading-subtitle">{preparingStatus}</span>
            </div>
          </div>
        </div>
      )}

      {/* Docked Top Studio Bar */}
      <header className="domain-map-hud">
        {/* Left Brand Column (Rowspan 2) */}
        <div className="domain-hud-brand">
          <span className="domain-hud-label">{hudLabel || 'Macro Architecture'}</span>
          <span className="domain-hud-title">{hudTitle || (isMatrixMode ? 'Architecture Matrix' : 'Architecture Map')}</span>
        </div>

        {/* Right Content Column: Two Stacked Rows of Controls */}
        <div className="domain-hud-content">
          {/* Row 1: Studio Canvas Controls */}
          <div className="domain-hud-row domain-hud-primary-row">
            <div className="domain-hud-controls-left">
            {headerSlot}

            {/* Graph Layout Selector */}
            {!isMatrixMode && (
              <div className="domain-hud-control-group domain-hud-layout-select" title="Graph Layout Algorithm">
                <select
                  value={layoutName}
                  onChange={(e) => handleLayoutChange(e.target.value as any)}
                  className="domain-layout-dropdown"
                  title="Graph Layout"
                >
                  {layoutOptions.map((opt) => (
                    <option key={opt.id} value={opt.id}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              </div>
            )}

            {/* Zoom Controls / Refresh */}
            <div className="domain-hud-control-group" title="Viewport Controls">
              {!isMatrixMode && (
                <>
                  <button
                    type="button"
                    className="domain-hud-btn"
                    onClick={handleZoomOut}
                    title="Zoom Out"
                  >
                    −
                  </button>
                  <button
                    type="button"
                    className="domain-hud-btn zoom-level-btn"
                    onClick={handleResetZoom}
                    title="Reset Zoom to 100%"
                  >
                    {Math.round(currentZoom * 100)}%
                  </button>
                  <button
                    type="button"
                    className="domain-hud-btn"
                    onClick={handleZoomIn}
                    title="Zoom In"
                  >
                    +
                  </button>
                  <button
                    type="button"
                    className="domain-hud-btn fit-btn"
                    onClick={handleFitView}
                    title="Fit to Screen"
                  >
                    ⛶ Fit
                  </button>
                </>
              )}
              {onRefresh && (
                <button
                  type="button"
                  className="domain-hud-btn refresh-btn"
                  onClick={onRefresh}
                  title={isMatrixMode ? 'Reload Architecture Matrix' : 'Reload Architecture Map'}
                >
                  🔄 Refresh
                </button>
              )}
            </div>
          </div>

          {/* Search & Sidebar Controls */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <div className="domain-hud-search">
              <input
                type="text"
                className="domain-search-input"
                placeholder="Search domain or service..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    const matches = cyRef.current?.nodes('.search-match');
                    if (matches && matches.length > 0) {
                      cyRef.current?.animate({ fit: { eles: matches, padding: 80 }, duration: 350 });
                    }
                  }
                }}
              />
              {searchMatchCount !== null && (
                <span
                  className={`domain-search-count ${searchMatchCount === 0 ? 'is-zero' : ''}`}
                  title={searchMatchCount === 0 ? 'No matches found' : `${searchMatchCount} matching entities (Press Enter to focus)`}
                  onClick={() => {
                    const matches = cyRef.current?.nodes('.search-match');
                    if (matches && matches.length > 0) {
                      cyRef.current?.animate({ fit: { eles: matches, padding: 80 }, duration: 350 });
                    }
                  }}
                >
                  {searchMatchCount}
                </span>
              )}
              {searchQuery && (
                <button
                  type="button"
                  className="domain-search-clear"
                  onClick={() => setSearchQuery('')}
                  title="Clear search"
                >
                  ✕
                </button>
              )}
            </div>

            {/* Focus Native VS Code Inspector Sidebar button */}
            <button
              type="button"
              className="domain-hud-btn sidebar-toggle-btn"
              onClick={() => {
                getVsCodeApi()?.postMessage({ type: 'FOCUS_INSPECTOR_VIEW', preserveFocus: false });
                try {
                  getVsCodeApi()?.postMessage({ type: 'SYNC_INSPECTOR_STATE', state: buildInspectorSyncedState() });
                } catch {}
              }}
              title="Open Graph Inspector & Controls in Secondary Side Bar (Info, Settings)"
            >
              <span className="btn-icon">🗂️</span>
              <span className="btn-text">Inspector</span>
              {hiddenCount > 0 && (
                <span style={{ fontSize: '9.5px', marginLeft: 4, background: 'rgba(239, 68, 68, 0.35)', padding: '1px 5px', borderRadius: '10px' }}>
                  {hiddenCount}
                </span>
              )}
            </button>
          </div>
        </div>
      </div>
    </header>

      {/* Main Content Area: Resizable Horizontal Panel Layout */}
      <Group orientation="horizontal" className="domain-panel-group">
        {/* Center Canvas / Matrix Panel */}
        <Panel id="center-canvas" minSize="30%" className="domain-dock-panel-center">
          <div className="domain-cytoscape-wrapper" style={{ display: 'flex', flexDirection: 'column', width: '100%', height: '100%', flex: 1, position: 'relative', overflow: 'hidden' }}>

      {isMatrixMode ? (
        <DomainMatrixView
          nodes={visibleNodes.map((n) => rawGraph.detailMap.get(n.data.id as string)!).filter(Boolean)}
          edges={visibleEdges.map((e) => ({
            source: (e.data as any).source,
            target: (e.data as any).target,
            category: (e.data as any).category,
            count: (e.data as any).count || 1,
            isTransitive: (e.data as any).isTransitive,
            label: (e.data as any).label,
          }))}
          selectedNodeId={selectedNode?.id}
          onSelectNode={(node) => {
            setSelectedNode(node);
            if (node) {
              const gNode: GraphNode = {
                id: node.id,
                name: node.name,
                displayName: node.displayName,
                kind: node.kind as any,
                filePath: node.primaryFilePath,
                properties: {
                  kind: node.kind,
                  framework: node.framework || '',
                  language: node.language || '',
                  inboundCalls: String(node.inboundCallsCount),
                  outboundCalls: String(node.outboundCallsCount),
                  dbCount: String(node.dbCount),
                  messagingCount: String(node.messagingCount),
                },
              };
              onSelectNode?.(gNode);
            } else {
              onSelectNode?.(null);
            }
          }}
          onOpenFile={onOpenFile}
        />
      ) : layoutName === 'mermaid' ? (
        <DomainMermaidView
          nodes={visibleNodes.map((n) => rawGraph.detailMap.get(n.data.id as string)!).filter(Boolean)}
          edges={visibleEdges.map((e) => ({
            source: (e.data as any).source,
            target: (e.data as any).target,
            category: (e.data as any).category,
            count: (e.data as any).count || 1,
            label: (e.data as any).label,
            kind: (e.data as any).kind,
          }))}
          selectedNodeId={selectedNode?.id}
          onSelectNode={(nodeId) => {
            const detail = rawGraph.detailMap.get(nodeId);
            if (detail) {
              setSelectedNode(detail);
              setSelectedEdge(null);
            }
          }}
          title={hudTitle || 'Architecture Mermaid Flowchart'}
        />
      ) : (
        <>
          {/* SVG Overlay Guides for Concentric, Swimlanes, Domain Islands, and Hive Plot */}
          {((layoutName.startsWith('concentric') && (concentricGuides.length > 0 || sectorGuides.length > 0)) ||
            (layoutName === 'swimlanes' && swimlaneGuides.length > 0) ||
            (layoutName === 'clusters' && islandGuides.length > 0) ||
            (layoutName === 'hive' && hiveGuides.length > 0)) && (
            <svg
              className="domain-layout-overlay"
              style={{
                position: 'absolute',
                inset: 0,
                width: '100%',
                height: '100%',
                pointerEvents: 'none',
                zIndex: 0,
              }}
            >
              <g transform={`translate(${cyTransform.pan.x}, ${cyTransform.pan.y}) scale(${cyTransform.zoom})`}>
                {/* 1. Standard Concentric Guides */}
                {layoutName.startsWith('concentric') &&
                  layoutName !== 'concentric-sectors' &&
                  concentricGuides.map((g, idx) => {
                    if (hiddenOrbitTiers.has(g.levelIndex)) return null;
                    const zoomFactor = Math.max(0.75, Math.min(1.35, 1 / cyTransform.zoom));
                    const strokeW = Math.max(1, Math.min(3, 1.5 / cyTransform.zoom));
                    const dashPattern = `${Math.round(8 / cyTransform.zoom)} ${Math.round(6 / cyTransform.zoom)}`;
                    const guideFontSize = Math.min(14, Math.max(9, 11 * zoomFactor));
                    const shortLabel = g.shortLabel || (g.label.includes(':') ? g.label.split(':')[0] : g.label);
                    const labelText = `${shortLabel} (${g.count})`;
                    const textWidth = labelText.length * guideFontSize * 0.58;
                    const hPad = Math.max(10, Math.min(18, 14 * zoomFactor));
                    const badgeWidth = textWidth + hPad * 2;
                    const badgeHeight = Math.max(18, Math.min(28, 22 * zoomFactor));
                    const badgeY = -g.radius - badgeHeight - Math.min(10, 6 * zoomFactor);

                    return (
                      <g key={idx}>
                        <circle
                          cx={0}
                          cy={0}
                          r={g.radius}
                          fill="none"
                          stroke="rgba(56, 189, 248, 0.22)"
                          strokeWidth={strokeW}
                          strokeDasharray={dashPattern}
                        />
                        <rect
                          x={-badgeWidth / 2}
                          y={badgeY}
                          width={badgeWidth}
                          height={badgeHeight}
                          rx={Math.min(6, 4 * zoomFactor)}
                          fill="rgba(15, 23, 42, 0.88)"
                          stroke="rgba(56, 189, 248, 0.45)"
                          strokeWidth={strokeW}
                        />
                        <text
                          x={0}
                          y={badgeY + badgeHeight / 2}
                          fill="#38bdf8"
                          fontSize={`${guideFontSize.toFixed(1)}px`}
                          fontWeight="700"
                          textAnchor="middle"
                          dominantBaseline="central"
                          textLength={badgeWidth - 8}
                          lengthAdjust="spacingAndGlyphs"
                        >
                          <title>{labelText}</title>
                          {labelText}
                        </text>
                      </g>
                    );
                  })}

                {/* 1.1 Concentric Domain Sector Guides */}
                {layoutName === 'concentric-sectors' && (
                  <g className="concentric-sectors-overlay">
                    {/* Subtle concentric orbit rings */}
                    {concentricGuides.map((cg, cIdx) => {
                      if (hiddenOrbitTiers.has(cg.levelIndex)) return null;
                      return (
                        <circle
                          key={`orbit-${cIdx}`}
                          cx={0}
                          cy={0}
                          r={cg.radius}
                          fill="none"
                          stroke="rgba(148, 163, 184, 0.12)"
                          strokeWidth={Math.max(1, 1 / cyTransform.zoom)}
                          strokeDasharray={`${6 / cyTransform.zoom} ${6 / cyTransform.zoom}`}
                        />
                      );
                    })}

                    {/* Sector wedges and headers */}
                    {sectorGuides.map((g) => {
                      const zoomFactor = Math.max(0.75, Math.min(1.35, 1 / cyTransform.zoom));
                      const strokeW = Math.max(1, Math.min(3, 1.5 / cyTransform.zoom));
                      const dashPattern = `${Math.round(6 / cyTransform.zoom)} ${Math.round(4 / cyTransform.zoom)}`;
                      const guideFontSize = Math.min(14, Math.max(9, 11.5 * zoomFactor));
                      const labelText = `${g.displayName} (${g.count})`;
                      const textWidth = labelText.length * guideFontSize * 0.58;
                      const hPad = Math.max(12, Math.min(20, 16 * zoomFactor));
                      const badgeWidth = textWidth + hPad * 2;
                      const badgeHeight = Math.max(20, Math.min(30, 24 * zoomFactor));

                      const isFullCircle = g.innerRadius <= 0 && g.endAngle - g.startAngle >= 2 * Math.PI - 0.01;
                      const wedgePath = isFullCircle
                        ? ''
                        : describeAnnularSector(
                            0,
                            0,
                            g.innerRadius,
                            g.outerRadius,
                            g.startAngle,
                            g.endAngle
                          );

                      // Place badge just outside outer radius along centerAngle (or above center hub)
                      const badgeDist = isFullCircle
                        ? g.outerRadius + Math.max(20, 24 / cyTransform.zoom)
                        : g.outerRadius + Math.max(22, 28 / cyTransform.zoom);
                      const badgeX = Math.round(badgeDist * Math.cos(g.centerAngle));
                      const badgeY = Math.round(badgeDist * Math.sin(g.centerAngle));

                      return (
                        <g key={g.id}>
                          {/* Annular sector filled wedge or center hub circle */}
                          {isFullCircle ? (
                            <circle
                              cx={0}
                              cy={0}
                              r={g.outerRadius}
                              fill={g.color.startsWith('#') ? `${g.color}18` : 'rgba(56, 189, 248, 0.08)'}
                              stroke={g.color.startsWith('#') ? `${g.color}50` : 'rgba(56, 189, 248, 0.35)'}
                              strokeWidth={strokeW}
                            />
                          ) : (
                            <path
                              d={wedgePath}
                              fill={g.color.startsWith('#') ? `${g.color}18` : 'rgba(56, 189, 248, 0.08)'}
                              stroke={g.color.startsWith('#') ? `${g.color}50` : 'rgba(56, 189, 248, 0.35)'}
                              strokeWidth={strokeW}
                              strokeDasharray={g.isShared ? dashPattern : undefined}
                            />
                          )}

                          {/* Sector Label Pill Badge */}
                          <rect
                            x={badgeX - badgeWidth / 2}
                            y={badgeY - badgeHeight / 2}
                            width={badgeWidth}
                            height={badgeHeight}
                            rx={5 / cyTransform.zoom}
                            fill="rgba(15, 23, 42, 0.94)"
                            stroke={g.color.startsWith('#') ? `${g.color}90` : '#38bdf8'}
                            strokeWidth={1.5 / cyTransform.zoom}
                          />
                          <text
                            x={badgeX}
                            y={badgeY}
                            fill={g.color}
                            fontSize={`${guideFontSize}px`}
                            fontWeight="700"
                            textAnchor="middle"
                            dominantBaseline="central"
                          >
                            {labelText}
                          </text>
                        </g>
                      );
                    })}
                  </g>
                )}

                {/* 2. Swimlane Guides */}
                {layoutName === 'swimlanes' &&
                  swimlaneGuides.map((g) => {
                    const strokeW = Math.max(1, Math.min(3, 1.5 / cyTransform.zoom));
                    const dashPattern = `${Math.round(6 / cyTransform.zoom)} ${Math.round(4 / cyTransform.zoom)}`;
                    const zoomFactor = Math.max(0.75, Math.min(1.35, 1 / cyTransform.zoom));
                    const headerHeight = Math.max(24, Math.min(36, 28 * zoomFactor));
                    const hPad = Math.max(6, Math.min(12, 8 * zoomFactor));
                    const boxWidth = Math.max(40, g.width - hPad * 2);
                    const maxTextWidth = Math.max(20, boxWidth - 14);

                    const fullLabel = `${g.icon ? g.icon + ' ' : ''}${g.title} (${g.count})`;
                    const estCharWidth = 0.56;
                    const preferredFontSize = 11.5 * zoomFactor;
                    const maxFittingFontSize = maxTextWidth / (fullLabel.length * estCharWidth);
                    const finalFontSize = Math.min(preferredFontSize, Math.max(8, maxFittingFontSize));

                    let displayLabel = fullLabel;
                    if (fullLabel.length * 8 * estCharWidth > maxTextWidth) {
                      const iconPart = g.icon ? g.icon + ' ' : '';
                      const countPart = ` (${g.count})`;
                      const fixedLen = iconPart.length + countPart.length + 3;
                      const availableTitleChars = Math.max(3, Math.floor((maxTextWidth / (8 * estCharWidth)) - fixedLen));
                      const truncatedTitle = g.title.length > availableTitleChars
                        ? g.title.substring(0, availableTitleChars).trim() + '...'
                        : g.title;
                      displayLabel = `${iconPart}${truncatedTitle}${countPart}`;
                    }

                    const isSqueezed = fullLabel.length * finalFontSize * estCharWidth > maxTextWidth;

                    return (
                      <g key={g.id}>
                        <rect
                          x={g.x}
                          y={g.y}
                          width={g.width}
                          height={g.height}
                          rx={Math.min(12, 8 * zoomFactor)}
                          fill="rgba(15, 23, 42, 0.45)"
                          stroke={`${g.color}35`}
                          strokeWidth={strokeW}
                          strokeDasharray={dashPattern}
                        />
                        <rect
                          x={g.x + hPad}
                          y={g.y + hPad}
                          width={boxWidth}
                          height={headerHeight}
                          rx={Math.min(6, 4 * zoomFactor)}
                          fill="rgba(15, 23, 42, 0.92)"
                          stroke={`${g.color}70`}
                          strokeWidth={strokeW}
                        />
                        <text
                          x={g.x + g.width / 2}
                          y={g.y + hPad + headerHeight / 2}
                          fill={g.color}
                          fontSize={`${finalFontSize.toFixed(1)}px`}
                          fontWeight="700"
                          textAnchor="middle"
                          dominantBaseline="central"
                          textLength={isSqueezed ? maxTextWidth : undefined}
                          lengthAdjust={isSqueezed ? "spacingAndGlyphs" : undefined}
                        >
                          <title>{fullLabel}</title>
                          {displayLabel}
                        </text>
                      </g>
                    );
                  })}

                {/* 3. Domain Island Guides */}
                {layoutName === 'clusters' &&
                  islandGuides.map((g) => {
                    const strokeW = Math.max(1, Math.min(3, 2 / cyTransform.zoom));
                    const zoomFactor = Math.max(0.75, Math.min(1.35, 1 / cyTransform.zoom));
                    const badgeH = Math.max(22, Math.min(34, 26 * zoomFactor));
                    const hPad = Math.max(6, Math.min(12, 8 * zoomFactor));

                    const fullLabel = `${g.title} (${g.count})`;
                    const estCharWidth = 0.56;
                    const preferredFontSize = 11.5 * zoomFactor;

                    const maxBadgeW = Math.max(40, g.width - hPad * 2);
                    const idealBadgeW = Math.min(maxBadgeW, Math.max(120, fullLabel.length * preferredFontSize * estCharWidth + 24));
                    const badgeW = idealBadgeW;
                    const maxTextWidth = Math.max(20, badgeW - 14);

                    const maxFittingFontSize = maxTextWidth / (fullLabel.length * estCharWidth);
                    const finalFontSize = Math.min(preferredFontSize, Math.max(8, maxFittingFontSize));

                    let displayLabel = fullLabel;
                    if (fullLabel.length * 8 * estCharWidth > maxTextWidth) {
                      const countPart = ` (${g.count})`;
                      const availableTitleChars = Math.max(3, Math.floor((maxTextWidth / (8 * estCharWidth)) - countPart.length - 3));
                      const truncated = g.title.length > availableTitleChars
                        ? g.title.substring(0, availableTitleChars).trim() + '...'
                        : g.title;
                      displayLabel = `${truncated}${countPart}`;
                    }
                    const isSqueezed = fullLabel.length * finalFontSize * estCharWidth > maxTextWidth;

                    return (
                      <g key={g.id}>
                        <rect
                          x={g.x}
                          y={g.y}
                          width={g.width}
                          height={g.height}
                          rx={Math.min(20, 16 * zoomFactor)}
                          fill={g.isSharedCore ? 'rgba(88, 28, 135, 0.14)' : 'rgba(30, 41, 59, 0.35)'}
                          stroke={`${g.color}50`}
                          strokeWidth={strokeW}
                          strokeDasharray={g.isSharedCore ? `${Math.round(8 / cyTransform.zoom)} ${Math.round(4 / cyTransform.zoom)}` : undefined}
                        />
                        <rect
                          x={g.x + hPad}
                          y={g.y + hPad}
                          width={badgeW}
                          height={badgeH}
                          rx={Math.min(6, 4 * zoomFactor)}
                          fill="rgba(15, 23, 42, 0.92)"
                          stroke={`${g.color}75`}
                          strokeWidth={strokeW}
                        />
                        <text
                          x={g.x + hPad + badgeW / 2}
                          y={g.y + hPad + badgeH / 2}
                          fill={g.color}
                          fontSize={`${finalFontSize.toFixed(1)}px`}
                          fontWeight="700"
                          textAnchor="middle"
                          dominantBaseline="central"
                          textLength={isSqueezed ? maxTextWidth : undefined}
                          lengthAdjust={isSqueezed ? "spacingAndGlyphs" : undefined}
                        >
                          <title>{fullLabel}</title>
                          {displayLabel}
                        </text>
                      </g>
                    );
                  })}

                {/* 4. Hive Plot Guides */}
                {layoutName === 'hive' &&
                  hiveGuides.map((g) => {
                    const zoomFactor = Math.max(0.75, Math.min(1.35, 1 / cyTransform.zoom));
                    const strokeW = Math.max(1, Math.min(3, 1.8 / cyTransform.zoom));
                    const dashPattern = `${Math.round(6 / cyTransform.zoom)} ${Math.round(4 / cyTransform.zoom)}`;
                    const tipX = Math.round(g.length * Math.cos(g.angle));
                    const tipY = Math.round(g.length * Math.sin(g.angle));
                    const guideFontSize = Math.min(14, Math.max(9, 11 * zoomFactor));
                    const labelText = `${g.title} (${g.count})`;
                    const textWidth = labelText.length * guideFontSize * 0.58;
                    const badgeW = textWidth + Math.min(28, 20 * zoomFactor);
                    const badgeH = Math.max(20, Math.min(30, 24 * zoomFactor));

                    return (
                      <g key={g.id}>
                        <line
                          x1={0}
                          y1={0}
                          x2={tipX}
                          y2={tipY}
                          stroke={g.color}
                          strokeWidth={strokeW}
                          strokeDasharray={dashPattern}
                          opacity={0.65}
                        />
                        <g transform={`translate(${tipX}, ${tipY})`}>
                          <rect
                            x={-badgeW / 2}
                            y={-badgeH / 2}
                            width={badgeW}
                            height={badgeH}
                            rx={4 / cyTransform.zoom}
                            fill="rgba(15, 23, 42, 0.94)"
                            stroke={g.color}
                            strokeWidth={1 / cyTransform.zoom}
                          />
                          <text
                            x={0}
                            y={0}
                            fill={g.color}
                            fontSize={`${guideFontSize}px`}
                            fontWeight="700"
                            textAnchor="middle"
                            dominantBaseline="central"
                          >
                            {g.icon} {g.title} ({g.count})
                          </text>
                        </g>
                      </g>
                    );
                  })}
              </g>
            </svg>
          )}
          <div ref={containerRef} className="domain-cytoscape-container" />
        </>
      )}

          {/* Floating Canvas Multi-Select Action Bar */}
          {multiSelectedNodeIds.size > 0 && (
            <div
              className="canvas-multiselect-bar"
              style={{
                position: 'absolute',
                bottom: 24,
                left: '50%',
                transform: 'translateX(-50%)',
                zIndex: 999,
                display: 'flex',
                alignItems: 'center',
                gap: 8,
                background: 'rgba(15, 23, 42, 0.92)',
                border: '1px solid rgba(56, 189, 248, 0.4)',
                borderRadius: 8,
                padding: '6px 12px',
                boxShadow: '0 8px 24px rgba(0, 0, 0, 0.45)',
                backdropFilter: 'blur(8px)',
              }}
            >
              <span style={{ fontSize: '12px', fontWeight: 600, color: '#38bdf8' }}>
                {multiSelectedNodeIds.size} selected
              </span>
              <input
                type="text"
                placeholder="Group name (optional)..."
                value={floatingGroupName}
                onChange={(e) => setFloatingGroupName(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    createCustomGroup(
                      floatingGroupName.trim() || `Group (${multiSelectedNodeIds.size})`,
                      Array.from(multiSelectedNodeIds)
                    );
                    setFloatingGroupName('');
                  }
                }}
                style={{
                  background: 'rgba(255, 255, 255, 0.08)',
                  border: '1px solid rgba(255, 255, 255, 0.15)',
                  borderRadius: 4,
                  padding: '3px 8px',
                  fontSize: '11px',
                  color: '#f8fafc',
                  outline: 'none',
                  width: 150,
                }}
              />
              <button
                type="button"
                onClick={() => {
                  createCustomGroup(
                    floatingGroupName.trim() || `Group (${multiSelectedNodeIds.size})`,
                    Array.from(multiSelectedNodeIds)
                  );
                  setFloatingGroupName('');
                }}
                style={{
                  background: '#0284c7',
                  color: '#ffffff',
                  border: 'none',
                  borderRadius: 4,
                  padding: '4px 10px',
                  fontSize: '11px',
                  fontWeight: 600,
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: 4,
                }}
              >
                <span>Group</span>
              </button>
              <button
                type="button"
                onClick={clearMultiSelection}
                style={{
                  background: 'transparent',
                  border: '1px solid rgba(255, 255, 255, 0.2)',
                  color: '#94a3b8',
                  borderRadius: 4,
                  padding: '4px 8px',
                  fontSize: '11px',
                  cursor: 'pointer',
                }}
                title="Clear multi-selection"
              >
                Clear
              </button>
            </div>
          )}

          {/* Context Menu for Nodes */}
          {contextMenu && (() => {
            const isGroupNode = contextMenu.nodeId.startsWith('group:');
            const targetNodeIds = multiSelectedNodeIds.has(contextMenu.nodeId) && multiSelectedNodeIds.size > 1
              ? Array.from(multiSelectedNodeIds)
              : [contextMenu.nodeId];
            const downstreamNodeIds = isGroupNode ? [] : getDownstreamNodeIds(contextMenu.nodeId);
            const isSubmenuLeft = typeof window !== 'undefined' && contextMenu.x + 360 > window.innerWidth;

            return (
              <div
                className="domain-node-context-menu"
                style={{
                  position: 'absolute',
                  left: `${contextMenu.x}px`,
                  top: `${contextMenu.y}px`,
                  zIndex: 1000,
                }}
                onClick={(e) => e.stopPropagation()}
              >
                <div className="context-menu-header">
                  <span className="context-menu-title" title={contextMenu.nodeName}>
                    {contextMenu.nodeName}
                  </span>
                  <span className="context-menu-kind">{contextMenu.nodeKind}</span>
                </div>
                <div className="context-menu-divider" />
                {multiSelectedNodeIds.size > 1 && (
                  <button
                    type="button"
                    className="context-menu-item"
                    onClick={() => {
                      createCustomGroup(`Group (${multiSelectedNodeIds.size})`, Array.from(multiSelectedNodeIds));
                      setContextMenu(null);
                    }}
                  >
                    <span>Group {multiSelectedNodeIds.size} Selected</span>
                  </button>
                )}
                {!isGroupNode && (
                  <>
                    <div
                      className="context-menu-submenu-container"
                      onMouseEnter={() => setIsAddGroupSubmenuOpen(true)}
                      onMouseLeave={() => setIsAddGroupSubmenuOpen(false)}
                    >
                      <button
                        type="button"
                        className="context-menu-item context-menu-submenu-trigger"
                        onClick={(e) => {
                          e.stopPropagation();
                          setIsAddGroupSubmenuOpen((prev) => !prev);
                        }}
                      >
                        <span>Add to group</span>
                        <span className="submenu-arrow">▶</span>
                      </button>
                      {isAddGroupSubmenuOpen && (
                        <div className={`context-menu-submenu ${isSubmenuLeft ? 'submenu-left' : ''}`}>
                          {customGroups.map((group) => {
                            const isMember = targetNodeIds.every((id) => group.nodeIds.includes(id));
                            return (
                              <button
                                key={group.id}
                                type="button"
                                className="context-menu-item"
                                onClick={() => {
                                  addNodesToCustomGroup(group.id, targetNodeIds);
                                  setContextMenu(null);
                                }}
                              >
                                <span className="context-menu-submenu-item-name" title={group.name}>
                                  {isMember ? '✓ ' : ''}{group.name}
                                </span>
                                <span className="context-menu-badge">{group.nodeIds.length}</span>
                              </button>
                            );
                          })}
                          {customGroups.length > 0 && <div className="context-menu-divider" />}
                          <button
                            type="button"
                            className="context-menu-item"
                            onClick={() => {
                              const defaultName = targetNodeIds.length > 1
                                ? `Group (${targetNodeIds.length})`
                                : `Group of ${contextMenu.nodeName}`;
                              createCustomGroup(defaultName, targetNodeIds);
                              setContextMenu(null);
                            }}
                          >
                            <span>+ New Group...</span>
                          </button>
                        </div>
                      )}
                    </div>
                    <button
                      type="button"
                      className="context-menu-item"
                      disabled={downstreamNodeIds.length === 0}
                      onClick={() => {
                        createCustomGroup(`Downstream of ${contextMenu.nodeName}`, downstreamNodeIds);
                        setContextMenu(null);
                      }}
                    >
                      <span>Group all downstream {downstreamNodeIds.length > 0 ? `(${downstreamNodeIds.length})` : ''}</span>
                    </button>
                  </>
                )}
                {isGroupNode && (
                  <button
                    type="button"
                    className="context-menu-item"
                    onClick={() => {
                      const gId = contextMenu.nodeId;
                      if (gId.startsWith('group:custom:')) {
                        deleteCustomGroup(gId.replace('group:custom:', ''));
                      } else if (gId.startsWith('group:kind:')) {
                        toggleTypeGrouping(gId.replace('group:kind:', '') as EntityKind);
                      }
                      setContextMenu(null);
                      setSelectedNode(null);
                    }}
                  >
                    <span>Ungroup / Expand</span>
                  </button>
                )}
                <div className="context-menu-divider" />
                <button
                  type="button"
                  className="context-menu-item"
                  onClick={() => hideNode(contextMenu.nodeId)}
                >
                  <span>Hide Node</span>
                </button>
                <button
                  type="button"
                  className="context-menu-item"
                  onClick={() => hideDownstreamNodes(contextMenu.nodeId)}
                >
                  <span>Hide Downstream</span>
                </button>
                <button
                  type="button"
                  className="context-menu-item"
                  onClick={() => hideAllOfKind(contextMenu.nodeKind)}
                >
                  <span>Hide all {contextMenu.nodeKind}s</span>
                </button>
              </div>
            );
          })()}

        </div>
      </Panel>

      {/* Graph Inspector & Controls (Info / Settings) now lives in the native VS Code WebviewView sidebar (codeExplorerInspectorView) */}
    </Group>

      <DomainOverrideModal
        isOpen={overrideModalOpen}
        serviceName={overrideTargetService}
        currentDomain={selectedNode?.displayName}
        availableDomains={availableDomains}
        onSave={(svc, dom, ctx) => onSetDomainOverride?.(svc, dom, ctx, false)}
        onReset={(svc) => onSetDomainOverride?.(svc, '', undefined, true)}
        onClose={() => setOverrideModalOpen(false)}
      />
    </div>
  );
};
