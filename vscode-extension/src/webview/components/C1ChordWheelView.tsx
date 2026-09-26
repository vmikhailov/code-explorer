import React, { useState, useMemo, useRef, useCallback } from 'react';
import { ClassifiedC1Node, ClassifiedC1Edge } from './C1SystemContextView';

export interface C1ChordWheelViewProps {
  nodes: ClassifiedC1Node[];
  edges: ClassifiedC1Edge[];
  selectedNodeId?: string;
  onSelectNode?: (node: any) => void;
  onDrillDownToC2?: (projectName: string) => void;
}

export const C1ChordWheelView: React.FC<C1ChordWheelViewProps> = ({
  nodes,
  edges,
  selectedNodeId,
  onSelectNode,
  onDrillDownToC2,
}) => {
  const [hoveredNodeId, setHoveredNodeId] = useState<string | null>(null);
  const [hoveredEdge, setHoveredEdge] = useState<ClassifiedC1Edge | null>(null);
  const [zoom, setZoom] = useState<number>(0.95);
  const [pan, setPan] = useState<{ x: number; y: number }>({ x: 0, y: 0 });

  const isDraggingRef = useRef(false);
  const dragStartRef = useRef<{ x: number; y: number }>({ x: 0, y: 0 });

  // Sort nodes in order: Apps first, then Services, then External
  const orderedNodes = useMemo(() => {
    const apps = nodes.filter((n) => n.category === 'app');
    const svcs = nodes.filter((n) => n.category === 'service');
    const exts = nodes.filter((n) => n.category === 'external');

    const sortFn = (a: ClassifiedC1Node, b: ClassifiedC1Node) => a.name.localeCompare(b.name);
    apps.sort(sortFn);
    svcs.sort(sortFn);
    exts.sort(sortFn);

    return [...apps, ...svcs, ...exts];
  }, [nodes]);

  // Compute angles for each node
  const RADIUS = 360;
  const nodeLayoutMap = useMemo(() => {
    const map = new Map<
      string,
      {
        node: ClassifiedC1Node;
        angle: number;
        x: number;
        y: number;
        labelX: number;
        labelY: number;
        rotation: number;
        isFlipped: boolean;
      }
    >();

    const total = orderedNodes.length;
    orderedNodes.forEach((node, idx) => {
      // Angle in radians (start from -PI/2 so Apps start at the top)
      const angle = (2 * Math.PI * idx) / Math.max(total, 1) - Math.PI / 2;
      const x = RADIUS * Math.cos(angle);
      const y = RADIUS * Math.sin(angle);

      const labelRadius = RADIUS + 18;
      const labelX = labelRadius * Math.cos(angle);
      const labelY = labelRadius * Math.sin(angle);

      // Angle in degrees
      let deg = (angle * 180) / Math.PI;
      // If label is on the left half of the circle, flip 180 so it's readable
      const isFlipped = deg > 90 || deg < -90;
      const rotation = isFlipped ? deg + 180 : deg;

      map.set(node.id, {
        node,
        angle,
        x,
        y,
        labelX,
        labelY,
        rotation,
        isFlipped,
      });
    });

    return map;
  }, [orderedNodes, RADIUS]);

  // Set of connected node IDs for active hover
  const activeConnectedNodeIds = useMemo(() => {
    if (!hoveredNodeId) return null;
    const set = new Set<string>();
    set.add(hoveredNodeId);
    for (const e of edges) {
      if (e.source === hoveredNodeId) set.add(e.target);
      if (e.target === hoveredNodeId) set.add(e.source);
    }
    return set;
  }, [hoveredNodeId, edges]);

  // Zoom / Pan handlers
  const handleWheel = useCallback((e: React.WheelEvent) => {
    e.preventDefault();
    const factor = e.deltaY < 0 ? 1.1 : 0.9;
    setZoom((z) => Math.min(3.0, Math.max(0.3, z * factor)));
  }, []);

  const handleMouseDown = useCallback((e: React.MouseEvent) => {
    if (e.button !== 0) return;
    isDraggingRef.current = true;
    dragStartRef.current = { x: e.clientX - pan.x, y: e.clientY - pan.y };
  }, [pan]);

  const handleMouseMove = useCallback((e: React.MouseEvent) => {
    if (!isDraggingRef.current) return;
    setPan({
      x: e.clientX - dragStartRef.current.x,
      y: e.clientY - dragStartRef.current.y,
    });
  }, []);

  const handleMouseUp = useCallback(() => {
    isDraggingRef.current = false;
  }, []);

  const handleReset = useCallback(() => {
    setZoom(0.95);
    setPan({ x: 0, y: 0 });
  }, []);

  return (
    <div
      className="c1-chord-container"
      onWheel={handleWheel}
      onMouseDown={handleMouseDown}
      onMouseMove={handleMouseMove}
      onMouseUp={handleMouseUp}
      onMouseLeave={handleMouseUp}
      style={{ cursor: isDraggingRef.current ? 'grabbing' : 'grab' }}
    >
      {/* Zoom / Info Overlay */}
      <div className="c1-chord-controls">
        <button className="ctrl-btn" onClick={() => setZoom((z) => Math.max(0.3, z / 1.15))}>
          −
        </button>
        <span className="zoom-pct" onClick={handleReset}>
          {Math.round(zoom * 100)}%
        </span>
        <button className="ctrl-btn" onClick={() => setZoom((z) => Math.min(3.0, z * 1.15))}>
          +
        </button>
        <button className="ctrl-btn reset-btn" onClick={handleReset}>
          ↺ Reset
        </button>
      </div>

      {/* Hover Info Tooltip */}
      {hoveredNodeId && (
        <div className="c1-chord-tooltip">
          {(() => {
            const info = nodeLayoutMap.get(hoveredNodeId);
            if (!info) return null;
            const inCount = edges.filter((e) => e.target === hoveredNodeId).reduce((a, b) => a + b.count, 0);
            const outCount = edges.filter((e) => e.source === hoveredNodeId).reduce((a, b) => a + b.count, 0);
            return (
              <div>
                <strong>{info.node.displayName || info.node.name}</strong>
                <div style={{ fontSize: '10.5px', color: '#94a3b8', marginTop: '2px' }}>
                  {info.node.category.toUpperCase()} • In: {inCount} calls • Out: {outCount} calls
                </div>
              </div>
            );
          })()}
        </div>
      )}

      {/* SVG Canvas */}
      <svg
        className="c1-chord-svg"
        viewBox="-560 -560 1120 1120"
        style={{
          transform: `translate(${pan.x}px, ${pan.y}px) scale(${zoom})`,
          transformOrigin: 'center center',
        }}
      >
        <defs>
          <radialGradient id="chord-bg-glow" cx="50%" cy="50%" r="50%">
            <stop offset="0%" stopColor="#1e293b" stopOpacity="0.3" />
            <stop offset="100%" stopColor="#0b1120" stopOpacity="0" />
          </radialGradient>
        </defs>

        {/* Subtle background guide circle */}
        <circle cx="0" cy="0" r={RADIUS} fill="url(#chord-bg-glow)" stroke="rgba(255, 255, 255, 0.08)" strokeWidth="1" strokeDasharray="4 6" />

        {/* Chords / Links */}
        <g className="chord-links-group">
          {edges.map((e, idx) => {
            const p1 = nodeLayoutMap.get(e.source);
            const p2 = nodeLayoutMap.get(e.target);
            if (!p1 || !p2) return null;

            const isHighlighted =
              hoveredNodeId !== null &&
              (e.source === hoveredNodeId || e.target === hoveredNodeId);
            const isDimmed =
              hoveredNodeId !== null &&
              e.source !== hoveredNodeId &&
              e.target !== hoveredNodeId;

            // Quadratic Bezier path bending towards the center (0, 0)
            const pathData = `M ${p1.x} ${p1.y} Q 0 0 ${p2.x} ${p2.y}`;

            const sourceCat = p1.node.category;
            const strokeColor =
              sourceCat === 'app' ? '#38bdf8' : sourceCat === 'external' ? '#f59e0b' : '#818cf8';

            return (
              <path
                key={`chord-${e.source}->${e.target}-${idx}`}
                d={pathData}
                fill="none"
                stroke={isHighlighted ? '#38bdf8' : strokeColor}
                strokeWidth={isHighlighted ? Math.min(5, Math.max(2.5, e.count * 1.2)) : Math.min(3, Math.max(1, e.count * 0.7))}
                strokeOpacity={isHighlighted ? 0.95 : isDimmed ? 0.03 : 0.25}
                className="chord-ribbon"
                onMouseEnter={() => setHoveredEdge(e)}
                onMouseLeave={() => setHoveredEdge(null)}
              />
            );
          })}
        </g>

        {/* Perimeter Nodes & Labels */}
        <g className="chord-nodes-group">
          {orderedNodes.map((node) => {
            const info = nodeLayoutMap.get(node.id);
            if (!info) return null;

            const isHovered = hoveredNodeId === node.id;
            const isConnected = activeConnectedNodeIds ? activeConnectedNodeIds.has(node.id) : true;
            const isSelected = selectedNodeId === node.id;

            const color =
              node.category === 'app' ? '#38bdf8' : node.category === 'external' ? '#fbbf24' : '#a5b4fc';

            return (
              <g
                key={`chord-node-${node.id}`}
                className="chord-node-item"
                style={{
                  opacity: isConnected ? 1 : 0.2,
                  cursor: 'pointer',
                  transition: 'opacity 0.15s ease',
                }}
                onMouseEnter={() => setHoveredNodeId(node.id)}
                onMouseLeave={() => setHoveredNodeId(null)}
                onClick={() => {
                  onSelectNode?.(node.rawNode);
                }}
              >
                {/* Node Outer Ring on hover */}
                {isHovered && (
                  <circle
                    cx={info.x}
                    cy={info.y}
                    r="9"
                    fill="none"
                    stroke={color}
                    strokeWidth="2"
                    strokeOpacity="0.8"
                  />
                )}

                {/* Node Dot */}
                <circle
                  cx={info.x}
                  cy={info.y}
                  r={isHovered || isSelected ? 6 : 4}
                  fill={isSelected ? '#ffffff' : color}
                  stroke="#0f172a"
                  strokeWidth="1.5"
                />

                {/* Service Label Text */}
                <text
                  x={info.labelX}
                  y={info.labelY}
                  textAnchor={info.isFlipped ? 'end' : 'start'}
                  dominantBaseline="central"
                  transform={`rotate(${info.rotation}, ${info.labelX}, ${info.labelY})`}
                  fill={isHovered || isSelected ? '#ffffff' : isConnected ? '#e2e8f0' : '#64748b'}
                  fontSize={isHovered ? 12 : 10.5}
                  fontWeight={isHovered || isSelected ? 700 : 500}
                  className="chord-label-text"
                >
                  {node.displayName || node.name}
                </text>
              </g>
            );
          })}
        </g>
      </svg>

      {/* Category Legend */}
      <div className="c1-chord-legend">
        <div className="legend-item">
          <span className="legend-dot app" />
          <span>Apps & Ingress</span>
        </div>
        <div className="legend-item">
          <span className="legend-dot service" />
          <span>Domain Services</span>
        </div>
        <div className="legend-item">
          <span className="legend-dot external" />
          <span>External APIs</span>
        </div>
      </div>
    </div>
  );
};
