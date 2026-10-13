import { test } from 'node:test';
import assert from 'node:assert/strict';
import { matchesSearchTokens } from './searchUtils';

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
