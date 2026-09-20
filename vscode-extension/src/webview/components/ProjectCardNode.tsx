import React, { memo } from 'react';
import { Handle, Position, NodeProps } from '@xyflow/react';
import { GraphNode } from '../../../../proto/types';

export interface ProjectCardData {
  graphNode: GraphNode;
  column: 'left' | 'center' | 'right';
  isCenter: boolean;
  onSelectProject?: (name: string) => void;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
}

export const ProjectCardNode = memo((props: any) => {
  const nodeData = props.data as ProjectCardData;
  const { graphNode, column, isCenter, onSelectProject, onOpenFile } = nodeData;

  const framework = graphNode.properties?.framework;
  const isDatabase = graphNode.kind === 'Database';
  const role = graphNode.properties?.role;

  const handleClick = () => {
    if (!isCenter && !isDatabase && onSelectProject) {
      onSelectProject(graphNode.name);
    }
  };

  const handleOpenClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (graphNode.filePath && onOpenFile) {
      onOpenFile(graphNode.filePath, graphNode.lineStart);
    }
  };

  let badgeColor = '#c084fc'; // Project purple
  let badgeLabel = 'Project';
  if (isDatabase) {
    badgeColor = '#34d399'; // Emerald
    badgeLabel = graphNode.properties?.db_type || 'Database';
  } else if (graphNode.kind === 'ExternalService') {
    badgeColor = '#fbbf24'; // Amber
    badgeLabel = 'Service';
  }

  const roleLabel =
    column === 'left' ? 'Caller / Inbound' : column === 'right' ? 'Dependency' : 'Target';

  return (
    <div
      className={`project-card ${column} ${isCenter ? 'center-hero' : 'clickable-card'}`}
      onClick={handleClick}
      title={!isCenter && !isDatabase ? `Click to focus on ${graphNode.name}` : undefined}
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

      {/* Card Body / Details */}
      {framework && <div className="project-framework">{framework}</div>}

      {/* File Path / Jump to Code */}
      {graphNode.filePath && (
        <div className="project-card-footer">
          <button
            className="code-jump-btn"
            onClick={handleOpenClick}
            title={`Open ${graphNode.filePath}`}
          >
            📄 Open Code
          </button>
        </div>
      )}

      {!isCenter && !isDatabase && (
        <div className="click-hint">Click to inspect ➔</div>
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
