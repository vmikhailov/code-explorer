import {
  EngineSelectionInput,
  EngineSelectionResult,
  InstalledEngineCandidate,
  ProbeInstalledOptions,
} from './types';
import { compareSemver } from './semver';
import { probeAllInstalledEngines } from './discovery';

/**
 * Returns the best installed engine candidate across all probed sources.
 * Sorts candidates by SemVer descending.
 * Tie-breaker when versions are equal: downloaded > dotnet-tool > path > bundled.
 * Explicit custom configuration always takes priority if present.
 */
export function getBestInstalledEngine(
  input?: InstalledEngineCandidate[] | ProbeInstalledOptions
): InstalledEngineCandidate | null {
  const candidates = Array.isArray(input)
    ? input
    : probeAllInstalledEngines(input);

  if (!candidates || candidates.length === 0) return null;

  const custom = candidates.find((c) => c.source === 'custom');
  if (custom) return custom;

  const sourcePriority: Record<InstalledEngineCandidate['source'], number> = {
    custom: 5,
    downloaded: 4,
    'dotnet-tool': 3,
    path: 2,
    bundled: 1,
  };

  const sorted = [...candidates].sort((a, b) => {
    const cmp = compareSemver(b.version, a.version);
    if (cmp !== 0) return cmp;
    return (sourcePriority[b.source] ?? 0) - (sourcePriority[a.source] ?? 0);
  });

  return sorted[0] || null;
}

/**
 * Decides whether to use the custom executable, local dotnet tool, or downloaded engine.
 * Rule: If there is a local dotnet tool whose version is HIGHER than the downloaded one,
 * it is used. If dotnet tool is not present or its version is lower, the downloaded engine is used.
 */
export function selectEngineExecutable(input: EngineSelectionInput): EngineSelectionResult {
  if (input.customExecutable) {
    return {
      command: input.customExecutable.command,
      args: input.customExecutable.args,
      source: 'custom',
      reason: 'Using explicitly configured executable path or environment override.',
    };
  }

  const { dotnetTool, downloadedEngine } = input;

  if (dotnetTool && downloadedEngine) {
    const cmp = compareSemver(dotnetTool.version, downloadedEngine.version);
    if (cmp > 0) {
      return {
        command: dotnetTool.command,
        args: dotnetTool.args,
        version: dotnetTool.version,
        source: 'dotnet-tool',
        reason: `Local dotnet tool (v${dotnetTool.version}) is higher than downloaded engine (v${downloadedEngine.version}). Using dotnet tool.`,
      };
    } else {
      return {
        command: downloadedEngine.command,
        args: downloadedEngine.args,
        version: downloadedEngine.version,
        source: 'downloaded',
        reason: `Downloaded engine (v${downloadedEngine.version}) is >= local dotnet tool (v${dotnetTool.version}). Using downloaded engine.`,
      };
    }
  }

  if (dotnetTool && !downloadedEngine) {
    return {
      command: dotnetTool.command,
      args: dotnetTool.args,
      version: dotnetTool.version,
      source: 'dotnet-tool',
      reason: `Using local dotnet tool (v${dotnetTool.version}) (no downloaded engine present).`,
    };
  }

  if (!dotnetTool && downloadedEngine) {
    return {
      command: downloadedEngine.command,
      args: downloadedEngine.args,
      version: downloadedEngine.version,
      source: 'downloaded',
      reason: `Using downloaded engine (v${downloadedEngine.version}) (dotnet tool not found).`,
    };
  }

  return {
    command: '',
    args: [],
    source: 'need-download',
    reason: 'Neither a local dotnet tool nor a downloaded engine is currently present.',
  };
}
