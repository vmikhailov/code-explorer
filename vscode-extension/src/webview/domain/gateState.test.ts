import test from 'node:test';
import assert from 'node:assert/strict';
import { GateSuiteSummaryDto, GateItemDto } from '../../../../proto/types';

export function recalculateSuiteTotals(gates: GateItemDto[]): Omit<GateSuiteSummaryDto, 'gates' | 'totalDurationMs'> {
  let passed = 0;
  let failed = 0;
  let newViolations = 0;
  let toleratedViolations = 0;
  let ratchetOpportunities = 0;

  for (const g of gates) {
    if (g.status === 'Passed') {
      passed++;
    } else if (g.status === 'PassedTolerated') {
      passed++;
      toleratedViolations += g.toleratedViolationsCount || 0;
    } else if (g.status === 'FailedNew' || g.status === 'Error') {
      failed++;
      newViolations += g.newViolationsCount || 0;
    }
    if (g.canRatchetDown) {
      ratchetOpportunities++;
    }
  }

  return {
    totalGates: gates.length,
    passedCount: passed,
    failedCount: failed,
    newViolationsCount: newViolations,
    toleratedViolationsCount: toleratedViolations,
    ratchetOpportunities,
    isSuccess: failed === 0,
  };
}

export function updateGateInSuite(
  existingSuite: GateSuiteSummaryDto,
  updatedGate: GateItemDto
): GateSuiteSummaryDto {
  const nextGates = [...existingSuite.gates];
  const idx = nextGates.findIndex((g) => g.id === updatedGate.id);
  if (idx >= 0) {
    nextGates[idx] = updatedGate;
  } else {
    nextGates.push(updatedGate);
  }

  const totals = recalculateSuiteTotals(nextGates);
  return {
    ...existingSuite,
    ...totals,
    gates: nextGates,
  };
}

export class InFlightDeduplicator<T> {
  private inFlight = new Map<string, Promise<T>>();

  async execute(key: string, fn: () => Promise<T>): Promise<{ result: T; wasDeduplicated: boolean }> {
    const existing = this.inFlight.get(key);
    if (existing) {
      const result = await existing;
      return { result, wasDeduplicated: true };
    }

    const promise = fn().finally(() => {
      this.inFlight.delete(key);
    });

    this.inFlight.set(key, promise);
    const result = await promise;
    return { result, wasDeduplicated: false };
  }

  isInFlight(key: string): boolean {
    return this.inFlight.has(key);
  }
}

test('recalculateSuiteTotals: correctly aggregates passed, tolerated, failed, and ratchet counts', () => {
  const gates: GateItemDto[] = [
    {
      id: 'gate-1',
      name: 'Gate 1',
      category: 'Architecture',
      platform: 'any',
      scope: 'workspace',
      dimension: 'modularity',
      severity: 'error',
      query: 'MATCH ...',
      source: 'test',
      ratchetEnabled: true,
      status: 'Passed',
      totalViolations: 0,
      newViolationsCount: 0,
      toleratedViolationsCount: 0,
      baselineCount: 0,
      canRatchetDown: false,
      durationMs: 10,
      violations: [],
    },
    {
      id: 'gate-2',
      name: 'Gate 2',
      category: 'Architecture',
      platform: 'any',
      scope: 'workspace',
      dimension: 'modularity',
      severity: 'warning',
      query: 'MATCH ...',
      source: 'test',
      ratchetEnabled: true,
      status: 'PassedTolerated',
      totalViolations: 3,
      newViolationsCount: 0,
      toleratedViolationsCount: 3,
      baselineCount: 3,
      canRatchetDown: false,
      durationMs: 15,
      violations: [],
    },
    {
      id: 'gate-3',
      name: 'Gate 3',
      category: 'Architecture',
      platform: 'any',
      scope: 'workspace',
      dimension: 'modularity',
      severity: 'error',
      query: 'MATCH ...',
      source: 'test',
      ratchetEnabled: true,
      status: 'FailedNew',
      totalViolations: 2,
      newViolationsCount: 2,
      toleratedViolationsCount: 0,
      baselineCount: 0,
      canRatchetDown: false,
      durationMs: 20,
      violations: [],
    },
    {
      id: 'gate-4',
      name: 'Gate 4',
      category: 'Architecture',
      platform: 'any',
      scope: 'workspace',
      dimension: 'modularity',
      severity: 'warning',
      query: 'MATCH ...',
      source: 'test',
      ratchetEnabled: true,
      status: 'Passed',
      totalViolations: 0,
      newViolationsCount: 0,
      toleratedViolationsCount: 0,
      baselineCount: 2,
      canRatchetDown: true,
      durationMs: 8,
      violations: [],
    },
  ];

  const totals = recalculateSuiteTotals(gates);
  assert.equal(totals.totalGates, 4);
  assert.equal(totals.passedCount, 3); // gate-1, gate-2 (PassedTolerated counts as passed), gate-4
  assert.equal(totals.failedCount, 1); // gate-3
  assert.equal(totals.newViolationsCount, 2);
  assert.equal(totals.toleratedViolationsCount, 3);
  assert.equal(totals.ratchetOpportunities, 1);
  assert.equal(totals.isSuccess, false);
});

test('updateGateInSuite: updates gate and accurately recalculates summary stats', () => {
  const initialSuite: GateSuiteSummaryDto = {
    totalGates: 2,
    passedCount: 1,
    failedCount: 1,
    newViolationsCount: 1,
    toleratedViolationsCount: 0,
    ratchetOpportunities: 0,
    isSuccess: false,
    totalDurationMs: 50,
    gates: [
      {
        id: 'gate-1',
        name: 'Gate 1',
        category: 'Architecture',
        platform: 'any',
        scope: 'workspace',
        dimension: 'modularity',
        severity: 'error',
        query: 'MATCH ...',
        source: 'test',
        ratchetEnabled: true,
        status: 'Passed',
        totalViolations: 0,
        newViolationsCount: 0,
        toleratedViolationsCount: 0,
        baselineCount: 0,
        canRatchetDown: false,
        durationMs: 10,
        violations: [],
      },
      {
        id: 'gate-2',
        name: 'Gate 2',
        category: 'Architecture',
        platform: 'any',
        scope: 'workspace',
        dimension: 'modularity',
        severity: 'error',
        query: 'MATCH ...',
        source: 'test',
        ratchetEnabled: true,
        status: 'FailedNew',
        totalViolations: 1,
        newViolationsCount: 1,
        toleratedViolationsCount: 0,
        baselineCount: 0,
        canRatchetDown: false,
        durationMs: 20,
        violations: [],
      },
    ],
  };

  // Now gate-2 is fixed and passes
  const updatedGate2: GateItemDto = {
    ...initialSuite.gates[1],
    status: 'Passed',
    totalViolations: 0,
    newViolationsCount: 0,
  };

  const updatedSuite = updateGateInSuite(initialSuite, updatedGate2);
  assert.equal(updatedSuite.totalGates, 2);
  assert.equal(updatedSuite.passedCount, 2);
  assert.equal(updatedSuite.failedCount, 0);
  assert.equal(updatedSuite.newViolationsCount, 0);
  assert.equal(updatedSuite.isSuccess, true);
});

test('InFlightDeduplicator: deduplicates concurrent requests for the same scope', async () => {
  const deduplicator = new InFlightDeduplicator<string>();
  let executionsCount = 0;

  const asyncJob = () =>
    new Promise<string>((resolve) => {
      setTimeout(() => {
        executionsCount++;
        resolve('data-result');
      }, 50);
    });

  // Launch two concurrent requests for the same key
  const [call1, call2] = await Promise.all([
    deduplicator.execute('all', asyncJob),
    deduplicator.execute('all', asyncJob),
  ]);

  assert.equal(executionsCount, 1, 'underlying job should only execute once');
  assert.equal(call1.result, 'data-result');
  assert.equal(call2.result, 'data-result');
  assert.equal(call1.wasDeduplicated, false);
  assert.equal(call2.wasDeduplicated, true);

  // Sequential call after completion executes anew
  const call3 = await deduplicator.execute('all', asyncJob);
  assert.equal(executionsCount, 2, 'subsequent call executes afresh after previous completed');
  assert.equal(call3.wasDeduplicated, false);
});

test('Gate definitions without evaluation: isEvaluated is false when all gates are pending', () => {
  const definitionsSuite: GateSuiteSummaryDto = {
    totalGates: 2,
    passedCount: 0,
    failedCount: 0,
    newViolationsCount: 0,
    toleratedViolationsCount: 0,
    ratchetOpportunities: 0,
    isSuccess: true,
    totalDurationMs: 0,
    gates: [
      {
        id: 'no-direct-db',
        name: 'No Direct DB',
        category: 'Architecture',
        platform: 'any',
        scope: 'workspace',
        dimension: 'modularity',
        severity: 'error',
        query: 'MATCH ...',
        source: 'test',
        ratchetEnabled: false,
        status: 'Pending',
        totalViolations: 0,
        newViolationsCount: 0,
        toleratedViolationsCount: 0,
        baselineCount: 0,
        canRatchetDown: false,
        durationMs: 0,
        violations: [],
      },
      {
        id: 'no-cross-domain',
        name: 'No Cross Domain',
        category: 'Architecture',
        platform: 'any',
        scope: 'workspace',
        dimension: 'modularity',
        severity: 'error',
        query: 'MATCH ...',
        source: 'test',
        ratchetEnabled: false,
        status: 'Pending',
        totalViolations: 0,
        newViolationsCount: 0,
        toleratedViolationsCount: 0,
        baselineCount: 0,
        canRatchetDown: false,
        durationMs: 0,
        violations: [],
      },
    ],
  };

  const isEvaluated = definitionsSuite.gates.some((g) => g.status !== 'Pending');
  assert.equal(isEvaluated, false, 'definitions suite should not be marked evaluated');

  // After explicit evaluation of one gate:
  const evaluatedGate = { ...definitionsSuite.gates[0], status: 'Passed' as const };
  const nextSuite = updateGateInSuite(definitionsSuite, evaluatedGate);
  const nextIsEvaluated = nextSuite.gates.some((g) => g.status !== 'Pending');
  assert.equal(nextIsEvaluated, true, 'suite should be marked evaluated once a gate has been checked');
});

