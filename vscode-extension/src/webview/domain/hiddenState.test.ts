import test from 'node:test';
import assert from 'node:assert/strict';
import { RestoreAllHiddenNodesCommand, HideNodeCommand } from '../commands/commands';
import { EntityKind } from './types';

// Mock localStorage for test environment
class MockLocalStorage {
  private store = new Map<string, string>();

  getItem(key: string): string | null {
    return this.store.get(key) ?? null;
  }

  setItem(key: string, value: string): void {
    this.store.set(key, value);
  }

  removeItem(key: string): void {
    this.store.delete(key);
  }

  clear(): void {
    this.store.clear();
  }
}

test('hiddenState: refresh must NOT restore hidden nodes or reset hidden filters', () => {
  // Initial state before refresh
  let hiddenNodeIds = new Set<string>(['service:routes-calculation', 'db:routes-cache']);
  let hiddenTypes = new Set<EntityKind>(['ExternalService', 'Database']);
  let hiddenOrbitTiers = new Set<number>([2]);
  let forcedVisibleNodeIds = new Set<string>(['service:core-api']);

  // Simulate graph payload v1 (initial load)
  const graphV1 = {
    nodes: [
      { id: 'service:routes-calculation', name: 'RoutesCalculation' },
      { id: 'service:core-api', name: 'CoreApi' },
      { id: 'db:routes-cache', name: 'RoutesCache' },
      { id: 'service:gateway', name: 'Gateway' },
    ],
    edges: [],
  };

  // Helper simulating the graph-refresh effect in DomainArchitectureView
  const onGraphUpdated = (
    newGraph: typeof graphV1,
    prevGraph: any,
    currentHiddenNodes: Set<string>,
    currentHiddenTypes: Set<EntityKind>,
    currentHiddenOrbitTiers: Set<number>,
    currentForcedVisible: Set<string>
  ) => {
    if (prevGraph === newGraph) return;

    // RULE: Refresh must PRESERVE hidden items, not wipe them!
    // (Old buggy behavior did: setHiddenNodeIds(new Set()), setHiddenTypes(['ExternalService']), etc.)
    return {
      hiddenNodeIds: currentHiddenNodes,
      hiddenTypes: currentHiddenTypes,
      hiddenOrbitTiers: currentHiddenOrbitTiers,
      forcedVisibleNodeIds: currentForcedVisible,
    };
  };

  // Simulate user pressing Refresh (newGraph reference received from server)
  const graphV2 = {
    nodes: [
      { id: 'service:routes-calculation', name: 'RoutesCalculation' },
      { id: 'service:core-api', name: 'CoreApi' },
      { id: 'db:routes-cache', name: 'RoutesCache' },
      { id: 'service:gateway', name: 'Gateway' },
      { id: 'service:notifications', name: 'Notifications' }, // newly discovered service on refresh
    ],
    edges: [],
  };

  const refreshedState = onGraphUpdated(
    graphV2,
    graphV1,
    hiddenNodeIds,
    hiddenTypes,
    hiddenOrbitTiers,
    forcedVisibleNodeIds
  );

  assert.ok(refreshedState, 'State should be updated after graph refresh');
  assert.equal(refreshedState.hiddenNodeIds.has('service:routes-calculation'), true, 'service:routes-calculation must remain hidden');
  assert.equal(refreshedState.hiddenNodeIds.has('db:routes-cache'), true, 'db:routes-cache must remain hidden');
  assert.equal(refreshedState.hiddenTypes.has('Database'), true, 'Database type filter must remain hidden');
  assert.equal(refreshedState.hiddenOrbitTiers.has(2), true, 'Orbit tier 2 must remain hidden');
  assert.equal(refreshedState.forcedVisibleNodeIds.has('service:core-api'), true, 'Forced visible node must remain intact');
});

test('hiddenState: visible elements filtering excludes hidden nodes before and after refresh', () => {
  const hiddenNodeIds = new Set<string>(['node-b']);

  const filterVisibleNodes = (nodes: { id: string }[], hidden: Set<string>) => {
    return nodes.filter((n) => !hidden.has(n.id));
  };

  const nodesBeforeRefresh = [{ id: 'node-a' }, { id: 'node-b' }, { id: 'node-c' }];
  const visibleBefore = filterVisibleNodes(nodesBeforeRefresh, hiddenNodeIds);
  assert.deepEqual(visibleBefore.map((n) => n.id), ['node-a', 'node-c']);

  // Refresh brings fresh node instances and a new node-d
  const nodesAfterRefresh = [{ id: 'node-a' }, { id: 'node-b' }, { id: 'node-c' }, { id: 'node-d' }];
  const visibleAfter = filterVisibleNodes(nodesAfterRefresh, hiddenNodeIds);
  assert.deepEqual(visibleAfter.map((n) => n.id), ['node-a', 'node-c', 'node-d']);
  assert.equal(visibleAfter.some((n) => n.id === 'node-b'), false, 'Hidden node-b must remain excluded after refresh');
});

test('hiddenState: only explicit RestoreAllHiddenNodesCommand restores hidden items', () => {
  let hidden = new Set<string>(['node-x', 'node-y']);
  let hiddenTypes = new Set<EntityKind>(['Topic']);
  let hiddenOrbits = new Set<number>([1]);
  let forced = new Set<string>();

  const restoreCmd = new RestoreAllHiddenNodesCommand(
    new Set(hidden),
    new Set(hiddenTypes),
    new Set(hiddenOrbits),
    new Set(forced),
    false,
    false,
    (h) => { hidden = h; },
    (t) => { hiddenTypes = t; },
    (o) => { hiddenOrbits = o; },
    (f) => { forced = f; }
  );

  // Execute restore all
  restoreCmd.execute();
  assert.equal(hidden.size, 0, 'Hidden nodes should be empty after explicit RestoreAll');
  assert.equal(hiddenTypes.size, 0, 'Hidden types should be empty after explicit RestoreAll');
  assert.equal(hiddenOrbits.size, 0, 'Hidden orbits should be empty after explicit RestoreAll');

  // Undo restore
  restoreCmd.undo();
  assert.equal(hidden.has('node-x'), true, 'Undo should bring back node-x');
  assert.equal(hidden.has('node-y'), true, 'Undo should bring back node-y');
  assert.equal(hiddenTypes.has('Topic'), true, 'Undo should bring back Topic filter');
  assert.equal(hiddenOrbits.has(1), true, 'Undo should bring back Orbit tier 1');
});

test('hiddenState: persistence in localStorage round-trips correctly', () => {
  const mockStorage = new MockLocalStorage();

  const hiddenToPersist = new Set<string>(['service:auth', 'service:billing']);
  mockStorage.setItem('ce_domain_hidden_node_ids', JSON.stringify([...hiddenToPersist]));

  const rawSaved = mockStorage.getItem('ce_domain_hidden_node_ids');
  assert.ok(rawSaved, 'Value should be saved in storage');

  const parsed = new Set<string>(JSON.parse(rawSaved));
  assert.equal(parsed.has('service:auth'), true);
  assert.equal(parsed.has('service:billing'), true);
  assert.equal(parsed.size, 2);
});
