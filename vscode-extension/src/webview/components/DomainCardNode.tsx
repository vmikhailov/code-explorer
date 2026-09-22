import React, { memo, useState } from 'react';
import { Handle, Position } from '@xyflow/react';
import { GraphNode } from '../../../../proto/types';

export interface DomainProjectInfo {
  id: string;
  name: string;
  kind?: string;
  filePath?: string;
  isLibrary?: boolean;
}

export interface DomainCardData extends Record<string, unknown> {
  domainId: string;
  name: string;
  displayName: string;
  zone: 'ingress' | 'service' | 'data' | 'topic' | 'external';
  primaryNode?: GraphNode;
  projects: DomainProjectInfo[];
  framework?: string;
  language?: string;
  inboundCallsCount: number;
  outboundCallsCount: number;
  dbCount: number;
  messagingCount: number;
  onFocusInFlow?: (projectName: string) => void;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
}

export const DomainCardNode = memo((props: any) => {
  const data = props.data as DomainCardData;
  const [showSubProjects, setShowSubProjects] = useState(false);

  if (!data) return null;

  const {
    name,
    displayName,
    zone,
    projects = [],
    framework,
    language,
    inboundCallsCount = 0,
    outboundCallsCount = 0,
    dbCount = 0,
    messagingCount = 0,
    onFocusInFlow,
    onOpenFile,
  } = data;

  const isDatabase = zone === 'data';
  const isTopic = zone === 'topic';
  const isIngress = zone === 'ingress';
  const isExternal = zone === 'external';

  // Zone accent colors and badges
  let zoneBadge = 'SERVICE';
  let zoneColor = '#a855f7';
  let zoneIcon = '⚙️';

  if (isIngress) {
    zoneBadge = 'INGRESS / APP';
    zoneColor = '#38bdf8';
    zoneIcon = '🌐';
  } else if (isDatabase) {
    zoneBadge = 'DATABASE';
    zoneColor = '#c084fc';
    zoneIcon = '🗄️';
  } else if (isTopic) {
    zoneBadge = 'EVENT QUEUE';
    zoneColor = '#fbbf24';
    zoneIcon = '📬';
  } else if (isExternal) {
    zoneBadge = 'EXTERNAL';
    zoneColor = '#34d399';
    zoneIcon = '☁️';
  }

  const primaryProject = projects[0];
  const primaryFilePath = data.primaryNode?.filePath || primaryProject?.filePath;
  const targetFlowProject = primaryProject?.name || name;

  const handleExploreFlow = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (onFocusInFlow) {
      onFocusInFlow(targetFlowProject);
    }
  };

  const handleOpenSource = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (primaryFilePath && onOpenFile) {
      onOpenFile(primaryFilePath, 1);
    }
  };

  return (
    <div
      className={`domain-card zone-${zone} ${showSubProjects ? 'has-expanded-list' : ''}`}
      style={{ borderLeftColor: zoneColor }}
    >
      {/* 4-Directional Handles for Force-Directed Graph Layout */}
      {/* Top Handles */}
      <Handle
        type="target"
        position={Position.Top}
        id="target-top"
        className="domain-handle"
        style={{ background: zoneColor }}
      />
      <Handle
        type="source"
        position={Position.Top}
        id="source-top"
        className="domain-handle"
        style={{ background: zoneColor }}
      />

      {/* Bottom Handles */}
      <Handle
        type="target"
        position={Position.Bottom}
        id="target-bottom"
        className="domain-handle"
        style={{ background: zoneColor }}
      />
      <Handle
        type="source"
        position={Position.Bottom}
        id="source-bottom"
        className="domain-handle"
        style={{ background: zoneColor }}
      />

      {/* Left Handles */}
      <Handle
        type="target"
        position={Position.Left}
        id="target"
        className="domain-handle"
        style={{ background: zoneColor }}
      />
      <Handle
        type="target"
        position={Position.Left}
        id="target-left"
        className="domain-handle"
        style={{ background: zoneColor }}
      />
      <Handle
        type="source"
        position={Position.Left}
        id="source-left"
        className="domain-handle"
        style={{ background: zoneColor }}
      />

      {/* Right Handles */}
      <Handle
        type="source"
        position={Position.Right}
        id="source"
        className="domain-handle"
        style={{ background: zoneColor }}
      />
      <Handle
        type="source"
        position={Position.Right}
        id="source-right"
        className="domain-handle"
        style={{ background: zoneColor }}
      />
      <Handle
        type="target"
        position={Position.Right}
        id="target-right"
        className="domain-handle"
        style={{ background: zoneColor }}
      />

      {/* Header */}
      <div className="domain-card-header">
        <div className="domain-card-title-row">
          <span className="domain-zone-icon" title={zoneBadge}>
            {zoneIcon}
          </span>
          <span className="domain-card-name" title={displayName || name}>
            {displayName || name}
          </span>
          {primaryFilePath && (
            <button
              className="domain-icon-action-btn"
              onClick={handleOpenSource}
              title={`Open code: ${primaryFilePath}`}
            >
              📄
            </button>
          )}
        </div>

        <div className="domain-badges-row">
          <span
            className="domain-zone-badge"
            style={{ color: zoneColor, borderColor: `${zoneColor}44`, background: `${zoneColor}14` }}
          >
            {zoneBadge}
          </span>
          {(framework || language) && (
            <span className="domain-tech-badge">
              {framework || language}
            </span>
          )}
        </div>
      </div>

      {/* Sub-projects list / count */}
      {projects.length > 1 && (
        <div className="domain-subprojects-container">
          <button
            className="domain-subprojects-toggle"
            onClick={(e) => {
              e.stopPropagation();
              setShowSubProjects((prev) => !prev);
            }}
            title={showSubProjects ? 'Hide sub-projects' : 'Show consolidated sub-projects'}
          >
            <span className="subprojects-count-icon">📦</span>
            <span>{projects.length} Projects</span>
            <span className="toggle-chevron">{showSubProjects ? '▴' : '▾'}</span>
          </button>

          {showSubProjects && (
            <div className="domain-subprojects-list">
              {projects.map((p) => (
                <div
                  key={p.id}
                  className="domain-subproject-item"
                  onClick={(e) => {
                    e.stopPropagation();
                    if (p.filePath && onOpenFile) onOpenFile(p.filePath, 1);
                  }}
                  title={p.filePath ? `Open ${p.filePath}` : p.name}
                >
                  <span className="subproject-bullet">•</span>
                  <span className="subproject-name">{p.name}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Communications Breakdown Metrics */}
      {!isDatabase && !isTopic && (
        <div className="domain-metrics-bar">
          <div className="domain-metric-item" title="Inbound service calls">
            <span className="metric-icon">📥</span>
            <span className="metric-val">{inboundCallsCount} in</span>
          </div>
          <div className="domain-metric-item" title="Outbound service calls">
            <span className="metric-icon">📤</span>
            <span className="metric-val">{outboundCallsCount} out</span>
          </div>
          {dbCount > 0 && (
            <div className="domain-metric-item" title="Databases used">
              <span className="metric-icon">🗄️</span>
              <span className="metric-val">{dbCount} db</span>
            </div>
          )}
          {messagingCount > 0 && (
            <div className="domain-metric-item" title="Message queues / event topics">
              <span className="metric-icon">📬</span>
              <span className="metric-val">{messagingCount} msg</span>
            </div>
          )}
        </div>
      )}

      {/* Card Actions Footer */}
      {!isDatabase && !isTopic && (
        <div className="domain-card-footer">
          <button
            className="domain-flow-drilldown-btn"
            onClick={handleExploreFlow}
            title={`Dive into detailed 3-column flow for ${targetFlowProject}`}
          >
            <span>Explore Flow</span>
            <span className="flow-arrow-icon">🔀</span>
          </button>
        </div>
      )}

    </div>
  );
});

DomainCardNode.displayName = 'DomainCardNode';
