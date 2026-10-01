import React, { useState, useMemo } from 'react';
import { SelectedNodeDetail, EntityKind } from '../types';
import { useDomainArchitecture } from '../context/DomainArchitectureContext';

interface HiddenItem {
  id: string;
  label: string;
  kind: string;
  kindTag: string;
  badgeColor: string;
  reasonTag: string;
}

export interface DomainHiddenPanelProps {
  nodeDetailMap: Map<string, SelectedNodeDetail>;
  echelonMap?: Map<string, number>;
  hiddenNodeIds?: Set<string>;
  hiddenTypes?: Set<EntityKind>;
  hiddenOrbitTiers?: Set<number>;
  hideSingleConnectionDbs?: boolean;
  hideIsolatedNodes?: boolean;
  onUnhideNode?: (id: string) => void;
  onRestoreAll?: () => void;
  isCollapsed?: boolean;
  onToggleCollapse?: (collapsed: boolean) => void;
}

export const DomainHiddenPanel: React.FC<DomainHiddenPanelProps> = (props) => {
  let ctx: any = null;
  try {
    ctx = useDomainArchitecture();
  } catch {}

  const hiddenNodeIds = props.hiddenNodeIds || ctx?.hiddenNodeIds || new Set();
  const hiddenTypes = props.hiddenTypes || ctx?.hiddenTypes || new Set();
  const hiddenOrbitTiers = props.hiddenOrbitTiers || ctx?.hiddenOrbitTiers || new Set();
  const hideSingleConnectionDbs = props.hideSingleConnectionDbs !== undefined ? props.hideSingleConnectionDbs : !!ctx?.hideSingleConnectionDbs;
  const hideIsolatedNodes = props.hideIsolatedNodes !== undefined ? props.hideIsolatedNodes : !!ctx?.hideIsolatedNodes;
  const unhideNode = props.onUnhideNode || ctx?.unhideNode;
  const restoreAllHiddenNodes = props.onRestoreAll || ctx?.restoreAllHiddenNodes;
  const isHiddenPanelCollapsed = props.isCollapsed !== undefined ? props.isCollapsed : !!ctx?.isHiddenPanelCollapsed;
  const setIsHiddenPanelCollapsed = props.onToggleCollapse || ctx?.setIsHiddenPanelCollapsed;
  const nodeDetailMap = props.nodeDetailMap;

  const [hiddenSearchQuery, setHiddenSearchQuery] = useState('');

  // Collect all effectively hidden items with reasons
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
  }, [
    nodeDetailMap,
    hiddenNodeIds,
    hiddenTypes,
    hiddenOrbitTiers,
    hideSingleConnectionDbs,
    hideIsolatedNodes,
  ]);

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

  if (allHiddenItems.length === 0) {
    return null;
  }

  return (
    <aside className={`domain-hidden-panel ${isHiddenPanelCollapsed ? 'is-collapsed' : ''}`}>
      {isHiddenPanelCollapsed ? (
        <div
          className="domain-hidden-collapsed-badge"
          onClick={() => setIsHiddenPanelCollapsed(false)}
          title={`Click to expand ${allHiddenItems.length} hidden entities`}
        >
          <span className="badge-icon">👁️‍🗨️</span>
          <span className="badge-count">{allHiddenItems.length}</span>
          <span className="badge-arrow">▶</span>
        </div>
      ) : (
        <div className="domain-hidden-panel-content">
          <div className="domain-hidden-panel-header">
            <div className="hidden-panel-title">
              <span className="hidden-panel-icon">👁️‍🗨️</span>
              <span>Hidden ({allHiddenItems.length})</span>
            </div>
            <div className="hidden-panel-header-actions">
              <button
                type="button"
                className="hidden-panel-restore-all-btn"
                onClick={restoreAllHiddenNodes}
                title="Restore all hidden entities to map"
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

          {allHiddenItems.length > 4 && (
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

          <div className="domain-hidden-list" style={{ maxHeight: 320, overflowY: 'auto' }}>
            {filteredHiddenItems.map((item) => (
              <div
                key={item.id}
                className="domain-hidden-list-item"
                title={`${item.label} (${item.kind}, ${item.reasonTag}). Click ✕ to restore.`}
              >
                <span
                  className="hidden-kind-tag"
                  style={{ backgroundColor: item.badgeColor }}
                >
                  {item.kindTag}
                </span>
                <span className="hidden-item-name">{item.label}</span>
                {item.reasonTag && item.reasonTag !== 'Manual' && (
                  <span className="hidden-reason-tag">{item.reasonTag}</span>
                )}
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
          </div>
        </div>
      )}
    </aside>
  );
};
