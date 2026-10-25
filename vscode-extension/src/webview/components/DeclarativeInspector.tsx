import React, { useState, useMemo } from 'react';
import { SelectedNodeDetail, SelectedEdgeDetail, ConnectionDetailItem } from '../domain/types';
import {
  InspectorSchema,
  InspectorSection,
  evaluateNodeHeader,
} from '../domain/inspectorSchema';
import {
  DatabaseEvidenceSection,
  EdgeEvidenceSection,
  NodeEvidenceSection,
} from '../domain/components/DomainInspector';
import { FloatingPanel } from './FloatingPanel';
import {
  ChevronRightIcon,
  ChevronDownIcon,
  EyeOffIcon,
  TagIcon,
  GitBranchIcon,
  ExternalLinkIcon,
  SettingsIcon,
  XIcon,
} from './Icons';
import { EntityNodeBadge, EntityCodeBadge } from '../domain/entityBadges';

export interface DeclarativeInspectorProps<TNode = SelectedNodeDetail, TEdge = SelectedEdgeDetail> {
  schema: InspectorSchema<TNode, TEdge>;
  selectedNode?: TNode | null;
  selectedEdge?: TEdge | null;
  nodeDetailMap?: Map<string, TNode>;
  onClearGraphHighlight?: () => void;
  onSelectNode?: (node: TNode | null) => void;
  onSelectNodeById?: (id: string) => void;
  onSelectEdge?: (edge: TEdge | null) => void;
  onClearSelection?: () => void;
  isInspectorCollapsed?: boolean;
  onToggleCollapse?: (collapsed: boolean) => void;
  isEdgeInspectorCollapsed?: boolean;
  onToggleEdgeCollapse?: (collapsed: boolean) => void;
  onHideNode?: (id: string) => void;
  onUngroupNode?: (node: TNode) => void;
  onOpenFile?: (path: string, line?: number) => void;
  docked?: boolean;
  onToggleDock?: () => void;
  emptyStateTitle?: string;
  emptyStateDescription?: string;
}

export function DeclarativeInspector<
  TNode extends { id: string; isGroup?: boolean; memberIds?: string[]; memberNodes?: any[] } = SelectedNodeDetail,
  TEdge = SelectedEdgeDetail
>(props: DeclarativeInspectorProps<TNode, TEdge>): React.ReactElement | null {
  const {
    schema,
    selectedNode = null,
    selectedEdge = null,
    nodeDetailMap,
    onClearGraphHighlight,
    onSelectNode,
    onSelectNodeById,
    onClearSelection,
    isInspectorCollapsed = false,
    onToggleCollapse,
    onHideNode,
    onUngroupNode,
    onOpenFile,
    docked,
    onToggleDock,
    emptyStateTitle = 'No Element Selected',
    emptyStateDescription = 'Click any node, group, or connection edge on the canvas to inspect its architecture, calls, and relationships.',
  } = props;

  const [localEdgeCollapsed, setLocalEdgeCollapsed] = useState(false);
  const isEdgeInspectorCollapsed =
    props.isEdgeInspectorCollapsed !== undefined ? props.isEdgeInspectorCollapsed : localEdgeCollapsed;
  const setIsEdgeInspectorCollapsed = props.onToggleEdgeCollapse || setLocalEdgeCollapsed;

  const [expandedConnEvidenceKey, setExpandedConnEvidenceKey] = useState<string | null>(null);
  const [groupMemberFilter, setGroupMemberFilter] = useState('');
  const [navHistory, setNavHistory] = useState<TNode[]>([]);
  const [collapsedSections, setCollapsedSections] = useState<Record<string, boolean>>({});
  const [isInspectorSettingsOpen, setIsInspectorSettingsOpen] = useState(false);
  const [showTierReason, setShowTierReason] = useState(true);
  const [showKpiMetrics, setShowKpiMetrics] = useState(true);
  const [showCodeEvidence, setShowCodeEvidence] = useState(true);

  const toggleSection = (key: string) => {
    setCollapsedSections((prev) => ({
      ...prev,
      [key]: !prev[key],
    }));
  };

  const setAllSectionsCollapsed = (collapsed: boolean) => {
    const next: Record<string, boolean> = {};
    for (const s of schema.nodeSections) {
      next[s.id] = collapsed;
    }
    setCollapsedSections(next);
  };

  // Find parent group if current selectedNode belongs to a collapsed group in nodeDetailMap
  const parentGroupNode = useMemo<TNode | null>(() => {
    if (!selectedNode || selectedNode.isGroup || !nodeDetailMap) return null;
    for (const n of nodeDetailMap.values()) {
      if (
        n.isGroup &&
        (n.memberIds?.includes(selectedNode.id) || n.memberNodes?.some((m: any) => m.id === selectedNode.id))
      ) {
        return n;
      }
    }
    return null;
  }, [selectedNode, nodeDetailMap]);

  const backTargetNode = useMemo<TNode | null>(() => {
    if (!selectedNode) return null;
    for (let i = navHistory.length - 1; i >= 0; i--) {
      const candidate = navHistory[i];
      if (candidate && candidate.id !== selectedNode.id) {
        return nodeDetailMap?.get(candidate.id) || candidate;
      }
    }
    return parentGroupNode;
  }, [selectedNode, navHistory, parentGroupNode, nodeDetailMap]);

  const handleClose = () => {
    setNavHistory([]);
    onClearSelection?.();
    onClearGraphHighlight?.();
  };

  const handleSelectNodeById = (nodeId: string) => {
    if (selectedNode && selectedNode.id !== nodeId) {
      setNavHistory((prev) => [...prev.filter((n) => n.id !== selectedNode.id), selectedNode]);
    }
    setGroupMemberFilter('');
    if (onSelectNodeById) {
      onSelectNodeById(nodeId);
      return;
    }
    const targetNode = nodeDetailMap?.get(nodeId);
    if (targetNode && onSelectNode) {
      onSelectNode(targetNode);
    }
  };

  const handleGoBack = () => {
    if (!backTargetNode) return;
    setNavHistory((prev) => {
      const idx = prev.map((n) => n.id).lastIndexOf(backTargetNode.id);
      return idx >= 0 ? prev.slice(0, idx) : [];
    });
    setGroupMemberFilter('');
    if (onSelectNodeById) {
      onSelectNodeById(backTargetNode.id);
      return;
    }
    if (onSelectNode) {
      onSelectNode(backTargetNode);
    }
  };

  if (!selectedNode && !selectedEdge) {
    if (docked) {
      return (
        <div className="domain-panel-empty-state">
          <div className="domain-empty-card">
            <span className="domain-empty-icon">ℹ️</span>
            <h4 className="domain-empty-title">{emptyStateTitle}</h4>
            <p className="domain-empty-desc">{emptyStateDescription}</p>
          </div>
        </div>
      );
    }
    return null;
  }

  const headerModel = selectedNode ? evaluateNodeHeader(schema, selectedNode) : null;
  const backHeaderModel = backTargetNode ? evaluateNodeHeader(schema, backTargetNode) : null;

  const renderSection = (section: InspectorSection<TNode, TEdge>, item: TNode | TEdge) => {
    switch (section.type) {
      case 'actions': {
        if (section.when && !section.when(item)) return null;
        const visibleActions = section.items.filter((a) => !a.when || a.when(item));
        if (visibleActions.length === 0) return null;
        return (
          <div key={section.id} className="inspector-actions">
            {visibleActions.map((act) => (
              <button
                key={act.id}
                type="button"
                className={`inspector-action-btn ${act.variant || 'secondary'}`}
                onClick={() => act.onClick(item)}
                title={act.tooltip || act.label}
              >
                {act.icon === 'tag' && <TagIcon size={12} />}
                {act.icon === 'external-link' && <ExternalLinkIcon size={12} />}
                {act.label}
              </button>
            ))}
          </div>
        );
      }

      case 'kpi-grid': {
        if (!showKpiMetrics) return null;
        if (section.when && !section.when(item)) return null;
        const visibleMetrics = section.metrics.filter((m) => !m.when || m.when(item));
        if (visibleMetrics.length === 0) return null;
        return (
          <div key={section.id} className="inspector-metrics-grid">
            {visibleMetrics.map((m) => (
              <div key={m.id} className="inspector-metric-card" title={m.tooltip || m.label}>
                <span className="metric-val" style={m.color ? { color: m.color } : undefined}>
                  {m.value(item)}
                </span>
                <span className="metric-lbl">{m.label}</span>
              </div>
            ))}
          </div>
        );
      }

      case 'group-members': {
        const node = item as TNode;
        if (section.when && !section.when(node)) return null;
        const members = section.getMembers(node);
        if (members.length === 0) return null;
        const isCollapsed = Boolean(collapsedSections[section.id]);
        const titleText = typeof section.title === 'function' ? section.title(node) : section.title;
        const canRemove = Boolean(section.canRemoveMember?.(node));

        return (
          <div key={section.id} className={`inspector-section ${isCollapsed ? 'is-collapsed' : 'is-expanded'}`}>
            <div
              className="inspector-section-header"
              onClick={() => toggleSection(section.id)}
              title={isCollapsed ? 'Expand grouped entities' : 'Collapse grouped entities'}
              style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <span className="inspector-section-title">{titleText}</span>
                <span
                  className="tab-badge"
                  style={{
                    backgroundColor: 'rgba(56, 189, 248, 0.2)',
                    color: '#38bdf8',
                    border: '1px solid rgba(56, 189, 248, 0.35)',
                    fontSize: '9.5px',
                    padding: '1px 5px',
                  }}
                >
                  {members.length}
                </span>
              </div>
              <span className="inspector-section-toggle">
                {isCollapsed ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
              </span>
            </div>
            {!isCollapsed && (
              <div
                className="inspector-conn-list"
                style={{ display: 'flex', flexDirection: 'column', gap: 4, marginTop: 6 }}
              >
                {members.length > 8 && (
                  <div style={{ marginBottom: 4 }}>
                    <input
                      type="text"
                      value={groupMemberFilter}
                      onChange={(e) => setGroupMemberFilter(e.target.value)}
                      placeholder={`Filter ${members.length} grouped entities...`}
                      style={{
                        width: '100%',
                        boxSizing: 'border-box',
                        padding: '4px 8px',
                        fontSize: '11px',
                        borderRadius: 4,
                        background: 'rgba(0, 0, 0, 0.25)',
                        border: '1px solid rgba(255, 255, 255, 0.12)',
                        color: '#e2e8f0',
                        outline: 'none',
                      }}
                    />
                  </div>
                )}
                {members
                  .filter((m) => {
                    if (!groupMemberFilter.trim()) return true;
                    const q = groupMemberFilter.toLowerCase();
                    return (
                      (m.displayName || '').toLowerCase().includes(q) ||
                      (m.name || '').toLowerCase().includes(q)
                    );
                  })
                  .map((m) => (
                    <div
                      key={m.id}
                      className="inspector-conn-item"
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        padding: '5px 8px',
                        borderRadius: 4,
                        background: 'rgba(255, 255, 255, 0.03)',
                        border: '1px solid rgba(255, 255, 255, 0.06)',
                        cursor: 'pointer',
                        flexShrink: 0,
                      }}
                      onClick={() => handleSelectNodeById(m.id)}
                      title={`Inspect ${m.displayName || m.name}`}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', gap: 6, minWidth: 0, flex: 1 }}>
                        <EntityNodeBadge kind={m.kind} size={18} />
                        <span
                          style={{
                            fontSize: '11px',
                            color: '#e2e8f0',
                            fontWeight: 600,
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            whiteSpace: 'nowrap',
                          }}
                        >
                          {m.displayName || m.name}
                        </span>
                        {m.tier !== undefined && (
                          <span
                            style={{
                              fontSize: '9px',
                              color: '#94a3b8',
                              background: 'rgba(255, 255, 255, 0.05)',
                              padding: '1px 4px',
                              borderRadius: 3,
                            }}
                          >
                            T{m.tier}
                          </span>
                        )}
                      </div>
                      {canRemove && section.onRemoveMember && (
                        <button
                          type="button"
                          className="action-icon-btn"
                          onClick={(e) => {
                            e.stopPropagation();
                            section.onRemoveMember!(node, m.id);
                          }}
                          title="Remove from group"
                          style={{
                            background: 'none',
                            border: 'none',
                            color: '#94a3b8',
                            cursor: 'pointer',
                            padding: '2px 4px',
                            lineHeight: 1,
                          }}
                        >
                          ✕
                        </button>
                      )}
                    </div>
                  ))}
              </div>
            )}
          </div>
        );
      }

      case 'internal-connections': {
        const node = item as TNode;
        if (section.when && !section.when(node)) return null;
        const connections = section.getItems(node);
        if (connections.length === 0) return null;
        const isCollapsed = Boolean(collapsedSections[section.id]);
        const titleText = typeof section.title === 'function' ? section.title(node) : section.title;

        return (
          <div key={section.id} className={`inspector-section ${isCollapsed ? 'is-collapsed' : 'is-expanded'}`}>
            <div
              className="inspector-section-header"
              onClick={() => toggleSection(section.id)}
              title={isCollapsed ? 'Expand internal connections' : 'Collapse internal connections'}
              style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <span className="inspector-section-title">{titleText}</span>
                <span
                  className="tab-badge"
                  style={{
                    backgroundColor: 'rgba(168, 85, 247, 0.2)',
                    color: '#c084fc',
                    border: '1px solid rgba(168, 85, 247, 0.35)',
                    fontSize: '9.5px',
                    padding: '1px 5px',
                  }}
                >
                  {connections.length}
                </span>
              </div>
              <span className="inspector-section-toggle">
                {isCollapsed ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
              </span>
            </div>
            {!isCollapsed && (
              <div
                className="inspector-conn-list"
                style={{ display: 'flex', flexDirection: 'column', gap: 4, marginTop: 6 }}
              >
                {connections.map((conn: ConnectionDetailItem) => {
                  const evKey = `internal:${conn.id}`;
                  const isEvOpen = expandedConnEvidenceKey === evKey;
                  return (
                    <div
                      key={conn.id}
                      className="inspector-conn-item"
                      style={{
                        padding: '6px 8px',
                        borderRadius: 4,
                        background: 'rgba(255, 255, 255, 0.03)',
                        border: '1px solid rgba(255, 255, 255, 0.06)',
                        display: 'flex',
                        flexDirection: 'column',
                        gap: 2,
                        flexShrink: 0,
                      }}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 4 }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: '11px', color: '#e2e8f0' }}>
                          <span style={{ fontWeight: 600, color: '#38bdf8' }}>{conn.sourceName}</span>
                          <span style={{ color: '#64748b' }}>➔</span>
                          <span style={{ fontWeight: 600, color: '#a78bfa' }}>{conn.targetName}</span>
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
                          <button
                            type="button"
                            style={{
                              fontSize: '9px',
                              fontWeight: 600,
                              padding: '1px 5px',
                              borderRadius: 3,
                              background: isEvOpen ? 'rgba(56, 189, 248, 0.3)' : 'rgba(255, 255, 255, 0.08)',
                              color: isEvOpen ? '#38bdf8' : '#cbd5e1',
                              border: '1px solid rgba(255, 255, 255, 0.12)',
                              cursor: 'pointer',
                            }}
                            onClick={(e) => {
                              e.stopPropagation();
                              setExpandedConnEvidenceKey((prev) => (prev === evKey ? null : evKey));
                            }}
                            title="Inspect code evidence for this connection"
                          >
                            {isEvOpen ? 'Hide Evidence' : 'Evidence'}
                          </button>
                          {conn.count > 1 && (
                            <span
                              style={{
                                fontSize: '9.5px',
                                fontWeight: 700,
                                background: 'rgba(255, 255, 255, 0.08)',
                                color: '#cbd5e1',
                                padding: '1px 5px',
                                borderRadius: 3,
                              }}
                            >
                              {conn.count}×
                            </span>
                          )}
                        </div>
                      </div>
                      {conn.label && <div style={{ fontSize: '10px', color: '#94a3b8' }}>{conn.label}</div>}
                      {isEvOpen && (
                        <div style={{ marginTop: 4 }}>
                          <EdgeEvidenceSection
                            sourceIds={[conn.sourceId]}
                            targetIds={[conn.targetId]}
                            edgeKind={conn.category}
                            onOpenFile={onOpenFile}
                            defaultExpanded={true}
                          />
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        );
      }

      case 'connection-list': {
        const node = item as TNode;
        const rows = section.getItems(node);
        if (section.when && !section.when(node, rows)) return null;
        const isCollapsed = Boolean(collapsedSections[section.id]);
        const baseTitle = typeof section.title === 'function' ? section.title(node) : section.title;
        const emptyText =
          typeof section.emptyHint === 'function'
            ? section.emptyHint(node)
            : section.emptyHint || 'No connections';

        return (
          <div key={section.id} className={`inspector-section ${isCollapsed ? 'is-collapsed' : 'is-expanded'}`}>
            <div
              className="inspector-section-header"
              onClick={() => toggleSection(section.id)}
              title={isCollapsed ? `Expand ${baseTitle.toLowerCase()}` : `Collapse ${baseTitle.toLowerCase()}`}
            >
              <span className="inspector-section-title">
                {baseTitle} ({rows.length})
              </span>
              <span className="inspector-section-toggle">
                {isCollapsed ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
              </span>
            </div>
            {!isCollapsed && (
              <div className="inspector-connection-list">
                {rows.map((row) => {
                  const evKey = `${section.id}:${row.id}`;
                  const isEvOpen = expandedConnEvidenceKey === evKey;
                  const evParams = section.inlineEvidence?.getParams(node, row);
                  return (
                    <div key={row.id} style={{ display: 'flex', flexDirection: 'column', width: '100%' }}>
                      <div
                        className="inspector-connection-item"
                        onClick={() => handleSelectNodeById(row.id)}
                        title={`Jump to ${row.name}`}
                      >
                        <div className="connection-item-main" style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                          <EntityNodeBadge kind={row.kind} size={18} />
                          <span className="connection-item-name">{row.name}</span>
                        </div>
                        <div className="connection-item-meta" style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                          {section.inlineEvidence && (
                            <button
                              type="button"
                              style={{
                                fontSize: '9px',
                                fontWeight: 600,
                                padding: '1px 5px',
                                borderRadius: 3,
                                background: isEvOpen ? 'rgba(56, 189, 248, 0.3)' : 'rgba(255, 255, 255, 0.08)',
                                color: isEvOpen ? '#38bdf8' : '#cbd5e1',
                                border: '1px solid rgba(255, 255, 255, 0.12)',
                                cursor: 'pointer',
                              }}
                              onClick={(e) => {
                                e.stopPropagation();
                                setExpandedConnEvidenceKey((prev) => (prev === evKey ? null : evKey));
                              }}
                              title="Inspect code evidence for this connection"
                            >
                              {isEvOpen ? 'Hide Evidence' : 'Evidence'}
                            </button>
                          )}
                          {row.badgeText ? (
                            <span className="connection-count-badge">{row.badgeText}</span>
                          ) : row.count !== undefined ? (
                            <span className="connection-count-badge">
                              {section.id === 'databases'
                                ? `${row.count} links`
                                : `${row.count} ${row.count === 1 ? 'call' : 'calls'}`}
                            </span>
                          ) : null}
                          <span className="connection-jump-arrow">➔</span>
                        </div>
                      </div>
                      {isEvOpen && section.inlineEvidence && evParams && (
                        <div style={{ marginLeft: 6, marginTop: 4, marginBottom: 6 }}>
                          {section.inlineEvidence.mode === 'database' ? (
                            <DatabaseEvidenceSection
                              databaseId={evParams.databaseId || row.id}
                              databaseName={evParams.databaseName || row.name}
                              serviceId={evParams.serviceId}
                              onOpenFile={onOpenFile}
                              defaultExpanded={true}
                            />
                          ) : (
                            <EdgeEvidenceSection
                              sourceIds={evParams.sourceIds || [node.id]}
                              targetIds={evParams.targetIds || [row.id]}
                              edgeKind={evParams.edgeKind}
                              onOpenFile={onOpenFile}
                              defaultExpanded={true}
                            />
                          )}
                        </div>
                      )}
                    </div>
                  );
                })}
                {rows.length === 0 && <div className="inspector-empty-hint">{emptyText}</div>}
              </div>
            )}
          </div>
        );
      }

      case 'file-list': {
        const node = item as TNode;
        const files = section.getFiles(node);
        if (section.when && !section.when(node, files)) return null;
        if (files.length === 0) return null;
        const isCollapsed = Boolean(collapsedSections[section.id]);
        const titleText = typeof section.title === 'function' ? section.title(node) : section.title;

        return (
          <div key={section.id} className={`inspector-section ${isCollapsed ? 'is-collapsed' : 'is-expanded'}`}>
            <div
              className="inspector-section-header"
              onClick={() => toggleSection(section.id)}
              title={isCollapsed ? `Expand ${titleText.toLowerCase()}` : `Collapse ${titleText.toLowerCase()}`}
            >
              <span className="inspector-section-title">{titleText}</span>
              <span className="inspector-section-toggle">
                {isCollapsed ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
              </span>
            </div>
            {!isCollapsed && (
              <div className="inspector-subprojects-list">
                {files.map((f, idx) => (
                  <div
                    key={f.id || idx}
                    className="inspector-subproject-item"
                    style={section.monospace ? { cursor: onOpenFile && f.path ? 'pointer' : 'default', padding: '6px 8px' } : undefined}
                    onClick={() => f.path && onOpenFile?.(f.path, f.line || 1)}
                    title={f.path ? `Open ${f.path}${f.line ? `:${f.line}` : ''}` : f.label}
                  >
                    {section.monospace ? <ExternalLinkIcon size={11} /> : <span className="subproject-dot">•</span>}
                    <span
                      className="subproject-name"
                      style={section.monospace ? { fontFamily: 'monospace', fontSize: '11px', marginLeft: 6 } : undefined}
                    >
                      {f.label}
                    </span>
                    {f.badge && <span className="subproject-lib-tag">{f.badge}</span>}
                    {f.secondaryRoles?.map((r: string) => (
                      <span
                        key={r}
                        className="subproject-lib-tag"
                        style={{ background: 'rgba(168, 85, 247, 0.2)', color: '#c084fc' }}
                      >
                        +{r.slice(0, 3).toUpperCase()}
                      </span>
                    ))}
                    {f.gitBranch && (
                      <span
                        className="subproject-lib-tag"
                        style={{ background: 'rgba(56, 189, 248, 0.2)', color: '#38bdf8' }}
                      >
                        <GitBranchIcon size={11} /> {f.gitBranch}
                      </span>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        );
      }

      case 'async-evidence': {
        if (!showCodeEvidence) return null;
        if (section.when && !section.when(item)) return null;
        const params = section.getParams(item);
        if (section.endpoint === 'database-evidence' && params.databaseId) {
          return (
            <DatabaseEvidenceSection
              key={section.id}
              databaseId={params.databaseId}
              databaseName={params.databaseName}
              serviceId={params.serviceId}
              onOpenFile={onOpenFile}
              defaultExpanded={section.defaultExpanded ?? true}
            />
          );
        }
        if (section.endpoint === 'edge-evidence' && params.sourceIds && params.targetIds) {
          return (
            <EdgeEvidenceSection
              key={section.id}
              sourceIds={params.sourceIds}
              targetIds={params.targetIds}
              edgeKind={params.edgeKind}
              onOpenFile={onOpenFile}
              defaultExpanded={section.defaultExpanded ?? true}
            />
          );
        }
        if (section.endpoint === 'node-evidence' && params.nodeIds) {
          return (
            <NodeEvidenceSection
              key={section.id}
              nodeIds={params.nodeIds}
              nodeKind={params.nodeKind}
              onOpenFile={onOpenFile}
              defaultExpanded={section.defaultExpanded ?? true}
            />
          );
        }
        return null;
      }

      case 'edge-breakdown': {
        const edge = item as TEdge;
        if (section.when && !section.when(edge)) return null;
        const items = section.getItems(edge);
        const titleText = typeof section.title === 'function' ? section.title(edge) : section.title;
        return (
          <div
            key={section.id}
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
            <div
              style={{
                fontSize: '10.5px',
                fontWeight: 700,
                textTransform: 'uppercase',
                letterSpacing: '0.05em',
                color: '#94a3b8',
              }}
            >
              {titleText}
            </div>
            {items.map((conn: ConnectionDetailItem, idx: number) => {
              const isDirectCall = conn.category === 'service_call' && !conn.isTransitive;
              const isMsg = conn.category === 'messaging';
              const isDb = conn.category === 'database';
              const tagColor = isDirectCall
                ? '#38bdf8'
                : isMsg
                ? '#fbbf24'
                : isDb
                ? '#c084fc'
                : '#34d399';
              const tagLabel = isDirectCall ? 'RPC' : isMsg ? 'EVENT' : isDb ? 'DB' : conn.kind;

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
                      {conn.isTransitive && (
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
                      {conn.isInferred && (
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
                      {conn.count} {conn.count === 1 ? 'call' : 'calls'}
                    </span>
                  </div>
                  <div style={{ fontSize: '12px', fontWeight: 500, color: '#f1f5f9' }}>{conn.label}</div>
                  {conn.viaNames && conn.viaNames.length > 0 && (
                    <div style={{ fontSize: '10px', color: '#64748b' }}>Via: {conn.viaNames.join(' ➔ ')}</div>
                  )}
                </div>
              );
            })}
          </div>
        );
      }

      default:
        return null;
    }
  };

  return (
    <>
      {/* Node Inspector */}
      {selectedNode && headerModel && (
        <FloatingPanel
          dock="top-right"
          docked={docked}
          onToggleDock={onToggleDock}
          width={360}
          minWidth={280}
          maxWidth={750}
          persistKey="ce_node_inspector"
          className="no-body-padding"
          isCollapsed={isInspectorCollapsed}
          onToggleCollapse={onToggleCollapse}
          onClose={handleClose}
          closeTooltip="Deselect"
          collapseTooltip="Collapse inspector"
          expandTooltip={`Click to expand details for ${headerModel.title}`}
          title={headerModel.title}
          badge={
            <div style={{ display: 'inline-flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
              <EntityNodeBadge kind={headerModel.kind} size={20} />
              <EntityCodeBadge kind={headerModel.kind} />
              {headerModel.secondaryRoles.map((r: string) => (
                <span
                  key={r}
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 3,
                    fontSize: 10,
                    fontWeight: 600,
                    padding: '1px 5px',
                    borderRadius: 3,
                    backgroundColor: 'var(--vscode-badge-background, rgba(255, 255, 255, 0.1))',
                    color: 'var(--vscode-badge-foreground, #ccc)',
                    border: '1px solid var(--vscode-editorGroup-border, rgba(255, 255, 255, 0.15))',
                  }}
                  title={`Secondary Role: ${r}`}
                >
                  <EntityNodeBadge kind={r as any} size={12} />+{r.slice(0, 3).toUpperCase()}
                </span>
              ))}
            </div>
          }
          headerActions={
            <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
              {backTargetNode && backHeaderModel && (
                <button
                  type="button"
                  className="floating-dock-btn action-btn back-btn"
                  onClick={handleGoBack}
                  title={`Back to ${backHeaderModel.title}`}
                  style={{
                    background: 'rgba(56, 189, 248, 0.14)',
                    color: '#38bdf8',
                    border: '1px solid rgba(56, 189, 248, 0.35)',
                    padding: '2px 7px',
                    fontSize: '11px',
                    fontWeight: 600,
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 4,
                  }}
                >
                  <span>← Back</span>
                </button>
              )}
              {headerModel.isGroup && onUngroupNode && (
                <button
                  type="button"
                  className="floating-dock-btn action-btn ungroup-btn"
                  onClick={() => onUngroupNode(selectedNode)}
                  title="Ungroup / Expand this group back into individual nodes"
                  style={{
                    background: 'rgba(56, 189, 248, 0.15)',
                    color: '#38bdf8',
                    border: '1px solid rgba(56, 189, 248, 0.35)',
                    padding: '2px 8px',
                    fontSize: '11px',
                    fontWeight: 600,
                  }}
                >
                  <span>Ungroup</span>
                </button>
              )}
              {onHideNode && (
                <button
                  type="button"
                  className="floating-dock-btn action-btn hide-btn"
                  onClick={() => onHideNode(headerModel.id)}
                  title="Hide this node (Transitive connections bypass it) [Shortcut: H, Del]"
                >
                  <EyeOffIcon size={12} />
                  <span>Hide</span>
                </button>
              )}
              <button
                type="button"
                className={`floating-dock-btn settings-gear-btn ${isInspectorSettingsOpen ? 'is-open' : ''}`}
                onClick={(e) => {
                  e.stopPropagation();
                  setIsInspectorSettingsOpen((prev) => !prev);
                }}
                title="Inspector Display Settings (Sections, KPI Metrics, Explainability)"
                aria-label="Inspector Display Settings"
              >
                <SettingsIcon size={13} />
                {!isInspectorSettingsOpen && (!showTierReason || !showKpiMetrics || !showCodeEvidence) && (
                  <span className="settings-gear-dot" />
                )}
              </button>
            </div>
          }
          subtitle={
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6, width: '100%' }}>
              {headerModel.tierLabel && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
                  <div style={{ display: 'flex', alignItems: 'center' }}>
                    <span
                      className="inspector-tier-chip"
                      style={{
                        color:
                          headerModel.tier === 0
                            ? '#38bdf8'
                            : headerModel.tier === 1
                            ? '#4ade80'
                            : headerModel.tier === 2
                            ? '#fbbf24'
                            : headerModel.tier === 3
                            ? '#c084fc'
                            : '#94a3b8',
                      }}
                      title={`Architectural Echelon: ${headerModel.tierLabel}`}
                    >
                      {headerModel.tierLabel}
                    </span>
                  </div>
                  {showTierReason && headerModel.tierReason && (
                    <div
                      className="inspector-tier-reason"
                      style={{
                        fontSize: '10.5px',
                        color: '#94a3b8',
                        lineHeight: 1.35,
                        paddingLeft: 6,
                        borderLeft: '2px solid rgba(56, 189, 248, 0.35)',
                      }}
                      title="Why is this node in this tier?"
                    >
                      {headerModel.tierReason}
                    </div>
                  )}
                </div>
              )}
              {headerModel.chips.length > 0 && (
                <div className="inspector-meta-row">
                  {headerModel.chips.map((chip, idx) => (
                    <span
                      key={idx}
                      className={`inspector-meta-tag ${chip.variant && chip.variant !== 'default' ? chip.variant : ''}`}
                    >
                      {chip.icon === 'branch' && <GitBranchIcon size={11} />}{' '}
                      {chip.icon === 'domain' && <TagIcon size={11} />} {chip.text}
                    </span>
                  ))}
                </div>
              )}
            </div>
          }
          collapsedTitle={headerModel.title}
          collapsedBadge={
            <div style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}>
              <EntityNodeBadge kind={headerModel.kind} size={18} />
              <EntityCodeBadge kind={headerModel.kind} />
              {headerModel.secondaryRoles.map((r: string) => (
                <span
                  key={r}
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 2,
                    fontSize: 9,
                    fontWeight: 600,
                    padding: '0 4px',
                    borderRadius: 2,
                    backgroundColor: 'rgba(255, 255, 255, 0.1)',
                    color: '#ccc',
                  }}
                  title={`Secondary Role: ${r}`}
                >
                  +{r.slice(0, 3).toUpperCase()}
                </span>
              ))}
            </div>
          }
        >
          <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, overflow: 'hidden' }}>
            {isInspectorSettingsOpen && (
              <div className="panel-settings-drawer">
                <div className="panel-settings-drawer-header">
                  <span className="panel-settings-drawer-title">
                    <SettingsIcon size={11} />
                    <span>Inspector Display</span>
                  </span>
                  <button
                    type="button"
                    className="floating-dock-btn close-btn"
                    style={{ width: 20, height: 20, minWidth: 20, padding: 0 }}
                    onClick={() => setIsInspectorSettingsOpen(false)}
                    title="Close inspector settings"
                  >
                    <XIcon size={11} />
                  </button>
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                  <label className={`domain-hud-checkbox-label ${showTierReason ? 'is-active' : ''}`} style={{ fontSize: '11px' }}>
                    <input
                      type="checkbox"
                      className="domain-hud-checkbox"
                      checked={showTierReason}
                      onChange={(e) => setShowTierReason(e.target.checked)}
                    />
                    <span>🧭 Show Tier Explainability</span>
                  </label>
                  <label className={`domain-hud-checkbox-label ${showKpiMetrics ? 'is-active' : ''}`} style={{ fontSize: '11px' }}>
                    <input
                      type="checkbox"
                      className="domain-hud-checkbox"
                      checked={showKpiMetrics}
                      onChange={(e) => setShowKpiMetrics(e.target.checked)}
                    />
                    <span>📊 Show KPI Summary Cards</span>
                  </label>
                  <label className={`domain-hud-checkbox-label ${showCodeEvidence ? 'is-active' : ''}`} style={{ fontSize: '11px' }}>
                    <input
                      type="checkbox"
                      className="domain-hud-checkbox"
                      checked={showCodeEvidence}
                      onChange={(e) => setShowCodeEvidence(e.target.checked)}
                    />
                    <span>🔍 Show Source Code Evidence</span>
                  </label>
                  <div style={{ display: 'flex', gap: 6, marginTop: 2 }}>
                    <button
                      type="button"
                      className="domain-hud-btn"
                      style={{ flex: 1, justifyContent: 'center', fontSize: '10.5px', padding: '3px 8px' }}
                      onClick={() => setAllSectionsCollapsed(false)}
                    >
                      ▾ Expand All
                    </button>
                    <button
                      type="button"
                      className="domain-hud-btn"
                      style={{ flex: 1, justifyContent: 'center', fontSize: '10.5px', padding: '3px 8px' }}
                      onClick={() => setAllSectionsCollapsed(true)}
                    >
                      ▸ Collapse All
                    </button>
                  </div>
                </div>
              </div>
            )}

            <div className="inspector-body" style={{ flex: 1, minHeight: 0, overflowY: 'auto' }}>
              {/* Back to Group / Previous Node Banner */}
              {backTargetNode && backHeaderModel && (
                <button
                  type="button"
                  onClick={handleGoBack}
                  title={`Return to ${backHeaderModel.title}`}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    gap: 8,
                    width: '100%',
                    padding: '6px 10px',
                    borderRadius: 6,
                    background: 'rgba(56, 189, 248, 0.1)',
                    border: '1px solid rgba(56, 189, 248, 0.3)',
                    color: '#38bdf8',
                    fontSize: '11px',
                    fontWeight: 600,
                    cursor: 'pointer',
                    textAlign: 'left',
                    flexShrink: 0,
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6, minWidth: 0 }}>
                    <span style={{ fontSize: '12px', lineHeight: 1 }}>←</span>
                    <EntityNodeBadge kind={backHeaderModel.kind} size={16} />
                    <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      Back to {backHeaderModel.title}
                    </span>
                  </div>
                  {backHeaderModel.isGroup && (
                    <span
                      style={{
                        fontSize: '9.5px',
                        padding: '1px 5px',
                        borderRadius: 3,
                        background: 'rgba(56, 189, 248, 0.18)',
                        color: '#7dd3fc',
                        flexShrink: 0,
                      }}
                    >
                      Group List
                    </span>
                  )}
                </button>
              )}

              {schema.nodeSections.map((section) => renderSection(section, selectedNode))}
            </div>
          </div>
        </FloatingPanel>
      )}

      {/* Edge Inspector */}
      {selectedEdge && schema.edgeHeader && (
        <FloatingPanel
          dock="top-right"
          docked={docked}
          onToggleDock={onToggleDock}
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
          expandTooltip={`Click to expand connection: ${schema.edgeHeader.getTitle(selectedEdge)}`}
          title={schema.edgeHeader.getTitle(selectedEdge)}
          badge={(() => {
            const dir = schema.edgeHeader!.getDirectionBadge(selectedEdge);
            return (
              <span
                className="floating-dock-badge"
                style={{
                  backgroundColor: dir.isBidirectional ? 'rgba(56, 189, 248, 0.2)' : 'rgba(251, 191, 36, 0.2)',
                  color: dir.isBidirectional ? '#38bdf8' : '#fbbf24',
                  border: `1px solid ${dir.isBidirectional ? 'rgba(56, 189, 248, 0.4)' : 'rgba(251, 191, 36, 0.4)'}`,
                }}
              >
                {dir.text}
              </span>
            );
          })()}
          subtitle={(() => {
            const ep = schema.edgeHeader!.getEndpoints(selectedEdge);
            return (
              <div className="inspector-edge-endpoints" style={{ width: '100%', marginTop: 2 }}>
                <div
                  className="inspector-edge-endpoint-btn"
                  onClick={() => handleSelectNodeById(ep.sourceId)}
                  title={`Inspect ${ep.sourceName}`}
                  style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}
                >
                  <EntityNodeBadge kind={ep.sourceKind} size={18} />
                  <span className="endpoint-name">{ep.sourceName}</span>
                </div>

                <span className="endpoint-arrow">{ep.isBidirectional ? '⇄' : '➔'}</span>

                <div
                  className="inspector-edge-endpoint-btn"
                  onClick={() => handleSelectNodeById(ep.targetId)}
                  title={`Inspect ${ep.targetName}`}
                  style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}
                >
                  <EntityNodeBadge kind={ep.targetKind} size={18} />
                  <span className="endpoint-name">{ep.targetName}</span>
                </div>
              </div>
            );
          })()}
          collapsedTitle={schema.edgeHeader.getTitle(selectedEdge)}
          collapsedBadge={
            <span
              className="floating-dock-badge"
              style={{ backgroundColor: 'rgba(56, 189, 248, 0.25)', color: '#38bdf8', padding: '1px 6px', fontSize: '10px' }}
            >
              {schema.edgeHeader.getCollapsedCount(selectedEdge)}
            </span>
          }
        >
          {(schema.edgeSections || []).map((section) => renderSection(section, selectedEdge))}
        </FloatingPanel>
      )}
    </>
  );
}
