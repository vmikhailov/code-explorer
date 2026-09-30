import test from 'node:test';
import assert from 'node:assert/strict';
import {
  calculateNormalizedZoomFactor,
  calculateAnchorPan,
  createEncoderBounceFilter,
} from './wheelZoom';

test('calculateNormalizedZoomFactor: zero delta returns 1.0', () => {
  const factor = calculateNormalizedZoomFactor({ deltaY: 0, deltaMode: 0, ctrlKey: false } as any);
  assert.equal(factor, 1.0);
});

test('calculateNormalizedZoomFactor: discrete mouse wheel zoom in and zoom out', () => {
  // Wheel UP (zoom in)
  const zoomIn = calculateNormalizedZoomFactor({ deltaY: -100, deltaMode: 0, ctrlKey: false } as any);
  assert.ok(zoomIn > 1.0, 'Wheel UP should zoom in (factor > 1)');
  assert.ok(zoomIn > 1.10 && zoomIn < 1.25, `Expected ~1.16, got ${zoomIn}`);

  // Wheel DOWN (zoom out)
  const zoomOut = calculateNormalizedZoomFactor({ deltaY: 100, deltaMode: 0, ctrlKey: false } as any);
  assert.ok(zoomOut < 1.0, 'Wheel DOWN should zoom out (factor < 1)');
  assert.ok(zoomOut > 0.80 && zoomOut < 0.90, `Expected ~0.86, got ${zoomOut}`);

  // Invariant reciprocal: zoomIn * zoomOut ≈ 1.0
  assert.ok(Math.abs(zoomIn * zoomOut - 1.0) < 1e-4, 'Zoom in and zoom out should be mathematically reciprocal');
});

test('calculateNormalizedZoomFactor: Ctrl + physical mouse wheel uses normal wheel sensitivity, not trackpad pinch', () => {
  // Holding Ctrl with a physical mouse wheel (deltaY = 100 or 120)
  const ctrlWheel = calculateNormalizedZoomFactor({ deltaY: -100, deltaMode: 0, ctrlKey: true } as any);
  const normalWheel = calculateNormalizedZoomFactor({ deltaY: -100, deltaMode: 0, ctrlKey: false } as any);

  // Both should use baseK = 0.0015, not 0.005!
  assert.equal(ctrlWheel, normalWheel, 'Ctrl+Mouse Wheel should not trigger trackpad pinch multiplier');
});

test('calculateNormalizedZoomFactor: true continuous trackpad pinch triggers micro-pinch multiplier', () => {
  // Trackpad pinch gesture produces tiny delta with ctrlKey = true
  const trackpadPinch = calculateNormalizedZoomFactor({ deltaY: -5, deltaMode: 0, ctrlKey: true } as any);
  const normalMicroScroll = calculateNormalizedZoomFactor({ deltaY: -5, deltaMode: 0, ctrlKey: false } as any);

  assert.ok(trackpadPinch > normalMicroScroll, 'Trackpad pinch with tiny delta should be properly scaled');
});

test('createEncoderBounceFilter: filters out isolated mechanical encoder bounce', () => {
  const filter = createEncoderBounceFilter();

  // User scrolling UP consistently (-100 per notch)
  assert.equal(filter(-100), -100);
  assert.equal(filter(-100), -100);
  assert.equal(filter(-100), -100);

  // Glitch tick: encoder bounce produces +100 within a few ms
  const glitchResult = filter(100);
  assert.equal(glitchResult, 0, 'Isolated opposite tick within short window should be suppressed as bounce');

  // Next normal tick continues UP
  assert.equal(filter(-100), -100, 'Normal scrolling in primary direction continues uninterrupted');
});

test('calculateAnchorPan: point under mouse remains invariant', () => {
  const containerRect = { left: 100, top: 100 };
  const cursorClientX = 300; // mouse at 200px inside container
  const cursorClientY = 250; // mouse at 150px inside container
  const currentPan = { x: 50, y: 50 };
  const oldZoom = 1.0;
  const newZoom = 2.0;

  const newPan = calculateAnchorPan(
    cursorClientX,
    cursorClientY,
    containerRect,
    currentPan,
    oldZoom,
    newZoom
  );

  // In old coordinates: mouse was at (200 - 50)/1.0 = 150 in graph model
  // In new coordinates: mouse at 200px = 150 * 2.0 + newPan.x => 200 = 300 + newPan.x => newPan.x = -100
  assert.equal(newPan.x, -100);
  // In old coordinates: mouse was at (150 - 50)/1.0 = 100 in graph model
  // In new coordinates: mouse at 150px = 100 * 2.0 + newPan.y => 150 = 200 + newPan.y => newPan.y = -50
  assert.equal(newPan.y, -50);
});
