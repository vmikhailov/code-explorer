import test from 'node:test';
import assert from 'node:assert/strict';

export type SidebarTab = 'info' | 'orbits' | 'hidden' | 'gates';

export function resolveEffectiveSidebarTab(
  tab: SidebarTab,
  hasOrbitLegend: boolean
): SidebarTab {
  if (tab === 'orbits' && !hasOrbitLegend) {
    return 'info';
  }
  return tab;
}

export function handleSelectionChange(
  selectedNode: any | null,
  selectedEdge: any | null,
  currentTab: SidebarTab,
  isSidebarOpen: boolean
): { tab: SidebarTab; isOpen: boolean } {
  if (selectedNode !== null || selectedEdge !== null) {
    return {
      tab: 'info',
      isOpen: true,
    };
  }
  return {
    tab: currentTab,
    isOpen: isSidebarOpen,
  };
}

test('unifiedSidebar: resolveEffectiveSidebarTab falls back to info if orbits not active', () => {
  assert.equal(resolveEffectiveSidebarTab('info', false), 'info');
  assert.equal(resolveEffectiveSidebarTab('info', true), 'info');
  assert.equal(resolveEffectiveSidebarTab('hidden', false), 'hidden');
  assert.equal(resolveEffectiveSidebarTab('hidden', true), 'hidden');
  assert.equal(resolveEffectiveSidebarTab('gates', false), 'gates');
  assert.equal(resolveEffectiveSidebarTab('gates', true), 'gates');

  // When concentric layout is active (hasOrbitLegend = true), orbits tab is retained
  assert.equal(resolveEffectiveSidebarTab('orbits', true), 'orbits');

  // When layout is changed to COSE / Swimlanes (hasOrbitLegend = false), orbits tab falls back to info
  assert.equal(resolveEffectiveSidebarTab('orbits', false), 'info');
});

test('unifiedSidebar: selection change auto-switches to info tab and opens sidebar', () => {
  // 1. Sidebar is currently closed and on hidden tab
  const state1 = handleSelectionChange(
    { id: 'service:payments', displayName: 'PaymentsService' },
    null,
    'hidden',
    false
  );
  assert.equal(state1.isOpen, true, 'Sidebar must open on node selection');
  assert.equal(state1.tab, 'info', 'Sidebar must switch to info tab on node selection');

  // 2. Selecting an edge while viewing orbits tab
  const state2 = handleSelectionChange(
    null,
    { id: 'edge:1', source: 'a', target: 'b' },
    'orbits',
    true
  );
  assert.equal(state2.isOpen, true);
  assert.equal(state2.tab, 'info', 'Edge selection must switch to info tab');

  // 3. Deselecting (both null) preserves current tab and open state (displays empty state)
  const state3 = handleSelectionChange(
    null,
    null,
    'info',
    true
  );
  assert.equal(state3.isOpen, true, 'Deselection preserves open state');
  assert.equal(state3.tab, 'info', 'Deselection preserves info tab so empty state renders');
});
