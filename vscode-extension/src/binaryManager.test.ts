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
  selectEngineExecutable,
  probeCommandVersion,
  getBestInstalledEngine,
  probeAllInstalledEngines,
  InstalledEngineCandidate,
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

test('selectEngineExecutable: prioritizes local dotnet tool when version is higher than downloaded engine', () => {
  const result = selectEngineExecutable({
    dotnetTool: { command: '/home/user/.dotnet/tools/ce', args: [], version: '1.23.0' },
    downloadedEngine: { command: '/home/user/.storage/bin/ce', args: [], version: '1.22.4' },
  });

  assert.equal(result.source, 'dotnet-tool');
  assert.equal(result.command, '/home/user/.dotnet/tools/ce');
  assert.equal(result.version, '1.23.0');
  assert.ok(result.reason.includes('higher than downloaded engine'));
});

test('selectEngineExecutable: uses downloaded engine when version is higher than local dotnet tool', () => {
  const result = selectEngineExecutable({
    dotnetTool: { command: '/home/user/.dotnet/tools/ce', args: [], version: '1.20.0' },
    downloadedEngine: { command: '/home/user/.storage/bin/ce', args: [], version: '1.23.0' },
  });

  assert.equal(result.source, 'downloaded');
  assert.equal(result.command, '/home/user/.storage/bin/ce');
  assert.equal(result.version, '1.23.0');
  assert.ok(result.reason.includes('Downloaded engine'));
});

test('selectEngineExecutable: uses downloaded engine when version is equal to local dotnet tool', () => {
  const result = selectEngineExecutable({
    dotnetTool: { command: '/home/user/.dotnet/tools/ce', args: [], version: '1.23.0' },
    downloadedEngine: { command: '/home/user/.storage/bin/ce', args: [], version: '1.23.0' },
  });

  assert.equal(result.source, 'downloaded');
  assert.equal(result.command, '/home/user/.storage/bin/ce');
  assert.equal(result.version, '1.23.0');
});

test('selectEngineExecutable: uses local dotnet tool when downloaded engine is not present', () => {
  const result = selectEngineExecutable({
    dotnetTool: { command: '/home/user/.dotnet/tools/ce', args: [], version: '1.23.0' },
    downloadedEngine: null,
  });

  assert.equal(result.source, 'dotnet-tool');
  assert.equal(result.command, '/home/user/.dotnet/tools/ce');
  assert.equal(result.version, '1.23.0');
});

test('selectEngineExecutable: uses downloaded engine when dotnet tool is not present', () => {
  const result = selectEngineExecutable({
    dotnetTool: null,
    downloadedEngine: { command: '/home/user/.storage/bin/ce', args: [], version: '1.23.0' },
  });

  assert.equal(result.source, 'downloaded');
  assert.equal(result.command, '/home/user/.storage/bin/ce');
  assert.equal(result.version, '1.23.0');
});

test('selectEngineExecutable: returns need-download when neither dotnet tool nor downloaded engine exists', () => {
  const result = selectEngineExecutable({
    dotnetTool: null,
    downloadedEngine: null,
  });

  assert.equal(result.source, 'need-download');
  assert.equal(result.command, '');
});

test('selectEngineExecutable: explicitly configured custom executable overrides both', () => {
  const result = selectEngineExecutable({
    customExecutable: { command: '/dev/cli/ce', args: [] },
    dotnetTool: { command: '/home/user/.dotnet/tools/ce', args: [], version: '1.23.0' },
    downloadedEngine: { command: '/home/user/.storage/bin/ce', args: [], version: '1.22.4' },
  });

  assert.equal(result.source, 'custom');
  assert.equal(result.command, '/dev/cli/ce');
});

test('checkAndUpdate version comparison: detects equal or higher version to avoid redundant download', () => {
  // Same version
  const current1 = '1.23.0';
  const target1 = '1.23.0';
  const cmp1 = compareSemver(current1, target1);
  const skip1 = cmp1 === 0 || isBuildDifferenceOnly(current1, target1);
  assert.ok(skip1, 'Should skip download when versions are equal');

  // Same version with build metadata/commit hash
  const current2 = '1.23.0+6cbaaa66d2947ece7c0bf709a3f2283e7c900a1c';
  const target2 = '1.23.0';
  const cmp2 = compareSemver(current2, target2);
  const skip2 = cmp2 === 0 || isBuildDifferenceOnly(current2, target2);
  assert.ok(skip2, 'Should skip download when current has build metadata');

  // Installed is higher than remote release
  const current3 = '1.24.0';
  const target3 = '1.23.0';
  const cmp3 = compareSemver(current3, target3);
  assert.ok(cmp3 > 0, 'Installed is higher than remote');

  // Remote release is higher: needs update
  const current4 = '1.22.4';
  const target4 = '1.23.0';
  const cmp4 = compareSemver(current4, target4);
  assert.ok(cmp4 < 0, 'Remote is higher, needs download');
});

test('getBestInstalledEngine: selects candidate with highest SemVer across all probed sources', () => {
  const candidates: InstalledEngineCandidate[] = [
    { command: '/storage/bin/ce', args: [], version: '1.21.1', source: 'downloaded' },
    { command: '/usr/bin/ce', args: [], version: '1.20.0', source: 'path' },
    { command: '/home/user/.dotnet/tools/ce', args: [], version: '1.23.0', source: 'dotnet-tool' },
    { command: '/ext/bin/ce', args: [], version: '1.18.0', source: 'bundled' },
  ];

  const best = getBestInstalledEngine(candidates);
  assert.ok(best);
  assert.equal(best.version, '1.23.0');
  assert.equal(best.source, 'dotnet-tool');
  assert.equal(best.command, '/home/user/.dotnet/tools/ce');
});

test('getBestInstalledEngine: breaks ties using source priority when versions are identical', () => {
  const candidates: InstalledEngineCandidate[] = [
    { command: '/home/user/.dotnet/tools/ce', args: [], version: '1.23.0', source: 'dotnet-tool' },
    { command: '/storage/bin/ce', args: [], version: '1.23.0', source: 'downloaded' },
    { command: '/usr/bin/ce', args: [], version: '1.23.0', source: 'path' },
  ];

  const best = getBestInstalledEngine(candidates);
  assert.ok(best);
  assert.equal(best.source, 'downloaded', 'Downloaded is preferred when versions are strictly equal');
  assert.equal(best.command, '/storage/bin/ce');
});

test('getBestInstalledEngine: custom executable explicitly configured always takes priority', () => {
  const candidates: InstalledEngineCandidate[] = [
    { command: '/my/custom/ce', args: [], version: '1.19.0', source: 'custom' },
    { command: '/home/user/.dotnet/tools/ce', args: [], version: '1.23.0', source: 'dotnet-tool' },
    { command: '/storage/bin/ce', args: [], version: '1.24.0', source: 'downloaded' },
  ];

  const best = getBestInstalledEngine(candidates);
  assert.ok(best);
  assert.equal(best.source, 'custom');
  assert.equal(best.command, '/my/custom/ce');
});

test('getBestInstalledEngine: returns null when candidates list is empty', () => {
  const best = getBestInstalledEngine([]);
  assert.equal(best, null);
});

test('Engine probing before download: avoids downloading if installed version is newer than remote release', () => {
  // Scenario: Local dotnet-tool is 1.23.0, but GitHub release is 1.21.1
  const installedVersion = '1.23.0';
  const remoteReleaseVersion = '1.21.1';

  const shouldDownload = compareSemver(installedVersion, remoteReleaseVersion) < 0;
  assert.equal(shouldDownload, false, 'Should NOT download when installed is newer than remote release');
});

test('getBestInstalledEngine: selects 1.23.0 dotnet-tool over 1.21.1 downloaded', () => {
  const candidates: InstalledEngineCandidate[] = [
    { command: 'C:\\Users\\user\\AppData\\Roaming\\Code\\User\\globalStorage\\vmikhailov.code-explorer-vscode\\bin\\ce.exe', args: [], version: '1.21.1', source: 'downloaded' },
    { command: 'C:\\Users\\user\\.dotnet\\tools\\ce.exe', args: [], version: '1.23.0', source: 'dotnet-tool' },
  ];

  const best = getBestInstalledEngine(candidates);
  assert.ok(best);
  assert.equal(best.version, '1.23.0');
  assert.equal(best.source, 'dotnet-tool');
  assert.equal(best.command, 'C:\\Users\\user\\.dotnet\\tools\\ce.exe');
});



