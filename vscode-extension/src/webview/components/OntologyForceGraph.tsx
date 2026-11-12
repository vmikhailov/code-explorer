import React, { useRef, useState, useEffect, useMemo, useCallback } from 'react';
import {
  OntologyNodeSchemaDto,
  OntologySchemaDocumentDto,
  OntologyWorkspaceStatsDto,
} from '../../../../proto/types';
import {
  buildOntologyGraphEdges,
  buildOntologyGraphLayout,
  OntologyGraphNode,
} from '../domain/ontologyGraphLayout';
import {
  computeFitViewport,
  hitTestOntologyNode,
  OntologyViewport,
  renderOntologyCanvas,
} from './ontologyCanvasRenderer';

export interface OntologyForceGraphProps {
  schema: OntologySchemaDocumentDto;
  visibleNodes: readonly OntologyNodeSchemaDto[];
  activeCategories: ReadonlySet<string>;
  workspace: OntologyWorkspaceStatsDto | null;
  onlyActiveInWorkspace: boolean;
  selectedKind: string;
  onSelectKind: (kind: string) => void;
}

interface DragInteraction {
  mode: 'pan' | 'node';
  nodeKind: string | null;
  startClientX: number;
  startClientY: number;
  originX: number;
  originY: number;
  moved: boolean;
}

const CONTEXT_2D = '2d';
const EVENT_WHEEL = 'wheel';
const CURSOR_GRAB = 'grab';
const CURSOR_GRABBING = 'grabbing';
const CURSOR_POINTER = 'pointer';
const MIN_ZOOM = 0.22;
const MAX_ZOOM = 2.8;

export const OntologyForceGraph: React.FC<OntologyForceGraphProps> = ({
  schema,
  visibleNodes,
  activeCategories,
  workspace,
  onlyActiveInWorkspace,
  selectedKind,
  onSelectKind,
}) => {
  const wrapRef = useRef<HTMLDivElement | null>(null);
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const dragRef = useRef<DragInteraction | null>(null);
  const initializedFitRef = useRef<boolean>(false);
  const lastNodeKeyRef = useRef<string>('');

  const [canvasSize, setCanvasSize] = useState<{ width: number; height: number }>({
    width: 860,
    height: 600,
  });
  const [viewport, setViewport] = useState<OntologyViewport>({ panX: 0, panY: 0, zoom: 0.85 });
  const [focusedKind, setFocusedKind] = useState<string | null>(null);
  const [hoveredKind, setHoveredKind] = useState<string | null>(null);
  const [isDragging, setIsDragging] = useState<boolean>(false);
  const [dragOverrides, setDragOverrides] = useState<Map<string, { x: number; y: number }>>(
    () => new Map()
  );

  const baseNodes = useMemo(
    () => buildOntologyGraphLayout(schema, visibleNodes, workspace),
    [schema, visibleNodes, workspace]
  );

  const positionedNodes = useMemo<OntologyGraphNode[]>(
    () =>
      baseNodes.map((n) => {
        const over = dragOverrides.get(n.kind);
        return over ? { ...n, x: over.x, y: over.y } : n;
      }),
    [baseNodes, dragOverrides]
  );

  const nodeByKind = useMemo(() => {
    const map = new Map<string, OntologyGraphNode>();
    for (const n of positionedNodes) {
      map.set(n.kind, n);
    }
    return map;
  }, [positionedNodes]);

  const visibleKindSet = useMemo(
    () => new Set(visibleNodes.map((n) => n.kind)),
    [visibleNodes]
  );

  const graphEdges = useMemo(
    () =>
      buildOntologyGraphEdges(
        schema,
        visibleKindSet,
        activeCategories,
        workspace,
        onlyActiveInWorkspace
      ),
    [schema, visibleKindSet, activeCategories, workspace, onlyActiveInWorkspace]
  );

  useEffect(() => {
    if (focusedKind && !visibleKindSet.has(focusedKind)) {
      setFocusedKind(null);
    }
  }, [focusedKind, visibleKindSet]);

  useEffect(() => {
    const el = wrapRef.current;
    if (!el) return;

    const measure = () => {
      const rect = el.getBoundingClientRect();
      if (rect.width > 20 && rect.height > 20) {
        setCanvasSize({ width: Math.floor(rect.width), height: Math.floor(rect.height) });
      }
    };

    measure();
    const observer = typeof ResizeObserver !== 'undefined' ? new ResizeObserver(measure) : null;
    observer?.observe(el);
    return () => observer?.disconnect();
  }, []);

  useEffect(() => {
    if (canvasSize.width <= 100 || baseNodes.length === 0) return;
    const nodeKey = baseNodes.map((n) => n.kind).join(',');
    if (!initializedFitRef.current || nodeKey !== lastNodeKeyRef.current) {
      initializedFitRef.current = true;
      lastNodeKeyRef.current = nodeKey;
      setViewport(computeFitViewport(baseNodes, canvasSize.width, canvasSize.height));
    }
  }, [canvasSize.width, canvasSize.height, baseNodes]);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const dpr = typeof window !== 'undefined' ? window.devicePixelRatio || 1 : 1;
    canvas.width = Math.max(1, Math.floor(canvasSize.width * dpr));
    canvas.height = Math.max(1, Math.floor(canvasSize.height * dpr));
    const ctx = canvas.getContext(CONTEXT_2D);
    if (!ctx) return;

    renderOntologyCanvas(ctx, {
      nodes: positionedNodes,
      edges: graphEdges,
      nodeByKind,
      selectedKind,
      focusedKind,
      hoveredKind,
      viewport,
      width: canvasSize.width,
      height: canvasSize.height,
      dpr,
    });
  }, [
    canvasSize,
    positionedNodes,
    graphEdges,
    nodeByKind,
    selectedKind,
    focusedKind,
    hoveredKind,
    viewport,
  ]);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const handleWheel = (e: WheelEvent) => {
      e.preventDefault();
      const rect = canvas.getBoundingClientRect();
      const mx = e.clientX - rect.left;
      const my = e.clientY - rect.top;
      const factor = e.deltaY < 0 ? 1.12 : 0.89;

      setViewport((prev) => {
        const nextZoom = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, prev.zoom * factor));
        const scale = nextZoom / prev.zoom;
        return {
          zoom: nextZoom,
          panX: mx - (mx - prev.panX) * scale,
          panY: my - (my - prev.panY) * scale,
        };
      });
    };

    canvas.addEventListener(EVENT_WHEEL, handleWheel, { passive: false });
    return () => canvas.removeEventListener(EVENT_WHEEL, handleWheel);
  }, []);

  const handlePointerDown = useCallback(
    (e: React.PointerEvent<HTMLCanvasElement>) => {
      const canvas = canvasRef.current;
      if (!canvas) return;
      const rect = canvas.getBoundingClientRect();
      const ox = e.clientX - rect.left;
      const oy = e.clientY - rect.top;
      const hit = hitTestOntologyNode(positionedNodes, viewport, ox, oy);

      canvas.setPointerCapture(e.pointerId);
      setIsDragging(true);

      if (hit) {
        dragRef.current = {
          mode: 'node',
          nodeKind: hit.kind,
          startClientX: e.clientX,
          startClientY: e.clientY,
          originX: hit.x,
          originY: hit.y,
          moved: false,
        };
      } else {
        dragRef.current = {
          mode: 'pan',
          nodeKind: null,
          startClientX: e.clientX,
          startClientY: e.clientY,
          originX: viewport.panX,
          originY: viewport.panY,
          moved: false,
        };
      }
    },
    [positionedNodes, viewport]
  );

  const handlePointerMove = useCallback(
    (e: React.PointerEvent<HTMLCanvasElement>) => {
      const canvas = canvasRef.current;
      if (!canvas) return;
      const drag = dragRef.current;

      if (drag) {
        const dx = e.clientX - drag.startClientX;
        const dy = e.clientY - drag.startClientY;
        if (Math.abs(dx) > 4 || Math.abs(dy) > 4) {
          drag.moved = true;
        }
        if (drag.mode === 'pan') {
          setViewport((prev) => ({
            ...prev,
            panX: drag.originX + dx,
            panY: drag.originY + dy,
          }));
        } else if (drag.mode === 'node' && drag.nodeKind) {
          const kind = drag.nodeKind;
          const nextX = drag.originX + dx / viewport.zoom;
          const nextY = drag.originY + dy / viewport.zoom;
          setDragOverrides((prev) => {
            const copy = new Map(prev);
            copy.set(kind, { x: nextX, y: nextY });
            return copy;
          });
        }
        return;
      }

      const rect = canvas.getBoundingClientRect();
      const hit = hitTestOntologyNode(
        positionedNodes,
        viewport,
        e.clientX - rect.left,
        e.clientY - rect.top
      );
      setHoveredKind(hit ? hit.kind : null);
    },
    [positionedNodes, viewport]
  );

  const handlePointerUp = useCallback(
    (e: React.PointerEvent<HTMLCanvasElement>) => {
      const canvas = canvasRef.current;
      const drag = dragRef.current;
      dragRef.current = null;
      setIsDragging(false);

      if (canvas && canvas.hasPointerCapture(e.pointerId)) {
        canvas.releasePointerCapture(e.pointerId);
      }

      if (!drag || drag.moved) return;

      if (drag.mode === 'node' && drag.nodeKind) {
        onSelectKind(drag.nodeKind);
        setFocusedKind((prev) => (prev === drag.nodeKind ? null : drag.nodeKind));
      } else {
        setFocusedKind(null);
      }
    },
    [onSelectKind]
  );

  const handleZoomStep = useCallback(
    (factor: number) => {
      const cx = canvasSize.width / 2;
      const cy = canvasSize.height / 2;
      setViewport((prev) => {
        const nextZoom = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, prev.zoom * factor));
        const scale = nextZoom / prev.zoom;
        return {
          zoom: nextZoom,
          panX: cx - (cx - prev.panX) * scale,
          panY: cy - (cy - prev.panY) * scale,
        };
      });
    },
    [canvasSize.width, canvasSize.height]
  );

  const handleFitReset = useCallback(() => {
    setDragOverrides(new Map());
    setFocusedKind(null);
    setViewport(computeFitViewport(baseNodes, canvasSize.width, canvasSize.height));
  }, [baseNodes, canvasSize.width, canvasSize.height]);

  const cursorStyle = isDragging
    ? CURSOR_GRABBING
    : hoveredKind
      ? CURSOR_POINTER
      : CURSOR_GRAB;

  return (
    <div ref={wrapRef} className="ontology-graph-stage">
      <canvas
        ref={canvasRef}
        className="ontology-graph-canvas"
        style={{ width: canvasSize.width, height: canvasSize.height, cursor: cursorStyle }}
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={handlePointerUp}
        onPointerLeave={() => {
          if (!dragRef.current) setHoveredKind(null);
        }}
      />

      <div className="ontology-graph-legend">
        <span className="ontology-legend-chip"><i style={{ background: '#7c3aed' }} />Workloads</span>
        <span className="ontology-legend-chip"><i style={{ background: '#d97706' }} />Contracts</span>
        <span className="ontology-legend-chip"><i style={{ background: '#16a34a' }} />Data &amp; SQL</span>
        <span className="ontology-legend-chip"><i style={{ background: '#0891b2' }} />Cloud</span>
        <span className="ontology-legend-chip"><i style={{ background: '#dc2626' }} />UAST</span>
        <span className="ontology-legend-chip"><i style={{ background: '#4f46e5' }} />Slicing</span>
        <span className="ontology-legend-chip"><i style={{ background: '#2563eb' }} />Projects</span>
      </div>

      <div className="ontology-graph-controls">
        {focusedKind && (
          <button
            type="button"
            className="ontology-graph-ctrl-btn is-accent"
            onClick={() => setFocusedKind(null)}
            title="Clear isolated neighborhood and show all entities"
          >
            Show All
          </button>
        )}
        <button
          type="button"
          className="ontology-graph-ctrl-btn"
          onClick={() => handleZoomStep(0.84)}
          title="Zoom out"
        >
          −
        </button>
        <button
          type="button"
          className="ontology-graph-ctrl-btn"
          onClick={() => handleZoomStep(1.18)}
          title="Zoom in"
        >
          +
        </button>
        <button
          type="button"
          className="ontology-graph-ctrl-btn"
          onClick={handleFitReset}
          title="Fit graph and reset layout"
        >
          Fit
        </button>
      </div>
    </div>
  );
};
