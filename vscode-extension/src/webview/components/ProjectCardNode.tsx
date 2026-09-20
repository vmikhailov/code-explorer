import React, { memo } from 'react';
import { Handle, Position } from '@xyflow/react';
import { GraphNode } from '../../../../proto/types';

export interface ProjectCardData {
  graphNode: GraphNode;
  column: 'left' | 'center' | 'right';
  isCenter: boolean;
  inCount?: number;
  outCount?: number;
  onNavigate?: (projectName: string, direction: 'all' | 'using' | 'used_by') => void;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
}

export const ProjectCardNode = memo((props: any) => {
  const nodeData = props.data as ProjectCardData;
  const {
    graphNode,
    column,
    isCenter,
    inCount = 0,
    outCount = 0,
    onNavigate,
    onOpenFile,
  } = nodeData;

  const framework = graphNode.properties?.framework;
  const isDatabase = graphNode.kind === 'Database';

  const handleCardClick = () => {
    if (!isCenter && !isDatabase && onNavigate) {
      onNavigate(graphNode.name, column === 'left' ? 'used_by' : column === 'right' ? 'using' : 'all');
    }
  };

  const handleUsedByClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (onNavigate) {
      onNavigate(graphNode.name, 'used_by');
    }
  };

  const handleUsingClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (onNavigate) {
      onNavigate(graphNode.name, 'using');
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

  const roleLabel =
    column === 'left' ? 'Inbound Caller' : column === 'right' ? 'Dependency' : 'Target';

  return (
    <div
      className={`project-card ${column} ${isCenter ? 'center-hero' : 'clickable-card'}`}
      onClick={handleCardClick}
    >
      {/* Inbound Handle (Left) */}
      {(column === 'center' || column === 'right') && (
        <Handle
          type="target"
          position={Position.Left}
          className="flow-handle target-handle"
        />
      )}

      {/* Card Header */}
      <div className="project-card-header">
        <span
          className="project-badge"
          style={{ backgroundColor: `${badgeColor}22`, color: badgeColor, borderColor: `${badgeColor}55` }}
        >
          {badgeLabel}
        </span>
        <span className="column-role">{roleLabel}</span>
      </div>

      {/* Card Title */}
      <div className="project-card-title">{graphNode.name}</div>

      {/* Card Details */}
      {framework && <div className="project-framework">{framework}</div>}

      {/* Navigation Buttons (Used by / Using) */}
      {!isDatabase && (
        <div className="project-card-actions">
          <button
            className={`flow-nav-btn used-by-btn ${column === 'left' ? 'primary' : ''}`}
            onClick={handleUsedByClick}
            title={`Explore callers: who uses ${graphNode.name}`}
          >
            ← Used by {inCount > 0 ? `(${inCount})` : ''}
          </button>
          <button
            className={`flow-nav-btn using-btn ${column === 'right' ? 'primary' : ''}`}
            onClick={handleUsingClick}
            title={`Explore dependencies: what ${graphNode.name} is using`}
          >
            Using → {outCount > 0 ? `(${outCount})` : ''}
          </button>
        </div>
      )}

      {/* Footer with Open Code */}
      {graphNode.filePath && (
        <div className="project-card-footer">
          <button
            className="code-jump-btn"
            onClick={handleOpenClick}
            title={`Open ${graphNode.filePath}`}
          >
            📄 Code
          </button>
        </div>
      )}

      {/* Outbound Handle (Right) */}
      {(column === 'left' || column === 'center') && (
        <Handle
          type="source"
          position={Position.Right}
          className="flow-handle source-handle"
        />
      )}
    </div>
  );
});
