import React, { memo } from 'react';
import { Handle, Position } from '@xyflow/react';
import { GraphNode } from '../../../../proto/types';

export interface ProjectCardData {
  graphNode: GraphNode;
  level: number;
  isCenter: boolean;
  inCount?: number;
  outCount?: number;
  isInboundExpanded?: boolean;
  isOutboundExpanded?: boolean;
  onToggleInbound?: (projectName: string) => void;
  onToggleOutbound?: (projectName: string) => void;
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
    isInboundExpanded = false,
    isOutboundExpanded = false,
    onToggleInbound,
    onToggleOutbound,
    onFocusProject,
    onOpenFile,
  } = nodeData;

  const framework = graphNode.properties?.framework;
  const isDatabase = graphNode.kind === 'Database';

  const handleTitleClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (!isCenter && !isDatabase && onFocusProject) {
      onFocusProject(graphNode.name);
    }
  };

  const handleToggleInboundClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (onToggleInbound && inCount > 0) {
      onToggleInbound(graphNode.name);
    }
  };

  const handleToggleOutboundClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (onToggleOutbound && outCount > 0) {
      onToggleOutbound(graphNode.name);
    }
  };

  const handleOpenClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (graphNode.filePath && onOpenFile) {
      onOpenFile(graphNode.filePath, graphNode.lineStart);
    }
  };

  let badgeColor = '#c084fc';
  let badgeLabel = 'Project';
  if (isDatabase) {
    badgeColor = '#34d399';
    badgeLabel = graphNode.properties?.db_type || 'Database';
  } else if (graphNode.kind === 'ExternalService') {
    badgeColor = '#fbbf24';
    badgeLabel = 'Service';
  }

  return (
    <div className={`project-card ${isCenter ? 'center-hero' : ''}`}>
      {/* Target handle on left for incoming edges */}
      <Handle
        type="target"
        position={Position.Left}
        className="flow-handle target-handle"
        isConnectable={false}
      />

      {/* Top row: badge + actions (code, focus) */}
      <div className="project-card-header">
        <span
          className="project-badge"
          style={{ backgroundColor: `${badgeColor}18`, color: badgeColor, borderColor: `${badgeColor}44` }}
        >
          {badgeLabel}
        </span>
        <div className="card-top-actions">
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

      {/* Action Links Row (Used by / Using) */}
      {!isDatabase && (
        <div className="card-links-row">
          {inCount > 0 ? (
            <button
              className={`action-link-btn used-by ${isInboundExpanded ? 'expanded' : ''}`}
              onClick={handleToggleInboundClick}
              title={isInboundExpanded ? `Close callers of ${graphNode.name}` : `Open callers of ${graphNode.name}`}
            >
              <span className="link-arrow">{isInboundExpanded ? '▾' : '◂'}</span>
              <span>Used by ({inCount})</span>
            </button>
          ) : (
            <span className="action-link-disabled">Used by (0)</span>
          )}

          {outCount > 0 ? (
            <button
              className={`action-link-btn using ${isOutboundExpanded ? 'expanded' : ''}`}
              onClick={handleToggleOutboundClick}
              title={isOutboundExpanded ? `Close dependencies of ${graphNode.name}` : `Open dependencies of ${graphNode.name}`}
            >
              <span>Using ({outCount})</span>
              <span className="link-arrow">{isOutboundExpanded ? '▾' : '▸'}</span>
            </button>
          ) : (
            <span className="action-link-disabled">Using (0)</span>
          )}
        </div>
      )}

      {/* Source handle on right for outgoing edges */}
      <Handle
        type="source"
        position={Position.Right}
        className="flow-handle source-handle"
        isConnectable={false}
      />
    </div>
  );
});
