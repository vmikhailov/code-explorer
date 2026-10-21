import { test } from 'node:test';
import assert from 'node:assert/strict';
import { getNodePresentation } from './nodePresentation';

test('nodePresentation: peer meshes are non-executable topology resources', () => {
  const presentation = getNodePresentation({
    kind: 'PeerMesh',
    name: '3-Party MPC Peer Mesh',
    properties: { member_count: '3', protocols: 'TCP/mTLS, NCCL' },
  });

  assert.equal(presentation.badgeLabel, 'PEER MESH');
  assert.equal(presentation.badgeClass, 'badge-peer-mesh');
  assert.equal(presentation.isResource, true);
  assert.equal(presentation.isExecutable, false);
  assert.equal(presentation.c1Category, null);
});

test('nodePresentation: AWS cloud resources preserve their specific resource type', () => {
  const presentation = getNodePresentation({
    kind: 'CloudService',
    name: 'AWS S3: Encrypted Shares Bucket',
    properties: { provider: 'AWS', resource_type: 'S3Bucket', service_type: 'CloudService' },
  });

  assert.equal(presentation.badgeLabel, 'AWS S3BUCKET');
  assert.equal(presentation.isResource, true);
  assert.equal(presentation.isExternal, true);
  assert.equal(presentation.c1Category, 'external');
});
