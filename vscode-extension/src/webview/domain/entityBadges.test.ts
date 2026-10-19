import { test } from 'node:test';
import assert from 'node:assert/strict';
import { getEntityBadgeMeta, getNodeBadgeSvgDataUri } from './entityBadges';

test('entityBadges: getEntityBadgeMeta maps all standard and alias kinds correctly', () => {
  const cases: Array<{ kind: string; expectedCode: string; expectedBgColor: string }> = [
    { kind: 'Service', expectedCode: 'SVC', expectedBgColor: '#e53935' },
    { kind: 'App', expectedCode: 'APP', expectedBgColor: '#0288d1' },
    { kind: 'Ingress', expectedCode: 'APP', expectedBgColor: '#0288d1' },
    { kind: 'FrontendApp', expectedCode: 'APP', expectedBgColor: '#0288d1' },
    { kind: 'Worker', expectedCode: 'WRK', expectedBgColor: '#c026d3' },
    { kind: 'CliTool', expectedCode: 'CLI', expectedBgColor: '#d97706' },
    { kind: 'Database', expectedCode: 'DB', expectedBgColor: '#7b1fa2' },
    { kind: 'Topic', expectedCode: 'MSG', expectedBgColor: '#f59e0b' },
    { kind: 'ExternalService', expectedCode: 'EXT', expectedBgColor: '#26a69a' },
    { kind: 'Library', expectedCode: 'LIB', expectedBgColor: '#4b5563' },
    { kind: 'TestSuite', expectedCode: 'TST', expectedBgColor: '#059669' },
    { kind: 'Group', expectedCode: 'GRP', expectedBgColor: '#4f46e5' },
    { kind: 'group', expectedCode: 'GRP', expectedBgColor: '#4f46e5' },
  ];

  for (const c of cases) {
    const meta = getEntityBadgeMeta(c.kind);
    assert.equal(meta.code, c.expectedCode, `kind ${c.kind} should have code ${c.expectedCode}`);
    assert.equal(meta.bgColor, c.expectedBgColor, `kind ${c.kind} should have bgColor ${c.expectedBgColor}`);
    assert.ok(meta.rawIconSvg.length > 0, `kind ${c.kind} should have non-empty rawIconSvg`);
  }
});

test('entityBadges: getNodeBadgeSvgDataUri returns valid SVG data URI with 3-letter code', () => {
  const uri = getNodeBadgeSvgDataUri('Service');
  assert.ok(uri.startsWith('data:image/svg+xml;utf8,'), 'Should start with SVG data URI prefix');
  const decoded = decodeURIComponent(uri.replace('data:image/svg+xml;utf8,', ''));
  assert.ok(decoded.includes('<svg'), 'Should contain svg tag');
  assert.ok(decoded.includes('SVC'), 'Should contain SVC text');
  assert.ok(decoded.includes('viewBox="0 0 64 64"'), 'Should have standard 64x64 viewBox');
});

test('entityBadges: getNodeBadgeSvgDataUri with isGroup returns dashed cluster ring and count indicator', () => {
  const uri = getNodeBadgeSvgDataUri('Topic', { isGroup: true, count: 14 });
  assert.ok(uri.startsWith('data:image/svg+xml;utf8,'), 'Should start with SVG data URI prefix');
  const decoded = decodeURIComponent(uri.replace('data:image/svg+xml;utf8,', ''));
  assert.ok(decoded.includes('stroke-dasharray="4 2"'), 'Should contain dashed cluster ring');
  assert.ok(decoded.includes('MSG ×14'), 'Should contain count indicator MSG ×14');
});
