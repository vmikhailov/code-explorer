import React, { useState, useMemo, useEffect } from 'react';
import { SelectedNodeDetail, SelectedEdgeDetail, DomainProjectInfo, ConnectionDetailItem } from '../types';
import { FloatingPanel } from '../../components/FloatingPanel';
import {
  ChevronRightIcon,
  ChevronDownIcon,
  EyeOffIcon,
  TagIcon,
  GitBranchIcon,
  ExternalLinkIcon,
} from '../../components/Icons';
import { EntityNodeBadge, EntityCodeBadge, getEntityBadgeMeta } from '../entityBadges';
import { webviewTransport } from '../../services/webviewTransport';
import { ArchitectureEvidenceItemDto } from '../../../../../proto/types';

interface EvidenceSectionProps {
  title: string;
  url: string;
  emptyHint: string;
  loadingHint: string;
  onOpenFile?: (path: string, line?: number) => void;
  defaultExpanded?: boolean;
}

const EvidenceSection: React.FC<EvidenceSectionProps> = ({
  title,
  url,
  emptyHint,
  loadingHint,
  onOpenFile,
  defaultExpanded = true,
}) => {
  const [isExpanded, setIsExpanded] = useState(defaultExpanded);
  const [evidence, setEvidence] = useState<ArchitectureEvidenceItemDto[]>([]);
  const [warnings, setWarnings] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    const fetchEvidence = async () => {
      setLoading(true);
      setError(null);
      try {
        const data = await webviewTransport.fetchJson<{ items?: ArchitectureEvidenceItemDto[]; warnings?: string[] }>(url);
        if (!cancelled) {
          setEvidence(data?.items || []);
          setWarnings(data?.warnings || []);
        }
      } catch (err: any) {
        if (!cancelled) {
          setError(err?.message || 'Failed to load evidence');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };
    fetchEvidence();
    return () => {
      cancelled = true;
    };
  }, [url]);
  const getOpBadgeStyle = (op: string) => {
    const upper = (op || '').toUpperCase();
    switch (upper) {
      case 'SELECT':
        return { bg: 'rgba(56, 189, 248, 0.2)', border: 'rgba(56, 189, 248, 0.4)', text: '#38bdf8' };
      case 'INSERT':
        return { bg: 'rgba(74, 222, 128, 0.2)', border: 'rgba(74, 222, 128, 0.4)', text: '#4ade80' };
      case 'UPDATE':
        return { bg: 'rgba(251, 191, 36, 0.2)', border: 'rgba(251, 191, 36, 0.4)', text: '#fbbf24' };
      case 'DELETE':
        return { bg: 'rgba(248, 113, 113, 0.2)', border: 'rgba(248, 113, 113, 0.4)', text: '#f87171' };
      case 'POST':
      case 'PUT':
      case 'PATCH':
      case 'PUBLISH':
        return { bg: 'rgba(251, 146, 60, 0.2)', border: 'rgba(251, 146, 60, 0.4)', text: '#fb923c' };
      case 'GET':
      case 'SUBSCRIBE':
      case 'CALL':
        return { bg: 'rgba(45, 212, 191, 0.2)', border: 'rgba(45, 212, 191, 0.4)', text: '#2dd4bf' };
      case 'ORM':
        return { bg: 'rgba(192, 132, 252, 0.2)', border: 'rgba(192, 132, 252, 0.4)', text: '#c084fc' };
      case 'MIGRATION':
        return { bg: 'rgba(244, 114, 182, 0.2)', border: 'rgba(244, 114, 182, 0.4)', text: '#f472b6' };
      case 'CONFIG':
      default:
        return { bg: 'rgba(148, 163, 184, 0.2)', border: 'rgba(148, 163, 184, 0.4)', text: '#94a3b8' };
    }
  };

  return (
    <div className={`inspector-section ${isExpanded ? 'is-expanded' : 'is-collapsed'}`}>
      <div
        className="inspector-section-header"
        onClick={() => setIsExpanded((prev) => !prev)}
        title={isExpanded ? `Collapse ${title.toLowerCase()}` : `Expand ${title.toLowerCase()}`}
      >
        <span className="inspector-section-title">
          {title} {loading ? '(loading...)' : `(${evidence.length})`}
        </span>
        <span className="inspector-section-toggle">
          {isExpanded ? <ChevronDownIcon size={12} /> : <ChevronRightIcon size={12} />}
        </span>
      </div>

      {isExpanded && (
        <div className="inspector-evidence-list" style={{ display: 'flex', flexDirection: 'column', gap: 6, padding: '6px 8px' }}>
          {loading && (
            <div className="inspector-empty-hint" style={{ color: '#94a3b8' }}>
              {loadingHint}
            </div>
          )}
          {error && (
            <div className="inspector-empty-hint" style={{ color: '#f87171' }}>
              Error: {error}
            </div>
          )}
          {!loading && warnings.length > 0 && (
            <div className="inspector-empty-hint" style={{ color: '#fbbf24' }}>
              {warnings.join(' · ')}
            </div>
          )}
          {!loading && !error && evidence.length === 0 && (
            <div className="inspector-empty-hint">{emptyHint}</div>
          )}
          {!loading && evidence.map((item, idx) => {
            const badgeStyle = getOpBadgeStyle(item.operation || item.kind);
            return (
              <div
                key={idx}
                className="inspector-evidence-card"
                style={{
                  padding: '8px 10px',
                  borderRadius: 6,
                  background: 'rgba(255, 255, 255, 0.03)',
                  border: '1px solid rgba(255, 255, 255, 0.07)',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 5,
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 6 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                    <span
                      style={{
                        fontSize: '9.5px',
                        fontWeight: 700,
                        padding: '1px 6px',
                        borderRadius: 3,
                        background: badgeStyle.bg,
                        color: badgeStyle.text,
                        border: `1px solid ${badgeStyle.border}`,
                        textTransform: 'uppercase',
                        letterSpacing: '0.04em',
                      }}
                    >
                      {item.operation || item.kind}
                    </span>
                    <span style={{ fontSize: '11px', fontWeight: 600, color: '#f1f5f9' }}>
                      {item.title}
                    </span>
                    {item.confidence === 'inferred' && (
                      <span
                        title="Inferred via fallback heuristics"
                        style={{
                          fontSize: '9px',
                          fontWeight: 600,
                          padding: '1px 5px',
                          borderRadius: 3,
                          background: 'rgba(148, 163, 184, 0.14)',
                          color: '#94a3b8',
                          border: '1px solid rgba(148, 163, 184, 0.3)',
                        }}
                      >
                        Inferred
                      </span>
                    )}
                  </div>
                </div>

                {item.reason && (
                  <div style={{ fontSize: '10px', color: '#94a3b8' }}>{item.reason}</div>
                )}

                {item.codeSnippet && (
                  <pre
                    style={{
                      margin: '2px 0 0 0',
                      padding: '5px 8px',
                      background: 'rgba(0, 0, 0, 0.35)',
                      borderRadius: 4,
                      border: '1px solid rgba(255, 255, 255, 0.05)',
                      fontSize: '10px',
                      fontFamily: 'monospace',
                      color: '#e2e8f0',
                      whiteSpace: 'pre-wrap',
                      wordBreak: 'break-all',
                      maxHeight: '120px',
                      overflowY: 'auto',
                    }}
                  >
                    <code>{item.codeSnippet}</code>
                  </pre>
                )}

                {item.callerSymbol && (
                  <div style={{ fontSize: '10px', color: '#94a3b8', display: 'flex', alignItems: 'center', gap: 4 }}>
                    <span style={{ color: '#64748b' }}>caller:</span>
                    <code style={{ color: '#cbd5e1', fontSize: '10px' }}>{item.callerSymbol}</code>
                    {item.targetSymbol && (
                      <>
                        <span style={{ color: '#64748b' }}>→</span>
                        <code style={{ color: '#cbd5e1', fontSize: '10px' }}>{item.targetSymbol}</code>
                      </>
                    )}
                  </div>
                )}

                {item.filePath && (
                  <div
                    style={{
                      fontSize: '10px',
                      color: '#38bdf8',
                      display: 'flex',
                      alignItems: 'center',
                      gap: 4,
                      cursor: onOpenFile ? 'pointer' : 'default',
                      textDecoration: 'underline',
                      marginTop: 2,
                    }}
                    onClick={() => onOpenFile?.(item.filePath!, item.lineStart && item.lineStart > 0 ? item.lineStart : undefined)}
                    title={`Open ${item.filePath}${item.lineStart && item.lineStart > 0 ? `:${item.lineStart}` : ''}`}
                  >
                    <ExternalLinkIcon size={10} />
                    <span>
                      {item.filePath.split(/[/\\]/).slice(-2).join('/')}
                      {item.lineStart && item.lineStart > 0 ? `:${item.lineStart}` : ''}
                    </span>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};

export interface DatabaseEvidenceSectionProps {
  databaseId: string;
  databaseName?: string;
  serviceId?: string;
  onOpenFile?: (path: string, line?: number) => void;
  defaultExpanded?: boolean;
}

export const DatabaseEvidenceSection: React.FC<DatabaseEvidenceSectionProps> = ({
  databaseId,
  serviceId,
  onOpenFile,
  defaultExpanded,
}) => {
  const url = useMemo(() => {
    const params = new URLSearchParams({ id: databaseId });
    if (serviceId) params.set('service', serviceId);
    return `/api/database/evidence?${params.toString()}`;
  }, [databaseId, serviceId]);

  return (
    <EvidenceSection
      title={`Database Evidence${serviceId ? ` (${serviceId})` : ''}`}
      url={url}
      loadingHint="Fetching query & schema evidence..."
      emptyHint="No direct SQL queries, ORM entities, or migrations found for this database."
      onOpenFile={onOpenFile}
      defaultExpanded={defaultExpanded}
    />
  );
};

export interface EdgeEvidenceSectionProps {
  sourceIds: string[];
  targetIds: string[];
  edgeKind?: string;
  onOpenFile?: (path: string, line?: number) => void;
  defaultExpanded?: boolean;
}

/** Source-code evidence for any selected edge; group edges pass every member id per side. */
export const EdgeEvidenceSection: React.FC<EdgeEvidenceSectionProps> = ({
  sourceIds,
  targetIds,
  edgeKind,
  onOpenFile,
  defaultExpanded,
}) => {
  const sourceKey = sourceIds.join('\u0000');
  const targetKey = targetIds.join('\u0000');
  const url = useMemo(() => {
    const params = new URLSearchParams();
    sourceIds.forEach((id) => params.append('sourceIds', id));
    targetIds.forEach((id) => params.append('targetIds', id));
    if (edgeKind) params.set('kind', edgeKind);
    return `/api/architecture/edge-evidence?${params.toString()}`;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sourceKey, targetKey, edgeKind]);

  return (
    <EvidenceSection
      title="Evidence"
      url={url}
      loadingHint="Tracing the edge back to source code..."
      emptyHint="No source-level evidence was recorded for this connection."
      onOpenFile={onOpenFile}
      defaultExpanded={defaultExpanded}
    />
  );
};

export interface NodeEvidenceSectionProps {
  nodeIds: string[];
  nodeKind?: string;
  onOpenFile?: (path: string, line?: number) => void;
  defaultExpanded?: boolean;
}

/** Source-code evidence proving any selected node (or grouped nodes). */
export const NodeEvidenceSection: React.FC<NodeEvidenceSectionProps> = ({
  nodeIds,
  nodeKind,
  onOpenFile,
  defaultExpanded = false,
}) => {
  const idsKey = nodeIds.join('\u0000');
  const url = useMemo(() => {
    const params = new URLSearchParams();
    nodeIds.forEach((id) => params.append('ids', id));
    if (nodeKind) params.set('kind', nodeKind);
    return `/api/architecture/node-evidence?${params.toString()}`;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [idsKey, nodeKind]);

  return (
    <EvidenceSection
      title="Code Evidence"
      url={url}
      loadingHint="Tracing node capabilities in source code..."
      emptyHint="No direct source-level evidence found for this node."
      onOpenFile={onOpenFile}
      defaultExpanded={defaultExpanded}
    />
  );
};

export interface RawEdgeInfo {
  id: string;
  source: string;
  target: string;
  category: 'service_call' | 'database' | 'messaging' | 'external' | 'library' | 'test';
  label: string;
  count: number;
  kind?: string;
}

export interface NodeConnectionItem {
  id: string;
  name: string;
  kind?: string;
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
  onUngroupNode?: (node: SelectedNodeDetail) => void;
  onRemoveFromGroup?: (groupId: string, nodeId: string) => void;
  onFocusInFlow?: (name: string) => void;
  onOpenProjectMap?: (name: string) => void;
  onOpenFile?: (path: string, line?: number) => void;
  onOpenOverrideModal?: (name: string) => void;
  docked?: boolean;
  onToggleDock?: () => void;
}

export const DomainInspector: React.FC<DomainInspectorProps> = (props) => {
  const selectedNode = props.selectedNode ?? null;
  const selectedEdge = props.selectedEdge ?? null;
  const selectNode = props.onSelectNode;
  const selectEdge = props.onSelectEdge;
  const clearSelection = props.onClearSelection;
  const isInspectorCollapsed = !!props.isInspectorCollapsed;
  const setIsInspectorCollapsed = props.onToggleCollapse;
  const hideNode = props.onHideNode;
  const onUngroupNode = props.onUngroupNode;
  const onRemoveFromGroup = props.onRemoveFromGroup;
  const onFocusInFlow = props.onFocusInFlow;
  const onOpenProjectMap = props.onOpenProjectMap;
  const onOpenFile = props.onOpenFile;
  const openOverrideModal = props.onOpenOverrideModal;
  const nodeDetailMap = props.nodeDetailMap;
  const onClearGraphHighlight = props.onClearGraphHighlight;
  const rawEdges = props.rawEdges;

  // Local state for edge inspector collapse if not controlled from parent
  const [localEdgeCollapsed, setLocalEdgeCollapsed] = useState(false);
  const isEdgeInspectorCollapsed = props.isEdgeInspectorCollapsed !== undefined ? props.isEdgeInspectorCollapsed : localEdgeCollapsed;
  const setIsEdgeInspectorCollapsed = props.onToggleEdgeCollapse || setLocalEdgeCollapsed;

  // Local state for expanded database / connection evidence under service or node
  const [expandedDbEvidenceId, setExpandedDbEvidenceId] = useState<string | null>(null);
  const [expandedConnEvidenceKey, setExpandedConnEvidenceKey] = useState<string | null>(null);

  // Filter for large grouped entity lists
  const [groupMemberFilter, setGroupMemberFilter] = useState('');

  // Inspector navigation history stack (for returning to group list or previous inspected node)
  const [navHistory, setNavHistory] = useState<SelectedNodeDetail[]>([]);

  // Find parent group if current selectedNode belongs to a collapsed group in nodeDetailMap
  const parentGroupNode = useMemo<SelectedNodeDetail | null>(() => {
    if (!selectedNode || selectedNode.isGroup || !nodeDetailMap) return null;
    for (const n of nodeDetailMap.values()) {
      if (
        n.isGroup &&
        (n.memberIds?.includes(selectedNode.id) || n.memberNodes?.some((m) => m.id === selectedNode.id))
      ) {
        return n;
      }
    }
    return null;
  }, [selectedNode, nodeDetailMap]);

  const backTargetNode = useMemo<SelectedNodeDetail | null>(() => {
    if (!selectedNode) return null;
    for (let i = navHistory.length - 1; i >= 0; i--) {
      const candidate = navHistory[i];
      if (candidate && candidate.id !== selectedNode.id) {
        return nodeDetailMap?.get(candidate.id) || candidate;
      }
    }
    return parentGroupNode;
  }, [selectedNode, navHistory, parentGroupNode, nodeDetailMap]);

  // Accordion section collapse state
  const [collapsedSections, setCollapsedSections] = useState<Record<string, boolean>>({});

  const toggleSection = (key: string) => {
    setCollapsedSections((prev) => ({
      ...prev,
      [key]: !prev[key],
    }));
  };

  const handleClose = () => {
    setNavHistory([]);
    clearSelection?.();
    onClearGraphHighlight?.();
  };

  const handleSelectNodeById = (nodeId: string) => {
    if (selectedNode && selectedNode.id !== nodeId) {
      setNavHistory((prev) => [...prev.filter((n) => n.id !== selectedNode.id), selectedNode]);
    }
    setGroupMemberFilter('');
    if (props.onSelectNodeById) {
      props.onSelectNodeById(nodeId);
      return;
    }
    const targetNode = nodeDetailMap?.get(nodeId);
    if (targetNode && selectNode) {
      selectNode(targetNode);
    }
  };

  const handleGoBack = () => {
    if (!backTargetNode) return;
    setNavHistory((prev) => {
      const idx = prev.map((n) => n.id).lastIndexOf(backTargetNode.id);
      return idx >= 0 ? prev.slice(0, idx) : [];
    });
    setGroupMemberFilter('');
    if (props.onSelectNodeById) {
      props.onSelectNodeById(backTargetNode.id);
      return;
    }
    if (selectNode) {
      selectNode(backTargetNode);
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
            kind: sourceDetail?.kind || 'Service',
            kindTag: getEntityBadgeMeta(sourceDetail?.kind || 'Service').code,
            bgColor: sourceDetail?.bgColor || getEntityBadgeMeta(sourceDetail?.kind || 'Service').bgColor,
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
            kind: targetDetail?.kind || 'Service',
            kindTag: getEntityBadgeMeta(targetDetail?.kind || 'Service').code,
            bgColor: targetDetail?.bgColor || getEntityBadgeMeta(targetDetail?.kind || 'Service').bgColor,
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
            kind: targetDetail?.kind || 'Database',
            kindTag: getEntityBadgeMeta(targetDetail?.kind || 'Database').code,
            bgColor: targetDetail?.bgColor || getEntityBadgeMeta(targetDetail?.kind || 'Database').bgColor,
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
            kind: sourceDetail?.kind || 'Service',
            kindTag: getEntityBadgeMeta(sourceDetail?.kind || 'Service').code,
            bgColor: sourceDetail?.bgColor || getEntityBadgeMeta(sourceDetail?.kind || 'Service').bgColor,
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
          kind: otherDetail?.kind || 'Topic',
          kindTag: getEntityBadgeMeta(otherDetail?.kind || 'Topic').code,
          bgColor: otherDetail?.bgColor || getEntityBadgeMeta(otherDetail?.kind || 'Topic').bgColor,
          count: e.count,
          label,
        });
      }
    }
    return Array.from(map.values()).sort((a, b) => b.count - a.count);
  }, [selectedNode, rawEdges, nodeDetailMap]);

  if (!selectedNode && !selectedEdge) {
    if (props.docked) {
      return (
        <div className="domain-panel-empty-state">
          <div className="domain-empty-card">
            <span className="domain-empty-icon">ℹ️</span>
            <h4 className="domain-empty-title">No Element Selected</h4>
            <p className="domain-empty-desc">
              Click any microservice, database, topic, or connection edge on the canvas to inspect its architecture, calls, and relationships.
            </p>
          </div>
        </div>
      );
    }
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
            <div style={{ display: 'inline-flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
              <EntityNodeBadge kind={selectedNode.kind} size={20} />
              <EntityCodeBadge kind={selectedNode.kind} />
              {selectedNode.secondaryRoles?.map((r: string) => (
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
                  <EntityNodeBadge kind={r as any} size={12} />
                  +{r.slice(0, 3).toUpperCase()}
                </span>
              ))}
            </div>
          }
          headerActions={
            <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
              {backTargetNode && (
                <button
                  type="button"
                  className="floating-dock-btn action-btn back-btn"
                  onClick={handleGoBack}
                  title={`Back to ${backTargetNode.displayName || backTargetNode.name}`}
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
              {selectedNode.isGroup && onUngroupNode && (
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
              {hideNode && (
                <button
                  type="button"
                  className="floating-dock-btn action-btn hide-btn"
                  onClick={() => hideNode(selectedNode.id)}
                  title="Hide this node (Transitive connections bypass it) [Shortcut: H, Del]"
                >
                  <EyeOffIcon size={12} />
                  <span>Hide</span>
                </button>
              )}
            </div>
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
            <div style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}>
              <EntityNodeBadge kind={selectedNode.kind} size={18} />
              <EntityCodeBadge kind={selectedNode.kind} />
              {selectedNode.secondaryRoles?.map((r: string) => (
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
          {/* Inspector Body */}
          <div className="inspector-body">
                {/* Back to Group / Previous Node Banner */}
                {backTargetNode && (
                  <button
                    type="button"
                    onClick={handleGoBack}
                    title={`Return to ${backTargetNode.displayName || backTargetNode.name}`}
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
                      <EntityNodeBadge kind={backTargetNode.kind} size={16} />
                      <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        Back to {backTargetNode.displayName || backTargetNode.name}
                      </span>
                    </div>
                    {backTargetNode.isGroup && (
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
                  {['Service', 'Ingress', 'App', 'Worker', 'CliTool', 'Library'].includes(selectedNode.kind) && onOpenProjectMap && (
                    <button
                      type="button"
                      className="inspector-action-btn secondary"
                      onClick={() => onOpenProjectMap(selectedNode.name)}
                      title="Open the internal module map (C3) of this project"
                    >
                      Open in Project Map
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
                {(selectedNode.isGroup || selectedNode.kind === 'Service' || selectedNode.kind === 'Ingress' || selectedNode.kind === 'Worker' || selectedNode.kind === 'App') && (
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

                {/* Grouped Entities Section */}
                {selectedNode.isGroup && selectedNode.memberNodes && selectedNode.memberNodes.length > 0 && (
                  <div className={`inspector-section ${collapsedSections['grouped-entities'] ? 'is-collapsed' : 'is-expanded'}`}>
                    <div
                      className="inspector-section-header"
                      onClick={() => toggleSection('grouped-entities')}
                      title={collapsedSections['grouped-entities'] ? 'Expand grouped entities' : 'Collapse grouped entities'}
                      style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                        <span className="inspector-section-title">
                          Grouped Entities ({selectedNode.memberNodes.length})
                        </span>
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
                          {selectedNode.memberNodes.length}
                        </span>
                      </div>
                      <span className="inspector-section-toggle">
                        {collapsedSections['grouped-entities'] ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
                      </span>
                    </div>
                    {!collapsedSections['grouped-entities'] && (
                      <div
                        className="inspector-conn-list"
                        style={{
                          display: 'flex',
                          flexDirection: 'column',
                          gap: 4,
                          marginTop: 6,
                        }}
                      >
                        {selectedNode.memberNodes.length > 8 && (
                          <div style={{ marginBottom: 4 }}>
                            <input
                              type="text"
                              value={groupMemberFilter}
                              onChange={(e) => setGroupMemberFilter(e.target.value)}
                              placeholder={`Filter ${selectedNode.memberNodes.length} grouped entities...`}
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
                        {selectedNode.memberNodes
                          .filter((m: SelectedNodeDetail) => {
                            if (!groupMemberFilter.trim()) return true;
                            const q = groupMemberFilter.toLowerCase();
                            return (
                              (m.displayName || '').toLowerCase().includes(q) ||
                              (m.name || '').toLowerCase().includes(q)
                            );
                          })
                          .map((m: SelectedNodeDetail) => (
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
                              <span style={{ fontSize: '11px', color: '#e2e8f0', fontWeight: 600, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                {m.displayName || m.name}
                              </span>
                              {m.tier !== undefined && (
                                <span style={{ fontSize: '9px', color: '#94a3b8', background: 'rgba(255, 255, 255, 0.05)', padding: '1px 4px', borderRadius: 3 }}>
                                  T{m.tier}
                                </span>
                              )}
                            </div>
                            {selectedNode.groupType === 'custom' && selectedNode.customGroupId && onRemoveFromGroup && (
                              <button
                                type="button"
                                className="action-icon-btn"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  onRemoveFromGroup(selectedNode.customGroupId!, m.id);
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
                )}

                {/* Internal Connections Section */}
                {selectedNode.isGroup && selectedNode.internalConnections && selectedNode.internalConnections.length > 0 && (
                  <div className={`inspector-section ${collapsedSections['internal-connections'] ? 'is-collapsed' : 'is-expanded'}`}>
                    <div
                      className="inspector-section-header"
                      onClick={() => toggleSection('internal-connections')}
                      title={collapsedSections['internal-connections'] ? 'Expand internal connections' : 'Collapse internal connections'}
                      style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                        <span className="inspector-section-title">
                          Internal Connections ({selectedNode.internalConnections.length})
                        </span>
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
                          {selectedNode.internalConnections.length}
                        </span>
                      </div>
                      <span className="inspector-section-toggle">
                        {collapsedSections['internal-connections'] ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
                      </span>
                    </div>
                    {!collapsedSections['internal-connections'] && (
                      <div
                        className="inspector-conn-list"
                        style={{
                          display: 'flex',
                          flexDirection: 'column',
                          gap: 4,
                          marginTop: 6,
                        }}
                      >
                        {selectedNode.internalConnections.map((item: ConnectionDetailItem) => {
                          const evKey = `internal:${item.id}`;
                          const isEvOpen = expandedConnEvidenceKey === evKey;
                          return (
                          <div
                            key={item.id}
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
                                <span style={{ fontWeight: 600, color: '#38bdf8' }}>{item.sourceName}</span>
                                <span style={{ color: '#64748b' }}>➔</span>
                                <span style={{ fontWeight: 600, color: '#a78bfa' }}>{item.targetName}</span>
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
                                {item.count > 1 && (
                                  <span style={{ fontSize: '9.5px', fontWeight: 700, background: 'rgba(255, 255, 255, 0.08)', color: '#cbd5e1', padding: '1px 5px', borderRadius: 3 }}>
                                    {item.count}×
                                  </span>
                                )}
                              </div>
                            </div>
                            {item.label && (
                              <div style={{ fontSize: '10px', color: '#94a3b8' }}>{item.label}</div>
                            )}
                            {isEvOpen && (
                              <div style={{ marginTop: 4 }}>
                                <EdgeEvidenceSection
                                  sourceIds={[item.sourceId]}
                                  targetIds={[item.targetId]}
                                  edgeKind={item.category}
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
                        {inboundConnections.map((item) => {
                          const evKey = `in:${item.id}`;
                          const isEvOpen = expandedConnEvidenceKey === evKey;
                          return (
                            <div key={item.id} style={{ display: 'flex', flexDirection: 'column', width: '100%' }}>
                              <div
                                className="inspector-connection-item"
                                onClick={() => handleSelectNodeById(item.id)}
                                title={`Jump to caller ${item.name}`}
                              >
                                <div className="connection-item-main" style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                                  <EntityNodeBadge kind={item.kind || 'Service'} size={18} />
                                  <span className="connection-item-name">{item.name}</span>
                                </div>
                                <div className="connection-item-meta" style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
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
                                    title="Inspect code evidence for this inbound call"
                                  >
                                    {isEvOpen ? 'Hide Evidence' : 'Evidence'}
                                  </button>
                                  <span className="connection-count-badge">
                                    {item.count} {item.count === 1 ? 'call' : 'calls'}
                                  </span>
                                  <span className="connection-jump-arrow">➔</span>
                                </div>
                              </div>
                              {isEvOpen && (
                                <div style={{ marginLeft: 6, marginTop: 4, marginBottom: 6 }}>
                                  <EdgeEvidenceSection
                                    sourceIds={[item.id]}
                                    targetIds={selectedNode.isGroup && selectedNode.memberIds?.length ? selectedNode.memberIds : [selectedNode.id]}
                                    edgeKind="service_call"
                                    onOpenFile={onOpenFile}
                                    defaultExpanded={true}
                                  />
                                </div>
                              )}
                            </div>
                          );
                        })}
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
                        {outboundConnections.map((item) => {
                          const evKey = `out:${item.id}`;
                          const isEvOpen = expandedConnEvidenceKey === evKey;
                          return (
                            <div key={item.id} style={{ display: 'flex', flexDirection: 'column', width: '100%' }}>
                              <div
                                className="inspector-connection-item"
                                onClick={() => handleSelectNodeById(item.id)}
                                title={`Jump to downstream service ${item.name}`}
                              >
                                <div className="connection-item-main" style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                                  <EntityNodeBadge kind={item.kind || 'Service'} size={18} />
                                  <span className="connection-item-name">{item.name}</span>
                                </div>
                                <div className="connection-item-meta" style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
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
                                    title="Inspect code evidence for this outbound call"
                                  >
                                    {isEvOpen ? 'Hide Evidence' : 'Evidence'}
                                  </button>
                                  <span className="connection-count-badge">
                                    {item.count} {item.count === 1 ? 'call' : 'calls'}
                                  </span>
                                  <span className="connection-jump-arrow">➔</span>
                                </div>
                              </div>
                              {isEvOpen && (
                                <div style={{ marginLeft: 6, marginTop: 4, marginBottom: 6 }}>
                                  <EdgeEvidenceSection
                                    sourceIds={selectedNode.isGroup && selectedNode.memberIds?.length ? selectedNode.memberIds : [selectedNode.id]}
                                    targetIds={[item.id]}
                                    edgeKind={item.kind === 'ExternalService' ? 'external' : 'service_call'}
                                    onOpenFile={onOpenFile}
                                    defaultExpanded={true}
                                  />
                                </div>
                              )}
                            </div>
                          );
                        })}
                        {outboundConnections.length === 0 && (
                          <div className="inspector-empty-hint">No outbound calls</div>
                        )}
                      </div>
                    )}
                  </div>
                )}

                {/* Database Node Evidence Inspector */}
                {selectedNode.kind === 'Database' && (
                  <DatabaseEvidenceSection
                    databaseId={selectedNode.id}
                    databaseName={selectedNode.displayName}
                    onOpenFile={onOpenFile}
                    defaultExpanded={true}
                  />
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
                          <div key={item.id} style={{ display: 'flex', flexDirection: 'column', width: '100%' }}>
                            <div
                              className="inspector-connection-item"
                              onClick={() => handleSelectNodeById(item.id)}
                              title={`Jump to ${item.name}`}
                            >
                              <div className="connection-item-main" style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                                <EntityNodeBadge kind={item.kind || 'Database'} size={18} />
                                <span className="connection-item-name">{item.name}</span>
                              </div>
                              <div className="connection-item-meta" style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                                <button
                                  type="button"
                                  style={{
                                    fontSize: '9px',
                                    fontWeight: 600,
                                    padding: '1px 5px',
                                    borderRadius: 3,
                                    background: expandedDbEvidenceId === item.id ? 'rgba(56, 189, 248, 0.3)' : 'rgba(255, 255, 255, 0.08)',
                                    color: expandedDbEvidenceId === item.id ? '#38bdf8' : '#cbd5e1',
                                    border: '1px solid rgba(255, 255, 255, 0.12)',
                                    cursor: 'pointer',
                                  }}
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    setExpandedDbEvidenceId((prev) => (prev === item.id ? null : item.id));
                                  }}
                                  title="Inspect queries and evidence for this database"
                                >
                                  {expandedDbEvidenceId === item.id ? 'Hide Evidence' : 'Evidence'}
                                </button>
                                <span className="connection-count-badge">{item.count} links</span>
                                <span className="connection-jump-arrow">➔</span>
                              </div>
                            </div>
                            {expandedDbEvidenceId === item.id && (
                              <div style={{ marginLeft: 6, marginTop: 4, marginBottom: 6 }}>
                                <DatabaseEvidenceSection
                                  databaseId={selectedNode.kind === 'Database' ? selectedNode.id : item.id}
                                  databaseName={selectedNode.kind === 'Database' ? selectedNode.name : item.name}
                                  serviceId={selectedNode.kind === 'Database' ? item.id : selectedNode.id}
                                  onOpenFile={onOpenFile}
                                  defaultExpanded={true}
                                />
                              </div>
                            )}
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
                        {topicConnections.map((item) => {
                          const evKey = `topic:${item.id}`;
                          const isEvOpen = expandedConnEvidenceKey === evKey;
                          return (
                            <div key={item.id} style={{ display: 'flex', flexDirection: 'column', width: '100%' }}>
                              <div
                                className="inspector-connection-item"
                                onClick={() => handleSelectNodeById(item.id)}
                                title={`Jump to ${item.name}`}
                              >
                                <div className="connection-item-main" style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                                  <EntityNodeBadge kind={item.kind || 'Topic'} size={18} />
                                  <span className="connection-item-name">{item.name}</span>
                                </div>
                                <div className="connection-item-meta" style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
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
                                    title="Inspect publisher/subscriber evidence for this topic"
                                  >
                                    {isEvOpen ? 'Hide Evidence' : 'Evidence'}
                                  </button>
                                  {item.label && <span className="connection-count-badge">{item.label}</span>}
                                  <span className="connection-jump-arrow">➔</span>
                                </div>
                              </div>
                              {isEvOpen && (
                                <div style={{ marginLeft: 6, marginTop: 4, marginBottom: 6 }}>
                                  <EdgeEvidenceSection
                                    sourceIds={selectedNode.kind === 'Topic' ? [item.id] : [selectedNode.id]}
                                    targetIds={selectedNode.kind === 'Topic' ? [selectedNode.id] : [item.id]}
                                    edgeKind="messaging"
                                    onOpenFile={onOpenFile}
                                    defaultExpanded={true}
                                  />
                                </div>
                              )}
                            </div>
                          );
                        })}
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
                {!selectedNode.isGroup && selectedNode.projects && selectedNode.projects.length > 1 && (
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
                            {p.secondaryRoles?.map((r: string) => (
                              <span
                                key={r}
                                className="subproject-lib-tag"
                                style={{ background: 'rgba(168, 85, 247, 0.2)', color: '#c084fc' }}
                              >
                                +{r.slice(0, 3).toUpperCase()}
                              </span>
                            ))}
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

                {/* Accordion: Universal Code Evidence for Node */}
                <NodeEvidenceSection
                  nodeIds={
                    selectedNode.isGroup && selectedNode.memberIds && selectedNode.memberIds.length > 0
                      ? selectedNode.memberIds
                      : selectedNode.projects && selectedNode.projects.length > 0
                      ? Array.from(new Set([selectedNode.id, ...selectedNode.projects.map((p: DomainProjectInfo) => p.id)]))
                      : [selectedNode.id]
                  }
                  nodeKind={selectedNode.kind}
                  onOpenFile={onOpenFile}
                  defaultExpanded={true}
                />
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
                style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}
              >
                <EntityNodeBadge kind={selectedEdge.sourceKind || 'Service'} size={18} />
                <span className="endpoint-name">{selectedEdge.sourceName}</span>
              </div>

              <span className="endpoint-arrow">{selectedEdge.isBidirectional ? '⇄' : '➔'}</span>

              <div
                className="inspector-edge-endpoint-btn"
                onClick={() => handleSelectNodeById(selectedEdge.targetId)}
                title={`Inspect ${selectedEdge.targetName}`}
                style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}
              >
                <EntityNodeBadge kind={selectedEdge.targetKind || 'Service'} size={18} />
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

              {/* Source-level evidence for every edge type; group edges expand to member ids */}
              <div style={{ padding: '4px 12px' }}>
                <EdgeEvidenceSection
                  sourceIds={
                    selectedEdge.items.length > 0
                      ? Array.from(new Set(selectedEdge.items.map((it: ConnectionDetailItem) => it.sourceId)))
                      : [selectedEdge.sourceId]
                  }
                  targetIds={
                    selectedEdge.items.length > 0
                      ? Array.from(new Set(selectedEdge.items.map((it: ConnectionDetailItem) => it.targetId)))
                      : [selectedEdge.targetId]
                  }
                  edgeKind={selectedEdge.primaryCategory}
                  onOpenFile={onOpenFile}
                  defaultExpanded={true}
                />
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
