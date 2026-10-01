import React, { useMemo } from 'react';
import { DomainGroupSummary, SelectedNodeDetail, EntityKind } from '../types';
import { useDomainArchitecture } from '../context/DomainArchitectureContext';

export interface DomainGridViewProps {
  domainGroups: DomainGroupSummary[];
  allNodes: SelectedNodeDetail[];
  searchQuery?: string;
  hiddenTypes?: Set<EntityKind>;
  hiddenNodeIds?: Set<string>;
  hiddenOrbitTiers?: Set<number>;
  hideSingleConnectionDbs?: boolean;
  hideIsolatedNodes?: boolean;
  selectedNode?: SelectedNodeDetail | null;
  onSelectNode?: (node: SelectedNodeDetail | null) => void;
  onFocusInFlow?: (name: string) => void;
  onOpenFile?: (path: string, line?: number) => void;
  onOpenOverrideModal?: (name: string) => void;
}

export const DomainGridView: React.FC<DomainGridViewProps> = (props) => {
  let ctx: any = null;
  try {
    ctx = useDomainArchitecture();
  } catch {}

  const searchQuery = props.searchQuery !== undefined ? props.searchQuery : (ctx?.searchQuery || '');
  const hiddenTypes = props.hiddenTypes || ctx?.hiddenTypes || new Set();
  const hiddenNodeIds = props.hiddenNodeIds || ctx?.hiddenNodeIds || new Set();
  const hiddenOrbitTiers = props.hiddenOrbitTiers || ctx?.hiddenOrbitTiers || new Set();
  const hideSingleConnectionDbs = props.hideSingleConnectionDbs !== undefined ? props.hideSingleConnectionDbs : !!ctx?.hideSingleConnectionDbs;
  const hideIsolatedNodes = props.hideIsolatedNodes !== undefined ? props.hideIsolatedNodes : !!ctx?.hideIsolatedNodes;
  const selectedNode = props.selectedNode !== undefined ? props.selectedNode : ctx?.selectedNode;
  const selectNode = props.onSelectNode || ctx?.selectNode;
  const onFocusInFlow = props.onFocusInFlow || ctx?.onFocusInFlow;
  const onOpenFile = props.onOpenFile || ctx?.onOpenFile;
  const openOverrideModal = props.onOpenOverrideModal || ctx?.openOverrideModal;
  const domainGroups = props.domainGroups;

  // Filter nodes based on active filters
  const filterNode = (node: SelectedNodeDetail): boolean => {
    if (hiddenNodeIds.has(node.id)) return false;
    if (hiddenTypes.has(node.kind)) return false;
    if (node.tier !== undefined && hiddenOrbitTiers.has(node.tier)) return false;

    if (hideSingleConnectionDbs && node.kind === 'Database' && node.inboundCallsCount <= 1) {
      return false;
    }

    if (
      hideIsolatedNodes &&
      node.inboundCallsCount === 0 &&
      node.outboundCallsCount === 0 &&
      node.dbCount === 0 &&
      node.messagingCount === 0
    ) {
      return false;
    }

    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase().trim();
      const matchName = node.name.toLowerCase().includes(q);
      const matchDisplay = node.displayName.toLowerCase().includes(q);
      const matchDomain = (node.domain || '').toLowerCase().includes(q);
      const matchKind = node.kind.toLowerCase().includes(q);
      const matchProj = node.projects.some((p) => p.name.toLowerCase().includes(q));
      if (!matchName && !matchDisplay && !matchDomain && !matchKind && !matchProj) {
        return false;
      }
    }

    return true;
  };

  const filteredGroups = useMemo(() => {
    return domainGroups
      .map((g) => ({
        ...g,
        nodes: g.nodes.filter(filterNode),
        databases: g.databases.filter(filterNode),
        topics: g.topics.filter(filterNode),
        externalServices: g.externalServices.filter(filterNode),
      }))
      .filter(
        (g) =>
          g.nodes.length > 0 ||
          g.databases.length > 0 ||
          g.topics.length > 0 ||
          g.externalServices.length > 0
      );
  }, [
    domainGroups,
    hiddenNodeIds,
    hiddenTypes,
    hiddenOrbitTiers,
    hideSingleConnectionDbs,
    hideIsolatedNodes,
    searchQuery,
  ]);

  return (
    <div className="domain-grid-viewport" style={{ flex: 1, overflowY: 'auto', padding: '16px 20px', background: 'var(--vscode-editor-background, #1e1e1e)' }}>
      {filteredGroups.length === 0 ? (
        <div style={{ textAlign: 'center', padding: '60px 20px', color: 'var(--vscode-descriptionForeground, #888)' }}>
          <div style={{ fontSize: 32, marginBottom: 12 }}>🔍</div>
          <div style={{ fontSize: 16, fontWeight: 600 }}>No domains or services match the current filters</div>
          <div style={{ fontSize: 13, marginTop: 6 }}>Try clearing your search query or enabling hidden tiers.</div>
        </div>
      ) : (
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fill, minmax(360px, 1fr))',
            gap: 16,
            alignItems: 'start',
          }}
        >
          {filteredGroups.map((group) => {
            const totalEntities =
              group.nodes.length +
              group.databases.length +
              group.topics.length +
              group.externalServices.length;

            return (
              <div
                key={group.domainKey}
                style={{
                  background: 'var(--vscode-sideBar-background, #252526)',
                  border: '1px solid var(--vscode-widget-border, #333)',
                  borderRadius: 8,
                  overflow: 'hidden',
                  display: 'flex',
                  flexDirection: 'column',
                  boxShadow: '0 4px 12px rgba(0, 0, 0, 0.2)',
                }}
              >
                {/* Domain Header */}
                <div
                  style={{
                    padding: '10px 14px',
                    background: 'var(--vscode-editorGroupHeader-tabsBackground, #2d2d2d)',
                    borderBottom: '1px solid var(--vscode-widget-border, #333)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <span style={{ fontSize: 16 }}>🏛️</span>
                    <span style={{ fontWeight: 600, fontSize: 14, color: 'var(--vscode-foreground, #ccc)' }}>
                      {group.displayName}
                    </span>
                  </div>
                  <span
                    style={{
                      fontSize: 11,
                      padding: '2px 8px',
                      borderRadius: 12,
                      background: 'rgba(255, 255, 255, 0.08)',
                      color: 'var(--vscode-descriptionForeground, #aaa)',
                    }}
                  >
                    {totalEntities} {totalEntities === 1 ? 'entity' : 'entities'}
                  </span>
                </div>

                {/* Domain Body */}
                <div style={{ padding: '12px 14px', display: 'flex', flexDirection: 'column', gap: 12 }}>
                  {/* Microservices & Components */}
                  {group.nodes.length > 0 && (
                    <div>
                      <div
                        style={{
                          fontSize: 11,
                          fontWeight: 600,
                          textTransform: 'uppercase',
                          letterSpacing: '0.5px',
                          color: 'var(--vscode-descriptionForeground, #888)',
                          marginBottom: 6,
                        }}
                      >
                        Services & Handlers ({group.nodes.length})
                      </div>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                        {group.nodes.map((node) => {
                          const isSelected = selectedNode?.id === node.id;
                          return (
                            <div
                              key={node.id}
                              onClick={() => selectNode(node)}
                              style={{
                                padding: '8px 10px',
                                borderRadius: 6,
                                background: isSelected
                                  ? 'var(--vscode-list-activeSelectionBackground, #04395e)'
                                  : 'rgba(255, 255, 255, 0.03)',
                                border: `1px solid ${
                                  isSelected
                                    ? 'var(--vscode-focusBorder, #007fd4)'
                                    : 'rgba(255, 255, 255, 0.06)'
                                }`,
                                cursor: 'pointer',
                                transition: 'all 0.15s ease',
                              }}
                            >
                              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: 6, overflow: 'hidden' }}>
                                  <span
                                    style={{
                                      fontSize: 10,
                                      fontWeight: 600,
                                      padding: '1px 6px',
                                      borderRadius: 4,
                                      background: node.bgColor,
                                      color: '#fff',
                                      flexShrink: 0,
                                    }}
                                  >
                                    {node.displayTag}
                                  </span>
                                  <span
                                    style={{
                                      fontWeight: 500,
                                      fontSize: 13,
                                      color: 'var(--vscode-foreground, #eee)',
                                      overflow: 'hidden',
                                      textOverflow: 'ellipsis',
                                      whiteSpace: 'nowrap',
                                    }}
                                    title={node.displayName}
                                  >
                                    {node.displayName}
                                  </span>
                                </div>

                                <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexShrink: 0 }}>
                                  {onFocusInFlow && (
                                    <button
                                      className="btn-icon"
                                      title="Focus in C2 Flow"
                                      onClick={(e) => {
                                        e.stopPropagation();
                                        onFocusInFlow(node.name);
                                      }}
                                      style={{
                                        background: 'transparent',
                                        border: 'none',
                                        cursor: 'pointer',
                                        padding: '2px 4px',
                                        fontSize: 12,
                                      }}
                                    >
                                      🌊
                                    </button>
                                  )}
                                  {onOpenFile && node.primaryFilePath && (
                                    <button
                                      className="btn-icon"
                                      title="Open Source File"
                                      onClick={(e) => {
                                        e.stopPropagation();
                                        onOpenFile(node.primaryFilePath!);
                                      }}
                                      style={{
                                        background: 'transparent',
                                        border: 'none',
                                        cursor: 'pointer',
                                        padding: '2px 4px',
                                        fontSize: 12,
                                      }}
                                    >
                                      📄
                                    </button>
                                  )}
                                </div>
                              </div>

                              {/* Metrics line */}
                              <div
                                style={{
                                  display: 'flex',
                                  alignItems: 'center',
                                  gap: 10,
                                  marginTop: 6,
                                  fontSize: 11,
                                  color: 'var(--vscode-descriptionForeground, #aaa)',
                                }}
                              >
                                {node.inboundCallsCount > 0 && <span>📥 {node.inboundCallsCount} in</span>}
                                {node.outboundCallsCount > 0 && <span>📤 {node.outboundCallsCount} out</span>}
                                {node.dbCount > 0 && <span>🗄️ {node.dbCount} db</span>}
                                {node.messagingCount > 0 && <span>📨 {node.messagingCount} msg</span>}
                                {node.tierLabel && (
                                  <span style={{ marginLeft: 'auto', opacity: 0.8 }}>
                                    {node.tierLabel}
                                  </span>
                                )}
                              </div>
                            </div>
                          );
                        })}
                      </div>
                    </div>
                  )}

                  {/* Databases */}
                  {group.databases.length > 0 && (
                    <div>
                      <div
                        style={{
                          fontSize: 11,
                          fontWeight: 600,
                          textTransform: 'uppercase',
                          letterSpacing: '0.5px',
                          color: '#c084fc',
                          marginBottom: 6,
                        }}
                      >
                        Databases ({group.databases.length})
                      </div>
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                        {group.databases.map((db) => (
                          <div
                            key={db.id}
                            onClick={() => selectNode(db)}
                            style={{
                              padding: '4px 8px',
                              borderRadius: 4,
                              background: 'rgba(192, 132, 252, 0.1)',
                              border: '1px solid rgba(192, 132, 252, 0.25)',
                              fontSize: 12,
                              color: '#e9d5ff',
                              cursor: 'pointer',
                              display: 'flex',
                              alignItems: 'center',
                              gap: 4,
                            }}
                          >
                            <span>🗄️</span>
                            <span>{db.displayName}</span>
                          </div>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* Messaging Topics */}
                  {group.topics.length > 0 && (
                    <div>
                      <div
                        style={{
                          fontSize: 11,
                          fontWeight: 600,
                          textTransform: 'uppercase',
                          letterSpacing: '0.5px',
                          color: '#fbbf24',
                          marginBottom: 6,
                        }}
                      >
                        Topics & Queues ({group.topics.length})
                      </div>
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                        {group.topics.map((t) => (
                          <div
                            key={t.id}
                            onClick={() => selectNode(t)}
                            style={{
                              padding: '4px 8px',
                              borderRadius: 4,
                              background: 'rgba(251, 191, 36, 0.1)',
                              border: '1px solid rgba(251, 191, 36, 0.25)',
                              fontSize: 12,
                              color: '#fef3c7',
                              cursor: 'pointer',
                              display: 'flex',
                              alignItems: 'center',
                              gap: 4,
                            }}
                          >
                            <span>📨</span>
                            <span>{t.displayName}</span>
                          </div>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* External Services */}
                  {group.externalServices.length > 0 && (
                    <div>
                      <div
                        style={{
                          fontSize: 11,
                          fontWeight: 600,
                          textTransform: 'uppercase',
                          letterSpacing: '0.5px',
                          color: '#f43f5e',
                          marginBottom: 6,
                        }}
                      >
                        External APIs ({group.externalServices.length})
                      </div>
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                        {group.externalServices.map((ext) => (
                          <div
                            key={ext.id}
                            onClick={() => selectNode(ext)}
                            style={{
                              padding: '4px 8px',
                              borderRadius: 4,
                              background: 'rgba(244, 63, 94, 0.1)',
                              border: '1px solid rgba(244, 63, 94, 0.25)',
                              fontSize: 12,
                              color: '#ffe4e6',
                              cursor: 'pointer',
                              display: 'flex',
                              alignItems: 'center',
                              gap: 4,
                            }}
                          >
                            <span>🌐</span>
                            <span>{ext.displayName}</span>
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
