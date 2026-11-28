import React, { useEffect, useRef, useState, useCallback } from 'react';
import { SelectedNodeDetail } from '../domain/types';

export type Orbital3DMode = 'echelon' | 'gyro' | 'sphere' | 'flat2d';

export interface UniversalGraph3DNodeInput {
  id: string;
  name: string;
  displayLabel: string;
  kind: string;
  echelonTier: number;
  size: number;
  bgColor: string;
  borderColor: string;
  x2d: number;
  y2d: number;
}

export interface UniversalGraph3DEdgeInput {
  id: string;
  source: string;
  target: string;
  category: string;
  label: string;
  count: number;
}

export interface UniversalGraph3DOrbitInput {
  levelIndex: number;
  radius: number;
  shortLabel: string;
  title: string;
  color: string;
}

export interface UniversalGraph3DCanvasProps {
  nodes: UniversalGraph3DNodeInput[];
  edges: UniversalGraph3DEdgeInput[];
  orbits: UniversalGraph3DOrbitInput[];
  selectedNodeId: string | null;
  multiSelectedNodeIds: Set<string>;
  searchMatchNodeIds: Set<string> | null;
  fontSize: number;
  onSelectNode: (nodeId: string | null, isMultiKey: boolean) => void;
  onContextMenuNode: (x: number, y: number, nodeId: string) => void;
  onDoubleClickNode: (nodeId: string) => void;
  detailMap: Map<string, SelectedNodeDetail>;
}

interface Internal3DNode extends UniversalGraph3DNodeInput {
  ringOrder: number;
  radius: number;
  angle: number;
  baseZ: number;
  incX: number;
  incY: number;
  x: number;
  y: number;
  z: number;
  tx: number;
  ty: number;
  tz: number;
  sx: number;
  sy: number;
  sz: number;
  scale: number;
}

interface Internal3DOrbit extends UniversalGraph3DOrbitInput {
  ringOrder: number;
  baseZ: number;
  incX: number;
  incY: number;
}

const GYRO_INCLINATIONS: Array<{ incX: number; incY: number }> = [
  { incX: 0.0, incY: 0.0 },
  { incX: 0.56, incY: -0.22 },
  { incX: -0.46, incY: 0.34 },
  { incX: 0.38, incY: 0.52 },
  { incX: -0.32, incY: -0.58 },
  { incX: 0.48, incY: -0.44 },
  { incX: -0.52, incY: 0.26 },
];

const FOV = 1400;
const MIN_ZOOM = 0.05;
const MAX_ZOOM = 8.0;
const MIN_PITCH = -1.52;
const MAX_PITCH = 1.52;
const STORAGE_KEY_INVERT_Y = 'ce_3d_invert_y';
const DEFAULT_ORBITAL_MODE: Orbital3DMode = 'echelon';
const INITIAL_CAMERA_READOUT = 'Pitch: 47° | Yaw: -26° | Zoom: 1.00x';
const CANVAS_CONTEXT_2D = '2d';
const EVENT_RESIZE = 'resize';
const EVENT_MOUSEDOWN = 'mousedown';
const EVENT_MOUSEMOVE = 'mousemove';
const EVENT_MOUSEUP = 'mouseup';
const EVENT_DBLCLICK = 'dblclick';
const EVENT_WHEEL = 'wheel';
const EVENT_CONTEXTMENU = 'contextmenu';
const BG_GRAD_INNER = '#172033';
const BG_GRAD_OUTER = '#0b0f19';
const DEFAULT_ACCENT_COLOR = '#38bdf8';
const SPHERE_HIGHLIGHT_COLOR = '#ffffff';
const WHEEL_LISTENER_OPTIONS: AddEventListenerOptions = { passive: false };

function resolveEdgeStrokeColor(category: string | undefined, fallbackColor: string): string {
  switch (category) {
    case 'service_call':
    case 'library':
      return '#34d399';
    case 'database':
      return '#c084fc';
    case 'messaging':
      return '#fbbf24';
    case 'external':
      return '#38bdf8';
    default:
      return fallbackColor || DEFAULT_ACCENT_COLOR;
  }
}

interface Orbital3DSceneState {
  mode: Orbital3DMode;
  depthScale: number;
  autoRotate: boolean;
  invertVertical: boolean;
  camYaw: number;
  camPitch: number;
  targetYaw: number;
  targetPitch: number;
  camZoom: number;
  targetZoom: number;
  panX: number;
  panY: number;
  targetPanX: number;
  targetPanY: number;
  focusOrigin: { x: number; y: number; z: number };
  targetFocusOrigin: { x: number; y: number; z: number };
  hoveredNodeId: string | null;
  selectedNodeId: string | null;
  multiSelectedNodeIds: Set<string>;
  searchMatchNodeIds: Set<string> | null;
  fontSize: number;
  internalNodes: Internal3DNode[];
  internalOrbits: Internal3DOrbit[];
  nodeMap: Map<string, Internal3DNode>;
  edges: UniversalGraph3DEdgeInput[];
}

function projectOrbital3DPoint(
  st: Orbital3DSceneState,
  x: number,
  y: number,
  z: number,
  width: number,
  height: number
) {
  const rx = x - st.focusOrigin.x;
  const ry = y - st.focusOrigin.y;
  const rz = z - st.focusOrigin.z;

  const cosY = Math.cos(st.camYaw);
  const sinY = Math.sin(st.camYaw);
  const x1 = rx * cosY - ry * sinY;
  const y1 = rx * sinY + ry * cosY;
  const z1 = rz;

  const cosP = Math.cos(st.camPitch);
  const sinP = Math.sin(st.camPitch);
  const y2 = y1 * cosP - z1 * sinP;
  const z2 = y1 * sinP + z1 * cosP;

  const depth = FOV / Math.max(160, FOV - z2);
  const scale = depth * st.camZoom;

  return {
    sx: width / 2 + st.panX + x1 * scale,
    sy: height / 2 + st.panY + y2 * scale,
    sz: z2,
    scale,
  };
}

function renderOrbital3DFrame(
  ctx: CanvasRenderingContext2D,
  canvas: HTMLCanvasElement,
  st: Orbital3DSceneState,
  now: number,
  lastReadoutRef: { value: number },
  onReadout: (text: string) => void
): void {
  const width = canvas.clientWidth || 900;
  const height = canvas.clientHeight || 650;

  const bgGrad = ctx.createRadialGradient(
    width * 0.5,
    height * 0.45,
    40,
    width * 0.5,
    height * 0.45,
    Math.max(width, height) * 0.75
  );
  bgGrad.addColorStop(0, BG_GRAD_INNER);
  bgGrad.addColorStop(1, BG_GRAD_OUTER);
  ctx.fillStyle = bgGrad;
  ctx.fillRect(0, 0, width, height);

  if (st.autoRotate) {
    st.targetYaw += 0.0022;
  }

  st.camYaw += (st.targetYaw - st.camYaw) * 0.14;
  st.camPitch += (st.targetPitch - st.camPitch) * 0.14;
  st.camZoom += (st.targetZoom - st.camZoom) * 0.14;
  st.panX += (st.targetPanX - st.panX) * 0.14;
  st.panY += (st.targetPanY - st.panY) * 0.14;
  st.focusOrigin.x += (st.targetFocusOrigin.x - st.focusOrigin.x) * 0.12;
  st.focusOrigin.y += (st.targetFocusOrigin.y - st.focusOrigin.y) * 0.12;
  st.focusOrigin.z += (st.targetFocusOrigin.z - st.focusOrigin.z) * 0.12;

  if (now - lastReadoutRef.value > 90) {
    lastReadoutRef.value = now;
    const pitchDeg = Math.round((st.camPitch * 180) / Math.PI);
    const yawDeg = Math.round(((st.camYaw * 180) / Math.PI) % 360);
    onReadout(`Pitch: ${pitchDeg}° | Yaw: ${yawDeg}° | Zoom: ${st.camZoom.toFixed(2)}x`);
  }

  for (const n of st.internalNodes) {
    n.x += (n.tx - n.x) * 0.14;
    n.y += (n.ty - n.y) * 0.14;
    n.z += (n.tz - n.z) * 0.14;
    const p = projectOrbital3DPoint(st, n.x, n.y, n.z, width, height);
    n.sx = p.sx;
    n.sy = p.sy;
    n.sz = p.sz;
    n.scale = p.scale;
  }

  if (Math.abs(st.camPitch) > 0.05 && st.mode === 'echelon' && st.internalOrbits.length > 1) {
    const zValues = st.internalOrbits.map((o) => o.baseZ);
    const maxZ = (Math.max(0, ...zValues) + 60) * st.depthScale;
    const minZ = (Math.min(0, ...zValues) - 60) * st.depthScale;
    const topP = projectOrbital3DPoint(st, 0, 0, maxZ, width, height);
    const botP = projectOrbital3DPoint(st, 0, 0, minZ, width, height);
    ctx.beginPath();
    ctx.moveTo(topP.sx, topP.sy);
    ctx.lineTo(botP.sx, botP.sy);
    ctx.strokeStyle = 'rgba(56, 189, 248, 0.55)';
    ctx.lineWidth = 2.2;
    ctx.setLineDash([4, 5]);
    ctx.stroke();
    ctx.setLineDash([]);
  }

  if (st.mode !== 'sphere') {
    const steps = 84;
    for (const orb of st.internalOrbits) {
      if (orb.radius <= 5) continue;
      ctx.beginPath();
      for (let i = 0; i <= steps; i++) {
        const a = (i / steps) * Math.PI * 2;
        let x = Math.cos(a) * orb.radius;
        let y = Math.sin(a) * orb.radius;
        let z = 0;

        if (st.mode === 'echelon') {
          z = orb.baseZ * st.depthScale;
        } else if (st.mode === 'gyro') {
          const ix = orb.incX * st.depthScale;
          const iy = orb.incY * st.depthScale;
          const y1 = y * Math.cos(ix);
          const z1 = y * Math.sin(ix);
          const x2 = x * Math.cos(iy) + z1 * Math.sin(iy);
          const z2 = -x * Math.sin(iy) + z1 * Math.cos(iy);
          x = x2;
          y = y1;
          z = z2;
        }

        const p = projectOrbital3DPoint(st, x, y, z, width, height);
        if (i === 0) ctx.moveTo(p.sx, p.sy);
        else ctx.lineTo(p.sx, p.sy);
      }
      ctx.strokeStyle = 'rgba(56, 189, 248, 0.52)';
      ctx.lineWidth = 2.0;
      ctx.setLineDash([5, 6]);
      ctx.stroke();
      ctx.setLineDash([]);
    }
  }

  const activeCenterId = st.hoveredNodeId || st.selectedNodeId;
  const hasSearch = st.searchMatchNodeIds !== null;
  const showAllMode = !activeCenterId && !hasSearch;
  const connectedNodeIds = new Set<string>();
  const activeEdgeIds = new Set<string>();

  if (activeCenterId) {
    connectedNodeIds.add(activeCenterId);
    for (const e of st.edges) {
      if (e.source === activeCenterId || e.target === activeCenterId) {
        connectedNodeIds.add(e.source);
        connectedNodeIds.add(e.target);
        activeEdgeIds.add(e.id);
      }
    }
  } else if (hasSearch && st.searchMatchNodeIds) {
    for (const id of st.searchMatchNodeIds) connectedNodeIds.add(id);
    for (const e of st.edges) {
      if (st.searchMatchNodeIds.has(e.source) || st.searchMatchNodeIds.has(e.target)) {
        activeEdgeIds.add(e.id);
      }
    }
  } else {
    for (const n of st.internalNodes) connectedNodeIds.add(n.id);
    for (const e of st.edges) activeEdgeIds.add(e.id);
  }

  const sortedEdges = [...st.edges].sort((a, b) => {
    const ua = st.nodeMap.get(a.source);
    const va = st.nodeMap.get(a.target);
    const ub = st.nodeMap.get(b.source);
    const vb = st.nodeMap.get(b.target);
    const za = ua && va ? (ua.sz + va.sz) * 0.5 : 0;
    const zb = ub && vb ? (ub.sz + vb.sz) * 0.5 : 0;
    return za - zb;
  });

  for (const e of sortedEdges) {
    const u = st.nodeMap.get(e.source);
    const v = st.nodeMap.get(e.target);
    if (!u || !v) continue;

    const isHighlighted = activeEdgeIds.has(e.id);
    const avgZ = (u.sz + v.sz) * 0.5;
    const depthAlpha = Math.max(0.68, Math.min(1, (avgZ + 950) / 1150));

    const midX = (u.sx + v.sx) * 0.5;
    const midY = (u.sy + v.sy) * 0.5 - (isHighlighted ? 12 : 6) * Math.max(0.65, u.scale);

    ctx.beginPath();
    ctx.moveTo(u.sx, u.sy);
    ctx.quadraticCurveTo(midX, midY, v.sx, v.sy);

    const edgeColor = resolveEdgeStrokeColor(e.category, v.bgColor || DEFAULT_ACCENT_COLOR);
    const effectiveScale = Math.min(1.45, Math.max(0.78, u.scale));
    if (showAllMode) {
      ctx.save();
      ctx.globalAlpha = Math.max(0.72, 0.88 * depthAlpha);
      ctx.strokeStyle = edgeColor;
      ctx.lineWidth = Math.max(2.0, 2.5 * effectiveScale);
      ctx.stroke();
      ctx.restore();
    } else if (isHighlighted) {
      ctx.save();
      ctx.globalAlpha = 1.0;
      ctx.strokeStyle = edgeColor;
      ctx.lineWidth = Math.max(2.8, 3.6 * effectiveScale);
      ctx.stroke();
      ctx.restore();
    } else {
      ctx.strokeStyle = `rgba(148, 163, 184, ${0.24 * depthAlpha})`;
      ctx.lineWidth = Math.max(1.2, 1.5 * effectiveScale);
      ctx.stroke();
    }

    if (isHighlighted || showAllMode) {
      const targetR = Math.max(7, v.size * 0.36 * v.scale);
      const dx = v.sx - midX;
      const dy = v.sy - midY;
      const len = Math.hypot(dx, dy) || 1;
      if (len > targetR + 6) {
        const ux = dx / len;
        const uy = dy / len;
        const tipX = v.sx - ux * (targetR + 2);
        const tipY = v.sy - uy * (targetR + 2);
        const arrowSize = Math.max(
          6.5,
          Math.min(11.5, (isHighlighted ? 9.5 : 7.5) * Math.max(0.78, v.scale))
        );

        ctx.save();
        ctx.globalAlpha = showAllMode ? Math.max(0.82, 0.94 * depthAlpha) : 1.0;
        ctx.fillStyle = edgeColor;
        ctx.beginPath();
        ctx.moveTo(tipX, tipY);
        ctx.lineTo(
          tipX - ux * arrowSize - uy * (arrowSize * 0.58),
          tipY - uy * arrowSize + ux * (arrowSize * 0.58)
        );
        ctx.lineTo(
          tipX - ux * arrowSize + uy * (arrowSize * 0.58),
          tipY - uy * arrowSize - ux * (arrowSize * 0.58)
        );
        ctx.closePath();
        ctx.fill();
        ctx.restore();
      }
    }
  }

  const sortedNodes = [...st.internalNodes].sort((a, b) => a.sz - b.sz);

  for (const n of sortedNodes) {
    const isSelected = n.id === st.selectedNodeId;
    const isMulti = st.multiSelectedNodeIds.has(n.id);
    const isHovered = n.id === st.hoveredNodeId;
    const isConnected = connectedNodeIds.has(n.id);
    const depthAlpha = Math.max(0.78, Math.min(1, (n.sz + 950) / 1150));
    const alpha = showAllMode
      ? Math.max(0.9, depthAlpha)
      : isConnected
      ? 1.0
      : 0.36 * depthAlpha;

    const baseRadius = Math.max(9, n.size * 0.38);
    const r = Math.max(6.5, baseRadius * n.scale) * (isHovered ? 1.18 : 1.0);

    ctx.save();
    ctx.globalAlpha = alpha;

    const nodeFillColor = n.bgColor || DEFAULT_ACCENT_COLOR;
    ctx.beginPath();
    ctx.arc(
      n.sx,
      n.sy,
      r + (isSelected || isHovered || isMulti || n.ringOrder === 0 ? 8 : 3.5) * Math.max(0.7, n.scale),
      0,
      Math.PI * 2
    );
    ctx.fillStyle = isMulti
      ? 'rgba(56, 189, 248, 0.45)'
      : isSelected || isHovered || n.ringOrder === 0
      ? `${nodeFillColor}66`
      : `${nodeFillColor}28`;
    ctx.fill();

    const grad = ctx.createRadialGradient(
      n.sx - r * 0.32,
      n.sy - r * 0.32,
      r * 0.1,
      n.sx,
      n.sy,
      r
    );
    grad.addColorStop(0, SPHERE_HIGHLIGHT_COLOR);
    grad.addColorStop(0.45, nodeFillColor);
    grad.addColorStop(1, n.borderColor || nodeFillColor);

    ctx.beginPath();
    ctx.arc(n.sx, n.sy, r, 0, Math.PI * 2);
    ctx.fillStyle = grad;
    ctx.fill();

    ctx.lineWidth = isSelected || isMulti ? 3.0 : isHovered ? 2.4 : 1.6;
    ctx.strokeStyle = isSelected
      ? SPHERE_HIGHLIGHT_COLOR
      : isMulti || isHovered
      ? DEFAULT_ACCENT_COLOR
      : 'rgba(255, 255, 255, 0.62)';
    ctx.stroke();

    if (isConnected || n.scale > 0.38) {
      const labelFontSize = Math.max(
        9,
        Math.min(16, Math.round((st.fontSize + 1) * Math.pow(Math.max(0.65, n.scale), 0.55)))
      );
      ctx.font = `600 ${labelFontSize}px system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`;
      const text = n.displayLabel || n.name;
      const textWidth = ctx.measureText(text).width;
      const padX = 6;
      const padY = 3;
      const boxW = textWidth + padX * 2;
      const boxH = labelFontSize + padY * 2;
      const boxX = n.sx - boxW / 2;
      const boxY = n.sy + r + 5;

      ctx.fillStyle =
        isSelected || isHovered ? 'rgba(15, 23, 42, 0.96)' : 'rgba(11, 16, 28, 0.9)';
      ctx.strokeStyle =
        isSelected || isHovered ? nodeFillColor : 'rgba(255, 255, 255, 0.24)';
      ctx.lineWidth = 1.2;

      ctx.beginPath();
      if (typeof ctx.roundRect === 'function') {
        ctx.roundRect(boxX, boxY, boxW, boxH, 4);
      } else {
        ctx.rect(boxX, boxY, boxW, boxH);
      }
      ctx.fill();
      ctx.stroke();

      ctx.fillStyle = '#f8fafc';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText(text, n.sx, boxY + boxH / 2 + 0.5);
    }

    ctx.restore();
  }
}

export const UniversalGraph3DCanvas: React.FC<UniversalGraph3DCanvasProps> = ({
  nodes,
  edges,
  orbits,
  selectedNodeId,
  multiSelectedNodeIds,
  searchMatchNodeIds,
  fontSize,
  onSelectNode,
  onContextMenuNode,
  onDoubleClickNode,
}) => {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const [mode, setMode] = useState<Orbital3DMode>(DEFAULT_ORBITAL_MODE);
  const [depthPercent, setDepthPercent] = useState<number>(100);
  const [autoRotate, setAutoRotate] = useState<boolean>(false);
  const [invertVertical, setInvertVertical] = useState<boolean>(() => {
    try {
      return localStorage.getItem(STORAGE_KEY_INVERT_Y) === 'true';
    } catch {
      return false;
    }
  });
  const [cameraReadout, setCameraReadout] = useState<string>(INITIAL_CAMERA_READOUT);

  const stateRef = useRef<Orbital3DSceneState>({
    mode: DEFAULT_ORBITAL_MODE,
    depthScale: 1.0,
    autoRotate: false,
    invertVertical: false,
    camYaw: -0.45,
    camPitch: 0.82,
    targetYaw: -0.45,
    targetPitch: 0.82,
    camZoom: 0.92,
    targetZoom: 0.92,
    panX: 0,
    panY: 0,
    targetPanX: 0,
    targetPanY: 0,
    focusOrigin: { x: 0, y: 0, z: 0 },
    targetFocusOrigin: { x: 0, y: 0, z: 0 },
    hoveredNodeId: null,
    selectedNodeId: null,
    multiSelectedNodeIds: new Set<string>(),
    searchMatchNodeIds: null,
    fontSize: 10,
    internalNodes: [],
    internalOrbits: [],
    nodeMap: new Map<string, Internal3DNode>(),
    edges: [],
  });

  stateRef.current.selectedNodeId = selectedNodeId;
  stateRef.current.multiSelectedNodeIds = multiSelectedNodeIds;
  stateRef.current.searchMatchNodeIds = searchMatchNodeIds;
  stateRef.current.fontSize = fontSize;
  stateRef.current.edges = edges;
  stateRef.current.invertVertical = invertVertical;

  const computeTargets = useCallback(() => {
    const st = stateRef.current;
    const list = st.internalNodes;
    const N = list.length || 1;
    const maxRadius = Math.max(
      260,
      ...st.internalOrbits.map((o) => o.radius),
      ...list.map((n) => n.radius)
    );
    const sphereR = Math.max(240, maxRadius * 0.72);

    list.forEach((n, idx) => {
      if (st.mode === 'flat2d') {
        n.tx = Math.cos(n.angle) * n.radius;
        n.ty = Math.sin(n.angle) * n.radius;
        n.tz = 0;
      } else if (st.mode === 'echelon') {
        n.tx = Math.cos(n.angle) * n.radius;
        n.ty = Math.sin(n.angle) * n.radius;
        n.tz = n.baseZ * st.depthScale;
      } else if (st.mode === 'gyro') {
        const x0 = Math.cos(n.angle) * n.radius;
        const y0 = Math.sin(n.angle) * n.radius;
        const ix = n.incX * st.depthScale;
        const iy = n.incY * st.depthScale;
        const y1 = y0 * Math.cos(ix);
        const z1 = y0 * Math.sin(ix);
        const x2 = x0 * Math.cos(iy) + z1 * Math.sin(iy);
        const z2 = -x0 * Math.sin(iy) + z1 * Math.cos(iy);
        n.tx = x2;
        n.ty = y1;
        n.tz = z2;
      } else if (st.mode === 'sphere') {
        const phi = Math.acos(1 - (2 * (idx + 0.5)) / N);
        const theta = Math.PI * (1 + Math.sqrt(5)) * idx;
        n.tx = sphereR * Math.sin(phi) * Math.cos(theta);
        n.ty = sphereR * Math.sin(phi) * Math.sin(theta);
        n.tz = sphereR * Math.cos(phi) * st.depthScale;
      }
    });
  }, []);

  // Rebuild 3D nodes & orbits whenever visible 2D layout changes
  useEffect(() => {
    const st = stateRef.current;
    const sortedOrbits = [...orbits].sort((a, b) => a.radius - b.radius);
    const orbitOrderMap = new Map<number, number>();
    sortedOrbits.forEach((o, idx) => {
      orbitOrderMap.set(o.levelIndex, idx);
    });

    const totalOrbits = Math.max(1, sortedOrbits.length);
    const midOrder = (totalOrbits - 1) / 2;
    const zStep = totalOrbits <= 3 ? 135 : 105;

    const internalOrbits: Internal3DOrbit[] = sortedOrbits.map((o, idx) => {
      const gyro = GYRO_INCLINATIONS[idx % GYRO_INCLINATIONS.length];
      const baseZ = Math.round((midOrder - idx) * zStep);
      return {
        ...o,
        ringOrder: idx,
        baseZ,
        incX: gyro.incX,
        incY: gyro.incY,
      };
    });

    const orbitByLevel = new Map<number, Internal3DOrbit>();
    internalOrbits.forEach((o) => orbitByLevel.set(o.levelIndex, o));

    const prevMap = st.nodeMap;
    const nextMap = new Map<string, Internal3DNode>();

    const internalNodes: Internal3DNode[] = nodes.map((n) => {
      const r2d = Math.hypot(n.x2d, n.y2d);
      const a2d = Math.atan2(n.y2d, n.x2d);
      const matchedOrbit = orbitByLevel.get(n.echelonTier);
      const ringOrder = matchedOrbit?.ringOrder ?? 0;
      const gyro = GYRO_INCLINATIONS[ringOrder % GYRO_INCLINATIONS.length];
      const baseZ = matchedOrbit ? matchedOrbit.baseZ : 0;
      const prev = prevMap.get(n.id);

      const item: Internal3DNode = {
        ...n,
        ringOrder,
        radius: r2d,
        angle: a2d,
        baseZ,
        incX: matchedOrbit ? matchedOrbit.incX : gyro.incX,
        incY: matchedOrbit ? matchedOrbit.incY : gyro.incY,
        x: prev ? prev.x : n.x2d,
        y: prev ? prev.y : n.y2d,
        z: prev ? prev.z : baseZ * st.depthScale,
        tx: n.x2d,
        ty: n.y2d,
        tz: baseZ * st.depthScale,
        sx: 0,
        sy: 0,
        sz: 0,
        scale: 1,
      };
      nextMap.set(n.id, item);
      return item;
    });

    st.internalOrbits = internalOrbits;
    st.internalNodes = internalNodes;
    st.nodeMap = nextMap;
    computeTargets();

    // Auto-fit initial zoom to graph radius on first load
    const maxR = Math.max(300, ...internalNodes.map((n) => n.radius));
    const canvas = canvasRef.current;
    if (canvas && prevMap.size === 0 && internalNodes.length > 0) {
      const rect = canvas.getBoundingClientRect();
      const minDim = Math.max(360, Math.min(rect.width || 900, rect.height || 650));
      const desiredZoom = Math.max(MIN_ZOOM, Math.min(2.5, (minDim * 0.44) / maxR));
      st.camZoom = desiredZoom;
      st.targetZoom = desiredZoom;
    }
  }, [nodes, orbits, computeTargets]);

  const handleModeChange = (nextMode: Orbital3DMode) => {
    setMode(nextMode);
    const st = stateRef.current;
    st.mode = nextMode;
    if (nextMode === 'flat2d') {
      st.targetPitch = 0;
    } else if (Math.abs(st.targetPitch) < 0.25) {
      st.targetPitch = 0.82;
    }
    computeTargets();
  };

  const handleDepthChange = (val: number) => {
    setDepthPercent(val);
    stateRef.current.depthScale = val / 100;
    computeTargets();
  };

  const handleToggleAutoRotate = () => {
    setAutoRotate((prev) => {
      const next = !prev;
      stateRef.current.autoRotate = next;
      return next;
    });
  };

  const handleToggleInvertVertical = (checked: boolean) => {
    setInvertVertical(checked);
    stateRef.current.invertVertical = checked;
    try {
      localStorage.setItem(STORAGE_KEY_INVERT_Y, String(checked));
    } catch {
      // ignore storage failure
    }
  };

  const handleResetCamera = () => {
    const st = stateRef.current;
    st.targetYaw = -0.45;
    st.targetPitch = st.mode === 'flat2d' ? 0 : 0.82;
    const maxR = Math.max(300, ...st.internalNodes.map((n) => n.radius));
    const rect = canvasRef.current?.getBoundingClientRect();
    const minDim = Math.max(360, Math.min(rect?.width || 900, rect?.height || 650));
    st.targetZoom = Math.max(MIN_ZOOM, Math.min(2.5, (minDim * 0.44) / maxR));
    st.targetPanX = 0;
    st.targetPanY = 0;
    st.targetFocusOrigin = { x: 0, y: 0, z: 0 };
  };

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext(CANVAS_CONTEXT_2D);
    if (!ctx) return;

    let animId = 0;
    let isDragging = false;
    let isPanning = false;
    let dragStartX = 0;
    let dragStartY = 0;
    let dragMoved = false;
    const lastReadoutRef = { value: 0 };

    const resize = () => {
      const rect = canvas.parentElement?.getBoundingClientRect();
      if (!rect) return;
      const dpr = window.devicePixelRatio || 1;
      canvas.width = Math.max(1, Math.floor(rect.width * dpr));
      canvas.height = Math.max(1, Math.floor(rect.height * dpr));
      canvas.style.width = `${rect.width}px`;
      canvas.style.height = `${rect.height}px`;
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    };

    resize();
    const ro = typeof ResizeObserver !== 'undefined' ? new ResizeObserver(resize) : null;
    if (ro && canvas.parentElement) ro.observe(canvas.parentElement);
    window.addEventListener(EVENT_RESIZE, resize);

    const findHitNode = (mx: number, my: number): Internal3DNode | null => {
      const sorted = [...stateRef.current.internalNodes].sort((a, b) => b.sz - a.sz);
      for (const n of sorted) {
        const r = Math.max(8, n.size * 0.36 * n.scale) + 5;
        const dx = mx - n.sx;
        const dy = my - n.sy;
        if (dx * dx + dy * dy <= r * r) {
          return n;
        }
      }
      return null;
    };

    const onMouseDown = (e: MouseEvent) => {
      if (e.button === 2) {
        const rect = canvas.getBoundingClientRect();
        const hit = findHitNode(e.clientX - rect.left, e.clientY - rect.top);
        if (hit) {
          e.preventDefault();
          onContextMenuNode(e.clientX - rect.left, e.clientY - rect.top, hit.id);
          return;
        }
      }
      isDragging = true;
      isPanning = e.button === 2 || e.button === 1 || e.shiftKey;
      dragStartX = e.clientX;
      dragStartY = e.clientY;
      dragMoved = false;
    };

    const onMouseMove = (e: MouseEvent) => {
      const st = stateRef.current;
      const rect = canvas.getBoundingClientRect();
      const mx = e.clientX - rect.left;
      const my = e.clientY - rect.top;

      if (isDragging) {
        const dx = e.clientX - dragStartX;
        const dy = e.clientY - dragStartY;
        if (Math.abs(dx) > 3 || Math.abs(dy) > 3) dragMoved = true;
        dragStartX = e.clientX;
        dragStartY = e.clientY;

        if (isPanning) {
          st.targetPanX += dx;
          st.targetPanY += dy;
        } else {
          if (st.autoRotate) {
            st.autoRotate = false;
            setAutoRotate(false);
          }
          st.targetYaw -= dx * 0.0075;
          const pitchSign = st.invertVertical ? 1 : -1;
          st.targetPitch = Math.max(
            MIN_PITCH,
            Math.min(MAX_PITCH, st.targetPitch + pitchSign * dy * 0.0075)
          );
        }
        return;
      }

      const hit = findHitNode(mx, my);
      st.hoveredNodeId = hit ? hit.id : null;
      canvas.style.cursor = hit ? 'pointer' : 'grab';
    };

    const onMouseUp = (e: MouseEvent) => {
      const st = stateRef.current;
      if (isDragging && !dragMoved && e.button === 0) {
        const isMultiKey = Boolean(e.ctrlKey || e.metaKey || e.shiftKey);
        if (st.hoveredNodeId) {
          if (!isMultiKey && st.selectedNodeId === st.hoveredNodeId) {
            onSelectNode(null, false);
          } else {
            onSelectNode(st.hoveredNodeId, isMultiKey);
          }
        } else {
          onSelectNode(null, false);
        }
      }
      isDragging = false;
    };

    const onDblClick = (e: MouseEvent) => {
      const st = stateRef.current;
      const rect = canvas.getBoundingClientRect();
      const hit = findHitNode(e.clientX - rect.left, e.clientY - rect.top);
      if (hit) {
        st.targetFocusOrigin = { x: hit.tx, y: hit.ty, z: hit.tz };
        st.targetPanX = 0;
        st.targetPanY = 0;
        onDoubleClickNode(hit.id);
      } else {
        st.targetFocusOrigin = { x: 0, y: 0, z: 0 };
      }
    };

    const onWheel = (e: WheelEvent) => {
      e.preventDefault();
      const st = stateRef.current;
      const factor = e.deltaY < 0 ? 1.15 : 0.87;
      st.targetZoom = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, st.targetZoom * factor));
    };

    const onContextMenu = (e: MouseEvent) => {
      e.preventDefault();
    };

    canvas.addEventListener(EVENT_MOUSEDOWN, onMouseDown);
    window.addEventListener(EVENT_MOUSEMOVE, onMouseMove);
    window.addEventListener(EVENT_MOUSEUP, onMouseUp);
    canvas.addEventListener(EVENT_DBLCLICK, onDblClick);
    canvas.addEventListener(EVENT_WHEEL, onWheel, WHEEL_LISTENER_OPTIONS);
    canvas.addEventListener(EVENT_CONTEXTMENU, onContextMenu);

    const tick = (now: number) => {
      renderOrbital3DFrame(ctx, canvas, stateRef.current, now, lastReadoutRef, setCameraReadout);
      animId = requestAnimationFrame(tick);
    };

    animId = requestAnimationFrame(tick);

    return () => {
      cancelAnimationFrame(animId);
      if (ro) ro.disconnect();
      window.removeEventListener(EVENT_RESIZE, resize);
      canvas.removeEventListener(EVENT_MOUSEDOWN, onMouseDown);
      window.removeEventListener(EVENT_MOUSEMOVE, onMouseMove);
      window.removeEventListener(EVENT_MOUSEUP, onMouseUp);
      canvas.removeEventListener(EVENT_DBLCLICK, onDblClick);
      canvas.removeEventListener(EVENT_WHEEL, onWheel);
      canvas.removeEventListener(EVENT_CONTEXTMENU, onContextMenu);
    };
  }, [onSelectNode, onContextMenuNode, onDoubleClickNode]);

  return (
    <div
      className="universal-graph-3d-wrapper"
      style={{
        position: 'relative',
        width: '100%',
        height: '100%',
        flex: 1,
        overflow: 'hidden',
        background: '#090b10',
      }}
    >
      {/* Floating 3D Controls Bar */}
      <div
        className="orbital-3d-hud-bar"
        style={{
          position: 'absolute',
          top: 12,
          left: 12,
          zIndex: 30,
          display: 'flex',
          flexWrap: 'wrap',
          alignItems: 'center',
          gap: 8,
          background: 'rgba(15, 21, 33, 0.86)',
          backdropFilter: 'blur(10px)',
          border: '1px solid rgba(148, 163, 184, 0.22)',
          borderRadius: 8,
          padding: '6px 10px',
          boxShadow: '0 8px 24px rgba(0, 0, 0, 0.4)',
          fontSize: 11.5,
          color: '#f8fafc',
        }}
      >
        <div style={{ display: 'flex', gap: 4 }}>
          {(
            [
              { id: 'echelon', label: '3D Stacked Echelons' },
              { id: 'gyro', label: '3D Gyroscope Orbits' },
              { id: 'sphere', label: '3D Fibonacci Sphere' },
              { id: 'flat2d', label: '2D Flat (0°)' },
            ] as Array<{ id: Orbital3DMode; label: string }>
          ).map((item) => (
            <button
              key={item.id}
              type="button"
              onClick={() => handleModeChange(item.id)}
              style={{
                background:
                  mode === item.id ? 'rgba(56, 189, 248, 0.22)' : 'rgba(255, 255, 255, 0.04)',
                color: mode === item.id ? '#38bdf8' : '#cbd5e1',
                border:
                  mode === item.id
                    ? '1px solid #38bdf8'
                    : '1px solid rgba(148, 163, 184, 0.2)',
                borderRadius: 5,
                padding: '4px 8px',
                fontSize: 11,
                fontWeight: 600,
                cursor: 'pointer',
              }}
            >
              {item.label}
            </button>
          ))}
        </div>

        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            paddingLeft: 6,
            borderLeft: '1px solid rgba(148, 163, 184, 0.2)',
            color: '#cbd5e1',
            fontSize: 11,
          }}
        >
          <span>Z-Depth</span>
          <input
            type="range"
            min={0}
            max={360}
            value={depthPercent}
            onChange={(e) => handleDepthChange(Number(e.target.value))}
            style={{ width: 84, accentColor: '#38bdf8', cursor: 'pointer' }}
            title={`Z-Depth Scale: ${depthPercent}%`}
          />
          <label
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 4,
              marginLeft: 4,
              cursor: 'pointer',
              userSelect: 'none',
              color: invertVertical ? '#38bdf8' : '#cbd5e1',
              fontWeight: 600,
            }}
            title="Invert vertical mouse drag (tilt / pitch)"
          >
            <input
              type="checkbox"
              checked={invertVertical}
              onChange={(e) => handleToggleInvertVertical(e.target.checked)}
              style={{ accentColor: '#38bdf8', cursor: 'pointer', margin: 0 }}
            />
            <span>Invert Y</span>
          </label>
        </div>

        <div
          style={{
            display: 'flex',
            gap: 4,
            paddingLeft: 6,
            borderLeft: '1px solid rgba(148, 163, 184, 0.2)',
          }}
        >
          <button
            type="button"
            onClick={handleToggleAutoRotate}
            style={{
              background: autoRotate ? 'rgba(56, 189, 248, 0.22)' : 'rgba(255, 255, 255, 0.04)',
              color: autoRotate ? '#38bdf8' : '#cbd5e1',
              border: autoRotate
                ? '1px solid #38bdf8'
                : '1px solid rgba(148, 163, 184, 0.2)',
              borderRadius: 5,
              padding: '4px 8px',
              fontSize: 11,
              fontWeight: 600,
              cursor: 'pointer',
            }}
          >
            Auto-Orbit
          </button>
          <button
            type="button"
            onClick={() => onSelectNode(null, false)}
            style={{
              background:
                selectedNodeId === null
                  ? 'rgba(56, 189, 248, 0.22)'
                  : 'rgba(255, 255, 255, 0.04)',
              color: selectedNodeId === null ? '#38bdf8' : '#cbd5e1',
              border:
                selectedNodeId === null
                  ? '1px solid #38bdf8'
                  : '1px solid rgba(148, 163, 184, 0.2)',
              borderRadius: 5,
              padding: '4px 8px',
              fontSize: 11,
              fontWeight: 600,
              cursor: 'pointer',
            }}
            title="Clear selection and illuminate entire graph"
          >
            Show All
          </button>
          <button
            type="button"
            onClick={handleResetCamera}
            style={{
              background: 'rgba(255, 255, 255, 0.04)',
              color: '#cbd5e1',
              border: '1px solid rgba(148, 163, 184, 0.2)',
              borderRadius: 5,
              padding: '4px 8px',
              fontSize: 11,
              fontWeight: 600,
              cursor: 'pointer',
            }}
          >
            Reset Camera
          </button>
        </div>

        <span
          style={{
            fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
            fontSize: 10.5,
            color: '#94a3b8',
            paddingLeft: 6,
            borderLeft: '1px solid rgba(148, 163, 184, 0.2)',
          }}
        >
          {cameraReadout}
        </span>
      </div>

      {/* Bottom-left navigation hint pill */}
      <div
        style={{
          position: 'absolute',
          bottom: 12,
          left: 12,
          zIndex: 25,
          background: 'rgba(15, 21, 33, 0.78)',
          backdropFilter: 'blur(8px)',
          border: '1px solid rgba(148, 163, 184, 0.18)',
          borderRadius: 6,
          padding: '5px 10px',
          fontSize: 11,
          color: '#94a3b8',
          pointerEvents: 'none',
        }}
      >
        <strong style={{ color: '#e2e8f0' }}>Drag</strong> Rotate 3D Orbit ·{' '}
        <strong style={{ color: '#e2e8f0' }}>Shift+Drag / Right-Drag</strong> Pan ·{' '}
        <strong style={{ color: '#e2e8f0' }}>Wheel</strong> Zoom ·{' '}
        <strong style={{ color: '#e2e8f0' }}>Click</strong> Inspect (2nd click = Show All) ·{' '}
        <strong style={{ color: '#e2e8f0' }}>Double-Click</strong> Open File / Focus
      </div>

      <canvas
        ref={canvasRef}
        style={{
          display: 'block',
          width: '100%',
          height: '100%',
          cursor: 'grab',
        }}
      />
    </div>
  );
};
