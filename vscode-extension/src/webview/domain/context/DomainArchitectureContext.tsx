import React, { createContext, useContext, useState, useCallback, useMemo, useEffect } from 'react';
import {
  PresentationMode,
  DomainLayoutName,
  EdgeCurveMode,
  EntityKind,
  SelectedNodeDetail,
  SelectedEdgeDetail,
  DomainArchitectureViewProps,
} from '../types';

export interface DomainArchitectureContextValue {
  // Presentation
  presentationMode: PresentationMode;
  setPresentationMode: (mode: PresentationMode) => void;

  // Selection
  selectedNode: SelectedNodeDetail | null;
  selectedEdge: SelectedEdgeDetail | null;
  selectNode: (node: SelectedNodeDetail | null) => void;
  selectEdge: (edge: SelectedEdgeDetail | null) => void;
  clearSelection: () => void;

  // Filtering
  searchQuery: string;
  setSearchQuery: (q: string) => void;
  hiddenTypes: Set<EntityKind>;
  toggleTypeVisibility: (kind: EntityKind) => void;
  hiddenNodeIds: Set<string>;
  hideNode: (id: string) => void;
  unhideNode: (id: string) => void;
  restoreAllHiddenNodes: () => void;
  hiddenOrbitTiers: Set<number>;
  toggleOrbitVisibility: (levelIndex: number) => void;
  hideSingleConnectionDbs: boolean;
  setHideSingleConnectionDbs: (hide: boolean | ((prev: boolean) => boolean)) => void;
  hideIsolatedNodes: boolean;
  setHideIsolatedNodes: (hide: boolean | ((prev: boolean) => boolean)) => void;
  showTransitiveEdges: boolean;
  setShowTransitiveEdges: (show: boolean | ((prev: boolean) => boolean)) => void;
  resetAllFilters: () => void;

  // Layout & Display
  layoutName: DomainLayoutName;
  setLayoutName: (name: DomainLayoutName) => void;
  edgeCurveMode: EdgeCurveMode;
  setEdgeCurveMode: (mode: EdgeCurveMode) => void;
  curveFactor: number;
  setCurveFactor: (factor: number) => void;
  edgeLabelsOnHover: boolean;
  setEdgeLabelsOnHover: (onHover: boolean) => void;
  fontSize: number;
  setFontSize: (size: number) => void;
  spacingFactor: number;
  setSpacingFactor: (factor: number) => void;

  // Panels
  isInspectorCollapsed: boolean;
  setIsInspectorCollapsed: (collapsed: boolean | ((prev: boolean) => boolean)) => void;
  isHiddenPanelCollapsed: boolean;
  setIsHiddenPanelCollapsed: (collapsed: boolean | ((prev: boolean) => boolean)) => void;

  // Domain Override Modal
  overrideModalOpen: boolean;
  overrideTargetService: string;
  openOverrideModal: (serviceName: string) => void;
  closeOverrideModal: () => void;

  // External Callbacks
  onOpenFile?: (filePath: string, lineStart?: number) => void;
  onFocusInFlow?: (projectName: string) => void;
  onSetDomainOverride?: (serviceName: string, domain: string, context?: string, reset?: boolean) => void;
  onSwitchToContexts?: () => void;
  availableDomains?: string[];
}

const DomainArchitectureContext = createContext<DomainArchitectureContextValue | null>(null);

export interface DomainArchitectureProviderProps extends DomainArchitectureViewProps {
  children: React.ReactNode;
}

export const DomainArchitectureProvider: React.FC<DomainArchitectureProviderProps> = ({
  children,
  onOpenFile,
  onFocusInFlow,
  onSelectNode,
  onSwitchToContexts,
  onSetDomainOverride,
  availableDomains,
}) => {
  const [presentationMode, setPresentationMode] = useState<PresentationMode>('graph');
  const [selectedNode, setSelectedNode] = useState<SelectedNodeDetail | null>(null);
  const [selectedEdge, setSelectedEdge] = useState<SelectedEdgeDetail | null>(null);

  const [searchQuery, setSearchQuery] = useState('');
  const [hiddenTypes, setHiddenTypes] = useState<Set<EntityKind>>(() => {
    try {
      const saved = localStorage.getItem('ce_domain_hidden_types');
      if (saved) {
        const parsed = JSON.parse(saved);
        if (Array.isArray(parsed)) return new Set<EntityKind>(parsed);
      }
    } catch { }
    return new Set<EntityKind>(['ExternalService']);
  });
  const [hiddenNodeIds, setHiddenNodeIds] = useState<Set<string>>(() => {
    try {
      const saved = localStorage.getItem('ce_domain_hidden_node_ids');
      if (saved) {
        const parsed = JSON.parse(saved);
        if (Array.isArray(parsed)) return new Set<string>(parsed);
      }
    } catch { }
    return new Set<string>();
  });
  const [hiddenOrbitTiers, setHiddenOrbitTiers] = useState<Set<number>>(() => {
    try {
      const saved = localStorage.getItem('ce_domain_hidden_orbit_tiers');
      if (saved) {
        const parsed = JSON.parse(saved);
        if (Array.isArray(parsed)) return new Set<number>(parsed);
      }
    } catch { }
    return new Set<number>();
  });
  const [hideSingleConnectionDbs, setHideSingleConnectionDbs] = useState<boolean>(() => {
    try {
      const saved = localStorage.getItem('ce_hide_single_connection_dbs');
      return saved !== null ? saved === 'true' : false;
    } catch { }
    return false;
  });
  const [hideIsolatedNodes, setHideIsolatedNodes] = useState<boolean>(() => {
    try {
      const saved = localStorage.getItem('ce_hide_isolated_nodes');
      return saved !== null ? saved === 'true' : false;
    } catch { }
    return false;
  });
  const [showTransitiveEdges, setShowTransitiveEdges] = useState(true);

  // Sync hidden and filter states to localStorage
  useEffect(() => {
    try {
      localStorage.setItem('ce_domain_hidden_types', JSON.stringify([...hiddenTypes]));
    } catch { }
  }, [hiddenTypes]);

  useEffect(() => {
    try {
      localStorage.setItem('ce_domain_hidden_node_ids', JSON.stringify([...hiddenNodeIds]));
    } catch { }
  }, [hiddenNodeIds]);

  useEffect(() => {
    try {
      localStorage.setItem('ce_domain_hidden_orbit_tiers', JSON.stringify([...hiddenOrbitTiers]));
    } catch { }
  }, [hiddenOrbitTiers]);

  useEffect(() => {
    try {
      localStorage.setItem('ce_hide_single_connection_dbs', String(hideSingleConnectionDbs));
    } catch { }
  }, [hideSingleConnectionDbs]);

  useEffect(() => {
    try {
      localStorage.setItem('ce_hide_isolated_nodes', String(hideIsolatedNodes));
    } catch { }
  }, [hideIsolatedNodes]);

  const [layoutName, setLayoutName] = useState<DomainLayoutName>('concentric');
  const [edgeCurveMode, setEdgeCurveMode] = useState<EdgeCurveMode>('avoid-inner');
  const [curveFactor, setCurveFactor] = useState(0.28);
  const [edgeLabelsOnHover, setEdgeLabelsOnHover] = useState(true);
  const [fontSize, setFontSize] = useState(12);
  const [spacingFactor, setSpacingFactor] = useState(1.0);

  const [isInspectorCollapsed, setIsInspectorCollapsed] = useState(false);
  const [isHiddenPanelCollapsed, setIsHiddenPanelCollapsed] = useState(false);

  const [overrideModalOpen, setOverrideModalOpen] = useState(false);
  const [overrideTargetService, setOverrideTargetService] = useState('');

  const selectNode = useCallback(
    (node: SelectedNodeDetail | null) => {
      setSelectedNode(node);
      if (node) {
        setSelectedEdge(null);
        setIsInspectorCollapsed(false);
      }
      if (onSelectNode) {
        // Adapt to proto GraphNode if needed
        onSelectNode(node ? ({ id: node.id, name: node.name, kind: node.kind } as any) : null);
      }
    },
    [onSelectNode]
  );

  const selectEdge = useCallback((edge: SelectedEdgeDetail | null) => {
    setSelectedEdge(edge);
    if (edge) {
      setSelectedNode(null);
      setIsInspectorCollapsed(false);
    }
  }, []);

  const clearSelection = useCallback(() => {
    setSelectedNode(null);
    setSelectedEdge(null);
  }, []);

  const toggleTypeVisibility = useCallback((kind: EntityKind) => {
    setHiddenTypes((prev) => {
      const next = new Set(prev);
      if (next.has(kind)) next.delete(kind);
      else next.add(kind);
      return next;
    });
  }, []);

  const toggleOrbitVisibility = useCallback((levelIndex: number) => {
    setHiddenOrbitTiers((prev) => {
      const next = new Set(prev);
      if (next.has(levelIndex)) next.delete(levelIndex);
      else next.add(levelIndex);
      return next;
    });
  }, []);

  const hideNode = useCallback((id: string) => {
    setHiddenNodeIds((prev) => new Set(prev).add(id));
  }, []);

  const unhideNode = useCallback((id: string) => {
    setHiddenNodeIds((prev) => {
      const next = new Set(prev);
      next.delete(id);
      return next;
    });
  }, []);

  const restoreAllHiddenNodes = useCallback(() => {
    setHiddenNodeIds(new Set());
    setHiddenTypes(new Set());
    setHiddenOrbitTiers(new Set());
    setHideSingleConnectionDbs(false);
    setHideIsolatedNodes(false);
    setShowTransitiveEdges(true);
    setSearchQuery('');
  }, []);

  const resetAllFilters = useCallback(() => {
    restoreAllHiddenNodes();
  }, [restoreAllHiddenNodes]);

  const openOverrideModal = useCallback((serviceName: string) => {
    setOverrideTargetService(serviceName);
    setOverrideModalOpen(true);
  }, []);

  const closeOverrideModal = useCallback(() => {
    setOverrideModalOpen(false);
    setOverrideTargetService('');
  }, []);

  const value = useMemo<DomainArchitectureContextValue>(
    () => ({
      presentationMode,
      setPresentationMode,
      selectedNode,
      selectedEdge,
      selectNode,
      selectEdge,
      clearSelection,
      searchQuery,
      setSearchQuery,
      hiddenTypes,
      toggleTypeVisibility,
      hiddenNodeIds,
      hideNode,
      unhideNode,
      restoreAllHiddenNodes,
      hiddenOrbitTiers,
      toggleOrbitVisibility,
      hideSingleConnectionDbs,
      setHideSingleConnectionDbs,
      hideIsolatedNodes,
      setHideIsolatedNodes,
      showTransitiveEdges,
      setShowTransitiveEdges,
      resetAllFilters,
      layoutName,
      setLayoutName,
      edgeCurveMode,
      setEdgeCurveMode,
      curveFactor,
      setCurveFactor,
      edgeLabelsOnHover,
      setEdgeLabelsOnHover,
      fontSize,
      setFontSize,
      spacingFactor,
      setSpacingFactor,
      isInspectorCollapsed,
      setIsInspectorCollapsed,
      isHiddenPanelCollapsed,
      setIsHiddenPanelCollapsed,
      overrideModalOpen,
      overrideTargetService,
      openOverrideModal,
      closeOverrideModal,
      onOpenFile,
      onFocusInFlow,
      onSetDomainOverride,
      onSwitchToContexts,
      availableDomains,
    }),
    [
      presentationMode,
      selectedNode,
      selectedEdge,
      selectNode,
      selectEdge,
      clearSelection,
      searchQuery,
      hiddenTypes,
      toggleTypeVisibility,
      hiddenNodeIds,
      hideNode,
      unhideNode,
      restoreAllHiddenNodes,
      hiddenOrbitTiers,
      toggleOrbitVisibility,
      hideSingleConnectionDbs,
      hideIsolatedNodes,
      showTransitiveEdges,
      resetAllFilters,
      layoutName,
      edgeCurveMode,
      curveFactor,
      edgeLabelsOnHover,
      fontSize,
      spacingFactor,
      isInspectorCollapsed,
      isHiddenPanelCollapsed,
      overrideModalOpen,
      overrideTargetService,
      openOverrideModal,
      closeOverrideModal,
      onOpenFile,
      onFocusInFlow,
      onSetDomainOverride,
      onSwitchToContexts,
      availableDomains,
    ]
  );

  return (
    <DomainArchitectureContext.Provider value={value}>
      {children}
    </DomainArchitectureContext.Provider>
  );
};

export function useDomainArchitecture(): DomainArchitectureContextValue {
  const ctx = useContext(DomainArchitectureContext);
  if (!ctx) {
    throw new Error('useDomainArchitecture must be used within a DomainArchitectureProvider');
  }
  return ctx;
}
