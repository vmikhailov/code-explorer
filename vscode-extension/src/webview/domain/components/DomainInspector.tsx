import React, { useState, useMemo, useEffect } from 'react';
import { SelectedNodeDetail, SelectedEdgeDetail } from '../types';
import {
  ChevronRightIcon,
  ChevronDownIcon,
  ExternalLinkIcon,
} from '../../components/Icons';
import { webviewTransport } from '../../services/webviewTransport';
import { ArchitectureEvidenceItemDto } from '../../../../../proto/types';
import { createArchitectureInspectorSchema, RawEdgeInfo } from '../inspectorSchema';
import { DeclarativeInspector } from '../../components/DeclarativeInspector';

export type { RawEdgeInfo };

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
  const schema = useMemo(
    () =>
      createArchitectureInspectorSchema({
        rawEdges: props.rawEdges,
        nodeDetailMap: props.nodeDetailMap,
        onFocusInFlow: props.onFocusInFlow,
        onOpenProjectMap: props.onOpenProjectMap,
        onOpenFile: props.onOpenFile,
        onOpenOverrideModal: props.onOpenOverrideModal,
        onRemoveFromGroup: props.onRemoveFromGroup,
      }),
    [
      props.rawEdges,
      props.nodeDetailMap,
      props.onFocusInFlow,
      props.onOpenProjectMap,
      props.onOpenFile,
      props.onOpenOverrideModal,
      props.onRemoveFromGroup,
    ]
  );

  return (
    <DeclarativeInspector
      schema={schema}
      selectedNode={props.selectedNode}
      selectedEdge={props.selectedEdge}
      nodeDetailMap={props.nodeDetailMap}
      onClearGraphHighlight={props.onClearGraphHighlight}
      onSelectNode={props.onSelectNode}
      onSelectNodeById={props.onSelectNodeById}
      onSelectEdge={props.onSelectEdge}
      onClearSelection={props.onClearSelection}
      isInspectorCollapsed={props.isInspectorCollapsed}
      onToggleCollapse={props.onToggleCollapse}
      isEdgeInspectorCollapsed={props.isEdgeInspectorCollapsed}
      onToggleEdgeCollapse={props.onToggleEdgeCollapse}
      onHideNode={props.onHideNode}
      onUngroupNode={props.onUngroupNode}
      onOpenFile={props.onOpenFile}
      docked={props.docked}
      onToggleDock={props.onToggleDock}
    />
  );
};
