import test from 'node:test';
import assert from 'node:assert/strict';
import { OntologyExplorerView, OntologyExplorerViewProps } from './OntologyExplorerView';

test('OntologyExplorerView: exports component and valid props interface', () => {
  assert.equal(typeof OntologyExplorerView, 'function');

  const props: OntologyExplorerViewProps = {
    serverHttpUrl: 'http://localhost:5100',
    onOpenNodeGrid: () => {},
    onRunSampleCypher: () => {},
  };

  assert.equal(props.serverHttpUrl, 'http://localhost:5100');
  assert.equal(typeof props.onOpenNodeGrid, 'function');
  assert.equal(typeof props.onRunSampleCypher, 'function');
});
