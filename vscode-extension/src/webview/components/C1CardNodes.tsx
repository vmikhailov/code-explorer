import React, { memo } from 'react';
import { Handle, Position } from '@xyflow/react';

// ============================================================================
// C1 Ingress Endpoint Card
// ============================================================================
export interface C1EndpointData extends Record<string, unknown> {
  id: string;
  name: string;
  method?: string;
  route?: string;
  filePath?: string;
  lineStart?: number;
  targetProject?: string;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
  onSelectNode?: () => void;
  isSelected?: boolean;
}

export const C1EndpointCardNode = memo((props: any) => {
  const data = props.data as C1EndpointData;
  if (!data) return null;

  const rawMethod = (data.method || (data.name.startsWith('GET:') ? 'GET' : data.name.startsWith('POST:') ? 'POST' : 'HTTP')).toUpperCase();
  const route = data.route || data.name.replace(/^[A-Z]+:/, '') || data.name;

  const getMethodBadgeClass = (m: string) => {
    switch (m) {
      case 'GET':
        return 'c1-method-get';
      case 'POST':
        return 'c1-method-post';
      case 'PUT':
      case 'PATCH':
        return 'c1-method-put';
      case 'DELETE':
        return 'c1-method-delete';
      default:
        return 'c1-method-generic';
    }
  };

  return (
    <div
      className={`c1-node-card c1-endpoint-card ${data.isSelected ? 'is-selected' : ''}`}
      onClick={(e) => {
        e.stopPropagation();
        data.onSelectNode?.();
      }}
    >
      <div className="c1-card-header">
        <span className={`c1-method-badge ${getMethodBadgeClass(rawMethod)}`}>{rawMethod}</span>
        <span className="c1-badge-sub">INGRESS</span>
      </div>
      <div className="c1-endpoint-route" title={route}>
        {route}
      </div>
      {data.targetProject && (
        <div className="c1-endpoint-target" title={`Routes to ${data.targetProject}`}>
          ➔ {data.targetProject}
        </div>
      )}
      {data.filePath && data.onOpenFile && (
        <button
          className="c1-source-btn"
          onClick={(e) => {
            e.stopPropagation();
            data.onOpenFile?.(data.filePath!, data.lineStart);
          }}
          title="Open source file"
        >
          📄 Source
        </button>
      )}
      <Handle type="source" position={Position.Right} id="egress" className="c1-handle c1-handle-right" />
    </div>
  );
});

// ============================================================================
// C1 System Project Card (Inside Core Boundary)
// ============================================================================
export interface C1ProjectData extends Record<string, unknown> {
  id: string;
  name: string;
  framework?: string;
  projectType?: string;
  filePath?: string;
  inboundCalls: number;
  outboundCalls: number;
  packageCount?: number;
  onDrillDown?: (projectName: string) => void;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
  onSelectNode?: () => void;
  isSelected?: boolean;
}

export const C1ProjectCardNode = memo((props: any) => {
  const data = props.data as C1ProjectData;
  if (!data) return null;

  return (
    <div
      className={`c1-node-card c1-project-card ${data.isSelected ? 'is-selected' : ''}`}
      onClick={(e) => {
        e.stopPropagation();
        data.onSelectNode?.();
      }}
    >
      <Handle type="target" position={Position.Left} id="ingress" className="c1-handle c1-handle-left" />

      <div className="c1-card-header">
        <div className="c1-proj-title-wrap">
          <span className="c1-proj-icon">📦</span>
          <span className="c1-proj-name" title={data.name}>
            {data.name}
          </span>
        </div>
      </div>

      <div className="c1-badges-row">
        {data.framework && <span className="c1-badge c1-framework-badge">{data.framework}</span>}
        {data.projectType && <span className="c1-badge c1-type-badge">{data.projectType}</span>}
        {data.packageCount !== undefined && data.packageCount > 0 && (
          <span className="c1-badge c1-pkg-badge" title={`${data.packageCount} dependencies`}>
            {data.packageCount} pkgs
          </span>
        )}
      </div>

      <div className="c1-stats-row">
        <span className="c1-stat" title="Inbound connections">
          <span className="c1-stat-icon">⬇</span> {data.inboundCalls} in
        </span>
        <span className="c1-stat" title="Outbound connections">
          <span className="c1-stat-icon">⬆</span> {data.outboundCalls} out
        </span>
      </div>

      <div className="c1-actions-row">
        {data.onDrillDown && (
          <button
            className="c1-drilldown-btn"
            onClick={(e) => {
              e.stopPropagation();
              data.onDrillDown?.(data.name);
            }}
            title={`Drill down into ${data.name} (C2 Project Flow)`}
          >
            <span>Drill Down to C2 Flow</span>
            <span className="c1-arrow">➔</span>
          </button>
        )}
        {data.filePath && data.onOpenFile && (
          <button
            className="c1-icon-btn"
            onClick={(e) => {
              e.stopPropagation();
              data.onOpenFile?.(data.filePath!);
            }}
            title="Open project file"
          >
            📄
          </button>
        )}
      </div>

      <Handle type="source" position={Position.Right} id="egress" className="c1-handle c1-handle-right" />
    </div>
  );
});

// ============================================================================
// C1 Egress Card (Database, External Service, Message Queue)
// ============================================================================
export interface C1EgressData extends Record<string, unknown> {
  id: string;
  name: string;
  category: 'database' | 'external' | 'messaging';
  subType?: string;
  details?: string;
  targetCount?: number;
  filePath?: string;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
  onSelectNode?: () => void;
  isSelected?: boolean;
}

export const C1EgressCardNode = memo((props: any) => {
  const data = props.data as C1EgressData;
  if (!data) return null;

  const isDb = data.category === 'database';
  const isExt = data.category === 'external';

  const icon = isDb ? '🗄️' : isExt ? '☁️' : '📨';
  const badgeLabel = isDb ? 'DATABASE' : isExt ? 'EXTERNAL SERVICE' : 'MESSAGE TOPIC';
  const badgeClass = isDb ? 'c1-badge-db' : isExt ? 'c1-badge-ext' : 'c1-badge-msg';

  return (
    <div
      className={`c1-node-card c1-egress-card ${badgeClass} ${data.isSelected ? 'is-selected' : ''}`}
      onClick={(e) => {
        e.stopPropagation();
        data.onSelectNode?.();
      }}
    >
      <Handle type="target" position={Position.Left} id="ingress" className="c1-handle c1-handle-left" />

      <div className="c1-card-header">
        <span className="c1-egress-icon">{icon}</span>
        <span className={`c1-badge ${badgeClass}`}>{badgeLabel}</span>
      </div>

      <div className="c1-egress-title" title={data.name}>
        {data.name}
      </div>

      {data.subType && <div className="c1-egress-type">{data.subType}</div>}

      {data.details && <div className="c1-egress-details">{data.details}</div>}

      {data.targetCount !== undefined && data.targetCount > 0 && (
        <div className="c1-egress-stats">
          {isDb ? `Used by ${data.targetCount} projects` : `Called by ${data.targetCount} projects`}
        </div>
      )}

      {data.filePath && data.onOpenFile && (
        <button
          className="c1-source-btn"
          onClick={(e) => {
            e.stopPropagation();
            data.onOpenFile?.(data.filePath!);
          }}
          title="Open configuration / definition"
        >
          📄 Source
        </button>
      )}
    </div>
  );
});

// ============================================================================
// C1 System Boundary Container (Visual Frame)
// ============================================================================
export interface C1BoundaryData extends Record<string, unknown> {
  systemName: string;
  projectCount: number;
  onToggleGroupAll?: () => void;
  isGroupedAll?: boolean;
}

export const C1BoundaryCardNode = memo((props: any) => {
  const data = props.data as C1BoundaryData;
  return (
    <div className="c1-boundary-container">
      <div className="c1-boundary-header">
        <div className="c1-boundary-header-left">
          <span className="c1-boundary-icon">🏢</span>
          <span className="c1-boundary-title">SYSTEM BOUNDARY: {data?.systemName || 'WORKSPACE'}</span>
          <span className="c1-boundary-count">({data?.projectCount || 0} projects)</span>
        </div>
        {data?.onToggleGroupAll && data.projectCount > 10 && (
          <button
            className="c1-boundary-toggle-btn"
            onClick={(e) => {
              e.stopPropagation();
              data.onToggleGroupAll?.();
            }}
            title={data.isGroupedAll ? "Show categorized project groups" : "Collapse all projects into 1 card"}
          >
            {data.isGroupedAll ? "Split by Kind" : "Collapse All to 1 Box"}
          </button>
        )}
      </div>
    </div>
  );
});

// ============================================================================
// C1 Grouped Box Card (For > 10 items of the same kind)
// ============================================================================
export interface C1GroupData extends Record<string, unknown> {
  id: string;
  kind: string; // 'Endpoint' | 'Database' | 'ExternalService' | 'Topic' | 'Service' | 'Library' | 'Worker' | 'App' | 'Project'
  category: 'ingress' | 'project' | 'egress';
  title: string;
  count: number;
  items: any[];
  subSummary?: string;
  methodCounts?: Record<string, number>;
  previewItems?: string[];
  isExpanded?: boolean;
  onToggleExpand?: () => void;
  onSelectNode?: () => void;
  isSelected?: boolean;
}

export const C1GroupCardNode = memo((props: any) => {
  const data = props.data as C1GroupData;
  if (!data) return null;

  const k = (data.kind || '').toLowerCase();
  const isEndpoint = k === 'endpoint';
  const isDb = k === 'database';
  const isExt = k.includes('external') || k.includes('cloud');
  const isMsg = k.includes('topic') || k.includes('queue') || k.includes('messaging');
  const isLib = k.includes('lib');
  const isService = k.includes('service');

  const icon = isEndpoint ? '🌐' : isDb ? '🗄️' : isExt ? '☁️' : isMsg ? '📨' : isLib ? '📚' : isService ? '⚙️' : '📦';
  const themeClass = isEndpoint
    ? 'c1-group-endpoint'
    : isDb
    ? 'c1-group-database'
    : isExt
    ? 'c1-group-external'
    : isMsg
    ? 'c1-group-messaging'
    : isLib
    ? 'c1-group-library'
    : isService
    ? 'c1-group-service'
    : 'c1-group-project';

  return (
    <div
      className={`c1-node-card c1-group-card ${themeClass} ${data.isSelected ? 'is-selected' : ''}`}
      onClick={(e) => {
        e.stopPropagation();
        data.onSelectNode?.();
      }}
    >
      {/* Left handle for incoming connections (Projects, Egress) */}
      {(data.category === 'project' || data.category === 'egress') && (
        <Handle type="target" position={Position.Left} id="ingress" className="c1-handle c1-handle-left" />
      )}

      <div className="c1-group-header">
        <div className="c1-group-header-left">
          <span className="c1-group-icon">{icon}</span>
          <span className="c1-group-badge">{data.kind.toUpperCase()} GROUP</span>
          <span className="c1-group-count-pill">{data.count}</span>
        </div>
        {data.onToggleExpand && (
          <button
            className="c1-group-toggle-btn"
            onClick={(e) => {
              e.stopPropagation();
              data.onToggleExpand?.();
            }}
            title={data.isExpanded ? "Collapse group to hide details" : "Expand group to show individual cards"}
          >
            {data.isExpanded ? '▴ Collapse' : '▾ Expand'}
          </button>
        )}
      </div>

      <div className="c1-group-title" title={data.title}>
        {data.title}
      </div>

      {data.subSummary && (
        <div className="c1-group-subsummary">
          {data.subSummary}
        </div>
      )}

      {data.methodCounts && Object.keys(data.methodCounts).length > 0 && (
        <div className="c1-group-methods-row">
          {Object.entries(data.methodCounts).map(([method, count]) => (
            <span key={method} className={`c1-group-method-tag c1-method-${method.toLowerCase()}`}>
              {count} {method}
            </span>
          ))}
        </div>
      )}

      {data.previewItems && data.previewItems.length > 0 && (
        <div className="c1-group-preview-chips">
          {data.previewItems.slice(0, 3).map((item, idx) => (
            <span key={idx} className="c1-group-chip" title={item}>
              {item}
            </span>
          ))}
          {data.count > 3 && (
            <span className="c1-group-chip-more">+{data.count - 3} more</span>
          )}
        </div>
      )}

      <div className="c1-group-hint">
        Click card to inspect all {data.count} items in drawer
      </div>

      {/* Right handle for outgoing connections (Ingress, Projects) */}
      {(data.category === 'ingress' || data.category === 'project') && (
        <Handle type="source" position={Position.Right} id="egress" className="c1-handle c1-handle-right" />
      )}
    </div>
  );
});

