import React, { memo, useState, useRef, useEffect } from 'react';
import { Handle, Position } from '@xyflow/react';
import { GraphNode } from '../../../../proto/types';

export interface NodeCommsSummary {
  callsOut: Array<{ id: string; name: string; type?: string; filePath?: string }>;
  acceptsIn: Array<{ id: string; name: string; type?: string; filePath?: string }>;
  dbOut: Array<{ id: string; name: string; dbType?: string }>;
  messagesOut: Array<{ id: string; name: string }>;
  messagesIn: Array<{ id: string; name: string }>;
  libsOut: Array<{ id: string; name: string }>;
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
  onToggleCategory?: (projectName: string, category: string, projectId?: string) => void;
  onFocusProject?: (projectName: string) => void;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
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
  } = nodeData;

  const [showCommsPopover, setShowCommsPopover] = useState(false);
  const [localExpanded, setLocalExpanded] = useState(false);
  const cardRef = useRef<HTMLDivElement>(null);

  const isCardExpanded = isExpanded ?? localExpanded;
  const activeCategoriesSet = new Set(activeCategories);

  const inboundCallsCount = (comms?.acceptsIn.length ?? 0) || inCount;
  const inboundEventsCount = comms?.messagesIn.length ?? 0;
  const callsOutCount = comms?.callsOut.length ?? 0;
  const dbOutCount = comms?.dbOut.length ?? 0;
  const messagesOutCount = comms?.messagesOut.length ?? 0;
  const libsOutCount = comms?.libsOut.length ?? 0;

  const totalCommsCount = callsOutCount + inboundCallsCount + dbOutCount + libsOutCount + messagesOutCount + inboundEventsCount;

  // Left ports (Inbound)
  const leftPorts: Array<{ id: string; category: string; color: string; label: string; count: number; active: boolean }> = [];
  if (inboundCallsCount > 0) {
    leftPorts.push({
      id: 'target-calls',
      category: 'acceptsIn',
      color: '#38bdf8',
      label: 'Accepts Calls',
      count: inboundCallsCount,
      active: activeCategoriesSet.has('acceptsIn'),
    });
  }
  if (inboundEventsCount > 0) {
    leftPorts.push({
      id: 'target-events',
      category: 'messagesIn',
      color: '#fbbf24',
      label: 'Receives Events',
      count: inboundEventsCount,
      active: activeCategoriesSet.has('messagesIn'),
    });
  }

  // Right ports (Outbound)
  const rightPorts: Array<{ id: string; category: string; color: string; label: string; count: number; active: boolean }> = [];
  if (callsOutCount > 0) {
    rightPorts.push({
      id: 'source-calls',
      category: 'callsOut',
      color: '#38bdf8',
      label: 'Calls Services',
      count: callsOutCount,
      active: activeCategoriesSet.has('callsOut'),
    });
  }
  if (dbOutCount > 0) {
    rightPorts.push({
      id: 'source-db',
      category: 'dbOut',
      color: '#c084fc',
      label: 'Databases',
      count: dbOutCount,
      active: activeCategoriesSet.has('dbOut'),
    });
  }
  if (messagesOutCount > 0) {
    rightPorts.push({
      id: 'source-events',
      category: 'messagesOut',
      color: '#fbbf24',
      label: 'Publishes Events',
      count: messagesOutCount,
      active: activeCategoriesSet.has('messagesOut'),
    });
  }
  if (libsOutCount > 0) {
    rightPorts.push({
      id: 'source-libs',
      category: 'libsOut',
      color: '#34d399',
      label: 'Libraries',
      count: libsOutCount,
      active: activeCategoriesSet.has('libsOut'),
    });
  }

  const handleOpenClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (graphNode.filePath && onOpenFile) {
      onOpenFile(graphNode.filePath, graphNode.lineStart);
    }
  };

  const rawFramework = graphNode.properties?.framework || graphNode.properties?.frameworks || '';
  const framework = Array.isArray(rawFramework) ? rawFramework.join(', ') : String(rawFramework || '');
  const isDatabase = graphNode.kind === 'Database';

  const handleTitleClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (!isCenter && !isDatabase && onFocusProject) {
      onFocusProject(graphNode.name);
    }
  };

  const projectType = (graphNode.properties?.project_type || graphNode.properties?.language || '').toLowerCase();
  const isLibrary =
    !isDatabase &&
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

  const maxPorts = Math.max(leftPorts.length, rightPorts.length);
  const cardMinHeight = isCardExpanded && maxPorts >= 3 ? (maxPorts >= 4 ? 96 : 84) : undefined;

  return (
    <div
      ref={cardRef}
      className={`project-card ${isCenter ? 'center-hero' : ''} ${isCardExpanded ? 'is-expanded' : 'is-compact'} ${showCommsPopover ? 'has-open-popover' : ''}`}
      style={{ minHeight: cardMinHeight }}
    >
      {/* Left Inbound Handles */}
      {!isCardExpanded || leftPorts.length === 0 ? (
        <Handle
          id="target-default"
          type="target"
          position={Position.Left}
          className="flow-handle center-handle target-handle"
          isConnectable={false}
          style={{ top: '50%' }}
          title={
            leftPorts.length > 0 || rightPorts.length > 0
              ? `Inbound (${inboundCallsCount + inboundEventsCount}) — Click to expand typed ports`
              : inboundCallsCount + inboundEventsCount > 0
              ? `Inbound (${inboundCallsCount + inboundEventsCount})`
              : 'Inbound'
          }
          onClick={(e) => {
            e.stopPropagation();
            if (leftPorts.length > 0 || rightPorts.length > 0) {
              if (onToggleExpand) onToggleExpand(graphNode.name, graphNode.id);
              else setLocalExpanded(true);
            }
          }}
        />
      ) : (
        leftPorts.map((p, idx) => {
          const topPct = leftPorts.length === 1 ? 50 : Math.round(((idx + 1) / (leftPorts.length + 1)) * 100);
          return (
            <Handle
              key={p.id}
              id={p.id}
              type="target"
              position={Position.Left}
              className={`flow-handle typed-handle handle-${p.category} ${p.active ? 'is-active' : 'is-inactive'}`}
              style={{
                top: `${topPct}%`,
                backgroundColor: p.active ? p.color : '#1c1c24',
                border: p.active ? `1.5px solid #ffffff` : `2px solid ${p.color}`,
                boxShadow: p.active ? `0 0 6px ${p.color}, 0 0 2px #fff` : 'none',
              }}
              title={`${p.label} (${p.count}) — ${p.active ? 'Active (click to hide)' : 'Hidden (click to show)'}`}
              onClick={(e) => {
                e.stopPropagation();
                onToggleCategory?.(graphNode.name, p.category, graphNode.id);
              }}
              isConnectable={false}
            />
          );
        })
      )}
      {/* Hidden fallback handles to preserve React Flow edge bindings */}
      {isCardExpanded && leftPorts.length > 0 && (
        <Handle
          id="target-default"
          type="target"
          position={Position.Left}
          className="flow-handle"
          isConnectable={false}
          style={{ top: '50%', opacity: 0, pointerEvents: 'none' }}
        />
      )}
      {!isCardExpanded && leftPorts.map((p) => (
        <Handle
          key={`hidden-in-${p.id}`}
          id={p.id}
          type="target"
          position={Position.Left}
          className="flow-handle"
          isConnectable={false}
          style={{ top: '50%', opacity: 0, pointerEvents: 'none' }}
        />
      ))}

      {/* Top row: badge + actions */}
      <div className="project-card-header">
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <span
            className="project-badge"
            style={{ backgroundColor: `${badgeColor}18`, color: badgeColor, borderColor: `${badgeColor}44` }}
          >
            {badgeLabel}
          </span>
          {isLibrary && (
            <span
              className="project-badge"
              style={{ backgroundColor: '#34d39918', color: '#34d399', borderColor: '#34d39944' }}
            >
              Library
            </span>
          )}
          {framework && framework.toLowerCase() !== badgeLabel.toLowerCase() && (
            <span
              className="project-badge"
              style={{ backgroundColor: '#64748b18', color: '#94a3b8', borderColor: '#64748b33', fontSize: '10px' }}
            >
              {framework}
            </span>
          )}
        </div>
        <div className="card-top-actions">
          {totalCommsCount > 0 && (
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
          {!isCenter && !isDatabase && (
            <button
              className="card-icon-link"
              onClick={handleTitleClick}
              title={`Center focus on ${graphNode.name}`}
            >
              🎯
            </button>
          )}
          {(leftPorts.length > 0 || rightPorts.length > 0) && (
            <button
              className={`card-icon-link expand-toggle-btn ${isCardExpanded ? 'is-expanded' : ''}`}
              onClick={(e) => {
                e.stopPropagation();
                if (onToggleExpand) {
                  onToggleExpand(graphNode.name, graphNode.id);
                } else {
                  setLocalExpanded((v) => !v);
                }
              }}
              title={isCardExpanded ? 'Collapse to single center point' : 'Expand typed connector dots'}
            >
              {isCardExpanded ? '▴' : '▾'}
            </button>
          )}
        </div>
      </div>

      {/* Project Title */}
      <div
        className={`project-card-title ${!isCenter && !isDatabase ? 'clickable-title' : ''}`}
        onClick={handleTitleClick}
        title={!isCenter && !isDatabase ? `Click to center focus on ${graphNode.name}` : graphNode.name}
      >
        {graphNode.name}
      </div>

      {/* Framework if available */}
      {framework && <div className="project-framework">{framework}</div>}

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
                        setShowCommsPopover(false);
                      }}
                      title={`Focus on ${c.name}`}
                    >
                      {c.name} {c.type ? `[${c.type}]` : ''}
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
                    <span key={d.id} className="comms-item-badge">
                      {d.name} {d.dbType ? `[${d.dbType}]` : ''}
                    </span>
                  ))}
                </div>
              </div>
            )}

            {comms.messagesOut.length + comms.messagesIn.length > 0 && (
              <div className="comms-section">
                <div className="comms-section-label msg">📨 Messaging & Events</div>
                <div className="comms-chips-wrap">
                  {comms.messagesOut.map((m) => (
                    <span key={`out-${m.id}`} className="comms-item-badge">
                      publishes: {m.name}
                    </span>
                  ))}
                  {comms.messagesIn.map((m) => (
                    <span key={`in-${m.id}`} className="comms-item-badge">
                      subscribes: {m.name}
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
                      className="comms-item-badge"
                      onClick={() => {
                        onFocusProject?.(l.name);
                        setShowCommsPopover(false);
                      }}
                      title={`Focus on ${l.name}`}
                    >
                      {l.name}
                    </span>
                  ))}
                </div>
              </div>
            )}

            {comms.acceptsIn.length > 0 && (
              <div className="comms-section">
                <div className="comms-section-label accepts">📥 Accepts Calls / Used By ({comms.acceptsIn.length})</div>
                <div className="comms-chips-wrap">
                  {comms.acceptsIn.map((a) => (
                    <span
                      key={a.id}
                      className="comms-item-badge"
                      onClick={() => {
                        onFocusProject?.(a.name);
                        setShowCommsPopover(false);
                      }}
                      title={`Focus on ${a.name}`}
                    >
                      {a.name}
                    </span>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Right Outbound Handles */}
      {!isCardExpanded || rightPorts.length === 0 ? (
        <Handle
          id="source-default"
          type="source"
          position={Position.Right}
          className="flow-handle center-handle source-handle"
          isConnectable={false}
          style={{ top: '50%' }}
          title={
            leftPorts.length > 0 || rightPorts.length > 0
              ? `Outbound (${callsOutCount + dbOutCount + messagesOutCount + libsOutCount}) — Click to expand typed ports`
              : callsOutCount + dbOutCount + messagesOutCount + libsOutCount > 0
              ? `Outbound (${callsOutCount + dbOutCount + messagesOutCount + libsOutCount})`
              : 'Outbound'
          }
          onClick={(e) => {
            e.stopPropagation();
            if (leftPorts.length > 0 || rightPorts.length > 0) {
              if (onToggleExpand) onToggleExpand(graphNode.name, graphNode.id);
              else setLocalExpanded(true);
            }
          }}
        />
      ) : (
        rightPorts.map((p, idx) => {
          const topPct = rightPorts.length === 1 ? 50 : Math.round(((idx + 1) / (rightPorts.length + 1)) * 100);
          return (
            <Handle
              key={p.id}
              id={p.id}
              type="source"
              position={Position.Right}
              className={`flow-handle typed-handle handle-${p.category} ${p.active ? 'is-active' : 'is-inactive'}`}
              style={{
                top: `${topPct}%`,
                backgroundColor: p.active ? p.color : '#1c1c24',
                border: p.active ? `1.5px solid #ffffff` : `2px solid ${p.color}`,
                boxShadow: p.active ? `0 0 6px ${p.color}, 0 0 2px #fff` : 'none',
              }}
              title={`${p.label} (${p.count}) — ${p.active ? 'Active (click to hide)' : 'Hidden (click to show)'}`}
              onClick={(e) => {
                e.stopPropagation();
                onToggleCategory?.(graphNode.name, p.category, graphNode.id);
              }}
              isConnectable={false}
            />
          );
        })
      )}
      {/* Hidden fallback handles */}
      {isCardExpanded && rightPorts.length > 0 && (
        <Handle
          id="source-default"
          type="source"
          position={Position.Right}
          className="flow-handle"
          isConnectable={false}
          style={{ top: '50%', opacity: 0, pointerEvents: 'none' }}
        />
      )}
      {!isCardExpanded && rightPorts.map((p) => (
        <Handle
          key={`hidden-out-${p.id}`}
          id={p.id}
          type="source"
          position={Position.Right}
          className="flow-handle"
          isConnectable={false}
          style={{ top: '50%', opacity: 0, pointerEvents: 'none' }}
        />
      ))}
    </div>
  );
});
