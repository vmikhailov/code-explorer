import test from 'node:test';
import assert from 'node:assert/strict';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { DIAGRAM_DEFINITIONS, getDiagramDefinition } from './diagramRegistry';
import { GraphSessionStore, SessionGraphData } from './graphSessionStore';
import { KeyValueMemento, ViewPersistenceService } from '../../persistence/viewPersistenceService';

const SAMPLE_GRAPH: SessionGraphData = {
  nodes: [
    { id: 'svc:api', kind: 'Service', name: 'OrdersApi' },
    { id: 'svc:billing', kind: 'Service', name: 'BillingService', properties: { in_cycle: 'true' } },
    { id: 'lib:core', kind: 'Library', name: 'SharedCore', properties: { is_library: 'true', in_cycle: 'true' } },
    { id: 'db:orders', kind: 'Database', name: 'OrdersDb', properties: { is_shared: 'true' } },
    { id: 'test:orders', kind: 'TestSuite', name: 'OrdersTests', properties: { is_test: 'true' } },
  ],
  edges: [
    { id: 'e1', source: 'svc:api', target: 'svc:billing', kind: 'SERVICE_CALL', category: 'service_call' },
    { id: 'e2', source: 'svc:api', target: 'svc:billing', kind: 'PUBLISHES_TO', category: 'messaging' },
    { id: 'e3', source: 'svc:billing', target: 'lib:core', kind: 'DEPENDS_ON', category: 'library', properties: { in_cycle: 'true' } },
    { id: 'e4', source: 'lib:core', target: 'svc:billing', kind: 'DEPENDS_ON', category: 'library', properties: { in_cycle: 'true' } },
    { id: 'e5', source: 'svc:api', target: 'db:orders', kind: 'USES_DB', category: 'database' },
    { id: 'e6', source: 'svc:billing', target: 'db:orders', kind: 'USES_DB', category: 'database' },
  ],
};

test('diagramRegistry: defines all 9 deterministic v2 diagrams', () => {
  assert.equal(DIAGRAM_DEFINITIONS.length, 9);
  assert.equal(getDiagramDefinition('project-dependencies').adapter, 'cytoscape');
  assert.equal(getDiagramDefinition('call-flow').adapter, 'reactflow');
  assert.equal(getDiagramDefinition('layer-dependencies').adapter, 'swimlane');
});

test('GraphSessionStore: hiding an entity type preserves its card in InspectorSnapshot (FullCatalogIndex invariant)', () => {
  const store = new GraphSessionStore(getDiagramDefinition('project-dependencies'), SAMPLE_GRAPH);

  const afterHide = store.toggleEntityTypeVisibility('Database');
  assert.equal(afterHide.projection.nodes.some((n) => n.kind === 'Database'), false);

  const dbSummary = afterHide.inspector.entityTypeSummaries.find((s) => s.kind === 'Database');
  assert.ok(dbSummary, 'Database summary must remain visible in Inspector even when hidden');
  assert.equal(dbSummary.totalCount, 1);
  assert.equal(dbSummary.visibleCount, 0);
  assert.equal(dbSummary.hiddenCount, 1);
  assert.equal(dbSummary.isHiddenByType, true);
});

test('GraphSessionStore: bundles parallel edges, tracks SIG gate overlays, and computes group metrics', () => {
  const store = new GraphSessionStore(getDiagramDefinition('project-dependencies'), SAMPLE_GRAPH);
  store.setGateViolations([
    {
      gateId: 'SIG-CQRS-001',
      severity: 'error',
      sourceNodeId: 'svc:api',
      targetNodeId: 'db:orders',
      message: 'Query handler must not write directly to OrdersDb',
    },
  ]);

  const snap = store.upsertGroup({
    id: 'grp:backend',
    name: 'Backend',
    nodeIds: ['svc:billing', 'lib:core'],
    collapsed: false,
  });

  const bundledApiToBilling = snap.projection.edges.find(
    (e) => e.source === 'svc:api' && e.target === 'svc:billing'
  );
  assert.ok(bundledApiToBilling);
  assert.equal(bundledApiToBilling.totalCount, 2);
  assert.equal(bundledApiToBilling.category, 'mixed');

  const apiToDb = snap.projection.edges.find(
    (e) => e.source === 'svc:api' && e.target === 'db:orders'
  );
  assert.ok(apiToDb);
  assert.deepEqual(apiToDb.violatedGateIds, ['SIG-CQRS-001']);
  assert.deepEqual(snap.projection.violatedGateIdsByNode['svc:api'], ['SIG-CQRS-001']);

  const grp = snap.projection.groups.find((g) => g.id === 'grp:backend');
  assert.ok(grp);
  assert.equal(grp.metrics.memberCount, 2);
  assert.equal(grp.metrics.inboundCount, 2);
  assert.equal(grp.metrics.outboundCount, 1);
  assert.equal(grp.metrics.cycleCount, 2);
});

test('GraphSessionStore: focusedNeighborhood slices graph to 1-hop ego neighborhood', () => {
  const store = new GraphSessionStore(getDiagramDefinition('project-dependencies'), SAMPLE_GRAPH);
  const snap = store.setFocusedNeighborhood({ anchorNodeId: 'db:orders', hops: 1 });
  const visibleIds = snap.projection.nodes.map((n) => n.id).sort();
  assert.deepEqual(visibleIds, ['db:orders', 'svc:api', 'svc:billing']);
});

test('ViewPersistenceService: saves and loads local workspaceState and shared .codeexplorer/views JSON', async () => {
  const mem = new Map<string, unknown>();
  const memento: KeyValueMemento = {
    get: <T>(k: string) => mem.get(k) as T | undefined,
    update: (k: string, v: unknown) => {
      mem.set(k, v);
    },
  };

  const tempRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'ce-view-persist-'));
  try {
    const service = new ViewPersistenceService(memento, tempRoot);
    const store = new GraphSessionStore(getDiagramDefinition('system-map'), SAMPLE_GRAPH);
    store.toggleEntityTypeVisibility('Database');
    const exported = store.exportPersistedState();

    await service.saveLocalView('system-map', 'workspace', exported, { zoom: 1.25, panX: 40, panY: -20 });
    const loadedLocal = service.loadLocalView('system-map', 'workspace');
    assert.ok(loadedLocal);
    assert.equal(loadedLocal.viewport?.zoom, 1.25);
    assert.deepEqual(loadedLocal.session.hiddenEntityTypes, ['Database']);

    const sharedPath = service.saveSharedView('system-map', 'workspace', exported);
    assert.equal(fs.existsSync(sharedPath), true);
    const loadedShared = service.loadSharedView('system-map');
    assert.ok(loadedShared);
    assert.deepEqual(loadedShared.session.hiddenEntityTypes, ['Database']);
  } finally {
    fs.rmSync(tempRoot, { recursive: true, force: true });
  }
});
