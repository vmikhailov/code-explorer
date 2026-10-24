import { test } from 'node:test';
import assert from 'node:assert/strict';
import { getEntityBadgeMeta, getNodeBadgeSvgDataUri, NodeShape } from './entityBadges';

test('entityBadges: getEntityBadgeMeta maps all standard and alias kinds and shapes correctly', () => {
  const cases: Array<{ kind: string; expectedCode: string; expectedBgColor: string; expectedShape: NodeShape }> = [
    { kind: 'Service', expectedCode: 'SVC', expectedBgColor: '#e53935', expectedShape: 'ellipse' },
    { kind: 'App', expectedCode: 'APP', expectedBgColor: '#0288d1', expectedShape: 'ellipse' },
    { kind: 'Ingress', expectedCode: 'APP', expectedBgColor: '#0288d1', expectedShape: 'ellipse' },
    { kind: 'FrontendApp', expectedCode: 'APP', expectedBgColor: '#0288d1', expectedShape: 'ellipse' },
    { kind: 'Worker', expectedCode: 'WRK', expectedBgColor: '#c026d3', expectedShape: 'ellipse' },
    { kind: 'CliTool', expectedCode: 'CLI', expectedBgColor: '#d97706', expectedShape: 'ellipse' },
    { kind: 'Database', expectedCode: 'DB', expectedBgColor: '#7b1fa2', expectedShape: 'round-rectangle' },
    { kind: 'Table', expectedCode: 'TBL', expectedBgColor: '#7b1fa2', expectedShape: 'round-rectangle' },
    { kind: 'Procedure', expectedCode: 'PRC', expectedBgColor: '#6d28d9', expectedShape: 'round-rectangle' },
    { kind: 'Module', expectedCode: 'MOD', expectedBgColor: '#2563eb', expectedShape: 'ellipse' },
    { kind: 'ProjectModule', expectedCode: 'MOD', expectedBgColor: '#2563eb', expectedShape: 'ellipse' },
    { kind: 'Topic', expectedCode: 'MSG', expectedBgColor: '#f59e0b', expectedShape: 'round-rectangle' },
    { kind: 'ExternalService', expectedCode: 'EXT', expectedBgColor: '#26a69a', expectedShape: 'round-rectangle' },
    { kind: 'CloudService', expectedCode: 'AWS', expectedBgColor: '#0284c7', expectedShape: 'round-rectangle' },
    { kind: 'PeerMesh', expectedCode: 'MESH', expectedBgColor: '#0f766e', expectedShape: 'round-rectangle' },
    { kind: 'Library', expectedCode: 'LIB', expectedBgColor: '#4b5563', expectedShape: 'ellipse' },
    { kind: 'TestSuite', expectedCode: 'TST', expectedBgColor: '#059669', expectedShape: 'ellipse' },
    { kind: 'Group', expectedCode: 'GRP', expectedBgColor: '#4f46e5', expectedShape: 'ellipse' },
    { kind: 'group', expectedCode: 'GRP', expectedBgColor: '#4f46e5', expectedShape: 'ellipse' },
  ];

  for (const c of cases) {
    const meta = getEntityBadgeMeta(c.kind);
    assert.equal(meta.code, c.expectedCode, `kind ${c.kind} should have code ${c.expectedCode}`);
    assert.equal(meta.bgColor, c.expectedBgColor, `kind ${c.kind} should have bgColor ${c.expectedBgColor}`);
    assert.equal(meta.shape, c.expectedShape, `kind ${c.kind} should have shape ${c.expectedShape}`);
    assert.ok(meta.rawIconSvg.length > 0, `kind ${c.kind} should have non-empty rawIconSvg`);
  }
});

test('entityBadges: getNodeBadgeSvgDataUri defaults to icon-only without 3-letter acronym and supports icon-code and metric modes', () => {
  const iconOnlyUri = getNodeBadgeSvgDataUri('Service');
  assert.ok(iconOnlyUri.startsWith('data:image/svg+xml;utf8,'), 'Should start with SVG data URI prefix');
  const decodedIcon = decodeURIComponent(iconOnlyUri.replace('data:image/svg+xml;utf8,', ''));
  assert.ok(decodedIcon.includes('<svg'), 'Should contain svg tag');
  assert.ok(!decodedIcon.includes('SVC'), 'Default icon mode should NOT contain mandatory 3-letter code');
  assert.ok(decodedIcon.includes('viewBox="0 0 64 64"'), 'Should have standard 64x64 viewBox');

  const iconCodeUri = getNodeBadgeSvgDataUri('Service', { contentMode: 'icon-code' });
  const decodedCode = decodeURIComponent(iconCodeUri.replace('data:image/svg+xml;utf8,', ''));
  assert.ok(decodedCode.includes('SVC'), 'icon-code mode should contain 3-letter code SVC');

  const metricUri = getNodeBadgeSvgDataUri('Service', { contentMode: 'metric', metricText: '42f' });
  const decodedMetric = decodeURIComponent(metricUri.replace('data:image/svg+xml;utf8,', ''));
  assert.ok(decodedMetric.includes('42f'), 'metric mode should contain metric text');
});

test('entityBadges: getNodeBadgeSvgDataUri with isGroup returns dashed cluster ring and count indicator', () => {
  const uriIcon = getNodeBadgeSvgDataUri('Topic', { isGroup: true, count: 14 });
  const decodedIcon = decodeURIComponent(uriIcon.replace('data:image/svg+xml;utf8,', ''));
  assert.ok(decodedIcon.includes('stroke-dasharray="4 2"'), 'Should contain dashed cluster ring');
  assert.ok(decodedIcon.includes('×14'), 'Should contain count indicator ×14');

  const uriCode = getNodeBadgeSvgDataUri('Topic', { isGroup: true, count: 14, contentMode: 'icon-code' });
  const decodedCode = decodeURIComponent(uriCode.replace('data:image/svg+xml;utf8,', ''));
  assert.ok(decodedCode.includes('MSG ×14'), 'icon-code group should contain MSG ×14');
});
