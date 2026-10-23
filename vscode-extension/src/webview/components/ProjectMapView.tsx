import React, { useEffect, useMemo, useState } from 'react';
import { GraphData } from '../../../../proto/types';
import {
  filterProjectMap,
  parseProjectMapGraph,
} from '../projectMap/projectMapModel';
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

export const ProjectMapView: React.FC<ProjectMapViewProps> = ({
  serverHttpUrl,
  project,
  onProjectChange,
  onOpenFile,
}) => {
  const [graph, setGraph] = useState<GraphData | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const [hideUtilities, setHideUtilities] = useState(false);
  const [hideResources, setHideResources] = useState(false);
  const [cyclesOnly, setCyclesOnly] = useState(false);

  useEffect(() => {
    const baseUrl = resolveBaseUrl(serverHttpUrl);
    if (!baseUrl) {
      setError('Server connection not ready yet.');
      return;
    }
    let cancelled = false;
    setLoading(true);
    setError(null);
    fetch(`${baseUrl}/api/view?view=project-map&scope=${encodeURIComponent(project ?? '')}`)
      .then(async (res) => {
        if (!res.ok) throw new Error(`HTTP ${res.status}: ${await res.text()}`);
        return (await res.json()) as GraphData;
      })
      .then((data) => {
        if (cancelled) return;
        setGraph(data);
      })
      .catch((err: unknown) => {
        if (!cancelled) setError(err instanceof Error ? err.message : String(err));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [serverHttpUrl, project, reloadToken]);

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

  const moduleCount = model.nodes.filter((n) => !n.isResource).length;
  const resourceCount = model.nodes.length - moduleCount;
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

      <span className="pm-chip" title="Code modules / files in this project">
        {moduleCount} {model.granularity === 'file' ? 'files' : 'modules'}
      </span>
      <span className="pm-chip" title="Internal module dependencies">
        {depCount} deps
      </span>
      {resourceCount > 0 && (
        <span className="pm-chip" title="Database tables, procedures, topics, and external resources">
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

      <button
        type="button"
        className={`domain-hud-btn ${!hideUtilities ? 'is-active' : ''}`}
        onClick={() => setHideUtilities((v) => !v)}
        title="Toggle foundation/utility modules (Orbit 4)"
      >
        🧰 Utilities
      </button>

      <button
        type="button"
        className={`domain-hud-btn ${!hideResources ? 'is-active' : ''}`}
        onClick={() => setHideResources((v) => !v)}
        title="Toggle tables, procedures, topics, and external services"
      >
        🗄️ Resources
      </button>
    </div>
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
        hudTitle="Project Map"
        headerSlot={headerControls}
        defaultLayoutName="concentric-barycentric"
        defaultNodeSizingMode="sqrt-weight"
        defaultNodeContentMode="icon"
        defaultHiddenTypes={[]}
        storagePrefix="ce_pm_"
        onOpenFile={onOpenFile}
        onRefresh={() => setReloadToken((t) => t + 1)}
      />
    </div>
  );
};
