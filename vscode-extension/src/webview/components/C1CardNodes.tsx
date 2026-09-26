import React, { memo } from 'react';
import { Handle, Position } from '@xyflow/react';

export interface C1NodeData extends Record<string, unknown> {
  id: string;
  name: string;
  displayName?: string;
  category: 'app' | 'service' | 'external';
  kind: string;
  framework?: string;
  language?: string;
  filePath?: string;
  lineStart?: number;
  orientation?: 'LR' | 'TB';
  fontSize?: number;
  width?: number;
  inboundCalls: number;
  outboundCalls: number;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
  onDrillDownToC2?: (projectName: string) => void;
  onSelectNode?: () => void;
  isSelected?: boolean;
}

// ============================================================================
// 1. Laconic Minimalist Card Node (Large Legible Title & Clean Shape)
// ============================================================================
export const C1LaconicCardNode = memo((props: any) => {
  const data = props.data as C1NodeData;
  if (!data) return null;

  const title = data.displayName || data.name;
  const isWorker = data.kind?.toLowerCase() === 'worker';
  const isLR = (data.orientation || 'LR') === 'LR';
  const targetPos = isLR ? Position.Left : Position.Top;
  const sourcePos = isLR ? Position.Right : Position.Bottom;

  const badgeText =
    data.category === 'app'
      ? 'APP'
      : data.category === 'external'
      ? 'EXT'
      : isWorker
      ? 'WORKER'
      : 'SVC';

  const badgeClass =
    data.category === 'app'
      ? 'laconic-badge-app'
      : data.category === 'external'
      ? 'laconic-badge-ext'
      : 'laconic-badge-svc';

  return (
    <div
      className={`c1-laconic-node c1-laconic-${data.category} ${data.isSelected ? 'is-selected' : ''}`}
      style={{
        width: data.width ? `${data.width}px` : undefined,
      }}
      onClick={(e) => {
        e.stopPropagation();
        data.onSelectNode?.();
      }}
    >
      <Handle
        type="target"
        position={targetPos}
        id="in"
        className={`c1-handle c1-handle-${isLR ? 'left' : 'top'}`}
      />

      <div className="c1-laconic-header">
        <span className={`c1-laconic-badge ${badgeClass}`}>{badgeText}</span>
        <div className="c1-laconic-stats">
          {data.outboundCalls > 0 && (
            <span className="c1-laconic-stat outbound" title="Outbound service calls">
              ➔ {data.outboundCalls}
            </span>
          )}
          {data.inboundCalls > 0 && (
            <span className="c1-laconic-stat inbound" title="Inbound calls">
              ◀ {data.inboundCalls}
            </span>
          )}
        </div>
      </div>

      <div
        className="c1-laconic-title"
        style={{ fontSize: data.fontSize ? `${data.fontSize}px` : '15px' }}
        title={title}
      >
        {title}
      </div>

      {data.onDrillDownToC2 && data.category === 'service' && (
        <button
          className="c1-laconic-drill-btn"
          onClick={(e) => {
            e.stopPropagation();
            data.onDrillDownToC2?.(data.name);
          }}
          title="Open C2 Project Flow"
        >
          🔀
        </button>
      )}

      <Handle
        type="source"
        position={sourcePos}
        id="out"
        className={`c1-handle c1-handle-${isLR ? 'right' : 'bottom'}`}
      />
    </div>
  );
});

// ============================================================================
// 2. C1 App Card (Ingress / Frontends / Web Apps / Gateways)
// ============================================================================
export const C1AppCardNode = memo((props: any) => {
  const data = props.data as C1NodeData;
  if (!data) return null;

  const title = data.displayName || data.name;
  const tech = data.framework || data.language || 'Application';
  const isLR = (data.orientation || 'LR') === 'LR';
  const targetPos = isLR ? Position.Left : Position.Top;
  const sourcePos = isLR ? Position.Right : Position.Bottom;

  return (
    <div
      className={`c1-node-card c1-app-card ${data.isSelected ? 'is-selected' : ''}`}
      style={{
        width: data.width ? `${data.width}px` : undefined,
      }}
      onClick={(e) => {
        e.stopPropagation();
        data.onSelectNode?.();
      }}
    >
      <Handle
        type="target"
        position={targetPos}
        id="in"
        className={`c1-handle c1-handle-${isLR ? 'left' : 'top'}`}
      />

      <div className="c1-card-header">
        <div className="c1-card-badge-wrap">
          <span className="c1-badge-app">🌐 APP</span>
          <span className="c1-tech-pill" title={tech}>{tech}</span>
        </div>
        {data.filePath && data.onOpenFile && (
          <button
            className="c1-source-btn"
            onClick={(e) => {
              e.stopPropagation();
              data.onOpenFile?.(data.filePath!, data.lineStart);
            }}
            title="Open source file"
          >
            📄
          </button>
        )}
      </div>

      <div
        className="c1-card-title"
        style={{ fontSize: data.fontSize ? `${data.fontSize}px` : undefined }}
        title={title}
      >
        {title}
      </div>

      <div className="c1-card-footer">
        <span className="c1-calls-stat outbound" title="Outbound service calls">
          ➔ {data.outboundCalls} calls
        </span>
      </div>

      <Handle
        type="source"
        position={sourcePos}
        id="out"
        className={`c1-handle c1-handle-${isLR ? 'right' : 'bottom'}`}
      />
    </div>
  );
});

// ============================================================================
// 3. C1 Service Card (Core Domain Microservices & Workers)
// ============================================================================
export const C1ServiceCardNode = memo((props: any) => {
  const data = props.data as C1NodeData;
  if (!data) return null;

  const title = data.displayName || data.name;
  const isWorker = data.kind.toLowerCase() === 'worker';
  const tech = data.framework || data.language || (isWorker ? 'Worker' : 'Service');
  const isLR = (data.orientation || 'LR') === 'LR';
  const targetPos = isLR ? Position.Left : Position.Top;
  const sourcePos = isLR ? Position.Right : Position.Bottom;

  return (
    <div
      className={`c1-node-card c1-service-card ${isWorker ? 'is-worker' : ''} ${data.isSelected ? 'is-selected' : ''}`}
      style={{
        width: data.width ? `${data.width}px` : undefined,
      }}
      onClick={(e) => {
        e.stopPropagation();
        data.onSelectNode?.();
      }}
    >
      <Handle
        type="target"
        position={targetPos}
        id="in"
        className={`c1-handle c1-handle-${isLR ? 'left' : 'top'}`}
      />

      <div className="c1-card-header">
        <div className="c1-card-badge-wrap">
          <span className={isWorker ? 'c1-badge-worker' : 'c1-badge-service'}>
            {isWorker ? '⚡ WORKER' : '⚙️ SERVICE'}
          </span>
          <span className="c1-tech-pill" title={tech}>{tech}</span>
        </div>

        <div className="c1-card-actions">
          {data.onDrillDownToC2 && (
            <button
              className="c1-drill-btn"
              onClick={(e) => {
                e.stopPropagation();
                data.onDrillDownToC2?.(data.name);
              }}
              title="Drill down into C2 Project Flow"
            >
              🔀
            </button>
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
              📄
            </button>
          )}
        </div>
      </div>

      <div
        className="c1-card-title"
        style={{ fontSize: data.fontSize ? `${data.fontSize}px` : undefined }}
        title={title}
      >
        {title}
      </div>

      <div className="c1-card-footer">
        <span className="c1-calls-stat inbound" title="Inbound calls">
          ◀ {data.inboundCalls} in
        </span>
        <span className="c1-calls-stat outbound" title="Outbound calls">
          {data.outboundCalls} out ▶
        </span>
      </div>

      <Handle
        type="source"
        position={sourcePos}
        id="out"
        className={`c1-handle c1-handle-${isLR ? 'right' : 'bottom'}`}
      />
    </div>
  );
});

// ============================================================================
// 4. C1 External Service Card (Third-party APIs & External SaaS)
// ============================================================================
export const C1ExternalCardNode = memo((props: any) => {
  const data = props.data as C1NodeData;
  if (!data) return null;

  const title = data.displayName || data.name;
  const tech = data.framework || 'External API';
  const isLR = (data.orientation || 'LR') === 'LR';
  const targetPos = isLR ? Position.Left : Position.Top;
  const sourcePos = isLR ? Position.Right : Position.Bottom;

  return (
    <div
      className={`c1-node-card c1-external-card ${data.isSelected ? 'is-selected' : ''}`}
      style={{
        width: data.width ? `${data.width}px` : undefined,
      }}
      onClick={(e) => {
        e.stopPropagation();
        data.onSelectNode?.();
      }}
    >
      <Handle
        type="target"
        position={targetPos}
        id="in"
        className={`c1-handle c1-handle-${isLR ? 'left' : 'top'}`}
      />

      <div className="c1-card-header">
        <div className="c1-card-badge-wrap">
          <span className="c1-badge-external">🔌 EXTERNAL</span>
          <span className="c1-tech-pill" title={tech}>{tech}</span>
        </div>
      </div>

      <div
        className="c1-card-title"
        style={{ fontSize: data.fontSize ? `${data.fontSize}px` : undefined }}
        title={title}
      >
        {title}
      </div>

      <div className="c1-card-footer">
        <span className="c1-calls-stat inbound" title="Inbound client calls">
          ◀ {data.inboundCalls} calls
        </span>
      </div>

      <Handle
        type="source"
        position={sourcePos}
        id="out"
        className={`c1-handle c1-handle-${isLR ? 'right' : 'bottom'}`}
      />
    </div>
  );
});

// ============================================================================
// 5. C1 Swimlane Container Node (For 3-Column Swimlane Layout)
// ============================================================================
export interface C1SwimlaneData extends Record<string, unknown> {
  id: string;
  title: string;
  count: number;
  category: 'app' | 'service' | 'external';
  width: number;
  height: number;
}

export const C1SwimlaneCardNode = memo((props: any) => {
  const data = props.data as C1SwimlaneData;
  if (!data) return null;

  return (
    <div
      className={`c1-swimlane-box swimlane-${data.category}`}
      style={{
        width: `${data.width}px`,
        height: `${data.height}px`,
      }}
    >
      <div className="c1-swimlane-badge">
        <span className="c1-swimlane-title">{data.title}</span>
        <span className="c1-swimlane-count">{data.count} items</span>
      </div>
    </div>
  );
});
