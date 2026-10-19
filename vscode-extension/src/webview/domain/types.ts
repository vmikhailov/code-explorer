import { GraphData, GraphNode } from '../../../../proto/types';

export type PresentationMode = 'graph' | 'grid' | 'matrix';

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

export type EntityKind =
  | 'App'
  | 'Service'
  | 'Ingress'
  | 'Worker'
  | 'CliTool'
  | 'Library'
  | 'TestSuite'
  | 'Database'
  | 'Topic'
  | 'ExternalService'
  | 'Group';

export interface CustomNodeGroup {
  id: string;
  name: string;
  nodeIds: string[];
  collapsed: boolean;
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
  tier?: number;
  tierLabel?: string;
  isGroup?: boolean;
  groupType?: 'category' | 'custom';
  groupKind?: EntityKind;
  customGroupId?: string;
  memberNodes?: SelectedNodeDetail[];
  internalConnections?: ConnectionDetailItem[];
}

export interface ConnectionDetailItem {
  id: string;
  sourceId: string;
  sourceName: string;
  targetId: string;
  targetName: string;
  category: 'service_call' | 'database' | 'messaging' | 'external';
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

export interface OrbitLegendItem {
  levelIndex: number;
  shortLabel: string;
  title: string;
  count: number;
  radius: number;
  nodeIds: string[];
}

export interface DomainArchitectureViewProps {
  graph: GraphData | null;
  onFocusInFlow?: (projectName: string) => void;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
  onSelectNode?: (node: GraphNode | null) => void;
  onSwitchToContexts?: () => void;
  onSetDomainOverride?: (serviceName: string, domain: string, context?: string, reset?: boolean) => void;
  availableDomains?: string[];
}

export interface DomainGroupSummary {
  domainKey: string;
  displayName: string;
  nodes: SelectedNodeDetail[];
  databases: SelectedNodeDetail[];
  topics: SelectedNodeDetail[];
  externalServices: SelectedNodeDetail[];
}
