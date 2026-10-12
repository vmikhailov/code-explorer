import React, { useState, useMemo } from 'react';
import { SelectedNodeDetail, EntityKind } from '../types';
import { useDomainArchitecture } from '../context/DomainArchitectureContext';
import { FloatingPanel } from '../../components/FloatingPanel';
import { EyeOffIcon, RotateCcwIcon, XIcon } from '../../components/Icons';

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
    <FloatingPanel
      dock="top-left"
      isCollapsed={isHiddenPanelCollapsed}
      onToggleCollapse={setIsHiddenPanelCollapsed}
      icon={<EyeOffIcon size={14} />}
      title="Hidden"
      badge={
        <span
          className="floating-dock-badge"
          style={{
            backgroundColor: 'rgba(239, 68, 68, 0.2)',
            color: '#fca5a5',
            border: '1px solid rgba(239, 68, 68, 0.35)',
            fontSize: '10px',
            padding: '1px 6px',
          }}
        >
          {allHiddenItems.length}
        </span>
      }
      collapsedTitle={`Hidden (${allHiddenItems.length})`}
      collapsedIcon={<EyeOffIcon size={13} />}
      collapseTooltip="Collapse hidden panel"
      expandTooltip={`Click to expand ${allHiddenItems.length} hidden entities`}
      headerActions={
        <button
          type="button"
          className="floating-dock-btn action-btn hidden-panel-restore-all-btn"
          onClick={restoreAllHiddenNodes}
          title="Restore all hidden entities to graph"
        >
          <RotateCcwIcon size={12} />
          <span>Restore All</span>
        </button>
      }
      subtitle={
        allHiddenItems.length > 4 ? (
          <div className="domain-hidden-search-wrap" style={{ width: '100%', margin: 0 }}>
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
                <XIcon size={11} />
              </button>
            )}
          </div>
        ) : undefined
      }
      className="domain-hidden-panel"
      width={340}
      minWidth={280}
      maxWidth={700}
      persistKey="ce_hidden_panel"
    >
      <div className="domain-hidden-list" style={{ maxHeight: 360, overflowY: 'auto' }}>
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
              onClick={() => unhideNode?.(item.id)}
              title={`Restore ${item.label}`}
            >
              <XIcon size={11} />
            </button>
          </div>
        ))}
        {filteredHiddenItems.length === 0 && (
          <div className="domain-hidden-empty">No matching hidden items</div>
        )}
      </div>
    </FloatingPanel>
  );
};
