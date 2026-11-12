import { OntologyGraphEdge, OntologyGraphNode } from '../domain/ontologyGraphLayout';

export interface OntologyViewport {
  panX: number;
  panY: number;
  zoom: number;
}

export interface OntologyRenderState {
  nodes: readonly OntologyGraphNode[];
  edges: readonly OntologyGraphEdge[];
  nodeByKind: ReadonlyMap<string, OntologyGraphNode>;
  selectedKind: string | null;
  focusedKind: string | null;
  hoveredKind: string | null;
  viewport: OntologyViewport;
  width: number;
  height: number;
  dpr: number;
}

interface ScreenBox {
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
}

const CANVAS_BG = '#0b101b';
const GRID_DOT_FILL = 'rgba(148, 163, 184, 0.08)';
const NODE_RING_DEFAULT = 'rgba(226, 232, 240, 0.86)';
const NODE_RING_SELECTED = '#38bdf8';
const NODE_HALO_SELECTED = 'rgba(56, 189, 248, 0.24)';
const NODE_HALO_HOVER = 'rgba(248, 250, 252, 0.18)';
const NODE_ICON_STROKE = '#f8fafc';
const NODE_LABEL_COLOR = '#f1f5f9';
const NODE_LABEL_DIM = 'rgba(203, 213, 225, 0.45)';
const BADGE_BG = '#0f172a';
const BADGE_BORDER = 'rgba(148, 163, 184, 0.48)';
const BADGE_TEXT_COLOR = '#f8fafc';
const BADGE_ZERO_COLOR = '#94a3b8';

const PILL_BG = 'rgba(15, 23, 42, 0.88)';
const PILL_BORDER = 'rgba(148, 163, 184, 0.24)';
const PILL_BORDER_ACTIVE = 'rgba(125, 211, 252, 0.55)';
const PILL_TEXT_DEFAULT = 'rgba(203, 213, 225, 0.86)';
const PILL_TEXT_ACTIVE = '#f8fafc';

const EDGE_STROKES: Record<string, string> = {
  control_flow: 'rgba(148, 163, 184, 0.52)',
  data_flow: 'rgba(148, 163, 184, 0.52)',
  type_dependency: 'rgba(148, 163, 184, 0.46)',
  structural: 'rgba(148, 163, 184, 0.38)',
};

const EDGE_STROKES_ACTIVE: Record<string, string> = {
  control_flow: '#38bdf8',
  data_flow: '#34d399',
  type_dependency: '#a78bfa',
  structural: '#fbbf24',
};

const EDGE_DIM_STROKE = 'rgba(100, 116, 139, 0.13)';
const FONT_LABEL = '600 12px -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
const FONT_BADGE = '700 10px ui-monospace, SFMono-Regular, Menlo, Consolas, monospace';
const FONT_EDGE_PILL = '600 9.5px ui-monospace, SFMono-Regular, Menlo, Consolas, monospace';
const ALIGN_CENTER: CanvasTextAlign = 'center';
const BASELINE_MIDDLE: CanvasTextBaseline = 'middle';
const BASELINE_TOP: CanvasTextBaseline = 'top';
const LINE_CAP_ROUND: CanvasLineCap = 'round';
const LINE_JOIN_ROUND: CanvasLineJoin = 'round';

function drawRoundedRect(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  w: number,
  h: number,
  r: number
): void {
  const radius = Math.min(r, w / 2, h / 2);
  ctx.beginPath();
  ctx.moveTo(x + radius, y);
  ctx.arcTo(x + w, y, x + w, y + h, radius);
  ctx.arcTo(x + w, y + h, x, y + h, radius);
  ctx.arcTo(x, y + h, x, y, radius);
  ctx.arcTo(x, y, x + w, y, radius);
  ctx.closePath();
}

function boxesOverlap(a: ScreenBox, b: ScreenBox): boolean {
  return !(a.maxX < b.minX || a.minX > b.maxX || a.maxY < b.minY || a.minY > b.maxY);
}

function drawGlyph(ctx: CanvasRenderingContext2D, glyph: string, x: number, y: number): void {
  ctx.save();
  ctx.translate(x, y);
  ctx.strokeStyle = NODE_ICON_STROKE;
  ctx.fillStyle = NODE_ICON_STROKE;
  ctx.lineWidth = 1.65;
  ctx.lineCap = LINE_CAP_ROUND;
  ctx.lineJoin = LINE_JOIN_ROUND;

  if (glyph === 'database' || glyph === 'procedure') {
    ctx.beginPath();
    ctx.ellipse(0, -5, 6.5, 2.6, 0, 0, Math.PI * 2);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(-6.5, -5);
    ctx.lineTo(-6.5, 5);
    ctx.arc(0, 5, 6.5, Math.PI, 0, true);
    ctx.lineTo(6.5, -5);
    ctx.stroke();
  } else if (glyph === 'table') {
    drawRoundedRect(ctx, -7, -6, 14, 12, 2);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(-7, -1.5);
    ctx.lineTo(7, -1.5);
    ctx.moveTo(-1.5, -1.5);
    ctx.lineTo(-1.5, 6);
    ctx.stroke();
  } else if (glyph === 'query') {
    ctx.beginPath();
    ctx.arc(-1.5, -1.5, 5, 0, Math.PI * 2);
    ctx.moveTo(2.2, 2.2);
    ctx.lineTo(6.8, 6.8);
    ctx.stroke();
  } else if (glyph === 'endpoint') {
    ctx.beginPath();
    ctx.arc(0, 0, 2.2, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.arc(0, 0, 5.5, -Math.PI * 0.7, Math.PI * 0.7);
    ctx.stroke();
    ctx.beginPath();
    ctx.arc(0, 0, 5.5, Math.PI * 0.3, Math.PI * 1.7);
    ctx.stroke();
  } else if (glyph === 'topic') {
    drawRoundedRect(ctx, -7, -5, 14, 10, 2);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(-6.5, -4.2);
    ctx.lineTo(0, 1);
    ctx.lineTo(6.5, -4.2);
    ctx.stroke();
  } else if (glyph === 'cloud' || glyph === 'cloudUp') {
    ctx.beginPath();
    ctx.arc(-2.5, 1, 3.8, Math.PI * 0.5, Math.PI * 1.5);
    ctx.arc(1, -1.5, 4.2, Math.PI, Math.PI * 1.95);
    ctx.arc(4.2, 1.5, 3.2, Math.PI * 1.5, Math.PI * 0.5);
    ctx.closePath();
    ctx.stroke();
  } else if (glyph === 'function') {
    ctx.beginPath();
    ctx.moveTo(2.5, -6.5);
    ctx.quadraticCurveTo(-1, -6.5, -1, -2);
    ctx.lineTo(-1, 6.5);
    ctx.moveTo(-4.5, -1);
    ctx.lineTo(3, -1);
    ctx.stroke();
  } else if (glyph === 'type') {
    ctx.beginPath();
    ctx.moveTo(0, -7);
    ctx.lineTo(6.5, -3.5);
    ctx.lineTo(6.5, 3.5);
    ctx.lineTo(0, 7);
    ctx.lineTo(-6.5, 3.5);
    ctx.lineTo(-6.5, -3.5);
    ctx.closePath();
    ctx.stroke();
  } else if (glyph === 'param' || glyph === 'arg' || glyph === 'literal') {
    ctx.beginPath();
    ctx.moveTo(-4, -5.5);
    ctx.lineTo(-7, 0);
    ctx.lineTo(-4, 5.5);
    ctx.moveTo(4, -5.5);
    ctx.lineTo(7, 0);
    ctx.lineTo(4, 5.5);
    if (glyph !== 'param') {
      ctx.moveTo(-1.5, 0);
      ctx.lineTo(1.5, 0);
    }
    ctx.stroke();
  } else if (glyph === 'folder') {
    ctx.beginPath();
    ctx.moveTo(-7, -5);
    ctx.lineTo(-2, -5);
    ctx.lineTo(0, -3);
    ctx.lineTo(7, -3);
    ctx.lineTo(7, 5.5);
    ctx.lineTo(-7, 5.5);
    ctx.closePath();
    ctx.stroke();
  } else if (glyph === 'file') {
    ctx.beginPath();
    ctx.moveTo(-5, -6.5);
    ctx.lineTo(2, -6.5);
    ctx.lineTo(5.5, -3);
    ctx.lineTo(5.5, 6.5);
    ctx.lineTo(-5, 6.5);
    ctx.closePath();
    ctx.stroke();
  } else if (glyph === 'package' || glyph === 'project') {
    drawRoundedRect(ctx, -6.5, -5.5, 13, 11, 2);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(-6.5, -1.5);
    ctx.lineTo(6.5, -1.5);
    ctx.stroke();
  } else if (glyph === 'zap') {
    ctx.beginPath();
    ctx.moveTo(1, -7);
    ctx.lineTo(-5, 0.5);
    ctx.lineTo(0, 0.5);
    ctx.lineTo(-1, 7);
    ctx.lineTo(5, -0.5);
    ctx.lineTo(0, -0.5);
    ctx.closePath();
    ctx.stroke();
  } else {
    drawRoundedRect(ctx, -6.5, -5.5, 13, 11, 2.5);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(-3.5, 0);
    ctx.lineTo(3.5, 0);
    ctx.stroke();
  }

  ctx.restore();
}

function drawArrowhead(
  ctx: CanvasRenderingContext2D,
  tipX: number,
  tipY: number,
  angle: number,
  color: string,
  size: number
): void {
  ctx.save();
  ctx.translate(tipX, tipY);
  ctx.rotate(angle);
  ctx.fillStyle = color;
  ctx.beginPath();
  ctx.moveTo(0, 0);
  ctx.lineTo(-size, -size * 0.54);
  ctx.lineTo(-size * 0.78, 0);
  ctx.lineTo(-size, size * 0.54);
  ctx.closePath();
  ctx.fill();
  ctx.restore();
}

export function hitTestOntologyNode(
  nodes: readonly OntologyGraphNode[],
  viewport: OntologyViewport,
  clientOffsetX: number,
  clientOffsetY: number
): OntologyGraphNode | null {
  const worldX = (clientOffsetX - viewport.panX) / viewport.zoom;
  const worldY = (clientOffsetY - viewport.panY) / viewport.zoom;

  for (let i = nodes.length - 1; i >= 0; i--) {
    const n = nodes[i];
    const dx = worldX - n.x;
    const dy = worldY - n.y;
    const hitR = n.radius + 7;
    if (dx * dx + dy * dy <= hitR * hitR) {
      return n;
    }
  }
  return null;
}

export function computeFitViewport(
  nodes: readonly OntologyGraphNode[],
  width: number,
  height: number
): OntologyViewport {
  if (nodes.length === 0 || width <= 0 || height <= 0) {
    return { panX: 0, panY: 0, zoom: 1 };
  }
  let minX = Infinity;
  let minY = Infinity;
  let maxX = -Infinity;
  let maxY = -Infinity;
  for (const n of nodes) {
    if (n.x - 68 < minX) minX = n.x - 68;
    if (n.y - 74 < minY) minY = n.y - 74;
    if (n.x + 68 > maxX) maxX = n.x + 68;
    if (n.y + 88 > maxY) maxY = n.y + 88;
  }
  const bw = Math.max(200, maxX - minX);
  const bh = Math.max(200, maxY - minY);
  const pad = 28;
  const scaleX = (width - pad * 2) / bw;
  const scaleY = (height - pad * 2) / bh;
  const zoom = Math.max(0.22, Math.min(1.32, Math.min(scaleX, scaleY)));
  const cx = (minX + maxX) / 2;
  const cy = (minY + maxY) / 2;
  return {
    panX: width / 2 - cx * zoom,
    panY: height / 2 - cy * zoom,
    zoom,
  };
}

export function renderOntologyCanvas(
  ctx: CanvasRenderingContext2D,
  state: OntologyRenderState
): void {
  const { width, height, dpr, viewport, nodes, edges, nodeByKind } = state;
  if (width <= 0 || height <= 0) return;

  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.fillStyle = CANVAS_BG;
  ctx.fillRect(0, 0, width, height);

  // Subtle spatial dot matrix
  const gridStep = 32;
  ctx.fillStyle = GRID_DOT_FILL;
  const offsetX = ((viewport.panX % gridStep) + gridStep) % gridStep;
  const offsetY = ((viewport.panY % gridStep) + gridStep) % gridStep;
  for (let gx = offsetX; gx < width; gx += gridStep) {
    for (let gy = offsetY; gy < height; gy += gridStep) {
      ctx.fillRect(gx, gy, 1.4, 1.4);
    }
  }

  const activeFocus = state.hoveredKind || state.focusedKind;
  const egoPeers = new Set<string>();
  if (activeFocus) {
    egoPeers.add(activeFocus);
    for (const e of edges) {
      if (e.fromKind === activeFocus) egoPeers.add(e.toKind);
      if (e.toKind === activeFocus) egoPeers.add(e.fromKind);
    }
  }

  ctx.save();
  ctx.translate(viewport.panX, viewport.panY);
  ctx.scale(viewport.zoom, viewport.zoom);

  const pendingPills: Array<{
    mx: number;
    my: number;
    text: string;
    active: boolean;
    priority: number;
  }> = [];

  for (const edge of edges) {
    const src = nodeByKind.get(edge.fromKind);
    const dst = nodeByKind.get(edge.toKind);
    if (!src || !dst) continue;

    const isIncident = Boolean(
      activeFocus && (edge.fromKind === activeFocus || edge.toKind === activeFocus)
    );
    const isDim = Boolean(activeFocus && !isIncident);

    const strokeColor = isDim
      ? EDGE_DIM_STROKE
      : isIncident
        ? EDGE_STROKES_ACTIVE[edge.category] || NODE_RING_SELECTED
        : EDGE_STROKES[edge.category] || EDGE_STROKES.control_flow;

    ctx.strokeStyle = strokeColor;
    ctx.lineWidth = isIncident ? 1.85 : 1.25;

    if (edge.isSelfLoop) {
      const baseAngle = -Math.PI * 0.64 - edge.selfLoopSlot * 0.55;
      const spread = 0.42;
      const startAngle = baseAngle - spread;
      const endAngle = baseAngle + spread;
      const r = src.radius;
      const sx = src.x + Math.cos(startAngle) * r;
      const sy = src.y + Math.sin(startAngle) * r;
      const ex = src.x + Math.cos(endAngle) * (r + 3);
      const ey = src.y + Math.sin(endAngle) * (r + 3);
      const loopReach = r + 42 + edge.selfLoopSlot * 16;
      const c1x = src.x + Math.cos(startAngle - 0.18) * loopReach;
      const c1y = src.y + Math.sin(startAngle - 0.18) * loopReach;
      const c2x = src.x + Math.cos(endAngle + 0.18) * loopReach;
      const c2y = src.y + Math.sin(endAngle + 0.18) * loopReach;

      ctx.beginPath();
      ctx.moveTo(sx, sy);
      ctx.bezierCurveTo(c1x, c1y, c2x, c2y, ex, ey);
      ctx.stroke();

      if (!isDim) {
        const arrowAngle = Math.atan2(ey - c2y, ex - c2x);
        drawArrowhead(ctx, ex, ey, arrowAngle, strokeColor, 7);
        const mx = 0.125 * sx + 0.375 * c1x + 0.375 * c2x + 0.125 * ex;
        const my = 0.125 * sy + 0.375 * c1y + 0.375 * c2y + 0.125 * ey;
        pendingPills.push({
          mx,
          my,
          text: edge.rel,
          active: isIncident,
          priority: isIncident ? 0 : edge.priority,
        });
      }
      continue;
    }

    const minFirst = edge.fromKind < edge.toKind;
    const ax = minFirst ? src.x : dst.x;
    const ay = minFirst ? src.y : dst.y;
    const bx = minFirst ? dst.x : src.x;
    const by = minFirst ? dst.y : src.y;
    const cdx = bx - ax;
    const cdy = by - ay;
    const cLen = Math.sqrt(cdx * cdx + cdy * cdy) || 1;
    const nx = -cdy / cLen;
    const ny = cdx / cLen;

    const cpx = (src.x + dst.x) / 2 + nx * edge.canonicalOffset;
    const cpy = (src.y + dst.y) / 2 + ny * edge.canonicalOffset;

    const startVecX = cpx - src.x;
    const startVecY = cpy - src.y;
    const startLen = Math.sqrt(startVecX * startVecX + startVecY * startVecY) || 1;
    const sx = src.x + (startVecX / startLen) * src.radius;
    const sy = src.y + (startVecY / startLen) * src.radius;

    const endVecX = dst.x - cpx;
    const endVecY = dst.y - cpy;
    const endLen = Math.sqrt(endVecX * endVecX + endVecY * endVecY) || 1;
    const tipPad = dst.radius + 3;
    const tx = dst.x - (endVecX / endLen) * tipPad;
    const ty = dst.y - (endVecY / endLen) * tipPad;

    ctx.beginPath();
    ctx.moveTo(sx, sy);
    ctx.quadraticCurveTo(cpx, cpy, tx, ty);
    ctx.stroke();

    if (!isDim) {
      const arrowAngle = Math.atan2(endVecY, endVecX);
      drawArrowhead(ctx, tx, ty, arrowAngle, strokeColor, isIncident ? 7.8 : 6.6);
      const mx = 0.25 * sx + 0.5 * cpx + 0.25 * tx;
      const my = 0.25 * sy + 0.5 * cpy + 0.25 * ty;
      pendingPills.push({
        mx,
        my,
        text: edge.rel,
        active: isIncident,
        priority: isIncident ? 0 : edge.priority,
      });
    }
  }

  // Draw circular nodes on top of edge lines
  const occupiedBoxes: ScreenBox[] = [];

  for (const node of nodes) {
    const isSelected = node.kind === state.selectedKind;
    const isHovered = node.kind === state.hoveredKind;
    const isDim = Boolean(activeFocus && !egoPeers.has(node.kind));

    const sx = node.x * viewport.zoom + viewport.panX;
    const sy = node.y * viewport.zoom + viewport.panY;
    const sr = (node.radius + 14) * viewport.zoom;
    occupiedBoxes.push({
      minX: sx - sr,
      minY: sy - sr,
      maxX: sx + sr,
      maxY: sy + sr + 18 * viewport.zoom,
    });

    ctx.save();
    if (isDim) {
      ctx.globalAlpha = 0.34;
    }

    if (isSelected || isHovered) {
      ctx.beginPath();
      ctx.arc(node.x, node.y, node.radius + 6.5, 0, Math.PI * 2);
      ctx.fillStyle = isSelected ? NODE_HALO_SELECTED : NODE_HALO_HOVER;
      ctx.fill();
    }

    ctx.beginPath();
    ctx.arc(node.x, node.y, node.radius, 0, Math.PI * 2);
    ctx.fillStyle = node.fill;
    ctx.fill();

    ctx.lineWidth = isSelected ? 2.4 : 1.7;
    ctx.strokeStyle = isSelected ? NODE_RING_SELECTED : NODE_RING_DEFAULT;
    ctx.stroke();

    drawGlyph(ctx, node.glyph, node.x, node.y);

    // Rim count pill at top-right (-42 deg)
    ctx.font = FONT_BADGE;
    const badgeW = Math.max(20, ctx.measureText(node.badgeText).width + 10);
    const badgeH = 17;
    const bx = node.x + node.radius * 0.72 - badgeW / 2 + 3;
    const by = node.y - node.radius * 0.74 - badgeH / 2;

    drawRoundedRect(ctx, bx, by, badgeW, badgeH, 8.5);
    ctx.fillStyle = BADGE_BG;
    ctx.fill();
    ctx.lineWidth = 1.1;
    ctx.strokeStyle = BADGE_BORDER;
    ctx.stroke();

    ctx.fillStyle = node.rawCount > 0 ? BADGE_TEXT_COLOR : BADGE_ZERO_COLOR;
    ctx.textAlign = ALIGN_CENTER;
    ctx.textBaseline = BASELINE_MIDDLE;
    ctx.fillText(node.badgeText, bx + badgeW / 2, by + badgeH / 2 + 0.5);

    // Entity name label below circle
    ctx.font = FONT_LABEL;
    ctx.fillStyle = isDim ? NODE_LABEL_DIM : NODE_LABEL_COLOR;
    ctx.textAlign = ALIGN_CENTER;
    ctx.textBaseline = BASELINE_TOP;
    ctx.fillText(node.kind, node.x, node.y + node.radius + 7);

    ctx.restore();
  }

  ctx.restore();

  // Draw screen-space collision-culled relationship pills crisp at 1x scale
  if (viewport.zoom >= 0.52) {
    pendingPills.sort((a, b) => a.priority - b.priority);
    ctx.font = FONT_EDGE_PILL;
    ctx.textAlign = ALIGN_CENTER;
    ctx.textBaseline = BASELINE_MIDDLE;
    const shownRelCounts = new Map<string, number>();

    for (const pill of pendingPills) {
      const prevShown = shownRelCounts.get(pill.text) || 0;
      if (!pill.active && prevShown >= 2) continue;
      const px = pill.mx * viewport.zoom + viewport.panX;
      const py = pill.my * viewport.zoom + viewport.panY;
      if (px < -40 || px > width + 40 || py < -20 || py > height + 20) continue;

      const tw = ctx.measureText(pill.text).width;
      const pw = tw + 11;
      const ph = 16;
      const box: ScreenBox = {
        minX: px - pw / 2 - 3,
        minY: py - ph / 2 - 2,
        maxX: px + pw / 2 + 3,
        maxY: py + ph / 2 + 2,
      };

      if (!pill.active && occupiedBoxes.some((ob) => boxesOverlap(box, ob))) {
        continue;
      }
      occupiedBoxes.push(box);
      shownRelCounts.set(pill.text, prevShown + 1);

      drawRoundedRect(ctx, px - pw / 2, py - ph / 2, pw, ph, 5);
      ctx.fillStyle = PILL_BG;
      ctx.fill();
      ctx.lineWidth = 1;
      ctx.strokeStyle = pill.active ? PILL_BORDER_ACTIVE : PILL_BORDER;
      ctx.stroke();

      ctx.fillStyle = pill.active ? PILL_TEXT_ACTIVE : PILL_TEXT_DEFAULT;
      ctx.fillText(pill.text, px, py + 0.5);
    }
  }
}
