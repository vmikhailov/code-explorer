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
  onToggleInbound?: (projectName: string, projectId?: string) => void;
  onToggleOutbound?: (projectName: string, projectId?: string) => void;
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
      onToggleInbound(graphNode.name, graphNode.id);
    }
  };

  const handleToggleOutboundClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (onToggleOutbound && outCount > 0) {
      onToggleOutbound(graphNode.name, graphNode.id);
    }
  };

  const handleOpenClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (graphNode.filePath && onOpenFile) {
      onOpenFile(graphNode.filePath, graphNode.lineStart);
    }
  };

  const projectType = (graphNode.properties?.project_type || graphNode.properties?.language || '').toLowerCase();

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
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <span
            className="project-badge"
            style={{ backgroundColor: `${badgeColor}18`, color: badgeColor, borderColor: `${badgeColor}44` }}
          >
            {badgeLabel}
          </span>
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
      <div className="card-links-row">
        {inCount > 0 ? (
          <button
            className={`action-link-btn used-by ${isInboundExpanded ? 'expanded' : ''}`}
            onClick={handleToggleInboundClick}
            title={isInboundExpanded ? `Collapse incoming callers of ${graphNode.name}` : `Expand incoming callers of ${graphNode.name}`}
          >
            <span className="link-arrow">{isInboundExpanded ? '▾' : '◂'}</span>
            <span>Used by ({inCount})</span>
          </button>
        ) : (
          <button
            className="action-link-btn disabled"
            disabled
            title={`No incoming callers for ${graphNode.name}`}
          >
            No callers
          </button>
        )}

        {outCount > 0 && !isDatabase ? (
          <button
            className={`action-link-btn using ${isOutboundExpanded ? 'expanded' : ''}`}
            onClick={handleToggleOutboundClick}
            title={isOutboundExpanded ? `Collapse dependencies of ${graphNode.name}` : `Expand dependencies of ${graphNode.name}`}
          >
            <span>Using ({outCount})</span>
            <span className="link-arrow">{isOutboundExpanded ? '▾' : '▸'}</span>
          </button>
        ) : (
          <button
            className="action-link-btn disabled"
            disabled
            title={`No dependencies for ${graphNode.name}`}
          >
            No dependencies
          </button>
        )}
      </div>

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
