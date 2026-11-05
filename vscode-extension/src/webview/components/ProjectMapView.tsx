import React, { useEffect, useMemo, useState, useCallback } from 'react';
import { GraphData } from '../../../../proto/types';
import {
  filterProjectMap,
  parseProjectMapGraph,
  ProjectMapModel,
  TIER_COLORS,
} from '../projectMap/projectMapModel';
import { TierDefinition } from '../layout/swimlanesLayout';
import { useServerTransport } from '../services/webviewTransport';
import {
  UniversalGraphMap,
  InspectorPresetDefinition,
  InspectorOverviewModel,
} from './UniversalGraphMap';

export interface ProjectMapViewProps {
  serverHttpUrl?: string;
  project?: string;
  onProjectChange?: (projectName: string) => void;
  onOpenFile: (filePath: string, lineStart?: number) => void;
}

function resolveBaseUrl(serverHttpUrl?: string): string | undefined {
  if (serverHttpUrl) return serverHttpUrl;
  const wsUrl = typeof window !== 'undefined' ? (window as any).__CE_CONFIG__?.wsUrl : undefined;
  return wsUrl?.replace(/^ws:\/\//, 'http://').replace(/^wss:\/\//, 'https://').replace(/\/ws$/, '');
}

export const ProjectMapView: React.FC<ProjectMapViewProps> = ({
  serverHttpUrl,
  project,
  onProjectChange,
  onOpenFile,
}) => {
  const { httpUrl: transportHttpUrl, connectionState } = useServerTransport();
  const [graph, setGraph] = useState<GraphData | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  // Filters state
  const [hideUtilities, setHideUtilities] = useState(false);
  const [hideResources, setHideResources] = useState(false);
  const [cyclesOnly, setCyclesOnly] = useState(false);

  // Fetch project map data
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
      fetch(`${currentBase}/api/view?view=project-map&scope=${encodeURIComponent(project ?? '')}`)
        .then(async (res) => {
          if (!res.ok) throw new Error(`HTTP ${res.status}: ${await res.text()}`);
          return (await res.json()) as GraphData;
        })
        .then((data) => {
          if (cancelled) return;
          setGraph(data);
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
  }, [serverHttpUrl, transportHttpUrl, connectionState === 'connected', project, reloadToken]);

  const model: ProjectMapModel = useMemo(() => parseProjectMapGraph(graph), [graph]);
  const visible = useMemo(
    () => filterProjectMap(model, { hideUtilities, hideResources, cyclesOnly }),
    [model, hideUtilities, hideResources, cyclesOnly]
  );

  // Filtered graph for UniversalGraphMap
  const filteredGraph = useMemo<GraphData | null>(() => {
    if (!graph) return null;
    if (!hideUtilities && !hideResources && !cyclesOnly) return graph;
    const visibleNodeIds = new Set(visible.nodes.map((n) => n.id));
    const visibleEdgeIds = new Set(visible.edges.map((e) => e.id));
    return {
      ...graph,
      nodes: (graph.nodes ?? []).filter((n) => visibleNodeIds.has(n.id)),
      edges: (graph.edges ?? []).filter((e) => {
        const edgeId = e.id ?? `${e.source}->${e.target}`;
        return visibleEdgeIds.has(edgeId);
      }),
    };
  }, [graph, visible, hideUtilities, hideResources, cyclesOnly]);

  const tierDefinitions = useMemo<TierDefinition[]>(() => {
    return model.tiers.map((t) => ({
      tier: t.tier,
      title: t.label,
      color: TIER_COLORS[t.tier] || '#475569',
    }));
  }, [model.tiers]);

  const projectSelector = (
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
      value={model.projectName || project || ''}
      onChange={(e) => onProjectChange?.(e.target.value)}
      title="Select project to inspect"
    >
      {model.projects.length === 0 && (
        <option value={project ?? ''}>{project || '(current workspace)'}</option>
      )}
      {model.projects.map((p) => (
        <option key={p.id} value={p.name} title={p.path}>
          {p.name} ({p.files} files)
        </option>
      ))}
    </select>
  );

  const inspectorOverviewSlot = useMemo<InspectorOverviewModel>(() => {
    const totalFiles = model.nodes.reduce((acc, n) => acc + (n.fileCount || 0), 0);
    const entryPoints = model.nodes.reduce((acc, n) => acc + (n.entryPoints || 0), 0);
    const classes = model.nodes.filter((n) => !n.isResource).length;
    const resources = model.nodes.filter((n) => n.isResource).length;

    return {
      title: model.projectName || 'Project Overview',
      subtitle: `${totalFiles} source files`,
      metrics: [
        { label: 'EntryPoints', value: entryPoints, color: '#38bdf8' },
        { label: model.granularity === 'class' ? 'Classes' : model.granularity === 'file' ? 'Files' : 'Modules', value: classes, color: '#a78bfa' },
        { label: 'Dependencies', value: model.edges.length, color: '#34d399' },
        { label: 'Resources', value: resources, color: '#f472b6' },
      ],
      actionBanner: model.cycles.length > 0 ? {
        text: `⚠️ ${model.cycles.length} dependency cycle${model.cycles.length === 1 ? '' : 's'} detected`,
        actionLabel: cyclesOnly ? 'ISOLATING' : 'ISOLATE',
        isActive: cyclesOnly,
        presetId: 'cyclesOnly',
        onAction: () => setCyclesOnly((v) => !v),
      } : undefined,
    };
  }, [model, cyclesOnly]);

  const visibilityPresetsSlot = useMemo<InspectorPresetDefinition[]>(() => [
    ...(model.cycles.length > 0 ? [{
      id: 'cyclesOnly',
      label: 'Cycles Only',
      description: `${model.cycles.length} dependency cycle${model.cycles.length === 1 ? '' : 's'} detected`,
      icon: '⚠️',
      isActive: cyclesOnly,
      onToggle: () => setCyclesOnly((v) => !v),
      badge: model.cycles.length,
    }] : []),
    {
      id: 'hideUtilities',
      label: 'Hide Utilities',
      description: 'Foundation & helper modules (Orbit 4)',
      icon: '🧰',
      isActive: hideUtilities,
      onToggle: () => setHideUtilities((v) => !v),
    },
    {
      id: 'hideResources',
      label: 'Hide Resources',
      description: 'Tables, procedures, topics & external APIs',
      icon: '🗄️',
      isActive: hideResources,
      onToggle: () => setHideResources((v) => !v),
    },
  ], [model.cycles.length, cyclesOnly, hideUtilities, hideResources]);

  const handleRefresh = useCallback(() => {
    setReloadToken((t) => t + 1);
  }, []);

  return (
    <UniversalGraphMap
      graph={filteredGraph}
      serverHttpUrl={serverHttpUrl}
      loading={loading}
      error={error || model.error || null}
      hudLabel="C3 Internal Structure"
      hudTitle={`Project Map${model.projectName ? `: ${model.projectName}` : ''}`}
      headerControlsSlot={projectSelector}
      tierDefinitions={tierDefinitions}
      storagePrefix="ce_pm_v3_"
      inspectorOverviewSlot={inspectorOverviewSlot}
      visibilityPresetsSlot={visibilityPresetsSlot}
      onOpenFile={onOpenFile}
      onRefresh={handleRefresh}
    />
  );
};
