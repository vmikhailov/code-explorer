import React, { useEffect, useRef, useState, useMemo, useCallback } from 'react';
import cytoscape from 'cytoscape';
import dagre from 'cytoscape-dagre';
import { GraphData, GraphNode, GraphEdge, isProjectKind } from '../../../../proto/types';
import {
  computeEchelonTiers,
  computeConcentricLayout,
  ConcentricNodeInput,
  ConcentricEdgeInput,
  ConcentricOrbitGuide,
  ConcentricLayoutResult,
  DomainSectorGuide,
} from '../layout/concentricLayout';
import { computeConcentricEquispacedLayout } from '../layout/concentricEquispacedLayout';
import { computeConcentricPolarForceLayout } from '../layout/concentricPolarForceLayout';
import { computeConcentricSectorsLayout, describeAnnularSector } from '../layout/concentricSectorsLayout';
import { computeSwimlanesLayout, SwimlaneGuide } from '../layout/swimlanesLayout';
import { computeDomainIslandsLayout, IslandGuide } from '../layout/domainIslandsLayout';
import { computeHivePlotLayout, HiveAxisGuide } from '../layout/hivePlotLayout';
import { DomainMatrixView } from './DomainMatrixView';

export type DomainLayoutName =
  | 'concentric'
  | 'concentric-equispaced'
  | 'concentric-polar-force'
  | 'concentric-sectors'
  | 'swimlanes'
  | 'clusters'
  | 'hive'
  | 'matrix'
  | 'cose';

export type EdgeCurveMode = 'bezier' | 'straight' | 'avoid-inner';

export interface DomainArchitectureViewProps {
  graph: GraphData | null;
  onFocusInFlow?: (projectName: string) => void;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
  onSelectNode?: (node: GraphNode | null) => void;
  onSwitchToContexts?: () => void;
}

// Common sub-project naming suffixes that belong to a parent domain
const SUB_PROJECT_SUFFIX_REGEX =
  /\.(Logic|Client|Contracts|Data|Core|Domain|Infrastructure|Api|Service|Services|Web|Worker|Test|Tests|Shared|Models|Dto|SDK|UnitTests|IntegrationTests|GraphQL|GrapQL|Grpc|Gateway|Bff|Endpoint)$/i;

// Ingress / Frontend indicators
const INGRESS_KEYWORDS = ['admin', 'app', 'ui', 'fe', 'gateway', 'bff', 'portal', 'web', 'client-app', 'landing', 'graphql', 'grapql', 'grpc', 'mqtt', 'endpoint'];
const INGRESS_FRAMEWORKS = ['angular', 'react', 'vue', 'svelte', 'next', 'vite', 'blazor'];

/**
 * Normalizes project names and directory paths into a cohesive Domain / Bounded Context key.
 */
function extractDomainKey(node: GraphNode): { domainKey: string; domainDisplayName: string; isIngressHint: boolean } {
  const name = node.name || '';
  const path = (node.filePath || '').toLowerCase().replace(/\\/g, '/');
  const lowerName = name.toLowerCase();

  const isProtocolIngress =
    INGRESS_KEYWORDS.some((kw) => lowerName.includes(kw) || path.includes(`/${kw}/`)) ||
    INGRESS_FRAMEWORKS.some((fw) => (node.properties?.framework || '').toLowerCase().includes(fw)) ||
    node.properties?.has_ingress_contract === 'true' ||
    node.properties?.layer === 'layer_ingress' ||
    node.properties?.layerId === 'layer_ingress';

  // 1. Check if name ends with standard architectural suffix (e.g. Lidoma.Services.Player.Logic)
  const suffixMatch = name.match(SUB_PROJECT_SUFFIX_REGEX);
  if (suffixMatch) {
    const parentName = name.substring(0, suffixMatch.index);
    const dotParts = parentName.split('.');
    const shortName = dotParts[dotParts.length - 1];
    return {
      domainKey: `domain:${parentName.toLowerCase()}`,
      domainDisplayName: `${shortName} Service`,
      isIngressHint: isProtocolIngress,
    };
  }

  // 2. Directory-based grouping (e.g. services/player/... or adhub/...)
  if (path) {
    const pathParts = path.split('/').filter(Boolean);
    const servicesIdx = pathParts.findIndex((p) => p === 'services' || p === 'microservices');
    if (servicesIdx !== -1 && servicesIdx + 1 < pathParts.length) {
      const folderDomain = pathParts[servicesIdx + 1];
      const cleanName = folderDomain.charAt(0).toUpperCase() + folderDomain.slice(1);
      return {
        domainKey: `domain:${folderDomain.toLowerCase()}`,
        domainDisplayName: `${cleanName} Service`,
        isIngressHint: isProtocolIngress,
      };
    }

    // Monorepo sub-directory pattern (e.g. adhub/adhub-cli -> adhub)
    if (pathParts.length >= 2 && !['src', 'packages', 'libs', 'projects'].includes(pathParts[0])) {
      const folderDomain = pathParts[0];
      const cleanName = folderDomain.charAt(0).toUpperCase() + folderDomain.slice(1);
      return {
        domainKey: `domain:${folderDomain.toLowerCase()}`,
        domainDisplayName: `${cleanName}`,
        isIngressHint: INGRESS_KEYWORDS.some((kw) => folderDomain.toLowerCase().includes(kw)),
      };
    }
  }

  // 3. Standalone project
  const isIngress =
    INGRESS_KEYWORDS.some((kw) => lowerName.includes(kw)) ||
    INGRESS_FRAMEWORKS.some((fw) => (node.properties?.framework || '').toLowerCase().includes(fw));

  return {
    domainKey: `domain:${lowerName}`,
    domainDisplayName: name,
    isIngressHint: isIngress,
  };
}

export interface DomainProjectInfo {
  id: string;
  name: string;
  kind?: string;
  filePath?: string;
  isLibrary?: boolean;
  gitBranch?: string;
}

export type EntityKind = 'Service' | 'Ingress' | 'Worker' | 'Library' | 'Database' | 'Topic' | 'ExternalService';

export interface SelectedNodeDetail {
  id: string;
  name: string;
  displayName: string;
  kind: EntityKind;
  domain?: string;
  displayTag: string;
  bgColor: string;
  borderColor: string;
  framework?: string;
  language?: string;
  gitBranch?: string;
  primaryFilePath?: string;
  projects: DomainProjectInfo[];
  inboundCallsCount: number;
  outboundCallsCount: number;
  dbCount: number;
  messagingCount: number;
  tier?: number;
  tierLabel?: string;
}

export type DomainNodeDetail = SelectedNodeDetail;

// Cytoscape stylesheets matching the circular Neo4j / graph ontology styling
const CYTOSCAPE_STYLES: cytoscape.StylesheetStyle[] = [
  // Base Node Style (Circular Discs)
  {
    selector: 'node',
    style: {
      'shape': 'ellipse',
      'width': 'data(size)',
      'height': 'data(size)',
      'background-color': 'data(bgColor)',
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
  // Ingress / App
  {
    selector: 'node[kind = "Ingress"]',
    style: {
      'background-color': '#0288d1',
      'border-color': '#01579b',
    },
  },
  // Service
  {
    selector: 'node[kind = "Service"]',
    style: {
      'background-color': '#e53935',
      'border-color': '#7f1d1d',
    },
  },
  // Worker
  {
    selector: 'node[kind = "Worker"]',
    style: {
      'background-color': '#c026d3',
      'border-color': '#86198f',
    },
  },
  // Library
  {
    selector: 'node[kind = "Library"]',
    style: {
      'background-color': '#475569',
      'border-color': '#1e293b',
    },
  },
  // Database
  {
    selector: 'node[kind = "Database"]',
    style: {
      'background-color': '#7b1fa2',
      'border-color': '#4a148c',
    },
  },
  // Topic
  {
    selector: 'node[kind = "Topic"]',
    style: {
      'background-color': '#f59e0b',
      'border-color': '#b45309',
    },
  },
  // External
  {
    selector: 'node[kind = "ExternalService"]',
    style: {
      'background-color': '#26a69a',
      'border-color': '#004d40',
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
      'line-color': '#38bdf8',
      'target-arrow-color': '#38bdf8',
    },
  },
  {
    selector: 'edge[category = "database"]',
    style: {
      'line-style': 'solid',
      'line-color': '#c084fc',
      'target-arrow-color': '#c084fc',
    },
  },
  {
    selector: 'edge[category = "messaging"]',
    style: {
      'line-style': 'dashed',
      'line-dash-pattern': [6, 4],
      'line-color': '#fbbf24',
      'target-arrow-color': '#fbbf24',
    },
  },
  {
    selector: 'edge[category = "external"]',
    style: {
      'line-style': 'solid',
      'line-color': '#34d399',
      'target-arrow-color': '#34d399',
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
      'target-arrow-shape': 'triangle',
      'arrow-scale': 2.3,
    },
  },
  // Hovered Edge
  {
    selector: 'edge.hovered',
    style: {
      'width': 3.5,
      'opacity': 1,
      'z-index': 998,
      'target-arrow-shape': 'triangle',
      'arrow-scale': 2.3,
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
      'line-color': '#38bdf8',
      'target-arrow-color': '#38bdf8',
    },
  },
  {
    selector: 'edge[category = "database"].highlighted, edge[category = "database"]:selected',
    style: {
      'line-color': '#c084fc',
      'target-arrow-color': '#c084fc',
    },
  },
  {
    selector: 'edge[category = "messaging"].highlighted, edge[category = "messaging"]:selected',
    style: {
      'line-style': 'dashed',
      'line-dash-pattern': [8, 5],
      'line-color': '#fbbf24',
      'target-arrow-color': '#fbbf24',
    },
  },
  {
    selector: 'edge[category = "external"].highlighted, edge[category = "external"]:selected',
    style: {
      'line-color': '#34d399',
      'target-arrow-color': '#34d399',
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
      'underlay-padding': '10px',
      'underlay-opacity': 0.6,
      'border-color': '#f59e0b',
      'border-width': 4,
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
  graph,
  onFocusInFlow,
  onOpenFile,
  onSelectNode,
  onSwitchToContexts,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const cyRef = useRef<cytoscape.Core | null>(null);

  const [searchQuery, setSearchQuery] = useState('');
  const [layoutName, setLayoutName] = useState<DomainLayoutName>(() => {
    try {
      const saved = localStorage.getItem('ce_domain_layout');
      if (
        saved === 'cose' ||
        saved === 'concentric' ||
        saved === 'concentric-equispaced' ||
        saved === 'concentric-polar-force' ||
        saved === 'concentric-sectors' ||
        saved === 'swimlanes' ||
        saved === 'clusters' ||
        saved === 'hive' ||
        saved === 'matrix'
      ) return saved as DomainLayoutName;
    } catch { }
    return 'concentric';
  });
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

  const [concentricGuides, setConcentricGuides] = useState<ConcentricOrbitGuide[]>([]);
  const [orbitLegendItems, setOrbitLegendItems] = useState<Array<{
    levelIndex: number;
    shortLabel: string;
    title: string;
    count: number;
    radius: number;
    nodeIds: string[];
  }>>([]);
  const [isOrbitLegendOpen, setIsOrbitLegendOpen] = useState(true);
  const [customOrbitOrder, setCustomOrbitOrder] = useState<number[] | null>(null);
  const customOrbitOrderRef = useRef<number[] | null>(null);
  customOrbitOrderRef.current = customOrbitOrder;

  const [draggedOrbitIndex, setDraggedOrbitIndex] = useState<number | null>(null);
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
    cy.batch(() => {
      cy.elements().removeClass('highlighted dimmed');
    });
  }, []);
  const [hiddenTypes, setHiddenTypes] = useState<Set<EntityKind>>(new Set());
  const [hiddenNodeIds, setHiddenNodeIds] = useState<Set<string>>(new Set());
  const [hideSingleConnectionDbs, setHideSingleConnectionDbs] = useState<boolean>(false);
  const [hideIsolatedNodes, setHideIsolatedNodes] = useState<boolean>(false);

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

  // Display Settings Popover state
  const [isDisplayOpen, setIsDisplayOpen] = useState(false);
  const displayMenuRef = useRef<HTMLDivElement>(null);

  // Close display popover on outside click
  useEffect(() => {
    if (!isDisplayOpen) return;
    const handleClickOutside = (e: MouseEvent) => {
      if (displayMenuRef.current && !displayMenuRef.current.contains(e.target as Node)) {
        setIsDisplayOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [isDisplayOpen]);

  // Floating Side Panels Collapse States
  const [isHiddenPanelCollapsed, setIsHiddenPanelCollapsed] = useState(false);
  const [hiddenSearchQuery, setHiddenSearchQuery] = useState('');
  const [isInspectorCollapsed, setIsInspectorCollapsed] = useState(false);

  // Restore only hidden concrete entities
  const restoreAllHiddenNodes = useCallback(() => {
    forceRelayoutRef.current = true;
    setHiddenNodeIds(new Set());
  }, []);

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
  const [spacingFactor, setSpacingFactor] = useState<number>(1.0);
  const spacingFactorRef = useRef<number>(1.0);
  spacingFactorRef.current = spacingFactor;

  // Mouse wheel zoom sensitivity (default 2.5, 10x of previous 0.25)
  const [wheelSensitivity, setWheelSensitivity] = useState<number>(() => {
    try {
      const saved = localStorage.getItem('ce_wheel_sensitivity');
      if (saved) {
        const val = parseFloat(saved);
        if (!isNaN(val) && val >= 0.5 && val <= 5.0) return val;
      }
    } catch { }
    return 2.5;
  });
  const wheelSensitivityRef = useRef<number>(2.5);
  wheelSensitivityRef.current = wheelSensitivity;

  const basePositionsRef = useRef<Map<string, cytoscape.Position>>(new Map());
  const centroidRef = useRef<{ cx: number; cy: number }>({ cx: 0, cy: 0 });

  // Clear hidden filters when switching graph
  useEffect(() => {
    setHiddenTypes(new Set());
    setHiddenNodeIds(new Set());
    setSelectedNode(null);
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
    setSelectedNode((curr) => (curr?.kind === kind ? null : curr));
    if (cyRef.current) {
      cyRef.current.elements().removeClass('highlighted dimmed');
    }
  }, []);

  const hideNode = useCallback((nodeId: string) => {
    forceRelayoutRef.current = true;
    setHiddenNodeIds((prev) => {
      const next = new Set(prev);
      next.add(nodeId);
      return next;
    });
    setSelectedNode((curr) => (curr?.id === nodeId ? null : curr));
    if (cyRef.current) {
      cyRef.current.elements().removeClass('highlighted dimmed');
    }
  }, []);

  const unhideNode = useCallback((nodeId: string) => {
    forceRelayoutRef.current = true;
    setHiddenNodeIds((prev) => {
      const next = new Set(prev);
      next.delete(nodeId);
      return next;
    });
  }, []);

  const unhideAll = useCallback(() => {
    forceRelayoutRef.current = true;
    setHiddenTypes(new Set());
    setHiddenNodeIds(new Set());
    setHideSingleConnectionDbs(false);
    setHideIsolatedNodes(false);
  }, []);

  // 1. Synthesize Domain Entities & Infrastructure Nodes from GraphData
  const rawGraph = useMemo(() => {
    const cyNodes: cytoscape.NodeDefinition[] = [];
    const detailMap = new Map<string, SelectedNodeDetail>();

    const projToDomainMap = new Map<string, string>();
    const domainProjectsMap = new Map<string, DomainProjectInfo[]>();
    const domainPrimaryMap = new Map<string, GraphNode>();
    const domainZoneMap = new Map<string, 'ingress' | 'service'>();
    const domainNameMap = new Map<string, { name: string; displayName: string; framework?: string; language?: string; color?: string }>();

    // 1a. Categorize Projects into Domains
    for (const node of graph?.nodes || []) {
      if (!isProjectKind(node.kind)) continue;

      const { domainKey, domainDisplayName, isIngressHint } = extractDomainKey(node);
      projToDomainMap.set(node.id, domainKey);
      projToDomainMap.set(node.name, domainKey);
      projToDomainMap.set(node.id.toLowerCase(), domainKey);
      projToDomainMap.set(node.name.toLowerCase(), domainKey);

      const isLib =
        node.kind === 'Library' ||
        node.kind === 'SharedLibrary' ||
        node.properties?.is_library === 'true';

      let pList = domainProjectsMap.get(domainKey);
      if (!pList) {
        pList = [];
        domainProjectsMap.set(domainKey, pList);

        const isIngress =
          !isLib &&
          (node.kind === 'App' ||
           node.kind === 'FrontendApp' ||
           node.properties?.layer === 'layer_ingress' ||
           node.properties?.layerId === 'layer_ingress' ||
           node.properties?.has_ingress_contract === 'true' ||
           isIngressHint) &&
          node.kind !== 'Worker';
        domainZoneMap.set(domainKey, isIngress ? 'ingress' : 'service');

        domainNameMap.set(domainKey, {
          name: node.name,
          displayName: domainDisplayName,
          framework: node.properties?.framework,
          language: node.properties?.language || node.properties?.project_type,
        });
      }

      pList.push({
        id: node.id,
        name: node.name,
        kind: node.kind,
        filePath: node.filePath,
        isLibrary: isLib,
        gitBranch: node.properties?.git_branch,
      });

      const currPrimary = domainPrimaryMap.get(domainKey);
      const isCurrLib =
        currPrimary?.kind === 'Library' ||
        currPrimary?.kind === 'SharedLibrary' ||
        currPrimary?.properties?.is_library === 'true';
      if (!currPrimary || (isCurrLib && !isLib)) {
        domainPrimaryMap.set(domainKey, node);
        if (!isLib) {
          domainNameMap.set(domainKey, {
            name: node.name,
            displayName: domainDisplayName,
            framework: node.properties?.framework,
            language: node.properties?.language || node.properties?.project_type,
          });
          const isIngress =
            (node.kind === 'App' ||
             node.kind === 'FrontendApp' ||
             node.properties?.layer === 'layer_ingress' ||
             node.properties?.layerId === 'layer_ingress' ||
             node.properties?.has_ingress_contract === 'true' ||
             isIngressHint) &&
            node.kind !== 'Worker';
          domainZoneMap.set(domainKey, isIngress ? 'ingress' : 'service');
        }
      }
    }

    // 1b. Collect Infrastructure Entities (Databases, Topics, ExternalServices)
    const dbNodes = new Map<string, { id: string; name: string; dbType: string }>();
    const topicNodes = new Map<string, { id: string; name: string; broker: string }>();
    const extNodes = new Map<string, { id: string; name: string; serviceType: string }>();

    for (const node of graph?.nodes || []) {
      if (node.kind === 'Database' || node.properties?.role === 'database') {
        const name = node.name || node.displayName || 'Database';
        dbNodes.set(node.id, {
          id: node.id,
          name,
          dbType: node.properties?.db_type || 'relational',
        });
        projToDomainMap.set(node.id, node.id);
        projToDomainMap.set(node.id.toLowerCase(), node.id);
      } else if (node.kind === 'Topic' || node.properties?.role === 'topic') {
        const broker = (node.properties?.broker_type || '').toLowerCase();
        const isInternal =
          node.properties?.is_internal === 'true' ||
          node.properties?.scope === 'internal' ||
          broker === 'mediatr' ||
          broker === 'in-memory' ||
          node.id.includes(':mediatr:') ||
          node.id.includes(':in-memory:');
        if (isInternal) continue;

        const rawName = node.name || node.displayName || 'Topic';
        const isBogus =
          !rawName ||
          rawName.startsWith(':') ||
          rawName.toLowerCase() === 'topic' ||
          rawName.toLowerCase() === 'string' ||
          rawName.toLowerCase() === 'undefined' ||
          rawName.startsWith('http://') ||
          rawName.startsWith('https://');
        if (isBogus) continue;
        const name = rawName;
        topicNodes.set(node.id, {
          id: node.id,
          name,
          broker: node.properties?.broker_type || 'Message Queue',
        });
        projToDomainMap.set(node.id, node.id);
        projToDomainMap.set(node.id.toLowerCase(), node.id);
      } else if (node.kind === 'ExternalService') {
        const name = node.name || node.displayName || 'External Service';
        extNodes.set(node.id, {
          id: node.id,
          name,
          serviceType: node.properties?.service_type || 'API',
        });
        projToDomainMap.set(node.id, node.id);
        projToDomainMap.set(node.id.toLowerCase(), node.id);
      }
    }

    // 2. Synthesize Macro Edges
    interface EdgeAggregator {
      source: string;
      target: string;
      category: 'service_call' | 'database' | 'messaging' | 'external';
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

    for (const edge of graph?.edges || []) {
      const isInternalEdge =
        edge.properties?.is_internal === 'true' ||
        edge.properties?.scope === 'internal' ||
        edge.properties?.broker_type === 'mediatr' ||
        edge.source.includes(':mediatr:') ||
        edge.target.includes(':mediatr:');
      if (isInternalEdge) continue;

      const srcDomain =
        projToDomainMap.get(edge.source) ||
        projToDomainMap.get(edge.source.toLowerCase()) ||
        (dbNodes.has(edge.source) ? edge.source : null) ||
        (topicNodes.has(edge.source) ? edge.source : null);

      const tgtDomain =
        projToDomainMap.get(edge.target) ||
        projToDomainMap.get(edge.target.toLowerCase()) ||
        (dbNodes.has(edge.target) ? edge.target : null) ||
        (topicNodes.has(edge.target) ? edge.target : null) ||
        (extNodes.has(edge.target) ? edge.target : null);

      if (!srcDomain || !tgtDomain || srcDomain === tgtDomain) continue;

      let cat: 'service_call' | 'database' | 'messaging' | 'external' | null = null;
      let label = 'CALLS';

      const isTopicEdge = topicNodes.has(tgtDomain) || topicNodes.has(srcDomain);

      if (dbNodes.has(tgtDomain) || edge.category === 'database' || edge.kind === 'USES_DB') {
        cat = 'database';
        label = 'USES_DB';
        if (!dbUsage.has(srcDomain)) dbUsage.set(srcDomain, new Set());
        dbUsage.get(srcDomain)!.add(tgtDomain);
      } else if (isTopicEdge) {
        cat = 'messaging';
        const isSub =
          edge.kind === 'SUBSCRIBES_TO' ||
          edge.kind === 'SUBSCRIBED_BY' ||
          (topicNodes.has(srcDomain) && !topicNodes.has(tgtDomain));
        label = isSub ? 'SUBSCRIBES' : 'PUBLISHES';

        const tDomain = topicNodes.has(tgtDomain) ? tgtDomain : srcDomain;
        const sDomain = topicNodes.has(tgtDomain) ? srcDomain : tgtDomain;

        if (isSub) {
          if (!topicSubscribers.has(tDomain)) topicSubscribers.set(tDomain, new Set());
          topicSubscribers.get(tDomain)!.add(sDomain);
        } else {
          if (!topicPublishers.has(tDomain)) topicPublishers.set(tDomain, new Set());
          topicPublishers.get(tDomain)!.add(sDomain);
        }

        if (topicNodes.has(tgtDomain)) {
          if (!msgUsage.has(srcDomain)) msgUsage.set(srcDomain, new Set());
          msgUsage.get(srcDomain)!.add(tgtDomain);
        } else if (topicNodes.has(srcDomain)) {
          if (!msgUsage.has(tgtDomain)) msgUsage.set(tgtDomain, new Set());
          msgUsage.get(tgtDomain)!.add(srcDomain);
        }
      } else if (extNodes.has(tgtDomain)) {
        cat = 'external';
        label = 'CALLS';
      } else if (
        edge.category === 'service_call' ||
        edge.kind === 'SERVICE_CALL' ||
        edge.kind === 'CALLS_ENDPOINT' ||
        edge.kind === 'TRIGGERS'
      ) {
        cat = 'service_call';
        label = 'CALLS';
      } else if (
        edge.kind === 'SUBSCRIBES_TO' ||
        edge.kind === 'SUBSCRIBED_BY' ||
        edge.kind === 'PUBLISHES_TO' ||
        edge.kind === 'PUBLISHED_BY' ||
        edge.category === 'messaging'
      ) {
        // Direct pub/sub relation between two services (unrolled contract / shared event).
        // Record as a direct pub/sub candidate chord.
        const chordLabel =
          edge.kind === 'SUBSCRIBES_TO' || edge.kind === 'SUBSCRIBED_BY' ? 'SUBSCRIBES' : 'PUBLISHES';
        const chordKey = `${srcDomain}->${tgtDomain}`;
        const existingChord = directPubSubChords.get(chordKey);
        if (existingChord) {
          existingChord.count += 1;
        } else {
          directPubSubChords.set(chordKey, {
            source: srcDomain,
            target: tgtDomain,
            label: chordLabel,
            count: 1,
          });
        }
        continue;
      } else {
        const isClientLib = edge.target.toLowerCase().includes('.client') || edge.target.toLowerCase().endsWith('client');
        if (isClientLib) {
          cat = 'service_call';
          label = 'CALLS';
        }
      }

      if (!cat) continue;

      if (cat === 'service_call') {
        outCalls.set(srcDomain, (outCalls.get(srcDomain) || 0) + 1);
        inCalls.set(tgtDomain, (inCalls.get(tgtDomain) || 0) + 1);
      }

      const key = `${srcDomain}->${tgtDomain}:${cat}`;
      const existing = macroEdges.get(key);
      if (existing) {
        existing.count += 1;
      } else {
        macroEdges.set(key, {
          source: srcDomain,
          target: tgtDomain,
          category: cat,
          label,
          count: 1,
        });
      }
    }

    // 3. Build Cytoscape Nodes
    let ingressCount = 0;
    let serviceCount = 0;
    let workerCount = 0;

    // 3a. Service / Ingress / Worker Nodes
    for (const [domainId, meta] of domainNameMap.entries()) {
      const projects = domainProjectsMap.get(domainId) || [];
      const primaryNode = domainPrimaryMap.get(domainId);

      const isPureLibDomain =
        projects.length > 0
          ? projects.every(
              (p) =>
                p.isLibrary ||
                p.kind === 'Library' ||
                p.kind === 'SharedLibrary' ||
                p.kind === 'Shared'
            )
          : primaryNode?.kind === 'Library' ||
            primaryNode?.kind === 'SharedLibrary' ||
            primaryNode?.properties?.is_library === 'true';

      const isUiComponentPackage =
        projects.length > 0 &&
        projects.every((p) => {
          const fp = (p.filePath || '').toLowerCase().replace(/\\/g, '/');
          return fp.includes('/packages/ui/') || fp.includes('/packages/components/') || fp.includes('/src/components/');
        });

      if (isPureLibDomain || isUiComponentPackage) {
        // Pure library & UI component domains MUST NOT appear on Domain Service Map
        continue;
      }

      const zone = domainZoneMap.get(domainId) || 'service';
      const isIngress = zone === 'ingress';

      const isWorker =
        primaryNode?.kind === 'Worker' ||
        primaryNode?.properties?.role === 'Worker' ||
        projects.some((p) => p.kind === 'Worker');

      let tag = ':Service';
      let nodeKind: EntityKind = 'Service';
      let bgColor = '#e53935';
      let borderColor = '#7f1d1d';
      let size = 50;

      if (isIngress) {
        ingressCount++;
        tag = ':Ingress';
        nodeKind = 'Ingress';
        bgColor = '#0288d1';
        borderColor = '#01579b';
        size = 54;
      } else if (isWorker) {
        workerCount++;
        tag = ':Worker';
        nodeKind = 'Worker';
        bgColor = '#c026d3';
        borderColor = '#86198f';
        size = 48;
      } else {
        serviceCount++;
      }

      const detail: SelectedNodeDetail = {
        id: domainId,
        name: meta.name,
        displayName: meta.displayName,
        domain: domainId,
        kind: nodeKind,
        displayTag: tag,
        bgColor,
        borderColor,
        framework: meta.framework,
        language: meta.language,
        gitBranch: primaryNode?.properties?.git_branch || projects.find((p) => p.gitBranch)?.gitBranch,
        primaryFilePath: primaryNode?.filePath || projects[0]?.filePath,
        projects,
        inboundCallsCount: inCalls.get(domainId) || 0,
        outboundCallsCount: outCalls.get(domainId) || 0,
        dbCount: dbUsage.get(domainId)?.size || 0,
        messagingCount: msgUsage.get(domainId)?.size || 0,
      };
      detailMap.set(domainId, detail);

      cyNodes.push({
        group: 'nodes',
        data: {
          id: domainId,
          name: meta.name,
          displayName: meta.displayName,
          displayLabel: `${tag}\n${meta.displayName}`,
          kind: nodeKind,
          bgColor,
          borderColor,
          size,
        },
      });
    }

    // 3b. Database Nodes
    let dbCount = 0;
    for (const [dbId, db] of dbNodes.entries()) {
      const isUsed = Array.from(macroEdges.values()).some((e) => e.target === dbId || e.source === dbId);
      if (!isUsed && dbNodes.size > 20) continue;
      dbCount++;

      const tag = ':DB';
      const bgColor = '#7b1fa2';
      const borderColor = '#4a148c';

      detailMap.set(dbId, {
        id: dbId,
        name: db.name,
        displayName: db.name,
        kind: 'Database',
        displayTag: tag,
        bgColor,
        borderColor,
        framework: db.dbType,
        projects: [],
        inboundCallsCount: 0,
        outboundCallsCount: 0,
        dbCount: 0,
        messagingCount: 0,
      });

      cyNodes.push({
        group: 'nodes',
        data: {
          id: dbId,
          name: db.name,
          displayName: db.name,
          displayLabel: `${tag}\n${db.name}`,
          kind: 'Database',
          bgColor,
          borderColor,
          size: 48,
        },
      });
    }

    // 3c. Message Topics / Queues
    let topicCount = 0;
    for (const [tId, t] of topicNodes.entries()) {
      const isUsed = Array.from(macroEdges.values()).some((e) => e.target === tId || e.source === tId);
      if (!isUsed && topicNodes.size > 25) continue;
      topicCount++;

      const tag = ':Topic';
      const bgColor = '#f59e0b';
      const borderColor = '#b45309';

      detailMap.set(tId, {
        id: tId,
        name: t.name,
        displayName: t.name,
        kind: 'Topic',
        displayTag: tag,
        bgColor,
        borderColor,
        framework: t.broker,
        projects: [],
        inboundCallsCount: 0,
        outboundCallsCount: 0,
        dbCount: 0,
        messagingCount: 0,
      });

      cyNodes.push({
        group: 'nodes',
        data: {
          id: tId,
          name: t.name,
          displayName: t.name,
          displayLabel: `${tag}\n${t.name}`,
          kind: 'Topic',
          bgColor,
          borderColor,
          size: 46,
        },
      });
    }

    // 3d. External Services
    let extCount = 0;
    for (const [extId, ext] of extNodes.entries()) {
      const isUsed = Array.from(macroEdges.values()).some((e) => e.target === extId);
      if (!isUsed) continue;
      extCount++;

      const tag = ':External';
      const bgColor = '#26a69a';
      const borderColor = '#004d40';

      detailMap.set(extId, {
        id: extId,
        name: ext.name,
        displayName: ext.name,
        kind: 'ExternalService',
        displayTag: tag,
        bgColor,
        borderColor,
        framework: ext.serviceType,
        projects: [],
        inboundCallsCount: 0,
        outboundCallsCount: 0,
        dbCount: 0,
        messagingCount: 0,
      });

      cyNodes.push({
        group: 'nodes',
        data: {
          id: extId,
          name: ext.name,
          displayName: ext.name,
          displayLabel: `${tag}\n${ext.name}`,
          kind: 'ExternalService',
          bgColor,
          borderColor,
          size: 44,
        },
      });
    }

    // 4. Raw Macro Edges & Outgoing Adjacency
    const validNodeIdSet = new Set(cyNodes.map((n) => n.data.id as string));
    const rawEdges: Array<{ id: string; source: string; target: string; category: 'service_call' | 'database' | 'messaging' | 'external'; label: string; count: number }> = [];
    const outAdj = new Map<string, Array<{ target: string; category: 'service_call' | 'database' | 'messaging' | 'external'; label: string; count: number }>>();

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
        label: e.label,
        count: e.count,
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

    // 5. Compute connected source services for each database node
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

    // 6. Compute Stable Architectural Echelon Tiers (0..5) across Full Graph
    const echelonMap = computeEchelonTiers(
      cyNodes.map((n) => ({ id: n.data.id as string, kind: (n.data as any).kind as string })),
      rawEdges
    );

    for (const node of cyNodes) {
      const id = node.data.id as string;
      const ech = echelonMap.get(id) ?? 2;
      (node.data as any).echelonTier = ech;
      const detail = detailMap.get(id);
      if (detail) {
        detail.tier = ech;
        detail.tierLabel =
          ech === 0
            ? 'Tier 0 (Ingress / Gateway & Frontends)'
            : ech === 1
            ? 'Tier 1 (First Echelon / Gateway-facing)'
            : ech === 2
            ? 'Tier 2 (Message Topics & Queues)'
            : ech === 3
            ? 'Tier 3 (Second Echelon / Internal Domain Services & Workers)'
            : 'Tier 4 (Databases & External Infrastructure)';
      }
    }

    return {
      allNodes: cyNodes,
      rawEdges,
      detailMap,
      echelonMap,
      outAdj,
      dbSourceServicesMap,
      topicPublishers,
      topicSubscribers,
      directPubSubChords,
      domainMap: domainNameMap,
      counts: {
        ingress: ingressCount,
        services: serviceCount,
        workers: workerCount,
        libraries: 0,
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

  // 2. Visible Graph Memo with Directional Transitive Contraction
  const { elements, visibleNodes, visibleEdges, hiddenNodeIdSet, stats, hiddenCount } = useMemo(() => {
    const hiddenNodeIdSet = new Set<string>(hiddenNodeIds);
    for (const node of rawGraph.allNodes) {
      const kind = (node.data as any).kind as EntityKind;
      if (hiddenTypes.has(kind)) {
        hiddenNodeIdSet.add(node.data.id as string);
      }
      if (hideSingleConnectionDbs && kind === 'Database') {
        const inboundCount = rawGraph.dbSourceServicesMap.get(node.data.id as string)?.size || 0;
        if (inboundCount <= 1) {
          hiddenNodeIdSet.add(node.data.id as string);
        }
      }
    }

    const visibleNodes = rawGraph.allNodes.filter((n) => !hiddenNodeIdSet.has(n.data.id as string));
    const visibleNodeIds = new Set<string>(visibleNodes.map((n) => n.data.id as string));

    // Direct edges between visible nodes
    const visibleEdges: cytoscape.ElementDefinition[] = [];
    const directVisibleEdgeKeys = new Set<string>();

    for (const e of rawGraph.rawEdges) {
      if (visibleNodeIds.has(e.source) && visibleNodeIds.has(e.target)) {
        visibleEdges.push({
          group: 'edges',
          data: {
            id: e.id,
            source: e.source,
            target: e.target,
            category: e.category,
            label: e.count > 1 ? `${e.label} (${e.count})` : e.label,
            count: e.count,
            isTransitive: 'false',
          },
        });
        directVisibleEdgeKeys.add(`${e.source}->${e.target}`);
      }
    }

    // Directional Transitive Contraction (u -> hidden... -> v)
    interface TransitivePath {
      curr: string;
      viaNames: string[];
      category: 'service_call' | 'database' | 'messaging' | 'external';
      label: string;
      count: number;
      depth: number;
    }

    const transitiveEdgesMap = new Map<
      string,
      {
        source: string;
        target: string;
        category: 'service_call' | 'database' | 'messaging' | 'external';
        label: string;
        count: number;
        viaNames: string[];
      }
    >();

    // A. Contract hidden Topic nodes: connecting subscribers to publishers
    for (const topicId of hiddenNodeIdSet) {
      if (!rawGraph.topicPublishers.has(topicId) && !rawGraph.topicSubscribers.has(topicId)) continue;
      const pubs = rawGraph.topicPublishers.get(topicId) || new Set<string>();
      const subs = rawGraph.topicSubscribers.get(topicId) || new Set<string>();
      const tDetail = rawGraph.detailMap.get(topicId);
      const tName = tDetail?.displayName || topicId;

      for (const p of pubs) {
        for (const s of subs) {
          if (p === s) continue;
          if (visibleNodeIds.has(s) && visibleNodeIds.has(p)) {
            // Direct edge takes precedence
            if (directVisibleEdgeKeys.has(`${s}->${p}`)) continue;

            const transKey = `${s}->${p}:messaging`;
            const existing = transitiveEdgesMap.get(transKey);
            if (existing) {
              existing.count += 1;
              if (!existing.viaNames.includes(tName)) {
                existing.viaNames.push(tName);
              }
            } else {
              transitiveEdgesMap.set(transKey, {
                source: s,
                target: p,
                category: 'messaging',
                label: 'SUBSCRIBES',
                count: 1,
                viaNames: [tName],
              });
            }
          }
        }
      }
    }

    // B. Direct service-to-service pub/sub chords (when no visible topic connects them)
    for (const chord of rawGraph.directPubSubChords.values()) {
      if (visibleNodeIds.has(chord.source) && visibleNodeIds.has(chord.target)) {
        // Check if there is an active visible topic connecting them
        const hasVisibleTopic = Array.from(rawGraph.topicPublishers.keys()).some(
          (tId) =>
            visibleNodeIds.has(tId) &&
            ((rawGraph.topicPublishers.get(tId)?.has(chord.target) &&
              rawGraph.topicSubscribers.get(tId)?.has(chord.source)) ||
              (rawGraph.topicPublishers.get(tId)?.has(chord.source) &&
                rawGraph.topicSubscribers.get(tId)?.has(chord.target)))
        );

        if (!hasVisibleTopic) {
          const transKey = `${chord.source}->${chord.target}:messaging`;
          if (!directVisibleEdgeKeys.has(`${chord.source}->${chord.target}`) && !transitiveEdgesMap.has(transKey)) {
            transitiveEdgesMap.set(transKey, {
              source: chord.source,
              target: chord.target,
              category: 'messaging',
              label: chord.label,
              count: chord.count,
              viaNames: ['event-bus'],
            });
          }
        }
      }
    }

    // C. General Hidden-Node BFS for multi-hop service/worker/infrastructure chains
    for (const u of visibleNodeIds) {
      const queue: TransitivePath[] = [];
      const visitedHidden = new Set<string>();

      const initialEdges = rawGraph.outAdj.get(u) || [];
      for (const edge of initialEdges) {
        if (hiddenNodeIdSet.has(edge.target)) {
          const targetDetail = rawGraph.detailMap.get(edge.target);
          const name = targetDetail?.displayName || edge.target;
          queue.push({
            curr: edge.target,
            viaNames: [name],
            category: edge.category,
            label: edge.label,
            count: edge.count,
            depth: 1,
          });
          visitedHidden.add(edge.target);
        }
      }

      let qIdx = 0;
      while (qIdx < queue.length) {
        const item = queue[qIdx++];
        if (item.depth > 6) continue;

        // If current hidden node is a topic, downstream nodes are its subscribers
        const isTopic = rawGraph.topicSubscribers.has(item.curr);
        const nextHops: Array<{ target: string; category: any; label: string; count: number }> = isTopic
          ? Array.from(rawGraph.topicSubscribers.get(item.curr) || []).map((sub) => ({
              target: sub,
              category: 'messaging' as const,
              label: 'SUBSCRIBES',
              count: 1,
            }))
          : (rawGraph.outAdj.get(item.curr) || []);

        for (const nextEdge of nextHops) {
          const v = nextEdge.target;
          if (v === u) continue; // Skip self loops

          if (visibleNodeIds.has(v)) {
            // Direct edge takes precedence
            if (directVisibleEdgeKeys.has(`${u}->${v}`)) {
              continue;
            }

            const transKey = `${u}->${v}`;
            const existing = transitiveEdgesMap.get(transKey);
            if (existing) {
              existing.count += nextEdge.count;
              for (const via of item.viaNames) {
                if (!existing.viaNames.includes(via)) {
                  existing.viaNames.push(via);
                }
              }
            } else {
              transitiveEdgesMap.set(transKey, {
                source: u,
                target: v,
                category: nextEdge.category,
                label: nextEdge.label || item.label,
                count: Math.max(item.count, nextEdge.count),
                viaNames: [...item.viaNames],
              });
            }
          } else if (hiddenNodeIdSet.has(v) && !visitedHidden.has(v)) {
            visitedHidden.add(v);
            const vDetail = rawGraph.detailMap.get(v);
            const name = vDetail?.displayName || v;
            queue.push({
              curr: v,
              viaNames: [...item.viaNames, name],
              category: nextEdge.category,
              label: nextEdge.label || item.label,
              count: item.count,
              depth: item.depth + 1,
            });
          }
        }
      }
    }

    // Append dashed transitive edges
    for (const [key, t] of transitiveEdgesMap.entries()) {
      const viaStr = t.viaNames.slice(0, 2).join(', ') + (t.viaNames.length > 2 ? '...' : '');
      const transLabel = `${t.label} (via ${viaStr})`;
      visibleEdges.push({
        group: 'edges',
        classes: 'transitive-edge',
        data: {
          id: `transitive:${key}`,
          source: t.source,
          target: t.target,
          category: t.category,
          label: transLabel,
          isTransitive: 'true',
          count: t.count,
        },
      });
    }

    let finalVisibleNodes = visibleNodes;
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
      finalVisibleNodes = visibleNodes.filter((n) => connectedNodeIds.has(n.data.id as string));
    }

    const visibleNodeIdSet = new Set(finalVisibleNodes.map((n) => n.data.id as string));
    const safeVisibleEdges = visibleEdges.filter((e) => {
      const s = (e.data as any)?.source as string;
      const t = (e.data as any)?.target as string;
      return s && t && visibleNodeIdSet.has(s) && visibleNodeIdSet.has(t);
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
      elements: [...finalVisibleNodes, ...safeVisibleEdges],
      visibleNodes: finalVisibleNodes,
      visibleEdges: safeVisibleEdges,
      hiddenNodeIdSet,
      hiddenCount: rawGraph.allNodes.length - finalVisibleNodes.length,
      stats: {
        total: finalVisibleNodes.length,
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
  }, [rawGraph, hiddenTypes, hiddenNodeIds, hideSingleConnectionDbs, hideIsolatedNodes]);

  const nodeDetailMap = rawGraph.detailMap;

  // Initialize Cytoscape Instance
  useEffect(() => {
    if (!containerRef.current) return;

    const cy = cytoscape({
      container: containerRef.current,
      elements: [],
      style: CYTOSCAPE_STYLES,
      boxSelectionEnabled: false,
      autoungrabify: false,
      minZoom: 0.15,
      maxZoom: 3.5,
      wheelSensitivity: wheelSensitivityRef.current,
    });

    // Node Selection & Highlight
    cy.on('tap', 'node', (evt) => {
      const node = evt.target;
      const nodeId = node.id();
      const detail = nodeDetailMap.get(nodeId);
      if (detail) {
        setSelectedNode(detail);
        const gNode: GraphNode = {
          id: detail.id,
          name: detail.name,
          displayName: detail.displayName,
          kind: detail.kind === 'Service' || detail.kind === 'Ingress' ? 'Service' : detail.kind,
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
        onSelectNode?.(gNode);
      }

      // Highlight neighborhood
      cy.elements().removeClass('highlighted dimmed');
      const neighborhood = node.neighborhood().add(node);
      cy.elements().not(neighborhood).addClass('dimmed');
      node.connectedEdges().addClass('highlighted');
    });

    // Background click -> Deselect
    cy.on('tap', (evt) => {
      if (evt.target === cy) {
        setSelectedNode(null);
        onSelectNode?.(null);
        cy.elements().removeClass('highlighted dimmed');
      }
    });

    // Double-click -> Drill down into Flow
    cy.on('dbltap', 'node', (evt) => {
      const node = evt.target;
      const nodeId = node.id();
      const detail = nodeDetailMap.get(nodeId);
      if (detail && (detail.kind === 'Service' || detail.kind === 'Ingress') && onFocusInFlow) {
        onFocusInFlow(detail.name);
      }
    });

    // Right-click / Context-tap -> Hide concrete node directly
    cy.on('cxttap', 'node', (evt) => {
      const nodeId = evt.target.id();
      hideNode(nodeId);
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
    const handleViewportSync = () => {
      setCurrentZoom(cy.zoom());
      setCyTransform({ pan: { ...cy.pan() }, zoom: cy.zoom() });
    };
    cy.on('zoom pan resize render', handleViewportSync);
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
    cy.style()
      .selector('node')
      .style('font-size', `${fontSizeRef.current}px`)
      .selector('edge')
      .style('font-size', `${Math.max(6, Math.round(fontSizeRef.current * 0.82))}px`)
      .update();
    applyEdgeLabelVisibility(cy, edgeLabelsOnHoverRef.current);

    return () => {
      cy.destroy();
      cyRef.current = null;
    };
  }, [nodeDetailMap, onFocusInFlow, hideNode]);

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
        cyRef.current?.elements().removeClass('highlighted dimmed');
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [selectedNode, hideNode]);

  // Asynchronously load elements and execute layout without freezing the UI
  useEffect(() => {
    const cy = cyRef.current;
    if (!cy) return;

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
      if (layoutChanged) {
        setCustomOrbitOrder(null);
        customOrbitOrderRef.current = null;
      }

      // Capture existing positions of visible nodes
      const savedPositions = new Map<string, cytoscape.Position>();
      cy.nodes().forEach((n) => {
        savedPositions.set(n.id(), { ...n.position() });
      });
      const hadExisting = savedPositions.size > 0;
      const isInitial = !hadExisting || layoutChanged || isForced;

      if (isInitial) {
        setIsPreparing(true);
        setPreparingStatus('Synthesizing domain clusters...');
        // Yield execution to the browser event loop so React renders the loading overlay immediately
        await new Promise((resolve) => setTimeout(resolve, 30));
        if (cancelled) return;
      }

      cy.elements().remove();
      cy.add(elements);
      applyEdgeLabelVisibility(cy, edgeLabelsOnHoverRef.current);

      if (hadExisting && !layoutChanged && layoutName === 'cose') {
        let hasNewNodes = false;
        cy.nodes().forEach((n) => {
          const pos = savedPositions.get(n.id());
          if (pos) {
            n.position(pos);
          } else {
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
      if (isInitial) {
        setPreparingStatus('Computing force-directed topology...');
        await new Promise((resolve) => setTimeout(resolve, 15));
        if (cancelled) return;
      }

      if (activeLayoutRef.current) {
        try {
          activeLayoutRef.current.stop();
        } catch {}
        activeLayoutRef.current = null;
      }

      if (layoutName === 'matrix') {
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

      const visibleNodesInput = cy.nodes().map((n) => {
        const id = n.id();
        const ech = (n.data('echelonTier') as number) ?? rawGraph.echelonMap.get(id) ?? 2;
        const detail = rawGraph.detailMap.get(id);
        return {
          id,
          echelonTier: ech,
          kind: n.data('kind') as string,
          name: detail?.name || id,
          displayName: detail?.displayName || id,
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
        layoutName === 'concentric-polar-force' ||
        layoutName === 'concentric-sectors'
      ) {
        setSwimlaneGuides([]);
        setIslandGuides([]);
        setHiveGuides([]);
        baseSwimlaneGuidesRef.current = [];
        baseIslandGuidesRef.current = [];
        baseHiveGuidesRef.current = [];

        const effectiveOrder = customOrbitOrderRef.current || undefined;
        let layoutResult: ConcentricLayoutResult;
        if (layoutName === 'concentric-sectors') {
          const nodeDomainMap = new Map<string, string>();
          const domainDetailsMap = new Map<string, { name: string; displayName: string }>();

          for (const [id, detail] of rawGraph.detailMap.entries()) {
            if (detail.kind === 'Service' || detail.kind === 'Ingress' || detail.kind === 'Worker') {
              nodeDomainMap.set(id, id);
              domainDetailsMap.set(id, { name: detail.name, displayName: detail.displayName });
            } else if (detail.kind === 'Database') {
              const callers = Array.from(rawGraph.dbSourceServicesMap.get(id) || []);
              if (callers.length === 1) {
                nodeDomainMap.set(id, callers[0]);
              } else if (callers.length > 1) {
                nodeDomainMap.set(id, '__shared__');
              }
            } else if (detail.kind === 'Topic') {
              const pubs = Array.from(rawGraph.topicPublishers.get(id) || []);
              const subs = Array.from(rawGraph.topicSubscribers.get(id) || []);
              const allParties = new Set([...pubs, ...subs]);
              if (allParties.size === 1) {
                nodeDomainMap.set(id, allParties.values().next().value!);
              } else if (allParties.size > 1) {
                nodeDomainMap.set(id, '__shared__');
              }
            }
          }

          layoutResult = computeConcentricSectorsLayout(
            visibleNodesInput,
            visibleEdgesInput,
            spacing,
            effectiveOrder,
            nodeDomainMap,
            domainDetailsMap
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
            layoutResult = computeConcentricEquispacedLayout(visibleNodesInput, visibleEdgesInput, spacing, effectiveOrder);
          } else if (layoutName === 'concentric-polar-force') {
            layoutResult = computeConcentricPolarForceLayout(visibleNodesInput, visibleEdgesInput, spacing, effectiveOrder);
          } else {
            layoutResult = computeConcentricLayout(visibleNodesInput, visibleEdgesInput, spacing, effectiveOrder);
          }
        }

        layoutConfig = {
          name: 'preset',
          positions: (node: any) => layoutResult.positions.get(node.id()) || { x: 0, y: 0 },
          fit: true,
          padding: 60,
          animate: false,
        };
        const effSpacing = spacing || 1.0;
        baseConcentricGuidesRef.current = layoutResult.guides.map((g) => ({
          ...g,
          radius: g.radius / effSpacing,
        }));
        setConcentricGuides(layoutResult.guides);
        setOrbitLegendItems(
          layoutResult.populatedOrbits.map((o) => ({
            levelIndex: o.levelIndex,
            shortLabel: o.shortLabel,
            title: o.title,
            count: o.nodeIds.length,
            radius: o.radius,
            nodeIds: o.nodeIds,
          }))
        );
      } else if (layoutName === 'swimlanes') {
        setConcentricGuides([]);
        setOrbitLegendItems([]);
        setIslandGuides([]);
        setHiveGuides([]);
        setSectorGuides([]);
        baseConcentricGuidesRef.current = [];
        baseIslandGuidesRef.current = [];
        baseHiveGuidesRef.current = [];
        baseSectorGuidesRef.current = [];
        const layoutResult = computeSwimlanesLayout(visibleNodesInput, visibleEdgesInput, spacing);
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
  }, [elements, layoutName, autoRelayoutOnFilter, relayoutTrigger]);

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

  // Concentric Orbit Reordering Handlers (drag-and-drop or ▲/▼)
  const applyConcentricOrder = useCallback(
    (newOrder: number[] | null) => {
      const cy = cyRef.current;
      if (!cy) return;
      if (!layoutName.startsWith('concentric')) return;

      setCustomOrbitOrder(newOrder);
      customOrbitOrderRef.current = newOrder;

      const spacing = spacingFactorRef.current || 1.0;
      const visibleNodesInput = cy.nodes().map((n) => {
        const id = n.id();
        const ech = (n.data('echelonTier') as number) ?? rawGraph.echelonMap.get(id) ?? 2;
        const detail = rawGraph.detailMap.get(id);
        return {
          id,
          echelonTier: ech,
          kind: n.data('kind') as string,
          name: detail?.name || id,
          displayName: detail?.displayName || id,
        };
      });

      const visibleEdgesInput = cy.edges().map((e) => ({
        source: e.data('source') as string,
        target: e.data('target') as string,
        category: e.data('category') as string,
        count: (e.data('count') as number) || 1,
      }));

      const effectiveOrder = newOrder || undefined;
      let layoutResult: ConcentricLayoutResult;
      if (layoutName === 'concentric-equispaced') {
        layoutResult = computeConcentricEquispacedLayout(visibleNodesInput, visibleEdgesInput, spacing, effectiveOrder);
        setSectorGuides([]);
        baseSectorGuidesRef.current = [];
      } else if (layoutName === 'concentric-polar-force') {
        layoutResult = computeConcentricPolarForceLayout(visibleNodesInput, visibleEdgesInput, spacing, effectiveOrder);
        setSectorGuides([]);
        baseSectorGuidesRef.current = [];
      } else if (layoutName === 'concentric-sectors') {
        const nodeDomainMap = new Map<string, string>();
        const domainDetailsMap = new Map<string, { displayName?: string; color?: string }>();
        for (const [nid, node] of rawGraph.detailMap.entries()) {
          if (node.domain) {
            nodeDomainMap.set(nid, node.domain);
          }
        }
        for (const [did, d] of rawGraph.domainMap.entries()) {
          domainDetailsMap.set(did, {
            displayName: d.displayName || d.name || did,
            color: d.color,
          });
        }
        layoutResult = computeConcentricSectorsLayout(
          visibleNodesInput,
          visibleEdgesInput,
          spacing,
          effectiveOrder,
          nodeDomainMap,
          domainDetailsMap
        );
        const effSpacing = spacing || 1.0;
        setSectorGuides(layoutResult.sectors || []);
        baseSectorGuidesRef.current = (layoutResult.sectors || []).map((s) => ({
          ...s,
          innerRadius: s.innerRadius / effSpacing,
          outerRadius: s.outerRadius / effSpacing,
        }));
      } else {
        layoutResult = computeConcentricLayout(visibleNodesInput, visibleEdgesInput, spacing, effectiveOrder);
        setSectorGuides([]);
        baseSectorGuidesRef.current = [];
      }

      const effSpacing = spacing || 1.0;
      baseConcentricGuidesRef.current = layoutResult.guides.map((g) => ({
        ...g,
        radius: g.radius / effSpacing,
      }));
      setConcentricGuides(layoutResult.guides);
      setOrbitLegendItems(
        layoutResult.populatedOrbits.map((o) => ({
          levelIndex: o.levelIndex,
          shortLabel: o.shortLabel,
          title: o.title,
          count: o.nodeIds.length,
          radius: o.radius,
          nodeIds: o.nodeIds,
        }))
      );

      // Smoothly animate nodes to new positions in Cytoscape
      cy.batch(() => {
        layoutResult.positions.forEach((pos, id) => {
          const node = cy.getElementById(id);
          if (node && !node.empty()) {
            node.animate(
              { position: pos },
              { duration: 350, easing: 'ease-in-out-cubic' }
            );
          }
        });
      });

      setTimeout(() => {
        if (cyRef.current) {
          recordBasePositions(cyRef.current);
          applyEdgeCurveMode(cyRef.current, edgeCurveModeRef.current, { x: 0, y: 0 }, curveFactorRef.current);
        }
      }, 370);
    },
    [layoutName, rawGraph, recordBasePositions]
  );

  const handleMoveOrbit = useCallback(
    (fromIndex: number, direction: -1 | 1) => {
      const toIndex = fromIndex + direction;
      if (toIndex < 0 || toIndex >= orbitLegendItems.length) return;
      const newItems = [...orbitLegendItems];
      const [moved] = newItems.splice(fromIndex, 1);
      newItems.splice(toIndex, 0, moved);
      const newOrder = newItems.map((it) => it.levelIndex);
      applyConcentricOrder(newOrder);
    },
    [orbitLegendItems, applyConcentricOrder]
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
      if (draggedOrbitIndex === null || draggedOrbitIndex === targetIdx) {
        setDraggedOrbitIndex(null);
        setDragOverIndex(null);
        return;
      }
      const newItems = [...orbitLegendItems];
      const [moved] = newItems.splice(draggedOrbitIndex, 1);
      newItems.splice(targetIdx, 0, moved);
      const newOrder = newItems.map((it) => it.levelIndex);
      setDraggedOrbitIndex(null);
      setDragOverIndex(null);
      applyConcentricOrder(newOrder);
    },
    [draggedOrbitIndex, orbitLegendItems, applyConcentricOrder]
  );

  const handleDragEnd = useCallback(() => {
    setDraggedOrbitIndex(null);
    setDragOverIndex(null);
  }, []);

  const handleResetOrbitOrder = useCallback(() => {
    applyConcentricOrder(null);
  }, [applyConcentricOrder]);

  // Wheel sensitivity change handler
  const handleWheelSensitivityChange = useCallback((val: number) => {
    const clamped = Math.max(0.5, Math.min(5.0, +val.toFixed(1)));
    setWheelSensitivity(clamped);
    try {
      localStorage.setItem('ce_wheel_sensitivity', clamped.toString());
    } catch { }
    if (cyRef.current) {
      const cy = cyRef.current as any;
      if (cy._private?.renderer) {
        cy._private.renderer.wheelSensitivity = clamped;
      }
      if (cy._private?.options) {
        cy._private.options.wheelSensitivity = clamped;
      }
    }
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
      localStorage.setItem('ce_domain_layout', newLayout);
    } catch { }
  }, []);

  const filteredHiddenItems = useMemo(() => {
    const q = hiddenSearchQuery.toLowerCase().trim();
    const items: Array<{
      id: string;
      label: string;
      kind: EntityKind;
      kindTag: string;
      badgeColor: string;
    }> = [];

    for (const id of hiddenNodeIds) {
      const detail = rawGraph.detailMap.get(id);
      const label = detail?.displayName || id;
      if (q && !label.toLowerCase().includes(q)) continue;
      const kind = detail?.kind || 'Service';
      const kindTag = detail?.displayTag || (kind === 'ExternalService' ? 'EXT' : kind.toUpperCase().slice(0, 3));
      const badgeColor = detail?.bgColor || '#6366f1';
      items.push({ id, label, kind, kindTag, badgeColor });
    }
    return items;
  }, [hiddenNodeIds, hiddenSearchQuery, rawGraph.detailMap]);

  return (
    <div className="domain-map-canvas-container">
      {/* Non-blocking loading overlay during heavy synthesis and layout */}
      {isPreparing && (
        <div className="view-loading-overlay">
          <div className="view-loading-card">
            <div className="view-loading-spinner" />
            <div className="view-loading-content">
              <span className="view-loading-title">Domain Microservice Map</span>
              <span className="view-loading-subtitle">{preparingStatus}</span>
            </div>
          </div>
        </div>
      )}

      {/* Docked Top Studio Bar */}
      <header className="domain-map-hud">
        {/* Left Brand Column (Rowspan 2) */}
        <div className="domain-hud-brand">
          <span className="domain-hud-label">Macro Architecture</span>
          <span className="domain-hud-title">Domain Microservice Map</span>
        </div>

        {/* Right Content Column: Two Stacked Rows of Controls */}
        <div className="domain-hud-content">
          {/* Row 1: Studio Canvas Controls */}
          <div className="domain-hud-row domain-hud-primary-row">
            <div className="domain-hud-controls-left">
            {/* Layout & Curve Selector */}
            <div className="domain-hud-layout-select">
              <select
                value={layoutName}
                onChange={(e) => handleLayoutChange(e.target.value as any)}
                title="Graph Layout"
                className="domain-layout-dropdown"
              >
                <option value="concentric">🎯 Concentric (Original)</option>
                <option value="concentric-equispaced">🪐 Concentric: Equispaced</option>
                <option value="concentric-polar-force">🪐 Concentric: Polar Force</option>
                <option value="concentric-sectors">🪐 Concentric: Domain Sectors</option>
                <option value="swimlanes">🏊 Swimlanes (Pipeline)</option>
                <option value="clusters">🏝️ Domain Islands (Bounded Contexts)</option>
                <option value="hive">🕸️ Hive Plot (Multi-Axis)</option>
                <option value="matrix">▦ Dependency Matrix</option>
                <option value="cose">⚡ Force (COSE)</option>
              </select>

              <select
                value={edgeCurveMode}
                onChange={(e) => handleEdgeCurveModeChange(e.target.value as any)}
                title="Line Style: Straight, Bezier curve, or Bypass Inner Orbits"
                className="domain-layout-dropdown"
              >
                <option value="bezier">〰️ Bezier Curves</option>
                <option value="straight">📏 Straight Lines</option>
                <option value="avoid-inner">🛡️ Bypass Inner Orbits</option>
              </select>
            </div>

            {/* Display & Sliders Tuning Popover */}
            <div className="domain-hud-popover-anchor" ref={displayMenuRef}>
              <button
                type="button"
                className={`domain-hud-btn domain-hud-display-btn ${isDisplayOpen ? 'is-active' : ''}`}
                onClick={() => setIsDisplayOpen((prev) => !prev)}
                title="Fine-tune display settings (Font size, Edge curvature, Air spacing, Wheel sensitivity)"
              >
                🎛️ Display <span style={{ fontSize: '9px', marginLeft: 2 }}>▾</span>
              </button>

              {isDisplayOpen && (
                <div className="domain-display-popover">
                  <div className="display-popover-header">
                    <span>Display & Tuning</span>
                    <button
                      type="button"
                      className="display-popover-close"
                      onClick={() => setIsDisplayOpen(false)}
                      title="Close"
                    >
                      ✕
                    </button>
                  </div>

                  <div className="display-popover-body">
                    {/* 1. Font Size Control */}
                    <div className="display-setting-row" title="Adjust graph label font size (7px - 24px)">
                      <div className="setting-label-row">
                        <span className="setting-icon">🔤</span>
                        <span className="setting-name">Font Size</span>
                        <span
                          className="domain-hud-value-badge"
                          onClick={() => handleFontSizeChange(10)}
                          title="Click to reset font size to 10px"
                        >
                          {fontSize}px
                        </span>
                      </div>
                      <div className="setting-slider-row">
                        <button
                          type="button"
                          className="domain-hud-step-btn"
                          onClick={() => handleFontSizeChange(fontSize - 1)}
                          title="Decrease font size (-1px)"
                        >
                          −
                        </button>
                        <input
                          type="range"
                          min="7"
                          max="24"
                          step="1"
                          value={fontSize}
                          onChange={(e) => handleFontSizeChange(parseInt(e.target.value, 10))}
                          className="domain-hud-slider"
                        />
                        <button
                          type="button"
                          className="domain-hud-step-btn"
                          onClick={() => handleFontSizeChange(fontSize + 1)}
                          title="Increase font size (+1px)"
                        >
                          +
                        </button>
                      </div>
                    </div>

                    {/* 2. Edge Curvature Control */}
                    {edgeCurveMode !== 'straight' && (
                      <div className="display-setting-row" title="Adjust edge curvature (-200px to +200px)">
                        <div className="setting-label-row">
                          <span className="setting-icon">〰️</span>
                          <span className="setting-name">Edge Curvature</span>
                          <span
                            className="domain-hud-value-badge"
                            onClick={() => handleCurveFactorChange(35)}
                            title="Click to reset curve to +35px"
                          >
                            {curveFactor > 0 ? `+${curveFactor}` : curveFactor}px
                          </span>
                        </div>
                        <div className="setting-slider-row">
                          <button
                            type="button"
                            className="domain-hud-step-btn"
                            onClick={() => handleCurveFactorChange(Math.max(-200, curveFactor - 10))}
                            title="Decrease curve (-10px)"
                          >
                            −
                          </button>
                          <input
                            type="range"
                            min="-200"
                            max="200"
                            step="5"
                            value={curveFactor}
                            onChange={(e) => handleCurveFactorChange(parseInt(e.target.value, 10))}
                            className="domain-hud-slider"
                          />
                          <button
                            type="button"
                            className="domain-hud-step-btn"
                            onClick={() => handleCurveFactorChange(Math.min(200, curveFactor + 10))}
                            title="Increase curve (+10px)"
                          >
                            +
                          </button>
                        </div>
                      </div>
                    )}

                    {/* 3. Node Spacing / Air Control */}
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

                    {/* 4. Mouse Wheel Zoom Sensitivity */}
                    <div className="display-setting-row" title="Adjust mouse wheel zoom sensitivity">
                      <div className="setting-label-row">
                        <span className="setting-icon">🖱️</span>
                        <span className="setting-name">Wheel Sensitivity</span>
                        <span
                          className="domain-hud-value-badge"
                          onClick={() => handleWheelSensitivityChange(2.5)}
                          title="Click to reset sensitivity to 2.5x"
                        >
                          {wheelSensitivity.toFixed(1)}x
                        </span>
                      </div>
                      <div className="setting-slider-row">
                        <button
                          type="button"
                          className="domain-hud-step-btn"
                          onClick={() => handleWheelSensitivityChange(Math.max(0.5, +(wheelSensitivity - 0.5).toFixed(1)))}
                          title="Decrease sensitivity"
                        >
                          −
                        </button>
                        <input
                          type="range"
                          min="0.5"
                          max="5.0"
                          step="0.1"
                          value={wheelSensitivity}
                          onChange={(e) => handleWheelSensitivityChange(parseFloat(e.target.value))}
                          className="domain-hud-slider"
                        />
                        <button
                          type="button"
                          className="domain-hud-step-btn"
                          onClick={() => handleWheelSensitivityChange(Math.min(5.0, +(wheelSensitivity + 0.5).toFixed(1)))}
                          title="Increase sensitivity"
                        >
                          +
                        </button>
                      </div>
                    </div>

                    {/* 5. Edge Labels Visibility Control */}
                    <div className="display-setting-row" title="Control when edge text labels are shown: always or only on hover">
                      <div className="setting-label-row">
                        <span className="setting-icon">🏷️</span>
                        <span className="setting-name">Edge Labels</span>
                        <span
                          className={`domain-hud-value-badge ${edgeLabelsOnHover ? 'is-active' : ''}`}
                          onClick={() => handleEdgeLabelsOnHoverChange(!edgeLabelsOnHover)}
                          title="Click to toggle: Always Show vs On Hover Only"
                          style={{ cursor: 'pointer' }}
                        >
                          {edgeLabelsOnHover ? 'On Hover' : 'Always'}
                        </span>
                      </div>
                      <div style={{ display: 'flex', gap: '4px', marginTop: '2px' }}>
                        <button
                          type="button"
                          className={`domain-hud-btn ${!edgeLabelsOnHover ? 'is-active' : ''}`}
                          onClick={() => handleEdgeLabelsOnHoverChange(false)}
                          title="Always show text labels on all edges"
                          style={{
                            flex: 1,
                            padding: '3px 8px',
                            fontSize: '10.5px',
                            background: !edgeLabelsOnHover ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255, 255, 255, 0.04)',
                            borderColor: !edgeLabelsOnHover ? '#38bdf8' : 'rgba(255, 255, 255, 0.1)',
                            color: !edgeLabelsOnHover ? '#ffffff' : '#94a3b8',
                          }}
                        >
                          Always
                        </button>
                        <button
                          type="button"
                          className={`domain-hud-btn ${edgeLabelsOnHover ? 'is-active' : ''}`}
                          onClick={() => handleEdgeLabelsOnHoverChange(true)}
                          title="Hide text labels by default, reveal on edge hover or selection"
                          style={{
                            flex: 1,
                            padding: '3px 8px',
                            fontSize: '10.5px',
                            background: edgeLabelsOnHover ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255, 255, 255, 0.04)',
                            borderColor: edgeLabelsOnHover ? '#38bdf8' : 'rgba(255, 255, 255, 0.1)',
                            color: edgeLabelsOnHover ? '#ffffff' : '#94a3b8',
                          }}
                        >
                          On Hover Only
                        </button>
                      </div>
                    </div>

                    <div className="display-popover-divider" />

                    {/* 5. Auto-update graph & Relayout */}
                    <div className="display-popover-actions">
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
                </div>
              )}
            </div>

            {/* Zoom Controls */}
            <div className="domain-hud-control-group" title="Zoom Controls">
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
            </div>
          </div>

          {/* Search */}
          <div className="domain-hud-search">
            <input
              type="text"
              className="domain-search-input"
              placeholder="Search domain or service..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
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
        </div>

        {/* Tier 2: Entity Filter Ribbon & Live Graph Telemetry */}
        <div className="domain-hud-row domain-hud-secondary-row">
          {/* Entity Type Toggle Filters Ribbon */}
          <div className="domain-hud-filter-group">
            {rawGraph.counts.ingress > 0 && (
              <button
                type="button"
                className={`hud-type-filter-btn ${hiddenTypes.has('Ingress') ? 'is-hidden' : 'is-active'}`}
                onClick={() => toggleTypeVisibility('Ingress')}
                title={hiddenTypes.has('Ingress') ? 'Show Ingress & Apps' : 'Hide Ingress & Apps'}
              >
                {hiddenTypes.has('Ingress') && <span className="filter-cross">✕</span>}
                🌐 Apps ({rawGraph.counts.ingress})
              </button>
            )}
            {rawGraph.counts.services > 0 && (
              <button
                type="button"
                className={`hud-type-filter-btn ${hiddenTypes.has('Service') ? 'is-hidden' : 'is-active'}`}
                onClick={() => toggleTypeVisibility('Service')}
                title={hiddenTypes.has('Service') ? 'Show Domain Services' : 'Hide Domain Services'}
              >
                {hiddenTypes.has('Service') && <span className="filter-cross">✕</span>}
                ⚙️ Services ({rawGraph.counts.services})
              </button>
            )}
            {rawGraph.counts.workers > 0 && (
              <button
                type="button"
                className={`hud-type-filter-btn ${hiddenTypes.has('Worker') ? 'is-hidden' : 'is-active'}`}
                onClick={() => toggleTypeVisibility('Worker')}
                title={hiddenTypes.has('Worker') ? 'Show Background Workers' : 'Hide Background Workers'}
              >
                {hiddenTypes.has('Worker') && <span className="filter-cross">✕</span>}
                ⚡ Workers ({rawGraph.counts.workers})
              </button>
            )}
            {rawGraph.counts.databases > 0 && (
              <button
                type="button"
                className={`hud-type-filter-btn ${hiddenTypes.has('Database') ? 'is-hidden' : 'is-active'}`}
                onClick={() => toggleTypeVisibility('Database')}
                title={hiddenTypes.has('Database') ? 'Show Databases' : 'Hide Databases'}
              >
                {hiddenTypes.has('Database') && <span className="filter-cross">✕</span>}
                🗄️ DBs ({rawGraph.counts.databases})
              </button>
            )}
            {rawGraph.counts.databases > 0 && (
              <button
                type="button"
                className={`hud-type-filter-btn ${hideSingleConnectionDbs ? 'is-active is-filter-on' : ''}`}
                onClick={() => {
                  forceRelayoutRef.current = true;
                  setHideSingleConnectionDbs((prev) => !prev);
                }}
                title={
                  hideSingleConnectionDbs
                    ? `Showing only shared databases (${rawGraph.counts.sharedDbs} shared). Click to show all databases.`
                    : `Hide databases with only 1 connection (${rawGraph.counts.singleConnDbs} dedicated 1:1 DBs). Keeps only shared databases.`
                }
                style={
                  hideSingleConnectionDbs
                    ? {
                        background: 'rgba(147, 51, 234, 0.28)',
                        borderColor: '#a855f7',
                        color: '#f3e8ff',
                        fontWeight: 600,
                      }
                    : undefined
                }
              >
                {hideSingleConnectionDbs ? '🔗 Shared DBs Only' : '🗄️ Hide 1:1 DBs'}
                <span style={{ opacity: 0.85, fontSize: '0.88em', marginLeft: 3 }}>
                  ({hideSingleConnectionDbs ? rawGraph.counts.sharedDbs : rawGraph.counts.singleConnDbs})
                </span>
              </button>
            )}
            <button
              type="button"
              className={`hud-type-filter-btn ${hideIsolatedNodes ? 'is-active is-filter-on' : ''}`}
              onClick={() => {
                forceRelayoutRef.current = true;
                setHideIsolatedNodes((prev) => !prev);
              }}
              title={
                hideIsolatedNodes
                  ? 'Showing only connected services/nodes. Click to show isolated services.'
                  : 'Hide isolated/disconnected services (leaves only nodes participating in service calls or shared resources)'
              }
              style={
                hideIsolatedNodes
                  ? {
                      background: 'rgba(234, 179, 8, 0.25)',
                      borderColor: '#eab308',
                      color: '#fef08a',
                      fontWeight: 600,
                    }
                  : undefined
              }
            >
              {hideIsolatedNodes ? '🏝️ Connected Only' : '🏝️ Hide Isolated'}
            </button>
            {rawGraph.counts.topics > 0 && (
              <button
                type="button"
                className={`hud-type-filter-btn ${hiddenTypes.has('Topic') ? 'is-hidden' : 'is-active'}`}
                onClick={() => toggleTypeVisibility('Topic')}
                title={hiddenTypes.has('Topic') ? 'Show Message Topics' : 'Hide Message Topics'}
              >
                {hiddenTypes.has('Topic') && <span className="filter-cross">✕</span>}
                📬 Topics ({rawGraph.counts.topics})
              </button>
            )}
            {rawGraph.counts.external > 0 && (
              <button
                type="button"
                className={`hud-type-filter-btn ${hiddenTypes.has('ExternalService') ? 'is-hidden' : 'is-active'}`}
                onClick={() => toggleTypeVisibility('ExternalService')}
                title={hiddenTypes.has('ExternalService') ? 'Show External Services' : 'Hide External Services'}
              >
                {hiddenTypes.has('ExternalService') && <span className="filter-cross">✕</span>}
                🔌 External ({rawGraph.counts.external})
              </button>
            )}

            {/* Reset Filters / Hidden Button */}
            {hiddenCount > 0 && (
              <button
                type="button"
                className="domain-hud-reset-hidden-btn"
                onClick={unhideAll}
                title="Reset all hidden nodes and type filters"
              >
                👁️ Reset All ({hiddenCount})
              </button>
            )}
          </div>

          {/* Stats Badges */}
          <div className="domain-hud-stats">
            <span
              className="hud-stat-pill"
              title="Service Calls (RPC / HTTP): Solid line = Direct call, Dashed line = Indirect call via hidden entities"
            >
              ⚡ {stats.serviceCalls} Calls
            </span>
            {stats.transitiveCalls > 0 && (
              <span
                className="hud-stat-pill"
                title={`${stats.transitiveCalls} indirect connections routing through hidden entities (shown as dashed lines)`}
                style={{ borderColor: 'rgba(192, 132, 252, 0.45)', color: '#c084fc' }}
              >
                ╌ {stats.transitiveCalls} via hidden
              </span>
            )}
            <span className="hud-stat-pill" title="Message Flows (Pub / Sub)">
              ✉️ {stats.messages} Msgs
            </span>
          </div>
        </div>
      </div>
    </header>

      {/* Main Graph Canvas Area */}
      <div className="domain-cytoscape-wrapper">

      {/* Matrix View or Cytoscape Canvas with SVG Overlays */}
      {layoutName === 'matrix' ? (
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
                kind: node.kind === 'Service' || node.kind === 'Ingress' ? 'Service' : node.kind,
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
                zIndex: 2,
              }}
            >
              <g transform={`translate(${cyTransform.pan.x}, ${cyTransform.pan.y}) scale(${cyTransform.zoom})`}>
                {/* 1. Standard Concentric Guides */}
                {layoutName.startsWith('concentric') &&
                  layoutName !== 'concentric-sectors' &&
                  concentricGuides.map((g, idx) => {
                    const strokeW = Math.max(1, 1.5 / cyTransform.zoom);
                    const dashPattern = `${8 / cyTransform.zoom} ${6 / cyTransform.zoom}`;
                    const guideFontSize = Math.max(9, 11 / cyTransform.zoom);
                    const shortLabel = g.shortLabel || (g.label.includes(':') ? g.label.split(':')[0] : g.label);
                    const labelText = `${shortLabel} (${g.count})`;
                    const textWidth = labelText.length * guideFontSize * 0.65;
                    const hPad = Math.max(14, 18 / cyTransform.zoom);
                    const badgeWidth = Math.max(textWidth + hPad, 75 / cyTransform.zoom);
                    const badgeHeight = Math.max(18, 22 / cyTransform.zoom);
                    const badgeY = -g.radius - badgeHeight - 6 / cyTransform.zoom;

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
                          rx={4 / cyTransform.zoom}
                          fill="rgba(15, 23, 42, 0.88)"
                          stroke="rgba(56, 189, 248, 0.45)"
                          strokeWidth={1 / cyTransform.zoom}
                        />
                        <text
                          x={0}
                          y={badgeY + badgeHeight / 2}
                          fill="#38bdf8"
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

                {/* 1.1 Concentric Domain Sector Guides */}
                {layoutName === 'concentric-sectors' && (
                  <g className="concentric-sectors-overlay">
                    {/* Subtle concentric orbit rings */}
                    {concentricGuides.map((cg, cIdx) => (
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
                    ))}

                    {/* Sector wedges and headers */}
                    {sectorGuides.map((g) => {
                      const strokeW = Math.max(1, 1.5 / cyTransform.zoom);
                      const dashPattern = `${6 / cyTransform.zoom} ${4 / cyTransform.zoom}`;
                      const guideFontSize = Math.max(9.5, 12 / cyTransform.zoom);
                      const labelText = `${g.displayName} (${g.count})`;
                      const textWidth = labelText.length * guideFontSize * 0.65;
                      const hPad = Math.max(16, 20 / cyTransform.zoom);
                      const badgeWidth = Math.max(textWidth + hPad, 85 / cyTransform.zoom);
                      const badgeHeight = Math.max(20, 24 / cyTransform.zoom);

                      const wedgePath = describeAnnularSector(
                        0,
                        0,
                        g.innerRadius,
                        g.outerRadius,
                        g.startAngle,
                        g.endAngle
                      );

                      // Place badge just outside outer radius along centerAngle
                      const badgeDist = g.outerRadius + Math.max(22, 28 / cyTransform.zoom);
                      const badgeX = Math.round(badgeDist * Math.cos(g.centerAngle));
                      const badgeY = Math.round(badgeDist * Math.sin(g.centerAngle));

                      return (
                        <g key={g.id}>
                          {/* Annular sector filled wedge */}
                          <path
                            d={wedgePath}
                            fill={g.color.startsWith('#') ? `${g.color}18` : 'rgba(56, 189, 248, 0.08)'}
                            stroke={g.color.startsWith('#') ? `${g.color}50` : 'rgba(56, 189, 248, 0.35)'}
                            strokeWidth={strokeW}
                            strokeDasharray={g.isShared ? dashPattern : undefined}
                          />

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
                    const strokeW = Math.max(1, 1.5 / cyTransform.zoom);
                    const dashPattern = `${6 / cyTransform.zoom} ${4 / cyTransform.zoom}`;
                    const headerHeight = Math.max(22, 28 / cyTransform.zoom);
                    const fontSize = Math.max(9.5, 11.5 / cyTransform.zoom);

                    return (
                      <g key={g.id}>
                        <rect
                          x={g.x}
                          y={g.y}
                          width={g.width}
                          height={g.height}
                          rx={8 / cyTransform.zoom}
                          fill="rgba(15, 23, 42, 0.45)"
                          stroke={`${g.color}35`}
                          strokeWidth={strokeW}
                          strokeDasharray={dashPattern}
                        />
                        <rect
                          x={g.x + 8 / cyTransform.zoom}
                          y={g.y + 8 / cyTransform.zoom}
                          width={g.width - 16 / cyTransform.zoom}
                          height={headerHeight}
                          rx={4 / cyTransform.zoom}
                          fill="rgba(15, 23, 42, 0.92)"
                          stroke={`${g.color}70`}
                          strokeWidth={1 / cyTransform.zoom}
                        />
                        <text
                          x={g.x + g.width / 2}
                          y={g.y + 8 / cyTransform.zoom + headerHeight / 2}
                          fill={g.color}
                          fontSize={`${fontSize}px`}
                          fontWeight="700"
                          textAnchor="middle"
                          dominantBaseline="central"
                        >
                          {g.icon} {g.title} ({g.count})
                        </text>
                      </g>
                    );
                  })}

                {/* 3. Domain Island Guides */}
                {layoutName === 'clusters' &&
                  islandGuides.map((g) => {
                    const strokeW = Math.max(1, 2 / cyTransform.zoom);
                    const badgeW = Math.min(g.width - 16 / cyTransform.zoom, Math.max(150, 220 / cyTransform.zoom));
                    const badgeH = Math.max(20, 26 / cyTransform.zoom);
                    const fontSize = Math.max(9.5, 11 / cyTransform.zoom);

                    return (
                      <g key={g.id}>
                        <rect
                          x={g.x}
                          y={g.y}
                          width={g.width}
                          height={g.height}
                          rx={16 / cyTransform.zoom}
                          fill={g.isSharedCore ? 'rgba(88, 28, 135, 0.14)' : 'rgba(30, 41, 59, 0.35)'}
                          stroke={`${g.color}50`}
                          strokeWidth={strokeW}
                          strokeDasharray={g.isSharedCore ? `${8 / cyTransform.zoom} ${4 / cyTransform.zoom}` : undefined}
                        />
                        <rect
                          x={g.x + 10 / cyTransform.zoom}
                          y={g.y + 8 / cyTransform.zoom}
                          width={badgeW}
                          height={badgeH}
                          rx={4 / cyTransform.zoom}
                          fill="rgba(15, 23, 42, 0.92)"
                          stroke={`${g.color}75`}
                          strokeWidth={1 / cyTransform.zoom}
                        />
                        <text
                          x={g.x + 10 / cyTransform.zoom + badgeW / 2}
                          y={g.y + 8 / cyTransform.zoom + badgeH / 2}
                          fill={g.color}
                          fontSize={`${fontSize}px`}
                          fontWeight="700"
                          textAnchor="middle"
                          dominantBaseline="central"
                        >
                          {g.title} ({g.count})
                        </text>
                      </g>
                    );
                  })}

                {/* 4. Hive Plot Guides */}
                {layoutName === 'hive' &&
                  hiveGuides.map((g) => {
                    const strokeW = Math.max(1, 1.8 / cyTransform.zoom);
                    const dashPattern = `${6 / cyTransform.zoom} ${4 / cyTransform.zoom}`;
                    const tipX = Math.round(g.length * Math.cos(g.angle));
                    const tipY = Math.round(g.length * Math.sin(g.angle));
                    const badgeW = Math.max(160, 210 / cyTransform.zoom);
                    const badgeH = Math.max(22, 26 / cyTransform.zoom);
                    const guideFontSize = Math.max(9, 11 / cyTransform.zoom);

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

      {/* Floating Collapsible Hidden Entities Panel (Clings to Left) */}
      {hiddenNodeIds.size > 0 && (
        <aside className={`domain-hidden-panel ${isHiddenPanelCollapsed ? 'is-collapsed' : ''}`}>
          {isHiddenPanelCollapsed ? (
            <div
              className="domain-hidden-collapsed-badge"
              onClick={() => setIsHiddenPanelCollapsed(false)}
              title={`Click to expand ${hiddenNodeIds.size} hidden entities`}
            >
              <span className="badge-icon">👁️</span>
              <span className="badge-count">{hiddenNodeIds.size}</span>
              <span className="badge-arrow">▶</span>
            </div>
          ) : (
            <div className="domain-hidden-panel-content">
              <div className="domain-hidden-panel-header">
                <div className="hidden-panel-title">
                  <span className="hidden-panel-icon">👁️</span>
                  <span>Hidden ({hiddenNodeIds.size})</span>
                </div>
                <div className="hidden-panel-header-actions">
                  <button
                    type="button"
                    className="hidden-panel-restore-all-btn"
                    onClick={restoreAllHiddenNodes}
                    title="Restore all hidden entities to graph"
                  >
                    ⟲ Restore All
                  </button>
                  <button
                    type="button"
                    className="hidden-panel-collapse-btn"
                    onClick={() => setIsHiddenPanelCollapsed(true)}
                    title="Collapse hidden panel"
                  >
                    ◀
                  </button>
                </div>
              </div>

              {hiddenNodeIds.size > 4 && (
                <div className="domain-hidden-search-wrap">
                  <input
                    type="text"
                    className="domain-hidden-search-input"
                    placeholder="Filter hidden..."
                    value={hiddenSearchQuery}
                    onChange={(e) => setHiddenSearchQuery(e.target.value)}
                  />
                  {hiddenSearchQuery && (
                    <button
                      type="button"
                      className="domain-hidden-search-clear"
                      onClick={() => setHiddenSearchQuery('')}
                      title="Clear filter"
                    >
                      ✕
                    </button>
                  )}
                </div>
              )}

              <div className="domain-hidden-list">
                {filteredHiddenItems.map((item) => (
                  <div
                    key={item.id}
                    className="domain-hidden-list-item"
                    title={`${item.label} (${item.kind}). Click ✕ to restore.`}
                  >
                    <span
                      className="hidden-kind-tag"
                      style={{ backgroundColor: item.badgeColor }}
                    >
                      {item.kindTag}
                    </span>
                    <span className="hidden-item-name">{item.label}</span>
                    <button
                      type="button"
                      className="hidden-item-unhide-btn"
                      onClick={() => unhideNode(item.id)}
                      title={`Restore ${item.label}`}
                    >
                      ✕
                    </button>
                  </div>
                ))}
                {filteredHiddenItems.length === 0 && (
                  <div className="domain-hidden-empty">No matching hidden items</div>
                )}
              </div>
            </div>
          )}
        </aside>
      )}

      {/* Floating Concentric Orbit Legend HUD (Clings to Bottom-Left) */}
      {layoutName.startsWith('concentric') && orbitLegendItems.length > 0 && (
        <aside className={`domain-orbit-legend ${!isOrbitLegendOpen ? 'is-collapsed' : ''}`}>
          <div
            className="domain-orbit-legend-header"
            onClick={() => setIsOrbitLegendOpen((prev) => !prev)}
            title={isOrbitLegendOpen ? 'Collapse Orbit Legend' : 'Expand Orbit Legend'}
          >
            <div className="domain-orbit-legend-title">
              <span className="legend-icon">🪐</span>
              <span>{isOrbitLegendOpen ? 'Orbit Legend' : `Orbits (${orbitLegendItems.length})`}</span>
            </div>
            <div className="domain-orbit-legend-header-actions">
              {isOrbitLegendOpen && customOrbitOrder !== null && (
                <button
                  type="button"
                  className="orbit-legend-reset-btn"
                  onClick={(e) => {
                    e.stopPropagation();
                    handleResetOrbitOrder();
                  }}
                  title="Reset to auto-calculated layout"
                >
                  ↺ Auto
                </button>
              )}
              <span className="legend-toggle">{isOrbitLegendOpen ? '—' : '▲'}</span>
            </div>
          </div>

          {isOrbitLegendOpen && (
            <>
              <div
                className="orbit-legend-hint"
                title="Orbits are ordered from Center (top) to Outer Periphery (bottom). Drag or use ▲/▼ to change orbit radii."
              >
                <span>Inner (Center)</span>
                <span>↕</span>
                <span>Outer (Periphery)</span>
              </div>
              <div className="domain-orbit-legend-body">
                {orbitLegendItems.map((item, idx) => (
                  <div
                    key={item.levelIndex}
                    className={`domain-orbit-legend-item ${draggedOrbitIndex === idx ? 'is-dragging' : ''} ${dragOverIndex === idx ? 'is-drag-over' : ''}`}
                    draggable={true}
                    onDragStart={(e) => handleDragStart(e, idx)}
                    onDragOver={(e) => handleDragOver(e, idx)}
                    onDrop={(e) => handleDrop(e, idx)}
                    onDragEnd={handleDragEnd}
                    onMouseEnter={() => highlightOrbitNodes(item.nodeIds)}
                    onMouseLeave={clearOrbitHighlight}
                    title={`${item.shortLabel}: ${item.title} (${item.count} nodes). Drag or use ▲/▼ to change orbit order.`}
                  >
                    <span className="orbit-drag-handle" title="Drag to reorder orbit">⠿</span>
                    <span className="orbit-legend-pill">{item.shortLabel}</span>
                    <span className="orbit-legend-title">{item.title}</span>
                    <span className="orbit-legend-count">{item.count}</span>
                    <div className="orbit-move-actions">
                      <button
                        type="button"
                        className="orbit-move-btn"
                        disabled={idx === 0}
                        onClick={(e) => {
                          e.stopPropagation();
                          handleMoveOrbit(idx, -1);
                        }}
                        title="Move toward center (Inner orbit)"
                      >
                        ▲
                      </button>
                      <button
                        type="button"
                        className="orbit-move-btn"
                        disabled={idx === orbitLegendItems.length - 1}
                        onClick={(e) => {
                          e.stopPropagation();
                          handleMoveOrbit(idx, 1);
                        }}
                        title="Move toward periphery (Outer orbit)"
                      >
                        ▼
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </>
          )}
        </aside>
      )}

      {/* Floating Node Inspector Panel (Clings to Top-Right) */}
      {selectedNode && (
        <aside className={`domain-inspector-panel ${isInspectorCollapsed ? 'is-collapsed' : ''}`}>
          {isInspectorCollapsed ? (
            <div
              className="domain-inspector-collapsed-badge"
              onClick={() => setIsInspectorCollapsed(false)}
              title={`Click to expand details for ${selectedNode.displayName}`}
            >
              <div className="inspector-badge" style={{ backgroundColor: selectedNode.bgColor }}>
                {selectedNode.displayTag}
              </div>
              <span className="collapsed-title">{selectedNode.displayName}</span>
              <button
                type="button"
                className="collapsed-expand-btn"
                onClick={(e) => {
                  e.stopPropagation();
                  setIsInspectorCollapsed(false);
                }}
                title="Expand inspector"
              >
                ▼
              </button>
              <button
                type="button"
                className="inspector-close-btn"
                onClick={(e) => {
                  e.stopPropagation();
                  setSelectedNode(null);
                  cyRef.current?.elements().removeClass('highlighted dimmed');
                }}
                title="Deselect"
              >
                ✕
              </button>
            </div>
          ) : (
            <>
              <div className="domain-inspector-header">
                <div className="inspector-badge" style={{ backgroundColor: selectedNode.bgColor }}>
                  {selectedNode.displayTag}
                </div>
                <div className="inspector-title-group">
                  <h4 className="inspector-title" title={selectedNode.displayName}>
                    {selectedNode.displayName}
                  </h4>
                  {selectedNode.tierLabel && (
                    <div style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      fontSize: '11px',
                      fontWeight: 600,
                      color: selectedNode.tier === 0 ? '#38bdf8' : selectedNode.tier === 1 ? '#4ade80' : selectedNode.tier === 2 ? '#fbbf24' : '#c084fc',
                      background: 'rgba(255, 255, 255, 0.06)',
                      padding: '2px 6px',
                      borderRadius: '4px',
                      marginTop: '2px',
                      marginBottom: '2px',
                      border: '1px solid rgba(255, 255, 255, 0.12)'
                    }}>
                      🎯 {selectedNode.tierLabel}
                    </div>
                  )}
                  {selectedNode.framework && (
                    <span className="inspector-subtitle">{selectedNode.framework}</span>
                  )}
                  {selectedNode.gitBranch && (
                    <span className="inspector-subtitle" style={{ opacity: 0.85, fontSize: '0.82em' }}>🌿 {selectedNode.gitBranch}</span>
                  )}
                </div>
                <button
                  type="button"
                  className="inspector-collapse-btn"
                  onClick={() => setIsInspectorCollapsed(true)}
                  title="Minimize inspector to badge"
                >
                  —
                </button>
                <button
                  className="inspector-header-hide-btn"
                  onClick={() => hideNode(selectedNode.id)}
                  title="Hide this node (Transitive connections will bypass it) [Shortcut: H, Del, or Right-Click]"
                >
                  👁️ Hide
                </button>
                <button
                  className="inspector-close-btn"
                  onClick={() => {
                    setSelectedNode(null);
                    cyRef.current?.elements().removeClass('highlighted dimmed');
                  }}
                  title="Close inspector"
                >
                  ✕
                </button>
              </div>

              <div className="inspector-body">
                {/* Subprojects breakdown if clustered */}
                {selectedNode.projects && selectedNode.projects.length > 1 && (
                  <div className="inspector-section">
                    <label className="inspector-section-label">
                      Clustered Projects ({selectedNode.projects.length})
                    </label>
                    <div className="inspector-subprojects-list">
                      {selectedNode.projects.map((p) => (
                        <div
                          key={p.id}
                          className="inspector-subproject-item"
                          onClick={() => p.filePath && onOpenFile?.(p.filePath, 1)}
                          title={p.filePath || p.name}
                        >
                          <span className="subproject-dot">•</span>
                          <span className="subproject-name">{p.name}</span>
                          {p.isLibrary && <span className="subproject-lib-tag">lib</span>}
                          {p.gitBranch && <span className="subproject-lib-tag" style={{ background: 'rgba(56, 189, 248, 0.2)', color: '#38bdf8' }}>🌿 {p.gitBranch}</span>}
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Metrics Grid */}
                <div className="inspector-metrics-grid">
                  {(selectedNode.kind === 'Service' || selectedNode.kind === 'Ingress') && (
                    <>
                      <div className="inspector-metric-card" title="Inbound calls from other services">
                        <span className="metric-val">{selectedNode.inboundCallsCount}</span>
                        <span className="metric-lbl">Inbound Calls</span>
                      </div>
                      <div className="inspector-metric-card" title="Outbound calls to downstream services">
                        <span className="metric-val">{selectedNode.outboundCallsCount}</span>
                        <span className="metric-lbl">Outbound Calls</span>
                      </div>
                      {selectedNode.dbCount > 0 && (
                        <div className="inspector-metric-card" title="Databases used directly">
                          <span className="metric-val">{selectedNode.dbCount}</span>
                          <span className="metric-lbl">Databases</span>
                        </div>
                      )}
                      {selectedNode.messagingCount > 0 && (
                        <div className="inspector-metric-card" title="Topics published or subscribed">
                          <span className="metric-val">{selectedNode.messagingCount}</span>
                          <span className="metric-lbl">Topics</span>
                        </div>
                      )}
                    </>
                  )}
                </div>

                {/* Action buttons */}
                <div className="inspector-actions">
                  {(selectedNode.kind === 'Service' || selectedNode.kind === 'Ingress') && onFocusInFlow && (
                    <button
                      className="inspector-action-btn primary"
                      onClick={() => onFocusInFlow(selectedNode.name)}
                      title="Drill down to Project Flow view"
                    >
                      Explore in Flow ➔
                    </button>
                  )}
                  {selectedNode.primaryFilePath && onOpenFile && (
                    <button
                      className="inspector-action-btn secondary"
                      onClick={() => onOpenFile(selectedNode.primaryFilePath!, 1)}
                      title="Open source file in editor"
                    >
                      Open Source
                    </button>
                  )}
                  <button
                    className="inspector-action-btn hide-node-btn"
                    onClick={() => hideNode(selectedNode.id)}
                    title="Hide this node from map (Shortcut: H, Del, or Right-Click)"
                  >
                    Hide Node
                  </button>
                </div>
              </div>
            </>
          )}
        </aside>
      )}
      </div>
    </div>
  );
};
