import React, { useState, useMemo } from 'react';
import { SelectedNodeDetail, EntityKind, CustomNodeGroup } from '../types';
import { FloatingPanel } from '../../components/FloatingPanel';
import { EyeIcon, EyeOffIcon, RotateCcwIcon, XIcon } from '../../components/Icons';
import { EntityNodeBadge, getEntityBadgeMeta } from '../entityBadges';

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
  groupedTypes?: Set<EntityKind>;
  onToggleTypeGrouping?: (kind: EntityKind) => void;
  customGroups?: CustomNodeGroup[];
  onCreateCustomGroup?: (name: string, nodeIds: string[]) => void;
  onToggleCustomGroupCollapse?: (groupId: string) => void;
  onDeleteCustomGroup?: (groupId: string) => void;
  onSelectGroupNode?: (groupId: string) => void;
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
  appearanceSection?: React.ReactNode;
  customPresetsSlot?: React.ReactNode;
}

interface EntityTypeItem {
  kind: EntityKind;
  label: string;
  icon: string;
  color: string;
  count: number;
}

export const DomainVisibilityPanel: React.FC<DomainVisibilityPanelProps> = (props) => {
  const hiddenNodeIds = props.hiddenNodeIds || new Set<string>();
  const hiddenTypes = props.hiddenTypes || new Set<EntityKind>();
  const hiddenOrbitTiers = props.hiddenOrbitTiers || new Set<number>();
  const hideSingleConnectionDbs = !!props.hideSingleConnectionDbs;
  const hideIsolatedNodes = !!props.hideIsolatedNodes;
  const showTransitiveEdges = !!props.showTransitiveEdges;
  const unhideNode = props.onUnhideNode;
  const restoreAllHiddenNodes = props.onRestoreAll;
  const isHiddenPanelCollapsed = !!props.isCollapsed;
  const setIsHiddenPanelCollapsed = props.onToggleCollapse;
  const toggleType = props.onToggleType;
  const toggleSingleConnDbs = props.onToggleSingleConnectionDbs;
  const toggleIsolated = props.onToggleIsolatedNodes;
  const toggleTransitive = props.onToggleTransitiveEdges;
  const nodeDetailMap = props.nodeDetailMap;

  const groupedTypes: Set<EntityKind> = props.groupedTypes || new Set<EntityKind>();
  const toggleTypeGrouping = props.onToggleTypeGrouping;
  const customGroups: CustomNodeGroup[] = props.customGroups || [];
  const createCustomGroup = props.onCreateCustomGroup;
  const toggleCustomGroupCollapse = props.onToggleCustomGroupCollapse;
  const deleteCustomGroup = props.onDeleteCustomGroup;
  const onSelectGroupNode = props.onSelectGroupNode;

  const [isCreatingGroup, setIsCreatingGroup] = useState(false);
  const [newGroupName, setNewGroupName] = useState('');
  const [groupEntitySearch, setGroupEntitySearch] = useState('');
  const [groupSelectedNodeIds, setGroupSelectedNodeIds] = useState<Set<string>>(new Set());

  const availableNodesForGroup = useMemo(() => {
    const list: SelectedNodeDetail[] = [];
    const q = groupEntitySearch.trim().toLowerCase();
    for (const [id, node] of nodeDetailMap.entries()) {
      if (hiddenNodeIds.has(id)) continue;
      if (hiddenTypes.has(node.kind)) continue;
      if (node.isGroup) continue;
      if (q) {
        const name = (node.displayName || node.name || '').toLowerCase();
        const kind = (node.kind || '').toLowerCase();
        if (!name.includes(q) && !kind.includes(q)) continue;
      }
      list.push(node);
    }
    list.sort((a, b) => (a.displayName || a.name).localeCompare(b.displayName || b.name));
    return list;
  }, [nodeDetailMap, hiddenNodeIds, hiddenTypes, groupEntitySearch]);

  const handleCreateGroupSubmit = (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (groupSelectedNodeIds.size === 0 || !createCustomGroup) return;
    const name = newGroupName.trim() || `Group (${groupSelectedNodeIds.size})`;
    createCustomGroup(name, Array.from(groupSelectedNodeIds));
    setNewGroupName('');
    setGroupEntitySearch('');
    setGroupSelectedNodeIds(new Set());
    setIsCreatingGroup(false);
  };

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
    { kind: 'ExternalService', label: 'External', icon: '🔌', color: '#26a69a', count: entityTypeCounts.external },
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
    groupedTypes.size > 0 ||
    customGroups.length > 0 ||
    hideSingleConnectionDbs ||
    hideIsolatedNodes;

  const content = (
    <div className="domain-visibility-panel-body" style={{ display: 'flex', flexDirection: 'column', gap: '14px', padding: '10px 12px', flex: 1, minHeight: 0, overflowY: 'auto' }}>
      {props.appearanceSection}
      {/* 1. Quick Entity Type Toggles */}
      <div className="visibility-section">
        <div className="visibility-section-header">
          <span className="visibility-section-title">Entity Types</span>
          <span className="visibility-section-hint">Click card to hide; click ⊞ to group into 1 node</span>
        </div>
        <div className="visibility-type-grid">
          {entityTypes.map((item) => {
            const isHidden = hiddenTypes.has(item.kind);
            const isGrouped = groupedTypes.has(item.kind);
            return (
              <div
                key={item.kind}
                role="button"
                tabIndex={0}
                className={`visibility-type-card ${isHidden ? 'is-hidden' : 'is-visible'}`}
                onClick={() => handleToggleKind(item.kind)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' || e.key === ' ') {
                    handleToggleKind(item.kind);
                  }
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
                  {toggleTypeGrouping && (
                    <span
                      role="button"
                      tabIndex={0}
                      className={`visibility-type-group-btn ${isGrouped ? 'is-active' : ''}`}
                      onClick={(e) => {
                        e.stopPropagation();
                        toggleTypeGrouping(item.kind);
                      }}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter' || e.key === ' ') {
                          e.stopPropagation();
                          toggleTypeGrouping(item.kind);
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
      </div>

      {/* 2. Node Groups */}
      <div className="visibility-section">
        <div className="visibility-section-header" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div>
            <span className="visibility-section-title">Node Groups</span>
            <span className="visibility-section-hint">Collapse entities into summary discs</span>
          </div>
          <button
            type="button"
            className="visibility-new-group-btn"
            onClick={() => setIsCreatingGroup((prev) => !prev)}
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

        {/* Active Groups List */}
        <div className="visibility-groups-list" style={{ display: 'flex', flexDirection: 'column', gap: 6, marginTop: 4 }}>
          {/* Active Category Groups */}
          {Array.from(groupedTypes).map((kind) => {
            const count = Array.from(nodeDetailMap.values()).filter(
              (n) => (n.kind === kind || (kind === 'App' && n.kind === 'Ingress')) && !hiddenNodeIds.has(n.id)
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
                  onClick={() => toggleTypeGrouping && toggleTypeGrouping(kind)}
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
                    onClick={() => toggleCustomGroupCollapse && toggleCustomGroupCollapse(group.id)}
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
                    onClick={() => deleteCustomGroup && deleteCustomGroup(group.id)}
                    title="Ungroup / Delete group"
                    style={{
                      background: 'none',
                      border: 'none',
                      color: '#94a3b8',
                      fontSize: '12px',
                      padding: '1px 4px',
                      cursor: 'pointer',
                      lineHeight: 1,
                    }}
                  >
                    ✕
                  </button>
                </div>
              </div>
            );
          })}

          {groupedTypes.size === 0 && customGroups.length === 0 && !isCreatingGroup && (
            <div style={{ fontSize: '10.5px', color: '#64748b', fontStyle: 'italic', padding: '2px 0' }}>
              No active groups. Click ⊞ on an entity type above, Shift+Click nodes on canvas, or create a group below.
            </div>
          )}
        </div>

        {/* "+ New Group" Creator Form */}
        {isCreatingGroup && (
          <form
            onSubmit={handleCreateGroupSubmit}
            style={{
              marginTop: 8,
              padding: '8px',
              background: 'rgba(0, 0, 0, 0.25)',
              border: '1px solid rgba(56, 189, 248, 0.25)',
              borderRadius: 6,
              display: 'flex',
              flexDirection: 'column',
              gap: 8,
            }}
          >
            <input
              type="text"
              placeholder="Group name (e.g. Order Subsystem)..."
              value={newGroupName}
              onChange={(e) => setNewGroupName(e.target.value)}
              style={{
                width: '100%',
                boxSizing: 'border-box',
                background: 'rgba(255, 255, 255, 0.06)',
                border: '1px solid rgba(255, 255, 255, 0.12)',
                borderRadius: 4,
                padding: '4px 8px',
                fontSize: '11px',
                color: '#f8fafc',
                outline: 'none',
              }}
            />

            <input
              type="text"
              placeholder="Filter entities to select..."
              value={groupEntitySearch}
              onChange={(e) => setGroupEntitySearch(e.target.value)}
              style={{
                width: '100%',
                boxSizing: 'border-box',
                background: 'rgba(255, 255, 255, 0.04)',
                border: '1px solid rgba(255, 255, 255, 0.08)',
                borderRadius: 4,
                padding: '3px 6px',
                fontSize: '10.5px',
                color: '#cbd5e1',
                outline: 'none',
              }}
            />

            <div
              style={{
                maxHeight: 160,
                overflowY: 'auto',
                display: 'flex',
                flexDirection: 'column',
                gap: 2,
                padding: '2px 0',
              }}
            >
              {availableNodesForGroup.map((node) => {
                const isSelected = groupSelectedNodeIds.has(node.id);
                return (
                  <label
                    key={node.id}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 6,
                      fontSize: '11px',
                      padding: '3px 4px',
                      borderRadius: 3,
                      background: isSelected ? 'rgba(56, 189, 248, 0.12)' : 'transparent',
                      cursor: 'pointer',
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
                      style={{ cursor: 'pointer' }}
                    />
                    <EntityNodeBadge kind={node.kind} size={16} />
                    <span style={{ color: '#e2e8f0', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {node.displayName || node.name}
                    </span>
                  </label>
                );
              })}
            </div>

            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 6, paddingTop: 4 }}>
              <span style={{ fontSize: '10px', color: '#94a3b8' }}>
                {groupSelectedNodeIds.size} selected
              </span>
              <div style={{ display: 'flex', gap: 6 }}>
                <button
                  type="button"
                  onClick={() => setIsCreatingGroup(false)}
                  style={{
                    background: 'none',
                    border: 'none',
                    color: '#94a3b8',
                    fontSize: '11px',
                    cursor: 'pointer',
                    padding: '3px 8px',
                  }}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  disabled={groupSelectedNodeIds.size === 0}
                  onClick={handleCreateGroupSubmit}
                  style={{
                    background: groupSelectedNodeIds.size > 0 ? '#0284c7' : 'rgba(255, 255, 255, 0.08)',
                    color: groupSelectedNodeIds.size > 0 ? '#ffffff' : '#64748b',
                    border: 'none',
                    borderRadius: 4,
                    padding: '3px 10px',
                    fontSize: '11px',
                    fontWeight: 600,
                    cursor: groupSelectedNodeIds.size > 0 ? 'pointer' : 'default',
                  }}
                >
                  Create Group ({groupSelectedNodeIds.size})
                </button>
              </div>
            </div>
          </form>
        )}
      </div>

      {/* 3. Architectural Presets */}
      <div className="visibility-section">
        <div className="visibility-section-header">
          <span className="visibility-section-title">Architectural Presets</span>
          <span className="visibility-section-hint">High-level view filters</span>
        </div>
        <div className="visibility-presets-list">
          {props.customPresetsSlot}
          {/* Hide 1:1 Databases */}
          <button
            type="button"
            className={`visibility-preset-item ${hideSingleConnectionDbs ? 'is-active' : ''}`}
            onClick={toggleSingleConnDbs}
            title="Hide dedicated databases connected to only 1 service, revealing only shared data stores"
          >
            <div className="visibility-preset-main">
              <EntityNodeBadge kind="Database" size={20} />
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
      <div className="visibility-section" style={{ flex: '1 0 auto', minHeight: 0, display: 'flex', flexDirection: 'column' }}>
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

        <div className="domain-hidden-list">
          {filteredHiddenItems.map((item) => (
            <div
              key={item.id}
              className="domain-hidden-list-item"
              title={`${item.label} (${item.kind}, ${item.reasonTag}). Click ✕ to restore.`}
            >
              <EntityNodeBadge kind={item.kind} size={20} />
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
