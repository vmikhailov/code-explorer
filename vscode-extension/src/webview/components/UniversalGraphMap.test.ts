import test from 'node:test';
import assert from 'node:assert/strict';
import { UniversalGraphMap, UniversalGraphMapProps } from './UniversalGraphMap';

test('UniversalGraphMap: exports component and valid props interface', () => {
  assert.equal(typeof UniversalGraphMap, 'function');

  const props: UniversalGraphMapProps = {
    graph: { nodes: [], edges: [] },
    hudLabel: 'C3 Internal Structure',
    hudTitle: 'Project Map: Core',
    storagePrefix: 'ce_pm_v3_',
  };

  assert.equal(props.hudLabel, 'C3 Internal Structure');
  assert.equal(props.hudTitle, 'Project Map: Core');
  assert.equal(props.storagePrefix, 'ce_pm_v3_');
});
