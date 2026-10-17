import test from 'node:test';
import assert from 'node:assert/strict';
import { GateItemDto } from '../../../../proto/types';

/**
 * Validates whether an OpenCypher query satisfies the Semantic Invariant Gate Zero-Match return contract.
 */
export function validateGateQueryContract(query: string): {
  hasFile: boolean;
  hasLine: boolean;
  hasSymbol: boolean;
  hasTarget: boolean;
  hasMessage: boolean;
  isFullyCompliant: boolean;
  missingColumns: string[];
} {
  const lower = query.toLowerCase();
  const returnIdx = lower.lastIndexOf('return');
  const returnClause = returnIdx >= 0 ? lower.substring(returnIdx) : lower;

  const hasFile = /\b(as\s+file|as\s+filepath|as\s+path)\b/.test(returnClause);
  const hasLine = /\b(as\s+line|as\s+startline|as\s+row)\b/.test(returnClause);
  const hasSymbol = /\b(as\s+symbol|as\s+name|as\s+source)\b/.test(returnClause);
  const hasTarget = /\b(as\s+target|as\s+destination)\b/.test(returnClause);
  const hasMessage = /\b(as\s+message|as\s+description|as\s+details)\b/.test(returnClause);

  const missingColumns: string[] = [];
  if (!hasFile) missingColumns.push('file');
  if (!hasLine) missingColumns.push('line');
  if (!hasSymbol) missingColumns.push('symbol');
  if (!hasTarget) missingColumns.push('target');
  if (!hasMessage) missingColumns.push('message');

  return {
    hasFile,
    hasLine,
    hasSymbol,
    hasTarget,
    hasMessage,
    isFullyCompliant: hasFile && hasLine && hasSymbol && hasMessage,
    missingColumns,
  };
}

/**
 * Validates gate form input fields before submitting save requests.
 */
export function validateGateForm(gate: Partial<GateItemDto>): {
  isValid: boolean;
  errors: Record<string, string>;
} {
  const errors: Record<string, string> = {};

  if (!gate.id || !gate.id.trim()) {
    errors.id = 'Gate ID is required.';
  } else if (!/^[A-Z0-9_-]+$/i.test(gate.id.trim())) {
    errors.id = 'Gate ID may only contain alphanumeric characters, hyphens, and underscores.';
  }

  if (!gate.name || !gate.name.trim()) {
    errors.name = 'Gate name is required.';
  }

  if (!gate.query || !gate.query.trim()) {
    errors.query = 'OpenCypher query is required.';
  } else {
    const contract = validateGateQueryContract(gate.query);
    if (!contract.hasFile || !contract.hasLine || !contract.hasMessage) {
      errors.queryContract = `Query is missing mandatory projection columns: ${contract.missingColumns.join(', ')}`;
    }
  }

  return {
    isValid: Object.keys(errors).length === 0,
    errors,
  };
}

test('validateGateQueryContract: correctly identifies fully compliant SIG return projections', () => {
  const query = `
    MATCH (f:Function)-[:CALLS]->(callee:Function)
    WHERE callee.name = 'DisallowedMethod'
    RETURN 
      coalesce(f.path, f.file_path) AS file,
      coalesce(f.start_line, 1) AS line,
      f.name AS symbol,
      callee.name AS target,
      'Prohibited call to ' + callee.name AS message
  `;

  const result = validateGateQueryContract(query);
  assert.equal(result.hasFile, true);
  assert.equal(result.hasLine, true);
  assert.equal(result.hasSymbol, true);
  assert.equal(result.hasTarget, true);
  assert.equal(result.hasMessage, true);
  assert.equal(result.isFullyCompliant, true);
  assert.equal(result.missingColumns.length, 0);
});

test('validateGateQueryContract: detects missing contract columns', () => {
  const query = `
    MATCH (p:Project)
    WHERE p.name CONTAINS 'Legacy'
    RETURN p.name AS symbol, 'legacy project found' AS message
  `;

  const result = validateGateQueryContract(query);
  assert.equal(result.hasSymbol, true);
  assert.equal(result.hasMessage, true);
  assert.equal(result.hasFile, false);
  assert.equal(result.hasLine, false);
  assert.equal(result.hasTarget, false);
  assert.equal(result.isFullyCompliant, false);
  assert.deepEqual(result.missingColumns, ['file', 'line', 'target']);
});

test('validateGateForm: rejects missing required fields and invalid ID format', () => {
  const invalidGate: Partial<GateItemDto> = {
    id: '',
    name: '',
    query: '',
  };

  const validation = validateGateForm(invalidGate);
  assert.equal(validation.isValid, false);
  assert.ok(validation.errors.id);
  assert.ok(validation.errors.name);
  assert.ok(validation.errors.query);
});

test('validateGateForm: accepts valid gate configuration', () => {
  const validGate: Partial<GateItemDto> = {
    id: 'SIG-P-B-007',
    name: 'View Decoupling Guarantee',
    category: 'Architectural',
    platform: 'agnostic',
    scope: 'Module',
    dimension: 'Boundaries',
    severity: 'Error',
    query: `
      MATCH (f:Function)
      WHERE f.name = 'Forbidden'
      RETURN f.path AS file, 1 AS line, f.name AS symbol, '' AS target, 'error' AS message
    `,
    ratchetEnabled: true,
  };

  const validation = validateGateForm(validGate);
  assert.equal(validation.isValid, true);
  assert.deepEqual(validation.errors, {});
});
