import React, { useRef, useState, useEffect, useMemo, useCallback } from 'react';
import {
  OntologyNodeSchemaDto,
  OntologySchemaDocumentDto,
  OntologyWorkspaceStatsDto,
} from '../../../../proto/types';
import {
  buildOntologyGraphCapsules,
  buildOntologyGraphEdges,
  buildOntologyGraphLayout,
  OntologyForceSimulation,
  OntologyGraphNode,
} from '../domain/ontologyGraphLayout';
import {
  computeFitViewport,
  hitTestOntologyCapsuleHeader,
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
  expandedHierarchies?: ReadonlySet<string>;
  redirectMap?: ReadonlyMap<string, string>;
  collapsedCounts?: ReadonlyMap<string, number>;
  selectedKind: string;
  onSelectKind: (kind: string) => void;
  onToggleHierarchy?: (hierarchyKind: string) => void;
}

interface DragInteraction {
  mode: 'pan' | 'node' | 'capsule';
  nodeKind: string | null;
  capsuleKind: string | null;
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
const SPACING_DECIMALS = 1;
const ZOOM_OUT_STEP = 0.84;
const ZOOM_IN_STEP = 1.18;

const OntologyGraphLegend: React.FC = () => (
  <div className="ontology-graph-legend">
    <span className="ontology-legend-chip"><i style={{ background: '#db2777' }} />Domains</span>
    <span className="ontology-legend-chip"><i style={{ background: '#7c3aed' }} />Workloads</span>
    <span className="ontology-legend-chip"><i style={{ background: '#d97706' }} />Contracts</span>
    <span className="ontology-legend-chip"><i style={{ background: '#16a34a' }} />Data &amp; SQL</span>
    <span className="ontology-legend-chip"><i style={{ background: '#0891b2' }} />Cloud</span>
    <span className="ontology-legend-chip"><i style={{ background: '#dc2626' }} />UAST</span>
    <span className="ontology-legend-chip"><i style={{ background: '#4f46e5' }} />Slicing</span>
    <span className="ontology-legend-chip"><i style={{ background: '#2563eb' }} />Projects</span>
  </div>
);

interface OntologyGraphControlsProps {
  focusedKind: string | null;
  spacingFactor: number;
  hubFactor: number;
  isFrozen: boolean;
  onClearFocus: () => void;
  onSpacingChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
  onHubChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
  onToggleFreeze: () => void;
  onZoomStep: (factor: number) => void;
  onFitReset: () => void;
}

const OntologyGraphControls: React.FC<OntologyGraphControlsProps> = ({
  focusedKind,
  spacingFactor,
  hubFactor,
  isFrozen,
  onClearFocus,
  onSpacingChange,
  onHubChange,
  onToggleFreeze,
  onZoomStep,
  onFitReset,
}) => (
  <div className="ontology-graph-controls">
    {focusedKind && (
      <button
        type="button"
        className="ontology-graph-ctrl-btn is-accent"
        onClick={onClearFocus}
        title="Clear isolated neighborhood and show all entities"
      >
        Show All
      </button>
    )}
    <label className="ontology-air-control" title="Regulate elastic spacing (air) between nodes">
      <span className="ontology-air-label">Air</span>
      <input
        type="range"
        min="0.1"
        max="2.4"
        step="0.1"
        value={spacingFactor}
        onChange={onSpacingChange}
        aria-label="Node spacing factor"
      />
      <span className="ontology-air-val">{spacingFactor.toFixed(SPACING_DECIMALS)}x</span>
    </label>
    <label className="ontology-air-control" title="Scale repulsion and spring distance by node edge degree (ForceAtlas2 hub factor)">
      <span className="ontology-air-label">Hub</span>
      <input
        type="range"
        min="0.0"
        max="5.0"
        step="0.1"
        value={hubFactor}
        onChange={onHubChange}
        aria-label="Degree repulsion factor"
      />
      <span className="ontology-air-val">{hubFactor.toFixed(SPACING_DECIMALS)}x</span>
    </label>
    <button
      type="button"
      aria-pressed={isFrozen}
      className={`ontology-graph-ctrl-btn ${isFrozen ? 'is-accent' : ''}`}
      onClick={onToggleFreeze}
      title={isFrozen ? 'Unfreeze elastic spring simulation' : 'Freeze node positions'}
    >
      {isFrozen ? 'Frozen' : 'Freeze'}
    </button>
    <button
      type="button"
      className="ontology-graph-ctrl-btn"
      onClick={() => onZoomStep(ZOOM_OUT_STEP)}
      title="Zoom out"
    >
      −
    </button>
    <button
      type="button"
      className="ontology-graph-ctrl-btn"
      onClick={() => onZoomStep(ZOOM_IN_STEP)}
      title="Zoom in"
    >
      +
    </button>
    <button
      type="button"
      className="ontology-graph-ctrl-btn"
      onClick={onFitReset}
      title="Fit graph and reset layout"
    >
      Fit
    </button>
  </div>
);

export const OntologyForceGraph: React.FC<OntologyForceGraphProps> = ({
  schema,
  visibleNodes,
  activeCategories,
  workspace,
  onlyActiveInWorkspace,
  expandedHierarchies,
  redirectMap,
  collapsedCounts,
  selectedKind,
  onSelectKind,
  onToggleHierarchy,
}) => {
  const wrapRef = useRef<HTMLDivElement | null>(null);
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const dragRef = useRef<DragInteraction | null>(null);
  const simRef = useRef<OntologyForceSimulation | null>(null);
  if (!simRef.current) {
    simRef.current = new OntologyForceSimulation();
  }
  const nodesRef = useRef<OntologyGraphNode[]>([]);
  const rafRef = useRef<number | null>(null);
  const initializedFitRef = useRef<boolean>(false);
  const lastNodeKeyRef = useRef<string>('');

  const [canvasSize, setCanvasSize] = useState<{ width: number; height: number }>({
    width: 860,
    height: 600,
  });
  const [viewport, setViewport] = useState<OntologyViewport>({ panX: 0, panY: 0, zoom: 0.85 });
  const [focusedKind, setFocusedKind] = useState<string | null>(null);
  const [hoveredKind, setHoveredKind] = useState<string | null>(null);
  const [hoveredCapsule, setHoveredCapsule] = useState<string | null>(null);
  const [isDragging, setIsDragging] = useState<boolean>(false);
  const [spacingFactor, setSpacingFactor] = useState<number>(1.0);
  const [hubFactor, setHubFactor] = useState<number>(1.0);
  const [isFrozen, setIsFrozen] = useState<boolean>(false);

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
        onlyActiveInWorkspace,
        redirectMap
      ),
    [schema, visibleKindSet, activeCategories, workspace, onlyActiveInWorkspace, redirectMap]
  );

  const capsules = useMemo(
    () => buildOntologyGraphCapsules(schema, nodesRef.current, expandedHierarchies),
    [schema, expandedHierarchies]
  );

  const renderStateRef = useRef({
    canvasSize,
    graphEdges,
    capsules,
    selectedKind,
    focusedKind,
    hoveredKind,
    hoveredCapsule,
    viewport,
  });
  renderStateRef.current = {
    canvasSize,
    graphEdges,
    capsules,
    selectedKind,
    focusedKind,
    hoveredKind,
    hoveredCapsule,
    viewport,
  };

  const paintCanvas = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const state = renderStateRef.current;
    const dpr = typeof window !== 'undefined' ? window.devicePixelRatio || 1 : 1;
    const targetW = Math.max(1, Math.floor(state.canvasSize.width * dpr));
    const targetH = Math.max(1, Math.floor(state.canvasSize.height * dpr));
    if (canvas.width !== targetW) canvas.width = targetW;
    if (canvas.height !== targetH) canvas.height = targetH;
    const ctx = canvas.getContext(CONTEXT_2D);
    if (!ctx) return;

    const currentNodes = nodesRef.current;
    const nodeByKind = new Map<string, OntologyGraphNode>();
    for (const n of currentNodes) {
      nodeByKind.set(n.kind, n);
    }

    renderOntologyCanvas(ctx, {
      nodes: currentNodes,
      edges: state.graphEdges,
      capsules: state.capsules,
      nodeByKind,
      selectedKind: state.selectedKind,
      focusedKind: state.focusedKind,
      hoveredKind: state.hoveredKind,
      hoveredCapsule: state.hoveredCapsule,
      viewport: state.viewport,
      width: state.canvasSize.width,
      height: state.canvasSize.height,
      dpr,
    });
  }, []);

  const ensureSimulationLoop = useCallback(() => {
    if (rafRef.current !== null || typeof window === 'undefined') return;
    const step = () => {
      rafRef.current = null;
      const sim = simRef.current;
      if (!sim) return;
      const keepRunning = sim.tick();
      nodesRef.current = sim.getNodes();
      paintCanvas();
      if (keepRunning) {
        rafRef.current = window.requestAnimationFrame(step);
      }
    };
    rafRef.current = window.requestAnimationFrame(step);
  }, [paintCanvas]);

  useEffect(
    () => () => {
      if (rafRef.current !== null && typeof window !== 'undefined') {
        window.cancelAnimationFrame(rafRef.current);
        rafRef.current = null;
      }
    },
    []
  );

  useEffect(() => {
    const sim = simRef.current;
    if (!sim) return;
    nodesRef.current = sim.sync(
      schema,
      visibleNodes,
      activeCategories,
      workspace,
      redirectMap,
      collapsedCounts
    );
    paintCanvas();
    if (sim.isRunning()) {
      ensureSimulationLoop();
    }
  }, [
    schema,
    visibleNodes,
    activeCategories,
    workspace,
    redirectMap,
    collapsedCounts,
    paintCanvas,
    ensureSimulationLoop,
  ]);

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
    if (canvasSize.width <= 100 || visibleNodes.length === 0) return;
    const nodeKey = visibleNodes.map((n) => n.kind).join(',');
    if (!initializedFitRef.current || nodeKey !== lastNodeKeyRef.current) {
      initializedFitRef.current = true;
      lastNodeKeyRef.current = nodeKey;
      const fitBoundsNodes = buildOntologyGraphLayout(schema, visibleNodes, workspace, collapsedCounts);
      setViewport(computeFitViewport(fitBoundsNodes, canvasSize.width, canvasSize.height, capsules));
    }
  }, [canvasSize.width, canvasSize.height, schema, visibleNodes, workspace, collapsedCounts, capsules]);

  useEffect(() => {
    paintCanvas();
  }, [
    canvasSize,
    graphEdges,
    capsules,
    selectedKind,
    focusedKind,
    hoveredKind,
    hoveredCapsule,
    viewport,
    paintCanvas,
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
      const currentNodes = nodesRef.current;
      const capHit = hitTestOntologyCapsuleHeader(capsules, currentNodes, viewport, ox, oy);
      const hit = capHit ? null : hitTestOntologyNode(currentNodes, viewport, ox, oy);

      canvas.setPointerCapture(e.pointerId);
      setIsDragging(true);

      if (capHit) {
        dragRef.current = {
          mode: 'capsule',
          nodeKind: null,
          capsuleKind: capHit,
          startClientX: e.clientX,
          startClientY: e.clientY,
          originX: viewport.panX,
          originY: viewport.panY,
          moved: false,
        };
      } else if (hit) {
        dragRef.current = {
          mode: 'node',
          nodeKind: hit.kind,
          capsuleKind: null,
          startClientX: e.clientX,
          startClientY: e.clientY,
          originX: hit.x,
          originY: hit.y,
          moved: false,
        };
        simRef.current?.startDrag(hit.kind);
        ensureSimulationLoop();
      } else {
        dragRef.current = {
          mode: 'pan',
          nodeKind: null,
          capsuleKind: null,
          startClientX: e.clientX,
          startClientY: e.clientY,
          originX: viewport.panX,
          originY: viewport.panY,
          moved: false,
        };
      }
    },
    [capsules, viewport, ensureSimulationLoop]
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
        if (drag.mode === 'pan' || (drag.mode === 'capsule' && drag.moved)) {
          setViewport((prev) => ({
            ...prev,
            panX: drag.originX + dx,
            panY: drag.originY + dy,
          }));
        } else if (drag.mode === 'node' && drag.nodeKind) {
          const nextX = drag.originX + dx / viewport.zoom;
          const nextY = drag.originY + dy / viewport.zoom;
          const sim = simRef.current;
          if (sim) {
            sim.moveDrag(drag.nodeKind, nextX, nextY);
            nodesRef.current = sim.getNodes();
            paintCanvas();
            if (sim.isRunning()) {
              ensureSimulationLoop();
            }
          }
        }
        return;
      }

      const rect = canvas.getBoundingClientRect();
      const ox = e.clientX - rect.left;
      const oy = e.clientY - rect.top;
      const currentNodes = nodesRef.current;
      const capHit = hitTestOntologyCapsuleHeader(capsules, currentNodes, viewport, ox, oy);
      setHoveredCapsule(capHit);
      if (capHit) {
        setHoveredKind(null);
        return;
      }
      const hit = hitTestOntologyNode(currentNodes, viewport, ox, oy);
      setHoveredKind(hit ? hit.kind : null);
    },
    [capsules, viewport, paintCanvas, ensureSimulationLoop]
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

      if (drag?.mode === 'node' && drag.nodeKind) {
        simRef.current?.endDrag(drag.nodeKind);
        if (simRef.current?.isRunning()) {
          ensureSimulationLoop();
        }
      }

      if (!drag || drag.moved) return;

      if (drag.mode === 'capsule' && drag.capsuleKind) {
        onToggleHierarchy?.(drag.capsuleKind);
      } else if (drag.mode === 'node' && drag.nodeKind) {
        onSelectKind(drag.nodeKind);
        setFocusedKind((prev) => (prev === drag.nodeKind ? null : drag.nodeKind));
      } else {
        setFocusedKind(null);
      }
    },
    [onSelectKind, onToggleHierarchy, ensureSimulationLoop]
  );

  const handleSpacingChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const next = parseFloat(e.target.value) || 1.0;
      setSpacingFactor(next);
      const sim = simRef.current;
      if (sim) {
        sim.setSpacingFactor(next);
        if (sim.isRunning()) {
          ensureSimulationLoop();
        }
      }
    },
    [ensureSimulationLoop]
  );

  const handleHubChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const parsed = parseFloat(e.target.value);
      const next = Number.isFinite(parsed) ? parsed : 1.0;
      setHubFactor(next);
      const sim = simRef.current;
      if (sim) {
        sim.setHubFactor(next);
        if (sim.isRunning()) {
          ensureSimulationLoop();
        }
      }
    },
    [ensureSimulationLoop]
  );

  const handleToggleFreeze = useCallback(() => {
    setIsFrozen((prev) => {
      const next = !prev;
      const sim = simRef.current;
      if (sim) {
        sim.setFrozen(next);
        if (sim.isRunning()) {
          ensureSimulationLoop();
        }
      }
      return next;
    });
  }, [ensureSimulationLoop]);

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
    const sim = simRef.current;
    if (sim) {
      nodesRef.current = sim.resetLayout();
    }
    setFocusedKind(null);
    setViewport(computeFitViewport(nodesRef.current, canvasSize.width, canvasSize.height, capsules));
    paintCanvas();
  }, [capsules, canvasSize.width, canvasSize.height, paintCanvas]);

  const cursorStyle = isDragging
    ? CURSOR_GRABBING
    : hoveredKind || hoveredCapsule
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
          if (!dragRef.current) {
            setHoveredKind(null);
            setHoveredCapsule(null);
          }
        }}
      />

      <OntologyGraphLegend />
      <OntologyGraphControls
        focusedKind={focusedKind}
        spacingFactor={spacingFactor}
        hubFactor={hubFactor}
        isFrozen={isFrozen}
        onClearFocus={() => setFocusedKind(null)}
        onSpacingChange={handleSpacingChange}
        onHubChange={handleHubChange}
        onToggleFreeze={handleToggleFreeze}
        onZoomStep={handleZoomStep}
        onFitReset={handleFitReset}
      />
    </div>
  );
};
