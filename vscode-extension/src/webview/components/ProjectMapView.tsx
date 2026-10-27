import React, { useEffect, useMemo, useState } from 'react';
import { GraphData } from '../../../../proto/types';
import {
  filterProjectMap,
  parseProjectMapGraph,
} from '../projectMap/projectMapModel';
import { useServerTransport } from '../services/webviewTransport';
import { BaseGraphMap } from './BaseGraphMap';

export interface ProjectMapViewProps {
  serverHttpUrl?: string;
  project?: string;
  onProjectChange?: (projectName: string) => void;
  onOpenFile: (filePath: string, lineStart?: number) => void;
}

function resolveBaseUrl(serverHttpUrl?: string): string | undefined {
  if (serverHttpUrl) return serverHttpUrl;
  const wsUrl = typeof window !== 'undefined' ? window.__CE_CONFIG__?.wsUrl : undefined;
  return wsUrl?.replace(/^ws:\/\//, 'http://').replace(/^wss:\/\//, 'https://').replace(/\/ws$/, '');
}

const PROJECT_MAP_LAYOUTS = [
  { id: 'concentric' as const, label: '🎯 Default' },
  { id: 'swimlanes' as const, label: '🏊 Swimlanes' },
  { id: 'mermaid' as const, label: '🧜 Mermaid' },
  { id: 'cose' as const, label: '🌐 Force' },
];

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
  const [hideUtilities, setHideUtilities] = useState(false);
  const [hideResources, setHideResources] = useState(false);
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

  const model = useMemo(() => parseProjectMapGraph(graph), [graph]);
  const visible = useMemo(
    () => filterProjectMap(model, { hideUtilities, hideResources, cyclesOnly }),
    [model, hideUtilities, hideResources, cyclesOnly]
  );

  // Build filtered GraphData for BaseGraphMap
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

  const entryPointNodeCount = model.nodes.filter((n) => n.kind === 'Endpoint' || n.kind === 'EntryPoint').length;
  const moduleCount = model.nodes.filter((n) => !n.isResource && n.kind !== 'Endpoint' && n.kind !== 'EntryPoint').length;
  const resourceCount = model.nodes.filter((n) => n.isResource).length;
  const depCount = model.edges.filter((e) => !e.isResourceLink).length;

  const headerControls = (
    <div style={{ display: 'inline-flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
      <select
        className="domain-layout-dropdown"
        style={{ minWidth: 180, height: 26, fontSize: 11 }}
        value={model.projectName || project || ''}
        onChange={(e) => onProjectChange?.(e.target.value)}
        title="Select project to inspect"
      >
        {model.projects.length === 0 && (
          <option value={project ?? ''}>{project || '(largest project)'}</option>
        )}
        {model.projects.map((p) => (
          <option key={p.id} value={p.name} title={p.path}>
            {p.name} ({p.files} files)
          </option>
        ))}
      </select>

      {entryPointNodeCount > 0 && (
        <span className="pm-chip" title="EntryPoints & Endpoints in Orbit 0">
          {entryPointNodeCount} {entryPointNodeCount === 1 ? 'entrypoint' : 'entrypoints'}
        </span>
      )}
      <span className="pm-chip" title="Classes / modules in this project">
        {moduleCount} {model.granularity === 'class' ? 'classes' : model.granularity === 'file' ? 'files' : 'modules'}
      </span>
      <span className="pm-chip" title="Internal call & dependency links">
        {depCount} deps
      </span>
      {resourceCount > 0 && (
        <span className="pm-chip" title="Transports, buses, libraries, databases, and tables">
          {resourceCount} resources
        </span>
      )}

      {model.cycles.length > 0 && (
        <button
          type="button"
          className={`domain-hud-btn ${cyclesOnly ? 'is-active' : ''}`}
          onClick={() => setCyclesOnly((v) => !v)}
          title="Show only modules involved in dependency cycles"
          style={{
            borderColor: cyclesOnly ? '#f59e0b' : 'rgba(245, 158, 11, 0.45)',
            color: '#fbbf24',
          }}
        >
          ⚠️ {model.cycles.length} cycle{model.cycles.length === 1 ? '' : 's'}
        </button>
      )}
    </div>
  );

  const projectMapPresets = (
    <>
      {model.cycles.length > 0 && (
        <button
          type="button"
          className={`visibility-preset-item ${cyclesOnly ? 'is-active' : ''}`}
          onClick={() => setCyclesOnly((v) => !v)}
          title="Isolate only modules involved in dependency cycles"
        >
          <div className="visibility-preset-main">
            <span className="visibility-preset-icon">⚠️</span>
            <div className="visibility-preset-text">
              <span className="visibility-preset-label">Cycles Only</span>
              <span className="visibility-preset-desc">
                {model.cycles.length} dependency cycle{model.cycles.length === 1 ? '' : 's'} detected
              </span>
            </div>
          </div>
          <div className={`visibility-preset-toggle ${cyclesOnly ? 'is-on' : ''}`}>
            {cyclesOnly ? 'ON' : 'OFF'}
          </div>
        </button>
      )}

      <button
        type="button"
        className={`visibility-preset-item ${hideUtilities ? 'is-active' : ''}`}
        onClick={() => setHideUtilities((v) => !v)}
        title="Hide foundation/utility modules (Orbit 4)"
      >
        <div className="visibility-preset-main">
          <span className="visibility-preset-icon">🧰</span>
          <div className="visibility-preset-text">
            <span className="visibility-preset-label">Hide Utilities</span>
            <span className="visibility-preset-desc">Foundation & helper modules (Orbit 4)</span>
          </div>
        </div>
        <div className={`visibility-preset-toggle ${hideUtilities ? 'is-on' : ''}`}>
          {hideUtilities ? 'ON' : 'OFF'}
        </div>
      </button>

      <button
        type="button"
        className={`visibility-preset-item ${hideResources ? 'is-active' : ''}`}
        onClick={() => setHideResources((v) => !v)}
        title="Hide database tables, procedures, topics, and external resources"
      >
        <div className="visibility-preset-main">
          <span className="visibility-preset-icon">🗄️</span>
          <div className="visibility-preset-text">
            <span className="visibility-preset-label">Hide Resources</span>
            <span className="visibility-preset-desc">Tables, procedures, topics & external APIs</span>
          </div>
        </div>
        <div className={`visibility-preset-toggle ${hideResources ? 'is-on' : ''}`}>
          {hideResources ? 'ON' : 'OFF'}
        </div>
      </button>
    </>
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', width: '100%', height: '100%', position: 'relative' }}>
      {loading && (
        <div className="view-loading-overlay" style={{ zIndex: 1000 }}>
          <div className="view-loading-card">
            <div className="view-loading-spinner" />
            <div className="view-loading-content">
              <span className="view-loading-title">Project Map</span>
              <span className="view-loading-subtitle">Loading project modules and resources…</span>
            </div>
          </div>
        </div>
      )}
      {!loading && (error || model.error) && (
        <div className="pm-overlay pm-error" style={{ zIndex: 900 }}>
          {error || model.error}
        </div>
      )}
      <BaseGraphMap
        graph={filteredGraph}
        serverHttpUrl={serverHttpUrl}
        hudLabel="C3 Internal Structure"
        hudTitle={`Project Map${model.projectName ? `: ${model.projectName}` : ''}`}
        headerSlot={headerControls}
        customPresetsSlot={projectMapPresets}
        projectMapPresets={{
          cyclesCount: model.cycles.length,
          cyclesOnly,
          hideUtilities,
          hideResources,
        }}
        onToggleProjectMapPreset={(preset) => {
          if (preset === 'cyclesOnly') setCyclesOnly((v) => !v);
          else if (preset === 'hideUtilities') setHideUtilities((v) => !v);
          else if (preset === 'hideResources') setHideResources((v) => !v);
        }}
        defaultLayoutName="concentric"
        allowedLayouts={PROJECT_MAP_LAYOUTS}
        defaultNodeSizingMode="sqrt-weight"
        defaultNodeContentMode="metric"
        defaultHiddenTypes={[]}
        storagePrefix="ce_pm_v3_"
        onOpenFile={onOpenFile}
        onRefresh={() => setReloadToken((t) => t + 1)}
      />
    </div>
  );
};
