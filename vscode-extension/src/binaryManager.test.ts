import test from 'node:test';
import assert from 'node:assert/strict';
import {
  resolveTargetAsset,
  matchesEnginePattern,
  compareSemver,
  getEngineConfigFromExtensionVersion,
  parseSemver,
  cleanSemver,
  isBuildDifferenceOnly,
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

test('getEngineConfigFromExtensionVersion: derives ^major.minor.0 pattern dynamically from extension version', () => {
  const cfg1 = getEngineConfigFromExtensionVersion('1.11.3');
  assert.equal(cfg1.pattern, '^1.11.0');
  assert.equal(cfg1.fallback, '1.11.3');

  const cfg2 = getEngineConfigFromExtensionVersion('v1.15.0');
  assert.equal(cfg2.pattern, '^1.15.0');
  assert.equal(cfg2.fallback, '1.15.0');

  const cfg3 = getEngineConfigFromExtensionVersion('2.4.9');
  assert.equal(cfg3.pattern, '^2.4.0');
  assert.equal(cfg3.fallback, '2.4.9');

  // Custom user override in settings
  const custom = getEngineConfigFromExtensionVersion('1.11.3', '1.15.*');
  assert.equal(custom.pattern, '1.15.*');
  assert.equal(custom.fallback, '1.11.3');

  const customExact = getEngineConfigFromExtensionVersion('1.11.3', '1.15.13');
  assert.equal(customExact.pattern, '1.15.13');
});

test('parseSemver and cleanSemver: parses complex version strings', () => {
  const p1 = parseSemver('ce 1.22.4+403774d8ab48e368e15ea6d5dc06106eb4a0e0c9');
  assert.ok(p1);
  assert.equal(p1.major, 1);
  assert.equal(p1.minor, 22);
  assert.equal(p1.patch, 4);
  assert.equal(p1.buildMetadata, '403774d8ab48e368e15ea6d5dc06106eb4a0e0c9');
  assert.equal(cleanSemver('ce 1.22.4+403774d8ab48e368e15ea6d5dc06106eb4a0e0c9'), '1.22.4');

  const p2 = parseSemver('1.22.4.0');
  assert.ok(p2);
  assert.equal(p2.major, 1);
  assert.equal(p2.minor, 22);
  assert.equal(p2.patch, 4);
  assert.equal(p2.buildNumber, 0);
  assert.equal(cleanSemver('1.22.4.0'), '1.22.4');

  const p3 = parseSemver('v1.22.4-preview.1+build.55');
  assert.ok(p3);
  assert.equal(p3.major, 1);
  assert.equal(p3.minor, 22);
  assert.equal(p3.patch, 4);
  assert.equal(p3.prerelease, 'preview.1');
  assert.equal(p3.buildMetadata, 'build.55');
  assert.equal(cleanSemver('v1.22.4-preview.1+build.55'), '1.22.4-preview.1');
});

test('isBuildDifferenceOnly: detects build differences correctly', () => {
  assert.ok(isBuildDifferenceOnly('1.22.4+abc', '1.22.4+xyz'));
  assert.ok(isBuildDifferenceOnly('1.22.4+403774d...', '1.22.4'));
  assert.ok(isBuildDifferenceOnly('1.22.4.1', '1.22.4.0'));
  assert.ok(isBuildDifferenceOnly('1.22.4-dev', '1.22.4'));
  assert.ok(!isBuildDifferenceOnly('1.22.4', '1.22.5'));
  assert.ok(!isBuildDifferenceOnly('1.22.4', '1.23.0'));
});

test('matchesEnginePattern: matches wildcard major.minor.* correctly', () => {
  assert.ok(matchesEnginePattern('1.11.3', '1.11.*'));
  assert.ok(matchesEnginePattern('v1.11.8', '1.11.*'));
  assert.ok(matchesEnginePattern('1.11.0', '1.11.*'));
  assert.ok(matchesEnginePattern('1.11.4+build.99', '1.11.*'));

  assert.ok(!matchesEnginePattern('1.10.9', '1.11.*'));
  assert.ok(!matchesEnginePattern('1.12.0', '1.11.*'));
  assert.ok(!matchesEnginePattern('2.0.0', '1.11.*'));
});

test('matchesEnginePattern: matches caret and exact patterns with build tolerance', () => {
  // Caret matches
  assert.ok(matchesEnginePattern('1.15.13', '^1.15.0'));
  assert.ok(matchesEnginePattern('1.15.2', '^1.15.0'));
  assert.ok(matchesEnginePattern('1.16.0', '^1.15.0')); // backwards-compatible minor bump
  assert.ok(!matchesEnginePattern('1.14.99', '^1.15.0'));
  assert.ok(!matchesEnginePattern('2.0.0', '^1.15.0')); // breaking major bump

  // Build metadata tolerance (+hash, +build)
  assert.ok(matchesEnginePattern('1.22.4+403774d8ab48e368e15ea6d5dc06106eb4a0e0c9', '1.22.4'));
  assert.ok(matchesEnginePattern('ce 1.22.4+403774d8ab48e368e15ea6d5dc06106eb4a0e0c9', '^1.22.0'));
  assert.ok(matchesEnginePattern('1.22.4+403774d8ab48e368e15ea6d5dc06106eb4a0e0c9', '1.22.*'));

  // 4th digit (.NET assembly build number) tolerance
  assert.ok(matchesEnginePattern('1.22.4.0', '1.22.4'));
  assert.ok(matchesEnginePattern('1.22.4.12', '1.22.4'));
  assert.ok(matchesEnginePattern('1.22.4.0', '^1.22.0'));

  // Pre-release tolerance within same release
  assert.ok(matchesEnginePattern('1.22.4-dev', '1.22.4'));
  assert.ok(matchesEnginePattern('1.22.4-preview.1', '^1.22.0'));

  // Tilde matches
  assert.ok(matchesEnginePattern('1.22.4', '~1.22.0'));
  assert.ok(!matchesEnginePattern('1.23.0', '~1.22.0'));

  // Exact matches
  assert.ok(matchesEnginePattern('1.15.13', '1.15.13'));
  assert.ok(!matchesEnginePattern('1.15.12', '1.15.13'));

  // Fallbacks
  assert.ok(matchesEnginePattern('1.15.13', 'latest'));
  assert.ok(matchesEnginePattern('2.0.0', '*'));
});

test('compareSemver: compares versions accurately and ignores build metadata', () => {
  assert.ok(compareSemver('1.15.13', '1.15.2') > 0);
  assert.ok(compareSemver('1.15.2', '1.15.13') < 0);
  assert.equal(compareSemver('1.15.13', '1.15.13'), 0);
  assert.ok(compareSemver('1.16.0', '1.15.99') > 0);
  assert.ok(compareSemver('2.0.0', '1.99.99') > 0);

  // Build metadata (+...) MUST be ignored per SemVer 2.0 Section 10
  assert.equal(compareSemver('1.22.4+foo', '1.22.4+bar'), 0);
  assert.equal(compareSemver('1.22.4+403774d8ab48e368e15ea6d5dc06106eb4a0e0c9', '1.22.4'), 0);
  assert.equal(compareSemver('1.22.4.0', '1.22.4'), 0);
  assert.ok(compareSemver('1.22.4.1', '1.22.4.0') > 0);

  // Normal version > prerelease version per SemVer 2.0 Section 9
  assert.ok(compareSemver('1.22.4', '1.22.4-beta.1') > 0);
  assert.ok(compareSemver('1.22.4-beta.2', '1.22.4-beta.1') > 0);
});
