import React, { useState, useEffect, useMemo, useRef, useCallback } from 'react';
import {
  OrbitConfig,
  DEFAULT_ORBIT_CONFIG,
  isDefaultOrbitConfig,
  mergeOrbits,
  splitOrbit,
  moveOrbit,
  moveKindToOrbit,
} from '../layout/orbitConfig';
import {
  DomainLayoutName,
  EdgeCurveMode,
  OrbitLegendItem,
  SelectedNodeDetail,
  SelectedEdgeDetail,
  EntityKind,
} from './DomainArchitectureView';
import { CustomNodeGroup, NodeContentMode, NodeSizingMode } from '../domain/types';
import { DomainInspector, RawEdgeInfo } from '../domain/components/DomainInspector';
import { DomainVisibilityPanel } from '../domain/components/DomainVisibilityPanel';
import { DomainOverrideModal } from './DomainOverrideModal';
import { FloatingPanel } from './FloatingPanel';
import {
  LayersIcon,
  RotateCcwIcon,
  ChevronUpIcon,
  ChevronDownIcon,
  EyeIcon,
  EyeOffIcon,
  InfoIcon,
} from './Icons';
import { getVsCodeApi } from '../vscodeApi';

export interface ProjectMapPresetsState {
  cyclesCount: number;
  cyclesOnly: boolean;
  hideUtilities: boolean;
  hideResources: boolean;
}

export interface InspectorSyncedState {
  hudLabel?: string;
  hudTitle?: string;
  isMatrixMode: boolean;
  effectiveSidebarTab: 'info' | 'orbits' | 'visibility';
  hasOrbitLegend: boolean;
  orbitLegendItems: OrbitLegendItem[];
  hiddenCount: number;
  // Info tab
  selectedNode: SelectedNodeDetail | null;
  selectedEdge: SelectedEdgeDetail | null;
  nodeDetailEntries: Array<[string, SelectedNodeDetail]>;
  rawEdges: RawEdgeInfo[];
  isInspectorCollapsed: boolean;
  isEdgeInspectorCollapsed: boolean;
  availableDomains: string[];
  // Orbits & Layout tab
  layoutName: DomainLayoutName;
  spacingFactor: number;
  orbitDistance: number;
  minOrbitNodeDistance: number;
  autoRelayoutOnFilter: boolean;
  orbitConfig: OrbitConfig;
  hiddenOrbitTiers: number[];
  // Visibility & Style tab
  echelonEntries: Array<[string, number]>;
  hiddenNodeIds: string[];
  hiddenTypes: EntityKind[];
  hideSingleConnectionDbs: boolean;
  hideIsolatedNodes: boolean;
  showTransitiveEdges: boolean;
  counts: {
    ingress?: number;
    services?: number;
    workers?: number;
    cli?: number;
    testSuites?: number;
    libraries?: number;
    databases?: number;
    singleConnDbs?: number;
    sharedDbs?: number;
    topics?: number;
    external?: number;
    serviceCalls?: number;
    messages?: number;
    transitiveCalls?: number;
  };
  groupedTypes: EntityKind[];
  customGroups: CustomNodeGroup[];
  nodeContentMode: NodeContentMode;
  nodeSizingMode: NodeSizingMode;
  edgeCurveMode: EdgeCurveMode;
  edgeLabelsOnHover: boolean;
  fontSize: number;
  curveFactor: number;
  wheelSensitivity: number;
  projectMapPresets?: ProjectMapPresetsState;
}

export type InspectorAction =
  | { type: 'SET_SIDEBAR_TAB'; tab: 'info' | 'orbits' | 'visibility' }
  | { type: 'SET_INSPECTOR_COLLAPSED'; collapsed: boolean }
  | { type: 'SET_EDGE_INSPECTOR_COLLAPSED'; collapsed: boolean }
  | { type: 'SELECT_NODE'; node: SelectedNodeDetail | null }
  | { type: 'SELECT_NODE_BY_ID'; id: string }
  | { type: 'SELECT_EDGE'; edge: SelectedEdgeDetail | null }
  | { type: 'CLEAR_SELECTION' }
  | { type: 'CLEAR_GRAPH_HIGHLIGHT' }
  | { type: 'HIDE_NODE'; id: string }
  | { type: 'UNGROUP_NODE'; node: SelectedNodeDetail }
  | { type: 'REMOVE_FROM_GROUP'; groupId: string; memberId: string }
  | { type: 'FOCUS_IN_FLOW'; projectName: string }
  | { type: 'OPEN_PROJECT_MAP'; projectName: string }
  | { type: 'OPEN_FILE'; filePath: string; lineStart?: number }
  | { type: 'SET_DOMAIN_OVERRIDE'; serviceName: string; domain: string; context?: string; reset?: boolean }
  | { type: 'SET_LAYOUT'; layoutName: DomainLayoutName }
  | { type: 'SET_SPACING'; spacingFactor: number }
  | { type: 'SET_ORBIT_DISTANCE'; orbitDistance: number }
  | { type: 'SET_MIN_ORBIT_NODE_DISTANCE'; minOrbitNodeDistance: number }
  | { type: 'SET_AUTO_RELAYOUT'; enabled: boolean }
  | { type: 'FORCE_RELAYOUT' }
  | { type: 'APPLY_ORBIT_CONFIG'; next: OrbitConfig; clearHiddenFor?: number[]; resetHidden?: boolean }
  | { type: 'TOGGLE_ORBIT_VISIBILITY'; levelIndex: number }
  | { type: 'HIGHLIGHT_ORBIT_NODES'; nodeIds: string[] }
  | { type: 'CLEAR_ORBIT_HIGHLIGHT' }
  | { type: 'UNHIDE_NODE'; id: string }
  | { type: 'RESTORE_ALL_HIDDEN' }
  | { type: 'TOGGLE_TYPE_VISIBILITY'; kind: EntityKind }
  | { type: 'TOGGLE_SINGLE_CONN_DBS' }
  | { type: 'TOGGLE_ISOLATED_NODES' }
  | { type: 'TOGGLE_TRANSITIVE_EDGES' }
  | { type: 'TOGGLE_TYPE_GROUPING'; kind: EntityKind }
  | { type: 'CREATE_CUSTOM_GROUP'; name: string; nodeIds: string[] }
  | { type: 'TOGGLE_CUSTOM_GROUP_COLLAPSE'; groupId: string }
  | { type: 'DELETE_CUSTOM_GROUP'; groupId: string }
  | { type: 'SET_NODE_CONTENT_MODE'; mode: NodeContentMode }
  | { type: 'SET_NODE_SIZING_MODE'; mode: NodeSizingMode }
  | { type: 'SET_EDGE_CURVE_MODE'; mode: EdgeCurveMode }
  | { type: 'SET_EDGE_LABELS_ON_HOVER'; onHover: boolean }
  | { type: 'SET_FONT_SIZE'; fontSize: number }
  | { type: 'SET_CURVE_FACTOR'; curveFactor: number }
  | { type: 'SET_WHEEL_SENSITIVITY'; wheelSensitivity: number }
  | { type: 'TOGGLE_PM_PRESET'; preset: 'cyclesOnly' | 'hideUtilities' | 'hideResources' };

export const InspectorSidebarView: React.FC = () => {
  const [state, setState] = useState<InspectorSyncedState | null>(null);
  const [overrideModalOpen, setOverrideModalOpen] = useState(false);
  const [overrideTargetService, setOverrideTargetService] = useState('');

  // Local orbit drag & multi-select state
  const [selectedOrbitRows, setSelectedOrbitRows] = useState<number[]>([]);
  const lastSelectedOrbitRowRef = useRef<number | null>(null);
  const [draggedOrbitIndex, setDraggedOrbitIndex] = useState<number | null>(null);
  const draggedKindRef = useRef<string | null>(null);
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);

  const dispatch = useCallback((action: InspectorAction) => {
    // Optimistically update local state for immediate slider/tab feedback
    setState((prev) => {
      if (!prev) return prev;
      switch (action.type) {
        case 'SET_SIDEBAR_TAB':
          return { ...prev, effectiveSidebarTab: action.tab };
        case 'SET_LAYOUT':
          return { ...prev, layoutName: action.layoutName };
        case 'SET_SPACING':
          return { ...prev, spacingFactor: action.spacingFactor };
        case 'SET_ORBIT_DISTANCE':
          return { ...prev, orbitDistance: action.orbitDistance };
        case 'SET_MIN_ORBIT_NODE_DISTANCE':
          return { ...prev, minOrbitNodeDistance: action.minOrbitNodeDistance };
        case 'SET_AUTO_RELAYOUT':
          return { ...prev, autoRelayoutOnFilter: action.enabled };
        case 'SET_NODE_CONTENT_MODE':
          return { ...prev, nodeContentMode: action.mode };
        case 'SET_NODE_SIZING_MODE':
          return { ...prev, nodeSizingMode: action.mode };
        case 'SET_EDGE_CURVE_MODE':
          return { ...prev, edgeCurveMode: action.mode };
        case 'SET_EDGE_LABELS_ON_HOVER':
          return { ...prev, edgeLabelsOnHover: action.onHover };
        case 'SET_FONT_SIZE':
          return { ...prev, fontSize: action.fontSize };
        case 'SET_CURVE_FACTOR':
          return { ...prev, curveFactor: action.curveFactor };
        case 'SET_WHEEL_SENSITIVITY':
          return { ...prev, wheelSensitivity: action.wheelSensitivity };
        default:
          return prev;
      }
    });

    getVsCodeApi()?.postMessage({
      type: 'INSPECTOR_ACTION',
      action,
    });
  }, []);

  useEffect(() => {
    const handleMessage = (event: MessageEvent) => {
      const msg = event.data;
      if (msg?.type === 'INSPECTOR_STATE_SYNC' && msg.state) {
        setState(msg.state as InspectorSyncedState);
      }
    };
    window.addEventListener('message', handleMessage);
    getVsCodeApi()?.postMessage({ type: 'WEBVIEW_READY' });
    return () => window.removeEventListener('message', handleMessage);
  }, []);

  const nodeDetailMap = useMemo(
    () => new Map<string, SelectedNodeDetail>(state?.nodeDetailEntries || []),
    [state?.nodeDetailEntries]
  );
  const echelonMap = useMemo(
    () => new Map<string, number>(state?.echelonEntries || []),
    [state?.echelonEntries]
  );
  const hiddenNodeIds = useMemo(
    () => new Set<string>(state?.hiddenNodeIds || []),
    [state?.hiddenNodeIds]
  );
  const hiddenTypes = useMemo(
    () => new Set<EntityKind>(state?.hiddenTypes || []),
    [state?.hiddenTypes]
  );
  const hiddenOrbitTiers = useMemo(
    () => new Set<number>(state?.hiddenOrbitTiers || []),
    [state?.hiddenOrbitTiers]
  );
  const groupedTypes = useMemo(
    () => new Set<EntityKind>(state?.groupedTypes || []),
    [state?.groupedTypes]
  );

  const orbitLegendItems = state?.orbitLegendItems || [];
  const orbitConfig = state?.orbitConfig || DEFAULT_ORBIT_CONFIG;

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
        groups.push({
          orbitIndex: item.orbitIndex,
          levelIndex: item.levelIndex,
          shortLabel: item.shortLabel,
          rows: [{ item, idx }],
        });
      }
    });
    return groups;
  }, [orbitLegendItems]);

  const applyOrbitCfg = useCallback(
    (next: OrbitConfig, clearHiddenFor: number[] = [], resetHidden = false) => {
      setSelectedOrbitRows([]);
      lastSelectedOrbitRowRef.current = null;
      dispatch({ type: 'APPLY_ORBIT_CONFIG', next, clearHiddenFor, resetHidden });
    },
    [dispatch]
  );

  const handleMoveOrbit = useCallback(
    (item: OrbitLegendItem, direction: -1 | 1) => {
      const populated = Array.from(new Set(orbitLegendItems.map((it) => it.orbitIndex)));
      const pos = populated.indexOf(item.orbitIndex);
      const target = populated[pos + direction];
      if (pos < 0 || target === undefined) return;
      applyOrbitCfg(moveOrbit(orbitConfig, item.orbitIndex, target));
    },
    [orbitLegendItems, orbitConfig, applyOrbitCfg]
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
        applyOrbitCfg(moveKindToOrbit(orbitConfig, kind, target.levelIndex), [target.levelIndex]);
        return;
      }
      const source = dragged === null ? undefined : orbitLegendItems[dragged];
      if (!source || source.orbitIndex === target.orbitIndex) return;
      applyOrbitCfg(moveOrbit(orbitConfig, source.orbitIndex, target.orbitIndex));
    },
    [draggedOrbitIndex, orbitLegendItems, orbitConfig, applyOrbitCfg]
  );

  const handleDragEnd = useCallback(() => {
    setDraggedOrbitIndex(null);
    setDragOverIndex(null);
    draggedKindRef.current = null;
  }, []);

  const handleOrbitRowClick = useCallback((e: React.MouseEvent, idx: number) => {
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
  }, []);

  const handleMergeSelected = useCallback(() => {
    if (!mergeSelection) return;
    const first = mergeSelection[0];
    const last = mergeSelection[mergeSelection.length - 1];
    const affected = orbitConfig.orbits.slice(first, last + 1).flat();
    applyOrbitCfg(mergeOrbits(orbitConfig, first, last), affected);
  }, [mergeSelection, orbitConfig, applyOrbitCfg]);

  const handleSplitOrbit = useCallback(
    (orbitIndex: number) => {
      applyOrbitCfg(splitOrbit(orbitConfig, orbitIndex), orbitConfig.orbits[orbitIndex] ?? []);
    },
    [orbitConfig, applyOrbitCfg]
  );

  const handleResetOrbitOrder = useCallback(() => {
    applyOrbitCfg(DEFAULT_ORBIT_CONFIG, [], true);
  }, [applyOrbitCfg]);

  if (!state) {
    return (
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          height: '100vh',
          padding: '24px 16px',
          textAlign: 'center',
          color: '#94a3b8',
          background: '#0b0f19',
          gap: '12px',
        }}
      >
        <div style={{ fontSize: '28px', opacity: 0.75 }}>🪐</div>
        <div style={{ fontSize: '13px', fontWeight: 600, color: '#e2e8f0' }}>
          Graph Inspector & Controls
        </div>
        <div style={{ fontSize: '11.5px', lineHeight: 1.5, maxWidth: '240px' }}>
          Open an Architecture Map or Project Map tab to inspect nodes, configure orbits, and adjust visual parameters here.
        </div>
        <button
          type="button"
          className="domain-hud-btn"
          style={{ marginTop: '4px', padding: '6px 12px', fontSize: '11.5px' }}
          onClick={() => getVsCodeApi()?.postMessage({ type: 'OPEN_VIEW', viewMode: 'semantic' })}
        >
          Open Architecture Map
        </button>
        <button
          type="button"
          className="domain-hud-btn"
          style={{ padding: '4px 10px', fontSize: '10.5px', opacity: 0.8 }}
          onClick={() => getVsCodeApi()?.postMessage({ type: 'MOVE_INSPECTOR_VIEW' })}
          title="Move this panel to the Secondary Side Bar (Right, next to AI Chat) or Primary Side Bar"
        >
          ⇄ Move Panel Location...
        </button>
      </div>
    );
  }

  const {
    effectiveSidebarTab,
    hasOrbitLegend,
    hiddenCount,
    selectedNode,
    selectedEdge,
    rawEdges,
    isInspectorCollapsed,
    isEdgeInspectorCollapsed,
    availableDomains,
    layoutName,
    spacingFactor,
    orbitDistance,
    minOrbitNodeDistance,
    autoRelayoutOnFilter,
    hideSingleConnectionDbs,
    hideIsolatedNodes,
    showTransitiveEdges,
    counts,
    customGroups,
    nodeContentMode,
    nodeSizingMode,
    edgeCurveMode,
    edgeLabelsOnHover,
    fontSize,
    curveFactor,
    wheelSensitivity,
    isMatrixMode,
    projectMapPresets,
  } = state;

  const customPresetsSlot = projectMapPresets ? (
    <>
      {projectMapPresets.cyclesCount > 0 && (
        <button
          type="button"
          className={`visibility-preset-item ${projectMapPresets.cyclesOnly ? 'is-active' : ''}`}
          onClick={() => dispatch({ type: 'TOGGLE_PM_PRESET', preset: 'cyclesOnly' })}
          title="Isolate only modules involved in dependency cycles"
        >
          <div className="visibility-preset-main">
            <span className="visibility-preset-icon">⚠️</span>
            <div className="visibility-preset-text">
              <span className="visibility-preset-label">Cycles Only</span>
              <span className="visibility-preset-desc">
                {projectMapPresets.cyclesCount} dependency cycle{projectMapPresets.cyclesCount === 1 ? '' : 's'} detected
              </span>
            </div>
          </div>
          <div className={`visibility-preset-toggle ${projectMapPresets.cyclesOnly ? 'is-on' : ''}`}>
            {projectMapPresets.cyclesOnly ? 'ON' : 'OFF'}
          </div>
        </button>
      )}

      <button
        type="button"
        className={`visibility-preset-item ${projectMapPresets.hideUtilities ? 'is-active' : ''}`}
        onClick={() => dispatch({ type: 'TOGGLE_PM_PRESET', preset: 'hideUtilities' })}
        title="Hide foundation/utility modules (Orbit 4)"
      >
        <div className="visibility-preset-main">
          <span className="visibility-preset-icon">🧰</span>
          <div className="visibility-preset-text">
            <span className="visibility-preset-label">Hide Utilities</span>
            <span className="visibility-preset-desc">Foundation & helper modules (Orbit 4)</span>
          </div>
        </div>
        <div className={`visibility-preset-toggle ${projectMapPresets.hideUtilities ? 'is-on' : ''}`}>
          {projectMapPresets.hideUtilities ? 'ON' : 'OFF'}
        </div>
      </button>

      <button
        type="button"
        className={`visibility-preset-item ${projectMapPresets.hideResources ? 'is-active' : ''}`}
        onClick={() => dispatch({ type: 'TOGGLE_PM_PRESET', preset: 'hideResources' })}
        title="Hide database tables, procedures, topics, and external resources"
      >
        <div className="visibility-preset-main">
          <span className="visibility-preset-icon">🗄️</span>
          <div className="visibility-preset-text">
            <span className="visibility-preset-label">Hide Resources</span>
            <span className="visibility-preset-desc">Tables, procedures, topics & external APIs</span>
          </div>
        </div>
        <div className={`visibility-preset-toggle ${projectMapPresets.hideResources ? 'is-on' : ''}`}>
          {projectMapPresets.hideResources ? 'ON' : 'OFF'}
        </div>
      </button>
    </>
  ) : undefined;

  const renderOrbitLegend = () => (
    <FloatingPanel
      dock="top-right"
      docked={true}
      isCollapsed={false}
      title="Orbit Legend"
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
              title="Merge the selected adjacent orbits into one ring"
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
              title="Reset to the default orbit order and show all orbits"
            >
              <RotateCcwIcon size={11} />
              <span>Reset</span>
            </button>
          ) : null}
        </>
      }
      collapsedTitle={`Orbits (${orbitLegendItems.length})`}
      collapsedIcon={<LayersIcon size={13} />}
      collapseTooltip="Collapse Orbit Legend"
      expandTooltip="Expand Orbit Legend"
      width={360}
      minWidth={240}
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
              onChange={(e) => dispatch({ type: 'SET_LAYOUT', layoutName: e.target.value as DomainLayoutName })}
              title="Graph Layout"
              className="domain-layout-dropdown"
              style={{ width: '100%', marginTop: '4px' }}
            >
              <option value="concentric">🎯 Concentric (Original)</option>
              <option value="concentric-equispaced">🪐 Concentric: Equispaced</option>
              <option value="concentric-barycentric">🪐 Concentric: Barycentric</option>
              <option value="concentric-polar-force">🪐 Concentric: Polar Force</option>
              <option value="concentric-sectors">🪐 Concentric: Domain Sectors</option>
              <option value="swimlanes">🏊 Swimlanes (Pipeline)</option>
              <option value="clusters">🏝️ Domain Islands (Bounded Contexts)</option>
              <option value="hive">🕸️ Hive Plot (Multi-Axis)</option>
              <option value="cose">⚡ Force (COSE)</option>
            </select>
          </div>

          {/* Node Spacing / Air Control */}
          <div className="display-setting-row" title="Adjust node spacing / distance between orbits">
            <div className="setting-label-row">
              <span className="setting-icon">💨</span>
              <span className="setting-name">Node Air (Spacing)</span>
              <span
                className="domain-hud-value-badge"
                onClick={() => dispatch({ type: 'SET_SPACING', spacingFactor: 1.0 })}
                title="Click to reset air to 1.0x"
              >
                {spacingFactor.toFixed(1)}x
              </span>
            </div>
            <div className="setting-slider-row">
              <button
                type="button"
                className="domain-hud-step-btn"
                onClick={() =>
                  dispatch({ type: 'SET_SPACING', spacingFactor: Math.max(0.4, +(spacingFactor - 0.2).toFixed(1)) })
                }
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
                onChange={(e) => dispatch({ type: 'SET_SPACING', spacingFactor: parseFloat(e.target.value) })}
                className="domain-hud-slider"
              />
              <button
                type="button"
                className="domain-hud-step-btn"
                onClick={() =>
                  dispatch({ type: 'SET_SPACING', spacingFactor: Math.min(3.5, +(spacingFactor + 0.2).toFixed(1)) })
                }
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
                  onClick={() => dispatch({ type: 'SET_ORBIT_DISTANCE', orbitDistance: 280 })}
                  title="Click to reset orbit distance to 280px"
                >
                  {orbitDistance}px
                </span>
              </div>
              <div className="setting-slider-row">
                <button
                  type="button"
                  className="domain-hud-step-btn"
                  onClick={() => dispatch({ type: 'SET_ORBIT_DISTANCE', orbitDistance: orbitDistance - 20 })}
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
                  onChange={(e) => dispatch({ type: 'SET_ORBIT_DISTANCE', orbitDistance: parseInt(e.target.value, 10) })}
                  className="domain-hud-slider"
                />
                <button
                  type="button"
                  className="domain-hud-step-btn"
                  onClick={() => dispatch({ type: 'SET_ORBIT_DISTANCE', orbitDistance: orbitDistance + 20 })}
                  title="Increase orbit distance (+20px)"
                >
                  +
                </button>
              </div>
            </div>
          )}

          {/* Min Distance on Orbit Control */}
          {layoutName.startsWith('concentric') && (
            <div
              className="display-setting-row"
              title="Adjust minimum distance between nodes on the same orbit (50px - 400px)"
            >
              <div className="setting-label-row">
                <span className="setting-icon">↔️</span>
                <span className="setting-name">Min Distance on Orbit</span>
                <span
                  className="domain-hud-value-badge"
                  onClick={() => dispatch({ type: 'SET_MIN_ORBIT_NODE_DISTANCE', minOrbitNodeDistance: 125 })}
                  title="Click to reset min orbit node distance to 125px"
                >
                  {minOrbitNodeDistance}px
                </span>
              </div>
              <div className="setting-slider-row">
                <button
                  type="button"
                  className="domain-hud-step-btn"
                  onClick={() =>
                    dispatch({ type: 'SET_MIN_ORBIT_NODE_DISTANCE', minOrbitNodeDistance: minOrbitNodeDistance - 15 })
                  }
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
                  onChange={(e) =>
                    dispatch({
                      type: 'SET_MIN_ORBIT_NODE_DISTANCE',
                      minOrbitNodeDistance: parseInt(e.target.value, 10),
                    })
                  }
                  className="domain-hud-slider"
                />
                <button
                  type="button"
                  className="domain-hud-step-btn"
                  onClick={() =>
                    dispatch({ type: 'SET_MIN_ORBIT_NODE_DISTANCE', minOrbitNodeDistance: minOrbitNodeDistance + 15 })
                  }
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
                onChange={(e) => dispatch({ type: 'SET_AUTO_RELAYOUT', enabled: e.target.checked })}
              />
              <span>🔄 Auto-recalc</span>
            </label>
            <button
              type="button"
              className="domain-hud-btn"
              onClick={() => dispatch({ type: 'FORCE_RELAYOUT' })}
              title="Recalculate graph layout now (Re-layout)"
              style={{ padding: '2px 8px', fontSize: '11px', lineHeight: 1 }}
            >
              ⟳ Re-layout
            </button>
          </div>
        </div>

        {/* 2. Concentric Orbits Hierarchy */}
        {layoutName.startsWith('concentric') ? (
          <>
            <div
              className="orbit-axis-label"
              title="Orbits run from the center of the map (top) to its periphery (bottom). Drag cards or use ▲/▼ to reorder; shift-click adjacent cards, then Merge, to share one ring; drag a kind chip onto another orbit to move it."
            >
              <span className="orbit-axis-dot" />
              <span>Center</span>
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
                  onMouseEnter={() =>
                    !isOrbitHidden && dispatch({ type: 'HIGHLIGHT_ORBIT_NODES', nodeIds: allNodeIds })
                  }
                  onMouseLeave={() => dispatch({ type: 'CLEAR_ORBIT_HIGHLIGHT' })}
                  title={`${group.shortLabel}: ${groupTitle} (${total} nodes). Drag or use ▲/▼ to reorder; shift-click to select for merging.`}
                >
                  <header className="orbit-card-header">
                    <span className="orbit-drag-handle" title="Drag to reorder orbit">
                      ⠿
                    </span>
                    <span className="orbit-legend-pill">{group.shortLabel}</span>
                    {isMerged ? <span className="orbit-merged-tag">Merged</span> : null}
                    <span className="orbit-card-spacer" />
                    <span className="orbit-legend-count" title={`${total} nodes on this orbit`}>
                      {total}
                    </span>
                    <div className="orbit-card-actions">
                      {isMerged ? (
                        <button
                          type="button"
                          className="orbit-split-btn"
                          onClick={(e) => {
                            e.stopPropagation();
                            handleSplitOrbit(group.orbitIndex);
                          }}
                          title="Split this merged orbit back into separate orbits"
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
                        title="Move toward center (inner orbit)"
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
                        title="Move toward periphery (outer orbit)"
                      >
                        <ChevronDownIcon size={10} />
                      </button>
                      <button
                        type="button"
                        className={`orbit-visibility-btn ${isOrbitHidden ? 'is-hidden' : ''}`}
                        onClick={(e) => {
                          e.stopPropagation();
                          dispatch({ type: 'TOGGLE_ORBIT_VISIBILITY', levelIndex: group.levelIndex });
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
                      onMouseEnter={() =>
                        isMerged &&
                        !isOrbitHidden &&
                        dispatch({ type: 'HIGHLIGHT_ORBIT_NODES', nodeIds: item.nodeIds })
                      }
                      onMouseLeave={() =>
                        isMerged &&
                        !isOrbitHidden &&
                        dispatch({ type: 'HIGHLIGHT_ORBIT_NODES', nodeIds: allNodeIds })
                      }
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
                            title={`Drag ${k.kind} onto another orbit to move all ${k.kind} entities there`}
                          >
                            <span className="orbit-kind-chip-name">{k.kind}</span>
                            <span className="orbit-kind-chip-count">{k.count}</span>
                            {k.overridden ? (
                              <button
                                type="button"
                                className="orbit-kind-chip-clear"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  applyOrbitCfg(moveKindToOrbit(orbitConfig, k.kind, null), [item.levelIndex]);
                                }}
                                title="Restore the default orbit for this kind"
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
              <span>Periphery</span>
            </div>
          </>
        ) : (
          <div style={{ padding: '14px 12px', fontSize: '11px', color: '#94a3b8', lineHeight: 1.45 }}>
            Concentric orbit rings and tier reordering are active when a <strong>Concentric</strong> layout algorithm is
            selected above.
          </div>
        )}
      </div>
    </FloatingPanel>
  );

  return (
    <div
      className="domain-dock-panel-right"
      style={{
        display: 'flex',
        flexDirection: 'column',
        width: '100%',
        height: '100vh',
        minHeight: 0,
        background: '#0b0f19',
        overflow: 'hidden',
      }}
    >
      {/* Active View Context Banner */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '6px 10px',
          background: 'rgba(15, 23, 42, 0.75)',
          borderBottom: '1px solid rgba(255, 255, 255, 0.06)',
          fontSize: '11px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px', minWidth: 0 }}>
          <span style={{ color: '#38bdf8', fontWeight: 700, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
            {state.hudTitle || 'Architecture Map'}
          </span>
          {state.hudLabel && (
            <span style={{ color: '#64748b', fontSize: '10px', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
              • {state.hudLabel}
            </span>
          )}
        </div>
        <button
          type="button"
          className="domain-hud-btn"
          style={{ padding: '2px 6px', fontSize: '10px', height: '20px' }}
          onClick={() => getVsCodeApi()?.postMessage({ type: 'MOVE_INSPECTOR_VIEW' })}
          title="Move Inspector panel to Secondary Side Bar (Right, next to Chat) or Primary Side Bar"
        >
          ⇄ Dock
        </button>
      </div>

      {/* Unified Tab Switcher Header */}
      <div className="domain-sidebar-tab-bar">
        <div className="domain-sidebar-tabs" style={{ width: '100%' }}>
          <button
            type="button"
            className={`domain-sidebar-tab ${effectiveSidebarTab === 'info' ? 'is-active' : ''}`}
            onClick={() => dispatch({ type: 'SET_SIDEBAR_TAB', tab: 'info' })}
            title="Inspect Selected Microservice, Database, or Edge"
          >
            <InfoIcon size={12} />
            <span>Info</span>
          </button>
          {hasOrbitLegend && (
            <button
              type="button"
              className={`domain-sidebar-tab ${effectiveSidebarTab === 'orbits' ? 'is-active' : ''}`}
              onClick={() => dispatch({ type: 'SET_SIDEBAR_TAB', tab: 'orbits' })}
              title="Concentric Orbits Legend and Layout Parameters"
            >
              <LayersIcon size={12} />
              <span>Orbits</span>
              <span className="tab-badge">{orbitLegendItems.length}</span>
            </button>
          )}
          <button
            type="button"
            className={`domain-sidebar-tab ${effectiveSidebarTab === 'visibility' ? 'is-active' : ''}`}
            onClick={() => dispatch({ type: 'SET_SIDEBAR_TAB', tab: 'visibility' })}
            title="Visibility, Entity Filters, Node/Edge Style, and Architectural Presets"
          >
            <EyeIcon size={12} />
            <span>Visibility</span>
            {hiddenCount > 0 && <span className="tab-badge">{hiddenCount}</span>}
          </button>
        </div>
      </div>

      {/* Tab Content Body */}
      <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>
        {effectiveSidebarTab === 'info' && (
          <DomainInspector
            selectedNode={selectedNode}
            selectedEdge={selectedEdge}
            nodeDetailMap={nodeDetailMap}
            rawEdges={rawEdges}
            isInspectorCollapsed={isInspectorCollapsed}
            onToggleCollapse={(collapsed) => dispatch({ type: 'SET_INSPECTOR_COLLAPSED', collapsed })}
            isEdgeInspectorCollapsed={isEdgeInspectorCollapsed}
            onToggleEdgeCollapse={(collapsed) => dispatch({ type: 'SET_EDGE_INSPECTOR_COLLAPSED', collapsed })}
            onSelectNode={(node) => dispatch({ type: 'SELECT_NODE', node })}
            onSelectNodeById={(id) => dispatch({ type: 'SELECT_NODE_BY_ID', id })}
            onSelectEdge={(edge) => dispatch({ type: 'SELECT_EDGE', edge })}
            onClearSelection={() => dispatch({ type: 'CLEAR_SELECTION' })}
            onClearGraphHighlight={() => dispatch({ type: 'CLEAR_GRAPH_HIGHLIGHT' })}
            onHideNode={(id) => dispatch({ type: 'HIDE_NODE', id })}
            onUngroupNode={(node) => dispatch({ type: 'UNGROUP_NODE', node })}
            onRemoveFromGroup={(groupId, memberId) => dispatch({ type: 'REMOVE_FROM_GROUP', groupId, memberId })}
            onFocusInFlow={(projectName) => dispatch({ type: 'FOCUS_IN_FLOW', projectName })}
            onOpenProjectMap={(projectName) => dispatch({ type: 'OPEN_PROJECT_MAP', projectName })}
            onOpenFile={(filePath, lineStart) =>
              getVsCodeApi()?.postMessage({ type: 'OPEN_FILE', filePath, lineStart })
            }
            onOpenOverrideModal={(name) => {
              setOverrideTargetService(name);
              setOverrideModalOpen(true);
            }}
            docked={true}
          />
        )}
        {effectiveSidebarTab === 'orbits' && renderOrbitLegend()}
        {effectiveSidebarTab === 'visibility' && (
          <DomainVisibilityPanel
            nodeDetailMap={nodeDetailMap}
            echelonMap={echelonMap}
            hiddenNodeIds={hiddenNodeIds}
            hiddenTypes={hiddenTypes}
            hiddenOrbitTiers={hiddenOrbitTiers}
            hideSingleConnectionDbs={hideSingleConnectionDbs}
            hideIsolatedNodes={hideIsolatedNodes}
            showTransitiveEdges={showTransitiveEdges}
            counts={counts}
            onUnhideNode={(id) => dispatch({ type: 'UNHIDE_NODE', id })}
            onRestoreAll={() => dispatch({ type: 'RESTORE_ALL_HIDDEN' })}
            onToggleType={(kind) => dispatch({ type: 'TOGGLE_TYPE_VISIBILITY', kind })}
            onToggleSingleConnectionDbs={() => dispatch({ type: 'TOGGLE_SINGLE_CONN_DBS' })}
            onToggleIsolatedNodes={() => dispatch({ type: 'TOGGLE_ISOLATED_NODES' })}
            onToggleTransitiveEdges={() => dispatch({ type: 'TOGGLE_TRANSITIVE_EDGES' })}
            isCollapsed={false}
            docked={true}
            groupedTypes={groupedTypes}
            onToggleTypeGrouping={(kind) => dispatch({ type: 'TOGGLE_TYPE_GROUPING', kind })}
            customGroups={customGroups}
            onCreateCustomGroup={(name, nodeIds) => dispatch({ type: 'CREATE_CUSTOM_GROUP', name, nodeIds })}
            onToggleCustomGroupCollapse={(groupId) => dispatch({ type: 'TOGGLE_CUSTOM_GROUP_COLLAPSE', groupId })}
            onDeleteCustomGroup={(groupId) => dispatch({ type: 'DELETE_CUSTOM_GROUP', groupId })}
            onSelectGroupNode={(groupId) => dispatch({ type: 'SELECT_NODE_BY_ID', id: groupId })}
            customPresetsSlot={customPresetsSlot}
            appearanceSection={
              !isMatrixMode ? (
                <div className="visibility-section">
                  <div className="visibility-section-header">
                    <span className="visibility-section-title">Node & Edge Style</span>
                    <span className="visibility-section-hint">Representation, sizing & labels</span>
                  </div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', marginTop: '4px' }}>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
                      {/* Node Content Mode */}
                      <div className="display-setting-row" title="Choose what is displayed inside each node shape">
                        <div className="setting-label-row">
                          <span className="setting-icon">🎨</span>
                          <span className="setting-name">Node Content</span>
                        </div>
                        <select
                          value={nodeContentMode}
                          onChange={(e) =>
                            dispatch({ type: 'SET_NODE_CONTENT_MODE', mode: e.target.value as NodeContentMode })
                          }
                          title="Node Content: Clean Icon, Icon + 3-letter Code, or Metric"
                          className="domain-layout-dropdown"
                          style={{ width: '100%', marginTop: '3px' }}
                        >
                          <option value="icon">✨ Icon Only</option>
                          <option value="icon-code">🏷️ Icon + Code</option>
                          <option value="metric">📊 Icon + Metric</option>
                        </select>
                      </div>

                      {/* Node Sizing Mode */}
                      <div className="display-setting-row" title="Scale node size uniformly, by √Weight, or by degree">
                        <div className="setting-label-row">
                          <span className="setting-icon">📏</span>
                          <span className="setting-name">Node Size</span>
                        </div>
                        <select
                          value={nodeSizingMode}
                          onChange={(e) =>
                            dispatch({ type: 'SET_NODE_SIZING_MODE', mode: e.target.value as NodeSizingMode })
                          }
                          title="Node Size: Uniform, √Weight (Files/Projects), or Connections (Degree)"
                          className="domain-layout-dropdown"
                          style={{ width: '100%', marginTop: '3px' }}
                        >
                          <option value="uniform">⚪ Uniform</option>
                          <option value="sqrt-weight">📐 √Weight</option>
                          <option value="degree">🔗 Degree</option>
                        </select>
                      </div>
                    </div>

                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
                      {/* Edge Curve Mode */}
                      <div
                        className="display-setting-row"
                        title="Line Style: Straight, Bezier curve, or Bypass Inner Orbits"
                      >
                        <div className="setting-label-row">
                          <span className="setting-icon">〰️</span>
                          <span className="setting-name">Line Style</span>
                        </div>
                        <select
                          value={edgeCurveMode}
                          onChange={(e) =>
                            dispatch({ type: 'SET_EDGE_CURVE_MODE', mode: e.target.value as EdgeCurveMode })
                          }
                          title="Line Style: Straight, Bezier curve, or Bypass Inner Orbits"
                          className="domain-layout-dropdown"
                          style={{ width: '100%', marginTop: '3px' }}
                        >
                          <option value="bezier">〰️ Bezier</option>
                          <option value="straight">📏 Straight</option>
                          <option value="avoid-inner">🛡️ Bypass Orbits</option>
                        </select>
                      </div>

                      {/* Edge Labels Visibility */}
                      <div
                        className="display-setting-row"
                        title="Control when edge text labels are shown: always or only on hover"
                      >
                        <div className="setting-label-row">
                          <span className="setting-icon">🏷️</span>
                          <span className="setting-name">Edge Labels</span>
                        </div>
                        <select
                          value={edgeLabelsOnHover ? 'hover' : 'always'}
                          onChange={(e) =>
                            dispatch({ type: 'SET_EDGE_LABELS_ON_HOVER', onHover: e.target.value === 'hover' })
                          }
                          title="Edge Labels visibility"
                          className="domain-layout-dropdown"
                          style={{ width: '100%', marginTop: '3px' }}
                        >
                          <option value="always">Always Show</option>
                          <option value="hover">On Hover Only</option>
                        </select>
                      </div>
                    </div>

                    {/* Font Size Control */}
                    <div className="display-setting-row" title="Adjust graph label font size (7px - 24px)">
                      <div className="setting-label-row">
                        <span className="setting-icon">🔤</span>
                        <span className="setting-name">Font Size</span>
                        <span
                          className="domain-hud-value-badge"
                          onClick={() => dispatch({ type: 'SET_FONT_SIZE', fontSize: 10 })}
                          title="Click to reset font size to 10px"
                        >
                          {fontSize}px
                        </span>
                      </div>
                      <div className="setting-slider-row">
                        <button
                          type="button"
                          className="domain-hud-step-btn"
                          onClick={() => dispatch({ type: 'SET_FONT_SIZE', fontSize: fontSize - 1 })}
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
                          onChange={(e) => dispatch({ type: 'SET_FONT_SIZE', fontSize: parseInt(e.target.value, 10) })}
                          className="domain-hud-slider"
                        />
                        <button
                          type="button"
                          className="domain-hud-step-btn"
                          onClick={() => dispatch({ type: 'SET_FONT_SIZE', fontSize: fontSize + 1 })}
                          title="Increase font size (+1px)"
                        >
                          +
                        </button>
                      </div>
                    </div>

                    {/* Edge Curvature Control */}
                    {edgeCurveMode !== 'straight' && (
                      <div className="display-setting-row" title="Adjust edge curvature (-200px to +200px)">
                        <div className="setting-label-row">
                          <span className="setting-icon">〰️</span>
                          <span className="setting-name">Edge Curvature</span>
                          <span
                            className="domain-hud-value-badge"
                            onClick={() => dispatch({ type: 'SET_CURVE_FACTOR', curveFactor: 35 })}
                            title="Click to reset curve to +35px"
                          >
                            {curveFactor > 0 ? `+${curveFactor}` : curveFactor}px
                          </span>
                        </div>
                        <div className="setting-slider-row">
                          <button
                            type="button"
                            className="domain-hud-step-btn"
                            onClick={() =>
                              dispatch({ type: 'SET_CURVE_FACTOR', curveFactor: Math.max(-200, curveFactor - 10) })
                            }
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
                            onChange={(e) =>
                              dispatch({ type: 'SET_CURVE_FACTOR', curveFactor: parseInt(e.target.value, 10) })
                            }
                            className="domain-hud-slider"
                          />
                          <button
                            type="button"
                            className="domain-hud-step-btn"
                            onClick={() =>
                              dispatch({ type: 'SET_CURVE_FACTOR', curveFactor: Math.min(200, curveFactor + 10) })
                            }
                            title="Increase curve (+10px)"
                          >
                            +
                          </button>
                        </div>
                      </div>
                    )}

                    {/* Mouse Wheel Zoom Sensitivity */}
                    <div className="display-setting-row" title="Adjust mouse wheel zoom sensitivity">
                      <div className="setting-label-row">
                        <span className="setting-icon">🖱️</span>
                        <span className="setting-name">Wheel Sensitivity</span>
                        <span
                          className="domain-hud-value-badge"
                          onClick={() => dispatch({ type: 'SET_WHEEL_SENSITIVITY', wheelSensitivity: 1.0 })}
                          title="Click to reset sensitivity to 1.0x"
                        >
                          {wheelSensitivity.toFixed(1)}x
                        </span>
                      </div>
                      <div className="setting-slider-row">
                        <button
                          type="button"
                          className="domain-hud-step-btn"
                          onClick={() =>
                            dispatch({
                              type: 'SET_WHEEL_SENSITIVITY',
                              wheelSensitivity: Math.max(0.2, +(wheelSensitivity - 0.2).toFixed(1)),
                            })
                          }
                          title="Decrease sensitivity"
                        >
                          −
                        </button>
                        <input
                          type="range"
                          min="0.2"
                          max="3.0"
                          step="0.1"
                          value={wheelSensitivity}
                          onChange={(e) =>
                            dispatch({ type: 'SET_WHEEL_SENSITIVITY', wheelSensitivity: parseFloat(e.target.value) })
                          }
                          className="domain-hud-slider"
                        />
                        <button
                          type="button"
                          className="domain-hud-step-btn"
                          onClick={() =>
                            dispatch({
                              type: 'SET_WHEEL_SENSITIVITY',
                              wheelSensitivity: Math.min(3.0, +(wheelSensitivity + 0.2).toFixed(1)),
                            })
                          }
                          title="Increase sensitivity"
                        >
                          +
                        </button>
                      </div>
                    </div>
                  </div>
                </div>
              ) : undefined
            }
          />
        )}
      </div>

      <DomainOverrideModal
        isOpen={overrideModalOpen}
        serviceName={overrideTargetService}
        currentDomain={selectedNode?.displayName}
        availableDomains={availableDomains}
        onSave={(svc, dom, ctx) => dispatch({ type: 'SET_DOMAIN_OVERRIDE', serviceName: svc, domain: dom, context: ctx, reset: false })}
        onReset={(svc) => dispatch({ type: 'SET_DOMAIN_OVERRIDE', serviceName: svc, domain: '', reset: true })}
        onClose={() => setOverrideModalOpen(false)}
      />
    </div>
  );
};
