import React, { useState, useMemo } from 'react';
import { SelectedNodeDetail, SelectedEdgeDetail, DomainProjectInfo, ConnectionDetailItem } from '../types';
import { useDomainArchitecture } from '../context/DomainArchitectureContext';
import { FloatingPanel } from '../../components/FloatingPanel';
import {
  ChevronRightIcon,
  ChevronDownIcon,
  EyeOffIcon,
  TagIcon,
  GitBranchIcon,
  ExternalLinkIcon,
} from '../../components/Icons';

export interface RawEdgeInfo {
  id: string;
  source: string;
  target: string;
  category: 'service_call' | 'database' | 'messaging' | 'external';
  label: string;
  count: number;
  kind?: string;
}

export interface NodeConnectionItem {
  id: string;
  name: string;
  kindTag: string;
  bgColor: string;
  count: number;
  label?: string;
}

export interface DomainInspectorProps {
  selectedNode?: SelectedNodeDetail | null;
  selectedEdge?: SelectedEdgeDetail | null;
  nodeDetailMap?: Map<string, SelectedNodeDetail>;
  rawEdges?: RawEdgeInfo[];
  onClearGraphHighlight?: () => void;
  onSelectNode?: (node: SelectedNodeDetail | null) => void;
  onSelectNodeById?: (id: string) => void;
  onSelectEdge?: (edge: SelectedEdgeDetail | null) => void;
  onClearSelection?: () => void;
  isInspectorCollapsed?: boolean;
  onToggleCollapse?: (collapsed: boolean) => void;
  isEdgeInspectorCollapsed?: boolean;
  onToggleEdgeCollapse?: (collapsed: boolean) => void;
  onHideNode?: (id: string) => void;
  onFocusInFlow?: (name: string) => void;
  onOpenFile?: (path: string, line?: number) => void;
  onOpenOverrideModal?: (name: string) => void;
  docked?: boolean;
  onToggleDock?: () => void;
}

export const DomainInspector: React.FC<DomainInspectorProps> = (props) => {
  let ctx: any = null;
  try {
    ctx = useDomainArchitecture();
  } catch {}

  const selectedNode = props.selectedNode !== undefined ? props.selectedNode : ctx?.selectedNode;
  const selectedEdge = props.selectedEdge !== undefined ? props.selectedEdge : ctx?.selectedEdge;
  const selectNode = props.onSelectNode || ctx?.selectNode;
  const selectEdge = props.onSelectEdge || ctx?.selectEdge;
  const clearSelection = props.onClearSelection || ctx?.clearSelection;
  const isInspectorCollapsed = props.isInspectorCollapsed !== undefined ? props.isInspectorCollapsed : !!ctx?.isInspectorCollapsed;
  const setIsInspectorCollapsed = props.onToggleCollapse || ctx?.setIsInspectorCollapsed;
  const hideNode = props.onHideNode || ctx?.hideNode;
  const onFocusInFlow = props.onFocusInFlow || ctx?.onFocusInFlow;
  const onOpenFile = props.onOpenFile || ctx?.onOpenFile;
  const openOverrideModal = props.onOpenOverrideModal || ctx?.openOverrideModal;
  const nodeDetailMap = props.nodeDetailMap;
  const onClearGraphHighlight = props.onClearGraphHighlight;
  const rawEdges = props.rawEdges;

  // Local state for edge inspector collapse if not controlled from parent
  const [localEdgeCollapsed, setLocalEdgeCollapsed] = useState(false);
  const isEdgeInspectorCollapsed = props.isEdgeInspectorCollapsed !== undefined ? props.isEdgeInspectorCollapsed : localEdgeCollapsed;
  const setIsEdgeInspectorCollapsed = props.onToggleEdgeCollapse || setLocalEdgeCollapsed;

  // Accordion section collapse state
  const [collapsedSections, setCollapsedSections] = useState<Record<string, boolean>>({});

  const toggleSection = (key: string) => {
    setCollapsedSections((prev) => ({
      ...prev,
      [key]: !prev[key],
    }));
  };

  const handleClose = () => {
    clearSelection?.();
    onClearGraphHighlight?.();
  };

  const handleSelectNodeById = (nodeId: string) => {
    if (props.onSelectNodeById) {
      props.onSelectNodeById(nodeId);
      return;
    }
    const targetNode = nodeDetailMap?.get(nodeId);
    if (targetNode && selectNode) {
      selectNode(targetNode);
    }
  };

  // 1. Inbound Callers
  const inboundConnections = useMemo<NodeConnectionItem[]>(() => {
    if (!selectedNode || !rawEdges) return [];
    const map = new Map<string, NodeConnectionItem>();
    for (const e of rawEdges) {
      if (e.target === selectedNode.id && (e.category === 'service_call' || e.category === 'external')) {
        const sourceDetail = nodeDetailMap?.get(e.source);
        const name = sourceDetail?.displayName || e.source;
        const existing = map.get(e.source);
        if (existing) {
          existing.count += e.count;
        } else {
          map.set(e.source, {
            id: e.source,
            name,
            kindTag: sourceDetail?.displayTag || ':CALLER',
            bgColor: sourceDetail?.bgColor || '#38bdf8',
            count: e.count,
            label: e.label,
          });
        }
      }
    }
    return Array.from(map.values()).sort((a, b) => b.count - a.count);
  }, [selectedNode, rawEdges, nodeDetailMap]);

  // 2. Outbound Targets
  const outboundConnections = useMemo<NodeConnectionItem[]>(() => {
    if (!selectedNode || !rawEdges) return [];
    const map = new Map<string, NodeConnectionItem>();
    for (const e of rawEdges) {
      if (e.source === selectedNode.id && (e.category === 'service_call' || e.category === 'external')) {
        const targetDetail = nodeDetailMap?.get(e.target);
        const name = targetDetail?.displayName || e.target;
        const existing = map.get(e.target);
        if (existing) {
          existing.count += e.count;
        } else {
          map.set(e.target, {
            id: e.target,
            name,
            kindTag: targetDetail?.displayTag || ':SERVICE',
            bgColor: targetDetail?.bgColor || '#e53935',
            count: e.count,
            label: e.label,
          });
        }
      }
    }
    return Array.from(map.values()).sort((a, b) => b.count - a.count);
  }, [selectedNode, rawEdges, nodeDetailMap]);

  // 3. Database Connections
  const dbConnections = useMemo<NodeConnectionItem[]>(() => {
    if (!selectedNode || !rawEdges) return [];
    const map = new Map<string, NodeConnectionItem>();
    for (const e of rawEdges) {
      if (e.source === selectedNode.id && e.category === 'database') {
        const targetDetail = nodeDetailMap?.get(e.target);
        const name = targetDetail?.displayName || e.target;
        const existing = map.get(e.target);
        if (existing) {
          existing.count += e.count;
        } else {
          map.set(e.target, {
            id: e.target,
            name,
            kindTag: targetDetail?.displayTag || ':DB',
            bgColor: targetDetail?.bgColor || '#a855f7',
            count: e.count,
          });
        }
      } else if (e.target === selectedNode.id && e.category === 'database') {
        const sourceDetail = nodeDetailMap?.get(e.source);
        const name = sourceDetail?.displayName || e.source;
        const existing = map.get(e.source);
        if (existing) {
          existing.count += e.count;
        } else {
          map.set(e.source, {
            id: e.source,
            name,
            kindTag: sourceDetail?.displayTag || ':SERVICE',
            bgColor: sourceDetail?.bgColor || '#38bdf8',
            count: e.count,
          });
        }
      }
    }
    return Array.from(map.values()).sort((a, b) => b.count - a.count);
  }, [selectedNode, rawEdges, nodeDetailMap]);

  // 4. Topic / Messaging Connections
  const topicConnections = useMemo<NodeConnectionItem[]>(() => {
    if (!selectedNode || !rawEdges) return [];
    const map = new Map<string, NodeConnectionItem>();
    for (const e of rawEdges) {
      if (e.category !== 'messaging') continue;

      const isSource = e.source === selectedNode.id;
      const isTarget = e.target === selectedNode.id;
      if (!isSource && !isTarget) continue;

      const otherId = isSource ? e.target : e.source;
      const otherDetail = nodeDetailMap?.get(otherId);
      const name = otherDetail?.displayName || otherId;

      const k = (e.kind || e.label || '').toUpperCase();
      const isSub = k === 'SUBSCRIBES_TO' || k === 'SUBSCRIBED_BY' || k === 'SUBSCRIBES' || k === 'CONSUMES' || k === 'CONSUMES_FROM';

      let label = 'Publishes';
      if (selectedNode.kind === 'Topic') {
        label = isSub ? 'Subscriber' : 'Publisher';
      } else {
        label = isSub ? 'Consumes' : 'Publishes';
      }

      const existing = map.get(otherId);
      if (existing) {
        existing.count += e.count;
      } else {
        map.set(otherId, {
          id: otherId,
          name,
          kindTag: otherDetail?.displayTag || ':TOPIC',
          bgColor: otherDetail?.bgColor || '#f59e0b',
          count: e.count,
          label,
        });
      }
    }
    return Array.from(map.values()).sort((a, b) => b.count - a.count);
  }, [selectedNode, rawEdges, nodeDetailMap]);

  if (!selectedNode && !selectedEdge) {
    return null;
  }

  return (
    <>
      {/* Node Inspector */}
      {selectedNode && (
        <FloatingPanel
          dock="top-right"
          docked={props.docked}
          onToggleDock={props.onToggleDock}
          width={360}
          minWidth={280}
          maxWidth={750}
          persistKey="ce_node_inspector"
          className="no-body-padding"
          isCollapsed={isInspectorCollapsed}
          onToggleCollapse={setIsInspectorCollapsed}
          onClose={handleClose}
          closeTooltip="Deselect"
          collapseTooltip="Collapse inspector"
          expandTooltip={`Click to expand details for ${selectedNode.displayName}`}
          title={selectedNode.displayName}
          badge={
            <span className="inspector-badge" style={{ backgroundColor: selectedNode.bgColor }}>
              {selectedNode.displayTag}
            </span>
          }
          headerActions={
            hideNode ? (
              <button
                type="button"
                className="floating-dock-btn action-btn hide-btn"
                onClick={() => hideNode(selectedNode.id)}
                title="Hide this node (Transitive connections bypass it) [Shortcut: H, Del]"
              >
                <EyeOffIcon size={12} />
                <span>Hide</span>
              </button>
            ) : undefined
          }
          subtitle={
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6, width: '100%' }}>
              {selectedNode.tierLabel && (
                <div style={{ display: 'flex', alignItems: 'center' }}>
                  <span
                    className="inspector-tier-chip"
                    style={{
                      color:
                        selectedNode.tier === 0
                          ? '#38bdf8'
                          : selectedNode.tier === 1
                          ? '#4ade80'
                          : selectedNode.tier === 2
                          ? '#fbbf24'
                          : selectedNode.tier === 3
                          ? '#c084fc'
                          : '#94a3b8',
                    }}
                    title={`Architectural Echelon: ${selectedNode.tierLabel}`}
                  >
                    {selectedNode.tierLabel}
                  </span>
                </div>
              )}
              <div className="inspector-meta-row">
                {selectedNode.framework && (
                  <span className="inspector-meta-tag">{selectedNode.framework}</span>
                )}
                {selectedNode.language && (
                  <span className="inspector-meta-tag">{selectedNode.language}</span>
                )}
                {selectedNode.gitBranch && (
                  <span className="inspector-meta-tag branch">
                    <GitBranchIcon size={11} /> {selectedNode.gitBranch}
                  </span>
                )}
                {selectedNode.domain && (
                  <span className="inspector-meta-tag domain">
                    <TagIcon size={11} /> {selectedNode.domain}
                  </span>
                )}
              </div>
            </div>
          }
          collapsedTitle={selectedNode.displayName}
          collapsedBadge={
            <span className="inspector-badge" style={{ backgroundColor: selectedNode.bgColor }}>
              {selectedNode.displayTag}
            </span>
          }
        >
          {/* Inspector Body */}
          <div className="inspector-body">
                {/* Quick Actions Row */}
                <div className="inspector-actions">
                  {(selectedNode.kind === 'Service' || selectedNode.kind === 'Ingress') && onFocusInFlow && (
                    <button
                      type="button"
                      className="inspector-action-btn primary"
                      onClick={() => onFocusInFlow(selectedNode.name)}
                      title="Drill down to Project Flow view"
                    >
                      Explore in Flow ➔
                    </button>
                  )}
                  {(selectedNode.kind === 'Service' || selectedNode.kind === 'Ingress') && openOverrideModal && (
                    <button
                      type="button"
                      className="inspector-action-btn secondary"
                      onClick={() => openOverrideModal(selectedNode.name)}
                      title="Override or reassign service domain and bounded context"
                    >
                      <TagIcon size={12} /> Override Domain
                    </button>
                  )}
                  {selectedNode.primaryFilePath && onOpenFile && (
                    <button
                      type="button"
                      className="inspector-action-btn secondary"
                      onClick={() => onOpenFile(selectedNode.primaryFilePath!, selectedNode.primaryLineStart || 1)}
                      title="Open source file in editor"
                    >
                      <ExternalLinkIcon size={12} /> Open Source
                    </button>
                  )}
                </div>

                {/* Metrics Grid */}
                {(selectedNode.kind === 'Service' || selectedNode.kind === 'Ingress' || selectedNode.kind === 'Worker' || selectedNode.kind === 'App') && (
                  <div className="inspector-metrics-grid">
                    <div className="inspector-metric-card" title="Inbound calls from other services">
                      <span className="metric-val">{selectedNode.inboundCallsCount}</span>
                      <span className="metric-lbl">Inbound Calls</span>
                    </div>
                    <div className="inspector-metric-card" title="Outbound calls to downstream services">
                      <span className="metric-val">{selectedNode.outboundCallsCount}</span>
                      <span className="metric-lbl">Outbound Calls</span>
                    </div>
                    <div className="inspector-metric-card" title="Databases used directly">
                      <span className="metric-val">{selectedNode.dbCount}</span>
                      <span className="metric-lbl">Databases</span>
                    </div>
                    <div className="inspector-metric-card" title="Topics published or subscribed">
                      <span className="metric-val">{selectedNode.messagingCount}</span>
                      <span className="metric-lbl">Topics</span>
                    </div>
                  </div>
                )}

                {/* Accordion: Inbound Calls */}
                {(selectedNode.kind === 'Service' || selectedNode.kind === 'Worker' || selectedNode.kind === 'Ingress' || inboundConnections.length > 0) && (
                  <div className={`inspector-section ${collapsedSections['inbound'] ? 'is-collapsed' : 'is-expanded'}`}>
                    <div
                      className="inspector-section-header"
                      onClick={() => toggleSection('inbound')}
                      title={collapsedSections['inbound'] ? 'Expand inbound calls' : 'Collapse inbound calls'}
                    >
                      <span className="inspector-section-title">
                        Inbound Calls ({inboundConnections.length})
                      </span>
                      <span className="inspector-section-toggle">
                        {collapsedSections['inbound'] ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
                      </span>
                    </div>
                    {!collapsedSections['inbound'] && (
                      <div className="inspector-connection-list">
                        {inboundConnections.map((item) => (
                          <div
                            key={item.id}
                            className="inspector-connection-item"
                            onClick={() => handleSelectNodeById(item.id)}
                            title={`Jump to caller ${item.name}`}
                          >
                            <div className="connection-item-main">
                              <span
                                className="inspector-badge"
                                style={{ backgroundColor: item.bgColor, fontSize: '8.5px', padding: '1px 5px' }}
                              >
                                {item.kindTag}
                              </span>
                              <span className="connection-item-name">{item.name}</span>
                            </div>
                            <div className="connection-item-meta">
                              <span className="connection-count-badge">
                                {item.count} {item.count === 1 ? 'call' : 'calls'}
                              </span>
                              <span className="connection-jump-arrow">➔</span>
                            </div>
                          </div>
                        ))}
                        {inboundConnections.length === 0 && (
                          <div className="inspector-empty-hint">No inbound calls</div>
                        )}
                      </div>
                    )}
                  </div>
                )}

                {/* Accordion: Outbound Calls */}
                {(selectedNode.kind === 'Service' || selectedNode.kind === 'Worker' || selectedNode.kind === 'Ingress' || selectedNode.kind === 'App' || outboundConnections.length > 0) && (
                  <div className={`inspector-section ${collapsedSections['outbound'] ? 'is-collapsed' : 'is-expanded'}`}>
                    <div
                      className="inspector-section-header"
                      onClick={() => toggleSection('outbound')}
                      title={collapsedSections['outbound'] ? 'Expand outbound calls' : 'Collapse outbound calls'}
                    >
                      <span className="inspector-section-title">
                        Outbound Calls ({outboundConnections.length})
                      </span>
                      <span className="inspector-section-toggle">
                        {collapsedSections['outbound'] ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
                      </span>
                    </div>
                    {!collapsedSections['outbound'] && (
                      <div className="inspector-connection-list">
                        {outboundConnections.map((item) => (
                          <div
                            key={item.id}
                            className="inspector-connection-item"
                            onClick={() => handleSelectNodeById(item.id)}
                            title={`Jump to downstream service ${item.name}`}
                          >
                            <div className="connection-item-main">
                              <span
                                className="inspector-badge"
                                style={{ backgroundColor: item.bgColor, fontSize: '8.5px', padding: '1px 5px' }}
                              >
                                {item.kindTag}
                              </span>
                              <span className="connection-item-name">{item.name}</span>
                            </div>
                            <div className="connection-item-meta">
                              <span className="connection-count-badge">
                                {item.count} {item.count === 1 ? 'call' : 'calls'}
                              </span>
                              <span className="connection-jump-arrow">➔</span>
                            </div>
                          </div>
                        ))}
                        {outboundConnections.length === 0 && (
                          <div className="inspector-empty-hint">No outbound calls</div>
                        )}
                      </div>
                    )}
                  </div>
                )}

                {/* Accordion: Databases */}
                {(dbConnections.length > 0 || selectedNode.dbCount > 0) && (
                  <div className={`inspector-section ${collapsedSections['databases'] ? 'is-collapsed' : 'is-expanded'}`}>
                    <div
                      className="inspector-section-header"
                      onClick={() => toggleSection('databases')}
                      title={collapsedSections['databases'] ? 'Expand databases' : 'Collapse databases'}
                    >
                      <span className="inspector-section-title">
                        {selectedNode.kind === 'Database' ? 'Connected Services' : 'Databases'} ({dbConnections.length})
                      </span>
                      <span className="inspector-section-toggle">
                        {collapsedSections['databases'] ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
                      </span>
                    </div>
                    {!collapsedSections['databases'] && (
                      <div className="inspector-connection-list">
                        {dbConnections.map((item) => (
                          <div
                            key={item.id}
                            className="inspector-connection-item"
                            onClick={() => handleSelectNodeById(item.id)}
                            title={`Jump to ${item.name}`}
                          >
                            <div className="connection-item-main">
                              <span
                                className="inspector-badge"
                                style={{ backgroundColor: item.bgColor, fontSize: '8.5px', padding: '1px 5px' }}
                              >
                                {item.kindTag}
                              </span>
                              <span className="connection-item-name">{item.name}</span>
                            </div>
                            <div className="connection-item-meta">
                              <span className="connection-count-badge">{item.count} links</span>
                              <span className="connection-jump-arrow">➔</span>
                            </div>
                          </div>
                        ))}
                        {dbConnections.length === 0 && (
                          <div className="inspector-empty-hint">No database connections</div>
                        )}
                      </div>
                    )}
                  </div>
                )}

                {/* Accordion: Topics & Queues */}
                {(topicConnections.length > 0 || selectedNode.messagingCount > 0) && (
                  <div className={`inspector-section ${collapsedSections['topics'] ? 'is-collapsed' : 'is-expanded'}`}>
                    <div
                      className="inspector-section-header"
                      onClick={() => toggleSection('topics')}
                      title={collapsedSections['topics'] ? 'Expand topics' : 'Collapse topics'}
                    >
                      <span className="inspector-section-title">
                        {selectedNode.kind === 'Topic' ? 'Connected Services' : 'Topics & Queues'} ({topicConnections.length})
                      </span>
                      <span className="inspector-section-toggle">
                        {collapsedSections['topics'] ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
                      </span>
                    </div>
                    {!collapsedSections['topics'] && (
                      <div className="inspector-connection-list">
                        {topicConnections.map((item) => (
                          <div
                            key={item.id}
                            className="inspector-connection-item"
                            onClick={() => handleSelectNodeById(item.id)}
                            title={`Jump to ${item.name}`}
                          >
                            <div className="connection-item-main">
                              <span
                                className="inspector-badge"
                                style={{ backgroundColor: item.bgColor, fontSize: '8.5px', padding: '1px 5px' }}
                              >
                                {item.kindTag}
                              </span>
                              <span className="connection-item-name">{item.name}</span>
                            </div>
                            <div className="connection-item-meta">
                              {item.label && <span className="connection-count-badge">{item.label}</span>}
                              <span className="connection-jump-arrow">➔</span>
                            </div>
                          </div>
                        ))}
                        {topicConnections.length === 0 && (
                          <div className="inspector-empty-hint">
                            {selectedNode.kind === 'Topic' ? 'No connected services' : 'No message topics'}
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                )}

                {/* Accordion: Clustered Subprojects */}
                {selectedNode.projects && selectedNode.projects.length > 1 && (
                  <div className={`inspector-section ${collapsedSections['projects'] ? 'is-collapsed' : 'is-expanded'}`}>
                    <div
                      className="inspector-section-header"
                      onClick={() => toggleSection('projects')}
                      title={collapsedSections['projects'] ? 'Expand clustered projects' : 'Collapse clustered projects'}
                    >
                      <span className="inspector-section-title">
                        Clustered Projects ({selectedNode.projects.length})
                      </span>
                      <span className="inspector-section-toggle">
                        {collapsedSections['projects'] ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
                      </span>
                    </div>
                    {!collapsedSections['projects'] && (
                      <div className="inspector-subprojects-list">
                        {selectedNode.projects.map((p: DomainProjectInfo) => (
                          <div
                            key={p.id}
                            className="inspector-subproject-item"
                            onClick={() => p.filePath && onOpenFile?.(p.filePath, p.lineStart || 1)}
                            title={p.filePath || p.name}
                          >
                            <span className="subproject-dot">•</span>
                            <span className="subproject-name">{p.name}</span>
                            {p.isLibrary && <span className="subproject-lib-tag">lib</span>}
                            {p.gitBranch && (
                              <span
                                className="subproject-lib-tag"
                                style={{ background: 'rgba(56, 189, 248, 0.2)', color: '#38bdf8' }}
                              >
                                <GitBranchIcon size={11} /> {p.gitBranch}
                              </span>
                            )}
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )}

                {/* Accordion: Physical Source Declaration */}
                {selectedNode.primaryFilePath && (
                  <div className={`inspector-section ${collapsedSections['source'] ? 'is-collapsed' : 'is-expanded'}`}>
                    <div
                      className="inspector-section-header"
                      onClick={() => toggleSection('source')}
                      title={collapsedSections['source'] ? 'Expand source location' : 'Collapse source location'}
                    >
                      <span className="inspector-section-title">Source Declaration</span>
                      <span className="inspector-section-toggle">
                        {collapsedSections['source'] ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
                      </span>
                    </div>
                    {!collapsedSections['source'] && (
                      <div
                        className="inspector-subproject-item"
                        style={{ cursor: onOpenFile ? 'pointer' : 'default', padding: '6px 8px' }}
                        onClick={() => onOpenFile?.(selectedNode.primaryFilePath!, selectedNode.primaryLineStart || 1)}
                        title={`Open ${selectedNode.primaryFilePath}${selectedNode.primaryLineStart ? `:${selectedNode.primaryLineStart}` : ''}`}
                      >
                        <ExternalLinkIcon size={11} />
                        <span className="subproject-name" style={{ fontFamily: 'monospace', fontSize: '11px', marginLeft: 6 }}>
                          {selectedNode.primaryFilePath}
                          {selectedNode.primaryLineStart ? `:${selectedNode.primaryLineStart}` : ''}
                        </span>
                      </div>
                    )}
                  </div>
                )}
              </div>
        </FloatingPanel>
      )}

      {/* Edge / Connection Inspector */}
      {selectedEdge && (
        <FloatingPanel
          dock="top-right"
          docked={props.docked}
          onToggleDock={props.onToggleDock}
          width={360}
          minWidth={280}
          maxWidth={750}
          persistKey="ce_edge_inspector"
          className="no-body-padding domain-edge-inspector-floating-panel"
          isCollapsed={isEdgeInspectorCollapsed}
          onToggleCollapse={setIsEdgeInspectorCollapsed}
          onClose={handleClose}
          closeTooltip="Deselect Connection"
          collapseTooltip="Collapse connection inspector"
          expandTooltip={`Click to expand connection: ${selectedEdge.sourceName} ➔ ${selectedEdge.targetName}`}
          title={`${selectedEdge.sourceName} ➔ ${selectedEdge.targetName}`}
          badge={
            <span
              className="floating-dock-badge"
              style={{
                backgroundColor: selectedEdge.isBidirectional ? 'rgba(56, 189, 248, 0.2)' : 'rgba(251, 191, 36, 0.2)',
                color: selectedEdge.isBidirectional ? '#38bdf8' : '#fbbf24',
                border: `1px solid ${selectedEdge.isBidirectional ? 'rgba(56, 189, 248, 0.4)' : 'rgba(251, 191, 36, 0.4)'}`,
              }}
            >
              {selectedEdge.isBidirectional ? '⇄ Bi-directional' : '➔ Directional'}
            </span>
          }
          subtitle={
            <div className="inspector-edge-endpoints" style={{ width: '100%', marginTop: 2 }}>
              <div
                className="inspector-edge-endpoint-btn"
                onClick={() => handleSelectNodeById(selectedEdge.sourceId)}
                title={`Inspect ${selectedEdge.sourceName}`}
              >
                <span
                  className="inspector-badge"
                  style={{ backgroundColor: selectedEdge.sourceBgColor, padding: '1px 5px', fontSize: '9px' }}
                >
                  {selectedEdge.sourceTag}
                </span>
                <span className="endpoint-name">{selectedEdge.sourceName}</span>
              </div>

              <span className="endpoint-arrow">{selectedEdge.isBidirectional ? '⇄' : '➔'}</span>

              <div
                className="inspector-edge-endpoint-btn"
                onClick={() => handleSelectNodeById(selectedEdge.targetId)}
                title={`Inspect ${selectedEdge.targetName}`}
              >
                <span
                  className="inspector-badge"
                  style={{ backgroundColor: selectedEdge.targetBgColor, padding: '1px 5px', fontSize: '9px' }}
                >
                  {selectedEdge.targetTag}
                </span>
                <span className="endpoint-name">{selectedEdge.targetName}</span>
              </div>
            </div>
          }
          collapsedTitle={`${selectedEdge.sourceName} ➔ ${selectedEdge.targetName}`}
          collapsedBadge={
            <span
              className="floating-dock-badge"
              style={{ backgroundColor: 'rgba(56, 189, 248, 0.25)', color: '#38bdf8', padding: '1px 6px', fontSize: '10px' }}
            >
              {selectedEdge.totalInteractions}
            </span>
          }
        >

              {/* Edge Metrics Summary */}
              <div className="inspector-section" style={{ padding: '8px 12px', borderBottom: '1px solid rgba(255, 255, 255, 0.08)' }}>
                <div className="domain-inspector-metrics-grid" style={{ display: 'grid', gridTemplateColumns: selectedEdge.transitiveCount > 0 ? 'repeat(4, 1fr)' : 'repeat(3, 1fr)', gap: 6 }}>
                  <div className="metric-box">
                    <span className="metric-val" style={{ color: '#f8fafc' }}>
                      {selectedEdge.totalInteractions}
                    </span>
                    <span className="metric-lbl">Total Links</span>
                  </div>
                  <div className="metric-box">
                    <span className="metric-val" style={{ color: '#38bdf8' }}>
                      {selectedEdge.directCallsCount}
                    </span>
                    <span className="metric-lbl">Direct Calls</span>
                  </div>
                  <div className="metric-box">
                    <span className="metric-val" style={{ color: '#fbbf24' }}>
                      {selectedEdge.messagingCount}
                    </span>
                    <span className="metric-lbl">Events / Topics</span>
                  </div>
                  {selectedEdge.transitiveCount > 0 && (
                    <div className="metric-box">
                      <span className="metric-val" style={{ color: '#c084fc' }}>
                        {selectedEdge.transitiveCount}
                      </span>
                      <span className="metric-lbl">Transitive</span>
                    </div>
                  )}
                </div>
              </div>

              {/* Edge Breakdown List */}
              <div
                className="inspector-body"
                style={{
                  overflowY: 'auto',
                  flex: 1,
                  minHeight: 0,
                  padding: '10px 12px',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 8,
                }}
              >
                <div style={{ fontSize: '10.5px', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.05em', color: '#94a3b8' }}>
                  Connection Details ({selectedEdge.items.length})
                </div>

                {selectedEdge.items.map((item: ConnectionDetailItem, idx: number) => {
                  const isDirectCall = item.category === 'service_call' && !item.isTransitive;
                  const isMsg = item.category === 'messaging';
                  const isDb = item.category === 'database';
                  const tagColor = isDirectCall
                    ? '#38bdf8'
                    : isMsg
                    ? '#fbbf24'
                    : isDb
                    ? '#c084fc'
                    : '#34d399';
                  const tagLabel = isDirectCall ? 'RPC' : isMsg ? 'EVENT' : isDb ? 'DB' : item.kind;

                  return (
                    <div
                      key={idx}
                      style={{
                        padding: '8px 10px',
                        borderRadius: 6,
                        background: 'rgba(255, 255, 255, 0.03)',
                        border: '1px solid rgba(255, 255, 255, 0.06)',
                        display: 'flex',
                        flexDirection: 'column',
                        gap: 4,
                      }}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                          <span
                            style={{
                              fontSize: '9.5px',
                              fontWeight: 700,
                              padding: '1px 6px',
                              borderRadius: 4,
                              background: tagColor,
                              color: '#000',
                              textTransform: 'uppercase',
                            }}
                          >
                            {tagLabel}
                          </span>
                          {item.isTransitive && (
                            <span
                              style={{
                                fontSize: '8.5px',
                                fontWeight: 700,
                                padding: '1px 5px',
                                borderRadius: 4,
                                background: 'rgba(192, 132, 252, 0.25)',
                                color: '#c084fc',
                                border: '1px solid rgba(192, 132, 252, 0.4)',
                                textTransform: 'uppercase',
                              }}
                            >
                              TRANSITIVE
                            </span>
                          )}
                          {item.isInferred && (
                            <span
                              style={{
                                fontSize: '8.5px',
                                fontWeight: 700,
                                padding: '1px 5px',
                                borderRadius: 4,
                                background: 'rgba(56, 189, 248, 0.2)',
                                color: '#38bdf8',
                                border: '1px solid rgba(56, 189, 248, 0.4)',
                                textTransform: 'uppercase',
                              }}
                            >
                              INFERRED
                            </span>
                          )}
                        </div>
                        <span style={{ fontSize: '11px', color: '#94a3b8' }}>
                          {item.count} {item.count === 1 ? 'call' : 'calls'}
                        </span>
                      </div>
                      <div style={{ fontSize: '12px', fontWeight: 500, color: '#f1f5f9' }}>{item.label}</div>
                      {item.viaNames && item.viaNames.length > 0 && (
                        <div style={{ fontSize: '10px', color: '#64748b' }}>
                          Via: {item.viaNames.join(' ➔ ')}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>

              {/* Quick Action Footer */}
              {onFocusInFlow && (
                <div className="inspector-actions" style={{ padding: '8px 12px', borderTop: '1px solid rgba(255, 255, 255, 0.08)', display: 'flex', gap: 6 }}>
                  <button
                    type="button"
                    className="inspector-action-btn secondary"
                    style={{ flex: 1, fontSize: '11px', padding: '4px 6px' }}
                    onClick={() => onFocusInFlow(selectedEdge.sourceName)}
                    title={`Focus ${selectedEdge.sourceName} in Project Flow`}
                  >
                    Focus {selectedEdge.sourceName}
                  </button>
                  <button
                    type="button"
                    className="inspector-action-btn secondary"
                    style={{ flex: 1, fontSize: '11px', padding: '4px 6px' }}
                    onClick={() => onFocusInFlow(selectedEdge.targetName)}
                    title={`Focus ${selectedEdge.targetName} in Project Flow`}
                  >
                    Focus {selectedEdge.targetName}
                  </button>
                </div>
              )}
        </FloatingPanel>
      )}
    </>
  );
};
