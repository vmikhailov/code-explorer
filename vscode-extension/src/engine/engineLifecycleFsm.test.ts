import test from 'node:test';
import assert from 'node:assert/strict';
import {
  EngineLifecycleFsm,
  EngineLifecycleState,
  isFatalEngineExit,
} from './engineLifecycleFsm';

test('EngineLifecycleFsm: happy path Idle -> Discovering -> Starting -> Ready -> Stopping -> Idle', () => {
  const fsm = new EngineLifecycleFsm();
  const transitions: Array<{ from: EngineLifecycleState; to: EngineLifecycleState }> = [];
  fsm.onStateChange((snap, prev) => {
    transitions.push({ from: prev, to: snap.state });
  });

  assert.equal(fsm.getState(), 'Idle');
  assert.equal(fsm.requestStart(), true);
  assert.equal(fsm.getState(), 'Discovering');

  // Duplicate requestStart while Discovering is a no-op
  assert.equal(fsm.requestStart(), false);

  assert.equal(fsm.onBinaryDiscovered('/usr/local/bin/ce'), true);
  assert.equal(fsm.getState(), 'Starting');
  assert.equal(fsm.getSnapshot().binaryCommand, '/usr/local/bin/ce');

  assert.equal(fsm.onServerReady(52145), true);
  assert.equal(fsm.getState(), 'Ready');
  assert.equal(fsm.getSnapshot().port, 52145);
  assert.equal(fsm.getSnapshot().retryCount, 0);

  assert.equal(fsm.beginStop(), true);
  assert.equal(fsm.getState(), 'Stopping');
  assert.equal(fsm.onStopped(), true);
  assert.equal(fsm.getState(), 'Idle');

  assert.deepEqual(
    transitions.map((t) => `${t.from}->${t.to}`),
    [
      'Idle->Discovering',
      'Discovering->Starting',
      'Starting->Ready',
      'Ready->Stopping',
      'Stopping->Idle',
    ]
  );
});

test('EngineLifecycleFsm: binary not found in Discovering transitions directly to Failed with 0 retries', () => {
  const fsm = new EngineLifecycleFsm();
  fsm.requestStart();
  assert.equal(fsm.getState(), 'Discovering');

  assert.equal(fsm.onBinaryNotFound('ce binary missing'), true);
  const snap = fsm.getSnapshot();
  assert.equal(snap.state, 'Failed');
  assert.equal(snap.retryCount, 0);
  assert.equal(snap.failure?.kind, 'binary-not-found');
  assert.equal(snap.failure?.reason, 'ce binary missing');
});

test('EngineLifecycleFsm: fatal exit code (missing .NET runtime / invalid flag) transitions directly to Failed with 0 retries', () => {
  const fsm = new EngineLifecycleFsm();
  fsm.requestStart();
  fsm.onBinaryDiscovered('ce.exe');
  assert.equal(fsm.getState(), 'Starting');

  const nextState = fsm.onProcessFailure({
    reason: 'Missing .NET 11 runtime',
    exitCode: 2147516566,
    stderr: 'FrameworkMissingFailure: Microsoft.NETCore.App 11.0 not found (0x80008096)',
  });

  assert.equal(nextState, 'Failed');
  assert.equal(fsm.getSnapshot().retryCount, 0);
  assert.equal(fsm.getSnapshot().failure?.kind, 'fatal-exit');

  // Verify invalid CLI flag is also classified as fatal
  assert.equal(
    isFatalEngineExit(1, 'Unrecognized command or argument --bad-flag'),
    true
  );
});

test('EngineLifecycleFsm: transient startup failure (e.g. port busy) retries at most once -> Failed', () => {
  const fsm = new EngineLifecycleFsm();
  fsm.requestStart();
  fsm.onBinaryDiscovered('ce');
  assert.equal(fsm.getState(), 'Starting');

  // 1st transient error -> RetryingOnce
  const afterFirst = fsm.onProcessFailure({
    reason: 'Port 52100 already in use (EADDRINUSE)',
    exitCode: 1,
    stderr: 'Failed to bind to address http://127.0.0.1:52100: address already in use.',
  });
  assert.equal(afterFirst, 'RetryingOnce');
  assert.equal(fsm.getSnapshot().retryCount, 1);

  // 2nd transient error in RetryingOnce -> Failed (Circuit Open)
  const afterSecond = fsm.onProcessFailure({
    reason: 'Port still occupied on retry',
    exitCode: 1,
    stderr: 'EADDRINUSE',
  });
  assert.equal(afterSecond, 'Failed');
  assert.equal(fsm.getSnapshot().retryCount, 1);
  assert.equal(fsm.getSnapshot().failure?.kind, 'transient-exhausted');
});

test('EngineLifecycleFsm: Starting -> RetryingOnce -> Ready succeeds when retry recovers', () => {
  const fsm = new EngineLifecycleFsm();
  fsm.requestStart();
  fsm.onBinaryDiscovered('ce');

  fsm.onProcessFailure({
    reason: 'Transient startup timeout',
    isTimeout: true,
  });
  assert.equal(fsm.getState(), 'RetryingOnce');
  assert.equal(fsm.getSnapshot().retryCount, 1);

  assert.equal(fsm.onServerReady(53000), true);
  assert.equal(fsm.getState(), 'Ready');
  assert.equal(fsm.getSnapshot().port, 53000);
  assert.equal(fsm.getSnapshot().failure, null);
});

test('EngineLifecycleFsm: Ready -> RetryingOnce -> Ready | Failed on unexpected crash during runtime', () => {
  const fsm = new EngineLifecycleFsm();
  fsm.requestStart();
  fsm.onBinaryDiscovered('ce');
  fsm.onServerReady(54000);
  assert.equal(fsm.getState(), 'Ready');

  // Unexpected transient crash while Ready -> RetryingOnce
  const stateAfterCrash = fsm.onProcessFailure({
    reason: 'Process exited unexpectedly with code 1',
    exitCode: 1,
  });
  assert.equal(stateAfterCrash, 'RetryingOnce');
  assert.equal(fsm.getSnapshot().retryCount, 1);

  // Recovery succeeds -> Ready
  assert.equal(fsm.onServerReady(54000), true);
  assert.equal(fsm.getState(), 'Ready');

  // Second crash after retry budget was already spent -> Failed immediately (no infinite crash loop)
  const stateAfterSecondCrash = fsm.onProcessFailure({
    reason: 'Process crashed again',
    exitCode: 1,
  });
  assert.equal(stateAfterSecondCrash, 'Failed');
});

test('EngineLifecycleFsm: Failed state ignores automatic start/events and ONLY leaves via explicit retry() or setting change', () => {
  const fsm = new EngineLifecycleFsm();
  fsm.requestStart();
  fsm.onBinaryNotFound('ce not found');
  assert.equal(fsm.getState(), 'Failed');

  // Automatic calls must NOT leave Failed
  assert.equal(fsm.requestStart(), false);
  assert.equal(fsm.onBinaryDiscovered('ce'), false);
  assert.equal(fsm.onServerReady(52100), false);
  assert.equal(fsm.getState(), 'Failed');

  // Explicit user retry() transitions Failed -> Discovering and resets retryCount
  assert.equal(fsm.retry(), true);
  assert.equal(fsm.getState(), 'Discovering');
  assert.equal(fsm.getSnapshot().retryCount, 0);
  assert.equal(fsm.getSnapshot().failure, null);

  // Fail again and recover via settings change
  fsm.onBinaryNotFound('still missing');
  assert.equal(fsm.getState(), 'Failed');
  assert.equal(fsm.onExecutableSettingChanged(), true);
  assert.equal(fsm.getState(), 'Discovering');
});
