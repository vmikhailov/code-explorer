import React, { useState, useMemo } from 'react';
import { ClassifiedC1Node, ClassifiedC1Edge } from './C1SystemContextView';

export interface C1FocusEgoViewProps {
  nodes: ClassifiedC1Node[];
  edges: ClassifiedC1Edge[];
  selectedNodeId?: string;
  onSelectNode?: (node: any) => void;
  onDrillDownToC2?: (projectName: string) => void;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
}

export const C1FocusEgoView: React.FC<C1FocusEgoViewProps> = ({
  nodes,
  edges,
  selectedNodeId,
  onSelectNode,
  onDrillDownToC2,
  onOpenFile,
}) => {
  // Determine default focused node (prefer selected, or node with most total calls, or first node)
  const defaultFocusId = useMemo(() => {
    if (selectedNodeId && nodes.some((n) => n.id === selectedNodeId)) {
      return selectedNodeId;
    }
    // Find node with highest combined in+out calls
    const activityMap = new Map<string, number>();
    for (const e of edges) {
      activityMap.set(e.source, (activityMap.get(e.source) || 0) + e.count);
      activityMap.set(e.target, (activityMap.get(e.target) || 0) + e.count);
    }
    let bestId = nodes[0]?.id || '';
    let maxCalls = -1;
    for (const n of nodes) {
      const calls = activityMap.get(n.id) || 0;
      if (calls > maxCalls) {
        maxCalls = calls;
        bestId = n.id;
      }
    }
    return bestId;
  }, [nodes, edges, selectedNodeId]);

  const [focusedId, setFocusedId] = useState<string>(defaultFocusId);
  const [filterText, setFilterText] = useState<string>('');

  // When selectedNodeId changes from outside, sync it
  React.useEffect(() => {
    if (selectedNodeId && nodes.some((n) => n.id === selectedNodeId)) {
      setFocusedId(selectedNodeId);
    }
  }, [selectedNodeId, nodes]);

  const currentNode = useMemo(() => {
    return nodes.find((n) => n.id === focusedId) || nodes[0];
  }, [nodes, focusedId]);

  // Inbound and Outbound edges
  const { inboundCallers, outboundDeps } = useMemo(() => {
    if (!currentNode) return { inboundCallers: [], outboundDeps: [] };

    const inList: { node: ClassifiedC1Node; count: number }[] = [];
    const outList: { node: ClassifiedC1Node; count: number }[] = [];

    for (const e of edges) {
      if (e.target === currentNode.id) {
        const caller = nodes.find((n) => n.id === e.source);
        if (caller) inList.push({ node: caller, count: e.count });
      }
      if (e.source === currentNode.id) {
        const callee = nodes.find((n) => n.id === e.target);
        if (callee) outList.push({ node: callee, count: e.count });
      }
    }

    inList.sort((a, b) => b.count - a.count || a.node.name.localeCompare(b.node.name));
    outList.sort((a, b) => b.count - a.count || a.node.name.localeCompare(b.node.name));

    return { inboundCallers: inList, outboundDeps: outList };
  }, [nodes, edges, currentNode]);

  const totalInboundCalls = inboundCallers.reduce((acc, c) => acc + c.count, 0);
  const totalOutboundCalls = outboundDeps.reduce((acc, c) => acc + c.count, 0);

  // Filtered list for selector dropdown
  const filteredOptions = useMemo(() => {
    if (!filterText.trim()) return nodes;
    const lower = filterText.toLowerCase();
    return nodes.filter(
      (n) => n.name.toLowerCase().includes(lower) || n.displayName.toLowerCase().includes(lower)
    );
  }, [nodes, filterText]);

  if (!currentNode) {
    return <div className="c1-ego-empty">No nodes available for focus view.</div>;
  }

  const badgeClass =
    currentNode.category === 'app'
      ? 'laconic-badge-app'
      : currentNode.category === 'external'
      ? 'laconic-badge-ext'
      : 'laconic-badge-svc';

  const badgeText =
    currentNode.category === 'app'
      ? 'APP'
      : currentNode.category === 'external'
      ? 'EXT'
      : currentNode.kind?.toLowerCase() === 'worker'
      ? 'WORKER'
      : 'SVC';

  return (
    <div className="c1-ego-container">
      {/* Top Focus Selector Bar */}
      <div className="c1-ego-topbar">
        <div className="c1-ego-selector-group">
          <span className="c1-ego-label">🎯 Focused Service:</span>
          <select
            className="c1-ego-select"
            value={currentNode.id}
            onChange={(e) => {
              setFocusedId(e.target.value);
              const n = nodes.find((x) => x.id === e.target.value);
              if (n) onSelectNode?.(n.rawNode);
            }}
          >
            {nodes.map((n) => (
              <option key={n.id} value={n.id}>
                {n.category === 'app' ? '🌐' : n.category === 'external' ? '🔌' : '⚙️'}{' '}
                {n.displayName || n.name} ({n.category.toUpperCase()})
              </option>
            ))}
          </select>
        </div>

        <div className="c1-ego-summary-pill">
          <span>Inbound: <strong>{inboundCallers.length}</strong> ({totalInboundCalls} calls)</span>
          <span className="pill-dot">•</span>
          <span>Outbound: <strong>{outboundDeps.length}</strong> ({totalOutboundCalls} calls)</span>
        </div>
      </div>

      {/* 3-Column Focus Stage */}
      <div className="c1-ego-stage">
        {/* Left Column: Inbound Callers */}
        <div className="c1-ego-col c1-ego-inbound">
          <div className="c1-ego-col-header">
            <span className="col-icon">📥</span>
            <span className="col-title">Inbound Consumers ({inboundCallers.length})</span>
            <span className="col-subtitle">Services calling this target</span>
          </div>

          <div className="c1-ego-list">
            {inboundCallers.length === 0 ? (
              <div className="c1-ego-list-empty">
                <span>Direct Ingress or Standalone</span>
                <p>No internal services call this service.</p>
              </div>
            ) : (
              inboundCallers.map(({ node, count }) => (
                <div
                  key={node.id}
                  className={`c1-ego-card c1-ego-neighbor cat-${node.category}`}
                  onClick={() => {
                    setFocusedId(node.id);
                    onSelectNode?.(node.rawNode);
                  }}
                  title="Click to pivot focus to this service"
                >
                  <div className="card-top">
                    <span className={`c1-laconic-badge laconic-badge-${node.category === 'app' ? 'app' : node.category === 'external' ? 'ext' : 'svc'}`}>
                      {node.category.toUpperCase()}
                    </span>
                    <span className="call-flow-badge inbound">
                      {count} calls ➔
                    </span>
                  </div>
                  <div className="card-title">{node.displayName || node.name}</div>
                  <div className="card-hint">Click to pivot 🎯</div>
                </div>
              ))
            )}
          </div>
        </div>

        {/* Center: The Focused Hero Node */}
        <div className="c1-ego-center">
          <div className={`c1-ego-hero-card cat-${currentNode.category}`}>
            <div className="hero-category-header">
              <span className={`c1-laconic-badge ${badgeClass}`}>{badgeText}</span>
              <span className="hero-kind">{currentNode.framework || currentNode.language || currentNode.kind}</span>
            </div>

            <h2 className="hero-title">{currentNode.displayName || currentNode.name}</h2>
            {currentNode.filePath && (
              <div className="hero-path" title={currentNode.filePath}>
                📂 {currentNode.filePath.split('/').slice(-3).join('/')}
              </div>
            )}

            <div className="hero-metrics-grid">
              <div className="metric-box">
                <span className="metric-val">{inboundCallers.length}</span>
                <span className="metric-lbl">Callers</span>
                <span className="metric-sub">{totalInboundCalls} calls</span>
              </div>
              <div className="metric-box">
                <span className="metric-val">{outboundDeps.length}</span>
                <span className="metric-lbl">Dependencies</span>
                <span className="metric-sub">{totalOutboundCalls} calls</span>
              </div>
            </div>

            <div className="hero-actions">
              {onDrillDownToC2 && currentNode.category === 'service' && (
                <button
                  className="hero-btn primary"
                  onClick={() => onDrillDownToC2(currentNode.name)}
                  title="Open C2 Project Component Flow"
                >
                  🔀 Drill Down to C2
                </button>
              )}
              {onOpenFile && currentNode.filePath && (
                <button
                  className="hero-btn secondary"
                  onClick={() => onOpenFile(currentNode.filePath!, currentNode.lineStart)}
                  title="Open source code file"
                >
                  📄 Source Code
                </button>
              )}
            </div>
          </div>
        </div>

        {/* Right Column: Outbound Dependencies */}
        <div className="c1-ego-col c1-ego-outbound">
          <div className="c1-ego-col-header">
            <span className="col-icon">📤</span>
            <span className="col-title">Outbound Dependencies ({outboundDeps.length})</span>
            <span className="col-subtitle">Services called by this target</span>
          </div>

          <div className="c1-ego-list">
            {outboundDeps.length === 0 ? (
              <div className="c1-ego-list-empty">
                <span>Leaf Service</span>
                <p>This service makes no outbound service calls.</p>
              </div>
            ) : (
              outboundDeps.map(({ node, count }) => (
                <div
                  key={node.id}
                  className={`c1-ego-card c1-ego-neighbor cat-${node.category}`}
                  onClick={() => {
                    setFocusedId(node.id);
                    onSelectNode?.(node.rawNode);
                  }}
                  title="Click to pivot focus to this service"
                >
                  <div className="card-top">
                    <span className="call-flow-badge outbound">
                      ➔ {count} calls
                    </span>
                    <span className={`c1-laconic-badge laconic-badge-${node.category === 'app' ? 'app' : node.category === 'external' ? 'ext' : 'svc'}`}>
                      {node.category.toUpperCase()}
                    </span>
                  </div>
                  <div className="card-title">{node.displayName || node.name}</div>
                  <div className="card-hint">Click to pivot 🎯</div>
                </div>
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
