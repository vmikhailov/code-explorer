import test from 'node:test';
import assert from 'node:assert/strict';
import { GateItemDto } from '../../../../proto/types';
import { highlightCypher } from './components/CypherEditor';

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

  // Check if query returns direct node/edge identifiers (v2 Node-First & Edge-Aware contract)
  const returnExprBody = returnClause
    .replace(/^return\s+(distinct\s+)?/i, '')
    .split(/\b(order\s+by|limit|skip)\b/i)[0]
    .trim();
  const returnTerms = returnExprBody
    .split(',')
    .map((t) => t.trim())
    .filter(Boolean);
  const hasDirectNodeReturn =
    returnTerms.length > 0 && returnTerms.some((t) => /^[a-z_][a-z0-9_]*(\s+as\s+[a-z_][a-z0-9_]*)?$/i.test(t));

  const missingColumns: string[] = [];
  if (!hasDirectNodeReturn) {
    if (!hasFile) missingColumns.push('file');
    if (!hasLine) missingColumns.push('line');
    if (!hasSymbol) missingColumns.push('symbol');
    if (!hasTarget) missingColumns.push('target');
    if (!hasMessage) missingColumns.push('message');
  }

  return {
    hasFile: hasFile || hasDirectNodeReturn,
    hasLine: hasLine || hasDirectNodeReturn,
    hasSymbol: hasSymbol || hasDirectNodeReturn,
    hasTarget: hasTarget || hasDirectNodeReturn,
    hasMessage,
    isFullyCompliant: (hasFile && hasLine && hasSymbol && hasMessage) || hasDirectNodeReturn,
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
    const hasGateMessage = Boolean(gate.message && gate.message.trim());
    if (!contract.hasFile || !contract.hasLine || (!contract.hasMessage && !hasGateMessage && !contract.isFullyCompliant)) {
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

test('validateGateQueryContract: detects missing contract columns on scalar-only projections', () => {
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

test('validateGateQueryContract: accepts v2 node-first and edge-aware RETURN clauses', () => {
  const query = `
    MATCH (caller:Function)-[r:CALLS]->(callee:Function)
    WHERE callee.name = 'DisallowedMethod'
    RETURN caller, r, callee
  `;

  const result = validateGateQueryContract(query);
  assert.equal(result.hasFile, true);
  assert.equal(result.hasLine, true);
  assert.equal(result.hasSymbol, true);
  assert.equal(result.hasTarget, true);
  assert.equal(result.isFullyCompliant, true);
  assert.equal(result.missingColumns.length, 0);
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

test('validateGateForm: accepts valid gate configuration (v1 and v2)', () => {
  const validGateV1: Partial<GateItemDto> = {
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

  assert.equal(validateGateForm(validGateV1).isValid, true);

  const validGateV2: Partial<GateItemDto> = {
    id: 'SIG-CUSTOM-002',
    name: 'Node-First Boundary Rule',
    category: 'Architectural',
    platform: 'agnostic',
    scope: 'Module',
    dimension: 'Boundaries',
    severity: 'Error',
    message: 'Function {caller.name} must not call {callee.name}',
    thresholds: { warning: 1, error: 5 },
    query: `
      MATCH (caller:Function)-[r:CALLS]->(callee:Function)
      WHERE callee.name = 'Forbidden'
      RETURN caller, r, callee
    `,
    ratchetEnabled: true,
  };

  assert.equal(validateGateForm(validGateV2).isValid, true);
});

test('highlightCypher: single-pass tokenization handles comments, strings, keywords, labels and relationships without corrupting markup', () => {
  const cypher = `// Look for direct calls
MATCH (f:Function)-[:CALLS]->(t:Target)
WHERE f.name = "MyFunction" AND $threshold > 10
RETURN f.path AS file, 1 AS line, count(t) AS symbol, '' AS target, "Violates rule" AS message`;

  const html = highlightCypher(cypher);

  // Comments
  assert.ok(html.includes('<span style="color:#6a9955;font-style:italic">// Look for direct calls</span>'));
  // Keywords
  assert.ok(html.includes('<span style="color:#569cd6;font-weight:600">MATCH</span>'));
  assert.ok(html.includes('<span style="color:#569cd6;font-weight:600">WHERE</span>'));
  assert.ok(html.includes('<span style="color:#569cd6;font-weight:600">AND</span>'));
  assert.ok(html.includes('<span style="color:#569cd6;font-weight:600">RETURN</span>'));
  // Labels
  assert.ok(html.includes('<span style="color:#4ec9b0;font-weight:500">:Function</span>'));
  assert.ok(html.includes('<span style="color:#4ec9b0;font-weight:500">:Target</span>'));
  // Relationships
  assert.ok(html.includes('<span style="color:#4fc1ff;font-weight:500">-[:CALLS]-&gt;</span>'));
  // Strings
  assert.ok(html.includes('<span style="color:#ce9178">&quot;MyFunction&quot;</span>'));
  assert.ok(html.includes('<span style="color:#ce9178">&quot;Violates rule&quot;</span>'));
  // Parameters
  assert.ok(html.includes('<span style="color:#9cdcfe">$threshold</span>'));
  // Numbers
  assert.ok(html.includes('<span style="color:#b5cea8">10</span>'));
  assert.ok(html.includes('<span style="color:#b5cea8">1</span>'));
  // Functions
  assert.ok(html.includes('<span style="color:#dcdcaa">count</span>'));

  // Ensure no unclosed or nested span corruption
  const openSpans = (html.match(/<span/g) || []).length;
  const closeSpans = (html.match(/<\/span>/g) || []).length;
  assert.equal(openSpans, closeSpans);
  assert.ok(openSpans > 10);
});
