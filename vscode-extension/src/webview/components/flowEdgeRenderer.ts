import { MarkerType } from '@xyflow/react';
import { GraphEdge, GraphNode } from '../../../../proto/types';

export interface EdgeVisuals {
  stroke: string;
  strokeDasharray?: string;
  strokeWidth: number;
  animated: boolean;
  markerColor: string;
  markerType: MarkerType;
  markerWidth: number;
  markerHeight: number;
  markerStrokeWidth?: number;
  label?: string;
  className?: string;
}

export type EdgeCategory = 'library' | 'service_call' | 'database' | 'messaging';

export const getEdgeCategory = (
  edge?: GraphEdge,
  targetNode?: GraphNode,
  sourceNode?: GraphNode
): EdgeCategory => {
  if (targetNode?.kind === 'ExternalReferences' || sourceNode?.kind === 'ExternalReferences') {
    return 'library';
  }

  // 1. Authoritative Backend Protocol Category
  if (edge?.category) {
    const cat = edge.category.toLowerCase();
    if (cat === 'service_call' || cat === 'database' || cat === 'messaging' || cat === 'library') {
      return cat as EdgeCategory;
    }
  }

  const depType = (edge?.properties?.dependency_type || '').toLowerCase();
  const kind = (edge?.kind || '').toUpperCase();
  const targetRole = targetNode?.properties?.role;
  const targetKind = targetNode?.kind;
  const sourceRole = sourceNode?.properties?.role;
  const sourceKind = sourceNode?.kind;
  const targetLayer = (targetNode?.properties?.layer || targetNode?.properties?.layerId || '').toLowerCase();
  const targetProjType = (targetNode?.properties?.project_type || '').toLowerCase();

  // 2. Explicit dependency_type or kind
  if (
    depType === 'database' ||
    kind === 'USES_DB' ||
    targetKind === 'Database' ||
    targetRole === 'database' ||
    sourceKind === 'Database' ||
    sourceRole === 'database'
  ) {
    return 'database';
  }

  if (
    depType === 'messaging' ||
    kind === 'TRIGGERS' ||
    kind === 'PUBLISHES' ||
    kind === 'PUBLISHES_TO' ||
    kind === 'SUBSCRIBES_TO' ||
    kind === 'SUBSCRIBED_BY' ||
    targetKind === 'Topic' ||
    targetRole === 'topic' ||
    sourceKind === 'Topic' ||
    sourceRole === 'topic'
  ) {
    return 'messaging';
  }

  if (depType === 'service_call' || kind === 'SERVICE_CALL' || kind === 'CALLS_ENDPOINT') {
    return 'service_call';
  }

  if (depType === 'library' || kind === 'LIBRARY') {
    return 'library';
  }

  // 3. Inferred Fallbacks
  const isLibraryTarget =
    targetNode?.properties?.is_library === 'true' ||
    targetLayer === 'layer_foundation' ||
    targetProjType === 'library';

  if (isLibraryTarget) {
    return 'library';
  }

  const isServiceTarget = targetLayer === 'layer_ingress' || targetLayer === 'layer_components' || targetLayer === 'layer_egress';
  if (isServiceTarget && targetProjType !== 'library') {
    return 'service_call';
  }

  return 'library';
};

export const getEdgeVisuals = (edge?: GraphEdge, targetNode?: GraphNode, sourceNode?: GraphNode): EdgeVisuals => {
  const category = getEdgeCategory(edge, targetNode, sourceNode);

  switch (category) {
    case 'database':
      return {
        stroke: '#c084fc',
        strokeDasharray: undefined,
        strokeWidth: 1.2,
        animated: false,
        markerColor: '#c084fc',
        markerType: MarkerType.ArrowClosed,
        markerWidth: 12,
        markerHeight: 12,
        markerStrokeWidth: 1.2,
        label: 'Database',
        className: 'edge-database',
      };
    case 'messaging':
      return {
        stroke: '#fbbf24',
        strokeDasharray: undefined,
        strokeWidth: 1.2,
        animated: false,
        markerColor: '#fbbf24',
        markerType: MarkerType.ArrowClosed,
        markerWidth: 12,
        markerHeight: 12,
        markerStrokeWidth: 1.2,
        label: 'Message',
        className: 'edge-messaging',
      };
    case 'service_call':
      return {
        stroke: '#38bdf8',
        strokeDasharray: undefined,
        strokeWidth: 1.2,
        animated: false,
        markerColor: '#38bdf8',
        markerType: MarkerType.ArrowClosed,
        markerWidth: 12,
        markerHeight: 12,
        markerStrokeWidth: 1.2,
        label: 'Service Call',
        className: 'edge-service-call',
      };
    case 'library':
    default:
      return {
        stroke: '#34d399',
        strokeDasharray: undefined,
        strokeWidth: 1.2,
        animated: false,
        markerColor: '#34d399',
        markerType: MarkerType.ArrowClosed,
        markerWidth: 12,
        markerHeight: 12,
        markerStrokeWidth: 1.2,
        label: 'Library',
        className: 'edge-library',
      };
  }
};
