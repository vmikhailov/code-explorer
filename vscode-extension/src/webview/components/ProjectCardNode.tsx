import React, { memo, useState, useRef, useEffect, useLayoutEffect, useMemo } from 'react';
import { Handle, Position, useUpdateNodeInternals } from '@xyflow/react';
import { GraphNode } from '../../../../proto/types';
import type { EdgeCategory } from './ProjectFlowView';

export interface NodeCommsSummary {
  callsOut: Array<{ id: string; name: string; type?: string; filePath?: string }>;
  acceptsIn: Array<{ id: string; name: string; type?: string; filePath?: string }>;
  libsOut: Array<{ id: string; name: string }>;
  libsIn?: Array<{ id: string; name: string; filePath?: string }>;
  dbOut: Array<{ id: string; name: string; dbType?: string }>;
  messagesOut: Array<{ id: string; name: string }>;
  messagesIn: Array<{ id: string; name: string }>;
}

export interface ProjectCardData {
  graphNode: GraphNode;
  level: number;
  isCenter: boolean;
  inCount?: number;
  outCount?: number;
  isExpanded?: boolean;
  onToggleExpand?: (projectName: string, projectId?: string) => void;
  comms?: NodeCommsSummary;
  activeCategories?: string[];
  onToggleCategory?: (projectName: string, category: string, projectId?: string, currentlyActive?: boolean) => void;
  onFocusProject?: (projectName: string) => void;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
  visibleEdgeTypes?: Record<EdgeCategory, boolean>;
}

export const ProjectCardNode = memo((props: any) => {
  const nodeData = props.data as ProjectCardData;
  const {
    graphNode,
    level,
    isCenter,
    inCount = 0,
    outCount = 0,
    isExpanded,
    onToggleExpand,
    comms,
    activeCategories = [],
    onToggleCategory,
    onFocusProject,
    onOpenFile,
    visibleEdgeTypes,
  } = nodeData;

  const [showCommsPopover, setShowCommsPopover] = useState(false);
  const [localExpanded, setLocalExpanded] = useState(false);
  const cardRef = useRef<HTMLDivElement>(null);

  const isDatabase = graphNode.kind === 'Database';
  const isPackage = graphNode.kind === 'Package';

  const isCardExpanded = !isDatabase && !isPackage && (isExpanded ?? localExpanded);
  const activeCategoriesSet = new Set(activeCategories);

  const inboundCallsCount = comms?.acceptsIn.length ?? 0;
  const inboundLibsCount = comms?.libsIn?.length ?? 0;
  const inboundEventsCount = comms?.messagesIn.length ?? 0;

  const callsOutCount = comms?.callsOut.length ?? 0;
  const rawPkgCount = parseInt(graphNode.properties?.package_count || '0', 10) || 0;
  const libsOutCount = Math.max(comms?.libsOut.length ?? 0, rawPkgCount);
  const messagesOutCount = comms?.messagesOut.length ?? 0;
  const dbOutCount = comms?.dbOut.length ?? 0;

  const totalCommsCount =
    callsOutCount +
    inboundCallsCount +
    libsOutCount +
    inboundLibsCount +
    messagesOutCount +
    inboundEventsCount +
    dbOutCount;

  // Protocol Rows Definition matching:
  // (*) serves -- API -- calls (*)
  // (*) used by -- Libraries -- uses (*)
  // (*) receives -- Messages -- sends (*)
  // ( )         -- DB -- uses (*)
  const commRows = useMemo(() => [
    {
      id: 'row-api',
      categoryType: 'service_call' as EdgeCategory,
      protocol: 'API',
      icon: '⚡',
      left: {
        hasHandle: true,
        handleId: 'target-calls',
        category: 'acceptsIn',
        verb: 'serves',
        count: inboundCallsCount,
        color: '#38bdf8',
        active: activeCategoriesSet.has('acceptsIn'),
      },
      right: {
        hasHandle: true,
        handleId: 'source-calls',
        category: 'callsOut',
        verb: 'calls',
        count: callsOutCount,
        color: '#38bdf8',
        active: activeCategoriesSet.has('callsOut'),
      },
    },
    {
      id: 'row-libraries',
      categoryType: 'library' as EdgeCategory,
      protocol: 'Libraries',
      icon: '📚',
      left: {
        hasHandle: true,
        handleId: 'target-libs',
        category: 'libsIn',
        verb: 'used by',
        count: inboundLibsCount,
        color: '#34d399',
        active: activeCategoriesSet.has('libsIn'),
      },
      right: {
        hasHandle: true,
        handleId: 'source-libs',
        category: 'libsOut',
        verb: 'uses',
        count: libsOutCount,
        color: '#34d399',
        active: activeCategoriesSet.has('libsOut'),
      },
    },
    {
      id: 'row-messages',
      categoryType: 'messaging' as EdgeCategory,
      protocol: 'Messages',
      icon: '📨',
      left: {
        hasHandle: true,
        handleId: 'target-events',
        category: 'messagesIn',
        verb: 'receives',
        count: inboundEventsCount,
        color: '#fbbf24',
        active: activeCategoriesSet.has('messagesIn'),
      },
      right: {
        hasHandle: true,
        handleId: 'source-events',
        category: 'messagesOut',
        verb: 'sends',
        count: messagesOutCount,
        color: '#fbbf24',
        active: activeCategoriesSet.has('messagesOut'),
      },
    },
    {
      id: 'row-db',
      categoryType: 'database' as EdgeCategory,
      protocol: 'DB',
      icon: '🗄️',
      left: {
        hasHandle: false,
        handleId: 'target-none',
        category: 'none',
        verb: '',
        count: 0,
        color: '#64748b',
        active: false,
      },
      right: {
        hasHandle: true,
        handleId: 'source-db',
        category: 'dbOut',
        verb: 'uses',
        count: dbOutCount,
        color: '#c084fc',
        active: activeCategoriesSet.has('dbOut'),
      },
    },
  ], [
    inboundCallsCount,
    callsOutCount,
    inboundLibsCount,
    libsOutCount,
    inboundEventsCount,
    messagesOutCount,
    dbOutCount,
    activeCategoriesSet,
  ]);

  // Generic filtering: when a connection type is hidden in the legend, omit its row from all cards
  const visibleCommRows = useMemo(() => {
    return commRows.filter((row) => {
      if (!visibleEdgeTypes) return true;
      return visibleEdgeTypes[row.categoryType] !== false;
    });
  }, [commRows, visibleEdgeTypes]);

  const updateNodeInternals = useUpdateNodeInternals();
  useLayoutEffect(() => {
    if (!props.id) return;
    updateNodeInternals(props.id);
    const raf = requestAnimationFrame(() => {
      updateNodeInternals(props.id);
    });
    return () => cancelAnimationFrame(raf);
  }, [isCardExpanded, props.id, updateNodeInternals, visibleCommRows.length]);

  const handleOpenClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (graphNode.filePath && onOpenFile) {
      onOpenFile(graphNode.filePath, graphNode.lineStart);
    }
  };

  const rawFramework = graphNode.properties?.framework || graphNode.properties?.frameworks || '';
  const framework = Array.isArray(rawFramework) ? rawFramework.join(', ') : String(rawFramework || '');

  const handleTitleClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (!isCenter && !isDatabase && !isPackage && onFocusProject) {
      onFocusProject(graphNode.name);
    }
  };

  const projectType = (graphNode.properties?.project_type || graphNode.properties?.language || '').toLowerCase();
  const isLibrary =
    !isDatabase &&
    !isPackage &&
    graphNode.kind !== 'ExternalService' &&
    (graphNode.properties?.is_library === 'true' ||
      projectType === 'library' ||
      graphNode.properties?.layer === 'layer_foundation' ||
      graphNode.properties?.layerId === 'layer_foundation');

  let badgeColor = '#c084fc';
  let badgeLabel = 'Project';

  if (isDatabase) {
    badgeColor = '#34d399';
    badgeLabel = graphNode.properties?.db_type || 'Database';
  } else if (graphNode.kind === 'ExternalService') {
    badgeColor = '#fbbf24';
    badgeLabel = 'Service';
  } else if (isPackage) {
    badgeColor = '#10b981';
    const pkgType = (graphNode.properties?.package_type || 'Package').toLowerCase();
    badgeLabel = pkgType === 'npm' ? 'npm' : (pkgType === 'nuget' ? 'NuGet' : pkgType.toUpperCase());
  } else if (projectType === 'go' || projectType === 'golang') {
    badgeColor = '#00add8';
    badgeLabel = 'Go';
  } else if (projectType === 'csharp' || projectType === 'cs' || projectType === 'dotnet') {
    badgeColor = '#a855f7';
    badgeLabel = 'C#';
  } else if (projectType === 'typescript' || projectType === 'ts') {
    badgeColor = '#3178c6';
    badgeLabel = 'TypeScript';
  } else if (projectType === 'javascript' || projectType === 'js') {
    badgeColor = '#f59e0b';
    badgeLabel = 'JavaScript';
  } else if (projectType === 'python' || projectType === 'py') {
    badgeColor = '#38bdf8';
    badgeLabel = 'Python';
  } else if (projectType === 'java') {
    badgeColor = '#ea580c';
    badgeLabel = 'Java';
  } else if (projectType === 'sql') {
    badgeColor = '#06b6d4';
    badgeLabel = 'SQL';
  } else if (framework) {
    badgeLabel = framework;
  }

  return (
    <div
      ref={cardRef}
      className={`project-card ${isCenter ? 'center-hero' : ''} ${isCardExpanded ? 'is-expanded' : 'is-compact'} ${showCommsPopover ? 'has-open-popover' : ''}`}
    >
      {/* Top Part of the Box */}
      <div className="card-top-box">
        {/* In Collapsed mode, all connections jump to the center of this top part */}
        {!isCardExpanded && (
          <>
            <Handle
              id="target-default"
              type="target"
              position={Position.Left}
              className="flow-handle center-handle target-handle"
              isConnectable={false}
              style={{ top: '50%' }}
              title={inboundCallsCount + inboundEventsCount > 0 ? `Inbound (${inboundCallsCount + inboundEventsCount})` : 'Inbound'}
            />
            <Handle
              id="source-default"
              type="source"
              position={Position.Right}
              className="flow-handle center-handle source-handle"
              isConnectable={false}
              style={{ top: '50%' }}
              title={callsOutCount + dbOutCount + messagesOutCount + libsOutCount > 0 ? `Outbound (${callsOutCount + dbOutCount + messagesOutCount + libsOutCount})` : 'Outbound'}
            />
            {/* Hidden fallback handles so any typed edge routes to top-center when collapsed */}
            {['target-calls', 'target-libs', 'target-events'].map((id) => (
              <Handle
                key={`hidden-in-${id}`}
                id={id}
                type="target"
                position={Position.Left}
                className="flow-handle"
                isConnectable={false}
                style={{ top: '50%', opacity: 0, pointerEvents: 'none' }}
              />
            ))}
            {['source-calls', 'source-libs', 'source-events', 'source-db'].map((id) => (
              <Handle
                key={`hidden-out-${id}`}
                id={id}
                type="source"
                position={Position.Right}
                className="flow-handle"
                isConnectable={false}
                style={{ top: '50%', opacity: 0, pointerEvents: 'none' }}
              />
            ))}
          </>
        )}

        {/* Top row: badge + actions */}
      <div className="project-card-header">
        <div className="project-card-badges">
          <span
            className="project-badge"
            style={{ backgroundColor: `${badgeColor}18`, color: badgeColor, borderColor: `${badgeColor}44` }}
            title={badgeLabel}
          >
            {badgeLabel}
          </span>
          {isLibrary && (
            <span
              className="project-badge"
              style={{ backgroundColor: '#34d39918', color: '#34d399', borderColor: '#34d39944' }}
              title="Library"
            >
              Library
            </span>
          )}
          {framework && framework.toLowerCase() !== badgeLabel.toLowerCase() && (
            <span
              className="project-badge"
              style={{ backgroundColor: '#64748b18', color: '#94a3b8', borderColor: '#64748b33', fontSize: '10px' }}
              title={framework}
            >
              {framework}
            </span>
          )}
        </div>
        <div className="card-top-actions">
          {!isDatabase && !isPackage && (
            <button
              className="card-icon-link toggle-expand-btn"
              onClick={(e) => {
                e.stopPropagation();
                if (onToggleExpand) {
                  onToggleExpand(graphNode.name, graphNode.id);
                } else {
                  setLocalExpanded((v) => !v);
                }
              }}
              title={isCardExpanded ? 'Collapse protocols matrix' : 'Expand protocols matrix'}
            >
              {isCardExpanded ? '▴' : '▾'}
            </button>
          )}
          {totalCommsCount > 0 && !isDatabase && !isPackage && (
            <button
              className={`card-icon-link comms-btn ${showCommsPopover ? 'active' : ''}`}
              onClick={(e) => {
                e.stopPropagation();
                setShowCommsPopover((v) => !v);
              }}
              title={`View all communications (${totalCommsCount})`}
            >
              ⚡
            </button>
          )}
          {graphNode.filePath && (
            <button
              className="card-icon-link"
              onClick={handleOpenClick}
              title={`Open code: ${graphNode.filePath}`}
            >
              📄
            </button>
          )}
          {!isCenter && !isDatabase && !isPackage && (
            <button
              className="card-icon-link"
              onClick={handleTitleClick}
              title={`Center focus on ${graphNode.name}`}
            >
              🎯
            </button>
          )}
        </div>
      </div>

      {/* Project Title */}
      <div
        className={`project-card-title ${!isCenter && !isDatabase && !isPackage ? 'clickable-title' : ''}`}
        onClick={handleTitleClick}
        title={!isCenter && !isDatabase && !isPackage ? `Click to center focus on ${graphNode.name}` : (graphNode.displayName || graphNode.name)}
      >
        {graphNode.displayName || graphNode.name}
      </div>
    </div>

      {/* Floating Interactive Comms Popover */}
      {showCommsPopover && comms && (
        <div className="card-comms-popover" onClick={(e) => e.stopPropagation()}>
          <div className="comms-popover-header">
            <span className="comms-popover-title">Communications: {graphNode.name}</span>
            <button
              className="comms-popover-close"
              onClick={() => setShowCommsPopover(false)}
              title="Close"
            >
              ✕
            </button>
          </div>
          <div className="comms-popover-body">
            {comms.callsOut.length > 0 && (
              <div className="comms-section">
                <div className="comms-section-label calls">⚡ Calls Services ({comms.callsOut.length})</div>
                <div className="comms-chips-wrap">
                  {comms.callsOut.map((c) => (
                    <span
                      key={c.id}
                      className="comms-item-badge"
                      onClick={() => {
                        onFocusProject?.(c.name);
                      }}
                      title={c.name}
                    >
                      {c.name}
                    </span>
                  ))}
                </div>
              </div>
            )}
            {comms.acceptsIn.length > 0 && (
              <div className="comms-section">
                <div className="comms-section-label accepts">📥 Accepts Inbound ({comms.acceptsIn.length})</div>
                <div className="comms-chips-wrap">
                  {comms.acceptsIn.map((c) => (
                    <span
                      key={c.id}
                      className="comms-item-badge"
                      onClick={() => {
                        onFocusProject?.(c.name);
                      }}
                      title={c.name}
                    >
                      {c.name}
                    </span>
                  ))}
                </div>
              </div>
            )}
            {comms.dbOut.length > 0 && (
              <div className="comms-section">
                <div className="comms-section-label db">🗄️ Databases ({comms.dbOut.length})</div>
                <div className="comms-chips-wrap">
                  {comms.dbOut.map((d) => (
                    <span key={d.id} className="comms-item-badge db" title={`${d.name} (${d.dbType || 'Database'})`}>
                      {d.name}
                    </span>
                  ))}
                </div>
              </div>
            )}
            {comms.messagesOut.length > 0 && (
              <div className="comms-section">
                <div className="comms-section-label msg">📨 Sends Messages ({comms.messagesOut.length})</div>
                <div className="comms-chips-wrap">
                  {comms.messagesOut.map((m) => (
                    <span key={m.id} className="comms-item-badge msg" title={m.name}>
                      {m.name}
                    </span>
                  ))}
                </div>
              </div>
            )}
            {comms.messagesIn.length > 0 && (
              <div className="comms-section">
                <div className="comms-section-label msg">📬 Receives Messages ({comms.messagesIn.length})</div>
                <div className="comms-chips-wrap">
                  {comms.messagesIn.map((m) => (
                    <span key={m.id} className="comms-item-badge msg" title={m.name}>
                      {m.name}
                    </span>
                  ))}
                </div>
              </div>
            )}
            {comms.libsOut.length > 0 && (
              <div className="comms-section">
                <div className="comms-section-label lib">📚 Libraries Used ({comms.libsOut.length})</div>
                <div className="comms-chips-wrap">
                  {comms.libsOut.map((l) => (
                    <span
                      key={l.id}
                      className="comms-item-badge lib"
                      onClick={() => {
                        if (!l.id.startsWith('workspace:package:')) {
                          onFocusProject?.(l.name);
                        }
                      }}
                      title={l.name}
                    >
                      {l.name}
                    </span>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Expandable Bottom Extension (Symmetric Protocol Matrix) */}
      {isCardExpanded && visibleCommRows.length > 0 && (
        <div className="card-bottom-extension card-protocol-matrix">
          {visibleCommRows.map((row) => (
            <div key={row.id} className="protocol-row">
              {/* Left Handle attached directly to row at exact card edge */}
              {row.left.hasHandle && (
                <Handle
                  id={row.left.handleId}
                  type="target"
                  position={Position.Left}
                  className={`flow-handle typed-handle handle-${row.left.category} ${row.left.count === 0 ? 'is-empty' : ''}`}
                  isConnectable={false}
                  onClick={(e) => {
                    e.stopPropagation();
                    if (row.left.count > 0 && onToggleCategory) {
                      onToggleCategory(graphNode.name, row.left.category, graphNode.id, row.left.active);
                    }
                  }}
                  onMouseDown={(e) => {
                    e.stopPropagation();
                  }}
                  title={
                    row.left.count > 0
                      ? `${row.left.verb} (${row.left.count}) — Click to toggle category`
                      : ''
                  }
                  style={{
                    top: '50%',
                    left: 0,
                    backgroundColor: row.left.count === 0 ? '#334155' : row.left.active ? row.left.color : '#1c1c24',
                    border: row.left.count === 0 ? '1.5px solid #475569' : row.left.active ? '1.5px solid #ffffff' : `2px solid ${row.left.color}`,
                    boxShadow: row.left.count > 0 && row.left.active ? `0 0 6px ${row.left.color}, 0 0 2px #fff` : 'none',
                    cursor: row.left.count > 0 ? 'pointer' : 'default',
                    pointerEvents: row.left.count > 0 ? 'auto' : 'none',
                  }}
                />
              )}

              {/* Left Cell: (*) serves [count] */}
              <div
                className={`protocol-cell cell-left ${!row.left.hasHandle ? 'is-placeholder' : ''} ${row.left.count === 0 ? 'is-empty' : ''} ${row.left.active ? 'is-active' : ''}`}
                onClick={(e) => {
                  e.stopPropagation();
                  if (row.left.hasHandle && row.left.count > 0 && onToggleCategory) {
                    onToggleCategory(graphNode.name, row.left.category, graphNode.id, row.left.active);
                  }
                }}
                title={
                  row.left.hasHandle
                    ? `${row.left.verb} (${row.left.count}) — Click to toggle category`
                    : ''
                }
              >
                {row.left.hasHandle ? (
                  <>
                    <span className="protocol-verb">{row.left.verb}</span>
                    <span
                      className="protocol-count"
                      style={{
                        color: row.left.count === 0 ? '#64748b' : row.left.color,
                        backgroundColor: row.left.count === 0 ? 'transparent' : `${row.left.color}1c`,
                        borderColor: row.left.count === 0 ? '#334155' : `${row.left.color}44`,
                      }}
                    >
                      {row.left.count}
                    </span>
                  </>
                ) : (
                  <span className="protocol-placeholder">( )</span>
                )}
              </div>

              {/* Center Protocol Badge without minuses */}
              <div className="protocol-center">
                <span className="protocol-tag">
                  <span className="protocol-icon">{row.icon}</span>
                  <span className="protocol-name">{row.protocol}</span>
                </span>
              </div>

              {/* Right Cell: [count] calls (*) */}
              <div
                className={`protocol-cell cell-right ${!row.right.hasHandle ? 'is-placeholder' : ''} ${row.right.count === 0 ? 'is-empty' : ''} ${row.right.active ? 'is-active' : ''}`}
                onClick={(e) => {
                  e.stopPropagation();
                  if (row.right.hasHandle && row.right.count > 0 && onToggleCategory) {
                    onToggleCategory(graphNode.name, row.right.category, graphNode.id, row.right.active);
                  }
                }}
                title={
                  row.right.hasHandle
                    ? `${row.right.verb} (${row.right.count}) — Click to toggle category`
                    : ''
                }
              >
                {row.right.hasHandle && (
                  <>
                    <span
                      className="protocol-count"
                      style={{
                        color: row.right.count === 0 ? '#64748b' : row.right.color,
                        backgroundColor: row.right.count === 0 ? 'transparent' : `${row.right.color}1c`,
                        borderColor: row.right.count === 0 ? '#334155' : `${row.right.color}44`,
                      }}
                    >
                      {row.right.count}
                    </span>
                    <span className="protocol-verb">{row.right.verb}</span>
                  </>
                )}
              </div>

              {/* Right Handle attached directly to row at exact card edge */}
              {row.right.hasHandle && (
                <Handle
                  id={row.right.handleId}
                  type="source"
                  position={Position.Right}
                  className={`flow-handle typed-handle handle-${row.right.category} ${row.right.count === 0 ? 'is-empty' : ''}`}
                  isConnectable={false}
                  onClick={(e) => {
                    e.stopPropagation();
                    if (row.right.count > 0 && onToggleCategory) {
                      onToggleCategory(graphNode.name, row.right.category, graphNode.id, row.right.active);
                    }
                  }}
                  onMouseDown={(e) => {
                    e.stopPropagation();
                  }}
                  title={
                    row.right.count > 0
                      ? `${row.right.verb} (${row.right.count}) — Click to toggle category`
                      : ''
                  }
                  style={{
                    top: '50%',
                    right: 0,
                    backgroundColor: row.right.count === 0 ? '#334155' : row.right.active ? row.right.color : '#1c1c24',
                    border: row.right.count === 0 ? '1.5px solid #475569' : row.right.active ? '1.5px solid #ffffff' : `2px solid ${row.right.color}`,
                    boxShadow: row.right.count > 0 && row.right.active ? `0 0 6px ${row.right.color}, 0 0 2px #fff` : 'none',
                    cursor: row.right.count > 0 ? 'pointer' : 'default',
                    pointerEvents: row.right.count > 0 ? 'auto' : 'none',
                  }}
                />
              )}
            </div>
          ))}
        </div>
      )}

      {/* Bottom Side Hover Expansion Trigger (only for actual services) */}
      {!isDatabase && !isPackage && (
        <div
          className={`card-bottom-trigger ${isCardExpanded ? 'is-expanded' : ''}`}
          onClick={(e) => {
            e.stopPropagation();
            if (onToggleExpand) {
              onToggleExpand(graphNode.name, graphNode.id);
            } else {
              setLocalExpanded((v) => !v);
            }
          }}
          title={isCardExpanded ? 'Click to collapse connection points' : 'Click to expand all connection points'}
        >
          <span className="trigger-arrow">{isCardExpanded ? '▴' : '▾'}</span>
        </div>
      )}
    </div>
  );
});
