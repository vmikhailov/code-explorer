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
  OrbitLegendItem,
  DomainLayoutName,
  EntityKind,
} from '../../components/DomainArchitectureView';
import { getEntityBadgeMeta } from '../entityBadges';
import { ProjectMapPresetsState } from './DomainSettingsPanel';
import {
  LayersIcon,
  RotateCcwIcon,
  ChevronUpIcon,
  ChevronDownIcon,
  EyeIcon,
  EyeOffIcon,
} from '../../components/Icons';

export interface DomainOrbitsPanelProps {
  hasOrbitLegend: boolean;
  orbitLegendItems: OrbitLegendItem[];
  orbitConfig: OrbitConfig;
  hiddenOrbitTiers: number[] | Set<number>;
  layoutName?: DomainLayoutName;
  onApplyOrbitConfig: (next: OrbitConfig, clearHiddenFor?: number[], resetHidden?: boolean) => void;
  onToggleOrbitVisibility: (levelIndex: number) => void;
  onHighlightOrbitNodes?: (nodeIds: string[]) => void;
  onClearOrbitHighlight?: () => void;
  projectMapPresets?: ProjectMapPresetsState;
  isMatrixMode?: boolean;
}

export const DomainOrbitsPanel: React.FC<DomainOrbitsPanelProps> = ({
  hasOrbitLegend,
  orbitLegendItems,
  orbitConfig,
  hiddenOrbitTiers: hiddenOrbitTiersProp,
  layoutName = 'concentric',
  onApplyOrbitConfig,
  onToggleOrbitVisibility,
  onHighlightOrbitNodes,
  onClearOrbitHighlight,
  projectMapPresets,
  isMatrixMode = false,
}) => {
  const hiddenOrbitTiers = useMemo(
    () => (hiddenOrbitTiersProp instanceof Set ? hiddenOrbitTiersProp : new Set(hiddenOrbitTiersProp)),
    [hiddenOrbitTiersProp]
  );

  const [selectedOrbitRows, setSelectedOrbitRows] = useState<number[]>([]);
  const lastSelectedOrbitRowRef = useRef<number | null>(null);
  const [draggedOrbitIndex, setDraggedOrbitIndex] = useState<number | null>(null);
  const draggedKindRef = useRef<string | null>(null);
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);

  const isConcentric = !layoutName || layoutName.startsWith('concentric');

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
      onApplyOrbitConfig(next, clearHiddenFor, resetHidden);
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

  if (!hasOrbitLegend || isMatrixMode) {
    return (
      <div style={{ padding: '16px', textAlign: 'center', color: '#64748b', fontSize: '11px' }}>
        Orbit hierarchy is not active in this view mode.
      </div>
    );
  }

  return (
    <div
      className="domain-orbit-legend-floating"
      style={{
        display: 'flex',
        flexDirection: 'column',
        flex: 1,
        minHeight: 0,
        overflow: 'hidden',
        background: '#0b0f19',
      }}
    >
      {/* Orbits Header & Actions */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '8px 12px',
          borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
          background: 'rgba(255, 255, 255, 0.02)',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <LayersIcon size={13} />
          <span style={{ fontSize: '11.5px', fontWeight: 600, color: '#f8fafc' }}>
            Orbit Rings
          </span>
          <span
            style={{
              backgroundColor: 'rgba(56, 189, 248, 0.18)',
              color: '#38bdf8',
              border: '1px solid rgba(56, 189, 248, 0.35)',
              fontSize: '10px',
              padding: '1px 6px',
              borderRadius: '8px',
              fontWeight: 600,
            }}
          >
            {orbitLegendItems.length}
          </span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
          {mergeSelection && (
            <button
              type="button"
              className="floating-dock-btn action-btn"
              onClick={handleMergeSelected}
              title="Merge the selected adjacent orbits into one ring"
              style={{ padding: '2px 8px', fontSize: '11px', display: 'flex', alignItems: 'center', gap: '4px' }}
            >
              <LayersIcon size={11} />
              <span>Merge {mergeSelection.length}</span>
            </button>
          )}

          {(!isDefaultOrbitConfig(orbitConfig) || hiddenOrbitTiers.size > 0) && (
            <button
              type="button"
              className="floating-dock-btn action-btn"
              onClick={handleResetOrbitOrder}
              title="Reset to default orbit order and unhide all orbits"
              style={{ padding: '2px 8px', fontSize: '11px', display: 'flex', alignItems: 'center', gap: '4px' }}
            >
              <RotateCcwIcon size={11} />
              <span>Reset</span>
            </button>
          )}
        </div>
      </div>

      {/* Orbit Body List */}
      <div className="domain-orbit-legend-body" style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: '8px 10px' }}>
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
                  onMouseEnter={() => !isOrbitHidden && onHighlightOrbitNodes?.(allNodeIds)}
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
                        isMerged && !isOrbitHidden && onHighlightOrbitNodes?.(item.nodeIds)
                      }
                      onMouseLeave={() =>
                        isMerged && !isOrbitHidden && onHighlightOrbitNodes?.(allNodeIds)
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
          <div style={{ padding: '24px 16px', fontSize: '11.5px', color: '#94a3b8', lineHeight: 1.5, textAlign: 'center' }}>
            <div style={{ fontSize: '24px', marginBottom: '8px' }}>🪐</div>
            <div>
              Concentric orbit rings and radial tier reordering are active when a <strong>Concentric</strong> layout algorithm is selected.
            </div>
            <div style={{ marginTop: '6px', fontSize: '11px', color: '#64748b' }}>
              Current layout: <strong>{layoutName}</strong>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
