import test from 'node:test';
import assert from 'node:assert/strict';
import {
  resolveTargetAsset,
  matchesEnginePattern,
  compareSemver,
  getEngineConfigFromExtensionVersion,
} from './binaryManager';

test('resolveTargetAsset: correctly resolves platform archives and binary names', () => {
  // Windows
  const win64 = resolveTargetAsset('win32', 'x64');
  assert.equal(win64.archiveName, 'ce-win-x64.zip');
  assert.equal(win64.binName, 'ce.exe');
  assert.equal(win64.format, 'zip');

  const winArm = resolveTargetAsset('win32', 'arm64');
  assert.equal(winArm.archiveName, 'ce-win-arm64.zip');
  assert.equal(winArm.binName, 'ce.exe');
  assert.equal(winArm.format, 'zip');

  // Linux
  const linux64 = resolveTargetAsset('linux', 'x64');
  assert.equal(linux64.archiveName, 'ce-linux-x64.tar.gz');
  assert.equal(linux64.binName, 'ce');
  assert.equal(linux64.format, 'tar.gz');

  const linuxArm = resolveTargetAsset('linux', 'arm64');
  assert.equal(linuxArm.archiveName, 'ce-linux-arm64.tar.gz');
  assert.equal(linuxArm.binName, 'ce');
  assert.equal(linuxArm.format, 'tar.gz');

  // macOS
  const osxArm = resolveTargetAsset('darwin', 'arm64');
  assert.equal(osxArm.archiveName, 'ce-osx-arm64.tar.gz');
  assert.equal(osxArm.binName, 'ce');
  assert.equal(osxArm.format, 'tar.gz');

  const osx64 = resolveTargetAsset('darwin', 'x64');
  assert.equal(osx64.archiveName, 'ce-osx-x64.tar.gz');
  assert.equal(osx64.binName, 'ce');
  assert.equal(osx64.format, 'tar.gz');
});

test('getEngineConfigFromExtensionVersion: derives major.minor.* pattern dynamically from extension version', () => {
  const cfg1 = getEngineConfigFromExtensionVersion('1.11.3');
  assert.equal(cfg1.pattern, '1.11.*');
  assert.equal(cfg1.fallback, '1.11.3');

  const cfg2 = getEngineConfigFromExtensionVersion('v1.15.0');
  assert.equal(cfg2.pattern, '1.15.*');
  assert.equal(cfg2.fallback, '1.15.0');

  const cfg3 = getEngineConfigFromExtensionVersion('2.4.9');
  assert.equal(cfg3.pattern, '2.4.*');
  assert.equal(cfg3.fallback, '2.4.9');

  // Custom user override in settings
  const custom = getEngineConfigFromExtensionVersion('1.11.3', '1.15.*');
  assert.equal(custom.pattern, '1.15.*');
  assert.equal(custom.fallback, '1.11.3');

  const customExact = getEngineConfigFromExtensionVersion('1.11.3', '1.15.13');
  assert.equal(customExact.pattern, '1.15.13');
});

test('matchesEnginePattern: matches wildcard major.minor.* correctly', () => {
  assert.ok(matchesEnginePattern('1.11.3', '1.11.*'));
  assert.ok(matchesEnginePattern('v1.11.8', '1.11.*'));
  assert.ok(matchesEnginePattern('1.11.0', '1.11.*'));

  assert.ok(!matchesEnginePattern('1.10.9', '1.11.*'));
  assert.ok(!matchesEnginePattern('1.12.0', '1.11.*'));
  assert.ok(!matchesEnginePattern('2.0.0', '1.11.*'));
});

test('matchesEnginePattern: matches caret and exact patterns', () => {
  assert.ok(matchesEnginePattern('1.15.13', '^1.15.0'));
  assert.ok(matchesEnginePattern('1.15.2', '^1.15.0'));
  assert.ok(!matchesEnginePattern('1.14.99', '^1.15.0'));

  assert.ok(matchesEnginePattern('1.15.13', '1.15.13'));
  assert.ok(!matchesEnginePattern('1.15.12', '1.15.13'));

  assert.ok(matchesEnginePattern('1.15.13', 'latest'));
  assert.ok(matchesEnginePattern('2.0.0', '*'));
});

test('compareSemver: compares versions accurately', () => {
  assert.ok(compareSemver('1.15.13', '1.15.2') > 0);
  assert.ok(compareSemver('1.15.2', '1.15.13') < 0);
  assert.equal(compareSemver('1.15.13', '1.15.13'), 0);
  assert.ok(compareSemver('1.16.0', '1.15.99') > 0);
  assert.ok(compareSemver('2.0.0', '1.99.99') > 0);
});
