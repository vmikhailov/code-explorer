import React, { useState, useEffect, useMemo, useCallback } from 'react';
import {
  OrbitConfig,
  DEFAULT_ORBIT_CONFIG,
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
import { DomainOrbitsPanel } from '../domain/components/DomainOrbitsPanel';
import { DomainVisibilityPanel } from '../domain/components/DomainVisibilityPanel';
import {
  DomainSettingsPanel,
  ProjectMapPresetsState,
} from '../domain/components/DomainSettingsPanel';
import { DomainOverrideModal } from './DomainOverrideModal';
import {
  InfoIcon,
  LayersIcon,
  EyeIcon,
  SettingsIcon,
} from './Icons';
import { getVsCodeApi } from '../vscodeApi';

export type { ProjectMapPresetsState };

export interface InspectorSyncedState {
  hudLabel?: string;
  hudTitle?: string;
  isMatrixMode: boolean;
  effectiveSidebarTab: 'info' | 'orbits' | 'visibility' | 'settings';
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
  // Settings & Geometry
  layoutName: DomainLayoutName;
  spacingFactor: number;
  orbitDistance: number;
  minOrbitNodeDistance: number;
  autoRelayoutOnFilter: boolean;
  orbitConfig: OrbitConfig;
  hiddenOrbitTiers: number[];
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
  baseIconSize: number;
  fontSize: number;
  curveFactor: number;
  wheelSensitivity: number;
  projectMapPresets?: ProjectMapPresetsState;
}

export type InspectorAction =
  | { type: 'SET_SIDEBAR_TAB'; tab: 'info' | 'orbits' | 'visibility' | 'settings' }
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
  | { type: 'SET_BASE_ICON_SIZE'; size: number }
  | { type: 'SET_FONT_SIZE'; fontSize: number }
  | { type: 'SET_CURVE_FACTOR'; curveFactor: number }
  | { type: 'SET_WHEEL_SENSITIVITY'; wheelSensitivity: number }
  | { type: 'TOGGLE_PM_PRESET'; preset: 'cyclesOnly' | 'hideUtilities' | 'hideResources' };

export const InspectorSidebarView: React.FC = () => {
  const [state, setState] = useState<InspectorSyncedState | null>(null);
  const [overrideModalOpen, setOverrideModalOpen] = useState(false);
  const [overrideTargetService, setOverrideTargetService] = useState('');

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
          color: 'var(--vscode-sideBar-foreground, #94a3b8)',
          background: 'var(--vscode-sideBar-background, #181818)',
          gap: '12px',
        }}
      >
        <div style={{ fontSize: '28px', opacity: 0.75 }}>🪐</div>
        <div style={{ fontSize: '13px', fontWeight: 600, color: '#e2e8f0' }}>
          Graph Inspector & Settings
        </div>
        <div style={{ fontSize: '11.5px', lineHeight: 1.5, maxWidth: '240px' }}>
          Open an Architecture Map or Project Map tab to inspect nodes, configure orbits, and adjust settings here.
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
    baseIconSize = 52,
    fontSize,
    curveFactor,
    wheelSensitivity,
    isMatrixMode,
    projectMapPresets,
  } = state;

  const currentTab = effectiveSidebarTab || 'info';
  const isSwimlanes = layoutName === 'swimlanes';
  const unitName = isSwimlanes ? 'Lane' : 'Orbit';
  const unitsName = isSwimlanes ? 'Lanes' : 'Orbits';

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
        title={`Hide foundation/utility modules (${unitName} 4)`}
      >
        <div className="visibility-preset-main">
          <span className="visibility-preset-icon">🧰</span>
          <div className="visibility-preset-text">
            <span className="visibility-preset-label">Hide Utilities</span>
            <span className="visibility-preset-desc">Foundation & helper modules ({unitName} 4)</span>
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

  return (
    <div
      className="domain-dock-panel-right"
      style={{
        display: 'flex',
        flexDirection: 'column',
        width: '100%',
        height: '100vh',
        minHeight: 0,
        background: 'var(--vscode-sideBar-background, #181818)',
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
          background: 'var(--vscode-sideBarSectionHeader-background, rgba(255, 255, 255, 0.03))',
          borderBottom: '1px solid var(--vscode-sideBar-border, var(--vscode-widget-border, rgba(255, 255, 255, 0.08)))',
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
          className="domain-dock-toggle-btn"
          onClick={() => getVsCodeApi()?.postMessage({ type: 'MOVE_INSPECTOR_VIEW' })}
          title="Move this panel to the Secondary Side Bar (Right, next to AI Chat) or Primary Side Bar"
        >
          ⇄ Dock
        </button>
      </div>

      {/* Unified Tab Switcher Header: Info, Orbits, Visibility, Settings */}
      <div className="domain-sidebar-tab-bar">
        <div className="domain-sidebar-tabs" style={{ width: '100%' }}>
          <button
            type="button"
            className={`domain-sidebar-tab ${currentTab === 'info' ? 'is-active' : ''}`}
            onClick={() => dispatch({ type: 'SET_SIDEBAR_TAB', tab: 'info' })}
            title="Inspect Selected Microservice, Database, or Edge"
          >
            <InfoIcon size={12} />
            <span>Info</span>
          </button>
          {hasOrbitLegend && (
            <button
              type="button"
              className={`domain-sidebar-tab ${currentTab === 'orbits' ? 'is-active' : ''}`}
              onClick={() => dispatch({ type: 'SET_SIDEBAR_TAB', tab: 'orbits' })}
              title={
                isSwimlanes
                  ? 'Swimlane Hierarchy and Column Arrangement'
                  : 'Concentric Orbits Hierarchy and Tier Arrangement'
              }
            >
              <LayersIcon size={12} />
              <span>{unitsName}</span>
              {orbitLegendItems.length > 0 && <span className="tab-badge">{orbitLegendItems.length}</span>}
            </button>
          )}
          <button
            type="button"
            className={`domain-sidebar-tab ${currentTab === 'visibility' ? 'is-active' : ''}`}
            onClick={() => dispatch({ type: 'SET_SIDEBAR_TAB', tab: 'visibility' })}
            title="Visibility Filters, Entity Toggles, and Grouping"
          >
            <EyeIcon size={12} />
            <span>Visibility</span>
            {hiddenCount > 0 && <span className="tab-badge">{hiddenCount}</span>}
          </button>
          <button
            type="button"
            className={`domain-sidebar-tab ${currentTab === 'settings' ? 'is-active' : ''}`}
            onClick={() => dispatch({ type: 'SET_SIDEBAR_TAB', tab: 'settings' })}
            title="Graph Geometry, Layout Spacing, Node & Edge Appearance Settings"
          >
            <SettingsIcon size={12} />
            <span>Settings</span>
          </button>
        </div>
      </div>

      {/* Tab Content Body */}
      <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>
        {currentTab === 'info' && (
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
        {currentTab === 'orbits' && (
          <DomainOrbitsPanel
            hasOrbitLegend={hasOrbitLegend}
            orbitLegendItems={orbitLegendItems}
            orbitConfig={orbitConfig}
            hiddenOrbitTiers={hiddenOrbitTiers}
            layoutName={layoutName}
            onApplyOrbitConfig={(next, clearHiddenFor, resetHidden) =>
              dispatch({
                type: 'APPLY_ORBIT_CONFIG',
                next,
                clearHiddenFor: clearHiddenFor ? clearHiddenFor.map(Number) : [],
                resetHidden,
              })
            }
            onToggleOrbitVisibility={(levelIndex) => dispatch({ type: 'TOGGLE_ORBIT_VISIBILITY', levelIndex })}
            onHighlightOrbitNodes={(nodeIds) => dispatch({ type: 'HIGHLIGHT_ORBIT_NODES', nodeIds })}
            onClearOrbitHighlight={() => dispatch({ type: 'CLEAR_ORBIT_HIGHLIGHT' })}
            projectMapPresets={projectMapPresets}
            isMatrixMode={isMatrixMode}
          />
        )}
        {currentTab === 'visibility' && (
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
            onToggleCustomGroupCollapse={(groupId) =>
              dispatch({ type: 'TOGGLE_CUSTOM_GROUP_COLLAPSE', groupId })
            }
            onDeleteCustomGroup={(groupId) => dispatch({ type: 'DELETE_CUSTOM_GROUP', groupId })}
            onSelectGroupNode={(groupId) => dispatch({ type: 'SELECT_NODE_BY_ID', id: groupId })}
            customPresetsSlot={customPresetsSlot}
          />
        )}
        {currentTab === 'settings' && (
          <DomainSettingsPanel
            layoutName={layoutName}
            onSetLayout={(name) => dispatch({ type: 'SET_LAYOUT', layoutName: name })}
            spacingFactor={spacingFactor}
            orbitDistance={orbitDistance}
            minOrbitNodeDistance={minOrbitNodeDistance}
            autoRelayoutOnFilter={autoRelayoutOnFilter}
            onSetSpacing={(factor) => dispatch({ type: 'SET_SPACING', spacingFactor: factor })}
            onSetOrbitDistance={(dist) => dispatch({ type: 'SET_ORBIT_DISTANCE', orbitDistance: dist })}
            onSetMinOrbitNodeDistance={(dist) =>
              dispatch({ type: 'SET_MIN_ORBIT_NODE_DISTANCE', minOrbitNodeDistance: dist })
            }
            onSetAutoRelayout={(enabled) => dispatch({ type: 'SET_AUTO_RELAYOUT', enabled })}
            onForceRelayout={() => dispatch({ type: 'FORCE_RELAYOUT' })}
            nodeContentMode={nodeContentMode}
            nodeSizingMode={nodeSizingMode}
            edgeCurveMode={edgeCurveMode}
            edgeLabelsOnHover={edgeLabelsOnHover}
            baseIconSize={baseIconSize}
            fontSize={fontSize}
            curveFactor={curveFactor}
            wheelSensitivity={wheelSensitivity}
            onSetNodeContentMode={(mode) => dispatch({ type: 'SET_NODE_CONTENT_MODE', mode })}
            onSetNodeSizingMode={(mode) => dispatch({ type: 'SET_NODE_SIZING_MODE', mode })}
            onSetEdgeCurveMode={(mode) => dispatch({ type: 'SET_EDGE_CURVE_MODE', mode })}
            onSetEdgeLabelsOnHover={(onHover) => dispatch({ type: 'SET_EDGE_LABELS_ON_HOVER', onHover })}
            onSetBaseIconSize={(size) => dispatch({ type: 'SET_BASE_ICON_SIZE', size })}
            onSetFontSize={(size) => dispatch({ type: 'SET_FONT_SIZE', fontSize: size })}
            onSetCurveFactor={(factor) => dispatch({ type: 'SET_CURVE_FACTOR', curveFactor: factor })}
            onSetWheelSensitivity={(sens) =>
              dispatch({ type: 'SET_WHEEL_SENSITIVITY', wheelSensitivity: sens })
            }
            isMatrixMode={isMatrixMode}
          />
        )}
      </div>

      <DomainOverrideModal
        isOpen={overrideModalOpen}
        serviceName={overrideTargetService}
        currentDomain={selectedNode?.displayName}
        availableDomains={state.availableDomains}
        onSave={(svc, dom, ctx) =>
          dispatch({
            type: 'SET_DOMAIN_OVERRIDE',
            serviceName: svc,
            domain: dom,
            context: ctx,
            reset: false,
          })
        }
        onReset={(svc) =>
          dispatch({
            type: 'SET_DOMAIN_OVERRIDE',
            serviceName: svc,
            domain: '',
            reset: true,
          })
        }
        onClose={() => setOverrideModalOpen(false)}
      />
    </div>
  );
};
