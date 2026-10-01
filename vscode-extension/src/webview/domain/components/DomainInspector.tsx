import React from 'react';
import { SelectedNodeDetail, SelectedEdgeDetail, DomainProjectInfo, ConnectionDetailItem } from '../types';
import { useDomainArchitecture } from '../context/DomainArchitectureContext';

export interface DomainInspectorProps {
  nodeDetailMap?: Map<string, SelectedNodeDetail>;
  onClearGraphHighlight?: () => void;
  selectedNode?: SelectedNodeDetail | null;
  selectedEdge?: SelectedEdgeDetail | null;
  onSelectNode?: (node: SelectedNodeDetail | null) => void;
  onSelectEdge?: (edge: SelectedEdgeDetail | null) => void;
  onClearSelection?: () => void;
  isInspectorCollapsed?: boolean;
  onToggleCollapse?: (collapsed: boolean) => void;
  onHideNode?: (id: string) => void;
  onFocusInFlow?: (name: string) => void;
  onOpenFile?: (path: string, line?: number) => void;
  onOpenOverrideModal?: (name: string) => void;
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

  if (!selectedNode && !selectedEdge) {
    return null;
  }

  const handleClose = () => {
    clearSelection();
    onClearGraphHighlight?.();
  };

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
              <div className="inspector-badge" style={{ backgroundColor: selectedNode.bgColor }}>
                {selectedNode.displayTag}
              </div>
              <span className="collapsed-title">{selectedNode.displayName}</span>
              <button
                type="button"
                className="collapsed-expand-btn"
                onClick={(e) => {
                  e.stopPropagation();
                  setIsInspectorCollapsed(false);
                }}
                title="Expand inspector"
              >
                ▼
              </button>
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
              <div className="domain-inspector-header">
                <div className="inspector-badge" style={{ backgroundColor: selectedNode.bgColor }}>
                  {selectedNode.displayTag}
                </div>
                <div className="inspector-title-group">
                  <h4 className="inspector-title" title={selectedNode.displayName}>
                    {selectedNode.displayName}
                  </h4>
                  {selectedNode.tierLabel && (
                    <div
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        fontSize: '11px',
                        fontWeight: 600,
                        color:
                          selectedNode.tier === 0
                            ? '#38bdf8'
                            : selectedNode.tier === 1
                            ? '#4ade80'
                            : selectedNode.tier === 2
                            ? '#fbbf24'
                            : '#c084fc',
                        background: 'rgba(255, 255, 255, 0.06)',
                        padding: '2px 6px',
                        borderRadius: '4px',
                        marginTop: '2px',
                        marginBottom: '2px',
                        border: '1px solid rgba(255, 255, 255, 0.12)',
                      }}
                    >
                      🎯 {selectedNode.tierLabel}
                    </div>
                  )}
                  {selectedNode.framework && (
                    <span className="inspector-subtitle">{selectedNode.framework}</span>
                  )}
                  {selectedNode.gitBranch && (
                    <span className="inspector-subtitle" style={{ opacity: 0.85, fontSize: '0.82em' }}>
                      🌿 {selectedNode.gitBranch}
                    </span>
                  )}
                </div>
                <button
                  type="button"
                  className="inspector-collapse-btn"
                  onClick={() => setIsInspectorCollapsed(true)}
                  title="Minimize inspector to badge"
                >
                  —
                </button>
                <button
                  className="inspector-header-hide-btn"
                  onClick={() => hideNode(selectedNode.id)}
                  title="Hide this node (Transitive connections will bypass it)"
                >
                  👁️‍🗨️ Hide
                </button>
                <button
                  className="inspector-close-btn"
                  onClick={handleClose}
                  title="Close inspector"
                >
                  ✕
                </button>
              </div>

              <div className="inspector-body">
                {/* Subprojects breakdown if clustered */}
                {selectedNode.projects && selectedNode.projects.length > 1 && (
                  <div className="inspector-section">
                    <label className="inspector-section-label">
                      Clustered Projects ({selectedNode.projects.length})
                    </label>
                    <div className="inspector-subprojects-list">
                      {selectedNode.projects.map((p: DomainProjectInfo) => (
                        <div
                          key={p.id}
                          className="inspector-subproject-item"
                          onClick={() => p.filePath && onOpenFile?.(p.filePath, 1)}
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
                  </div>
                )}

                {/* Metrics Grid */}
                <div className="inspector-metrics-grid">
                  {(selectedNode.kind === 'Service' || selectedNode.kind === 'Ingress') && (
                    <>
                      <div className="inspector-metric-card" title="Inbound calls from other services">
                        <span className="metric-val">{selectedNode.inboundCallsCount}</span>
                        <span className="metric-lbl">Inbound Calls</span>
                      </div>
                      <div className="inspector-metric-card" title="Outbound calls to downstream services">
                        <span className="metric-val">{selectedNode.outboundCallsCount}</span>
                        <span className="metric-lbl">Outbound Calls</span>
                      </div>
                      {selectedNode.dbCount > 0 && (
                        <div className="inspector-metric-card" title="Databases used directly">
                          <span className="metric-val">{selectedNode.dbCount}</span>
                          <span className="metric-lbl">Databases</span>
                        </div>
                      )}
                      {selectedNode.messagingCount > 0 && (
                        <div className="inspector-metric-card" title="Topics published or subscribed">
                          <span className="metric-val">{selectedNode.messagingCount}</span>
                          <span className="metric-lbl">Topics</span>
                        </div>
                      )}
                    </>
                  )}
                </div>

                {/* Action buttons */}
                <div className="inspector-actions">
                  {(selectedNode.kind === 'Service' || selectedNode.kind === 'Ingress') && onFocusInFlow && (
                    <button
                      className="inspector-action-btn primary"
                      onClick={() => onFocusInFlow(selectedNode.name)}
                      title="Drill down to Project Flow view"
                    >
                      Explore in Flow ➔
                    </button>
                  )}
                  {(selectedNode.kind === 'Service' || selectedNode.kind === 'Ingress') && (
                    <button
                      className="inspector-action-btn secondary"
                      onClick={() => openOverrideModal(selectedNode.name)}
                      title="Override or reassign service domain and bounded context"
                    >
                      🏷️ Override Domain
                    </button>
                  )}
                  {selectedNode.primaryFilePath && onOpenFile && (
                    <button
                      className="inspector-action-btn secondary"
                      onClick={() => onOpenFile(selectedNode.primaryFilePath!, 1)}
                      title="Open source file in editor"
                    >
                      Open Source
                    </button>
                  )}
                  <button
                    className="inspector-action-btn hide-node-btn"
                    onClick={() => hideNode(selectedNode.id)}
                    title="Hide this node from map"
                  >
                    Hide Node
                  </button>
                </div>
              </div>
            </>
          )}
        </aside>
      )}

      {/* Edge Inspector */}
      {selectedEdge && (
        <aside className="domain-inspector-panel domain-edge-inspector-panel">
          <div className="inspector-header">
            <div className="inspector-title-group" style={{ width: '100%' }}>
              <div
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  width: '100%',
                  marginBottom: 4,
                }}
              >
                <span
                  style={{
                    fontSize: '11px',
                    textTransform: 'uppercase',
                    letterSpacing: '0.06em',
                    color: '#94a3b8',
                    fontWeight: 600,
                  }}
                >
                  {selectedEdge.isBidirectional
                    ? '⇄ Bi-directional Connection'
                    : '➔ Directional Connection'}
                </span>
                <button
                  type="button"
                  className="inspector-close-btn"
                  onClick={handleClose}
                  title="Deselect Connection"
                >
                  ✕
                </button>
              </div>

              {/* Endpoint badges */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap', marginTop: 4 }}>
                <div
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 6,
                    padding: '3px 8px',
                    borderRadius: 4,
                    background: 'rgba(255, 255, 255, 0.06)',
                    cursor: 'pointer',
                  }}
                  onClick={() => {
                    const node = nodeDetailMap?.get(selectedEdge.sourceId);
                    if (node) selectNode(node);
                  }}
                  title={`Inspect ${selectedEdge.sourceName}`}
                >
                  <span
                    className="inspector-badge"
                    style={{ backgroundColor: selectedEdge.sourceBgColor, padding: '1px 5px', fontSize: '9px' }}
                  >
                    {selectedEdge.sourceTag}
                  </span>
                  <span style={{ fontWeight: 600, fontSize: '12px', color: '#f8fafc' }}>
                    {selectedEdge.sourceName}
                  </span>
                </div>

                <span style={{ color: '#38bdf8', fontWeight: 700, fontSize: '14px' }}>
                  {selectedEdge.isBidirectional ? '⇄' : '➔'}
                </span>

                <div
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 6,
                    padding: '3px 8px',
                    borderRadius: 4,
                    background: 'rgba(255, 255, 255, 0.06)',
                    cursor: 'pointer',
                  }}
                  onClick={() => {
                    const node = nodeDetailMap?.get(selectedEdge.targetId);
                    if (node) selectNode(node);
                  }}
                  title={`Inspect ${selectedEdge.targetName}`}
                >
                  <span
                    className="inspector-badge"
                    style={{ backgroundColor: selectedEdge.targetBgColor, padding: '1px 5px', fontSize: '9px' }}
                  >
                    {selectedEdge.targetTag}
                  </span>
                  <span style={{ fontWeight: 600, fontSize: '12px', color: '#f8fafc' }}>
                    {selectedEdge.targetName}
                  </span>
                </div>
              </div>
            </div>
          </div>

          {/* Metrics summary */}
          <div
            className="inspector-section"
            style={{ padding: '8px 12px', borderBottom: '1px solid rgba(255, 255, 255, 0.08)' }}
          >
            <div
              className="domain-inspector-metrics-grid"
              style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 6 }}
            >
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
                <span className="metric-lbl">Events/Topics</span>
              </div>
            </div>
          </div>

          {/* Breakdown List */}
          <div
            className="inspector-body"
            style={{
              overflowY: 'auto',
              maxHeight: '380px',
              padding: '10px 12px',
              display: 'flex',
              flexDirection: 'column',
              gap: 8,
            }}
          >
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
              const tagIcon = isDirectCall ? '📞' : isMsg ? '📨' : isDb ? '🗄️' : '🔌';

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
                    <span
                      style={{
                        fontSize: '10px',
                        fontWeight: 600,
                        padding: '1px 6px',
                        borderRadius: 4,
                        background: tagColor,
                        color: '#000',
                      }}
                    >
                      {tagIcon} {item.kind}
                    </span>
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
        </aside>
      )}
    </>
  );
};
