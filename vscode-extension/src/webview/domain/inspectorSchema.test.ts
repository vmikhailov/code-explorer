import test from 'node:test';
import assert from 'node:assert/strict';
import {
  createArchitectureInspectorSchema,
  evaluateNodeHeader,
  computeNodeConnections,
} from './inspectorSchema';
import { computeEchelonTierReasons } from '../layout/concentricLayout';
import { SelectedNodeDetail } from './types';

test('inspectorSchema: evaluateNodeHeader extracts title, kind, tierLabel, tierReason, and chips', () => {
  const schema = createArchitectureInspectorSchema({});
  const node: SelectedNodeDetail = {
    id: 'svc:orders',
    name: 'OrdersService',
    displayName: 'Orders Service',
    kind: 'Service',
    displayTag: 'SVC',
    bgColor: '#0284c7',
    borderColor: '#38bdf8',
    projects: [],
    framework: 'ASP.NET Core',
    language: 'C#',
    gitBranch: 'main',
    domain: 'Commerce',
    secondaryRoles: ['Worker'],
    inboundCallsCount: 3,
    outboundCallsCount: 2,
    dbCount: 1,
    messagingCount: 1,
    tier: 1,
    tierLabel: 'Tier 1 (First Echelon / Gateway-facing)',
    tierReason: 'Gateway-facing service called directly by ApiGateway',
  };

  const header = evaluateNodeHeader(schema, node);
  assert.equal(header.id, 'svc:orders');
  assert.equal(header.title, 'Orders Service');
  assert.equal(header.kind, 'Service');
  assert.equal(header.tier, 1);
  assert.equal(header.tierLabel, 'Tier 1 (First Echelon / Gateway-facing)');
  assert.equal(header.tierReason, 'Gateway-facing service called directly by ApiGateway');
  assert.deepEqual(header.secondaryRoles, ['Worker']);
  assert.equal(header.chips.length, 4);
});

test('inspectorSchema: computeNodeConnections classifies inbound, outbound, db, and topic links', () => {
  const ordersNode: SelectedNodeDetail = {
    id: 'svc:orders',
    name: 'OrdersService',
    displayName: 'Orders Service',
    kind: 'Service',
    displayTag: 'SVC',
    bgColor: '#0284c7',
    borderColor: '#38bdf8',
    projects: [],
    inboundCallsCount: 1,
    outboundCallsCount: 1,
    dbCount: 1,
    messagingCount: 1,
  };
  const detailMap = new Map<string, SelectedNodeDetail>([
    ['svc:gateway', { ...ordersNode, id: 'svc:gateway', displayName: 'API Gateway', kind: 'Ingress' }],
    ['svc:orders', ordersNode],
    ['svc:billing', { ...ordersNode, id: 'svc:billing', displayName: 'Billing Service', kind: 'Service' }],
    ['db:orders', { ...ordersNode, id: 'db:orders', displayName: 'orders_db', kind: 'Database' }],
    ['top:events', { ...ordersNode, id: 'top:events', displayName: 'order-events', kind: 'Topic' }],
  ]);

  const conns = computeNodeConnections(
    ordersNode,
    [
      { id: 'e1', source: 'svc:gateway', target: 'svc:orders', category: 'service_call', label: 'CALLS', count: 4 },
      { id: 'e2', source: 'svc:orders', target: 'svc:billing', category: 'service_call', label: 'CALLS', count: 2 },
      { id: 'e3', source: 'svc:orders', target: 'db:orders', category: 'database', label: 'USES_DB', count: 5 },
      { id: 'e4', source: 'svc:orders', target: 'top:events', category: 'messaging', label: 'PUBLISHES', count: 1 },
    ],
    detailMap
  );

  assert.equal(conns.inboundConnections.length, 1);
  assert.equal(conns.inboundConnections[0].name, 'API Gateway');
  assert.equal(conns.outboundConnections.length, 1);
  assert.equal(conns.outboundConnections[0].name, 'Billing Service');
  assert.equal(conns.dbConnections.length, 1);
  assert.equal(conns.dbConnections[0].name, 'orders_db');
  assert.equal(conns.topicConnections.length, 1);
  assert.equal(conns.topicConnections[0].badgeText, 'Publishes');
});

test('computeEchelonTierReasons: produces human-readable explainability strings for each tier', () => {
  const reasons = computeEchelonTierReasons(
    [
      { id: 'app:web', kind: 'App', displayName: 'Web Portal' },
      { id: 'svc:orders', kind: 'Service', displayName: 'Orders Service' },
      { id: 'svc:Fulfillment', kind: 'Service', displayName: 'Fulfillment Service' },
      { id: 'top:orders', kind: 'Topic', displayName: 'orders.topic' },
      { id: 'db:pg', kind: 'Database', displayName: 'Postgres' },
    ],
    [
      { source: 'app:web', target: 'svc:orders', category: 'service_call' },
      { source: 'svc:orders', target: 'svc:Fulfillment', category: 'service_call' },
    ]
  );

  assert.match(reasons.get('app:web') || '', /Orbit 0/i);
  assert.match(reasons.get('svc:orders') || '', /Web Portal/);
  assert.match(reasons.get('svc:Fulfillment') || '', /Orders Service/);
  assert.match(reasons.get('top:orders') || '', /Orbit 2/i);
  assert.match(reasons.get('db:pg') || '', /Orbit 4/i);
});
