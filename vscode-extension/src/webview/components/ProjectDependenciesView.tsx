import React, { useEffect, useMemo, useState, useCallback } from 'react';
import { GraphData, GraphNode } from '../../../../proto/types';
import { TierDefinition } from '../layout/swimlanesLayout';
import { OrbitConfig } from '../layout/orbitConfig';
import { useServerTransport } from '../services/webviewTransport';
import {
  UniversalGraphMap,
  InspectorPresetDefinition,
  InspectorOverviewModel,
} from './UniversalGraphMap';

export interface ProjectDependenciesViewProps {
  serverHttpUrl?: string;
  scope?: string;
  onScopeChange?: (projectName: string) => void;
  onOpenProjectMap?: (projectName: string) => void;
  onOpenFile: (filePath: string, lineStart?: number) => void;
}

const PROJECT_DEP_TIERS: TierDefinition[] = [
  { tier: 0, title: 'Core & Foundation Kernel', color: '#38bdf8' },
  { tier: 1, title: 'Shared Domain & Infrastructure', color: '#a78bfa' },
  { tier: 2, title: 'Feature & Adapter Libraries', color: '#34d399' },
  { tier: 3, title: 'Executable Services, Apps & CLI', color: '#fbbf24' },
  { tier: 4, title: 'Test & Verification Suites', color: '#059669' },
  { tier: 5, title: 'External Packages', color: '#94a3b8' },
];

const PROJECT_DEP_ORBIT_CONFIG: OrbitConfig = {
  version: 1,
  orbits: [[0], [1], [2], [3], [4], [5]],
  kindTiers: {},
};

function resolveBaseUrl(serverHttpUrl?: string): string | undefined {
  if (serverHttpUrl) return serverHttpUrl;
  const wsUrl = typeof window !== 'undefined' ? (window as any).__CE_CONFIG__?.wsUrl : undefined;
  return wsUrl?.replace(/^ws:\/\//, 'http://').replace(/^wss:\/\//, 'https://').replace(/\/ws$/, '');
}

function assignFallbackProjectTiers(data: GraphData | null): GraphData | null {
  if (!data || !data.nodes) return data;

  const fanIn = new Map<string, number>();
  const fanOut = new Map<string, number>();
  for (const n of data.nodes) {
    fanIn.set(n.id, 0);
    fanOut.set(n.id, 0);
  }
  for (const e of data.edges ?? []) {
    if (e.source === e.target) continue;
    fanOut.set(e.source, (fanOut.get(e.source) ?? 0) + 1);
    fanIn.set(e.target, (fanIn.get(e.target) ?? 0) + 1);
  }

  const enrichedNodes: GraphNode[] = data.nodes.map((n) => {
    const p = { ...(n.properties ?? {}) };
    const inCount = Number(p.fan_in ?? fanIn.get(n.id) ?? 0);
    const outCount = Number(p.fan_out ?? fanOut.get(n.id) ?? 0);
    p.fan_in = String(inCount);
    p.fan_out = String(outCount);

    const rawKind = String(n.kind || 'Project');
    const isPkg = rawKind === 'Package' || p.entity_kind === 'Package';
    const isTest = p.is_test === 'true' || rawKind === 'TestSuite' || p.entity_kind === 'Test';
    const isWorkload =
      rawKind === 'Service' ||
      rawKind === 'App' ||
      rawKind === 'Worker' ||
      rawKind === 'CliTool';

    if (p.tier === undefined || p.tier === '') {
      let tier = 2;
      if (isPkg) tier = 5;
      else if (isTest) tier = 4;
      else if (isWorkload) tier = 3;
      else if (outCount === 0 && inCount > 0) tier = 0;
      else if (inCount >= outCount && inCount >= 2) tier = 1;

      const tierDef = PROJECT_DEP_TIERS.find((t) => t.tier === tier);
      p.tier = String(tier);
      p.tier_label = tierDef?.title ?? `Orbit ${tier}`;
    }

    const mappedKind = isPkg || rawKind === 'Project' ? 'Library' : n.kind;
    return {
      ...n,
      kind: mappedKind,
      properties: p,
    };
  });

  return {
    ...data,
    nodes: enrichedNodes,
  };
}

export const ProjectDependenciesView: React.FC<ProjectDependenciesViewProps> = ({
  serverHttpUrl,
  scope = '',
  onScopeChange,
  onOpenFile,
}) => {
  const { httpUrl: transportHttpUrl, connectionState } = useServerTransport();
  const [graph, setGraph] = useState<GraphData | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  // Filters
  const [hideTests, setHideTests] = useState(false);
  const [includeExternalPackages, setIncludeExternalPackages] = useState(false);
  const [cyclesOnly, setCyclesOnly] = useState(false);

  useEffect(() => {
    const baseUrl = serverHttpUrl || transportHttpUrl || resolveBaseUrl(serverHttpUrl);
    if (!baseUrl) {
      setLoading(true);
      setError(null);
      return;
    }
    let cancelled = false;
    let retryTimer: ReturnType<typeof setTimeout> | null = null;
    setLoading(true);
    setError(null);

    const loadWithRetry = (attempt: number) => {
      const currentBase = serverHttpUrl || transportHttpUrl || resolveBaseUrl(serverHttpUrl) || baseUrl;
      const url = `${currentBase}/api/view?view=project-dependencies&scope=${encodeURIComponent(scope)}&includeLibraries=${includeExternalPackages}`;
      fetch(url)
        .then(async (res) => {
          if (!res.ok) throw new Error(`HTTP ${res.status}: ${await res.text()}`);
          return (await res.json()) as GraphData;
        })
        .then((data) => {
          if (cancelled) return;
          setGraph(assignFallbackProjectTiers(data));
          setError(null);
          setLoading(false);
        })
        .catch((err: unknown) => {
          if (cancelled) return;
          if (attempt < 5) {
            retryTimer = setTimeout(() => loadWithRetry(attempt + 1), 500);
            return;
          }
          setError(err instanceof Error ? err.message : String(err));
          setLoading(false);
        });
    };

    loadWithRetry(0);
    return () => {
      cancelled = true;
      if (retryTimer) clearTimeout(retryTimer);
    };
  }, [serverHttpUrl, transportHttpUrl, connectionState === 'connected', scope, includeExternalPackages, reloadToken]);

  const allProjectOptions = useMemo(() => {
    if (!graph?.nodes) return [];
    return graph.nodes
      .filter((n) => n.properties?.entity_kind !== 'Package')
      .map((n) => ({ id: n.id, name: n.name || n.id, kind: n.kind }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [graph]);

  const cycleNodeIds = useMemo(() => {
    const set = new Set<string>();
    for (const n of graph?.nodes ?? []) {
      if (n.properties?.in_cycle === 'true') {
        set.add(n.id);
      }
    }
    return set;
  }, [graph]);

  const cycleCount = useMemo(() => {
    const raw = Number(graph?.metadata?.cycle_count ?? 0);
    if (Number.isFinite(raw) && raw > 0) return raw;
    const groups = new Set<string>();
    for (const n of graph?.nodes ?? []) {
      if (n.properties?.cycle_group) groups.add(n.properties.cycle_group);
    }
    return groups.size;
  }, [graph]);

  const filteredGraph = useMemo<GraphData | null>(() => {
    if (!graph) return null;
    if (!hideTests && !cyclesOnly) return graph;

    const keptNodes = (graph.nodes ?? []).filter((n) => {
      const p = n.properties ?? {};
      if (hideTests && (p.is_test === 'true' || n.kind === 'TestSuite' || p.tier === '4')) {
        return false;
      }
      if (cyclesOnly && p.in_cycle !== 'true') {
        return false;
      }
      return true;
    });

    const keptIds = new Set(keptNodes.map((n) => n.id));
    const keptEdges = (graph.edges ?? []).filter((e) => {
      if (!keptIds.has(e.source) || !keptIds.has(e.target)) return false;
      if (cyclesOnly && e.properties?.in_cycle !== 'true') return false;
      return true;
    });

    return {
      ...graph,
      nodes: keptNodes,
      edges: keptEdges,
    };
  }, [graph, hideTests, cyclesOnly]);

  const scopeSelector = (
    <select
      className="project-map-project-dropdown"
      style={{
        height: 26,
        boxSizing: 'border-box',
        fontSize: 11,
        maxWidth: 240,
        background: 'var(--vscode-dropdown-background, #1e293b)',
        color: 'var(--vscode-dropdown-foreground, #f8fafc)',
        border: '1px solid var(--vscode-dropdown-border, #334155)',
        borderRadius: 4,
        padding: '0 6px',
        textOverflow: 'ellipsis',
        overflow: 'hidden',
        flexShrink: 0,
      }}
      value={scope}
      onChange={(e) => onScopeChange?.(e.target.value)}
      title="Filter by project neighborhood (1-hop upstream & downstream)"
    >
      <option value="">All Projects ({allProjectOptions.length})</option>
      {allProjectOptions.map((p) => (
        <option key={p.id} value={p.name}>
          {p.name} ({p.kind})
        </option>
      ))}
    </select>
  );

  const inspectorOverviewSlot = useMemo<InspectorOverviewModel>(() => {
    const nodes = filteredGraph?.nodes ?? [];
    const edges = filteredGraph?.edges ?? [];
    const workloads = nodes.filter(
      (n) => n.kind === 'Service' || n.kind === 'App' || n.kind === 'Worker' || n.kind === 'CliTool'
    ).length;
    const libraries = nodes.filter(
      (n) => n.kind === 'Library' && n.properties?.entity_kind !== 'Package'
    ).length;
    const tests = nodes.filter(
      (n) => n.kind === 'TestSuite' || n.properties?.is_test === 'true'
    ).length;

    return {
      title: scope ? `Scope: ${scope}` : 'Project Dependencies',
      subtitle: `${nodes.length} projects · ${edges.length} DEPENDS_ON edges`,
      metrics: [
        { label: 'Workloads', value: workloads, color: '#fbbf24' },
        { label: 'Libraries', value: libraries, color: '#38bdf8' },
        { label: 'Tests', value: tests, color: '#34d399' },
        { label: 'Cycles', value: cycleCount, color: cycleCount > 0 ? '#fb7185' : '#94a3b8', highlight: cycleCount > 0 },
      ],
      actionBanner: cycleCount > 0 ? {
        text: `⚠️ ${cycleCount} project dependency cycle${cycleCount === 1 ? '' : 's'} (${cycleNodeIds.size} projects)`,
        actionLabel: cyclesOnly ? 'ISOLATING' : 'ISOLATE',
        isActive: cyclesOnly,
        presetId: 'cyclesOnly',
        onAction: () => setCyclesOnly((v) => !v),
      } : undefined,
    };
  }, [filteredGraph, scope, cycleCount, cycleNodeIds.size, cyclesOnly]);

  const visibilityPresetsSlot = useMemo<InspectorPresetDefinition[]>(() => [
    ...(cycleCount > 0 ? [{
      id: 'cyclesOnly',
      label: 'Cycles Only',
      description: `Isolate ${cycleCount} circular project dependency component${cycleCount === 1 ? '' : 's'}`,
      icon: '⚠️',
      isActive: cyclesOnly,
      onToggle: () => setCyclesOnly((v) => !v),
      badge: cycleCount,
    }] : []),
    {
      id: 'hideTests',
      label: 'Hide Test Projects',
      description: 'Hide unit, integration & benchmark test suites (Orbit 4)',
      icon: '🧪',
      isActive: hideTests,
      onToggle: () => setHideTests((v) => !v),
    },
    {
      id: 'includeExternalPackages',
      label: 'External Packages',
      description: 'Include third-party NuGet / npm / PyPI / Cargo packages (Orbit 5)',
      icon: '📦',
      isActive: includeExternalPackages,
      onToggle: () => setIncludeExternalPackages((v) => !v),
    },
  ], [cycleCount, cyclesOnly, hideTests, includeExternalPackages]);

  const handleRefresh = useCallback(() => {
    setReloadToken((t) => t + 1);
  }, []);

  return (
    <UniversalGraphMap
      graph={filteredGraph}
      serverHttpUrl={serverHttpUrl}
      loading={loading}
      error={error}
      hudLabel="C2 Project Dependency Topology"
      hudTitle={`Project Dependencies${scope ? `: ${scope}` : ''}`}
      headerControlsSlot={scopeSelector}
      tierDefinitions={PROJECT_DEP_TIERS}
      defaultOrbitConfig={PROJECT_DEP_ORBIT_CONFIG}
      defaultLayoutName="concentric-barycentric"
      storagePrefix="ce_pdeps_v1_"
      inspectorOverviewSlot={inspectorOverviewSlot}
      visibilityPresetsSlot={visibilityPresetsSlot}
      onOpenFile={onOpenFile}
      onRefresh={handleRefresh}
    />
  );
};
