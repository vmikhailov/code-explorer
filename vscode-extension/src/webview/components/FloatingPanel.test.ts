import test from 'node:test';
import assert from 'node:assert/strict';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { FloatingPanel } from './FloatingPanel';
import { EyeOffIcon, LayersIcon, RotateCcwIcon, TagIcon, GitBranchIcon } from './Icons';

test('FloatingPanel: renders correct dock corner classes and attributes', () => {
  const htmlTopRight = renderToStaticMarkup(
    React.createElement(
      FloatingPanel,
      { dock: 'top-right', title: 'Node Details' },
      React.createElement('div', null, 'Content')
    )
  );
  assert.ok(htmlTopRight.includes('dock-top-right'), 'Must include dock-top-right class');
  assert.ok(htmlTopRight.includes('aria-label="Node Details"'), 'Must have correct aria-label');
  assert.ok(!htmlTopRight.includes('is-collapsed'), 'Must not be collapsed by default');

  const htmlBottomLeft = renderToStaticMarkup(
    React.createElement(
      FloatingPanel,
      { dock: 'bottom-left', title: 'Orbit Legend' },
      React.createElement('div', null, 'Orbits')
    )
  );
  assert.ok(htmlBottomLeft.includes('dock-bottom-left'), 'Must include dock-bottom-left class');
});

test('FloatingPanel: collapsed state renders minimal corner pill with correct badges and title', () => {
  const htmlCollapsed = renderToStaticMarkup(
    React.createElement(FloatingPanel, {
      dock: 'top-left',
      title: 'Hidden Entities',
      isCollapsed: true,
      collapsedTitle: 'Hidden (5)',
      collapsedBadge: React.createElement('span', { className: 'test-badge' }, '5'),
      icon: React.createElement(EyeOffIcon, { size: 14 }),
    })
  );

  assert.ok(htmlCollapsed.includes('is-collapsed'), 'Must have is-collapsed class');
  assert.ok(htmlCollapsed.includes('floating-dock-collapsed-pill'), 'Must render collapsed pill');
  assert.ok(htmlCollapsed.includes('Hidden (5)'), 'Must render collapsedTitle');
  assert.ok(htmlCollapsed.includes('test-badge'), 'Must render collapsedBadge');
  assert.ok(!htmlCollapsed.includes('floating-dock-body'), 'Must not render panel body when collapsed');
});

test('FloatingPanel: 2-row header architecture isolates subtitle/metadata from action buttons', () => {
  const htmlExpanded = renderToStaticMarkup(
    React.createElement(
      FloatingPanel,
      {
        dock: 'top-right',
        title: 'orders-service',
        badge: React.createElement('span', { className: 'kind-badge' }, ':SERVICE'),
        subtitle: React.createElement(
          'div',
          { className: 'custom-sub' },
          React.createElement('span', null, 'Tier 1 (Core Services)'),
          React.createElement(GitBranchIcon, { size: 11 })
        ),
        headerActions: React.createElement('button', { className: 'custom-action' }, 'Hide'),
        onClose: () => {},
        onToggleCollapse: () => {},
      },
      React.createElement('div', { className: 'body-content' }, 'Metrics and Calls')
    )
  );

  // Verify Row 1: Header Main with title, badge, controls
  assert.ok(htmlExpanded.includes('floating-dock-header-main'), 'Row 1 main bar must exist');
  assert.ok(htmlExpanded.includes(':SERVICE'), 'Badge must render');
  assert.ok(htmlExpanded.includes('orders-service'), 'Title must render');
  assert.ok(htmlExpanded.includes('custom-action'), 'Action button must render in controls');
  assert.ok(htmlExpanded.includes('collapse-btn'), 'Collapse button must render');
  assert.ok(htmlExpanded.includes('close-btn'), 'Close button must render');

  // Verify Row 2: Subtitle is isolated in floating-dock-header-sub
  assert.ok(htmlExpanded.includes('floating-dock-header-sub'), 'Row 2 subtitle wrapper must exist');
  assert.ok(htmlExpanded.includes('custom-sub'), 'Subtitle content must render in sub-row');
  assert.ok(htmlExpanded.includes('Tier 1 (Core Services)'), 'Tier string renders on dedicated row');

  // Verify Body
  assert.ok(htmlExpanded.includes('floating-dock-body'), 'Panel body must render');
  assert.ok(htmlExpanded.includes('Metrics and Calls'), 'Body content must render');
});

test('Icons: clean stroke SVG icons render with consistent viewBox and zero colorful emojis', () => {
  const eyeHtml = renderToStaticMarkup(React.createElement(EyeOffIcon, { size: 14 }));
  const layersHtml = renderToStaticMarkup(React.createElement(LayersIcon, { size: 14 }));
  const resetHtml = renderToStaticMarkup(React.createElement(RotateCcwIcon, { size: 12 }));
  const tagHtml = renderToStaticMarkup(React.createElement(TagIcon, { size: 12 }));

  for (const svgHtml of [eyeHtml, layersHtml, resetHtml, tagHtml]) {
    assert.ok(svgHtml.includes('<svg'), 'Must render SVG element');
    assert.ok(svgHtml.includes('viewBox="0 0 24 24"'), 'Must have uniform 24x24 viewBox');
    assert.ok(svgHtml.includes('stroke="currentColor"'), 'Must use stroke="currentColor"');
    // Ensure no emojis are included
    assert.ok(!/[\uD800-\uDFFF]/.test(svgHtml), 'Must not contain any emoji surrogate pairs');
  }
});

test('FloatingPanel: resizable mode renders directional handles and corner grip when expanded, and omits them when collapsed or resizable=false', () => {
  const htmlBottomLeft = renderToStaticMarkup(
    React.createElement(
      FloatingPanel,
      { dock: 'bottom-left', title: 'Orbit Legend', width: 360, resizable: true },
      React.createElement('div', null, 'Orbits')
    )
  );

  assert.ok(htmlBottomLeft.includes('floating-dock-resize-handle-x handle-right'), 'Must render right-edge resize handle for bottom-left dock');
  assert.ok(htmlBottomLeft.includes('floating-dock-resize-handle-y handle-top'), 'Must render top-edge resize handle for bottom-left dock');
  assert.ok(htmlBottomLeft.includes('floating-dock-resize-handle-corner corner-top-right'), 'Must render top-right corner handle for bottom-left dock');
  assert.ok(htmlBottomLeft.includes('floating-dock-corner-grip'), 'Must render corner grip icon');
  assert.ok(htmlBottomLeft.includes('width:360px'), 'Must render 360px width');

  const htmlTopRight = renderToStaticMarkup(
    React.createElement(
      FloatingPanel,
      { dock: 'top-right', title: 'Node Details', width: 380, resizable: true },
      React.createElement('div', null, 'Details')
    )
  );
  assert.ok(htmlTopRight.includes('floating-dock-resize-handle-x handle-left'), 'Must render left-edge handle for top-right dock');
  assert.ok(htmlTopRight.includes('floating-dock-resize-handle-y handle-bottom'), 'Must render bottom-edge handle for top-right dock');
  assert.ok(htmlTopRight.includes('floating-dock-resize-handle-corner corner-bottom-left'), 'Must render bottom-left corner handle for top-right dock');

  const htmlNonResizable = renderToStaticMarkup(
    React.createElement(
      FloatingPanel,
      { dock: 'top-right', title: 'Fixed Panel', resizable: false },
      React.createElement('div', null, 'Content')
    )
  );
  assert.ok(!htmlNonResizable.includes('floating-dock-resize-handle-x'), 'Must not render resize handles when resizable=false');
});
