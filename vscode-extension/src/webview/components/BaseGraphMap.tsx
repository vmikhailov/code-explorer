import React from 'react';
import { GraphData, GraphNode } from '../../../../proto/types';
import { DomainArchitectureView, DomainLayoutName } from './DomainArchitectureView';
import { EntityKind, NodeContentMode, NodeSizingMode } from '../domain/types';

export interface BaseGraphMapProps {
  graph: GraphData | null;
  serverHttpUrl?: string;
  hudLabel?: string;
  hudTitle?: string;
  headerSlot?: React.ReactNode;
  customPresetsSlot?: React.ReactNode;
  projectMapPresets?: {
    cyclesCount: number;
    cyclesOnly: boolean;
    hideUtilities: boolean;
    hideResources: boolean;
  };
  onToggleProjectMapPreset?: (preset: 'cyclesOnly' | 'hideUtilities' | 'hideResources') => void;
  defaultLayoutName?: DomainLayoutName;
  defaultNodeSizingMode?: NodeSizingMode;
  defaultNodeContentMode?: NodeContentMode;
  defaultHiddenTypes?: EntityKind[];
  storagePrefix?: string;
  onFocusInFlow?: (projectName: string) => void;
  onOpenProjectMap?: (projectName: string) => void;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
  onSelectNode?: (node: GraphNode | null) => void;
  onSetDomainOverride?: (serviceName: string, domain: string, context?: string, reset?: boolean) => void;
  availableDomains?: string[];
  onRefresh?: () => void;
}

/**
 * Universal Base Map (`<BaseGraphMap />`):
 * Shared Cytoscape + SVG Overlay canvas, 12 layout algorithms (including Concentric Barycentric),
 * semantic shapes (`ellipse` for code/modules, `round-rectangle` for data stores & resources),
 * `🎛️ Display` options (`Node Content`, `Node Size` with `sqrt-weight`, `Line Style`, etc.),
 * `Orbits` legend, `Visibility` panel with grouping & transitive contraction, and `DeclarativeInspector`.
 */
export const BaseGraphMap: React.FC<BaseGraphMapProps> = (props) => {
  return <DomainArchitectureView mode="map" {...props} />;
};
