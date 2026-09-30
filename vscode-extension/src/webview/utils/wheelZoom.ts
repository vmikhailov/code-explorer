/**
 * Universal Wheel Zoom Normalization Utility
 * 
 * Solves the severe cross-platform / cross-device discrepancies in WheelEvent deltas:
 * - Stepped mouse wheel (Windows / external mouse): discrete single events with deltaY ~ 100-120
 * - macOS / Precision Touchpad: continuous high-frequency stream of small deltas (1-5px)
 * - Trackpad pinch-to-zoom: WheelEvent with e.ctrlKey = true
 * - DeltaMode discrepancies: DOM_DELTA_LINE (Firefox / Windows line scroll) vs DOM_DELTA_PIXEL
 * 
 * Uses an exponential formulation: exp(-dy * k).
 * Because exp(a + b) = exp(a) * exp(b), the total zoom over a given scroll distance is
 * mathematically identical whether it arrived in one 100px mouse notch or fifty 2px trackpad micro-events.
 */

import type cytoscape from 'cytoscape';

export interface NormalizedWheelOptions {
  /** Sensitivity multiplier (default: 1.0) */
  sensitivity?: number;
  /** Minimum zoom limit */
  minZoom?: number;
  /** Maximum zoom limit */
  maxZoom?: number;
}

/**
 * Calculates a smooth, invariant zoom factor for any WheelEvent.
 * 
 * @param e Native or React WheelEvent
 * @param sensitivity Multiplier for zoom speed (default 1.0)
 * @returns Zoom multiplier, e.g. ~1.15 for 1 notch in, ~0.87 for 1 notch out
 */
export function calculateNormalizedZoomFactor(
  e: WheelEvent | React.WheelEvent,
  sensitivity: number = 1.0
): number {
  let dy = e.deltaY;

  // 1. Normalize deltaMode to virtual pixels
  if (e.deltaMode === 1) {
    // DOM_DELTA_LINE: Firefox or Windows line scrolling (~3 lines per notch, 1 line ~ 33px)
    dy *= 33.33;
  } else if (e.deltaMode === 2) {
    // DOM_DELTA_PAGE
    dy *= 600;
  }

  // 2. Pinch-to-zoom on trackpad (Safari, Chrome, Edge send wheel with e.ctrlKey = true)
  const isPinch = e.ctrlKey;
  const baseK = isPinch ? 0.005 : 0.0015;
  const k = baseK * Math.max(0.1, sensitivity);

  // 3. Clamp per-event delta to prevent extreme jumps from glitchy drivers or trackpad flings
  const clampedDy = Math.max(-120, Math.min(120, dy));

  // 4. Invariant exponential zoom factor
  return Math.exp(-clampedDy * k);
}

/**
 * Calculates new pan coordinates so the point under the mouse cursor
 * stays invariant during zooming (zoom-to-cursor).
 * 
 * Applicable for SVG / Canvas / CSS transform layers with `transformOrigin: '0 0'`.
 */
export function calculateAnchorPan(
  cursorClientX: number,
  cursorClientY: number,
  containerRect: { left: number; top: number },
  currentPan: { x: number; y: number },
  oldZoom: number,
  newZoom: number
): { x: number; y: number } {
  if (oldZoom <= 0 || newZoom <= 0) return currentPan;

  const mouseX = cursorClientX - containerRect.left;
  const mouseY = cursorClientY - containerRect.top;

  const zoomRatio = newZoom / oldZoom;

  return {
    x: mouseX - (mouseX - currentPan.x) * zoomRatio,
    y: mouseY - (mouseY - currentPan.y) * zoomRatio,
  };
}

/**
 * Attaches a normalized wheel listener to a Cytoscape container element.
 * 
 * Bypasses Cytoscape's internal heuristic GCD-based wheel handler which causes
 * erratic jumpy zooming on discrete mouse wheels or differing OS platforms.
 * 
 * @param container The container DOM element Cytoscape is mounted in
 * @param cyGetter Function returning the active Cytoscape instance
 * @param getSensitivity Optional getter for user-customizable sensitivity multiplier
 * @returns Cleanup function to remove event listener
 */
export function attachNormalizedCytoscapeWheel(
  container: HTMLElement,
  cyGetter: () => cytoscape.Core | null | undefined,
  getSensitivity?: () => number
): () => void {
  const onWheel = (e: WheelEvent) => {
    const cy = cyGetter();
    if (!cy) return;

    // Prevent native page scroll and block Cytoscape's internal wheel handler
    e.preventDefault();
    e.stopImmediatePropagation();

    const rect = container.getBoundingClientRect();
    const renderedPosition = {
      x: e.clientX - rect.left,
      y: e.clientY - rect.top,
    };

    const currentZoom = cy.zoom();
    const sensitivity = getSensitivity ? getSensitivity() : 1.0;
    const factor = calculateNormalizedZoomFactor(e, sensitivity);

    const minZoom = cy.minZoom();
    const maxZoom = cy.maxZoom();
    const newZoom = Math.min(maxZoom, Math.max(minZoom, currentZoom * factor));

    cy.zoom({
      level: newZoom,
      renderedPosition,
    });
  };

  container.addEventListener('wheel', onWheel, { capture: true, passive: false });
  return () => {
    container.removeEventListener('wheel', onWheel, { capture: true });
  };
}
