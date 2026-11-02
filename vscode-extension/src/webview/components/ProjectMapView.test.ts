import test from 'node:test';
import assert from 'node:assert/strict';
import { ProjectMapView, ProjectMapViewProps } from './ProjectMapView';

test('ProjectMapView: exports component and valid props interface', () => {
  assert.equal(typeof ProjectMapView, 'function');

  const props: ProjectMapViewProps = {
    serverHttpUrl: 'http://localhost:5000',
    project: 'CodeExplorer.Core',
    onOpenFile: () => {},
  };

  assert.equal(props.serverHttpUrl, 'http://localhost:5000');
  assert.equal(props.project, 'CodeExplorer.Core');
  assert.equal(typeof props.onOpenFile, 'function');
});
