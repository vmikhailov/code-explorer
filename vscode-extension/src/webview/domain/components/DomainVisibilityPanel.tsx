import React, { useState, useMemo } from 'react';
import { SelectedNodeDetail, EntityKind } from '../types';
import { useDomainArchitecture } from '../context/DomainArchitectureContext';
import { FloatingPanel } from '../../components/FloatingPanel';
import { EyeIcon, EyeOffIcon, RotateCcwIcon, XIcon } from '../../components/Icons';

export interface HiddenItem {
  id: string;
  label: string;
  kind: string;
  kindTag: string;
  badgeColor: string;
  reasonTag: string;
}

export interface DomainVisibilityPanelProps {
  nodeDetailMap: Map<string, SelectedNodeDetail>;
  echelonMap?: Map<string, number>;
  hiddenNodeIds?: Set<string>;
  hiddenTypes?: Set<EntityKind>;
  hiddenOrbitTiers?: Set<number>;
  hideSingleConnectionDbs?: boolean;
  hideIsolatedNodes?: boolean;
  showTransitiveEdges?: boolean;
  counts?: {
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
  onUnhideNode?: (id: string) => void;
  onRestoreAll?: () => void;
  onToggleType?: (kind: EntityKind) => void;
  onToggleSingleConnectionDbs?: () => void;
  onToggleIsolatedNodes?: () => void;
  onToggleTransitiveEdges?: () => void;
  isCollapsed?: boolean;
  onToggleCollapse?: (collapsed: boolean) => void;
  docked?: boolean;
  onClose?: () => void;
}

interface EntityTypeItem {
  kind: EntityKind;
  label: string;
  icon: string;
  color: string;
  count: number;
}

export const DomainVisibilityPanel: React.FC<DomainVisibilityPanelProps> = (props) => {
  let ctx: any = null;
  try {
    ctx = useDomainArchitecture();
  } catch {}

  const hiddenNodeIds = props.hiddenNodeIds || ctx?.hiddenNodeIds || new Set();
  const hiddenTypes = props.hiddenTypes || ctx?.hiddenTypes || new Set();
  const hiddenOrbitTiers = props.hiddenOrbitTiers || ctx?.hiddenOrbitTiers || new Set();
  const hideSingleConnectionDbs = props.hideSingleConnectionDbs !== undefined ? props.hideSingleConnectionDbs : !!ctx?.hideSingleConnectionDbs;
  const hideIsolatedNodes = props.hideIsolatedNodes !== undefined ? props.hideIsolatedNodes : !!ctx?.hideIsolatedNodes;
  const showTransitiveEdges = props.showTransitiveEdges !== undefined ? props.showTransitiveEdges : !!ctx?.showTransitiveEdges;
  const unhideNode = props.onUnhideNode || ctx?.unhideNode;
  const restoreAllHiddenNodes = props.onRestoreAll || ctx?.restoreAllHiddenNodes;
  const isHiddenPanelCollapsed = props.isCollapsed !== undefined ? props.isCollapsed : !!ctx?.isHiddenPanelCollapsed;
  const setIsHiddenPanelCollapsed = props.onToggleCollapse || ctx?.setIsHiddenPanelCollapsed;
  const toggleType = props.onToggleType || ctx?.toggleTypeVisibility;
  const toggleSingleConnDbs = props.onToggleSingleConnectionDbs || (() => ctx?.setHideSingleConnectionDbs?.((p: boolean) => !p));
  const toggleIsolated = props.onToggleIsolatedNodes || (() => ctx?.setHideIsolatedNodes?.((p: boolean) => !p));
  const toggleTransitive = props.onToggleTransitiveEdges || (() => ctx?.setShowTransitiveEdges?.((p: boolean) => !p));
  const nodeDetailMap = props.nodeDetailMap;

  const [hiddenSearchQuery, setHiddenSearchQuery] = useState('');

  // Count entities by type if counts prop is not fully provided
  const entityTypeCounts = useMemo(() => {
    const calculated: Record<string, number> = {
      Service: 0,
      Worker: 0,
      TestSuite: 0,
      Database: 0,
      Topic: 0,
      ExternalService: 0,
      App: 0,
      CliTool: 0,
      Library: 0,
    };
    for (const node of nodeDetailMap.values()) {
      if (calculated[node.kind] !== undefined) {
        calculated[node.kind]++;
      }
    }
    return {
      services: props.counts?.services ?? calculated.Service,
      workers: props.counts?.workers ?? calculated.Worker,
      testSuites: props.counts?.testSuites ?? calculated.TestSuite,
      databases: props.counts?.databases ?? calculated.Database,
      singleConnDbs: props.counts?.singleConnDbs ?? 0,
      sharedDbs: props.counts?.sharedDbs ?? 0,
      topics: props.counts?.topics ?? calculated.Topic,
      external: props.counts?.external ?? calculated.ExternalService,
      ingress: props.counts?.ingress ?? calculated.App,
      cli: props.counts?.cli ?? calculated.CliTool,
      libraries: props.counts?.libraries ?? calculated.Library,
      transitiveCalls: props.counts?.transitiveCalls ?? 0,
    };
  }, [nodeDetailMap, props.counts]);

  // Entity types catalog for quick toggles
  const entityTypes: EntityTypeItem[] = useMemo(() => [
    { kind: 'Service', label: 'Services', icon: '⚙️', color: '#e53935', count: entityTypeCounts.services },
    { kind: 'Worker', label: 'Workers', icon: '⚡', color: '#c026d3', count: entityTypeCounts.workers },
    { kind: 'TestSuite', label: 'Tests', icon: '🧪', color: '#059669', count: entityTypeCounts.testSuites },
    { kind: 'Database', label: 'Databases', icon: '🗄️', color: '#7b1fa2', count: entityTypeCounts.databases },
    { kind: 'Topic', label: 'Topics', icon: '📬', color: '#f59e0b', count: entityTypeCounts.topics },
    { kind: 'ExternalService', label: 'External', icon: '🔌', color: '#0891b2', count: entityTypeCounts.external },
    { kind: 'App', label: 'Apps', icon: '🌐', color: '#0288d1', count: entityTypeCounts.ingress },
    { kind: 'CliTool', label: 'CLI Tools', icon: '💻', color: '#d97706', count: entityTypeCounts.cli },
    { kind: 'Library', label: 'Libraries', icon: '📚', color: '#4b5563', count: entityTypeCounts.libraries },
  ], [entityTypeCounts]);

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

  const handleToggleKind = (kind: EntityKind) => {
    if (toggleType) {
      toggleType(kind);
      if (kind === 'App') {
        toggleType('Ingress' as EntityKind);
      }
    }
  };

  const hasAnyFiltersOrHidden =
    allHiddenItems.length > 0 ||
    hiddenTypes.size > 0 ||
    hideSingleConnectionDbs ||
    hideIsolatedNodes;

  const content = (
    <div className="domain-visibility-panel-body" style={{ display: 'flex', flexDirection: 'column', gap: '14px', padding: '10px 12px', flex: 1, minHeight: 0, overflowY: 'auto' }}>
      {/* 1. Quick Entity Type Toggles */}
      <div className="visibility-section">
        <div className="visibility-section-header">
          <span className="visibility-section-title">Entity Types</span>
          <span className="visibility-section-hint">Click to show/hide category</span>
        </div>
        <div className="visibility-type-grid">
          {entityTypes.map((item) => {
            const isHidden = hiddenTypes.has(item.kind);
            return (
              <button
                key={item.kind}
                type="button"
                className={`visibility-type-card ${isHidden ? 'is-hidden' : 'is-visible'}`}
                onClick={() => handleToggleKind(item.kind)}
                title={isHidden ? `Show ${item.label}` : `Hide ${item.label}`}
              >
                <div className="visibility-type-left">
                  <span className="visibility-type-icon">{item.icon}</span>
                  <span className="visibility-type-name">{item.label}</span>
                </div>
                <div className="visibility-type-right">
                  <span
                    className="visibility-type-count"
                    style={{ backgroundColor: isHidden ? 'rgba(239, 68, 68, 0.2)' : 'rgba(255, 255, 255, 0.08)' }}
                  >
                    {item.count}
                  </span>
                  <span className="visibility-type-eye">
                    {isHidden ? <EyeOffIcon size={12} /> : <EyeIcon size={12} />}
                  </span>
                </div>
              </button>
            );
          })}
        </div>
      </div>

      {/* 2. Architectural Presets */}
      <div className="visibility-section">
        <div className="visibility-section-header">
          <span className="visibility-section-title">Architectural Presets</span>
          <span className="visibility-section-hint">High-level view filters</span>
        </div>
        <div className="visibility-presets-list">
          {/* Hide 1:1 Databases */}
          <button
            type="button"
            className={`visibility-preset-item ${hideSingleConnectionDbs ? 'is-active' : ''}`}
            onClick={toggleSingleConnDbs}
            title="Hide dedicated databases connected to only 1 service, revealing only shared data stores"
          >
            <div className="visibility-preset-main">
              <span className="visibility-preset-icon">🗄️</span>
              <div className="visibility-preset-text">
                <span className="visibility-preset-label">Hide 1:1 Databases</span>
                <span className="visibility-preset-desc">
                  {entityTypeCounts.singleConnDbs > 0
                    ? `${entityTypeCounts.singleConnDbs} dedicated / ${entityTypeCounts.sharedDbs} shared`
                    : 'Leaves only shared databases'}
                </span>
              </div>
            </div>
            <div className={`visibility-preset-toggle ${hideSingleConnectionDbs ? 'is-on' : ''}`}>
              {hideSingleConnectionDbs ? 'ON' : 'OFF'}
            </div>
          </button>

          {/* Hide Isolated Components */}
          <button
            type="button"
            className={`visibility-preset-item ${hideIsolatedNodes ? 'is-active' : ''}`}
            onClick={toggleIsolated}
            title="Hide isolated components that have zero inbound or outbound connections"
          >
            <div className="visibility-preset-main">
              <span className="visibility-preset-icon">🏝️</span>
              <div className="visibility-preset-text">
                <span className="visibility-preset-label">Hide Isolated</span>
                <span className="visibility-preset-desc">Nodes with 0 calls or dependencies</span>
              </div>
            </div>
            <div className={`visibility-preset-toggle ${hideIsolatedNodes ? 'is-on' : ''}`}>
              {hideIsolatedNodes ? 'ON' : 'OFF'}
            </div>
          </button>

          {/* Transitive Messaging Calls */}
          <button
            type="button"
            className={`visibility-preset-item ${showTransitiveEdges ? 'is-active' : ''}`}
            onClick={toggleTransitive}
            title="Show or hide direct interaction chords between services through message topics"
          >
            <div className="visibility-preset-main">
              <span className="visibility-preset-icon">╌</span>
              <div className="visibility-preset-text">
                <span className="visibility-preset-label">Transitive Messaging</span>
                <span className="visibility-preset-desc">
                  {entityTypeCounts.transitiveCalls > 0
                    ? `${entityTypeCounts.transitiveCalls} cross-topic calls`
                    : 'Direct producer-consumer chords'}
                </span>
              </div>
            </div>
            <div className={`visibility-preset-toggle ${showTransitiveEdges ? 'is-on' : ''}`}>
              {showTransitiveEdges ? 'ON' : 'OFF'}
            </div>
          </button>
        </div>
      </div>

      {/* 3. Hidden Items List */}
      <div className="visibility-section" style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>
        <div className="visibility-section-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span className="visibility-section-title">Hidden Entities</span>
            <span
              className="tab-badge"
              style={{
                backgroundColor: allHiddenItems.length > 0 ? 'rgba(239, 68, 68, 0.25)' : 'rgba(16, 185, 129, 0.2)',
                color: allHiddenItems.length > 0 ? '#fca5a5' : '#6ee7b7',
                border: `1px solid ${allHiddenItems.length > 0 ? 'rgba(239, 68, 68, 0.4)' : 'rgba(16, 185, 129, 0.35)'}`,
                fontSize: '9.5px',
                padding: '1px 5px',
              }}
            >
              {allHiddenItems.length}
            </span>
          </div>
          {allHiddenItems.length > 0 && (
            <button
              type="button"
              className="visibility-restore-btn"
              onClick={restoreAllHiddenNodes}
              title="Restore all hidden entities to graph"
            >
              <RotateCcwIcon size={11} />
              <span>Restore All</span>
            </button>
          )}
        </div>

        {allHiddenItems.length > 3 && (
          <div className="domain-hidden-search-wrap" style={{ width: '100%', marginBottom: 6 }}>
            <input
              type="text"
              className="domain-hidden-search-input"
              placeholder="Filter hidden entities..."
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
        )}

        <div className="domain-hidden-list" style={{ flex: 1, minHeight: 60, overflowY: 'auto' }}>
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
              {item.reasonTag && (
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

          {allHiddenItems.length === 0 && (
            <div className="domain-panel-empty-state" style={{ padding: '16px 8px' }}>
              <div className="domain-empty-card" style={{ padding: '12px' }}>
                <span className="domain-empty-icon" style={{ fontSize: '20px' }}>👁️</span>
                <h4 className="domain-empty-title" style={{ fontSize: '11.5px', margin: '4px 0' }}>All Entities Visible</h4>
                <p className="domain-empty-desc" style={{ fontSize: '10.5px' }}>
                  Use the quick toggles and presets above to hide services, tests, or databases.
                </p>
              </div>
            </div>
          )}

          {allHiddenItems.length > 0 && filteredHiddenItems.length === 0 && (
            <div className="domain-hidden-empty">No matching hidden items</div>
          )}
        </div>
      </div>
    </div>
  );

  if (props.docked) {
    return content;
  }

  return (
    <FloatingPanel
      dock="top-right"
      isCollapsed={isHiddenPanelCollapsed}
      onToggleCollapse={setIsHiddenPanelCollapsed}
      icon={<EyeIcon size={14} />}
      title="Visibility"
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
      collapsedTitle={`Visibility (${allHiddenItems.length})`}
      collapsedIcon={<EyeIcon size={13} />}
      collapseTooltip="Collapse visibility panel"
      expandTooltip={`Click to expand visibility panel (${allHiddenItems.length} hidden)`}
      headerActions={
        hasAnyFiltersOrHidden ? (
          <button
            type="button"
            className="floating-dock-btn action-btn hidden-panel-restore-all-btn"
            onClick={restoreAllHiddenNodes}
            title="Restore all hidden entities to graph"
          >
            <RotateCcwIcon size={12} />
            <span>Restore All</span>
          </button>
        ) : undefined
      }
      className="domain-hidden-panel"
      width={360}
      minWidth={280}
      maxWidth={700}
      persistKey="ce_visibility_panel"
      docked={props.docked}
      onClose={props.onClose}
    >
      {content}
    </FloatingPanel>
  );
};

// Re-export as DomainHiddenPanel for backwards compatibility
export const DomainHiddenPanel = DomainVisibilityPanel;
