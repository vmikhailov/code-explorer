import React, { useState, useMemo, useRef, useCallback } from 'react';
import {
  OrbitConfig,
  DEFAULT_ORBIT_CONFIG,
  DEFAULT_PROJECT_MAP_ORBIT_CONFIG,
  isDefaultOrbitConfig,
  mergeOrbits,
  splitOrbit,
  moveOrbit,
  moveKindToOrbit,
} from '../../layout/orbitConfig';
import {
  DomainLayoutName,
  EdgeCurveMode,
  OrbitLegendItem,
  SelectedNodeDetail,
  EntityKind,
} from '../../components/DomainArchitectureView';
import { CustomNodeGroup, NodeContentMode, NodeSizingMode } from '../types';
import { EntityNodeBadge, getEntityBadgeMeta } from '../entityBadges';
import { matchesSearchPattern } from '../searchUtils';
import { FloatingPanel } from '../../components/FloatingPanel';
import {
  LayersIcon,
  RotateCcwIcon,
  ChevronUpIcon,
  ChevronDownIcon,
  ChevronRightIcon,
  EyeIcon,
  EyeOffIcon,
  SettingsIcon,
  XIcon,
} from '../../components/Icons';

export interface ProjectMapPresetsState {
  cyclesCount: number;
  cyclesOnly: boolean;
  hideUtilities: boolean;
  hideResources: boolean;
}

export interface DomainSettingsPanelProps {
  layoutName: DomainLayoutName;
  spacingFactor: number;
  orbitDistance: number;
  minOrbitNodeDistance: number;
  autoRelayoutOnFilter: boolean;
  onSetSpacing: (factor: number) => void;
  onSetOrbitDistance: (dist: number) => void;
  onSetMinOrbitNodeDistance: (dist: number) => void;
  onSetAutoRelayout: (enabled: boolean) => void;
  onForceRelayout: () => void;

  nodeContentMode: NodeContentMode;
  nodeSizingMode: NodeSizingMode;
  edgeCurveMode: EdgeCurveMode;
  edgeLabelsOnHover: boolean;
  fontSize: number;
  curveFactor: number;
  wheelSensitivity: number;
  onSetNodeContentMode: (mode: NodeContentMode) => void;
  onSetNodeSizingMode: (mode: NodeSizingMode) => void;
  onSetEdgeCurveMode: (mode: EdgeCurveMode) => void;
  onSetEdgeLabelsOnHover: (onHover: boolean) => void;
  onSetFontSize: (size: number) => void;
  onSetCurveFactor: (factor: number) => void;
  onSetWheelSensitivity: (sens: number) => void;

  projectMapPresets?: ProjectMapPresetsState;
  onToggleProjectMapPreset?: (preset: 'cyclesOnly' | 'hideUtilities' | 'hideResources') => void;
  hideSingleConnectionDbs?: boolean;
  hideIsolatedNodes?: boolean;
  showTransitiveEdges?: boolean;
  onToggleSingleConnectionDbs: () => void;
  onToggleIsolatedNodes: () => void;
  onToggleTransitiveEdges: () => void;

  nodeDetailMap: Map<string, SelectedNodeDetail>;
  echelonMap?: Map<string, number>;
  counts?: {
    singleConnDbs?: number;
    sharedDbs?: number;
    transitiveCalls?: number;
    [key: string]: any;
  };
  hiddenNodeIds: Set<string>;
  hiddenTypes: Set<EntityKind>;
  onToggleType: (kind: EntityKind) => void;
  onUnhideNode: (id: string) => void;
  onRestoreAll: () => void;

  groupedTypes: Set<EntityKind>;
  onToggleTypeGrouping: (kind: EntityKind) => void;
  customGroups: CustomNodeGroup[];
  onCreateCustomGroup: (name: string, nodeIds: string[]) => void;
  onToggleCustomGroupCollapse: (groupId: string) => void;
  onDeleteCustomGroup: (groupId: string) => void;
  onSelectGroupNode?: (groupId: string) => void;

  hasOrbitLegend: boolean;
  orbitLegendItems: OrbitLegendItem[];
  orbitConfig: OrbitConfig;
  hiddenOrbitTiers: Set<number>;
  onApplyOrbitConfig: (config: OrbitConfig, affectedNodeIds?: string[], resetHiddenTiers?: boolean) => void;
  onToggleOrbitVisibility: (levelIndex: number) => void;
  onHighlightOrbitNodes?: (nodeIds: string[]) => void;
  onClearOrbitHighlight?: () => void;

  isMatrixMode?: boolean;
}

interface HiddenItem {
  id: string;
  label: string;
  kind: EntityKind;
  kindTag: string;
  badgeColor: string;
  reasonTag: string;
}

export const DomainSettingsPanel: React.FC<DomainSettingsPanelProps> = (props) => {
  const {
    layoutName,
    spacingFactor,
    orbitDistance,
    minOrbitNodeDistance,
    autoRelayoutOnFilter,
    onSetSpacing,
    onSetOrbitDistance,
    onSetMinOrbitNodeDistance,
    onSetAutoRelayout,
    onForceRelayout,
    nodeContentMode,
    nodeSizingMode,
    edgeCurveMode,
    edgeLabelsOnHover,
    fontSize,
    curveFactor,
    wheelSensitivity,
    onSetNodeContentMode,
    onSetNodeSizingMode,
    onSetEdgeCurveMode,
    onSetEdgeLabelsOnHover,
    onSetFontSize,
    onSetCurveFactor,
    onSetWheelSensitivity,
    projectMapPresets,
    onToggleProjectMapPreset,
    hideSingleConnectionDbs = false,
    hideIsolatedNodes = false,
    showTransitiveEdges = false,
    onToggleSingleConnectionDbs,
    onToggleIsolatedNodes,
    onToggleTransitiveEdges,
    nodeDetailMap,
    counts,
    hiddenNodeIds,
    hiddenTypes,
    onToggleType,
    onUnhideNode,
    onRestoreAll,
    groupedTypes,
    onToggleTypeGrouping,
    customGroups,
    onCreateCustomGroup,
    onToggleCustomGroupCollapse,
    onDeleteCustomGroup,
    onSelectGroupNode,
    hasOrbitLegend,
    orbitLegendItems,
    orbitConfig,
    hiddenOrbitTiers,
    onApplyOrbitConfig,
    onToggleOrbitVisibility,
    onHighlightOrbitNodes,
    onClearOrbitHighlight,
    isMatrixMode = false,
  } = props;

  // Collapsible section state
  const [collapsedSections, setCollapsedSections] = useState<Record<string, boolean>>({});
  const toggleSection = (id: string) => {
    setCollapsedSections((prev) => ({ ...prev, [id]: !prev[id] }));
  };

  // Group creation state
  const [isCreatingGroup, setIsCreatingGroup] = useState(false);
  const [newGroupName, setNewGroupName] = useState('');
  const [groupEntitySearch, setGroupEntitySearch] = useState('');
  const [groupSelectedNodeIds, setGroupSelectedNodeIds] = useState<Set<string>>(new Set());

  // Hidden entity search state
  const [hiddenSearchQuery, setHiddenSearchQuery] = useState('');

  // Orbits drag & selection state
  const [draggedOrbitIndex, setDraggedOrbitIndex] = useState<number | null>(null);
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);
  const [selectedOrbitRows, setSelectedOrbitRows] = useState<number[]>([]);
  const draggedKindRef = useRef<string | null>(null);
  const lastSelectedOrbitRowRef = useRef<number | null>(null);

  // Derive entity types directly from nodeDetailMap
  const entityTypes = useMemo(() => {
    const countsByKind = new Map<EntityKind, number>();
    for (const node of nodeDetailMap.values()) {
      if (node.isGroup) continue;
      countsByKind.set(node.kind, (countsByKind.get(node.kind) ?? 0) + 1);
    }
    return Array.from(countsByKind.entries()).map(([kind, count]) => {
      const meta = getEntityBadgeMeta(kind);
      return {
        kind,
        label: meta.label,
        count,
      };
    });
  }, [nodeDetailMap]);

  // Derive hidden items with reasons
  const allHiddenItems = useMemo(() => {
    const items: HiddenItem[] = [];
    for (const [id, node] of nodeDetailMap.entries()) {
      let isHidden = false;
      let reason = 'Manual';

      if (hiddenNodeIds.has(id)) {
        isHidden = true;
        reason = 'Manual';
      } else if (hiddenTypes.has(node.kind)) {
        isHidden = true;
        reason = `${node.kind} Filter`;
      } else if (node.tier !== undefined && hiddenOrbitTiers.has(node.tier)) {
        isHidden = true;
        reason = `Tier ${node.tier + 1} Filter`;
      } else if (hideSingleConnectionDbs && node.kind === 'Database' && node.inboundCallsCount <= 1) {
        isHidden = true;
        reason = 'Single-Connection DB';
      } else if (
        hideIsolatedNodes &&
        node.inboundCallsCount === 0 &&
        node.outboundCallsCount === 0 &&
        node.dbCount === 0 &&
        node.messagingCount === 0
      ) {
        isHidden = true;
        reason = 'Isolated Node';
      }

      if (isHidden) {
        items.push({
          id,
          label: node.displayName || node.name,
          kind: node.kind,
          kindTag: node.displayTag || node.kind.substring(0, 3).toUpperCase(),
          badgeColor: node.bgColor || '#64748b',
          reasonTag: reason,
        });
      }
    }
    return items;
  }, [nodeDetailMap, hiddenNodeIds, hiddenTypes, hiddenOrbitTiers, hideSingleConnectionDbs, hideIsolatedNodes]);

  const filteredHiddenItems = useMemo(() => {
    if (!hiddenSearchQuery.trim()) return allHiddenItems;
    const q = hiddenSearchQuery.toLowerCase().trim();
    return allHiddenItems.filter(
      (item) =>
        item.label.toLowerCase().includes(q) ||
        item.kind.toLowerCase().includes(q) ||
        item.reasonTag.toLowerCase().includes(q)
    );
  }, [allHiddenItems, hiddenSearchQuery]);

  const hasAnyFiltersOrHidden =
    allHiddenItems.length > 0 ||
    hiddenTypes.size > 0 ||
    groupedTypes.size > 0 ||
    customGroups.length > 0 ||
    hideSingleConnectionDbs ||
    hideIsolatedNodes;

  // Available nodes for custom group creation (supports text tokens, *wildcards*, and /regex/)
  const availableNodesForGroup = useMemo(() => {
    const list: SelectedNodeDetail[] = [];
    const q = groupEntitySearch.trim();
    for (const [id, node] of nodeDetailMap.entries()) {
      if (hiddenNodeIds.has(id) || hiddenTypes.has(node.kind) || node.isGroup) continue;
      if (q) {
        const fields = [
          node.displayName,
          node.name,
          node.kind,
          node.displayTag,
          node.id,
        ];
        if (!matchesSearchPattern(fields, q)) continue;
      }
      list.push(node);
    }
    list.sort((a, b) => (a.displayName || a.name).localeCompare(b.displayName || b.name));
    return list;
  }, [nodeDetailMap, hiddenNodeIds, hiddenTypes, groupEntitySearch]);

  const areAllMatchingSelected = useMemo(() => {
    if (availableNodesForGroup.length === 0) return false;
    return availableNodesForGroup.every((node) => groupSelectedNodeIds.has(node.id));
  }, [availableNodesForGroup, groupSelectedNodeIds]);

  const handleToggleSelectAllMatching = useCallback(() => {
    setGroupSelectedNodeIds((prev) => {
      const next = new Set(prev);
      if (areAllMatchingSelected) {
        for (const node of availableNodesForGroup) {
          next.delete(node.id);
        }
      } else {
        for (const node of availableNodesForGroup) {
          next.add(node.id);
        }
      }
      return next;
    });
  }, [areAllMatchingSelected, availableNodesForGroup]);

  const handleCreateGroupSubmit = (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (groupSelectedNodeIds.size === 0 || !onCreateCustomGroup) return;
    const name = newGroupName.trim() || `Group (${groupSelectedNodeIds.size})`;
    onCreateCustomGroup(name, Array.from(groupSelectedNodeIds));
    setNewGroupName('');
    setGroupEntitySearch('');
    setGroupSelectedNodeIds(new Set());
    setIsCreatingGroup(false);
  };

  // Orbits groupings & actions
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
      onApplyOrbitConfig(next, clearHiddenFor.map(String), resetHidden);
    },
    [onApplyOrbitConfig]
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
    const defaultCfg = projectMapPresets ? DEFAULT_PROJECT_MAP_ORBIT_CONFIG : DEFAULT_ORBIT_CONFIG;
    applyOrbitCfg(defaultCfg, [], true);
  }, [applyOrbitCfg, projectMapPresets]);

  const isConcentric = layoutName.startsWith('concentric');

  return (
    <FloatingPanel
      dock="top-right"
      docked={true}
      isCollapsed={false}
      title="Settings"
      icon={<SettingsIcon size={14} />}
      badge={
        <span
          className="floating-dock-badge"
          style={{
            backgroundColor: allHiddenItems.length > 0 ? 'rgba(239, 68, 68, 0.2)' : 'rgba(16, 185, 129, 0.2)',
            color: allHiddenItems.length > 0 ? '#fca5a5' : '#6ee7b7',
            border: `1px solid ${allHiddenItems.length > 0 ? 'rgba(239, 68, 68, 0.35)' : 'rgba(16, 185, 129, 0.35)'}`,
            fontSize: '10px',
            padding: '1px 6px',
          }}
        >
          {allHiddenItems.length > 0 ? `${allHiddenItems.length} hidden` : 'All Visible'}
        </span>
      }
      headerActions={
        <>
          {hasAnyFiltersOrHidden ? (
            <button
              type="button"
              className="floating-dock-btn action-btn hidden-panel-restore-all-btn"
              onClick={onRestoreAll}
              title="Restore all hidden entities to graph"
            >
              <RotateCcwIcon size={11} />
              <span>Restore All</span>
            </button>
          ) : null}
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
          {(!isDefaultOrbitConfig(orbitConfig) || hiddenOrbitTiers.size > 0) && isConcentric ? (
            <button
              type="button"
              className="floating-dock-btn action-btn"
              onClick={(e) => {
                e.stopPropagation();
                handleResetOrbitOrder();
              }}
              title="Reset to default orbit order and show all orbits"
            >
              <RotateCcwIcon size={11} />
              <span>Reset Orbits</span>
            </button>
          ) : null}
          <button
            type="button"
            className="floating-dock-btn action-btn"
            onClick={(e) => {
              e.stopPropagation();
              onForceRelayout();
            }}
            title="Recalculate graph layout now"
          >
            <span>⟳ Re-layout</span>
          </button>
        </>
      }
      className="domain-settings-floating no-body-padding"
      width={360}
      minWidth={280}
      maxWidth={700}
      persistKey="ce_settings_panel"
    >
      <div
        className="domain-settings-panel-body"
        style={{
          display: 'flex',
          flexDirection: 'column',
          gap: '16px',
          padding: '12px 14px',
          flex: 1,
          minHeight: 0,
          overflowY: 'auto',
        }}
      >
        {/* 1. Layout & Geometry */}
        {!isMatrixMode && (
          <div className="visibility-section">
            <div
              className="visibility-section-header"
              style={{ cursor: 'pointer', userSelect: 'none' }}
              onClick={() => toggleSection('geometry')}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                {collapsedSections['geometry'] ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
                <span className="visibility-section-title">Layout & Spacing</span>
              </div>
              <span className="visibility-section-hint">Air, orbit distance, auto-recalc</span>
            </div>

            {!collapsedSections['geometry'] && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', paddingTop: '4px' }}>
                {/* Node Spacing / Air Control */}
                <div className="display-setting-row" title="Adjust node spacing / distance between orbits">
                  <div className="setting-label-row">
                    <span className="setting-icon">💨</span>
                    <span className="setting-name">Node Air (Spacing)</span>
                    <span
                      className="domain-hud-value-badge"
                      onClick={() => onSetSpacing(1.0)}
                      title="Click to reset air to 1.0x"
                    >
                      {spacingFactor.toFixed(1)}x
                    </span>
                  </div>
                  <div className="setting-slider-row">
                    <button
                      type="button"
                      className="domain-hud-step-btn"
                      onClick={() => onSetSpacing(Math.max(0.4, +(spacingFactor - 0.2).toFixed(1)))}
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
                      onChange={(e) => onSetSpacing(parseFloat(e.target.value))}
                      className="domain-hud-slider"
                    />
                    <button
                      type="button"
                      className="domain-hud-step-btn"
                      onClick={() => onSetSpacing(Math.min(3.5, +(spacingFactor + 0.2).toFixed(1)))}
                      title="Increase spacing"
                    >
                      +
                    </button>
                  </div>
                </div>

                {/* Orbit Distance Control (Concentric only) */}
                {isConcentric && (
                  <div className="display-setting-row" title="Adjust distance between concentric orbits (100px - 900px)">
                    <div className="setting-label-row">
                      <span className="setting-icon">🪐</span>
                      <span className="setting-name">Orbit Distance</span>
                      <span
                        className="domain-hud-value-badge"
                        onClick={() => onSetOrbitDistance(280)}
                        title="Click to reset orbit distance to 280px"
                      >
                        {orbitDistance}px
                      </span>
                    </div>
                    <div className="setting-slider-row">
                      <button
                        type="button"
                        className="domain-hud-step-btn"
                        onClick={() => onSetOrbitDistance(orbitDistance - 20)}
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
                        onChange={(e) => onSetOrbitDistance(parseInt(e.target.value, 10))}
                        className="domain-hud-slider"
                      />
                      <button
                        type="button"
                        className="domain-hud-step-btn"
                        onClick={() => onSetOrbitDistance(orbitDistance + 20)}
                        title="Increase orbit distance (+20px)"
                      >
                        +
                      </button>
                    </div>
                  </div>
                )}

                {/* Min Distance on Orbit Control (Concentric only) */}
                {isConcentric && (
                  <div
                    className="display-setting-row"
                    title="Adjust minimum distance between nodes on the same orbit (50px - 400px)"
                  >
                    <div className="setting-label-row">
                      <span className="setting-icon">↔️</span>
                      <span className="setting-name">Min Distance on Orbit</span>
                      <span
                        className="domain-hud-value-badge"
                        onClick={() => onSetMinOrbitNodeDistance(125)}
                        title="Click to reset min orbit node distance to 125px"
                      >
                        {minOrbitNodeDistance}px
                      </span>
                    </div>
                    <div className="setting-slider-row">
                      <button
                        type="button"
                        className="domain-hud-step-btn"
                        onClick={() => onSetMinOrbitNodeDistance(minOrbitNodeDistance - 15)}
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
                        onChange={(e) => onSetMinOrbitNodeDistance(parseInt(e.target.value, 10))}
                        className="domain-hud-slider"
                      />
                      <button
                        type="button"
                        className="domain-hud-step-btn"
                        onClick={() => onSetMinOrbitNodeDistance(minOrbitNodeDistance + 15)}
                        title="Increase min distance on orbit (+15px)"
                      >
                        +
                      </button>
                    </div>
                  </div>
                )}

                {/* Auto-recalc toggle & Re-layout button */}
                <div className="display-popover-actions" style={{ paddingTop: '4px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <label
                    className={`domain-hud-checkbox-label ${autoRelayoutOnFilter ? 'is-active' : ''}`}
                    title={
                      autoRelayoutOnFilter
                        ? 'Auto-relayout enabled: layout automatically refits when entities are hidden'
                        : 'Auto-relayout disabled: nodes are hidden in place without moving remaining nodes'
                    }
                  >
                    <input
                      type="checkbox"
                      className="domain-hud-checkbox"
                      checked={autoRelayoutOnFilter}
                      onChange={(e) => onSetAutoRelayout(e.target.checked)}
                    />
                    <span>🔄 Auto-recalc layout</span>
                  </label>
                  <button
                    type="button"
                    className="domain-hud-btn"
                    onClick={onForceRelayout}
                    title="Recalculate layout immediately"
                    style={{ padding: '2px 8px', fontSize: '11px', lineHeight: 1 }}
                  >
                    ⟳ Re-layout
                  </button>
                </div>
              </div>
            )}
          </div>
        )}

        {/* 2. Node & Edge Appearance */}
        {!isMatrixMode && (
          <div className="visibility-section">
            <div
              className="visibility-section-header"
              style={{ cursor: 'pointer', userSelect: 'none' }}
              onClick={() => toggleSection('appearance')}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                {collapsedSections['appearance'] ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
                <span className="visibility-section-title">Appearance & Styling</span>
              </div>
              <span className="visibility-section-hint">Node size, line curves, labels</span>
            </div>

            {!collapsedSections['appearance'] && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', paddingTop: '4px' }}>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
                  {/* Node Content Mode */}
                  <div className="display-setting-row" title="Choose what is displayed inside each node shape">
                    <div className="setting-label-row">
                      <span className="setting-icon">🎨</span>
                      <span className="setting-name">Node Content</span>
                    </div>
                    <select
                      value={nodeContentMode}
                      onChange={(e) => onSetNodeContentMode(e.target.value as NodeContentMode)}
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
                      onChange={(e) => onSetNodeSizingMode(e.target.value as NodeSizingMode)}
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
                  <div className="display-setting-row" title="Line Style: Straight, Bezier curve, or Bypass Inner Orbits">
                    <div className="setting-label-row">
                      <span className="setting-icon">〰️</span>
                      <span className="setting-name">Line Style</span>
                    </div>
                    <select
                      value={edgeCurveMode}
                      onChange={(e) => onSetEdgeCurveMode(e.target.value as EdgeCurveMode)}
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
                  <div className="display-setting-row" title="Control when edge text labels are shown: always or only on hover">
                    <div className="setting-label-row">
                      <span className="setting-icon">🏷️</span>
                      <span className="setting-name">Edge Labels</span>
                    </div>
                    <select
                      value={edgeLabelsOnHover ? 'hover' : 'always'}
                      onChange={(e) => onSetEdgeLabelsOnHover(e.target.value === 'hover')}
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
                      onClick={() => onSetFontSize(10)}
                      title="Click to reset font size to 10px"
                    >
                      {fontSize}px
                    </span>
                  </div>
                  <div className="setting-slider-row">
                    <button
                      type="button"
                      className="domain-hud-step-btn"
                      onClick={() => onSetFontSize(Math.max(7, fontSize - 1))}
                      title="Decrease font size"
                    >
                      −
                    </button>
                    <input
                      type="range"
                      min="7"
                      max="24"
                      step="1"
                      value={fontSize}
                      onChange={(e) => onSetFontSize(parseInt(e.target.value, 10))}
                      className="domain-hud-slider"
                    />
                    <button
                      type="button"
                      className="domain-hud-step-btn"
                      onClick={() => onSetFontSize(Math.min(24, fontSize + 1))}
                      title="Increase font size"
                    >
                      +
                    </button>
                  </div>
                </div>

                {/* Line Curvature Control (when bezier) */}
                {edgeCurveMode === 'bezier' && (
                  <div className="display-setting-row" title="Adjust edge curve intensity (0 = straight, 100 = full loop)">
                    <div className="setting-label-row">
                      <span className="setting-icon">➰</span>
                      <span className="setting-name">Line Curvature</span>
                      <span
                        className="domain-hud-value-badge"
                        onClick={() => onSetCurveFactor(35)}
                        title="Click to reset curve to 35"
                      >
                        {curveFactor}
                      </span>
                    </div>
                    <div className="setting-slider-row">
                      <button
                        type="button"
                        className="domain-hud-step-btn"
                        onClick={() => onSetCurveFactor(Math.max(0, curveFactor - 5))}
                        title="Decrease curvature"
                      >
                        −
                      </button>
                      <input
                        type="range"
                        min="0"
                        max="100"
                        step="5"
                        value={curveFactor}
                        onChange={(e) => onSetCurveFactor(parseInt(e.target.value, 10))}
                        className="domain-hud-slider"
                      />
                      <button
                        type="button"
                        className="domain-hud-step-btn"
                        onClick={() => onSetCurveFactor(Math.min(100, curveFactor + 5))}
                        title="Increase curvature"
                      >
                        +
                      </button>
                    </div>
                  </div>
                )}

                {/* Mouse Wheel Sensitivity */}
                <div className="display-setting-row" title="Adjust canvas zoom speed when using mouse wheel / trackpad">
                  <div className="setting-label-row">
                    <span className="setting-icon">🖱️</span>
                    <span className="setting-name">Wheel Zoom Speed</span>
                    <span
                      className="domain-hud-value-badge"
                      onClick={() => onSetWheelSensitivity(1.0)}
                      title="Click to reset wheel sensitivity to 1.0x"
                    >
                      {wheelSensitivity.toFixed(1)}x
                    </span>
                  </div>
                  <div className="setting-slider-row">
                    <button
                      type="button"
                      className="domain-hud-step-btn"
                      onClick={() => onSetWheelSensitivity(Math.max(0.5, +(wheelSensitivity - 0.2).toFixed(1)))}
                      title="Decrease zoom sensitivity"
                    >
                      −
                    </button>
                    <input
                      type="range"
                      min="0.5"
                      max="3.0"
                      step="0.1"
                      value={wheelSensitivity}
                      onChange={(e) => onSetWheelSensitivity(parseFloat(e.target.value))}
                      className="domain-hud-slider"
                    />
                    <button
                      type="button"
                      className="domain-hud-step-btn"
                      onClick={() => onSetWheelSensitivity(Math.min(3.0, +(wheelSensitivity + 0.2).toFixed(1)))}
                      title="Increase zoom sensitivity"
                    >
                      +
                    </button>
                  </div>
                </div>
              </div>
            )}
          </div>
        )}

        {/* 3. Architectural Presets */}
        <div className="visibility-section">
          <div
            className="visibility-section-header"
            style={{ cursor: 'pointer', userSelect: 'none' }}
            onClick={() => toggleSection('presets')}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              {collapsedSections['presets'] ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
              <span className="visibility-section-title">Architectural Presets</span>
            </div>
            <span className="visibility-section-hint">High-level architecture lenses</span>
          </div>

          {!collapsedSections['presets'] && (
            <div className="visibility-presets-list" style={{ display: 'flex', flexDirection: 'column', gap: '6px', paddingTop: '4px' }}>
              {/* Project Map Presets (Cycles, Utilities, Resources) */}
              {projectMapPresets && (
                <>
                  {projectMapPresets.cyclesCount > 0 && (
                    <button
                      type="button"
                      className={`visibility-preset-item ${projectMapPresets.cyclesOnly ? 'is-active' : ''}`}
                      onClick={() => onToggleProjectMapPreset?.('cyclesOnly')}
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
                    onClick={() => onToggleProjectMapPreset?.('hideUtilities')}
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
                    onClick={() => onToggleProjectMapPreset?.('hideResources')}
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
              )}

              {/* Single-Connection DBs */}
              <button
                type="button"
                className={`visibility-preset-item ${hideSingleConnectionDbs ? 'is-active' : ''}`}
                onClick={onToggleSingleConnectionDbs}
                title="Hide dedicated/private databases connected to only 1 service"
              >
                <div className="visibility-preset-main">
                  <span className="visibility-preset-icon">🗄️</span>
                  <div className="visibility-preset-text">
                    <span className="visibility-preset-label">Private Databases</span>
                    <span className="visibility-preset-desc">
                      Single-service databases ({counts?.singleConnDbs ?? 0})
                    </span>
                  </div>
                </div>
                <div className={`visibility-preset-toggle ${hideSingleConnectionDbs ? 'is-on' : ''}`}>
                  {hideSingleConnectionDbs ? 'HIDDEN' : 'VISIBLE'}
                </div>
              </button>

              {/* Isolated Nodes */}
              <button
                type="button"
                className={`visibility-preset-item ${hideIsolatedNodes ? 'is-active' : ''}`}
                onClick={onToggleIsolatedNodes}
                title="Hide standalone nodes with 0 incoming and 0 outgoing edges"
              >
                <div className="visibility-preset-main">
                  <span className="visibility-preset-icon">🏝️</span>
                  <div className="visibility-preset-text">
                    <span className="visibility-preset-label">Isolated Nodes</span>
                    <span className="visibility-preset-desc">Disconnected nodes without links</span>
                  </div>
                </div>
                <div className={`visibility-preset-toggle ${hideIsolatedNodes ? 'is-on' : ''}`}>
                  {hideIsolatedNodes ? 'HIDDEN' : 'VISIBLE'}
                </div>
              </button>

              {/* Transitive Calls */}
              <button
                type="button"
                className={`visibility-preset-item ${showTransitiveEdges ? 'is-active' : ''}`}
                onClick={onToggleTransitiveEdges}
                title="Render inferred multi-hop transitive dependencies with dashed edges"
              >
                <div className="visibility-preset-main">
                  <span className="visibility-preset-icon">🔗</span>
                  <div className="visibility-preset-text">
                    <span className="visibility-preset-label">Transitive Edges</span>
                    <span className="visibility-preset-desc">
                      Multi-hop call chains ({counts?.transitiveCalls ?? 0})
                    </span>
                  </div>
                </div>
                <div className={`visibility-preset-toggle ${showTransitiveEdges ? 'is-on' : ''}`}>
                  {showTransitiveEdges ? 'SHOWN' : 'HIDDEN'}
                </div>
              </button>
            </div>
          )}
        </div>

        {/* 4. Entity Types */}
        <div className="visibility-section">
          <div
            className="visibility-section-header"
            style={{ cursor: 'pointer', userSelect: 'none' }}
            onClick={() => toggleSection('types')}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              {collapsedSections['types'] ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
              <span className="visibility-section-title">Entity Types</span>
            </div>
            <span className="visibility-section-hint">Click to hide; click ⊞ to group into 1 node</span>
          </div>

          {!collapsedSections['types'] && (
            <div className="visibility-type-grid" style={{ paddingTop: '4px' }}>
              {entityTypes.map((item) => {
                const isHidden = hiddenTypes.has(item.kind);
                const isGrouped = groupedTypes.has(item.kind);
                return (
                  <div
                    key={item.kind}
                    role="button"
                    tabIndex={0}
                    className={`visibility-type-card ${isHidden ? 'is-hidden' : 'is-visible'}`}
                    onClick={() => onToggleType(item.kind)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter' || e.key === ' ') onToggleType(item.kind);
                    }}
                    title={isHidden ? `Show ${item.label}` : `Hide ${item.label}`}
                  >
                    <div className="visibility-type-left">
                      <EntityNodeBadge kind={item.kind} size={24} />
                      <span className="visibility-type-name">{item.label}</span>
                    </div>
                    <div className="visibility-type-right">
                      <span
                        className="visibility-type-count"
                        style={{ backgroundColor: isHidden ? 'rgba(239, 68, 68, 0.2)' : 'rgba(255, 255, 255, 0.08)' }}
                      >
                        {item.count}
                      </span>
                      {onToggleTypeGrouping && (
                        <span
                          role="button"
                          tabIndex={0}
                          className={`visibility-type-group-btn ${isGrouped ? 'is-active' : ''}`}
                          onClick={(e) => {
                            e.stopPropagation();
                            onToggleTypeGrouping(item.kind);
                          }}
                          onKeyDown={(e) => {
                            if (e.key === 'Enter' || e.key === ' ') {
                              e.stopPropagation();
                              onToggleTypeGrouping(item.kind);
                            }
                          }}
                          title={
                            isGrouped
                              ? `Ungroup ${item.label} (Show individual nodes on canvas)`
                              : `Group all ${item.label} into 1 summary node on canvas`
                          }
                        >
                          ⊞
                        </span>
                      )}
                      <span className="visibility-type-eye">
                        {isHidden ? <EyeOffIcon size={12} /> : <EyeIcon size={12} />}
                      </span>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* 5. Node Groups */}
        <div className="visibility-section">
          <div
            className="visibility-section-header"
            style={{ cursor: 'pointer', userSelect: 'none' }}
            onClick={() => toggleSection('groups')}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              {collapsedSections['groups'] ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
              <span className="visibility-section-title">Node Groups</span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span className="visibility-section-hint">Collapse entities into discs</span>
              <button
                type="button"
                className="visibility-new-group-btn"
                onClick={(e) => {
                  e.stopPropagation();
                  setIsCreatingGroup((prev) => !prev);
                  if (collapsedSections['groups']) toggleSection('groups');
                }}
                style={{
                  background: isCreatingGroup ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255, 255, 255, 0.06)',
                  border: `1px solid ${isCreatingGroup ? 'rgba(56, 189, 248, 0.5)' : 'rgba(255, 255, 255, 0.12)'}`,
                  borderRadius: 4,
                  color: isCreatingGroup ? '#38bdf8' : '#cbd5e1',
                  padding: '2px 8px',
                  fontSize: '10.5px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                {isCreatingGroup ? 'Cancel' : '+ New Group'}
              </button>
            </div>
          </div>

          {!collapsedSections['groups'] && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', paddingTop: '4px' }}>
              {/* Inline Group Creator Drawer */}
              {isCreatingGroup && (
                <form
                  onSubmit={handleCreateGroupSubmit}
                  style={{
                    display: 'flex',
                    flexDirection: 'column',
                    gap: 8,
                    padding: '8px 10px',
                    background: 'rgba(56, 189, 248, 0.06)',
                    border: '1px solid rgba(56, 189, 248, 0.25)',
                    borderRadius: 6,
                    marginBottom: 4,
                  }}
                >
                  <div style={{ fontSize: '11px', fontWeight: 600, color: '#38bdf8' }}>
                    Create Custom Summary Group
                  </div>
                  <input
                    type="text"
                    placeholder="Group Name (e.g. Identity Services)"
                    value={newGroupName}
                    onChange={(e) => setNewGroupName(e.target.value)}
                    style={{
                      background: 'rgba(0, 0, 0, 0.3)',
                      border: '1px solid rgba(255, 255, 255, 0.15)',
                      borderRadius: 4,
                      color: '#f8fafc',
                      fontSize: '11px',
                      padding: '4px 8px',
                      outline: 'none',
                    }}
                  />
                  <input
                    type="text"
                    placeholder="Filter nodes (text, *wildcard*, /regex/)..."
                    value={groupEntitySearch}
                    onChange={(e) => setGroupEntitySearch(e.target.value)}
                    style={{
                      background: 'rgba(0, 0, 0, 0.2)',
                      border: '1px solid rgba(255, 255, 255, 0.1)',
                      borderRadius: 4,
                      color: '#cbd5e1',
                      fontSize: '10.5px',
                      padding: '3px 8px',
                      outline: 'none',
                    }}
                  />
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '2px 0' }}>
                    <span style={{ fontSize: '10px', color: '#94a3b8' }}>
                      {availableNodesForGroup.length} matching
                      {groupSelectedNodeIds.size > 0 && ` • ${groupSelectedNodeIds.size} selected`}
                    </span>
                    {availableNodesForGroup.length > 0 && (
                      <button
                        type="button"
                        onClick={handleToggleSelectAllMatching}
                        style={{
                          background: areAllMatchingSelected ? 'rgba(239, 68, 68, 0.15)' : 'rgba(56, 189, 248, 0.15)',
                          border: `1px solid ${areAllMatchingSelected ? 'rgba(239, 68, 68, 0.35)' : 'rgba(56, 189, 248, 0.35)'}`,
                          borderRadius: 3,
                          color: areAllMatchingSelected ? '#fca5a5' : '#38bdf8',
                          fontSize: '10px',
                          fontWeight: 600,
                          padding: '1px 7px',
                          cursor: 'pointer',
                        }}
                        title={areAllMatchingSelected ? 'Deselect all matching nodes' : 'Select all matching nodes'}
                      >
                        {areAllMatchingSelected ? 'Deselect Matching' : `Select All Matching (${availableNodesForGroup.length})`}
                      </button>
                    )}
                  </div>
                  <div style={{ maxHeight: '120px', overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 3 }}>
                    {availableNodesForGroup.map((node) => {
                      const isSelected = groupSelectedNodeIds.has(node.id);
                      return (
                        <label
                          key={node.id}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: 6,
                            fontSize: '10.5px',
                            color: isSelected ? '#38bdf8' : '#cbd5e1',
                            cursor: 'pointer',
                            padding: '2px 4px',
                            background: isSelected ? 'rgba(56, 189, 248, 0.12)' : 'transparent',
                            borderRadius: 3,
                          }}
                        >
                          <input
                            type="checkbox"
                            checked={isSelected}
                            onChange={() => {
                              setGroupSelectedNodeIds((prev) => {
                                const next = new Set(prev);
                                if (next.has(node.id)) next.delete(node.id);
                                else next.add(node.id);
                                return next;
                              });
                            }}
                          />
                          <EntityNodeBadge kind={node.kind} size={14} />
                          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                            {node.displayName || node.name}
                          </span>
                        </label>
                      );
                    })}
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: 6, marginTop: 4 }}>
                    <button
                      type="button"
                      onClick={() => setIsCreatingGroup(false)}
                      style={{
                        background: 'transparent',
                        border: '1px solid rgba(255, 255, 255, 0.15)',
                        color: '#94a3b8',
                        borderRadius: 3,
                        padding: '2px 8px',
                        fontSize: '10.5px',
                        cursor: 'pointer',
                      }}
                    >
                      Cancel
                    </button>
                    <button
                      type="submit"
                      disabled={groupSelectedNodeIds.size === 0}
                      style={{
                        background: groupSelectedNodeIds.size > 0 ? '#0284c7' : 'rgba(255, 255, 255, 0.1)',
                        border: 'none',
                        color: groupSelectedNodeIds.size > 0 ? '#ffffff' : '#64748b',
                        borderRadius: 3,
                        padding: '2px 10px',
                        fontSize: '10.5px',
                        fontWeight: 600,
                        cursor: groupSelectedNodeIds.size > 0 ? 'pointer' : 'not-allowed',
                      }}
                    >
                      Create ({groupSelectedNodeIds.size})
                    </button>
                  </div>
                </form>
              )}

              {/* Active Category Groups */}
              {Array.from(groupedTypes).map((kind) => {
                const count = Array.from(nodeDetailMap.values()).filter(
                  (n) => n.kind === kind && !hiddenNodeIds.has(n.id)
                ).length;
                const meta = getEntityBadgeMeta(kind);
                return (
                  <div
                    key={`group-cat-${kind}`}
                    className="visibility-group-item"
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      padding: '5px 8px',
                      background: 'rgba(255, 255, 255, 0.04)',
                      border: '1px solid rgba(255, 255, 255, 0.08)',
                      borderRadius: 5,
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                      <EntityNodeBadge kind={kind} size={20} />
                      <span style={{ fontSize: '11px', fontWeight: 600, color: '#f1f5f9' }}>
                        {meta.label}s ({count})
                      </span>
                      <span style={{ fontSize: '9px', background: 'rgba(56, 189, 248, 0.15)', color: '#38bdf8', padding: '1px 4px', borderRadius: 3 }}>
                        Category
                      </span>
                    </div>
                    <button
                      type="button"
                      onClick={() => onToggleTypeGrouping(kind)}
                      title="Ungroup category"
                      style={{
                        background: 'none',
                        border: '1px solid rgba(255, 255, 255, 0.15)',
                        borderRadius: 3,
                        color: '#94a3b8',
                        fontSize: '10px',
                        padding: '1px 6px',
                        cursor: 'pointer',
                      }}
                    >
                      Ungroup
                    </button>
                  </div>
                );
              })}

              {/* Active Custom Groups */}
              {customGroups.map((group) => {
                const memberCount = group.nodeIds.length;
                return (
                  <div
                    key={group.id}
                    className="visibility-group-item"
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      padding: '5px 8px',
                      background: 'rgba(79, 70, 229, 0.12)',
                      border: '1px solid rgba(79, 70, 229, 0.35)',
                      borderRadius: 5,
                    }}
                  >
                    <div
                      style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: onSelectGroupNode ? 'pointer' : 'default', minWidth: 0, flex: 1 }}
                      onClick={() => onSelectGroupNode?.(`group:custom:${group.id}`)}
                      title={`Inspect ${group.name}`}
                    >
                      <EntityNodeBadge kind="Group" size={20} />
                      <div style={{ minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        <span style={{ fontSize: '11px', fontWeight: 600, color: '#e0e7ff' }}>
                          {group.name}
                        </span>
                        <span style={{ fontSize: '9.5px', color: '#a5b4fc', marginLeft: 4 }}>
                          ({memberCount})
                        </span>
                      </div>
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                      <button
                        type="button"
                        onClick={() => onToggleCustomGroupCollapse(group.id)}
                        title={group.collapsed ? 'Expand to individual nodes' : 'Collapse into summary node'}
                        style={{
                          background: group.collapsed ? 'rgba(56, 189, 248, 0.15)' : 'rgba(255, 255, 255, 0.06)',
                          border: `1px solid ${group.collapsed ? 'rgba(56, 189, 248, 0.4)' : 'rgba(255, 255, 255, 0.12)'}`,
                          borderRadius: 3,
                          color: group.collapsed ? '#38bdf8' : '#cbd5e1',
                          fontSize: '10px',
                          padding: '1px 6px',
                          cursor: 'pointer',
                        }}
                      >
                        {group.collapsed ? 'Expand' : 'Collapse'}
                      </button>
                      <button
                        type="button"
                        onClick={() => onDeleteCustomGroup(group.id)}
                        title="Delete custom group"
                        style={{
                          background: 'none',
                          border: 'none',
                          color: '#f87171',
                          fontSize: '11px',
                          cursor: 'pointer',
                          padding: '1px 4px',
                        }}
                      >
                        ✕
                      </button>
                    </div>
                  </div>
                );
              })}

              {groupedTypes.size === 0 && customGroups.length === 0 && !isCreatingGroup && (
                <div style={{ fontSize: '11px', color: '#64748b', fontStyle: 'italic', padding: '4px 0' }}>
                  No active node groups. Click ⊞ on an entity type above or use + New Group.
                </div>
              )}
            </div>
          )}
        </div>

        {/* 6. Concentric Orbits Hierarchy */}
        {hasOrbitLegend && !isMatrixMode && (
          <div className="visibility-section">
            <div
              className="visibility-section-header"
              style={{ cursor: 'pointer', userSelect: 'none' }}
              onClick={() => toggleSection('orbits')}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                {collapsedSections['orbits'] ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
                <span className="visibility-section-title">Orbits Hierarchy</span>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <span className="tab-badge">{orbitLegendItems.length}</span>
                <span className="visibility-section-hint">Center → Periphery</span>
              </div>
            </div>

            {!collapsedSections['orbits'] && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', paddingTop: '4px' }}>
                {isConcentric ? (
                  <>
                    <div
                      className="orbit-axis-label"
                      title="Orbits run from center (top) to periphery (bottom). Drag cards or use ▲/▼ to reorder; shift-click adjacent cards, then Merge, to share one ring; drag a kind chip onto another orbit to move it."
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
                            !isOrbitHidden && onHighlightOrbitNodes?.(allNodeIds)
                          }
                          onMouseLeave={() => onClearOrbitHighlight?.()}
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
                                  onToggleOrbitVisibility(group.levelIndex);
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
                                onHighlightOrbitNodes?.(item.nodeIds)
                              }
                              onMouseLeave={() =>
                                isMerged &&
                                !isOrbitHidden &&
                                onHighlightOrbitNodes?.(allNodeIds)
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
                                    <span className="orbit-kind-chip-name">
                                      {getEntityBadgeMeta(k.kind as EntityKind)?.label || k.kind}
                                    </span>
                                    <span className="orbit-kind-chip-count">{k.count}</span>
                                    {k.overridden ? (
                                      <button
                                        type="button"
                                        className="orbit-kind-chip-clear"
                                        onClick={(e) => {
                                          e.stopPropagation();
                                          applyOrbitCfg(moveKindToOrbit(orbitConfig, k.kind, null), [item.levelIndex]);
                                        }}
                                        title="Restore default orbit for this kind"
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
                  <div style={{ padding: '10px 4px', fontSize: '11px', color: '#94a3b8', lineHeight: 1.45 }}>
                    Concentric orbit rings and tier reordering are active when a <strong>Concentric</strong> layout algorithm is selected in the HUD.
                  </div>
                )}
              </div>
            )}
          </div>
        )}

        {/* 7. Hidden Entities */}
        <div className="visibility-section">
          <div
            className="visibility-section-header"
            style={{ cursor: 'pointer', userSelect: 'none' }}
            onClick={() => toggleSection('hidden')}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              {collapsedSections['hidden'] ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
              <span className="visibility-section-title">Hidden Entities</span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              {allHiddenItems.length > 0 && <span className="tab-badge">{allHiddenItems.length}</span>}
              <span className="visibility-section-hint">Manual & filter hidden items</span>
            </div>
          </div>

          {!collapsedSections['hidden'] && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', paddingTop: '4px' }}>
              {allHiddenItems.length > 0 ? (
                <>
                  <input
                    type="text"
                    placeholder="Search hidden entities..."
                    value={hiddenSearchQuery}
                    onChange={(e) => setHiddenSearchQuery(e.target.value)}
                    style={{
                      background: 'rgba(0, 0, 0, 0.25)',
                      border: '1px solid rgba(255, 255, 255, 0.12)',
                      borderRadius: 4,
                      color: '#f8fafc',
                      fontSize: '11px',
                      padding: '4px 8px',
                      outline: 'none',
                    }}
                  />
                  <div className="hidden-items-list" style={{ display: 'flex', flexDirection: 'column', gap: 4, maxHeight: 200, overflowY: 'auto' }}>
                    {filteredHiddenItems.map((item) => (
                      <div
                        key={item.id}
                        className="hidden-item-row"
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'space-between',
                          padding: '4px 8px',
                          background: 'rgba(255, 255, 255, 0.03)',
                          border: '1px solid rgba(255, 255, 255, 0.06)',
                          borderRadius: 4,
                        }}
                      >
                        <div style={{ display: 'flex', alignItems: 'center', gap: 6, minWidth: 0, flex: 1 }}>
                          <EntityNodeBadge kind={item.kind} size={18} />
                          <span
                            style={{
                              fontSize: '11px',
                              color: '#cbd5e1',
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                              whiteSpace: 'nowrap',
                            }}
                            title={item.label}
                          >
                            {item.label}
                          </span>
                          <span
                            style={{
                              fontSize: '9px',
                              padding: '1px 5px',
                              borderRadius: 3,
                              background: 'rgba(255, 255, 255, 0.06)',
                              color: '#94a3b8',
                              whiteSpace: 'nowrap',
                            }}
                          >
                            {item.reasonTag}
                          </span>
                        </div>
                        <button
                          type="button"
                          className="hidden-item-unhide-btn"
                          onClick={() => onUnhideNode(item.id)}
                          title={`Unhide ${item.label}`}
                          style={{
                            background: 'none',
                            border: 'none',
                            color: '#38bdf8',
                            fontSize: '12px',
                            cursor: 'pointer',
                            padding: '0 4px',
                          }}
                        >
                          ✕
                        </button>
                      </div>
                    ))}
                    {filteredHiddenItems.length === 0 && (
                      <div style={{ fontSize: '11px', color: '#64748b', fontStyle: 'italic', padding: '4px' }}>
                        No hidden entities match &ldquo;{hiddenSearchQuery}&rdquo;
                      </div>
                    )}
                  </div>
                </>
              ) : (
                <div style={{ fontSize: '11px', color: '#64748b', fontStyle: 'italic', padding: '4px 0' }}>
                  All entities are visible on the canvas.
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </FloatingPanel>
  );
};
