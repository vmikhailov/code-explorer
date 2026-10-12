import React, { useState, useMemo } from 'react';
import { SelectedNodeDetail, SelectedEdgeDetail, DomainProjectInfo, ConnectionDetailItem } from '../types';
import { useDomainArchitecture } from '../context/DomainArchitectureContext';

export interface RawEdgeInfo {
  id: string;
  source: string;
  target: string;
  category: 'service_call' | 'database' | 'messaging' | 'external';
  label: string;
  count: number;
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
}

const EyeOffIcon: React.FC<{ size?: number; className?: string }> = ({ size = 12, className = '' }) => (
  <svg
    width={size}
    height={size}
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth="2"
    strokeLinecap="round"
    strokeLinejoin="round"
    className={className}
    style={{ display: 'inline-block', verticalAlign: 'middle', flexShrink: 0 }}
  >
    <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" />
    <line x1="1" y1="1" x2="23" y2="23" />
  </svg>
);

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
      if (e.source === selectedNode.id && e.category === 'messaging') {
        const targetDetail = nodeDetailMap?.get(e.target);
        const name = targetDetail?.displayName || e.target;
        const existing = map.get(e.target);
        if (existing) {
          existing.count += e.count;
        } else {
          map.set(e.target, {
            id: e.target,
            name,
            kindTag: targetDetail?.displayTag || ':TOPIC',
            bgColor: targetDetail?.bgColor || '#f59e0b',
            count: e.count,
            label: 'Publishes',
          });
        }
      } else if (e.target === selectedNode.id && e.category === 'messaging') {
        const sourceDetail = nodeDetailMap?.get(e.source);
        const name = sourceDetail?.displayName || e.source;
        const existing = map.get(e.source);
        if (existing) {
          existing.count += e.count;
        } else {
          map.set(e.source, {
            id: e.source,
            name,
            kindTag: sourceDetail?.displayTag || ':TOPIC',
            bgColor: sourceDetail?.bgColor || '#f59e0b',
            count: e.count,
            label: selectedNode.kind === 'Topic' ? 'Publisher' : 'Consumes',
          });
        }
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
        <aside className={`domain-inspector-panel ${isInspectorCollapsed ? 'is-collapsed' : ''}`}>
          {isInspectorCollapsed ? (
            <div
              className="domain-inspector-collapsed-badge"
              onClick={() => setIsInspectorCollapsed(false)}
              title={`Click to expand details for ${selectedNode.displayName}`}
            >
              <span className="inspector-badge" style={{ backgroundColor: selectedNode.bgColor }}>
                {selectedNode.displayTag}
              </span>
              <span className="collapsed-title">{selectedNode.displayName}</span>
              <span className="collapsed-expand-arrow">▶</span>
              <button
                type="button"
                className="inspector-close-btn"
                onClick={(e) => {
                  e.stopPropagation();
                  handleClose();
                }}
                title="Deselect"
              >
                ✕
              </button>
            </div>
          ) : (
            <>
              {/* Two-Row Inspector Header */}
              <div className="domain-inspector-header">
                {/* Row 1: Badges & Action Buttons */}
                <div className="domain-inspector-header-top">
                  <div className="inspector-header-badges">
                    <span className="inspector-badge" style={{ backgroundColor: selectedNode.bgColor }}>
                      {selectedNode.displayTag}
                    </span>
                    {selectedNode.tierLabel && (
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
                        🎯 {selectedNode.tierLabel}
                      </span>
                    )}
                  </div>
                  <div className="inspector-header-actions">
                    {hideNode && (
                      <button
                        type="button"
                        className="inspector-header-icon-btn hide-btn"
                        onClick={() => hideNode(selectedNode.id)}
                        title="Hide this node (Transitive connections bypass it) [Shortcut: H, Del]"
                      >
                        <EyeOffIcon size={12} />
                        <span>Hide</span>
                      </button>
                    )}
                    <button
                      type="button"
                      className="inspector-header-icon-btn collapse-btn"
                      onClick={() => setIsInspectorCollapsed(true)}
                      title="Collapse inspector to badge"
                    >
                      ◀
                    </button>
                    <button
                      type="button"
                      className="inspector-header-icon-btn close-btn"
                      onClick={handleClose}
                      title="Deselect"
                    >
                      ✕
                    </button>
                  </div>
                </div>

                {/* Row 2: Full Title & Metadata Chips */}
                <div className="domain-inspector-header-info">
                  <h4 className="inspector-title" title={selectedNode.displayName}>
                    {selectedNode.displayName}
                  </h4>
                  <div className="inspector-meta-row">
                    {selectedNode.framework && (
                      <span className="inspector-meta-tag">{selectedNode.framework}</span>
                    )}
                    {selectedNode.language && (
                      <span className="inspector-meta-tag">{selectedNode.language}</span>
                    )}
                    {selectedNode.gitBranch && (
                      <span className="inspector-meta-tag branch">🌿 {selectedNode.gitBranch}</span>
                    )}
                    {selectedNode.domain && (
                      <span className="inspector-meta-tag domain">🏷️ {selectedNode.domain}</span>
                    )}
                  </div>
                </div>
              </div>

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
                      🏷️ Override Domain
                    </button>
                  )}
                  {selectedNode.primaryFilePath && onOpenFile && (
                    <button
                      type="button"
                      className="inspector-action-btn secondary"
                      onClick={() => onOpenFile(selectedNode.primaryFilePath!, selectedNode.primaryLineStart || 1)}
                      title="Open source file in editor"
                    >
                      📄 Open Source
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
                  <div className="inspector-section">
                    <div
                      className="inspector-section-header"
                      onClick={() => toggleSection('inbound')}
                      title={collapsedSections['inbound'] ? 'Expand inbound calls' : 'Collapse inbound calls'}
                    >
                      <span className="inspector-section-title">
                        Inbound Calls ({inboundConnections.length})
                      </span>
                      <span className="inspector-section-toggle">
                        {collapsedSections['inbound'] ? '▶' : '▼'}
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
                  <div className="inspector-section">
                    <div
                      className="inspector-section-header"
                      onClick={() => toggleSection('outbound')}
                      title={collapsedSections['outbound'] ? 'Expand outbound calls' : 'Collapse outbound calls'}
                    >
                      <span className="inspector-section-title">
                        Outbound Calls ({outboundConnections.length})
                      </span>
                      <span className="inspector-section-toggle">
                        {collapsedSections['outbound'] ? '▶' : '▼'}
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
                  <div className="inspector-section">
                    <div
                      className="inspector-section-header"
                      onClick={() => toggleSection('databases')}
                      title={collapsedSections['databases'] ? 'Expand databases' : 'Collapse databases'}
                    >
                      <span className="inspector-section-title">
                        {selectedNode.kind === 'Database' ? 'Connected Services' : 'Databases'} ({dbConnections.length})
                      </span>
                      <span className="inspector-section-toggle">
                        {collapsedSections['databases'] ? '▶' : '▼'}
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
                  <div className="inspector-section">
                    <div
                      className="inspector-section-header"
                      onClick={() => toggleSection('topics')}
                      title={collapsedSections['topics'] ? 'Expand topics' : 'Collapse topics'}
                    >
                      <span className="inspector-section-title">
                        Topics & Queues ({topicConnections.length})
                      </span>
                      <span className="inspector-section-toggle">
                        {collapsedSections['topics'] ? '▶' : '▼'}
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
                          <div className="inspector-empty-hint">No message topics</div>
                        )}
                      </div>
                    )}
                  </div>
                )}

                {/* Accordion: Clustered Subprojects */}
                {selectedNode.projects && selectedNode.projects.length > 1 && (
                  <div className="inspector-section">
                    <div
                      className="inspector-section-header"
                      onClick={() => toggleSection('projects')}
                      title={collapsedSections['projects'] ? 'Expand clustered projects' : 'Collapse clustered projects'}
                    >
                      <span className="inspector-section-title">
                        Clustered Projects ({selectedNode.projects.length})
                      </span>
                      <span className="inspector-section-toggle">
                        {collapsedSections['projects'] ? '▶' : '▼'}
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
                                🌿 {p.gitBranch}
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
                  <div className="inspector-section">
                    <div
                      className="inspector-section-header"
                      onClick={() => toggleSection('source')}
                      title={collapsedSections['source'] ? 'Expand source location' : 'Collapse source location'}
                    >
                      <span className="inspector-section-title">Source Declaration</span>
                      <span className="inspector-section-toggle">
                        {collapsedSections['source'] ? '▶' : '▼'}
                      </span>
                    </div>
                    {!collapsedSections['source'] && (
                      <div
                        className="inspector-subproject-item"
                        style={{ cursor: onOpenFile ? 'pointer' : 'default', padding: '6px 8px' }}
                        onClick={() => onOpenFile?.(selectedNode.primaryFilePath!, selectedNode.primaryLineStart || 1)}
                        title={`Open ${selectedNode.primaryFilePath}${selectedNode.primaryLineStart ? `:${selectedNode.primaryLineStart}` : ''}`}
                      >
                        <span className="subproject-dot">📄</span>
                        <span className="subproject-name" style={{ fontFamily: 'monospace', fontSize: '11px' }}>
                          {selectedNode.primaryFilePath}
                          {selectedNode.primaryLineStart ? `:${selectedNode.primaryLineStart}` : ''}
                        </span>
                      </div>
                    )}
                  </div>
                )}
              </div>
            </>
          )}
        </aside>
      )}

      {/* Edge / Connection Inspector */}
      {selectedEdge && (
        <aside className={`domain-inspector-panel domain-edge-inspector-panel ${isEdgeInspectorCollapsed ? 'is-collapsed' : ''}`}>
          {isEdgeInspectorCollapsed ? (
            <div
              className="domain-inspector-collapsed-badge domain-edge-collapsed-badge"
              onClick={() => setIsEdgeInspectorCollapsed(false)}
              title={`Click to expand connection: ${selectedEdge.sourceName} ➔ ${selectedEdge.targetName}`}
            >
              <span className="edge-collapsed-symbol">{selectedEdge.isBidirectional ? '⇄' : '➔'}</span>
              <span className="collapsed-title">
                {selectedEdge.sourceName} ➔ {selectedEdge.targetName}
              </span>
              <span className="edge-collapsed-count">{selectedEdge.totalInteractions}</span>
              <span className="collapsed-expand-arrow">▶</span>
              <button
                type="button"
                className="inspector-close-btn"
                onClick={(e) => {
                  e.stopPropagation();
                  handleClose();
                }}
                title="Deselect Connection"
              >
                ✕
              </button>
            </div>
          ) : (
            <>
              <div className="domain-inspector-header">
                {/* Row 1: Connection Type & Actions */}
                <div className="domain-inspector-header-top">
                  <span className="inspector-edge-type-chip">
                    {selectedEdge.isBidirectional ? '⇄ Bi-directional' : '➔ Directional'}
                  </span>
                  <div className="inspector-header-actions">
                    <button
                      type="button"
                      className="inspector-header-icon-btn collapse-btn"
                      onClick={() => setIsEdgeInspectorCollapsed(true)}
                      title="Collapse connection inspector"
                    >
                      ◀
                    </button>
                    <button
                      type="button"
                      className="inspector-header-icon-btn close-btn"
                      onClick={handleClose}
                      title="Deselect Connection"
                    >
                      ✕
                    </button>
                  </div>
                </div>

                {/* Row 2: Source ➔ Target Badges */}
                <div className="inspector-edge-endpoints">
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
              </div>

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
                  maxHeight: '340px',
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
            </>
          )}
        </aside>
      )}
    </>
  );
};
