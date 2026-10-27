import { test } from 'node:test';
import assert from 'node:assert/strict';
import { matchesSearchTokens, matchesSearchPattern } from './searchUtils';

test('matchesSearchTokens: space-separated tokens match independently', () => {
  // User's specific case
  assert.equal(
    matchesSearchTokens('bq-routes-calculation', 'bq calc'),
    true,
    '"bq calc" should match "bq-routes-calculation"'
  );

  // Token order independence
  assert.equal(
    matchesSearchTokens('bq-routes-calculation', 'calc bq'),
    true,
    '"calc bq" should match "bq-routes-calculation"'
  );

  // Extra spaces between tokens
  assert.equal(
    matchesSearchTokens('bq-routes-calculation', '  bq    calc  '),
    true,
    'multiple spaces should be handled as token separators'
  );

  // Case insensitivity
  assert.equal(
    matchesSearchTokens('Bq-Routes-Calculation', 'BQ CALC'),
    true,
    'should be case insensitive'
  );
});

test('matchesSearchTokens: rejects if any token is missing', () => {
  assert.equal(
    matchesSearchTokens('bq-routes-calculation', 'bq calc auth'),
    false,
    'should fail if token "auth" is not present'
  );

  assert.equal(
    matchesSearchTokens('user-service', 'bq calc'),
    false,
    'should fail if tokens are not present'
  );
});

test('matchesSearchTokens: works with array of target strings', () => {
  const fields = [
    'order-service',
    'Orders & Billing',
    'Service',
    'api/v1/orders',
    'payment-db',
  ];

  assert.equal(
    matchesSearchTokens(fields, 'order billing'),
    true,
    'tokens across multiple fields should match'
  );

  assert.equal(
    matchesSearchTokens(fields, 'billing payment'),
    true,
    'tokens across name and database should match'
  );

  assert.equal(
    matchesSearchTokens(fields, 'billing auth'),
    false,
    'should fail if any token is missing in array'
  );
});

test('matchesSearchTokens: empty or whitespace queries always match', () => {
  assert.equal(matchesSearchTokens('anything', ''), true);
  assert.equal(matchesSearchTokens('anything', '   '), true);
  assert.equal(matchesSearchTokens([], ''), true);
});

test('matchesSearchPattern: wildcard patterns match prefixes, suffixes, and wildcards', () => {
  assert.equal(matchesSearchPattern('user-service', 'user-*'), true);
  assert.equal(matchesSearchPattern('user-db', 'user-*'), true);
  assert.equal(matchesSearchPattern('auth-user-service', 'user-*'), false, 'user-* should be anchored to start');

  assert.equal(matchesSearchPattern('OrderService', '*Service'), true);
  assert.equal(matchesSearchPattern('billing-service', '*service'), true);
  assert.equal(matchesSearchPattern('service-client', '*service'), false);

  assert.equal(matchesSearchPattern('CodeExplorer.Tests.Core', '*.Tests.*'), true);
  assert.equal(matchesSearchPattern('CodeExplorer.Core', '*.Tests.*'), false);

  assert.equal(matchesSearchPattern('api1', 'api?'), true);
  assert.equal(matchesSearchPattern('apis', 'api?'), true);
  assert.equal(matchesSearchPattern('api12', 'api?'), false);

  // Array of target texts
  const nodeFields = ['UserService', 'user-service', 'Service', 'service:user'];
  assert.equal(matchesSearchPattern(nodeFields, 'user-*'), true);
  assert.equal(matchesSearchPattern(nodeFields, '*Service'), true);
});

test('matchesSearchPattern: regex literals and implicit patterns', () => {
  // Regex literal
  assert.equal(matchesSearchPattern('user-123', '/^user-\\d+$/i'), true);
  assert.equal(matchesSearchPattern('user-abc', '/^user-\\d+$/i'), false);

  // Implicit regex pattern
  assert.equal(matchesSearchPattern('auth-service', '(auth|billing)'), true);
  assert.equal(matchesSearchPattern('billing-db', '(auth|billing)'), true);
  assert.equal(matchesSearchPattern('user-service', '(auth|billing)'), false);

  // Starts-with / ends-with regex
  assert.equal(matchesSearchPattern('payment-processor', '^payment-'), true);
  assert.equal(matchesSearchPattern('old-payment-processor', '^payment-'), false);
  assert.equal(matchesSearchPattern('user-service', 'service$'), true);
});

test('matchesSearchPattern: incomplete regex syntax falls back safely without error', () => {
  // Incomplete regex should not throw and fall back to token search
  assert.doesNotThrow(() => {
    assert.equal(matchesSearchPattern('user(test)', 'user('), true);
    assert.equal(matchesSearchPattern('user-service', 'user['), false);
  });
});

test('matchesSearchPattern: standard queries fall back to token matching', () => {
  assert.equal(matchesSearchPattern('bq-routes-calculation', 'bq calc'), true);
  assert.equal(matchesSearchPattern(['user-service', 'Service'], 'user service'), true);
  assert.equal(matchesSearchPattern('anything', ''), true);
});
