import { MarkerType } from '@xyflow/react';
import { GraphEdge } from '../../../../proto/types';

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

const VISUALS_BY_CATEGORY: Record<EdgeCategory, EdgeVisuals> = {
  database: {
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
  },
  messaging: {
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
  },
  service_call: {
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
  },
  library: {
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
  },
};

/**
 * Normalizes an edge to its authoritative architectural category.
 * The graph is normalized on the backend, so category is read directly from edge.category or edge.kind.
 */
export const getEdgeCategory = (edge?: GraphEdge): EdgeCategory => {
  const cat = (edge?.category || edge?.properties?.category || edge?.properties?.dependency_type)?.toLowerCase();
  if (cat === 'database' || cat === 'messaging' || cat === 'service_call' || cat === 'library') {
    return cat as EdgeCategory;
  }

  const kind = edge?.kind?.toUpperCase();
  if (kind === 'USES_DB') return 'database';
  if (kind === 'TRIGGERS' || kind === 'PUBLISHES' || kind === 'PUBLISHES_TO' || kind === 'SUBSCRIBES_TO') return 'messaging';
  if (kind === 'SERVICE_CALL' || kind === 'CALLS_ENDPOINT') return 'service_call';

  return 'library';
};

/**
 * Returns visual styling for an edge or category.
 */
export const getEdgeVisuals = (edgeOrCategory?: GraphEdge | EdgeCategory): EdgeVisuals => {
  const category: EdgeCategory = typeof edgeOrCategory === 'string'
    ? (VISUALS_BY_CATEGORY[edgeOrCategory] ? edgeOrCategory : 'library')
    : getEdgeCategory(edgeOrCategory);

  return VISUALS_BY_CATEGORY[category] || VISUALS_BY_CATEGORY.library;
};
